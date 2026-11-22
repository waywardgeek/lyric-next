# Chapter 3 — Parse the Language

## TL;DR

Turn Chapter 2's tokens into a syntax tree for the complete current grammar. Parse hello world, arithmetic, declarations, types, interfaces, relations, patterns, and control flow. Preserve the written structure; do not resolve names, check types or lifetimes, or execute the program yet.

The implementation is a hand-written recursive-descent parser guided by the canonical [PEG grammar](../lyric-grammar.peg). Use precedence climbing for binary expressions and restorable cursor checkpoints for ordered alternatives. Peggy supplies a separate recognizer for comparison, not the implementation under test.

The tree is shared compiler vocabulary declared in Common. Its nodes are identity-bearing objects with complete backlinks, not value-like records. The source owns every node, including unfinished nodes; tree edges describe structure without creating another destruction cascade.

Read the [tree-shape contract](../graders/ch03/tree-shapes.md) and [grader contract](../graders/ch03/README.md) alongside this chapter. They specify the public output and child meanings. They are readable requirements, not an existing solution.

## 1. What parsing adds

The lexer recognizes `func`, a name, parentheses, braces, and a string. Parsing establishes their relationships:

```lyric
func main() {
    print("Hello, world!")
}
```

The significant tree is:

```text
file
  function
    name: main
    parameters
    block
      expression-statement
        call
          name: print
          string: "Hello, world!"
```

The call's first child is its callee; subsequent children are arguments. `print` is an ordinary name. This chapter neither supplies its library definition nor prints the greeting by executing Lyric. Likewise, a very large `u8` literal is syntactically an integer even when later type checking must reject its magnitude.

We initially considered a calculator-sized parser. A full syntax boundary is more useful here: later chapters can give already-represented constructs meaning, rather than repeatedly inventing the front end. Full syntax does not mean full compiler functionality, and parsing a proposed spelling does not settle unresolved semantic policy. Consult the specification's open decisions rather than treating an accepted tree as language-design approval.

## 2. Design the shared tree before the parser

### Authoritative storage and relationships

Retain `Compilation` and `SourceFile` from the earlier chapters. The source remains the authoritative home of its bytes. Add a source-owned collection of syntax nodes and a closed `SyntaxKind` enumeration.

Each `SyntaxNode` stores:

- Its kind.
- A source backlink.
- An optional spelling anchor: byte offset and length in that source.
- A syntactic-parent backlink.
- An ordered child collection.
- Its logical destruction state.

A structural node has no spelling anchor. An operator, name, or literal node anchors the exact original spelling. This is not a copied spelling string or a token index used as a hidden reference. Tokens are temporary parsing inputs; the parser converts the chosen token's position to a source span when constructing the node. Literal anchors retain escapes and suffixes. They are not the lexer's decoded literal values.

There are two distinct relationships. The source **strongly owns** every node and the node points back. Parent/child syntax edges are **weak structural relationships**, maintained at both ends. Here weak means that removing or destroying a tree parent does not destroy its child nodes; the source still owns them. It does not mean `WeakReference<T>`.

Why use a source-owned collection instead of recursively owned trees? A parser constructs unfinished subtrees and abandons speculative alternatives. One collection gives every node an owner immediately, even before it has a tree parent. It also makes cleanup iterative. Do not replace the source backlink with a service bag or a global lookup.

### Relationship operations

Declare the enum, persistent fields, constructors, traversal operations, and relationship-changing operations in Common. Put their matching partial implementations in a syntax/model module. Parser and later semantic modules depend on this shared vocabulary, not one another's private helpers.

Construction registers a node with its live source through the source's add operation. Adding a child checks both nodes are alive, belong to the same source, and that the child has no parent. Reject self-attachment and cycles by walking the prospective parent's ancestor chain. Validate before mutation; then update the child collection and backlink together. Child-edge removal checks the actual relationship and clears both ends. Source membership has a stricter lifetime contract: register during construction and remove only during destruction. A live node cannot be detached from its source or transferred to another source, even if it has no tree parent. Its byte anchors belong to its original source; moving membership would silently change their meaning.

Expose read-only child count and indexed traversal, not a publicly mutable list. Give the source a read-only root accessor. Read the root and serialize the tree only after parsing succeeds, never during construction. The root is the first node constructed for a successful parse; an empty source still has a `file` root.

Destroying a node removes its parent edge and detaches its child edges, then marks the node destroyed and removes its source membership through the complete relationship operation. Source removal validates that destruction has begun and both tree directions are detached before changing membership. Source insertion refuses destroyed nodes. Every refused operation leaves the graph unchanged; repeated destruction is harmless. Destroying the source clears its nodes before its tokens and bytes. Clearing nodes in reverse construction order makes the ordinary removal path efficient and avoids recursive destruction of deep trees. Re-lexing also clears any previous syntax first.

Temporary local references do not need model backlinks. A future semantic object that **retains** a node reference does introduce a persistent relationship and must maintain its reverse end. The two existing backlinks do not exempt later modules from this rule.

### Closed kinds, explicit shapes

One node class with a closed enum is sufficient for this increment. It avoids a class hierarchy for every production, but it makes the shape contract essential. A `binary` node has an operator anchor and exactly two ordered children. A `slice` has a base and distinct lower/upper wrappers, each possibly empty. A function preserves its name, modifiers, type parameters, parameters, result, constraints, body, and visibility where present.

The [complete inventory](../graders/ch03/tree-shapes.md) documents all 108 kinds. Use it as the construction contract, not merely as output naming. Preserve both endpoint roles in relations and relation bindings. Do not reduce a binding to two type names. A declaration without a body differs from an empty body; wrappers preserve such distinctions.

## 3. Build a cursor, not another lexer

Run the real lexer first. On lexical failure return its diagnostic unchanged. Otherwise keep the parser cursor local to the parse operation: a token index, a flag for a pending second closing angle, and a recursive-call budget.

Provide small operations to inspect token kind/spelling, consume a complete token, require a spelling, require an identifier, and skip separators. Never advance past the end token. The token's byte offset, line, and scalar column provide syntax-error locations.

The one special cursor state is contextual `>>`. The lexer correctly emits a shift token. When closing a type argument list, consuming the first `>` leaves the cursor at that token with the second-half flag set. Consuming the second close clears the flag and advances. Ordinary operations must not consume that half as a whole token. Do not split `>=`. An error at the pending second half is located one byte and one scalar column after the original token start.

Thus `Box<Box<i32>>` works without changing lexer output, and `a >> b` remains a shift. The PEG recognizer uses immutable token views for the same purpose. Neither implementation destructively rewrites the token list.

## 4. Translate ordered choices deliberately

A PEG's alternatives are ordered. They are not a suggestion to accept whichever interpretation later passes type checking. Follow the canonical grammar and its [review notes](../grammar-review.md).

For an alternative that requires speculation, save three things: cursor index, second-half flag, and source node count. Try the alternative. If it fails with an ordinary syntax failure, destroy nodes added after the saved count and restore both cursor components. If it succeeds, keep the constructed subtree. Do not catch resource exhaustion as a reason to try another grammar alternative.

Apply this rule to optional and repeated sequences too, not just alternatives named “generic.” In a named type, optional type arguments must roll back as a whole: `x as T < y` is a comparison whose left operand is a cast, not a malformed required type application. Conversely, `x as T<U>` keeps the completed generic type. For repeated `"." Ident` segments, consume neither token unless both match. Thus `x as T.0` leaves `.0` for tuple-member parsing, while `x as pkg.T` retains the qualified type. The same distinction applies to the variant name in `x is T.0` versus `x is pkg.T`. A two-token lookahead suffices for this short repeated unit; nested optional type arguments use the full checkpoint.

Speculative helpers must not perform lasting edits to nodes that predate their checkpoint. In particular, parse a proposed generic suffix and its required lookahead before attaching the existing base expression to a new generic node. Destruction can then undo the abandoned attempt locally; no token copies or general transaction framework are needed.

The generic suffix is tried before comparison parsing and must be followed by `(` or `.`. Consequently `a < b > (c)` is generic syntax, while `a < b >> c` falls back to comparison and shift syntax after the attempted suffix fails. Restore the half-token state as well as the index. A later unknown type name does not reopen that syntactic choice.

Other ordered boundaries include function types before named types, closures before grouped expressions, records before a bare name, and blocks before expression statements where the grammar says so. Contextual `gen` can remain a name when a generator-type prefix cannot complete. Do not turn every contextual word into a hard keyword.

## 5. Construct expressions with precedence climbing

Start an expression with prefix parsing. A prefix operator recursively parses another prefix operand. Otherwise parse a primary and extend it through the postfix loop. This makes `-f()` mean `-(f())`, not `(-f)()`. For binary operators, use the precedence table from the PEG. While the next operator has precedence at least the requested minimum, consume it and parse its right operand with minimum `precedence + 1`. Join the previous left subtree and new right subtree beneath a binary node.

The increased threshold makes equal-precedence operators associate left:

```text
9 - 3 - 1       means       (9 - 3) - 1
1 + 2 * 3      means       1 + (2 * 3)
```

Prefix parsing recurses for the operand, giving right-nested unary operators. Parentheses construct an explicit group node. Postfix parsing repeatedly extends a base with calls, generic applications, member access, indexing, slices, casts, variant tests, or postfix operators according to their grammar positions. Cast target boundaries come from the type grammar, not from name lookup.

Primary parsing handles literals, unit, tuples, arrays, dictionaries, records, closures, interpolation, `if`, and `match`. F-string text and interpolation expressions remain ordered separate children. Empty function arguments differ from a single unit argument. Named wrappers distinguish an absent slice bound from a missing child.

A token kind is not always the complete grammar classification. The lexer gives ordinary and triple-quoted raw strings the same string token kind. Expressions accept both, but import paths and literal patterns require the grammar's ordinary `StringLiteral`. Check the token's original source spelling in those narrower contexts; do not broaden them merely because the token kinds match.

Header expressions require care: a bare record or dictionary can consume the brace intended to begin the body. Pass the grammar's header restriction through expression parsing. Parentheses and call arguments restore full expression parsing where the grammar allows it. Do not globally ban records just to simplify an `if` condition.

## 6. Add types, patterns, statements, and declarations

Construct types independently of whether a type name exists. Support the canonical prefix, optional, array, group/tuple, union, function, qualified-name, and generic-application forms. Keep type arguments separate from parameter declarations. Role bindings are admitted only in the grammar's binding contexts; they preserve the written role.

Patterns have their own shapes: wildcard, literals, negative integer patterns, tuples, qualified names, and optional constructor arguments. An empty constructor-argument wrapper is different from no wrapper. Match arms preserve alternative patterns, an optional guard, and the body.

For blocks, parse a statement, then its separator run. Newlines alone do not discard a value. A semicolon anywhere in that run does: retain a `discard-result` marker anchored at its first semicolon, even when a newline came first. Leading separators do not manufacture a statement. Do not implement block parsing as an indiscriminate separator skip that loses this fact.

Statements include local bindings and their permitted cleanup `else`, assignments, expressions, returns, yields, blocks, loops, breaks, and continues. A return followed by a newline has no operand. Global bindings are a different production and must not inherit local-only cleanup syntax. The parser represents `break` without deciding whether a surrounding loop exists; that is later semantic work.

At file level dispatch imports, aliases, globals, functions, structs, classes, enums, interfaces, relations, and implementations according to the PEG. Preserve declaration-only functions, visibility and other modifiers, constraints, fields, variants, adapters, abstract relations, concrete relations, and both sides of field/method/relation bindings. Require separators where the grammar does. Require end of input after the selected root; accepting only a valid prefix is not a successful parse.

## 7. Fail cleanly and print deep trees safely

Use located `LYC201` syntax diagnostics in Chapter 1's format. A failed parse clears the unfinished node collection and emits no tree. Do not write success output as each subtree becomes available.

Bound active recursive parsing calls. The reference uses a budget of 128 guarded calls and reports `LYC203: Parser nesting limit exceeded.` Resource exhaustion is not evidence that the language forbids the input. Guard recursive type, expression, prefix, pattern, conditional, and block paths; a guard on parentheses alone leaves other overflow paths open.

A long left-associated expression creates a deep tree without equally deep parser recursion. Serialize with an explicit stack of node/child-position frames, writing the opening object, children in order, then closing it. Do not use recursive serialization or destruction after making the parser iterative enough to accept that tree. Configure the JSON writer’s independent nesting limit to accommodate these accepted trees too; in C#, set `JsonWriterOptions.MaxDepth` explicitly rather than relying on its default. The 4,000-term boundary check verifies the complete serialized tree, not just successful parsing. Parser recursion guards, iterative traversal, and serializer configuration address separate limits; none promises unlimited inputs.

## 8. Inspect and verify real components

Add two driver modes:

- `--inspect-syntax FILE`: parse a complete file and emit a `file` tree.
- `--inspect-expression FILE`: parse one complete expression and emit an `expression` root.

Success is exit zero, empty stderr, and one JSON tree. Every node has `kind`, `text`, and ordered `children`. Text is exact anchored source spelling or null. Errors have nonzero status, empty stdout, and the prescribed diagnostic; crashes and timeouts are not expected rejections. Preserve all earlier inspection modes.

Testing has three independent concerns. Authored accepted/rejected fixtures check recognition. Exact expected trees check associations and retained information. Direct lifecycle checks inspect ownership/refusal/cleanup; output tests alone cannot prove those relationships. Peggy recognizes the same source cases through the existing lexer, so it is independent syntax evidence, not an independent lexer or a semantic checker.

During development, 66 exact trees, 143 recognition cases, and seven boundary cases passed in both configurations alongside all retained earlier checks. A disposable right-associative mutant failed the associativity test. A mutant dropping semicolon markers failed two tests. Supplementary direct model probes passed 22 lifecycle checks and 11 source-membership checks. Code review exposed optional/repeated-sequence rollback errors and a source-transfer hole; the construction rules above and regression cases incorporate their corrections. These are bounded observations, not a proof of every production combination.

One fixture taught a useful lesson: `f<A><B>()` was incorrectly expected to pass, but the first generic suffix lacks its required following `(` or `.`. The corrected suite rejects that spelling and accepts `f<A>()<B>()`. Resolve disagreements against the grammar, not by voting between parsers.

## Exercises and completion

Starting with your accepted Chapter 2 implementation:

1. Declare and implement the shared syntax model, complete relationship operations, root/traversal access, and iterative cleanup. Document the identity/value distinction and the weak tree edges.
2. Implement the full current PEG syntax using the algorithms above and the exact public shape inventory. Preserve source anchors, contextual closing angles, ordered alternatives, separators, and all role information.
3. Add both inspection modes and the diagnostic/resource behavior. Review failure paths as carefully as successful trees.
4. Run all retained graders in Debug and Release. No Chapter 3 tests are chapter-only. The [grader contract](../graders/ch03/README.md) supplies entry points, comparison rules, timeouts, and stable test identifiers. You may read fixtures, but not change their expectations or inspect an existing Chapter 3 solution.

With the repository layout and .NET 10/C# 14, Python 3 prerequisites from earlier chapters, run from the repository root:

```sh
make test
```

Use `make test DOTNET=/absolute/path/to/dotnet` if necessary. A different implementation may supply its own command after `--` to each public grader; run every retained Chapter 1–3 grader in both configurations. Do not use the reference executable to stand in for your implementation.

For the separate required syntax comparison, Node 22 or newer and the pinned Peggy development dependency are needed. If not already installed, `make grammar-setup` downloads that dependency locally with install scripts disabled; it requires network access but no credentials. Ordinary checking does not install anything:

```sh
make release
DOTNET=dotnet node tools/peg/test.mjs graders/ch03/cases.json
```

Substitute your SDK path for `dotnet` when required. Passing means all authored expectations pass, not merely that the parsers agree. Resource-limit cases intentionally are not PEG language-rejection fixtures.

Completion requires those checks, architecture/lifecycle review, and the practical walkthrough below. Human acceptance remains distinct from automated results. Do not begin type checking or code generation to make this syntax chapter appear more complete.

## Take it for a Spin

From the repository root:

```sh
make spin03
```

This builds Release and uses temporary `.ly` files to show hello world's tree, arithmetic precedence, and a located rejection of an unfinished expression. It does not execute Lyric or require Peggy, network access, credentials, or paid services. Temporary files are removed automatically. Set `DOTNET` or `PYTHON` on the Make command line if needed.

Look for `main`, the ordinary `print` call, and its string argument in the first tree. In the arithmetic tree, multiplication must be beneath addition. The final example must report an error, not print a partial tree. Edit your own source file and inspect it directly with `dotnet compiler/bin/Release/net10.0/Lyric.dll --inspect-syntax your-file.ly` when you want to explore further.
