# Chapter 7 independent reconstruction — complete

## Candidate and provenance

Workspace: `work/`; implementation: `work/compiler/`.
Only prerequisite: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch07-student-input`.
Input manifest SHA-256: `b26066801b5963ca6166a574377ed26ad97d4b979740abb98560ce3d662704a8`.
All 232 frozen input files verified unchanged at completion. No grader, fixture, expected output, or teaching file was edited.

Changed files are listed in `evidence/changed-files.txt` (six implementation files). Final source hashes: `evidence/candidate-source.sha256` (27 files). Debug/Release artifact hashes: `evidence/candidate-artifacts.sha256` (10 files). Paths in hash manifests are relative to `work/`.

## Implementation

Common declares immutable scalar-kind/array-depth types, reference and builtin contracts, and the mandatory semantic operation. The checker records resolved facts for contextual arrays, exact array/reference types, writable selection and checked methods. C emission implements recursive deep copy/drop, checked growth, one-time indexed selection, moves and scope/transfer cleanup. Nonowning reference pointers never own cleanup.

`Semantics/Borrows.cs` implements temporary event graphs, acquisition-sensitive invalidation propagation and helper summaries converging to a fixed point. Formal-relative mutation-before-use obligations are checked against caller paths before possible helper mutations are applied. Reference argument selection remains live across later argument effects. Loop targets exist before traversing conditions; short-circuit expression spines use staged iterative traversal. No additional persistent relationship graph or runtime service wrapper was added.

## Required verification

Commands run from `work/` after `. ../setup-env.sh`:

- `make test DOTNET="$DOTNET" GRADERS="$GRADERS"`: exit 0, Debug and Release, authoritative external readonly graders. `evidence/make-test-final.log` and `.exit`.
- `"$NODE" tools/peg/test.mjs "$GRADERS/ch03/cases.json"`: exit 0, 143/143 plus grammar negative controls. `evidence/peg143.log` and `.exit`.
- `make spin07 DOTNET="$DOTNET" GRADERS="$GRADERS"`: exit 0, fresh Release build, copy independence, fresh reference acceptance and expected stale-reference LYC326. `evidence/spin07.log` and `.exit`.
- Structural self-review: `evidence/structural-review.md`.

Each configuration: Chapter 1 82; Unicode 99,826 NFC vectors, 1,112,064 scalar XID classifications and 7 malformed requests; tokens 58; syntax trees 66; syntax recognition 143; parser boundaries 7; semantic cases 79; Chapter 5 execution 75; Chapter 6 execution 250; Chapter 7 execution 102. All passed. Both builds had zero warnings/errors. Grader comparison self-check passed.

## Failures, corrections, and assistance

Exact failed execution logs retained: `evidence/ch07-first.log` (84/102 before borrow analysis) and `evidence/make-test.log` (Chapter 6 249/250: loop condition break reached an unregistered loop target). Borrow analysis resolved the first; registering loop targets before condition traversal resolved the second. Structural review also corrected recursive short-circuit traversal before the final run. Intermediate builds and diagnostics are retained in evidence.

`evidence/assistance-and-teaching.md` records lessons, feedback and limits. No implementation assistance, target-solution inspection, grader correction or contract change was requested. Environment/tool setup was supplied in the assignment. A status call was refused for lacking a fresh commentary preface and then successfully retried; it had no implementation effect.

## Limits and stop boundary

This is behavioral and structural evidence, not sanitizer, injected allocation failure, allocator-balance, cross-platform or human acceptance evidence. No network, installation, deployment, publication, reviewer, or next-chapter work occurred. Human acceptance remains separate. Required execution is complete; no passing execution was rerun for report polish.

## Orchestrator verification

Read the full consolidated report, assistance notes, structural self-review, PEG143 and spin07 logs, cumulative suite summaries and zero exit receipts. Verified 27 source hashes, 10 artifact hashes and all 232 frozen input hashes. An initial verification script used the wrong manifest base directory; corrected to the documented work/ directory and all hashes passed. Candidate was not rerun. Student shutdown reported stopped=true. No implementation assistance or grader corrections occurred. Human use is separate.

Integrated the student lesson at the loop graph construction point: register header/exit targets before condition traversal. Frozen teaching and passing candidate remain unchanged.
