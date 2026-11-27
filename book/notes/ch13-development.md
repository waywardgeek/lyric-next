# Chapter 13 — Maintained relationships and generated teardown

> This is a chronological development record. Opening design sections are historical,
> not the current implementation status. Later decisions supersede them: participant
> scalar locals/parameters borrow; debug uses allocation identities; Chapter14 owns
> aggregate borrowing, handle redesign and the completed safety claim. See the latest
> entries below and the current Chapter13 construction draft.

## Status

Design agreed with Bill after Chapter 12 acceptance, November 26, 2026. This is a plan, not an implemented chapter or grammar. Documentation work only: no compiler changes, new graders, reviewers, student, or commit.

Target single-threaded memory safety for the supported subset, conditional on correct compiler/runtime and trusted relationship implementations. The standard library supplies core relationship algorithms. The compiler does not prove arbitrary add/remove algorithms correct. Do not revive the discarded general heap-attachment checker or add a structural-proof system. Concurrency remains later work.

## Agreed syntax

```lyric
interface DoublyLinkedList<parent P, child C> {
    class P {
        first: C?
        last: C?

        pub add func append(self, child: C) {
            prepare {
                assert(child.parent == null)
            }
            child.parent = self
            child.previous = self.last
            child.next = null
            if self.last != null {
                self.last!.next = child
            } else {
                self.first = child
            }
            self.last = child
        }

        pub remove func remove(self, child: C) {
            prepare {
                assert(child.parent == self)
            }
            if child.previous != null {
                child.previous!.next = child.next
            } else {
                self.first = child.next
            }
            if child.next != null {
                child.next!.previous = child.previous
            } else {
                self.last = child.previous
            }
            child.parent = null
            child.previous = null
            child.next = null
        }

        partial owns destroy(self) {
            while self.first != null {
                self.first!.destroy()
            }
        }

        partial refs destroy(self) {
            while self.first != null {
                self.remove(self.first!)
            }
        }
    }
    class C {
        parent: P?
        previous: C?
        next: C?

        partial owns destroy(self) {
            if self.parent != null {
                self.parent!.remove(self)
            }
        }

        partial refs destroy(self) {
            if self.parent != null {
                self.parent!.remove(self)
            }
        }
    }
}

impl DoublyLinkedList<Graph:nodes, Node:graph> owns {}
```

Agreed syntax, not parser-tested. The impl modifier selects partial owns destroy or partial refs destroy at specialization. owns requires an ordinary child; binding it to a counted value child is rejected. refs to an ordinary child only detaches; refs to a counted child contributes exactly one retain per membership and releases it on removal. No ordinary/counted owns variants are needed. The old destructor owns P / destructor refs C spelling is superseded. Bill grounded this choice in his DataDraw code generators: use explicit policy-selected code, not a general policy mechanism.

Roles are explicit, including when one concrete class fills both. owns/refs belongs to the implementation; relation syntax is shorthand for that machinery. Initial recommendation: one parent role, one child role, one designated removal method; multiple add methods may support append/prepend. Exact signature and duplicate-admission rules need contracts.

## Mutation and counting

prepare is first, checks preconditions and may prepare storage, but changes no logical membership. Its locals remain in scope for structural updates. The structural body has no normal early return or recoverable propagation. Invalid membership check-fails before mutation; Result/Status is not a prerequisite. All interface helpers share its managed-link privilege. The trusted algorithm must sequence operations correctly during temporary inconsistency; ordinary lifetime effects still apply.

For a refs membership to a counted child, insert one retain after preparation and before structural updates. Insert one release after removal reconnects neighbors and clears membership. Release may finalize at zero; subsequent accesses and ordinary local cleanup must respect its effects. refs to ordinary children adds no count. owns accepts only ordinary children and cascades destruction. No source manual ref/unref.

All object-reference fields contributed by an explicitly parent/child relationship interface are managed automatically. The compiler initializes detached links. Applications may read subject to visibility/lifetime checks but cannot write or obtain writable references that bypass the relationship operations. All code in the declaring multi-class relationship interface is privileged to maintain its managed links, including helpers, getters and iterators. Privilege follows the specific relationship binding and does not grant arbitrary application code access or waive type/initialization/lifetime checks. Correct link manipulation is part of the trusted library contract.

Only structural storage suppresses ordinary per-field counting. Locals, arguments, results and unrelated fields keep normal ownership. Backlinks and siblings do not each retain endpoints. General multi-class generics remain ordinary contributions, not implicit relationships.

The library author must maintain reciprocal links, unique membership, reachability and valid teardown. An annotation is not certification. Standard implementations require adversarial tests and source review, not claims of compiler proof.

## Destruction phases

1. Mark destruction in progress; run final only for a completed object.
2. **Detach from parents:** fragments declared in child participant blocks, in any order within this phase.
3. **Clean up children:** fragments declared in parent participant blocks, in any order within this phase.
4. Release remaining ordinary fields.
5. Free storage last.

All parent detachments finish before child cleanup. Name phases by their action, avoiding ambiguous parent-role/child-role terminology. A concrete class may contribute to both phases. Fragments return unit, are called only by generated destruction, and cannot cancel destruction or free self.

Do not pre-detach an ordinary owned child before invoking its destructor: its final runs before its own detachment. Partial construction skips its own final; completed children finalize normally. Every fragment reads current membership because earlier fragments/cascades may already have removed members. Counted children retained elsewhere must not be forcibly destroyed.

## Why representation-specific fragments

The cross-relationship example discussed with Bill: P contains A and B through R; A owns B through S. Destroying A may remove and destroy B. A saved successor can therefore be stale even with correct maintenance.

The doubly linked fragment rereads first after each destruction instead of retaining next. A slot-preserving array can read each slot afresh and skip nulls. Swap-removal requires different traversal to avoid skipping moved elements. No stale interior addresses may survive invalidating storage mutation.

This explicitly supersedes the earlier requirement to use safe iterators for generated teardown and the earlier rejection of repeatedly reading first. Application generators remain explicitly safe func ... -> gen C when they promise current-child removable traversal; no automatic upgrade. Their consumer/effect contract is separate from destructor fragments.

A true tree under one exclusive-parent relationship protects siblings from descendant-only cascades. Same-class destruction alone is too coarse to describe that protection. Cross-relationship cascades require separate treatment; do not silently relax application safe-iterator rules before deciding their revised contract.

Lesson: representation-specific teardown belongs beside trusted add/remove algorithms. Compiler orchestration need not infer a universal traversal strategy. Use action-based phase names to prevent ordering confusion.

## Latest decisions and remaining engineering work

Bill clarified: there is no owns relationship with counted children. Each refs membership to a value object contributes one reference, not zero and not one per structural link. This supersedes the earlier owning-counted and weak-counted questions. All code in the multi-class interface is privileged; separate helper privilege annotations are unnecessary.

Persistent ordinary references outside maintained relationships are rejected in the safe subset, including container/aggregate hiding. Counted ordinary fields remain permitted. Bill confirmed this was already the intended end-to-end rule. Chapter 12's temporary allowance must be closed through explicit teaching and retained-test updates, preserving historical evidence rather than rewriting it.

The substantive policy questions are settled. Remaining engineering work includes checking teardown receiver validity across same-class cascades, complete effects, reentrancy, partial construction, self-relations, and exactly-once membership release. Do not manufacture a zero-count-but-still-retained-membership case: refs membership itself holds a counted child alive. Finalization of a counted parent can still release children, with all transitive effects accounted for.

Resolve minor source admissions with concrete contracts: contextual keywords, duplicate fragment rules, extra add parameters, and relation shorthand mapping. References to nonparticipant classes still require actual maintained lifetime protection; privilege and managed-field naming alone do not establish it.

Lesson: owns is cascading ordinary-object ownership; refs follows the target lifetime category. A counted membership retains once. Trust the whole relationship implementation to maintain its representation, while continuing compiler lifetime and type enforcement.

## Next step and evidence limits

Cadence recommended waiting until morning to implement: this is a substantial lifetime/compiler increment, not a small overnight task. Bill requested documentation preservation and a local commit regardless; no autonomous compiler campaign is started. Next, review small cascade/counting traces and public positive/negative contracts with Bill, then follow the normal development-driven chapter process.

Planned checks: list transitions, multiple memberships, graph source/target edges and self-loops, shared-child cascades, final/detach/free ordering, exactly one membership count, refs cleanup, partial construction, external link-write/writable-borrow refusal, and stale local-reference rejection. Tests establish demonstrated behavior, not formal whole-language proof. No live jobs or workers created by this note.

## Implementation begun — syntax and first public execution contracts

Bill returned and authorized personal implementation with explanations. No worker,
installation, restart or push authority implied. First fixtures in
`graders/ch13/relationships.py` exercise ordinary owning-list parent cascade and
child detachment, with final hooks asserting memberships still exist before
teardown. Concrete constructors do not write managed links. Public syntax cases
are in `graders/ch13/relationship-syntax.json`.

Baseline3041: both execution fixtures rejected participant role syntax with
LYC201; 0/2 passed. Parser/Common/PEG then added contextual parent/child parameters,
participant add/remove functions, policy-selected DestructorFragment nodes and a
Prepare statement boundary. An unverified Peek helper in the edit was corrected
to existing token access before the first build; no failed build was claimed.
Job3046 terminal0: fresh Debug build zero warnings/errors, new syntax6/6 and
retained Chapter12 syntax16/16. Job3047 terminal0: PEG188 rules, six new fixtures
and corruption controls passed; retained parser155/155 and trees70/70 passed.
Job3048 terminal1: execution now reaches LYC306 unsupported TypeParameter, still
0/2. No semantic relationship or runtime teardown success yet; no Release or
cumulative acceptance claim. All these jobs are terminal, input freeze lifted.

Next: relationship policy/participant roles and selected fragment declarations
in the existing multi-class ownership model, then checking/IR lifecycle integration.
LowerDestruction already emits final, field releases, and reclamation in IR;
insert phase-ordered fragment calls there rather than hiding teardown in C.
Preserve transactional contribution admission and full reciprocal retirement.


### Binding policy and endpoint roles — measured semantic admission

The instance now stores immutable `RelationshipPolicy` (`None`, `Owns`, `Refs`)
and includes it in matching. `ParticipantRoleAt` reads the template's ordered
parameter declaration, not concrete class identity or conventional parameter
order. Constructor validation precedes all attachments: exactly one parent and
one child for a relationship, explicit policy, and no counted child under `Owns`.
Ordinary multi-class bindings retain policy-free behavior. Source diagnostics
reject malformed roles with LYC303 and incompatible policy/ownership with LYC304.

Public Chapter 13 baseline job3059: 0/2 executions and 0/3 intended rejections;
all stopped at unsupported role syntax. After semantic admission, job3064 built
Debug with zero warnings/errors and passed 3/3 intended rejections. Executions
remain 0/2, now explicitly blocked at unsupported DestructorFragment. This is
not executable relationship delivery.

Model probe job3068 built but failed before model checks: adjacent participant
class declarations in the new synthetic source lacked separators. Corrected
fixture job3069 built cleanly and passed 500 model checks, including reversed
role order, refused ownership leaving labels untouched, counted Refs admission,
policy matching and endpoint-preserving retirement. Disposable SDK-only project
at /tmp/cadence-ch13-model used cleared package sources; the compiler entry point
was not replaced. Retained Chapter 12 job3071 passed 18/18 executions and 11/11
rejections against the normal Debug compiler. No Release or cumulative run yet.

An ambiguous Check.cs edit was refused before write (job3062); narrowing to the
two multi-class functions corrected it. Preserve distinction between build,
model evidence and actual teardown. Next: selected fragments as normal owned,
checked function symbols excluded from source name lookup; borrowed teardown
receiver, ordered generated calls, managed initialization and prepare semantics.
No separate teardown registry or backend-only lifecycle shortcut is intended.


### Selected fragment execution — bounded milestone

Destructor fragments now have ordinary single-self signatures, owned function
symbols/scopes, contributor backlinks, and checked IR bodies. They are excluded
from ordinary and participant name lookup, not removed from ownership inventories.
Scope admission receives the prospective declaration because declaration attachment
requires an already-attached scope; no duplicate persistent hidden flag was added.
Only the binding policy's fragment becomes a concrete callable. Generated
destruction calls child-role fragments after finalization and parent-role fragments
after all child-role fragments, before ordinary field cleanup/reclamation.
Fragment effects join the existing destruction-effect fixed point. Receiver borrowing
is recognized consistently in checking, lowering, and ownership validation; this
is not a lifetime exemption. Managed membership storage/prepare are still pending.

Public phase-order baseline job3085 printed Fdone instead of FCPdone. After actual
IR calls, jobs3086/3090 pass that execution contract. Two list contracts still fail
on unsupported add Modifier: final totals3090 are 1/3 execution, 7/7 intended
rejections, clean Debug build. New compile-only tests cover hidden fragment calls,
duplicate policies, missing roles, and destruction effects reaching saved handles.
The first effect fixture used unsupported participant-alias construction C();
corrected to concrete Other() so it reaches the intended LYC330 check.

The hidden-call negative exposed existing label.destroy() being treated as built-in
object destruction before member lookup. Restricting that branch to object receivers
keeps labels as member placement rather than alternate object values. No invalid
program was executed. Lesson: forwarding a namespace receiver must preserve member
selection, not implicitly grant object operations.

Job3091 rebuilt the isolated model probe:500 checks passed; retained Chapter12
18/18 executions and11/11 rejections passed. Its final attempted syntax command
failed because syntax.py does not exist; corrected via the documented shared
recognizer rather than treating the shell failure as a test pass. No Release or
full cumulative claim. No commit, push, or worker.


## Preparation boundary and detached-storage checkpoint

Template validation now checks add/remove role declarations and the mandatory
first prepare block, forbids a second/nested prepare and any return in structural
syntax. These checks run even for unused templates. Baseline 3098 accepted all
five malformed declarations; 3099 clean Debug build changed them to intended
LYC303 rejections. This does not yet enable add/remove execution or prove that
preparation leaves logical membership unchanged.

SemanticSymbol.IsManagedLink derives policy from the contributing instance and
walks stored canonical types through arrays/tuples/structs to identify object
handles; it does not follow object fields beyond a class handle or store another
ownership flag. WritableRoot restricts assignment and writable-reference
acquisition to functions contributed by the same instance. Ordinary helpers are
privileged too. Constructors mark only nullable scalar managed fields initialized:
allocation already writes UINT64_MAX to scalar handle slots, including reuse.
Nonnullable cleanup-safe storage remains uninitialized at the language level.
Container/aggregate accounting and access escape paths still need full coverage.

Baseline 3109 rejected the new detached-storage case at LYC328 and stopped both
access negatives at that earlier error. Build 3110 failed because local Stack and
HashSet types lacked imports; explicitly qualifying them fixed the build. 3111
built Debug with zero warnings/errors. Final focused 3112: 2/4 execution cases,
15/15 intended compiler rejections. The detached case includes an unannotated
helper writing its link; the nonnullable negative still requires initialization.
Both list cases remain LYC306 at the add modifier. No full relationship success.
Retained Chapter12 runner 3114:18/18 executions,11/11 rejections, exit0. No new
Release/cumulative/model-probe claim in this slice.

Next: integrate prepare with a shared lexical scope and cleanup frame rather than
ordinary nested-block expiration. CheckBlock currently creates a scope, borrow
BuildBlock unconditionally creates/cleans/pops a frame, and IR Statement has no
Prepare case. Coordinate all three and preserve the explicit accounting boundary.
Managed preparation mutations (including effects through helpers), membership
retain/release, same-class teardown receiver validity, persistent ordinary-field
closure, and the two actual list execution cases remain unfinished.


## First ordinary owning-list execution and preparation lifetime

CheckBlock now shares its surrounding scope/assignment facts only for the initial
prepare phase of an annotated relationship operation. Borrow BuildBlock likewise
shares its cleanup frame; lowering schedules preparation statements into the same
IR region without an End or early cleanup. Ordinary nested blocks are unchanged.
The public lifetime fixture creates a counted local in prepare, uses it afterward,
and requires livebodydropdone: body execution precedes its final release.

Ordinary-child add/remove modifiers are admitted; counted-child operations remain
explicitly unsupported pending membership retain/release. This is temporary
incompleteness, not the final refs-counted contract. Policy validation still rejects
owns-counted. No broad teardown receiver or lifetime exemption was added.

After borrow-effect stabilization, temporary managed-writer summaries follow
resolved helper calls and known destruction effects to reject direct/helper
managed writes or writable-link acquisition during prepare. Traversal skips typeof
and unselected matchtype arms. This is not algorithm verification. Dedicated
cleanup-triggered effect, aggregate escape, operation signature, and broader
cascade tests remain needed before this safety boundary is complete.

Evidence:3121 new lifetime baseline stopped at add Modifier,2/5 executions and
15/15 rejects.3122 build failed on two incorrect API names (RelationshipRole and
BodyOf); corrected to actual ParticipantRole and declaration-child identity.
3124 clean Debug then5/5 executions,15/15 rejects: both original list cases execute,
parent final sees membership before child cleanup, child final precedes detachment,
and surviving parent endpoints update.3128 added direct/helper preparation negatives
were wrongly accepted:5/5 executions,15/17 rejects.3130 clean Debug and5/5 execution,
17/17 intended rejections after summary implementation.3132 terminal0 retained
Chapter12 multi-class18/18 executions11/11 rejects; Chapter10 struct-values34/34
executions39/39 rejects. No fresh Release/full cumulative/model-probe claim.

Next: challenge cleanup-mediated preparation mutation, then operation signatures
and refs-counted membership accounting; preserve full managed-field/aggregate
closure and cascade/receiver obligations. Passing ordinary list examples is an
executable milestone, not full Chapter13 safety or completion. No workers,
commits, pushes, installations, or user swap/scratch changes in this slice.

### Preparation cleanup events

The nested-cleanup negative was initially accepted (3139). A counted local in a
nested preparation block can invoke a selected destructor fragment that writes a
managed link without an explicit source call. The final borrow diagnostic pass
now checks its actual scheduled destruction events against the stabilized
managed-writer summary. Syntax ancestry distinguishes preparation cleanup from
cleanup of preparation locals that survive into the structural body. This reuses
occupied-holder analysis rather than inferring release from declarations.

Inspection also found and corrected an accidental conditional pop of a while
condition cleanup frame: that frame always ends, even when the containing
preparation shares its enclosing block's frame. Build3143 was clean (Debug,
zero warnings/errors), with5 executions/18 rejections passing. A positive
companion adds a preparation loop and defers a managed-writing fragment until
after the body. Its first fixture used immutable `let` for the loop counter and
failed LYC308; corrected `var` fixture3145 passes6/6 executions and18/18 intended
rejections. No Release or cumulative pass is claimed for this change.

Counted membership accounting, operation signature restrictions, persistent
ordinary-reference closure, aggregate escape checks and cascade coverage remain
unfinished. These checks establish the tested phase boundary, not a proof of
trusted relationship algorithms.

### Relationship operation signatures

The public specification already requires parent `self`, one child-role value,
and a unit result. Signature checking now enforces endpoint views as well as
canonical types, including when the same concrete class fills both roles.
Five new compile-only cases cover missing/extra parameters, nullable child,
parent-view substitution, and non-unit result. Baseline3155 rejected none with
the required diagnostic (18/23 negatives); Debug build3160 is clean and passes
6/6 executions and23/23 intended rejections. No Release/cumulative claim.

IR accounting inspection confirms explicit Ref/Unref and ownership-transfer
Move; aggregate Copy/drop operations recursively handle owned contents.
Relationship membership accounting is still pending: its retain must survive
operation-local cleanup, while structural links must not each own a count.

### Counted membership: public baseline and IR accounting inspection

After signature checks passed (six executions/twenty-three intended rejections), added `refs-counted-membership-lifetime`: the local child holder expires while attached, membership keeps it live, then removal must clear links before finalization (`liveNdone`). Unchanged Debug compiler job3172 exits1: six/seven executions, twenty-three/twenty-three rejections; new case stops at the intentional unsupported counted-operation modifier gate. Added independent-holder companion (`heldNdone`); not yet run.

Current IR uses Ref to create an owning holder, Unref to empty/release one, Move for ownership transfer, and recursive aggregate copy/drop operations. Membership accounting is not implemented. Inspection confirms Store emission itself is plain assignment; Lowering.Store currently transfers into/release-replaces all counted destinations. Generated destruction similarly releases counted fields. Managed structural links must not individually own counts; reads into ordinary holders still must retain. Do not evade Move's single-consuming-use validator with an intentionally leaked temporary. Proposed explicit membership retain/release operations can derive the selected instance through the owning IR function's declaration/contributing participant, avoiding duplicated instance state. Insert retain only after successful preparation and release after structural completion before ordinary parameter cleanup. Real effect analysis and validator contracts must accompany lowering. Current scalar ordinary-parent/counted-child fixtures do not establish counted-parent, aggregate/container, multiple-membership, or cascade support.


### Scalar counted membership IR and execution

Added explicit result-free `MembershipRetain`/`MembershipRelease` IR operations.
The function contributor identifies the relationship; the operand is its actual
child parameter slot, not a duplicated instance identifier. Retain follows
successful preparation and precedes structural writes. Release follows structural
completion and precedes ordinary parameter cleanup. Runtime release empties its
holder, so emission passes a private temporary to preserve the parameter's separate
owning reference. Existing parameter cleanup conservatively exports potential child
destruction to callers; membership release cannot finalize while that holder lives.

`IsManagedHandle()` derives scalar managed storage from the existing relationship
and type. Such stores do not transfer/release per-field ownership; generated field
cleanup and its effect closure omit those structural slots. Ordinary value reads,
locals, arguments, results, and nonmanaged counted fields retain normal ownership.
Arrays/aggregates and writable aliases inside trusted implementations still require
specific handling; the scalar exception is not a claim they are complete.

Baseline3188: 6/8 execution and23/23 rejection, both counted cases deliberately
blocked. First implementation3192 built but the emitter admission table omitted the
new vocabulary: both counted cases crashed before execution, not safe rejections.
Corrected admission3194: 8/8 execution and23/23 rejection. IR validation now checks
operation/child/policy identity, outer-body phase origins, and missing/duplicate
accounting. Real lowered-graph corruption controls remove/duplicate each operation;
probe3199 passes233 checks in BOTH configurations with clean builds. This checks
cardinality and declared boundaries, not arbitrary trusted algorithm correctness.

Final focused3201 terminal0: fresh Debug/Release builds zero warnings/errors;
Chapter13 EACH8/8 executions23/23 intended rejections (C O0/O2 through public
harness); retained Debug Chapter12 multi-class18/18 executions11/11 rejections and
struct-values34/34 executions39/39 rejections. Report was clipped in the middle;
terminal exit and all numerical summaries were visible. No cumulative acceptance,
SoA-specific run, or complete relationship safety claim. No commit/push/worker.
Next: parent refs-teardown with retained counted child, independent memberships,
malformed IR phase-order controls, and unresolved storage/access/lifetime cases.

### Counted-child lifecycle coverage after scalar accounting

Public contracts now include parent teardown with an independently held counted
child and two independently labeled memberships. Debug job3207 passed9/9
executions and23/23 rejections. Initial two-membership job3209 passed9/10 and
23/23: observed `helddoneN`, expected `heldNdone`. Emitted C inspection3212
showed assertion reads retaining ordinary counted temporaries until main block
cleanup; this was not evidence of an extra membership reference. Nested the
observation reads in their own block to finish these holders before the last
removal, preserving the original expected output. Job3214 terminal0 then passed
10/10 executions and23/23 rejections at both harness optimization levels.
No compiler changes or new Release/cumulative claim in this slice.

Lesson: a nonowning managed storage slot does not make ordinary values read from
it nonowning. Lifecycle tests must isolate membership ownership from observation
holders. Counted-parent zero-count teardown, indirect managed stores, aggregate
managed storage, and broader cascades remain unfinished.

### Counted-parent teardown exposes receiver-contract boundary

Added retained positive refs-counted-parent-zero-count-teardown: counted Graph
loses its last holder while attached to an independently held counted Node.
Expected GheldNdone. Job3217 terminal1:10/11 execution,23/23 rejection; new case
fails checked LYR009 invalid object retain in destroy -> Graph.destroy -> main.
No compiler modification or weakening of runtime checks.

Source inspection: IR/Lowering.cs272-278 excludes fragment self from owning
cleanup, but ClassCall971-1017 evaluates a normal receiver expression and
transfers it for every nonconstructor call. Thus self.remove from a fragment
crosses back into the ordinary owning-receiver convention. This confirms one
broken boundary, not exhaustive diagnosis of all retains in removal: identity
comparisons, managed backlink reads and helper calls need coherent borrowing
rules too. Need agree on borrowed maintenance-call contract before changing
shared IR/call/lifetime rules; never permit resurrection at zero count or weaken
checked retain globally. Current suite deliberately failing, no new build,
no jobs/children, no commit/push.

### Human correction: lifecycle invariants are debug-only

Bill requires speed, explicit debug-only state metadata and checks, and debug
mode that accepts legitimate teardown activity. Actual retain checked live-state,
not count zero; earlier chat conflated them and was corrected. Agreed direction:
eliminate unnecessary destructor-self ownership operations in IR, including
matching callee cleanup, rather than add a production destruction hold/state.
No permission to suppress child membership release or ordinary ownership globally.

Added public debug_mode.py: baseline3234 failed0/4 as expected (state emitted
by default; --debug rejected). Driver now accepts --emit-c --debug; Common
EmitC/EmitIrC signatures pass debugChecks explicitly. Emission gates state enum,
slot field, transitions, invalid retain/release/destroy checks and count-underflow
assertion. Count overflow remains checked; language null/bounds checks unchanged.
Job3236 terminal0: clean Debug build,4/4 mode/layout contracts, O0/O2 executions,
whitespace clean. No new Release/cumulative result.

DO NOT execute known counted-parent case without debug until correction: removing
a diagnostic guard alone does not resolve ownership. Inspected actual graph3239/
3240 without executing: Graph fragment Ref18->Move25->Call26(remove), remove
self Local1 with Unref97; remove Ref5(parent link) and Ref6(self) feed Binary7
identity equality and cleanup only. Need mandatory nonescaping observation and
call ownership simplification, preserving ordinary caller holders and other child
counts. Current one source method/one IR body convention requires care; no second
runtime dispatch or guessed emission-level ownership.

### Mandatory receiver ownership simplification verified

Bill proposed eliminating destructor-self retains/releases in IR rather than
adding a destruction hold. Implemented IR/Ownership.cs: identity-only Ref uses
become Load when all real uses are observations with no intervening effects;
matching Unrefs are removed. Infer ObservesReceiver only for a counted declared
self whose uses are nonescaping observations. Remove its parameter Unref; callers
keep the existing receiver holder through the call and release it immediately
afterward instead of transferring it. A borrowed destructor-self temporary can
then lose its redundant pair. Iterate to cover ordinary helper chains. Field
reads used as receivers are NOT generally stripped of ownership across calls.
Escaping/unknown patterns retain their original convention. No runtime dispatch,
extra production state or destruction hold was introduced. No general90% savings
claim, recursive receiver-call groups and broader effects remain conservative.

Common declares the inferred IR contract, BorrowsReceiver, and atomic ordered
node replacement/insertion and operand retargeting. Retarget reserves reverse-use
capacity before detach. Source syntax/semantic identities unchanged. Mandatory
pass runs after initial validation, then all three validators rerun. Publication
revalidates observing receiver nonescape/no-cleanup and call transfer contracts.
Inspection exposes borrowedReceiver. Probe challenges restored receiver Unref,
false borrowed flag on returning-self method, and removed flag with borrowed call.

Build mistakes corrected before tests: nonexistent SemanticTypes.IsCounted was
replaced with actual nominal IsCountedClass; per-node Require was not available
at function-level validation, so receiver contract uses its own local helper.
Added explicit declared-instance guard after source inspection (not arbitrary
first parameter). Scoped methods whose receiver escapes remain owning.

3250 direct debug case1/1;3256 direct/helper debug2/2;3257 focused12/12 execution
23/23 rejection in default and debug modes, debug-layout4/4, old IR233/config.
3264 final expanded IR probe239/config, clean Debug/Release builds.3266 terminal0:
fresh normal Debug/Release clean builds; each Ch13 default12/12+23/23 and
debug-layout4/4; retained Debug ownership92/92 (including corruption selfchecks),
struct-values34/34+39/39, multi-class18/18+11/11. Report clipped; read saved output
lines75–280 to recover actual92/92 summary.3269 public --runtime-debug command
12/12+23/23 plus public observing/escaping IR assertion and debug-layout4/4.
No full cumulative acceptance; no workers/commit/push/install.

Lesson: observation of a valid teardown receiver is not new ownership. Correctness
simplification must reconcile both sides of calls and matching cleanup, not hide
a runtime assertion or delete an unmatched retain. Debug metadata is diagnostic,
not production lifetime authority. Counted parent/helper cases now pass; earlier
DO NOT RUN note is superseded for these verified cases. Aggregate managed storage,
indirect writes, persistent ordinary-field restriction and broader cascades remain
Chapter13 work; this is not complete relationship memory-safety evidence.

### Cross-membership ordinary cascade and active receiver lifetime

Added public relationship-cross-membership-cascade: Graph owns A/B, A owns B
through a second same-class labeled binding. Expected GABdone; finals observe
memberships before their own detach, Graph rereads first after A destroys B.
Baseline3279 rejected LYC330 at fragment while self.first, no execution. Byte
inspection3280 confirmed span despite copied-source diagnostic coordinates.

Check.cs ApplyClassEffect invalidated every ordinary handle of a class. Added
transient teardownReceivers identities for the active fragment parameter and
fresh direct Name observations of that exact semantic parameter. These alone
survive class-wide object invalidation; field storage/payload invalidation is
unchanged, saved children are not protected. No persistent heap graph, alias
resurrection, or broad class exemption. Explicit fragment self.destroy now rejects
LYC334 after stripping group/namespace syntax. Trusted algorithm correctness still
required; this is not a proof of all indirect reentrant attempts.

Public controls: direct self.destroy baseline3290 wrongly accepted; saved child
already rejected LYC330. Added saved child field ref after destroy requiring
LYC326. No invalid program executed.3291 clean Debug build;3292 targeted1/1+2/2.
3295 terminal0 full Debug Ch13 default/debug13/13 executions26/26 rejections and
retained Chapter8 checking154/154; report clipped middle but totals visible.
3296 terminal0 fresh clean Release plus new cascade1/1 and controls3/3 in default
AoS, debug AoS, debug SoA, each execution harness O0/O2; whitespace clean.
No full cumulative or whole Release-suite rerun in this slice.

Lesson: same nominal class does not mean same object lifetime. Active teardown
receiver storage survives child cascades; saved children/interior references do
not acquire that guarantee. Next authorized slice: public test for indirect
managed-link stores via local writable alias; scalar nonowning policy currently
recognized only for direct SelectField. Keep helper-parameter and aggregate
coverage explicitly unfinished. No live jobs/children/freezes; no commit/push.

### Last-release destruction hold: decision before implementation

Bill proposed preserving the last reference during destruction rather than
requiring IR simplification to remove every destructor-self retain/release. His
final refinement keeps decrement-and-zero-test on the common release path, then
restores count one only on the zero branch before invoking destruction. The
releasing holder is emptied first. Balanced temporary ownership during cleanup
may take the count 1 -> 2 -> 1; reclaim frees directly, not via another release.
Debug checks allow destroying-state retain/release, reject reentrant destructor
entry, and require count one at reclamation. No production lifecycle flag is
added. Overflow remains checked. Escaping references are not authorized.

This supersedes optimization as the correctness prerequisite for counted-self
teardown; receiver inference and pair elimination remain useful optimizations.
The earlier pre-decrement count-one proposal was superseded by this fast-path
refinement. Performance benefit is unmeasured. Planned public test calls the
owning, self-returning keep method from final so real temporary ownership must
work. At this record point the change and test have not yet passed.

Implementation and evidence: release still empties the holder, decrements, and
checks zero. Only that branch restores references=1 before destroy. Retain/release
debug guards now permit live or destroying slots, while destroy still requires
live state. ReclaimObject checks references==1 in debug mode before returning the
slot to the free list. It reselects metadata by index after cleanup; never reuse a
slot pointer across user code that can allocate and relocate the metadata array.
No production state flag or final zero write was added. Overflow stays checked.

3302 failed build: diagnostic Location received IrNode instead of its SyntaxNode
Origin.3303 broad exact-edit assertion refused without changes;3304 narrowed fix
built cleanly. Proposed final calling self.keep was rejected LYC334 by existing
own-class final-effect restriction BEFORE execution. Did not weaken that rule.
Reverted that fixture and used explicit public generated-runtime instrumentation:
insert a balanced real retain/release in emitted Box destruction body, assert
entry/exit count one and cleared temporary holder. This is runtime-boundary
coverage, not proof that that source-level final call is admitted. Remove hold
restoration in disposable C and require exact exit91 before any recursive release;
this control demonstrates detection of the missing hold without unsafe execution.

3305 terminal0: Debug public runtime contracts4/4, relationship debug executions
13/13 and intended rejects26/26.3306 terminal0: expanded Debug runtime contracts
4/4 including hold probe and missing-hold control, both layouts, default/debug
runtime, O0/O2.3307 terminal0: clean Release build0 warnings/errors; same expanded
runtime4/4 and runtime-debug relationships13/13+26/26. No full cumulative rerun,
performance benchmark, new worker, commit, push, installation or restart.

Lesson: development exposed a runtime lifecycle obligation that optimization
should not carry. Keep decrement-and-zero-test common path; restore one reference
on cold destruction path, allow balanced temporary holders, reclaim directly.
IR ownership simplification remains useful but is not the runtime hold mechanism.
Source effect restrictions/escape safety remain separate obligations. Chapter13
is unfinished; next slice remains managed writable aliases and container coverage.


## Local managed-link address aliases

A local `ref` to a scalar managed link must preserve the storage's nonowning
policy. Lowering previously recognized only direct SelectField destinations;
Borrow/AddressOf aliases incorrectly used ordinary owning transfer and release.
The first test aliased both insertion and removal and passed (3312): the erroneous
retain/release balanced. Changing only insertion exposed the defect (3314):
`livedone` instead of `liveNdone`, because the extra owner prevented finalization.
This is why lifecycle contracts need asymmetric paths, not only round trips.

IrNode.IsManagedHandleStorage now follows local Borrow/AddressOf inputs back to
the actual field declaration. It derives policy rather than duplicating mutable
state. Missing parameter provenance and cyclic/malformed paths are not inferred
as managed. Lowering and both ownership-validator directions share this query;
the C emitter still merely executes explicit ownership IR. Reference parameters
and aggregate managed storage remain separate unfinished contracts, not certified
by this scalar local-alias correction.

3316: clean Debug build, corrected focused execution passed. 3317: all16
relationship executions and26 intended rejections passed with runtime debug;
retained ownership IR92/92 passed. Public cases cover alias insertion/direct
removal, direct insertion/alias removal, and chained local aliases. No full
cumulative or performance claim; no commit or publication.

3318 terminal0: fresh Release build0 warnings/errors; all3 local-alias cases
passed runtime-debug AoS and SoA, each at O0/O2.3319 readback and whitespace
check passed. This is focused verification, not the full Release/cumulative suite.

### Open decision: managed storage through reference parameters

3321 compile-only probe: an in-relationship helper
`write_link(self, slot: ref C?, child: C) { slot = child }` called with
`ref self.first` is accepted by --inspect-ir (exit0, empty stderr). It was NOT
executed. The local provenance walk ends at the parameter; the same signature
can receive ordinary owning storage, so treating every such parameter as
nonowning would be incorrect. Existing acquisition privilege alone does not
supply the callee's ownership contract.

Recommendation raised with Bill, not implemented: reject managed counted-link
storage passed through ordinary reference parameters for this increment. Helpers
can instead receive participant objects and select their managed fields directly;
local aliases remain supported. Supporting both policies through a parameter
requires an explicit specialization/calling-contract decision. Do not silently
introduce this restriction or claim this unresolved path safe. No live jobs.

### Debug use-after-free coverage clarification

Bill asked whether runtime debug already catches use-after-free. Source inspection
3323/3324 and bounded FieldStorage read show lifecycle guards on retain/release/
destroy, but SelectField only saves the index and FieldStorage computes an address
without checking slot state. Therefore ordinary nonowning field access is not
covered by a general debug use-after-free check. This is source evidence, not an
executed invalid program. Slot reuse would additionally defeat a free/live check;
full stale-handle detection requires identity beyond the reused index. Do not
claim existing --debug guarantees general use-after-free detection. No such new
instrumentation implemented in this clarification.


## Execution-mode trust boundaries clarified with Bill

Bill's formulation: unsafe trusts user code; normal safe trusts compiler/runtime
and relationship-generated code; debug seeks bugs even in those trusted layers.
Created chapter-13.md as an explicitly incomplete construction draft with a debug
section. Required object bounds/lifetime/UAF checks are debug-only, including slot
reuse and borrowed access. Safe array bounds (also object arrays) and node! checks
remain; unsafe omits them. This explicitly supersedes the earlier requirement
that unsafe nullable unwrap still fail with LYR013. Compiler/Chapter12 graders
have NOT yet changed, and prior passes do not verify this new contract.

No invariant instrumentation is to be automatically generated in normal safe
output. Resource-exhaustion guards and combined --debug/--unsafe behavior remain
explicit unresolved details, not silent removals. Debug detection is a requirement,
not an assertion of complete coverage or proof. Documentation changes only here.


## Introductory safety prose corrected

Bill requested correction after the stale introduction was identified (said
Chapter10; the identified text was actually Chapter0). Corrected Chapter0's
owns-counted/refs-zero claims: owns accepts ordinary children; refs on counted
children contributes one owner per attachment, not one per structural field.
Defined membership concretely and reconciled the old participant-local counting
paragraph with the newly agreed trusted structural borrowing boundary. Chapter10
search matches concerned compiler model/borrow relationships, not this obsolete
attachment rule, so no Chapter10 edit was made.

Recommendation to Bill: retain single-threaded safe-mode memory safety as the
Chapter13 deliverable. Close persistent ordinary-reference escape routes, complete
participant borrowing/owning boundaries and teardown effects; do not defer these
while claiming maintained relationships are safe. Debug lifetime instrumentation
is a separate diagnostic mechanism, not what makes the conditional safe-mode
guarantee true. No implementation/test result is implied by these prose edits.


## Safe versus unsafe nullable unwrap implemented

Bill authorized proceeding toward single-threaded memory safety with the agreed
mode/trust and relationship changes. Grader updated first:3337 rejected the old
emitter's unconditional LYR013 branch.3338 clean Debug build0 warnings/errors:
11/11 safe nullable executions (including3 intended checked failures),10/10
compiler rejections,3/3 unsafe emission-only omission controls,8/8 valid unsafe
executions,4/4 existing debug-mode contracts. Invalid unsafe cases were NOT run.
Emitter gates only the null-test branch on !unsafeMode; operand evaluation and
explicit ownership IR remain unchanged. Chapter12 teaching/public grader guide
updated together; this is an authorized contract change, not weakening a failure
to manufacture a pass. Historical accepted evidence remains historical.

Lesson: inspect omitted unsafe checks without executing invalid unsafe inputs;
run valid inputs separately to preserve semantics. Debug UAF instrumentation,
participant borrowing and persistent ordinary-reference closure remain unfinished.

3339 terminal0: clean Release build0 warnings/errors, nullable11/11 safe
executions10/10 rejections,3/3 emission-only unsafe omission controls,8/8 valid
unsafe executions; whitespace clean. No invalid unsafe execution. No live jobs.
Next: participant borrowing through helpers and owning boundaries. LowerFunction
currently exempts only ctor/final/fragment self; ordinary participant parameters
remain owning. Changing managed stores alone cannot implement the newly agreed
rule. No broader ownership patch has been applied yet.


## Writable participant helper slots: bounded ownership correction

Continuing Bill's authorized single-threaded safety work, inspected actual
participant occurrences and IR lowering/validation. Public helper insertion with
direct removal failed3345: livedone rather than liveNdone, showing an extra
counted owner (not an executed dangling-access probe). IsManagedHandleStorage
now recognizes operand-free writable parameter Borrow nodes whose declared
ParticipantView belongs to the enclosing function's policy relationship instance.
Uses existing semantic identities; no new persistent flag or class-name inference.
Local alias traversal still applies. Both store validators and lowering already
consume this common query.3348 clean Debug build and focused1/1 pass.

Added unrelated Held reference-parameter control: its assigned local survives an
inner scope and finalizes at outer scope exit.3350 terminal0 clean Debug build,
18/18 relationship executions and26/26 rejections with runtime debug enabled.
This demonstrates scalar writable helper slots, NOT the whole participant-borrowing
contract: participant value parameters/locals and owning call/return boundaries
remain unfinished. No general UAF/debug coverage or chapter completion claimed.
Lesson: asymmetric helper insertion/direct removal exposes extra reference counts;
endpoint identity permits borrowed participant slots without disabling ordinary
ownership for unrelated helper values. No workers/commits/push/install.

3351 terminal0: clean Release0 warnings/errors; both new helper controls2/2
in AoS and2/2 in SoA, runtime debug enabled, harness O0/O2. Whitespace clean.
No full cumulative rerun; no live jobs. Next: participant value/local borrowing
and ordinary ownership boundaries, then persistent ordinary-reference closure.

## Participant return boundary baseline

Before changing participant value parameters and locals to borrowing, added public
`refs-counted-return-to-application`: a relationship helper returns its first child,
the ordinary caller removes the attachment, uses the returned value, and observes
one finalizer at holder scope exit (`heldNdone`). Job 3357 passed 1/1 on the existing
Debug assembly with runtime debug, through the harness O0/O2 checks. No compiler
change or broader rerun in this slice; nineteen public execution fixtures now exist,
with the prior eighteen-suite result and this separate baseline, not a fresh 19/19.

Job 3356 was NOT verification: an ambiguous edit anchor matched NEGATIVE_CASES too,
refused before writing, then a non-fail-fast shell ran an empty selection (0/0).
Corrected with a newline-bounded anchor, fail-fast shell, and exact-one selection
assertion. Lesson: zero selected tests and shell exit zero do not establish coverage.

Inspection confirms LowerFunction registers counted parameters/locals as owners,
ClassCall and general calls transfer value arguments, and End transfers returns.
Changing only callee cleanup is insufficient. Preserve the owning application
return boundary while representing borrowed participant parameters/local values
consistently in lowering and both ownership-validator directions. No such broader
change has been made yet. Corrected README's stale assertion that receiver
optimization is necessary for destructor correctness: the implemented count-one
destruction hold provides correctness independently.

## Borrowed participant value parameters: call-boundary increment

Added public graders/ch13/participant_ir.py: inspect a real lowered refs-counted
list, require exactly one MembershipRelease of remove's child parameter and no
Unref/Move directly consuming that borrowed parameter. Baseline3367 exit1 detected
existing parameter ownership. No invalid program execution.

BorrowsParticipantParameter derives the rule from the function's contributing
relationship and the parameter's declared endpoint identity, same scope, compatible
scalar view and non-reference parameter. No class-name matching or duplicated flag.
Lowering omits callee ownership registration and outgoing Move for these arguments;
both Move-validator directions agree, and direct Unref of the borrowed parameter
is rejected. Existing attachment accounting remains explicit and unchanged.

First clean build3368 followed by3369 exposed timing regressions: new IR1/1 passed,
but executions13/19 and rejections26/26. Caller temporaries remained alive until
outer scope exit, delaying six finalizers. Corrected by releasing only registered
caller-owned argument temporaries immediately after the borrowed call, preserving
holders through argument effects/call and preserving the returned owning result.
Direct borrowed values are not released. Ordinary later cleanup sees empty holders.
3371 terminal0: clean Debug0 warnings/errors, IR1/1, relationships19/19 executions
and26/26 rejections with runtime debug, O0/O2 harness. Owning application return
and unrelated counted helper control preserved. No full cumulative claim.

Lesson: changing parameter ownership must preserve caller temporary lifetime, not
merely eliminate callee cleanup. This is parameter borrowing only: expression reads
and participant locals still create ordinary counted owners. Their removal, full
persistent ordinary-reference closure and debug UAF including slot reuse remain
unfinished. No claim of completed single-threaded safety; no workers/commit/push.

3372 terminal0: clean Release0 warnings/errors, participant IR1/1,
relationships19/19 executions26/26 rejections (runtime debug, O0/O2); diff whitespace
clean. No live jobs. Only a comment corrected afterward to describe borrowed
participant parameters accurately. Next: participant expression/local borrowing
and explicit ownership acquisition at ordinary call/return boundaries; preserve
current successful owning-return control and immediate caller temporary cleanup.

## Borrowed participant parameter reads

The first stronger IR check against remove alone passed unchanged (3378): existing
ownership simplification already removed its direct child Ref reads. Inspection3379
showed append still had them. Extended the public check to both operations;
3380 failed for the intended direct owning-read defect. This distinction matters:
a passing optimized example is not evidence all participant reads borrow.

Lowering now reads borrowed participant parameters with Load. Transfer explicitly
creates a Ref holder when a counted Load crosses an owning boundary, then Moves
that holder. No moving a borrowed observation or emitter-side ownership inference.
3381 terminal0: clean Debug build, zero warnings/errors, public participant IR1/1,
relationships19/19 executions and26/26 intended rejections with runtime debug and
O0/O2. Owning return to application and unrelated counted types remain covered.

This is still a bounded increment: nullable wrap/unwrap still create owning
holders, field reads and participant locals still need borrowing support. No claim
of count-free relationship code or completed single-thread safety. Lesson: borrowed
read and owning boundary are distinct; ownership must be acquired at the latter.
3382 terminal0: clean Release zero warnings/errors, participant IR1/1,
relationships19/19 executions26/26 rejections with runtime debug/O0/O2;
whitespace clean. No live jobs. Next: preserve borrowed ownership through nullable
conversions, then participant field reads/locals, with explicit ownership at
ordinary boundaries. No worker, commit, push, installation or broad rerun.

## Borrowed nullable conversions preserve ownership provenance

The stronger public IR check follows append's child Load rather than checking only
Ref(child). Baseline3388 exit1 detected an owning copy introduced by wrapping.
IsBorrowedHandleValue is derived from scalar counted Load plus a finite chain of
WrapNullable/UnwrapNullable operands; malformed/cyclic drafts return false. No
persistent ownership flag was added. Nullable lowering does not Transfer or
register cleanup for borrowed inputs. Transfer acquires an explicit Ref at an
owning boundary; validators reject Move/Unref of borrowed conversion results and
permit nonconsuming conversion operands. The emitter continues to follow IR.

3389 terminal0: clean Debug zero warnings/errors, then public IR1/1 and
relationships19/19 executions26/26 rejections with runtime-debug/O0/O2.
Extended the public inspection source with an unused nullable-parameter comparison
helper to check two borrowed unwraps without executing invalid input.3391 terminal0:
Debug IR2/2; nullable11/11 safe executions10/10 rejections,3/3 unsafe omission
inspections (never executed),8/8 valid unsafe executions. Clean Release zero
warnings/errors; IR2/2, relationships19/19 executions26/26 rejections.

Lesson: nullability and ownership are independent. A borrowed conversion must not
create a hidden owner; an owning return still explicitly retains. The existing
application-return and unrelated-counted-slot controls pass. Participant field
reads/locals, persistent ordinary-reference closure, and debug UAF (including slot
reuse) remain unfinished. No complete memory-safety, cumulative, or performance
claim. No workers, commits, pushes, installations. No live jobs after3391.

## Borrowed reads of this relationship's participant fields

Public IR baseline3395 exit1 detected Ref on append's selected structural fields.
Member lowering now selects Load only for a managed scalar field whose declared
participant view belongs to the enclosing function's relationship instance and
accepts the field type. No concrete-name/type guessing. Other relationship instances,
ordinary application reads and unrelated counted values retain normal ownership.
Owning boundaries still use Transfer to acquire a holder from a borrowed value.

3397 terminal0: clean Debug zero warnings/errors, participant IR2/2 (now also
checks structural field reads),19/19 relationship executions26/26 rejections with
runtime debug/O0/O2.3398 terminal0: clean Release, same2/2,19/19,26/26,
git diff --check clean. Application owning return and unrelated slot controls
preserved. No full cumulative test or completed memory-safety claim.

Lesson: borrowing permission follows relationship instance and participant role,
not merely concrete class identity. Next inspect and implement participant local
storage/assignment/cleanup together. Read-only discovery3399 located local lowering
around1235–1267 and Store around1392; not yet edited. Nullable and field changes
are verified separately above. No live jobs, workers, commits, pushes or installs.


## Scalar participant locals and alias reads

Public helper fixture3406 initially used an unsupported standalone block and
failed LYC306, not ownership. Corrected to if true;3408 exit1 then detected
owning participant-local operations. BorrowsParticipantLocal derives scalar view,
relationship policy/instance and owning function through lexical scope parents.
Lowering omits owning cleanup registration, uses borrowed reads, and shares
IsManagedHandleStorage for assignments (including aliases). Validation rejects
Move/Unref of borrowed local storage. No persistent ownership flag was added.

3409 terminal0 clean Debug, IR2/2 and19/19 executions26/26 rejections.
New executable local insertion fixture covers inference, explicit nullable view,
uninitialized local, nested assignment and writable alias.3411 terminal0 Debug
20/20+26/26; clean Release, IR2/2 and20/20+26/26. Alias-read inspection3412 then
failed: unwrap still consumed an owner. Name lowering now resolves storage once
and derives borrowed reads through the same authoritative address chain used for
stores. Executable fixture now reads the alias into first.

3413 terminal0: Debug and Release each clean build zero warnings/errors,
IR2/2,20/20 executions26/26 intended rejections, runtime-debug and C O0/O2;
whitespace clean. Owning application return and unrelated counted-slot controls
preserved. No full cumulative, aggregate borrowing, debug UAF or complete memory
safety claim. Chapter construction teaching and grader guide updated at this
boundary. No workers, commits, pushes or installs; no live jobs.

Lesson: borrowed local semantics require coordinated creation, reads, replacement
and cleanup. Removing only a final release is insufficient. Alias reads and writes
must use the same actual-storage provenance. Follow-up remains persistent ordinary
reference closure, aggregate managed storage, and debug UAF including slot reuse.


## Persistent ordinary-field admission — bounded implementation, migration pending

The settled spec section on persistent references already requires maintained
relationships, including container hiding; counted fields remain allowed. Added
a post-binding/pre-body physical-field check in Check.cs. It visits canonical
arrays/tuples/inline structs once, stops at class handles, permits counted handles,
and requires ordinary targets to be participants of the field's explicit relationship.
No runtime state or broad privilege exemption was added.

Baseline3422: scalar field accepted; tuple field reached emission and crashed in
CType (not a passing rejection); policy-free fixture initially hit constructor
initialization first. Removed its irrelevant constructor. After implementation,
3425 clean Debug build and20/20 executions29/29 rejects. Expanded3426 yielded
20/20 and31/32: struct-valued class field is already unsupported LYC306 rather than
reaching the new check. Renamed/documented that control to preserve this existing
boundary, not broaden class-field support. Nested arrays and unrelated relationship
class controls passed.3428 terminal0: clean Release0warnings/errors,20/20 executions
32/32 intended rejects, runtime debug with C O0/O2, whitespace clean. Final six-case
Debug rerun still owed; no full cumulative claim.

Migration audit3427 terminal1: unchanged Chapter12 multi_class14/18 execution,
10/11 rejection. Affected positives: participant-link-fields, nullable-role-chain,
list-transitions, independent-list-memberships. nullable-concrete-view negative
now reaches LYC304 before its old lookup diagnostic. Other Chapter12 runners not
rerun yet; object_fields and list_refusals also depend on temporary ordinary links.
Original fixtures/results remain intact. Need explicit Chapter13 successor coverage
for views, initialization and refusal ordering, not deletion/weakening to hide failures.

Lesson: local invalidation cannot repair a stale handle hidden in object storage;
check persistent storage admission separately. Rejecting an invalid aggregate before
emission does not prove valid aggregate class storage is implemented. Struct-valued
class fields remain unsupported, and general managed aggregate ownership/debug UAF
remain unfinished. No commit/push/worker/install or Chapter13 completion claim.

Final focused Debug3433 terminal0: all6/6 persistent admission rejections pass
against the same implementation; this supersedes the focused-rerun-owed note above.
No execution cases selected in that focused run, intentionally. All jobs terminal.

## Chapter 12 migration: lookup/refusal successors and nonnullable decision

Added public ch13/multi_class_migration.py without altering original Chapter12
fixtures. Four named ordinary-link examples now have maintained successors;
application main bodies and exact expected outputs are preserved. Old sources
also require LYC304. Concrete nullable annotation still must lose participant
view and produce LYC329 in the valid maintained successor. Other cases unchanged.
Debug3441 terminal0:18/18 executions15/15 rejections at C O0/O2.
New ch13/list_refusals.py preserves all four old call sequences, exact LYR010
message and method/main frames, calculating locations in the maintained source.
Debug3443 terminal0:4/4 intended runtime failures. Assertion termination does
not prove post-failure heap state. No Release or cumulative migration pass yet;
Makefile still invokes historical Chapter12 runners and is not migrated.

Object-field migration inspection found a contract boundary: CheckAssignment
calls WritableRoot before marking constructor assignment. Concrete constructor
cannot write relationship fields (LYC308); leaving nonnullable field uninitialized
is LYC328. Compile-only3448 terminal0 confirms LYC308 for Holder constructor
assigning its contributed item:C field. This is not an execution/safety proof.
Need Bill's decision before dropping old ordinary nonnullable-field positive or
adding constructor privilege. Recommendation: structural links nullable for
unattached state; nonnullable permanent attachments need an explicit lifecycle
contract rather than an ad hoc write exception. Counted ordinary fields remain
supported. Alias/helper detach successors and object-field/nullable runner audit
still unfinished. No compiler changes in this migration slice, no commits,
workers/pushes/installs, no live jobs. Lesson: preserve behavior checks in valid
successors; earlier rejection alone does not retain lookup/initialization tests.

## Decision: nonnullable fields must be initialized in the constructor

Bill requires all nonnullable fields initialized before constructor completion;
revisit only if a concrete need arises. Internal cleanup-safe null storage remains
unreadable until a valid write and does not satisfy definite initialization.
This supersedes the author's nullable-only recommendation above. No deferred
post-construction initialization tracking is authorized or needed for this rule.
Keep existing branch/return/fallthrough and read-before-write checks.

Separate unresolved implementation issue: concrete constructors currently fail
LYC308 when directly writing contributed managed links. Need a legitimate
relationship-controlled construction path, not a blanket write exemption or
removal of the nonnullable contract. No implementation change in this decision
capture. Chapter13 teaching now records the rule and this verification limit.
Handle representation changes are queued immediately after Chapter13: AoS C
pointers/NULL versus SoA u32 indices/zero with reserved slot0; neither implemented
by this decision. Lesson: initialization state, representation, and permission
to modify a relationship are independent requirements.

## Constructor decision applied to migration; focused checks complete

3460 terminal0: Release multi-class successors18/18 executions15/15 rejects,
refusal successors4/4; unchanged Debug nullable11/11 safe10/10 rejects,
3/3 unsafe omission inspections8/8 valid unsafe executions. No invalid unsafe run.
Inspection3463: existing constructor recognition is concrete-class-name based;
partial self access permits direct fields, not ordinary initialization helper calls.
Do not invent relationship construction hooks absent a concrete requirement.
Nonnullable relationship fields still must meet constructor and permission rules;
no nullable-only rule or post-construction initialization was introduced.

New object_fields_migration.py leaves old fixtures intact. Ordinary alias/helper
cases use maintained Link attach/detach, including destroy inside helper and
returned holder alias. Existing counted ownership cases unchanged. Nonnullable
field observation and original initialization/type diagnostics use legal counted
Box targets; original unmanaged sources separately require LYC304. This is an
explicit change of storage premise, not proof of nonnullable managed construction.
3465 terminal0 both configs10/10 executions19/19 rejects at C O0/O2,
whitespace clean. No compiler change or rebuild needed for these grader changes.
Current Make target now invokes the three Ch13 successors in place of old
multi_class/object_fields/list_refusals; nullable and syntax remain unchanged.
Full cumulative not rerun; focused passes do not certify complete Chapter13.

## Debug allocation identities: implemented and focused verification complete

Bill approved a debug-only allocation-identity table and explicitly accepted a
remaining gap in raw borrowed-address detection. Production representation stays
unchanged; AoS C pointers/NULL versus SoA u32/null0 remains queued after Ch13.

Compilation.cs now emits per-class append-only identity-to-slot tables only with
runtime --debug. Source handles carry identity; allocator free lists retain raw
slot indices. Allocate initializes a slot then publishes a fresh identity. Resolve
checks identity bounds before table access, tombstone/slot bounds/free state before
storage use. FieldStorage resolves on each selected-field use, not only selection;
address acquisition checks but subsequent raw-pointer dereferences do not.
Retain/release/destruction resolve handles to metadata. Destroying objects remain
accessible for final/fragment cleanup; repeated destroy still rejects. Reclaim
resolves again after cleanup, checks destruction hold, tombstones identity and
frees slot. No metadata pointer survives user cleanup. Identity exhaustion fails
rather than wraps. Tombstones cost debug memory proportional to total allocations
until process exit. No ownership IR or production handle change.

Public graders/ch13/debug_identity.py uses valid Lyric source plus deliberately
invalid C runtime-boundary injections ONLY in debug output. 3480 baseline4/12:
valid controls pass, stale reads before and after slot reuse incorrectly return0.
Initial exploratory source errors (var field syntax, numeric print) corrected;
not ownership/runtime failures. 3481 clean Debug build0 warnings/errors. 3482
runtime checks emitted correct LYR009 but expected column45 was wrong (actual44);
identity runner still failed, shell0 reflected following relationship runner,
NOT overall success. Relationship20/20 exec32/32 rejects passed. Corrected exact
location;3483 terminal0 identity12/12, debug-mode4/4. Existing destruction-hold
probe now resolves identity for metadata inspection; missing-hold defect control
preserved. 3485 expanded64/64 covers ordinary/count classes, AoS/SoA, O0/O2,
valid/free/reused/growth/null/out-of-range/stale-destroy and counted stale
retain/release. Finalizer field reads exercise permitted destroying state.

3490 terminal0: fresh Release build0 warnings/errors; BOTH Debug/Release identity
64/64 and debug-mode4/4 (including explicit absence of identity tables/resolver
in default output); Release relationships BOTH default and runtime-debug20/20
executions32/32 rejections. Inputs frozen throughout. git diff --check clean.
Make target now includes both debug runners. Full cumulative Make NOT run.
Chapter13 teaches construction/cost/limits; spec narrows debug borrowed-address
promise to agreed gap, without relaxing safe-language static lifetime rules.

No complete memory-safety or general UAF detector claim. No --unsafe+--debug
certification; resource-exhaustion behavior unchanged pending separate decision.
No workers, commits, pushes, installs or restarts. All jobs terminal.
Lesson: slot occupancy is not allocation identity; freeing/reusing storage must
never revive an old debug reference. Checks test trusted-runtime defects, not
permission for ordinary safe source programs to access stale objects.

## Aggregate participant boundary: three diagnostic failures, scope decision pending

After Bill authorized continuing the remaining Ch13 work, inspected scalar
BorrowsParticipantLocal/Parameter, IsManagedHandleStorage, NewArray/NewTuple
transfer and aggregate Copy/drop. Participant views currently describe scalar
nominal/nullable endpoints only. Aggregate ownership is ordinary recursive owning
storage; do not suppress all aggregate cleanup or infer roles from class equality.

3499 diagnostic public-harness probe reused refs-counted-membership-lifetime,
adding relationship helper remove_saved: let saved=(self.first!,self.first!);
self.remove(saved.0); print("after"). Expected borrowed-participant timing
liveNafterdone; actual liveafterNdone. Terminal1,0/1. No saved handle is read after
removal; this tests extra ownership, not invalid-memory execution.
3500 terminal1,0/2: equivalent array [self.first!,self.first!] / saved[0] and
explicitly annotated tuple saved:(C,C) both produce liveafterNdone instead of
liveNafterdone. Debug runtime instrumentation enabled. Harness stops each at first
mismatch, so no claim both optimization levels passed. No compiler edits this slice;
these are diagnostic probes, not yet retained fixture policy.

Decision pending: implement per-element participant/ownership information through
aggregate construction/copies/selections/calls/boundaries, or explicitly reject
participant-containing aggregates inside relationships for Ch13. Arrays still own
backing storage and unrelated counted elements still count; wholesale cleanup
suppression is invalid. Concrete class matching cannot distinguish roles/explicit
concrete ownership. Keep valid ordinary application containers supported.
No jobs live, no workers/commits/pushes. Lesson: aggregate storage ownership and
participant-element ownership are separate; scalar passing tests do not certify
aggregate borrowing. Do not claim these extra-reference timing probes establish
a use-after-free defect or full aggregate safety assessment.

## Decision: bounded Chapter13; Chapter14 patch and safety closure

Bill chose to defer aggregate participant borrowing to the next patch chapter,
alongside SoA u32/null0 and AoS pointer/NULL representations. First-edition patch
chapters are expected; do not expand Chapter13 merely to avoid one. This resolves
the scope question above. Chapter13 unsupported aggregate rejection is still
planned, not implemented at this record.

Bill explicitly defers the completed single-threaded memory-safety claim until
Chapter14 closes the remaining paths, assuming correct compiler/runtime and
bug-free relationship implementations generated from multi-class interfaces.
This excludes unsafe/FFI contract violations, leak freedom, concurrency and full
debug detection. The observed aggregate failures are extra ownership/delayed
finalization, not demonstrated memory violations. Accepted raw-address debug
coverage gap remains accepted.

Starting obligations, actual evidence, limits and proposed checks are saved in
[ch14-starting-plan.md](ch14-starting-plan.md) and linked from Cadence's queue.
That plan is not implementation, completed acceptance or authorization to start
Chapter14 before Chapter13 is finished. No new compiler/grader change in this
recording slice. Resume Chapter13's explicit aggregate boundary next.

## Scalar aggregate boundary implemented; cumulative verification running

3513 baseline:2/2 explicit concrete-owner executions pass;0/8 unsupported
participant aggregate rejections (all accepted). Shell0 from subsequent reads
was not a grader pass. Check.cs now rejects LYC304 while TypeView/checked facts
still identify participant endpoints: aggregate type leaves, tuple/array literal
elements, element assignment and push. Value restriction applies to the matching
maintained relationship; annotations to maintained participant aliases reject.
Explicit concrete locals and application aggregates retain normal ownership.
No aggregate cleanup/IR/emitter policy changed. This is a temporary Chapter13
admission boundary, not Chapter14's aggregate borrowing implementation.

3514 clean Debug build0warnings/errors;2/2+8/8. Expanded3515 terminal0:
4/4 executions11/11 rejections (includes parameter/result/field annotations and
application owning tuple/array), followed by relationship20/20exec32/32reject.
Runtime debug on; public harness O0/O2 positives. Full Release check pending in
cumulative target. Current tests and compiler frozen at admission3518.

Make now includes relationships both modes, participant_ir, participant_aggregates,
relationship syntax, and both debug runners, alongside inherited/migrated tests.
3518 RUNNING: actual make test with installed SDK. Debug clean build, front-end
82/0, Unicode99826NFC/all1112064scalars/7 malformed,58tokens70trees155recognition
7boundaries82semantics passed so far; execution suites underway. NOT cumulative
success. Keep job3518; do not edit compiler/graders until terminal result.
Prose work may continue. No workers/reviews/student/commits/pushes/installations.

Next: collect cumulative failures/results; finish chapter teaching from actual
source (not stale opening design), diagrams and walkthrough. Review/student
campaign still requires authorization. Chapter14 plan already records deferred
safety closure. Lesson: reject unsupported borrowed-element behavior while
preserving ordinary aggregate ownership; do not suppress cleanup globally.

## Chapter 13 construction teaching while cumulative job 3518 runs

Personally expanded `book/chapter-13.md` with the existing-model guide,
Introduction/TL;DR, role/policy specialization, prepare versus structural mutation,
explicit membership IR placement, phased teardown, conservative ownership
simplification, destruction hold, and exercises/cumulative commands. Actual source
read: `IR/Lowering.cs` 140–245 and 1125–1230; `IR/Ownership.cs` 1–248; public list
fixture 1–165; Common diff against accepted Chapter 12 documentation commit
`4bc7afb` (complete 239 lines). No new stored Common object relationships: new
policy enums and ObservesReceiver are scalar facts. Existing Chapter 12 figures
must not be redrawn merely for context. Lifecycle/inventory verification remains
separate and outstanding; the diff is not that verification.

Correction: checkpoint's `book/chapter13.md` path was wrong; actual file is
`book/chapter-13.md`. A guessed job-output subdirectory tail failed; job3518
remains observed through wait_for_job. A source search included binaries; direct
bounded source reads recovered relevant code. The grammar exercise initially
named nonexistent typeof-syntax.json, corrected to actual ch11/type-syntax.json
before execution. PEG test.mjs argument interface inspected, commands not yet run.

Job3518 remains RUNNING, compiler/graders/Makefile frozen. Debug completed all
current target runners, including relationships both modes20/32, participant IR2,
aggregate boundary4/11, relationship syntax6. Release progressing through layout
checks at this record. No full cumulative success claim. Prose-only edits during
this interval. New walkthrough script and Make spin target not yet added.

Source-comment correction deferred until terminal freeze lifts: Lowering.cs
Membership helper around1160 says participant parameter stays owned until cleanup;
actual borrowed participant contract supersedes this. Explain independent
membership count without that stale assertion. No behavioral fix implied.

Next: collect terminal3518; inspect full numerical summaries; implement and run
standalone spin13 walkthrough with installed tools; run independent PEG suites;
complete documentation/evidence and scoped diagram inventory check. No reviewer,
student, commit, push, install, restart or Chapter14 work authorized.

## Cumulative verification and walkthrough completed

Job3518 terminal exit0: both configurations passed the complete current Make target, two zero-warning/error builds; selected numerical summaries saved/read back in ch13-author-verification.md. Job3547 terminal0: independent PEG suites50/5/11/16/6 with malformed-grammar controls. Input freeze lifted only after terminal receipts.

Corrected Lowering.cs Membership comment only: participant parameter borrows, membership count is independent of caller owners. Added standalone tools/spin13.py and Make spin13; job3553 terminal0 clean Release build, exact four lines in both layouts runtime-debug/O2, temp cleanup. Added model guide, Introduction/TLDR, mutation/teardown/optimization construction, exercises, and practical walkthrough to chapter-13.md. No new Common edges in complete239-line diff against accepted4bc7afb; separate source inventory/lifecycle consistency verification still owed. Historical figures not changed.

No remaining jobs. No reviews/student campaign, commit/push/install/restart or Chapter14 implementation. Human acceptance pending. Next: source-inventory/documentation consistency and bounded self-review, then ask for review/student authority when ready.

## Author consistency audit complete; review authorization boundary

Fresh source extraction3562:25 Common types/110 stored relationship-bearing fields
unchanged from accepted4bc7afb; two new scalar facts only. All54 historical figure
files byte-identical. No historical fingerprint refresh or new diagrams. Bounded
IR/specialization relationship-operation self-review and public-status corrections
recorded in ch13-author-verification.md.3566 checks62 local file links, all resolve;
anchor targets not verified. README/book index/roadmap now distinguish implemented
scalar relationships from Chapter14 safety closure and future graph ambitions.

No live jobs; no compiler behavior changes in this audit. Full cumulative3518,
PEG3547 and walkthrough3553 remain the verified execution baseline. Next is one
independent code review, justified fixes, one text review, then fresh isolated
student reconstruction; agent launch authority must be obtained from Bill first.
Human walkthrough acceptance and commit permission remain separate. No Chapter14
implementation, commit, push, installation, restart or worker launch occurred.

## Standard review/reconstruction sequence authorized

Bill explicitly authorized continuing the standard sequence after author audit.
Sole code reviewer `ch13-code-review` admitted as job3570, read-only privileged
review of actual chapter/reference/public graders/evidence. No other reviewers
or student launched. Collect its one consolidated submission, incorporate
justified corrections and affected checks, then one text review and fresh
isolated student from accepted Chapter12. No review loops. No commits/pushes,
installations/restarts, or Chapter14 work authorized. Human acceptance remains
pending.

Accepted Chapter12 compiler exported from Git4bc7afb to the Agent-local
ch13-student-inputs/accepted-ch12 subtree,43 files byte-verified; full commit and
SHA256 receipt stored separately. No target source, Git history or build products
exported. Public input bundle waits for review corrections before freezing.

## Sole code review corrections and focused verification

The single privileged code review is retained in `ch13-code-review.md` (11,736 characters read back); reviewer `ch13-code-review` submitted and was shut down. No re-review is requested. Required findings were accepted: unrelated counted contributed-field ownership, actual membership execution placement, and stale public status prose.

- `SemanticSymbol.IsManagedHandle` now requires the field's participant view to belong to its contributing relationship instance. Broad `IsManagedLink` remains an access restriction, not an ownership exemption. Shared stores, field destruction and destruction-effect propagation already consume the narrower query. No new persistent state.
- New retained Python case `unrelated-counted-contributed-field` safely observes finalizer timing without reading stale storage. Initial invocation3633 was a harness usage error, not a test result. Correct baseline3635 failed with `XsavedXreplaceddone` instead of `savedXreplacedXdone`. Corrected Debug build3636 passed at C O0/O2.
- Membership validation now checks actual outer-region child order, classifying existing origins by syntax ancestry: preparation before accounting, structure after retain/before release, final outer-block cleanup afterward. No duplicate phase flag or schedule. Four added malformed-order controls preserve graph links and never emit/execute corrupted IR. Probe3643 failed because malformed order was admitted; corrected3644 exit0 passed255 checks in each clean Debug/Release probe build. This C# same-assembly lifecycle probe is supplementary author evidence, NOT an independent-language retained student grader.
- Focused3645 terminal exit0: clean Debug/Release builds, each21/21 relationship executions and32/32 rejections in default and runtime-debug modes, participant IR2/2, aggregate4/4 executions11/11 rejections. Both runtime C optimization levels exercised by the existing harness. Prior20-case results remain historical.
- Specification now describes implemented fragment grammar and separates protected fields from nonowning participant slots. Chapter teaching integrates these corrections at the ownership and execution-order construction points. Grader guide marks old migration evidence historical instead of implying cumulative checking is pending forever. Whitespace check3648 clean.

Lessons: protected write access is not nonowning storage; provenance is not execution placement. Current lowering's accounting sequence was correct, but validators had admitted reordered instructions. No invalid lifetime program was executed to demonstrate the payload bug.

Full corrected cumulative Make job3649 is RUNNING; compiler/grader inputs frozen until terminal result. Earlier3518 remains the previous full baseline, not proof of the corrected compiler. Text review and fresh student remain unlaunched pending verification and final public prose. No commit/push/install/Chapter14 work.


Post-review cumulative job 3649 terminated exit 0, both configurations; selected summaries saved in ch13-author-verification.md. No executable input changes during run. Bill’s requested first-person introduction now emphasizes bringing declarations to life, eliminating per-class hand-written ownership destructors, and avoiding LLM reliance on remembering scattered references; reusable algorithm authors still own detach/drain correctness. Next: sole text review, then isolated student.

## Text review incorporated; student inputs finalized

The sole text review is saved in `ch13-text-review.md`; reviewer shut down.
Integrated nullable protected-field initialization and exact active teardown-receiver
lifetime teaching at their construction points. Removed private-note dependencies
from Chapter 13 and the Chapter 12 acceptance statement; preserved Bill's Introduction.
No compiler or grader behavior changed after cumulative job3649. Whitespace clean.
Public bundle has 306 byte-verified files, including offline Peggy dependencies and
licenses; all 71 chapter/preface local links resolve. No target compiler, private
notes, Git history, build products, or reference probes are included. The separate
43-file accepted Chapter 12 export remains the prerequisite. Student not yet launched
at this record; human acceptance and commit permission remain pending.

## Fresh student admitted

Confined student `ch13-student` admitted as job3707; join3708 is running.
Only accepted-ch12 and finalized public subtrees plus installed toolchains are
read-only command imports. Student owns all candidate implementation changes;
parent may clarify teaching/correct defective graders, never patch candidate.
Required cumulative Debug/Release, five PEG suites, structural self-review,
public integrity, and spin13. Stop reruns at complete applicable pass. No
student result or human acceptance yet; no commit/push/installation authorized.

## Independent reconstruction complete; human acceptance pending

Join3708 terminal submission inspected in full via saved report and summaries.
Parent3879 inspected terminal receipts3827/3840/3831/3866 exit0, all five PEG
outputs and both-layout walkthrough, independently verified306 public hashes and
manifest. Student shutdown confirmed. No parent candidate edits, extra reviewers
or passing rerun. Full evidence ch13-student-acceptance.md. Incorporated unused
ownership/cleanup-window lesson in original teaching point; prose-only.

Bill's additional graph request clarified reusable multi-class composition, not
merely a concrete graph. examples/ch13/graph.ly emitted C but was NOT executed.
Nested-binding probe rejected LYC201 at75:5; generic-binding probe LYC306 at71:6.
No generic graph/cycle algorithm pass claimed. Confirmed composition gap belongs
to separately planned graph chapter; no new compiler feature implemented.
Future chapter plan expanded separately; frozen student materials unchanged.
Chapter13 implementation/reviews/reconstruction done; human spin13/acceptance and
commit authority pending. No Chapter14 implementation, push/install/restart.
