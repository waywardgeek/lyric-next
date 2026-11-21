# Building Lyric — an agentic construction codebook

This book is being developed with the implementation, not reconstructed after a finished compiler. The [language specification](../lyric-language-spec.md) defines Lyric's behavior; the book teaches how to construct a compiler that implements it.

## Current drafts

- [Preface](preface.md): Bill's author-voice draft on agentic codebooks, the two-edition/self-hosting plan, and the prospective connection to LLM training.
- [Chapter 0 — The Compiler We Will Build](chapter-00.md): working TL;DR and orientation exercises. Not yet an accepted or execution-ready chapter. No implementation, grader run, independent review, student trial, or human walkthrough is claimed.

## Provisional construction sequence

This is the requested planning sketch, not a fixed chapter contract. Only Chapter 0 has been drafted. Each later increment will be agreed with Bill and specified before implementation; experiments may change the sequence.

1. **Establish the executable foundation.** Verify the C#/.NET prerequisites and build a small real compiler entry point with an owned compilation/source model, source diagnostics, and independent checks. Demonstrate Common declarations, functional implementations, complete relationships, and cleanup before multiplying compiler types.
2. **Read source faithfully.** Implement UTF-8 handling, source spans, tokenization, and diagnostics against the approved lexical rules. Pin the Unicode data version and settle remaining relevant lexical questions before treating this slice as complete.
3. **Earn a narrow end-to-end compilation path.** Follow the PEG for a small expression/declaration subset; resolve and type-check it; emit and execute C. Negative inputs must be rejected, and unsupported constructs must not slip through as placeholders. Expand syntax and semantics from a working vertical slice rather than assembling an untested full frontend first.
4. **Build the language's central model.** Add the required type/declaration forms, logical packages, complete relation bindings, construction/destruction, value operations, generics, and multi-class interfaces in bounded chapters. The actual dependency order is development work, not settled by this list.
5. **Demonstrate lifetime and generator safety.** Develop the separate checker design alongside accepted/rejected examples for stable borrows, cross-call effects, counted releases, and safe generators. Introduce checks with the features they protect, not as an optional final pass after unsafe object programs are already accepted.
6. **Make the compiler-oriented library usable.** Adapt the retained stdlib design, verify collision handling and cleanup, and supply the file/process operations the compiler needs. Keep source-level contracts distinct from runtime/native boundaries.
7. **Subsequent execution and self-hosting.** Follow the edition strategy in the [Preface](preface.md). Run shared conformance checks and establish controlled repeated-stage/fixed-point evidence. Preserve the architecture through the rewrite; translating output alone is insufficient. This is a later execution milestone, not a requirement to finish a Lyric port before the first book exists.
8. **Human use and crossover.** Exercise the result on real tasks. Record every correction and its reason in crossover teaching, then repair the construction chapters that should have prevented it before another full execution.

Several items will require more than one chapter; others may combine or move earlier. Concurrency, shared-library unloading, escaping closures, and broad library expansion are not proposed prerequisites for the first single-threaded self-hosting milestone.

## Working method

Start each increment with TL;DR and exercises. Develop implementation and independent graders while explaining decisions, ownership, failures, and checks. Write the teaching body from demonstrated experience. Finish the applicable one-code-review, one-text-review, fresh-student, and human-acceptance sequence before advancing an accepted chapter. Include concrete Take it for a Spin steps; document checks and agent demonstrations do not substitute for human use.

No future chapter, compiler implementation, external dependency installation, or public release is authorized merely by its appearance in this planning sketch.
