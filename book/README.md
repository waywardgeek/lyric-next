# Building Lyric — an agentic construction codebook

This book is being developed with the implementation, not reconstructed after a finished compiler. The [language specification](../lyric-language-spec.md) defines Lyric's behavior; the book teaches how to construct a compiler that implements it.

## Current drafts

- [Preface](preface.md): Bill's author-voice draft on agentic codebooks, the two-edition/self-hosting plan, and the prospective connection to LLM training.
- [Chapter 0 — The Compiler We Will Build](chapter-00.md): student-facing orientation and exercises, accepted by Bill. No executable compiler work belongs to Chapter 0.
- [Chapter 1 — Read the Source Faithfully](chapter-01.md): teaching draft for the working source-reader increment.

## Reference implementation in development

The [Chapter 1 reference increment](../compiler/README.md) now implements strict
UTF-8 source inspection and the pinned source-character bans. Its [public graders](../graders/README.md)
pass 82/82 in Debug and Release on the development platform. The single code
review identified bounded driver/grader corrections, now incorporated and verified.
The single text review is complete and its documentation corrections incorporated.
Fresh-student reproduction passed all 82 cases in both Debug and Release without
implementation assistance; human walkthrough and acceptance remain pending. Development decisions and
verification limits are recorded in [authoring notes](notes/ch01-development.md).

## Provisional construction sequence

This is the requested planning sketch, not a fixed chapter contract. Chapters 0 and 1 have been drafted. Each later increment will be agreed with Bill, pair-programmed with independent graders, then taught from demonstrated experience; experiments may change the sequence.

1. **Read characters and report errors.** Establish the executable foundation, character parsing, and the error interface together. Independent positive and negative graders must test successful output and illegal-input diagnostics. Demonstrate Common declarations, functional implementations, complete relationships, and cleanup in this small slice.
2. **Recognize tokens.** Build on Chapter 1’s UTF-8 validation and source locations to recognize lexical elements and enforce identifier rules. Settle remaining relevant lexical questions before treating this slice as complete.
3. **Earn a narrow end-to-end compilation path.** Follow the PEG for a small expression/declaration subset; resolve and type-check it; emit and execute C. Negative inputs must be rejected, and unsupported constructs must not slip through as placeholders. Expand syntax and semantics from a working vertical slice rather than assembling an untested full frontend first.
4. **Build the language's central model.** Add the required type/declaration forms, logical packages, complete relation bindings, construction/destruction, value operations, generics, and multi-class interfaces in bounded chapters. The actual dependency order is development work, not settled by this list.
5. **Demonstrate lifetime and generator safety.** Develop the separate checker design alongside accepted/rejected examples for stable borrows, cross-call effects, counted releases, and safe generators. Introduce checks with the features they protect, not as an optional final pass after unsafe object programs are already accepted.
6. **Make the compiler-oriented library usable.** Adapt the retained stdlib design, verify collision handling and cleanup, and supply the file/process operations the compiler needs. Keep source-level contracts distinct from runtime/native boundaries.
7. **Subsequent execution and self-hosting.** Follow the edition strategy in the [Preface](preface.md). Run shared conformance checks and establish controlled repeated-stage/fixed-point evidence. Preserve the architecture through the rewrite; translating output alone is insufficient. This is a later execution milestone, not a requirement to finish a Lyric port before the first book exists.
8. **Human use and crossover.** Exercise the result on real tasks. Record every correction and its reason in crossover teaching, then repair the construction chapters that should have prevented it before another full execution.

Several items will require more than one chapter; others may combine or move earlier. Concurrency, shared-library unloading, escaping closures, and broad library expansion are not proposed prerequisites for the first single-threaded self-hosting milestone.

## Working method

Pair-program each solution and independent graders first, explaining decisions, ownership, failures, and checks. Focus testing on those graders; additional tests and probes are welcome when they improve confidence or development speed. Extract small shared grader helpers as actual repetition warrants. Then write the TL;DR, plain-language teaching, and exercises from demonstrated experience. Graders and fixtures are readable by students but not student-editable; existing target solutions are off-limits. Students may write diagnostic tests, but only designated graders carry forward across executions. Chapter completion includes all retained checks through that chapter and its applicable chapter-only checks. Finish the applicable one-code-review, one-text-review, fresh-student, and human-acceptance sequence before advancing an accepted chapter. Include concrete Take it for a Spin steps; document checks and agent demonstrations do not substitute for human use.

No future chapter, compiler implementation, external dependency installation, or public release is authorized merely by its appearance in this planning sketch.
