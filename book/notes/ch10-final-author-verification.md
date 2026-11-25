# Original Chapter 10 final author verification

Job 904 completed with exit 0. The actual cumulative `make test` ran Debug and Release using the installed .NET 10 SDK; a temporary wrapper added `--no-restore` only to build commands. Compiler, graders and assemblies remained frozen throughout. Canonical corruption selfcheck ran separately before the Make target. No compiler edits were made during this completion pass.

This is reference-implementation behavioral evidence, not independent reconstruction, exhaustive lifetime proof, or human acceptance. Historical model probes and walkthrough receipts remain in ch10-development.md; they were not freshly replayed by this run.

```text
PASS canonical inspection selfcheck (4 defects)
Grader comparison self-check passed
[?1h=  Lyric -> /Users/bill/projects/lyric-next/compiler/bin/Debug/net10.0/Lyric.dll
Build succeeded.
[39;49m    0 Warning(s)
[39;49m    0 Error(s)
82 passed; 0 failed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=PASS: 99826 NFC vectors; XID classification for all 1112064 Unicode scalars; 7 malformed requests
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=58/58 token cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=70/70 syntax trees passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=155/155 syntax recognition cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=7/7 parser boundary cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=82/82 semantic cases passed
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
5/5 canonical cases passed; selfcheck rejected 4 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
[?1h=  Lyric -> /Users/bill/projects/lyric-next/compiler/bin/Release/net10.0/Lyric.dll
Build succeeded.
[39;49m    0 Warning(s)
[39;49m    0 Error(s)
82 passed; 0 failed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=PASS: 99826 NFC vectors; XID classification for all 1112064 Unicode scalars; 7 malformed requests
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=58/58 token cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=70/70 syntax trees passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=155/155 syntax recognition cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=7/7 parser boundary cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=82/82 semantic cases passed
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
5/5 canonical cases passed; selfcheck rejected 4 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
```

## Sole code review and bounded grader correction

The completed consolidated review is retained in ch10-code-review.md. Reviewer original-ch10-code-review-default submitted and was shut down; no re-review requested. No established compiler defect was found. The required correction concerned the already-published inspection schema, not a new language requirement.

Author baseline job1007 confirmed acceptance of missing componentTypeIds and a consistently linked string ID. A null component list instead raised TypeError during traversal; it was not accepted by the complete validator. The shell receipt was zero because a following README read succeeded, not because that probe completed.

canonical.py now requires list-valued componentTypeIds and exact integer IDs/nullable links (excluding Boolean values) before graph lookup. The selfcheck retains all four original corruptions and adds fourteen malformed-schema controls. Job1008 passed 18/18 selfcheck defects. Job1009 terminal0 passed all five canonical cases and rejected all18 defects in BOTH Debug and Release against unchanged author assemblies. No compiler edit or full-suite rerun was needed. The earlier complete job904 evidence remains unchanged and historical relative to this grader-only correction.

Chapter text now explains that normal canonical grader invocations also run the selfcheck. Optional numeric-event refactoring was not adopted during closure: no defect justifies touching the lifetime engine here. Lesson: identity consistency does not establish serialized schema validity. Human acceptance and student reconstruction remain pending.

## Text review completed; isolated student admitted

Sole text review join1048 completed; full 10,789-character report saved/readback in book/notes/ch10-text-review.md. Reviewer original-ch10-text-review shut down. Three bounded prose corrections incorporated: declared defaults validate even when overridden; external grader cumulative command supplies GRADERS; status distinguishes full904/original4 corruption controls from focused1009/18 controls and completed reviews. No compiler or grader change in this slice, no re-review. Guarded publication1061 passed current18types39relationships/fourviews, readback14 outputs, all34 figure files unchanged, whitespace clean.

Public bundle prepared job1062:105 files byte-verified with input-manifest.json; all chapter-local links resolve. Includes chapters00–10/preface, public spec/grammar, figures, graders1–10, Unicode data/license/checksums, public spin01–09 and Makefile. Excludes compiler target, Git/private notes/builds/reference generators/caches. Location Cadence ch10-student-inputs/public; prerequisite accepted-ch09 remains separate35-file accepted6073e13 export.

Fresh confined original-ch10-student admitted job1064; ONLY accepted-ch09 and public input subtrees plus installed SDK/system toolchain imports. Student alone owns candidate/compiler and all writes in child directory. Parent may clarify teaching/correct graders, never patch student. Required retained1–10 both configs, canonical selfcheck, structural self-review and Chapter10 walkthrough; stop at100% applicable pass, human acceptance pending. No commits/pushes/install/restart/Ch11. Next verify student skill/setup activation, answer questions, observe healthy work without duplicate workers/tests.

## Original Chapter 10 autonomous completion

Student original-ch10-student submitted on join1532; final unchanged cumulative Make job1533 terminal exit0, Debug and Release each zero build warnings/errors. Saved log inspected: both configuration totals present through20/20 contained executions and23/23 intended rejections.25 public runners per configuration (not earlier spoken26);106 public files hash-match per retained integrity receipt. Full copied receipt: book/notes/ch10-student-acceptance.md. Same student attempt, two authorized per-turn limit continuations; setup and existing-contract clarifications recorded; no parent candidate patches or grader edits during reconstruction. Student shut down; no rerun after passing. Candidate remains in Cadence child workspace, not merged into reference.

Final lessons integrated at their construction points in chapter10: exact identity versus permitted scalar conversion; table descriptors versus array payloads, saved object indices versus pointers, destruction and diagnostic ordering. These were student implementation regressions, caught by retained graders, not a changed language contract. Walkthrough and structural probes passed with limits in receipt; human walkthrough acceptance remains pending. Guarded publisher1566 passed18types39relationships/fourviews,14output readbacks, all34 figures unchanged, git diff --check clean. No compiler changes during author closure. No commits/pushes/installations/restarts/Chapter11. Completion pending human acceptance and commit permission; no further review or student campaign authorized.

## Human acceptance — Chapters 9 and 10 fully approved

Bill reports running `make spin09` and `make spin10` and explicitly approves both chapters in full. This completes human walkthrough acceptance, superseding earlier pending-acceptance status. Chapter 9 approval is reaffirmed; original Chapter 10 is accepted. No further reviewer/student campaign or rerun is required. Commit permission remains separate under the existing explicit no-commit boundary; no Chapter 11 work, push, installation or restart is authorized by this acceptance.

## Accepted chapter commit scope

Bill fully approved Chapters 9 and 10 after both Make walkthroughs, then authorized committing all work. This local commit packages the original Chapter 10 reference, public specification/graders, diagrams/tooling, review and acceptance records, and spin10 shortcut. Earlier no-commit statements describe historical boundaries now superseded by this authorization. Unrelated arith.ly, hello.ly and the editor swap file remain excluded and untouched. No push, installation, restart or Chapter 11 work is authorized. Final spin09 passed; spin10 initially failed due to Python string escaping, then its raw-source correction matched the chapter example exactly and passed. No compiler change or student rerun resulted from that script fix.
