# Lyric Language Specification

**Draft 4 — reviewed reconstruction, with approved safety and library upgrades.**

This document specifies the current Lyric design. It consolidates the main rules and former appendices into one reading order; subsequent approved upgrades are integrated with their affected contracts. The [canonical specification](lyric-language-spec.md) is the current editing target. [Reviews and provenance](reviews/README.md) describe the retained comparisons and how to retrieve the historical reconstruction record from Git.

## Contents

- [1. Purpose, status, and design principles](#1-purpose-status-and-design-principles)
- [2. Source organization and declaration identity](#2-source-organization-and-declaration-identity)
- [3. Lexical structure, expressions, and parsing](#3-lexical-structure-expressions-and-parsing)
- [4. Types and data declarations](#4-types-and-data-declarations)
- [5. Bindings, arrays, and storage references](#5-bindings-arrays-and-storage-references)
- [6. Functions, generics, closures, and generators](#6-functions-generics-closures-and-generators)
- [7. Control flow and patterns](#7-control-flow-and-patterns)
- [8. Results, Status, and checked failures](#8-results-status-and-checked-failures)
- [9. Maintained relationships](#9-maintained-relationships)
- [10. Construction and mutation](#10-construction-and-mutation)
- [11. Lifetime policies and generated destruction](#11-lifetime-policies-and-generated-destruction)
- [12. Static safety and relationship iteration](#12-static-safety-and-relationship-iteration)
- [13. Interfaces, capabilities, and implementation bindings](#13-interfaces-capabilities-and-implementation-bindings)
- [14. Numeric operations and conversions](#14-numeric-operations-and-conversions)
- [15. Concurrency, class access, and storage](#15-concurrency-class-access-and-storage)
- [16. Standard-library contracts](#16-standard-library-contracts)
- [17. Compiler and toolchain](#17-compiler-and-toolchain)
- [18. Conformance and evidence](#18-conformance-and-evidence)
- [19. Unresolved decisions](#19-unresolved-decisions)

## 1. Purpose, status, and design principles

Lyric is a typed systems language built around explicit relationships, generated destruction, and readable programs. It is intended to be straightforward for AI collaborators to write and for humans to review. Its compiler should enforce mechanical invariants rather than ask programmers to recover them by convention. No garbage collector is introduced.

This is a design specification, not a description of a completed compiler or a proof of memory safety. Unless marked **Proposal**, **Open**, or **Deferred**, its rules describe the current intended language. Examples state contracts, not successful parser/compiler tests. Historical implementation behavior is evidence to examine, not permission to accept an unsafe construct.

The [PEG draft](lyric-grammar.peg) and [grammar review](grammar-review.md) accompany this document. PEG parsing, generic-first priority, and the concrete D1–D5 recommendations are adopted. Their explicitly unresolved details and D6 remain open. The grammar is a review dialect, not generator-ready input. Section 19 indexes unresolved contracts; their detailed boundaries remain beside the affected rules.

### Keep it simple

KISS is a requirement. Prefer an ordinary function, class, or relation over a new category or special case. A feature earns its place by solving a demonstrated problem. Fewer lines of syntax are not a simplification if they introduce more hidden rules.

The compiler must not accept unsupported features and emit placeholders. It should explain errors in terms of source declarations, not require the programmer to decipher generated C.

### Make relationships explicit

Shared declarations define types, stored state, relationships, and externally callable operations. Functional units implement those same declarations. They do not introduce wrappers, service bags, or duplicate mutable state merely to connect the program.

Constructors receive their owning parents and establish both ends of their relationships before returning. Relationship mutations maintain both ends. Refusal leaves the graph unchanged. Children navigate to services through their actual parents.

An object can have several owning parents. One authoritative home for a fact does not mean one parent per object.

### Make lifetime behavior predictable

- No garbage collector.
- Structural destruction is generated from declarations.
- A class may define a `final` method called before that object's structural destruction.
- Static analysis invalidates local references after calls that may destroy objects of their class.
- Only explicitly safe generators permit consumer destruction of the current yielded object; peer destruction remains forbidden. Ordinary generators do not grant this permission.
- Borrowed storage remains allocated, at a stable address, and valid for its referenced type until the borrow is no longer needed; potentially invalidating operations are rejected across a still-needed borrow.
- Indexing is bounds checked by default.

A `value class` uses reference counting. An ordinary `class` uses explicit/cascading destruction. The declaration states the lifetime policy; the compiler does not discover or change it by searching for owning relations. Value-like holders do not require backlinks solely for reclamation; this is not an excuse to omit relationships between components.

### Distinguish design from evidence

Self-hosting is useful evidence of capability, not proof of correctness or memory safety. A safe compiler must reject invalid programs as reliably as it runs valid ones. The compiler's own object model should demonstrate the discipline it implements.

### Provenance and deliberate exclusions

The original reference, original full specification, and multi-class interface redesign are preserved unchanged in `background/`. Earlier reconciliation drafts, frozen sources, and verification receipts are preserved in Git history at commit `d2cf0c0`. Their historical alternatives do not compete with the current rules.

The current design deliberately uses ordinary constructors instead of attach clauses, explicit `value class` instead of inferred lifetime policy or a root modifier, visible interface requirements instead of privileged hidden Collection contracts, and `ref` instead of a second view type. It does not impose an immutable-only restriction on counted values. There is no new declaration-file extension, manual application structural destructor, finalization-view type, or broad effect-annotation language.

The original implementation's admitted unchecked casts, stale references after destruction, and placeholder-supported constructs are not compatibility requirements. Nor are its performance claims established properties of this design. Parser, runtime, safety, and performance claims require their own evidence (Section 18).

## 2. Source organization and declaration identity

Lyric source uses `.ly` exclusively. There is no separate declaration-only source extension or extension-specific language mode. The former `.lyric` design-artifact functionality belongs to the separate Lyre tool, not the Lyric compiler. Ordinary declarations and separately supplied bodies remain supported in `.ly` files under the matching rules below; no `implement` keyword is introduced.

A package is an explicitly configured group of source files sharing one namespace. It may span several directories; a directory does not automatically create a package or access boundary. Declaration and file order do not affect meaning. A module is a project rooted at `lyric.mod`; its configuration identifies package membership and dependencies. The exact configuration-file grammar remains toolchain work, but membership must be explicit and deterministic rather than inferred from implementation discovery.

Small programs may keep declarations and bodies in one file. Larger programs use shared declarations and functional implementations of those declarations:

```text
common/model.ly
parser/parser.ly
checker/checker.ly
application/main.ly
```

These directories may belong to one logical package. Common owns the shared declarations; functional files supply bodies for those declarations using ordinary qualified names, such as `func Node.parse(...)`. The body and declaration identify one callable on one type, not an extension object, duplicate storage, or forwarding wrapper. Package-private declarations are accessible across the package's files regardless of their directories.

Implementation matching uses the resolved package, declaring type/interface where applicable, callable name, and generic parameter/signature contract. Parameter and return types, reference modes, generic arity and constraints, instance/static status, and the safe-generator modifier must agree after resolving names and corresponding type parameters. A differently spelled but equivalent alias does not create a different type. Signature mismatch is a diagnostic, not a new overload silently invented to satisfy the declaration. Every required callable has exactly one body or explicitly declared external implementation; missing and duplicate implementations are errors. A body included directly with its declaration counts as that implementation. Interface abstract requirements and defaults follow their own satisfaction rules, rather than requiring a body for an abstract slot in the declaring package.

The architectural requirement remains that Common supplies shared vocabulary, state, relationships, and callable declarations; functional units consume those declarations rather than each other's implementation internals; application code composes the program. Package privacy alone does not enforce this internal dependency discipline. Any compiler/tooling check of Common-versus-functional-unit dependencies needs its own explicit contract; directory organization is not evidence that such a check exists.

Names are private to their package unless marked `pub`. Import access is qualified:

```lyric
import ast
import parse from "parser"
```

The compiler resolves imports recursively and diagnoses unresolved or ambiguous declarations. Dependency policy and `lyric.mod` contents are toolchain design, not reasons to add restrictions to object graphs.

A bodyless concrete declaration is not an empty function. Imported packages expose only public declarations; an implementation in a different package cannot claim package-private access merely by naming the original type.

## 3. Lexical structure, expressions, and parsing

This section defines the lexical and expression baseline. Detailed value, numeric, reference, and cleanup semantics follow in their respective sections.

### Names, statements, and declaration spelling

Identifiers are case-sensitive. Capitalization does not determine visibility, type variables, or pattern meaning. A name must resolve to an actual declaration; unknown names are not inferred type variables.

Newlines terminate statements except inside parentheses and square brackets. Wrap a continued expression in parentheses; an operator at the end of a line does not introduce implicit continuation. Declaration clauses remain on the same logical line. Braces delimit blocks; semicolons may explicitly separate statements on one line. There is no backslash line continuation. The nearest unmatched delimiter controls newline handling: parentheses, square brackets, and interpolation-expression openings suppress ordinary newlines; nested block/record/dictionary braces restore significant newlines until they close.

`func` declares functions; `fn(T) -> U` spells callable types. Parameters and return values are typed; omission of a return type means `unit`. Local binding types may be inferred. Generic variables must be declared explicitly as in `func identity<T>(value: T) -> T`; they are never inferred from capitalization or a misspelled type name. `let`, `var`, and `ref` have the storage semantics in Section 5. `mut` and `let mut` are not alternate spellings.

### Tokens and literals

Source text is UTF-8; malformed UTF-8 is a source diagnostic, not silently replaced or interpreted using the host locale. An identifier begins with a Unicode XID_Start character or underscore and continues with Unicode XID_Continue characters or underscore. Standalone `_` is discard/wildcard, not an ordinary binding name. Identifier spelling must already be NFC-normalized. Reject a non-NFC spelling with a diagnostic suggesting the NFC form; do not silently rewrite it.

Names remain case-sensitive and match exactly after this validity check. There is no case folding, compatibility normalization, transliteration, or fuzzy matching. Keywords retain their exact ASCII spellings and are recognized as complete words, not prefixes of longer Unicode identifiers. The hard/contextual keyword split below is adopted; the no-shadowing rule in Section 5 applies to visible builtin names too.

```lyric
let café = "coffee"
let 温度 = 20
let Δtime = 0.1
```

Visually similar characters from different scripts remain distinct. When a name diagnostic would otherwise be ambiguous, it must expose the relevant Unicode code points as well as the spelling and source location. A mixed-script or confusable-name warning may assist users but does not merge identifiers or impose an unapproved script restriction.

XID membership and NFC validation must use one explicitly pinned Unicode data version for the language/toolchain release, including dependencies in a compilation. The compiler must report that version; host-library upgrades cannot silently change the accepted identifier set. Selecting the initial version remains a release-contract decision. This specifies requirements, not a normalization or character-classification algorithm.

These identifier rules do not normalize string contents, backtick-symbol names, or runtime sym(string) input. Symbols retain their literal/runtime text semantics. Numeric literal digits remain ASCII under the existing literal grammar; Unicode identifier continuations do not add Unicode numeric-literal spellings.

### Visible source characters

Literal source characters with Unicode Default_Ignorable_Code_Point or Bidi_Control properties are forbidden everywhere, including identifiers, comments, ordinary/interpolated strings, raw triple strings, and backtick-symbol literals. Literal control characters (General_Category Cc) are also forbidden except horizontal tab U+0009, line feed U+000A, and carriage return U+000D used under the language's whitespace/newline rules. Ordinary space U+0020 remains permitted. These exceptions do not allow tabs or line endings inside identifiers or relax a literal's existing newline restrictions. A literal byte-order mark U+FEFF is rejected, including at the start of a source file; it is default-ignorable, not an invisible source prefix.

Identifier characters must pass these exclusions in addition to XID membership and NFC validation. Identifiers cannot contain escape sequences to bypass the exclusions. The checks use the same pinned Unicode version as identifier classification. Validate source text even where a comment or raw-literal scanner would otherwise skip its contents; do not silently delete prohibited characters.

Combining marks are not rejected merely because they combine with another character; they remain subject to XID/NFC and the prohibited-property rules. Arabic and Hebrew letters remain allowed. Their natural directionality is distinct from explicit bidi control characters; this rule does not guarantee left-to-right display or eliminate visible cross-script confusables.

A diagnostic identifies the source location and renders the prohibited character as a code point or visible escape, with its name/property where available. Diagnostic excerpts must escape prohibited controls rather than reproduce their display effects. Source validation is not a promise that arbitrary runtime text is safe to print unescaped.

String data may contain these characters when constructed through explicit escapes. This restriction is on literal source text, not the set of byte-string values or external input. Raw triple strings and backtick literals retain their adopted raw-content rules and cannot hide literal forbidden characters; use an ordinary escaped string, concatenation, or sym(escaped_string) when such data is needed.

The literal forms are:

| Kind | Form | Meaning |
|---|---|---|
| Integer | `42`, `1_000` | Decimal integer; default `i32` when context does not select another representable type |
| Float | `1.5` | Default `f64`; `1.` is not a float literal |
| Byte character | `'a'`, `'\n'`, `'\xFF'` | A `u8` byte |
| String | `"hello"` | Byte string, normally UTF-8 text |
| Triple string | `"""..."""` | Preserves internal newlines and trims boundary whitespace, retaining the original baseline |
| Interpolated string | `f"value={value}"` | Evaluate expressions and stringify; `{{` and `}}` escape braces |
| Symbol | `` `name` `` | Equivalent to `sym("name")` |
| Boolean | `true`, `false` | Boolean values |
| Absence | `null` | Optional absence; needs a nullable target type |

Supported byte escapes are `\n`, `\r`, `\t`, `\\`, `\'`, `\"`, `\0`, and `\xHH`.

Ordinary strings and f-string text additionally accept `\u{HEX}`, where HEX is one to six ASCII hexadecimal digits, with no sign, separators, or whitespace. The value must be a Unicode scalar in U+0000 through U+10FFFF, excluding surrogates U+D800 through U+DFFF. Append its UTF-8 encoding to the string value. Empty, oversized, malformed, surrogate, or out-of-range escapes are diagnostics. Uppercase and lowercase hexadecimal digits are accepted.

Unicode escapes do not occur in identifiers, comments, byte-character literals, or the raw triple/backtick literal forms. Byte-character literals retain the existing byte escapes and u8 contract. Within f-string text, recognize the Unicode escape as a whole: its braces are not interpolation delimiters. Decoded characters are data, not recursively scanned source; an escaped quote, brace, backslash, or bidi control cannot change the surrounding source grammar. Existing byte escapes may still construct arbitrary byte values and do not imply that every resulting byte string is valid UTF-8.

```lyric
let zero_width_space = "\u{200B}"
let right_to_left_override = "\u{202E}"
let special_symbol = sym("name\u{200B}")
// A literal U+200B or U+202E here would be rejected, even in a comment.
```

Numeric base prefixes, numeric type suffixes, and exponent notation remain outside the initial grammar. Adding Unicode string escapes does not add those numeric-literal forms.

F-string interpolation accepts expressions, not a second restricted expression language. Nested braces belonging to an expression must be parsed correctly. Evaluation follows ordinary left-to-right expression order. Invalid escapes and unterminated literals are diagnostics, not malformed runtime strings.

Line comments begin with `//`; block comments are outside the initial language. Comments do not erase the statement-terminating newline. Nested delimiter/newline behavior follows D5 below.

### Precedence

Integer bitwise operators bind more tightly than comparisons: `a & 1 == 0` means `(a & 1) == 0`. Lowest precedence is first.

| Level | Operators |
|---|---|
| 1 | `\|\|` |
| 2 | `&&` |
| 3 | `==`, `!=` |
| 4 | `<`, `<=`, `>`, `>=` |
| 5 | bitwise `\|` |
| 6 | `^` |
| 7 | `&` |
| 8 | `<<`, `>>` |
| 9 | `+`, `-`, `!+`, `!-` |
| 10 | `*`, `/`, `%`, `!*` |
| 11 | unary `-`, `!-`, `!`; `~` when explicitly implemented |
| 12 | member access, calls, indexing, postfix `!`, postfix `?`, `is`, `as`, `!as` |

Binary arithmetic is left-associative. Logical operators short-circuit. Parentheses override precedence. Assignment is a statement, not a value-producing expression. The exact grammar of postfix casts/tests must disambiguate type expressions from neighboring operators; a precedence table is not a substitute for that grammar.

The compound assignments are `+=`, `-=`, `*=`, `/=`, plus explicit wrapping `!+=`, `!-=`, and `!*=`. Additional bitwise and remainder compound assignments are specified extensions, not silently accepted placeholders. Every compound assignment evaluates its destination once and preserves lifetime checks across evaluation of its right side. Wrapping forms follow Section 14's modular arithmetic rules.

### Indexing, slicing, and tuple access

```lyric
items[index]
items[low:high]
items[low:]
items[:high]
items[:]
pair.0
let (left, right) = pair
```

Slice endpoints are half-open: low is included, high excluded. Missing low means zero; missing high means current length. Invalid indices or ranges fail a checked bounds operation. An empty range is valid. Strings support the same byte-range forms.

These forms define selection. Ordinary array slices produce independent array values; `ref array[low:high]` borrows a fixed-length window, as specified in Section 5. Borrowed windows require stable backing storage throughout their usable lifetime and bounds-check accesses within their fixed length. String is [u8]; writable byte/range borrows require mutable storage and obey the same array value and stable-borrow rules.

### PEG parsing contract

Lyric uses a normative PEG grammar specifying its syntax and ordered parsing choices. Ordered alternatives are attempted in written order; the first syntactically successful alternative wins. Generic syntax is attempted before comparison syntax. Later name-resolution or type-checking failure does not cause the parser to reinterpret that successful syntax choice.

PEG supplies an inspectable recursive-descent roadmap. The implementation may execute, generate, or directly follow that grammar; a particular parser generator and a packrat/memoization strategy are not selected yet. A manually written parser must preserve the PEG's ordered choices, lookahead, and backtracking behavior rather than independently redefine the syntax. LLM assistance reduces maintenance cost but does not replace the grammar contract or conformance tests.

The grammar specifies lexical boundaries, declaration and expression forms, precedence/associativity, and newline handling. It must distinguish prefix/infix wrapping operators, !as from unwrap-plus-cast syntax, generic angle brackets from comparisons, unit expressions from calls, and labeled interface bindings. Prose specifies name resolution, type compatibility, lifetime/effect checks, and other semantic constraints that syntax grammar alone does not establish.

The parser implements the grammar rather than silently defining additional language rules. Maintain accepted and rejected conformance examples tied to grammar rules, including ambiguous-looking boundaries and useful source diagnostics. Update the grammar, explanatory prose, and corresponding tests together when syntax changes. The existence of handwritten parser code is not a substitute for the normative grammar, and a grammar document alone is not proof of parser conformance.

The active consolidated syntax draft is [lyric-grammar.peg](lyric-grammar.peg), with [grammar-review.md](grammar-review.md) documenting lexical conventions, remaining proposed syntax choices, coverage gaps, and example expectations. Generic-first ordering and the concrete D1–D5 rules are approved; explicitly unresolved details and D6 are not approved merely by appearing in the grammar. Historical grammar receipts, the superseded EBNF draft, and its review are preserved in Git history at commit `d2cf0c0`. Their structural checks do not establish parser conformance, and receipts apply only to their recorded revisions. No Lyric parser or PEG generator has been run.

### Adopted syntax rules and remaining decisions

The concrete recommendations in D1–D5 below are adopted. Explicitly open details remain decisions, and D6 remains a proposal. The detailed rules and G01–G60 example expectations are in [grammar-review.md](grammar-review.md); grammar productions alone do not approve additional semantics.

- **D1 — identifiers and keywords.** UTF-8/XID identifiers, required NFC, case sensitivity, ASCII keyword spellings, and standalone `_` are adopted. Hard words are `func class struct enum interface relation destructor import impl as is type where owns refs ref self from true false null pub let var if else for in while match return break continue yield extends`. They cannot be ordinary identifiers. `value`, `final`, `safe`, `field`, `gen`, and `fn` are contextual in their documented syntactic positions and ordinary names elsewhere. The old exceptions permitting hard keywords as identifiers are not retained. The initial Unicode data version remains open. Name shadowing is prohibited under Section 5.
- **D2 — literal edges.** Numeric underscores occur singly between digits, never leading, trailing, or consecutive. Ordinary strings exclude raw newlines. Triple strings contain raw text through the first closing triple quote, with no escape processing; trim only leading/trailing ASCII whitespace (U+0009–U+000D and U+0020), not internal indentation. Source-wide character validation precedes trimming, so prohibited controls cannot be hidden in trimmed text. Backtick symbols contain raw text without escapes or boundary trimming; empty symbols denote the canonical interned empty byte string. Unknown punctuation is a lexical diagnostic. Triple f-strings are not included. Ordinary-string Unicode escapes and source-character exclusions remain as specified above.
- **D3 — generic priority and header/body braces.** Generic-first parsing is adopted. Direct record/dictionary literals are excluded from unparenthesized `if`, `while`, `for ... in`, `match`, and guard headers. Parentheses and call arguments restore ordinary expressions: `match (Point { x: 1, y: 2 }) { ... }`. A body brace cannot be consumed as a direct header literal.
- **D4 — cast boundaries.** `x as u32 | y` means `(x as u32) | y`; a union target is grouped as `x as (A | B)`. `x as T?` names optional T, while `(x as T)?` applies propagation to a result-producing expression if its type permits it. A dotted target is a qualified type name; use `(x as T).member` for member access after conversion. These parse forms do not authorize arbitrary class/union casts.
- **D5 — separators and block values.** the nearest unmatched delimiter controls newline handling, so a block within a call has statement newlines. A trailing newline preserves an if/match branch's final expression value; an explicit trailing semicolon discards it. Ordinary function bodies still require explicit non-unit returns. Opening function braces stay on the signature's logical line. `else` may follow newlines but not a separating semicolon. Where requirements use commas; trailing commas are admitted in ordinary lists but not angle lists. CRLF is one logical newline; lone-CR policy remains open.
- **D6 — references and patterns.** Proposed: prefix `ref` has unary precedence, so `ref a[i]` references indexed storage and `ref a + b` parses as `(ref a) + b`, subject to checking. Bare pattern names must be resolved as binding, variant, or type without capitalization; collision precedence remains open. Explicit borrowed payload patterns are not yet specified.

Generic-first priority selects a syntactically complete generic angle list followed by a call or member continuation before comparison parsing: `a<b>(c)` is generic syntax; `(a < b) > c` requests comparisons. Later type failure does not trigger reinterpretation. `>>` can supply two generic closing brackets; speculative parsing must restore split tokens and source offsets on failure. The review draft does not split `>=` into a generic close and assignment: use separation in that case. Longest applicable punctuation tokens distinguish wrapping assignment, wrapping negation/subtraction, and contiguous `!as` with a word boundary; `!assert` is not a wrapping cast.

The grammar also leaves explicit coverage choices for read-only-borrow spelling, default/variadic parameters, generic external methods, struct/primitive methods, and historical alternate forms. Dictionary literals are included in the updated syntax draft. Section 19 lists them rather than treating omission as permanent rejection.

## 4. Types and data declarations

### Scalars

The initial scalar types are:

```text
bool
u8 u16 u32 u64
i8 i16 i32 i64
f32 f64
unit
```

Larger widths may be added when needed. Foreign interfaces must specify ABI widths explicitly rather than make ordinary code depend silently on C's integer sizes.

Integer literals default to `i32` unless context determines another representable integer type. Float literals default to `f64`. Decimal integers permit underscores. Hexadecimal, octal, binary, exponent, and suffix forms are not initially accepted without an explicit grammar extension.

`true`, `false`, and `null` have their usual literal meanings. `nil` is not an alternative spelling.

Strings use double quotes. Byte characters use single quotes. F-strings interpolate expressions, as in `f"count={count}"`. Backticks construct interned symbols, as in `` `name` ``.

Literal syntax is specified in Section 3; numeric conversions and overflow are specified in Section 14. Backend accidents do not define source semantics.

### Strings are byte arrays

`string` is a transparent standard alias for `[u8]`, not a distinct nominal type or a separate immutable object category. String literals construct byte-array values. All ordinary array assignment, parameter, slice, mutation, equality, and stable-borrow rules apply. `let` prevents mutation of the array value; `var` permits byte/length changes. Copies are logically independent, including nested arrays of strings. Moves and copy elision may avoid physical copies only when they preserve those semantics.

```lyric
let original = "hello"
var changed = original
changed[0] = 'H'
assert_eq(original, "hello")
assert_eq(changed, "Hello")
let bytes: [u8] = original
let text: string = bytes
```

Indexing returns u8 and len counts bytes. Runtime values may contain arbitrary bytes, including invalid UTF-8; source-file UTF-8 validation and Unicode-text operations are separate contracts. Explicit references into strings borrow ordinary byte-array storage and must not survive potentially invalidating resize, replacement, or owner destruction.

There is no type-based distinction between string and [u8] for overload selection or capability satisfaction. String-to-bytes and bytes-to-string conversions are ordinary value identity operations, not implicit shared mutable views or encoding conversions. The compatibility helpers string_to_bytes and bytes_to_string have those same value semantics. Explicit borrowing, not a conversion helper, requests shared access to existing storage.

### Unit and absence

`unit` has exactly one value, written `()`, which carries no information. It is the ordinary result type of a function with no declared return type; there is no separate `void` type. Bare `return` or reaching the end of a unit-returning function supplies this value. `null` instead denotes optional absence and is not the unit value.

Empty parentheses in expression position form a unit value. They use the ordinary separate `(` and `)` tokens, not a special two-character token; whitespace between them is permitted. Parentheses following a callable expression remain the call syntax:

```lyric
work()        // call with no arguments
let value = () // unit value
work(())      // call with one unit argument
```

The unit payload need not occupy runtime storage. A containing tagged type such as `Result<unit>` still distinguishes its success and failure alternatives. Section 8 specifies unit success for fallible functions.

### Composite types

```text
T?               optional T
T | U            tagged union
[T]              array
(T, U)           tuple
fn(T) -> U       callable type
gen T            generator result
ref T            borrow of existing T storage that must remain stable and valid
```

Tuple fields use `.0`, `.1`, and so on, with zero-based positions. The selector is a nonnegative ASCII decimal digit sequence, not an expression; no sign, underscore, suffix, or numeric base prefix is accepted. Leading zeros do not change its decimal value. The tuple's statically known arity determines whether the position exists; an out-of-range selector is a compile-time error, not a runtime bounds check. The result has the selected component's type. There is no legacy underscore-prefixed tuple selector.

```lyric
let sample = (42, "hello")
let number = sample.0     // i32
let text = sample.1       // string
// sample.2              // rejected: no third field
let nested = ((1, 2), "label")
let second = nested.0.1   // two tuple-field selections, result i32
let (left, right) = sample
```

Tuple field selection has ordinary member/postfix precedence and evaluates its receiver once. Reading a selected value follows that type's ordinary copy/retention rules. Assignment to a tuple field requires mutable storage; borrowing a field uses the ordinary ref rules and depends on the containing tuple storage remaining stable and valid. Selecting a class-reference component does not clone or deep-freeze its object.

A tuple field selector is not array indexing: `tuple[index]`, including a literal index, is not an alternate tuple-access form. Runtime-indexed heterogeneous tuples are not introduced. Tuple destructuring remains available.

After a member-access dot, digits form one integer field selector and do not consume a following dot into a floating-point token. Consequently `nested.0.1` means `(nested.0).1`, whereas a numeric literal in expression position such as `1.25` remains a float. This lexical/syntactic boundary is independent of name resolution and whitespace; parsing must not depend on discovering that the receiver happens to be a tuple. Non-tuple receivers with numeric field selectors are rejected by type checking.

Transparent aliases use `type Name = Type`. An alias does not create a second nominal type.

Null is accepted only in nullable positions. Optional wrapping is implicit; unwrapping uses `!` or a checked conditional. No unsafe cast may manufacture an arbitrary live object reference.

### Structs

Structs are values without identity:

```lyric
struct Point {
    x: f64
    y: f64
}
```

They are copied as values. Writable reference parameters use `ref` in the type, with an explicit reference expression at the call site:

```lyric
func translate(point: ref Point, dx: f64) {
    point.x = point.x + dx
}
translate(ref p, 3.0)
```

Copying a struct does not exempt object references stored inside it from lifetime checking.

### Enums

Enums describe alternatives, with optional payloads:

```lyric
enum Shape {
    Circle(radius: f64)
    Rect(width: f64, height: f64)
}
```

Pattern matching supports variants, tuples, literals, bindings, guards, and `_`. Expression branches must produce compatible result types. A match producing a value must be exhaustive. The compiler must not use the first branch's type without checking the others.

### Expected-type enum constructor resolution

An unqualified variant constructor name may be resolved using a known expected enum type from an annotated binding/assignment, a parameter, or the enclosing function's declared return type. If that enum declares the named variant, select it and check its payload arguments normally. A payload mismatch is an error; do not retry a variant from another enum. Without an expected enum type, ordinary lookup must identify one accessible variant or require qualification. An expected tagged union containing several enums does not select between their same-named variants.

```lyric
enum Coordinates { Pair(x: i32, y: i32) }
enum Labels { Pair(left: string, right: string) }
let point: Coordinates = Pair(1, 2)       // Coordinates.Pair
let names = Labels.Pair("left", "right") // explicit qualification
// let ambiguous = Pair(1, 2)            // rejected if both names are visible
// let wrong: Coordinates = Pair("x", "y") // payload type error, no fallback
```

This is name resolution, distinct from Section 4's implicit wrapping of an untagged payload. It does not infer an expected enum by searching arbitrary conversions, does not depend on capitalization, and does not override an ordinary lexical value binding. New declarations cannot hide visible names under Section 5; qualification selects the intended enum when lookup is ambiguous. Qualified variant construction always identifies its enum explicitly.

### Classes

Classes have identity. Two objects with identical fields are still different objects.

```lyric
class Node {
    name: string
    func Node(self, graph: Graph, name: string) -> Result<unit>
}
```

The declaration makes lifetime policy explicit:

```lyric
value class Matrix {
    rows: i32
    columns: i32
    // Mutable, reference-counted value-like object.
}

class Node {
    // Explicitly destroyed or cascade-deleted through owning relations.
}
```

A value class may be mutable. The modifier does not imply stack allocation, deep copying, or immutability. An ordinary class does not become a value class merely because it currently has no parent. There is no separate `root class` category. Both kinds may have methods and a `final` hook.

There is no classical inheritance. Interfaces describe capabilities; relations describe stored relationships.

Methods may be declared in a class and defined separately using qualified names. For a constructor declared as `func Node(self, ...)` inside Node, its external definition is `func Node.Node(self, ...)`. This uses the ordinary qualified-member rule, not a separate constructor grammar.

### Construction expressions

Named-field struct construction uses `Point { x: 1.0, y: 2.0 }`. Field names must match exactly, fields cannot be specified twice, and omitted fields require a defined default or explicit initialization rule. Field defaults belong to the declaration.

The original permits positional struct construction in expression contexts where braces are unambiguous. Retaining that second form is a compatibility choice, not necessary for KISS. Until that choice is made, examples in this specification use named fields only.

A class with an explicit constructor uses `ClassName(arguments)`. Generic construction uses `ClassName<T>(arguments)`. Field-literal class construction is allowed only when it cannot bypass a required constructor, parent attachment, or initialization invariant. The exact eligibility check must be specified rather than treating all class literals as harmless records.

### Tagged unions

`T | U` is a tagged union. Each alternative injects into the union without reinterpretation. Extraction requires a type/variant match, not an unchecked cast. A union-to-union assignment requires every source alternative to be accepted by the target; it does not silently discard alternatives. Matching a union must respect its actual tag.

### Unambiguous tagged wrapping

When an assignment, argument, or return supplies an expected enum or tagged-union type, an untagged value may be wrapped implicitly if exactly one alternative accepts its payload type. Enum inference applies to alternatives with one payload value; explicit variant construction remains available for every variant. A value already of the expected tagged type is passed normally, not wrapped again.

Match resolved payload types using ordinary type compatibility, including checked interface satisfaction. Do not search arbitrary conversion chains or use speculative numeric widening to choose a variant. Multiple matching alternatives are an ambiguity error; no match is a type error. Without an expected tagged type, do not search for an enum to invent as the expression's type. This rule only wraps values; it never implicitly unwraps a tagged value.

```lyric
enum Measurement {
    Width(value: i32)
    Height(value: i32)
}

// let ambiguous: Measurement = 10  // rejected: both variants match
let width: Measurement = Width(10)
```

Similarly a Status value returned as `Result<Status>` requires explicit `Ok(status)` or `Err(status)`; Err still requires non-OK status. Tagged unions use the same unique-alternative requirement. Implicit wrapping preserves the ordinary value-copy, ownership, and effect rules of the selected payload; it does not create a new lifetime policy.

## 5. Bindings, arrays, and storage references

A binding names a value or fixed storage alias. Array values remain independent on assignment; references name existing storage without inventing a new owner.

### Value bindings and definite assignment

```lyric
let x = 42
var count: i32 = 0
let later: i32
let _ = operation()
let (left, right) = pair()
```

A declaration without an initializer is permitted locally only when definite-assignment analysis proves initialization before use. A `let` binding may be initialized once, not subsequently changed. Module-level declarations require an initializer. `let` prevents changing the bound value, including array elements and length; `var` permits value mutation and rebinding. Neither makes referenced class objects immutable. Reference parameters use `name: ref T`; reference arguments use `ref storage`. Constants and mutable globals need visibility and initialization-order rules before cross-module use is accepted.

### No name shadowing

A declaration must not hide a name already visible through unqualified lookup at its declaration site. This applies to local bindings, function parameters, pattern-bound names, generic type parameters, and other declarations that introduce names, including import aliases. Visible builtin and standard-library type names receive the same protection as other visible names; they are not a special lexical keyword category. Duplicate declarations in the same scope remain errors except for explicitly supported declaration/body matching or other separately specified declaration groups.

Independent scopes and qualified namespaces may reuse names. Parameters in different functions, bindings in disjoint sibling blocks, and members of different classes do not hide one another. A member accessed only through an explicit receiver, such as self.name, is not implicitly introduced as an unqualified local name: a local name can coexist with it. Likewise ast.Node and graph.Node can coexist as qualified names. Import/name resolution must diagnose an ambiguous unqualified reference rather than silently choose one declaration.

```lyric
let count = 10
if ready {
    // let count = 20   // rejected: hides the outer count
    let remaining_count = 20
}

if let live_node = maybe_node {
    use(live_node)
}
// if let maybe_node = maybe_node { ... } // rejected shadowing
```

A name stops being available for reuse only according to its lexical visibility, not its last value use: ending a borrow does not permit a later declaration in that scope to hide the old name. Assignment to an existing mutable binding is not a declaration. Pattern extraction and value transformations use distinct names instead of shadowing. Inherited generic parameters and a callable's matching declaration/definition identify the same declarations; they do not redeclare fresh shadowing parameters.

Qualification and expected-type enum variant resolution retain their existing rules. Distinct enums may each declare the same member name in their own scopes; an unqualified variant reference must still be resolved unambiguously under Section 4. No-shadowing does not select one of several equally applicable variants or interface bindings.

### Assignment has value semantics

Ordinary array assignment copies the array value at every array level. Mutating the destination's elements or length, including nested arrays, does not change the source array value:

```lyric
let original = [1, 2]
var working = original
working[0] = 9
assert_eq(original[0], 1)
assert_eq(working[0], 9)
```

For an array of class references, copying copies the references, not the referenced objects. Replacing a destination slot leaves the source slot unchanged; mutating an object referenced by both arrays is visible through both references. Copies of counted references retain their objects; ordinary-class references remain subject to destruction invalidation.

An array property belongs to its containing object. Different objects do not share a mutable array property or mutable backing allocation; assignment creates an independent array value, recursively through nested arrays. Explicit references into an array do not change its owner.

```lyric
let original = [[1, 2], [3, 4]]
var working = original
working[0][0] = 9
assert_eq(original[0][0], 1)
```

This is a semantic copy, not a demand for avoidable allocations. Moves and copy elision are allowed when they preserve independence and lifetime behavior. This initial concurrency design does not rely on shared copy-on-write backing between object owners; that would require an additional synchronization contract and is not silently assumed.

`let` prevents mutation of its array value, including nested array elements and length. `var` permits it. Writable reference access requires mutable storage and cannot bypass `let`. Ordinary parameters follow assignment's value semantics; explicit reference parameters borrow the caller's storage under the reference-parameter rules in this section. Ordinary slices produce independent array values; explicit reference slices borrow fixed-length windows.

### Elements and resizing

All indexing is bounds checked by default. Growing an array initializes new elements; new reference slots are null. Shrinking releases removed elements, decrementing reference counts where applicable. Any resulting destruction contributes its effects.

Element types and initialization must agree: an array cannot expose null as a valid nonnull element. Nondefaultable elements need explicit initialization before access.

### Named references are fixed aliases

A named reference denotes fixed existing storage. Reading it in a value context reads the referenced value; assignment and compound assignment write through to that storage. A `let` reference binding fixes the storage it denotes, not the contents of mutable source storage. It cannot grant write permission to immutable storage.

```lyric
var count = 1
let slot = ref count
slot = 2
let snapshot = slot
let alias = ref slot
alias += 1
assert_eq(count, 3)
assert_eq(snapshot, 2)
```

Here `snapshot` is an ordinary value copy. `ref slot` aliases the same storage as `slot`; it does not create a reference-to-reference. Forwarding that alias to a reference parameter uses the ordinary explicit argument form, such as `increment(ref slot)`. The alias carries the original storage provenance, permissions, and lifetime obligations; creating it does not revive an invalid reference.

The initial language has no rebindable reference variables. A reference local is declared with `let`, not `var`; `var slot: ref T` and inferred `var slot = ref storage` are rejected. Reference parameters are fixed aliases too. Assignment never retargets an existing reference binding, and a reference expression is not accepted as a retargeting assignment. To denote another location, introduce another binding. In a value context, ordinary reference reads still copy according to the referenced type's value semantics.

For a storage reference, fixed identity means the borrowed storage location, not a recipe for finding a replacement location. It cannot retarget after relocation or storage replacement. Borrowing an array element is distinct from borrowing the variable that holds the array value, as specified below.

The root's lifetime is checked, not automatically extended by inventing a new heap owner. References to local array storage cannot outlive that storage. References to object-owned arrays require the object to remain alive and, in concurrent code, its class lock to remain held. Global roots have program lifetime but still require protection against concurrent mutation.

### Read-only borrowing

A read-only borrow names existing storage without granting writes through that access path. It may borrow immutable or mutable storage, including arrays and fixed-length windows. Reading copies values under the ordinary value rules; the borrow itself neither copies the whole source nor extends its lifetime. It remains a fixed alias with the same stable-storage, bounds, active-payload, nonescape, and owner-lifetime obligations as other references.

Writes, compound assignment, resize, whole-value replacement, and forwarding to a writable-reference parameter through a read-only borrow are rejected. A writable borrow may supply a read-only requirement, but a read-only borrow cannot be promoted to writable access. These permissions survive helper calls, captures, and specialization. A read-only array borrow permits inspection of its value elements, not writes to nested value storage through that path.

Read-only access is not deep object immutability: reading a class-reference value does not freeze that separate object. Nor does a read-only borrow freeze the source against mutation through another permitted alias. It observes the current contents of the same valid storage; mutations through another alias are allowed only when they preserve that storage and satisfy all other safety requirements. In concurrent code, read-only access still requires protection against conflicting writes and destruction.

Accepted behavior includes inspecting a let-bound array without acquiring writable access, and passing a mutable array to a helper requiring only read access. Rejected behavior includes writing through that helper's borrow or forwarding it to a resize-capable helper. Ordinary value parameters remain immutable independent values; copy elision is an optimization, not a conversion of their semantics into borrowing.

**Syntax open:** read-only borrowing is part of the intended language, but its type, argument, and local-binding spelling must be selected together. Existing `ref` remains writable and cannot borrow immutable storage. No new keyword or implicit permission-changing syntax is established by this section. The PEG must be extended after that spelling decision.

### Stable-storage borrow requirement

Throughout a borrow's usable lifetime, the referenced storage must remain allocated, at a stable address, and valid for access as the referenced type. An operation that might free, relocate, or invalidate that storage is rejected if the borrow or an alias derived from it may subsequently be used. This is a static requirement for single-threaded code, not a runtime attempt to reconstruct an address after mutation. The checker algorithm is separate implementation work.

A usable lifetime is not necessarily the whole lexical scope of a reference variable. It extends through every possible use, including uses through aliases, parameters, captured environments, aggregates, compiler-generated temporaries, and required cleanup. After the last possible use, the borrow imposes no further storage-stability restriction. Control-flow joins, loop iterations, callbacks, and recursive calls must preserve this guarantee; inability to establish it requires conservative rejection.

Potentially relocating operations such as array growth are initially treated as invalidating regardless of whether a particular execution happens to have spare capacity. An implementation may accept additional cases only when it proves storage and type validity are preserved. The source cannot rely on unproved allocator behavior.

```lyric
var numbers = [1, 2]
let element = ref numbers[0]
element = 9
numbers.push(3)          // accepted: element has no later use

var other = [1, 2]
let held = ref other[0]
// other.push(3)        // rejected if followed by the use of held below
held = 9
```

An invalidated borrow is never revived by restoring the array length, inserting a new value at the same index, or reusing an allocation address. Obtain a new reference after the invalidating operation. Index and optional/tag checks occur when obtaining the borrow; invalid acquisition produces checked failure before any invalid access.

### Array variables, elements, and separate objects

`ref array` borrows the storage holding the array value (its descriptor), not every element in its backing allocation. A writable whole-array reference may resize or replace that array value when its own storage remains stable and no still-needed element, range, or nested-storage borrow would be invalidated. Resize permission alone is not proof that no such borrow exists.

`ref array[index]` borrows the selected element's storage. The array and index expressions evaluate once, in the normal left-to-right order, with bounds checking before acquisition. A range borrow similarly selects existing backing storage. These borrows do not follow future replacement allocations. A single-element borrow can be implemented as an ordinary pointer; no particular machine representation is required. A range needs its fixed length for bounds checking, but not a root/index reconstruction mechanism.

An object reference copied from an array selects the separate class object, not its array slot. Relocating the array need not invalidate that object reference, provided the object itself remains alive. Conversely, destroying the object invalidates its references even if the array slot remains allocated. Counted-element releases and all transitive destruction effects still apply.

### Nested storage and in-place mutation

For `ref matrix[0][1]`, the checker must account for every enclosing value or owner whose mutation can invalidate the selected element storage. Resizing or replacing the selected inner array, removing or replacing the enclosing value that owns it, and destroying the containing object can invalidate the borrow. Checking only the outermost allocation is insufficient. A conservative implementation may reject uncertain cases; it must not assume nested backing remains valid merely because an outer object is stable.

Stable storage does not mean immutable contents. Writing a new integer to an existing integer slot can preserve a borrow to that slot. Replacing an owning array value can free its former element storage and invalidate interior borrows even if the array descriptor's address is unchanged. Reordering values within demonstrably stable slots changes their contents, not the identity of a slot borrow; it is not an element-identity tracking mechanism. Sorting, insertion, removal, or replacement is permitted across a borrow only when all relevant storage, active-type, and transitive destruction requirements are established. There is no blanket permission for every in-place-looking operation.

```lyric
var matrix = [[1, 2], [3, 4]]
let slot = ref matrix[0][1]
slot = 99                  // accepted: ordinary write to existing storage
// matrix[0] = [10, 20]     // rejected if slot will be used afterward
let copied = slot
matrix[0] = [10, 20]        // accepted: slot has no subsequent use
let fresh = ref matrix[0][1]
fresh = 30
```

Owner destruction and class-access release retain their existing invalidation rules. Neither another object occupying the same slab slot nor reentering a protected region makes an old borrow usable again.

### Slices and borrowed ranges

An ordinary array slice `array[low:high]` produces an independent array value, recursively through nested array elements. Class-reference elements copy references, not objects. Missing low means zero; missing high means the array's length when the expression is evaluated. Bounds are half-open, and the evaluated endpoints must satisfy `0 <= low <= high <= length`. An empty range is valid. Invalid ranges produce a checked bounds failure.

```lyric
var numbers = [10, 20, 30, 40]
var middle = numbers[1:3]
middle[0] = 99
assert_eq(numbers[1], 20)

let window = ref numbers[1:3]
window[0] = 99
assert_eq(numbers[1], 99)
```

An explicit `ref array[low:high]` borrows a fixed-length window of existing mutable storage. Evaluate the array and endpoints once, left to right, and validate the half-open bounds before acquiring the borrow. The selected storage must remain allocated, stable, and valid throughout the window's usable lifetime. Each element access is checked against the fixed window length; out-of-window access produces checked failure. Resizing or replacing the source is rejected while the window is still needed if it might invalidate the selected storage. An empty window is valid but does not waive owner-lifetime or invalidation checks.

A borrowed window permits element reads and writes but cannot resize the source through the window. Operations that change the window's length, such as push, pop, clear, or length-changing replacement, are rejected. A source mutation through another alias must preserve the still-needed window's storage; the window cannot be retargeted or extended to make an invalidating operation safe. Owner-lifetime checks, nonescape rules, and continuous class-lock requirements apply just as for other borrowed references.

The window uses the `ref` mechanism, not a new user-visible view category. Its fixed-length permission remains enforced through helper calls using the inferred requirements below. Replacement is subject to the stable-storage requirements in this section.

### Whole-window assignment

Assignment to a borrowed window copies an equal-length array value into its existing slots without resizing or retargeting the window. The copy is overlap-safe: it has `memmove`, not `memcpy`, semantics for plain elements.

```lyric
var numbers = [1, 2, 3, 4]
let destination = ref numbers[1:4]
destination = numbers[0:3]
assert_eq(numbers, [1, 1, 2, 3])
```

Evaluate the destination array selection and endpoints once and validate its bounds, then evaluate the right-hand array value before writing destination elements. The destination borrow remains needed across right-hand evaluation: any effects that could invalidate its storage or owner are rejected, rather than followed by re-resolution. Require equal lengths before assignment changes any destination element. Bounds or length failure leaves the assignment's destination writes unperformed; effects already performed while evaluating expressions are not rolled back. Releases and callbacks during element assignment must also preserve every borrow still needed to complete the operation.

The result must be as if the right-hand value were preserved before destination writes, including when source and destination overlap. No temporary array allocation is required when an overlap-safe copy or another optimization preserves that behavior. Nested arrays retain independent value semantics; class-reference elements copy references rather than objects. Counted elements require correct retains/releases and their destruction effects, so raw bytewise `memmove` is not sufficient for every element type. This rule specifies overlap and pre-write bounds/length checks, not a general transaction or rollback guarantee for arbitrary finalizer effects.

Ordinary slice values may be moved or have copies elided when value independence and lifetime behavior are preserved. No mutable sharing is implied by ordinary slice syntax.

### Inferred resize requirements at function boundaries

Whole-array references and borrowed windows use the same source spelling, `ref [T]`. The compiler infers whether a function may resize storage denoted by each reference parameter. Element reads and writes alone require no resize permission. Operations such as push, pop, clear, or whole-array replacement that may change length require it. A whole-window assignment remains equal-length under the fixed-length window contract and does not grant resize permission.

```lyric
func fill(items: ref [i32], value: i32) {
    for item, index in items {
        items[index] = value
    }
}

func grow(items: ref [i32]) {
    items.push(0)
}

var numbers = [1, 2, 3, 4]
let window = ref numbers[1:3]
fill(ref window, 9)  // accepted: element writes only
// grow(ref window) // rejected: a fixed window cannot supply resize permission
grow(ref numbers)   // accepted: window has no subsequent use
```

Requirements propagate transitively through calls, retaining the actual reference arguments' storage origins and enclosing storage dependencies. Resize requirements must account for every reachable call in recursive groups; generic specialization and callbacks must not hide a resize requirement. The requirement belongs to the affected parameter and nested storage level, not indiscriminately to every array argument. Resizing an independently owned local copy does not require permission to resize the caller's storage. Nested-array operations must identify the affected level; permission to write window elements does not mean permission to change the window's own length.

At a call, the compiler checks that each actual argument supplies the inferred permissions. An ordinary mutable whole-array reference can supply resize permission; a fixed-length borrowed window cannot. No additional source keyword or separate slice type is required for ordinary functions. For external or otherwise opaque code, the conservative reference/resize requirements must be explicitly declared; unknown effects are not assumed harmless. The spelling of opaque boundary contracts remains part of the foreign-interface work.

These permissions complement rather than replace stable-borrow checking, bounds checks, and locking. A helper that does not resize may still destroy an owner through another argument. A helper with resize permission may use it only when it does not invalidate any borrow still needed inside or after the call. Overlapping arguments must not erase this obligation.

```lyric
func invalid_update(element: ref i32, items: ref [i32]) {
    items.push(0)
    element = 7
}
// invalid_update(ref numbers[0], ref numbers)
// rejected: this call would let push invalidate element before its write
```

This example establishes the required rejection for the overlapping call; it does not prescribe context specialization or a particular effect-analysis algorithm.

### References into optional and tagged payloads

A borrow into an optional or enum/union value requires its payload to be present and of the selected alternative at acquisition. Check this before obtaining the reference, unless already proven. Absence or the wrong alternative produces checked failure before an invalid access.

The selected payload storage must then remain live, stable, and valid for its type until the borrow is no longer needed. Clearing the optional, changing the active alternative, or replacing/reconstructing the enclosing payload is rejected across a still-needed interior borrow. The same tag and reused bytes are not sufficient to revive a borrow whose payload lifetime ended. Ordinary writes to fields of the existing active payload can preserve validity.

```lyric
struct Point { x: i32, y: i32 }
var point: Point? = Point { x: 1, y: 2 }
let x = ref point!.x
x = 99
// point = null          // rejected if followed by another use of x
let copied = x
point = null             // accepted: x has no later use
point = Point { x: 10, y: 20 }
let fresh = ref point!.x // new borrow of the new payload
fresh = 30
```

Nested field, array, and payload selections retain all enclosing storage dependencies. Read-only borrowing does not waive stability or active-payload requirements. The precise borrowed-pattern spelling remains open; ordinary value-pattern bindings do not silently become writable aliases.

Copying a class-reference value from a payload acquires a reference to that separate object, not to its containing value slot. Replacing the slot does not retarget that object reference, and any destruction caused by releasing the former value still contributes its full invalidation effects.

### Reference parameters and escape

```lyric
func increment(value: ref u32) {
    value += 1
}

var count: u32 = 0
increment(ref count)
```

A ref parameter borrows existing storage. A function cannot assume that storage is global or heap-owned; it treats the reference as potentially stack-backed. It cannot return that reference or store it in a persistent location. Passing it to a helper is allowed only when the helper preserves the same nonescape obligation.

In a value context, reading through a reference produces a value. Returning that value or assigning it to a global value slot copies according to ordinary type semantics; it does not make a stack pointer escape. In a reference context, an escaping borrowed reference is rejected rather than silently allocating replacement storage to satisfy a `ref T` result.

```lyric
func copy_value(value: ref u32) -> u32 {
    return value
}

// Rejected: the caller may have passed a reference to its stack.
func escape_reference(value: ref u32) -> ref u32 {
    return value
}
```

An array value copied out of a scope has independent value semantics; the implementation may move storage when safe. Copying a class-reference value does not clone its object and does not waive that object's lifetime rules.

### Storage categories

- **Stack:** the defining activation remains alive during a synchronous call. No reference into it may escape that lifetime. In-place value writes may preserve borrowed storage, but moves, enclosing-value replacement, or cleanup that invalidates it are prohibited while the borrow is still needed. Destruction of a separate owner still invalidates that owner's borrows.
- **Object-owned:** the object must outlive the reference. Calls that may destroy its class invalidate held references; no later access is allowed without reacquisition. Class locking prevents concurrent deletion, not same-thread invalidation.
- **Global:** the slot lives for the program's lifetime. Objects or arrays reached through its contents still have their own destruction and relocation rules. A global slot is not a guarantee that its current target lives forever.

For a reference into an object, invalidation follows the **owner's** class, not the scalar type of the field. A `ref u32` into a Node becomes invalid when Node may be destroyed. This provenance is compiler metadata even though the source type remains `ref u32`.

### Optimization

The implementation may use direct references when the stable-storage requirements hold. Optimizations must not move or reclaim borrowed storage before its last possible use. Bounds and acquisition-time tag checks can be removed when proven redundant; optimization cannot revive a borrow invalidated by a source operation.

An explicit unsafe benchmarking flag may disable bounds checks. Such builds are outside the safe-language guarantee and must be identified as unsafe in benchmark results.

## 6. Functions, generics, closures, and generators

### Declarations, calls, and methods

Ordinary functions, external methods, static functions, and generic functions use the same `func` mechanism. Instance methods declare `self`; static functions do not.

```lyric
func add(a: i32, b: i32) -> i32 {
    return a + b
}

func identity<T>(value: T) -> T {
    return value
}
```


Method calls use parentheses. Direct field access does not implicitly execute an arbitrary zero-argument action. A field can still satisfy an abstract getter requirement when the compiler binds an interface; that does not require universal property-call syntax.

Generic call arguments may be inferred or supplied explicitly when needed. `object.method<T>()` follows the same generic rules. No additional exception is needed solely because the call has a receiver.

### Generic declarations and constraints

```lyric
func first<T>(items: [T]) -> T?
func transform<T, U>(items: [T], operation: fn(T) -> U) -> [U]
func clamp<T: Comparable>(value: T, low: T, high: T) -> T
func count<P, C>(parent: P) -> i32 where Collection<P, C>
```

Type variables are declared explicitly. The canonical placement remains after the function name, not `func<T> name`. Both explicit call arguments and inference are supported:

```lyric
identity<i32>(42)
identity(42)
```

Inference matches parameter and argument type structure, including arrays, optionals, tuples, functions, and generic class arguments. Repeated occurrences of a variable must agree; the first match cannot silently override a conflicting later match. Ambiguity or an uninferred variable is a diagnostic. Implicit conversions must not be used as arbitrary guesses between implementations.

Nested generic closing brackets must parse correctly, including `Outer<Inner<T>>`, without confusing them with a shift in expression context.

Inline `T: Capability` is shorthand for the corresponding single-participant interface requirement. A relational `where Capability<P, C>` names all participants. Multiple requirements can be listed in a where-clause; adding a separate top-level constraint-alias declaration is deferred.

### Ordinary value parameters

An ordinary parameter is an immutable local binding, like `let`. Argument evaluation follows the ordinary left-to-right rule. Passing a value has the same semantics as assigning that value: arrays are independent at every nested array level, structs copy their values, and class-reference elements copy references rather than clone objects. Counted references retain/release as required; ordinary object references remain subject to lifetime checking.

To modify a local array or struct value, create a mutable local copy with `var`. To modify the caller's storage, declare a `ref` parameter and supply an explicit reference argument. An ordinary class-reference parameter does not freeze its referenced object; methods may mutate that object under the usual lifetime and locking rules.

```lyric
func changed_copy(items: [i32]) -> [i32] {
    var working = items
    working[0] = 99
    return working
}

func change_original(items: ref [i32]) {
    items[0] = 99
}

var numbers = [1, 2]
let changed = changed_copy(numbers)
assert_eq(numbers[0], 1)
assert_eq(changed[0], 99)
change_original(ref numbers)
assert_eq(numbers[0], 99)
```

Assigning to `items[0]` inside `changed_copy` is rejected because its ordinary parameter binding is immutable. Passing `ref` to immutable array storage is also rejected. Passing an array containing class references does not prevent mutation of the shared objects; replacing an array slot and mutating its referenced object are different operations.

These are value semantics, not a requirement to allocate and copy at every call. The compiler may borrow backing storage, move values, or eliminate copies when it proves the observable values, mutation permissions, reference validity, and permitted cleanup behavior are preserved. Ordinary array parameters do not inherit the original specification's shared-mutable-backing exception.

### Nonescaping closures

The initial closure model uses a concrete compiler-generated environment type for each closure expression and an ordinary function implementing its body. Nonescaping closures may reference caller storage. Their environments can live on the stack or be eliminated by optimization; heap allocation is not required merely because a closure captures variables.

```lyric
let offset = 10
let add_offset = (x: i32) -> i32 {
    return x + offset
}
```

A statically known closure call is specialized for that concrete environment and body. It may become a direct call or be inlined. This is an opportunity for zero additional overhead, not a guarantee that every closure is free or every call is inlined.

The compiler must prove nonescape through helper calls: a borrowed closure cannot be returned or stored where it can outlive its captured storage. Capture does not freeze a referenced object or prevent destruction. Reference validity, mutation permissions, and transitive destruction effects apply to captures exactly as to other held references. Detailed capture-selection syntax remains to be specified; no ownership guarantee is inferred merely from the surface callable type.

Escaping closures are deferred. Unsupported escaping uses are rejected, not silently heap-boxed with unspecified lifetime semantics. Ordinary `return` inside an ordinary closure returns from that closure; the special consumer-loop behavior below does not change ordinary function semantics.

### Loop-only generators

A generator is consumed directly by a `for` loop. It is not an independently resumable object and cannot be stored, returned as suspended state, or manually advanced. Its body contains exactly one syntactic `yield` statement. That statement may execute repeatedly inside loops, or not execute on a particular path.

```lyric
func range(start: i32, end: i32) -> gen i32 {
    var i = start
    while i < end {
        yield i
        i += 1
    }
}

for number in range(0, 10) {
    println(number)
}
```

The generator is expanded at the consuming loop. At `yield`, the yielded expression is evaluated and bound to the consumer's iteration variable, then the consumer body runs. After that body completes normally, execution resumes immediately after `yield`. Generator arguments are evaluated once, in ordinary left-to-right order, before iteration; expansion must not duplicate their effects.

The compiler may implement this expansion by representing the consumer body as a statically known nonescaping closure, specializing the producer and consumer together, and inlining. No independently allocated suspended-generator frame is required. The old Duff's-device state-machine description is not the contract for this initial design.

### Explicit safe-generator contract

An ordinary generator does not permit its consumer to destroy the current yielded object. A generator declared with `safe` explicitly grants current-object destruction permission:

```lyric
class Traversal {
    func iter(self) -> gen Node
    safe func iter_removable(self) -> gen Node
}
```

These are schematic declarations; actual traversal bodies must supply their stated behavior. The spelling is `[pub] safe func name(...) -> gen T`, with the ordinary receiver, generic, and where-clause forms. `safe` is contextual immediately before func. It is valid only on generators, not ordinary functions, constructors, or final hooks, and cannot be combined with final. It introduces no escaping generator object and does not relax the single-yield restriction.

The modifier means only that the consumer may destroy the current yielded object, subject to Section 12's current-only effect restrictions. It does not permit destruction of peer objects of the yielded class, arbitrary collection mutation, invalidating storage moves, or use of the current reference after destruction. For non-object yielded values it supplies no extra destruction operation; ordinary value/lifetime rules still apply. Both ordinary and safe generators must be memory-safe; the adjective describes the specific deletion permission, not an unsafe alternative execution mode.

The compiler must verify a safe generator's continuation and cleanup for every consumer behavior permitted by its contract, including current-object destruction and the ordinary break/continue/return paths. A body that may access the destroyed current object after yield does not satisfy the contract. Saving another reference is insufficient unless its continued validity is established under all permitted effects. A safe declaration is a checked obligation, never a trusted bypass or a promise inferred from the function name.

At each consuming loop, the compiler checks direct and transitive consumer effects, including helpers, cascades, callbacks, final hooks, and counted releases. For an ordinary generator, a potentially current-destroying consumer is rejected even if a particular body could happen to tolerate it; opting in requires safe on the declaration. For a safe generator, the compiler must prove destruction affects only the current object of the yielded class and that all other producer/consumer storage obligations hold. Unknown effects do not establish permission. Opaque generator implementations require the same explicit contract at the trusted boundary; a safe spelling is not evidence that arbitrary foreign code honors it.

Declaration/body matching includes the modifier. A bodyless interface requirement can declare safe, and its implementation, adapters, aliases, defaults, and specialized calls must preserve and verify that permission. An unmarked implementation cannot satisfy a safe requirement. Calling through an ordinary requirement does not gain safe permission merely because its implementation has a stronger contract. The compiler tracks the selected declaration's contract; spelling a method iter_removable does not confer permission by itself.

A rejected consumer should report the potentially destructive operation and the selected generator declaration, explain whether current-item permission is absent or peer destruction is possible, and point to an available safe traversal when known. Rejection is a compile-time error, not a warning. The analysis algorithm remains separate implementation work.

### Consumer control flow

- A `continue` targeting the consuming loop finishes the current consumer body and resumes the generator immediately after `yield`. It does not skip producer code such as the increment in `range`.
- A `break` targeting the consuming loop stops the entire generated iteration.
- A `return` in the consumer body returns from the enclosing consumer function, not from a synthetic callback.
- Control flow in nested consumer loops retains its normal lexical target. Producer-local `break`, `continue`, and `return` retain their producer targets; a producer return ends generation.

Every exit performs the required cleanup for the scopes it leaves. Ordinary callback substitution without preserving these control-flow targets is not a correct implementation.

### Safety and initial limits

Analyze producer, consumer, captures, and calls together under the same reference-validity and destruction rules as ordinary code. In particular, a consumer's destructive effects apply to producer references held across `yield`. A producer that uses a deleted iteration object after `yield` is rejected; a generator marked safe must establish continuation validity under its declared deletion permission. An ordinary generator may retain the current object across yield because its consumer is forbidden to destroy it, but all other effect and storage checks still apply.

The single-yield restriction counts yield statements belonging to the generator body, not the number of values produced. Multiple yield sites and independently resumable generators are deferred extensions. The language does not promise general coroutine behavior from the `gen T` spelling.

### Declaration details still open

Separate generic external-method definitions need a precise scope rule when both the receiver class and the method declare type parameters. Closure capture selection remains unfinished. Default arguments and variadic syntax must be resolved for library calls such as assertions and formatting; compiler intrinsics or overloads do not silently establish a general variadic mechanism. These are source-language contracts, distinct from selecting a closure lowering or specialization implementation.

## 7. Control flow and patterns

Blocks introduce lexical scope. `if`, `while`, `for`, `match`, `break`, `continue`, and `return` have structured behavior.

`if let` conditionally extracts an optional or pattern. `let Pattern = expression else { ... }` requires a diverging else branch. Pattern recognition must use resolved declarations, not capitalization.

Optional access is explicit. `x!` checks for null and unwraps; unsafe implicit nullable dereference is not a language feature.

### Conditionals and loops

```lyric
if condition {
    action()
} else {
    alternative()
}

while condition {
    action()
}

for item in collection {
    use(item)
}

for item, index in array {
    use(item, index)
}
```

Conditions have boolean type. A two-variable array loop yields the element followed by its zero-based index. `break` exits the nearest loop; `continue` begins its next iteration. Required cleanup and validity analysis apply on both paths.

An if-expression requires an else branch and compatible branch result types:

```lyric
let name = if enabled { "enabled" } else { "disabled" }
```

Safe relation iteration follows Section 12. Ordinary array iteration still needs the array storage/mutation contract; it is not automatically interchangeable with relation iteration merely because both use `for`.

### Patterns and optionals

```lyric
match shape {
    Circle(radius) => { use(radius) }
    Rect(width, height) => { use(width, height) }
}

if let Circle(radius) = shape {
    use(radius)
}

let Circle(radius) = shape else { return }
```

Qualified variant names disambiguate variants from different enums. Nested variant and tuple patterns recursively bind their payloads. Literal leaves match their values. `_` discards. Guards run only after the structural pattern matches. Pattern-bound variables exist only in the arm, except successful let-else bindings, which continue in the enclosing scope.

Alternatives in one arm must bind compatible names and types. `is` tests a variant without binding. A let-else failure branch must diverge; this is checked, not merely a convention.

`isnull(x)`, `x == null`, and `x != null` test optional absence. `x!` unwraps or produces a checked failure. The lvalue form `object.data!.field = value` writes through an optional struct rather than modifying a discarded copy. Its receiver path is evaluated once and lifetime-checked.

A `T` can wrap implicitly into `T?`. `let x = null` without a determinable target type is an error. There is no implicit null dereference on class optionals.

## 8. Results, Status, and checked failures

### Fallible returns and propagation

Fallible value-producing functions return the ordinary tagged type `Result<T>`:

```lyric
enum Result<T> {
    Ok(value: T)
    Err(status: Status)
}

func divide(a: i32, b: i32) -> Result<i32> {
    if b == 0 {
        return invalid_argument("division by zero")  // unambiguously Err
    }
    return a / b                             // unambiguously Ok
}
```

Explicit `Ok(value)` and `Err(problem)` remain available. The implicit returns above use Section 4's general expected-type tagged wrapping rule, not Result-specific conversion magic. Only the active alternative has a payload; failure contains no dummy success value. `Result<unit>` represents success without additional information or failure with an error. `return ()` wraps as `Ok(())`; bare `return` and reaching the end also supply unit success for functions declared to return `Result<unit>`. Functions returning other Result success types must supply a success value or return/propagate an error; falling through is a diagnostic.

The postfix `?` operator evaluates its operand once. Its operand must have type `Result<T>`: `Ok(value)` yields T, while `Err(status)` returns `Err(status)` from the enclosing function, performing required cleanup. That enclosing function must return `Result<U>` for some U; success types may differ, but the propagated Status is unchanged. Propagation explicitly selects Err even for `Result<Status>`, where implicit wrapping would be ambiguous. Ordinary tuples, optional values, and bare Status values are not operands for this propagation rule. Early returns and propagated failures perform the required generated cleanup.

### Concrete Status values

Failures use one concrete `Status` value type, not an open-ended `error` interface or a separate error hierarchy for each subsystem. Status carries exactly a standard code and a human-readable diagnostic string. Code-specific error factories take a string; ordinary string formatting includes additional diagnostic context, for example `not_found(f"symbol '{name}' was not found in {filename}:{line}")`.

Status has no structured-payload or attachment API. Callers make recovery decisions from the standard code, never by parsing message text. Rare operations needing additional machine-readable information return it separately through an explicitly declared function result contract, using ordinary types. That contract must define when the additional information is available, including failure cases; it is not smuggled through a diagnostic string. This keeps ordinary errors small and uniform without forbidding richer results where genuinely needed.

### Standard status codes

The standard code set is closed: projects and libraries cannot add enum cases. Diagnostic context belongs in the formatted message; additional machine-readable return data belongs in the specific function's declared result, not in Status. The codes describe failure categories shared across subsystems, not the identity of the subsystem that failed.

- `Ok`: success, never a valid Err payload.
- `Cancelled`: the operation was cancelled.
- `Unknown`: a failure occurred but no more specific category is known.
- `InvalidArgument`: a supplied argument is invalid independently of the current system state; the caller should correct it.
- `DeadlineExceeded`: the operation did not complete within its deadline.
- `NotFound`: the requested entity does not exist.
- `AlreadyExists`: an entity the operation would create already exists.
- `PermissionDenied`: the caller lacks permission for the operation.
- `Unauthenticated`: valid authentication credentials are absent or invalid.
- `ResourceExhausted`: a required resource or quota is exhausted.
- `FailedPrecondition`: the current system state does not permit the operation; that state must change before a retry can succeed.
- `Aborted`: the operation was aborted, for example because of a concurrency conflict; recovery may require restarting the larger operation or transaction rather than retrying its last step.
- `OutOfRange`: the requested position or value is outside an operation's valid range; unlike an intrinsically invalid argument, changing the relevant state may make it valid.
- `Unimplemented`: the requested operation or capability is unsupported.
- `Internal`: an internal invariant or expectation failed.
- `Unavailable`: the service or resource is presently unavailable; retry may succeed later.
- `DataLoss`: unrecoverable data loss or corruption was detected.

These codes categorize outcomes, not automatic retry policy. Cancellation, deadline expiry, or unavailability does not prove that an operation performed no effects. A caller must use the operation's contract to decide whether retry is safe. Messages supply useful context without becoming machine-parsed control flow. Standard codes are not replaced by per-library error enums and conversion hierarchies.

The source names above are normative; numeric serialization values and foreign-code mappings require a separate interoperability contract. Choosing these categories does not assert Abseil ABI compatibility.

### Status representation and Result invariants

Status has safe value semantics. Failure details may use shared reference-counted backing storage internally; copying a Status does not require copying its diagnostic message. Success requires no error allocation. No exact byte size, allocation count for failures, or backend ABI is promised here. Shared backing must preserve logical value independence and ordinary lifetime/thread-safety guarantees; it is not mutable application state shared without protection.

`Result<T>` contains either a T or a non-OK Status. Every entry into Err, including implicit tagged wrapping, enforces this invariant. If the supplied status is statically known to be OK, compilation fails. Otherwise Err construction checks the code and produces a checked programming failure if it is OK; it never creates an invalid Result, silently supplies a missing T, or substitutes an Internal status. This check follows the runtime's checked-failure contract, not ordinary Result error propagation.

A factory guaranteed to return a non-OK status, such as `invalid_argument(message)`, permits elimination of the check when that guarantee is proved. Success construction needs no invalid-OK check. The small validation cost belongs on the failure-construction path; exact performance remains an implementation measurement, not a specification claim.

Explicit `Ok(status)` remains valid for a function successfully returning a Status as data, including an OK status. It does not enter the failure alternative.

Fallible operations without a success value use `Result<unit>`. Bare Status is not a second propagation protocol: `?` accepts Result only and propagates its failure status unchanged with required cleanup. This deliberately adopts the efficient concrete-status design direction associated with Abseil Status/StatusOr without claiming API or ABI compatibility. There is no additional open-ended runtime error-interface mechanism.

### Result handling and explicit discard

Silently discarding a Result<T> is a compile-time error. An expression statement whose value is Result must propagate it with ?, return it, retain it for use, handle it, or explicitly discard it. A Result bound to a local but never used is also a diagnostic; merely introducing a name does not acknowledge the outcome.

```lyric
// save_file(path, data)             // rejected: Result ignored
save_file(path, data)?              // propagate failure
let result = save_file(path, data)  // must subsequently be used
let _ = try_optional_cleanup()      // intentional discard
```

The existing `let _ = expression` syntax explicitly discards a result and performs the ordinary cleanup of its value. Returning, storing, or passing a Result can transfer responsibility for handling it; it does not assert that the operation succeeded. No new discard keyword or per-function annotation is required for Result-returning operations.

Explicit discard is not proof of success and does not waive other checks. In particular, discarding a relation-attachment result cannot establish a constructor's required-parent postcondition. Generated structural destruction may not use explicit discard to bypass its invariant that memberships are removed before storage is reclaimed. The compiler diagnoses implicit omission, not the wisdom of every deliberate discard.

### Checked programming failures and stack traces

In the initial runtime, a checked programming failure reports a diagnostic and terminates the whole process with a nonzero exit status. This applies to failed bounds/payload checks, invalid Err(OK) construction, explicit panic, assertion failure, and detected teardown invariant failure. The invalid access or invalid Result construction must not occur. Such failures are not catchable exceptions and do not become ordinary Status returns.

The diagnostic includes the failure kind, a useful message, the source location of the failed check, and a stack trace of the calling chain. Diagnostic builds must retain the metadata needed to present Lyric function names and source locations. Optimized/inlined or stripped builds may have fewer recoverable frames or locations; report available frames and clearly indicate missing/truncated trace information rather than invent it. Stack-trace formatting and collection are diagnostic operations, not application stack unwinding. The default trace does not dump local variable values.

Reporting must not depend on successfully allocating large diagnostic objects or acquiring application class locks. If resource exhaustion or diagnostic-output failure prevents a complete trace, emit whatever minimal diagnostic is possible and terminate anyway. Failure to report must not authorize continuation. Exact backend unwinding/symbolization machinery is an implementation choice; source-level trace quality requires conformance checks, not just a native address dump called a complete trace.

There is no application cleanup guarantee on this abnormal path: no promised final hooks, lock-release sequence, or stack unwinding. Expected recoverable failures use Result<T> and ordinary cleanup instead. A failure in any thread terminates the process, not merely that thread while leaving damaged shared state running. Future recoverable-panic support would require an explicit new safety contract; C longjmp is not a substitute.

An assertion failure therefore terminates its test process. A test runner that continues after failure must isolate tests in separate processes and collect their diagnostics and exit statuses. It cannot skip cleanup and run another test in the same process.

## 9. Maintained relationships

A relation describes both endpoint roles, representation, cardinality, and destruction behavior.

```lyric
relation ArrayList Team:roster owns [Player:team]
relation DoublyLinked Room:guests refs [Guest:room]
```

- The hint, such as `ArrayList`, chooses representation.
- `owns` means parent destruction cascades to its children.
- `refs` means parent destruction only unlinks the relationship.
- Brackets mean multiple children.
- Labels name roles on each participating class.

The child's destruction removes it from every parent relationship, owning or weak. Destroying a child does not destroy its parents.

### Labels are scopes

```lyric
team.roster.append(player)
player.team.parent
```

A label is a compile-time scope on the class, not an allocated adapter object. Two relations between the same types remain distinct through their endpoint roles.

Unlabeled endpoints inject their members directly into the class namespace. Any actual name collision is an error. Same labels on different classes are legal; colliding labels on the same class are not.

A scope does not implicitly convert to its parent. `player.team.parent` is explicit. A scope is not a first-class value that can be assigned to a variable.

### Representation is independent of lifetime

The standard representations begin with Single for an unbracketed zero-or-one child endpoint, and ArrayList, DoublyLinked, and HashedList for bracketed many-child endpoints. Every representation supports both `owns` and `refs`. A representation/cardinality mismatch is a diagnostic, not permission to silently change cardinality.

ArrayList uses an array and child index. DoublyLinked uses links at both ends and on each child. HashedList adds a hash index. All maintain backlinks and provide their operations through the same relation mechanism.

Application code cannot mutate the injected backing storage independently of the relationship operations. Such a write would bypass the very invariant the declaration establishes.

### Multiple parents

```lyric
relation DoublyLinked Graph:nodes owns [Node:graph]
relation DoublyLinked Node:outgoing owns [Edge:source]
relation DoublyLinked Node:incoming owns [Edge:target]
```

An Edge has two owning Node roles. Destroying either endpoint destroys the edge; the edge's generated cleanup removes both memberships. The two endpoints may be the same node instance without becoming the same relationship role.

### Single-child relationships

`Single` is the representation for a maintained relation with at most one child. The child endpoint is unbracketed:

```lyric
relation Single Server:connection owns Connection:server
```

The parent role exposes `server.connection.child: Connection?`; the child role exposes `connection.server.parent: Server?`. Both start null. Application code reads these endpoints but cannot independently assign the backing links.

`server.connection.set(child)` establishes both ends. Before mutation it requires that the parent's slot is empty and the child is detached through this child role. If either condition fails, it refuses and leaves both ends unchanged. In particular, setting an already-occupied slot is not a silent replacement or an idempotent success, even if it contains that same child. Other relationship roles remain independent: this rule does not prohibit multiple parents through distinct roles.

`server.connection.remove(child)` detaches both ends without destroying the child. It requires that this exact child occupies this parent's slot with the corresponding backlink; otherwise it refuses without mutation. It does not detach a child from some other parent as a side effect.

Both `set(child)` and `remove(child)` return `Result<unit>`. Success supplies unit. An occupied parent slot, an already-attached child role, or removal of the wrong membership returns a non-OK `FailedPrecondition` Status with a diagnostic string. Each refusal leaves both relationship ends unchanged. Callers handle the result explicitly or propagate it with `?`; these operations are not unchecked pointer assignments.

Generated destruction must establish the removal preconditions through the maintained relationship state. It must not ignore a failed removal and reclaim an object while stale memberships remain. An unexpected refusal during generated teardown is an invariant failure to handle under the checked-failure runtime contract, not ordinary successful cleanup.

A constructor establishes its required single-parent membership by calling the parent's `set(self)` operation and handling any refusal before successful return. Replacing a child is explicit: detach or destroy the old child, then attach the new child. These separate operations are not implicitly an atomic replacement transaction.

The `owns`/`refs` policy remains orthogonal to cardinality. Parent destruction through `owns` destroys the current child using its generated destructor; `refs` detaches it without destroying it. Child destruction removes its membership from the parent under the normal final-hook/destruction ordering. No new lifetime category, manual structural destructor, or unmaintained pointer exception is introduced.

### Many-child relationship operations

ArrayList, DoublyLinked, and HashedList expose maintained operations within the selected endpoint scopes. All supply Collection's ordinary `iter()`, `count()`, and child-side `parent()` behavior, plus RemovableCollection's explicitly safe `iter_removable()` traversal. Reading an endpoint does not authorize independent mutation of its backing fields. Both endpoint roles determine every operation; there is no ambiguous type-pair-only helper selecting an arbitrary relation.

`append(child)` on ArrayList or DoublyLinked, and `insert(child)` on HashedList, return `Result<unit>`. Successful insertion establishes the parent membership and the child's backlink together. The child must be detached in that child role. An already-attached child returns FailedPrecondition without changing either end, including an attempted duplicate attachment to the same parent. Other child roles remain independent. These operations do not silently reparent, replace, or destroy objects.

Each representation provides `remove(child) -> Result<unit>`. The exact child must belong to the receiver through the selected role with its matching backlink. Otherwise removal returns FailedPrecondition without mutation. Success detaches both ends without destroying the child. Recoverable capacity/resource refusal returns ResourceExhausted and leaves membership unchanged. Argument-evaluation effects are not rolled back. The runtime's general allocation-failure policy still requires its own contract; this does not promise recovery after every underlying allocation failure.

Generated destruction must establish removal preconditions and cannot discard refusal before freeing storage. It follows the same invariant-failure rule as Single. Constructors handle insertion failure before claiming successful attachment. Detach-then-attach is two operations, not an implicit atomic reparenting transaction.

### Representation-specific traversal and lookup

- **ArrayList:** append places the child last. Removing a nonlast child moves the last child into its position and updates that child's maintained index. Traversal visits current array order. Child-side `index` is readable while attached; detached index access is rejected when known invalid, otherwise a checked failure. Safe current-child deletion visits the moved child rather than skipping it. `first()` and `last()` return the corresponding child or null when empty.
- **DoublyLinked:** append places the child last; removal preserves the relative order of remaining children. `first()` and `last()` return optional endpoints. Child-side `next()` and `prev()` return optional neighbors in the same selected relation; a detached child has no neighbors. Traversal follows append order unless a separately specified operation changes it. Safe current-child deletion preserves continuation to the next remaining child.
- **HashedList:** the child provides `hash_key(self) -> u64`. Here the returned u64 is the actual lookup key, not permission to equate distinct application keys merely because their hashes collide. `lookup(key: u64)` returns the matching child or null. At most one child with a given key belongs to a parent in this role; insertion of a detached child whose key is occupied returns AlreadyExists without mutation. Membership preconditions are checked before the duplicate-key condition. The key must remain unchanged while attached; a key-changing operation must detach first or be rejected when it could violate this invariant. Unknown key-changing effects do not establish safety. The exact declaration/effect contract that establishes key stability remains to be specified. Traversal order is unspecified; permitted current-child deletion must neither skip nor repeat remaining children. No probing algorithm or load factor is part of this contract.

All operations retain the normal lifetime/effect and concurrent data-protection requirements. Insertion, reordering, and reparenting during an active relation traversal are rejected unless an additional explicit traversal contract permits them. For the standard iter_removable traversal, the guaranteed structural mutation is removal/destruction of the current child only; deleting peers remains forbidden. Standard iter rejects structural membership mutation by the consumer, including removal or destruction of its current child. Payload mutation is allowed when it preserves the traversal and ordinary safety requirements. Removal through an independent helper must satisfy the same active-loop obligations.

These interfaces are not permission for arbitrary user-written low-level representations to bypass memory checks. Representation implementations must preserve the stated contracts and remain within the checked or explicitly audited boundary described in Section 13.

## 10. Construction and mutation

Constructors are ordinary functions with checked postconditions. There is no `attach` clause or new construction language.

A constructor takes its owning parents and establishes their relationships before successful return:

```lyric
func Node.Node(self, graph: Graph, name: string) -> Result<unit> {
    self.name = name
    graph.nodes.append(self)?
}
```

The standard relation insertion returns Result<unit>; the constructor propagates refusal. Ignoring or explicitly discarding that result cannot satisfy the required-parent postcondition.

```lyric
func Edge.Edge(self, source: Node, target: Node) -> Result<unit> {
    source.outgoing.append(self)?
    target.incoming.append(self)?
}
```

The constructor body may initialize fields before establishing relationships. The requirement is that its returned object has all required state and both relationship ends established. A caller must not perform an additional attachment step.

A constructor can express an optional parent with a nullable parent argument. Its body must consistently establish the relationship when the argument is present. This is a constructor contract, not a blanket rule that every instance of a class must always have every possible parent.

### Fallible construction

A fallible construction expression has type `Result<ClassName>` (with the class's concrete generic arguments when applicable): successful construction supplies `Ok(object)`; failed construction cleans up the partial object and supplies `Err(problem)`, with no usable object payload. For example, `let connection = Connection(server)?` extracts the successfully constructed Connection or propagates failure through Section 8's Result rule.

The constructor body's success status is distinct from the construction expression's object result. A fallible constructor body declares `Result<unit>`:

```lyric
class Connection {
    func Connection(self, owner: Server) -> Result<unit> {
        owner.connection.set(self)?
        return ()
    }
}
```

Here the example uses the fallible Single.set operation with a Result<unit> contract. The body reports success, not `self`. `return ()`, bare `return`, or reaching the end supplies unit success. Only after checking definite initialization and required parent attachment does successful construction produce `Ok(object)` at the call site. Failure propagates Err after the partial-object cleanup below. An infallible constructor body returns unit and its construction expression supplies the object directly. An optional error return is not an alternate construction or propagation protocol.

The object is safe for generated destruction immediately after allocation. Relationship links start detached, reference fields start null, and containers start empty. Internal cleanup-safe defaults are not permission for application code to read a required field before initialization. Every completed initialization step leaves valid state for generated cleanup.

Returning an error, including propagation by `?`, automatically invokes that generated destructor. It detaches any established parent memberships, destroys already-attached owned children, releases stored value references, and frees the partial object's storage. It does **not** call the failed object's `final` hook. Fully constructed children destroyed during this cleanup run their own final hooks normally.

No separate rollback ledger or second destructor algorithm is needed. External effects not represented by initialized fields or relationships are not magically undone. Constructors must not publish a usable reference to a partial object or leave unowned work running after failure.

The compiler checks definite initialization and parent attachment on successful paths, rejects escape of an incompletely initialized object, and inserts the specified generated destruction on error paths. Construction-complete status determines whether final runs; it is not a per-field initialization ledger.

Parent add/remove operations update both ends. Invalid operations either fail before mutation or follow a documented infallible precondition. Removing an object from a relation does not, by itself, mean destroying it. Destruction and detachment are distinct operations.

## 11. Lifetime policies and generated destruction

### Ordinary classes

An ordinary `class` has explicit lifetime. Its generated `destroy()` may be called directly or through an owning relation's cascade. Holding a temporary reference does not prevent destruction. The static invalidation rules protect those references, and maintained relations protect persistent component links.

An ordinary class may be independently created or attached to owning parents. No `root` modifier is needed. Detachment does not free the object automatically or convert it to reference counting. Independent allocation still needs eventual explicit destruction; retaining an object indefinitely is a leak concern distinct from dangling-reference safety.

### Value classes

A `value class` is reference-counted and may be mutable. Copying an owning reference retains it; overwriting or dropping that reference releases it. Once no use or retention requires it to remain alive, it becomes eligible for automatic destruction. Destruction invokes its final hook and generated cleanup before freeing storage; the timing is specified below.

Ordinary code cannot call `destroy()` on a value-class instance. It releases references instead. This prevents a forced deletion from leaving other counted aliases pointing at freed storage and later attempting to release it again. Compiler-generated destruction at count zero is not a user-callable escape hatch.

The modifier does not require deep-copy assignment or immutable contents. Value-like vectors, matrices, and symbols need not record each holder as a parent merely for memory reclamation. Their chosen sharing/copy operations must still be documented.

A direct owning relation cannot forcibly cascade-delete a retained value-class child. A container holding values retains/releases them; that operation is not reinterpreted as explicit destruction. How relation-library APIs expose counted value collections should follow this distinction, without silently selecting a different lifetime policy.

### Generated releases are observable lifetime operations

When an array or field releases a value-class object, destruction at count zero has the same transitive effects as any other destruction. Those effects include its final hook and any owned-resource cleanup. The compiler cannot treat release as harmless merely because it was generated.

### Both categories

Both have generated cleanup and may have a final hook. Neither changes lifetime policy when attached, detached, passed to a function, or stored in an array. Persistent links to ordinary component objects still require maintained relationships; placing an unchecked ordinary-class pointer inside a value class is not a safety exception.

### Generated structural destruction

Application programmers do not write structural destructors. The compiler generates them from fields and relations. The destruction order is:

1. If construction completed successfully, call the object's `final` hook.
2. Remove the object from **all** parent relations, owning and weak, through the two-way remove operations. Those operations also clear the object's parent backlinks.
3. Destroy the object's strongly owned children.
4. Detach weak child relationships without destroying those children.
5. Release remaining fields and reclaim the object's storage.

Failed construction uses steps 2–5 and skips its own final hook. Parent code invoking a child's destruction must not detach that child first merely to simplify iteration: the child's final hook precedes the child's own structural teardown.

```lyric
class Connection {
    fd: i32
    final func close_resource(self) {
        close_fd(self.fd)
    }
}
```

A class has at most one final hook. Its object and fields remain allocated during the hook. Its own relationship teardown has not yet begun, but other objects reachable from it may already be partway through an enclosing cascade. The hook must obey ordinary reference-validity rules; it cannot assume every ancestor still satisfies a fully attached invariant.

Removing incoming relationships before destroying children prevents those maintained links from returning to a freed parent during the cascade. It does not erase temporary local references: static destruction analysis invalidates those. The implementation reexamines current child membership after recursive destruction, rather than preserving a stale list of pointers across callbacks or cascades.

Multiple parents, self-relations, and cyclic ownership must use these complete two-way operations. Each object must be reclaimed at most once. The ordering is decided; it still requires adversarial conformance cases rather than being declared correct merely from prose.

### Final-hook return and failure contract

A final hook returns `unit`; a declared Result or other return type is rejected. Bare return or normal completion ends the hook and continues generated structural destruction. The hook cannot veto destruction or return a recoverable failure to a cascade caller or counted-release site.

Fallible resource operations whose failures require caller decisions, such as flush or close, should be explicit ordinary methods returning `Result<unit>`. Callers invoke and handle them while the object is live. A final hook performs remaining cleanup under the resource's documented state contract; it does not retroactively turn an unsuccessful explicit operation into success.

A final hook may call fallible functions, but must handle their results locally. `?` cannot propagate from the hook because its return type is unit. Handling may report an operational failure as appropriate for the resource contract; an invariant violation invokes the checked-failure behavior. No generic promise of successful external I/O follows from finalization. Every normally returning hook proceeds to structural teardown, and checked process termination has the previously specified lack of cleanup guarantees.

### Final-hook effects and reentrancy

Final hooks may call ordinary functions. Their transitive destruction, mutation, and lock requirements are part of the generated destructor's requirements. Graph effects are permitted subject to these restrictions and ordinary reference-validity checks.

While a final hook for class C is executing, it may not call an operation whose transitive effects can destroy any instance of C. Such a call is a compile-time error, even if the programmer believes it would destroy a different instance. This deliberately uses the existing conservative class-wide destruction analysis rather than requiring instance-sensitive proof of reentrancy.

The rule includes direct destroy calls, indirect helper calls, callbacks, ownership cascades, other final hooks, and generated counted releases/cleanup that may destroy C during the hook. Infer effects through recursive call groups and generic specializations. Opaque boundaries require conservative contracts; unknown effects cannot be treated as harmless. Deferring a release cannot authorize a C finalizer to execute inside a C final hook.

The generated destructor's ordinary structural cascade is distinct from user final-hook execution. After the hook returns, generated teardown still detaches the current object from all parents before destroying its strong children, including same-class children where permitted by the maintained-graph rules. The prohibition does not accidentally ban every recursive same-class ownership structure. Generated cascade correctness and alias invalidation still require their specified checks.

A hook cannot publish a new usable alias to an object whose lifetime is ending. Destruction-in-progress state is established before invoking the hook, so any reentrant attempt reaching generated destruction is a checked invariant failure, never a second final call, double reclamation, or silently successful no-op. This defensive runtime guard does not replace the compile-time class-wide prohibition or justify accepting a program with unproved reference safety.

Class locks required by any cascade or final hook must be acquired before the destruction operation begins, as specified in Section 15.

### Value-finalizer timing

A value object is destroyed when it is no longer used, but not necessarily at the earliest possible point. Eligible objects are eventually destroyed during continuing normal runtime execution; the language provides no exact scheduling or relative-order guarantee. This freedom does not permit a runtime to retain every eligible object indefinitely.

When destruction occurs, its final hook is invoked exactly once if construction succeeded, while the object remains valid, and before storage is reclaimed. Diagnostic output from value finalizers may therefore vary between builds or optimization levels. That is unspecified timing, not undefined behavior.

Reference-count cycles may prevent eligibility. Abnormal termination does not guarantee cleanup. The exact orderly-process-shutdown guarantee remains to be specified; do not use value-finalizer timing as a substitute for explicit timely resource management.

Retain/release elision, borrowing, and moves are permitted when reference validity and observable values are preserved. They may change finalizer timing within this contract. An optimizer need not preserve one exact debugging-output order.

A deferred finalizer cannot run at an arbitrary point that bypasses static effects or class locking. Every point at which it may execute must account for its transitive destruction effects and hold all required locks. If the compiler/runtime cannot establish that, it must defer to a suitable boundary or reject the unsafe use. No concurrent background reclamation mechanism is implied by unspecified timing.

Reference-count cycles can retain memory. Leak freedom is not identical to memory safety, and a blanket ban on recursive types is not justified. No tracing collector is introduced to reclaim cycles.

## 12. Static safety and relationship iteration

### Class-wide invalidation

If a function can destroy an object of class C, calling it invalidates previously acquired local references to C.

```lyric
let node = graph.nodes.first()
prune(graph)
println(node!.name)     // rejected if prune may destroy Node
```

This conservative rule does not need to prove which particular Node was destroyed. It is deliberately simpler than general alias analysis.

Effects include explicit destruction, ownership cascades, final hooks, and releases that may reach reference-count zero. Effects propagate through ordinary calls, recursive call groups, generic specialization, callbacks, and interface implementations.

The compiler infers these effects from bodies. It must include generated bodies and cleanup paths, not just user-written statements.

### No large effect language by default

This design does not require `pure`, `reads`, or `mutates` annotations on every operation. Track destruction and storage/iteration invalidation because they are needed for safety. Additional effect categories must earn their place.

For statically selected generic implementations, the compiler can analyze the actual specialized call graph. It does not need an explicit destruction annotation on every abstract getter.

Opaque boundaries are different. Foreign functions and runtime-selected callbacks must expose conservative effect contracts, or the compiler must reject operations whose safety depends on unknown effects. The spelling of these boundary annotations is secondary to the requirement that unknown effects never mean “none.”

### Borrow validity across storage mutation

Section 5's stable-storage requirement applies to every storage borrow, not just references to ordinary class objects. Invalidation effects include potential relocation, backing-storage release, enclosing-value replacement, and active-payload lifetime changes. These effects must remain visible through calls, callbacks, generated cleanup, and specialization. Reject an operation/use combination that could access an invalidated borrow, including while a callee is running. A reference variable remaining in lexical scope after its last possible use does not by itself prohibit mutation. This requirement does not introduce exclusive mutable borrowing: multiple aliases are permitted when their uses preserve storage validity and obey mutation permissions and data synchronization.

### Track every held reference

Invalidation covers parameters, receivers, optionals, temporary expressions, references nested in local structs/tuples/enums, and captured borrows. Copying an invalid reference is not a way to restore it.

At a control-flow join, a reference is valid only if valid on all incoming paths. Reference validity must hold on every reachable loop iteration and recursive call, including effects carried across loop back edges and recursive calls. A returned reference must still be valid at the return point.

Evaluation order is defined left to right. In `f(node, destructive_call())`, the first argument is held across evaluation of the second and is invalidated if the second destroys Node. Compiler-generated temporaries are subject to the same rule.

### Reacquire through surviving structure

```lyric
prune(graph)
let current = graph.nodes.first()
if let node = current {
    println(node.name)
}
```

This is valid if graph remains valid. Reacquisition navigates maintained relationships; it does not dereference the old node to rediscover its parent.

### Persistent references are a separate obligation

Local invalidation does not clear a stale pointer hidden in an unrelated object's field. Persistent component references therefore require maintained relations. Value-like counted references follow the counted lifetime rules.

Arrays of borrowed object references are not automatically forbidden. If they are local, their elements can participate in invalidation as a group. If they escape or persist, their references need maintained links or appropriate reference counting. Distinguish lifetime obligations from a proliferation of collection types.

### Safe relationship iteration

Current-object destruction requires explicit selection of a generator declared safe. The standard relationship operations distinguish ordinary and removable traversal:

```lyric
for node in graph.nodes.iter_removable() {
    if unwanted(node) {
        node.destroy()
    }
}
```

The ordinary `for node in graph.nodes` sugar selects iter(), not iter_removable(), and rejects a consumer that may destroy its current Node. No automatic upgrade selects the more permissive traversal. A loop using a safe generator may destroy only the current object of the iterated class. It may not destroy another object of that class through an ordinary call, a cascade, a final hook, or a reference-count release.

After deletion, the iteration variable is invalid. Other existing references of that class are invalidated as usual. The compiler-controlled continuation is protected by the narrower current-object rule, not by pretending ordinary references remain valid.

The checker must infer when a helper destroys only a specified argument. A helper that deletes the current item should not require inlining by hand. An opaque helper must provide equivalent evidence through its contract.

Representation-specific continuation matters:

- A removable doubly linked traversal must retain a valid continuation even after current-item deletion; a saved successor is valid only if permitted operations cannot destroy or invalidate it.
- Swap-removal arrays must visit the element moved into the removed slot.
- Hashed collections need a continuation contract that survives the permitted removal.

Deleting the current Node is not safe if it cascades to another Node. Deleting incident Edges may be safe because those have a different class, provided their cleanup cannot destroy another Node.

Insertion, reparenting, and reordering require their own iterator-validity rules. Initially, reject mutations not covered by the representation's safe traversal contract. Do not infer that a deletion rule permits arbitrary collection mutation.

Nested loops must satisfy every active loop's obligations. A safe inner traversal does not authorize destruction of an outer ordinary traversal's current object. The current-only exception protects only continuation whose validity is established; arbitrary saved same-class references remain subject to ordinary class-wide invalidation.

### Cross-call safety requirements

Owner provenance and transitive effects must remain available inside a callee, not merely invalidate references after it returns. The following schematic program must be rejected when `value` refers into `node`:

```lyric
func update(value: ref i32, node: Node) {
    node.destroy()
    value = 7
}

update(ref node.count, node)
```

The scalar type `ref i32` does not erase its Node owner. The checker must preserve owner-lifetime obligations across overlapping arguments, nested calls, callbacks, recursive functions, and generic specializations. It must reject accesses through invalidated references inside a callee as well as after the call returns. Calls that may release counted values or perform generated cleanup must be covered too. The algorithm used to establish these facts belongs in a separate implementation design. Any remaining uncertainty about which programs are accepted or rejected is a specification question; the absence of a chosen analysis procedure is not.

## 13. Interfaces, capabilities, and implementation bindings

A multi-class interface describes a contract among several participants. It is not an inheritance hierarchy.

A minimal method-based example is:

```lyric
interface Endpoint<E, N> {
    pub func E.target(self) -> N
}
```

It can be bound explicitly:

```lyric
impl Endpoint<Edge, Node> {
    E.target = Edge.target_node
}
```

A complete binding must supply every required operation, either explicitly, through an exact unambiguous match, or through a default body. Missing members are errors. Two applicable explicit implementations are ambiguous, not an invitation to pick the last one registered.

### Structural satisfaction

An interface can be satisfied without an explicit `impl` declaration. For the requested participant types and endpoint roles, resolve satisfaction as follows:

1. Use an applicable explicit implementation to select the intended bindings. Multiple applicable explicit implementations are an ambiguity error; declaration order does not decide.
2. In the absence of an explicit implementation, match requirements against accessible existing members and maintained relations by exact name, resolved type/signature, and endpoint role. Each required binding must be unambiguous. A field may satisfy a getter requirement under the ordinary field-binding rules, but a getter alone does not supply writable storage.
3. Use interface default bodies for otherwise unsupplied operations where the interface defines them. All requirements used by those defaults must themselves be satisfied, and all participant/role bindings must reach the specialized body.
4. Diagnose missing or ambiguous requirements. Do not guess by fuzzy names, implicit widening, declaration order, or receiver type alone.

```lyric
interface Named<T> {
    pub func T.name(self) -> string
}

class Widget {
    pub func name(self) -> string { return "widget" }
}

// Widget satisfies Named without a registration declaration.
// An optional checked assertion of the same conformance is:
impl Named<Widget> { }
```

An empty impl is a checked assertion of conformance using the same derivation/default rules, not mandatory registration or permission to accept missing members. Mixed impls supply selected explicit mappings and derive the remaining requirements. Explicit mappings can adapt different concrete names or choose particular relation roles; they do not waive type, visibility, lifetime, or effect checks.

Structural satisfaction inspects existing capabilities. It does not inject fields, create relationships, or change destruction policy merely because a generic algorithm is used. Such changes require an explicit relation or storage-providing implementation declaration. Signature satisfaction is not proof that an implementation preserves behavioral invariants such as both-end relationship maintenance.

### Interface-qualified calls and ambiguity

A short interface method call is accepted only when the receiver, explicit type arguments if supplied, ordinary arguments, and applicable constraints determine one binding. The receiver's type alone must not silently select the other participants. If multiple bindings remain, compilation fails with the candidate bindings and a suggested qualified call.

Interface qualification explicitly selects the contract and its participants:

```lyric
// Given bindings for both graph interpretations:
// DirectedGraph<Network, Person, Follow>
// DirectedGraph<Network, City, Road>

DirectedGraph<Network, Person, Follow>.edge_count(network)
DirectedGraph<Network, City, Road>.edge_count(network)
// network.edge_count() is ambiguous when both remain applicable.
```

For an instance method, the qualified call supplies its receiver as the first ordinary argument. Static interface operations have no receiver argument. Qualification selects a compile-time binding; it allocates no adapter and performs no runtime interface conversion. The selected implementation, all participant types, and all relation-role mappings accompany specialization and effect checking.

Qualification uses the same satisfaction rules as an unqualified call and can select a structural binding without an explicit impl. It does not bypass visibility, type, lifetime, or effect requirements. Conflicting explicit implementations of the same fully specified binding remain an error; spelling the interface explicitly does not resolve that conflict. Declaration order never breaks ties.

Default algorithms belong to the same interface contract and use its abstract operations. Their definitions can be separated from declarations like other functions. All participant bindings must reach the specialized body; the receiver's type alone is insufficient.

### Abstract relations and Collection

Abstract relations declare required relationship capabilities without injecting concrete storage:

```lyric
interface DirectedGraph<G, N, E> {
    relation Collection G:nodes [N:graph]
    relation Collection N:outgoing [E:source]
    relation Collection N:incoming [E:target]

    pub func G.edge_count(self) -> u64 {
        var count: u64 = 0
        for node in self.nodes {
            for edge in node.outgoing {
                count = count + 1
            }
        }
        return count
    }
}
```

This compact example includes its default body. A shared-declaration `.ly` file may omit the body and a functional `.ly` file supply it, under the ordinary declaration/body matching rules. This is source organization, not a separate declaration-only language mode.

`Collection` is the ordinary declared capability for traversing a maintained parent/children relationship:

```lyric
interface Collection<P, C> {
    pub func P.iter(self) -> gen C
    pub func P.count(self) -> i32
    pub func C.parent(self) -> P?
}
```

All three operations refer to the same bound endpoint roles, not merely matching parent and child types:

- `iter()` visits the children belonging to the receiver through that relationship. It is a loop-only generator under Section 6, not an independently resumable iterator object.
- `count()` returns the current number of children in that relationship. This contract does not promise constant-time counting.
- `parent()` returns the child's parent through that relationship, or null when detached. A maintained parent field may satisfy this getter under the ordinary binding rules.
- Ordinary iter() does not permit consumer destruction of the current child. Standard relationship iterators additionally reject consumer structural membership mutation. Use the explicit safe traversal capability below when current-item removal/destruction is required; signature matching alone does not prove traversal correctness.
- Traversal order is defined by the representation. An algorithm constrained only by Collection cannot assume insertion, sorted, or any other particular order.

The capability requires no append/remove operations and no particular storage layout. Both owning and weak relationships can satisfy it. Required lifetime and lock checks remain in force throughout access and traversal.

For a relation scope satisfying this capability, `for child in owner.children` invokes that scope's `iter()`, and `len(owner.children)` invokes its `count()`. These are compile-time scope operations; the scope itself remains non-first-class. They select the complete relation binding, including both endpoint roles. The compiler does not infer capabilities from the identifier `Collection` alone: satisfaction requires the visible declarations and their contract.

Deletion-capable generic algorithms require the ordinary interface extension:

```lyric
interface RemovableCollection<P, C> extends Collection<P, C> {
    pub safe func P.iter_removable(self) -> gen C
}
```

This adds one explicitly safe generator. Its consumer may remove or destroy the current child under Section 12; the continuation must preserve traversal of remaining children without skipping or repeating them under the representation's order contract. ArrayList, DoublyLinked, and HashedList supply both operations. A generic algorithm constrained only by Collection cannot assume the removable operation exists or silently replace iter() with it. An abstract relation can require RemovableCollection instead of Collection when its algorithms need that behavior; the same whole-relation and endpoint-role bindings apply.

Safe traversal may require additional continuation work relative to ordinary traversal. Callers choose explicitly; no universal performance difference or measured overhead is promised. The safe modifier declares the destruction permission; this interface additionally specifies maintained current-child removal behavior, not permission for arbitrary mutations in every safe generator.

An abstract relation without `owns` or `refs` accepts either destruction policy when the algorithm only needs traversal. An algorithm requiring owning behavior declares that requirement explicitly.

Concrete bindings map whole relations:

```lyric
impl DirectedGraph<Net, Route, Via> {
    G:nodes/N:graph = Net:routes/Route:net
    N:outgoing/E:source = Route:a/Via:a_source
    N:incoming/E:target = Route:b/Via:b_target
}
```

Each line binds both endpoint roles together. It does not copy state or create a second relationship. This is the important semantic distinction: identical types with different roles are different bindings.

Per-member bindings remain available for nonrelational operations or adapters. There is no requirement to write five forwarding methods when one relationship binding already conveys the meaning.

### Endpoint labels in constraints and qualified calls

A participating type argument may carry an endpoint label in a constraint, just as in an impl. Both endpoint roles are part of the binding identity:

```lyric
func outgoing_count<N, E>(node: N) -> i32 where Collection<N:outgoing, E:source> {
    return Collection<N:outgoing, E:source>.count(node)
}
```

The constraint selects the parent role `outgoing` on N and child role `source` on E. Exact existing roles can satisfy it structurally. Different concrete role names require an explicit binding mapping abstract roles to concrete roles; neither type coincidence nor a unique-looking unrelated relation authorizes guessing that mapping.

Omitting a label selects the unlabeled, flat scope. It is not a wildcard search across all relationships on the participating class. A label selects a compile-time scope, not a runtime argument or adapter object. Distinct labels on the same participating types remain distinct bindings throughout inference, satisfaction, specialization, and effect checking.

Interface-qualified calls accept the same labeled type arguments:

```lyric
Collection<Node:outgoing, Edge:source>.count(node)
```

All participant types and labels must select one valid binding under the ordinary rules. Whole-relation mappings in an enclosing interface impl carry both endpoint roles into its algorithms; per-member mappings must remain consistent with the selected binding. A constraint checks existing capabilities and never creates relation storage.

An interface's type parameters are in scope for all its member declarations and default bodies. A generic class's parameters are in scope for its fields and methods. Type-variable scope must be explicit across separately declared implementations.

### Storage-providing interfaces and relation hints

A storage hint is an interface that supplies endpoint fields, operations, and cleanup templates. There is no separate `hint interface` keyword.

```lyric
interface ArrayList<P, C> {
    field P.children: [C]
    field C.parent: P?
    field C.index: i32

    pub func P.append(self, child: C) -> Result<unit>
    pub func P.remove(self, child: C) -> Result<unit>
}
```

A relation binds both type variables and their labels. Members on P are injected into the parent role; members on C into the child role.

A parent-side append operation must be able to update the child's backlink through its child argument. The correct rule is that each access uses the right instance and the same relation binding, not a blanket prohibition on cross-side updates.

Ownership-sensitive cleanup templates distinguish `owns` from `refs`. Application classes still do not handwrite structural destructors.

The compiler checks hint shape, types, roles, and effects. That does not prove an arbitrary implementation maintains its data-structure invariants. Low-level representation code belongs to a small audited boundary. Merely spelling a function `trusted` must not grant arbitrary application code permission to bypass safety.

Capability requirements inspect an existing implementation. They do not silently add fields when an unrelated algorithm is called. Storage injection happens at the explicit relation or storage-implementation declaration.

### Generic classes and partial impls

```lyric
value class Box<T> {
    value: T
}

// Given an existing Container<B, T> contract satisfied by Box<T>:
impl<T> Container<Box<T>, T> { }
```

The impl's explicit type variables remain open and specialize at concrete use sites. Concrete type arguments, endpoint labels, and implementation identity all belong in the specialization key. Reusing a specialization with the same receiver but different other participants is incorrect.

The class category is part of generic checking: a value class instantiated with stored ordinary-class references cannot silently evade the relationship/lifetime obligations. This example describes generic syntax, not a waiver for every possible T.

### Method, field, and inline bindings

For a declared interface `Printable<P>` requiring `P.text(self) -> string`, an impl may bind the abstract member to an existing concrete operation. These are alternative examples, not simultaneous implementations:

```lyric
impl Printable<Widget> {
    P.text = Widget.text
}
```

Alternatively, given a Widget with an accessible string field `name`, the impl may define an adapter inline:

```lyric
impl Printable<Widget> {
    P.text = (self) -> string { return self.name }
}
```

For storage-providing interfaces, `<->` binds an abstract field to existing concrete storage:

```lyric
impl ArrayList<Team:roster, Player:team> {
    P.children <-> Team.members
    C.parent <-> Player.team_parent
    C.index <-> Player.team_index
}
```

A field binding is not a second copy or a synchronization callback. Read/write permission, types, and lifetime category must match. A getter alone cannot satisfy a writable field requirement.

An empty impl is an optional checked assertion requesting exact unambiguous derivation of remaining requirements; structurally matching types do not need one merely to satisfy a capability. Mixed-mode impls explicitly bind some members and derive the rest under Section 13's precedence rules. Diagnostics list every unsatisfied requirement and its expected signature. No fuzzy names, arbitrary widening, or declaration-order selection resolves ambiguity.

### Ownership-bearing impls and cleanup templates

The underlying storage-binding form remains available:

```lyric
impl ArrayList<Team:roster, Player:team> owns { }
```

A relation is the convenient spelling for the corresponding labeled, ownership-bearing binding. Its shape must satisfy the relation rules. A relation that names an unknown or malformed hint is an error, not a silently omitted declaration.

Cleanup templates use the following explicit spelling:

```lyric
interface ExampleRelation<P, C> {
    destructor owns P { ... }
    destructor owns C { ... }
    destructor refs P { ... }
    destructor refs C { ... }
}
```

The ellipses indicate omitted library implementation, not executable syntax. The modifier selects the matching pair. Application classes do not copy these blocks into handwritten destructors. The old bare `destructor P` shorthand should be either documented as `owns` or rejected consistently; it is not needed in new examples.

### Composition, defaults, and static requirements

`extends Parent<...>` substitutes the parent's participants and carries its requirements and defaults. Same-signature override is explicit in the child declaration; incompatible signatures are errors. Initial composition is single-parent. Runtime subtype conversion is not implied.

An interface can require a static operation by omitting `self`, such as `T.zero()`. Calls on a type variable resolve through its bound capability. Numeric capabilities must list their supported operations and types; they must not be recognized only through a suggestive name.

The old `implements` clause is a declaration of conformance, not evidence of it. If retained, it must be checked using the same satisfaction rules as impls. Whether it remains useful alongside explicit impls is a simplification decision, not permission to leave an unchecked assertion in the language.

There is no separate constraint-alias mechanism in the initial design. Functions state their interface requirements directly. This chooses interface defaults and single-parent composition rather than maintaining two competing ways to name capability bundles. A shorthand can be considered if actual use demonstrates a need.

### Standard generic capabilities

The standard library declares these capabilities explicitly; their names alone do not confer privileged satisfaction. The compiler supplies conforming implementations for the listed primitive types and must apply the same signatures, effects, and behavior as ordinary implementations.

```lyric
interface Equatable<T> {
    pub func T.equals(self, other: T) -> bool
}

interface Comparable<T> extends Equatable<T> {
    pub func T.less(self, other: T) -> bool
}

interface Hashable<T> extends Equatable<T> {
    pub func T.get_hash(self) -> u64
}

interface Numeric<T> {
    pub func T.zero() -> T
    pub func T.one() -> T
    pub func T.add(self, other: T) -> T
    pub func T.mul(self, other: T) -> T
}
```

Numeric covers i8/i16/i32/i64, u8/u16/u32/u64, f32/f64. Its factories return zero and one in T; add/mul have the same checked, wrapping-in-unsafe-mode, or IEEE behavior as the corresponding ordinary operators in Section 14. The capability does not silently widen T or promise algebraic identities that floating-point rounding or checked overflow would violate. User-defined conforming types may also supply the capability.

Standard Equatable implementations cover those numeric types, bool, string (therefore [u8]), and Sym. Numeric equality follows Section 14, including NaN inequality; string equality compares bytes and symbol equality compares interned names. Comparable initially covers integers, bool (false before true), and byte arrays/string (lexicographic unsigned-byte order). It requires a total order consistent with equals. Floating-point types do not receive this total-order capability by default; IEEE comparisons remain available without it.

Standard Hashable implementations cover integers, bool, byte arrays/string, and Sym. Transparent aliasing means string and [u8] have the same equality, ordering, and hashing operations; no nominal string-only implementation is selected. Equal keys must have equal hashes; equal hashes need not mean equal keys. A dictionary always resolves collisions using equals. For dictionary keys, equals must be a stable equivalence relation and hashing/equality must remain stable while stored. Floats are not standard hash keys, avoiding an implicit policy for NaN keys. A user-defined key type needs a contract ensuring these laws and key stability; method signatures alone cannot prove them. Mutation or opaque effects that could invalidate stored-key identity are not accepted as harmless.

The capability methods do not authorize general user-defined operator overloading. Generic code can call the declared operations explicitly. Writable reference compatibility remains invariant in storage type. Custom callbacks and capability methods retain all ordinary lifetime, effect, and concurrency obligations.

## 14. Numeric operations and conversions

Operands evaluate left to right. Numeric operations specify mathematical values or fixed-width bits explicitly; emitted C must preserve those semantics in safe and unsafe builds.

### Evaluation and compound assignment

Section 3 contains the precedence table. Operands and call arguments evaluate left to right; logical operators short-circuit.

Compound assignment evaluates its destination once. If that destination is an array slot, its storage borrow remains needed across right-hand evaluation; an operation that could relocate, free, or invalidate it is rejected. A lowering must preserve that stable-storage guarantee.

### Integer arithmetic and explicit wrapping

In safe builds, ordinary integer addition, subtraction, multiplication, and negation are checked against the result type's representable range. Statically known overflow is a compile-time error; dynamic overflow produces a checked programming failure under Section 8. Signed and unsigned arithmetic follow this rule. Debug and optimized safe builds have the same semantics. The compiler may remove checks proved redundant, but may not assume overflow cannot occur merely because the C backend would otherwise give it undefined behavior.

Intentional modular arithmetic uses wrapping operators rather than method-call ceremony:

```lyric
let total = a + b       // checked addition
var hash = a !+ b      // wrapping addition
var prior = a !- b     // wrapping subtraction
var mixed = a !* b     // wrapping multiplication
hash !+= contribution
prior !-= amount
mixed !*= factor
```

Wrapping arithmetic computes modulo 2^N for an N-bit integer type, interpreting the resulting bits in that type. `!+`, `!-`, and `!*` have the precedence and associativity of `+`, `-`, and `*` respectively. Their compound assignments evaluate the destination once and obey ordinary reference/lifetime rules. These explicit infix and assignment forms are distinct tokens; they do not change postfix optional unwrap.

Prefix `!-value` is wrapping integer negation in every mode. It retains the operand's type and width and computes zero minus the value modulo 2^N. The minimum signed value therefore negates to itself; for unsigned values it computes the modular additive inverse. Prefix `!-` has the precedence of ordinary unary minus. The parser distinguishes prefix negation from infix wrapping subtraction by position, just as for ordinary `-`. It is not logical-not applied to a negation and does not introduce a floating-point wrapping operation.

```lyric
let opposite = -value  // checked integer negation in safe mode
let wrapped = !-value  // modular negation in every mode
```

Numeric conversions, integer comparisons, shifts, division, and remainder are specified in this section.

Explicit unsafe mode disables automatic integer-overflow checks as well as the separately specified bounds-check opt-out. Ordinary integer addition, subtraction, multiplication, and negation then wrap modulo 2^N for an N-bit result type, with the resulting bits interpreted in that type. These operations have the same modular results as explicit wrapping arithmetic. Their result is defined even for signed overflow: the optimizer may not assume that overflow never occurs. The backend must implement these semantics deliberately rather than emit undefined signed C overflow and inherit its consequences.

Unchecked bounds access remains outside the safe-language guarantee, and unsafe execution must be identified in benchmark results. Defined arithmetic wraparound does not restore memory safety to unchecked indexing. Shifts normalize counts in unsafe mode as specified below; division and remainder retain zero-divisor checks. Explicit wrapping operators retain their defined modular meaning in every mode.

### Implicit integer conversions and mixed arithmetic

An already-typed integer value converts implicitly to another integer type only when the destination's representable range contains the source type's entire range. This is a type-level rule, not a value-dependent guess: u8 to u32, i16 to i64, and u32 to i64 are allowed; i32 to u32, u32 to i32, and i64 to i32 require explicit conversion. Transparent aliases retain their underlying type's rules.

Integer literals remain context-sensitive. A literal may take an expected integer type when its value is representable, so `let byte: u8 = 12` is valid. This does not authorize narrowing an already-typed variable merely because its current value happens to fit. Without a determining context, the existing i32 literal default applies.

For ordinary binary integer arithmetic, equal operand types retain that type. If the types differ and one operand's type can represent the entire range of the other, convert the latter to the former and perform the operation in that type. Otherwise reject the expression and require explicit conversion. The compiler does not invent a third wider type or let an assignment's destination silently widen an already-typed arithmetic operation.

For example, i64 plus u32 uses i64; i32 plus u32 is rejected even though both ranges fit in i64. To choose that calculation width, explicitly convert the operands to i64. Result overflow is checked or wrapped in the selected operation type under the integer arithmetic rules. The wrapping addition/subtraction/multiplication forms use the same operand-type selection as their ordinary counterparts.

Shift counts are the specified exception: the count may have a different integer type from the left operand, and never changes the result width. Implicit integer/float conversion is not permitted, as specified in the floating-point conversion rules. Mixed integer comparisons and explicit integer casts are specified below. Numeric widening does not supply writable reference compatibility: a ref u8 cannot become ref u32, because that would describe different storage. Nor does this rule authorize element-wise conversion of array storage or numeric conversion searches to disambiguate tagged alternatives.

### Mixed integer comparisons

Integer `==`, `!=`, `<`, `<=`, `>`, and `>=` compare mathematical values across signedness and widths. The result is bool; no common numeric result type or explicit cast is required. This rule applies even when neither operand type contains the other's entire range, such as i64 versus u64. Safe and unsafe modes have identical comparison semantics.

A negative signed value is less than every unsigned value and is never equal to one. Nonnegative signed values compare with unsigned values by magnitude, without truncation or conversion to a type that cannot represent them. For example, signed -1 compares less than unsigned 1, never as a large positive integer.

Operands evaluate once, left to right, under ordinary lifetime/effect rules. The backend must implement value-correct comparison rather than inherit C's usual mixed signed/unsigned conversions. A sign test followed by a suitable magnitude comparison can handle the mixed case without requiring a wider integer type. This specifies behavior, not a tested lowering implementation. Floating-point comparisons and integer/float mixtures are separate contracts.

### Explicit integer conversions

`value as Type` converts an integer value to the named integer type. The right operand is a type expression, including a declared generic type parameter, not a value expression whose type is inferred as the target. Expression-derived targets and a typeof facility are not part of the initial language.

In safe mode, `as` preserves the mathematical value when it is representable in the destination type. A statically known out-of-range conversion is a compile-time error; a dynamic out-of-range conversion produces checked failure. In unsafe mode, integer `as` converts modulo 2^N for the N-bit destination and interprets the resulting bits in its signed or unsigned type.

`value !as Type` explicitly requests that same modulo-width conversion in every mode, including safe mode. It is appropriate for intentional truncation or signedness reinterpretation. It does not report overflow merely because bits are discarded. Widening a negative integer under modular conversion uses its mathematical value modulo the destination width, not zero-extension of the source bit pattern.

```lyric
let small = large as u8       // checked in safe mode
let low_byte = hash !as u8    // explicitly modulo 256
```

Evaluate the converted operand once. The target type expression is resolved statically and has no runtime evaluation. `as` and `!as` have the same cast precedence and association; `!as` is the wrapping-cast operator, not a postfix unwrap followed by a cast. Parenthesize an optional unwrap when needed, for example `(optional!) as u8`. No `!as` wrapping behavior is implied for floating-point, reference, or arbitrary class conversions.

The C backend must preserve the defined result for every accepted integer conversion without relying on implementation-defined out-of-range signed casts. Integer casts do not manufacture object references or bypass reference/lifetime checks. Generic casts are valid only when the actual constrained/specialized operand and target types support the operation.

### Integer shifts

Shifts manipulate fixed-width bit patterns rather than perform checked multiplication or division. For an N-bit left operand, the result has the same integer type and width:

- `<<` discards bits shifted beyond the width and fills low bits with zeros, for signed and unsigned operands. Discarded bits do not cause arithmetic-overflow failure.
- `>>` on unsigned operands fills high bits with zeros.
- `>>` on signed operands replicates the sign bit (arithmetic right shift).

The shift count must be an integer. Safe mode requires `0 <= count < N`; a statically known invalid count is a diagnostic, and a dynamic invalid count produces checked failure. Unsafe mode normalizes the count to the unique value in `[0, N)` congruent to it modulo N, including negative counts. The lowering explicitly performs this normalization rather than relying on target-specific instruction masking. Operand evaluation remains left to right and occurs once.

```lyric
// Division and arithmetic right shift intentionally differ for negatives:
assert_eq(-7 / 2, -3)
assert_eq(-7 >> 1, -4)
```

The backend must preserve these semantics without invoking C undefined behavior or depending on implementation-defined signed shifts. Left shift operates on an unsigned representation of the required width; small integer promotions must not accidentally turn it into overflowing signed arithmetic. Signed right shift must preserve sign extension explicitly where the backend's guarantees do not establish it. Reinterpretation of result bits must preserve the source signed type's defined representation, not rely on an unchecked out-of-range C conversion. Zero-count paths must not construct an auxiliary shift by N. These are lowering obligations, not evidence that a shift implementation has been tested.

### Integer division and remainder

Integer division truncates toward zero. A nonzero remainder has the dividend's sign: `-7 / 3` is -2 and `-7 % 3` is -1. Division or remainder by zero produces a checked programming failure in both safe and unsafe modes; the absence of a meaningful modular result makes this an explicit retained check in unsafe mode.

For signed minimum divided by -1, safe division produces checked overflow failure; unsafe division returns the signed minimum by modulo-width wraparound. The remainder for signed minimum and -1 is zero in either mode. These outcomes must be implemented before executing a backend instruction or C operation that has undefined behavior for the exceptional operands.

A C11 lowering can first reject a zero divisor, then handle the signed-minimum/-1 pair explicitly, and only then evaluate C `/` or `%`. Both C operators require this guard, not just division. The ordinary defined C cases provide truncation toward zero and the required remainder sign. The failure branches call the runtime's non-returning checked-failure diagnostic; they do not return a fake arithmetic value.

A standalone 64-bit C probe of this guarded arithmetic recipe passed 271 checks each under Apple Clang 21 on ARM64 at -O0, -O3, and -O3 with undefined-behavior sanitization. It checked failure classification rather than executing Lyric's not-yet-implemented fatal diagnostic. The local `gcc` executable is also Apple Clang, so this is not independent GNU GCC or cross-target verification. The test's wider-integer oracle is not required by the lowering recipe.

Overflow checks have a real potential cost, especially for signed arithmetic; no zero-cost claim is made. Safe default behavior and explicit wrapping/unsafe choices make that tradeoff visible rather than dependent on optimization level.

### Floating-point values and arithmetic

`f32` and `f64` use IEEE 754 binary32 and binary64 behavior respectively. Ordinary floating-point arithmetic does not use the integer-overflow failure rule: overflow produces appropriately signed infinity, invalid operations such as 0.0/0.0 produce NaN, and underflow follows IEEE rounding with subnormal values rather than silently flushing them to zero. Positive and negative zero compare equal.

NaN compares unequal to every value, including itself. Equality with NaN is false, inequality is true, and ordered comparisons involving NaN are false. These rules apply in safe and unsafe modes; disabling integer/bounds checks does not authorize different floating-point values.

The backend must not silently enable fast-math assumptions that discard NaNs, infinities, signed zero, or subnormal behavior. C is a suitable lowering language on conforming supported targets, but portable C alone does not promise these properties. A C backend must verify that its float/double representations match binary32/binary64 and select compiler/runtime settings preserving the specified behavior. Targets or configurations that cannot provide it must be rejected or use an explicitly specified conforming implementation, not silently change the language.

### Implicit floating-point conversions

`f32` converts implicitly to `f64`. Finite binary32 values are exactly representable in binary64; widening preserves infinities and signed zero and preserves NaN classification without promising preservation of a particular NaN payload encoding. Mixed f32/f64 arithmetic and comparisons widen the f32 operand to f64 and use f64 operations.

Conversions between integer and floating-point types require an explicit cast in either direction, even when a particular value or a particular source integer type could be represented exactly. Mixed integer/float arithmetic and comparisons are rejected until the programmer explicitly chooses a conversion. This is a simple category boundary, not an attempt to infer whether the current value happens to fit. Likewise f64-to-f32 narrowing requires an explicit cast.

```lyric
let wider: f64 = narrow_f32
let approximate = count as f64
let integer = measurement as i64
```

These examples specify when conversion must be explicit; the rounding and failure rules follow below. Ordinary literal contextual typing remains distinct from converting an already-typed integer variable. No implicit conversion changes writable reference or array-storage types.

### Explicit floating-point conversions

An explicit integer-to-float `as` conversion rounds to the destination floating-point precision. Low-order precision loss is permitted; exact representation is not required. Explicit f64-to-f32 conversion likewise rounds to f32, allowing sufficiently large finite magnitudes to become signed infinity and sufficiently small magnitudes to become subnormal or zero. Existing infinities and NaN classification are preserved where applicable; no particular NaN payload-bit mapping is promised. Detailed rounding-mode policy follows the floating-point environment contract rather than introducing a separate per-cast mode here.

A float-to-integer `as` conversion truncates toward zero, then requires that the resulting mathematical integer be representable in the destination type. For example, `3.9 as i32` is 3 and `-3.9 as i32` is -3. NaN, infinity, or an unrepresentable truncated result produces checked failure. A statically known invalid conversion is a compile-time diagnostic.

These float-to-integer checks remain enabled in unsafe mode. They are an explicit exception to the integer-overflow/bounds opt-out, like zero-divisor checks. `!as` is restricted to integer-to-integer conversion; it does not define floating-point wrapping, saturation, or reinterpretation.

The C backend must establish finiteness and the representable range of the truncated value before executing a float-to-integer cast. It may not cast first and inspect the result afterward, or rely on an out-of-range C conversion. Boundary tests must include destination extrema, nearby fractional values, NaN, infinities, and the fact that converting a large integer bound to a float can round that bound outward. A naive comparison against a rounded integer maximum is not sufficient. These are required lowering/conformance cases, not a claim that a float-cast probe has been run.

### Initial C behavior and preferred contraction implementation

Initially, Lyric may use the selected C compiler's ordinary floating-point contraction and intermediate-evaluation behavior on a supported IEEE target. The toolchain must record the compiler/version and relevant floating-point options so this behavior can be reproduced. It does not initially promise identical last-bit results across compilers, optimization settings, or targets. In particular, `a * b + c` may be fused when the selected C configuration permits it. This is an explicit initial implementation allowance, not a claim that separate and fused evaluation are numerically identical.

The preferred later implementation is controlled contraction: preserve separately written operations by disabling implicit multiply-add contraction, and expose explicit `fma(a, b, c)` for one-rounding fused semantics. A C backend can map binary32/binary64 operations to C's `fmaf`/`fma` facilities and use compiler options such as `-ffp-contract=off` for ordinary expressions. Hardware acceleration is target-dependent; a library implementation may provide the semantics. This is a preferred implementation direction, not a tested or required initial intrinsic/API.

The initial allowance does not authorize general fast-math assumptions that violate the NaN, infinity, signed-zero, or subnormal requirements above. Detailed rounding-mode exposure and excess-precision policy remain to be specified. No initial cross-target bit-identical evaluation or local floating-point lowering verification is claimed. Explicit floating-point/integer casts, implicit conversions, and mixed operations follow the preceding conversion rules.

## 15. Concurrency, class access, and storage

### Memory-safety guarantee and its limits

Safe Lyric must prevent concurrent operations from violating memory validity. Accepted safe programs must not perform use-after-free, out-of-bounds access, double-free, uninitialized storage reads, or accesses through corrupted or incompatible references because of thread interleaving. A checked failure must stop the operation before an invalid memory access under Section 8's failure rules. This requirement covers generated code and runtime operations as well as source-level accesses.

Memory safety does not guarantee atomicity of multi-operation application logic, freedom from deadlock, deterministic results, or general concurrent application correctness. For example, separately valid atomic reads and writes may still lose an application-level update. Such a logical race is distinct from an unprotected memory access. Progress obligations stated elsewhere in this chapter still apply; the absence of a general deadlock-freedom guarantee does not waive them.

Class lifetime/allocation locks are necessary protection in this design, but are not alone sufficient for this guarantee. Payload accesses, dynamic-array storage, counted references, maintained relationships, and publication of initialized objects also require enforceable safety contracts. The compiler must reject safe-language operations whose required protection cannot be established; unchecked programmer discipline is not a substitute. The precise permitted mechanisms and source contracts remain open where identified below. Until those contracts are complete and enforced, the class-lock design alone does not establish memory safety for the full concurrent language.

The backend must preserve these requirements. In particular, ordinary conflicting non-atomic C accesses without the required synchronization can constitute a C data race with undefined behavior; they cannot be emitted as supposedly harmless nondeterminism in safe Lyric. Permitted concurrent operations must lower to defined accesses under their synchronization, atomicity, or confinement contracts. Explicit unchecked operations and foreign code that violates its declared contracts are outside the safe-language guarantee, not exceptions silently admitted within it.

### One per-class lifetime and allocation lock

The agreed starting point is one reader–writer lock per class, aligned with its allocation storage. It protects object lifetimes and allocator state, not every shared payload mutation:

- **Shared access** permits obtaining and using references to existing objects of the class. Multiple threads may hold shared access simultaneously. None may create or destroy an instance of that class while holding only shared access.
- **Exclusive access** permits creating, using, and destroying objects of the class. It excludes other threads' shared and exclusive access for the duration of the protected region.

“Reader” describes lifetime access, not field immutability. Shared lifetime access may accompany field writes when separate data-synchronization rules permit them. It does not by itself make conflicting reads and writes safe.

Creation and destruction both require exclusive class access. Allocation/free-list operations, slot reuse, allocator metadata growth, and SoA backing-storage updates occur under that protection. Generated allocator helpers therefore need no additional per-class allocator mutex or recursive locking: they require the caller's exclusive access and perform ordinary allocator-state updates within it. Create/delete are thread-safe complete operations because protection is established around them, not because each internal helper locks independently. Construction failure cleanup obeys the same access requirements.

Different classes have distinct locks. Generic specializations with distinct allocation storage have distinct lock identities; if an implementation shares allocator storage, it must also share its protection. The same physical mutable allocator state must never be accessed under unrelated locks.

This intentionally serializes creation against all existing users of the class, even though stable addresses mean creation alone need not invalidate their references. A region creating Nodes needs exclusive Node access, just like a region destroying Nodes. The simplicity trades away allocation/read concurrency; no throughput or latency benefit is claimed without measurement. Separating allocator synchronization later would be a deliberate design revision, not an implicit second lock in this model.

### Payload and relationship synchronization is separate

Shared lifetime access prevents another thread from deleting an object; it does not prevent other shared holders from mutating it. Shared mutable fields, relationship links, and object-owned dynamic arrays need independently checked data-synchronization contracts, such as guarded-by/must-hold rules, confinement, or supported atomic operations. The exact guard syntax and allowed mechanisms remain to be specified. Unknown protection is not permission to accept a data race.

A relationship update must preserve both ends under the relevant data guards as well as hold the required endpoint lifetime access. Following a relationship to acquire a reference must protect the relationship read too. Borrow acquisition and the entire usable lifetime of an array-element or range borrow must be protected against concurrent resize/replacement that could invalidate its storage. Stable SoA property storage does not make ordinary owned-array backing stable. For example, one thread may resolve and bounds-check an element while another resizes the array and frees its old backing before the first thread accesses it. Shared lifetime access by both threads keeps the owner alive but does not make this sequence safe. The required data-protection contract must prevent invalidation from acquisition through the borrow's last possible use, not merely make the initial base-pointer load atomic. Single-threaded static checks do not by themselves exclude another thread's relocation.

Value classes follow the same lifetime/allocation modes. Concurrent counted retains/releases still require a safe count-update protocol: shared lifetime access alone does not serialize count changes. Any release that may run destruction requires exclusive access for the affected classes, or a specified deferred-finalization boundary that obtains it. Whether count updates use atomics or another checked protocol remains runtime design; no unsynchronized count updates or implicit shared-to-exclusive upgrade are authorized. An owning counted handle may preserve an object's lifetime across access regions, but it does not authorize unprotected mutable-payload access.

Thread-confined local scalar and array values need no class lifetime lock. Shared global mutable storage requires its own data guard. A global slot's lifetime does not guarantee the lifetime of the object it currently names. Object/class confinement-based lock elision requires proof covering actual shared allocator storage and lifetimes; it is not implied merely because two threads use different objects.

### Reference acquisition and use

Acquire at least shared class access **before** obtaining a borrowed reference to a shared object or object-owned storage. Reading a raw pointer first and locking afterward is unsafe: deletion and slot reuse could occur in between. Obtain references through a protected maintained relationship or another explicitly lifetime-safe source; root publication and acquisition contracts remain to be completed.

Lifetime protection remains continuously held across use of the borrow. Releasing it ends the validity of affected borrowed references, including references in aggregates, closures, array elements, and borrowed ranges. Reacquiring access does not resurrect them. Reacquire references through still-valid maintained state under the appropriate lifetime and data protection.

A reference passed into a helper carries its owner provenance and access requirements. Helpers reuse access already held rather than independently reacquiring the same lock. The checker must preserve these obligations through calls and specialization; opaque calls need conservative contracts. Exclusive access satisfies a shared requirement, but shared access cannot satisfy an exclusive requirement.

A region that may create or destroy instances must obtain exclusive access before those operations and before acquiring borrows it will retain across them. Do not upgrade shared access while retaining it: two upgrading readers could deadlock. Ending a shared region and starting an exclusive one is possible only after ending its borrows and then reacquiring/rechecking objects under the new protection; that gap is not an atomic upgrade.

The intended efficiency comes from amortizing protection across a region of work rather than locking at every field access or reference copy. Region boundaries and their source spelling remain to be finalized. Long-lived regions can delay creation/destruction, and class-wide exclusion affects unrelated objects of the same class. Writer progress and scheduling/fairness policy need an explicit implementation contract; a particular reader–writer lock's behavior must not be assumed.

### Stable class storage

Both AoS and SoA use slab allocation with a free list. Allocation removes a slot and deallocation returns it under exclusive class access. Generated helpers do not acquire an additional allocator mutex. Any shared runtime allocator used beneath class storage retains its own separate contract; the class lock does not purport to protect unrelated runtime allocations.

AoS object storage does not move during an object's lifetime. In SoA, each class property array has its own stable virtual-address region, reserved large enough to cover the full 32-bit class-index space. For an element stride S, reserve at least 2^32 times S bytes, with appropriate alignment and checked address/size calculations. A one-byte property reserves 4 GiB; an eight-byte property reserves 32 GiB. Growing the population must not move the property array's base address or copy it into a replacement allocation.

Virtual address reservation is distinct from physical backing. Use operating-system virtual-memory mapping facilities to reserve the region and provide physical backing as pages are needed. The property base and slot-index address remain stable as population grows. Exact reserve/commit/decommit calls, mapping flags, page initialization/publication, and resource-failure handling must be specified for supported targets; the design does not assume that every platform exposes the same mmap2 syscall. It requires an adequate virtual-address space, not a 32-bit process address space. Address-space exhaustion must be handled explicitly rather than silently falling back to relocating live property arrays.

The intended compilation/deployment scale is a program or shared library, with its class property reservations. Bill reports having tested this reservation strategy successfully; this document records that design input, not an independent measurement of a new Lyric runtime. Shared-library unload and allocator-region teardown must not release regions while live objects or accesses remain; exact module lifetime coordination remains to be specified.

Stable property arrays eliminate the need to re-read a moving SoA base solely because another object was allocated. They do not prevent slot reuse after deletion or concurrent payload mutation. Lifetime exclusion handles deletion; data guards handle conflicting payload operations. Exclusive creation is the chosen synchronization simplification, not a requirement caused by property-array relocation.

An ordinary dynamic array owned by an object may still resize or relocate under Section 5. Its descriptor's stable property address does not stabilize its backing elements. Element and range borrows require stable backing throughout their usable lifetime, with acquisition bounds checks and required data protection. Same-thread potentially invalidating operations are rejected while those borrows are still needed; concurrent interference must be excluded by the protection contract. This reservation policy does not reserve a full class-index-sized virtual region for every ordinary array value.

### Cross-class requirements and acquisition proposal

The compiler infers conservative transitive class-access requirements from object use, creation, destruction, generated cleanup, callbacks, and final hooks. A class used without creation/destruction requires shared access; a class that may be created or destroyed requires exclusive access. Exclusive mode wins when requirements combine. Access requirements must cover all reachable calls in recursive groups, and generic specialization must carry all participant/role bindings. Unknown external effects cannot be treated as no effects.

A constructor or destructor may touch several classes. Destruction needs exclusive access for every class it may destroy, and at least shared access for surviving parent/other objects it uses. Detaching links also requires the appropriate data guards; exclusive access to the destroyed child's class alone does not protect every parent field. Generated counted releases and final hooks contribute their full requirements.

**Proposed initial acquisition discipline:** acquire the complete required lifetime-lock set and modes before a region, in one process-wide stable order. Helpers reuse held access. Nested regions use subsets with no stronger modes; no new class lock or shared-to-exclusive upgrade is acquired midway through the region. This is a proposed protocol, not an approved source grammar. Explicit outer regions with compiler-inferred sets/modes are the current recommendation; exact boundaries, syntax, and diagnostics remain for review.

Lock identities and their order must be consistent across dependencies and actual allocator storage. Ordering the lifetime locks alone does not establish safe ordering with payload guards, foreign locks, channels, or joins. Their combined acquisition/blocking contract remains to be specified. Do not infer deadlock freedom from the lifetime-lock ordering proposal alone.

### Same-thread destruction and safe loops

Lifetime locks exclude interfering threads; they do not make the current thread's destructive operations harmless. Ordinary class-wide invalidation applies inside exclusive regions. Creation alone does not invalidate existing object references, though constructor calls can have other destructive or mutating effects.

A relation loop explicitly selecting a safe generator may still destroy only its current object of the iterated class. Exclusive class access does not grant this permission to an ordinary generator. Such a loop requires exclusive access to that class from the outset. Deletion invalidates the iteration variable and references into it; the representation preserves continuation under its stated traversal contract. Neither exclusive lifetime access nor a data guard permits same-thread peer deletion that violates that contract. A non-destructive loop can share lifetime access with other threads, but its collection traversal also needs protection against conflicting structural mutation by other shared holders.

### Finalizers, blocking, and thread communication

Every deferred-finalizer execution point must account for transitive destruction effects, exclusive lifetime access to classes it may destroy, and applicable data guards. A finalizer needing stronger modes or new locks must defer to a suitable specified boundary rather than upgrade or reclaim without protection. Exact boundaries and orderly-shutdown guarantees remain open.

Blocking waits, joins, foreign callbacks, and lock release need explicit checked contracts. In particular, a thread must not hold lifetime access while waiting for work that needs conflicting access without a protocol establishing safe progress. Compiler inference of lock requirements is not a proof of arbitrary external wait behavior.

Lifetime/allocation locking does not by itself specify publication, thread entry, spawn, channels, cancellation, or joining. These operations must transfer values or use maintained relationships/other explicitly safe lifetime mechanisms; borrowed pointers cannot be placed in queues as unchecked escapes. Full concurrent-language support is not established merely by specifying a class lock.

Foreign functions need truthful creation, destruction, mutation, escape, and lifetime/data-lock contracts. Unknown effects are not harmless. Native code violating its contracts is outside the safe-language guarantee; keep that trust boundary small and explicit.

## 16. Standard-library contracts

This section specifies the retained and upgraded compiler-oriented library surface while distinguishing completed behavior from explicitly incomplete API contracts. A signature inventory is not a complete library specification: every operation also needs defined failure, ownership, mutation, lifetime, and edge behavior. The numeric and storage rules elsewhere in this document apply to library code too. The numeric parsing APIs below use Result. Historical operating-system boolean/tuple signatures have not yet been uniformly revised; their presence does not select a second `?` propagation protocol.

String is the [u8] alias specified in Section 4. Unicode-aware iteration and transformations are separately specified library operations. A byte is not a Unicode character.

`Sym` interns strings and caches hashes. Hash collisions are resolved by actual key equality; interning does not make hash equality sufficient. `Dict<K,V>` requires consistent hashing and equality.

Library containers obey the same lifetime, effect, and relationship rules as user code. They are not exceptions that can hide stale references from the checker.

### Library source as the baseline

The inspected original `stdlib/std.ly` and `stdlib/string.ly` provide the baseline for these choices: relation-backed owned entries, interned names with cached hashes, entry-returning dictionary lookup, a byte-array StringBuilder, and byte-oriented string utilities. These are ordinary Lyric libraries, not compiler-privileged object categories. Their implementations must satisfy the same constructor, relation, value, borrow, and effect rules as application code. Effects are inferred from available bodies; public contracts state required behavior rather than prescribing their algorithms.

Porting does not copy every old behavior. In particular, equal hashes cannot substitute for key equality; manual trusted ref/unref operations and permanent classes are not restored; shared array backing on ordinary assignment/parameter passing is not restored; and parent teardown must not pre-detach children before their final hooks. Source-defined behavior is evidence for a contract, not authority to preserve an implementation defect. The comparison and adaptation decisions are recorded in [the stdlib adoption note](reviews/stdlib-adoption.md).

### Core and conversion

| Operation | Baseline contract |
|---|---|
| `len(array_or_string)` | Return byte/element count as `i32`; changing count width requires a deliberate decision |
| `append(array, element)` | Return an independent array value with element appended; no mutation of the input array. Use writable array push/append for in-place mutation |
| `print`, `println`, `eprint`, `eprintln` | Format supplied values; select stdout/stderr and newline behavior |
| `to_string(value)` | Generate or dispatch a textual representation |
| `format(pattern, ...)` | Format values; exact format grammar must be defined |
| `isnull(optional)` | Return whether absent |
| `panic(message)` | Report a checked programming failure with source location and stack trace, then terminate the process under Section 8 |
| `exit(code)` | Terminate process with explicit status |
| `itoa(i64)` | Decimal string |
| `parse_int(string)` | `Result<i64>`; strict full-input decimal parsing below |
| `atoi(string)` | Compatibility alias for parse_int, with the same Result return |
| `parse_float(string)` | `Result<f64>`; strict decimal parsing below |
| `char_to_string(u8)` | One-byte string |
| `string_to_bytes(string)` | `[u8]` identity-by-value helper; does not grant shared writable backing |
| `bytes_to_string([u8])` | string identity-by-value helper; no encoding validation or conversion |
| `hash_string(string)` | `u64` hash; not equality |
| `sym(string)` | Interned symbol |
| `invalid_argument(string)` | Non-OK Status for an invalid argument; the full standard-code factory inventory remains to be specified |

The original used `any...` to describe printing, but that does not by itself specify a general dynamic `any` type. Formatting can be compiler-specialized. Dynamic interface representation and lifetime must be explicit wherever runtime erasure is supported.

### Concatenation

String `+` concatenates bytes in left-to-right order. Array `+` concatenates two array values of the same element type, preserving each operand's element order. The operands evaluate once, left to right; the result is an independent value and neither operand is mutated by concatenation itself. Nested arrays copy by the ordinary recursive value semantics; class-reference elements copy references, with counted retention where applicable. No element-wise numeric coercion or implicit stringification is introduced by `+`.

```lyric
let greeting = "hello " + "Lyric"
var joined = [1, 2] + [3, 4]
assert_eq(joined, [1, 2, 3, 4])
```

Empty operands are valid. Result-length and allocation-size calculations must not overflow into invalid memory access; exceeding the supported length is a checked failure before constructing an invalid result. General allocation failure follows the runtime's separately specified policy. `+` uses additive precedence; this adds no general operator-overloading facility.

### Arrays

Preserve the operations `len`, `is_empty`, `push`, `append`, `pop`, `contains`, `index_of`, `first`, `last`, `sort`, `reverse`, `clear`, `remove`, `extend`, `slice`, and string-array `join`.

Baseline results: `first` and `last` return `T?`; `index_of` returns `i32` with -1 for not found; `pop` returns T; mutators return `unit`. Empty pop, equality requirements, sort ordering/stability, invalid ranges, and remove-not-found behavior need explicit rules rather than backend defaults.

Aliases such as `length`/`len`, `find`/`index_of`, and `push`/`append` are compatibility conveniences. KISS can select one canonical spelling, but removal must be documented rather than accidental.

### Byte-string utilities

The canonical text operations are the existing str_* free functions. Because string is [u8], an ordinary array method such as contains takes an element (u8), not a substring. Use str_contains for substring search; no nominal-string dispatch changes the meaning of an array method. Thin convenience aliases may be provided only when unambiguous and behaviorally identical, not as a second competing API.

Retain these source-defined contracts:

- `str_index_of(text, needle) -> i32` returns the first byte offset or -1; an empty needle matches at zero. `str_contains` tests this result. Empty prefix/suffix matches in str_has_prefix/str_has_suffix.
- `str_split(text, separator) -> [string]` preserves empty parts, including trailing empty parts. An empty separator splits into individual one-byte strings; empty input with empty separator yields an empty array.
- `str_split_n(text, separator, n) -> [string]` returns `[text]` for n == 1; n <= 0 has the same behavior as str_split. For n > 1 it yields at most n parts, with the final part retaining the unsplit remainder. With an empty separator, split at byte boundaries under that same limit, rather than repeatedly matching without advancing. Empty input with empty separator yields an empty array except the explicit n == 1 case.
- `str_trim`, `str_trim_left`, and `str_trim_right` remove only space, tab, LF, and CR bytes at the specified ends, matching string.ly. They preserve internal bytes. This library operation is distinct from source literal scanning/trimming.
- `str_to_lower` and `str_to_upper` transform ASCII letters only; other bytes are preserved. They return independent values rather than mutating the input.
- `str_replace(text, old, replacement)` replaces nonoverlapping matches from left to right; replacement text is not rescanned. Empty old returns the input value unchanged.
- `str_repeat(text, n)` returns empty for n <= 0 and n concatenated copies otherwise. `str_concat(a, b)` is the byte-array concatenation operation. `str_join(parts, separator)` places separators between parts, not at either end; no parts yields empty.
- `str_substr(text, low, high)` follows ordinary half-open slicing; `str_char_at(text, index)` returns a one-byte string, while indexing itself returns u8. Invalid bounds cause checked failure.

Returned arrays/strings use ordinary independent value semantics. Length/allocation arithmetic must be checked before invalid storage access; the general resource-failure contract still applies. Byte predicates char_is_digit/alpha/space/upper/lower/alnum retain explicit ASCII classification, not locale or Unicode classification.

### Numeric parsing utilities

`parse_int(text) -> Result<i64>` consumes the whole byte string: optional leading ASCII + or -, then at least one ASCII decimal digit. No whitespace, underscore, radix prefix, or trailing junk is accepted. Malformed input returns InvalidArgument; a mathematically out-of-range integer returns OutOfRange, including checking the signed minimum boundary correctly. It does not ignore nondigits or use zero as an error sentinel. atoi is an alias, not a second tuple-returning protocol.

`parse_float(text) -> Result<f64>` initially accepts an optional ASCII sign, one or more decimal digits, and optionally a decimal point followed by one or more digits. It requires full consumption and rejects whitespace, exponent notation, NaN/infinity spellings, and trailing junk with InvalidArgument. Finite decimal input that overflows to infinity returns OutOfRange; subnormal/zero underflow follows the normal rounding contract. Signed zero is preserved. Source float-literal grammar and library parsing are separate; no exponent syntax is added to either by implication. The old permissive str_to_float spelling may be a compatibility alias for this contract, not its old partial-input/sentinel behavior.

### Dictionaries and owned entries

Dict<K,V> and DictEntry<K,V> are ordinary identity classes. A dictionary maintains an owning relation to its entries; each entry has its matching backlink. Constructors attach new entries before success, destruction detaches both ends, and dictionary destruction cascades to its entries. Keys satisfy Hashable<K> and its equality/stability laws. This is ordinary library source, not a new compiler-owned lifetime category.

Public operations are:

```text
Dict<K,V>() -> Dict<K,V>
set(key: K, value: V) -> unit
get(key: K) -> DictEntry<K,V>?
has(key: K) -> bool
remove(key: K) -> bool
keys() -> [K]
len() -> i32
```

set inserts a new entry or updates the existing equal key's value in place. Updating preserves that entry's object identity; replacing its value still performs normal releases and their transitive destruction effects. A new entry copies its key/value under ordinary semantics. Hash collisions between unequal keys must preserve distinct entries. Hashing selects candidates; actual key equality determines the match for get, set, has, and remove. The old hash-only lookup path is not sufficient.

get returns the actual owned entry, not a copied entry or value. Its key is readable but not writable through the public API; its value is readable/writable subject to ordinary lifetime and storage rules. remove returns false without mutation for a missing key; otherwise it destroys the matching entry, whose generated teardown removes membership, and returns true. Destroying the dictionary destroys all remaining entries. Index growth or insertion alone must not move existing entry objects.

There is no special promise that a held entry reference survives every dictionary operation. The compiler derives invalidation from bodies and their transitive effects. Removing an entry can invalidate held references to that entry class under the conservative class-wide rule, even if the programmer believes a different key was removed. Value replacement can invalidate references into the old value or destroy other objects through counted release. Reacquire through a surviving dictionary after an invalidating operation. Conversely, an operation proven not to destroy or invalidate an entry need not be treated as destructive merely because it mutates the dictionary.

keys returns an independent array in unspecified order. Byte-array keys are copied by value; Sym keys are stable counted values. Other user-defined keys must preserve hash/equality while stored; the exact contract for mutable reference-bearing keys remains open and unsupported uses must be rejected. Ordinary class references in K or V still require maintained lifetime relationships; generic storage is not a loophole for persistent unchecked pointers.

The unit-returning construction/set surface does not silently discard relation insertion failures: the library must establish its internal attachment preconditions or produce a checked invariant failure. Resource exhaustion for an operation with no Result return must terminate with checked failure, not return a partially successful dictionary. A separate recoverable-allocation API is not introduced. Internal indexes must remain consistent with maintained entry lifetime; they are not permission for arbitrary unchecked stored references.

A hash-table implementation may reuse suitable relation machinery, but Dict must not equate its arbitrary application keys with HashedList's raw-u64 lookup keys. The existing HashedList API treats u64 as an actual key. A collision-correct generic dictionary requires equality-aware candidate handling in its library representation; the probing or bucket algorithm is implementation design.

### Dictionary literals

A dictionary literal is `{ key: value, ... }`. Keys and values are expressions. Entries evaluate in source order, each key before its value, exactly once. Construction has the behavior of creating an empty dictionary and applying set in that order: later equal keys replace earlier values, while unequal colliding keys remain distinct. Earlier evaluation effects are not rolled back. The dictionary is not published as a successful value before construction completes; partial cleanup follows ordinary lifetime rules.

An expected Dict<K,V> supplies the key/value types. Without one, a nonempty literal must have one consistent inferred key type and one consistent inferred value type, using normal literal defaults and exact resolved types rather than searching for a third common type. An empty literal requires an expected Dict type. General literal defaulting is not arbitrary overload resolution. Normal copy/retain, key-stability, and stored-reference rules apply to every entry.

```lyric
let names = {`alice`: 1, `bob`: 2}  // Dict<Sym, i32>
let cities = {"NYC": 8, "SF": 1}   // Dict<string, i32>
let empty: Dict<Sym, string> = {}
// let unknown = {}                // rejected: no key/value types
// let mixed = {1: "one", "two": "two"} // rejected without compatible context
```

Braces in a block position remain a block; use a dictionary in an expression position or parenthesize it. The PEG applies the adopted header/body-brace rule to dictionaries as well as records: direct header literals are excluded, parentheses restore ordinary expressions. Entry separators follow the adopted record-member separator convention in D5.

### Symbols and interning

Sym is an explicitly counted value class with a private immutable name value and cached hash. sym(text) interns the byte-array value; a backtick literal such as `` `mySymName` `` means sym("mySymName"), not variable lookup. Equal names return the same canonical live symbol from the library's intern table. Unequal names remain distinct even when hashes collide. Name comparison is exact byte equality, with no Unicode normalization applied to symbol data.

`get_name(self) -> string` returns an independent value copy; `get_hash(self) -> u64` returns the cached hash. Application code cannot mutate the stored name/hash or forcibly destroy a counted Sym. Changing the caller's original byte array or a returned name copy cannot alter the interned name. Empty names are valid: sym("") and an empty backtick literal return the canonical empty symbol, distinct from null.

The standard library owns one program-lifetime intern-table root, initialized before its first use. The table holds counted references to canonical Sym values until its teardown; callers and containers retain/release counted handles normally. No permanent keyword, forced owning cascade into retained values, or holder backlinks are required. Table teardown releases its references rather than force-destroying symbols retained elsewhere. Exact global initialization/shutdown sequencing and concurrent synchronization still require the general runtime contracts; a source-level global is not an exemption. The initial policy favors simple program-lifetime interning over reclaiming unused names during execution.

Intern lookup must compare full names among hash candidates. Sym equality is consistent with name equality; canonical identity may implement it only while that invariant is maintained. The hash algorithm need not be exposed as a stable serialization format. Integer hashing must accept every value of its key type, including negative signed keys, without triggering a checked narrowing/sign-conversion failure.

### StringBuilder and bulk byte append

`new_string_builder()` constructs an empty mutable value class StringBuilder containing a string/[u8] buffer. `write(string)` and `write_byte(u8)` append bytes; `len()` returns byte length; mutators return unit. `to_string()` returns the buffer by ordinary value semantics. A later builder mutation cannot change a previously returned value: no separate string snapshot mechanism or shared-mutable-backing exception is required. Copying a builder handle shares the mutable counted builder object, not an independent builder state.

Retain `push_bytes(destination: ref string, source: string) -> unit` as an explicit writable-storage helper. It appends the source value's original bytes to the destination; source evaluation precedes mutation and follows value semantics, including self-append. The destination's array-variable storage must stay valid, and still-needed interior borrows that would be invalidated prohibit growth. Copy elision must preserve overlap/value behavior. No particular growth factor or memcpy lowering is required by this specification.

Length/allocation overflow or resource failure in these unit-returning operations produces checked failure rather than invalid memory access. Recoverable variants can be designed separately. Ordinary borrow/effect and concurrency checks apply to all methods.

### Text display and streaming-library scope

`to_string` and diagnostic formatting must support scalar values, enum variant names/payloads, and useful struct/class descriptions. Automatically generated traversal must not recurse forever on cyclic object relationships or dereference invalid objects; repeated references must be represented finitely. Exact formatting and application customization remain a library contract to finish. Diagnostic stack traces still do not dump locals by default.

Reader/Writer-style byte streaming and buffered readers/writers are retained as the next library layer. Required capabilities include partial reads/writes, explicit end-of-input, flush, recoverable close, read_line/read_all, and buffered string/line writes. Operational failures use Result rather than the obsolete error interface. The final signatures must distinguish end-of-input from failure and preserve partial progress when an operation can both transfer bytes and fail; a bare Result<count> alone does not establish that contract. Read buffers require explicit writable access, including fixed windows; passing an ordinary array value must not silently mutate the caller's buffer. Resource ownership, close idempotence, buffering failure, and final cleanup must be specified before these APIs are treated as complete.

File/directory/path operations remain part of the compiler-oriented library. Retained next-layer operations include mkdir/mkdir_all, remove/remove_all, rename, stat, and standard input/output/error through Reader/Writer capabilities. Network I/O is deferred; naming streaming capabilities does not authorize an unreviewed network or concurrency API.

### Files and processes

The original minimal I/O surface is retained for coverage:

```text
read_file(path: string) -> (string, bool)
write_file(path: string, content: string) -> bool
os_args() -> [string]
os_getwd() -> string
exec_command(name: string, args: [string]) -> (string, bool)
path_join(parts: [string]) -> string
path_dir(path: string) -> string
path_base(path: string) -> string
path_ext(path: string) -> string
list_dir(path: string) -> ([string], bool)
file_exists(path: string) -> bool
mkdtemp() -> string
mkdtemp(prefix: string) -> string
```

The two mkdtemp forms preserve no-prefix and explicit-prefix construction; the prefix names the requested temporary-directory basename prefix, not an arbitrary destination path. Exact platform validation and error-return signatures remain open. These library call forms do not establish general default-argument syntax.

These signatures are not a complete operating-system contract. Define whether bool means success, how exit status differs from spawn failure, byte encoding, subprocess cancellation, resource cleanup, and failure of functions with no error result. A uniform error-returning API may be preferable, but that is a deliberate API revision.

Streaming/buffering and directory additions follow the requirements above; network I/O remains deferred. Concrete resource classes may use final hooks for handles, but recoverable flush/close belongs in explicit Result-returning operations. Their relationship and value lifetimes follow the ordinary language rules.

## 17. Compiler and toolchain

This specification defines the requirements on compiler analyses: the language behavior they must preserve, the programs they must accept or reject under the stated rules, and the guarantees and diagnostics they must provide. Algorithms and internal data structures for meeting those requirements belong in separate implementation-design documents. For example, the lifetime checker must enforce Section 12's validity and effect rules, but this specification does not prescribe its analysis algorithm. Observable evaluation and cleanup order remain language requirements, not optional implementation choices.

The compiler should follow the architecture the language supports:

- Shared declarations hold types, relations, and callable contracts.
- AST nodes have their structural parents.
- Symbols know their scopes.
- Bindings retain all participating types and endpoint roles.
- Functional passes navigate those relationships.
- There is one authoritative representation of each fact, not competing lookup tables compensating for absent links.

Parsing, resolution, elaboration, specialization, effect inference, safety checking, lowering, optimization, and emission are distinct responsibilities. Their exact pass arrangement is an implementation choice. No generated specialization may escape the safety checks.

Specialization substitutes resolved declarations and bindings, not ambiguous text. Generated name mangling is not the identity of a relationship.

Compiler explanations should answer concrete questions:

```text
Show Node's parents and children.
Show what Graph.prune may destroy, and why.
Show which relation satisfies this abstract graph role.
Show the structural cleanup generated for Edge.
```

These answers come from the authoritative model. A separate tool should not have to guess them from text.

The C backend is a practical starting point. AoS and SoA are layout choices with the same source semantics. Bounds checks, arithmetic rules, evaluation order, and lifetime checks must survive lowering and optimization. Debug UAF sentinels are diagnostic aids, not a substitute for static safety.

### Commands and build contracts

The original command surface is:

```text
lyric compile <files-or-module> -o output
lyric test <files>
lyric fmt <files>
lyric help
```

Keep these names unless deliberately changed. `lyric fmt` formats `.ly` language source. Design artifacts, documentation annotations, and their formatting/verification belong to Lyre; the Lyric compiler does not provide a `.lyric` source mode or maintain Lyre's annotation layer.

`--soa` selects layout without changing language behavior. `--lir-dump` provides diagnostic output. A UAF-debug option is diagnostic support, not a substitute for static checking. Bounds-check disabling is an explicitly unsafe benchmark mode. Old RC-disable flags cannot silently change the accepted lifetime contract in an ordinary safe build.

A whole-module compile includes the declared files and resolves dependencies deterministically. Parser/checker errors prevent executable output. An example appearing in this document is not tested merely because it is fenced as Lyric; compiler validation remains future work.

## 18. Conformance and evidence

The cases below are requirements for a future implementation, not passing language tests. Mechanical document checks, a C lowering probe, self-hosting reports, and compiler conformance establish different things.

### Tests and checked failures

The baseline test is a no-argument, no-result function whose name starts with `test_`. Test files are ordinary `.ly` files. Functions named otherwise may serve as helpers but are not automatically tests.

```lyric
func test_add() {
    assert_eq(add(2, 3), 5)
}
```

`assert(condition, message)` produces a checked programming failure if false. `assert_eq(actual, expected, message?)` compares values and reports both, with the source file and line. A failing assertion reports a stack trace and terminates the test process under Section 8. A runner continuing the suite executes other tests in separate processes, not by recovering within the failed process. Suite exit status is 0 if all selected tests pass and nonzero if any fail or compilation fails.

### Required conformance cases

A specification must state both accepted and rejected behavior. Useful conformance cases include:

1. Constructors establish both endpoints, including two same-class parent roles.
2. Refused relationship changes leave both ends unchanged.
3. Destroying either parent of an edge removes both memberships exactly once.
4. Weak relationships detach without cascading destruction.
5. Final hooks run before their own object's structural teardown.
6. Direct, indirect, recursive, generated, and counted-release destruction invalidates local references.
7. Reacquisition through a surviving parent is accepted.
8. Aggregate-contained aliases and generated temporaries cannot evade invalidation.
9. Ordinary generators reject consumer destruction of the current item; explicitly safe generators accept proven current-only destruction and reject deletion of another same-class item.
10. Each relation representation continues iteration correctly after permitted removal.
11. Array-element and range borrows reject potential backing relocation/release before a later use; mutation after their last possible use is accepted. Fresh acquisition after mutation is permitted, but an old borrow is not revived.
12. Nested array borrowing checks acquisition bounds at every level and rejects inner-array replacement or enclosing-owner changes that could invalidate the borrowed storage before its last use.
13. Two same-type relations specialize independently under distinct roles.
14. Multi-class default methods receive all type and relation bindings.
15. Hash collisions preserve distinct keys.
16. Incomplete constructors, unsafe escapes, invalid casts, and unsupported concurrent lifetimes are rejected.
17. A failed constructor detaches partial memberships and destroys initialized children without calling its own final hook; fully constructed children's hooks still run.
18. A shared-object reference is acquired only after shared or exclusive lifetime access; multiple threads may use existing objects under shared access. Payload accesses must separately satisfy their data guards.
19. A relationship update missing either endpoint's required lifetime access or the data protection needed for both ends is rejected.
20. Unlocking and reacquiring a class lock does not make a previously borrowed object or array-slot reference valid again.
21. Shared class lifetime access alone does not protect an element/range borrow against another shared holder's resize. Required data protection prevents invalidation from acquisition through last use; same-thread invalidating resize is statically rejected across the borrow.
22. A cascade's requirements include exclusive lifetime access for all classes it may destroy, at least shared access for other objects used, and the data guards required by detachment, releases, and final hooks.
23. Under the proposed complete-set discipline, nested regions cannot acquire new lifetime locks or stronger modes. Helpers reuse held access; shared-to-exclusive upgrades are not allowed.
24. Nested array assignment preserves independence at every array level; two object owners never require unrelated locks for shared mutable backing storage.
25. A C final hook calling a helper that may destroy C is rejected at compile time, including indirect cascades, callbacks, and counted releases; claiming a different C instance does not bypass the rule.
26. A final hook may perform checked operations on other classes whose transitive effects do not destroy its own class. Generated same-class child cascades after the hook remain possible under the normal detachment and lifetime rules.
27. A final hook declaring Result or another non-unit return type is rejected, as is using ? to propagate a fallible call from the hook. A locally handled failure may permit normal hook completion, after which structural destruction continues.
28. An implicitly discarded Result or a local Result that is never used is rejected. Explicit let _ discard is accepted but does not satisfy a required-success postcondition or permit generated teardown to ignore failed detachment.
29. Growing a SoA class population leaves every property-array base and existing live slot address unchanged. Reservations cover the full 32-bit index range with checked stride calculations; backing newly used pages does not relocate the region. Ordinary object-owned dynamic arrays retain their separate resize and stable-borrow rules.

30. Creation or deletion with only shared class access is rejected. Exclusive access excludes all other class users and permits allocator/free-list updates without an additional per-class allocator mutex.
31. A non-destructive traversal can run alongside other shared class users; a traversal creating or destroying instances of that class requires exclusive access. Neither case bypasses payload/collection data guards.
32. Concurrent value-class count updates are not accepted solely on the basis of shared lifetime access. A potentially final release must satisfy exclusive destruction requirements or a specified deferred-finalization protocol.

33. Standard many-child insertion rejects already-attached children without changing either endpoint; removal of the wrong membership likewise refuses. A second Edge attachment failure cleans up its first membership during failed construction.
34. ArrayList swap-removal updates the moved child's index and safe traversal visits it; DoublyLinked removal preserves remaining order. HashedList distinguishes actual keys and rejects duplicate keys without mutation.
35. A read-only borrow can inspect immutable storage but cannot write, resize, or forward writable access through helpers. It does not freeze separate class objects or waive destruction checks.
36. Concatenation evaluates operands once in order and preserves independent nested array values; mutations to its result do not mutate operand arrays.
37. Dictionary literals use expected types for empty construction, reject uninferred/incompatible key/value types, evaluate each key before its value, and apply last-equal-key replacement without confusing hash collisions with equality.
38. Numeric capability operations follow the corresponding primitive arithmetic rules; standard Hashable equality resolves collisions. NaN IEEE equality does not silently become a total-order or hash-key policy.
39. Expected enum type selects a same-named variant constructor; payload mismatch is an error rather than fallback to another enum. Missing type context requires unambiguous lookup or qualification.
40. Backtick symbols and sym(string) agree; unequal names survive hash collisions. StringBuilder snapshots remain unchanged after later writes to the builder.

41. Replacing or clearing an optional/tagged payload before a later interior-borrow use is rejected; restoring the same alternative does not revive it. Ordinary writes within an existing active payload may preserve valid borrows.
42. Borrowing an array variable permits resizing through that reference when no still-needed element/range borrow is invalidated. An overlapping element-and-array helper call that resizes and then writes through the element is rejected inside the call, not merely checked after return.
43. Aliases, captures, temporaries, loop paths, and generated cleanup extend usable borrow lifetimes as required; mere lexical scope without a possible later use does not. Replacing an integer in stable storage is distinct from replacing the backing of an owning array.

44. NFC Unicode identifiers using XID_Start/XID_Continue are accepted, including accented Latin, Greek, and CJK names; invalid starts/continuations and malformed UTF-8 are diagnosed. A decomposed non-NFC spelling is rejected with its NFC suggestion, not silently merged with another identifier.
45. Identifier comparison is case-sensitive and distinguishes cross-script confusables; diagnostics expose code points when needed. Keywords match complete ASCII spellings, so a longer valid Unicode identifier beginning with a keyword is not split. The pinned Unicode data version is reported and independent of host updates; string/symbol contents are not normalized by identifier rules.

46. Literal default-ignorable characters, bidi controls, and non-whitespace control characters are diagnosed in every source context, including comments and raw literals; a leading BOM is rejected. Diagnostics render code points/escapes rather than emitting the hidden controls.
47. Ordinary strings and f-string text decode valid Unicode scalar escapes into UTF-8 data, including characters prohibited literally in source. Invalid scalars and malformed escapes are rejected; identifiers have no escaped-character bypass. Combining marks and naturally right-to-left letters remain allowed under the identifier rules.

48. Local, parameter, pattern, generic-parameter, and import-alias declarations cannot hide names visible through unqualified lookup, including builtin type names. Disjoint scopes and separately qualified class/package namespaces may reuse names. Last use of a value does not end lexical name visibility.

49. Tuple `.0`/`.1` selection has the selected component's static type and rejects positions outside the tuple arity. Legacy underscore-prefixed access and bracket indexing are not tuple-access alternatives; destructuring remains supported.
50. `nested.0.1` parses as chained tuple fields, not a floating-point selector; ordinary `1.25` remains a float. Field assignment and ref borrowing enforce mutability and containing-storage validity.

51. A potentially current-destroying call in an ordinary generator's consumer is a compile-time error, including indirect helper, cascade, final-hook, and counted-release effects. The compiler does not silently select a safe traversal.
52. A safe generator that uses the potentially destroyed current object after yield or has invalid continuation/cleanup is rejected. Its permission never authorizes peer destruction, same-class cascades to peers, or violation of an active outer loop's contract.
53. Safe interface requirements require implementations that explicitly provide and satisfy the contract; aliases/adapters/specialization preserve the selected permission. An ordinary interface requirement does not expose stronger implementation permission. Safe on a non-generator or combined with final is rejected.
54. Collection.iter and relation-scope for sugar remain ordinary traversal; RemovableCollection.iter_removable is called explicitly. Standard removable traversals handle current removal/destruction correctly while standard ordinary traversals reject structural mutation.

55. String and [u8] are the same type with independent array value assignment/parameters/slices; mutable copies and conversion-helper results cannot mutate their sources. Byte-array equality/hashing/ordering do not distinguish the alias spelling.
56. Distinct keys and interned names with equal hashes remain distinct; updating an equal dictionary key preserves entry identity, while removal destroys its entry and triggers ordinary inferred invalidation. get returns the owned entry object.
57. Sym retains a private name value: modifying input or returned copies does not alter it. Empty names intern normally; table teardown releases rather than force-destroys retained counted symbols.
58. str_split_n with empty separator terminates and follows the byte-split/limit rules, including n <= 0, n == 1, and empty input. Integer/float parsing consumes the whole input and rejects malformed or out-of-range values rather than returning a success sentinel.
59. StringBuilder.to_string and push_bytes preserve ordinary byte-array value/overlap semantics; old manual ref/unref or shared-array assumptions do not bypass the current lifetime rules.

Self-hosting, fixed-point compilation, and benchmarks supplement these checks. They do not replace them.

### Evidence boundaries

The new Lyric compiler and concurrent runtime have not been implemented. No PEG engine has executed the grammar examples. Historical grammar receipts record checks of rule references and, for some revisions, recursion/repetition hazards; they do not establish accepted-language behavior, semantic safety, or performance.

The standalone guarded 64-bit division/remainder C probe is the bounded executable evidence described in Section 14: 271 checks at each of -O0, -O3, and -O3 with undefined-behavior sanitization under Apple Clang 21 on ARM64. It classified failures rather than running Lyric's fatal diagnostic. The local `gcc` command was also Apple Clang; GNU GCC, other targets, shifts, floating-point conversions, and the complete compiler remain unverified.

Bill reports successful use of full-index-space virtual reservations for stable SoA storage. That is attributed input, not a newly run allocator benchmark. Document readback, cross-reference checks, source preservation hashes, and this rewrite's coverage review do not establish memory safety, race freedom, deadlock freedom, or runtime efficiency.

## 19. Unresolved decisions

Open decisions are grouped here so a settled semantic rule is not reopened merely because implementation has not begun. Closing each decision requires an accepted example, a rejected example, and its reason. Exact source syntax and runtime mechanisms must be earned rather than inferred from historical compiler accidents.

### Decision register

- **Syntax.** D1–D5 recommendations are adopted. Still open within them: the initial pinned Unicode data version and lone-CR handling. Name shadowing is prohibited under Section 5. D6 prefix ref and pattern-name resolution remain proposed. Generic-first PEG priority, header-brace boundaries, cast boundaries, separators and block-value rules are settled.
- **Additional syntax coverage.** Explicit enum/union payload borrowing; generic external-method parameter scope; defaults/variadics; read-only-borrow type/argument/binding spelling. Dictionary literals and expected-type enum constructor resolution are retained; delimiter rules follow adopted D3/D5. Decide whether to retain positional record construction, pipe closures, optional lyric wrappers, bare import paths, `implements`, and bare destructor shorthand. Generic aliases, single-element tuples, type-level null alternatives, multiple interface inheritance, and chained-comparison meaning must not be inferred from nearby features. The draft grammar folds ordinary comparisons left; Python-style chaining is not established. The original behaviorless-struct baseline remains in the grammar; struct and primitive extension/static methods need a deliberate decision.
- **Package/toolchain contracts.** Manifest grammar, package membership/dependency configuration, automatic standard-library imports, mutable-global initialization order and visibility, entry/linking details, and any check of Common-versus-functional-unit dependency discipline. Package privacy and exact declaration/body matching are settled.
- **Construction and relations.** Eligibility of field-literal class construction without bypassing required constructors; nondefaultable field/array initialization details; the exact custom-hint/key-stability checking contract and additional traversal mutations beyond current-child removal. Standard many-child insertion/removal, representation ordering, Single set/remove, and the visible Collection iter/count/parent contract are specified in Section 9.
- **Reference safety contracts.** Complete the accepted/rejected cases and owner-provenance/effects requirements across overlapping arguments, calls, callbacks, recursive groups, and specialized bodies; the analysis algorithm is separate implementation work. Fixed aliases, value parameters, array value independence, stable-storage borrows through last possible use, fixed windows, inferred resize permissions, and overlap-safe assignment are settled. References do not reconstruct a location or revive after invalidating replacement. Ordinary array iteration under resize/reorder still needs its own contract.
- **Closures/generators.** Capture-selection syntax and complete safety, control-flow, and cleanup requirements. Nonescaping concrete environments, loop-only single-yield generators with lexical consumer control flow, and the explicit checked safe-generator modifier are settled. Ordinary traversal does not grant current-item destruction permission. Escaping closures and independently resumable generators are deferred, not silently supported.
- **Interfaces.** Complete scope rules for separately defined generic methods, extension cycles/conflicts, and precise storage-requirement versus explicit-injection checks, and the complete hint-shape/arity/cardinality validation contract. Structural satisfaction, explicit-binding precedence, labeled constraints/calls, and complete participant/role specialization are settled; constraint use cannot silently inject storage.
- **Concurrency.** Lifetime-region boundaries and source spelling; complete-set/global-order acquisition; payload/global guards and ordering with lifetime locks; writer progress; safe counted updates; publication, spawn, channels, cancellation, joins, and blocking/FFI callback contracts. Shared existing-object access and exclusive creation/destruction under one class lock, without another per-class allocator mutex, are settled.
- **Finalization and runtime storage.** Checked deferred-finalizer boundaries, orderly shutdown, shared-library unload coordination, supported VM reserve/backing flags, initialization/publication, resource failures, and index/null-sentinel capacity details. Final order, unit-only hooks, class-wide reentrant-destruction prohibition, failed-construction cleanup, and stable full-index-space SoA property bases are settled.
- **Numeric environment.** Rounding-mode exposure and precise excess-precision policy; implementation validation across widths/targets. Integer arithmetic, shifts, conversions/comparisons, IEEE value behavior, and the initial C-contraction allowance are settled. Controlled explicit FMA is a preferred later direction, not an initial API requirement.
- **Library behavior.** Custom key-stability enforcement for mutable reference-bearing keys, size/count limits for i32 len/count versus the 32-bit class-index space, empty pop, sorting, remove-not-found, formatting/printing errors, and I/O/process result/cleanup/cancellation behavior. String-as-byte-array value/borrow behavior, dictionary owned-entry behavior, collision-safe lookup, byte-string utilities, numeric parsing, program-lifetime counted interning, concatenation, and standard generic capabilities are specified above. Finish the retained streaming/directory APIs and decide any uniform Result migration explicitly. The full standard-code factory inventory and numeric serialization/foreign Status mappings remain open; Status itself has no structured attachment facility.

### Completion standard

A complete specification must define the syntax, name resolution, typing, evaluation, and cleanup requirements of every supported construct, including its interactions and failure cases. It need not prescribe the algorithms used to implement or check those requirements. Unsupported forms must be rejected rather than emitted as placeholders. Grammar, prose, diagnostics, and accepted/rejected conformance cases must evolve together.

This rewrite organizes the existing design; it does not supply the missing contracts by assertion. KISS governs the number and interaction of mechanisms, not how few words are used to specify them. The preserved originals, retained reviews, and frozen drafts/coverage records in Git history remain available for checking that simplification has not discarded essential behavior.

