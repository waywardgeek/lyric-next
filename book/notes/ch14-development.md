# Chapter 14 development

## Starting boundary

Bill accepted Chapter13 and spin13, authorized local commit14dd2fe574b615d65e222f636d2513d454f30132, reported pushing it, and then authorized proceeding personally with Chapter14. No workers, new commit/push, installation or later feature campaign is authorized. Preserve unrelated arith.ly, fact.ly, hello.ly and test.ly.

## First aggregate contract and baseline

Added public graders/ch14/aggregate_participants.py using the independent public tuple execution harness and Chapter13 list fixture. Four new positive cases exercise tuple, array, explicitly annotated tuple and nested tuple borrowing. Removal ends the last attachment, then prints after; no saved child is read afterward. Expected liveNafterdone distinguishes borrowing from delayed owning cleanup. Four existing explicit-concrete/application-owner controls remain unchanged.

Job3897 terminal exit1: unchanged accepted compiler clean Debug build, zero warnings/errors; runtime-debug AoS baseline 4/8 execution cases pass. All four new cases reject with the intentional Chapter13 LYC304 aggregate restriction. All four owning controls execute successfully at C O0/O2. The one type-error negative reports PASS but is masked by the same aggregate restriction; it does NOT yet demonstrate element mismatch checking. No invalid lifetime program executed. No Chapter14 compiler changes or feature pass.

Source observations: Check.cs TypeView and RequireAggregateElementView carry/restrict scalar views. ParticipantBinding.AcceptsViewType accepts exactly its nominal class or one nullable wrapper; Add/RemoveViewSymbol and Add/RemoveViewFact maintain reciprocal weak memberships and clear IR. SemanticSymbol/BorrowsParticipantLocal and IR/Model.cs IsBorrowedHandleValue are scalar. Lowering ReadKind/DropKind select aggregate copy/cleanup from canonical types. A canonical type alone cannot distinguish ordinary owning containers from borrowed participant containers of the same concrete class.

## Rejected proposal: per-element borrowing (superseded by Bill's clarification)

Recommend per-position views for tuples and one uniform element-view contract for arrays. Reject inferred arrays mixing a participant view with an ordinary owning view (or different endpoint views), rather than adding runtime per-element ownership tags or silently retaining everything. Explicit ordinary conversions remain available. This needs Bill's agreement; it is not yet a language rule or compiler change.

Any eventual view representation must preserve actual endpoint identity and retirement relationships; it must not alter canonical type identity, infer privilege from concrete class spelling, or create an emitter-only ownership authority. Array storage still has an owner even when its elements borrow. Mixed tuples retain/release unrelated counted elements normally. Implementation and further contracts follow the semantic decision.

## Agreed correction and first implementation

Bill confirmed ordinary arrays/tuples own counted elements, including copies from
nonowning structural links. Attachment bookkeeping remains one count per counted
membership; scalar participant maintenance stays borrowed. No per-element
borrowing model, runtime ownership tags or mixed-view rejection. Earlier delayed
finalization was expected ordinary ownership, not a defect.

Removed only RequireAggregateElementView and its six calls from Check.cs. Existing
Lowering.Transfer already emits Ref before moving a borrowed scalar into owning
aggregate storage. No new model state, IR operation or emission algorithm. First
edit guard incorrectly counted the helper declaration as a call: job3905 refused
before any write/build. Corrected edit job3906 built Debug cleanly, zero warnings
or errors; all12 executions and1 intended type rejection pass with runtime-debug
AoS at C O0/O2. The type mismatch is no longer masked by the aggregate gate.
Additional copy and helper-roundtrip contracts are added next, not yet verified.

Job3908 terminal0:16/16 local/helper execution cases and1 type rejection pass.
Job3910 terminal1: added contributed [C] field control rejected LYC306 at its
declaration (not assignment);16/17 executions pass,1 rejection pass. Fail-fast
stopped before SoA/scalar regression checks. Inspection finds the same explicit
class-handle-array field exclusion in ordinary and contributed registration.
The assumed-supported field fixture was corrected to retain that existing
unsupported boundary; no field admission/emitter extension made. This is a
separate known limitation, not proof of aggregate field support.

## First increment verified

Job3914 terminal0: Debug runtime-debug AoS and SoA each16/16 executions,2/2
rejections at C O0/O2; retained participant IR2/2; relationships21/21 executions
and32/32 intended rejections. Job3916 terminal0: fresh Release zero warnings/errors,
all four AoS/SoA x production/runtime-debug combinations each16/16 and2/2 at
C O0/O2. Whitespace clean. Inputs frozen during each run; no active jobs now.

Make now uses the Chapter14 successor instead of the temporary Chapter13 aggregate
restriction suite. The historical suite is unchanged. Specification, Chapter14
first construction section and public grader guide state ordinary container
ownership and the remaining field-array limitation. No full Chapter14 cumulative,
review/student/human acceptance, commit or representation migration yet.

Next authorized increment: public production-handle contracts before edits.
Emission/Compilation.cs currently hardcodes uint64_t handles in CType, destruction
signatures, saved field receivers, membership temporaries and counted-array helpers;
UINT64_MAX null is used by both layouts. Debug already maps monotonically fresh
identities to physical slots and tombstones after cleanup. Read complete relevant
sections before choosing pointer/free-list representation; don't replace integer
spellings indiscriminately or compromise debug identity checks.

## Safety milestone and first ordered-call audit

Bill reaffirmed that single-threaded memory safety is the central Chapter14
milestone, not merely a patch-list completion. The chapter now requires students
to map specification obligations to actual enforcement paths and positive/negative
tests, resolve holes or agree explicit rejection boundaries, and report limits.
Handle representation is not itself a prerequisite for this safety guarantee.

Source inspection: CheckBorrows uses ordered mutation/use obligations, substitutes
actual storage at calls, and propagates unresolved formal aliases. ApplyClassEffect
includes final hooks, fragments and counted-field cleanup. These are observations
of mechanisms, not a completed audit. Existing Chapter8 fixtures distinguish saved
field destinations across growth from destruction. The receiver-use path remains
to be traced fully.

Job3934 terminal exit0: six new compile-only cross-call contracts pass on the
existing current Debug compiler. Direct/forwarded destruction-before-use and
nested replacement-before-use reject with intended LYC326; reversed-order controls
pass. No invalid programs executed, no compiler edit, no full audit completion.
Suite registered in Make and public guide; Release still pending for these cases.

Expanded ordered-call audit: job3941 passed10/10 Debug cases; job3943 terminal0
passed14/14 on BOTH existing current Debug/Release assemblies, including nested
struct-in-tuple aliases, finalizer-induced cleanup, generic calls and loop back
edges. Safe reverse-order/fresh-acquisition controls pass. Invalid programs were
compile-only. No compiler changes in this audit slice.

Saved field-store path inspected: Select stores objectValues[receiver] in
selectedObjects; Use emits object-use events after RHS evaluation. Thus exemption
from address-relocation invalidation does not exempt the saved receiver from
object destruction. RequireSafe exports formal ordered obligations and checks
actual field owner/class and depth after substitution. This is bounded source
and behavior evidence, not completion of all safety obligations.

A guessed contained_references.py path failed (3938 exit1); actual suite is
borrowed_structs.py, subsequently inspected. No failed read counted as verification.
Next: persistent-field admission and definite initialization, then ownership/
teardown and runtime access checks; production handle migration remains paused.

## Initialization, access and ownership source audit

Actual source reads: Semantics/Check.cs 520–590 recursively admits persistent
fields after contributions; counted handles terminate that walk and their own
physical fields are separately checked. Constructor setup 810–836 initializes
only nullable managed links. RequireInitializedReceiver near4809 enumerates
physical fields, Return near3872 checks it, CheckIf near4110 intersects continuing
paths, and CheckAssignment near4970 marks only after RHS/type validation.
Existing ch08 controls cover ordinary initialization/partial-self/early exits; new
ch14/safety_initialization.py covers labeled contributions. Jobs3953/3954 passed
initial6 in Debug/Release; final3960 passed7/7 each, including finalizer-cycle guard.

Potential nested tuple-field counted-array shrink relocation was investigated
compile-only (3959). It rejected with exact LYC334 own-class finalizer dependency;
no unsafe execution or demonstrated hole. Retained that refusal, not an execution
claim. Emitter SelectIndex near580 checks negative/index>=length before pointer
formation; UnwrapNullable near752 checks sentinel in safe mode; resize near1900
initializes before publishing length. Full runtime access audit still open.

IR/Lowering.cs LowerDestruction140–230 orders hook/child-role detach/parent-role
drain/ordinary fields/reclaim. Transfer near363 retains borrowed counted scalars
at owning boundaries. End460 transfers before Cleanup; break/continue near1364
clean exited regions/condition. Emission release1333 clears holder then decrements,
restores last hold before destruction and does not dereference cached metadata
after callback. Structure/operation/execution validations run before and after
simplification and before mutable-IR emission. These are source observations, not
whole-language proof. Regression job3964 still running at this note: both-config
ch08 check and ch09 ownership; keep inputs frozen until terminal receipt.

Read failures/limits: initial Checking/Check.cs guess nonexistent; later successful
ls masked failure in shell exit0. Corrected to Semantics/Check.cs with fail-fast.
Several combined reads clipped; relevant paths reread boundedly afterward. No
compiler change during this audit; only existing container-gate removal remains.

Regression3964 terminal exit0; input freeze lifted. Both configurations completed
retained Chapter8 lifetime checking and Chapter9 ownership IR interpretation.
Saved output summary extracted separately after clipped inline delivery; ownership
92/92 each. No compiler edits in this audit, no runtime execution of invalid
programs, no whole-chapter completion claim. Teaching readback3967 confirms ordered
call obligations and cleanup construction plus required student audit. New
initialization/admission suite is registered in cumulative Make alongside calls.


## Runtime access audit and introduction

Job3975 terminal exit0, compiler/fixtures frozen through completion. Both Debug
and Release: new initialized-length access4/4 (one valid regrowth, three intended
safe failures); nullable11/11 executions,10/10 compiler rejections,3/3 unsafe
omission inspections without invalid execution,8/8 valid unsafe executions;
relationships21/21 executions and32/32 compiler rejections in each normal/debug
runtime mode. Numerical summaries extracted from full saved job file after clipped
inline receipt. Prior3964 summaries also confirm lifetime154/154 and ownership
IR92/92 in each configuration. No compiler changes during this audit.
New access suite registered in cumulative Make and public guide. Source checks
establish initialized-length bounds before pointer formation and null checking in
safe mode; these tests do not establish exhaustive lifetime correctness.
Bill requested and approved direct authorial framing of Rust comparison and future
self-hosting ambition; introduction saved/readback3987. One-week self-hosting is
his prospective commitment, not a completed result. Safety milestone remains open.


Recursive audit3990 terminal0:18/18 cross-call checks in both Debug/Release,
including four new direct/mutual descent/unwinding controls. Check.cs846–890
initializes all body summaries, starts returning paths at bottom, and iterates to
fixed point. Exact teardown receiver inspected1126–1175/1472–1506/2197–2215:
only fragment parameter0/direct exact-symbol observations receive protected graph
identities; ordinary same-class children still invalidate. Retained relationships
3975 includes stale-child/stale-storage negatives and cross-membership cascade.
Teaching now explains recursion, initialized-length access, and the narrow receiver
exception at construction points; public cross-call count updated18. No compiler
change, no exhaustive proof or completed chapter claim.


Escape and opaque-boundary audit: Check.cs667/3880/6117 rejects borrow-containing
returns, RequireValidArrayStorage5111+ rejects borrowed array elements; ReadType
5179+ rejects FunctionType and body admission804+ rejects missing implementations.
Return3736+ rechecks object validity after generated cleanup. Retained contained
reference run4000 terminal0, summaries4005 confirmed20/20 execution23/23 rejection
both configurations. New call-admission4005 passed3/3 each. These are supported
subset boundaries, not implemented FFI/callback contracts. Teaching and exercises
now require an enforcement map, retained cumulative run, and explicit limitations.
Next one full cumulative checkpoint before any production-handle changes.

## Allocation arithmetic audit during cumulative checkpoint

While cumulative4007 remains running with compiler/grader inputs frozen, inspected
Emission/Compilation.cs190–273,1210–1265,1855–1898. ly_array_new/reserve check count
against SIZE_MAX/stride before multiplication, saturate growth before addition,
check allocation result, and publish replacement only after success. Class slot
and array-push increments reject UINT64_MAX before adding one; push installs its
element before increasing initialized length. Class allocation reserves storage
before publishing the high-water mark. Existing ch07/run.py allocation-size-overflow
requires exact LYR007 and trace without host-memory exhaustion. No new runtime
claim for allocator failure injection or class-index exhaustion. Required chapter
audit checklist now explicitly includes allocation arithmetic/publication. No
compiler/grader change; no cumulative success claimed until4007 terminal receipt.

## Bounded author audit cumulative checkpoint complete

Job4007 terminal exit0; compiler/grader freeze lifted. Job4021 extracted both full
numerical summary blocks: zero build warnings/errors, all retained runners pass
Debug/Release. New per-config checks: aggregates16exec/2reject, ordered calls18,
initialization7, initialized-length access4, call admission3. Full extracted receipt
is ch14-safety-audit-verification.md. No additional demonstrated safety hole found
in the inspected paths. This completes the bounded author audit, not formal proof
or Chapter14 completion/review/student/human acceptance. Handle migration remains
unimplemented; no new compiler changes during the audit.

## Production handle migration started — metadata and SoA increment

Bill confirmed production AoS metadata belongs in each stable object record,
not a separate index-addressed slot array. He also required SoA metadata to use
separate next-free and reference-count arrays, not an array of metadata structs.
Benchmark competitiveness is the goal; no timing or memory win has been measured.
The starting plan now records these choices and the completed author audit4007.

Public `graders/ch14/handles.py` baseline4030 exited1: both allocators returned
uint64_t instead of the required pointer/u32 formats (0/2). Lyric emission itself
succeeded. The grader includes counted copies, cleanup and slot reuse at C O0/O2.
It is an initial representation contract, not complete null/exhaustion coverage.

First production-only SoA metadata split: clean Debug build4033; smoke and
aggregate run4037 exited0, O0/O2 counted reuse and16/16 executions2/2 rejects.
Then introduced temporary emitter HandleType/EmptyHandle queries. Production SoA
uses uint32_t handles/next-free entries and zero null/free-list termination, starts
allocation at1, retains a64-bit high-water counter and checks >UINT32_MAX before
narrowing. Counts remain64-bit. Updated saved receivers, membership release holders,
nullable conversions/emptying, function signatures and counted-array leaf strides.
Debug identities and current AoS indices remain unchanged in this intermediate step.
Clean Debug build4043. Grader4044 exited1 as expected for incomplete migration:
SoA passes representation/reuse; AoS still fails uint64_t return (1/2 overall).

Focused production SoA nullable/relationship/aggregate run4049 is RUNNING at this
entry; compiler/fixture inputs frozen until its terminal receipt. No result claimed
yet. Remaining: bounded zero/index-exhaustion controls, AoS pointer conversion,
new-layout successor for historical debug_mode.py production metadata probe,
debug regression, Release and affected retained tests. Historical grader must not
be silently weakened: preserve the injected missing-destruction-hold failure.
No cumulative migration pass, benchmark, review or acceptance claim.

### Production AoS pointer increment

Run4049 terminal0: production SoA Debug nullable11/11 executions10/10 rejects,
relationships21/21 executions32/32 rejects, aggregate16/16 executions2/2 rejects.
Compiler/fixtures unfrozen afterward. Typed record forward declarations now precede
aggregate layouts. Production AoS records embed next_free pointer and counted
references; allocation still uses stable64-record chunks, returning actual record
pointers. Free-list reuse follows embedded pointers. Live fields/counts never recover
indices from pointers. Chunk-position counter remains private allocator bookkeeping.
Debug identity layout stays separate and unchanged. HandleType/EmptyHandle select
physical types/null, not semantic ownership. Clean Debug build4052. Public handle
contract4053 terminal0:2/2 layouts, both O0/O2 counted cleanup/reuse.

Focused AoS nullable/relationship/aggregate run4054 RUNNING at this entry; tested
inputs frozen. Debug identity suite must be run separately afterward (command4054
only lists its Makefile entry, it does not execute that suite). Historical
ch13/debug_mode.py production injected probe is not compatible with new metadata;
a documented successor retaining its checks is still required. No full migration,
Release, cumulative, performance or acceptance claim yet.

### Handle milestone focused verification and teaching

4054 terminal0: Debug production AoS nullable11/11 execution10/10 rejection,
unsafe omission3/3 inspected-only and valid unsafe8/8; relationships21/21+32/32;
aggregates16/16+2/2. Inputs unfrozen. New ch14/debug_mode.py preserves original
four modes and injected missing-hold controls with new production metadata access;
original ch13 file unchanged.4056 terminal0: successor4/4 and unchanged debug
identity64/64. Bounded allocator test4059 exposed a fixture bug: release before
fresh exhaustion request correctly made another reusable slot. Corrected probe
keeps reused object live;4061 terminal0 both layouts/O0/O2 pass, SoA exact LYR007.
No invalid unsafe execution or huge allocation. Lesson: reuse is valid at exhausted
high water; negative tests must keep all free slots occupied.

Emitter comments corrected; removed unnecessary unused byte from fieldless
production AoS (metadata already makes record nonempty).4063 clean Release build,
zero warnings/errors and whitespace clean.4065 terminal0: Release handles2/2,
debug-mode4/4, identity64/64, retained layout212/212. Middle report clipped, totals
visible in terminal receipt. No new full cumulative run yet. Compiler inputs
currently unfrozen; no live jobs or children.

Make target now uses ch14/debug_mode.py and adds handles.py. Current ROOT
lyric-language-spec.md layout section updated, not background/ historical spec.
Chapter14 section8 teaches production AoS pointers/in-record counts/free links,
SoA u32/separate arrays/zero reservation/wide high water, typed aggregate strides,
debug identities and unchanged conservative borrows. Exercises and public grader
README updated;4073 whitespace clean. Plan has physical-layout decision and prior
audit status. Remaining: full retained regression after shared emitter migration,
verify documentation readback and stale plan statuses, concrete nonnullable
relationship construction decision, practical walkthrough/full chapter closure.
No performance measurements, new workers/reviews, commits/push/install or safety
proof claimed. Do not resume old background spec or unrelated scratch work.

### Post-migration cumulative allocation contract correction

Cumulative4076 terminal exit2 stopped in Debug Chapter8 allocation.py after
212/212 layout executions. Reuse passed; growth measured9 rather than historical19
AoS allocations. The historical expectation includes ten now-removed metadata-array
allocations. New Chapter14 successor preserves all workloads/modes/assertions and
expects9. First focused4083 then exposed my incorrect unchanged SoA expectation20:
reserved slot zero makes first capacity2, so two arrays grow nine times each,18.
Corrected from the actual reserve algorithm, not an unexplained observed number.
No compiler change; historical grader preserved. Make selects successor. Full
cumulative remains unverified at this entry.

4085 terminal0: allocation successor both configurations4/4 reuse plus4/4 growth.
4086 full retained cumulative terminal0 after migration: Debug/Release clean, all
required runners passed; complete numerical summary retained in safety-audit record.
4088 extracted both build results and all totals without report clipping. Freeze
lifted, no live jobs. No compiler changes in this correction slice; no performance
claim. Next: concrete protected nonnullable construction decision, remaining chapter
teaching/walkthrough and separately authorized reviews/reconstruction.

## Mandatory relationship scope resolved

Bill clarified that a required parent denotes a mandatory relationship: the parent
must exist throughout the child's live lifetime. Its future constructor receives
the parent and calls the parent's attachment function before returning. Bill then
explicitly deferred mandatory relationships beyond this book. No constructor
escape, protected-write exemption or new attachment semantics were implemented.
An intermediate rejection-only interpretation and subsequent proposed current
construction obligation were superseded by this explicit scope decision.

Updated chapter/spec/starting plan/roadmap at their teaching points. Public
initialization suite adds compile-only direct-write LYC308 and omitted-required-link
LYC328 controls. Job4100 terminal0: 9/9 Debug and 9/9 Release, whitespace clean.
Compiler unchanged; cumulative4086 remains the post-migration baseline with the
original seven initialization cases. No claim of mandatory-relationship support,
new full cumulative run, independent review, reconstruction or acceptance.


## Chapter 14 walkthrough

Added standalone tools/spin14.py and Make spin14 target using the existing list
algorithm, an ordinary owning array in maintenance, initialized regrowth, and a
compile-only cross-call destroyed-owner borrow rejection. Job4104 terminal0:
clean Release build, exact six-line output for production/debug AoS/SoA at O2,
intended LYC326 without execution, temporary cleanup reported. No compiler change.
Exercises and walkthrough now specify setup, command, observations, cleanup and
limits. Bill's mandatory-parent performance motivation recorded in roadmap;
no timing measurement or speedup claim. Human use/acceptance, independent reviews
and fresh reconstruction remain pending and not authorized by this walkthrough.
