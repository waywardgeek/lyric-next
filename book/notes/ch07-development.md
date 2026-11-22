# Chapter 7 development — dynamic arrays and stable borrows

Bill authorized proceeding after agreeing size/capacity, roughly 50% allocation
growth, typed zero defaults, non-defaultable reference restrictions, and fatal
allocation failure. Dynamic storage uses malloc/realloc/free, distinct from fixed
SoA property backing. Element borrows preserve storage through last possible use;
descriptor borrows may resize only without invalidating still-needed elements.

Approved scope: integers, booleans, nested arrays, strings as [u8], ordinary value
copying/parameters/returns and cleanup; local element borrows and ref parameters
including resizing helpers. Classes, floating expressions, borrowed ranges and
reference returns remain outside the chapter. Reserve/resize/truncate signatures
must be completed before their implementation; a separate truncate is preferred
for non-defaultable arrays. No runtime implementation follows from spec edits.

Inspection found existing len(array) and array.push(value). Free append(array,
value) returns an independent array; the earlier suggested append(ref array,value)
would conflict and is not adopted. Retain established names. Initial tests focus
on values/indexing before growth and borrow analysis. Existing scalar enum plus
ByteArray cannot express nested array types; extend it with an immutable value-like
scalar-kind/array-depth descriptor, not a mutable registry or copied symbol model.

## First baseline and type foundation

Ten initial independent execution cases all failed against the Chapter 6 binary
with expected unsupported-array/unknown-len diagnostics; saved initial-baseline.txt.
No crash was observed. The current SemanticType is now an immutable record struct
with ScalarKind and ArrayDepth; equality is structural and ByteArray is U8 depth1.
Common declares fields, constructor and shared APIs; Semantics implements them.
ArrayOf/ElementOf change one layer; scalar predicates do not classify arrays as
numeric. Existing type registration and arithmetic helper enumeration use the same
predefined list. No registry or new relationship-bearing model was added.

First refactor build failed because the emitter retained Enum.GetValues on the
former enum; corrected to Predefined. Job946 then exited0: clean Debug build,
79/79 semantic (--chapter=6),75/75 Chapter5 and250/250 Chapter6 checks. These are
refactor regressions, not Chapter7 functionality; Release and full source/Unicode/
parser suites have not yet been rerun. Logs are under ch07-checks/type-*.txt.

Next: add array literal/type/index checking and resolved standard len identity,
then C storage/copy/cleanup. Existing static string spans must become ordinary
[u8] ownership; do not preserve a second nominal string lifetime policy. Required
cleanup includes expression temporaries, parameters, returned values, branches,
loops and all exits. Keep single evaluation/left-to-right effects. Borrow analysis
and growth follow the working array-value slice. Mixed inferred element coercion
is not specified: do not silently invent it; initial fixtures are homogeneous or
explicitly typed. Library API inspection established len(array), array.push(value),
and nonmutating free append(array,value). Reserve/resize/truncate exact signatures
are not yet recorded. No Chapter7 draft body/reviews/student or commit/push.

## Array semantics and first owned backend

Bill selected u64 rather than an i32 count limit. The specification's core len
contract, array method contract, and size/capacity paragraph now agree: u64
counts, checked target-size/byte multiplication before allocation. The resolved
Length builtin returns U64. Its parameter explicitly accepts an array shape,
not a fabricated concrete scalar or byte-only type; ordinary parameters remain
concrete. Debug build and positive u64/negative i32 assignment probes passed.

Array type reading, literal contexts, index type checking, and mutable rooted
index assignment are implemented. Hints and expression checking retain explicit
stacks. Contextual empty arrays work; an uncontextualized empty literal rejects
with LYC320 (currently file-start location because it has no anchored child).
Mixed inferred literal coercion remains a design gap: current first-element
inference must not be mistaken for an adopted general mixed-type policy.

The C backend now uses ly_array (data, u64 length, u64 capacity), including strings.
Allocation guards against SIZE_MAX/stride before multiplication; LYR007 is size
overflow and LYR008 allocator OOM. LYR006 checks size, not spare capacity, and
rejects negative signed indices before unsigned comparison. No compiler pragma
replaces dynamic metadata checks; Clang optimization may remove proven redundant
checks, but no assembly/performance claim has been verified.

Recursive copy/drop use checked type depth and scalar stride, not a runtime
registry or callbacks. Literals evaluate left-to-right and transfer child owned
values. Array reads currently copy, including read-only len/index consumers;
this is correct but inefficient and is not a claim of optimized borrowing.
Emission-only scope inventories track owning C descriptors. Moves empty source
slots; return preserves its result while cleaning remaining function scopes;
break/continue clean only scopes inside the nearest loop. Parameters own their
independent argument copies. Static literal bytes are copied into owned storage.
Normal scope cleanup is separate from fatal termination, which does not unwind.

First public array run passed11/11 at O0/O2. Expanded public cases passed25/25:
18 successes,5 compile-time rejections,2 bounds failures. Cases cover u64 length,
recursive copies/replacement, string mutation, calls/returns, conditional results,
loop exits, short circuit, left-to-right effects, let mutation and invalid types.
Retained Debug Chapter4 passed79/79 and Chapter5 passed75/75, but Chapter6 initially
failed17 cases (233/250): new group move bookkeeping looked up an un-emitted child
of a constant-folded scalar group. Restricting this transfer to array groups
corrects the cause; Job977 verified a clean Debug build and250/250 Chapter6,25/25 Chapter7. Lesson: ownership
bookkeeping follows emitted evaluation, not every syntactic child indiscriminately.
Original failure logs preserved. Supplemental O0/O2 ASan/UBSan plus real malloc/free
outstanding-allocation probe Job978 passed18/18 successful public programs at both optimization levels, with zero outstanding generated allocations. Neither is a full safety proof.

No growth, capacity reservation API, default resize, truncate, local references or
helper-aware borrow checking yet. No Chapter7 reviewer/student campaign, commits,
pushes, or human walkthrough acceptance. Next finish this verification, then
continue the approved increment with explicit growth signatures and borrow rules.

The later spec inventory reread corrected my mistaken identification of a second
len signature: it was dictionary len, not array len. Restored that unrelated i32
declaration, and explicitly documented array len()/length as u64. Dictionary and
index_of result widths remain separate contracts, not silently redesigned here.
Full make test (now includes Chapter7) completed Job980 exit0: clean Debug and
Release builds and all retained suites through Chapter7. Each configuration passed
82 source,58 tokens,66 trees,143 syntax recognition,7 parser boundaries,79 semantics,
75 Chapter5,250 Chapter6 and25 Chapter7 cases, plus full Unicode checks. Separate
143-case Peggy comparison Job984 exit0. Reports retained under ch07-checks.
No jobs remain live. The next growth API proposal is not yet approved: mutating
array methods reserve(n:u64), resize(n:u64), truncate(n:u64), all unit; truncate
would be a no-op for n>=size, never grow. Existing push(value) remains canonical.
Ask Bill about that contract before encoding it in implementation/fixtures.

## Growth contract accepted

Bill approved writable-receiver unit methods reserve(n:u64), resize(n:u64),
truncate(n:u64), and existing push(value). Truncate is a no-op at or above
current size; reserve never changes size or shrinks. Added eight public growth
cases before implementation, covering typed defaults, nested release/regrowth,
copy independence, writable nested receivers, and strings. These are new checks,
not part of the previously verified 25-case milestone.

### Implemented growth slice

The original 25 checks passed and all eight initial growth checks failed with
unsupported Member (growth-baseline.txt). Added value-like ArrayOperation on the
owning SemanticFact; checker resolves the bounded intrinsic method once, emitter
dispatches that checked value rather than spelling. Ordinary len remains a resolved
standard symbol. Methods require initialized writable storage and one argument;
sizes accept u64 by the normal safe-widening rules, push accepts the element type.

Runtime reserve checks count against SIZE_MAX/stride before multiplication,
uses ~50% geometric growth with minimum one and required-size floor, clamps the
preferred capacity to the allocation limit, and updates data/capacity only after
successful realloc. Shrink releases removed nested arrays before reducing live
ownership; growth uses typed C zero initialization, not a blanket memset. Push
moves its independently evaluated argument into the appended live slot. Strings
use the same implementation. No shrink operation reduces capacity.

Lesson: growth creates invalidation risks before named refs exist. Nested method
receivers and indexed assignment targets already select storage before evaluating
arguments/RHS. A temporary checker stack conservatively protects their root array,
including index evaluation, and rejects any mutation of that root with LYC324.
This is intentionally root-level precision, not final last-use borrow analysis.
Whole descriptor selection remains valid across backing relocation; a positive
case allows resizing that same descriptor during push argument evaluation.

Debug build zero warnings/errors; growth-first.txt33/33. Expanded cases41/41:
28 successful programs,10 static rejects,3 runtime failures. Includes nested
receiver/store invalidation, root replacement, self-value push and preallocation
byte overflow (no huge allocation attempted). Full cumulative run is pending;
no sanitizer or allocation-balance claim for realloc yet. No review/student.

Growth milestone verified: Job998 full make test exit0, clean Debug/Release and
all retained checks including99826 NFC vectors/all1112064 scalars/7 malformed,
82 source,58 token,66 tree,143 recognition,7 boundary,79 semantic,75 Chapter5,
250 Chapter6,41 Chapter7 in each configuration. Job1005 PEG143/143 exit0.
Job1001 real malloc/realloc/free allocation-count wrappers plus ASan/UBSan at
O0/O2 passed28/28 successful cases with zero outstanding allocations. This is
Apple Clang/macOS evidence, not a universal proof or fatal-exit cleanup promise.
Bounded injected realloc refusal initially failed the probe expectation because
I omitted the existing quotes around trace function names (growth-oom.txt).
Corrected expectation only; Job1008 passed O0/O2 with exact exit1, empty stdout,
LYR008/location/quoted grow/main trace (growth-oom-corrected.txt). No actual OOM
was induced and no allocator guarantee inferred. Whitespace check passed.
All growth jobs collected. Next: explicit ref parameters/local aliases and
helper-aware stable-borrow analysis; full Chapter7 teaching/reviews/student remain
outside this completed slice. No commit/push/walkthrough acceptance.

## Scalar reference foundation (in progress)

Added nine initial public reference cases; unchanged compiler baseline is
`ch07-checks/references-baseline.txt`: 41/50, with all nine failures reporting
unsupported TypePrefix/Unary, not crashes. Added three further scalar successes
and five explicit reference rejection cases afterward. The public suite now has
58 cases; array reference implementation remains pending.

`SemanticSymbol.IsReference` records fixed nonowning storage mode independently
of referent `Type`. Constructor validation occurs before scope attachment.
`Mutable` on a reference grants referent writes, not alias retargeting. The first
implemented slice deliberately accepts only initialized writable scalar names:
local aliases, exact-typed explicit ref parameters/arguments, forwarding, scalar
value returns, and overlapping scalar aliases. It still rejects array descriptor
and indexed acquisition until storage-invalidation analysis is implemented.
The emitter distinguishes C pointer declarations from dereferenced reads/writes,
selects addresses without value copies, and excludes reference parameters/locals
from ownership registration. Ref returns remain unsupported; ordinary alias reads
are values. This is not yet array borrow safety.

`scalar-reference-build.txt`: Debug build zero warnings/errors.
`scalar-reference-expanded.txt`: 11/11 (six positive programs at O0/O2 and five
exact static rejections). Retained semantic/Chapter5/Chapter6 run is pending at
this writing (job 1034). No Release/full cumulative rerun for this slice yet.

Proposed next analysis: invalidation followed by possible use is rejected, so an
unused stale alias does not unnecessarily prohibit growth after its last use.
Derived alias acquisition counts as use; joins/loop back edges propagate possible
invalidation. Helper summaries must remain parameter-specific and transitive;
this design is not implemented or proven by the scalar tests.

Scalar follow-up evidence: job1034 completed Debug79/79 semantics,75/75 Chapter5,
250/250 Chapter6. Job1037 passed52/52 supported Chapter7 cases with six pending
array-reference cases explicitly excluded, not removed/reclassified. Inspection
then found conditional type preview still reading `ref i32` as an unsupported
value type. Retained `ref-conditional-preview` demonstrated LYC306 TypePrefix
(0/1, reference-preview-baseline.txt). Preview now strips the storage marker only
for value prediction; real checking still enforces permissions/acquisition.
Job1044 clean Debug build and53/53 supported cases after correction, with six
array-reference cases still pending (59 total public cases). Earlier79/75/250
passes preceded the small preview correction; no fresh full cumulative/Release
claim. Lesson: new storage modes affect provisional type prediction as well as
real binding checking. All jobs through1044 collected; no live work.

Next: array-reference provenance and possible-use invalidation, including
selected call arguments across later argument effects, branches/loop back edges,
and parameter-specific transitive helper effects. Pointer-only positive examples
are not sufficient. No array descriptor/index ref syntax enabled yet. No new
review/student campaign, commit, push, or chapter completion.

## Local stable borrows before interprocedural effects

Implemented a temporary control-flow graph inside Compilation.Check, after each
function body has semantic facts. Events acquire/use storage or invalidate array
backing; edges model branches, short circuit, return, break, continue and loop
back edges. A forward worklist merges possible invalidation. Acquisition clears
only the newly acquired handle, allowing iteration-fresh aliases and growth after
the last possible use without permitting old-alias revival. No persistent duplicate
semantic model was introduced. Paths identify resolved owner symbols and nesting
depth; different indices are conservatively possibly overlapping. This precision
limit can reject safe sibling-array operations and is not a new language prohibition.

Local ref acquisition now accepts arrays and indices. Array-ref parameters remain
explicitly unsupported until helper effects are checked. Scalar-ref helper calls
select arguments left-to-right and retain each selection through later argument
effects. Descriptor references survive backing changes; element/interior references
do not. LYC326 diagnoses the unsafe later use. Backend pointer selection was already
present; no new array-helper ownership transfer is enabled yet.

Public baseline Job1051:53/66, with all thirteen failures unsupported refs rather
than crashes. Job1054 clean Debug build. Job1055:62/66, only four array-ref helper
cases pending. Expanded Job1058:72/76, same four pending; fresh loop aliases, loop
break, reacquisition, returning arms, descriptor replacement, nested descriptor
growth, alias-based invalidation, temporary call arguments and continue back edges
all passed. Job1059 retained Debug79/79 semantics,75/75 Chapter5,250/250 Chapter6.
No Release/full cumulative or sanitizer verification of this borrow change yet.

Added another eight helper fixtures afterward (not yet run): ordering-sensitive
overlap, distinct owners, forwarding, recursion and nested effects. Current count
is derived from grader lists, not the preceding successful run. Those fixtures
do not convert the 72/76 observation into a later full-suite pass. README now
documents the lifetime diagnostics and explicitly retains pending requirements.

Lesson: mutation summaries alone cannot detect use of an invalidated parameter
inside a helper. Preserve ordered mutation-then-use requirements, instantiate
them against actual argument paths, and propagate through recursive call groups
to a fixed point. Write-then-grow overlap can be safe; grow-then-write overlap is
not. Next implement those summaries before enabling array-ref parameters, and
exclude borrowed argument pointers from emitter Moved cleanup bookkeeping.

## Parameter-specific helper effects and ordered overlap obligations

Array-reference parameters are now enabled behind interprocedural borrow analysis.
All function bodies are checked first. Temporary summaries keyed by resolved function
symbols record formal-root/depth backing mutations and ordered mutation-to-use
obligations. A monotone fixed point propagates both through forward and recursive
calls; a final pass diagnoses actual invalid uses. Formal reference parameters enter
the graph as acquired aliases. Invalid state now retains mutation causes, allowing
uses across distinct formal roots to become caller obligations rather than rejecting
all potentially overlapping helpers. Actual arguments substitute their root/depth
paths into those obligations. Same-root deeper uses are unsafe; distinct owning
value bindings cannot overlap. Index distinctions remain conservatively collapsed.

Order matters: writing an element then growing its owner is allowed when that element
is not used again. Growing then using that element is rejected at an overlapping
call. A caller's live old alias is invalidated by helper effects even if the callee
never uses it. Argument storage is selected left-to-right and checked again after
later argument effects. Fresh acquisitions kill only their own invalid history.
Emitter argument transfer now skips reference parameters: caller-owned descriptors
must not be reset as though their values were transferred to callee ownership.

Initial build failed because unnecessary ToArray calls depended on an absent LINQ
import; graph construction only reads summaries, so removed those calls rather than
adding a dependency. Failed log helper-build.txt retained. Corrected build had zero
warnings/errors, and helper-first.txt passed all 84 then-public cases. Added six
retained cases for safe forwarded ordering, fresh acquisition after helper growth,
mutual recursion, mutual invalidation, later-argument helper effects, and a stale
local alias inside a helper. helper-expanded.txt passes 90/90 at O0/O2 (Debug compiler).
Full cumulative Debug/Release run is pending in helper-cumulative.txt; no full-suite,
Release, sanitizer, PEG or human-use pass is claimed yet for this implementation.
No reviews or student trial have been launched.

### Helper milestone verified

helper-cumulative.txt completed successfully in job1074: clean Debug/Release builds,
82 source cases, 99,826 NFC vectors, all 1,112,064 Unicode scalars for XID, seven
malformed Unicode requests, 58 tokens, 66 trees, 143 recognition, seven boundaries,
79 Chapter4 semantics selected for Chapter6, 75 Chapter5, 250 Chapter6, and 90
Chapter7 cases in EACH configuration. helper-sanitizers.txt (job1079) passed all
90 cases with CC='cc -fsanitize=address,undefined -fno-sanitize-recover=all', using
the unchanged grader and Debug compiler at both O0/O2. This sanitizer run did not
repeat allocation balancing. helper-peg.txt passed 143/143 through the separate
Peggy checker (job1087), not its smaller default fixture set.

Added tools/spin07.py: grows and sums, copies, mutates only the copy through a ref
helper; demonstrates fresh acquisition after last use and rejects a stale element
alias. Direct script run passed with exact output/diagnostic and temporary cleanup
(helper-spin07.txt, job1087). Added make spin07 with Release prerequisite; its
receipt completed successfully and its full log was read in job1112: clean Release
build, exact expected outputs and rejection, and temporary cleanup. Human walkthrough
remains unperformed. All 90
public fixtures now pass, but this is implementation evidence, not full chapter
completion. Full teaching/exercises are next; no reviewer/student launched.


### Full teaching draft

Wrote book/chapter-07.md from direct reads of the checked type, array operation,
storage-selection, ownership emission, and borrow graph implementation. The draft
teaches descriptor length/capacity, recursive independent copies and transfers,
cleanup at every control-flow exit, typed defaults and checked allocation growth,
fixed reference storage mode, temporary selections, graph invalidation with causes,
and parameter-specific ordered helper obligations propagated to a fixed point.
It includes cumulative commands, retained/read-only grader rules, and the verified
spin07 walkthrough. Explicit limits include conservative root/depth overlap,
currently excessive read copies, unsupported future features, platform evidence,
and no final allocation-balance rerun. No full safety proof is claimed.

Corrected a stale emitter comment that still called compound targets local-only;
no executable behavior changed. Updated book index and grader status. Chapter draft
is 3,912 words before any later edits; link/fence and whitespace checks passed.
No independent Chapter7 reviewer or fresh student has been launched. Human use
and acceptance remain pending. Next authorized-boundary decision: request permission
for the single code review, then single text review and fresh reconstruction under
the established workflow. No commit, push, installation, or new chapter.


### Single code review correction

Bill authorized the full completion sequence without repeated permission requests.
Code reviewer lyric-ch07-code-review-01 submitted one consolidated review and was
shut down. Findings retained in ch07-code-review.md and the original typed child
submission. Reviewer found inferred reference aliases omitted from provisional
hint traversal, changing narrow arithmetic semantics relative to redundant ref
annotations. Fresh Debug build reproduced six failures in twelve retained new
cases (review-preview-baseline.txt): annotated controls passed; inferred aliases
falsely rejected widening or incorrectly avoided u8 overflow.

Corrected DiscoverHints to traverse ref without effects; excluded unresolved ref
initializers from BranchHint's default-i32 fallback. Full targeted Chapter7 now
102/102 with clean Debug build (review-corrected-*). Integrated rationale into the
chapter's original reference-preview construction point. Full cumulative run,
PEG and walkthrough are running in job1192; no final pass claimed yet. No re-review.
Text review is next after verification. Public files/implementation remain fixed
while the verification runs. Compiler README overview updated for current scope.

Correction verification completed: job1192 exit0, saved summaries read in job1203.
Both builds zero warnings/errors; all retained suites passed including102/102
Chapter7,250 Chapter6,75 Chapter5,79semantics and full earlier Unicode/parser.
Separate PEG143/143 and make spin07 passed. No new sanitizer run for the twelve
added cases is claimed. Single code review correction complete; text review next.

## Text review and fresh reconstruction admission

The single text reviewer identified three documentation corrections, now integrated: replacement is an explicit backing-invalidation event and helper effect; direct indexed compounds remain unsupported while named scalar aliases support compounds; sanitizer evidence is the earlier 90-case run, not all 102. Reviewer submitted and was stopped. No implementation or grader change resulted; no second review.

Verified all 26 accepted Chapter 6 candidate hashes before copying. Fresh public bundle: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch07-student-input`, 232 files, INPUTS.sha256 digest `b26066801b5963ca6166a574377ed26ad97d4b979740abb98560ce3d662704a8`. Excludes target Chapter 7 compiler, author notes, repair history and build outputs. Includes public source/data/tools and accepted Chapter 6 compiler. Fresh confined student `lyric-ch07-student-01` admitted with explicit read-only bundle/toolchain imports. Spawn Job1249, join Job1250. Reconstruction ongoing, no pass claimed. Student alone owns implementation edits; no new reviewer campaign or next chapter. Human walkthrough remains separate.

## Reconstruction complete

Fresh student submitted all required passes; full report preserved in ch07-student-report.md. Parent verified saved cumulative totals, PEG/spin logs and exit receipts, 27 source/10 artifact/232 frozen input hashes. Student stopped=true; no passing rerun. Integrated loop-condition target-admission teaching feedback without changing frozen inputs or student code. Both reviews and reconstruction complete; human acceptance pending. No commit/push/next chapter.
