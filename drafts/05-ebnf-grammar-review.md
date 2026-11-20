# Lyric grammar review — draft 1

## Status and how to read it

`lyric-grammar.ebnf` is the first consolidated syntax draft for the new Lyric design. It is intended to become normative after review, not a claim that every proposed syntax choice is already approved. The current language specification remains the authority for approved semantics. A disagreement is a defect or an explicit proposal to resolve, not permission to pick whichever text is convenient.

This is EBNF over tokens, with a lexical layer and explicit disambiguation conventions. It is not a PEG and its alternatives are not implicitly ordered choices. Commas concatenate grammar symbols; `|` separates alternatives; `[]` is optional; `{}` repeats; `()` groups. Quoted terminals use either quote style; inside a terminal, doubled backslash denotes a single backslash. `? description ?` denotes an external character class or token boundary. Source strings/escapes are described by lexical productions, not by the EBNF file's own quoting convention.

The grammar is based primarily on the preserved full specification and the decisions now in `lyric-language-spec.md`. Initial inspection of old `parser.ly` and `expr_parser.ly` exposed source comments and a precedence parser, but is not the source of authority for this grammar. In particular, the old expression precedence puts bitwise operations below comparisons; this draft uses our approved corrected ordering. No old compiler implementation or grammar generator was copied.

No Lyric parser was implemented or run. Mechanical checks of this EBNF are described in the verification receipt; they establish production integrity, not language acceptance, absence of ambiguity, or semantic safety.

## Lexical conventions

### L1. Identifiers and words

Draft choice D1 uses ASCII letters/underscore followed by letters/underscore/digits. This is a proposed initial alphabet; the earlier prose only settled case sensitivity. Non-ASCII text remains supported in strings as UTF-8 source text. Standalone `_` is the discard/wildcard token, not an ordinary named binding.

Proposed hard words for this draft:

```text
func class struct enum interface relation destructor import impl
as is type where owns refs ref self from true false null pub
let var if else for in while match return break continue yield extends
```

`value`, `final`, `field`, `gen`, and `fn` are contextual words. This permits fields and variables named `value`, including `Ok(value: T)`, while recognizing `value class`. Recognize `final` before `func`, `field` in an interface field declaration, `gen` as a type prefix, and `fn` before a callable type's `(`. A contextual spelling is not an ordinary name in that particular syntactic position. Builtin type names (`unit`, `i32`, `Status`, `Result`, etc.) are resolved type names, not lexical magic. Exact shadowing policy for builtin names is semantic work.

The original parser's broad acceptance of selected hard keywords as identifiers is not silently retained. We need to approve one keyword policy rather than copy a workaround. Future concurrency words are not given operational syntax by this draft.

### L2. Literals and trivia

Decimal digits may have single underscores between digits; leading/trailing/consecutive separators are rejected by this draft. That tightens the old implementation's permissive stripping behavior and needs D2 review. Floats require digits on both sides of the decimal point. No exponent, suffix, hexadecimal/octal/binary, or Unicode escape syntax is added silently.

Normal strings and byte literals use the approved escapes. A byte literal must encode one byte after escape processing. Normal strings may contain UTF-8 text but no raw newline. Comments consume text through but not including the newline; they do not erase statement boundaries. Unknown punctuation is a lexical diagnostic rather than an invented identifier token.

For triple strings, D2 proposes raw content through the first closing triple quote, with no backslash processing; trim boundary ASCII whitespace after capture. The original spec establishes trimming but not a fully precise escape/whitespace contract. This is a proposal, not a claim about the old lexer. Backtick symbols likewise propose raw content without escapes; empty symbols are syntactically allowed pending library semantics. Triple f-strings are not included.

F-strings use a lexer mode for text and a normal expression parser inside each interpolation. Nested string literals, record braces, and nested f-strings do not terminate interpolation prematurely. `{{` and `}}` escape braces in text mode. A lone `}` in text mode is an error. Interpolation is an expression delimiter; a nested block inside it still has statement newlines under L3.

### L3. Newlines and separators

The baseline says newlines terminate statements, parentheses/brackets suppress them, and braces retain them. To make nested closures/blocks work inside calls, D5 proposes a delimiter stack: the nearest unmatched delimiter determines newline handling. A `(`, `[`, or interpolation-expression opening suppresses ordinary newlines; a nested block/record `{` makes them significant again until its close. String contents use their own lexer mode.

For example, a multiline call is one expression, but a closure body passed in that call can contain multiple newline-separated statements. A comment followed by a newline follows the same rule. CRLF is one logical newline; whether lone CR is accepted is a lexical portability choice to finalize.

Logical declaration clauses stay on one line unless inside parentheses/brackets. Angle brackets do not introduce implicit newline continuation. Semicolons explicitly separate statements. A last declaration/statement may be followed directly by `}` or EOF; newline or semicolon separates successive items. This draft does not require a separator between nullary enum variants, preserving `enum Color { Red Green Blue }`. Else may follow a closing brace on the same line or after newlines; a semicolon before else ends the preceding statement and is rejected.

### L4. Operators and angle closes

Use longest applicable punctuation tokens: notably `!+=`, `!-=`, `!*=` before their shorter forms; `<->` before `<`; and ordinary two-character comparisons/arrows/shifts. `!-` is one token interpreted in prefix or infix position. `!as` is recognized only as the contiguous wrapping-cast operator followed by a word boundary. It is not the prefix of `!assert`. No new `()` token exists.

Within type argument lists, a `>>` token supplies two closing `>` tokens; inside expression shifts it remains `>>`. The parser needs a token view that preserves source offsets and restores any split token during failed speculative parsing. This is ordinary contextual token handling, not inference from identifier capitalization. Whether `>=` may split as a generic close plus assignment is not assumed: write a separator before `=` in that case in this draft.

### L5. Context-sensitive validation is not lexical grammar

A name may denote a type, value, variant, package, or compile-time relation scope. Parsing constructs a name/reference form; resolution determines the declaration. Do not branch on whether a name starts with a capital letter. Integer literal range, enum payload compatibility, reference provenance, and destruction effects are not lexer responsibilities.

## Concrete syntax decisions exposed by the draft

These are Cadence's draft resolutions for review, not new settled decisions merely because an EBNF file exists.

### D1. Identifier and keyword policy

Use the alphabet and hard/contextual split in L1. This avoids reserving ordinary names such as `value`, while keeping declaration/control keywords predictable. Unicode identifiers and the old soft-hard-keyword exceptions are not included without a decision.

### D2. Literal edge grammar

Approve or revise underscore placement, raw triple-string content and exact boundary trimming, backtick escapes, and raw newline handling. Existing escapes and basic literal categories are retained; these edge contracts were previously incomplete.

### D3. Generic calls and required block braces

The unqualified CFG permits two parses of `a < b > (c)`: comparisons versus a generic application/call. Proposed rule: in expression position, recognize a complete angle argument list as generic syntax only if the closing angle is followed by `(` or `.`. If that complete shape exists, select the generic parse without consulting names; resolution failure is an error, not a retry as comparisons. Thus `a<b>(c)` is a generic call, and `(a < b) > c` explicitly requests comparison. This is a stated syntactic disambiguation rule on the EBNF, not a claim that raw alternatives alone eliminate ambiguity.

Labels in these argument lists are admitted syntactically for interface-qualified calls. Ordinary function type arguments cannot acquire endpoint labels merely because the parser can represent them; the checker rejects invalid placements. Repeated generic suffixes and generic instantiation of arbitrary runtime values are likewise not authorized by broad postfix syntax.

`if`, `while`, `for ... in`, `match`, and arm guards use a duplicated header-expression ladder excluding direct record literals. Parentheses/call arguments restore ordinary expressions. This prevents `if ready { ... }` from consuming the body as `ready{...}`. A record in that position must be parenthesized, e.g. `match (Point { x: 1, y: 2 }) { ... }`. The restriction is grammar-visible rather than a hidden parser flag.

### D4. Cast-target boundaries

`as`/`!as` require a type, but an unparenthesized union type can consume the expression's bitwise `|`. Proposed rule: cast targets use a prefix/optional type; group a union type with parentheses. Then `x as u32 | y` means `(x as u32) | y`, and `x as (A | B)` denotes the grouped type syntactically. Type checking may reject that cast because arbitrary union/class casts are not approved numeric conversions.

A following `?` is consumed by a cast's type when valid in the type grammar: `x as T?` names optional T. To apply result propagation to a cast expression, write `(x as T)?`; it will still need to produce a Result. Similarly a dotted cast type is read as a qualified type name: write `(x as T).member` for access after conversion. These boundary rules should be explicit, not decided by symbol lookup or spacing.

Function return types and nested generic/array types contain their own full type expressions; the grouping rule does not forbid unions inside those delimited types. `(T)` groups a type; tuples require at least two elements. `()` is the unit value, while `unit` is its type.

### D5. Separators and block results

Use L3's newline processing. Blocks use statement lists; a final expression can supply an if/match expression's branch value when the block is in a value-required position. Proposed rule: a trailing newline does not discard that value, but a trailing explicit semicolon does. A declaration/assignment/return is not secretly an expression value. Ordinary function bodies still use explicit return, apart from the approved unit/fallthrough cases; this draft does not add implicit value-return from all functions.

The bodyless function declaration grammar shares a prefix with a definition. A body begins on the same logical declaration line, after its signature/where clause. Newline then `{` is not silently attached to the declaration. This is consistent with keeping declaration clauses on one logical line but needs review for desired formatting.

This draft uses commas between where requirements, and permits trailing commas in parameter/argument/field/tuple lists, but not angle parameter/argument lists. Those punctuation details are proposals to make the draft concrete. The original spec has examples but not one complete list rule.

### D6. Prefix ref and pattern scope

Place `ref` at unary precedence. Thus `ref a[i]` takes the reference to the indexed storage, and `ref a + b` parses as `(ref a) + b`; checking rejects any unsupported use. This is consistent with the examples, but its table entry was previously missing.

Patterns include literals, tuples, nested variants, names, and `_`; arm alternatives use `|`. Resolving a bare name distinguishes a variable binding, nullary variant, or named union alternative, without capitalization. Exact precedence when a binding name collides with a variant/type remains a name-resolution decision. Only simple bindings may omit initialization; let-else needs an initializer and a diverging else. The broad grammar permits some forms that semantic validation rejects.

Explicit borrowed enum/union payload patterns remain unrepresented until their syntax is chosen. Ordinary patterns do not silently borrow mutable storage. This is a documented gap, not an invented `ref` pattern extension.

## Coverage against the source documents

- Original full spec **Modules and Packages**, **Functions**, **Visibility**, **Compilation**: file/import declarations, qualified methods, and bodies. New logical packages replace directory-implies-package behavior. Package manifests are outside the source grammar.
- Original **Lexical Structure**, **Primitive/Composite Types**, **Operator Precedence**, **F-Strings**: lexical terminals, types, literal forms, expression hierarchy. Approved precedence promotion and wrapping/cast operators replace old behavior.
- Original **Structs**, **Enums**, **Classes**, **Variables and Constants**, **Control Flow**: data declarations, named record literals, bindings, control flow, and patterns. New value class / let-var-ref / unit / Result rules replace old lifetime/mut/error conventions.
- Original **Interfaces and Multi-Class Contracts**, **Relations**, and redesign §§3.8–3.9 / later Wave 2: labeled impl arguments, fields, adapters, explicit ownership cleanup templates, abstract relations, and whole-relation mappings. New labeled constraints and qualified calls are included.
- New **Functions, closures, generators**: parenthesized nonescaping closure syntax and yield in ordinary statements. Exactly one syntactic yield in a generator, permitted consumer control flow, and nonescape are semantic restrictions, not special parser tokens.
- New **Arrays/references**: index/slice forms, prefix ref, and ordinary assignment. Path validity, fixed-window resize permission, and overlap behavior are semantic/lowering contracts.

## Explicit remaining coverage choices

The draft is substantial but is not yet the full supported-language grammar:

- Lock-region/guard syntax, spawn/channels/join, FFI and opaque effect contracts remain undecided. Do not parse-and-ignore them.
- Exact generic external-method syntax needs an example when receiver and method have independently scoped type parameters. Current FunctionName plus method TypeParams covers existing examples but is not a full scoping contract.
- Optional-parameter/default-argument and variadic grammar must be resolved for library APIs such as assertions and formatting. Intrinsic overloads are possible; they are not silently general variadics.
- Dictionary literals from the original spec need restoration or explicit rejection. Braces already represent blocks and named records; add dictionary syntax deliberately rather than guess from one parser hack.
- Original positional record construction, pipe closures, optional lyric wrappers, bare import paths, implements, and bare destructor shorthand remain explicit compatibility decisions. Their omission is not an unnoticed removal or permanent prohibition.
- Struct methods remain absent from this draft's StructBody, matching the original behaviorless-struct baseline; decide whether that restriction should remain. Primitive extension/static methods also need a clear declaration/resolution contract.
- Generic type aliases, single-element tuples, type-level null alternatives, multiple interface inheritance, and chained comparison meaning are not inferred from neighboring features. Ordinary comparison syntax folds left; there is no Python-style chain semantics.
- Hint shape, role/cardinality consistency, extends cycles/conflicts, visibility, initialization, nonescape, lifetimes, and Result handling require semantic checks. An EBNF production is not proof of these properties.

## Review examples and expected classification

These are authored expectations, NOT parser-run results. Fragments are identified as such; name/type resolution requires suitable declarations.

| Case | Fragment | Expected classification |
|---|---|---|
| G01 | `let x = ()` | Syntax accepted: unit expression |
| G02 | `work()` / `work(())` | Syntax accepted: zero arguments / one unit argument |
| G03 | `let x = !-a !+ b !* c` | Syntax accepted: prefix negation, multiplication before addition |
| G04 | `a & 1 == 0` | Syntax accepted: `(a & 1) == 0` |
| G05 | `value !as u8` | Syntax accepted: wrapping cast |
| G06 | `ref matrix[0][1]` | Syntax accepted: ref of indexed storage |
| G07 | `items[:high]` / `items[low:]` / `items[:]` | Syntax accepted: slices |
| G08 | `items[]` | Syntax rejected: neither index nor colon |
| G09 | `let slot = ref count` | Syntax accepted; storage permissions checked later |
| G10 | `var slot = ref count` | Syntax accepted, semantic rejection: references are fixed let aliases |
| G11 | `identity<i32>(42)` | Generic call under D3 |
| G12 | `a < b > (c)` | Generic call under D3, not comparison fallback |
| G13 | `(a < b) > c` | Comparison syntax; operand types checked later |
| G14 | `Collection<Node:outgoing, Edge:source>.count(node)` | Qualified binding/call syntax accepted |
| G15 | `Outer<Inner<T>>` | Type fragment accepted with contextual angle splitting |
| G16 | `x as u32 | y` | Cast then bitwise OR under D4 |
| G17 | `x as (A | B)` | Type grouping syntax accepted; cast may be semantically rejected |
| G18 | `func f(x: ref u32) { x += 1 }` | Function and write-through syntax accepted |
| G19 | `func f(mut x: u32) { }` | Syntax rejected: old mut parameter form |
| G20 | `relation Single Server:connection owns Connection:server` | Syntax accepted; hint shape checked later |
| G21 | `relation Single Server:connection owns [Connection:server]` | Syntax accepted, cardinality mismatch rejected semantically |
| G22 | `let x = if ready { 1 } else { 2 }` | Syntax accepted; branch types checked later |
| G23 | `if Point { x: 1 } { work() }` | Rejected as a direct header record; use parentheses under D3 |
| G24 | `let value = null` | Syntax accepted; rejected if no expected nullable type |
| G25 | `let _ = save_file(path, data)` | Syntax accepted: explicit discard; postconditions remain checked |
| G26 | `save_file(path, data)` | Syntax accepted; rejected as statement when a Result is implicitly discarded |
| G27 | `1.` / `1__0` / `1_` | Not float / malformed number under D2; no silent suffix stripping |
| G28 | `enum Color { Red Green Blue }` | Preserved adjacent variant syntax |
| G29 | `return` followed by newline then `x` | Bare return then separate statement, not return of x |
| G30 | `return (x +` newline `y)` | Single return expression under L3 |
| G31 | `P.children <-> Team.members` | Impl field-binding fragment accepted |
| G32 | `N:outgoing/E:source = Route:a/Via:a_source` | Whole-relation-binding fragment accepted |
| G33 | `P.text = (self) -> string { return self.name }` | Impl adapter syntax accepted; signature inferred/checked |
| G34 | `fn(i32) -> Result<unit>` | Function-type fragment accepted |
| G35 | `Point { x: 1, y: 2 }` | Named record syntax accepted; constructor eligibility checked later |

## Verification and next review

The first verification step is EBNF bookkeeping: production syntax, unique definitions, all references defined, delimiter balance, and repetition operands not nullable. This is not a Lyric parser and cannot validate G01–G35 as language inputs. Do not report the examples as passing tests until a parser actually consumes them.

Recommended first human decision is **D3: generic-angle disambiguation**. It affects both readability and parser behavior and cannot be settled by a precedence table alone. Then settle D4 cast targets and D5 separators. The remaining lexical choices can be reviewed as a group. Once approved, expand the missing supported forms and build the parser against these cases; do not enshrine every draft proposal merely because it makes implementation easy.
