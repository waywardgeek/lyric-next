# Chapter 10 — composite types and canonical type identity

## Phase and boundary

Authorized interactive development with Bill. Chapter 9 and `spin09` are human
accepted. No Chapter 10 compiler changes, graders, reviews, reconstruction or
performance evidence exist at the start of these notes. Work personally; do not
launch reviewers or students until the normal authoring milestones are reached.
The language specification records the accepted behavior; these notes describe
construction choices and evidence, not additional hidden language requirements.

Keep scalar/class/array behavior and all retained graders. Add tuples and nominal
structs, named-field construction and expression defaults, instance/static methods,
finite recursive layouts, composite ownership, and nonescaping borrow-containing
structs. Enums, unions, general generics, interface specialization, concurrency,
new object-to-object fields and relations remain later increments. Existing class
handles may be struct components; this does not authorize new class-field graphs.

## Accepted behavior to carry into the chapter and graders

- One-element tuples use `(T,)`, `(value,)` and `(name,)` binding patterns;
  parentheses without a comma group, and `()` remains unit.
- Tuple types are structural by ordered component identities; structs are nominal
  by declaration identity. Transparent aliases preserve identity.
- Tuples have `.0` fields, exact shape destructuring, nested patterns and `_`.
  `let`/`var` patterns introduce fresh immutable/mutable names, never assign existing
  bindings. Evaluate the initializer once; preserve ordinary cleanup for discards.
- Struct construction names fields. Duplicates/unknown fields reject. Missing
  fields require explicit declaration defaults; no invented implicit zeros.
- Defaults may call functions. Evaluate supplied expressions once, left to right
  as written; then omitted defaults in declaration order. Skip overridden defaults.
  Defaults use declaration scope, cannot access partial self or sibling fields.
- Instance `self` is by value; `self: ref Point` is a writable reference receiver,
  called explicitly as `(ref point).method(...)`, never silently borrowed.
  No-self methods are static and called on the type. Common declares methods;
  functional files in the same package supply matching qualified bodies.
- Inline containment cycles reject, including through tuples/aliases. Arrays and
  class handles break inline layout recursion without weakening lifetime rules.
- Borrow-containing structs are nonescaping: local values/copies and synchronous
  arguments only; no returns, globals, object fields or arrays. Nested wrappers
  cannot hide a borrow. Alias copies do not retain targets or extend their validity.
  Invalidating operations cannot precede a possible later use of a contained borrow.
- Ordinary aggregate contents retain existing independent-array-copy and class
  retention semantics. A class handle is not a borrowed storage alias.

## Source observations

`compiler/Common/SemanticTypes.cs` and `compiler/Semantics/SemanticTypes.cs` define
one flat readonly record struct: scalar kind, array depth and optional class symbol.
Predefined types are static values; ArrayOf/ElementOf construct new descriptors.
Current equality is constant-size, not a recursive type traversal.

`SemanticAnalysis` owns facts/scopes and IR. Its Destroy clears IR, retires facts,
then destroys scopes. Symbols, facts and IR retain nominal reverse dependencies.
`SemanticSymbol.SetType` validates and reserves destination membership before
removing the old dependency. A composite type can mention several nominal types;
this dependency model must be extended deliberately, not replaced by name searches.

Syntax already names Tuple, PatternTuple, Struct and composite type constructs.
That is syntax vocabulary, not evidence those forms are semantically implemented.
The sole C execution path consumes validated IR; aggregates must extend it rather
than reintroduce AST execution or emitter-owned cleanup decisions.

## Provisional model sketch — not a published chapter diagram

Analysis strongly owns one canonical type table; that table strongly owns each
canonical type. Canonical types describe scalar, array, tuple, nominal and borrowed
forms as needed by this increment. A nominal type refers to the actual declaration,
not its name or an expanded copy of its fields. That permits declaration recursion
without infinitely expanding an interning key.

Composite types refer to canonical components. Ordered component links preserve
positions and repeated components, with reverse use memberships. Symbols, facts
and IR nodes refer to canonical types; persistent uses retain reverse memberships.
Nominal declaration retirement must invalidate dependent types and their consumers
without treating a reference as ownership of that declaration. Analysis teardown
retires IR and semantic consumers and then releases canonical state, joining the
existing lifecycle rather than relying on garbage collection for logical detach.

This is a provisional ownership/dependency sketch, not a choice to implement C#
inheritance or a license to add interfaces, virtual dispatch or a service registry.
Decide representation from the required payloads and actual implementation.

Interning uses one custom hash table with complete-key collision checking. Keys
contain kind, scalar tag or declaration identity, and ordered canonical components.
Canonical identity makes later equality constant-time; no hash-only equality, global
cross-analysis cache, copied declaration name or independent IR type registry.
Resizing the table must preserve canonical objects and relationships. Rejected
cross-analysis inputs must leave existing memberships unchanged.

## Construction slices

1. Establish the unchanged build/grader baseline. Replace flat descriptors with
   canonical types while preserving current scalar/array/class behavior. Add
   independent checks for reuse, distinct nominal identity, collision correctness,
   table growth and invalidation. Do not leave two authoritative type systems.
2. Collect struct declarations before resolving their component types. Resolve
   tuple shapes and nominal identities, then reject infinite inline layouts.
   Exercise forward and mutual references without enabling unrelated features.
3. Implement tuple/struct expressions, fields, binding patterns and ordinary
   functions with aggregate parameters/results. Preserve left-to-right snapshots,
   value-copy independence, source diagnostics and no partial inspection output.
4. Add explicit default evaluation and method declaration/body matching through
   the existing semantic pipeline. Method syntax must not hide receiver borrowing.
5. Extend IR operations/validation, lowering, generated copy/drop/retain behavior
   and C layouts for aggregate values. Extend the independent evaluator; retain
   old inspection contracts. Implement real behavior, not fixture-shaped paths.
6. Extend storage origins/escape/effects through contained borrows and copies.
   Test local legality, forbidden escapes, alias invalidation, last use, branches,
   loops and helper calls, including nested wrappers and generated cleanup effects.

These slices can overlap where a working vertical increment requires it. Do not
claim a partial tuple implementation completes the whole chapter. If scope becomes
unwieldy, discuss a split with Bill rather than silently dropping adopted behavior.

## Verification and teaching

Public Python graders remain outside the C# implementation and any future Lyric
edition. Add retained cases with exact success/failure contracts, exercise the real
compiler through documented entry points, and independently check important IR
semantics. Model lifecycle/collision probes complement behavioral graders, not
replace them. Freeze inputs while builds and grader jobs run; collect receipts.

The finished chapter begins with source-checked diagrams and class descriptions,
then Introduction where appropriate, TL;DR, construction, exercises and Spin.
The design sketch above is disposable; the published map follows working code.
Preserve all existing relationships exactly once across chapter figures. Actual
hierarchies, if introduced, receive their own clearly distinguished view.
One code review, one text review, then one fresh isolated reconstruction; no extra
review loops or rerunning a passing student. Human acceptance remains separate.

## Initial baseline

Unchanged compiler: Debug `dotnet build compiler/Lyric.csproj -c Debug --no-restore
-tl:off` passed with zero warnings/errors, followed by the public Chapter 9 scalar
IR grader: 21/21 and its snapshot-order self-check passed. Receipts: job3209/3210;
local logs in ignored `book/build/ch10-baseline-build.log` and
`book/build/ch10-baseline-ir.log`. This is not a new full cumulative/sanitizer run.
No compiler source was changed for this baseline.

Direct descriptor construction occurs in SemanticTypes, Check, IR validation and
one C type-formatting path. Static predefined type references span the checker,
lowering and validation. The conversion must route creation through one canonical
owner and adapt nullable value-type assumptions; a local equality patch is not
sufficient. Existing nominal dependency mutations reserve before detaching and
invalidate IR only after validation succeeds; preserve that refusal contract.

## Owner-context prerequisite — verified

The first compiler edit routes scalar and nominal creation in Check, IR lowering,
IR operation validation and C emission through SemanticAnalysis.ScalarType,
ClassType and PredefinedTypes. ClassType rejects retired/cross-analysis declarations.
The emitter walks array elements to obtain the base C type rather than manufacturing
a flattened descriptor. Common declares the entry points; Semantics implements them.
This is deliberately only the creation-boundary prerequisite: SemanticType remains
the old value descriptor, with legacy static constants still used by model helpers.
There is no canonical table or composite support yet, and no identity/performance claim.

Debug build job3221 passed, zero warnings/errors. Job3223 passed the unchanged public
Chapter 9 scalar IR 21/21 plus self-check, ownership IR 92/92, and IR-to-C 33/33.
Logs: book/build/ch10-owner-build.log, ch10-owner-ir.log,
ch10-owner-ownership.log, ch10-owner-c.log. These are not full cumulative or
sanitizer results. Source was frozen while the graders ran. No live job remains.

Next replace the value descriptor with analysis-owned canonical objects and the
custom full-key table. Preserve current lifecycle behavior: declaration mutation
retires IR first; nominal retirement invalidates dependent facts and symbols.
Move persistent holder memberships to canonical types and propagate invalidation
through component/declaration links, including repeated tuple components later.
Do not retain both direct nominal-holder lists and canonical-holder lists as two
independent authorities. Add focused collision/reuse/growth/refusal/lifecycle checks.

## Canonical scalar/array/class model — implemented, cumulative run pending

SemanticType is now a sealed identity-bearing class, not a flat record struct.
SemanticAnalysis directly owns the one custom bucket table; a separate table
object had no useful lifetime boundary. Full keys include form, scalar tag,
canonical element identity and actual nominal declaration identity. Hashes select
buckets only; comparisons inspect the complete key. Resizing retains objects.
No process-global scalar identities or second type cache remain. Scalar/Class/
ArrayDepth compatibility queries derive from the canonical element chain; equality
is reference identity, not recursive comparison. No speedup was measured.

Canonical types track symbol, fact and IR users. Arrays track element/backlinks;
nominal types track their declaration/backlink. The former direct nominal-to-holder
lists were removed, not retained as another authority. Symbol replacement validates
and reserves before retiring IR or detaching the old type. IR replacement similarly
preserves the old graph on refusal. Analysis teardown clears IR/facts/scopes before
remaining types. Type retirement invalidates dependent arrays and semantic holders;
it detaches its declaration before recursively retiring dependents. Published keys
cannot be detached/reassigned through relationship operations; detach requires
retirement. Removal from analysis requires complete unlinking.

Implementation: Common/SemanticTypes.cs declarations; Semantics/CanonicalTypes.cs
table/lifecycle; existing SemanticTypes helpers and checker/lowering/emitter/validator
call sites migrated. SemanticFact now has nullable TypeLink for partial construction
and teardown, with a required live Type accessor. No tuple/struct language behavior,
optimizer, public type-inspection table or Chapter 10 grader is implemented yet.

First build3233 caught two target-typed constructors missed by text search; corrected
checker self typing and IR object validation to use ClassType. Also removed the
caller-supplied hash parameter so direct construction cannot choose a false bucket.
Build3235 passed zero warnings/errors. Frozen CLI run3236 passed unchanged Chapter9
21 scalar + selfcheck, 92 ownership, 33 C-emission cases. Logs ch10-canonical-{build-2,
ir,ownership,c}.log in ignored book/build. Those results precede the small key-detach
and unlinked-removal guard refinements.

Development-only probe tools/probes/check_canonical_types.py plus canonical-types.cs
is NOT a retained chapter grader. Latest3247: 193 checks each Debug/Release, zero
warnings/errors. Tests reuse, distinct nominal identities with equal spelling,
bucket collisions, rehash stability, duplicate-construction refusal, cross-analysis
symbol/IR refusal without retiring IR, successful replacement retirement, dependent
array invalidation, complete teardown and actual parsed/checked fact detachment.
Earlier3243 had190 before checked-fact cases. Migrated existing ir-model.cs creation
API only;3246 passed the same175 checks both configurations. Logs ch10-canonical-
model-2.log and ch10-ir-model.log. No sanitizer or performance claim.

LIVE job3249 runs make test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet,
output book/build/ch10-canonical-cumulative.log. Source/build inputs frozen until
terminal receipt. Whitespace fixed one extra EOF blank in IR/Model.cs; checked
tracked edits and new source/probe trailing whitespace before launching. 14GiB free.
Latest read3251 had reached Debug Ch5 80/80; not a completed cumulative result.
No commit/push; preserve unrelated arith.ly/hello.ly and earlier documentation edits.

Next public seam recommendation: keep --inspect-ir format lyric-ir-1 and existing
readable type strings; add node typeId and a document-local types array describing
actual referenced canonical objects and their element/declaration links. Writer
already uses actual-symbol dictionaries for serialization IDs. Type IDs likewise
must not become persistent model state or be reconstructed from names. Add Python
retained checks for reuse/distinction/nesting/large nominal sets, including malformed
output selfchecks, before implementing that additive publication contract. Existing
IR/Inspection.cs was read fully1-131. No new grader/source contract edits yet.
Then continue agreed composite slices, with independent public graders. Final
source-checked chapter diagrams wait for the working model; current diagram evidence
will need updating rather than pretending the old flat descriptor still applies.

## Canonical baseline and public inspection verified

Cumulative job3249 completed exit0. Saved ch10-canonical-cumulative.log summaries
verified3264: both configurations clean builds; retained checks include 82semantic,
80Ch5,250expressions,87unsafe,112arrays,6Ch8execution,154static,212layouts,
4allocation+4growth,12balance,21IR,92ownership,33C plus earlier source/parser checks.
This cumulative result precedes the additive inspection fields below.

IR/Inspection.cs now assigns document-local IDs to actual canonical objects and
writes node typeId plus top-level types records (kind/name/scalar/elementTypeId/
classId). Iterative component discovery includes referenced elements; no name-based
repair, persistent IDs or second interner. Existing fields remain unchanged.
Public graders/ch10/canonical.py and README document the contract and four retained
checks: scalar reuse/distinction, nested arrays,100nominal declarations, located
LYC301 rejection with empty stdout. Selfcheck rejects3 malformed documents.
Baseline3264 failed at absent types field as intended; implementation3265 passed4.
Makefile now includes canonical.py in both cumulative configurations.
Compatibility3267 completed exit0; summaries3272 verified Debug+Release clean builds
and21/92/33 Chapter9 plus4 canonical tests each. Not another full cumulative run.
Tracked whitespace passed; new grader files checked separately. No sanitizer claim.

Lesson: inspection must expose actual identity, not reconstruct/deduplicate by name
and accidentally conceal an interner defect. JSON evidence complements direct
model probes and source review; it cannot prove internal pointer equality alone.

Next composite staging inspection: Check.cs113-192 collects all class/function
names then member types, excludes structs and qualified bodies currently; fields
require exactly one child and reject nominal/unit types. ReadType1927-1953 supports
ArrayType and TypeName only, returning analysis.ClassType for class declarations.
These blocks fully read; earlier combined read clipped and is not full inspection.
Next inspect parser tuple/struct representation and scope ownership before choosing
component relationships. No tuple/struct changes yet. No live jobs/children.
Bill additionally requested queue captures: terminal-control noise saved726-733;
existing clipping-defaults entry633-635 updated and read back. Both queued, not
active investigations. Continue Chapter10; preserve scratch/unrelated work.

## Chapter 10 single-element tuple syntax prerequisite

Inspection found the parser and canonical PEG still required a second tuple
component, conflicting with the adopted Chapter 10 single-element forms. Updated
expression/type/pattern comma handling in Parser/SourceFile.cs and the three PEG
rules together. Existing multi-element trees remain unchanged. `(T,)` uses comma
text on its one-child TypeGroup; `(T)` retains null text. No semantic support yet.
Updated the two old single-tuple rejection expectations intentionally; added five
recognition fixtures (single type/nested expression/doubled-comma rejections),
three tree checks, and public contract explanation. Restored incidental Unicode
JSON spelling churn. Lesson: syntax agreements must reach both recognizers and
the published tree contract before the checker can use their distinction.
Verification: jobs3286/3289 terminal0; summaries3291. Debug and Release builds
0 warnings/errors; each69/69 trees and148/148 recognition; Peggy148/148 public
fixtures; Debug boundaries7/7; whitespace clean. Logs book/build/ch10-tuple-*.log.
Not cumulative or tuple execution evidence. No live jobs/children.
Next: ordered canonical tuple component links with repeated positions and reverse
dependencies, model probes first; then semantic tuple typing and real IR/runtime.
Recommended relation: tuple strongly owns occurrence links; each link weakly
references canonical component with reverse use. Retiring component invalidates
dependent tuples; retiring tuple detaches, never destroys component types.

## Ordered canonical tuple model (not yet language semantics)

Common/SemanticTypes.cs adds Tuple kind, ordered strongly owned
SemanticTypeComponent occurrence links and reverse ComponentUses dependencies.
Semantics/TupleTypes.cs implements tuple factory/complete-key lookup, constructor,
SameKey, occurrence add/remove and teardown. Tuple keys compare ordered canonical
identities, including repetitions; empty tuple keys refused (source () is unit).
Cross-analysis/retired inputs refused before attachment; both list capacities
reserved before mutation. Published keys cannot acquire/remove components.
CanonicalTypes.cs duplicate admission uses SameKey; retirement invalidates tuple
users and removes owned occurrence links without deleting shared components.
Type naming now uses an explicit stack for arrays/tuples, preserving old names.
No tuple ReadType/checking/lowering/execution support enabled yet.

Probe expanded to208 checks: tuple reuse/order/repetition/single identity/name,
array-of-tuple rendering, empty/foreign/duplicate/key-edit refusals, nominal
retirement through repeated tuple -> array -> tuple while unrelated shared
components survive. Job3295 terminal0, both Debug/Release clean208; summaries3296.
Actual CLI Debug build clean; public canonical4+selfcheck3 and scalarIR21 passed,
summaries3297. Job3296 final status alone was not proof (whitespace command followed
grader chain); saved summaries verified. Not full cumulative since tuple model.

Next check semantic expression work stack before enabling tuple types. Check.cs
ReadType1927-1953 still ArrayType/TypeName only; Array support rejects noninteger,
nonbool,nonclass bases. CheckExpression traversal around2036 handles Array/Index/
Member specially; Member is also in borrow/effect paths. Last combined read3298
clipped; do not count missing chunks as inspected. Need narrow2030-2100, subsequent
work-stack context propagation, member fact/lowering/storage before implementation.
No live jobs/children; no commits/push. Syntax predecessor evidence above remains.

## Tuple ownership classification prerequisite

SemanticTypes.HasOwnedContents now computes ownership from live canonical types:
arrays own backing, counted nominal class handles own a reference, tuples recurse
through ordered components, scalars/ordinary handles do not. Explicit work stack
and visited identity set avoid recursion/repeated shared-subgraph traversal; arrays
terminate traversal at descriptor indirection. No cached ownership fact. Lowering's
Owned predicate uses this shared query; ReadKind/drop emission remain unchanged
until tuple execution exists. No source tuple typing enabled.

Probe217 Debug/Release clean (3317, saved ch10-tuple-ownership-model.log);
real Debug CLI build clean and ownership92 passed (3318). That command then failed
because I guessed c.py instead of c_emission.py. Failed log retained; corrected
3322 exit0 C-emission33 verified3324. Tracked/untracked TupleTypes whitespace clean.
No full cumulative rerun in this slice; no live jobs/children.

Read actual checker2305-2409: ordered array stages provide one element context;
tuples need positional context, not first-element inference. Hint discovery2020-
2115 is side-effect-free. Lowering Owned formerly array/count only; emitter CType
805-823 would currently treat tuple as unit. Do not enable checker alone.
Recommended next IR design (not implemented): NewTuple consuming ordered snapshots,
SelectTuple constant component selection, aggregate Copy/Move and explicit aggregate
cleanup; inspect borrow/selection contracts before admitting writable components.
Add independent public execution fixtures first, then implement real semantic/IR/C
paths together. No parallel tuple evaluator or fixture-shaped emitter.

## Tuple execution contract and IR vocabulary (source support still pending)

Public graders/ch10/tuples.py adds eight retained positive runtime fixtures:
singleton/grouping, positional context, nested selectors, left-to-right snapshots,
call/return, independent owned-array copies, component borrow, counted finalization.
Exact byte stdout/empty stderr/exit0 at O0 and O2; external compiler argument;
not yet in cumulative recipe. Baseline3328 exit1: all8 parse, all8 intentionally
fail LYC306 Tuple/TypeGroup semantic gate. Negative source cases still needed.

Common/IR.cs adds NewTuple, SelectTuple, DropTuple. NewTuple ordered operands match
canonical components; ownership-bearing incoming values require Move. SelectTuple
uses Integer Literal selector, checked in range, exact component type, returns
fixed Address from Value holder or Address (Place must resolve first). Copy admits
tuples, Move admits any HasOwnedContents type, DropTuple only owning tuples.
Validation adds both transfer directions and NewTuple owning-holder classification.
At this milestone tuple source lowering/emission was not implemented; the later backend milestone below supersedes the emission status.
Execution validator already tracks Move obligations generically; no parallel path.

Probe valid scalar/owned tuple graphs run structure+operation+execution validation;
isolated defects: reversed/missing components, negative/out-of-range/wrong-type
selection, missing Move, wrong DropTuple referent. Probe226 both configs clean
3338/summaries3339. Existing IR model probe3339 terminal0, saved
book/build/ch10-tuple-ir-regression.log. CLI build3333 clean. Whitespace check3339
clean. Earlier CLI ownership92/C33 evidence predates new IR vocabulary. No full
cumulative after tuple changes; source fixtures still0/8, no feature-complete claim.

Next: C tuple record naming/definitions and recursive copy/drop helpers integrated
with existing array helpers; inspect Emission/Compilation.cs ordering and helper
functions before edits. Read CType805-823 proves tuple currently falls through to
unit, must change before checker admission. Then semantic tuple ReadType and
expression positional context plus fixed component access/borrow tracking and
real IR lowering. Do not enable checker prematurely. All work personal; no new
review/student/commit/push. No live jobs/children at this checkpoint.


## Tuple C backend foundation (source support still pending)

EmitC now lowers then calls EmitIrC, which validates structure, operation types,
and execution before buffering C. This is the same backend, not a second path.
Common/Emission.cs declares the boundary. Direct model diagnostics can emit real
IR without having it silently replaced by LowerIr.

Emission gathers actual canonical types from IR, walks component-first iteratively,
and emits tuple records plus helper prototypes before declarations/class storage.
Helper bodies follow class retain/release definitions. Per-tuple empty/copy/drop
operations recurse through shared ownership dispatch; arrays of tuple leaves use
specialized recursive helpers rather than scalar memcpy. Empty counted components
use UINT64_MAX, not zero. NewTuple assigns snapshots, SelectTuple captures a field
address, DropTuple uses Drop; local/moved tuple holders get cleanup-safe emptiness.

New development-only tools/probes/tuple-emission.cs and check_tuple_emission.py
build real compiler sources and a checked main with explicit tuple IR. Generated C
runs at O0/O2 and checks independent array copying, surviving original destruction,
cleared holders, and nested arrays of tuple leaves. First run3357 failed at probe
compile: private Compilation.Analysis access. Corrected via declaration type's
analysis, without widening field access. Failure log preserved. Run3359 terminal0;
probe-2 log clean build and both optimization checks, C regression33/33. CLI build
3354 zero warnings/errors. Whitespace3362 clean, including new probes. No sanitizer
or full cumulative claim. Scalar/array tuple backend exercised; counted tuple and
nested inline tuple cases still need direct tests. Public source tuples remain0/8.

Important remaining gap: EmitArrayMutation currently rejects default growth only
for direct handles; tuple-contained nonnullable handles need the recursive guard.
Cleanup-safe empty holder is not necessarily an observable valid language value.
Check agreed default rules before enabling tuple semantics. Then continue semantic
ReadType, positional expression context, selection/borrow effects, and IR lowering.
No live jobs/children; no commits/pushes. Preserve unrelated scratch files.


## Source tuple checking, lowering, and fixed component borrows

The scalar-first source slice now resolves grouped and tuple type syntax through
canonical types, supplies positional expected types, and checks children left to
right. Numeric selectors check the receiver once and require an in-range index.
Tuple reads copy an evaluated receiver; writable component selection traverses
original storage and captures SelectTuple addresses. Inline selections retain the
borrow root/depth; they do not cross an array backing allocation. Immutable roots
remain immutable. Nested reference-parameter forwarding is covered publicly.

Lowering ReadKind uses Copy for tuples; owned NewTuple temporaries register with
region cleanup. DropKind selects DropTuple for both replacement and scope exit.
Construction widens/transfers each component into NewTuple. The temporary checker
gate admits scalar arrays recursively, but rejects any class handle or unsupported
float inside tuples until aggregate class lifetime/finalization effects are ready.
This gate is an implementation boundary, not an adopted language limitation.
Default-growing arrays of tuple elements remain rejected with LYR011: tuple
public defaults are still unapproved, unlike cleanup-safe internal empty holders.

IR inspection emits ordered componentTypeIds from actual canonical identities.
Public canonical grader now has five cases/four mutation selfchecks, including
repeated components and differing orders. Public tuple grader now has ten positive
and seven negative cases. Rejections require their intended located code, exit1,
empty stdout, not arbitrary failure. Docs updated with actual contracts.

Evidence and corrections:
- 3372 clean Debug build, tuple backend O0/O2, ownership92 and C33.
- 3379 first source slice5/8 positive;3385 five intended rejections passed.
- 3384 used ch04 without its chapter flag:80/82, rejecting tests expected old
  unsupported branch values/classes. Corrected invocation3389 --chapter=8:82/82,
  IR21, ownership92,C33. No fixture weakening. Lesson: recipe flags are contracts.
- 3391 clean build;3392 source7/9 plus6/6 rejections, including nested component
  borrow and immutable rejection. 3394 both configs82semantic/112arrays/21IR/
  92ownership/33C/5canonical. Compiler sources frozen, but I mistakenly added tuple
  fixtures before Release reached them: Debug7/9+6/6, Release7/10+6/7; preserve
  distinction. These are not identical-input tuple runs.
- 3398 array-content gate relaxation:9/10 positives,6/7 negatives. Invalidation
  fixture incorrectly printed i32, causing LYC304 before lifetime checking.
  Corrected later use to assert(item==1), preserving intended LYC326 obligation.
- LIVE3400 freezes current compiler AND ten/seven fixture set; both configurations
  build plus tuples,82semantic,112array,21IR,92ownership,33C,5canonical. Tuple exit1
  is retained because counted fixture is pending, not treated as acceptance.
  Logs book/build/ch10-tuple-arrays-{Debug,Release}-*.log. Collect terminal receipt
  and saved summaries before edits. No full cumulative or sanitizer claim for this
  slice. No live children/commits/pushes. Preserve scratch arith.ly/hello.ly.

Next: finish frozen verification; inspect aggregate class effect tracking before
admitting counted/ordinary handles. Existing Release/RegisterHolder/objectValues
use one Type.Class and cannot silently cover several nominal tuple components.
Also pending arrays-of-tuples source gates, direct tuple-component assignment,
destructuring/structs/contained borrowed fields, final teaching/diagrams/reviews.

## Aggregate class lifetimes — verified focused slice, cumulative run pending

The earlier run3400 is terminal exit0: both Debug/Release builds clean; each
9/10 tuple execution,7/7 intended rejections,82 semantics,112 arrays,21 IR,
92 ownership,33 C emission,5 canonical. The counted tuple gate accounted for the
one pending positive; this was not full tuple acceptance.

Check.cs now traverses canonical arrays/tuples iteratively to enumerate distinct
contained nominal classes. This is temporary effect analysis, not a second type
cache or runtime destruction sequence. Release and occupied-holder cleanup apply
all contained counted finalizer summaries. Identity maps now hold a list of graph
handles, one per contained class, preserving every saved value across later
arguments, return cleanup, and conditional-result cleanup. Graph dataflow remains
per handle; repeated class components contribute one conservative class effect.
Storage selection from a tuple retains its root's saved object handles rather
than inventing fresh ones. Aggregate temporaries include tuples and are disarmed
on transfer into a new aggregate, binding, argument, or returned value. Runtime
copy/drop/retain/release remain explicit IR operations, not checker algorithms.
Removed only the temporary class-content gate; floating point remains unsupported.

3409 cleanup generalization: clean Debug build and154/154 Chapter8 checks.
3412 list-of-identities refactor: clean Debug build and154/154 checks. 3415 new
public fixtures baseline9/11 positive7/11 negative, blocked by LYC306 as expected.
3418/3419: clean build,11/11 execution11/11 intended rejections. Expanded3420/3422:
12/12 executions at O0/O2 and13/13 intended rejections. Includes ordinary copy,
counted copy, nested mixed counted tuple/array return and copy (three finalizations
only after live marker), second-class invalidation, later-argument invalidation,
return/branch cleanup, and own-final self-copy rejection LYC334. No sanitizer or
universal safety proof claim. Whitespace passed. Public README updated; tuples.py
now retained in both configurations of make test.

LIVE3423: make test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet;
log book/build/ch10-class-tuples-cumulative.log. Compiler AND graders frozen until
terminal receipt. Collect saved summaries, not just exit status. No children,
commits, pushes or installs. Next read-only inspection: direct tuple-component
assignment (CheckAssignment around1908, Lowering assignment around827). It must
release only selected component, not whole aggregate; existing depth-only holder
replacement needs care because inline tuple selection keeps depth zero. Other
pending Chapter10 features unchanged. Tuple default-growing array rule remains
unapproved; continue is not approval of componentwise defaults.

### Newly demonstrated partial-holder defect (before cumulative run ends)

Read-only assignment inspection found that inline tuple selection retains array
depth0, but depth0 is incorrectly treated as whole-root replacement/emptying.
Diagnostic3427 (saved book/build/ch10-component-alias-diagnostic.log) checked:
class Item with n=7; counted Drop final creates/destroys an Item; make owns
var pair=([1],Drop()); let alias=ref pair.0; alias=[]; return Item(). Checker
returns0/empty streams, but must reject LYC330 because pair's Drop finalizes after
return evaluation. No faulty generated program was executed. Emptying one array
component disarms whole tuple holder, hiding sibling finalization. Public tests
not yet added: compiler/graders remain frozen for3423. This is a real pending
checker bug, despite12/12+13/13 focused passes.

Next fix after3423 terminal: track graph-local partial-holder selections separately
from backing depth. Preserve marker and selected object handles through alias
Acquire, tuple Member, nested Index. Whole-holder assignment/reset allowed only
nonpartial depth0; partial replacement releases selected type and conservatively
keeps containing holder occupied. Resize/truncate zero must not disarm aggregate
siblings; partial mutation releases selected type, not unrelated sibling classes.
Add public negative alias=[] + component.resize(0) cases, including alias forwarding;
positive controls last use/whole replacement. Existing Select Name aliases currently
Acquire a fresh path without copying selectedObjects: preserve these identities too,
so later RHS effects cannot silently lose their lifetime obligation. Direct tuple
component assignment remains separate/unimplemented. Do not enable it as part of
this fix without its own tests.

3428 cumulative log latest at112 array checks/6 class executions, not terminal.
Only notes and diagnostic log changed during freeze; no compiler/grader changes.

### Partial-holder correction

Cumulative job 3423 completed exit 0 before this correction: both configurations
had clean builds, 212 layout executions, 12 tuple executions and 13 tuple
rejections. That passing suite did not cover the new alias defect.

Check-only scratch baseline 3435 accepted four invalid returns: empty array
component assignment, a forwarded alias, direct resize(0), and alias resize(0).
No faulty generated program was executed. Array depth counts allocation
boundaries, not ownership extent: an inline component can have depth zero
without being the entire cleanup holder. Clearing it had disarmed a counted
sibling's finalization effect.

The checker now preserves a graph-local partial-holder marker and saved object
obligations through derived selections. Partial assignment/shrink releases the
selected type without clearing containing-holder occupancy. Whole-holder
replacement keeps its previous behavior. This is effect analysis only; runtime
IR cleanup is unchanged. Direct tuple-component assignment remains unfinished.

Job 3438 built Debug cleanly and passed 13/13 tuple executions and 17/17
intended rejections, including a valid mutation-before-cleanup control.
Whitespace passed. Broader post-fix regressions are next; pre-fix cumulative
success is not post-fix evidence.

Post-fix job 3440 is running with compiler and grader inputs frozen. Debug
completed clean build, 13/13 tuple executions, 17/17 tuple rejections, 112 array
executions, 154 class-checking cases, and 92 ownership IR cases. Release build,
tuple and array checks have completed; collect the terminal receipt and remaining
summaries before claiming all selected checks passed. No post-fix full cumulative
or sanitizer claim.

Next inspected increment: direct numeric tuple-component assignment. Lowering's
SelectStorage already selects tuple addresses before RHS and Store uses the
shared drop dispatch. CheckAssignment currently routes all Members through
CheckField; numeric-selector syntax can route through CheckExpression plus
WritableRoot/protectedArrays without evaluating class receivers twice or
breaking constructing-self handling. Keep component compound assignment outside
this slice. BuildBlock's indexedFieldDestinations exemption should apply only to
real class-field destinations, not tuple components. Add public assignment,
replacement-copy, immutable/shape rejection and sibling-cleanup fixtures before
enabling this path. No assignment implementation has been changed yet.

Job 3440 completed exit 0; 3447 verified BOTH configurations: zero warnings/errors,
13/13 tuple executions, 17/17 rejections, 112 array executions, 154 class checks,
and 92 ownership IR cases. Partial-holder correction is verified at that scope.

Direct tuple assignment baseline 3449 failed the six new fixtures at LYC329
(class-field lookup), while existing 13/17 cases passed. Numeric Member
destinations now share Index checked-storage handling; identifier fields retain
partial-self constructor handling. The class-field growth exemption excludes
tuple destinations. No new lowering mechanism was needed: SelectStorage already
captures the component address before RHS, and Store applies type-directed drop.
Job 3451: Debug build clean, 16/16 executions and 20/20 intended rejections;
whitespace passed. New cases cover nested scalar replacement, independent array
copy, counted replacement order (AliveBdone), immutable/type failures, and
sibling finalization after direct empty assignment. Broader rerun follows.


### Destructuring implementation boundary

Assignment regression job 3452 completed exit 0. Saved summaries verified both
configurations with clean builds, 16/16 tuple executions, 20/20 tuple rejections,
112 array cases, 154 class checks, and 92 ownership IR cases.

Destructuring baseline 3460 rejected all nine new pattern cases at LYC306; the
existing 16/20 remained passing. CheckDestructuring now checks the initializer
once before attaching names, validates the complete nested shape and duplicate
names, and creates scope-owned symbols attached to named pattern leaves. `_`
creates no symbol. Existing single-name binding representation remains unchanged.
The hint-only scan distributes known component types without publishing symbols.

Lowering evaluates one tuple snapshot, selects components with SelectTuple,
copies named leaves with existing Copy/Ref/Load into ordinary Local holders, then
explicitly drops the initializer. It does not Move from borrowed component
addresses. The dropped holder remains empty for harmless later scope cleanup.
An iterative stack avoids recursive pattern traversal. All named copies exist
before any discard finalizers run. Lifetime analysis likewise registers named
holders/identities before applying discarded-component release summaries.

Job 3467 passed 20/20 executions and 25/25 rejections. Expanded job 3470 passed
22/22 executions and 26/26 rejections, clean Debug build and whitespace check.
Added evidence: branch hint propagation; named counted leaf stays alive after
initializer cleanup; discarded finalizer invalidates a named ordinary handle
(LYC330). No sanitizer, full cumulative, or Release destructuring claim yet.
The key lesson is that destructuring cleanup affects newly bound values, not
just anonymous discarded temporaries. Broader checks follow with frozen inputs.


### Destructuring regression boundary verified

Job 3472 completed with exit 0. Debug and Release each built with zero warnings/errors and passed 22 tuple executions, 26 tuple rejections, 112 array executions, 154 class checking cases, 92 ownership IR cases, and 21 scalar IR cases. Whitespace checking also passed. These are selected regression suites, not a new full cumulative or sanitizer run.

Next model prerequisite: generalize the qualified member-scope owner to class or struct declarations, preserving two-way attachment and teardown. Struct source semantics are not enabled by this change.

## Struct model prerequisite — verified, source structs still unsupported

The analysis continues to own one canonical table. `SymbolKind.Struct` and
`StructType` now provide nominal value identities keyed by actual declarations.
`SemanticScope.MemberOwner` is the authoritative class/struct member-owner link;
`Class` is a computed class-only query, not a second stored reference. This keeps
existing constructor and finalizer paths class-specific. `Fields()` enumerates
actual field symbols in declaration order, without a copied field inventory.
Member attachment and destruction retain their two-way guarantees.

Struct canonical keys use the existing nominal declaration dependency. `Class`
on a canonical type returns only class declarations, including through arrays;
struct identities cannot accidentally masquerade as object handles. The model
probe checks reuse, nominal distinction, wrong-kind/foreign-owner refusal,
recursive array dependencies, and complete retirement without damaging unrelated
declarations.

`HasOwnedContents` now follows inline struct fields, stopping at arrays and
counted handles. `HasFiniteLayout` independently traverses inline struct fields
and tuple components with an iterative active/completed DFS. Active recurrence is
an infinite inline layout; a repeated completed component is merely shared type
structure. Arrays and class handles stop layout expansion. This is a layout
query, not a proof of borrow safety or ownership balance. Invalid draft layouts
remain destructible.

Evidence and corrections:

- Initial scope rename build 3483 failed because class-specific nullable owner
  paths still used `Class`. Preserving a computed class-only query was the correct
  separation, rather than mechanically broadening those paths to structs.
- Member-scope probe: 240 checks per Debug/Release; CLI check 3489 passed 154 class
  checks and 22 tuple executions/26 tuple rejections.
- Struct identity probe: 250 checks per configuration.
- Initial ownership probe 3497 failed: a fixture attempted a reference field,
  but the symbol constructor still admits references only for locals/parameters.
  Removed that premature fixture instead of silently widening stored-reference
  support. Corrected probe: 253 checks per configuration. Borrow-containing
  structs and their escape/effect rules remain an explicit future slice.
- Layout probe 3501: 269 checks in each configuration, zero build warnings/errors.
  Cases include direct/mutual/tuple-mediated cycles, legal array recursion,
  repeated shared components, class handles, and invalid-draft teardown.
- CLI regression 3502 completed exit 0. Debug/Release each cleanly built and passed
  5 canonical inspection cases (selfcheck rejects 4 defects), 22 tuple executions,
  26 tuple rejections, and 154 class checks. Whitespace check passed. Logs are
  `book/build/ch10-struct-model-{Debug,Release}-*.log`. No new cumulative or
  sanitizer claim.

Next: source structs remain gated. Parser inspection found that `Aggregate` only
tries methods for classes; `StructBody` in root `lyric-grammar.peg` also needs
checking/updating against the adopted method syntax. Update both recognizers and
public syntax fixtures before enabling source struct semantics. Named construction
already has `Record`/`FieldValue` syntax. Finish the public struct execution and
rejection contract alongside source typing, default evaluation, IR and C lowering;
do not treat the model probe as source-language acceptance.

## Struct syntax and source-declaration slice (jobs 3513–3532)

Both recognizers now admit function members in struct bodies. The handwritten
Aggregate parser shares its function-or-field attempt across structs/classes;
the PEG uses AggregateMember. Function self parameters now permit an optional
type annotation, preserving bare self. Adapter syntax is unchanged. Syntax
admission does not establish receiver matching or method execution.

The first baseline failed exactly five new positive forms (150/155 recognition).
The first changed run built and passed 70 trees but failed two recognition
cases: an obsolete no-struct-method rejection, and explicit self:ref Point.
Corrected the former to acceptance and added the missing optional self annotation
to both recognizers. Job 3521 completed exit 0: Debug/Release each 70 trees,
155 recognition cases, 7 boundaries; independent Peggy passed all 155 fixtures.
Logs: book/build/ch10-struct-syntax-fixed-*.log. Earlier failure logs preserved.
Lesson: sharing the method parser did not imply all adopted receiver syntax was
already admitted. This was a parser prerequisite, not semantic completion.

Source struct declarations now collect nominal names and member scopes before
resolving fields. A separate temporary struct traversal list is not persistent
model ownership. All fields resolve before HasFiniteLayout runs for every struct;
this catches an invalid inline type even when reached through an array. LYC341
reports infinite inline layout. Class constructor/finalizer semantics remain
class-only. Default expressions and methods remain explicitly unsupported in
this declaration-only slice. ReadType's temporary allowStructs gate propagates
through nested tuple/array field types, but does not admit struct values to
execution paths before lowering supports them. This is staging, not language
policy. No arbitrary aggregate default has been decided.

Public graders/ch10/structs.py contains 14 retained check-library cases and is
in make test: 8 successes (empty/scalars/forward/sharing/array recursion/handles),
4 infinite-layout failures, and 2 duplicate-declaration failures. Requires exact
exit/stream conditions and a correctly located expected diagnostic, not any
nonzero exit. Baseline job 3527 failed all 14 with the old unsupported gate.
Initial implementation job 3528 failed C# CS0136 (new name local overlapped the
existing enclosing function-name local); rename to structName fixed it. Job
3530 clean Debug build and 14/14 checks passed. No struct execution claim.

LIVE at this entry: job 3532 runs full make test with frozen compiler/graders,
logging book/build/ch10-struct-declarations-cumulative.log. Collect outer receipt
and actual summaries before claiming cumulative success or editing test inputs.
No children/commits/pushes. Next: after verification, struct construction/value
IR/backend and source semantics; methods/defaults/contained borrows still ahead.

### Struct IR contract prerequisite

Cumulative declaration milestone: job3532 completed exit 0. The saved `book/build/ch10-struct-declarations-cumulative.log` contains successful Debug/Release suites, including 70 syntax trees, 155 recognition cases, 7 boundaries, 154 class checks, 212 layout executions, 21 scalar IR, 92 ownership IR, 33 IR-C, 5 canonical cases, 22 tuple executions/26 rejections, and 14 struct declaration/layout checks. This precedes the following IR edits; it is not post-edit cumulative evidence.

Added explicit NewStruct, SelectStruct, DropStruct vocabulary in Common/IR.cs and contracts in IR/Validation.cs. NewStruct consumes values in nominal field declaration order, with finite-layout and exact field-type checks; lowering must first evaluate written initializers in source order. SelectStruct uses the actual field Symbol and requires its MemberOwner to equal the holder type's declaration. No spelling lookup or duplicate field registry. DropStruct requires an ownership-bearing struct; Copy admits structs; Move and reciprocal consuming-operand checks recognize completed struct owners and construction. Borrowed fields remain explicitly outside this construction slice.

Development probe uses real model graphs: valid construction/selection, reversed and missing operands, foreign/missing field identity, wrong result type, missing ownership transfer, wrong cleanup type, whole-owner copy and returned Move. Job3552 exit0: 280 model checks in each Debug/Release, zero warnings/errors, log `book/build/ch10-struct-ir-model.log`. These establish validation behavior, NOT recursive-copy execution. Public source construction and C struct operations are not implemented yet. Job3555 is running rebuilt Debug/Release scalar/ownership/C-emission regressions; keep compiler/probe/grader inputs frozen until its receipt.

Backend inspection lesson: TupleLayouts currently combines discovery with inline layout ordering, following array elements. Recursive structs require discovering all needed types separately from ordering inline dependencies; arrays break layout cycles. Empty structs need a standards-conforming C representation without inventing a visible field. Existing Record syntax already preserves written FieldValue order.

Post-contract regression completion: job3555 exited0, summaries read in3557. Debug and Release each built with zero warnings/errors and passed21/21 scalar IR,92/92 ownership IR,33/33 IR C-emission cases. `git diff --check` passed. No live job or input freeze remains. Next implement C struct record discovery/layout/copy/drop and direct IR backend probes before source admission; do not claim current emitter supports these new operations.

### Struct backend prerequisite — shared aggregate helpers (in progress)

Emission/Compilation.cs now handles NewStruct/SelectStruct/DropStruct, with actual field-symbol selection and shared tuple/struct empty/copy/drop/array-leaf helpers. Canonical identities key temporary emitted names; struct fields come from MemberScope.Fields. Discovery follows arrays and aggregate fields; a separate ordering walk follows only inline aggregate dependencies. This distinction is necessary for legal A->[B], B->A layouts. Empty C records receive a private storage byte. Struct array growth remains rejected: cleanup-safe empty storage is not an adopted observable default policy.

The development tuple-emission probe now accepts optional `struct`, using a checked Pair declaration and real struct IR. Both modes assert independent copied array backing after original mutation/drop and recursive nested-array leaf copying at O0/O2. Job3575 struct mode exited0. This does not yet test empty/recursive struct layout emission or source construction/lowering. Probe setup initially omitted a top-level declaration separator (LYC201); removing a trailing field separator alone did not fix it. Corrected source then exposed a real emitter fallback treating every non-class declaration as a function; explicit Function symbol filtering fixes that assumption. Initial backend build also required a nullable assertion after moving the aggregate predicate into a helper. No visibility was widened.

Job3576 runs frozen tuple+struct probes, then Debug/Release builds, public tuple and IR-C regressions, and whitespace. Receipt pending at note write; do not claim the whole batch passed. Next: collect receipt/summaries, add empty/recursive-layout execution coverage, then source construction tests/checking/lowering. No source struct values admitted by these backend changes alone.

### Aggregate backend boundary: empty and recursive structs

Collected job3576 exit0. Tuple and struct direct-emission probes passed O0/O2;
Debug and Release builds each had zero warnings/errors and passed all 22 tuple
execution/26 tuple rejection cases and 33 Chapter9 C-emission cases. This is
focused regression evidence, not a new full cumulative or sanitizer run.

Extended the development emitter probe with `empty` and `recursive` modes.
The first empty probe incorrectly emitted DropStruct for a nonowning empty
value; ValidateOperations rejected it. Corrected the probe rather than weakening
the ownership contract. Empty structs construct/copy using the private C byte,
without a language-visible field or resource cleanup obligation.

The recursive mode checks Branch { children:[Leaf] } and Leaf { parent:Branch }.
It constructs a finite populated value (a Leaf whose parent has no children),
copies and drops the enclosing Branch using production IR and C helpers.
Job3586 exited0; saved ch10-struct-recursive.log records O0/O2 success. Empty
mode passed both levels in job3582, saved ch10-struct-empty.log. These smoke
checks do not assert allocation balance or prove independent backing for every
recursive node; the earlier Pair harness separately checks independent backing.

Lesson: discover aggregate types across all dependencies, but topologically
order records only across inline fields. Array descriptors break storage cycles.
Source struct construction is still gated. Next: public construction cases,
actual field binding, written-order snapshots, declaration-order NewStruct
operands, and shared lowering copy/cleanup/owner registration. Contained class
handle and borrow effects must be extended before admitting those field values.

### Struct source-construction baseline and ownership prerequisites

Added `graders/ch10/struct_values.py`: five retained execution fixtures for empty
construction, named field order, left-to-right snapshots, nested structs, and
independent array-valued field copies. The existing tuple runner now accepts
case lists and a label, retaining its default tuple behavior and exact byte,
exit-status, stderr, and O0/O2 contract. Baseline against the Debug compiler:
all five parsed and failed specifically with LYC306 Unsupported Record; zero
source-construction passes. No source admission has been added yet.

Prepared lowering dispatch: structs use Copy; owning struct holders use
DropStruct; NewStruct participates in the existing owner registration. Extended
semantic contained-class traversal through nonreference struct fields using
actual member scopes and the existing visited-type set. Recursive type graphs
therefore terminate without hiding contained class lifetime effects. Borrowed
fields remain unsupported; this traversal is not contained-borrow admission.

Verification: job3601 unchanged Debug DLL passed 22 tuple executions and 26
intended rejections after runner parameterization. A source-only ReadKind edit
occurred during that run, but its DLL and grader inputs were unchanged; no build
was run before collecting the receipt. Job3608 then rebuilt Debug and passed
22 executions/26 rejections plus all 14 struct declaration/layout cases and
`git diff --check`. Logs: `book/build/ch10-struct-source-prerequisite-*`.
No new cumulative, Release, sanitizer, or struct-source execution claim.

Next: connect Record evaluation to actual field-symbol facts, preserving source
order while arranging saved operands in declaration order. Extend inline member
selection/writable roots, partial-holder lifetime tracking and aggregate temporary
registration to structs; visit record expression children explicitly (not type
or FieldValue wrappers). Keep defaults, methods and contained borrows gated until
their own contracts are implemented. Source construction must not be admitted
with only the C layout path connected.

### Source record construction connected (jobs 3612–3645)

- `Check.cs` binds named construction to canonical struct types and actual field symbols before evaluating initializers. Field wrappers hold semantic facts, not copied/name-reconstructed field inventories. Required fields, duplicate names, unknown fields and value types are checked. Explicit expressions execute once in written order, with each field's declared type as context. Defaults/methods/contained-reference fields and broader struct signature admission remain gated.
- `Lowering.cs` evaluates initializer expressions in written order, then arranges saved values by actual field identity in declaration order and transfers ownership into `NewStruct`. Read selection snapshots the receiver; writable selection traverses original storage. `SelectStruct` carries the actual field symbol; tuple selectors retain integer literals. Checked struct declarations supply metadata, not class destruction bodies or function signatures.
- Field lookup and hint discovery now recognize struct nominal declarations. Struct fields inherit the containing root's mutability. Lifetime selection preserves partial-holder markers and saved handle obligations; class-growth destination exemption is limited to actual class receivers. Record traversal visits initializer expressions (not type names or wrappers), checks saved objects after later initializers, transfers child aggregates, and registers one completed owner.
- Initial build 3630 failed because a type-syntax SemanticFact omitted its mandatory nullable symbol argument; corrected to explicit null. No grader ran on that failure. Build 3633 succeeded but all five fixtures reached the top-level LYC340 struct declaration gate. Lowering now skips checked struct metadata declarations; 3636 passed all five source execution cases.
- Expanded Debug run 3641 passed 8 executions at O0/O2 and 7 intended rejections (readback 3642). Covered empty/nested structs, reversed field order, effectful snapshots, independent arrays, field mutation, counted copy finalization, last-use before resize, missing/duplicate/unknown/wrong-type fields, nominal mismatch, immutability and borrow invalidation.
- Added ordinary-handle success plus stale-handle, later-field invalidation and partial-sibling-cleanup rejection cases, making 9 execution/10 rejection cases. Retained `struct_values.py` in root make test. Live job 3645 builds and checks both Debug/Release: struct values, tuples, declarations, class semantics and ownership IR, then whitespace. Inputs frozen until terminal receipt; no claim yet for its final result, Release, cumulative tests or sanitizers.
- Lesson: evaluation order and field storage order are separate guarantees. Actual field links let the implementation preserve both. Inline fields retain parent mutability and sibling cleanup obligations; they cannot reuse the class-handle growth exemption.

Job 3645 completed exit 0; summaries freshly read in 3649. Debug and Release each built with zero warnings/errors and passed 9/9 struct-value executions (O0/O2), 10/10 intended struct rejections, 22/22 tuple executions, 26/26 tuple rejections, 14/14 declaration/layout checks, 154/154 class checks and 92/92 ownership IR checks. Whitespace passed. Freeze lifted; no live jobs. This is focused regression evidence, not a new full cumulative/sanitizer result. Next: explicit struct annotations and value/reference parameters/returns, with retained tests first and no accidental broadening of class fields, array defaults or contained-reference admission. ReadType call-site inventory 3648: struct fields178 true; class fields213 default; function parameter263/result270; destructuring annotation1910; binding annotation1989; recursive type2264+; hints cast2511/local2642; record2710 true; actual cast3055. Offsets will drift. Initial source record implementation remains gated in default ReadType; inferred values now execute.

### Typed struct value interfaces

Enabled canonical struct types explicitly in parameter/result signatures, ordinary
and destructuring local annotations, and their hint pass. Class-field admission
remains unchanged. Array literals now admit struct leaves, using the existing
recursive aggregate helpers; this does not grant aggregate array-growth defaults.
The public struct-value suite adds typed locals/reference locals, value and ref
parameters, owned-array and empty returns, nominal parameter/return mismatches,
counted return ordering, return invalidation by parameter cleanup, typed tuple
patterns and arrays of structs with independently copied nested arrays.

Baseline job3653 retained the five new execution failures and two rejection
mismatches, all at the prior TypeName gate. Job3654 clean Debug build then14/14
executions and12/12 rejections. Job3655 added counted ownership observations:
15/15 and13/13. Composition job3658 found an older Array literal gate (16/17),
not a C helper failure. Corrected that gate; job3662 exited0 with all17 executions
and13 rejections. The array composition case mutates a returned nested array and
checks that the original remains unchanged. These are O0/O2 execution checks,
not sanitizer or allocation-balance evidence.

Focused both-configuration regressions job3663 are pending: compiler and graders
frozen until receipt. Logs book/build/ch10-struct-interfaces-{Debug,Release}-*.
Next: explicit defaults, after signatures exist, checked in declaration scope;
construction must evaluate supplied expressions first and omitted defaults in
field declaration order. Methods and contained reference fields remain gated.

Typed-interface regression job3663 completed exit0; summaries inspected job3669.
Both Debug and Release build with zero warnings/errors. Each passed17 struct
executions/13 intended rejections,22 tuple executions/26 rejections,14 struct
layout checks,154 class checks,5 canonical cases+4 defect selfchecks,21 scalar IR,
92 ownership IR and33 C-emission cases. Whitespace passed. No jobs remain live.
This is focused regression evidence, not full cumulative or sanitizer evidence.

Default-initializer design investigation (not implementation): Check.cs lifetime
analysis keys objectValues and aggregateTemporaries by syntax identity. Reusing
one declaration expression in several constructions therefore needs explicit
execution identities; blindly splicing the same node is unsafe. Defaults must
resolve/check after function signatures in declaration scope, without self or
sibling visibility, and execute after supplied expressions in field order.
Candidate approach: field-linked initializer execution body with real reciprocal
relationships, analogous in honesty (not semantics) to generated destruction IR;
no fabricated source function declaration. Common/IR.cs currently only Source
and Destruction function kinds. This design has not yet been implemented or
finalized. Next inspect semantic callable/lifetime ownership requirements and add
public defaults cases before changing admission. All existing default/method and
contained-reference gates remain.

## Field-default model prerequisite

Added six retained cases to `graders/ch10/struct_values.py`: supplied-before-default order and skipping supplied defaults; fresh independent array defaults; all-supplied skip; invalid declared type; sibling invisibility; caller-local invisibility. Baseline job3675 exited1: all existing17 execution/13 rejection cases passed; three new positive and three new negative cases all hit old Field LYC306 gate (not parse errors). Saved `book/build/ch10-default-baseline.log`. Defaults remain unsupported; inventory now20 executions16 rejections, not all passes.

Implemented actual field-owned initializer scopes: `SemanticSymbol.InitializerScope` / `SemanticScope.InitializerField`, paired add/remove. Existing SemanticScope(symbol) constructor accepts struct fields, derives lexical parent through MemberOwner.Scope, and registers analysis/field ownership. No invented function symbol, no sibling/self namespace. Class fields and duplicate scopes refused before mutation. Scope/field teardown removes both ends; direct initializer-scope removal clears dependent IR first. Scope-only model probe3694 clean Debug/Release289 each.

Added IrFunctionKind.FieldInitializer. Existing declaration/body relationship links actual field to body, requires live struct-field initializer scope, rejects wrong kind/duplicate/missing scope. Structure validation checks initializer owner/scope backlinks. Constant-return real-model probe validates all three IR passes and scope-destruction invalidation. Initial3713 build failed nullable scope dereference; fixed by explicit field.Scope nonnull requirement, not null suppression. Retry3716 exited0 bothconfigs; summaries pending collection by3719. This is model evidence, NOT source default execution or C backend support.

Next: explicit default invocation operation/validation/emission and field-owned body lowering; checker admits/checks expressions after callable signatures in declaration scope; initializer bodies join SAME function-effect fixed point. Current CheckBorrows entry Check.cs450–481 assumes FunctionScope parameters and BuildBlock(body); initializer expression must evaluate, transfer result, cleanup remaining owners, then check returned handles after cleanup. Reusing one declaration-site syntax node directly at multiple constructions risks merging aggregateTemporaries/objectValues keyed by SyntaxNode; separate invocation bodies avoid that. Supplied fields evaluate first; omitted defaults declaration order; all values transfer into NewStruct only after evaluation. Preserve current array-growth/default policy and no contained refs yet.

Regression3719 live at this note: Debug build then retained21 scalar IR and14 struct declaration/layout checks then whitespace; inputs frozen until actual receipt. No commits/pushes/delegation. Public default cases must not be reclassified as expected unsupported successes.


### Default invocation and IR backend prerequisite

The earlier job3719 completed successfully: clean Debug build, 21 scalar-IR and 14 declaration/layout checks; model296 each configuration and whitespace clean. Source defaults remain gated (six retained missing-feature cases).

`CallInitializer` is a distinct zero-argument IR operation. It names the actual struct field, requires its reciprocal initializer scope and live FieldInitializer body in the same program, and returns exactly the field type. Its result is an owning holder when appropriate; ordinary Call parameter/transfer rules are unchanged. Model tests exercise valid invocation, wrong result type, extra operands, and non-field targets. The first test created a semantic declaration after IR construction and correctly invalidated that IR; declarations now precede IR. One edit refused a nonunique anchor; only Common had been saved, and validation was subsequently edited with a unique anchor. An attempted --help invocation was not supported by the probe runner and ran no tests.

C emission traverses actual IR functions for zero-argument initializer prototypes and bodies, uses InitializerScope for local storage, and has an explicit initializer-call branch. The development aggregate probe has an initializer mode: checked struct metadata plus a real field-owned body returning a freshly allocated array, invocation moved into a struct, and two further emitted initializer calls checked for distinct backing storage and independent contents. First C harness build failed on a duplicate test variable name, corrected in harness only. Retry job3749 passed -O0 and -O2; no sanitizer/leak-freedom claim. Model retry3733 passed both configurations; actual counts in its saved log. Job3750 retained Debug IR scalar/ownership/C suites and whitespace exited0. Logs: ch10-default-call-model[-fixed].log, ch10-default-call-emission[-fixed].log, ch10-default-call-{build,ir,ownership,c}.log.

Lowering now creates initializer bodies for fields with checked initializer scopes, reuses Expression plus End for implicit returns (widen, transfer result, clean remaining owners, return), and registers CallInitializer results in the ordinary owner inventory. Job3755 clean build; this path is not yet exercised by source defaults because the semantic gate remains. Next connect declaration-scope expression checking, field-owner effect summaries and initializer implicit-return lifetime analysis, and record omission/default calls with supplied-LTR then omitted-declaration-order execution. No commits, children, or publication.

### Source defaults: checking, effects, and construction (verification in progress)

The field-default gate is now replaced by real checking. Struct field registration creates an initializer scope for each explicit default; methods remain gated. After ordinary function signatures and bodies are checked, every initializer expression is checked in its field-owned enclosing lexical scope and required to produce the field type, even if all constructions override it. Initializer bodies join the existing symbol-keyed effect/overlap/class-destruction fixed point. They evaluate the expression, transfer the result, clean remaining owners, then check the preserved result for cleanup invalidation, matching explicit returns.

Record lifetime analysis evaluates written expressions first, then visits omitted fields in declaration order. It applies each field initializer's exported summaries without reevaluating its syntax in caller context. Fresh default-result object handles are saved through subsequent default effects; both explicit and default handles are checked before assembly. Record lowering likewise finishes explicit snapshots, emits zero-argument CallInitializer operations for omissions in declaration order, then arranges and transfers all field values into NewStruct by actual field identity. Required fields without defaults remain errors. Initializer scopes exist before any source body is checked, so forward constructions can bind omitted fields without depending on the order defaults are checked.

Job 3768 built Debug cleanly. Job 3770 passed the previously gated default tests: 20/20 execution cases and 16/16 intended rejections. Job 3772 additionally passed nested defaults, counted result copying/finalization, skipped-effect control, later-default invalidation of explicit/earlier-default handles, and transitive return-cleanup rejection (23 execution / 19 rejection cases). Logs: `book/build/ch10-default-source-{build,values}.log` and `ch10-default-effects-values.log`. Source-default successes execute at both O0/O2 through the retained runner; these are not sanitizer or allocation-balance evidence.

Job 3773 is pending: frozen Debug/Release build + struct values (two more rejection cases for initializer-local cleanup and class-growth borrow invalidation), tuples, declarations/layout, class semantics, ownership IR, C-emission regressions, then whitespace. Do not infer its completion from individual logs. Defaults do not grant implicit aggregate array-growth defaults, contained references, or methods. No commits or pushes.

Lesson: default initialization is a real invocation with a fresh returned owner and exported effects, not caller-scope syntax substitution. The latter would both resolve names incorrectly and merge temporary identities for repeated execution of one declaration AST.

Job 3773 stopped in the first Debug value suite: 23/23 executions, 20/21 rejections. `struct-default-own-cleanup` reported LYC306 for If instead of LYC330. The growth-borrow case correctly rejected. No later suite or Release result belongs to that failed run. Inspection found value-if's enclosing-owner lookup recognized only Function scopes, not InitializerField. The correction accepts either actual execution owner and reuses the existing branch checker. Added a positive conditional default returning an owned array, independently constructed twice and mutated once. This is a real missing context path, not a fixture relaxation.

Job 3781 reruns the frozen corrected inputs in both configurations, preserving failed logs and writing `ch10-default-integrated-fixed-*`. Pending until its outer receipt. Lesson: an expression-body execution owner must be recognized consistently by branch typing as well as by calls, cleanup, and effect analysis.

Job 3781 completed exit 0, including git diff --check; individual summaries inspected in 3785 and outer receipt collected. Debug and Release each pass 24/24 struct-value executions, 21/21 intended struct rejections, 22/22 tuple executions, 26/26 tuple rejections, 14/14 struct declaration/layout checks, 154/154 Chapter 8 checks, 92/92 ownership IR cases, and 33/33 C-emission cases. Both builds report zero warnings/errors. These are focused regressions, not a new full cumulative or sanitizer claim. No live jobs remain. The public grader README now documents struct-value execution/default ordering and its exact success/failure contract.

Next authorized implementation: struct methods, including value versus explicit reference receivers and static calls, then exact qualified separate-body matching. Initial read-only inspection: Check.cs top-level declaration collection requires simple Name (qualified bodies gated); class member/signature pass around 210–323 has class-specific receiver/constructor checks separate from function signature checking. Preserve that distinction. Actual field/member ownership already supports structs. Inspect full current spec around 444–494 before changing method semantics; earlier search output was clipped and not a full reading. Contained-reference fields and aggregate growth-default decision remain separate, unresolved work. No commits/pushes/children.

## Struct methods: body/signature prerequisite

Read the agreed spec receiver/static/separate-body rules (§4, lines 444–494).
Added four retained positive execution cases (value self, explicit ref self,
static factory, owned field copying/return). Unchanged compiler baseline
`ch10-method-baseline.log` job3795: 24/28 executions, 21/21 rejections;
all four new cases parse and reject LYC306 Function at the struct-member gate.

Struct methods now register ordinary actual function symbols in the existing
member scope and body/signature passes. Implicit self derives its type from
MemberOwner (StructType versus ClassType); explicit struct self must be first
and have the enclosing nominal type, otherwise LYC304. No self means static.
Class constructor/final checks remain class-only. IR collects struct method
bodies as ordinary functions alongside distinct field initializer bodies;
emitter collects nominal methods without adding structs to class allocation
metadata. No new model relationship or fake declaration was introduced.

Six public check-library cases cover value/ref/static bodies, wrong nominal
receiver, duplicate method, and wrong return type. Debug build clean;
job3810 passed 20/20 declaration/layout and 154/154 class checks.
Added retained `struct-method-bodies-emitted`: uncalled methods must validate
and emit, not be silently skipped. Job3816 ran the existing exact-byte O0/O2
runner on all implemented struct cases, explicitly excluding ONLY the four
pending method-call cases (unchanged required public positives). Exit0.
Full struct suite is NOT passing; method invocation still gated. No newest
Release/cumulative/sanitizer evidence. Collection build3814 exit0.

Next connect instance/static call checking, hints, lifetime argument mapping,
and IR call lowering. ClassCall allocates constructors and cannot simply become
a struct-call path. Reference receiver `(ref p)` must unwrap groups, check once,
retain address through later args, and enter the saved ref-argument list first;
existing parameter-order substitution can then reuse its effects/obligations.
Value receiver must own a copied parameter and transfer AFTER later arguments.
Static type namespace is not an evaluated receiver. Source functions retain
actual symbol identity. Qualified separate-body matching remains later work;
contained-reference fields and aggregate growth-default policy remain deferred.

## Inline struct method invocation

Connected static and instance calls after the checked-body prerequisite. Static `Type.method` resolves the actual method in the nominal member scope without evaluating the type namespace. An instance method requires parameter zero named self; explicit reference mode must match, with no auto-borrow or ref-to-value coercion. Grouped references use the existing writable-storage checker and retain group facts. Hint lookup follows nominal declaration members rather than class-only compatibility views.

Receiver is parameter zero in ordinary call IR, not a new operation or a class call. Lowering evaluates it before written arguments and delays all owner transfers until evaluation finishes. Lifetime analysis includes reference receivers first in the existing saved-reference/formal substitution list, checks them after later arguments, and transfers value receiver aggregate ownership. Static calls have no runtime receiver. Class constructor/allocation/final semantics remain unchanged.

Evidence: static prerequisite Debug build3825 passed; full suite3826 had exactly three pending instance-call failures and static success. First coordinated receiver build3829 failed C# CS0136 (local owner collision with array branch); renamed receiverOwner, build3831 and full suite passed29 executions/21 intended rejections. Added explicit-mode/immutable/type-namespace and later-argument array-growth reference rejection cases plus value-receiver snapshot control. Job3833 terminal0:30 executions/26 intended rejections at O0/O2; read summaries3834, receipt3833; git diff --check passed3834. These are Debug source execution checks, not sanitizer/formal safety evidence.

Lesson: the receiver is parameter zero with an explicit mode. Namespace resolution and argument evaluation order need special syntax handling, but ordinary call validation/copy/cleanup should remain authoritative. Saved receiver addresses must survive later argument effects; copied values instead preserve their earlier snapshots.

Job3835 pending: rebuilt Debug and Release struct values, struct declarations/layout, tuples, classes, ownership IR and whitespace; inputs frozen until terminal receipt. Separate qualified declarations/bodies, contained borrows, broader Chapter10 teaching/review/reconstruction remain unfinished. No commits/pushes/children.

Inline method follow-through: job3835 terminal0; summaries3840 verify Debug AND Release zero warnings/errors,30/30 struct executions26/26 intended rejections,20/20 declaration/layout,22/22 tuple executions26/26 tuple rejections,154/154 class checks,92/92 ownership IR. Whitespace passed within3835. No current full cumulative/sanitizer claim. No live jobs/frozen inputs. Next separate qualified body matching. Inspected spec465–500 and Common/SemanticModel.cs112–145: symbol has one weak Declaration plus syntax DeclaredSymbol reverse; do not overwrite it to point at implementation. Proposed explicit second implementation-syntax link with reverse membership, exact signature check, same actual method symbol; not yet implemented. Top-level Check still rejects QualifiedName; parser already parses it. Combined declaration/model/parser read3838 clipped, not full inspection. Preserve original declaration contract, no duplicate method symbol or service registry.


## Separate qualified struct method bodies

Kept `SemanticSymbol.Declaration` as the original contract. Added a weak reciprocal
`Implementation` / `SyntaxNode.ImplementedSymbol` relationship and `BodySyntax()`
selection. Attach/detach validates endpoints before mutation and retires IR only
on success. Symbol destruction detaches both syntax links; syntax destruction
already invalidates the owning analysis. No duplicate method symbol or wrapper.

Checker collects qualified body syntax temporarily, resolves it after all struct
members exist, and rejects undeclared/duplicate/missing bodies with LYC342. Exact
canonical parameter/result types, reference modes, arity, and self/static status
must agree. Ordinary parameter spelling is not signature identity (spec Section 2
now says so explicitly). One ordered parameter symbol per slot uses the body-local
name after matching; declaration and implementation name restrictions are checked
independently. Types resolve outside body-local bindings. IR registers the callable
from its member declaration once, then lowers its selected body. Emitter parameter
scope access already handles this without additional machinery.

Baselines3845/3853 terminal1: all existing30 executions26 negatives passed; four
new executions and ten new matching rejections failed expected unsupported/missing
body behavior. Initial build3855 clean; full source3856 terminal0 passed34 execution
cases at O0/O2 and36 expected rejections. Added declaration-shadowing, duplicate
body parameter, and actual separate-body return checking tests; Debug summary3861
shows34 executions39 rejections. Current fixtures remain frozen under job3860.

Development model3858 terminal0:314 checks each Debug/Release, builds zero warnings
and errors. New checks exercise preserved original declaration, renamed body binding,
duplicate refusal preserving IR, foreign-analysis refusal, detach invalidating IR,
reattach, symbol destruction clearing both syntax links, and syntax destruction
retiring analysis/links. This is model evidence, not independent language grading.

Lesson: callable identity, declared signature, and body-local names are different
facts. Keeping the original declaration plus a real implementation link preserves
that distinction; neither overwritten declarations nor duplicate callables are needed.
Public fixtures currently use one input file and do not prove configured multi-file
package support. Contained-reference fields and aggregate default policy remain
unfinished. Job3860 runs both-configuration focused regressions; no pass claimed
until terminal receipt and summaries. No commits, pushes, or children.

Separate-body follow-through: job3860 terminal0; both configurations clean builds,34 struct executions39 intended rejections,20 declaration/layout,22 tuple executions26 rejections,154 class checks,92 ownership IR; whitespace passed. Full cumulative verification is next, not yet claimed.

## Separate-body cumulative closure and contained-borrow baseline

Full retained `make test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet`
(job3864) completed exit0. Debug and Release builds had zero warnings/errors;
each passed canonical5 plus four malformed-document selfchecks, tuple22 executions
and26 rejections, struct20 declaration/layout checks, and struct-value34 executions
and39 intended rejections, alongside earlier retained suites. Full log:
`book/build/ch10-separate-cumulative.log`. This is not sanitizer evidence or
Chapter10 completion. Inputs remained frozen through the outer terminal receipt.

New public `graders/ch10/borrowed_structs.py` has five execution and five rejection
cases for local alias copies, synchronous use, nested wrappers, target mutation,
last-use-before-resize, forbidden direct/wrapped return and arrays, and copied/call
borrow invalidation. Not integrated into make test while unfinished. The first
baseline invocation was refused by the tool-batch limit and did not execute.
Retry job3892 completed exit1: all ten parsed and reached the existing LYC306
TypePrefix gate. Zero of five executions and zero of five intended rejections
passed; the intended escape diagnostic is LYC343 and invalidation remains LYC326.
Saved log: `book/build/ch10-borrow-struct-baseline.log`.

Bill requested a completion goal. Saved/verified Agent-local lyric-ch10-goal.md
and updated the stale queue entry; goal_control confirmed enabled. Initial
idle_seconds=0 configuration was refused by legacy positive-value validation;
retry with3 succeeded (idle-triggered scheduling, not a three-second timer).
Goal does not expand hands-on authority or authorize child launches.

Model prerequisite in progress: reference fields permitted only on actual struct
member scopes, not class/object fields; computed HasContainedBorrows traverses
canonical tuple/array/struct contents with a visited set and stops at class handles.
No stored cache or second type authority. Ownership/layout queries still skip
reference targets. Source and NewStruct IR gates remain closed. Probe job3895
running at this note; no result claimed yet. Lesson: arrays terminate inline-layout
expansion, immediately establish ownership, but must be traversed for escape
classification. These three queries intentionally have different stopping rules.

Model prerequisite job3895 completed exit0: 330 canonical model checks in each Debug/Release, zero warnings/errors. Summary inspected3897; git diff --check passed. Sixteen new checks cover nested/recursive classification, nonownership/finite borrowed layout, no cached result after field removal, object-field refusal without registration, class-handle distinction and teardown. Source/IR gates remain closed; no source behavior or sanitizer claim. No live jobs at this boundary.

### Contained-borrow IR/backend prerequisite

The model now admits address operands only for reference fields of `NewStruct`; ordinary fields still require values and explicit ownership transfers where applicable. The reciprocal ownership check distinguishes field mode, so borrowing an array descriptor does not move its backing storage. Existing missing-move checks for ordinary owned fields remain intact. `SelectStruct` on a reference field follows its stored pointer rather than taking the address of the pointer slot.

The C backend first publishes named aggregate typedefs, then emits record definitions in inline-dependency order. Discovery includes reference targets, but reference edges do not impose inline layout ordering. Reference fields use typed pointers, construction captures the operand address, copy preserves pointer identity, and cleanup skips the referent. Owned fields in the same struct retain recursive independent copying and cleanup. Cleanup-safe empty pointer fields are null; this is not a source-language default or permission to observe an uninitialized borrow.

The named-record-only prerequisite passed a clean Debug build and all 34 struct executions/39 intended rejections (job3904, logs `ch10-named-record-*`). The coordinated pointer edit initially refused a nonunique selection anchor shared with tuples; no emitter write occurred on that failed command. The narrowed retry changed only struct selection. Model job3918 passed 333 checks in each Debug/Release with zero warnings/errors (`ch10-borrow-address-model.log`), including valid address construction and rejected value/move operands for a borrowed array field.

Development backend mode `borrowed` uses the production IR and emitter, not source admission. Its mixed borrowed-array/owned-array record verifies alias identity, independent owned backing, and referent survival after both aggregate copies are dropped. The expanded scenario also has a self-typed reference field, checking pointer-recursive C layout and preservation of the original target rather than rebasing to the copy. Jobs3924/3925 passed generated C at -O0/-O2 with clean probe builds; logs `ch10-borrow-address-emission.log` and `ch10-borrow-recursive-emission.log`. These are bounded execution checks, not sanitizer or source-lifetime proof.

Focused Debug/Release struct, tuple, and ownership regressions are running as job3926; inputs remain frozen until its terminal receipt. Source TypePrefix/record safety gates remain closed. Next: collect those results, then implement aggregate-contained alias propagation and escape checking in the existing lifetime engine before opening source admission. No commits, publication, or child launches.

Job3926 completed exit0; summaries3932 verified Debug/Release each zero warnings/errors,34 struct executions39 intended rejections,22 tuple executions26 rejections,92 ownership IR cases. Whitespace passed. No live jobs or frozen inputs remain. The existing CheckBorrows call substitution (Check.cs around1405–1440) maps one direct reference parameter to a root/depth; contained component targets are not yet represented. That is the next source-safety prerequisite, not permission to treat the parameter storage itself as its referenced target.

### Contained-borrow lifetime analysis: expanded boundary cases

Read the actual CheckBorrows graph and source contract before attempting source admission. Paths and exported effects currently contain only (Root, Depth); aliases maps each direct reference symbol to one graph handle. ApplyMutation records invalidation causes; use events turn causes into ordered interprocedural obligations. Call substitution maps only direct ref parameters. Select treats inline struct/tuple fields as storage within the containing root. None of these facts yet models the target stored in a reference field.

Important newly identified obligations: multiple nested occurrences of the same Borrowed type must remain distinct; assignment through branches and loops must update possible target sets in control-flow state, not merely a lexical dictionary; a copied alias must not be treated as a fresh acquisition that revives invalid storage. Local scalar scope exit ends storage even when the local has no owned cleanup. A synchronous helper may not write its stack alias into a caller's referenced holder and then return. These follow specification lines 395–399 (target stable and valid through last possible use), not a new exclusivity rule.

Added public cases in graders/ch10/borrowed_structs.py: distinct nested targets and ordered read-before-growth success controls; nested second-target invalidation, transitive helper effects, branch replacement, later argument invalidation, inner-local scope escape, and reference-parameter stack escape rejections. Suite now has 7 execution and 11 rejection cases, still not integrated into make test and still unimplemented. Job3943 captured intermediate 16-case baseline; job3948 captured final 18-case baseline, exit1. Saved ch10-borrow-expanded-baseline.log and ch10-borrow-lifetime-baseline.log. Final log read back: 0/7 execution and 0/11 intended-rejection cases; all fail existing LYC306 TypePrefix rather than intended LYC326/LYC343. No unsafe generated program executed. Existing compiler source and binaries unchanged in this investigation.

Proposed next implementation: graph-local slots for individual contained reference occurrences, with target-copy/replacement events and possible-target state merged at joins; distinguish target provenance from holder storage. Preserve existing ordered effect/obligation fixed point and substitute each formal contained occurrence at calls, including transitive effects and reference-parameter writes. Model end-of-local-storage events separately from owning cleanup. This is design direction, not implemented support. Do not open the declaration/construction gate merely to test one direct copy: a borrow-containing parameter can otherwise expose unsupported selection without any local constructor. First implement and test the graph state representation under the gate, then connect source escape validation and lowering.

### Lifetime graph prerequisites: local storage expiry and component-aware paths

- `CheckBorrows` cleanup frames now hold both cleanup holders and actual local-storage symbols. Destructured and ordinary value locals register storage regardless of ownership; by-value parameter slots register in the outer frame, while reference parameters do not claim their caller's storage. Event 12 ends local storage after that frame's cleanup effects. A separate expired-handle worklist set unions at control-flow joins and rejects later use with LYC326; a fresh acquisition clears only that handle. This is not yet aggregate-copy propagation: copying an alias must preserve the original target's expiry in the forthcoming target-slot events.
- The initial local-storage edit built clean in job3961; job3962 exited0 and saved summaries3964 confirm Debug arrays112, tuple22 execution/26 rejection, struct34 execution/39 rejection. These preservation regressions do not exercise the still-gated aggregate escape cases. By-value parameter registration was added afterward and is covered by the following pending run, not retroactively by3962.
- Temporary lifetime paths and interprocedural summary keys now include `Component`: -1 for ordinary storage, nonnegative for an ordered contained-reference occurrence of a formal parameter. Derived paths and formal-to-actual substitution preserve it; exact target comparison uses root plus component. Distinct formal components may alias and retain conservative ordered overlap obligations. No canonical-type cache or synthetic semantic symbol is introduced. No nonnegative target is generated yet: assignment/target-state integration remains unfinished and source admission stays closed.
- First checked migration3968 refused a nonexistent actual-map anchor before writing. Its shell continued to build unchanged source and returned0, which was NOT migration success. Read the actual `arguments[referenceIndex].Handle` path and retried with `set -e`;3970 applied the migration and built successfully. Keep failures distinguished from the final shell status.
- Job3971 runs frozen Debug/Release builds plus array, tuple, struct and ownership regressions; pending at this writing. Logs `book/build/ch10-contained-path-{Debug,Release}-{build,arrays,tuples,structs,ownership}.log`. No cumulative or sanitizer claim.
- Specification755–776 resolves field-assignment interpretation through the fixed-reference rule: assignment writes the referent, not a rebindable reference. Whole aggregate replacement changes the aggregate value and its contained aliases. Preserve this distinction when wiring target-slot assignment; do not let pointer representation choose source semantics.
- Final3977 summaries and3978 outer receipt confirm job3971 exit0: Debug and Release each zero warnings/errors,112 array cases,22 tuple executions/26 rejections,34 struct executions/39 rejections,92 ownership IR cases; whitespace passed. No live job/freeze remains. Goal updated to this prerequisite boundary. Actual contained-target slots, copy/replacement events, calls and source escape/lowering integration remain next; the eighteen public borrow cases remain gated, not passing.

### Flow-sensitive target-state prerequisite

- Replaced the lifetime worklist's fixed-path-only interpretation with per-event incoming target sets keyed by borrow handle. Acquisition establishes its fixed target; joins union possible targets; event 13 snapshots source targets, invalidation causes, and local-storage expiry into a destination, with an array-depth delta for derived selection. Replacement clears the destination's previous state. Incoming maps and sets are copied before mutation, not shared with predecessors. Existing AcquireDerived now exercises this event; ordinary acquisition still resets only its own handle. Copying state rather than retaining a live source-handle link is necessary so later loop acquisition cannot revive an old alias copy.
- Mutation overlap, use validation, and local-storage expiry consult active possible targets. Original path metadata still supports graph construction and existing call substitution; aggregate occurrence slots, expression projection/copy, branch assignment, dynamic call substitution and ref-parameter write propagation remain unconnected. Source admission remains closed. No claim that the 18 borrowed-struct cases now pass.
- Initial Debug build job3987 succeeded. Regression3990 stopped at array grader exit1: 105/112, with seven improper acceptances (borrow-mutual-invalidation, borrow-helper-later-argument, borrow-helper-forward-invalidation, borrow-helper-recursive-invalidation, borrow-helper-nested-invalidation, borrow-helper-invalidation, capacity-selection-invalidation). Saved ch10-target-state-arrays.log contains generated C because rejection was expected; it is not an unsafe execution success. Inspection found synthetic constructor/default/call mutation descriptors have no acquisition event. The first dynamic dispatch silently skipped these effects. Correction retains fixed-path dispatch when the mutation descriptor has no acquired target state; selected-address mutations use active targets. Lesson: effect descriptions and acquired addresses have different lifecycles despite sharing the old path container.
- Corrected frozen job3994 terminal exit0; summaries3997 verified Debug zero warnings/errors, arrays112, tuples22 execution/26 rejection, structs34 execution/39 rejection, ownership IR92; whitespace passed. Logs ch10-target-state-fixed-{build,arrays,tuples,structs,ownership}.log preserve this evidence; failed earlier log retained. No Release/full cumulative/sanitizer evidence for this increment. No live jobs at this record.
- Next: create formal contained-reference occurrences and local/expression slots using this state transfer; connect projection and whole-value replacement; defer summary substitution to flow state at call events and carry caller-visible reference-holder writes. Preserve ordered read-before-growth behavior, scope expiry and all escape restrictions before opening source checking/lowering.

### Formal occurrences and dynamic call substitution

- Added ordered `ReferenceOccurrences` traversal in `CheckBorrows`: explicit stack, tuple component order and struct field declaration order, reference fields as leaves, no global type deduplication. Arrays/class handles terminate the inline traversal; escape classification remains separate. Parameter initialization creates one acquired handle per occurrence under `(parameter, 0, component)` and stores the ordered handles in `containedBindings`. This scaffolding remains behind source admission: expression/local slots, projection, assignment and mapping aggregate actuals are not connected yet, so no source contained-borrow success is claimed.
- First build4007 failed CS0136 because local `targets` collided with the worklist array. Renamed to `parameterTargets`; build4010 succeeded. Retained ch10-formal-target-build.log and ch10-formal-target-build-fixed.log. This was a C# name-scope error, not a lifetime-test failure.
- Ordinary call actual maps now hold saved handles rather than graph-time paths. Substitution emits derived-target copy events before ordered obligation/effect events. Ordered requirements hold handle pairs and compare their flow-state target alternatives at execution; unmatched class effects and zero-argument initializer requirements retain fixed descriptors via `FixedTarget`. Existing mutation fallback remains essential. Internal use-before-growth order is preserved rather than converting every use to post-effect checking. `IsFormal` already accepts nonnegative contained occurrences on by-value parameters.
- Frozen job4019 completed exit0, final summaries4024: Debug clean build,112 array/reference cases,22 tuple executions/26 rejections,34 struct executions/39 rejections,92 ownership IR checks and git diff--check. Logs ch10-dynamic-call-{build,arrays,tuples,structs,ownership}.log. This verifies preservation through dynamic substitution for existing source references; no Release/cumulative/sanitizer or new aggregate-borrow-source proof. No live jobs remain.
- Next: connect expression target snapshots and local occurrence slots, projection of stored reference targets versus inline holder addresses, whole-value replacement and branch result slots; map aggregate actual occurrences and propagate writes through reference-holder parameters. Keep source gate closed until escape checks and lowering are connected. Lesson: formal substitution must consume the saved target state at the call graph event, not freeze graph-construction metadata; repeated canonical types still contain distinct occurrence slots.

### Contained expression snapshots and local target slots

- Added `containedValues` for expression snapshots in the existing lifetime graph. Named reads and grouping copy occurrence handles with event13, preserving target alternatives, invalidation causes and expiry. Each nonreference local has stable occurrence slots; initialization and simple whole-local assignment replace their state through `StoreContained`, with an explicit equal-count invariant. An uninitialized slot has no acquisition; definite-assignment checking remains independent. Reference-local writes, projection, branch result construction, destructuring and caller-visible writes are not yet connected.
- Job4033 built Debug cleanly. Frozen job4034 completed exit0; summaries4038 confirmed zero warnings/errors,112 array/reference cases,22 tuple executions26 rejections,34 struct executions39 rejections,92 ownership IR cases and whitespace. Logs `ch10-contained-snapshots-*`. This is preservation evidence, not direct execution of contained-reference source (gate still closed), Release/cumulative or sanitizer evidence.
- Construction connection added afterward: explicit reference-field initializer selects its storage once, saved in `referenceValues`; ordinary initializers evaluate normally. Record target snapshots are assembled in declaration order only after source-ordered evaluation/default effects. Tuple targets concatenate component snapshots without type deduplication. Construction build/struct/tuple job4039 is pending at this note.
- Lesson: a local slot is a mutable analysis location, whereas an expression is a saved snapshot. Projection must preserve that distinction: actual slot lists for replacement, saved occurrence lists for value reads. Following a reference field writes/reads its target, not its containing slot. No source gate was widened.
- Construction job4039 is terminal exit0; summaries4042 verify Debug build0warnings0errors,34 struct executions39 rejections,22 tuple executions26 rejections and whitespace. Logs `ch10-contained-construction-{build,structs,tuples}.log`. No current Release/cumulative/sanitizer or source-contained-borrow claim. No live jobs remain at this boundary. Next projection should compute occurrence slices in canonical tuple/declaration field order; semantic tuple selection already uses invariant-culture `int.Parse(Text(node))`. Storage-selected slot lists and expression snapshots must stay distinct, particularly for whole-holder replacement versus writes through a reference field.

### Contained-reference projection and live selected slots

- Added `ReferenceCount` and `ContainedProjection` in CheckBorrows: offsets follow tuple component order or actual struct field order, counting every repeated inline occurrence. Ordinary value projection snapshots its range; reference-field value reads emit Use on the saved target, not the pointer-slot address. Missing range state is an internal failure, not silently dropped obligations.
- Frozen job4050 completed exit0: clean Debug build,22 tuple executions26 rejections,34 struct executions39 rejections and whitespace. Logs ch10-contained-projection-{build,tuples,structs}.log. Report4055 unnecessarily listed all PASS lines and clipped; visible final summaries supplied counts. No borrowed-source acceptance or Release/sanitizer claim.
- Added selectedContained mapping selected address handles to live local target-slot lists. Name selection attaches slots, derived addresses preserve their linkage, ordinary member selection narrows it to a field range, reference-field selection follows its stored alias. Local aggregate reference bindings share the selected slots; assignment uses its already-selected destination to store targets, including nested selections and aliases. Expression reads still snapshot those slots; replacing a holder must not alter an earlier copied expression. Build4053 exit0; focused regression4054 pending at this entry, inputs frozen.
- Call inspection clarified that by-value actuals use saved expression target handles, whereas reference actuals must refer to live selected holder slots at call consumption: a later argument can replace that holder. Do not snapshot reference-actual contents too early. Caller-visible writes, reference referents containing borrows, branch/destructuring results, defaults and escape/source admission remain unfinished. Source TypePrefix gate remains closed.

- Storage regression4054 terminal exit0; summaries4059 confirmed clean Debug build,112 arrays,22 tuple executions26 rejections,34 struct executions39 rejections,92 ownership IR and whitespace (ch10-contained-storage-*). No source gate change.
- Call actual map now includes each contained occurrence. Value parameters use containedValues snapshots; reference parameters use selectedContained live slot handles. Existing Substitute resolves these through event13 at the call. Regression4059 completed exit0; summaries4062 confirmed identical focused counts and clean Debug build (ch10-contained-actuals-*). No owned children at4062.
- Moved actual-map creation after all argument evaluation but before callee effects. Emit Use on contained actuals at entry, analogous to existing direct reference arguments: later-argument invalidation must reject even for read-only helpers, but checking all uses after all callee mutations would wrongly reject read-before-growth helpers. Entry build4064 clean; regression4065 exit0 (arrays/structs, saved counts still to inspect). Added contained actual Uses after implicit class-constructor allocation, matching direct-reference recheck at that boundary.
- Combined Debug/Release focused regression4069 RUNNING with compiler/grader inputs frozen: builds,arrays,tuples,struct values,ownership IR,whitespace. Prefix ch10-contained-projection-call-{Debug,Release}-*. No completion claim yet. Remaining work includes branch-result/destructuring slots, caller-visible writes, reference referents containing borrows, defaults/escape/lowering/source checks. Named construction/projection/local assignment/actual mapping are connected prerequisites, not safe source admission; LYC306 TypePrefix gate stays closed.

### Contained branch-result continuation

Job4069 completed exit0; saved summaries4073 confirm Debug/Release zero warnings/errors, each112 array/reference cases,22 tuple executions26 rejections,34 struct executions39 rejections,92 ownership IR cases, whitespace. This verifies preservation for projection/live local slots/call actuals and entry/post-constructor checks; source admission remains closed, no cumulative/sanitizer/source-contained-borrow claim.

BuildIf now shares a contained-result handle list across its continuing arms and else-if chain. First encountered value tail creates snapshot handles on that graph arm; subsequent tails replace the same handles via event13. Handles are identities, not a lexical choice of runtime target. Tail occurrence counts must match the checked type, failing explicitly on missing propagation. Stores precede branch cleanup; uses afterward preserve scope-expiry and finalizer invalidation. Else-if adds a use after its condition cleanup too. Worklist joins actual target states. Build4077 passed; guarded build/focused Debug regression4078 is pending, inputs frozen. Next: collect4078, add value-if/destructuring retained fixtures and connect destructuring leaf occurrence ranges before discard cleanup. Caller-visible writes, borrowed referents containing aliases, defaults, escape/lowering/source admission remain unfinished.

Branch regression4078 terminal exit0; summaries4082 clean Debug112arrays22tuple executions26rejections34struct executions39rejections92ownership IR. Destructuring now checks initializer occurrence coverage, advances a left-to-right offset at every leaf (wildcards included), allocates each named leaf's local slots and stores its corresponding snapshot range before discarded ownership cleanup. Complete consumed coverage checked explicitly. Build4082 and regression4083 exit0; summaries4089 clean Debug22/26tuples34/39structs92ownership IR, diff clean. No newest Release/cumulative/sanitizer claim.

Public borrowed_structs.py now9 execution14 rejection cases. New five cases cover else-if value result, nested destructuring after discarded borrowed leaf, invalidation after value-if/destructuring and branch-local target expiry. Baseline4084 exit1; diagnostics4085 confirm all5 existingLYC306 TypePrefix gate, no malformed syntax/crash and no successful source borrow execution. Existing18-case baseline retained separately.

Next inspected gap: Check() effects/obligations fixed point around528–566 calls CheckBorrows; worklist around850–882 copies target state plus invalidation causes and expiry. Current summaries export effects and ordered requirements, not caller-visible target replacement. Need explicit successful normal/return exit capture AFTER cleanup; do not infer exits from arbitrary graph nodes without successors. Export target/invalidation/expiry together: paths alone would revive a target invalidated or expired inside callee. Possible unchanged targets on no-store paths must join replacement outcomes monotonically. This is a construction direction, not implemented. Source gate stays closed. No live jobs through4091; no new children/commits.

### Caller-visible replacement summaries: exit capture and application prerequisite

Added explicit graph event14 after normal/initializer cleanup and after explicit return cleanup, before clearing the return frontier. Only reachable events contribute; successorless break/continue/unreachable nodes are not inferred exits. Summaries are keyed by actual reference parameter plus ordered occurrence, carrying possible target paths, invalidation causes, and expiry separately. By-value parameter replacement is callee-local and excluded. Current summary facts union across exits and fixed-point iterations.

Build4098 and focused regression4100 terminal exit0. Saved ch10-contained-exit-{build,arrays,tuples,structs,ownership}.log: clean Debug build,112 arrays,22 tuple executions26 rejections,34 struct executions39 rejections,92 ownership IR checks. No source-contained-borrow evidence.

Added event15 for caller slot replacement. Call graph construction substitutes every output source/cause before any destination write, merges outputs for aliased formal destinations, then replaces each live destination slot with saved target alternatives and their invalidation/expiry history. Fixed summary roots use their fixed path; actual-derived handles use copied flow state. This avoids sequential substitution corrupting swaps. Build4104 terminal0; regression4105 pending when recorded. Inputs frozen until its receipt. No admission/lowering change.

Open precision concern identified during inspection: monotonically unioning an early transitive-call unchanged approximation may retain obsolete targets after a definite replacement. Add legal direct/transitive replacement controls (old target invalidated after replacement), swap control, and replacement invalidation negatives. Do not call this resolved without source/model evidence. Existing source gate remains closed; reference referents containing borrows, defaults, escape/source/lowering handling still incomplete.

Replacement regression4105 completed exit0; summaries4108 confirm clean Debug build112 arrays22/26 tuples34/39 structs92 ownership IR. Added three public controls: direct replacement, transitive wrapper declared before implementation with explicit return, and swapping nested borrowed fields. Baseline4109/read4110: all three reach LYC306 TypePrefix; no parse/harness failure. Suite now12 executions14 rejections, outside make test.

Addressed the provisional unchanged-summary mechanism by adding returningFunctions to the fixed point. Event14 establishes a reachable return. A call to a body whose return has not yet been established disconnects its continuation for that graph build; later iterations rebuild it when reachable exits are known. Unknown summary is bottom, not identity. This permits base-path recursive groups to grow without permanently accumulating spurious unchanged outputs. Not a general termination proof, and actual borrow replacement precision remains unverified behind source gate. Build4111 clean exit0. Two-configuration focused run4112 is LIVE/frozen when recorded, logs ch10-contained-returning-{Debug,Release}-{build,arrays,tuples,structs,ownership}.log.

Read-only source follow-through: struct field type resolution Check.cs232 still calls ReadType on TypePrefix without unwrapping; BindRecord ~3688 explicitly rejects reference fields. Both untouched. Field defaults use zero-argument value-returning initializer scopes, so reference-field defaults require explicit address/escape handling, not accidental reuse. Lowering NewStruct ~782 still needs field-specific address handling; inspect exact source before edits.

Run4112 terminal exit0; summaries4122 verify clean Debug and Release each112 arrays22tuple executions26rejections34struct executions39rejections92ownership IR; whitespace passed. No current full cumulative/sanitizer/source-borrow claim. All jobs through4122 terminal. Lowering read782–831: explicit expressions evaluated once, then every field currently Widen+Transfer; Unary(ref)700–714 already yields Borrow Address. Next bounded change can preserve reference-field Address operands without introducing another lowering path; defaults/referent alias linkage/escape admission still unfinished.

## Reference-field lowering prerequisite and default-policy decision

NewStruct lowering now preserves an exact Address operand for reference fields without Widen or Transfer; ordinary fields retain widening and ownership transfer. An internal invariant rejects a non-address or nonidentical canonical referent type. Explicit evaluation and omitted-default order remain unchanged; source reference-field admission remains closed. In particular this does not implement reference defaults or nested referent-slot propagation.

Job4129 terminal exit0; saved summaries4135 show Debug build zero warnings/errors, 34/34 struct executions and 39/39 intended rejections; git diff --check passed. Logs book/build/ch10-reference-lowering-{build,structs}.log. Preservation only: source gate prevents coverage of new borrowed operand branch. No new Release/cumulative/sanitizer claim.

Readback confirms selectedContained currently attaches live slot lists to derived handles; references whose referents themselves contain borrows still require flow-sensitive storage linkage, not recursive snapshots of those referents. No such extension made here.

Human decision requested on already-unresolved aggregate array-growth policy: recommend tuple default iff every component is defaultable, but no implicit struct array-growth default even if all fields declare defaults. Struct reserve/push/shrink stay legal. This is a recommendation, not an approved contract. Current conservative runtime growth rejection remains unchanged. Pausing reminders at this language-decision boundary; next implementation remains contained-reference safety after Bill's response.


## Approved aggregate array-growth policy and implementation

Bill approved recursive tuple defaults and no implicit struct growth defaults,
including when all struct fields have declaration defaults, and requested goal
reminders re-enabled. Goal control confirmed enabled. Spec section5 now states
that rule. New shared SemanticTypes.HasDefaultValue is declared in Common and
implemented by an iterative canonical-type traversal: stop successfully at scalar
or array, descend tuple components, reject nominal types. No cached state. Arrays
stop because empty descriptors construct no elements. Emitter resize uses this
query; typed C zero initialization is reached only for eligible components and
initializes every newly live slot, including dirty shrink/regrowth storage.
Array literal element admission now includes tuples; contained-reference fields
remain gated. Struct reserve/push/shrink behavior is unchanged.

Public array_defaults.py adds12 retained cases, each safe/unsafe and O0/O2. Six
successes and six precise runtime LYR011 failures. Extended tuple harness accepts
optional runtime cases and emission options; unchanged tuple suite passed22/26.
Initial baseline4155 exposed a harness bug (unquoted expected source path) and
tuple literal gate. Corrected harness to the existing ch08 layout contract's
json.dumps(path); corrected baseline4167 terminal1 passed4/12 each mode, eight
tuple cases unsupported. Logs retained, not represented as intended default-only
failures. No chapter-10.md exists yet; teaching belongs here pending prose phase.

Implementation4168 terminal0; summaries4171 cleanDebug0warnings/errors and12/12
safe plus12/12 unsafe execution checks, each O0/O2. This is48 real C executions,
not a full cumulative/Release/sanitizer claim. Grader added to make test and public
README documents exact commands/contracts. Broader preservation checks next.

Lesson: cleanup-safe initialization, observable default eligibility, and field
initializer execution are separate contracts. An empty array is valid without a
default for its elements; an inline tuple needs defaults for every component.

Default-policy regression4174 terminal exit0; summaries4181 verified both Debug/Release zero warnings/errors,12/12 safe and12/12 unsafe default cases (O0/O2),112 arrays,22 tuple executions26 rejections,34 struct executions39 rejections,92 ownership IR. git diff --check passed. Inputs frozen throughout. Logs ch10-default-policy-{Debug,Release}-*.log. No full cumulative or sanitizer claim.

Read-only follow-through: Select1530–1603 already removes inherited selectedContained entry before adding component slice; suspicion of duplicate Add was disproved by full read (no patch needed). Reference-field value reads2147–2152 only Use the outer alias, no inner referent snapshot yet. Need live storage linkage when a reference points at a borrow-containing holder, distinct from copied value snapshots; new focused cases next.

## Nested borrowed-referent storage prerequisite

Added four retained public borrowed_structs.py fixtures: live holder replacement visible through ref Borrowed, earlier value-copy snapshot preserved, synchronous helper read, and rejection after the replacement target is invalidated. Suite now contains 15 execution controls and 15 intended rejections, still outside make test. Focused baseline job4189 exit1: all four reach LYC306 TypePrefix, not feature success (book/build/ch10-nested-referent-baseline.log).

Check.cs now carries referent storage identities as worklist state: address handle -> ordered occurrence sets of live slot IDs. Event16 attaches/narrows selected storage. Acquisition clears prior identities; event13 snapshots pointer metadata alongside targets/history, retaining live slot identities rather than their contents; event15 merges source identities before writing the destination. Control-flow joins merge each occurrence independently, with an exact shape guard. Array-depth selection does not inherit inline referent slots. This remains a prerequisite: no dynamic dereference consumer yet, no nested formal-summary representation, and source gates remain closed. The existing lexical selectedContained map still serves old selection paths until those consumers are connected.

Lesson: copying a holder address retains access to live storage, whereas evaluating the holder value must snapshot its current aliases. Neither may erase invalidation/expiry history. Avoid recursively flattening reference types: recursive pointers are finite storage links, not infinite inline values.

Job4200 Debug build clean, zero warnings/errors. Job4201 terminal exit0 with frozen inputs and git diff --check: 112 arrays,22 tuple executions/26 rejections,34 struct executions/39 rejections,92 ownership IR. Numeric summaries collected4203. No Release/full cumulative/sanitizer or source-contained-borrow success claimed. All jobs through4203 terminal, no freeze.

Next: connect reference-field value-read completion (currently near Check.cs2240) to an event that reads each current possible referent slot and copies its targets, causes, expiry and nested storage identities into expression snapshot handles. Then dynamic selected-address/write behavior and formal-to-actual nested referent substitution; reference defaults/escapes must be connected before source gates open. Do not mistake preservation passes for feature verification.

### Nested referent value-read event (source admission still closed)

Event 17 selects a live occurrence from the address's flow-state slot identities and snapshots the selected slots' target paths, invalidation causes, expiry, and further referent identities. It shares the state merge with caller replacement event 15; it does not reacquire aliases. Reference-field value completion now publishes these snapshots. Missing slot metadata is an explicit internal invariant failure rather than silently losing obligations. Formal reachable-storage substitution and writes are not yet connected.

Debug build job4211 passed with zero warnings/errors. Frozen preservation job4213 completed exit0: 112 arrays, 22 tuple executions/26 rejections, 34 struct executions/39 rejections, 92 ownership IR checks, whitespace check. Summaries inspected4217; logs `book/build/ch10-referent-read-*.log`. No source-contained-borrow success, Release, cumulative, or sanitizer claim. Next edit uses the same read event for reference-field storage selection and a dynamic range projection for inline components.

### Dynamic referent selection and assignment prerequisites

Reference-field storage selection now uses event17 to read the stored pointer's current state rather than indexing a lexical target list. Inline storage projection uses event18 to narrow ordered flow-state slot identities. Static maps remain for not-yet-converted reference bindings and call actuals. Debug build4218 clean; frozen job4219 completed exit0, summaries4225:112 arrays,22 tuple executions/26 rejections,34 struct executions/39 rejections,92 ownership IR and whitespace. Logs `book/build/ch10-referent-selection-*.log`.

Added legal nested write-through and ambiguous-destination invalidation controls to borrowed_structs.py (now16 executions/16 rejections). Baseline4226 exit1, diagnostics4227 both known LYC306 TypePrefix, not intended feature behavior. Source gate stays closed; suite remains outside make test.

Event19 now updates selected live occurrence slots for assignment. One possible destination gets replacement; multiple possible destinations each retain the old alternative as well as the assigned snapshot. Right-hand-side values are already snapshotted and destination selection precedes their evaluation. Extracted existing target/history/storage merge to local ReplaceTargets, shared by call output15, dereference read17, and store19; self-copy snapshots before clearing destination. Debug build4228 succeeded. Frozen two-configuration job4229 RUNNING at note time: Debug clean112 arrays22/26 tuples34/39 structs12/12 safe+unsafe defaults92 ownership (4232), Release build clean, remaining checks pending. Do not claim terminal or complete Release evidence yet.

Inspection found missing formal ref-parameter attachment: parameter initialization creates containedBindings and a separate aliases address but does not attach the latter to those slots. Fix after freeze. Reference call actuals still use selectedContained; outputs prepare all sources before writes, which must remain true for swaps. Nested formal reachable-storage paths and dynamic caller destination substitution remain unfinished. Local reference bindings also still depend on static mappings. No source-contained-borrow success, cumulative or sanitizer claim.

Final receipt: job4229 completed exit0; summaries4236 confirm Debug AND Release zero warnings/errors,112 arrays22 tuple executions26 rejections34 struct executions39 rejections12 default cases each safe/unsafe92 ownership IR, whitespace passed. Freeze released. No source borrow admission/success, full cumulative or sanitizer evidence.

### Live reference reads and dynamic call destinations

- Formal-reference attachment verified by job4239 (terminal exit0): Debug build zero warnings/errors, 34 struct executions/39 rejections and 22 tuple executions/26 rejections; whitespace passed. Logs `ch10-formal-storage-*.log`.
- Name reads through an alias now use `SnapshotReferent` (event17), observing current flow-selected slots. Ordinary value names retain `SnapshotContained`. Job4245 terminal exit0: clean Debug build,112 array/reference executions,34/39 structs,22/26 tuples,whitespace. Logs `ch10-live-ref-read-*.log`. This remains preservation evidence with source admission closed.
- Call input values and writable output destinations must be separate: reference arguments snapshot current slot contents after all argument evaluation, while outputs retain actual address plus occurrence. Event15 now batches a call's output destinations, resolves live slot sets, merges aliased outputs, preserves untouched alternatives for ambiguous destinations, and only then writes. All output sources are captured before this event, preserving swap ordering. Debug build4253 passed; preservation job4254 pending at this note.
- Two checked-edit attempts refused before disk write on a mismatched comment-whitespace anchor. First4251 shell continued to build unchanged source and returned0; not evidence of the conversion. Second4252 used fail-fast and returned1 without building. Third4253 used unique comment text and wrote/built successfully. Lesson: atomic checked edits need fail-fast command sequencing too; trailing build success must not mask edit failure.
- Remaining static selectedContained map is now redundant bookkeeping; remove after pending checks. Nested formal reachable storage, reference defaults, escape restrictions and source admission remain unfinished. No source-contained-borrow, cumulative or sanitizer success claimed.

Verification follow-through: dynamic-call job4254 completed exit0; summaries4257 confirmed the complete focused Debug suite. Removed redundant selectedContained lexical map and local-reference containedBindings aliases: reference reads/calls now use live address flow state; actual value holders and formal parameters retain their authoritative slots. Events16/18 attach/project storage identities. Batched event15 captures all output sources before resolving and merging destination writes, preserving swaps and ambiguous untouched alternatives.

Frozen job4259 completed exit0. Saved summaries4264/4267 confirm BOTH Debug and Release builds with zero warnings/errors;112 array executions,34 struct executions/39 rejections,22 tuple executions/26 rejections,12 array-default cases each safe/unsafe,92 ownership IR checks; final whitespace check passed. Logs ch10-live-storage-{Debug,Release}-*.log. Inputs unfrozen, no live job. This is preservation evidence only: contained-borrow source gates remain closed, public borrowed_structs controls remain outside make test, no cumulative/sanitizer/source-feature success claimed.

Next gap: immediate formal occurrence slots do not describe storage reachable through reference-valued fields. Summary identities must survive graph rebuilds and preserve repeated occurrences; graph-local handles and eager recursive pointer flattening are unsuitable. Need finite demand-driven formal reachability with a convergence rule for recursive calls in the existing lifetime engine. Reference defaults and explicit escape/source admission remain unfinished.

### Nested formal reachability: write-through contract and decision

Inspection Check.cs1183–1194 confirms existing exit export covers only direct ref parameters. By-value holders also permit caller-visible writes THROUGH their reference fields; replacing their own fields remains local. Added paired public cases borrow-struct-value-parameter-write-through and borrow-struct-value-parameter-write-invalidated. Suite now17 executions/17 rejections, still outside make test. Baseline4283 terminal exit1, log ch10-value-referent-baseline.log: both reach LYC306 TypePrefix, not implementation success or intended LYC326. Whitespace4280 passed. No compiler changes after4259; no live jobs/freeze.

Design decision raised with Bill before summary representation changes: nested formal paths must preserve parameter and occurrence identity and export writes through value parameters too. Demand-driven path expansion alone can diverge across recursive helper calls. Recommended finite graph retaining distinct parameters/field occurrences but conservatively merging repeated recursive paths; this may reject safe recursive programs. No recursion abstraction or new rejection rule implemented. Await precision-policy agreement; source gate stays closed. Search lesson: broad recursive/contained queries produced clipped historical/generated output; exact spec395–399 and targeted Check.cs searches supplied actual contract evidence.

### Finite formal storage graph prerequisite (approved conservative recursion)

Bill approved finite conservative recursive-path merging and requested reminders re-enabled; goal_control confirmed enabled. Distinguish finite representation from precision: cycles alone do not mandate alias merging, but this initial abstraction folds a repeated referent type along the SAME ancestry path. Distinct parameters/field-occurrence branches stay separate. Nodes reachable from a folded cycle are marked repeated; writes retain old alternatives even for a singleton abstract destination.

Added borrow-struct-recursive-formal-analysis (uncalled recursive helper over Link.next:ref Link). This is intended semantic termination coverage, NOT construction/runtime recursive-value proof. Baseline4292 terminal exit1 reaches TypePrefix LYC306; public suite now18 execution/17 rejection controls, still gated/outside make test. Initial exact-edit anchor was ambiguous against NEGATIVE_CASES and refused without write; full-line anchor succeeded.

Check.cs local FormalStorage builds ordered symbolic nodes/slots iteratively, folding matching types only on the parent ancestry. Immediate slots retain original component numbering; nested components have deterministic additional numbers. Allocate all formal handles before event16 attaches referent-slot identities, so cycles are explicit links. summarizedStorage marks repeated concrete-location abstractions; events15 and19 preserve untouched alternatives for them. Caller nested substitution and exit exports remain UNCONNECTED; gates closed. Debug build4296 clean; frozen focused4297 terminal exit0, summaries4301 clean112arrays34/39struct22/26tuples12defaults each safe/unsafe92ownership; whitespace passed. No Release/cumulative/sanitizer/source-borrow claim for this slice.

Inspection correction: a by-value parameter root is private copied storage, not a possible destination of its own reference fields. FormalStorage now takes rootIsReferent: skip root as back-edge candidate for value parameters, permit root folding for reference parameters. Nested nodes may still fold. Job4302 running build+struct preservation with inputs frozen; collect result before further edits. Logs ch10-formal-root-{build,struct}.log. Next actual mapping needs visited formal-slot/actual-handle pairs and separate snapshot-value versus writable-destination handles; never leave nested callee symbolic roots in caller flow state via Substitute fallback.

### Nested formal-to-actual mapping prerequisite

- Root-isolation job4302 was terminal exit0;4332's predecessor4305 confirmed clean Debug build and34 struct executions/39 rejections. The pending statement above is superseded.
- Check.cs now registers externalFormalSlots for every nested formal slot and direct-ref root slot, excluding private value roots. Successful exit14 exports their targets, invalidation causes and expiry after cleanup. Thus writes through references inside value parameters can be summarized without exporting replacement of their private copies.
- New event20 maps finite formal graphs to actual live storage using a visited set of (formal slot, actual handle) pairs. Root value inputs retain expression snapshots; ref roots read current slots after argument evaluation. Traversal follows actual referent identities, collecting source and destination sets separately. It publishes saved values via the existing ReplaceTargets merge and separate destination-address handles with one occurrence. No caller storage is written during traversal.
- Calls substitute all formal components from these snapshots; replacements use writableActuals occurrence0 and existing capture-all-before-batched-write event15. Missing nested formal substitutions throw rather than leak callee parameter identities. Recursive graph and actual handle sets are finite; this is structural reasoning, not source behavior evidence.
- Initial4318 built clean, but4319 stopped exit1 at arrays92/112: builtin placeholder parameters may have null Type, causing FormalStorage null dereference. Corrected call mapping with the same concrete-type guard used by parameter initialization. Lesson: builtin placeholder signatures are not concrete user-function storage graphs. Failed logs retained under ch10-nested-mapping-*; corrected logs ch10-nested-mapping-fixed-*.
- Corrected frozen4325 terminal exit0;4332 summaries clean Debug0warnings/errors,112arrays,34struct executions39rejections,22tuple executions26rejections,12default safe+12unsafe cases,92ownership; whitespace passed. No Release/cumulative/sanitizer or admitted source-contained-borrow verification for this slice. All jobs through4332 terminal, no live freeze.
- Source gates remain closed: struct field annotation ReadType at Check.cs232 does not admit ref; BindRecord4124 rejects reference fields. Remaining work: direct behavior evidence for nested mapping, explicit return/array/default escape checks, reference construction checking and defaults, then connected source admission. Initializer checking around515 and record expression checking after4160 need inspection. Do not infer completed safety from extensive preservation checks.

### Source escape restrictions and explicit reference inputs (in progress)

- Added LYC343 for borrow-containing explicit returns and function result contracts, plus array literals. RequireValidArrayStorage walks canonical tuples/structs/arrays with a visited set, rejecting array elements containing borrows. It runs on array type reads and again after all struct fields are collected so forward nominal fields cannot hide a borrow. Class-field admission has not been widened.
- 4345 and 4347 terminal exit0: each Debug build zero warnings/errors, structs34 executions39 rejections, tuples22 executions26 rejections, whitespace clean. These are preservation checks only; TypePrefix and BindRecord source gates remain closed.
- Added three public rejection controls: empty typed array, forward-declared array field, tuple/struct-wrapped empty array. 4356 baseline terminal1, all three LYC306 TypePrefix rather than required LYC343. Borrow suite now18 executions20 rejections, outside make test. No source-borrow success.
- Explicit record reference inputs now use CheckReference in written order and require exact canonical referent type (LYC325); ordinary inputs retain contextual value checking/widening. Lowering already preserves exact Address operands. 4364 Debug build/struct/tuple/whitespace run pending at this note; compiler/fixtures frozen until outer receipt.
- Defaults remain unresolved implementation work: declaration-scoped initializer bodies currently return values; they cannot silently produce address-valued defaults. Need inspect possible stable storage in declaration scope, connect reference default diagnostics/lowering or resolve a consequential contract decision with Bill. No new default prohibition approved or implemented. All source gates remain closed until safety connections complete.
- 4364 terminal exit0; 4366 saved summaries clean Debug zero warnings/errors, structs34/39, tuples22/26, whitespace passed. Source input gates remain closed. Subsequent edits only relocate ReadType's existing comment beside its declaration. No Release/cumulative/sanitizer/new source-borrow evidence.
- Declaration collection Check.cs157–199 admits structs/classes/functions only, no global storage. CheckReference3256 requires explicit ref to Name/Index/Member (with grouping); checks writable initialized storage. Defaults cannot see caller locals/self/siblings by existing contract. Need finish investigating any valid stable declaration-scope address before choosing default support; do not silently turn a value-returning initializer into an address or invent a permanent default ban.

## Reference-default checking prerequisite

Check.cs now checks reference-field defaults with CheckReference and exact canonical type equality (LYC325), while ordinary defaults retain CheckExpression/RequireType. Source admission remains closed; this is not reference-default support. Job4380 completed exit0; saved ch10-reference-default-check-{build,structs,tuples}.log summaries4384 show Debug zero warnings/errors,34/34 struct executions39/39 rejections22/22 tuple executions26/26 rejections; whitespace passed. No Release/cumulative/sanitizer/source-borrow evidence.

Inspection corrected an earlier hypothesis: no globals does NOT imply no possible reference default. WritableRoot stops at class fields and permits a call receiver, so a declaration-scoped function may provide class storage. Do not impose a default ban based on scope alone. Spec624 applies ordinary declaration-scope rules. CallInitializer currently promises fresh owned Value (Common/IR.cs149); lowering End always Widen+Transfer then cleanup. Initializer lifetime branch Check.cs787 builds expression, MoveAggregate, Cleanup, UseObject; it does not export a returned address. Next coordinated work: address initializer result/lifetime summary (including post-cleanup invalidation), construction-site alias application, IR validation/lowering/emission; add concrete default controls before enabling support. Ordinary source reference-return rules must not silently change. No jobs live after4384.

### Reference-default controls and result-summary gap

- Added two public borrowed_structs rejection controls: a reference default borrowing `Box().n` from a temporary counted Box requires LYC326; a non-ref expression supplied as a reference default requires LYC325. Job4393 terminal exit1, log ch10-reference-default-baseline.log: both reach LYC306 TypePrefix, 0/2 intended rejections. These are gated controls, not source-feature passes. Suite now18 execution/22 rejection controls, outside make test.
- No compiler edits in this slice. Inspection Check.cs2451–2505 found omitted defaults contribute effects/objects but are skipped when assembling recordTargets. Therefore ordinary value defaults containing borrows also need result summaries, not just reference fields.
- Initializer lifetime branch787–795 currently BuildExpression/MoveAggregate/Cleanup/UseObject. Exit14 at1311 exports caller replacement slots after cleanup. New initializer results must have separate ordered result slots and preserve targets/causes/expiry after cleanup, rather than misuse parameter replacement keys. Nested storage identities cannot escape as graph-local handles. Unknown result remains bottom until reachable exit established.
- Positive reference-default factory fixture deferred until explicit cleanup is clear; no new language ban inferred from absent globals. Class-field storage through call remains possible. Next implement coordinated initializer result summary and construction application before address IR support/admission; source gates remain closed. All jobs through4396 terminal, no input freeze. No new build or broader evidence beyond4380.

### Initializer local-exit borrow checks

- Reference initializer lifetime construction now selects the explicit reference operand once (unwrapping outer groups), retains the address handle, and does not perform value/ownership transfer. Ordinary value defaults preserve BuildExpression/MoveAggregate and retain their contained-alias handles.
- Both paths clean up initializer-owned temporaries before emitting Use for the saved aliases; UseObject remains for value object obligations. Select retains computed class receiver lifetime obligations, so checking the saved address is necessary rather than reacquiring after cleanup.
- Job4410 completed exit0; summaries4412: clean Debug build zero warnings/errors,34/34 struct executions39/39 rejections22/22 tuple executions26/26 rejections; whitespace passed. Logs book/build/ch10-default-exit-Debug-*.log. No Release/cumulative/sanitizer/source-contained-borrow evidence. Inputs unfrozen, no live jobs.
- Source gates remain closed. This establishes local exit ordering only, not initializer-result export/application or address-return IR. Next: ordered result alias summaries with post-cleanup targets/history/expiry and graph-independent reachable-storage identities; unknown results stay bottom until reachable exit. Omitted value defaults also require result summaries. Never export graph-local slot handles directly.

## Initializer result graphs and unknown-return handling

- Closed the missing default-call bottom rule: omitted defaults now disconnect continuation until `returningFunctions` establishes a reachable initializer exit, matching ordinary calls. Job4421 exit0; summaries4428 clean Debug build,34/39 struct execution/rejection and22/26 tuple execution/rejection, whitespace. Logs `ch10-default-bottom-Debug-*`.
- Added positive `borrow-struct-default-ordinary-owner`: a reference default borrows an ordinary object's scalar field, then its holder copy reads it. One object is intentionally process-lived in this short-lived fixture; this is not explicit reclamation/leak evidence. Suite now19 executions/22rejections, outside make test. Baseline4438 exit1/read4439 is LYC306 TypePrefix, not success. Attempted owner-recovery probe first used wrong `destroy owner` syntax (4431), corrected to `owner.destroy()` but self-typed object field was unsupported (4434). Both failed logs retained; abandoned that unrelated object-field requirement. Existing counted-temporary negative uses supported scalar field syntax, not the failed self-field pattern.
- Lesson: finite type paths alone do not preserve actual alias sharing. Shared initializer summaries now use deterministic result positions derived from the existing finite `FormalStorage` graph, with a separate position0 for a direct reference result. Each position retains targets, invalidation causes, expiry, ordered sets of referent positions, and a repeated-location flag. Graph-local handles are never exported.
- After initializer cleanup, existing event20 maps reachable result aliases into these stable positions. Exit14 unions target/history and translates actual referent slot identities through mapped destinations into result-position edges. Overlapping paths retain all matching positions; cyclic/multiply represented locations remain conservative. Private value roots remain snapshots. Missing occurrence/edge mappings fail visibly. This can lose correlation and reject safe cases; no precision claim from gated fixtures.
- Added event21 importer: construction allocates every invocation-local result handle before restoring target/history and reconnecting saved position edges. It does not acquire fresh borrows. Omitted reference and ordinary borrow-containing defaults contribute ordered roots to recordTargets. Roots from earlier defaults are checked after all defaults, so later effects cannot erase their obligations. Unknown returns remain bottom, not empty results.
- Exporter4443 build/whitespace then4444 focused tests terminal0; summaries4457 cleanDebug34/39struct22/26tuple. Importer4445 build/whitespace terminal0. Combined frozen4446 terminal0; summaries4457 BOTH Debug/Release zero warnings/errors,112arrays34/39struct22/26tuple,12safe+12unsafe default-policy cases,92ownershipIR; whitespace passed. Logs `ch10-default-result-{Debug,Release}-*` and earlier export/import build logs retained. No live jobs/freeze. No full cumulative/sanitizer/source-contained-borrow evidence; new nonempty graphs are still behind source gates.
- Next: address-returning FieldInitializer IR/backend contract. Current Lowering initializer212 and End always evaluate/convert/transfer Value; omitted CallInitializer799 always Value. Validation expected-result switch188–198 defaults Value; control Return245–277 demands Value; missing-Move predicate452–494 treats all Returns as consuming (needs role-specific exception for reference initializer). Selected field already authoritative owner/type; do not add wrapper identity. Emitter has dedicated initializer prototype/definition349/385 and CallInitializer713; inspect exact return declaration/type before edits. Ordinary source return/If/Yield contracts must remain value-only. Source gates remain closed until these and remaining selection checks are complete. Goal reminders enabled; personal/no commit/push/children/install boundaries unchanged.

### Reference initializer address IR contract

- Lowering now returns an exact Address for reference FieldInitializer bodies, bypassing Widen/Transfer while retaining cleanup ordering. Omitted reference-field calls produce Address and are excluded from owned-temporary registration even for array referents. Ordinary source returns remain values.
- Validation requires reference initializer calls and Return operands to have Address category and exact field type. Missing-Move validation does not demand ownership transfer for these borrowed returns. Common/IR.cs documents the distinction. Emission uses pointer initializer prototypes/definitions, assigns calls to pointer locals (not dereferenced lvalues), and returns the selected address.
- 4467 clean Debug build and whitespace; 4468 terminal0 with 34/39 struct execution/rejection,22/26 tuple,92 ownership checks. No Release/full cumulative/sanitizer claim for this slice.
- Added development-only address-initializer mode to tools/probes/tuple-emission.cs and check_tuple_emission.py. It retains real lowered class construction/destruction bodies and builds explicit address-return/call IR over an ordinary object's array field. Array backing is explicitly released; ordinary objects are process-lived, not object reclamation evidence. 4473 positive and4475 category-corruption rerun terminal0; zero warnings/errors, generated C O0/O2 pass. Invalid value-category call and value Return are refused by specific category diagnostics before restoring valid IR. This exercises IR validation/emission, not gated reference-default source lowering or lifetime safety. Corrected generic probe success label to describe this mode rather than claim recursive copy/drop coverage.
- Lesson: pointer return support requires coherent ownership registration, return category, and C destination representation; changing the signature alone would still dereference an uninitialized call destination or transfer/drop the referent.
- Source gates remain closed. Next inspect source selection and all admission safety paths, then exercise retained borrowed source controls once connected; reference-default lowering still needs actual source evidence.

### First contained-reference source admission (jobs 4477–4489)

After inspecting field resolution, named construction, reference checking and member selection, removed the two blanket source gates. Struct fields unwrap the `ref` annotation, resolve the canonical referent type and set the actual field symbol's reference mode. BindRecord retains its finite closure/float check but no longer rejects reference fields. Earlier escape, default-result graph, address-aware IR and caller-flow prerequisites now run on real source.

The first rejection-only run (4482) built cleanly but passed 20/22 intended diagnostics. No positive generated programs were executed in that run. The counted-default fixture incorrectly called string-only print with an integer; replaced it with an assertion preserving the borrow use. The value-parameter write-through case exposed a real missing writability boundary: WritableRoot walked through a reference field to the immutable wrapper. It now stops at the reference field, because assignment selects the writable referent rather than rebinding the pointer slot. Ordinary inline fields and whole-holder replacement still inherit holder mutability. A proposed correction command was refused by the tool batch limit without executing; 4486 applied it and completed cleanly, 22/22 intended rejections.

Lesson: immutable holder storage does not revoke the writable-reference permission captured during construction. Copying the alias and replacing its referent are distinct from replacing the holder's own pointer field.

4487 completed exit 0: 19/19 source execution controls passed through production C emission at both -O0/-O2 (runner confirmed at tuples.py360). Includes ordinary-object reference default, finite recursive formal analysis, by-value-wrapper write-through, direct/transitive replacement, swap, live nested referent and saved value snapshot. The recursive helper is uncalled: analysis termination evidence, not runtime recursive construction/traversal. The ordinary default uses a process-lived object, not object reclamation evidence. 4488 read those results; its final unrelated grep had no matches and exit1, not test failure.

4489 is the pending frozen Debug/Release focused preservation run: array/reference, struct, tuple, default-policy, ownership IR and borrowed source graders, then whitespace. Logs ch10-borrow-source-{Debug,Release}-*.log. Do not edit compiler/fixtures until outer completion. Borrowed suite still outside make test; no cumulative, sanitizer, review or Chapter10 completion claim. Source admission is now enabled in the candidate, superseding earlier CLOSED status.

### Contained-borrow retained integration

Focused job4489 completed exit0. Saved logs `ch10-borrow-source-{Debug,Release}-*.log` confirm both configurations clean (zero warnings/errors), 112 array cases, 34 struct executions/39 rejections, 22 tuple executions/26 rejections, 12 array-default cases in each safety mode, 92 ownership IR checks, and 19 borrowed-struct executions/22 intended rejections. Outer command also passed whitespace checking. This is focused source/preservation evidence, not cumulative or sanitizer evidence.

Added borrowed_structs.py to Makefile's retained Debug/Release suite and corrected its stale header and the guide's original unfinished-status paragraphs. The guide documents actual negative matching (exit1, empty stdout, located expected code, not whole-stderr equality), recursive-analysis-only coverage and the process-lived ordinary default object. No compiler semantics changed in this integration.

Cumulative job4497 is RUNNING with compiler, fixtures and Makefile frozen. It runs the actual `make test` using a temporary pinned-SDK wrapper that adds `--no-restore` to build calls only; the wrapper is removed on exit. Log: `book/build/ch10-borrow-cumulative.log`. Collect outer receipt and final saved totals before changing tested inputs. Chapter10 manuscript is still absent; teaching, coverage review, authorized independent reviews/reconstruction and human acceptance remain ahead.

Coverage inspection during frozen cumulative run4497: borrowed_structs.py has a direct reference-default positive and counted-temporary negative, but no ordinary value field default whose resulting aggregate contains aliases. Next bounded controls: `inner:Borrowed=Borrowed {target:ref Box().n}` with an ordinary process-lived Box should execute; the corresponding counted Box must reject after temporary cleanup (expected lifetime diagnostic to be checked against the contract). These are proposed controls, not saved grader fixtures or verified outcomes. Do not edit fixtures until4497 completes. This gap exercises the value-root branch of initializer result export/import, which the direct address-default control does not establish. No dedicated voice-guide file matched `book/notes/*voice*`; authoring.md was read completely, but this glob is not evidence of absence elsewhere.

## Provisional Chapter 10 teaching structure (cumulative 4497 still running)

This is a construction outline, not completed teaching or a new contract. Keep the chapter organized by dependencies rather than the sequence of repairs:

1. Canonical identity: Analysis owns collision-safe interned type keys; structural tuple/array identity differs from actual nominal declaration identity. Holder/component/declaration backlinks and retirement preserve one authority. Type identity does not identify a runtime occurrence or alias.
2. Aggregate storage and layout: ordered tuple components and nominal fields; only inline edges participate in finite-layout cycle rejection. Reference/array edges still matter for discovery and lifetime, but do not imply inline recursion. Explain singleton tuples and grouping.
3. Evaluation and construction: written-order input snapshots, declaration-order assembly/defaults, exact reference inputs versus value widening; destructuring evaluates once and establishes named owners before discard cleanup. Defaults resolve in declaration scope and run afresh only when omitted.
4. Copy, transfer, replacement and cleanup in the existing IR: own arrays recursively, share counted handles under their existing rules, copy contained aliases without taking referent ownership. Address selection and value snapshots remain distinct. Preserve actual selected field identity through validation/emission.
5. Methods: actual member identity, value/ref receiver distinction, no automatic borrowing; separate implementations match the declaration rather than replace it. Explain lifecycle and body-local parameter spelling without inventing multi-file package support.
6. Array growth: eligibility is not cleanup-safe empty representation. Tuple components must all default; an empty array component terminates the query; nominal structs never implicitly default for growth. Reserve/push/shrink retain their contracts.
7. Contained aliases: local value copies save aliases; references reach live slots. Flow copies retain targets, invalidation causes and expiry. Calls separate saved inputs from writable destinations, capture all output sources before writes, and conservatively retain untouched alternatives. By-value wrappers can still write through external references. Finite formal graphs preserve parameter/occurrence distinctions and fold recursive paths, with explicit precision limits.
8. Defaults and escape boundaries: initializer result graphs export post-cleanup aliases and reachable-slot relationships, not temporary graph handles. Address defaults are a generated-initializer-only IR exception; ordinary source returns remain value-only. Reject borrow-containing returns and array storage including wrappers and forward fields. Add the proposed value-default positive/negative controls before describing that result shape as source-tested.
9. Public exercises, exact cumulative commands and Take it for a Spin. Distinguish retained behavior graders, structural/model evidence, recursion-analysis-only fixture, process-lived ordinary-object fixture, and human acceptance. Do not claim reviews, reconstruction, sanitizer evidence or completed diagrams before they occur.

Next executable action remains collecting 4497's terminal receipt and summaries, then adding the paired ordinary-value default controls. Compiler, fixtures and Makefile remain frozen during that run.

### Retained cumulative receipt and value-default coverage

Job4497 completed exit0; summaries4531 confirm both Debug/Release clean builds and the full retained suite, including 212 layouts, 92 ownership, 34/39 struct, 22/26 tuple, 12 safe and 12 unsafe default cases, and 19/22 borrowed cases. This is cumulative evidence, not sanitizer evidence or chapter completion. Input freeze lifted. Added a value-root initializer-result pair (ordinary process-lived owner succeeds; counted temporary owner must reject LYC326), bringing borrowed controls to20 executions/23 rejections; new results pending. Draft chapter now includes source-checked canonical identity and tuple/destructuring sections; it remains incomplete.

The expanded value-default suite passed without compiler edits: job4532 Debug and4533 Release each exit0,20/20 executions at C O0/O2 and23/23 intended rejections. Both new case identifiers explicitly PASS in both saved logs (`ch10-value-default-{Debug,Release}.log`); whitespace passed. Lesson: direct address-root defaults and aggregate value-root defaults transport aliases through different result shapes and need separate evidence. Neither process-lived ordinary-owner positive establishes reclamation. Full cumulative4497 predates only these two fixture additions and prose; no compiler changes followed it.

### Teaching continuation: construction, methods, and type queries

- Extended `book/chapter-10.md` through section 5 (182 lines): explicit-input/default/storage ordering, address versus value initializer results, member declaration/body identity and lexical parameter names, separate layout/ownership/defaultability/contained-borrow walks. Corrected the stale introductory cumulative-verification notice.
- Source checked spec 582–637 and 435–495, IR/Lowering.cs 765–845, Common/SemanticModel.cs implementation declarations, Check.cs 325–418, and Semantics/SemanticTypes.cs 80–269. Readback and `git diff --check` passed (4543,4549,4554). No compiler or grader changes in this writing pass; no new runtime evidence claimed.
- Lessons stated in chat: rearranging saved operands differs from rearranging evaluation; declared signature types differ from implementation-local parameter spelling; graph queries need distinct edge policies. Single-file method evidence does not establish a package loader. Existing process-lived default controls do not establish object reclamation.
- Next: source-check and teach the contained-alias lifetime engine as construction steps (snapshots versus live slots, history/expiry, finite formal reachability, capture-all call outputs, default result graphs), then backend details, remaining agreed coverage, exercises/commands, diagrams and Take it for a Spin. Reviewers/student still require Bill's authorization. No live jobs through4554; no input freeze.

### Lifetime teaching construction (through job 4569)

- Extended chapter-10.md through line 242 with occurrence identities, value snapshots versus live referent reads, conservative ambiguous writes, branch/destructuring cleanup, finite formal graphs, and actual substitution/caller replacement. This is draft teaching, not completed/reviewed Chapter 10.
- Source-checked Check.cs2080–2160,1315–1380,1963–2035,1204–1314,1131–1203; located returning-path/export declarations and handlers. Formal ancestry folding excludes private value roots; repeated descendants require conservative updates. Calls traverse finite formal/actual pairs and separate saved inputs from writable destinations; batch outputs preserve swaps and overlapping actuals.
- Lesson stated in chat: one abstract handle need not represent one location; saved call values and live call destinations are different facts. Copying must retain invalidation history, never reacquire validity.
- Jobs4563/4569 read back added prose and passed git diff --check. Search4558 clipped; used bounded direct reads instead. One initial tool invocation was refused before execution and retried with explanation. No compiler/grader edits or new execution checks; runtime evidence remains cumulative4497 plus expanded borrow4532/4533. All jobs through4569 terminal, no freeze.
- Next: teach initializer result graphs and explicit escape checks from current source, then backend, exact exercises/commands, diagrams and Take it for a Spin. No review/student authority added; no commits/pushes/install/children.

## Initializer/escape and backend teaching continuation

- Extended chapter-10.md through line299: initializer address/value result shapes, post-cleanup saved-alias checks, stable result-slot graph export/import and overlap; separate LYC343 storage restrictions versus LYC326 invalid use; empty/forward/wrapped array checking.
- Source inspected Check.cs802–907,1204–1247,1450–1512,3935–3978 plus bounded diagnostic searches. Export read covers target/history/expiry and edge mapping beginning; prior checkpoint contains remaining graph construction inspection. Search4571 clipped, not treated as full inspection.
- Added IR-backed C section: broad type discovery versus inline-only definition ordering, forward record names, empty-record private byte, type-directed empty/copy/drop, pointer alias preservation, reference-field selection, narrow address-initializer signature/return/destination handling. Checked Compilation.cs955–1048,1317–1373,650–723 and focused address emission search. Search4579 clipped; bounded reads supplied the relevant evidence.
- Prose readbacks and git diff --check4578/4586 passed. No compiler or grader edits, no new runtime execution evidence. Existing cumulative4497/4531 and expanded borrow4532/4533 results unchanged. No jobs live through4586, no input freeze.
- Lessons stated in chat: initializer import must preserve invalidation rather than reacquire; storage permission is separate from current validity; address return requires call-destination handling, not only a pointer signature; discovery/layout/ownership require distinct edge policies.
- Next: inspect actual grader guide/Makefile and prior exercise format; finish exercises, exact commands, source-checked diagrams and practical walkthrough, with bounded coverage assessment. Reviews/student still require Bill authorization; chapter remains draft, no human acceptance claim.

## Exercise command draft and live verification

- Chapter draft through343 approximately now includes staged exercises, independent readable grader boundary, exact focused compiler-command injection, cumulative Makefile contract, retained classification and evidence limits. Read grader guide1–220 and Makefile41–74; search4591 clipped then bounded read.
- Corrected draft shell loop from `|| break` (could mask failed status) to whole-block fail-fast subshell including build/selfcheck. First edit4594 refused wrong anchor, no effect; read4595 then correction4596 succeeded. Lesson stated in chat.
- LIVE4597 executes the focused shell block extracted directly from chapter-10.md with only SDK placeholder replaced by pinned SDK. Script book/build/ch10-exercise-command.sh; output book/build/ch10-exercise-command.log. Compiler/graders frozen until outer receipt and saved totals. This is Debug command verification, not new cumulative/Release/sanitizer evidence. No pass claim yet.
- Next collect4597; while running only read/prose walkthrough preparation. Need finish guide remaining lines, assess coverage within agreed scope, source-checked diagrams and Take it for a Spin. No children/commits/install authorization. Prior cumulative and expanded borrowed results unchanged.

### Verified exercise command and practical walkthrough

- Job4597 completed exit0; saved numeric summary4599 confirms clean Debug build, canonical5/5 plus4 rejected corruptions, tuple22/26, struct declaration20/20, struct executions34/39, defaults12 per safe/unsafe mode, borrowed20/23. This ran the focused shell block extracted from chapter10 with only the authorized SDK path substituted. Compiler/grader freeze lifted after receipt and totals. Prior both-configuration cumulative evidence remains4497/4531; this is not a new cumulative or sanitizer run.
- Chapter section9 now provides disposable local source/C/executable walkthrough: independent Point copy, copied reference-field write-through, scalar-tuple array growth. Job4605 ran its extracted shell block with pinned SDK, exit0.4606 verified exact three-line stdout, empty stderr, and whitespace. Files are trap-cleaned; no external services or persistent objects. This is author execution, not Bill's human walkthrough/acceptance.
- Lesson: ordinary value copy isolates fields; alias copy preserves the same referent, including through an immutable holder. Exercise commands and walkthroughs now have direct invocation evidence; diagrams, consistency pass, authorized reviews/reconstruction and human acceptance remain unfinished. No compiler changes in this writing slice, no children or installation.

## Chapter 10 diagram inventory prerequisite

The source-only diagram extractor built offline and inventoried ten current Common files / 21 types (4626). Manifest comparison (4627) found new SemanticTypeComponent and canonical-type, initializer, and separate-body relationships; old SemanticSymbol.TypedSymbols/TypedFacts/IrTypes fields are retired. Six existing reviewed lifecycle fingerprints differ. No fingerprints refreshed and no generator/publication run: current manifest and validator still assume the pre-canonical model. Earlier Chapter 7–9 views explicitly teach that earlier representation and need deliberate chapter-era preservation rather than silent relabeling.

Inspection found an extractor defect: bodyless partial properties were classified as auto-properties. SemanticFact.Type, SemanticScope.Class and SemanticType.Class are access paths, not additional stored links. Added tools/common-diagrams/test_extractor.py, exercising the real extractor on synthetic source with a field, auto-property, expression/body accessors and a partial declaration. Baseline4633 exit1 reported precisely the extra Navigation property (log ch10-diagram-extractor-baseline.log). Excluding partial property declarations corrected it;4635 exit0 verified the focused regression, clean offline build, fresh inventory and whitespace. Log ch10-diagram-extractor-fixed.log. No compiler behavior changed; full diagram validation remains blocked by the unmigrated manifest, not claimed passed.

Next inspect historical-view validation options and current lifecycle implementations, classify all actual stored relationships, preserve earlier chapter-era evidence, and add Chapter10 diagrams through the existing pipeline. Never blindly refresh lifecycle hashes. No live jobs, new children, commits or installation.

### Diagram evidence boundary and extractor integration

- Jobs4637–4652 inspected validation/rendering and confirmed `chapters.json` is the manifest (an initial `manifest.json` read failed without changes). The current validator binds every historical view to current Common declarations and special-cases obsolete nominal holder backlinks. Rendering success rewrites all marked chapter blocks; it has not been run.
- Accepted commit6073e13 contains the original manifest and Common declarations. Inspected historical SemanticSymbol.Type, SemanticFact.Type, SemanticScope.Class and SemanticType.Class: these were stored fields, not the newly discovered partial-property classification defect. This is bounded inspection, not a complete historical validation run.
- Source inspection TupleTypes.cs134–179 confirms complete two-way occurrence registration/removal, reserve-before-mutation and no destruction of shared component types. CanonicalTypes.cs317–354 clears IR, detaches nominal dependency, retires dependent tuples/arrays/holders, detaches owned occurrences, then element/analysis memberships. Diagram arrows must distinguish dependency invalidation from ownership.
- Regeneration recipe now runs test_extractor.py after its offline build and before inventory extraction. README documents the standalone command and explicitly marks the unresolved Chapter10 manifest migration. Job4651 exit0: shell syntax, actual extractor regression, whitespace and README readback passed. Full regeneration/diagram validation remains unperformed and not claimed passing. No manifest, lifecycle hashes, published figures, compiler source or earlier chapter blocks changed in this slice.
- Pending design decision: whether regeneration may depend on pinned Git history for chapter-era source validation, or must work from a source export without history. Preserve original chapter-era teaching and strict full coverage of current relationships either way; do not silently relabel old diagrams as current or waive retired edges.

### Updated diagrams accompany model revisions (Bill-approved direction)

Bill approved pinned Git history for historical validation and asked that changed earlier diagrams also be presented in updated form. Agreed direction: preserve the chapter-era view, present a clearly labeled revised view in the chapter changing the model, and link earlier teaching forward. Refine exactly-once coverage to one authoritative introduction per relationship; explicit revision views may repeat unchanged context, distinguish changed/new relationships, and must not silently permit accidental duplicates. Both historical and revised views need source/lifecycle evidence.

Job4657 exit0 validated the accepted baseline using the corrected extractor and a temporary source export from full commit6073e136c772019dc8dbe4ca11211340904cbf9e:10 Common files,17 nonstatic types,36 relationships,8 views. Existing validation, including lifecycle hashes and full stored-field coverage, passed. Saved receipt book/build/ch10-historical-diagram-validation.json. Temporary sources removed; no figures rendered/published. This is source-model validation, not new rendered-page inspection. Job4658 identified the six changed previously reviewed lifecycle files; they still require actual delta review before current fingerprints change. No manifest migration yet. Reminder decision pause is resolved; normal personal work continues without needing idle reminders.

### Lifecycle review for revised Chapter 10 views

Jobs4661–4663 inspected all six changed pre-existing reviewed-file deltas against6073e13: analysis teardown now calls DestroyTypes after holders/scopes; fact Type is computed from stored TypeLink and attaches directly to canonical type; MemberOwner generalizes nominal scope ownership; initializer scopes are co-owned by analysis/field and detach through the field method; separate implementation syntax is a reciprocal weak link whose attachment/removal invalidates IR; symbol retirement destroys initializer/member/function scopes and nominal type, detaches its own type and both syntax links; IR type membership moved from nominal declaration to canonical type, while initializer bodies reuse the existing IR/declaration relationship.

Jobs4664–4670 inspected CanonicalTypes.cs42–110,123–316,358–381 and TupleTypes.cs60–133, supplementing earlier retirement317–354 and tuple links134–179. Analysis owns the index, canonical keys publish only after payload links, tuple construction validates all inputs then creates ordered occurrence links and rolls them back on failed publication. Holder replacements validate/reserve before old-link removal; canonical type retirement cascades toward dependent users, never shared component inputs. Nominal backlink is direct, computed compatibility queries add no state. No runtime behavior edits/tests in this review; this is source/lifecycle evidence for diagram semantics, not a new safety certification.

Planned current views: canonical ownership; revised type users; nominal/default scopes (show both owners); separate method bodies. Keep source-validated historical views. Current inventory must remain completely covered. Explicit revision metadata identifies prior views and repeated context; accidental duplicates remain errors. Three retired nominal-holder relationships must identify replacement canonical-holder dependencies rather than disappear silently. No fingerprints refreshed or manifest changed yet.

## Restored original Chapter 10: diagram closure

The abandoned reorganization was not imported. The current diagram model was
reviewed against this restored compiler: TypeBuckets is the owning collection,
SemanticFact stores TypeLink, and field-initializer IR uses Declaration/IrBody.
CanonicalTypes.cs and TupleTypes.cs were read fully; the changed scope, symbol,
fact and IR lifecycle implementations were reread against 6073e13. The current
manifest records those reviewed bytes, not a fresh compiler correctness claim.

Added chapter10.json and a bounded revisions.py path. Historical source and the
historical manifest come from pinned local Git 6073e136c772019dc8dbe4ca11211340904cbf9e;
the current inventory is freshly extracted. Both retain full stored-edge coverage,
backlink validation, and separate type-model policy checks. Explicit predecessor
accounting covers removed and changed relationships, including unchanged-spelling
IR role changes. Four revised views mark NEW/CHANGED/CONTEXT; earlier blocks stay
historical with separate forward links. Current model: 18 types, 39 relationships.

Verification: job896 passed 47 historical and 31 migration checks plus the late
render-refusal control. Job897 published/read back 14 files and proved all 25
pre-existing figure files unchanged. All four final PNGs visually inspected;
the final IR role label was inspected separately after refinement. Actual SVGs
meet ten-point minimum at five-inch width. First preview failed 9.45pt because a
long heading widened the page; shortened the image heading while keeping the
full title in adjacent prose, without weakening the threshold. No physical print
or human acceptance claimed. Postpublication tests and idempotence checked.

Lesson: validate historical models against their actual source version. Do not
refresh old fingerprints against a changed model or import a same-named model
from the abandoned reconstruction. Generated IR bodies are program-owned even
when their weak declaration identity is a field.

Remaining: final chapter consistency and stable completion verification; exactly
one code reviewer, then one text reviewer, then isolated fresh student from
accepted Chapter 9. No compiler change was needed for diagram closure. No commits,
pushes, installations, or restarts authorized; Bill's walkthrough remains pending.

### Final author completion verification (in progress)

After the revised-diagram milestone, inspected Chapter 10 construction and completion teaching and the public Make targets. Corrected `make diagrams` to avoid rerunning the old active-tree validator after `regenerate.sh`, which now already runs the pinned historical and current revision checks. Dry-run confirms the single script entry. This is tooling integration only; no compiler or grader behavior changed.

Final cumulative run is Cadence job 904, using the actual `make test` target and a temporary SDK wrapper adding `--no-restore` only for builds. Canonical selfcheck rejected all four corruptions; Debug build clean and early suites pass. Run remains live: do not claim final success or restart it. Compiler, public graders and assemblies frozen until terminal receipt and summaries. No reviewer/student launched yet. Next: collect full two-configuration result, then exactly one consolidated code review, then text review, then isolated fresh student from accepted commit 6073e136c772019dc8dbe4ca11211340904cbf9e. Student must not receive Chapter 10 target sources/private development history. Human acceptance and commit permission remain pending.


## Final author verification complete; code review admitted

Job 904 completed with exit 0: the full retained Make target passed Debug and Release, with zero build warnings/errors. Exact summaries are in `ch10-final-author-verification.md`. Compiler/grader/assembly freeze lifted. No compiler edits during this completion pass. Canonical corruption selfcheck rejected all four deliberate defects. Human acceptance remains pending.

The current diagram guide now explicitly includes field-initializer bodies in IrProgram ownership; guarded publication job916 verified all 25 historical figures unchanged and whitespace clean.

Sole code reviewer `original-ch10-code-review-default` admitted as job924. Explicit primary names codebook-code-reviewer and ensemble-coding were refused before child creation because regular skills are not primary launch configurations; normal load_skill confirms codebook-code-reviewer active at root. Default child instructed to activate review/coding skills before inspection, read actual reference/chapter/tests and fresh evidence, produce one report, and not edit or recruit. No other reviewer/student launched. Next collect review, incorporate justified fixes with affected checks, then sole text review, then isolated student from accepted Chapter9 commit6073e13. No commit/push/install/restart/Chapter11 authority.

## Original Chapter 10 autonomous completion

Student original-ch10-student submitted on join1532; final unchanged cumulative Make job1533 terminal exit0, Debug and Release each zero build warnings/errors. Saved log inspected: both configuration totals present through20/20 contained executions and23/23 intended rejections.25 public runners per configuration (not earlier spoken26);106 public files hash-match per retained integrity receipt. Full copied receipt: book/notes/ch10-student-acceptance.md. Same student attempt, two authorized per-turn limit continuations; setup and existing-contract clarifications recorded; no parent candidate patches or grader edits during reconstruction. Student shut down; no rerun after passing. Candidate remains in Cadence child workspace, not merged into reference.

Final lessons integrated at their construction points in chapter10: exact identity versus permitted scalar conversion; table descriptors versus array payloads, saved object indices versus pointers, destruction and diagnostic ordering. These were student implementation regressions, caught by retained graders, not a changed language contract. Walkthrough and structural probes passed with limits in receipt; human walkthrough acceptance remains pending. Guarded publisher1566 passed18types39relationships/fourviews,14output readbacks, all34 figures unchanged, git diff --check clean. No compiler changes during author closure. No commits/pushes/installations/restarts/Chapter11. Completion pending human acceptance and commit permission; no further review or student campaign authorized.

## Human acceptance — Chapters 9 and 10 fully approved

Bill reports running `make spin09` and `make spin10` and explicitly approves both chapters in full. This completes human walkthrough acceptance, superseding earlier pending-acceptance status. Chapter 9 approval is reaffirmed; original Chapter 10 is accepted. No further reviewer/student campaign or rerun is required. Commit permission remains separate under the existing explicit no-commit boundary; no Chapter 11 work, push, installation or restart is authorized by this acceptance.
