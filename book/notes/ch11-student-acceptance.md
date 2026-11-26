# Chapter 11 reconstruction acceptance receipt

Parent inspected the complete student report and saved terminal exit receipts in job2168: cumulative2141, PEG2147, walkthrough2163 and integrity2164. All applicable checks passed; no post-pass rerun. Student submitted on join2070 and was shut down. Candidate remains separate from reference. Setup assistance is recorded below. Bill subsequently read and accepted the chapter, reported that `spin11` passes, and authorized a scoped local Chapter 11 commit. No physical-print check is claimed. The following is the student report, with its initial pending-status sentence superseded by its final receipts.

---

# Chapter 11 isolated reconstruction

Candidate: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/ch11-student/candidate`

## Scope and provenance
Reconstructed Chapter 11 only from the accepted Chapter 10 compiler export and authorized public teaching/test materials. Same student throughout; no target Chapter 11 solution, private history, agents, reviewers, package downloads, commits, deployment, or later chapters. Public authority is unchanged. `receipts/input-sha256.json` records the 165 original copies (37 compiler files plus 128 public files, including the manifest of 127 payload entries). `receipts/frozen-compiler-sha256.json` records 39 frozen compiler files. The PEG dependency link points only at the authorized node_modules subtree. Public grammar already supported this syntax and was not modified.

## Construction and structural self-review
Common declares specialization state and callable operations; functional partial implementations retain the existing compiler architecture. A specialization belongs to analysis and its template, owns independent copied syntax, concrete callable and scopes, and records one binding occurrence per type argument. Canonical types remain shared, with backlinks to each occurrence, including repeated uses of one type. Constructors validate before relationship mutation. Concrete callables remain in scope membership but are excluded from ordinary lookup, so they do not shadow the template.

Signature completion distinguishes active/completed signatures and rejects cycles. Generic templates do not receive concrete body facts. Calls either bind explicit types or check arguments once left-to-right and structurally infer canonical types. Growing signature/body work lists include specializations found in field defaults. The query path uses the ordinary checker while restoring assignment state; it suppresses uninitialized-read errors only, not ordinary name/type errors. `matchtype` validates patterns, records the selected index on the semantic fact, and checks only that arm's lexical body. Borrow analysis follows that selected block. Lowering splices the selected block into the current region without a runtime conditional, while preserving local cleanup and the outgoing stable holder. Emission enumerates actual IR source functions rather than parsed templates. There are no fixture-specific output substitutions.

Retirement clears dependent IR and marks the specialization destroyed before cascading, preventing reentrant double destruction. Direct destruction of a concrete callable, copied node, scope or binding retires its specialization. Template/analysis/source destruction cascades; bound-type destruction retires dependent specializations. Ordinary specialization retirement detaches but does not destroy shared canonical types or unrelated specializations. Parsed-source edits retain normal analysis invalidation; derived syntax follows the specialization-specific registration path and is immutable after callable publication. `SemanticType.IsUnlinked` was extended to include generic binding dependents.

Local diagnostic `local/model-probe/Probe.csproj` compiles the real compiler sources; reflection inspects relationship collections for assertions only. It tests duplicate/foreign keys, parsed-root adoption and callable-parent refusal without harming a live specialization, repeated argument occurrences, hidden concrete lookup, and nine destruction entry points (specialization/callable/copied node/scope/binding/type/template/source/compilation). Terminal job2140 exit0: 9/9 scenarios; `receipts/model-probe.log`. This is diagnostic evidence, not an independent public retained grader.

## Failures, corrections, and lessons
- Signature refactoring initially duplicated a local variable; corrected the collision. New binding constructors initially omitted required declaration/mutability arguments; corrected to the actual API. Final targeted builds had zero warnings/errors.
- Initial generic executions exposed missing generic-callee lowering and parsed-template-only emission. Corrected both inventories/guards; inference diagnostic codes were aligned with the chapter's count/shape contracts.
- Direct Clang initially lacked system headers. Parent supplied authorized `SDKROOT=/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk`. Local standalone stdio probe printed `sdk-ok`, exit0 (`receipts/sdk-probe.log`, terminal2110).
- Initial selected-block IR created a transient Move with multiple consumers: 3 matchtype executions failed validation. Preserving the existing outgoing holder instead passed the runner. Lesson: compile-time selection removes runtime branching, not lexical lifetime/cleanup; a stable holder and a transient ownership transfer have different contracts.
- SDK first-run output reported installing a development certificate despite local CLI home and skip-first-time. No host effect was independently verified, and no cleanup was attempted. Parent authorized continuation with `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`. Lesson: skip-first-time alone does not suppress this SDK behavior.
- The final Make invocation's Apple wrapper attempted an external xcrun cache write, denied by the sandbox; the recipe continued. This is a setup warning, not a compiler test assertion. No successful outside write is claimed.
- A partial-run wrapper exited zero after printing a failing runner status. The report uses each runner's recorded result, not that wrapper's exit; subsequent command wrappers propagate failures.

## Earlier targeted evidence (not a substitute for final campaign)
`generics-second.log`: 17/17 execution, 10/10 rejection.
`typeof-second.log`: 17/17 execution, 8/8 rejection.
`matchtype-third.log`: 18/18 execution, 13/13 rejection, 1/1 IR selection, 3/3 corruption controls; terminal2130 exit0.

## Teaching feedback
The chapter's distinction between declaration and implementation name scopes, its separate signature/body discovery work lists, and its warning about emission's independent function inventory were necessary construction guidance. Syntax copies prevent specialization fact contamination, but their benefit depends on explicit co-ownership and complete retirement. Keep the stable-holder/Move distinction explicit near selected-arm cleanup teaching. A direct SDK/Clang/Make offline recipe is useful setup guidance; SDK and Apple-wrapper side effects are not language defects. No public grader was changed or weakened.

## Final campaign
Pending terminal results at this report's initial write. Human acceptance remains pending; no installation or publication is implied.

### PEG terminal receipt
Job2147 exit0 ran four independent processes, each exit0, from candidate with the local environment and installed `/opt/homebrew/bin/node`:
- `node tools/peg/test.mjs`: 50/50 (`receipts/peg-base.log`, `.exit`).
- `node tools/peg/test.mjs graders/ch03/cases.json`: 155/155 (`receipts/peg-ch03.log`, `.exit`).
- `node tools/peg/test.mjs graders/ch11/type-syntax.json`: 5/5 (`receipts/peg-type.log`, `.exit`).
- `node tools/peg/test.mjs graders/ch11/matchtype-syntax.json`: 11/11 (`receipts/peg-matchtype.log`, `.exit`).
Parent approved direct `/Library/Developer/CommandLineTools/usr/bin/make` for the remaining walkthrough and requested retaining, not restarting, healthy Make job2141 despite its denied wrapper-cache attempt.

### Final cumulative and walkthrough receipts
Job2141 terminal exit0: `. ./env.sh; cd candidate; make DOTNET="$DOTNET" PYTHON="$PYTHON" test`. Full retained Chapters 1–11 in Debug and Release, stop-on-failure. `receipts/make-test.log` and `make-test.exit`; comparison self-check passed. Both builds succeeded with zero warnings/errors. The denied Apple Make-wrapper cache warning remains in the log.

The following exact summaries occur in **each** configuration (not combined totals):

```text
82 passed; 0 failed
PASS: 99826 NFC vectors; XID classification for all 1112064 Unicode scalars; 7 malformed requests
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
```

Job2163 terminal exit0: `. ./env.sh; cd candidate; /Library/Developer/CommandLineTools/usr/bin/make DOTNET="$DOTNET" PYTHON="$PYTHON" spin11`. `receipts/spin11.stdout`, `.stderr` (empty), `.exit` (0). The public walkthrough verifies generated program exit0, exact stdout below, and empty program stderr; generated temporary files were removed by the harness.

```text
generic array copy stays independent
type query did not execute
singleton tuple
array
other type
```

Final hash verification: Public unchanged: 128/128 (127 manifest payloads plus manifest). Compiler unchanged since freeze: 39/39. See `receipts/final-integrity.log`. No implementation or public-input edits occurred during the final campaign. All applicable checks passed; no further reruns were launched. Human acceptance remains pending.
