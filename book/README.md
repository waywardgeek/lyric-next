# An Evolving Programming Language for Superintelligence

*Lyric: An Agentic Construction Codebook*

This book is being developed with the implementation, not reconstructed after a finished compiler. The [language specification](../lyric-language-spec.md) defines Lyric's behavior; the book teaches how to construct a compiler that implements it.

Authoring collaboration follows the [recorded agreements](notes/authoring.md).

## Current drafts

- [Preface](preface.md): Bill's author-voice draft on agentic codebooks, the two-edition/self-hosting plan, and the prospective connection to LLM training.
- [Chapter 0 — The Compiler We Will Build](chapter-00.md): student-facing orientation and exercises, accepted by Bill. No executable compiler work belongs to Chapter 0.
- [Chapter 1 — Read the Source Faithfully](chapter-01.md): accepted chapter for the working source-reader increment; Bill completed the walkthrough and fully accepted Chapter 1.

- [Chapter 2 — Recognize Tokens Without Parser Feedback](chapter-02.md): teaching for the verified Unicode and lexer increment; both independent reviews and an unassisted student reconstruction are complete. Bill reviewed and accepted Chapter 2.

- [Chapter 3 — Parse the Language](chapter-03.md): full-parser teaching and public tree contracts. Both independent reviews are incorporated. A fresh student passed all cumulative Debug/Release checks and the PEG comparison, with runtime setup assistance only. Bill has accepted Chapter 3 and closed it out.

- [Chapter 4 — Give the Program Meaning](chapter-04.md): teaching for the verified first semantic increment. Both configurations pass the 79 semantic cases and retained earlier suites. The single code and text reviews are incorporated. A fresh student passed all cumulative Debug/Release checks, the separate PEG comparison, and the scripted walkthrough without requirement clarification or implementation assistance. Bill has accepted Chapter 4 and closed it out.

- [Chapter 5 — Run the Program](chapter-05.md): reference C emission and execution implemented; 75 retained Chapter 5 cases pass in both configurations alongside retained checks. The scripted walkthrough is verified. The single code and text reviews are incorporated; fresh-student reconstruction passed all required cumulative checks, the separate PEG comparison, and the scripted walkthrough; Bill has accepted Chapter 5 and closed it out.

- [Chapter 6 — Expressions and Control Flow](chapter-06.md): expanded reference implementation and teaching complete through both reviews and fresh-student reconstruction. The reference and independent student each pass 250 Chapter 6 cases in both configurations, all retained checks, the separate PEG comparison, and the scripted walkthrough. The student needed runtime setup assistance only; Bill has accepted Chapter 6 and closed it out.

- [Chapter 7 — Arrays, Ownership, and Stable References](chapter-07.md): teaching for the implemented array and reference increment. The single code review is incorporated. All 102 Chapter 7 cases and retained earlier suites pass in Debug and Release, plus the separate 143-case PEG comparison and scripted walkthrough. The earlier 90-case sanitizer run passed. Both single reviews are incorporated; a fresh confined student independently passed all required cumulative checks, PEG143, and the scripted walkthrough without implementation assistance or grader changes. Bill has read and accepted Chapter 7 and reports running `make spin07`.

The historical completion results above predate the newly approved assertion
and capacity contracts. Their construction requirements are integrated into
Chapters 4–7 and their retained graders; successful frozen students have not
been rerun. Current verification is recorded in the development notes.

## Reference implementation in development

The [reference implementation](../compiler/README.md) implements strict
UTF-8 source inspection and the pinned source-character bans. Its [public graders](../graders/README.md)
pass 82/82 in Debug and Release on the development platform. The single code
review identified bounded driver/grader corrections, now incorporated and verified.
The single text review is complete and its documentation corrections incorporated.
Fresh-student reproduction passed all 82 cases in both Debug and Release without
implementation assistance. Bill subsequently ran `make spin01` and fully accepted
Chapter 1. Development decisions and
verification limits are recorded in [authoring notes](notes/ch01-development.md).

Chapter 2 adds pinned NFC/XID processing and source-backed tokenization. Both
configurations pass 58 token cases, 99,826 NFC vectors, all-scalar XID checks, seven
Unicode-request rejection cases, and the retained Chapter 1 suite. The single code and text reviews are complete, with useful readability feedback
incorporated. A fresh confined student independently passed the same cumulative
checks in both configurations without clarification or implementation assistance.
The student also executed the walkthrough and supplementary lifecycle checks;
Bill subsequently reviewed and accepted Chapter 2 and confirmed running `make spin02`.
See [Chapter 2 development notes](notes/ch02-development.md) for evidence limits.

## Chapter 13 reference status

[Maintained relationships and generated teardown](chapter-13.md) passes the corrected
cumulative Debug/Release target. The five PEG suites and scripted `spin13`
walkthrough passed at the earlier author milestone; they were not rerun after the
code-review corrections. The single code and text reviews are incorporated.
See [author verification](notes/ch13-author-verification.md) for exact evidence.
Fresh isolated reconstruction passed every required cumulative Debug/Release
check, all five PEG suites, and spin13 in both layouts. The 306 public input files
were independently hash-verified; the student is stopped without a passing rerun.
See [reconstruction evidence](notes/ch13-student-acceptance.md). Bill has accepted Chapter 13 and the `spin13` walkthrough and authorized a local commit. Pushing remains Bill's responsibility.
Chapter 13 is scalar-participant scoped; the [Chapter 14 patch plan](notes/ch14-starting-plan.md)
carries aggregate borrowing, distinct AoS/SoA handles, and the remaining safety
closure work. Completed single-threaded memory safety is not claimed here.

## Agreed next increments

See [the next-increment roadmap](roadmap.md) for the approved Chapter 8 scope and the interfaces → multi-class interfaces → relationships dependency spine. It supersedes the broad sketch below where more specific.

- [Chapter 8 — Classes, Lifetimes, and Two Storage Layouts](chapter-08.md): reference implementation and teaching complete through the single code and text reviews and fresh independent reconstruction. Both configurations pass all retained checks, including 154 static class cases and 212 layout executions, plus allocation/ownership checks, PEG143, and the scripted walkthrough. The student received a grader correction and constructor-scope clarification, but no implementation assistance; see the [reconstruction report](notes/ch08-student-report.md). Bill has read and accepted Chapter 8 and reports running `make spin08`.

Chapter 9 completes **explicit execution and ownership IR**. Chapter 10 adds
**composite types and canonical type identity**. Chapter 11 builds generic
specialization and compile-time type selection on that accepted foundation;
Chapter 12 adds multi-class interfaces. Chapter 5
stays where it is to provide early executable behavior. See the [design
record](notes/ir-interface-design.md) for the semantic-resolution/IR/C-emission
boundary. [Chapter 9 — Make Execution Explicit](chapter-09.md) now has a full
teaching chapter and an IR-only reference backend. Post-review cumulative
Debug/Release checks and corrected `make spin09` pass; sanitizer evidence predates
the review fixes. One code review, one text review, and fresh isolated reconstruction
are complete. The student passed all retained checks with setup-only assistance,
not implementation patches; see the [reconstruction record](notes/ch09-reconstruction.md).
Bill has fully approved Chapters 9 and 10 after running `make spin09` and
`make spin10`. [Chapter 10 — Composite Types and Canonical Type Identity](chapter-10.md)
is complete through implementation, source-checked diagrams, the single code and
text reviews, and assisted independent reconstruction. The student passed every
applicable retained check in Debug and Release; see its
[acceptance receipt](notes/ch10-student-acceptance.md).

[Chapter 11 — Generic Functions and Compile-Time Type Selection](chapter-11.md)
is complete through implementation, teaching, source-checked diagrams, and both
independent reviews. A fresh isolated student passed all retained Debug/Release
checks, four PEG comparisons, and the walkthrough, with toolchain setup assistance
and no author implementation edits. See the [acceptance receipt](notes/ch11-student-acceptance.md)
and [development evidence](notes/ch11-development.md). Bill has read and accepted
Chapter 11 and reports that `make spin11` passes. He authorized the scoped local
Chapter 11 commit; no push or later-chapter work is included.

[Chapter 12 — Multi-Class Interfaces](chapter-12.md) implements compile-time
participant contributions, independent labeled memberships, nullable handles,
and scalar object fields. Its single code and text reviews are incorporated.
The reference passed cumulative Debug/Release checks before a narrow admission
fix and focused checks afterward. The isolated student has passed the complete
33-runner suite in both configurations, all five PEG comparisons, and the
walkthrough. Runtime setup assistance was required; no author implementation
patches were supplied. Bill has accepted the Chapter 12 text and `spin12` walkthrough. See the [reference evidence](notes/ch12-cumulative-verification.md)
and [student status](notes/ch12-student-status.md). Chapter 12 requires explicit
link removal; automatic relationship-aware destruction is not implemented.

## Future chapters

The [current ordered plan](roadmap.md#future-chapter-sequence--current-planning-order)
now covers Chapter 14 safety closure; tagged unions and matching; generators;
reusable graph composition; modules and Result/Status; standard-library
relationships and Sym; generic classes demonstrated by Dict; floating-point
values and operations; practical I/O and formatting; and guided self-hosting. Later chapter numbers remain provisional.
Edition 1 and its C# compiler remain maintainable foundations during the Lyric
reconstruction: add or revise chapters when concrete gaps emerge, rather than
rush self-hosting through architectural workarounds. Variadics are deferred.

## Earlier broad construction sketch

The ordered future plan above supersedes this historical high-level sketch.

This is the requested planning sketch, not a fixed chapter contract. Chapters 0 through 12 have been drafted. Each later increment will be agreed with Bill, pair-programmed with independent graders, then taught from demonstrated experience; experiments may change the sequence.

1. **Read characters and report errors.** Establish the executable foundation, character parsing, and the error interface together. Independent positive and negative graders must test successful output and illegal-input diagnostics. Demonstrate Common declarations, functional implementations, complete relationships, and cleanup in this small slice.
2. **Recognize tokens.** Build on Chapter 1’s UTF-8 validation and source locations to recognize lexical elements and enforce identifier rules. Settle remaining relevant lexical questions before treating this slice as complete.
3. **Parse the full current grammar.** Build the shared syntax tree, follow PEG ordered choices, and verify recognition, tree structure, diagnostics, and cleanup. Later chapters will add resolution, type checking, and C emission incrementally; parsing a construct does not implement its semantics.
4. **Resolve names and check the first programs.** Chapter 4 builds scopes, symbols, typed facts, calls, contextual integer arithmetic, local initialization, and statement-branch joins. Chapter 5 adds C emission and execution, and Chapter 6 expands integer expressions and control flow.
5. **Extend the language's central model.** Add the required type/declaration forms, logical packages, complete relation bindings, construction/destruction, value operations, generics, and multi-class interfaces in bounded chapters. The actual dependency order is development work, not settled by this list.
6. **Demonstrate lifetime and generator safety.** Develop the separate checker design alongside accepted/rejected examples for stable borrows, cross-call effects, counted releases, and safe generators. Introduce checks with the features they protect, not as an optional final pass after unsafe object programs are already accepted.
7. **Make the compiler-oriented library usable.** Adapt the retained stdlib design, verify collision handling and cleanup, and supply the file/process operations the compiler needs. Keep source-level contracts distinct from runtime/native boundaries.
8. **Subsequent execution and self-hosting.** Follow the edition strategy in the [Preface](preface.md). Run shared conformance checks and establish controlled repeated-stage/fixed-point evidence. Preserve the architecture through the rewrite; translating output alone is insufficient. This is a later execution milestone, not a requirement to finish a Lyric port before the first book exists.
9. **Human use and crossover.** Exercise the result on real tasks. Record every correction and its reason in crossover teaching, then repair the construction chapters that should have prevented it before another full execution.

Several items will require more than one chapter; others may combine or move earlier. Concurrency, shared-library unloading, escaping closures, and broad library expansion are not proposed prerequisites for the first single-threaded self-hosting milestone.

## Working method

Pair-program each solution and independent graders first, explaining decisions, ownership, failures, and checks. Focus testing on those graders; additional tests and probes are welcome when they improve confidence or development speed. Extract small shared grader helpers as actual repetition warrants. Then write the TL;DR, plain-language teaching, and exercises from demonstrated experience. Graders and fixtures are readable by students but not student-editable; existing target solutions are off-limits. Students may write diagnostic tests, but only designated graders carry forward across executions. Chapter completion includes all retained checks through that chapter and its applicable chapter-only checks. Finish the applicable one-code-review, one-text-review, fresh-student, and human-acceptance sequence before advancing an accepted chapter. Include concrete Take it for a Spin steps; document checks and agent demonstrations do not substitute for human use.

No future chapter, compiler implementation, external dependency installation, or public release is authorized merely by its appearance in this planning sketch.

## Build the PDF locally

From the repository root:

```sh
make book
make book-print-test
```

The PDF is written to `book/build/An Evolving Programming Language for Superintelligence.pdf`. `make book-pdf` is an
alias for the same build. Each build assembles the current Preface and numbered
chapters afresh and copies their SVG figures; no stale assembled manuscript is
used. `book/build/` contains disposable local publication output, not source.

The local pipeline is adapted from Ensemble's book scripts. It requires existing
Pandoc, Google Chrome (the macOS application), Poppler (`pdftotext` and `pdfinfo`),
and Python with PyMuPDF. `BOOK_PYTHON` defaults to `python3.13`, the installed Python
with PyMuPDF on the development machine; override it for another environment:

```sh
make book BOOK_PYTHON=/path/to/python
```

For this development machine, ignored `.book-tools.mk` selects the existing Node
and Playwright paths because direct Chrome CLI teardown fails. Other machines
can supply the environment overrides below or their own `.book-tools.mk`.

No packages are installed. Chrome runs headlessly with a temporary private profile,
not the reader's browser session. An optional controlled renderer uses an existing
Playwright installation, with no downloads:

```sh
BOOK_NODE=/path/to/node BOOK_PLAYWRIGHT_MODULE=/path/to/playwright make book
```

The 7×10-inch PDF retains ten-point body and code text. Title, dedication and
contents are unnumbered; the Preface starts at Roman i; Chapter 0 starts Arabic
page 1 on a right-hand physical page. The builder measures pagination before
stamping contents references and footers, then verifies the saved PDF labels,
links, footer clearance and text sizes. Diagram colors are preserved. The reading PDF prepends `book/cover.png`, while
the separate print-interior PDF starts with the title page. Cover insertion
preserves and checks the interior's page labels and navigation links.

The cover master is a 3× Lanczos enlargement of the approved generated artwork:
3147×4497 pixels, approximately 450 PPI at 7×10 inches. Upscaling preserves the
composition but does not create original detail. This is front-cover art, not a
KDP hardcover wrap; final spine, hinge and wrap dimensions require the KDP template
for the selected paper and final page count.

`make diagrams DOTNET=/path/to/dotnet` regenerates the source-checked diagrams
separately when Common declarations change. Building a PDF does not compile the
Lyric compiler, certify its behavior, publish the book, or commit generated files.
