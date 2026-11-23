# Initial array ownership milestone

The independent ownership grader passes all eight retained cases in Debug and
Release. Both deliberate defects (Copy replaced by shared Load; removed release)
are detected. Full cumulative `make test` job 2341 completed with exit 0; both
builds reported zero warnings/errors and passed 18 scalar IR and 8 ownership IR
cases alongside the retained compiler suites. Full log: ownership-cumulative.txt.

Cleanup order was corrected during source readback to reverse declaration order,
matching the existing C emitter, before the cumulative run. Allocation balance
alone would not establish observable finalization order.

This checks inspected IR through an independent small evaluator. Production C
emission is unchanged. Loop ownership, selected storage, references, classes,
full validation and IR-backed C emission remain unfinished. No benchmark,
sanitizer, reviewer or independent reconstruction was run for this milestone.

## While ownership extension

Seven additional retained cases cover repeat, break, continue, return, nested
return, false condition, and nested-loop transfers. Baseline had eight passes and
six intended LYC340 failures before the nested-return case was added. Baseline
shell receipt was the final log-display status, not the failing grader status.

Job 2350 built Debug cleanly and passed the initial 14 ownership / 18 scalar IR
checks. Job 2351 completed exit 0: final 15 ownership cases pass Debug and Release;
Release build is clean; 19 IR checks pass with the remaining milestone-only class
rejection. Logs are ownership-loop-*.txt. No full cumulative rerun after this
loop extension yet. Job 2352 verified whitespace, including untracked new files.

Condition Yield preserves its holders through the selected body. Explicit frees
occur after the body, before break/continue, on return, and following the loop for
false condition. Empty repeat cleanup after break is allowed. Return traverses
loop condition scopes as well as body ancestors: condition is a sibling, not a
parent. The independent evaluator rejects live-holder overwrite on iterations,
uses a bounded execution budget, and the grader checks absence of premature
condition-holder cleanup. This is not counted-finalizer or C-emission evidence.

## Indexed storage extension

Seven retained cases add scalar reads/writes, independent copies, nested writes,
owned nested replacement, index calls and destination-before-RHS ordering. The
first baseline had 15 passes and seven failures; six were intended LYC340 lowering
rejections, but indexed compound assignment was already rejected by semantics
(LYC306). That proposed positive fixture was removed rather than expanding the
language. The baseline remains in ownership-index-baseline.txt.

SelectIndex captures an element address after evaluating its index and checking
length under the safe/unsafe policy. Writable selection traverses original
descriptors; ordinary indexed expressions read copied parents and Copy owned
elements. Selection is scheduled before RHS evaluation. The Python evaluator
models element addresses separately from local holders and detects dead-address
use and replacement leaks. An ordering self-check rejects a selection deliberately
moved after an RHS call.

Job 2361 passed 21 ownership and 18 scalar cases in Debug. Job 2363 completed exit
0 after the ordering extension: 22 ownership cases in Debug and Release, 18 retained
scalar cases in Release, clean builds and git whitespace check. Saved logs are
ownership-index-*.txt. Untracked source/grader whitespace was checked separately.
No full cumulative rerun since the initial eight-case ownership milestone, and no
sanitizer, benchmark, review or reconstruction claim. C emission is unchanged.

## Non-owning reference extension

Six retained cases cover scalar aliases, forwarding, array replacement through a
reference parameter, independent alias reads, nested returned copies, and an
indexed reference captured before a later argument mutates its index variable.
Baseline job 2368: existing 22 passed, all six additions rejected with LYC340.
Borrow now represents non-owning addresses for parameters and local aliases.
Checked parameter symbols select address forwarding versus value transfer.
Load/Copy through Borrow reads values; reference holders never register cleanup.
The independent evaluator passes mutable addresses across separate call frames.

Job 2369 passed 28 ownership and 18 scalar checks in Debug, clean build. An added
structural grader assertion incorrectly prohibited FreeArray through Borrow,
rejecting valid replacement of an owned array referent (27/28, job 2371).
Preserved ownership-ref-overstrict-grader.txt; corrected assertion prohibits Move
through Borrow, while replacement release remains valid. No compiler change for
this grader correction. Job 2372 completed exit 0: 28 ownership checks in each
build, 18 scalar checks in Release, whitespace check. Release build was clean.
Logs ownership-ref-*.txt preserve evidence. Full cumulative verification remains
at the earlier eight-case milestone; C emission remains unchanged.

## Array mutations and capacity

Nine mutation cases: baseline job 2380 exit 1, 28/37; job 2381 exit 0,
37/37 ownership and 18/18 scalar in Debug, clean build. Six capacity cases:
baseline job 2384 exit 1, 37/43; job 2385 exit 0, 43/43 Debug and Release,
18/18 scalar Release, clean builds. Summaries read back in jobs 2382 and 2386.
Logs ownership-mutations-*, ownership-capacity-*, capacity-*, mutations-* retain
these checks. C emission remains unchanged. No performance/sanitizer evidence.

Checked ArrayOperation tags choose direct IR operations. Receiver descriptor is
selected before argument effects; push transfers array elements, shrink releases
removed owners. Capacity selects original descriptors without copying, with an
explicit computed-value fallback that preserves temporary ownership. Independent
evaluation tracks reservation separately, recursive releases, copies and aliases.
Full cumulative verification for this expanded slice is pending at this record.

## Expanded cumulative verification

Job 2387 completed with exit 0. The frozen mutation/capacity implementation passed the full `make test` Debug and Release suites, including 212 layout executions, 18 scalar IR cases and 43 ownership IR cases per configuration. Both builds reported zero warnings and errors. Full output: `ownership-expanded-cumulative.txt`. C execution still uses the existing emitter, not IR.

## String literal construction

Literal source bytes are already decoded. Reuse immutable IrLiteral.Bytes as a
NewArray payload, rather than emit a scalar node per byte or introduce a separate
string owner. Inspection exposes numeric bytes, not decoded JSON text. Every
execution allocates a fresh ordinary array; immutable literal data does not imply
shared runtime storage. Normal copy/move/cleanup rules remain authoritative.

Baseline job 2401 exited 1 with 43/49: six new string cases rejected by LYC340.
Job 2403 exited 0: clean Debug build, 49/49 ownership, 18/18 scalar. A seventh
repeated-literal loop case was then added. Job 2406 exited 0: Debug and Release
each passed 50/50 ownership and 18/18 scalar checks, with zero build warnings
and errors. Saved summaries were read back in job 2407; git diff --check passed.
No C emitter change and no interpolation support claimed.

## Logical field locations before class lowering

The working emitter snapshots a receiver handle before an assignment RHS, but
resolves direct field storage at the eventual access. A borrowed field pointer
instead remains fixed. IR now names this distinction: SelectField produces Place
from a saved receiver and the actual field symbol; AddressOf captures a fixed
Address. Loads/stores resolve a Place when executed. Existing indexed addresses
remain fixed. Neither Place nor Address creates ownership. Class lowering remains
gated; these declarations are not evidence of implemented relocation behavior.

The real-model development probe now checks place/address operand backlinks and
non-cascading deletion. Both configurations passed 29 checks with zero warnings
and errors (place-model.txt; job 2431, summaries read back afterward). First the
fixture used nonexistent SymbolKind.Variable, corrected to Field. Then creating
that typed semantic declaration after IrProgram invalidated the derived IR via
SetType; the fixture now establishes semantic declarations first. Both failed
logs are retained. Lesson: checked semantic identities precede IR construction;
mutation invalidates derived execution rather than silently leaving stale nodes.
The C# probe is development evidence, not an independent chapter grader. No class
execution, full validation, sanitizer, benchmark or emitter migration is claimed.

## Ordinary scalar-field classes

Class declarations now establish method bodies before calls lower. The bounded
slice accepts ordinary scalar-field classes, aliases, methods, returns and explicit
destruction. Counted classes, object arrays, final hooks and owned array fields
remain LYC340 gates. NewObject allocation follows explicit argument evaluation;
an ordinary Call executes the resolved constructor with self. Constructor body
unit and constructor expression handle are distinct. Method self executes before
later arguments. No source-name lookup is repeated by lowering.

SelectField yields a logical Place using an evaluated handle and actual field
symbol. Loads/stores resolve it at execution; AddressOf captures a fixed field
address for Borrow. The independent Python evaluator models relocation on every
class allocation and rejects stale captured addresses. Symbol IDs are document-wide
inspection labels for actual declarations: two read methods must not collide.
The production graph still holds symbols, not numeric IDs or reconstructed names.

Initial ten fixtures all failed semantics because the common method fixture used
unsupported field compound assignment. Preserved ownership-class-fixture-failure.txt;
changed it to explicit read/add/store and likewise changed the growth-snapshot case.
Corrected baseline job 2452: existing 50 passed, ten class cases rejected LYC340.
This was a fixture correction, not a new language feature. Job 2456 then passed
60 ownership and 18 scalar cases with a clean Debug build.

Three further cases exercise method receiver snapshotting, constructor argument
effects and method array ownership. Job 2460 exited 0: Debug and Release each pass
63 ownership cases. Self-checks reject premature field-address capture, wrong
same-name method binding and omitted explicit destruction. Release passed 18
retained scalar checks plus the counted-class milestone rejection (19 total).
Both builds reported zero warnings/errors; saved summaries were read in job 2464.
Logs: ownership-class-*, classes-*. Public README records the contracts and cases.
The optional old all-class rejection is superseded, not retained as a prohibition.

No C emission switch, full validator, sanitizer, performance measurement, review or
student reconstruction is claimed. Full cumulative evidence still ends at the
43-case ownership slice; later strings/classes have focused two-build verification.
Ordinary-object fixtures may intentionally leave objects alive; no automatic
ordinary destruction is invented. Next design boundary: reusable generated
class teardown bodies, explicit final calls/field releases and counted transitions,
with actual class backlinks rather than fabricated source declarations.

## Generated destruction body relationship

Bill approved one generated destruction function per class, strongly owned by the
IR program and weakly linked in both directions to the actual class. `IrFunctionKind`
distinguishes Source and Destruction. Source bodies retain their existing
`Declaration`/`IrBody` relationship; generated bodies instead use
`DestructionClass`/`IrDestruction`. No synthetic source declaration is created.
Attachment validates kind, uniqueness and analysis identity before publishing either
endpoint. Failed construction unwinds program membership. Destruction removes the
class backlink and destroys the function's node arena without destroying the class.
Semantic invalidation still retires the whole derived IR before changing authority.

The actual-source lifecycle probe passed 35 checks in Debug and Release with zero
warnings/errors (`destruction-model.txt`), including duplicate refusal, wrong-kind
refusal, explicit teardown and class invalidation. This verifies model relationships,
not generated teardown execution: lowering still gates final hooks, owned fields and
counted classes. Next: route existing scalar-class destruction through the generated
body before introducing hook calls and owned-field release operations.

### Executing generated destruction

Lowering now creates each ordinary class's Destruction body before lowering source
calls. DestroyObject dispatches through the actual class's IrDestruction link;
ReclaimObject is the terminal slot-release operation inside that body. The incoming
handle uses an unnamed Local, not a fabricated semantic parameter. Inspection emits
explicit Source/Destruction kinds and class identity for generated bodies. The
independent evaluator separates those dispatch maps and executes the generated body.
A self-check removes ReclaimObject and detects the surviving object, in addition to
the existing omitted-DestroyObject check.

The first build was clean, but the evaluator exposed an operand-free generated
Return. Corrected lowering to emit the same explicit unit constant/Return operand
contract used by source functions, rather than weakening the evaluator. Failed log:
destruction-return-baseline.txt. Lesson: generated provenance does not waive normal
execution contracts.

Job 2492 exited 0. Saved summaries read in job 2494 confirm 63/63 ownership and
18/18 scalar cases in both Debug and Release; both builds have zero warnings/errors.
Logs destruction-{debug,release}-{build,ownership,scalar}.txt. Whitespace check passed.
Final hooks, owned fields, and counting remain gated; no C-emitter switch or full
cumulative verification of this slice is claimed. Next: explicit final-hook call
before slot reclamation, then owned-field release and counted ownership.

### Final hooks in generated destruction

Two public cases first failed with intended LYC340 diagnostics (final-baseline.txt,
63/65). Ordinary scalar-field classes now lower their existing final hooks. The
generated body loads its incoming stable handle and Calls the actual hook symbol
before ReclaimObject. Hook source bodies use the normal lowering path. No invented
source declaration or cached interior field address is needed. Counted classes,
object arrays, and owned array fields remain gated.

The evaluator counts the fixture hook independently of generated dispatch. Both
cases require exactly one invocation; one allocates another same-class object before
reading the original field. Deliberately omitted, duplicated and late hooks are
rejected. Lesson: generated provenance does not waive ordinary execution contracts,
and cleanup must preserve live storage through the hook even when it grows class
storage. The hook is not a source-language generator.

Job 2504 exited 0: Debug/Release 65/65 ownership and 18/18 scalar checks,
zero build warnings/errors; git diff --check passed. Logs final-{debug,release}-
{build,ownership,scalar}.txt. These are focused inspection/interpreter checks,
not IR-backed C execution or a new full cumulative run. Next: owned array fields,
released after the hook and before slot reclamation.

### Owned array fields in generated destruction

The existing emitter releases fields in declaration order after the final hook.
LowerDestruction now does the same explicitly: load the stable handle, SelectField
using the actual field declaration, FreeArray, then finally ReclaimObject. No field
pointer survives the hook. Existing Store handles constructor initialization and
replacement; ordinary field reads Copy recursively. Field selections are not owners.
The evaluator models initial array descriptors as empty while retaining its scalar
uninitialized-field rejection. Reclamation does not silently release payloads.

Three new cases first failed with intended LYC340 (field-baseline.txt,65/68).
Four retained cases now cover copies, nested replacement, multiple fields, and
final-hook access/replacement. Removing generated field releases is detected as a
leak. Job2514 exited0: Debug/Release69/69 ownership and18/18 scalar, clean builds,
and git diff --check. Logs field-{debug,release}-{build,ownership,scalar}.txt.
Lesson: field ownership belongs to the object slot, not a transient selection;
resolve its descriptor after hook effects before releasing it. These are focused
IR inspection/interpreter checks, not IR-backed C execution or a new cumulative
run. Counted classes/object arrays remain gated. Next: explicit counted holders,
retains/releases, and last-release dispatch through the same destruction body.

## Counted scalar holders — construction in progress

The four initial counted fixtures preserved the 69 earlier cases and failed with
located LYC340 diagnostics (`counted-baseline.txt`, 69/73). The first implementation
passed all 73 focused ownership cases; expanded checks are pending.

Reuse the existing region owner lists: arrays release with FreeArray, counted
scalars with Unref. Ref reads create independent holders of shared object identity;
Move empties the source before the receiving operation establishes its owner.
Unref empties the holder before decrementing; zero invokes the same generated
destruction body (hook, owned fields, reclamation). Object arrays remain gated.

Constructor and final-hook self are borrowed parameters. Ordinary methods consume
a receiver holder. Field selection of a named receiver loads its handle without
retaining; computed receivers retain their ordinary temporary ownership. This is
necessary particularly inside a final hook, where retaining self would revive an
object being destroyed. Logical Place selection remains distinct from fixed Address.

Inspection exposes the checked counted-class fact; the independent evaluator
tracks counts separately from object storage and rejects reclamation with live
holders. Added follow-up cases cover conditional returns, loop cleanup, reference
replacement, and counted objects owning arrays. The optional scalar milestone
rejection now covers object arrays, not the newly supported counted scalar class.
No C-emitter switch, cumulative runtime run, or optimization is claimed here.

Counted-scalar verification completed: Debug and Release each passed 77/77
ownership cases, including deliberate missing-Ref and missing-Unref rejection.
Debug passed 18/18 retained scalar checks; Release passed those plus the temporary
object-array boundary (19/19). Both builds reported zero warnings and errors.
Terminal command 2537 exited 0; saved summaries were read back in 2540.
Evidence: counted-{debug,release}-{build,ownership,scalar}.txt. The initial 73-case
passing log was superseded by the expanded run; the failing baseline is retained.
Whitespace check passed. Shared comments and public grader contract now document
the counted protocol. No new cumulative C-runtime, sanitizer, performance, reviewer,
or student-reconstruction result is claimed. Next: object-array ownership lowering.

## Object-array lowering

Five initial fixtures preserved the preceding 77 passes and failed with located
LYC340 diagnostics (object-array-baseline.txt). Removed only the object-array type
gate; existing typed Copy/Move/FreeArray/Ref/Unref lowering composes for object
leaves. The independent evaluator now preserves leaf class/counting metadata per
array allocation: recursive copy retains counted leaves, removal/drop releases
them, ordinary handles add no lifetime ownership. The original 82-case focused
Debug run passed with no build warnings/errors. Follow-up push, replacement and
resize-zero cases expanded the Debug pass to 85. Debug and Release verification completed in command 2554 (exit 0): each passed
85/85 ownership and 18/18 retained scalar cases; Release built with zero warnings
and errors. Saved object-array-{debug,release}-{ownership,scalar}.txt summaries
were read back. The earlier 82-case success log was superseded by this expansion;
the original rejection baseline remains intact.

Bill confirmed the existing no-default-object rule: reserve does not construct,
push supplies an object, direct object-array resize cannot grow; nested empty
arrays remain defaultable. This is Chapter 8 behavior, not a Chapter 9 addition.
The historical optional rejection check was retired rather than made a permanent
language prohibition. Existing failed baselines remain saved. No C-emitter switch,
sanitizer run, cumulative runtime result or performance measurement is claimed.

## Structural publication boundary

Added IrProgram.ValidateStructure in IR/Validation.cs, declared in Common and
called before LowerIr succeeds. Editing still permits incomplete drafts; completed
publication rejects detached arena nodes, absent operand definitions, inconsistent
membership/backlinks, malformed If/While region roles, non-enclosing loop transfers,
and the not-yet-lowered For vocabulary. Traversal is iterative. Failure does not
mutate the graph; LowerIr's existing exception path destroys its partial program.
These are internal compiler errors, not new source-language diagnostics.

Debug build had zero warnings/errors; structure-debug-ownership.txt passed 85/85
and structure-debug-scalar.txt passed 18/18 (command 2564 exit 0). Real-model probe
structure-model.txt passed both configurations (2566 exit 0), testing detached
drafts, deleted operand definitions, invalid transfer, missing branch, refusal
without mutation and repair. This C# development probe is not the independent
chapter grader. Structural success does NOT certify opcode typing, use order,
ownership balance, full region termination, or IR-backed C emission. Those checks
remain to be implemented. No new language feature or generator was introduced.

Structural cumulative verification completed: job 2567 exited 0. The saved
structure-cumulative.txt summaries were read back (2577): Debug and Release each
passed 18/18 scalar IR and 85/85 ownership IR checks, 154/154 class checking,
212/212 layouts, 4/4 free-slot and 4/4 class-growth comparisons, and 12/12
owned-field/array balance comparisons, alongside the retained earlier suites.
Both builds had zero warnings/errors. Validation.cs separately checked clean for
trailing whitespace. This establishes regression compatibility, not full IR
validation or IR-backed C execution.

Next validation design observations from actual Lowering.cs: loop-condition
holders are intentionally available to body and post-loop cleanup, despite being
in a sibling region. An ancestor-only definition rule is therefore incorrect.
Return Move results can span cleanup before their consuming Return; immediate
consumption means a single transfer destination, not necessarily adjacent nodes.
Validate operand/result contracts first, then execution availability and ownership
as explicitly separate obligations. No further validator implementation is claimed
by these observations.

## Operation-shape validation and generated receiver types

`ValidateOperations` follows structural validation before publication. It checks
implemented opcode arity, result categories, typed results, non-result operands,
fixed-address borrowing, storage operands for loads/stores/address capture, and
byte-payload versus element-operand array construction. Calls retain selected
semantic identities. This is not yet full type/signature, availability, termination,
or ownership verification; those remain subsequent work.

The first run built cleanly but passed only 50/85 ownership cases. Every class
case encountered an untyped generated receiver: destruction lowering had used
`nominal.Type`, although a class declaration does not carry its instance type
there. Construct the same nominal descriptor used by checking,
`SemanticType(ScalarKind.Unit, 0, nominal)`, and assign it to the generated
receiver and handle loads. This also registers the normal nominal reverse type
dependencies. Preserve `operation-untyped-baseline.txt` as failed evidence.

Lesson for construction: an interpreter can execute a handle without all the type
metadata required by emission and transformation. Passing behavioral examples
therefore does not replace validating the typed representation. Generated bodies
must obey the same contracts and relationships as source bodies.

The real-model probe keeps shape checks separate from its intentionally incomplete
structural graphs. It now refuses missing return operands, untyped locals, loads
from plain values, borrows of logical places, and result-free constants; repaired
operations validate without mutation. Debug focused results: 85/85 ownership and
18/18 scalar cases; model probe 49 checks in both Debug and Release, zero warnings
or errors. Release inspection also passed 85/85 ownership and 18/18 scalar
cases, with a warning-free build; the verification command and whitespace check
finished with exit 0. C emission is unchanged; the earlier cumulative run predates
this pass.

## Exact storage and array type contracts

Extended ValidateOperations using existing SemanticType equality (including actual
nominal class identity), not a separate comparison system. Load, AddressOf, Copy,
Move, Ref and alias Borrow preserve exact types; Store requires an exact-typed
value. Array literals require exact element values, byte payloads require [u8],
and indexed selection removes one array layer and accepts an integer value index.
Reason: conversion must be represented explicitly; a storage or ownership operation
must not silently change the type of the value it carries.

The real-model probe adds mismatched load/store/array-element/index-result cases,
then repairs them. These isolate type validation, not execution ordering. Debug
and Release focused inspection runs passed 85/85 ownership and 18/18 scalar cases;
both builds had zero warnings/errors. Probe driver completed successfully (54
checks reported); logs type-model.txt and type-{debug,release}-*.txt retained.
Jobs 2596 and 2600 exit 0; 2599 was refused before execution and retried as 2600.
Whitespace diff check passed. Public grader README now states validation coverage
and remaining obligations. No full cumulative rerun after this extension; C
emission unchanged. Call/operator compatibility, availability, termination and
ownership validation remain ahead.

## Resolved call contracts

ValidateOperations now checks Call against its actual Function symbol and owned
parameter scope: exact arity, result type, parameter type, and Address for reference
parameters versus Value for ordinary parameters. No names/signatures are rebuilt.
Constructor calls correctly return their declaration's unit type; NewObject's
handle separately supplies the source construction result. Generated final-hook
calls obey the same signature rules as ordinary source calls.

Added malformed-call model cases for missing argument, value instead of reference,
wrong referent type, and wrong result type, with repairs in the same graph. Semantic
callee/parameter declarations are created before IR so setup does not invalidate it.
Jobs 2609 and 2612 exit 0; readback 2614 confirms Debug/Release 85/85 ownership,
18/18 scalar, and 59 model checks in each configuration, zero warnings/errors.
Logs call-*.txt retained. Whitespace diff check passed. README updated. No C backend
switch or full cumulative rerun claimed. Remaining operation-specific contracts,
execution availability, termination, and ownership validation remain unfinished.

## Array mutation and metadata contracts

ValidateOperations now requires array operands for metadata/mutation, u64 metadata
results, and unit mutation results. Length consumes an evaluated value; capacity
can observe selected storage or a computed owner. Mutations require a Place or
Address, with an exact element value for push or an integer value for count-based
operations. Lesson: exact types alone cannot distinguish a copied array value from
the descriptor mutation must affect; category and evaluation contracts matter too.

The real-model probe rejects mutation of a value, noninteger resize counts, wrong
push elements, and wrong capacity result types, then accepts repaired graphs.
Initial build 2619 failed because u64 type construction omitted required depth;
corrected to explicit depth zero, failed log retained as array-contract-build-failure.txt.
Debug 2621 and probe/Release 2624 exit 0. Summary readback 2627 confirms 64 model
checks in both configurations, 85/85 ownership and 18/18 scalar cases in each,
zero warnings/errors and clean whitespace. Logs array-contract-*.txt retained.
README updated. No cumulative rerun or IR-backed C claim. Remaining class/operator,
control-flow availability, termination and ownership contracts still need work.

## Nominal class operation contracts

ValidateOperations now checks SelectField against field.Scope.Class and the exact
field type, NewObject against its selected nominal class, and DestroyObject's
value-handle/unit contract. ReclaimObject requires matching receiver storage and
must occur inside that class's generated destruction function. No name lookup or
handle-representation equivalence substitutes for declaration identity.

The operation probe now constructs a real member scope and field, so its existing
Borrow-of-Place rejection still tests borrowing rather than invalid field setup.
New negatives cover wrong receiver class, wrong field type, allocation mismatch,
and reclamation from a source body; repaired graphs remain usable.
Lesson: a handle-shaped value is not enough evidence of class identity.

An exact-anchor assertion stopped the initial probe edit before writing it;
the validator edit had saved and the shell continued the focused build/tests.
The corrected probe then hit a local variable collision with an older array test;
renamed the new object receiver and retained class-contract-model-build-failure.txt.
Job 2639 completed exit 0 after correction. Debug/Release focused suites pass
85/85 ownership and 18/18 scalar; model probes pass 69 checks in both builds,
zero warnings/errors. Logs class-contract-*.txt retained; whitespace check passed.
No full cumulative rerun or C-emitter switch claimed. Counted-operation restrictions,
operators, control-flow availability, termination and ownership remain unfinished.


## Ownership-operation type contracts

ValidateOperations now restricts Copy/FreeArray to arrays, Ref/Unref to scalar
counted-class instances, and Move to those ownership-bearing types. Existing
exact-type preservation remains in force. Five isolated negative real-model
cases reject unit storage, remove the defective operation, and revalidate.

Construction lesson: type eligibility is not holder provenance. Replacement
through a reference legitimately releases its old array or counted value; a
blanket ban on releasing borrowed addresses would reject correct lowering.
Execution availability, transfer provenance, and balance remain separate work.

Verification: jobs 2650 and 2651 exited 0. Debug and Release passed 85/85 ownership
and 18/18 scalar IR cases; real-model probes passed 74 checks per configuration.
Builds reported zero warnings/errors and git diff --check passed. Logs are
ownership-contract-*.txt. No cumulative suite rerun or IR-backed C claim.
