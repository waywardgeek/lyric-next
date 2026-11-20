# Lyric specification comparison and completion review

## Conclusion

The KISS draft was not a full language specification. It retained the architectural and lifetime direction but replaced too much concrete language material with summaries. The explicit-lifetime update and restored appendices improve coverage; they do not close every semantic gap.

KISS should reduce independent mechanisms and surprising exceptions. It should not remove the information needed to implement and use a mechanism correctly. Bill explicitly permits new syntax that makes the language natural and reliable for AI collaborators. Compatibility is a useful baseline, not a veto on improvements.

This is a document comparison, not a compiler audit. Old claims that features are implemented or tested have not been independently verified against source or execution. The preserved sources are byte-identical to those read for this task.

## Sources and notation

- **S:** `background/lyric-language-spec.md`, 3,450 lines.
- **R:** `background/multi-class-interface-redesign.md`, 1,834 lines. Its early proposal and later implementation notes sometimes describe different directions.
- **Q:** `background/lyric-language-reference.md`, 855 lines.
- **D:** current `lyric-language-spec.md`, Draft 3.2. Earlier assessment paragraphs are comparison history; later decision updates supersede their open-status claims. Numbered sections and appendices are used instead of unstable current line numbers.

Source line ranges refer to the preserved copies. The full documents were read earlier; targeted passages were reread for this comparison, including S440–733, S1036–1325, S1944–2415, and R705–1250. The comparison also uses the remaining full text already read, not only headings or snippets.

Dispositions:

- **Keep:** retain the behavior, with complete rules.
- **Restore:** material omitted from D without a language simplification to justify it.
- **Change:** deliberate proposed or accepted semantic/syntax difference.
- **Resolve:** conflicting or incomplete contracts; requires a concrete decision or experiment.
- **Exclude:** historical bug, dead feature, or external tool concern, not part of the new language.

## 1. Declaration and organization coverage

### Packages, modules, and imports

**Source:** S146–283; Q9–27, 793–797, 819–831. **Draft:** D2, Appendix E.

Keep package-wide namespaces, order-independent declarations, qualified imports, `.ly` source, and a module marker. Restore all accepted import forms or explicitly remove redundant ones. Specify module root discovery, package identity, recursive imports, dependency lookup, duplicate imports, entry-point selection, and visibility.

The old source describes whole-module compilation but also documents only single-level import resolution. That limitation is not a normative reason to ignore imports of imports. The new architecture's shared declarations across functional directories need an actual assembly rule; “no wrapper objects” is a requirement, not an implementation of it.

**Status:** partial. Full module grammar and declaration/definition linking remain missing.

### Declaration-only files and CDD

**Source:** S29–34, 2566–2598, 3366–3388. **Draft:** D2.

Keep `.lyric` for declaration-only language files unless another syntax demonstrably helps. No need for the earlier `.lyd` and `implement` additions. Exclude CDD annotation parsing, three-zone metadata maintenance, and `lyre` commands from core language obligations. If architecture requires a new linking declaration, evaluate it on a complete small module rather than prohibit it for familiarity's sake.

**Status:** conceptual boundary clear; precise linking incomplete.

### Visibility and naming

**Source:** S535–603. **Draft:** D2–3; Appendix A.

Keep case-sensitive exact names and explicit `pub`; naming conventions are not parser semantics. Restore field visibility, modifier placement, imported-member checks, shadowing/duplicate declarations, and private access rules. The old missing `pub` enforcement and capitalization-based pattern hack are defects to exclude.

**Status:** basic policy retained; full name-resolution and access rules missing.

## 2. Lexical, value, and expression coverage

### Literals, comments, and line termination

**Source:** S318–339, 1820–1849, 2341–2415. **Draft:** D3–4; Appendix A.

Restore exact escapes, byte characters, triple strings, interpolation, symbols, decimal separators, and newline handling. The recent appendices restore these baseline forms. The earlier draft's operator-at-end-of-line continuation was an unneeded divergence and has been removed.

Hex/octal/binary forms, suffixes, Unicode escapes, exponent syntax, and block comments need a chosen grammar. The old source marks several as roadmap; neither claiming them implemented nor silently forbidding them forever is appropriate.

Triple-string trimming is retained as a compatibility baseline, not claimed to be the only good syntax. Whitespace-preserving strings may be preferable, but the change needs an explicit contract.

**Status:** substantial restored coverage; formal lexical grammar and keyword classification still missing.

### Primitive types and conversions

**Source:** S286–346, 1653–1674. **Draft:** D4, D18; Appendix A.

Keep the fixed-width initial tower. Restore or explicitly exclude `int`, `uint`, `any`, and larger reserved widths. `int` is described as platform width in Q and C-int interop elsewhere; choose a contract before retaining the name.

The old unrestricted casts, lossy implicit cross-sign assignments, and null assignable to everything are not safe-language rules. A complete replacement must specify promotion, narrowing, overflow, shifts, float conversion, NaN comparisons, and division failure. Merely saying “no C undefined behavior” is insufficient.

**Status:** major numeric semantic gap remains.

### Arrays, tuple forms, and type aliases

**Source:** S350–437, 1489–1563. **Draft:** D4, D12; Appendices A–B.

Restore all slice endpoint forms, positional tuple access, destructuring, transparent aliases, and type inference for empty literals. The new appendices restore the selection forms but do not settle backing-store behavior.

The source conflicts: S1498–1501 and S2293–2294 say descriptor copy/shared backing, while S1559–1563 contrasts assignment copying with parameter sharing; Q153 also describes variable-assignment backing copies. We cannot settle this by quoting only one sentence.

**Decision update:** Bill approved value semantics for ordinary array assignment: changing the copy's elements or length does not change the source. Class-reference elements copy references, not objects. `let` declares an immutable value and `var` a mutable one; reference parameters now use `name: ref T` and calls use `ref storage`, superseding `mut`. Multilevel array copying and object ownership are settled; precise ordinary parameter and range-view contracts still need completion. The original disagreement above is historical evidence, not a still-open ordinary-assignment choice.

### Structs and field literals

**Source:** S773–796. **Draft:** D5; Appendix B.

Restore field defaults, omitted-field checks, duplicate-field checks, named construction, and empty structs. Decide whether positional construction is worth the extra ambiguity rule. The old “no methods on structs” rule was omitted, not explicitly adopted or rejected; decide rather than let omission change the language.

The C empty-struct placeholder is backend detail, not source semantics.

**Status:** field construction partly restored; value/default and method policy still needs a final rule.

### Bindings and assignment

**Source:** S1489–1563. **Draft:** D3, D10; Appendix B.

Restore uninitialized locals with definite assignment, `_`, tuple bindings, mutability, shadowing, and top-level initialization. Keep binding immutability distinct from object immutability. Preserve ref-binding vocabulary while deciding its arrayref meaning; do not conflate the old zero-copy slice view with a reference to one array slot.

**Status:** baseline forms restored; initialization order and ref semantics incomplete.

### Operators

**Source:** S735–769, 2375–2386. **Draft:** D18; Appendix A.

The precedence table is now restored with integer bitwise operators above comparisons, reflecting Bill's original direction. List supported unary and compound assignments explicitly. Define associativity, checked versus wrapping arithmetic, destination evaluation, and expression sequencing.

An array compound assignment can hold a destination across a destructive/resizing RHS. This must be checked over the actual evaluation order, not optimized into a stale interior pointer.

**Status:** precedence coverage restored; arithmetic semantics still missing.

### Enums, patterns, and unions

**Source:** S800–907, 1636–1649, 1799–1816. **Draft:** D5, D14; Appendix B.

Restore qualified variants, nested patterns, tuples, alternatives, guards, literals, wildcard/binding rules, `is`, if-let, and let-else. Define exhaustiveness and branch-result typing. The old first-branch-only checker is a defect.

Tagged-union assignability wording in the original is confusing; the new appendix explicitly requires every source alternative to be accepted by the target. A full spec must also define duplicate alternatives, ordering/normalization, recursive aliases, and overlap between variant constructors and ordinary names.

**Status:** substantial restored coverage; exact type/pattern rules still need completion.

### Optionals

**Source:** S1678–1756. **Draft:** D4, D14; Appendix B.

Restore wrapping, null tests, unwrap, conditional extraction, and lvalue unwrap. Exclude segfaulting automatic optional-class dereference. Changing optional receiver syntax should be documented as a deliberate safety simplification.

`error?` is a proposed consistency change from the old secretly nullable `error`, not an already accepted decision. Nested optional representation and default initialization need specification.

**Status:** core operations restored; a few type-level rules and the error choice remain.

## 3. Functions and generic coverage

### Calls, methods, and mutable parameters

**Source:** S607–707; R705–825. **Draft:** D3, D5, D13.

Restore complete parameter/call matching, result typing, static calls, receiver qualification, and mutable element arguments. The original full spec says `mut` on classes is a no-op; Q warns it can segfault. The implementation defect cannot become the language rule. Choose reject-as-redundant or accept-as-no-op consistently.

`Node.Node` as an external constructor definition follows normal qualification but was introduced in D without a complete grammar. The grammar and examples must make declaration/call/definition forms unambiguous.

**Status:** method vocabulary present; full call/lvalue grammar incomplete.

### Lambdas and capture

**Source:** S643–665; Q545–552. **Draft:** D13.

The original has both parenthesized and pipe lambda forms. D omitted both concrete forms. Select one or retain both with an explicit reason; KISS does not mean forgetting closure syntax.

Capture-by-pointer is an old implementation technique, not safe semantics for escaping closures. Define capture mode, capture lifetime, mutation, and release behavior. Restrict unsupported escapes while implementation is incomplete, without declaring a permanent arbitrary exclusion.

**Status:** major syntax and lifetime gap remains.

### Generic functions and constraints

**Source:** S440–531; R1140–1202. **Draft:** D3, D15–16; Appendix C.

Restore explicit type variables, call-site inference, repeated-variable consistency, nested type syntax, inline constraints, and relational where-clauses. Appendix C now restores the major forms.

The original and redesign disagree on generic declaration spelling: `func name<T>` versus `func<T> name`. D chooses the former. That is a syntax choice, not a claim the sources agree.

Built-in Comparable, Equatable, Hashable, and Numeric require actual contracts and satisfaction rules. Do not replace them with keyword-like names whose behavior only the compiler knows.

**Status:** concrete forms restored; complete inference/conversion and capability contracts unfinished.

### Generic classes and partial impls

**Source:** S966–974, 1162–1179; R1609–1617. **Draft:** Appendix C.

Partial `impl<T>` declarations were entirely omitted from the KISS draft. Restored syntax and the full-participant specialization requirement. Class lifetime category must survive instantiation; `Box<T>` cannot use generic abstraction to hide an illegal stored reference.

**Status:** restored outline and example; overlap/coherence and substitution rules still require precision.

### Generators

**Source:** S1921–1940. **Draft:** D4, D13.

D lists `gen T` but provides no full `yield` syntax, suspension, completion, or early-close semantics. The old source gives a working range example but not a complete lifetime model. Restore the concrete forms and define cleanup when iteration stops early, held references across suspension, and what a generator owns.

Duff's-device lowering is an implementation option, not a semantic definition.

**Status:** major missing language section.

## 4. Classes and lifetime coverage

### Class construction and conformance

**Source:** S911–1032. **Draft:** D5, D7, D9; Appendix B.

Preserve class identity, fields, constructors, generics, and no inheritance. Field literals must not bypass constructor attachment. `implements` must either be checked via ordinary conformance rules or intentionally removed; its old declarative-only behavior is not acceptable.

The new constructor postconditions are stronger than the old description. They need checkable definitions for successful/failed paths and escape, not just prose saying “the compiler checks.”

**Status:** direction stronger; construction checking incomplete.

### Explicit lifetime category — accepted change

**Source:** S2288–2337; earlier inferred owned/permanent/RC model. **Draft:** D1, D5, D9.

Bill approved `value class` for reference-counted, possibly mutable value-like objects and ordinary `class` for explicit/cascading lifetime. This supersedes inference from whether an owning relation happens to mention the class.

Value objects cannot be user-force-destroyed while other references retain them. Last release invokes final/generated cleanup. The old immutable-only restriction is removed. Both categories may have final methods. A value class is not a struct and does not imply deep-copy assignment.

**Status:** accepted core rule updated. Borrowed arguments, move elision, release timing, and container contracts still require completion.

### Generated destruction and final hooks

**Source:** S976–1012, 2676–2707; R604–701. **Draft:** D8–11.

Retain final-before-own-structural-destruction, cascade versus unlink, and child detachment. D adds explicit analysis of transitive destruction and multiple-parent cleanup obligations.

The original does not establish safe reentrancy, cycles, destructor ordering, or partially torn-down ancestor observations. The previous draft's restrictive finalization-view mechanism has been removed, not replaced by a proof that every hook is safe. Work through those examples before adopting an algorithm.

**Status:** essential unresolved operational contract.

### Permanent allocation and manual ref operations

**Source:** S1006–1012, 2315–2323, 2553–2554. **Draft:** no final decision.

`permanent`, manual trusted `ref`/`unref`, and RC-disabling flags are not addressed completely in D. Their omission is not an accepted removal. Determine whether permanent allocation is necessary or merely historical compiler convenience. Keep manual counting behind an explicit audited boundary if needed. A safe build must not quietly change lifetime semantics because of an optimization/debug flag.

**Status:** explicit disposition still needed.

### Static invalidation — new required semantics

**Source:** old roadmap S2794–2803, 3305–3314; current human discussion. **Draft:** D10–12.

The new contract advances well beyond the old roadmap: class-wide invalidation, helper-specific current-object effects for loops, counted-release effects, aggregate aliases, and root-relative array-slot references.

A full specification needs transfer rules for each expression/statement, joins/loops, calls/results, generated cleanup, and opaque boundaries. Saying “fixed point” names an algorithm family, not a complete soundness argument.

**Status:** strong new core, not yet a complete formal checking specification.

## 5. Relationship and interface coverage

### Relation syntax, cardinality, and labels

**Source:** S1328–1485; R211–346. **Draft:** D6, D16–17.

Keep owns/refs independent of storage, labels as scopes, flat unlabeled injection, mixed labeled/unlabeled endpoints, same labels on different classes, and collision checks. Restore full generic endpoint syntax and single-child cardinality rules.

The original “at most one unlabeled per type pair” is not sufficient by itself: different type pairs can still inject colliding members on a shared class. The final spec needs both relationship identity and actual namespace collision rules.

**Status:** central semantics retained; full grammar/diagnostics and single-child library contract incomplete.

### Representation contracts

**Source:** S1411–1485, 2829–2947. **Draft:** D6, D11, D17.

Restore ArrayList swap removal, DoublyLinked links, HashedList bucket behavior, concrete operations, both endpoint cleanup, and iterator invariants. The old 75% load factor is an implementation choice unless exposed as a guarantee; mutation semantics and complexity bounds are the useful contract.

Hash-only matching is not collision-correct. State the new equality requirement clearly rather than preserving the old defect.

**Status:** mostly overview in main draft; library details still needed.

### Multi-class contracts and defaults

**Source:** S1036–1089; R1516–1635. **Draft:** D15–16.

Keep several cooperating types in one interface, complete binding substitution, default algorithms, and no runtime inheritance. The source status claims conflict: S presents defaults as working, while later R notes unresolved multi-class emission. The fresh design should specify behavior without claiming the old compiler implements it.

**Status:** conceptual contract retained; full default/override conflict rules incomplete.

### Impl forms

**Source:** S1091–1179, 1254–1304; R500–701, 944–1007. **Draft:** D15–17; Appendix C.

Restore aliases, inline adapters, field `<->` bindings, empty/mixed derivation, partial impls, per-participant labels, and owns/refs-bearing impls. Much of this was simply missing from D and is now restored in Appendix C.

Define explicit-versus-derived selection, overlapping implementations, exact signature matching, field mutability, and alias cycles. “An explicit impl wins” does not resolve two explicit candidates.

**Status:** surface restored; complete coherence rules missing.

### Abstract relation bindings

**Source:** S1181–1209; R1640–1689. **Draft:** D16.

Retain the redesign's relation equivalence forms, mapping both endpoints at once. This is a justified new syntax because it replaces several accessor adapters without duplicating state.

Collection must expose its actual operations and iteration contract in the library/interface declaration. D no longer makes it secretly compiler-special. The exact safe iteration capability remains to be specified.

**Status:** central syntax present; capability declaration and iteration proof interface incomplete.

### Composition choices

**Source:** S1065–1089; R884–942, 1072–1083, 1584–1593. **Draft:** D16.

D chooses single-parent extends/defaults and defers separate constraint aliases. This is a deliberate simplification proposal, not a faithful reproduction of every layer of R. The report preserves the disagreement so it can be reviewed rather than erased.

**Status:** proposed direction; adequate examples should establish whether it covers required composition.

### Property syntax and explicit method generics

**Source:** R725–835. **Draft:** D13.

D requires parentheses for actions and allows explicit method generic arguments under ordinary generic rules. These intentionally differ from R's universal zero-arg property invocation and prohibition on `obj.method<T>()`.

Bill allows new syntax that improves regularity. Evaluate these on parsing, callable-valued fields, inference, and effect visibility—not merely compatibility. Bound method values still need a defined spelling/lifetime.

**Status:** explicit design differences; callable grammar not complete.

### Hint shape and cross-side access

**Source:** R377–498. **Draft:** D17.

Keep two participants and side-qualified declarations. Correct the literal “no cross-side references” restriction: append must be able to update a supplied child's backlink. Receiver-correct access through the same binding is the useful rule. A malformed hint must produce an error, not be silently skipped.

**Status:** necessary clarification documented; trusted-invariant boundary remains.

## 6. Runtime and library coverage

### Errors

**Source:** S1307–1324, 1760–1795, 3049–3070. **Draft:** D14; Appendices B/D.

Retain ordinary message-bearing errors and propagation. Proposed `error?` makes absence explicit. Specify success/error tuple consistency, cleanup on propagation, nested `?`, and concrete error lifetime under explicit value classes. Detect results by resolved type, not a field named “error.”

**Status:** semantic and syntax choice still needs confirmation; concrete error implementation example needed.

### Built-ins and methods

**Source:** S1944–2110. **Draft:** Appendix D.

These were largely missing, not intentionally simplified. Restored the inventory of core operations, conversion, arrays, strings, symbols, formatting, and character predicates. Decide canonical spellings rather than accidentally drop aliases.

An inventory is not enough: empty pop, splitting by empty separator, failed conversion, invalid ranges, encoding, allocation, and mutation effects need explicit behavior.

**Status:** inventory restored; full operation contracts still incomplete.

### Dict, Sym, and StringBuilder

**Source:** S2949–3096. **Draft:** D18; Appendix D.

Restore constructor/method/results and classify value lifetimes. The original contradictory string-key literal versus non-Hashable-string policy must be reconciled. DictEntry references after removal and rehash are a lifetime concern, not only a hash-table detail.

**Status:** partial restored surface; lifetime/edge contracts remain.

### I/O

**Source:** S2017–2033, 3124–3219. **Draft:** Appendix D.

Restore the minimal file/path/process API and distinguish planned Reader/Writer/network operations. Bool-returning versus error-returning APIs require a deliberate policy. Subprocess exit versus launch failure, cleanup, cancellation, and byte/text behavior must not be guessed.

**Status:** signatures restored; library specification incomplete.

### Concurrency

**Source:** S1853–1917; R has no complete replacement. **Draft:** D19.

The original has actual spawn/channel/select/lock syntax and documented unsafe capture/polling/closure limitations. D contains only a safety warning. A full intended-language spec needs the syntax and operational rules, or an explicit scoped exclusion, not an indefinite paragraph.

Shared lifetime cannot be proved by sequential invalidation alone. This remains a genuine design boundary, not an omission fixed by copying old pthread details.

**Status:** major design and specification gap.

### Tests and command-line behavior

**Source:** S2114–2284, 2533–2562. **Draft:** D21; Appendix E.

Restore test discovery, signatures, assertion results, source locations, continuation, exit status, commands, and flags. The recent appendix restores baseline behavior without mandating the prior draft's new per-process runner.

Ensure failed tests do not leave corrupted state when continuing. Command aliases, directory selection, outputs, and formatter scope require concrete contracts. Distinguish compiler tooling from the language proper without omitting it from the project specification.

**Status:** baseline restored; execution/isolation and full CLI details remain.

## 7. Material that should not be copied as language semantics

- Old bugs: unrestricted casts, untyped null assignment, missing visibility checking, incomplete branch typing, dangling captures, hash-only equality, and unchecked conformance.
- Dead/stub syntax: `map[K]V` producing null, unreachable defer/cascade remnants, and obsolete Go backend aliases.
- Historical progress: commit hashes, partial implementation statuses, pass counts, LOC estimates, and migration archaeology belong in historical background, not normative rules.
- CDD metadata: external `lyre` concern, not implicit new language grammar.
- Unsupported marketing claims: fastest language, perfect memory management, or general superiority are not verified consequences of the draft.

## 8. What was changed in this pass

1. Archived the pre-lifetime KISS draft unchanged.
2. Added explicit mutable `value class` versus ordinary `class` lifetime policy throughout the current draft.
3. Removed the stale claim that the policy is inferred from relation participation.
4. Disallowed user-forced destruction of retained value objects; preserved generated cleanup at count zero.
5. Restored concrete lexical, expression, binding, pattern, generic/impl, library, testing, and toolchain coverage in appendices.
6. Reverted the gratuitous operator-based newline continuation rule to the original parenthesis/bracket rule.
7. Recorded intentional differences separately from omissions and historical defects.

No compiler examples were executed. No implementation, installation, or repository publication occurred.

## 9. Completion criteria for a full specification

For every supported construct, the final specification must answer:

- How is it parsed, including ambiguity and precedence?
- Which names and types are in scope?
- What type does it produce, and what conversions are permitted?
- What executes, in what order, and what state changes?
- What is retained, released, invalidated, or destroyed on success and failure?
- Which errors are compile-time diagnostics versus checked runtime failures?
- Which combinations are deliberately excluded?
- Which positive and negative examples verify the rule?

Finish the existing mechanisms before adding new ones. First close the lifetime/array/destruction and module-assembly choices, then complete the grammar and operation contracts around those decisions. A full language specification can be long while the language remains small.

## 10. Decision update: closures and generators

Bill accepted concrete compiler-generated closure environments, statically specialized calls, and an initial nonescaping closure subset. The draft now specifies loop-only generators with exactly one syntactic yield site, which may execute repeatedly. Producer/consumer expansion preserves consumer continue, break, return, and scope cleanup, and analyzes destructive effects across yield.

This supersedes the earlier report's description of generators as a wholly undecided design gap. The old spec S1921–1940 and reference Q664–675 describe a Duff's-device state machine; the older `multi-class-interfaces-design.md` lines 146–174 also describes iterator state machines. Those passages do not state a one-yield restriction. The new ruling is an intentional design choice, not a verified account of the old compiler implementation.

Remaining work includes precise capture-selection and expansion/checking rules. Escaping closures, multiple yield sites, and independently resumable generator values are explicitly deferred. No compiler tests have been run for the new design.

## 11. Decision update: reference lifetime, finalization, and class locking

Draft 3.2 incorporates the subsequent discussion at the original semantic sections, not only in this report:

- `ref T` parameters and `ref storage` expressions replace `mut`; let/var remain declaration forms. Borrowed parameter references do not escape; value destinations receive copies.
- Arrays copy by value through all nested array levels. Array properties belong to their containing objects; the initial locking model does not rely on shared mutable or copy-on-write backing between owners.
- Value finalization has unspecified safe timing, not undefined behavior. Destruction remains valid and once-only when it occurs; eligible-finalizer execution must honor effects and required locks.
- Constructors may fail with an error. Cleanup-safe default state lets the generated destructor clean partial construction without a separate rollback ledger. The failed object's final hook is skipped; fully constructed children follow ordinary cleanup.
- Normal destruction calls final first, then detaches all parents, destroys strong children, detaches weak children, and releases storage. Final-last was corrected in the conversation and is not the rule.
- One class lock protects the class's objects, owned arrays, allocation slots, and SoA storage. Borrowed references require continuous protection and cannot be revived by reacquiring the lock. Same-thread destruction still invalidates them.
- A complete transitive lock set acquired in a global order before an operation is the concrete proposed initial acquisition policy. Mid-operation acquisition of new locks is rejected under that proposal; syntax remains for review.

The earlier report's statements that failed-construction cleanup, multilevel array ownership, finalizer timing, and all concurrency protection are undecided are superseded. Final-hook reentrancy checks, class-lock syntax/global guards, safe publication between threads, spawn/channel semantics, and several library/type-system contracts remain unfinished. The draft contains additional positive/negative conformance obligations but none has been executed against a new compiler.


## Decision update: shared/exclusive lifetime and allocation access

This update supersedes earlier claims that the class lock exclusively protects all payload access, and the separate allocator-mutex proposal. Bill approved one reader–writer lock per class: shared access permits references to existing instances, while creation and destruction require exclusive access. Exclusive access protects free lists, slot reuse, allocator metadata and backing-storage updates; generated helpers require held access and need no additional per-class allocator mutex. This intentionally blocks creation while other threads use the class. Payload/relationship/owned-array synchronization is separate: shared lifetime access does not serialize mutation or counted updates.

The current specification is Draft 3.3, Section 19. Access-region syntax, full-set/global-order acquisition, ordering with data guards, reference-count synchronization, writer progress, publication and blocking contracts remain unfinished. No compiler, runtime, lock benchmark or concurrency test was implemented. The lesson is to give allocator state one synchronization owner while keeping lifetime safety distinct from data-race protection.
