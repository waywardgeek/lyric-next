# Lyric grammar review — PEG draft 8

## Status and how to read it

`lyric-grammar.peg` is the active ordered syntax draft for the new Lyric design. PEG parsing, generic-first priority, and concrete D1–D5 recommendations are adopted; their explicitly unresolved details and D6 still require decisions. The language specification remains the authority for approved semantics. The earlier EBNF draft, original review, and check receipts are preserved in Git history at commit `d2cf0c0`; see [reviews and provenance](reviews/README.md). A disagreement is a defect or a decision to resolve, not permission to pick whichever text is convenient.

This is a PEG review dialect over tokens, with source-character lexer subrules and explicit lexical hooks. `<-` defines a rule, `/` is ordered choice, adjacency is sequence, postfix `?` is optional, postfix `*` repeats, `&(...)` is positive lookahead, `!(...)` is negative lookahead, and parentheses group. Quoted terminals match exact tokens in syntax rules and characters in lexical subrules; doubled backslash denotes a single backslash. `@Name` identifies a lexical hook described at the end of the grammar and in L1–L5, not an undocumented semantic predicate. No generator-specific grammar format is claimed. Source literals use their own scanning modes. A failed alternative restores its input position; a successful choice is not revisited because later type checking dislikes the result.

The grammar is based primarily on the preserved full specification and the decisions now in `lyric-language-spec.md`. Initial inspection of old `parser.ly` and `expr_parser.ly` exposed source comments and a precedence parser, but is not the source of authority for this grammar. In particular, the old expression precedence puts bitwise operations below comparisons; this draft uses our approved corrected ordering. No old compiler implementation or grammar generator was copied.

No Lyric parser or PEG engine was implemented or run. The historical `peg-check.txt` at commit `d2cf0c0` records pre-dictionary structural checks, including defined references, nonnullable repetition, and absence of nullable-prefix left-recursion cycles. These do not establish intended language acceptance or semantic safety. Ordered choice makes parsing priority explicit; it does not prove that a poorly ordered alternative cannot hide a desired form.

## Lexical conventions

### L1. Identifiers and words

D1's identifier alphabet is approved: UTF-8 source decoded to Unicode scalars, XID_Start or underscore initially, then XID_Continue or underscore. Require NFC spelling and reject non-normalized identifiers with an NFC suggestion instead of rewriting them. Standalone `_` is discard/wildcard, not an ordinary named binding. Names remain case-sensitive; classify ASCII keywords only after consuming the complete identifier. Malformed UTF-8 is diagnosed. The toolchain must pin and report one Unicode data version; the initial version is Unicode 16.0.0. String and symbol contents are not normalized by this rule.

Adopted hard keywords:

```text
func class struct enum interface relation destructor import impl
as is type where owns refs ref self from true false null pub
let var if else for in while match return break continue yield extends
```

`value`, `final`, `safe`, `field`, `gen`, and `fn` are contextual words. safe before func declares a checked current-item-deletion-safe generator; its result must be gen T and it cannot combine with final. This permits fields and variables named `value`, including `Ok(value: T)`, while recognizing `value class`. Recognize `final` before `func`, `field` in an interface field declaration, `gen` as a type prefix, and `fn` before a callable type's `(`. A contextual spelling is not an ordinary name in that particular syntactic position. Builtin type names (`unit`, `i32`, `Status`, `Result`, etc.) are resolved type names, not lexical magic. The no-shadowing rule applies to visible builtin names as well as other visible declarations; it is a name-resolution check, not a change to token spelling.

The original parser's broad acceptance of selected hard keywords as identifiers is not silently retained. The listed hard/contextual policy is adopted; old hard-keyword exceptions are rejected. Future concurrency words are not given operational syntax by this draft.

### L2. Literals and trivia

Decimal digits may have single underscores between digits; leading/trailing/consecutive separators are rejected by this draft. This deliberately tightens the old implementation's permissive stripping behavior. Decimal-point floats require digits on both sides of the dot. Numeric suffixes are adopted: integers accept u8/u16/u32/u64/i8/i16/i32/i64, and floats accept f32/f64. A floating suffix also makes a digits-only spelling such as `123f32` a float. Suffixes are contiguous lowercase ASCII parts of one literal token, recognized without parser feedback. Reject identifier-continuation characters after a complete numeric spelling, including unknown or extended suffixes; do not split `123u16name` or `123abc` into valid-prefix tokens. A separator before a suffix (`123_u16`) or an integer suffix on a decimal-point float (`1.5u16`) is malformed. Signs remain separate tokens; range checking, including signed minima, is semantic. Exponent and hexadecimal/octal/binary numeric syntax remain deferred. Ordinary-string Unicode escapes are now explicitly specified below.

Normal strings and byte literals use the approved byte escapes. Ordinary strings and f-string text additionally accept `\u{HEX}` with one to six hexadecimal digits denoting a Unicode scalar, excluding surrogates and values above U+10FFFF. Byte literals do not accept this Unicode escape. A byte literal must encode one byte after escape processing. Normal strings may contain UTF-8 text but no raw newline. Comments consume text through but not including the newline; they do not erase statement boundaries. Unknown punctuation is a lexical diagnostic rather than an invented identifier token.

For triple strings, D2 specifies raw content through the first closing triple quote, with no backslash processing; trim leading/trailing ASCII whitespace U+0009–U+000D and U+0020 after source validation; do not strip internal indentation. This is the adopted source contract, not a claim about the old lexer. Backtick symbols contain raw content without escapes or boundary trimming; empty symbols are valid and denote the canonical interned empty byte string. Triple f-strings are not included.

F-strings use a lexer mode for text and a normal expression parser inside each interpolation. Nested string literals, record braces, and nested f-strings do not terminate interpolation prematurely. `{{` and `}}` escape braces in text mode. A lone `}` in text mode is an error. Interpolation is an expression delimiter; a nested block inside it still has statement newlines under L3.

### Source-wide character exclusions

Reject literal Default_Ignorable_Code_Point and Bidi_Control characters in every source context, including comments, raw triple strings and backticks. Reject Cc controls except HT/LF/CR under existing whitespace/newline rules. This includes rejecting a leading BOM. Identifier XID/NFC validation does not override these exclusions; escaped identifiers are not supported. Combining marks and naturally right-to-left letters remain permitted under the identifier rules. Use the same pinned Unicode version for every property check.

Ordinary escaped strings may represent prohibited literal characters as data. Unicode escapes encode the selected scalar as UTF-8; they are consumed as one escape within f-string text, not interpolation. Escaped characters are never retokenized. Raw triple/backtick forms do not gain escape processing; use ordinary strings and sym(string) for special symbol data. Diagnostics escape the rejected character in excerpts and report its code point instead of executing its visual effect.

### L3. Newlines and separators

The baseline says newlines terminate statements, parentheses/brackets suppress them, and braces retain them. To make nested closures/blocks work inside calls, the adopted D5 rule makes the nearest unmatched delimiter determine newline handling; no particular tracking algorithm is prescribed. A `(`, `[`, or interpolation-expression opening suppresses ordinary newlines; a nested block/record `{` makes them significant again until its close. String contents use their own lexer mode.

For example, a multiline call is one expression, but a closure body passed in that call can contain multiple newline-separated statements. A comment followed by a newline follows the same rule. CRLF is one logical newline; lone CR is rejected.

Logical declaration clauses stay on one line unless inside parentheses/brackets. Angle brackets do not introduce implicit newline continuation. Semicolons explicitly separate statements. A last declaration/statement may be followed directly by `}` or EOF; newline or semicolon separates successive items. This draft does not require a separator between nullary enum variants, preserving `enum Color { Red Green Blue }`. Else may follow a closing brace on the same line or after newlines; a semicolon before else ends the preceding statement and is rejected.

### L4. Operators and angle closes

Use longest applicable punctuation tokens: notably `!+=`, `!-=`, `!*=` before their shorter forms; `<->` before `<`; and ordinary two-character comparisons/arrows/shifts. `!-` is one token interpreted in prefix or infix position. `!as` is recognized only as the contiguous wrapping-cast operator followed by a word boundary. It is not the prefix of `!assert`. No new `()` token exists.

Within type argument lists, a `>>` token supplies two closing `>` tokens; inside expression shifts it remains `>>`. The parser needs a token view that preserves source offsets and restores any split token during failed speculative parsing. This is ordinary contextual token handling, not inference from identifier capitalization. Whether `>=` may split as a generic close plus assignment is not assumed: write a separator before `=` in that case in this draft.

### Tuple field token boundary

Tuple fields use a dot followed by ASCII decimal digits: `.0`, `.1`, and so on. After a member-access dot, consume the decimal field token without combining a following dot into a float: `nested.0.1` is two member suffixes. Ordinary expression-position `1.25` remains a floating-point literal. No identifier/type lookup decides this boundary. TupleIndex has no signs, separators, suffixes, or base prefixes; leading zeros retain decimal meaning. A non-tuple receiver or an index beyond its static arity is a semantic error. Named members still use Ident; an underscore-prefixed name no longer denotes a tuple field.

### L5. Context-sensitive validation is not lexical grammar

A name may denote a type, value, variant, package, or compile-time relation scope. Parsing constructs a name/reference form; resolution determines the declaration. Do not branch on whether a name starts with a capital letter. Integer literal range, enum payload compatibility, reference provenance, and destruction effects are not lexer responsibilities.

## Concrete syntax decisions exposed by the draft

The concrete recommendations in D1–D5 are adopted. Explicitly open details remain open; D6 has not yet been adopted. Grammar presence alone does not approve additional semantics.

### D1. Identifier and keyword policy

The Unicode XID alphabet, NFC requirement, and exact case-sensitive spelling in L1 are settled. The listed hard/contextual split is adopted and old hard-keyword exceptions are rejected. Name shadowing is prohibited; the initial pinned Unicode data version is 16.0.0. Cross-script confusables remain distinct; ambiguous name diagnostics expose code points rather than perform fuzzy lookup.

### D2. Literal edge grammar

Adopt L2's underscore placement, raw triple-string content/boundary trimming, raw backticks without escapes, and ordinary-string newline restriction. Empty symbols are defined by the Sym contract; Unicode string escapes and source-wide exclusions remain in force.

### D3. Generic calls and required block braces

Bill selected generic-first PEG priority. The draft encodes `GenericSuffix <- BindingArgs &("(" / ".")` before other postfix alternatives. The postfix layer is attempted before the comparison layer, so a complete generic angle-list shape followed by a call/member continuation is selected without consulting names. An unsuccessful syntactic generic attempt restores input so comparison parsing can proceed. Successful generic syntax is not retried as comparisons because later name/type resolution fails. Thus `a<b>(c)` is a generic call; `(a < b) > c` requests comparisons. The precise accepted continuation set is visible in this rule rather than hidden in parser code.

Labels in these argument lists are admitted syntactically for interface-qualified calls. Ordinary function type arguments cannot acquire endpoint labels merely because the parser can represent them; the checker rejects invalid placements. Repeated generic suffixes and generic instantiation of arbitrary runtime values are likewise not authorized by broad postfix syntax.

`if`, `while`, `for ... in`, `match`, and arm guards use a duplicated header-expression ladder excluding direct record and dictionary literals. Parentheses/call arguments restore ordinary expressions. This prevents `if ready { ... }` from consuming the body as `ready{...}`. A record in that position must be parenthesized, e.g. `match (Point { x: 1, y: 2 }) { ... }`. The restriction is grammar-visible rather than a hidden parser flag.

### D4. Cast-target boundaries

`as`/`!as` require a type, but an unparenthesized union type can consume the expression's bitwise `|`. Cast targets use a prefix/optional type; group a union type with parentheses. Then `x as u32 | y` means `(x as u32) | y`, and `x as (A | B)` denotes the grouped type syntactically. Type checking may reject that cast because arbitrary union/class casts are not approved numeric conversions.

A following `?` is consumed by a cast's type when valid in the type grammar: `x as T?` names optional T. To apply result propagation to a cast expression, write `(x as T)?`; it will still need to produce a Result. Similarly a dotted cast type is read as a qualified type name: write `(x as T).member` for access after conversion. These boundary rules should be explicit, not decided by symbol lookup or spacing.

Function return types and nested generic/array types contain their own full type expressions; the grouping rule does not forbid unions inside those delimited types. `(T)` groups a type; tuples require at least two elements. `()` is the unit value, while `unit` is its type.

### D5. Separators and block results

Use L3's newline processing. Blocks use statement lists; a final expression can supply an if/match expression's branch value when the block is in a value-required position. A trailing newline does not discard that value, but a trailing explicit semicolon does. A declaration/assignment/return is not secretly an expression value. Ordinary function bodies still use explicit return, apart from the approved unit/fallthrough cases; this draft does not add implicit value-return from all functions.

The bodyless function declaration grammar shares a prefix with a definition. A body begins on the same logical declaration line, after its signature/where clause. Newline then `{` is not silently attached to the declaration. This is the adopted logical-line rule; an opening function brace cannot move to a separate logical line.

This draft uses commas between where requirements, and permits trailing commas in parameter/argument/field/tuple lists, but not angle parameter/argument lists. These separator choices are adopted.

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
- New **Arrays/references**: index/slice forms, prefix ref, and ordinary assignment. Stable-storage validity through last use, fixed-window resize permission, and overlap behavior are semantic contracts; borrow-checking algorithms remain separate. The syntax is unchanged by replacing root/index re-resolution with stable borrows.

## Explicit remaining coverage choices

The draft is substantial but is not yet the full supported-language grammar:

- Lock-region/guard syntax, spawn/channels/join, FFI and opaque effect contracts remain undecided. Do not parse-and-ignore them.
- Exact generic external-method syntax needs an example when receiver and method have independently scoped type parameters. Current FunctionName plus method TypeParams covers existing examples but is not a full scoping contract.
- Optional-parameter/default-argument and variadic grammar must be resolved for library APIs such as assertions and formatting. Intrinsic overloads are possible; they are not silently general variadics.
- Dictionary literals are retained: `{ key: value, ... }`, with empty literals requiring an expected Dict type. DictExpr is an ordinary expression; bare statement-position braces remain blocks. Direct header dictionaries are excluded under adopted D3; parenthesized literals are allowed. Entry separators follow MemberSep under adopted D5. Read-only-borrow spelling remains open and is not yet represented in the grammar.
- Original positional record construction, pipe closures, optional lyric wrappers, bare import paths, implements, and bare destructor shorthand remain explicit compatibility decisions. Their omission is not an unnoticed removal or permanent prohibition.
- Struct methods remain absent from this draft's StructBody, matching the original behaviorless-struct baseline; decide whether that restriction should remain. Primitive extension/static methods also need a clear declaration/resolution contract.
- Generic type aliases, single-element tuples, type-level null alternatives, multiple interface inheritance, and chained comparison meaning are not inferred from neighboring features. Ordinary comparison syntax folds left; there is no Python-style chain semantics.
- Hint shape, role/cardinality consistency, extends cycles/conflicts, visibility, initialization, nonescape, lifetimes, and Result handling require semantic checks. A grammar production is not proof of these properties.

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

### Added dictionary expectations

- G36: ``let names = {`alice`: 1, `bob`: 2}`` — dictionary expression, inferred Dict<Sym,i32>.
- G37: `let empty: Dict<Sym,string> = {}` — empty dictionary with expected type.
- G38: `let unknown = {}` — syntax accepted, semantic rejection without expected key/value types.
- G39: `match ({1: "one"}) { _ => {} }` — dictionary allowed inside parenthesized header expression; matching semantics still checked.
- G40: `let bad = {1: "one", "two": "two"}` — syntax accepted, incompatible inferred key types rejected.

These remain authored expectations, not passing parser tests. Expected-type enum variant selection is semantic resolution and needs no additional constructor production.

### Added Unicode identifier expectations

- G41: `let café = 1` using precomposed U+00E9 — accepted identifier spelling.
- G42: `let 温度 = 20` and `let Δtime = 0.1` — accepted XID identifiers.
- G43: a name spelled with U+0065 followed by U+0301 instead of U+00E9 — rejected when the full identifier is not NFC; suggest its NFC spelling.
- G44: `let 1name = 0` — rejected identifier start; numeric literal digits remain ASCII.
- G45: `let if温度 = 1` — one identifier, not the keyword if followed by another token. Latin `a` and Cyrillic `а` remain distinct, with code-point diagnostics when needed.

These are expectations, not tests run against a lexer or the host's Unicode tables. XID hooks describe scalar membership; whole-identifier NFC validation and reserved-word classification are additional lexical requirements.

### Added visible-source and escape expectations

- G46: a literal U+200B in an identifier, string, or comment — lexical rejection, not silent removal.
- G47: a literal U+202E in a comment or raw triple string — lexical rejection with a visible code-point diagnostic.
- G48: `let text = "\u{200B}\u{202E}"` — accepted ordinary string escapes, yielding those characters as data.
- G49: `"\u{D800}"`, `"\u{110000}"`, and `"\u{}"` — rejected invalid scalar or malformed escape; `f"\u{202E}"` is string text, not interpolation.
- G50: a literal U+FEFF at source start — rejected; ordinary HT/LF/CR remain subject to the existing whitespace/newline rules.

These are authored expectations, not lexer-run results. Tests use visible code-point notation here rather than inserting prohibited characters into this document.

### Added tuple-field expectations

- G51: `let n = (42, "hello").0` — tuple selection, result i32.
- G52: `let text = (42, "hello").1` — tuple selection, result string.
- G53: `let n = ((1, 2), "label").0.1` — chained selectors, not a float token.
- G54: `(42, "hello").2` — syntax accepted, out-of-range tuple field rejected semantically.
- G55: `pair._0` and `pair[0]` when pair is a tuple — syntactically representable member/index forms but rejected as tuple access; `1.25` remains a float literal.

These are authored expectations, not parser results. Destructuring and the normal field mutation/lifetime rules remain unchanged.

### Added safe-generator expectations

- G56: `safe func walk(owner: Graph) -> gen Node` — generator declaration with current-item destruction permission.
- G57: `pub safe func P.iter_removable(self) -> gen C` — interface requirement; matching implementation must satisfy the safe contract.
- G58: `safe func work() -> unit {}` — parses as a function declaration, rejected semantically because it is not a generator.
- G59: `safe final func work() {}` — rejected modifier combination.
- G60: `for node in graph.nodes.iter_removable() { node.destroy() }` — explicit safe traversal; accepted only when the selected declaration is safe and effects prove current-only destruction. The same consumer through ordinary iter() is rejected.

These are expectations, not executed parser or lifetime-checker tests. Names do not confer permission; the safe declaration does.

## Verification and next review

The initial EBNF integrity receipt is retained in Git history, not as a current validation artifact. The historical PEG-conversion checks covered nullable-prefix recursion, nonnullable repetition, and defined references at that revision; those results do not certify every subsequent grammar edit. Ordered alternatives were reviewed for shadowing hazards: function types precede named types, closures/records precede their shorter prefixes, floats precede integers, triple strings precede ordinary strings, and type-class where requirements precede bare capability names. This is not a Lyric parser and does not validate G01–G60 as language inputs. Do not report examples as passing tests until a parser actually consumes them.

D1–D5 recommendations are adopted. The initial Unicode version is 16.0.0; LF and CRLF are accepted and lone CR is rejected. Empty symbols are valid. Name shadowing is prohibited across declarations visible through unqualified lookup. D6 remains the next numbered syntax review. PEG execution/generator selection and memoization are implementation decisions still open. Once syntax choices are approved, expand missing supported forms and test a parser against these cases. Do not enshrine unrelated draft proposals merely because they make implementation easy.

### Numeric-suffix conformance examples

These are specification expectations, not executed lexer tests.

- `123u16`, `1_000i64`: each is one explicitly typed integer token.
- `1.5f32`, `123f64`: each is one explicitly typed floating-point token.
- `-128i8`: minus operator followed by an i8-suffixed magnitude; semantic checking handles the signed minimum.
- `123u16name`, `123u128`, `123abc`, `123_u16`, `1.5u16`: lexical errors, not valid-prefix token splitting.
- `123 u16`: separate integer and identifier tokens; syntactic validity depends on context.
- `pair.0.1`: tuple selectors remain unsuffixed decimal indices, not floating-point or suffixed literals.
