# Lyric Draft 4: comparison with the original specification and interface redesign

> Historical review: this compares the exact earlier revisions listed below. Later decisions supersede some findings; see [the review index](README.md).

## Scope and evidence

This is a fresh three-source document review, performed after Draft 4's requirements-versus-algorithms clarification and concurrent-memory-safety update. All three files were personally read in full in contiguous, untruncated ranges. Targeted searches supplemented those reads; a clipped search was not treated as a complete review. No old compiler source, external worked-example files, or live implementation was audited. No language rules were changed during this comparison.

Source abbreviations and exact reviewed versions:

- **V4**: [current specification](../lyric-language-spec.md), 1,932 lines, SHA256 `76d273c6ffa6269795c20d89169895c0b6832f08a5038356076995ddd3343857`.
- **O**: [original full specification](../background/lyric-language-spec.md), 3,450 lines, SHA256 `5ed18282dbf5c9fa931e77181edf5f146ac07259657f2956f3aae4fd70c90dd5`.
- **R**: [multi-class interface redesign](../background/multi-class-interface-redesign.md), 1,834 lines, SHA256 `6db0b9cb4335648b1a699cb51750ff9cdb275c8bae4539cbf7d56745c76815a2`.

All three background hashes in `background/SHA256SUMS` were verified. The full original spec and redesign were also compared byte-for-byte with `/Users/bill/projects/lyric/cr/docs/lyric-language-spec.md` and `/Users/bill/projects/lyric/cr/docs/multi-class-interface-redesign.md`; both match exactly. The short original reference was preserved but was not a fourth full-read input to this review. Line references below apply to these versions; later canonical edits will move them. Historical implementation and shipping claims are attributed to the documents, not independently verified here.

**Classification:** retained means an identifiable contract survives; changed means a deliberate semantic departure; deferred means the current text expressly leaves it open; gap means a useful older contract or feature lacks a sufficiently explicit current disposition. A gap is not a recommendation to restore old syntax automatically. A proposed feature in R is not necessarily a feature of the old working compiler.

## Executive judgment

V4 preserves Lyric's distinctive core: cooperating classes, bidirectional relations, ownership independent of representation, generated destruction, role-aware multi-class interfaces, and specialized generic algorithms. It is considerably stronger about value semantics, construction failure, destruction effects, checked access, numeric behavior, and failure reporting.

It is **not a complete superset** of O or R. It deliberately replaces some features, narrows others, and retains a number only as an inventory or open decision. The most consequential gaps in the reverse direction are concrete standard-relation APIs, generic capability declarations, interface callable/binding forms, and ordinary library contracts. The largest remaining safety questions concern accepted/rejected combinations and trust boundaries, not the absence of a prescribed checker algorithm.

The reconstruction coverage map proves a different thing: V4 accounts for its frozen Draft 3.3 source. That does not establish complete semantic coverage of O and R. This report supplies the separate comparison and identifies more specific omissions than the earlier high-level preservation claim.

## 1. Source organization, declarations, and compilation

### Retained

`.ly` executable source, `.lyric` declarations, typed signatures, explicit generic parameters, package-private defaults, `pub`, qualified imports, order-independent declarations, and a C backend remain. V4 preserves compile/test/fmt/help and AoS/SoA layout selection (V4 §§2, 17; O lines 146–283, 2533–2598).

### Deliberately changed

O equates package and directory. V4 makes packages explicit logical groups that can span directories and defines matching between Common declarations and functional implementation bodies (V4 75–105). This enables one declared type and callable rather than wrappers across artificial directory boundaries. Missing, mismatched, and duplicate bodies are errors.

O's single-level imports and unenforced `pub` are admitted implementation limitations, not features V4 must preserve. V4 requires recursive resolution and actual package visibility. Its exact dependency configuration and entry/linking contracts remain open.

### Gaps and scope changes

- O explicitly makes a module one whole-program compilation unit with no separate package compilation (O 180–188). V4 requires deterministic whole-module compilation but does not settle the complete separate-compilation/linking contract. This matters to generic availability, effect contracts, and shared-library class identity; §19 recognizes linking as open.
- Automatic unqualified standard-library imports are explicit in O 244–253. V4 leaves them open. A sample using `Result`, `Status`, `Collection`, or `println` therefore does not itself settle how those declarations enter scope.
- O's optional `lyric` wrappers and bare-path imports are explicitly undecided in V4. That is recorded deferral, not accidental loss.
- O's unique-prefix CLI commands, exact UAF flag spelling, legacy `--c`, and test discovery/order/output conventions are not all retained. These are tool/API compatibility choices, not core language safety requirements. Do not infer support from preserving the four command names.
- R's proposed migration command is absent. This workspace is a fresh design, not an authorized old-code migration project; absence is not a language defect.
- CDD annotations and verification machinery remain outside the language. O itself moved them to `lyre`; restoring them would be a scope expansion, not preservation.

## 2. Types, syntax, binding, and control flow

### Retained baseline

Fixed-width integers, f32/f64, bool, string, unit, structs, classes, enums, tagged unions, optionals, arrays, tuples, aliases, explicit generic variables, contextual literal typing, f-strings, byte characters, symbols, ordinary loops, match, if-let, let-else, and qualified enum variants survive (V4 §§3–7; O §§Primitive Types through Control Flow).

V4 makes branch compatibility, exhaustiveness for value matches, definite initialization, boolean conditions, and let-else divergence actual requirements. O explicitly admits missing checks for some of these. This strengthens existing intentions rather than inventing every feature anew.

### Deliberate syntax changes

- `var` replaces `let mut`; explicit `ref` signatures and arguments replace `mut` parameters. A fixed alias is not a rebindable reference variable.
- Methods require parentheses; field access does not execute arbitrary zero-argument methods. This rejects R's universal property-call sugar.
- `func name<T>` retains O's spelling rather than R's `func<T> name` examples. R says this is unchanged despite showing a different spelling (R 1142–1173).
- Generic method arguments remain allowed. R's proposed parse error for `object.method<T>()` is deliberately not adopted.
- PEG generic-first parsing is now explicit. Later type failure cannot reinterpret a successful generic parse as comparisons. O describes parser heuristics and token splitting rather than this complete priority contract.
- Corrected bitwise precedence implements O's own requested change; wrapping arithmetic and `!as` are additional forms.

### Syntax gaps needing explicit disposition

1. **Expected-type resolution of named enum constructors.** O 870–873 says an expected type chooses between identically named variants from different enums. V4 specifies qualified names and implicit wrapping of an untagged payload, but does not clearly specify this distinct name-resolution case. `return Pair(...)` with two visible `Pair` variants is not the same question as wrapping a plain payload into an enum. State whether expected type resolves the name or qualification is mandatory.
2. **Alternate callable type spellings.** O 390–399 accepts `fn(T)->U`, `func(T)->U`, and `T->U`. V4 establishes `fn` but does not individually dispose of the other two. Keeping one spelling is reasonable; document that choice instead of relying on grammar omission.
3. **Generic struct declarations.** O's `struct Range<T>` example is explicit (773–788). V4 explicitly demonstrates generic classes and enums but does not similarly teach generic struct declaration/scope. Its general generics account may intend them; this is an exposition/contract gap, not evidence they were deliberately forbidden.
4. **Read-only borrowed views.** O distinguishes `let ref` immutable views from mutable views, while admitting implementation problems with the latter (1489–1515). V4 specifies writable fixed aliases/windows and immutable value parameters, but no explicit read-only borrowing contract for immutable storage. Ordinary value passing may be optimized without copying, so the performance use case may be covered; persistent source-level read-only aliases still need a clear supported/deferred/rejected answer.
5. **Pattern and block edges.** D1/D2/D4/D5/D6, header braces, variant/binding name collisions, and borrowed payload patterns remain openly proposed. Neither a fenced example nor a PEG production settles them.

Positional records, pipe lambdas, generic aliases, defaults/variadics, type-level null alternatives, and struct/primitive methods are already listed as open. Larger numeric widths, Unicode escapes, base-prefixed literals, block comments, bitwise NOT, and additional compound assignments are future scope rather than lost working features merely because O mentions them.

## 3. Values, references, and lifetime: changes in both directions

### V4 adds precise contracts absent or incomplete in O/R

- Recursive array value independence on assignment, ordinary parameter passing, and ordinary slicing; class-reference elements copy references rather than objects (V4 380–405, 658–685).
- Explicit fixed aliases, no reference retargeting, and forwarding preserving provenance (413–432).
- Array references as checked root/index paths, including nested arrays, replacement, disappearance and later restoration of a path, and the distinction between slot location and object identity (436–478).
- Fixed-length borrowed windows; overlap-safe whole-window assignment; destination revalidation after RHS effects; inferred resize permissions per parameter/path/level (480–544).
- Checked optional/tagged value-payload paths, distinct from borrows into separately allocated class objects (546–571).
- Stack/object/global origin rules and no escape of local storage disguised as a returned reference (573–607).
- Explicit `value class` versus ordinary `class`, rather than lifetime policy inferred from participation in owning relations (V4 §11; O 2288–2337).

These are substantial new requirements. O's borrowed parameters and shared slice backing are intentionally replaced, not compatible alternate modes. O also contains inconsistent copy-on-assignment descriptions; importing one paragraph would not restore one coherent old rule.

### Deliberate restrictions and losses of convenience

- No `permanent class` category. Ordinary objects may remain alive until explicitly destroyed, but no special declaration waives cleanup/lifetime rules.
- No application `destroy()` on a retained value-class instance. Releases, not forced destruction, preserve other counted holders.
- No general manual `ref`/`unref` application protocol. O restricts such statements to `trusted` functions; V4 uses `ref` to name storage and does not adopt those old RC statements.
- O's implicit optional-class dereference is replaced with explicit checked unwrap/pattern access. Its admitted segfault behavior is not a desirable feature gap.
- Reference-count cycles may leak. V4 explicitly separates memory safety from leak freedom rather than repeating O's broad no-leaks claims.

### Remaining contract questions, not algorithm requirements

Cross-call owner provenance is now a required guarantee. Remaining work is to classify concrete cases involving overlapping arguments, captures, recursion, and releases—not to prescribe a fixed-point implementation in the language spec.

Additional useful cases: distinguish an owning counted handle from a borrow into its payload; specify permitted return/escape forms for object-backed references; and test persistent ordinary-class references inside generic containers. V4's general rules constrain these cases, but a signature inventory alone does not demonstrate a usable, complete contract.

## 4. Construction, destruction, and errors

### Stronger V4 contracts

V4 requires constructors to initialize and attach required relationships before success. Fallible constructor bodies return `Result<unit>` while successful construction expressions yield `Result<Class>`. Partial cleanup uses normal generated structural teardown, skips the incomplete object's final hook, and finalizes constructed children normally (V4 §10). O provides constructors but no equivalently complete failure/attachment contract.

V4 specifies final-before-own-detachment, detachment from all parents, strong-child destruction, weak-child detachment, then field release/reclamation. It covers multiple owners, self/cyclic relationships, reentrant-final restrictions, and generated release effects (V4 §§11–12). O specifies final-before-generated-destructor broadly, but not this complete ordering and invalidation model.

`Result<T>` plus concrete `Status` replaces `(T,error)` propagation and the open error interface. V4 defines unit success, unchanged-status propagation across differing success types, unique tagged wrapping, Err(non-OK), explicit discard, closed status categories, and process-fatal checked failures (V4 §§4, 8). These are intentional changes, not compatible syntax aliases.

### What the old model offered that V4 deliberately does not

Custom error classes could carry arbitrary fields under the old `error` interface. V4's Status has only code and message; machine-readable extra results belong to a separately declared operation contract. This loses ad-hoc error-object extensibility but gains a uniform failure vocabulary and avoids dummy success payloads.

O's in-process assertion recovery used setjmp/longjmp. V4 deliberately requires process isolation for a suite continuing after fatal failure. That is a testing execution change with safety implications, not a missing test feature.

### Open integration points

The library still contains historical bool/tuple results. That is not inherently inconsistent—ordinary tuples remain legal—but `?` works only on Result. User code cannot assume that `read_file(path)?` is supported while its inventory says `(string,bool)`. Resolve the public APIs explicitly. Finalizer scheduling boundaries and orderly shutdown remain open; arbitrary background destruction is not allowed by the unspecified-timing rule.

## 5. Relations and multi-class interfaces

### Core preserved from both older documents

V4 retains role-aware cooperating types, labels as compile-time scopes, flat unlabeled injection, same-label legality across different classes, owns/refs independent of representation, multiple parents, abstract Collection relations, whole-relation bindings, method/field adapters, partial impls, ownership-bearing impls, cleanup templates, structural satisfaction, explicit-binding precedence, defaults, and single-parent extends (V4 §§9, 13).

Both endpoint roles remain part of identity throughout constraint resolution, specialization, and effects. This directly addresses R's central four-tuple diagnosis. Receiver type alone cannot choose all other participants. Explicit interface-qualified calls are a clear additional disambiguation facility.

V4 also adds a detailed maintained **Single** contract: optional endpoints, mutation-free refusal, no silent replacement/reparenting, Result<unit>, and teardown obligations. R explicitly deferred its one-link gap (1784–1787).

### Deliberate departures from R

- Capability satisfaction does not inject storage. R 414–422 proposes injection even from a where-clause; V4 requires explicit storage/relationship declarations.
- Parent operations may update a child supplied as an argument under the same relation binding. R's blanket cross-side prohibition (435–440) would obstruct normal two-ended mutation.
- No implicit scope-to-parent conversion; this agrees with R's explicit rejection, though some R examples violate it.
- No universal field/method invocation equivalence; field-to-getter interface satisfaction remains available without it.
- No mandatory prohibition on explicit generic method arguments.
- No constraint-alias declaration mechanism. V4 selects defaults and single-parent interface composition; multiple requirements can still be stated directly.
- Resolved declaration/binding identity, not textual substitution, governs specialization. Exact pass arrangement is implementation design.

### Significant interface gaps

**A. Non-method functions declared inside an interface.** R 1046–1070 explicitly scopes all interface parameters into such a function and gives it an implicit interface constraint, with `ParseGraph<Net,Route,Via>(...)` as the call form. V4 says interface parameters scope over members and provides qualified static operations, but does not clearly specify this non-receiver declaration and its callable namespace. Retain, replace with explicitly constrained free functions, or defer it deliberately.

**B. Whole-interface alias impls.** R 983–1007 provides `impl Collection<Route:out,Via:source> = Collection<Route:a,Via:a>` and grouped aliases. V4 has per-member adapters and whole-relation mappings, but does not retain this general whole-interface alias spelling. Much behavior can be expressed without it; the alias convenience and its exact generality are not currently promised. Constraint-alias grouped impls are naturally absent with constraint aliases, but ordinary interface-to-interface aliasing is a separate question.

**C. Direct method declarations inside impls.** R 764–785/973–981 shows `pub func P.iter(...) { ... }` directly in a multi-class impl. V4 demonstrates alias assignments and inline lambda adapters instead. Its general description does not establish all of R's receiver-implicit/explicit impl-body forms. Decide the accepted form rather than assuming all examples migrate unchanged.

**D. Generic capability vocabulary.** R supplies `Numeric<T>` with zero/one/add/mul and primitive implementations (1175–1202). O lists Comparable, Equatable, and Hashable (509–523). V4 uses Comparable in an example and requires numeric capabilities to be explicit, but does not actually declare the full capability set or supported primitive implementations. Precise concrete arithmetic is not a substitute. This is a real library-contract gap for reusable numeric algorithms.

**E. Concrete method call forms and name collisions.** R explicitly treats `Widget.method(widget,...)` as equivalent to instance-call syntax and forbids field/zero-argument-method name collisions. V4 clearly defines interface-qualified calls and requires method parentheses but does not fully specify the analogous concrete-class long form, method references, or same-name field/method policy. Rejecting property sugar does not settle these independently.

**F. Hint validation.** V4 demands shape/type/role checks and rejects unknown or malformed hints, but leaves the full validation contract open. R is more explicit about exactly two participants and side-qualified members, although its cross-side-body rule must not be imported. Define the valid declaration shapes and cardinality checks, including direct owns/refs impls.

**G. Unlabeled uniqueness.** R explicitly limits unlabeled relations to one per (P,C) pair (296–333). V4 instead states actual namespace collisions are errors (929). Common standard hints will collide anyway, but custom hints with disjoint members expose a difference. Is pair-level uniqueness still required, or are actual collisions the complete rule? This is a semantic choice, not just missing wording.

**H. General object iteration.** V4 defines array, generator, and Collection-scope iteration. R separately defers general `for x in obj` sugar based on an Iterable-shaped capability. V4 does not clearly dispose of that broader protocol. Do not infer it from the relation-scope rule.

### Concrete relation APIs need more than representation names

O spells out ArrayList append/swap-remove, DoublyLinked first/last/next/prev and append/unlink, and HashedList insert/lookup/remove with key requirements (O 1411–1485, 2829–2947). V4 retains the representations and general append/remove obligation but not the complete public surface.

In particular, HashedList's key type, equality/collision contract, insertion/lookup/removal results, and relation-role selection are not specified merely by naming HashedList. Its old raw-u64 key API may need revision; neither restoring hash-only equality nor prescribing linear probing is the answer. Similarly distinguish read access to traversal fields from forbidden independent mutation of relationship backing storage.

The rejection of unlabeled free helper calls is sensible when they cannot identify two same-type relations. But the resulting public scoped methods and qualified alternatives must be explicitly sufficient. Preserve behavior contracts, not every old helper spelling.

## 6. Closures and generators

O offers capturing lambdas and describes pointer-based environments without a complete escape contract. V4 explicitly supports nonescaping closures and defers escaping ones. This is a narrower initial feature set with a stronger safety boundary.

O describes generators via Duff's-device state machines but shows ordinary loop consumption (1921–1940). V4 defines loop-only generation, exactly one syntactic yield, once-only argument evaluation, consumer break/continue/return targets, cleanup, and consumer effects across yield. This is additional source semantics and a deliberate restriction. O's backend description is not proof that arbitrary stored resumable generators or multiple yields worked safely.

The unresolved choices are capture selection, permitted payload borrows, and full accepted/rejected cleanup combinations. A chosen closure representation or lifetime-checking algorithm is not required to complete those language contracts.

## 7. Numeric behavior

V4's major additions are checked signed/unsigned arithmetic, explicit wrapping forms, defined unsafe arithmetic, range-containing widening, operand-type selection without invented third types, mathematical mixed-signed comparisons, checked/modular casts, defined shifts, exceptional division/remainder, IEEE floating behavior, and safe float-to-integer bounds (V4 §14).

O mostly supplies type names, operators, permissive casts, and a tightening roadmap. These precise rules are substantive progress, not feature omissions caused by refusing the old unchecked behavior. V4 retains an explicit initial allowance for C contraction/intermediate behavior, so it does not promise cross-target last-bit reproducibility.

One remaining specification consistency task follows Bill's requirements-only direction: V4 still contains implementation recipes, including closure lowering suggestions (724), unsigned shift lowering detail (1572), guarded C division steps (1580), and the preferred FMA/compiler-option recipe (1622). Preserve observable arithmetic requirements and the historical probe evidence, but move algorithmic recipes into a separate implementation note. This review does not perform that move.

## 8. Concurrency and memory safety

O provides concrete spawn/channel/select/lock syntax and reports pthread implementations, but also admits pointer captures, unenforced guards, and closed-channel ambiguity (1853–1917). R is not a complete concurrency redesign.

V4 introduces a clearer lifetime/storage model: shared existing-object access, exclusive creation/destruction, safe acquisition before reference use, invalidation on unlock, transitive access requirements, stable SoA property reservations, and separation of lifetime from data protection (V4 §15).

The latest V4 requirement is memory validity under thread interleavings, not a promise of application-level atomicity, deadlock freedom, or deterministic results. C undefined data races cannot be accepted as harmless nondeterminism. This is a stronger requirement than O's surface synchronization features establish.

In the reverse direction, V4 lacks final executable contracts for spawn, channels, select, channel closure, cancellation, joins, shared-root publication, and payload guards. Most are expressly open, but **select/multiplexing is not individually inventoried**. The old feature inventory should not vanish merely because its implementation strategy is unsuitable. Keep multiplexing as a named decision; do not silently restore polling, unsafe captures, or zero-on-close.

The proposed complete-lock-set/order discipline remains a proposal. Stable property storage does not stabilize nested dynamic arrays, count updates, or payload races. No concurrency safety or performance result follows just from these documents.

## 9. Library and tool gaps in the reverse direction

V4 §16 preserves broad inventory, but its opening claim to preserve the original surface is stronger than the detail presently supports.

- **StringBuilder construction:** O 1978 and 3073–3095 names `new_string_builder()`; V4 lists builder methods but not that factory or a definite replacement construction contract.
- **Temporary-directory prefix:** O 2033 and 3145 permits an optional prefix; V4 lists only `mkdtemp()`. This is a specific lost API detail, not resolved by the general defaults question.
- **Sym accessors:** O names `get_name()` and `get_hash()`; V4 says name/hash are exposed without committing to exact accessors. Choose the public API.
- **Hash/equality interfaces:** V4 requires collision-safe equality but does not yet fully declare methods, supported key types, and laws. Raw-string keys are explicitly undecided. The old documents contradict themselves on string-key literals versus banning Hashable<string>.
- **Concatenation:** string and array `+` are explicit in O; V4 correctly records them as unresolved. This is a deferred user feature, not something already supplied by the numeric `+` rule.
- **String/byte conversions:** O promises zero-copy views. V4 leaves ownership/mutation contracts open instead of silently violating its independent array values. This is an intentional unresolved compatibility point.
- **Universal stringification:** O specifies enum names and struct/class field dumps. V4 promises generate-or-dispatch text but not the exact formatting, recursion/cycle handling, or customization contract. Generic debug output is useful; the old unconditional field dump should not be copied without considering cycles and private data.
- **Planned I/O:** O names Reader/Writer, read_line/read_all, buffered writes, mkdir/remove/rename/stat, standard streams, and TCP operations (3150–3219). V4 compresses them to broad planned categories. These were roadmap features, not verified old implementations, but detailed intent is no longer fully represented. A separate library requirements document can preserve it without filling the core spec with backend recipes.
- **Small conveniences:** starts_with/ends_with aliases, os_exit, exact str_* signatures, approximate float assertions, per-test timing, directory discovery, and unique-prefix commands are not all explicitly preserved. Give them a retain/drop/defer disposition if compatibility matters; they do not all deserve implementation priority.

Several library contracts are incomplete in both generations: empty pop, mutation while iterating an ordinary array, dictionary-entry validity, sorting/equality details, formatting failure, and operating-system error semantics. V4's candid open list is an improvement, but not completion.

## 10. Historical contradictions that must not become current requirements

1. O claims copy-on-assign while also specifying shared slice backing and shared arguments. V4 deliberately chooses recursive value independence.
2. O's relation chapter mixes prefix terminology with dotted scopes; R explicitly replaces prefix-based semantic identity. Mangled backend names do not decide source-level identity.
3. R's header says not adopted, but later sections report shipped phases. Its later default-method-emission account remains deferred even where O describes working specialization. These are historical phases, not one reliable implementation status.
4. R rejects implicit parent projection, yet its explicit adapter returns `self.a` as Route? (979), a scope rather than `.parent`. That example should not override the stated rejection.
5. R says function generic placement is unchanged while showing `func<T> name`, unlike O's `func name<T>`.
6. R's early architecture chooses constraint aliases/free algorithms; later phases ship extends/defaults and defer relation equivalence. V4 adopts a particular coherent direction, not every paragraph simultaneously.
7. R suggests hash-only matching works because Sym is interned. Interning does not prove distinct strings cannot have equal hashes. O also offers inconsistent string-key examples. No implementation bug is alleged without code inspection; the current requirement must be actual equality on collisions.
8. O's claims of no borrow checker/no leaks and fastest/most-safe outcomes exceed the supplied semantics and evidence. V4 correctly avoids inheriting them.

## 11. Recommended disposition and order

### First: close the contracts most central to the design

1. Specify public ArrayList/DoublyLinked/HashedList behavior, including refusal, duplicates, ordering, current-item iteration, and key equality. Do not prescribe probing or storage algorithms beyond approved representation requirements.
2. Specify capability vocabulary and interface callable/binding forms: Numeric/Comparable/Equatable/Hashable, interface-local functions, direct impl methods, whole-interface aliases, and concrete qualified calls.
3. Finish the accepted/rejected memory-safety cases, including calls, counted releases, retained handles versus borrows, generic persistent references, and finalization boundaries. Keep the checker algorithm separate.
4. Complete the ordinary container/string contracts needed by a compiler: value/borrow behavior, equality, array iteration mutations, dictionary entry validity, and failure returns.

### Then: settle bounded syntax and compatibility decisions

Resolve the outstanding PEG choices, enum-name inference, readonly borrowing, generic struct exposition, alias spellings, and the explicit smaller API omissions. A decision to omit is legitimate; silence is not an adequate compatibility record.

### Separately: concurrent language support

Retain the class-lock design and memory-safety requirements. Specify safe publication, payload protection, counted updates, spawn/communication/multiplexing, and blocking contracts before claiming full concurrent-language coverage. Do not confuse this with proving arbitrary concurrent application logic correct.

### Editorial follow-through

Move implementation recipes out of the normative requirements document, qualify the library-preservation claim, and fold these newly identified decisions into §19 only after their wording is reviewed. The old reconstruction snapshot and its receipts should remain historical, not be overwritten to look as if the latest additions were present at promotion.

## Bottom line

V4 is a stronger and more coherent language design, not merely a renamed original or a verbatim adoption of R. Its biggest gains are explicit lifetime/value/error rules and complete relation-binding identity. Its most important losses of detail are in public APIs and interface usage contracts, not the central multi-class idea. Most can be addressed by stating requirements and decisions, without adding a new mechanism or an implementation algorithm.

A document can preserve every source paragraph from its immediate predecessor and still omit behavior present in older sources. Coverage, consistency, completeness, and implemented correctness are separate checks. This review establishes documentary findings only; it does not establish parser acceptance, compiler conformance, memory safety, or runtime performance.
