# Chapter 4 development — names and types

## Authorized increment

Bill approved proceeding with semantic analysis after Chapter 3 reconstruction. Chapter 3 human acceptance remains separate; no Chapter 4 success, commit, or push is claimed. Preserve all uncommitted Chapter 3 work.

Build scope/symbol resolution, primitive/byte-string typing, ordinary functions and calls, bindings and assignment, return checking, and located diagnostics. Parsed but semantically unsupported constructs must be explicitly rejected. No C emission, Lyric execution, object-lifetime safety certification, or full generic semantics is implied.

## Agreed language decisions

- Automatically expose the standard library’s public exports through unqualified global lookup. No std accessor/import. Ordinary no-shadowing applies; private exports remain private; one declaration identity per symbol.
- Initial executable entry: one top-level nongeneric main, no parameters, unit result; normal completion exits zero. Library checking does not require main.
- Initial print/println: one byte-string argument, unit result; raw bytes, println adds LF; output failure is diagnostic/nonzero fatal termination. Recoverable Writer APIs remain distinct.

These contracts are integrated into the canonical spec. Implementing only the supported library increment does not silently narrow the intended automatic-import rule.

## Construction plan

Design shared semantic vocabulary in Common before algorithms. Scopes own symbols; resolved uses maintain symbol backlinks. Syntax associations also need reverse relationships. Primitive type descriptions are explicitly value-like, and string must remain the same type as [u8]. Compilation owns one semantic analysis across its sources, avoiding duplicate global scopes and standard-library declarations. Replacing source syntax destroys that analysis first. Semantic cleanup precedes syntax invalidation. Independently authored Python graders grow alongside implementation; they inspect the real semantic component and retain all earlier checks.

Record consequential choices, failures, verification, and teaching lessons here as construction proceeds. Full teaching and one code/text review plus a fresh student follow verified development, not precede it.

## First verified type increment

Common/SemanticTypes.cs declares the value-like enum and shared operation signatures; Semantics/SemanticTypes.cs implements exact integer ranges, identity/widening compatibility, and arithmetic operand-type selection. ByteArray is the single identity for string/[u8]. BigInteger represents all integer endpoints without host overflow. No semantic driver mode or checking algorithm exists yet.

Job353: Debug build succeeded, zero warnings/errors. Job354: disposable C# emitter invoked the actual operations; independent Python calculation checked 169 type pairs and eight integer-boundary records, all 177 passed. This is supplementary construction evidence, not a retained public source-level grader, and does not establish literal inference, constant evaluation, or whole-program checking. No earlier regression suite was rerun for this unconnected type helper yet.

Lesson: contextual literal typing is distinct from conversion of an already-typed value. Another design correction before implementation: semantic analysis belongs to Compilation, not SourceFile, because a package spans files and must have one global environment. The relationship model is the next implementation step.

## Compilation analysis and lexical-scope lifetime foundation

Added Common declarations and Semantics implementations for compilation-owned
SemanticAnalysis and analysis-owned SemanticScope. Scope Parent/Children links
are weak lexical structure, maintained at both ends; analysis owns all scopes,
including unfinished ones. Constructor failure after first registration retires
that membership. Cycles and cross-analysis attachments refuse without mutation.
Compilation source changes and syntax-node creation/edge changes/destruction
invalidate analysis, before stale syntax relationships could be observed. This
is conservative whole-compilation invalidation, not incremental analysis.

Debug build passed with zero warnings/errors (job 360). Disposable probe against
actual classes passed 25 analysis/scope lifecycle checks (job 361), extending the
15 analysis-only checks from job 359. Scope cascade, weak detachment, rejected
cycles/cross-analysis construction, and mutation-free refusal were exercised.
No semantic checking or source-level Chapter 4 grader exists yet. A follow-up
`trees.py --help` invocation failed because that script accepts a compiler command,
not argparse options; it attempted to launch `--help`. Not a compiler failure.

## Symbols and resolved facts

Added Common declarations and Semantics implementations for scope-owned symbols,
weak syntax declaration links, and analysis-owned expression facts. A fact links
back to its syntax node and optionally its resolved symbol; the node has one fact
and the symbol maintains its use collection. Scope lookup walks lexical parents,
not siblings. Insertion refuses a currently visible duplicate; the checker must
still register top-level declarations before bodies and locals in statement order.

Symbol destruction clears its declaration and destroys dependent facts. Analysis
cleanup destroys facts before scopes/symbols. Syntax replacement already clears the
compilation analysis. Constructors unwind completed relationships on refusal;
none of these backlinks is delegated to GC. Primitive types remain value-like.

Evidence: job 362 cumulative Debug/Release suite passed before symbol/fact changes
(saved /tmp/lyric-ch04-foundation-regressions.txt). Symbol build 367 succeeded;
fact build 370 reports zero warnings/errors. Disposable real-class probe progressed
from 25 to 41 then 54 checks, job 371 exit 0, including lookup, sibling reuse,
mutation-free duplicate refusal, cross-compilation refusal, constructor rollback,
symbol-use invalidation and source/analysis destruction. This is supplementary
model evidence, not an independent retained source grader or completed checker.
Probe: /tmp/lyric-ch04-lifetime.7ehmJu; summary /tmp/lyric-ch04-fact-checks.txt.
Next: public source-level semantic contract/graders and actual hello-world checking.


## First source-level semantic milestone

Connected Compilation.Check(executable) and driver --check / --check-library.
Success is silent exit 0, located errors exit 1 without stdout. Public initial
contract and 16 retained cases are in graders/ch04; make test includes them.
All top-level function names are collected before signatures/bodies. Builtin
print/println are ordinary function declarations. Types have ordinary symbols;
string and [u8] map to the same value-like type. Function scopes strongly owned
by function symbols carry ordered parameter symbols, with complete backlinks;
analysis also owns scopes. No copied parameter-type array. Constructor validates
before mutation and unwinds completed memberships on failure. Function and scope
destruction each clear both directions; parameter symbols cascade normally.

Call facts point to resolved functions. Bare callee nodes deliberately do not get
fake result-typed value facts. Parameter-use facts point to their parameter symbols.
Unknown names, duplicate names, count/type mismatches, invalid entry signatures and
unsupported constructs are located diagnostics. Library checking omits main.
Diagnostic positions derive from original bytes, including scalar columns/CRLF.
First grader run was 0/13 because the CLI mode did not yet exist, then 13/13;
exact location checks and library/Unicode/CRLF cases expanded it to 16/16.

Evidence: build 380 zero warnings/errors; job 382 cumulative make test exit 0,
Debug and Release all retained suites including 16/16 new cases. Full log:
book/notes/ch04-checks/first-source-checks.txt. Supplementary real-model probe
passes 62 checks including function-scope destruction/refusal. Its first expansion
failed to compile due to a duplicate local variable in the probe; renamed it and
reran, not a product failure. Earlier pending build 370 collected exit 0.
No review/student/prose completion/commit yet. Current body support is only unit
functions, calls, parameter values, strings/bools/unit/groups and bare return;
unsupported features explicitly reject. Next: typed local bindings, arithmetic,
nonunit returns and definite assignment. Spec permits uninitialized let with
one-time initialization: distinguish definite assignment (read safety) from
possible assignment (immutable reassignment), with eventual intersection/union
at branch joins. No checker implementation of those flow rules yet.

## Straight-line locals, returns, and integer literals

Implemented local symbol registration after initializer checking; inferred and
annotated bindings, plain assignment, initialized immutable parameters, and
explicit value returns. Temporary definite/possible assignment sets distinguish
reads from one-time let initialization. Branches/loops remain unsupported; their
join rules are not implemented by the straight-line pass. New LYC307–310 errors
cover unassigned reads, repeated immutable assignment, missing nonunit returns,
and literal range failures. Direct signed integer literals use BigInteger before
range checks; contextual literals do not authorize narrowing typed variables.

Independent cases grew from 16 to 29 (baseline 17 passed), then 42 (baseline 29
passed). Debug clean build and 42/42 passed in job395. Bare return initially
reported byte zero because its structural syntax node has no anchor. Corrected
the checker and explicit public expectation to locate the containing function;
no invented keyword span or parser change. Earlier 28/29 was a real location
failure, followed by 29/29. Whitespace check passed before integer additions;
full cumulative verification of this slice is being run separately. No execution,
binary arithmetic, constant-operation overflow, branch joins, or codegen proof.

Cumulative job396 completed exit 0: Debug and Release clean builds, all retained
Chapter 1–3 suites and 42/42 semantic cases each; grader comparison self-check
and final whitespace check passed. Full log: ch04-checks/locals-and-integers.txt.
Next: retain an optional BigInteger constant on SemanticFact (value-like, no new
relationship), then check arithmetic types and statically known overflow. This
model change is explained but not yet implemented.

## Neighbor and return context for arithmetic

Bill settled the previously unstated interaction between contextual literals and
binary operations: untyped integers take their type from neighboring operands or
the return type. Recorded neighbor-first, surrounding-expectation-second, i32-last
in the numeric specification, consistently extending existing binding/argument
contexts. Typed operations retain their width despite a wider destination.

Implemented checked integer addition/subtraction/multiplication. A read-only type
hint walk discovers operand context without creating facts or evaluating a right
operand early; actual checking remains left-to-right. SemanticFact now retains an
optional exact BigInteger constant (value-like, no backlinks) so nested constant
overflow is diagnosed at its offending operation. Unknown constants remain null.
No constant propagation through variable values or code generation is claimed.

New baseline: 42/56 passed, with all 14 new arithmetic cases correctly failing at
the old unsupported boundary (job407). Implementation built without warnings and
passed56/56 Debug (job408). Added three controls for default i32, default overflow,
and neighbor priority over a wider return. Cumulative run pending job410, log
ch04-checks/arithmetic-context.txt. General unary/wrapping/division/comparison and
control-flow checking remain unfinished.

Cumulative arithmetic verification completed: job410 exit0, both configurations
zero warnings/errors,59/59 semantics and all retained suites. Job414 extracted
unclipped build/source/token/tree/recognition/boundary/semantic summaries from the
saved log. Grader comparison self-check passed. Whitespace check passed. No code
execution/C emission is implied. Next: branch scopes and definite/possible
assignment joins, with public source cases before implementation.

## Statement branches and assignment joins

Implemented statement-position if/else/else-if in Semantics/Check.cs. Every
block gets an analysis-owned lexical scope; scope parent links remain weak.
Local flow entries expire at block exit without destroying persistent symbols
or facts. Both alternatives begin with the same incoming definite/possible
assignment sets. Continuing paths intersect definite assignment and union
possible assignment; a returning path contributes neither. Missing else is an
unchanged continuing path. A boolean block result means control can continue,
replacing the unsound-for-branches test that merely a return occurred somewhere.

Conditions must be bool. Both alternatives are checked, even for literal
conditions. Value-producing if, loops, and unreachable statements explicitly
reject as unsupported for this increment, not as new language prohibitions.
Semicolon discard markers after returns remain harmless. The earlier return
location policy remains: an unanchored bare return mismatch names its function.

Added 18 public source cases: two-arm initialization, missing-arm read/reassignment
failure, both return directions, returning assignment excluded from join,
complete/incomplete returns, sibling reuse, local escape/shadow refusal, wrong
condition type, nested joins, else-if, branch isolation, mutable reassignment,
and the two unsupported boundaries. Initial 12-case addition produced 59/71
against the previous compiler (job420 exit1); implementation clean build and
71/71 (421 exit0). All 77 final cases pass.

Cumulative job422 exit0: Debug and Release each clean builds, source82,
NFC99826/XID1112064/malformed7, tokens58, trees66, recognition143, boundaries7,
semantics77, comparison selfcheck, and whitespace check. Saved
ch04-checks/branch-flow.txt. A subsequent comment correction and equivalent
expanded if/else dispatch were rebuilt and semantic-tested in both configurations
(job427; ch04-checks/branch-final.txt): zero warnings/errors,77/77 each.
Readable summaries extracted untruncated in job428. No generated runtime or C
execution, reviewers, student, commit, or push. Next: write Chapter4 teaching and
walkthrough from this bounded semantic increment, then established review/trial
sequence. Do not expand to loops/full arithmetic merely to postpone teaching.

## Chapter draft and pre-review evidence

Wrote chapter-04.md from the verified implementation: ownership first, declaration/signature/body passes, ordinary builtin declarations, range/context distinction, non-evaluating type hints, exact constants, local flow, continuing-path joins, diagnostic anchors, exercises, cumulative requirements and non-executing walkthrough. No review or student result claimed. Updated indexes; corrected stale Chapter 3 reviewer/student status without claiming human acceptance.

Added tools/spin04.py and make spin04. Published command with installed absolute DOTNET override passed: clean Release build, three silent checks and intended LYC307 at line4/column13/byte78. Saved spin04.txt. This is agent walkthrough evidence, not Bill's acceptance. Links/fence balance and whitespace passed; not a teaching-quality proof.

Three disposable Release mutants built zero-warning/error and were rejected by unchanged public semantic graders: definite intersection replaced by union (branch-one,76/77); possible union omitted (branch-possible,76/77); constant-overflow rejection bypassed (four arithmetic cases,73/77). Saved mutations.txt. Working compiler source unchanged.

Single code reviewer lyric-ch04-code-review admitted with confined read-only project import, job441. Review pending at this note. No text reviewer or student launched yet.

## Single code review correction — deep arithmetic trees

The sole code reviewer reported unbounded semantic recursion and repeated subtree hint scans (R1). Full report: ch04-code-review.md. Before correction, two 4,000-term Release probes succeeded silently in approximately 0.7 seconds each; no crash threshold was measured. The issue is the visible algorithm, not an invented failed test. Replaced arithmetic/group recursion with an explicit checking stack and a single bottom-up hint-map traversal per expression. Hints remain temporary, checking remains left-before-right, and child facts remain authoritative for checked types/constants. Direct signed literals preserve signed-minimum handling. Added retained long-context and long-neighbor cases, bringing the suite to 79.

Job 464 completed exit 0: clean Debug/Release builds and all retained suites, including 79/79 semantic cases each; full log ch04-checks/review-correction.txt. Chapter teaching integrates both stacks at the contextual-type construction point. Reviewer stopped; no re-review requested. Optional coverage/lifecycle suggestions were not made additional gates. Lesson: shallow parsing can yield deep trees; each subsequent pass must account for their actual shape. No code generation/runtime or human acceptance is implied.

## Single text review and frozen reconstruction inputs

The single text reviewer requested clarification of the retained PEG comparison: Chapter 3 requires it, but Chapter 4 had said only separately runnable. Kept the existing requirement and published its exact command, 143 expectations, Node/Peggy prerequisites, and explicit separation from make test. Clarified supported function-body/conditional-arm blocks versus unsupported standalone block statements. No implementation changed and no re-review requested. Reference comparison completed exit 0, 143/143; saved ch04-checks/peg.txt. Reviewer stopped; report preserved as ch04-text-review.md.

Fresh trial prerequisites are the preserved passing Chapter 3 student implementation, not the current reference compiler: 18 files verified byte-identical, including 15 C# sources. Public bundle excludes private notes, build outputs and Chapter 4 semantic implementation. Student will own all implementation changes.

Fresh confined student lyric-ch04-student-01 admitted with the 203-file frozen bundle (manifest SHA-256 dae45870c8d41b13cabc0cd1f20f42d39f4bbdee5fd3cdab60cf8a7ec158e5f4), read-only SDK and Node/runtime imports only. No project/reference/private-history grant. Required cumulative suites, separate PEG comparison and spin04 explicit. Trial in progress, no success claimed.

## Fresh Chapter 4 reconstruction completed

The isolated student lyric-ch04-student-01 extended the preserved passing Chapter 3 implementation without reference-solution access, requirement clarification, grader changes, or parent implementation assistance. Its saved report is ch04-student-report.md. Parent inspection of saved logs confirmed clean Debug/Release builds and all required cumulative checks, including 79 semantic cases in each configuration, the full Unicode checks, separate 143/143 PEG comparison, and spin04. The supplementary lifecycle probe passed 27 assertions; it is not a retained grader. No passing trial was rerun.

Parent verification matched all 203 frozen input hashes and all 185 listed noncompiler files in the candidate, plus the manifest itself (186 unchanged public files). The student was shut down successfully after submission; candidate and logs remain in its preserved workspace. No compiler or frozen input was edited during evidence collection.

Teaching feedback required no correction. Explicit iterative arithmetic traversal and continuing-path joins supported reconstruction. One student-owned diagnostic initially used invented syntax and failed; the student corrected only that diagnostic and retained the failure. Lesson: diagnostic scaffolding must obey the actual language contract, and a scaffolding failure is not an expected compiler rejection. CLI passes and relationship-lifecycle evidence remain distinct.

This is one successful unassisted reconstruction, not universal semantic correctness. The student walkthrough is not Bill's acceptance. Chapter 4 is ready for human reading and make spin04; no Chapter 5, commit, or push was performed.

## Human acceptance

Bill reported, “Chapter 4 looks good.” Chapter 4 is accepted and closed. This records his acceptance after reading; it does not claim that he ran the manual walkthrough. Earlier pending statements describe the historical milestones above. No student rerun or compiler change was needed.
