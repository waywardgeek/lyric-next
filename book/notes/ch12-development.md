# Chapter 12 — Multi-Class Generics: construction plan

## Status and authority

Bill accepted Chapter 11 and its walkthrough; baseline commit is `5947bed06e8327b0e3c0c079c14508847be0f84f`. The initial grammar-and-plan preparation is complete. Bill now explicitly authorizes personal interactive Chapter 12 implementation and is present for the design explanation. No review/student campaign, commit, push, installation, or autonomous campaign is authorized.

## Current safety boundary — Bill's clarification

Chapter 12 is intentionally not a complete memory-safe language milestone. Ordinary code may write regular-object reference fields so that multi-class implementations can maintain relationships. The later relation chapter will restrict those writes to designated relationship operations and generate teardown that invokes removal, clearing both ends before reclamation. An independent saved local reference still requires ordinary lifetime checking; detaching a relationship does not rewrite that local.

Preserve existing definite-initialization, nullable access, counted ownership, and earlier borrow checks. Do not require a new persistent-attachment analysis or full heap-identity/call-summary redesign as a prerequisite for this chapter. Examples explicitly remove memberships before destroying endpoints. No automatic nulling of arbitrary fields, no safety exemption for counted destruction, and no whole-language memory-safety claim are implied.

The recent experimental requirement that an attached field must itself prohibit destruction even without a later use was an author scope mistake. Its pending fixtures and heap-analysis preparation must be audited and separated from the chapter requirements, not used to expand this increment. Existing verified features must be preserved during that cleanup. Revisit actual safety integration with relationship teardown, not by completing the premature heap-analysis detour.

Lesson: the final design combines controlled relationship mutation, generated two-way detachment, and ordinary borrow checks. Losing that connection made an intentionally incomplete intermediate chapter look like a requirement for a different safety mechanism.

## Target and agreed vocabulary

Build a reusable multi-class generic, using an intrusive doubly linked list between Graph and Node as the concrete design target. `interface` is the retained spelling; this feature has no dynamic dispatch, runtime interface object, or adapter allocation. Participant class blocks reuse ordinary fields and methods:

```lyric
interface DoublyLinkedList<P, C> {
    class P {
        first: C?
        last: C?

        pub func append(self, child: C) {
            // Validate detached membership before changing either endpoint.
            // Connect the child after last and maintain both directions.
        }

        pub func remove(self, child: C) {
            // Validate membership before mutation, reconnect neighbors,
            // and clear the detached child's membership links.
        }
    }

    class C {
        parent: P?
        previous: C?
        next: C?
    }
}
```

These comment-only bodies explain the design, not a working implementation. Within an interface, `class P` contributes members to participant P; it does not create a new nested nominal class. P must be a declared participant. Methods inside its class block are unqualified (`append`, not `P.append`). Cross-participant mutation through an explicit child argument is essential, not forbidden.

## Explicit instantiation

```lyric
class Graph {}
class Node {}
impl DoublyLinkedList<Graph:nodes, Node:graph> {}
```

The empty impl requests the supplied storage and method implementations. It is not merely a conformance assertion. P's contributions enter Graph.nodes; C's enter Node.graph. These scopes allocate no wrapper objects. Fields of type P/C refer to the real Graph/Node classes. Generic field and method references retain resolved declaration identities and are mapped through the complete binding, never recovered from emitted names.

Labels are independent and optional:

```lyric
impl DoublyLinkedList<Graph:nodes, Node> {}
```

Here access is `graph.nodes.append(node)` and `node.parent`. Omitting both labels yields `graph.append(node)` and `node.parent`. An omitted label means the ordinary class member scope, not a wildcard search. Conflicting contributions must be diagnosed before changing class membership; do not silently reuse or overwrite an existing field or method.

Different roles are different specializations even for the same concrete classes. Graph:nodes/Node:graph and Graph:selected/Node:selection retain separate storage and operations. The complete key includes generic declaration, ordered concrete participants, endpoint scopes/roles, and any selected implementation identities. Canonical types alone are insufficient.

## Explicitly deferred

- Relation statements and their owns/refs policy.
- Relationship-specific compiler-managed ownership accounting and teardown. Explicit source-level ref/unref count manipulation is rejected, not a future feature.
- Dynamic dispatch, generic classes, unrelated ecosystem features.
- Mapping onto existing fields, override bodies, and automatic conformance unless a demonstrated requirement changes this scope with Bill.

The existing PEG describes broader future syntax; recognition is not Chapter 12 semantic admission. Capability use must never silently inject storage. Only explicit instantiation requests these contributions.

## Settled follow-up decisions

Nonnullable object-handle storage starts internally at the cleanup-safe empty sentinel (`UINT64_MAX` in the current backend), not zero: zero may identify a live object. `var foo: Foo` is legal, but reading before assignment of a nonnull value is an error; assigning null is forbidden. Internal empty storage does not satisfy definite assignment or constructor initialization obligations. Nullable links use that sentinel for the language null value and require explicit checked `!` before dereference. This corrects the earlier provisional zero wording; it does not change the handle representation.

Support ordinary and value-class participants without disabling existing destruction or reference counting. Chapter 12 defers relation-aware teardown; examples avoid destruction while attached and remove memberships explicitly. Ordinary contributed fields retain existing field semantics for this increment, including counted fields. Do not claim that this already implements the later relationship ownership policy or arbitrary endpoint deletion safety.

Removing from the wrong parent, or appending an already-attached child, violates an invariant: panic before mutation. Callers must establish membership. No recoverable Result subsystem is required for this increment.

## Relationship chapter contract (deferred implementation)

Relations should elaborate into underlying multi-class generic/impl machinery, not a separate backend mechanism. Declare parent/child roles, owns versus refs policy, a marked removal operation, and the safe iterator used by generated teardown. Exact annotation spelling remains deferred.

Destruction runs the completed object's finalizer first, detaches it from all parent relationships second, then removes ref children and destroys owned children in any order, finally releasing remaining ordinary fields and reclaiming storage. An owned child's destruction must start before detaching that child's membership, so its finalizer sees its memberships. Generated traversal must respect current maintained memberships and avoid duplicate destruction through multiple owning relationships.

If teardown traverses while destroying children, an explicitly safe iterator must be visible through the binding; otherwise compilation fails. Ordinary iterators do not acquire deletion permission from implementation details. Safe iteration permits proven current-item-only destruction, not peer destruction or transitive cascades deleting peers. Continuation and cleanup must remain valid; merely saving next is not proof. Generated destructors obey the same checks and are constructed in IR, not invented by C emission.

No source-level ref/unref count-manipulation operations are needed. The compiler owns accounting: a counted child gaining an owning parent membership gains one count; removal releases that count. Parent backlinks and previous/next structural links do not retain their targets. A refs membership contributes no owning count. Complete two-ended unlinking precedes release, since release can immediately finalize the child. Refused attachment/removal changes neither membership nor counts.

Suppress ordinary per-assignment counting only for declared relationship-managed structural links, never globally throughout generated methods. Locals, returns, and unrelated ordinary value-class holders still use normal reference counting. Ordinary code cannot manually rewrite relationship-managed links; it uses declared operations. This is not a blanket ban on existing local handles, object arrays, or ordinary counted fields. Internal IR retain/release operations remain necessary.

PEG inspection found no explicit count-manipulation statement productions to remove. Its ref type and ref acquisition-expression productions remain unchanged. The lesson is to distinguish source reference acquisition, internal retain/release, and logical membership accounting rather than treating their shared vocabulary as one operation.

## Construction sequence

1. Read actual interface/impl parsing and current member-scope/type/specialization ownership. Align the handwritten parser with the authoritative participant-block grammar and retain AST shape/negative tests. Preserve older parsed forms without admitting deferred semantics.
2. Establish real semantic declarations for participants, contributions, binding roles, and generated members. Explain Common vocabulary and all owning/backlink relationships before implementation. Preflight collisions and complete bindings before mutation; make retirement invalidate dependent IR and detach every relationship.
3. Deliver a small executable vertical slice early: two participant blocks, concrete fields and methods, an empty labeled impl, and calls using both endpoints. Verify flat and mixed scopes too. Do not build a large disconnected foundation first.
4. Add complete role-sensitive specialization and copied checked bodies using Chapter 11's machinery. Feed all generated methods through ordinary signature/body checks, borrow/effect analysis, IR, and C emission; no second checker, backend name resolution, or source-emission fallback.
5. Extend the executable slice to the agreed list behavior once nullable/initialization/lifetime decisions are settled. Exercise empty, singleton, multiple nodes, first/middle/last removal, detached state, refusal without mutation, independent bindings, and cross-endpoint updates. Keep syntax, semantic safety, execution, and ownership evidence distinct.
6. Run affected retained checks with source/assembly inputs frozen. Then the full cumulative suites at a coherent milestone. No redundant broad reruns after prose-only changes.
7. After demonstrated implementation, write construction teaching, source-checked diagrams, exercises, and spin12. Review/reconstruction is the normal eventual chapter process, but launching those agents requires authorization; the implementation authorization does not launch them.

## Grader plan

Public independent Python behavioral graders remain outside the compiler. Positive cases require exact stdout, empty stderr, specified exit, and real emitted-C execution at both optimization levels using the established harness. Negative cases require the intended diagnostic, not crashes or arbitrary nonzero exits. Do not execute known-invalid dangling-handle cases. Retain earlier chapters' behavior.

Model probes should cover full binding identity, repeated concrete types with different roles, preflight collision/foreign-parent refusals, original-source preservation, generated member reciprocity, and retirement through every owning parent. These supplement public behavior rather than replace it. Every model relationship must enter the book inventory exactly once, with source/lifecycle validation and rendered inspection.

## Tonight's syntax increment

`ParticipantClass <- "class" Ident ClassBody` is added to InterfaceMember. Existing class member grammar and existing BindingArgs/ImplBody are reused. Fifteen public PEG fixtures in `graders/ch12/multi-class-syntax.json` cover participant blocks, all four endpoint-label combinations, independent bindings, ordinary field/method syntax, and malformed declarations/bindings. Participant-name validity and member collisions are semantic obligations, not false PEG rejections.

The PEG harness uses the existing compiler lexer and independently executes the grammar. It neither updates the handwritten parser nor proves semantic support. Final syntax results are recorded below after terminal collection.

### Syntax verification receipt

Job2187 completed with exit0: 181 canonical rules compiled; new syntax15/15, base50/50, retained Chapter3 155/155, typeof5/5, and matchtype11/11 passed, including the harness malformed-grammar controls. Used installed Peggy and existing Release lexer, with no build/download or compiler edit. This is PEG recognition evidence only. No handwritten parser, semantic implementation, chapter walkthrough, review, or student run is claimed.

## Handwritten parser increment

Job2206 terminal exit0: clean Debug build, Chapter12 recognition16/16, retained Chapter3 recognition155/155. Participant class blocks reuse Aggregate member parsing but reject nested type-parameter lists, matching the PEG. Existing Class syntax is contextual under Interface; no new nominal type is created by parsing. The public recognition harness now accepts an optional fixture path without changing its default Chapter3 contract. One additional retained negative checks participant nested generics. PEG execution of that new negative remains pending. No semantic instantiation or list execution is claimed.

Source inspection found that existing class field admission rejects object-handle fields. The list therefore needs an explicit object-field storage extension and nullable support; it cannot be delivered merely by connecting generic methods. Start with two-participant scalar-field/method execution while preserving that integration requirement.

## Executable binding baseline and namespace decision

Job2217 terminal exit0: all181 PEG rules compile, malformed-grammar controls pass, Chapter12 syntax16/16 including the added participant nested-type-parameter rejection. This closes the pending PEG negative only, not semantic support.

Added public graders/ch12/multi_class.py using unchanged Chapter10 C execution harness. Four retained method-only positives cover both/parent/child/no labels and cross-participant calls; two retained negatives require unknown-participant LYC301 and member-collision LYC302. Job2222 terminal exit1:0/4 executions and0/2 intended rejections; all fail with existing unsupported Interface LYC306. This is the expected missing-feature baseline, not a passing negative or C execution claim. Fixture source and harness comparison logic read back.

Source inspection: GenericSpecialization owns one callable and cannot directly represent a multi-class instantiation. Lexical scope parents are weak, so instantiation retirement must explicitly own contributed scopes/copied syntax. IR Lowering still inventories class methods and destruction fields from parsed declaration children; contributed members require authoritative semantic inventory traversal, not parsed-tree mutation. Role identity must survive even when multiple participants bind the same canonical concrete type; canonical substitution alone cannot select their distinct contributions.

Bill agreed: the label itself occupies an ordinary member name. Each labeled scope belongs to one instantiation; reuse of the same class-and-label by another instantiation is a scope-name collision, even if contents differ. Component.layer and Wire.layer do not collide because their containing classes differ. Flat contributions may coexist if ordinary member names do not collide. Scopes are not implicitly merged.

## Contribution namespace model increment

Added SymbolKind.MemberNamespace: compile-time placement only, directly class-owned, no runtime value/reference/builtin contract. Existing symbol owns its member scope; analysis co-owns scope. Class navigation follows namespace.Scope.MemberOwner instead of copying a class link. Fields() includes namespace fields in physical inventory; source-based IR inventory/destructor traversal still needs integration. No interface/impl semantic admission yet.

Job2227 clean Debug compiler build. Job2232 terminal exit0 on frozen inputs: new source-linked namespace probe31 checks EACH Debug/Release; existing generic specialization probe1108 EACH Debug/Release; four clean probe builds and whitespace clean. Namespace checks cover reciprocal owners, qualified-only lookup, physical field enumeration, duplicate label/ordinary member collision, invalid global/nested namespace refusal preserving existing state, cascade destruction through class or label, and name reuse after retirement. This is model evidence, not public execution success. Existing offline probe runner accepts optional probe source/startup type; original invocation unchanged.

Next integration: explicit multi-class instantiation/participant owners for copied syntax, scopes and contributed symbols, complete role-sensitive binding key and atomic collision preflight. Function-specific GenericSpecialization cannot stand in for a multi-callable owner. Signature ParameterType currently uses direct MemberOwner for self; namespace methods must navigate physical class. Participant bindings need private checking environment, with role identity retained even for equal concrete types. Continue toward four public method execution cases; no broad regression rerun before meaningful integration.

## Role-sensitive contracts and pending view rule

Extended graders/ch12/multi_class.py to six execution contracts and four rejection contracts: equal concrete class in both labeled roles, two independent labeled bindings, repeated scope-label collision, and label-versus-ordinary-member collision. Job2245 exit0 verified fixture import/counts/unique names and whitespace. These new behavioral cases have NOT run against the compiler; prior 0/4+0/2 baseline remains historical evidence only.

Inspected Common/SemanticModel.cs facts and Check.cs class method selection: call facts already retain final concrete method symbols, but selection currently uses receiver canonical class alone. Existing function GenericBinding stores only canonical types and cannot represent role plus placement. The instantiation needs participant identity without new runtime wrapper or nominal class identity.

Bill agreed to the propagation rule: inferred locals retain initializer participant view; explicitly annotated locals, parameters, and results use their declared view; explicit concrete class annotations use ordinary class lookup. The explicit type wins for lookup but does not waive compatibility: semantic checking rejects a conflicting concrete specialization. P/C bound to the same class remain ordinary compatible handles; the declaration view chooses contributions, not runtime provenance. A body operation can impose an additional requirement checked at specialization, not assume or manufacture conformance. Header constraints may document such obligations more clearly; adding constraint syntax is not part of this decision. This rule is agreed but not yet implemented. No compiler edits or running jobs in this inspection slice.

Added explicit/inferred local-view contracts after Bill's agreement: same-class P/C binding must select C through inference and P through explicit annotation; incompatible Graph/Node assignment must reject with LYC304. Public suite now seven execution/five rejection contracts. Job2254 terminal exit0: all12 fixture sources parsed via current Debug --inspect-syntax with valid file trees, empty stderr and exit0; whitespace clean. No semantic/execution success claimed. CheckBinding3890–3955 already prioritizes declared canonical type and checks RequireType before registration; extend view propagation alongside this, not instead of compatibility. Call facts already retain resolved concrete target; do not move role resolution into IR/backend.

## Multi-class owner and endpoint model

Added Common/Semantics MultiClassInstantiation partial types and SymbolKind.Interface. Analysis and template strongly own instances; ordered ParticipantBinding occurrences retain reciprocal concrete-class dependencies and optional label values. Complete-key lookup includes template, ordered classes and labels. Constructor checks arity, live/same-analysis classes, existing member/endpoint label collisions and repeated class-label pairs before registration. Flat contribution-member admission remains future integration. Analysis/template/class teardown retires instances; retiring one endpoint retires the instance, not peer classes. No generated members or interface semantic admission yet.

Job2264 built Debug cleanly but probe fixture failed to parse: adjacent participant class declarations on one line lacked separators. Corrected fixture to separate lines; no parser/model relaxation. Job2266 terminal exit0:131 actual model checks EACH Debug and Release, both clean builds, whitespace clean. Covers same-class distinct occurrences, independent full keys, duplicate/late/internal collision refusal, labels reusable after retirement, and teardown through instance/endpoint/class/template/analysis/source. No public semantic or C execution pass claimed.

Next: derived syntax/scopes/contributed-symbol ownership, then checker admission and concrete method pipeline. Inspected function GenericSpecialization.cs1–250, symbol teardown and analysis teardown; syntax constructor Common/SyntaxNode.cs1–180 and search of specialization guards. Syntax/SyntaxNode.cs still needs full bounded read before edits. Shared specialized-syntax branches include child attachment/removal, destruction and source registration; add multi-class owner consistently, not constructor-only. No jobs/freeze remain.

### Derived contribution ownership — 2026-11-26

- Multi-class instantiation now co-owns derived syntax with its original source, and contributed symbols with their member scopes. Checked function/member/nested scopes retain explicit instantiation ownership; lexical parent edges remain weak.
- Syntax assembly ends with SealSyntax before checking; later tree edits refuse without mutation. Original parsed nodes cannot be adopted, derived trees cannot attach to original or other-instance trees, and destroying any owned node/contribution/checking scope retires the instantiation. Retirement marks first, invalidates IR, drains contributions/scopes/nodes, then endpoints and template/analysis memberships.
- Job2276 failed compilation in the diagnostic only: it accessed private Compilation.Analysis. Corrected probe to inspect analysis.Compilation/live state, without widening production visibility. Job2277 terminal0:210 multi-class model checks and1108 existing generic checks EACH Debug/Release, clean builds. Subsequent contribution/scope integration job2281 terminal0:278 multi-class checks EACH configuration; job2284 terminal0:1108 existing generic checks EACH configuration; clean builds and git diff --check.
- These diagnostics do not establish impl execution. Public contracts remain7 execution/5 rejection cases; all12 sources previously parsed but semantic admission still unsupported. Next connect actual interface/impl registration after ordinary class member collection and before separate bodies/signatures in Check.cs. Interface node contains TypeParameters and Class participant blocks; empty Impl has QualifiedName and TypeArguments(true). Preserve canonical types while carrying participant view for lookup; implicit self must use physical class through namespace.

### First concrete multi-class execution — declaration admission and IR

Interface declarations now register real Interface symbols and validate header names, participant blocks and contributed member collisions independently of instantiation. Empty explicit impls resolve actual classes/labels, preflight all placement names before mutation, construct the owned full syntax copy, register concrete methods and private participant type symbols, then seal syntax. Fields/adapters/relationship policy remain explicitly unsupported in this method-only slice. Implicit self navigates labeled scopes to the physical class. Role-view propagation is NOT implemented by these canonical type bindings.

IR discovery now enumerates semantic class methods (including namespace scopes) instead of only parsed class children, skips compile-time Interface/Impl declarations, and enumerates semantic fields for destruction. First run exposed a real lowering crash: a public method's trailing Visibility node was mistaken for its body. Lowering now finds Block by kind and separately selects field-initializer expression child1, matching checking rather than assuming last-child order.

Evidence:2296 first build failed CS0136 local-name conflict; renamed interface-local variable.2297 clean Debug build, public0/7 executions1/5 rejections (unknown participant now correct).2302 clean build after concrete admission;2303 public0/7+4/5 (flat reached IR unsupported).2306 clean build but flat lowering crashed; terminal exit1, no accepted rejection. Corrected2308 clean Debug build; terminal exit1 with1/7 executions (flat cross-participant calls, O0/O2) and4/5 intended rejections. Labels/role views remain unsupported; no full feature pass.2310 terminal0 focused unchanged Debug regressions: struct-values34/34 executions39/39 rejections, generics17/17 executions10/10 rejections. No Release or full cumulative verification of this slice yet.

Next: namespace-expression facts retain actual MemberNamespace symbol plus physical receiver type; method selection follows that scope. Add explicit reciprocal participant-view relationships for declared/inferred types, locals and results; same-class roles cannot be represented by canonical types alone. Read Check.cs5716–5880: Member calls CheckField; class-call branch uses hint then CheckExpression and direct class scope lookup. Lowering must treat namespace selection as receiver forwarding, not field storage/extra ownership. No live jobs or freezes remain. No workers/commit/push/install authority.

### Labeled receiver execution (namespace, not participant-view completion)

Namespace selections now retain the MemberNamespace symbol and physical receiver
type in their fact. Method lookup selects that symbol's member scope. IR forwards
the original receiver without creating a wrapper. Lifetime checking forwards its
saved object identities; computed-receiver classification unwraps namespace paths.
The first build failed CS8602 in the moved class-method guard; restored its existing
class-branch nonnull assertion. Corrected Debug build: zero warnings/errors.
Public job2323 ended exit1: 2/8 executions, 5/5 intended rejections. Added counted
receiver coverage; job2325 ended exit1: 3/9 executions, 5/5 rejections. Direct
labeled methods (including temporary receiver), flat cross-role methods, and
counted named/temporary labeled receivers execute at O0/O2. Exact counted output
NliveGdone confirms those holders' finalization ordering in this fixture.
Six cross-role/view positives still fail LYC329: participant types currently lack
the compile-time view relationship. No full Chapter12 pass, Release verification,
or broad regression claim for this edit. No running jobs.
Lesson: a compile-time label preserves lifetime identity as well as runtime handle
identity; it must not introduce a second owner or revive invalidated state.


### Role-aware method slice verified

Added explicit reciprocal ParticipantView links on SemanticSymbol/SemanticFact and
separate ContributingParticipant links for contributed members. Endpoint reverse
collections support refusal without mutation and complete detachment. A P method
can return C: contribution identity and result view are deliberately independent.
Views require exact nominal canonical type and same analysis. SetType preflights
the replacement before detaching an obsolete view; unchanged types retain views.
Endpoint retirement drains remaining weak users; symbol/fact destruction detaches
its own view. New implementation: Semantics/ParticipantView.cs; declarations remain
in Common/MultiClassInstantiation.cs.

Private P/C type symbols carry endpoint views. Parameters (including implicit
self), declared results and explicitly annotated locals select their declared view;
inferred scalar locals retain their initializer view. Canonical compatibility is
still checked first. Calls resolve explicit namespaces first, then participant
contributions, then ordinary class members. Checked call facts retain actual method
symbols; runtime types/handles and ownership are unchanged. Grouping preserves
namespace and participant identity. Declared result views survive chained calls.

Evidence:2338 terminal0 model359 checks in each Debug/Release.2342 terminal0 clean
Debug build and9/9 execution5/5 rejection contracts. Added grouped/result/concrete
view fixture:2344 terminal1,9/10 executions5/5 rejects; chained result lookup failed
LYC306 because nondiagnostic hints discarded lookup placement. Corrected hints use
only temporary view/namespace maps, with no facts, references or binding admission.
2347 terminal0 clean Debug build,10/10 executions5/5 intended rejections (O0/O2).
2349 terminal0 frozen focused regression batch: model359 and generic-model1108 each
Debug/Release, zero build warnings/errors; Debug generics17/10, typeof17/8,
matchtype18/13 plus1 IR and3 corruption controls, struct-values34/39, all pass.
2351 terminal0 clean Release compiler build and10/10 executions5/5 rejections.
No full cumulative suite, chapter acceptance, or storage/list support claimed.

Lesson: non-evaluating previews must preserve lookup distinctions needed to predict
result types, while real checking remains authoritative. Equal runtime class types
do not collapse participant identities. Composite-contained view propagation and
conditional joins are not certified by this scalar method slice. Next: contributed
storage integration through semantic field inventory, constructor initialization,
member selection, backend layout and cleanup; inspect nullable prerequisites before
expanding to DoublyLinkedList. No live jobs/children/freeze after2351. No commit/push.

## Contributed scalar/array storage — verified increment

- Admission now creates real instantiation-owned Field symbols in flat or labeled placement scopes, with endpoint contributor backlinks. It deliberately retains ordinary class restrictions: no object-typed fields/defaults in this increment.
- Emitter storage and constructor completion enumerate semantic Fields(), not parsed class children. IR discovery/destruction already uses that inventory. Namespace lookup and hints resolve actual contributed fields, including same-class roles.
- ConstructorReceiver validates only self/group/namespace paths before attaching facts. Direct field initialization can use partial self; standalone namespace values still require complete initialization, so passing self.label cannot evade LYC328. Both reads and completion use actual field identities.
- 2357 clean Debug build. Initial2358 new execution fixtures failed because print expects bytes, not i32; corrected fixture uses assertion and literal finalizer marker, no compiler change for that correction.2359 passed12/12 executions7/7 rejections.
- Expanded2361 clean Debug build and13/13 executions8/8 rejections, including two roles on one class and partial-namespace escape refusal.2363 terminal0: retained Debug Chapter8 checking154/154; layout212/212; fresh clean Release and Chapter12 13/13+8/8.2368 terminal0: same Chapter12 cases explicitly SoA,13/13+8/8. Harness executes positives at O0/O2. No full cumulative suite or exhaustive leak claim.
- Lesson: semantic member inventories must drive storage/initialization once contributions are possible; a namespace is transparent to identity, not an escape from constructor obligations.
- Next: nullable class-handle prerequisite and object fields, then executable list. Parser accepts OptionalType/null/postfix unwrap, but canonical kinds are Scalar/Array/Nominal/Tuple and checker has no nullable semantics yet. Spec377 permits null only in nullable positions, implicit wrapping and explicit checked unwrap. Spec2230 distinguishes future zero-null indexing from current bootstrap sentinel; do not silently change representation. Relationship-managed counting/teardown remains deferred. No live jobs at this milestone; no worker/commit/push/install.

## Nullable canonical identity — model-only prerequisite

Added SemanticTypeKind.Nullable and analysis.NullableType for live nonnullable class handles only. Canonical key is kind plus actual element identity; nullable wrappers have their own reverse dependency collection, not an array membership or second nominal declaration. ArrayDepth now traverses only Array kinds; Class still reaches the underlying nominal handle. Formatting preserves `Owner?` and `[Owner?]`. Nullable handles have an observable null default; nonnullable handles remain nondefaultable. Nullable counted handles retain ownership classification. Wrapper retirement leaves the base class alive; base retirement cascades through wrappers and their consumers. Constructor and factory both refuse scalar/struct/array/already-nullable or foreign/retired elements.

New implementation compiler/Semantics/NullableTypes.cs; Common declarations and canonical retirement updated. Source nullable admission, IR, serialization and runtime operations are NOT implemented by this milestone. Existing compiler binaries were not rebuilt; diagnostic compiles actual current source into temporary assemblies.

Probe2384 failed compilation because the new test incorrectly named SymbolKind.Variable; actual enum is Local. Corrected probe only. Job2387 terminal exit0: canonical model359 checks each Debug/Release, clean builds. Includes nullable interning/duplicate rollback/type queries/retirement/counting-classification alongside previous canonical controls. Source syntax remains unsupported. Job2388 git diff --check clean; combined report clipped later fixture/note output, not a full read of those portions. No live jobs or source freeze.

Lesson: nullable canonical identity and physical handle representation are separate. Current allocation reserves UINT64_MAX; release accepts empty, retain rejects it. Reuse that representation but implement nullable retain/copy explicitly; do not make all ordinary retain calls silently accept invalid empty values. Existing Class/ArrayDepth-based checking may admit nullable dereferences unless audited before enabling syntax. Public null/wrap/checked-unwrap execution and intended-failure contracts are next, then semantic/IR/emitter integration. Participant-type field annotations and object-field effects are still outstanding before the list; no relation-managed ownership/teardown is admitted.

### Nullable public contracts and IR integration boundary

- Added `graders/ch12/nullable_handles.py`: four positive cases (wrap/unwrap, parameter/result, empty counted copy, live counted copy/finalization), two intended LYC304 rejections, and one checked null-unwrap runtime rejection. Reserved `LYR013: null handle unwrap`; repository search found no prior use in compiler/graders/spec. Seven sources passed syntax inspection against current Debug assembly (job2401); source nullable semantics still not admitted.
- Extended shared tuple harness with optional exact `runtime_diagnostic` bytes, default unchanged LYR011. Escapes diagnostic literally and retains exit1, empty stdout, located diagnostic and sole main frame. Python AST checks and whitespace passed. Initial exact edit anchor refused without mutation, corrected after reread.
- Job2402 terminal: nullable baseline exit1, 0/5 executions and 0/2 intended rejections (unsupported OptionalType or Literal, not intended failures); existing array-default runner exit0, 12/12 safe and 12/12 unsafe at both optimization levels. This uses existing Debug assembly, not a rebuilt nullable compiler. Outer exit0 reflects retained runner, not nullable success.
- IR inspection: numeric Widen currently creates unregistered Convert result (Lowering.cs361–375); ownership registration Add at304–308 only designated holders. Recommend explicit null representation and dedicated wrapping/checked-unwrapping operations, not integer null or numeric conversion masquerade. Counted conversions must establish registered output ownership and preserve explicit Move contract. Validation.cs428–505 checks BOTH transfer consumers and required transfers. Need implement/check both directions and emitter retain/null behavior before source admission. No IR/checker/emitter change yet.
- No live jobs or input freeze. Next: inspect exact ownership validation window, implement bounded nullable IR/checker/emitter integration with current public contracts; guard every nullable dereference/destroy path, preserve sentinel and existing nonnullable defaultability. Linked list/object fields and relation teardown remain separate unfinished boundaries.

### Nullable IR ownership contract

- Added explicit `NullHandle`, `WrapNullable`, and `UnwrapNullable` operations. Null is a zero-operand nullable value, not an integer literal. Wrap/unwrap preserve exact canonical payload identity; counted conversions consume Move inputs and create cleanup holders. Numeric Convert remains unchanged. Both transfer-consumer and required-transfer directions are checked.
- An initial validator used nonexistent `ElementType`; build rejected it. Corrected to the actual declared dependency `Element`, then Debug built cleanly (job2418). No source nullable admission or emission yet.
- Extended the existing IR model probe with ordinary/counted graphs, malformed null/result types, and missing transfers at unwrap, wrap, and return. Graphs validate but do not execute a null unwrap. Job2423 terminal0: 211 IR model checks in each Debug/Release configuration, clean builds. Public nullable source tests remain unpassed. Next implement emission, checker/lifetime integration, and run the public nullable contracts.

### Scalar nullable handles execute; container audit still open

Emitter integration adds explicit NullHandle/WrapNullable/UnwrapNullable emission.
Null is UINT64_MAX. Checked unwrap calls the existing located runtime-failure
path with `LYR013: null handle unwrap`, including unsafe mode. Counted conversion
consumes the IR Move without retaining again; nullable CopyAt skips strict retain
for null, while nonnullable retain remains strict. Nullable locals use the same
sentinel. ReadType now admits class-only OptionalType; contextual null, implicit
wrapping, explicit postfix unwrap, hints and lowering are connected. Nullable
field/method/destruction access requires unwrap (LYC304). Canonical compatibility
still rejects implicit nullable-to-nonnullable conversion.

Evidence: job2456 Debug passes 5/5 safe executions (including checked failure),
7/7 intended compiler rejections, and 1/1 exact unsafe null-unwrap failure.
Job2458 terminal exit0: clean Release build and identical nullable results;
IR model211 checks both Debug/Release; Debug multi-class13 executions/8 rejections,
ownership IR92 cases, array defaults12 safe/12 unsafe. No full cumulative claim.

Failures corrected: edit2449 refused an ambiguous anchor before any write; the
following shell build nonetheless ran and verified unchanged code, not the edit.
Retry2450 used fail-fast sequencing and a function-specific anchor. Unsafe-block
fixture2453 was invalid syntax; corrected to the existing --unsafe emission flag,
repeating only the defined null-failure runtime case (never dangling unsafe code).

Lesson: equal runtime representation does not imply equal ownership operation.
Wrapping/unwrap transfers a counted holder; copying null acquires no reference;
explicit unwrap checks null before any storage access. A destroyed ordinary
handle remains rejected through unwrap (retained LYC330 case).

OPEN BEFORE claiming general nullable support: ReadType also admits containers
of nullable types. Read-only emitter audit found CountedArrays._array_copy retains
all leaves unconditionally (null fails strict retain). Array resize initializes
new elements with C zero, wrong for nullable ordinary/counted handles and tuples
containing them. Add public container cases then fix counted-array copy with an
explicit nullable-leaf policy, recursive propagation, and typed default/empty
initialization (sentinel for nullable leaves, aggregate _empty for tuples).
Keep nonnullable object-array growth forbidden and nonnullable retain strict.
Aggregate _empty currently handles counted/aggregate components, not nullable
ordinary handles; Moved has the same gap. No edits to those paths yet. Nullable
links/fields, comparison, and the linked-list example remain unfinished.

## Nullable container emission corrected

Counted array-copy helpers now receive a nullable-leaf flag, propagate it through nested arrays, and skip retain only for null nullable leaves. Nonnullable retain remains strict. Nullable ordinary/counting handles use the sentinel in aggregate empty helpers; resize initializes typed nullable/aggregate defaults before publishing length, including dirty reused slots.

Public nullable contracts added nested null/live counted copies, counted array/tuple growth and regrowth, and runtime LYR013 for grown ordinary array/tuple null entries. Job2466 terminal0: clean Debug build;9/9 execution contracts,7/7 compiler rejections,3/3 unsafe checked failures. Job2467 terminal0: clean Release and same nullable totals; Debug retained array defaults12/12 safe and12/12 unsafe; tuples22/22 executions26/26 rejections; whitespace clean. No full cumulative claim. Editing script initially stopped after fixture write but before emitter write on ambiguous anchor; retry script quoting failed before writes; corrected scoped edit then built/tested. No pre-fix new-fixture baseline was executed.

Bill requested deferred representation optimization AFTER Chapter12 acceptance: AoS C pointers/NULL; SoA default32-bit indices/null0/reserved slot0/next allocation1; audit unnecessary object-access bounds checks. Captured/readback Cadence queue.md763–771; not implemented and no current safety weakening. Lesson: cleanup-safe/uninitialized storage is not a language default; typed null initialization must honor representation even inside containers. Next: actual participant/object field admission and null comparison, preserving constructor definite initialization and current lifetime boundary. No live jobs/freezes after2467.

## Object-field experiment — admission remains closed pending lifetime design

Added retained public graders/ch12/object_fields.py: four executions for ordinary/nonnullable/nullable and counted replacement/destruction; five intended rejections for missing initialization, early read, zero/null nonnullable assignment, and stored dangling ordinary referent. Baseline2487 terminal1:0/4 executions0/5 intended rejections, all unsupported Field. Ordinary scalar object-field admission temporarily enabled; allocation sentinel initialization, explicit generated Unref for owned scalar fields, and ApplyClassEffect visited closure through counted fields/finalizer summaries implemented. Experimental2494 clean Debug build,4/4 execution cases pass. Four negatives had fixture setup defect (missing executable main, LYC305); corrected those fixtures afterward, not yet rerun for intended errors. Fifth negative was accepted: destroying Box then reading Holder.item.n lost stored referent history. It was never executed.

Admission gate restored before further use; class object fields remain unsupported. Supporting cleanup/effect edits retained but not full-regression-certified. Job2499 terminal0 clean Debug, nullable9/9 execution7/7 rejection3/3 unsafe runtime contracts, whitespace clean. Release predates these supporting edits. No full cumulative or object-field safety claim. No live jobs/freezes, commits, workers, pushes, installs.

Observed cause: ordinary expression completion creates fresh object history; receiver Holder history currently does not preserve stored Box destruction history. Per-field-declaration state alone conflates instances; clearing such state on assignment could revive another object's stale link. Need settle conservative transitive handle obligations versus instance-sensitive stored-field history before opening admission. Recommend retaining class-wide conservative effects initially, with explicit limits on recovery after referent destruction; ask Bill about required precision rather than silently imposing a new language restriction. Constructor checks already enumerate semantic fields but corrected new negatives still need actual intended-diagnostic verification once admission reopens. Participant field type environment/view propagation and null comparisons remain unfinished.

## Chapter12 write permission settled — relation-only restriction deferred

Bill explicitly permits ordinary code to write regular-object reference fields on regular objects in Chapter12, so multi-class relationship algorithms can be implemented now. Cadence agrees. Defer the restriction/exception machinery that limits these writes to designated generated relationship operations until the relation-statement chapter. No relation keyword, generated-code permission system, owns/refs accounting, or relation-aware teardown is a prerequisite to the current write permission.

This relaxes mutation authority only, not lifetime safety. Counted value-class fields retain their referents; explicit destruction of value-class objects remains rejected. Regular-object stored references are nonowning borrows and must prevent invalid destruction while required; fresh field reads cannot erase destruction history. Do not reinterpret this authorization as allowing dangling accesses or uninitialized reads. Current implementation gate may remain temporarily closed while that safety is developed, but is not the agreed language restriction. Continue personally with counted-field verification and regular-field lifetime integration; no new worker/commit/push/install authority.

## Counted scalar object fields verified; ordinary links next

Ordinary class field admission now permits scalar counted nominal/nullable handles only, retaining the temporary gate for regular handles and object arrays until their lifetime integration. This gate is implementation staging, not the agreed write restriction. Generated destruction explicitly Unrefs owned scalar fields; storage begins at current sentinel so first owning assignment can safely release empty backing. ApplyClassEffect conservatively closes over counted field destruction and hook effects with a visited set. No new relationship accounting policy.

Public object_fields.py now contains6 execution cases and10 rejection cases total, including pending ordinary cases; NOT all pass yet. Focused counted subset4 executions5 rejections: replacement releases previous value, final-before-field-cleanup order, copied handle outlives holder, returned struct keeps handle alive; missing init/early read/zero/null nonnullable stores and explicit counted destroy reject. First2509 run4/4exec1/5reject: four compact fixtures lacked declaration separator before main (LYC201); corrected eight ordinary/counted fixtures with semicolon. Job2510 terminal0 Debug+Release clean builds,4/4exec5/5reject each; whitespace clean. Job2512 terminal0 Debug retained ownership IR92/92, multi-class13/13exec8/8reject, nullable9/9exec7/7reject3/3unsafe runtime; no full cumulative/SoA-new-field claim.

Integration inspection Check.cs2096–2180: ReferenceOccurrences stops arrays/class handles, follows explicit struct reference fields and tuples; FormalStorage builds per-occurrence ancestry-folded graph with external/private root distinction. Existing events13–21 already support copied history, live storage, weak updates, calls, initializer results. Ordinary heap links require extending this actual shape/value/storage model rather than resetting field histories or adding parallel safety checker. No implementation of that extension yet. Next inspect remaining FormalStorage and consumers before selecting heap-node representation; preserve stored-handle vs storage-address distinction and counted ownership. No live jobs/freezes, no worker/commit/push/install authority.

### Ordinary-link lifetime audit and public contracts

- Bill's temporary write permission remains authoritative: ordinary code may mutate regular-object fields in Chapter 12; generated-only mutation permissions wait for relation statements. No lifetime exception is implied.
- Added six public contracts to `graders/ch12/object_fields.py`: detach through an alias and through a helper (positive); attached link blocks destruction even without a later read, copied-out borrow survives detachment as an independent obligation, second holder remains attached, helper detachment does not clear another holder (negative LYC330). Total file now eight executions and fourteen rejections; ordinary-field cases remain pending behind the implementation gate. No unsafe negative was executed.
- Job2530 terminal exit0: all22 fixture sources passed the published syntax recognition harness with the existing Debug assembly. Python syntax and `git diff --check` also pass. This is syntax evidence only, not semantic admission or lifetime enforcement.
- Source audit: `ReferenceOccurrences` ends at class handles; `FormalStorage` preserves sibling identity and folds ancestry, but has only value/explicit-reference storage. Parameter roots are private unless explicitly referenced. `Select` for class fields presently records declaration-root storage and receiver validity, not per-object field contents. `BuildExpression` completion creates a fresh object-history record. Thus simply admitting ordinary fields loses stored handle history.
- Existing events13/17/19/20 carry snapshots, live referent reads, weak updates, and formal/actual mapping; reuse those semantics rather than building a second checker. `ApplyMutation` and object effects currently check invalidation at use, not persistent attached-link obligations at destruction. Successful-exit result graphs are currently exported for field initializers, not ordinary handle returns. Heap-link integration must connect all these paths, including constructors, without expiring heap storage when a local handle slot ends.
- No compiler edits in this audit slice. Counted-field verified milestone above remains unchanged. No live jobs after2530; no full cumulative/SoA/new ordinary-field pass claimed.

### Stored-borrow graph prerequisites: typed occurrences and shared result capture

The temporary finite-storage graph now records each occurrence's referent type directly instead of retaining a field symbol solely to read its type. Actual member lookup and projection still use semantic field identity; repeated occurrences remain distinct, and ancestry-only folding is unchanged. This avoids inventing a synthetic field for the future handle-to-heap edge. It does not yet traverse class storage or open ordinary-object field admission.

Extracted `CaptureResult` from initializer-only graph construction. It allocates ordered result value/destination slots once per body analysis and maps each explicit return path into the same slots after cleanup, preserving graph aliasing and invalidation history. Initializers use this operation too; ordinary currently permitted return types have no such contained-reference slots. Exit-summary export recognizes the result summary rather than testing initializer ownership. Heap constructor results and caller result imports remain unfinished; this is not ordinary stored-link support.

Verification: job2541 Debug build clean and retained contained-reference suite20/20 executions23/23 intended rejections after typed-occurrence edit. Extraction first failed C# CS0136 because a new loop variable `target` collided with an enclosing switch local (job2545); no graders ran. Renamed `resultTarget`; job2546 terminal0 clean Debug build and20/20+23/23. Job2547 terminal0 generic suite17/17 executions10/10 rejections and `git diff --check`. Source helper read back969–1090. No fresh Release or cumulative claim. Ordinary-object fixtures remain gated/pending; no unsafe negative executed. No live jobs after2547, no commits or representation changes.

### Heap storage role and returned-alias provenance

The finite formal graph now distinguishes value storage from heap-object storage
in both nodes and edges; ancestry folding requires both canonical type and storage
role to match. Existing traversal still creates only value-storage edges. Heap
field traversal is not yet activated, and ordinary object-field admission remains
gated pending lifetime integration.

Result summaries now retain the set of external formal slots corresponding to each
result destination. Export compares actual live destination identities, not equal
types or equal target contents. Caller-side import does not yet consume this new
provenance. This is needed so returning a holder alias preserves its connection to
caller-owned field storage rather than importing a disconnected graph copy.

Added two public pending contracts to object_fields.py: detachment through a
returned alias succeeds; detachment through that alias does not clear a different
holder's attachment. The file now contains nine execution contracts and fifteen
compiler-rejection contracts. These two new sources have not yet been parser-checked
or behaviorally passed; ordinary links are still gated.

Verification: job2553 clean Debug build for storage-role vocabulary. Job2562 failed
with five compiler errors because the body-analysis summary parameter retained its
old tuple signature; fail-fast prevented tests. Corrected that signature. Job2565
terminal exit0: clean Debug build, 20/20 contained-reference executions and 23/23
intended rejections. No Release, full cumulative, or ordinary-link pass claim.
Next: connect result import to caller destinations using the recorded formal slot
provenance, preserving target invalidation histories and aliases; then connect
heap creation/field projection/constructor transport and destruction obligations.

### Shared result import preserves storage provenance

Export now records HasLocalStorage independently of ExternalSlots. Exact live
slot matches identify caller storage; unmatched destinations preserve a local
alternative. A function may return either on different paths, so an external
match must not erase the local possibility or fabricate a second fresh object
when only caller storage exists.

Event21 now imports already-substituted source/cause histories with ReplaceTargets
and reconnects external edges through actual destination addresses. Fresh result
slots are used only for exported local-storage alternatives. Shared ImportResult
serves field defaults and ordinary calls producing nonempty storage graphs;
missing return summaries retain bottom until the outer fixed point discovers an
exit. Ordinary heap traversal is still inactive and ordinary fields remain gated,
so caller-alias behavior is connected but not yet behaviorally exercised.

Verification: job2575 terminal0 clean Debug build and contained-reference20/20
executions23/23 rejections for export distinction. Job2579 terminal0 clean Debug
build and same20/23 plus whitespace check after shared importer. No Release/full
cumulative/ordinary-link success claimed. No live jobs or input freeze. Next
connect heap object creation, field projection/stores, constructor transport and
persistent attachment destruction obligations before opening field admission.

### Constructor result graph boundary

Constructor signatures declare unit, while constructor expressions return the actual self object. `ResultStorageType` now obtains the concrete receiver type from the actual constructor parameter, and shared capture/import uses it. `UseConstructedReceiver` captures existing receiver slots at the established post-cleanup exit boundary (normal fallthrough and explicit return), rather than reacquiring links. Heap traversal remains inactive and ordinary-object fields remain gated: this is preparatory, not ordinary-link support.

Job 2589: Debug build zero warnings/errors; retained borrowed_structs 20/20 executions and 23/23 intended rejections; whitespace check; terminal exit 0. No Release/full suite claim. Next connect constructor self as fresh heap storage (not external formal storage), field projections, and caller mapping together before admission. No workers/commit/install/push.

### Constructor self is not external caller storage

Formal-slot registration now excludes constructor self from `externalFormalSlots`, while preserving external slots for constructor arguments and ordinary method receivers. Constructor self is created by the invocation, not supplied by the caller. This prevents result import from incorrectly reconnecting new object storage to a nonexistent incoming self parameter when heap traversal is activated.

Job2610 terminal exit0: clean Debug build (zero warnings/errors), contained-reference20/20 executions23/23 rejections, counted-object-field4/4 executions5/5 rejections, git diff --check. No Release or ordinary-link support claim. Heap traversal remains inactive and ordinary-object fields remain gated; no allocator change.

Direct source inspection: events17/18/19 preserve live-slot selection and weak updates; event20 maps formal-to-actual graph pairs. ApplyClassEffect currently invalidates by class but does not reject persistent attachments at destruction. The next connected increment must create constructor self storage at the call (no incoming self argument), project per-object field slots in Select while preserving existing array-address checks, transport those slots through return import, and check attached-link destruction even without a later use. Null must represent no attachment; a nonnull target must retain heap identity even if it has no reference-bearing fields. Do not mistake the preparatory graph regressions for this integration.

## Ordinary-link integration: destruction-time call-state boundary

Source inspection after the constructor-self checkpoint found an additional
interprocedural requirement. `CheckBorrows` applies `classEffects[target]` before
the batched `replacements[target]` in its call path. These summaries preserve
possible destruction and final field contents, not attachment state at each
destruction. Moving all replacements before effects would accept the inverse
unsafe order. Existing ordered mutation/use obligations are not, by themselves,
a persistent-attachment guarantee.

Added pending public pair in `graders/ch12/object_fields.py`: a helper that clears
its holder link before destroying the referent, and a helper that destroys first
and clears afterward. They have the same final contents and destruction effect;
the first must succeed and the second must reject with LYC330. These are pending
contracts, not passes. Python fixture syntax and whitespace were checked only;
no new compiler build, Lyric parse, or execution is claimed.

The existing slot graph also does not yet represent heap object identities:
objects with zero reference-bearing fields still need an identity distinct from
null. Ordinary field admission remains gated. No compiler edits were made in
this inspection slice; constructor-self Debug verification remains the latest
compiler evidence. Before a larger analysis change, discuss explicit temporary
heap storage identities and destruction-time attachment summaries with Bill.
Do not describe enabling traversal alone as sufficient or reorder summary batches
to manufacture acceptance. Existing counted-field support remains distinct.

## Corrected scope and executable participant fields

Bill clarified explicitly that Chapter 12 is not a complete memory-safe milestone.
Restore the original plan: ordinary regular-object links are writable for manual
relationship implementation; examples explicitly detach, and later relations
provide controlled mutation and generated teardown. The top-level boundary now
states this. No existing counted/initialization/borrow guarantees were waived.

Removed 22 reviewed Check.cs diff hunks belonging to the premature heap-analysis
preparation: generalized result import/export, external-slot provenance, inactive
heap roles, and constructor-result graph handling. Preserved original Chapter 10
initializer analysis, counted-field destruction closure, nullable support, and
participant lookup. Exact pre-cleanup source and reverse patch are retained in
Cadence's private data, not public student inputs. Provisional ordinary-link safety
rejection sources are preserved as superseded non-executable development history
in ch12-superseded-object-link-checks.md, not future requirements by default.

Job2635 terminal0: clean Debug build; object fields10/10 execution9/9 rejection;
contained structs20/20 execution23/23 rejection. Then added a real participant-field
fixture. Baseline2644 exit1 was LYC301 unknown C in field annotation. Fixed through
SemanticScope(MultiClassInstantiation): analysis+instance ownership, weak template
lexical parent, rollback on failed attachment. Fields resolve role types in one
private annotation scope; concrete field placement/physical ownership unchanged.
Scalar nominal fields admitted for ordinary and contributed fields; object arrays
remain outside this slice. No new heap safety machinery.

Job2645 terminal0 clean Debug and new two-way link execution1/1 at O0/O2. Full
Release job2647 terminal0: clean build; multi-class14/14 executions8/8 rejections;
object fields10/10 executions9/9 rejections; existing model359 checks in each Debug
and Release. Not full cumulative verification, whole-language safety, or a completed
list. Existing probe does not yet directly test new annotation-scope constructor.
Next: nullable participant-view compatibility/propagation and focused lifecycle
cases, then null comparison and executable list. No live jobs/freeze after2647.

## Nullable participant views verified

Central `ParticipantBinding.AcceptsViewType` admits only its canonical nominal
type or that type's nullable wrapper. Symbol/fact admission and SetType share
this query; arrays, tuples and other classes remain excluded. Optional type
annotations select the participant view. Contributed fields attach it from the
private annotation scope; field facts, unwrap facts and nondiagnostic member/unwrap
hints preserve it. Runtime types, ownership and nullable dereference rules unchanged.

New same-class role-chain fixture initially failed LYC329 at peer!.value(). First
patch still failed because CheckField returns its own fact before common expression
completion; attaching the field view there fixed lookup. Explicit destruction of
two same-class endpoints then hit existing conservative LYC330. Fixture now detaches
and leaves ordinary objects alive to process exit; this is lookup, not reclamation
evidence. Explicit Graph? local loses the role and intentionally rejects LYC329;
inferred and C? locals retain it.

Job2663 terminal0: expanded real-model probe485 checks in each Debug/Release,
including private annotation-scope ownership/retirement, nullable facts/symbols,
nominal/nullable SetType preservation, wrong-class/container refusal. Job2664
terminal0: clean Debug/Release builds, each multi-class15 executions/9 rejections,
nullable9 executions/7 rejections plus3 checked unsafe-runtime failures. Whitespace
clean. Not full cumulative verification or whole Chapter12 completion.

Lesson: endpoint lookup identity survives nullable wrapping, but does not change
canonical identity, ownership, or class-wide destruction precision. Next null
equality and executable list; no persistent attachment checker or allocator change.
No live jobs/freeze after2664.

### Nullable handle identity equality

Implemented same-nominal-class `==`/`!=`, with nullable operands on either side.
`SemanticTypes.CanCompareHandleIdentity` is declared in Common and shared by
checking, IR validation and emission. Contextual null uses the existing neighboring
operand hint; equality neither unwraps nor narrows and does not consume ownership.
Ordering and different nominal classes remain rejected. Emission directly compares
already-sequenced handle values; runtime representation is unchanged.

Public tests precede the implementation: job2674 exited1 at LYC313 for both new
positive fixtures (9/11 executions,9/10 intended rejections). Initial fixture edit2673
refused an ambiguous anchor before writing; non-fail-fast sequencing then ran the
old suite, which is not new evidence. Narrowed line anchors and fail-fast commands
corrected that procedure. Job2678 clean Debug build and job2680 clean Release build
both passed11/11 nullable execution contracts,10/10 compiler rejections and3/3
unsafe checked-unwrap failures. Job2680 additionally passed250/250 retained Chapter6
execution cases in Debug. Job2685 clean probe builds passed221 IR checks in EACH
configuration, including nullable equality admission and ordering/arithmetic/mixed
boolean refusal. No full cumulative pass or complete Chapter12 claim.

Lesson: observing nullable identity is independent of dereference permission,
participant lookup view and counted ownership. Next: explicit doubly linked list,
with membership checks before mutation and explicit detachment; no heap attachment
checker or automatic relationship teardown is part of this milestone.


### Executable explicit doubly linked list

The public multi-class fixture now contains the actual DoublyLinkedList<P,C>
algorithm. Parent first/last and child parent/previous/next are nullable role-typed
fields. Append refuses an attached child before writes; remove refuses a child
whose parent differs before writes, reconnects neighbors/endpoints, then clears
all child links. No compiler mechanism was added in this slice. Nullable field
substitution, role-preserving unwrap, and identity equality integrate successfully.
The transition fixture checks empty/singleton/head/middle/tail and reattachment.
Detached ordinary nodes remain allocated until process exit; conservative
same-class destruction precision and automatic teardown are not established.

Job2691 passed the initial transition case at O0/O2. Job2693 passed four exact
assertion refusals; shared Chapter5 harness extended backwards-compatibly to accept
(line,column) locations, with method/main traces. Job2694 terminal0: Debug and
Release each16/16 multi-class executions9/9 semantic rejections,4/4 list refusals,
and1/1 SoA transition case; retained Chapter5 Debug80/80, whitespace clean.
Job2697 then passed the added counted-list case; job2698 terminal0 passed that case
in Debug/Release and default/SoA layouts, each at O0/O2. Current inventory17
multi-class executions9 rejections; no claim that the17 were run in one combined
batch. The counted case verifies empty links after removal and liveNGdone output,
not universal leak freedom. No full cumulative run or chapter completion claimed.

Lesson: ordinary contributed fields keep ordinary ownership semantics. Counted
links can cycle; explicit removal breaks the example's cycle. Nullable fields do
not imply generated detachment. Refusal exit/diagnostics prove intentional
rejection; guard-before-write source ordering supplies the no-partial-mutation
argument, since fatal assertions do not return inspectable heap state.

Guide status/counts and algorithm explanation updated. Corrected stale object-field
comment implying persistent attachment tracking; no fixture or language contract
weakened. No workers/reviews/student, commits, pushes, installations, or reminders.


Independent-list fixture2701 passed Debug/Release, default/SoA, O0/O2: a shared
concrete child joins two distinct labeled bindings; removing one preserves the
other, then removing the second permits explicit endpoint destruction. Inventory
now18 executions9 semantic rejections plus4 runtime refusals. Corrected provisional
internal-zero wording at its original decision point to current UINT64_MAX sentinel.
Cumulative Make target now includes Chapter12 syntax and all four behavioral
runners. Full cumulative verification starts next with compiler/grader inputs frozen;
no result inferred from admission. Diagram implementation is tools/common-diagrams,
not the nonexistent tools/diagrams guessed in inspection2702.

## Full cumulative reference milestone

Job 2703 terminated with exit 0: 33 public runners in each configuration, two clean builds, all retained and Chapter 12 behavior checks passed. Numerical summaries are preserved in [the cumulative receipt](ch12-cumulative-verification.md). Inputs stayed frozen until terminal result; no compiler edits occurred during this run. Source-checked diagrams, complete teaching/exercises, and walkthrough remain in progress. No review/student/human acceptance is implied.

## Practical list walkthrough

`make spin12` job 2723 terminated with exit 0; saved output read back in job 2724. Clean Release build, exact three lines (two nodes linked; head removed; all links detached), empty runtime stderr enforced, disposable files removed. The standalone source uses counted participants and explicitly removes both memberships before holder cleanup. It imports no grader at runtime. Author execution only; Bill has not accepted this chapter. Chapter 12 construction prose is now drafted through list algorithms; diagrams and exercises remain unfinished. No compiler edits or additional cumulative rerun.

## Source-checked Chapter 12 diagrams

Reviewed the changed lifecycle paths against accepted Chapter 11, including
instance-owned annotation scopes, contribution/syntax retirement, participant
view maintenance and nullable reverse dependencies. Added chapter12.json and a
pinned, guarded chapter12.py publisher. Fresh inventory: 22 non-static Common
types and 57 relationships; eleven additions appear exactly once across six
views. All historical relationship records remain unchanged.

The first role view passed size validation but its parallel edges crossed;
split contributor membership from value views and inspected both final renders.
All six final PNG previews were visually inspected. Minimum ten-point text at
five-inch width passes; physical print acceptance is unperformed.
Job 2745 terminal exit 0 published/read back fourteen outputs, verified all 41
pre-existing figure files unchanged, verified byte-identical regeneration, and
passed git diff --check. No compiler/grader changes or repeat cumulative run.
The chapter opening now contains actual source-checked diagrams and adjacent
class descriptions. Construction teaching and exercises remain unfinished;
no review/student/human acceptance is claimed.

## Construction teaching and exercises drafted

Expanded Chapter 12 from the inspected registration, BindMultiClass, CheckField,
ConstructorReceiver, and namespace-lowering paths: preflight before contribution,
private aliases and copied syntax, ordinary function admission, lookup precedence,
receiver forwarding, and physical-field inventory. Recovered the clipped binding
publication loop before writing its explanation. Corrected append prose to the
actual previous/next/parent write order. Added exercises from accepted Chapter 11,
external read-only grader commands, cumulative and separate PEG obligations,
retained classification, structural review, setup boundaries, and acceptance limits.
Removed the stale unfinished-figures/exercises footer. Updated the public guide's
status and clarified which ordinary list fixture leaves detached allocations until
process exit versus which independent-binding fixture explicitly destroys endpoints.

Jobs 2752/2753 and bounded readback verified prose writes, whitespace, chapter
links, fixture/script path existence, and the PEG fixture-argument contract.
No compiler, executable grader, or fixture change; cumulative 2703 and walkthrough
2723 remain the behavioral evidence, not rerun for prose. Diagrams retain their
verified generated block. Reviews, independent reconstruction, and human acceptance
are still pending; no worker or commit authority inferred. No live jobs.

Lesson: semantic placement disappears during lowering, while selected symbol
identity survives. This avoids both runtime namespace wrappers and backend lookup.

## Autonomous review and reconstruction authorized

Bill authorized the one code reviewer, one text reviewer, and fresh isolated
student while he spends Thanksgiving with family; he remains available for
questions. No commit, push, install, restart, Chapter 13, or reminder authorization
is inferred. Code reviewer ch12-code-review admitted as job 2757, read-only
privileged reference review. No text reviewer or student admitted yet.

Prepared accepted Chapter 11 prerequisite directly from commit
5947bed06e8327b0e3c0c079c14508847be0f84f in Cadence's ch12-student-inputs/accepted-ch11.
All 39 compiler files byte-verified against Git blobs; separate SHA256 receipt
retained. No live Chapter 12 source, Git history, builds, private notes, or repair
history included. Final public bundle waits for review corrections. This export
is input-integrity evidence, not a fresh build or student result.

Student setup preparation job 2770: a separate disposable copy of the 39-file
accepted Chapter 11 export restored offline and built Debug with zero warnings
and errors. Package feeds cleared by the accepted NuGet.Config; CLI home, package
cache, temp directory local; certificate generation disabled. Original export
unchanged. Copied and byte-verified 145 installed PEG dependency files; all copied
symlinks resolve within node_modules. No download. These are setup evidence only.
Future student imports must name accepted-ch11 and peg-dependencies separately,
not their parent directory (which contains setup-check artifacts/private receipts).
Code review join 2763 remains active; latest observation cursor 74 was clipped,
not full review evidence. Reviewer coding instructions activated, no failure.

## Sole code review and author follow-through

The sole privileged code reviewer `ch12-code-review` submitted on join2763 and
was shut down. Its full consolidated report is retained in ch12-code-review.md.
R1 confirmed that wrapper-traversing SemanticType.Class admitted a nullable
participant. New retained labeled/flat fixtures both falsely accepted on the
unchanged Release assembly (2789 exit1, 0/2 intended rejections). BindMultiClass
now requires SemanticTypeKind.Nominal before accepting its class. The chapter
teaches this distinction at the binding introduction.

Focused author job2790 completed exit0: clean Debug and Release builds, each
18/18 multi-class executions and 11/11 intended rejections, 11/11 nullable
execution contracts, 10/10 nullable compiler rejections, and 3/3 checked unsafe
unwrap failures. Compiler/grader inputs remained frozen during this run; only
prose changed. The earlier full cumulative2703 remains baseline evidence, not a
fresh full-suite claim for this narrow admission correction. Whitespace clean.

R2 resolved by describing the actual nullable harness contract: exact message,
path, stack, exit and stdout, but positive—not exact—line/column. No grader was
weakened or runtime implementation failure inferred. The guide now names the
transition fixture as leaving detached ordinary nodes allocated, distinct from
the independent-membership fixture's explicit destruction. No re-review.
Lesson: canonical navigation is not nominal admission; a plausible positive
source position is not proof of the exact unwrap location.

## Sole text review incorporated

Text reviewer ch12-text-review submitted once on join2800. Full report retained
in ch12-text-review.md (12,986 characters), read back, worker shut down. No
re-review. Corrected chapter runtime-location evidence and verification chronology;
nullable runner docstring now matches its unchanged comparison logic. Clarified
contributed declaration initializers are syntax-only, with initialization in
concrete constructors. Added the demonstrated participant-block storage form to
specification Section 13 and qualified capability-only empty-impl statements.
This reconciles existing requirements, not a new implementation or safety project.
Both reviews are incorporated; independent reconstruction and human acceptance
remain pending. No executable logic changed in this slice; no redundant test run.
Guarded diagram publication preserves current teaching and all existing figures.
Lesson: syntax support, semantic admission, and exact evidence claims are distinct.

## Fresh isolated reconstruction admitted

Student ch12-student admitted2850; join2851 running. Exactly one new student,
no target/private-history access. Prerequisite:39 exported accepted Chapter11
compiler files from5947bed06e8327b0e3c0c079c14508847be0f84f, earlier byte-verified.
Final public bundle2849:149 byte-verified files plus SHA256 manifest, public
chapters00–12/spec/grammar/graders/Unicode/figures/Makefile/walkthrough/PEG tools.
Only three historical provenance links are intentionally unavailable; all local
chapter links resolve. Offline Peggy dependency tree separately imported.
Only those three input subtrees and installed toolchains are read-only command
imports; no reference tree, private notes, Git history, setup-check or probe source.
Student owns candidate implementation and candidate grammar; immutable public
checks may not be altered. Required cumulative33 runners/config, five PEG checks,
structural self-review and spin12. Parent may clarify/correct prose or defective
graders, never patch candidate. Stop at full applicable success, no passing rerun.
Human acceptance pending; no commit/push/install/restart/Chapter13 authorized.
Guarded publication2848 read back14 outputs, all54 figures unchanged, whitespace
clean. Both reviews complete and shut down; no new review campaign.

### Student clarification: compile-only invalid-reference negatives

During the same `ch12-student` reconstruction, question 8 identified ambiguous assignment wording: “Do not run invalid dangling-reference negatives” versus requiring all public runners. Clarified that compiling a negative to verify its intended diagnostic/nonzero exit is required; executing the invalid generated program is prohibited. The public `nullable-unwrap-after-destroy` case had correctly rejected with LYC330 and no invalid program execution. No grader, contract, or student implementation was changed by the author. Student reported partial nullable progress only (4/11 execution contracts, 6/10 static checks), not completion. Join 2851 remains active; last observed child History cursor 58 before this question. Required skills activated and approved input access succeeded. Earlier clipped history is progress evidence, not a full source review.

## Local publication checkpoint authorized

Bill authorized committing current work, including Chapter 12, for his own push.
This supersedes earlier no-commit notes, not the remaining reconstruction gates.
Student cumulative Debug/Release and spin12 receipts inspected; five PEG checks
blocked by Node libuv sandbox access. See ch12-student-status.md. No student
implementation edits, passing-suite rerun, push, or human acceptance implied.

## Independent reconstruction completed

Same student completed remaining five PEG comparisons after bounded local Node
runtime/OpenSSL setup; join2996 terminal submission. Parent inspected numerical
receipts, status exits, structural report and frozen-input integrity receipt3009.
Full submission saved/readback ch12-student-acceptance.md. Student shutdown confirmed;
no passing cumulative suite/walkthrough rerun, candidate patch or new reviewer.
Human acceptance remains pending. Introduction rewritten from Bill's supplied
public-post prose; student frozen teaching unchanged. These edits postdate94cb532.
Lesson integrated beside setup instructions: node --version does not establish
script startup; restricted runtime dependencies/configuration need bounded setup.

## Human acceptance — November 26, 2026

Bill explicitly accepted Chapter 12, both the text and `spin12`. This supersedes
earlier pending human-acceptance statements. No further student run or review is
required. Commit permission was separate from this acceptance; Bill subsequently
authorized committing these book updates for his own push. No Chapter 13 work
is authorized by this acceptance.
