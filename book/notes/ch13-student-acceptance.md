# Chapter 13 independent reconstruction evidence

Parent inspected the full student report, complete per-configuration numerical
summaries, PEG log, walkthrough output and terminal receipts for jobs3827,3840,
3831,3866 (all exit0). Parent independently verified all306 candidate public hashes
and the exact approved manifest. Student shut down after submission; no passing
student reruns or parent implementation edits. Candidate remains separate from
the reference. Bill subsequently accepted Chapter 13 and the `spin13` walkthrough and authorized committing the accumulated work locally. The student report below retains its original pre-acceptance wording.
No claim of full memory-safety closure or exemplary formatting of every student
guard; structural self-review limits below are retained, not erased by test passes.

## Student submission

Chapter 13 isolated reconstruction completed; all required public checks pass. No target/reference/history access, reviewer/child campaign, installs, network, services, commits, or Chapter 14 implementation.

CANDIDATE
/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/ch13-student/candidate
REPORT + EVIDENCE ROOT
/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/ch13-student/
Read reconstruction-report.md for architecture, command block, structural review, lessons and limitations; make-test-summary.txt contains complete per-configuration numerical summaries; source-inventory.log compares against approved Chapter 12. Candidate compiler was extended, not replaced; 3 new source files IR/Borrowing.cs, IR/Ownership.cs, Semantics/Relationships.cs, existing modules modified, no prerequisite files removed.

IMPLEMENTATION
Common retains shared declarations/state. Added participant roles, policy-keyed bindings, hidden selected fragments, template-shape validation, persistent-field admission and participant-aggregate boundary rejections. Relationship facts are derived through existing contributor/view/scope relationships. Preparation shares its operation scope; transient fixed-point summaries reject protected writes through helpers and nested owning-local cleanup. Scalar participant storage/conversions are borrowed, while explicit concrete owning boundaries acquire Ref. Real membership IR instructions have cardinality/order validation against outer-body positions. Generated teardown walks the existing inventory: final hook, child fragments, parent fragments, owned fields, reclaim. Lifetime analysis includes fragments with only the active receiver identity exempted. Ownership simplification proves observing receivers and updates callers, preserves reciprocal def-use links and validates before/after; same-region Ref folding includes cleanup timing. Debug runtime uses nonreused allocation identities/tombstones, checked current-slot resolution and destruction states; production omits that metadata. Last release establishes a destruction hold before user cleanup and does not retain metadata pointers across it. Safe/unsafe remains separate from debug.

EXACT CONTRACT COMMANDS
All run with cwd=candidate and source ../environment.sh, which exports approved absolute installed tool paths and workspace-local HOME/DOTNET_CLI_HOME/NUGET_PACKAGES/TMPDIR/caches; compiler NuGet.Config clears feeds.
1. /Library/Developer/CommandLineTools/usr/bin/make test DOTNET="$DOTNET" PYTHON="$PYTHON"
Terminal job3827 exit0, log make-test-second.log; both Debug+Release, 0 build warnings/errors. All cumulative Make runners passed including migration successors, retained regressions, production/debug relationship modes, and unsafe emission-only negatives. Per configuration Chapter13 totals: migrations 18/18+15/15 and 10/10+19/19; list refusals 4/4; debug-mode4/4; debug-identity64/64; relationships21/21 execution+32/32 rejection in each of two runtime modes; participant IR2/2; aggregates4/4+11/11; syntax6/6. Both configurations also passed 99,826 NFC vectors/all1,112,064 scalars/7 malformed requests. Full earlier chapter totals retained in make-test-summary.txt.
2. "$NODE" tools/peg/test.mjs
   "$NODE" tools/peg/test.mjs graders/ch11/type-syntax.json
   "$NODE" tools/peg/test.mjs graders/ch11/matchtype-syntax.json
   "$NODE" tools/peg/test.mjs graders/ch12/multi-class-syntax.json
   "$NODE" tools/peg/test.mjs graders/ch13/relationship-syntax.json
Terminal job3840 exit0, peg-final.log: 50/50,5/5,11/11,16/16,6/6 plus negative controls. Uses candidate Release assembly.
3. /Library/Developer/CommandLineTools/usr/bin/make spin13 DOTNET="$DOTNET" PYTHON="$PYTHON"
Terminal job3866 exit0, spin13-final.log. Both AoS and SoA generated C with runtime debug: exit0, empty stderr, exact stdout:
attachment keeps child alive
parent final sees attached child
child final sees cleared links
parent teardown complete

INTEGRITY
job3831 exit0, public-integrity-final.log: 306/306 hashes match; manifest byte-identical to approved public source, SHA256 cd6c7dc70d24ca77f196d2a2b5b40c9c5a523f5ca423c065c6f1670801dbd0d4. Public inputs remained immutable and compiler source frozen throughout successful full tests. Terminal tool receipts retained in jobs/3827, jobs/3840, jobs/3831, jobs/3866. Required commands have no remaining live jobs; no reruns after all required checks passed.

CORRECTIONS / LESSONS
Initial diagnostics isolated missing template-level admission, reference-parameter borrowing and preparation helper/finalizer summaries. Corrected them; relationships then passed all public execution/rejection cases. Initial debug-identity result60/64 exposed observing receiver ownership, not simply allocator growth; ownership pass corrected it to64/64. Structural self-review found unused Ref cleanup timing was observable; intentionally cancelled first Make job3819 (exit9), tightened the fold proof to include cleanup and restarted frozen cumulative job3827. Cancelled partial results are not completion evidence. Important teaching lessons: participant view is not concrete class identity; noninstantiated templates still need shape checks; helper ref parameters need their own declared participant-view recognition; nested preparation cleanup differs from top-level local lifetime; unused ownership is not unobservable; C# partial defaults belong on declarations only. All lessons stated in chat and preserved in report.

STRUCTURAL REVIEW / LIMITS
Read-only self-review covered policy identity, preflight publication/refusal and reciprocal contribution destruction, derived borrowing queries, phased teardown, actual IR accounting validation, def-use mutation, debug identity/hold behavior. No supplementary source-specific C# corruption probes or independent reviewer campaign; public tests/controls provide behavioral evidence. Some new guards retain compact one-line layout, a teaching-readability limitation. Preparation analysis is verified for helpers and owning-local finalizers including public nested cleanup cases; exhaustive temporary/parameter-cleanup coverage was not independently established. This is not full memory-safety closure; Chapter14 aggregate participant borrowing/handle redesign stays deferred. Chapter/public inputs sufficed without hidden-source assistance; future teaching should keep the preparation-summary boundary explicit. Human acceptance, installation and deployment were not performed.

## Full student construction report

# Chapter 13 reconstruction

Candidate: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/ch13-student/candidate`

## Scope and isolation

Extended only the approved accepted Chapter 12 compiler and copied public bundle. No target solution/history, private probes, reviewers, children, network, installs, commits, or deployment. Installed SDK/compiler/node/Python and offline Peggy were used with workspace-local HOME, caches, NuGet packages and temporary storage. Public compiler NuGet.Config clears feeds. Human acceptance is not performed.

## Construction

- Common remains the declaration/state vocabulary; implementations stay in the existing functional modules. Added `IR/Borrowing.cs`, `IR/Ownership.cs`, and `Semantics/Relationships.cs`; no prerequisite files removed.
- Parse parent/child interface roles, selected partial destructor policies, and preparation blocks with placement constraints. Include policy in specialization equality; selected fragment implementations retain their contributor identity while avoiding public-name collisions.
- Derive relationship and borrowing facts through the existing binding/view/scope graph rather than duplicate ownership flags. Preflight contribution conflicts before publication; failed contribution construction destroys the instance and its reciprocal links.
- Admit scalar managed participant fields, reject unmanaged persistent ordinary handles and unsupported participant aggregates, but preserve explicit concrete owners and unrelated counted payload ownership.
- Preparation shares the operation scope. Top-level preparation locals survive structural work; nested cleanup executes during preparation. Transient fixed-point write summaries follow protected assignments, helper calls and owning-local cleanup. Structural return and out-of-operation protected mutation are rejected.
- Lower scalar participant storage and conversions as borrowed. Crossing into an explicitly owning concrete destination acquires a Ref. Shared IR queries and operation validators agree on storage versus value borrowing, including participant reference parameters.
- Emit explicit result-free membership accounting instructions and validate actual outer-body cardinality/order. Preparation precedes retain; mutation precedes release; final cleanup follows. Removal uses a private handle holder so runtime release does not erase a borrowed parameter.
- Teardown walks the existing contributed-method inventory in phases: ordinary final hook, child-role fragments, parent-role fragments, ordinary owned fields, reclaim. Managed links are not released a second time as ordinary fields. Lifetime effects include selected fragments; only the active receiver identity is exempted, not its whole class.
- Ownership simplification proves observing receivers to a fixed point and updates callers with immediate cleanup. Graph operations preserve reciprocal def-use links and validate before mutation. Ordinary Ref folding requires same-region observations and cleanup with no intervening effects; unused holders still have observable destruction timing. Full validation runs before and after the pass; pre-pass IR remains inspectable.
- Safe/unsafe semantics are separate from runtime debug emission. Debug handles use monotonic allocation identities and tombstones, resolve current physical slots at use, and reject stale/reused identities and recursive destruction. Production omits debug identity/state machinery. A last-release destruction hold allows balanced cleanup temporaries, and metadata pointers are not retained across user cleanup that can grow storage.

## Evidence so far

- Public integrity: job3831 exit0, `public-integrity-final.log`: 306/306 hashes match, manifest byte-identical to approved source. Manifest SHA256 `cd6c7dc70d24ca77f196d2a2b5b40c9c5a523f5ca423c065c6f1670801dbd0d4`.
- Five PEG suites: job3840 exit0, `peg-final.log`: 50/50, 5/5, 11/11, 16/16, 6/6; negative controls passed.
- Full cumulative Make: job3827 TERMINAL exit0, `make-test-second.log`; both Debug and Release complete, each build 0 warnings/0 errors. Full numerical summaries are retained in `make-test-summary.txt`. Chapter 13 per configuration: migration 18/18 + 15/15 and 10/10 + 19/19; list refusal execution 4/4; debug mode 4/4; identities 64/64; relationship execution/rejection 21/21 + 32/32 in each of production and runtime-debug modes; participant IR 2/2; aggregate controls/rejections 4/4 + 11/11; relationship syntax 6/6. All earlier retained suites in Make also passed.
- `make spin13`: job3866 TERMINAL exit0, `spin13-final.log`; build 0 warnings/0 errors; AoS and SoA each exit0, exact stdout, empty stderr. No tests rerun after required completion.
- Earlier make job3819 was intentionally cancelled (exit9) after structural review found an unused-Ref cleanup-window proof gap. Corrected the implementation, then restarted the frozen full check as job3827. Partial cancelled-run results are not completion evidence.

## Structural review and limits

Reviewed real graph registration/destruction, policy identity, preparation refusal, borrowed/owning boundaries, generated teardown, actual membership ordering, reciprocal def-use mutation, and runtime identity/hold behavior. No independent reviewer campaign was launched. Supplementary source-specific C# corruption probes were not run; public graders and their controls are the behavioral evidence.

Some added guards retain compact one-line layout; this is a teaching-readability limitation, not claimed exemplary formatting. Preparation-effect evidence covers helper calls and owning-local cleanup, including the published nested-finalizer cases; exhaustive temporary/parameter-cleanup coverage has not been independently established. Do not infer full memory safety closure from debug probes or these checks. Chapter 14 aggregate borrowing/handle redesign remains deferred.

## Lessons and teaching feedback

1. Participant view, not concrete class identity, determines scalar borrowing; same-class endpoints and explicit concrete owners need separate treatment.
2. Template declaration-shape checks must run even when a template is never instantiated.
3. Ref-parameter storage must carry the declared participant view inside a helper; it has no caller operand chain to inspect there.
4. Preparation local lifetime and nested cleanup are different: flattening the whole preparation lifetime would conceal finalizer side effects.
5. Unused ownership is not unobservable ownership: cleanup timing must be inside the fold's effect-free proof window.
6. Counted runtime growth probes can reveal receiver IR ownership conventions, not merely allocator bugs.
7. C# partial-member defaults belong on declarations, not both declarations and implementation signatures.

The chapter/public contracts were sufficient for this reconstruction without hidden source assistance. The distinction between local-finalizer summary coverage and exhaustive cleanup/lifetime closure should remain explicit in future teaching.

## Reproduction commands and terminal receipts

Working directory for every test: the candidate directory above. First `source ../environment.sh`; that file records the absolute approved tool paths and all local writable cache paths. SDK-only feed clearing is retained in compiler/NuGet.Config.

```sh
/Library/Developer/CommandLineTools/usr/bin/make test DOTNET="$DOTNET" PYTHON="$PYTHON"
# job3827 exit0, make-test-second.log (captured with pipefail and tee)

"$NODE" tools/peg/test.mjs
"$NODE" tools/peg/test.mjs graders/ch11/type-syntax.json
"$NODE" tools/peg/test.mjs graders/ch11/matchtype-syntax.json
"$NODE" tools/peg/test.mjs graders/ch12/multi-class-syntax.json
"$NODE" tools/peg/test.mjs graders/ch13/relationship-syntax.json
# job3840 exit0, peg-final.log; DOTNET exported by environment.sh

/Library/Developer/CommandLineTools/usr/bin/make spin13 DOTNET="$DOTNET" PYTHON="$PYTHON"
# job3866 exit0, spin13-final.log (captured with pipefail and tee)
```

Exact executed-C output asserted by the immutable spin13 runner, separately for AoS and SoA:

```text
attachment keeps child alive
parent final sees attached child
child final sees cleared links
parent teardown complete
```

Logs and this report live in `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/ch13-student/`. Tool terminal receipts are retained under that directory's `jobs/3827`, `jobs/3840`, `jobs/3831`, and `jobs/3866`. `source-inventory.log` records comparison against the approved accepted Chapter 12 source. Compiler/public bytes were frozen throughout the successful cumulative and grammar checks. No live Jobs remain from the required acceptance commands.

## Cumulative numerical summaries

```text
Grader comparison self-check passed
    0 Warning(s)
    0 Error(s)
82 passed; 0 failed
58/58 token cases passed
70/70 syntax trees passed
155/155 syntax recognition cases passed
7/7 parser boundary cases passed
82/82 semantic cases passed
80/80 execution cases passed
250/250 execution cases passed
PASS unknown and duplicate options rejected
87/87 unsafe checks passed
112/112 execution cases passed
6/6 execution cases passed
154/154 Chapter 8 checking cases passed
212/212 layout executions passed
4/4 free-slot allocation and 4/4 class growth comparisons passed
12/12 owned-field/array balance comparisons passed
21/21 IR cases passed
92/92 ownership IR cases passed
33/33 IR C-emission cases passed
5/5 canonical cases passed; selfcheck rejected 18 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
17/17 generic execution cases passed
10/10 generic rejection cases passed
17/17 typeof execution cases passed
8/8 typeof rejection cases passed
18/18 matchtype execution cases passed
13/13 matchtype rejection cases passed
1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
16/16 syntax recognition cases passed
PASS multi-class-participant-link-fields-unmanaged-rejected
PASS multi-class-nullable-role-chain-unmanaged-rejected
PASS multi-class-list-transitions-unmanaged-rejected
PASS multi-class-independent-list-memberships-unmanaged-rejected
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
PASS object-field-nonnull-unmanaged-rejected
PASS object-field-nullable-unmanaged-rejected
PASS object-field-detach-through-alias-unmanaged-rejected
PASS object-field-detach-through-call-unmanaged-rejected
PASS object-field-detach-and-destroy-in-call-unmanaged-rejected
PASS object-field-detach-through-returned-alias-unmanaged-rejected
PASS object-field-missing-initialization-unmanaged-rejected
PASS object-field-read-before-initialization-unmanaged-rejected
PASS object-field-zero-not-handle-unmanaged-rejected
PASS object-field-null-not-nonnull-unmanaged-rejected
10/10 object-field-migration execution cases passed
19/19 object-field-migration rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
3/3 unsafe unwrap-check omission cases passed (not executed)
8/8 nullable-handle-unsafe-valid execution cases passed
0/0 nullable-handle-unsafe-valid rejection cases passed
4/4 execution cases passed
4/4 debug-mode contracts passed
64/64 debug object-identity contracts passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
PASS borrowed participant wrapping and unwrapping (2/2)
4/4 participant-aggregates execution cases passed
11/11 participant-aggregates rejection cases passed
6/6 syntax recognition cases passed
    0 Warning(s)
    0 Error(s)
82 passed; 0 failed
58/58 token cases passed
70/70 syntax trees passed
155/155 syntax recognition cases passed
7/7 parser boundary cases passed
82/82 semantic cases passed
80/80 execution cases passed
250/250 execution cases passed
PASS unknown and duplicate options rejected
87/87 unsafe checks passed
112/112 execution cases passed
6/6 execution cases passed
154/154 Chapter 8 checking cases passed
212/212 layout executions passed
4/4 free-slot allocation and 4/4 class growth comparisons passed
12/12 owned-field/array balance comparisons passed
21/21 IR cases passed
92/92 ownership IR cases passed
33/33 IR C-emission cases passed
5/5 canonical cases passed; selfcheck rejected 18 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
17/17 generic execution cases passed
10/10 generic rejection cases passed
17/17 typeof execution cases passed
8/8 typeof rejection cases passed
18/18 matchtype execution cases passed
13/13 matchtype rejection cases passed
1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
16/16 syntax recognition cases passed
PASS multi-class-participant-link-fields-unmanaged-rejected
PASS multi-class-nullable-role-chain-unmanaged-rejected
PASS multi-class-list-transitions-unmanaged-rejected
PASS multi-class-independent-list-memberships-unmanaged-rejected
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
PASS object-field-nonnull-unmanaged-rejected
PASS object-field-nullable-unmanaged-rejected
PASS object-field-detach-through-alias-unmanaged-rejected
PASS object-field-detach-through-call-unmanaged-rejected
PASS object-field-detach-and-destroy-in-call-unmanaged-rejected
PASS object-field-detach-through-returned-alias-unmanaged-rejected
PASS object-field-missing-initialization-unmanaged-rejected
PASS object-field-read-before-initialization-unmanaged-rejected
PASS object-field-zero-not-handle-unmanaged-rejected
PASS object-field-null-not-nonnull-unmanaged-rejected
10/10 object-field-migration execution cases passed
19/19 object-field-migration rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
3/3 unsafe unwrap-check omission cases passed (not executed)
8/8 nullable-handle-unsafe-valid execution cases passed
0/0 nullable-handle-unsafe-valid rejection cases passed
4/4 execution cases passed
4/4 debug-mode contracts passed
64/64 debug object-identity contracts passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
PASS borrowed participant wrapping and unwrapping (2/2)
4/4 participant-aggregates execution cases passed
11/11 participant-aggregates rejection cases passed
6/6 syntax recognition cases passed
```
