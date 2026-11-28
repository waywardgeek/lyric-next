# Chapter 14 — independent reconstruction receipt

Bill explicitly approved Chapter 14, both prose and the `spin14` walkthrough, correcting an initial chapter-number typo. Human acceptance is complete; commit permission remains pending. The student report below preserves its historical pre-acceptance status.

One fresh confined student, `ch14-student`, completed the assigned reconstruction. Parent inspected its complete report, numerical inventory, enforcement map, comment-only post-test delta, and saved job endings (4219 and 4241: exit 0). Parent independently verified all 317 public files and the manifest against the approved bundle. No parent implementation patches or trial-time contract changes were supplied. Student shutdown confirmed after submission; candidate preserved, not merged into reference. No rerun after success.

The two post-test edits change comments only; no post-comment executable rebuild is claimed. Student setup failures are recorded below, not counted as passes. Chapter 13 forward-looking prose was corrected after the student identified its obsolete borrowed-element proposal; this does not change Chapter 14 requirements or trigger a rerun.


## Student completion report

# Chapter14 isolated student completion

## Result and scope

Complete: all applicable assigned execution passed. Candidate: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/ch14-student/candidate`.
Personally extended 44-file accepted Chapter13 import pinned by the assignment to `14dd2fe574b615d65e222f636d2513d454f30132`. No children/reviewers, hidden target/history inspection, public-input modifications, package installation/download, commits, publishing, deployment or service restarts. Human acceptance remains pending.

## Implementation

Only compiler/Emission/Compilation.cs and compiler/Semantics/Check.cs differ functionally from the prerequisite. Containers own counted elements: remove the old participant aggregate admission ban and retain existing IR Transfer (retain borrowed observations then move into owning destinations). Keep type, managed-link write and lifetime checks.

Production AoS uses stable nominal record pointers into fixed chunks with in-record metadata; production SoA uses uint32 handles and separate arrays, zero sentinel, a checked uint64 high-water cursor, and free-slot reuse before exhaustion. Debug preserves uint64 monotonic identities, resolution/state diagnostics and UINT64_MAX sentinel. Handle/null/metadata selection consistently covers locals, saved receivers, nullable values, array strides/copy/drop, membership holders, and reclamation. Release preserves the destruction hold and does not dereference saved metadata after callbacks may relocate storage.

See `source-audit.md` for the detailed enforcement map, source locations, ownership/destruction review and reconstruction rationale. `receipts/compiler-functional.diff` records the verified functional changes against the permitted prerequisite.

## Actual terminal evidence

- Offline SDK-only restore and Debug build passed; `receipts/restore.log`, `receipts/build-debug.log`. Build zero warnings/errors. Local environment recipe: `env.sh`.
- Focused job4217 passed: production handles 2/2; allocation 4/4 free-slot plus 4/4 growth comparisons; debug 4/4; aggregates 16/16 executions + 2/2 rejections; cross-call safety 18/18; initialization 9/9; access 4/4; call admission 3/3. Logs `receipts/focused-*.log`.
- Unchanged full Make test job4219: DONE exit0; `receipts/make-test.log`, `receipts/make-test.exit`. Comparison self-check plus **48 runner invocations per configuration**, Debug and Release (**96 total**). Both builds zero warnings/errors. Every published retained runner and Chapter14 successor in the unchanged Make target completed. Full per-configuration numerical summaries and runner list: `receipts/numerical-inventory.md`.
- Four aggregate modes in each compiler configuration: AoS/SoA × production/debug, each 16 successful executions and 2 intended rejections. This is 128 execution checks and 16 rejection checks across the eight invocations, not 144 unique fixtures.
- PEG+Spin job4241: DONE exit0; `receipts/peg-spin.exit`. PEG base50/50, retained Chapter3 parser155/155, type5/5, matchtype11/11, multiclass16/16, relationships6/6 = **243/243 fixture checks**. Each run compiled all188 canonical grammar rules and passed four malformed-grammar controls (24 control executions). Logs `receipts/peg-base.log`, `peg-parser.log`, `peg-type.log`, `peg-matchtype.log`, `peg-multiclass.log`, `peg-relationships.log`.
- Spin14: all four runtime/layout executions produced the expected six output lines. Stale-borrow example rejected at compilation with LYC326 and explicitly not executed. `receipts/spin14.log`.
- No invalid unsafe/lifetime case was intentionally executed. Successful logs include specified negative diagnostic checks, not arbitrary crashes treated as rejection.

All tests stopped after applicable success. No broad suite was rerun after documentation-only corrections.

## Integrity

Manifest verified on copying and at completion: **317/317 public files unchanged**. Frozen compiler verification: **44/44 hashes unchanged through tests**. Hash records: `baseline-compiler-hashes.json`, `verification-compiler-hashes.json`, `final-compiler-hashes.json`; terminal integrity receipts: `receipts/frozen-integrity.json`, `receipts/final-integrity.json`.

- Public manifest SHA256: `74fa60530ccb233b70365837a84c70b562da08c1b61da0ef9a8e43ae80511c8a`
- Frozen compiler hash-manifest SHA256: `8690ecc1950747ee4faaa345c664bf3ea0efdd59b81d546f074856ad3dbdc1a4`
- Final compiler hash-manifest SHA256: `3296d67576712c2caa44412fe2f71bff4513768083220b2760447b5b047642c9`
- Tested Debug DLL SHA256: `f209a25932e849d234db0765f5d1a0f9b6ea753e5caa8bb5ce01f8ee828e62c6`
- Tested Release DLL SHA256: `5a858c12c5e8df45d66118a43246556c44078e1746a483161d59dd532ea9935b`

After tests, corrected two comments in Emission/Compilation.cs that incorrectly generalized debug's sentinel to all layouts. `receipts/post-verification-comments.diff` records the entire post-test delta. A comparison excluding comment lines proved all other lines identical. Thus the stored assemblies are the tested frozen build, not a claimed rebuild of the final comment-only text. Final public hashes reverified317/317.

## Failed experiments, clarifications and lessons

1. Premature PEG job4226 failed because Make had not yet created the candidate Release assembly. Its grammar/control compilation succeeded, but missing lexer assembly is setup failure, not syntax rejection. Preserved `receipts/peg-base-premature.log`; corrected by waiting for candidate Release, never using a parent assembly. Rerun job4241 passed.
2. First compiler integrity diagnostic used unresolved Path('.').parent and could not find the manifest; a later shell command masked Python's status. Corrected using Path.cwd() and fail-fast shell, then verified44/44; public317 had already passed. Lesson: validate the verification command itself and do not report an incomplete check as success.
3. Chapter13's stale future-work text conflicts with Chapter14's explicitly accepted owning-container semantics. Followed the newer assigned contract. Mandatory relationships remain explicitly unsupported future work; no borrowed-element container model added.
4. SDK printed a workload-verification notice; no workload update performed, and SDK-only restore/build passed offline.
5. Destination ownership governs reference transfer, while physical handle representation is layout-specific. Changing allocation alone would miss saved handles, sentinel checks, and nested container strides.

No implementation-test failure or suspected grader defect required a repair loop. No parent implementation patch or hidden solution assistance was used.

## Limits

Passing public checks and bounded source inspection are not formal verification. Bounded SoA allocator probes cover limit arithmetic/free reuse without physically allocating 2^32 slots. Supported direct known-body calls are checked; callbacks/function values/opaque calls and mandatory relationships remain outside this endpoint. Trusted relationship algorithms remain an assumption. No real external services or human adoption exercise performed. Installation and human acceptance are separate, pending decisions.


## Numerical inventory

# Unchanged Make test numerical receipts
Comparison self-check passed; terminal job4219 exit 0.

## Debug
```
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
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
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
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
18/18 Chapter 14 cross-call safety checks passed
9/9 Chapter 14 initialization safety checks passed
4/4 safe-initialized-length execution cases passed
0/0 safe-initialized-length rejection cases passed
3/3 Chapter 14 call admission safety checks passed
2/2 production handle contracts passed
6/6 syntax recognition cases passed
```

## Release
```
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
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
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
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
18/18 Chapter 14 cross-call safety checks passed
9/9 Chapter 14 initialization safety checks passed
4/4 safe-initialized-length execution cases passed
0/0 safe-initialized-length rejection cases passed
3/3 Chapter 14 call admission safety checks passed
2/2 production handle contracts passed
6/6 syntax recognition cases passed
```

## Runner invocations per configuration: 48
```
"$(PYTHON)" "$(GRADERS)/ch01/grade.py" -- "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch02/unicode.py" -- "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch02/tokens.py" -- "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch03/trees.py" -- "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch03/recognition.py" -- "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch03/boundaries.py" -- "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch04/check.py" --chapter=8 "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch05/run.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch06/run.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch06/unsafe.py" --chapter=8 "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch07/run.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch08/run.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch08/check.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch08/layout.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/allocation.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch08/ownership.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch09/ir.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch09/ownership.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch09/c_emission.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch10/canonical.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch10/tuples.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch10/structs.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch10/struct_values.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch10/array_defaults.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch10/borrowed_structs.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch11/generics.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch11/typeof.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch11/matchtype.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch03/recognition.py" --fixtures "$(GRADERS)/ch12/multi-class-syntax.json" -- "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch13/multi_class_migration.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch13/object_fields_migration.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch12/nullable_handles.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch13/list_refusals.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/debug_mode.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch13/debug_identity.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch13/relationships.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch13/relationships.py" --runtime-debug "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch13/participant_ir.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/aggregate_participants.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/aggregate_participants.py" --soa "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/aggregate_participants.py" --runtime-debug "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/aggregate_participants.py" --soa --runtime-debug "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/safety_calls.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/safety_initialization.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/safety_access.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/safety_boundaries.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch14/handles.py" "$(DOTNET)" "$$assembly";
"$(PYTHON)" "$(GRADERS)/ch03/recognition.py" --fixtures "$(GRADERS)/ch13/relationship-syntax.json" -- "$(DOTNET)" "$$assembly";
```


## Safety enforcement and structural audit

# Chapter14 source audit and reconstruction

Candidate: `candidate/`, reconstructed personally from accepted Chapter13 import (44 files, supplied pin 14dd2fe574b615d65e222f636d2513d454f30132). Only compiler/Semantics/Check.cs and compiler/Emission/Compilation.cs changed. No target solution/history read, no delegated children, grader edits, downloads, installs or deployment.

## Construction and rationale

Remove the temporary RequireAggregateElementView restriction and its six uses. Do not remove type compatibility, managed-link write restrictions, persistent-field admission, or borrow checking. Containers own counted elements regardless of whether an element expression is a borrowed participant observation. Existing IR Transfer retains borrowed counted handles before moving them to the destination; there is no new borrowed-container type or accounting system.

Production AoS handles are nominal record pointers into fixed 64-record chunks. Directory growth cannot move records. Free-list links and counted reference counts reside in records. Production SoA handles are uint32_t, with zero reserved for null/empty, separate field arrays, uint32_t free links, and uint64_t counts. The high-water cursor remains uint64_t; check >UINT32_MAX before narrowing, reserve slot storage before publishing the cursor, and reuse free slots before allocating new ones. Debug remains uint64_t monotonic identities, identity-to-slot mapping, UINT64_MAX empty sentinel, and state/count diagnostics. HandleType, EmptyHandle, Metadata, and FieldStorage centralize these physical choices without changing semantic types or IR ownership. Update saved receivers, nullable values, membership holders, counted-array strides/copy/drop and declarations consistently.

Retain checks count overflow before increment. Release empties its holder, decrements, and reinstates a one-count destruction hold only when reaching zero. Teardown may allocate, so no saved metadata pointer is dereferenced after the call. Reclamation resolves metadata anew. This protects balanced temporary ownership of the active teardown receiver, not arbitrary other objects.

## Required enforcement map

Locations below are candidate source lines at the frozen verification version. Functional paths are described rather than treated as filename-only evidence.

| Obligation | Enforcement and why | Public evidence / limits |
|---|---|---|
| Physical initialization and typed persistent storage | Semantics/Check.cs 475–594 walks physical Fields(), including labeled contributions and nested aggregate types; ordinary-object fields require maintained participant relationships, counted targets end the inline walk. 795–838 initializes only nullable managed links, checks fallthrough construction; 4794–4922 recognizes exact constructing self, checks each physical field before receiver escape/read. 4108–4157 intersects definite assignments only across continuing branches. Required-field cleanup sentinels are not source initialization. | ch08/check.py; ch13/object_fields_migration.py; ch14/safety_initialization.py (9 focused passes). Mandatory-parent constructions explicitly rejected, not implemented. |
| Nullable and initialized-length access | CheckField rejects nullable access without unwrap. Emission/Compilation.cs 590–605 emits safe index test against length before pointer formation; 760–772 tests the layout-specific sentinel on unwrap. 1954–2025 resize refuses growth without a default, drops removed elements, initializes before length publication. Reserve never publishes initialized elements. | ch07/run.py, ch12/nullable_handles.py, ch14/safety_access.py (4 focused passes at O0/O2). Deliberately invalid unsafe accesses are not executed. |
| Allocation arithmetic and failure | Emission/Compilation.cs 188–274 checks count against SIZE_MAX/stride before multiplication; retains original realloc pointer until success; geometric growth bounded by remaining capacity. 1177–1358 ClassStorage checks cursor limits before increment/narrowing, allocates before cursor publication; free-list reuse precedes exhaustion check. 1958–1973 checks push length increment. | ch14/allocation.py 8 focused comparisons and handles.py 2 focused layout contracts, including bounded SoA limit/reuse. Does not physically exhaust all 2^32 slots or prove every OS allocation failure behavior. |
| Ordered interprocedural effects and alias stability | Check.cs 844–889 computes monotone effects, ordered obligations, replacements and initializer result graphs to a fixed point; 1044 onward builds temporary event graph with saved alias snapshots and separate writable slots; RequireSafe at 2156–2187 checks mutation-before-use path overlap. 3100–3207 substitutes actual argument storage into callee obligations, batches replacements after snapshotting sources, and does not assume unresolved recursive calls return. Formal component paths represent nested contained aliases, not names. | ch08 lifetime checks, ch10/borrowed_structs.py, ch14/safety_calls.py 18 focused passes including last-use controls, direct/forwarded/generic calls, recursive descent/unwinding, overlap and nested aliases. Conservative class effects are not precise runtime identity tracking. |
| Evaluation, branches, loops, saved receivers | Checker snapshots each argument before subsequent effects (Check.cs ~2970–3100), checks entry validity and constructor allocation effects. Loop graph (~3690–3750) routes cleanup through back edges/break/continue; return cleanup precedes exit. IR/Lowering.cs lowers operands into named ordered nodes; emitter SelectField saves the handle and SelectIndex fixes the pointer at selection. Storage-changing effects must be rejected statically before unsafe stale use. | ch08 lifetime, ch09 IR/C emission, ch10 borrowed structs, ch14 safety_calls controls. PEG acceptance is not evidence for evaluation order. |
| Copies, transfer, replacement, cleanup exits | IR/Lowering.cs 363–383 Transfer inserts Ref for a borrowed counted value before Move; Move empties its source. 407–421 Cleanup visits holders in reverse declaration order, including early exits and condition scopes (1339–1377). Existing aggregate copies recursively own nested contents. ValidateStructure/Operations/Execution run before and after ownership simplification (108–114). | ch09 ownership and C emission, ch13/participant_ir.py, ch14/aggregate_participants.py (focused 16 executions/2 refusals; cumulative four runtime/layout modes per compiler configuration passed). Finalization output constrains lifetime, not every intermediate count. |
| Membership and complete destruction | IR/Lowering.cs 1148–1205 gives refs membership its own count: acquire after preparation before link publication, release after structural removal. Destruction 145–220 runs final hook, all child-role fragments, parent-role fragments, owned fields, reclamation, in that order. Managed links are not ordinary owning fields. Emission 556–581 reselects metadata after hooks; 1381–1452 retains the destruction hold. Check.cs 889–1027 closes generated/final effects and rejects transitive same-class final destruction (LYC334); exact teardown receiver protection is separate from children. | ch13 relationships/participant/debug-identity suites; ch14 safety_initialization nested counted finalizer cycle and ordinary-container finalization cases. Trusted relationship algorithms remain a stated assumption. |
| Unsupported call/effect boundaries | Check.cs 795–811 requires a body; ReadType supports only admitted type forms and otherwise Unsupported emits LYC306 (4213–4216). Function-valued parameter types and missing bodies are rejected; known direct and specialized bodies enter the same summary engine. | ch14/safety_boundaries.py 3 focused passes: direct body success, exact FunctionType and Function refusals. No callback/opaque-call safety claim. |

## Structural self-review

Common/Model.cs, Common/IR.cs and Common/MultiClassInstantiation.cs declare persistent state/relationships and mandatory partial operations; implementations remain in functional directories in one assembly. This increment introduces no persistent state, dependency wiring, interfaces, callbacks, or duplicate semantic authority. Emitter-local lists/names and checker flow graphs are temporary algorithmic data; diagnostics/literals and canonical value vocabulary have explicit value roles.

Inspected IR/Model.cs 63–235, 335–387: program owns functions, functions own nodes, nodes own operands. Tree and definition-use links are weak: logical destruction detaches both directions rather than deleting shared definitions. IrFunction/IrNode/IrOperand constructors unwind partially attached ownership on failure. Parent operations validate before mutation and update collection and backlink before returning.

Inspected Semantics/MultiClassInstantiation.cs 8–280: constructor validates concrete participants and policy/arity/labels, registers with analysis/template, rolls back through Destroy on failure. AddParticipant reserves capacity and attaches the concrete-class reverse membership and instantiation membership; RemoveParticipant clears both. Destroy marks before cascade, retires contributions/scopes/nodes/participants, then detaches template/analysis. Endpoint destruction redirects to the owning instantiation, preventing partially valid bindings. No asynchronous owned work added.

No structural defect requiring code change found in this bounded review. The inherited checker is large; inspection and regressions do not constitute formal verification of every possible program.

## Teaching/setup lessons

- Destination ownership, not source participant spelling, governs counted array/tuple elements. Preserve Transfer instead of inventing borrowed containers.
- Production physical representation and debug diagnostics are separate choices; changing allocation alone misses saved handles, null sentinels and recursive container strides.
- The older Chapter13 future-work text conflicts with the explicitly newer Chapter14 owning-container contract; followed Chapter14 and user direction. Mandatory relationships remain unsupported.
- First PEG attempt (job4226, receipts/peg-base-premature.log) ran before Make reached its Release build: grammar/control compilation succeeded, lexer failed because candidate Release DLL did not exist. This is a setup-order failure, not syntax or implementation evidence. Wait for Release prerequisite; never redirect to a parent assembly.
- One stale explanatory comment in ClassStorage describes UINT64_MAX universally; it applies only to debug now. Corrected after frozen executable verification, with a comment-only diff retained; no executable lines changed.

## Evidence status

Focused Debug checks all passed (job4217); restore and Debug build exited0, zero warnings/errors. Full unchanged Make cumulative job4219 completed exit0: 48 runner invocations in Debug and 48 in Release plus comparison self-check. PEG job4241 completed all six suites (243/243 fixtures, 24 malformed-grammar controls); Spin14 passed four runtime/layout modes and its compile-only LYC326 rejection. Frozen integrity verified public317/compiler44; final public317 verified after two comment-only corrections. See final-result.md and receipts/numerical-inventory.md for terminal completion and numerical inventory. Human acceptance pending; no installation/publication authorized.

Diagnostic-script lesson: the first final compiler-integrity attempt used unresolved Path('.').parent and failed to find the manifest; a subsequent shell command masked that Python failure. The corrected fail-fast script used Path.cwd() and verified all44 hashes. No integrity pass was claimed for the failed check.
