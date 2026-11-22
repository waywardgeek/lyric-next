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
