# Chapter 8 development

## Current approved direction

Bill authorized standalone ordinary and counted value classes, constructors, scalar fields, methods, final hooks, indexed identity, destruction and lifetime checking, in AoS and SoA. Later array fields/arrays of counted handles must preserve Chapter 7 ownership rules as this increment develops. No graph, relations, object-to-object fields, fallible construction or general generics yet. Full future dependency spine is in ../roadmap.md, linked from the book index.

AoS uses fixed nonmoving chunks and a free list: reuse freed slot, otherwise take unused slot in last chunk, otherwise allocate a new chunk. SoA uses growable per-property arrays. Class handles are indices. This replaces the full virtual-reservation requirement below; the probe remains historical feasibility evidence only. Both backends share source acceptance: class allocation is conservatively potential backing invalidation for field borrows, while object indices survive growth. Destruction/slot reuse never revives old handles. Class access feeds the existing borrow/effect analysis directly like array access; no extra generated-code reanalysis pass.

Counted objects retain/release on ownership operations, move transfers, and final runs at zero in the initial backend. Scope/return/branch/loop cleanup is required. No queue/cycle collector/manual structural destructor. Final effects and generated releases participate in checks. Keep immediate-backend timing distinct from the broader language timing contract. Ordinary destroy and counted zero-release initially perform local cleanup only, not relationship teardown.

Lesson: do not let a graph demonstration invent language features or force prerequisites prematurely. Establish class and interface foundations before maintained graph relationships.

## Fixed-reservation feasibility probe

Source: ch08-checks/soa-reservation.c. Job1299 exit0 on Darwin ARM64, Apple Clang21, 16384-byte pages. Ordinary -O0, -O2 and -O2 with address/undefined sanitizers all passed. Build/run receipts: reservation-{O0,O2,sanitized}{-build,}.txt.

Each process held ordinary 1MiB heap storage and three simultaneous anonymous private PROT_NONE mappings: 2^32 slots at strides1,4,8 (52GiB total virtual span). mmap(NULL, ..., MAP_PRIVATE|MAP_ANON) used kernel-selected addresses, never MAP_FIXED. mprotect enabled first/middle/last page of each region; writes and rereads checked nine distant slots after all mappings were established. Heap contents were checked and every mapping munmapped. Only nine 16KiB field pages were enabled; no claim of measured RSS or complete physical-memory accounting. Temporary executables removed.

Lesson: use kernel placement, not guessed addresses or destructive fixed mappings. The sanitizer build selected different addresses and passed on this host, so no collision workaround was necessary here. This is bounded feasibility, not cross-platform support, exhaustion handling proof, allocator implementation, shadow-memory correctness proof, or performance evidence. No compiler changes yet.

## Public baseline and specification reconciliation

Updated specification storage section, dynamic-array/concurrency wording, conformance case29, historical evidence and open-items summary to indexed chunked AoS/growable SoA. Preserved source-level stable borrows and transitive effects; no stale borrow is re-resolved. Removed algorithm-prescriptive lowering wording from normative specification; implementation strategy stays in this note/roadmap.

Six public initial cases in graders/ch08/run.py: constructors/fields, alias identity, methods, ordinary final, counted scope final and counted alias final-once. Fresh Debug build job1305 had zero warnings/errors; all six fail with intended unsupported Class (LYC306), not parser errors. No class compiler implementation yet. No new finalizer timing promise beyond initial immediate-release backend. Future negative lifetime, layout and allocation tests remain required.

First inspection guessed Common/Semantics.cs and Common/Syntax.cs incorrectly; actual declarations are Common/SemanticModel.cs and Common/SyntaxNode.cs. Later bounded reads located them. Compound reports clipped; missing source is not reviewed. Parser Aggregate/Field/Function shapes confirmed for class/value/modifier/self/qualified declaration. Next inspect full semantic declarations, model lifecycle, top-level signature/body collection and type operations before selecting authoritative class/member representation.


## Nominal model foundation (implementation underway)

Bill explicitly asked to develop AoS and SoA together, not finish AoS first.
Every class-behavior increment must run in both layouts once emission exists.
The final stale concurrency sentence assuming stable class addresses was corrected:
exclusive protection prevents other-thread relocation, not the same thread's
potentially invalidating allocation. Readback and whitespace checks passed.

The first compiler change adds nominal class identity to SemanticType, preserving
class identity through array nesting. Class symbols own member scopes; those scopes
also retain their existing analysis ownership, and both relationships have backlinks.
The existing owner-taking scope constructor now accepts function or class owners.
A separate class-definition wrapper and factory were considered then removed as
unnecessary: the declaration symbol already supplies identity and the real parent.

Nominal descriptions are not a loophole in relationship tracking. Persistent symbol
and fact holders register reverse type dependencies on the class symbol. SetType
validates and reserves reverse-list capacity before removing the old membership.
Constructors register dependencies; failed construction uses ordinary destruction
to unwind. Class removal retires dependent facts/symbols; destruction clears both
ends. The checker still rejects class syntax until declaration/body support exists.

Verified: clean Debug build (zero warnings/errors), 79/79 Chapter4 semantic cases
selected for Chapter6, 102/102 Chapter7 execution cases at O0/O2, and a supplemental
14-assertion real-model lifecycle probe. Saved nominal-model-* logs and
nominal-lifecycle.cs/.txt under ch08-checks. Probe covers nominal identity, array
nesting, refused cross-analysis replacement, retyping, class-first teardown and
analysis-first teardown. It is not a public language grader. No Release/full
cumulative run, class behavior, allocator behavior, or sanitizer proof claimed.

Next: register class names before signatures; introduce qualified member lookup
without leaking fields into lexical lookup; resolve self and constructors; stage
field/body checking. Emission currently assumes top-level functions and array-only
member calls, so accepting classes requires updating those paths, not skipping
class bodies. Both allocator layouts remain unimplemented. Parser file is
compiler/Parser/SourceFile.cs; guessed Parsing/Parser.cs read failed with no effects.

### Qualified member lookup infrastructure

Class member scopes now have direct `LookupMember` selection. Ordinary lexical
`Lookup` skips class member scopes while continuing through their real parent
links. Class member registration checks duplicates in that qualified namespace;
nonmember registration keeps the existing lexical no-shadowing rule. This allows
a constructor to share its class name without exposing fields as bare locals.
No class syntax has been enabled by this infrastructure change.

Verification: `member-lookup-build.txt` records a clean Debug build (zero warnings
and errors). The real-model `nominal-lifecycle.cs` probe now passes 19 assertions,
including constructor-name reuse, explicit field selection, lexical field exclusion,
duplicate refusal preserving membership, and both teardown orders. Output is in
`member-lookup-lifecycle.txt`. This C# probe is supplementary model evidence, not
a public language grader. No full cumulative or Release rerun for this change yet.

Next: collect class/member declarations, resolve constructor/self signatures and
check all bodies, then implement matching indexed AoS and SoA emission. Constructor
bodies remain unit-valued; construction expressions produce the nominal handle.
Do not treat final methods as handwritten structural destructors. Keep unsupported
features rejected until checking and emission are coherent.

### Declaration and signature collection

The checker now collects top-level class identities before resolving member types,
then registers qualified field and method symbols in each class-owned member scope.
Methods join the existing signature/body passes; their bodies are not skipped.
`self` obtains its nominal type through the method's actual scope/class relationship,
not a copied owner pointer. Infallible constructor bodies must return unit (LYC327).
Missing/late receivers remain unsupported in this milestone. Field initializers,
object-to-object fields, and final modifiers remain unsupported; class construction,
field operations and runtime storage are not implemented yet. Emission explicitly
rejects class declarations with empty stdout instead of treating them as functions.

Debug declaration build passed. Eight focused declaration checks passed, followed
by a retained-test failure: the old Chapter 4 empty-class rejection is now obsolete
for Chapter 8. The public semantic grader retains its original default and adds
chapter 7/8 selectors; Chapter 8 checks that exact source as a silent success.
The failed log is preserved in declarations-semantics.txt. Corrected selection
passed 79/79 and the array suite passed 102/102 (job1356, exit0). These are Debug
results, not Release/full cumulative or class runtime evidence.

Receiver/result checks then passed a clean build and 11 focused assertions
(job1363). The first probe revision failed only its emission-boundary assertion:
it reused list position zero after negative cases were prepended. Corrected the
probe to select the empty-constructor case by name, preserving signatures-probe.txt
and saving signatures-probe-corrected.txt. No behavior requirement was weakened.

Next: constructor field initialization on returns/fallthrough, resolved field reads
and writes, and class-owner storage dependencies in borrow selection. The existing
borrow pass calls Select for every assignment; fields cannot be routed through the
old array/local model unchecked. Specification1195–1222 explicitly distinguishes
cleanup-safe defaults from permission to read required uninitialized fields and
requires checking successful construction and partial-object escape. Both AoS and
SoA remain required together. No allocation implementation or runtime success yet.

## Direct field checking and construction initialization

The checker now resolves direct class fields to authoritative field symbols. Direct reads and plain `=` writes are supported during semantic checking; class C emission remains explicitly rejected pending both storage layouts. Field references, indexed field mutation and field compound assignment are not enabled by this slice.

Constructor field state uses the existing temporary definite/possible assignment sets and branch joins. Only writes through the actual constructor `self` parameter initialize its fields; writes to another instance do not. Direct `self.field` selection can inspect initialized fields without exposing the unfinished object. Copying/passing whole `self` requires every field initialized. Both explicit return and reachable fallthrough check completion. Cleanup-safe allocator defaults are not treated as language initialization. Fields have ordinary syntax/symbol fact backlinks; no duplicate persistent field-type table was added.

`CheckBorrows.Select` explicitly recognizes a direct member assignment rather than incorrectly treating every non-name as an index with two children. Its field-root event is preliminary array-backing bookkeeping, NOT completed class-growth/destruction analysis. Field borrows remain rejected until those dependencies and transitive effects are represented.

New public retained `graders/ch08/check.py` has 14 checker cases: initialization, reads, branch joins, early returns, partial-self escape, another receiver not initializing self, owned array fields, narrow type hints and missing-field diagnostics. Invoke from repository root with the compiler command as trailing arguments. Success requires exit 0 and empty streams; rejection exit 1, empty stdout and specified diagnostic code/message. Location precision is not asserted by these cases.

Evidence: `fields-build.txt` clean Debug build, zero warnings/errors. First `fields-check.txt` was 7/14: seven fixtures omitted declaration separators, producing parser diagnostics before semantics. Fixed those seven sources with explicit semicolons; compiler unchanged. `fields-check-corrected.txt` 14/14; `fields-declarations.txt` 11/11 supplementary declaration assertions. Retained Debug semantic/expression/array checks started as job 1383 with outputs `fields-semantics.txt`, `fields-expressions.txt`, `fields-arrays.txt`; completion is pending at this note. No Release/full cumulative/runtime-class pass claimed.

Next: constructor/method call checking and matching borrow traversal (implicit receiver versus explicit arguments), then coordinated indexed-handle AoS/SoA emission. Layout should enter `EmitC` as a transient option, not a second semantic checker. Actual driver currently accepts exactly mode+file and has no `--soa` support.

### Field regression receipt and call checking

Job 1383 completed exit 0; saved summaries read in job 1391: Debug 79/79 semantic (--chapter=8), 250/250 expressions, 102/102 arrays, whitespace check passed. This resolves the pending field-slice note above.

Constructor and instance method calls now resolve to actual function symbols. Shared `CheckArguments` accepts an implicit-parameter offset: zero for top-level calls, one for constructors/methods. A constructor call's fact has the class result type while resolving the unit-returning constructor body. Instance receiver is checked before explicit arguments; reinitialization through `existing.C(...)` is rejected. Constructor/method hints expose their actual result types, preserving narrow arithmetic selection.

Borrow traversal evaluates the method receiver before arguments and uses the same formal offset when selecting reference arguments. Existing parameter-specific effect/ordered-overlap summaries apply to methods and constructors without a second analysis model. A retained method test grows a caller array through its first explicit ref parameter and rejects a subsequent element-borrow read, proving the implicit receiver did not shift the effect onto a wrong formal.

Public checker inventory now 23 cases (nine call cases added). Clean Debug `calls-build.txt` zero warnings/errors and `calls-check.txt` 23/23, job 1395 exit 0. Job 1399 is running retained Debug semantic/expression/array checks, outputs `calls-semantics.txt`, `calls-expressions.txt`, `calls-arrays.txt`. No class runtime pass, Release or full cumulative rerun yet.

Backend inspection: `EmitC` currently rejects classes before output; ordinary function signatures/expressions assume no implicit receiver. `Storage` assumes Name or Index, `CType` has no nominal branch, owned temporaries cover arrays only. These must change coherently. Field assignment should snapshot receiver index before RHS then resolve current slot after RHS, not retain a moving SoA interior pointer. Explicit field refs remain unsupported until class effects are checked. Driver currently exactly mode+file; layout can enter `EmitC` as a transient option (default AoS, --soa alternative), not duplicate semantic state. Both backend layouts remain unimplemented.

### First executable AoS and SoA scalar classes

Call regression job 1399 completed exit 0; saved summaries read in job 1401: 79/79 semantics (--chapter=8), 250/250 Chapter 6, 102/102 Chapter 7 in Debug.

Driver accepts `--emit-c --soa FILE`; default `--emit-c FILE` remains AoS. Layout is a transient EmitC argument, not persistent semantic state or a separate checker. Invalid option combinations reject with usage. Initial build failed CS1066 because the partial implementation repeated the optional default; corrected to put default only on Common declaration. Failed and corrected build logs preserved. Bounded option probe passed before enabling class storage; its old unsupported-class expectation is historical, superseded below.

Emitter now collects checked ordinary class methods, emits nominal values as indexed handles, injects self in calls, and preserves explicit argument ownership offsets. Constructors allocate a handle then invoke their unit-returning checked bodies. Receiver executes before method arguments. Direct field assignment evaluates receiver once into an index, then resolves storage after RHS effects; it does not retain a field pointer across SoA growth.

Both layouts now execute scalar-field classes. AoS has a movable pointer directory with separately allocated fixed 64-record chunks; SoA has growable per-field buffers using existing checked allocation primitives. No live compaction. Slots are currently append-only: free-list reuse, explicit destruction, final, counted ownership and class-backed borrows are NOT implemented. Array fields and counted classes remain explicitly rejected by C emission to avoid silently skipping their ownership obligations. This is an intermediate construction milestone, not complete Chapter 8. Live objects/storage are not yet reclaimed by this slice; tests do not assert allocation balance. Internal uint64 indices and overflow checks are backend choices, not a settled public maximum/null representation.

New public retained `graders/ch08/layout.py` exercises construction/read, shared identity, growth past four chunk boundaries, and RHS-induced allocation before a field store. It runs identical sources with AoS/SoA at O0/O2, requires exact successful output/streams, and documents no destruction coverage. Clean Debug build `storage-build.txt`; `storage-layout.txt` 16/16 executions, job 1411 exit 0. Source readback and whitespace passed. No Release or sanitizer claim.

Job 1413 now runs fixed-build Debug public checker23, semantic79, Chapter5 75, Chapter6 250, Chapter7 102; logs storage-check/semantics/ch05/ch06/ch07. Pending at this note. Next collect same job and summaries, then lifecycle implementation including explicit destruction plus class-wide invalidation before slot reuse is exposed. Do not mistake append-only allocation for completed free-list storage.

Job 1413 completed exit 0. Saved summaries: 23/23 Chapter8 checking, 79/79 semantic (--chapter8), 75/75 Chapter5, 250/250 Chapter6, 102/102 Chapter7; Debug only. Whitespace passed. All jobs collected, no live child. Next safety slice must track ordinary nominal handles as well as field borrows: current CheckBorrows only acquires explicit reference parameters/aliases and summarizes formal array paths. Class destruction cannot be encoded as an arbitrary array resize. Allocation affects field addresses, not indexed handles; destruction affects both. Reuse remains disabled until stale-handle analysis is connected.

## Destruction checking — class-wide effects (in progress)

Added `ClassOperation.Destroy` to semantic facts; the existing fact-to-symbol relationship retains the class identity. No untracked class pointer or backend spelling dispatch. Explicit `destroy()` rejects counted classes (LYC331) and arguments (LYC303). C emission deliberately refuses checked destruction with LYC306 until reclamation is implemented.

The existing temporary borrow CFG now also carries object acquisition/use/class-destruction events. A separate per-function class-effect set reaches a fixed point alongside array-path effects; destruction is not encoded as array resizing. The same worklist propagates invalid object identities through branches/loops. Class-valued parameters, locals, and expression temporaries are tracked; selected field destinations retain the receiver's temporary identity across RHS evaluation. Caller argument uses occur after later argument evaluation, catching destruction of an earlier argument. Fresh acquisition resets only its identity, not older aliases. Allocation itself does not invalidate indexed object handles. Field-borrow growth effects remain unfinished and field refs remain unsupported.

Public checking inventory increased from 23 to 43. Saved initial baseline `destruction-baseline.txt`:24/33 (old23 + growth control pass, destruction still unknown method). Two more cases cover field RHS and later argument effects. After implementation `destruction-check-first.txt`:35/35. Additional eight edge cases baseline `destruction-edge-baseline.txt`:40/43; unsafe constructor self-destruction (fallthrough and explicit return) and a delayed-initialized local were accepted. Corrected by tracking all class-valued locals and treating constructor completion as implicit receiver use. `destruction-edge-corrected.txt`:43/43, clean Debug build in `destruction-edge-build.txt`. Initial enabling build failed CS0136 owner-name collision; renamed destroyedClass, preserved failed log. Earlier refused edit/fixture indentation corrected before tests.

Lesson: implicit object results and delayed initialization participate in lifetimes even without an explicit source read. Evaluation temporaries are lifetime-bearing too; checking only named locals misses receiver/RHS and later-argument hazards.

Retained Debug job1445 completed exit0; summaries independently read in job1449:79/79 semantic with chapter8 selection,75/75 ch05,250/250 ch06,102/102 ch07,16/16 both-layout executions; whitespace clean. No Release/full Unicode/PEG/sanitizer run for this slice. No backend destruction, free-list reuse, final hooks, counted ownership, array-field emission, or complete lifetime-safety proof.

Next: extend both allocators with per-slot state/free-list metadata, allocating freed slots before extending backing. Keep final/counting/owned-field release explicit separate remaining work; never compact live slots or revive stale aliases.

## Scalar ordinary destruction and free-slot reuse

Implemented in both layouts in `Emission/Compilation.cs`. A private per-class
metadata array holds slot live state and next-free links. It may relocate because
source borrows never address it. AoS retains fixed 64-record chunks; SoA retains
separate growable field arrays. Allocation pops the free head before touching the
high-water mark or backing growth. Only fresh slots advance `_next`; reuse resets
fields before constructor execution. Destroy evaluates the receiver once, consumes
the semantic `ClassOperation.Destroy` fact, defensively rejects nonlive/out-of-range
slots, and links the slot once. No backend method-name rediscovery. Internal
UINT64_MAX free-list sentinel does not settle public null/index capacity semantics.
The live flag will need a destroying state when final hooks are enabled. No final,
counted ownership, owned array field release, or shutdown backing teardown is
claimed here. Runtime guards do not replace static stale-alias rejection.

Public `layout.py` expanded from four to seven cases: reconstruction, 257 reuse
cycles, helper destruction, plus existing construction/identity/growth cases.
Saved `reuse-baseline.txt` failed at the intended old emission guard. Fresh Debug
build then passed 28/28 executions (both layouts, O0/O2), zero warnings/errors.
New public retained `allocation.py` wraps real generated-C malloc/realloc and
compares one versus 257 cycles without private emitted names: all four comparisons
passed, AoS [3,3] and SoA [2,2] at each optimization. This establishes allocation
reuse for these workloads, not leak freedom, peak memory, or performance.

Job 1464 completed exit0; saved summaries independently read in job1475: 43/43
class checks, 75/75 Ch5, 250/250 Ch6, 102/102 Ch7. `git diff --check` clean. This
slice did not rerun Release, full Unicode/parser/PEG, or sanitizers. Grader README
updated to remove the stale unsupported-destruction claim and document allocation
probe command and evidence limits.

Next: final hooks before owned-field and counted cleanup. Inspected spec
1258–1294: at most one hook, unit return, fields live during hook, transitive
same-class destruction forbidden, effects included in generated destructor.
Parser inspection disproved my hypothesis that modifier precedes name in children:
name stays child0; signature checking currently rejects the later modifier child.
No parser rearrangement is needed. Existing fixed-point class effects can enforce
same-class prohibition, but generated destructor must include hook effects.
Open language question raised to Bill before implementation: may source explicitly
call a final-marked method? Recommendation: reserve it for generated destruction;
use separate ordinary methods for explicit resource-close operations. The inspected
spec defines automatic invocation but does not explicitly settle this callable
surface. No final checker/backend edits made yet.

## Ordinary final hooks: agreed callable boundary and implementation

Bill agreed final hooks may only be invoked by generated destruction, never an
explicit source call. Added this at the original final-hook contract in the spec
and in public grader documentation; explicit resource close remains an ordinary
method. No complete Chapter8 teaching body exists yet; this is construction
material for it, not a completion claim.

Added shared declared queries SemanticSymbol.IsFinalHook()/FinalHook(), implemented
in Semantics by traversing the existing declaration/member graph. No extra stored
hook pointer, copied ownership, or registry. Signature checking accepts the final
modifier only on class methods, requires self-only/unit, rejects final constructor
and multiple hooks (LYC333). Explicit method calls reject LYC332. After class-effect
fixed point, any hook whose effects include its own class rejects LYC334, even
without a subsequent source use. A destroy event imports its hook's effects before
own-class invalidation; this propagates other-class destruction and cross-class
hook cycles through the same fixed point. Future counted releases/owned fields
must extend these generated effects, not bypass them.

Public static baseline 43/54: eleven new cases all hit unsupported Modifier, old43
passed. Corrected build clean and54/54. Runtime baseline then hit explicit staged
final-emission rejection. Backend removed that guard only when invocation existed:
function prototypes precede class storage routines; slot state free/live/destroying;
destroy requires live, marks destroying, calls resolved hook, rereads metadata via
index, then frees slot. Hook may allocate and relocate metadata even in AoS, so no
metadata pointer survives it. Ordinary fields remain available during the hook.
Reentry/nonlive guard remains fatal LYR009; it is defensive, not stale-handle proof.

Four new layout programs cover field reads, bare return, nested other-class final,
and 257 same-class allocations during hook then self-field read. Job1496 exit0:
44/44 executions AoS/SoA O0/O2,4/4 allocator comparisons unchanged [3,3] AoS/[2,2]
SoA. Saved final-check-build/final-check-first/final-layout-baseline/final-runtime-
build/final-layout-first/final-allocation logs. Compiler build zero warnings/errors.
Lesson: indexed source identity does not permit keeping even private allocator
metadata pointers across hook calls; recompute from the index after user code.

Job1497 completed exit0; saved summaries verified: sanitized44/44 layouts,
54/54 static cases,79/79 Ch4 (--chapter8),75/75Ch5,250/250Ch6,102/102Ch7 and
whitespace passed. Sanitizers were ASan+UBSan with no recovery, Apple Clang on this
host; these checks are not leak freedom or general platform proof. No Release/full earlier
Unicode/parser/PEG run for this slice. Counted classes, owned array fields, field
borrows/growth effects, shutdown backing cleanup still unfinished.


## Owned array and string fields

Added six public layout cases before implementation: independent scalar/nested
copies, returned value surviving destruction, self-assignment, final reading a
string, and repeated slot reuse. Baseline job1506 hit the intended owned-field
emission guard, not a parser or checker error. Removed that guard only with two
backend changes: Member reads use the same recursive Copy as local reads, and
class destruction drops each owned array field after the final hook and before
slot reclamation. Existing typed-zero initialization supplies empty descriptors;
existing assignment evaluates the RHS before dropping the old value and moving
its replacement. Each field is resolved again from the object index after hook
code, which may relocate SoA backing. No persistent model relationships changed.

Job1508: clean Debug build,68/68 layout executions. Added public ownership.py
wrapping real malloc/realloc/free, comparing residual block counts after1 and257
cycles involving nested arrays, strings, replacement, self-assignment, and reads
inside final. Job1510 exit0:4/4 comparisons, [3,3] blocks for each layout at each
optimization;68/68 ASan+UBSan executions;54/54 static checks. Residual class
backing remains allocated; this is bounded payload-balance evidence, not universal
leak freedom. Two additional layout cases cover array replacement after RHS class
growth and final reading an owned field after same-class growth, yielding76
executions. Job1514 completed exit0: expanded sanitizer76/76, retained
Ch4 (--chapter8)79/79, Ch5 75/75, Ch6 250/250, Ch7 102/102. Saved summaries
read after completion. These are Debug results on this host, not Release or the
full earlier Unicode/parser/PEG suites.

Lesson: an array field descriptor is not itself an independent value copy.
Ordinary reads must recursively copy payload ownership; finalization must observe
live fields before generated teardown releases them. Readback of WritableRoot
confirms indexed field mutation remains unsupported; field borrows are not enabled
by this emission change. Counted classes remain explicitly blocked.

Next counted work needs checked cleanup effects, not just runtime counters:
CheckBorrows.BuildBlock currently has no scope-release events. Assignment and
block/return/break/continue cleanup may run a counted final and invalidate other
classes. Emitter Own/Moved/Cleanup currently handle only arrays; parameter/self
ownership and returned/moved holders must be treated deliberately. Inspections
of combined emitter ranges clipped; reread narrowly before editing those paths.

### Counted cleanup effects — first static slice

Added eight public checking cases: helper fallthrough/return, branch exit,
replacement, break, continue, discarded temporary, and retained counted alias.
Baseline `counted-effects-baseline.txt` passed 55/62: all seven new negative
cases were incorrectly accepted; retained-alias control passed. No parse errors.

`IsCountedClass()` queries the authoritative class modifier through the existing
symbol; no policy field or ownership relation was duplicated. Temporary cleanup
frames in the borrow CFG now record owned parameters and locals, release on
block/control-transfer exits, and propagate potential final-hook effects.
Constructor/final receiver is borrowed rather than an owned parameter. Counted
finalization contributes to the final same-class prohibition but does not mark
other retained counted holders invalid. Replacement and discarded-result release
are represented explicitly. Clean Debug build and 62/62 public checking cases
passed (`counted-effects-build.txt`, `counted-effects-first.txt`).

This is not complete counted checking or emission. Nested expression temporaries,
returned-value preservation across cleanup, uninitialized-holder precision, and
move-aware cleanup still need alignment with backend ownership. Existing array
emission keeps unused temporaries until scope exit; counted discarded results are
planned to release immediately to match the newly explicit graph event. Counted
emission remains blocked. Retained Debug Ch4/Ch7 verification job 1528 is pending
at this writing; no Release/full Unicode/parser/sanitizer claim for this slice.

Job 1528 completed exit 0: saved summaries verify Debug 79/79 Ch4 and
102/102 Ch7. Other retained suites were not rerun in this first counted slice.

Added three boundary checks: owned-parameter release, implicit same-class release
inside a final hook, and ordinary object return across destructive cleanup.
The first two passed immediately; the return case was incorrectly accepted
(`counted-return-baseline.txt`, 64/65). The CFG now uses the preserved object
result after cleanup, and similarly checks implicit constructor self after
cleanup. Clean Debug build and 65/65 checks passed (`counted-return-build.txt`,
`counted-return-check.txt`). No counted backend claim: temporary-holder/move
alignment and initialization precision remain unfinished. The preceding 79/102
retained runs predate this final small return-order change.

### Counted temporary receivers and loop-condition cleanup

The counted backend remains blocked. Static cleanup was extended in two bounded steps before enabling it:

- Added four public receiver cases. Baseline `counted-temporary-baseline.txt` passed 68/69: temporary method receiver cleanup already arrived through the callee's owned-self summary, but a computed field receiver was never released in the graph. A field selection now registers a computed counted receiver with its enclosing cleanup frame. Named receivers (including grouped names) remain borrowed for selection. Clean Debug build and 69/69 checks are saved in `counted-temporary-build.txt` and `counted-temporary-check.txt`.
- Added three loop-condition cases. Baseline `counted-condition-baseline.txt` passed 69/72: false exit, normal back edge, and continue all missed release effects from condition temporaries. The checker now creates a condition cleanup frame, matching the emitter's existing scope. False-path cleanup is separated from the true path; normal back edges clean the frame, and existing break/continue cleanup depths include it. The frame is registered before condition traversal so control transfers from within a value-producing condition still have the correct target and cleanup boundary.

Condition correction verification completed: job 1550 exited zero; saved summaries were read in job 1554. Clean Debug build, 72/72 Chapter 8 checks, 79/79 retained semantic checks with chapter=8, and 102/102 Chapter 7 array checks passed. No counted runtime emission, Release/full cumulative suite, or sanitizer evidence is added by this slice. General temporary moves, returned/branch values, and class-containing arrays remain unfinished ownership work.

### Empty versus occupied counted holders

Three positive controls exposed false cleanup effects: an uninitialized local, an uninitialized local inside a same-class final hook, and the first assignment to an empty local. Baseline `counted-empty-baseline.txt` passed 72/75; all three were incorrectly rejected with lifetime/final diagnostics. The cleanup inventory now holds temporary holder identities rather than only types. A third worklist state tracks possible occupancy, joined by union. Declaration events reset empty storage on each execution, initialization occupies it, and cleanup/replacement releases only a possibly occupied holder. Releasing empties the holder. This remains separate from source definite-assignment checking, which determines whether reads are legal. Parameters start occupied except borrowed constructor/final self; computed field receiver temporaries are occupied. Non-counted types do not need these holder identities.

`counted-empty-build.txt` is a clean Debug build and `counted-empty-check.txt` passes 75/75. Two further public branch cases require release after optional initialization and permit its empty-path control. Job 1561 completed with exit zero; saved summaries read in job 1564 confirm 77/77 Chapter 8 checks, 79/79 retained semantic checks, and 102/102 array cases. Source/grader/notes whitespace validation passed. No new runtime support or safety proof is claimed. Counted C emission remains blocked pending agreement between all ownership transfers and graph cleanup.

### Counted conditional-tail transfer

The lifetime graph treated every expression statement as a discarded value, including the final expression of a value-producing conditional branch. Added two public checks: transfer a newly constructed counted value from either branch and read an ordinary object before holder cleanup (valid); discard the branch values before the read (invalid because their final hooks destroy that ordinary class). The unchanged compiler passed 78/79: only the valid transfer was falsely rejected. Evidence: `counted-conditional-baseline.txt`.

`BuildIf` now passes the checked non-unit result distinction into `BuildBlock`. A genuine final value tail skips the discard release; local block cleanup still runs. A trailing discard marker prevents tail transfer. This matches existing semantic checking and C branch-result moves, without changing function/loop block behavior. Clean Debug build: zero warnings/errors; 79/79 checking cases pass (`counted-conditional-build.txt`, `counted-conditional-check.txt`). Retained Debug verification also completed: 250/250 expression cases and 102/102 array cases pass (`counted-conditional-expressions.txt`, `counted-conditional-arrays.txt`). Scoped whitespace checks passed. No Release or sanitizer run was performed for this change.

Lesson: ownership transfers, not expression-statement syntax alone, determine whether to release a result. Counted runtime emission remains blocked. Further integration must cover general temporary moves, local copies returned through cleanup, and recursive class-element arrays before claiming complete counted safety. Existing emitter scope helpers own only arrays. Ordinary counted method self must own the transferred caller holder; constructor/final self remain borrowed. Proposed backend empty-holder representation is the allocator's already-reserved UINT64_MAX, not source-level nullable references. No backend edits made in this slice.

### Counted backend holders and matched cleanup boundaries

Added public layout cases before enabling counted emission; the baseline `counted-layout-baseline.txt` failed at the intended LYC306 gate on `counted-alias`. The backend now uses the allocator's reserved UINT64_MAX as an internal empty-holder sentinel, not a source null value. Counted slot metadata holds the authoritative reference count alongside existing state/free-list metadata. Allocation initializes one holder; copies retain with overflow checking (LYR010); moves empty the source; release empties the holder before decrement/finalization. Zero count invokes the existing final/owned-field/free-slot destructor. Metadata pointers do not survive user final-hook calls.

Extended the existing Own/Moved/Cleanup paths rather than adding another scope manager. Named field receivers borrow a snapshotted index; computed receivers own a temporary. Ordinary method self and non-ref arguments transfer to the callee, while constructor/final self remain borrowed. Named counted reads retain, groups transfer, binding/assignment/branch results move, discarded counted results release immediately, and returned holders survive function cleanup. Empty declarations initialize to the sentinel; replacement evaluates and retains the RHS before releasing the old holder. Cleanup diagnostics use the containing source function where no single expression site exists. Class-element arrays remain explicitly unsupported; owned scalar/nested arrays and strings inside objects retain their existing independent-value behavior.

Three final-self controls passed: copying/passing counted self during finalization is rejected by existing LYC334 effects, while direct field reads remain permitted (82/82 checks). The first ownership integration build failed with CS0136 from a colliding local `result`; renamed it `fieldResult` and removed the superseded member-expression branch. Failure log `counted-ownership-build.txt` is retained; the corrected build passed.

Inspection found two real safety-graph/backend cleanup mismatches. The right operand of &&/|| and an else-if condition have conditional C scopes, but their computed receiver holders were initially registered in the outer graph frame. A final hook could therefore run earlier than the checker modeled. Short-circuit baseline was 83/85 (both unsafe cases accepted); else-if baseline was 86/87 (unsafe case accepted). Added matching temporary ownership frames, cleanup before path merge, and named-receiver controls. Lesson: every emitted cleanup boundary must have its effects at the corresponding graph boundary, including compiler-introduced conditional scopes.

`counted-expanded-build.txt` is a clean Debug build; `counted-expanded-static.txt` passes 87/87; `counted-expanded-layout.txt` passes 124/124 executions (31 cases, both layouts, O0/O2). Earlier `counted-enabled-initial.txt` passes the six initial execution cases. New layout cases cover aliases, method self, returns/groups, conditional transfer, empty holders, self-assignment/replacement, break/continue, computed fields, and conditional cleanup order. These order checks target this immediate-release backend, not a universal total order for unrelated finalizers. Retained regressions/balance job 1626 and layout sanitizers job 1627 are pending at this note; no results inferred. No Release/full cumulative/PEG or Chapter 8 review/student claim yet.

The retained/balance run (job 1626) completed with exit zero: 8/8 ordinary+counted owned-field balance comparisons, 79/79 semantic, 75/75 Chapter 5, 250/250 Chapter 6, and 102/102 Chapter 7 cases. Job 1627 completed with exit zero and saved 124/124 layout executions under ASan+UBSan. These results precede the next receiver correction; no Release/full Unicode/parser/PEG claim. The balance grader now tests both ordinary explicit destruction and counted scope cleanup, retaining its real malloc/realloc/free wrapper and comparing one versus 257 cycles.

A further inspection found that Select(Member), used for field-assignment destinations, evaluated a computed receiver but omitted its temporary holder from the safety graph. Public `counted-assignment-receiver-cleanup` was wrongly accepted: baseline 87/88 (`counted-assignment-receiver-baseline.txt`). Extracted RegisterComputedReceiver for both field-value reads and destination selection; named/grouped-name receivers remain borrowed. Added a runtime case whose final observes the assigned field. Job 1634 completed with exit zero; saved summaries confirm a clean Debug build, 88/88 static cases, and 128/128 layout executions. Lesson: storage-selection evaluation can own temporaries too; it must participate in the same cleanup analysis as value evaluation. The latest receiver correction has not rerun the retained suites or sanitizers; their results above apply to the preceding slice.


### Scalar class-field borrows and ordered growth effects

The first eight public field-reference cases failed at the explicit Member
boundary (88/96). The checker now admits initialized scalar field references.
It reuses the existing temporary storage-path fixed point: negative path depths
mark class-wide growth/destruction effects, while nonnegative depths remain
array storage paths. Class roots are actual nominal symbols, not reconstructed
names. Growth does not invalidate indexed object handles. Class-field address
uses and unknown reference parameters retain effect-before-use causes; formal
obligations are substituted at callers. Both layouts use the same safety rule.
Plain field assignment saves an index, so its destination is excluded from
pointer invalidation and still uses receiver lifetime checks. The initial patch
reported LYC326 instead of the retained LYC330 for destruction on an assignment
RHS (95/96); correcting that distinction restored the established diagnostic.

Ordered helper controls passed, but constructor reference arguments exposed a
real hole (100/101): argument selection precedes allocation, while user body use
follows it. The call graph now rechecks selected reference arguments after the
implicit allocation event. No allocator AST or second checker was generated.
The existing backend storage-address path required no emitter changes.

Clean Debug build, 101/101 checks and 136/136 layout executions verified in
field-borrow-final-build/check and field-borrow-layout logs. Added six controls
for later arguments, final/release growth, loop backedges, alias non-revival and
counted fields.
The six additional controls passed: 107/107 Chapter 8 checks. Retained Debug
semantics passed 79/79, expressions 250/250, and arrays 102/102 (job1651 exit0).
The same 136 layout executions passed ASan+UBSan (job1652 exit0); saved summaries
were verified. No Release/full Unicode/PEG run for this slice. Array-field
descriptor/payload references remain gated; do not infer support or complete
safety from scalar-field results.


### Approved assertions and observable capacity

Bill approved `capacity(array) -> u64`, independent copies with capacity equal
to length recursively, and implementation of the already specified resize(0)
release. Nine public Chapter 7 cases established a 102/111 baseline. Copy was
already tight; capacity now observes selected descriptors rather than copying
an argument, while computed array values are evaluated normally. Resize-zero
uses recursive drop after shrink, including reserved-empty descriptors. An
initial builtin dispatch dereferenced a missing function symbol on array-method
calls; the guard now tests optional builtin identity. Corrected Debug array
suite passed 111/111. The new cases now use explicit assertions.

Bill proposed assertions to simplify tests, approved the always-on contract,
and required teaching at the original construction points. Chapter 4 declares
assert(bool)->unit; Chapter 5 constructs the once-evaluated helper and located
LYR010 fatal trace; Chapter 6 retains it in unsafe builds. Five runtime cases
established 75/80 baseline. The first semantic grader addition omitted its two
required location anchors (KeyError); corrected fixtures pass 82/82. Runtime
passes 80/80, unsafe 87/87 including false assertions with NDEBUG at O0/O2 in
both layouts. Debug builds were clean. Broader cumulative verification follows.
No frozen student reruns or performance claims follow from these additions.

Bill also authorized proactive language simplification proposals followed by
immediate implementation upon approval; the agreement is saved in authoring.md
and linked from the book index. It does not grant unapproved redesign authority.

The cumulative intermediate version passed make test in Debug and Release
(job1746): source/Unicode/tokens/parser/semantics82/runtime80/expressions250/
unsafe87/arrays111. During review, a temporary probe showed capacity's nested
selection omitted storage invalidation checking: a later index could resize
away an earlier selected descriptor. The probe was checked, never executed.
ArrayDescriptor now snapshots each indexed descriptor address; ObserveArray
uses the existing borrow graph, leaving computed arrays as owned temporaries.
A public negative regression requires LYC326. Its first expected location used
the bracket, but Index has no separate token anchor; the established diagnostic
anchors the indexed expression start. Corrected fixture, clean Debug/Release
builds, and 112/112 arrays in both configurations passed (job1759). The full
cumulative log predates this narrow correction; no new sanitizer/PEG/performance
claim. All logs, including failures, are in ch08-checks/capacity-assert.

## Array-field references: descriptor and payload dependencies

Enabled the pending twelve cases using the existing path graph, not a separate
borrow mechanism. Checked selections supply the authoritative field symbol;
writable-root checks now follow selection checking and reuse its fact. Field
mutations and field/formal ordered obligations propagate through helper and final
summaries. Class growth invalidates depth-zero field descriptors, destruction all
owner-dependent depths. Independently allocated payload can survive class growth.

The first build exposed a removed nullable-type assertion; a checked field-type
local corrected it. First checking run was 118/119: exporting constructor self
initialization as an existing-field mutation falsely rejected payload survival.
Direct fresh-self stores now retain local invalidation without exporting that
store as mutation of older instances. The constructor-local regression proves
that replacing self.a still invalidates an earlier local borrow of its payload.

Verified clean Debug build, 119/119 original then 122/122 expanded checking cases,
160/160 layout executions (40 programs, AoS/SoA, O0/O2), and 112/112 retained
array cases. No emitter changes for this slice: existing pointer capture versus
indexed member-store resolution already implements the distinction. Evidence,
including failed build and 118/119 result, is under ch08-checks/field-array-references.
Full cumulative, Release, sanitizer checks and chapter teaching/reviews/student
remain separate work at this point. No performance or general safety proof claim.

## Object arrays: approved scope and first implementation

Bill approved including ordinary and counted class-handle arrays in Chapter 8.
This extends local values, parameters, returns, literals, nesting and array
operations, not persistent object-to-object fields or relationship graphs.
The specification already excludes default values for non-nullable handles:
reserve and push are valid; resize may shrink but must fail before default growth.
The backend reports located LYR011 for growth in safe and unsafe modes.

Array slot copies preserve object identity. Ordinary handles remain non-owning;
counted leaf copies retain, while recursive drops release. Generated class-specific
array helpers call the existing class retain/release operations; there is no
runtime type registry or duplicate liveness state. Indexed counted reads retain
before their temporary parent array is released. Array moves empty their source
cleanup obligation, including literals, calls, bindings, returns and value tails.

The original six new semantic cases failed at the array gate (122/128). The
first staged semantic version passed 128/128 with emission explicitly gated.
The backend then passed 192/192 layout executions. One initial C# build failed
because the generated diagnostic string was misescaped; correcting that string
allowed the runtime checks to run. Later growth failures are checked for exact
LYR011, exit 1, empty stdout, located diagnostic and sole main frame—not arbitrary
failure. Both modes and layouts are covered. The expanded layout run passed
208/208 under ASan/UBSan on the existing Apple Clang ARM64 host; retained Chapter 7
arrays passed 112/112. This is not a portable safety proof.

Safety analysis uses the existing temporary object identities for array values,
so class destruction invalidates later ordinary contained-handle uses. Counted
array holders use the existing possible-occupancy worklist. The 135/137 effects
baseline exposed false finalization of [] and [[]] inside a final hook. Known-empty
literal construction starts empty; insertion, nested stores and helper mutation
can populate it. Partial shrink releases possible leaves without declaring the
remaining array empty; whole zero shrink empties the holder. Controls for later
population passed, reaching 141/141.

A source review found that inline len does not consume its argument: its evaluated
array temporary remains caller-owned. The first generic call-transfer graph
incorrectly disarmed it. A public negative confirmed unsafe acceptance (142/143;
capacity control already rejected). Restricting transfers to ordinary callees
restored the expected rejection, 143/143. Lesson: builtin lowering and ownership
must agree at the actual cleanup boundary, not merely share call syntax.

The ownership wrapper now also exercises counted nested arrays, copies, pushes,
element replacement, shrink and scope exit. All 12 policy/layout/optimization
comparisons passed: one versus 257 counted-array cycles left 3 versus 3 outstanding
backing blocks. This is bounded payload-balance evidence, not shutdown reclamation
or universal leak freedom. Whole-array replacement and slot-reuse controls are
being checked next; final cumulative verification, full teaching, reviews and
student reconstruction are still pending.

The final focused replacement baseline was 144/145: whole-array replacement
incorrectly treated selecting a local descriptor as reading stale contained
handles, while the old-array-after-slot-reuse case correctly rejected. Restricting
contained-handle use to deeper storage selections permits whole-value replacement;
ordinary value reads still check their object identity. The final checking suite
passed 145/145. A matching runtime case brings the layout inventory to 49 success
programs plus two growth-failure programs in both modes: 212 executions. All 212
passed under ASan/UBSan in the final focused run. No Chapter 8 review, student
reconstruction, or human acceptance is implied. Cumulative Debug/Release checks
are the next verification step.


## Full object-array cumulative checkpoint and construction draft

Job 1831 completed with exit 0: Debug and Release each passed all retained checks, including 145 static class cases, 212 layout executions, four free-slot comparisons and twelve ownership-balance comparisons; separate PEG 143/143. Full logs saved in ch08-checks/object-arrays/cumulative.txt and peg.txt. Chapter 8 construction draft now exists. The new spin08 walkthrough passed both layouts after a clean Release build (job1845); scripted use is not human acceptance.

Source reading found reference-count overflow reused assertion code LYR010. Changed only that overflow diagnostic to unused LYR012; assertion remains LYR010 and non-defaultable array growth LYR011. No claim of reaching 2^64 owners in a runtime test. Generated-code spelling is checked separately; normal ownership executions remain the applicable runtime tests. Reviews and reconstruction have not started.

## Consolidated code review corrections

The single code reviewer found two implementation defects: conditional class-valued tails acquired fresh validity after branch cleanup, and whole-array writes through aliases did not populate the selected owner's cleanup holder. Nine public cases established a 148/154 baseline, with six failing variants and safe controls. A shared conditional result identity now crosses cleanup and is checked afterward; assignment occupancy uses selected root and depth, including chained and nested aliases. Debug rebuilt cleanly and passed 154/154. The graph event vocabulary is documented beside its declaration. The consolidated report is `ch08-code-review.md`; no second code review is requested.

Lesson: transfer cannot restore validity, and aliases do not change storage ownership. These explanations were integrated into the chapter's cleanup construction, not appended as student repair history.

The reviewer identified an older 208/208 sanitizer receipt. A fresh full 212-case sanitizer run is scheduled with cumulative verification below; until its receipt is collected it is not a pass. Earlier full cumulative results preceded these corrections and remain historical evidence.

## Review corrections verified and reconstruction prepared

The single code review is retained in `ch08-code-review.md`; its two required lifetime fixes pass 154/154 static cases. Job 1925 completed with exit 0: the full cumulative suite passed in Debug and Release, including all 154 static cases, 212 layout executions, four free-slot comparisons, and twelve ownership comparisons in each configuration. A subsequent ASan/UBSan layout run passed the current 212/212 executions. Logs and extracted summaries are in `ch08-checks/review/`. This supersedes the narrower earlier sanitizer inventory, not a general memory-safety proof.

The single text review is retained in `ch08-text-review.md`; the chapter now names both Chapter 8 cumulative selectors, shows final-hook syntax, states the explicit-constructor boundary, and traces alias-selected ownership. Both reviewers are stopped; no re-review was requested. The fresh public reconstruction bundle contains 234 files, including 27 accepted independent Chapter 7 compiler inputs. It excludes the reference compiler and private notes; prerequisite refresh is explicit. Its INPUTS.sha256 digest is `97c6cb9750c35d7c91a2600ca2b61b33be5a2b1059495efd864608636f8c69c9`. Preparation is not student completion.


## Reconstruction clarification: unsafe helper identifiers

The fresh student identified that `graders/ch06/unsafe.py` required private helper names (`ly_i32_add`, `mul`, `div`) and assumed their unused definitions remained in unsafe output. Its accepted prerequisite used different helper names. Neither naming nor retention of unused definitions is a public requirement. The orchestrator removed the name-count assertions and corrected the grader README; valid execution, exceptional emission-only, and retained safety checks remain. Static-inline helpers and bypass of checked helpers remain source-review construction obligations, not claimed automated proof. No student implementation was edited or supplied. The public bundle changed only these two grader files and its manifest; all 234 hashes verify. Original manifest digest remains recorded above; corrected digest is `23122938012fb283140416a1f9e142a97ff41e35f2ca6153823e680c6e686e03`. Reference verification of the corrected grader is saved in `ch08-checks/review/unsafe-grader-correction.txt`.

Lesson: generated identifiers are implementation details unless explicitly published as an interface; reference-shaped text tests can reject a valid independent construction. This was a grader correction during reconstruction, not unassisted success.

## Reconstruction clarification: constructor mutation provenance

During independent source review, the fresh student reported that its checker suppressed all same-class field mutation summaries inside constructors, including effects on existing parameter objects. Its current public static154/runtime212/allocation4/ownership12/basic6 checks had passed, with cumulative verification still running. The parent confirmed that existing same-class parameters and their mutations are in scope: only effects proven confined to fresh self storage can be omitted outward, while local invalidation still applies. The student owns the correction and verification; no implementation or target source was supplied. The construction chapter now explains this at the helper-summary rule. Frozen public inputs were not changed for this prose clarification. This is student-discovered implementation risk plus parent scope clarification, not completed correction evidence.

Lesson: being inside a constructor is not proof that a mutation targets fresh storage; provenance must distinguish self from existing objects, including aliases and helpers.


## Reconstruction completed and verified

The fresh student independently corrected constructor freshness provenance, then passed the final cumulative suite in Debug and Release, separate PEG143, and spin08. Parent read final receipts and summaries and verified all 28 source/project and 10 artifact hashes without rerunning the candidate. The child is stopped. See `ch08-student-report.md` and `ch08-checks/student/` for results, assistance, and limits. The public static inventory remains 154: the student's additional constructor counterexample is diagnostic evidence, not silently promoted to a new completion gate. No student sanitizer run or human acceptance is claimed.

The reference implementation is not replaced by the independent student. Constructor-provenance teaching is integrated; any future retained-case extension should preserve this completed result rather than trigger an automatic student rerun.


## Student-feedback integration audit

Compared all six student lessons with construction prose: capacity/grouped selection in Chapter 7; exceptional unsafe folding in Chapter 6; discard cleanup, validity/occupancy, constructor provenance, and bounded allocation evidence in Chapter 8. Added the student's existing-object constructor counterexample beside the provenance rule, and explicit private-helper naming/source-review guidance in Chapter 6. This is teaching integration, not new grading or another execution of the passing student.
