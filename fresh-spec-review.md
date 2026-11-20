# Fresh full-document comparison after handoff

## Scope and evidence

Read in full after the handoff:

- `lyric-language-spec.md`, Draft 3.2, lines 1–1294.
- `background/lyric-language-spec.md`, lines 1–3450.
- `background/multi-class-interface-redesign.md`, lines 1–1834.

All three reads delivered the complete selected source without truncation. This is a specification review, not inspection or execution of the old compiler. Statements about its implementation in the background documents remain attributed claims. Line references below refer to these versions before further edits.

The current draft is a substantially stronger lifetime design, but is not yet a complete language specification. Missing rules are not all new decisions for Bill: several are documentation omissions that can be restored, while others require a concrete design choice. This report changes no language semantics.

## Decisions correctly retained

The draft retains explicit `value class` versus ordinary `class`, mutable value objects, `let`/`var`/`ref`, independent nested array values, owned array properties, generated cleanup of failed construction without its final hook, normal final-before-detach-before-cascade ordering, class-wide destruction effects, current-item-only safe iteration, loop-only single-yield generators, nonescaping closures, fat references, and per-class locking. Those decisions should not be reopened merely because implementation has not begun.

It also retains the redesign's most important improvement: relationship identity includes endpoint roles, not merely the participating class types. Labeled scopes, whole-relation bindings, multiple parents, and representation-independent algorithms remain central.

## Blocking semantic gaps

### 1. Declaration ownership and implementation access

New §§2,20 (lines 41–67,850–876); original Modules and Packages (146–283), external methods (628–641).

The new draft says a package is a directory, names are package-private, Common owns declarations, and functional implementations live in other directories. It leaves the assembly mechanism open. Consequently the reader cannot determine how `parser/parser.ly` implements a method declared on a private type in `common/model.lyric`, what private fields that body may access, or how duplicate bodies are rejected.

Required contract: logical declaration identity, implementation association, access rights of the associated body, generic parameter scope, exact signature matching, and one-body enforcement. This is central to the requested architecture, not merely build-tool configuration. Do not solve it by wrapper objects or by making all fields public.

### 2. Reference safety must hold during a call, not only after it

New §§10,12 (390–418,533–567).

Class-wide invalidation after a call is necessary but not sufficient by itself. For example, a helper can receive a reference into a Node and another argument through which it destroys that Node, then use the first argument before returning. Its surface `ref u32` type does not identify Node. The draft acknowledges provenance metadata but does not define how that metadata and effects are checked together across actual call bindings.

Likewise, two `ref` arguments may overlap; resizing through one may change the path resolved by the other. Define sequential evaluation, provenance substitution, caller-held aliases, and checking of the callee body under the actual bindings. Opaque functions require equivalent conservative contracts. A blanket no-alias rule is not assumed or recommended without evidence.

Positive test: a helper mutates a live scalar field through `ref`.
Negative test: it destroys the owner through another argument, then reads the borrowed field.

### 3. Reference variables still lack a complete expression contract

New lines 514–559,917; original Variables and Constants (1489–1563).

`let slot = ref array[i]`, implicit value reads, and mutable `ref` parameters are shown. Assignment through a named reference, copying a reference binding, rebinding a mutable reference variable, forwarding an existing reference, and references to optionals/unions are not all defined. In particular, replacing an optional or tagged-union payload can invalidate an interior reference without destroying a class or resizing an array.

Needed examples include `slot = 3`, `let other = slot`, `let other = ref slot`, and forwarding `slot` to another `ref T` parameter. They must say whether a value or storage location is selected and avoid implicitly inventing heap ownership. Array references also need a settled answer for replacement of intermediate arrays. Slice selection and slice references still require exact types, range behavior, and resizing permissions.

### 4. Fallible construction needs a representable failure result

New §§7,14 (287–307,645–658); original Errors (1760–1795).

The draft promises an object/error result with no usable object on failure, while its ordinary tuple convention is `(T, error?)`. A nonnullable class T cannot contain a fabricated null or invalid object in the failure branch. The text explicitly postpones the representation, so `Connection(...) ?` cannot yet be fully type-checked from this specification.

Choose and explain a result representation or a precisely specified special construction result. Apply `?` consistently to constructor bodies returning `error?`, value-producing functions, and cleanup. The old compiler's name-based tuple lowering is not a language contract to inherit.

### 5. Final hooks and deferred release need executable validity rules

New §§8–10,19 (311–418,834–838).

The destruction ordering is settled. Reentrancy through `final` remains unresolved: final runs while parent links still exist, so a hook can call a path that attempts to destroy itself again. The draft says reject cases not proved safe, but does not define the proof obligations or accepted subset. Do not claim that parent detachment prevents this earlier reentrancy.

Class-wide invalidation also affects generated destruction: deleting a same-class child can invalidate the active receiver under a naive implementation of the rule. The generated operation needs a precise justified treatment of its active object, not an unexplained immunity. Detachment and restrictions on resurrection may provide that justification; adversarial same-class cycles and final callbacks must demonstrate it.

Flexible value-finalizer timing is accepted. The permitted execution boundaries still need definition so inferred effects remain meaningful. The draft's added eventual-reclamation wording is stronger than merely saying timing is unspecified; preserve it as a proposal pending review, not an independently established guarantee.

### 6. Class locking versus shared class allocation

New §19 (777–844).

The draft requires class locks for shared-object access and allows thread-local values without locks. It does not fully specify object confinement. A thread-local object can nevertheless live in a class-wide SoA allocation also used by another thread. Growth can relocate shared field arrays even if those threads access different objects. Payload nonsharing alone therefore does not justify lock elision for that storage.

Define the baseline acquisition rule for all class-storage access, and the proof needed for any elision or allocator partitioning. Distinguish local standalone arrays from object fields in a shared class slab. Keep the complete-set/fixed-order lock policy explicitly proposed until accepted. Publication, spawn, channels, thread termination, and globals remain genuine missing operational contracts.

## Interface coverage that needs restoration or resolution

### 7. Labeled constraints and implementation selection

New §§15–17, Appendix C; redesign §§3.8,5.3–5.5,6.4 (500–602,921–1007,1226–1249).

The new draft explains labeled impls and whole-relation bindings, but its where-clause examples are unlabeled. Restore an explicit labeled constraint example and the rule connecting its abstract role to the selected concrete role. Define whether structural satisfaction works with no impl at all or only through an empty impl. The early redesign permits no-impl derivation; later implementation notes describe explicit impl validation. They are not interchangeable policies.

Also define a call when one concrete receiver participates in two distinct implementations of a multi-class interface. Receiver type alone cannot select the remaining participants or relation roles. State ambiguity behavior and an explicit disambiguation form. Specialization keys must include the selected implementation and all role bindings, as the new draft correctly requires.

### 8. Relation hint shape and one-to-one contracts

New §§6,16,17 (211–259,684–757); redesign §3.7 (414–491), §11 (1784–1787).

The new draft says hint shape is checked but does not state the full shape. Restore the two-participant requirement and explicit side ownership of injected declarations. Preserve the intentional correction that a parent method can update a child's backlink through an actual child argument; do not restore the redesign's literal blanket prohibition on cross-side access.

Single-child relations need actual syntax, cardinality, append/replace/remove behavior, and failure cases. Saying the library hint's name does not matter leaves users unable to declare a simple maintained parent-to-single-child link. Mixed labeled/unlabeled endpoints and same-class collision examples should also be restored.

The newer draft rejects actual namespace collisions; the redesign additionally imposes at most one unlabeled relation per type pair. Record whether the latter restriction is intentionally removed when distinct hints have disjoint names, or restore it. It must not remain an accidental difference.

### 9. Interface composition and public method surface

New lines 579–581,726–732,1178–1184; redesign §§4–6 and later Wave 1/2.

The new design intentionally chooses parentheses for calls, permits explicit method type arguments, uses interface defaults plus single-parent `extends`, and omits constraint aliases. These are proposals already explained in chat, not claims that the redesign consistently chose them. Its early text favors aliases/free algorithms, while later phases use `extends`/defaults.

Missing details include extends-cycle rejection, substitution of abstract relations, field/default/destructor conflicts, visibility of injected methods, and interface-owned non-method functions. The original/redesign show non-method algorithms inheriting interface type variables; the new draft does not say whether they remain supported. Method-versus-field name collision policy also needs an explicit rule despite removal of universal property-call sugar.

A complete `Collection<P,C>` declaration and its traversal contract should replace the current promise that such a declaration will exist. Loop-only `gen C` makes that contract simpler to write now.

## Coverage omissions beyond the central design

These should be restored or explicitly excluded, not inferred from the old implementation:

- **Grammar:** identifier character set, keyword list, declaration separators, modifiers, generic method definition syntax, default arguments, variadics, and valid type/expression contexts. The new precedence table omits the newly important prefix `ref`.
- **Numeric semantics:** overflow, division/remainder, shift counts, signedness, widening/narrowing, float conversions/NaN, and equality. The old spec is incomplete here too; this is new design work, not merely copying.
- **Structs:** whether methods are allowed, whether structs can be relation endpoints, and field-default ordering. The original explicitly calls structs behaviorless and excludes relation endpoints (773–797); the new text does not dispose of those restrictions.
- **Enums/unions:** exact variant construction and matching grammar, contextual name disambiguation, union flattening/duplicate alternatives, and exhaustive matching rules for guards.
- **Errors/interfaces:** storing an `error` value requires a concrete representation and lifetime policy for a runtime-selected implementation; static multi-class specialization alone does not define it.
- **Libraries:** Dict literal syntax (original 2983–2998), string/array concatenation (365–368), complete method signatures rather than names, equality/order requirements, mutation return values, and dictionary entry validity. The new `mkdtemp()` inventory loses the optional prefix explicitly present in the original (2033,3145).
- **String safety:** owned value behavior, mutability, conversions to/from bytes, exact trimming rules and invalid encodings. Byte-oriented behavior does not itself define ownership.
- **Toolchain:** entry point, module discovery, stdlib visibility, global initialization/destruction order, test ordering, assertion cleanup/isolation, and compiler failure outputs. The old spec provides some of these, but historical longjmp cleanup behavior must not be copied uncritically.
- **Explicitly removed/deferred forms:** `permanent`, manual RC `ref`/`unref`, pipe lambdas, alternative function-type spellings, `lyric` wrappers, legacy map syntax, and old unsafe compiler flags need a concise supported/excluded inventory. In particular the new prefix `ref` must not inherit the old trusted retain operation's meaning.

## Recommended completion order

1. Ordinary value parameters and fixed reference aliases are now settled (see decisions below). Settle slices with tiny positive/negative examples.
2. Specify declaration/body association and interface binding selection; these are architecture foundations.
3. Settle result/error representation and complete construction/finalization rules.
4. Finish per-class synchronization, publication, and thread lifecycle.
5. Supply the grammar and concrete library contracts; move settled appendix content into its authoritative topic rather than maintain duplicate rules.
6. Use a small worked program to trace parsing, binding, ownership, effects, cleanup, and errors end-to-end. This is a future conformance example, not a compiler implementation already authorized or verified.

## Review conclusion

KISS has improved the mechanism count, but the draft still often specifies an obligation instead of a rule that fulfills it. A full specification must let two implementers agree on acceptance and observable behavior without consulting the old compiler or reconstructing this conversation.

The most important lesson from this fresh reading is that local rules need explicit composition rules: a reference parameter carries owner provenance into a callee; a relation binding carries both endpoint identities into a generic algorithm; and a class lock covers the allocator as well as the object payload. Making those connections precise is more valuable than adding another syntax category.

## Subsequent decision: ordinary parameters

Bill approved Cadence's recommendation: ordinary parameters are immutable local bindings with assignment's value semantics. Nested arrays are independent values; class references copy references, not objects. A mutable local copy uses `var`. Borrowing mutable caller storage uses an explicit `ref` parameter and reference argument. Copy elision, safe borrowing, and moves may avoid physical copies without changing observable semantics. This deliberately replaces the original specification's shared-mutable-array parameter exception. The canonical specification now includes these rules and a positive/negative example contract; reference-variable assignment and slices remain open.

## Subsequent decision: fixed reference aliases

Bill approved fixed named references. A read in a value context copies the referenced value; assignment writes through. `ref alias` denotes the same original storage, not reference-to-reference storage, and forwarding uses an explicit argument such as `increment(ref alias)`. Provenance, permissions, and invalidation are preserved. Reference locals use `let`; rebindable `var` reference locals are rejected. Reference parameters are fixed aliases as well. The specification now includes these rules and an example. This resolves the reference-variable/rebinding portion of finding 3, not optional/union interior-reference validity, slices, or nested-path replacement permissions.

## Subsequent decision: slices

Bill approved independent ordinary array slice values and explicit borrowed ranges. `ref array[low:high]` records a root path, offset, and fixed length, with endpoints evaluated once. It allows element access but not resizing through the window. Access re-resolves current storage and checks bounds, preserving owner-lifetime and locking obligations. The canonical main section and lexical appendix now agree. This resolves basic slice value/sharing semantics; path replacement, permission notation at helper boundaries, and whole-window assignment remain open. Examples are document contracts, not compiler-tested programs.

## Subsequent decision: array path identity

Bill approved root-and-index path identity across replacement and resizing. Replacing the root array value or an intermediate array preserves the path; sorting/insertion/removal change its current occupant, not its identity. Missing indices fail checked access, and later growth can make the path accessible again without reviving any destroyed object. Borrowed windows follow the same rule. Owner destruction and lock-release invalidation remain authoritative; counted releases during replacement retain their destruction effects. The canonical specification now states the rule with an example. This resolves the array-path replacement question, not optional/union payload references or whole-window assignment.

## Subsequent decision: whole-window assignment

Bill approved equal-length, overlap-safe whole-window assignment and clarified the plain-element implementation as memmove rather than memcpy. The specification now states destination-then-right-hand evaluation, current bounds and equal-length checks before destination writes, and normal nested-array/counting semantics. Overlap does not require a temporary allocation. This resolves whole-window assignment; the function-boundary permission contract remains open. Evaluation side effects are not rolled back, and this is not an arbitrary-finalizer transaction guarantee.

## Subsequent decision: inferred resize requirements

Bill approved inferred per-reference-parameter resize requirements with one `ref [T]` spelling. Requirements propagate through actual argument paths, helper calls, recursion, and specialized code. Whole-array mutable references can supply resize permission; fixed-length windows cannot. Independent local copies do not impose caller resize requirements. Opaque code needs explicit conservative contracts, whose syntax remains open. The canonical spec includes accepted fill/whole-array-grow calls and a rejected window-grow call. This closes the ordinary function-boundary permission decision, not optional/union payload validity or the remaining effect-analysis details.

## Subsequent decision: optional and tagged payload paths

Bill approved checked root/path references into optional, enum, and union value payloads. Access checks presence/the expected alternative; replacement resolves the current payload, and restoring a valid alternative permits access again. Owner destruction remains invalidating. Separate class-object references do not follow replacement of the slots from which they were acquired. The specification includes an optional-struct example and explicitly leaves enum/union payload-borrowing syntax to the pattern grammar. This resolves the payload-validity part of finding 3 without claiming that all pattern grammar or compiler checking is complete.

## Subsequent decision: logical packages across directories

Bill approved logical packages whose explicit file membership may span directories. A directory no longer defines a package/access boundary. Common declarations and functional implementations can therefore share one package and one type/callable identity, using ordinary qualified definitions and package-private access. Matching signatures and exactly one concrete body/external implementation are required; abstract interface slots remain governed by satisfaction rules. Separately imported packages expose public declarations only. Package privacy does not enforce internal Common/spoke dependency discipline. Configuration grammar and any architectural dependency checker remain open; the core association/access model resolves finding 1 without wrappers or public-everything workarounds.

## Subsequent decision: structural interface satisfaction

Bill approved automatic satisfaction from exact unambiguous existing members and endpoint roles, without mandatory impl registration. An applicable explicit implementation selects bindings; multiple applicable explicit implementations are errors. Defaults supply otherwise unsupplied operations only with satisfied dependencies. Empty impls are optional checked assertions; mixed impls derive remaining requirements. Capability matching does not inject storage, create relations, or prove behavioral invariants. The canonical interface section and appendix now agree. This resolves the no-impl-versus-empty-impl question in finding 7; explicit call disambiguation remains next.

## Subsequent decision: interface-qualified calls

Bill approved `Interface<Participants>.method(receiver, arguments)` for explicit selection of an instance-method binding; static operations omit the receiver. Short calls require a unique binding from receiver, type arguments, arguments, and applicable constraints. Ambiguity reports candidates rather than selecting by declaration order. Qualification creates no runtime adapter, works with structural satisfaction, and carries complete participant/role mappings into specialization. Conflicting explicit implementations of the same fully specified binding remain errors. The canonical interface section now records this decision, resolving the call-disambiguation portion of finding 7; labeled where-constraints remain next.

## Subsequent decision: labeled constraints and calls

Bill approved endpoint labels in where-constraints and interface-qualified calls. Both roles belong to binding identity. Exact roles may satisfy structurally; differently named concrete roles require explicit mapping. An omitted label selects the flat scope, not a wildcard relationship search. Labels persist through inference/specialization/effect checking without creating runtime adapters or injecting storage. The canonical generic section now contains the outgoing-count constraint and qualified-call examples. This resolves the labeled-constraint omission in finding 7; the concrete Collection contract remains next.

## Subsequent decision: Collection contract

Bill approved the ordinary Collection<P,C> capability with P.iter -> gen C, P.count -> i32, and C.parent -> P?. All operations use the same bound relationship. Traversal supports current-child deletion under existing safe-loop rules with representation-correct continuation; generic algorithms assume no particular traversal order. Count need not be constant-time, append/remove are not required, and owns/refs are both eligible. Scope for/len select iter/count without allocating a scope object. The canonical section now supplies the declaration and behavioral contract rather than promising one later. Signature matching is not proof of iterator correctness; compiler/runtime verification remains future work.

## Subsequent decision: Single relation

Bill approved Single with an unbracketed zero-or-one child endpoint. Readable child/parent links are maintained together; set requires an empty parent slot and a detached child role, remove requires the exact existing membership, and refusals leave both ends unchanged. No silent replacement/reparenting/deletion; remove detaches without destruction. Constructors establish membership before successful return. Distinct roles still permit multiple parents, and owns/refs keep the usual destruction semantics. The canonical relation section and representation inventory now include Single. Exact failure-result spelling remains dependent on the upcoming error-result decision, not silently selected here.

## Subsequent decision: Result and unambiguous tagged wrapping

Bill approved Result<T> with Ok(T)/Err(error), together with general expected-type wrapping when exactly one enum/union alternative accepts the value. Explicit tags resolve ambiguity; no implicit unwrapping or arbitrary conversion search. Result<error> requires explicit choice for error values. The canonical error section now defines single-evaluation ? extraction/propagation with cleanup; tuples/error? no longer drive ?. Fallible construction expressions yield Result<Class>, never a dummy object on failure. Constructor-body unit-success spelling remains next, and runtime error-interface representation remains open. This resolves finding 4's absent-success-value representation, not every constructor/error implementation detail.

## Subsequent decision: unit success and constructor bodies

Bill clarified that dictated 'stop that' meant 'adopt that'. The approved rule is one unit type, no separate void, and null reserved for optional absence. Unit's value is (), parsed from ordinary parentheses in expression position, distinct from a zero-argument call. Bare return/fallthrough supplies unit for unit and Result<unit> functions; other result success types require a value. Fallible constructor bodies return Result<unit>, while construction expressions yield Result<Class> after successful initialization/attachment or Err after partial cleanup. The spec now records these rules. The later proposal requiring counted runtime error objects has not yet been approved.

## Subsequent decision: concrete Status failures

Bill favored Abseil-style efficient errors over subsystem-specific error-type ceremony and approved one concrete Status model. Result<T> is Ok(T) or Err(non-OK Status); Result<unit> remains the no-value fallible form and ? remains the only propagation mechanism, preserving status unchanged. Status carries a standard code, human diagnostic message, and optional structured details; messages are not parsed for recovery. Success requires no error allocation; failure details may share counted backing with value semantics. No exact ABI/performance measurement is claimed. This supersedes the open-ended error interface/countable error-implementation proposal. Exact codes, detail API, and enforcement of a dynamically supplied OK status in Err remain open. Active examples and the library entry now use Status; historical review entries remain as decision history.

## Subsequent decision: fixed Status codes

Bill approved the closed Abseil-style code vocabulary: Ok, Cancelled, Unknown, InvalidArgument, DeadlineExceeded, NotFound, AlreadyExists, PermissionDenied, Unauthenticated, ResourceExhausted, FailedPrecondition, Aborted, OutOfRange, Unimplemented, Internal, Unavailable, DataLoss. The specification now defines their meanings and rejects project-specific additions to the enum; domain detail belongs in messages or documented structured payloads. Codes do not authorize automatic retry or prove absence of effects. Source names are specified, not numeric ABI/serialization compatibility. Structured-detail API and invalid-OK enforcement remain open.

## Subsequent decision: Err rejects OK

Bill approved checked rejection of OK statuses entering Err, noting that error creation is the slow path and can afford small overhead. Statically known OK is a compile-time error; otherwise construction checks and produces a checked programming failure, not a substituted Internal status or malformed Result. Implicit wrapping follows the same rule. Proven non-OK factories permit check elimination, and ordinary success construction needs no invalid-OK check. Ok(status) remains valid when Status is returned as data. The checked-failure runtime/cleanup contract remains separate work; no benchmark claim is made.

## Subsequent decision: Status is code plus string only

Bill chose no structured Status attachments: error factories take a string, and string formatting supplies diagnostic context. Rare functions needing machine-readable additional data return it separately through their explicitly declared result contracts. Cadence adopted this as simpler than a universal payload facility. The specification now limits Status to standard code plus diagnostic string, prohibits message parsing for recovery, and removes structured details from required open work. This supersedes the optional-structured-payload direction above rather than merely deferring an expected API. Lesson: rare richer return contracts need not burden every error with extensibility machinery.

## Subsequent decision: Single mutation results

Bill approved Result<unit> for Single.set/remove. Success supplies unit; occupied parent slots, attached child roles, and wrong-membership removals return FailedPrecondition with a diagnostic string before mutation. Constructors can propagate refusal through ?. Generated destruction must establish removal preconditions and may not ignore refusal then reclaim storage with stale links; unexpected teardown refusal is an invariant failure under the forthcoming checked-failure runtime contract. The canonical Single API is settled; many-child API edge contracts remain separate work.

## Subsequent decision: terminating checked failures with stack traces

Bill approved process-terminating checked programming failures and requested stack traces. The canonical runtime contract now reports kind/message/source location/call trace, prevents the invalid operation, and exits nonzero without application unwinding or final-hook guarantees. Diagnostic builds preserve source metadata; optimized/stripped or resource-exhausted cases report available information honestly and still terminate. Trace collection is not cleanup and does not dump locals by default. Failure in any thread terminates the process. Recoverable failures use Result. Tests continuing after assertion failure require separate processes; verification, test appendix, and panic inventory agree. No runtime/compiler implementation or trace-quality test has occurred.

## Subsequent decision: checked default, wrapping operators, unsafe opt-out

Bill confirmed checked arithmetic should be the default despite real performance concerns, particularly signed arithmetic, and explicitly requested no checks in unsafe mode. Following his Rune precedent, the spec uses !+, !-, !* and !+=, !-=, !*= rather than wrapping method calls; the multiplication/assignment family is the consistent Lyric proposal, not a claim about all Rune syntax. Safe ordinary addition/subtraction/multiplication/negation diagnose known overflow or fail checked at runtime; wrapping is modulo integer width with counterpart precedence and single-evaluation compound destinations. Tables now agree. Unsafe mode removes checks, but its ordinary-overflow semantics remain the next decision rather than implicitly adopting C undefined behavior. Division, shifts, conversions and unary wrapping spelling remain open. No performance measurements were made.

## Subsequent decision: defined unsafe arithmetic wraparound

Bill approved modulo-width wraparound for ordinary integer addition, subtraction, multiplication, and negation in unsafe mode. Signed overflow is defined; the optimizer/backend cannot assume it never occurs or inherit C signed-overflow undefined behavior. Explicit wrapping operations keep the same modular results in every mode. Unchecked indexing still falls outside the memory-safety guarantee. Division/remainder and invalid shifts remain separate decisions. The canonical arithmetic section now replaces the former open unsafe-overflow question.

## Division/remainder decision and bounded C feasibility check

Bill found truncation-toward-zero division, dividend-sign remainder, zero-divisor failure even in unsafe mode, safe MIN/-1 overflow failure, unsafe MIN/-1 wraparound, and MIN%-1 = 0 reasonable, and requested C backend feasibility verification. A standalone scratch probe at /private/tmp/lyric-division-probe.TJ08Vs/probe.c passed 271 checks each at -O0, -O3, and -O3 with UBSan under Apple Clang 21/ARM64 (Job227 exit0). Both local clang and gcc names are Apple Clang (Job226); GNU GCC was not independently run. Guards precede both / and %, because C makes MIN%-1 undefined too. Probe failures are classified outcomes, not tests of an implemented Lyric fatal runtime. No Lyric compiler was changed. The spec records the semantics and lowering/evidence limits; other widths, targets, and full compiler integration remain unverified.

## Subsequent decision: fixed-width shifts

Bill approved shifts as bit operations: left shifts discard high bits and zero-fill, unsigned right shifts zero-fill, signed right shifts sign-extend. Safe counts must lie in [0,width); known invalid counts diagnose and dynamic ones fail checked. Unsafe counts normalize modulo width, including negative counts, explicitly rather than relying on CPU masking. The spec records unsigned C lowering, small-integer promotion, sign-extension, result-bit interpretation, and zero-count hazards. Division and signed right shift deliberately differ for negatives. No shift-specific compiler probe has been run; the earlier division probe does not establish shift correctness.

## Subsequent decision: lossless implicit integer widening

After comparing Go/Rust's explicit typed-integer conversions, Bill directed Cadence to choose its preferred Cadence-friendly rule. Adopted whole-source-range containment for implicit integer widening, with context-fitting literals kept distinct. Mixed integer arithmetic uses an existing operand type that contains the other's entire range; otherwise explicit conversion is required, without inventing a third wider type. i64+u32 works; i32+u32 requires a choice. Wrapping operators use the same type selection; shift counts do not alter the left operand's width. No writable-reference widening or automatic array-storage conversion is implied. Explicit casts, float conversions, and comparisons remain open. This is a deliberate departure from Go/Rust, not a claim that they use this rule.

## Subsequent decision: integer as and !as

Bill approved Cadence's preferred postfix-style as/!as syntax with a type expression on the right. Safe integer as checks representability (known failure diagnoses, dynamic failure terminates checked); unsafe as and explicit !as use defined modulo-destination-width conversion. Negative source values use their mathematical value modulo that width. Target types may be generic parameters; expression-derived targets and typeof are not adopted. Casts evaluate the value once and never manufacture object references. The precedence table now includes !as. Rune's <type-or-expression>value forms remain attributed background, not additional Lyric syntax. Float conversions and mixed comparisons remain next.

## Subsequent decision: value-correct mixed integer comparisons

Bill approved mathematical-value comparison across integer widths and signedness for ==, !=, <, <=, >, >= in both safe and unsafe modes. No common integer result type or explicit cast is needed, including i64/u64. Negative signed values sort below unsigned values; nonnegative values compare by magnitude without lossy conversion. The spec requires backend lowering that does not inherit C mixed-conversion surprises. Operands evaluate once left-to-right. No comparison-specific probe has been run; floating-point and integer/float comparisons remain separate contracts.

## Subsequent decision: IEEE floating-point baseline

Bill approved IEEE 754 f32/binary32 and f64/binary64 behavior and asked about C compatibility. The specification now covers infinities, NaNs, subnormals, signed-zero equality, NaN comparisons, and no silent fast-math weakening in safe or unsafe mode. C lowering is feasible on supported IEEE targets with appropriate compiler/runtime settings, but portable C alone is not that guarantee. Representation/configuration verification is required; no floating-point probe has been run. Rounding-mode exposure, FMA contraction, excess precision, and numeric conversions remain explicit open details rather than an unearned bit-identical cross-target claim.

## Subsequent decision: initial C floating behavior, preferred controlled FMA

Bill chose a staged approach: initially accept the selected C compiler's ordinary floating-point contraction behavior, with compiler/version/options recorded and no last-bit cross-build equivalence promise. Preferred later implementation disables implicit contraction and maps explicit fma to C fmaf/fma; this is not an implemented or mandatory initial API. The initial allowance does not authorize broad fast-math violations of approved NaN/infinity/signed-zero/subnormal rules. Rounding-mode exposure, precise excess-precision policy, and conversions remain separate. No local FMA probe was run.

## Subsequent decision: implicit floating conversion boundary

Bill approved implicit f32-to-f64 widening and f64 evaluation for mixed f32/f64 arithmetic/comparisons. Integer/float conversions in either direction require explicit casts, as do mixed integer/float operations and f64-to-f32 narrowing. This is a category rule even where particular integer types or values fit exactly, separate from contextual literal typing. The spec preserves finite f32 values exactly on widening and NaN classification without promising payload-bit identity. Explicit float-cast rounding/range/failure rules remain next; no compiler implementation or tests were added.

## Subsequent decision: explicit float casts

Bill approved explicit integer-to-float rounding with permitted precision loss, f64-to-f32 rounding including infinity/subnormal/zero outcomes, and float-to-integer truncation toward zero with representability checks. NaN/infinity/out-of-range conversion fails checked (known invalid constants diagnose). Float-to-integer checks remain in unsafe mode; !as remains integer-only. The spec requires C lowering checks before casting and notes rounded-bound hazards, with edge cases for future conformance tests. No float-cast probe has run. Detailed floating rounding-environment policy remains separately bounded by the initial C-backend allowance.

## Subsequent decision: prefix wrapping negation

Bill approved prefix !-value as integer modular negation, retaining operand width/type and computing 0-value modulo that width in every mode. Signed minimum maps to itself; unsigned negation gives the modular additive inverse. Prefix !- has unary-minus precedence and is distinguished from infix !- by parser position, not logical-not composition. The canonical arithmetic section and precedence table now agree; the pending-negation marker is removed. No parser/compiler tests were run.

## Subsequent decision: class-wide final-hook destruction prohibition

Bill agreed to reject reentrancy and strengthened the rule: a final hook for class C calling any function that can destroy C is a compile-time error. The spec now applies this conservatively to every C instance and transitive helpers/callbacks/cascades/final hooks/counted cleanup, with opaque contracts required. The generated structural cascade after the hook is distinct, retaining same-class children under normal detachment rules. Destruction-in-progress guards remain defensive invariant checks, not a replacement for static rejection. Added positive/negative conformance cases and removed reentrancy policy from open decisions. No checker or runtime implementation/tests were performed.

## Subsequent decision: final hooks return unit

Bill approved unit-only final hooks. Result/other return types and ? propagation out of a hook are rejected. Normal return continues structural destruction and cannot veto it. Explicit fallible flush/close-style operations return Result<unit> while callers can handle failure; final hooks handle any fallible calls locally under the resource contract. Finalization does not assert successful external I/O, and checked termination retains its no-cleanup guarantee. Added canonical rules and conformance case; no implementation/tests performed.

## Subsequent decision: Result must not be implicitly discarded

Bill approved compile-time rejection of bare Result-valued expression statements and Result locals never used. Propagation, handling, return/transfer, or explicit let _ discard is required. Intentional discard uses existing syntax and ordinary cleanup; it proves neither success nor constructor attachment, and cannot bypass generated teardown invariants. Added canonical rules/examples and a conformance case. This is a diagnostic policy, not proof that every deliberately discarded outcome is harmless; no implementation/tests were performed.

## Subsequent decision: stable virtual-address SoA property arrays

Bill corrected the assumed relocation model: reserve each SoA class property array in its own virtual-memory region covering all 2^32 class indices (4 GiB for byte stride, 32 GiB for eight-byte stride); acquire physical backing as needed and never move the property base. Bill reports testing this strategy successfully at intended program/shared-library scale. Recorded as attributed experience, not a new runtime test. Both AoS/SoA use mutex-protected slab free lists; allocator locking is separate from payload/lifetime synchronization. Replaced spec's moving-SoA-base discussion and added stability conformance case. Ordinary dynamic arrays stored in objects still have independent relocating backing and fat-path rules. Exact OS mapping calls/flags, reserve failures, publication and shared-library unload remain implementation contracts. No mmap probe, runtime changes, or blanket payload-lock elimination performed. Lesson: class population growth need not cause SoA relocation, so that cannot justify ordinary access overhead under this design.

## Subsequent decision: PEG parser and explicit grammar

Bill specified a PEG parser, like Lyric, using a similar grammar file. Recorded this compiler direction and grammar/parser conformance obligations without selecting a generator or copying a grammar. Read-only inspection found /Users/bill/projects/lyric/src/parser/parser.ly and expr_parser.ly; filename search excluding .git/.worktrees found no *.peg/*.pegjs/*.pest/*grammar* files. This does not establish absence in another checkout/version or under a different filename. Exact reusable baseline location remains unconfirmed. No parser generation, implementation, or tests performed.

## Correction: PEG precedent is Rune, not the implemented Lyric parser

Bill recalled that he required a PEG parser like Rune for original Lyric, but Opus implemented recursive descent instead. This is Bill's attributed account, not an independent source audit of that implementation. Corrected the canonical parser section to require the intended Rune-style PEG grammar approach, explicitly disallow substituting handwritten recursive descent, and stop presuming a reusable PEG grammar exists in the old Lyric checkout. Rune grammar location/tool selection remain unconfirmed. The prior filename-search result is consistent with the correction but does not prove the full historical account. Lesson: preserve the intended requirement separately from what a prior implementation happened to deliver.

## Superseding decision: normative syntax, recursive-descent implementation

Bill reconsidered parser maintenance given LLM assistance and emphasized that syntax still needs specification; he requested the document update. The canonical compiler section now uses a normative EBNF grammar with preferred handwritten recursive descent/precedence parsing and accepted/rejected grammar-linked tests. PEG-only requirements and the explicit handwritten-parser prohibition are removed. Earlier PEG discussion is preserved as history, not active authority. Grammar/prose/parser/tests must evolve together; semantic rules remain in prose/checking. No grammar or parser implementation was performed. Lesson: lower parser-maintenance cost does not remove the need for a precise syntax contract.

## Milestone: first consolidated EBNF review draft

On Bill's request, Cadence wrote lyric-grammar.ebnf from the preserved specification and approved new syntax, with grammar-review.md explaining lexical conventions, six grouped syntax proposals, source coverage, deferred features, and 35 example expectations. Started with the spec rather than extracting implementation; initial parser source inspection was bounded and not treated as language authority. Draft covers declarations/types/relations/impls/statements/expression precedence/patterns/literals. Header expression rules are explicit productions; generic-angle lookahead, cast target boundaries, keywords, separators, and other proposed resolutions are clearly not yet approved.

Job328 completed with exit0: 166 unique productions, valid EBNF metasyntax, no undefined references, no nullable repetitions, all productions reachable from File or lexer-trivia roots, 35 unique example IDs, all three background documents byte-equal originals. grammar-check.txt records hashes and limits. This was a document metasyntax checker, not a Lyric parser; examples were not executed, ambiguity freedom not proved. Linked the grammar/review/receipt from the main spec and README. Original sources unchanged. Next human review recommendation: D3 generic angle syntax versus comparison ambiguity, then D4 cast targets and D5 separators. Remaining unsupported forms are inventoried rather than silently omitted.

## Superseding decision and milestone: ordered PEG draft 2

Bill chose PEG for now after discussing manual reviewability, recursive-descent roadmaps, and LALR(1) verification. PEG and generic-first ordered choice now govern the active specification; execution/generator/memoization choice remains open. Converted preserved EBNF to lyric-grammar.peg (166 rules), explicitly ordering function types before names, record/closure shapes before shorter prefixes, floats before integers, and typed where requirements before bare capabilities. GenericSuffix uses BindingArgs followed by positive lookahead for call/member continuation. Semantic failure does not trigger alternate syntactic interpretation. Lexer hooks are documented; this is not a generator-specific ready-to-run claim.

Job334 exit0 verified references, nonnullable repetitions, and absence of nullable-prefix left recursion; peg-check.txt contains current PEG hash. Original lyric-grammar.ebnf/grammar-check.txt unchanged. Job335 preserved original review as drafts/05-ebnf-grammar-review.md with its original hash aac5af6b6d87987cd6800536f8b496c4c5344fef83c19b09f4836d989bc1b194. Job339 updated active grammar-review.md with PEG notation/status and decided generic priority. Main spec and README link active PEG/review/receipt and historical EBNF artifacts. PEG read back completely (196 lines). No PEG engine, Lyric parser, conformance examples, or performance tests executed. Remaining grammar choices D1/D2/D4/D5/D6 and deferred coverage still need review. Next recommended syntax decision is cast-target boundaries (D4), not reopening generic-first priority.


## Decision update: shared/exclusive lifetime and allocation access

This update supersedes earlier claims that the class lock exclusively protects all payload access, and the separate allocator-mutex proposal. Bill approved one reader–writer lock per class: shared access permits references to existing instances, while creation and destruction require exclusive access. Exclusive access protects free lists, slot reuse, allocator metadata and backing-storage updates; generated helpers require held access and need no additional per-class allocator mutex. This intentionally blocks creation while other threads use the class. Payload/relationship/owned-array synchronization is separate: shared lifetime access does not serialize mutation or counted updates.

The current specification is Draft 3.3, Section 19. Access-region syntax, full-set/global-order acquisition, ordering with data guards, reference-count synchronization, writer progress, publication and blocking contracts remain unfinished. No compiler, runtime, lock benchmark or concurrency test was implemented. The lesson is to give allocator state one synchronization owner while keeping lifetime safety distinct from data-race protection.
