# Chapter 4 — consolidated text review

## Scope and conclusion

This is the single consolidated Chapter 4 text review, not a second code review or a reconstruction trial. The `codebook-text-reviewer`, `coding`, and `code-tools` skills were explicitly activated under the inherited primary. Project inspection used read-only absolute-path command imports with the command working directory in this reviewer's own sandbox. No project files were edited; no compiler, grader, installation, or delegation was run.

**Conclusion:** Chapter 4 provides a coherent, reconstructible account of the bounded semantic increment. Its ownership model, three-pass declaration strategy, contextual integer algorithm, assignment analysis, diagnostic interface, and non-executing walkthrough agree with the inspected current implementation and public fixtures. One completion-contract ambiguity should be clarified. One optional scope clarification follows. Neither finding calls for new compiler features, additional acceptance gates, a target-solution listing, or another review cycle.

## Required clarification

### R1 — State whether the prior required PEG comparison carries forward

**Location:** `book/chapter-04.md:202–228`, especially 216 and 226; compare `book/chapter-03.md:175–184`, `book/chapter-00.md:101–109`, and `graders/ch03/README.md:50–81`.

**Problem:** Chapter 4 directs students to the cumulative `make test`, describes it as including all retained Chapter 1–3 checks, and describes the independent PEG comparison only as remaining “separately runnable.” Chapter 3 explicitly calls that comparison “required” and says completion requires those checks. Chapter 0 requires retained checks to carry forward. The public Chapter 3 README calls all checks retained, but places PEG under a separate heading rather than clearly classifying its future completion status. A student cannot confidently tell whether Chapter 4 completion requires rerunning PEG or whether the accepted Chapter 3 result suffices while the parser remains unchanged.

**Evidence:** The actual `Makefile` test recipe runs the comparison-helper self-check, source, Unicode, token, tree, recognition, parser-boundary, and semantic graders in Debug and Release. It does not invoke `tools/peg/test.mjs`. The current `book/notes/ch04-checks/review-correction.txt` confirms that same suite, not a fresh PEG comparison. This does not invalidate those successful results or the prior Chapter 3 comparison; it exposes an unclear carry-forward rule.

**Why it matters:** “Cumulative completion” needs a determinate stopping condition. The student should not silently omit an existing requirement, infer an extra run from private authoring knowledge, or install dependencies unnecessarily.

**Recommended correction:** Make the carry-forward policy explicit at Chapter 4's completion paragraph. If PEG remains required at this milestone, identify it as the existing separately invoked requirement and link its command/prerequisites. If accepted Chapter 3 PEG evidence is sufficient for this unchanged-parser increment, say that explicitly and reconcile the prior blanket retained/completion wording. This is a consequential contract question for the author and Bill, not authority for this reviewer to impose a new gate or demand a rerun. Continue to distinguish PEG recognition evidence from semantic checking.

## Optional clarity improvement

### O1 — Name the supported block contexts more narrowly

**Location:** `book/chapter-04.md:43`, 149–182, and the supported/unsupported overview at 23–25.

**Observation:** “Each ordinary block gets a distinct scope” is correct for the blocks the checker processes, but can suggest that standalone nested block statements are supported as well as function bodies and conditional arms. The implementation's `CheckBlock` statement dispatch (`compiler/Semantics/Check.cs:193–240`) handles bindings, assignments, expression statements, and returns; a standalone `Block` reaches the unsupported case. Chapter 3 expressly teaches standalone blocks as parsed statements (`book/chapter-03.md:131`).

**Why it matters:** A reader exploring lexical scope may naturally try a bare nested block. Its rejection could look inconsistent with the scope explanation even though Chapter 4's general unsupported-boundary rule permits it.

**Suggested refinement:** Say that each supported function-body or conditional-arm block receives a scope, and optionally include standalone block statements among examples outside this increment. This is a precision improvement, not a request to add block support or another retained case.

## Teaching and accuracy findings that need no correction

- **Problem before machinery:** Lines 11–27 explain what parsing does not establish and why unsupported syntax must not be accepted as checked. The bounded milestone is distinguished from the language contract and from execution.
- **Ownership and authoritative data:** Lines 29–71 explain compilation ownership, analysis-owned scopes/facts, weak lexical edges, strong symbol ownership, multiply owned function scopes, backlinks, constructor unwind, and destruction order. This matches `Common/SemanticModel.cs`, `Semantics/SemanticModel.cs`, `FunctionScope.cs`, `SemanticSymbol.cs`, and `SemanticFact.cs`. The distinction between persistent semantic relationships and temporary algorithm references is explicit. Ordered parameter symbols are the signature authority, not a copied array.
- **Declaration ordering:** Lines 73–92 teach collection before signatures before bodies and give the reason: forward calls and no-shadowing must not depend on top-level declaration order. Builtin calls use ordinary symbols; executable and library modes have distinct entry requirements. These match the current `Compilation.Check` setup and passes.
- **Integer typing and the code-review correction:** Lines 94–132 distinguish whole-range widening from literal context, apply signs before signed-minimum range checks, preserve operation width, and check every exact intermediate result. Lines 122–130 incorporate the prior robustness correction at the original construction point: bottom-up temporary hint discovery and an explicit checking stack, left-before-right processing, separate call argument entries, and bounded visits. Current `Check.cs:465–689` implements that design. The explanation preserves both the reason for the correction and its 4,000-term verification, rather than appending a patch diary. No recurrence of the old recursive-hint algorithm is alleged.
- **Assignment and control flow:** Lines 134–182 teach initializer-before-registration, assignment-target versus read facts, separate definite/possible symbol sets, fresh incoming branch state, intersection/union only over continuing paths, and removal of block-local flow entries without destroying retained symbols. The worked returning-branch example makes the reason for excluding a path understandable. These agree with `Check.cs:193–295,328–398`.
- **Public contracts are accessible:** Lines 184–224 point to public exact diagnostic messages and anchors, preserve earlier inspection modes, prohibit reference-solution inspection and grader edits, publish the independent executable invocation, explicitly warn that Chapter 4 takes no `--` separator, and distinguish the isolated grader from cumulative completion. `graders/ch04/check.py` supplies 79 named retained cases and independently computes expected locations; each subprocess has a 15-second limit. Exit/stream/message comparison rules match the README. The chapter's long-chain sentence does not promise a performance benchmark; the ordinary timeout remains stated.
- **Practical walkthrough:** Lines 230–254 match `Makefile` and `tools/spin04.py`: Release build, temporary files, three silent successful checks, and intentional LYC307 rejection. The helper prints labels and source, not executed Lyric output. Its negative check is a diagnostic-substring/status check, not an exact diagnostic oracle; the chapter does not claim otherwise. SDK overrides, cleanup, and no-network/no-credential behavior of the walkthrough are explained.
- **Limits and student evidence:** The chapter does not claim C emission, runtime overflow enforcement, full-language safety, human acceptance, or a successful Chapter 4 reconstruction. There are no quoted student questions/answers to audit. The existing prose is instructional rather than dependent on private construction conversations. No standalone voice/style guide was found by filename search; the Preface and Chapters 0/3 supplied the relevant construction and evidence conventions.

## Evidence read and verification limits

Read `book/chapter-04.md` in full (254 lines), `graders/ch04/README.md` and `check.py`, Common semantic declarations/types and the Semantics implementation files, relevant Chapter 0/3 contracts, the Preface/book index, current Makefile and spin helper, model/syntax invalidation excerpts and driver integration, the prior single code-review report, Chapter 4 development notes, and relevant numeric language-contract passages.

The latest saved cumulative log, `book/notes/ch04-checks/review-correction.txt:1–200`, records comparison self-check success and Debug/Release builds with zero warnings/errors. Each configuration reports 82 source cases, 99,826 NFC vectors, XID classification across all 1,112,064 scalars, seven malformed requests, 58 token cases, 66 exact syntax trees, 143 recognition cases, seven parser boundary cases, and 79 semantic cases passing. These are inspected saved results, not runs independently reproduced by this reviewer.

`book/notes/ch04-checks/spin04.txt:1–38` records the earlier successful published walkthrough, including the intended located LYC307. It is agent-run evidence, not a human walkthrough. The historical code review and branch/mutation evidence refer to the then-current 77 cases; the two added long-chain cases explain the current 79. Their historical counts are not current-count errors. Supplementary lifecycle claims in development notes were read, but disposable probes were not inspected or rerun and are not substituted for public graders.

Chapter 3 reviews/student work are reported complete; human spin remains unconfirmed. Chapter 4 fresh-student reconstruction and human acceptance are not established by this prose review. No claim is made that inspection proves all ownership invariants or every supported program.

One inspection command ended with `rg: command not found`; subsequent inspection used `grep`. Oversized reports clipped some output; necessary chapter/review/contract passages were recovered with bounded reads. No compiler check failed because no compiler was invoked.

**Lesson:** parser recursion depth and resulting tree depth are distinct; the chapter now correctly teaches each later pass to handle the tree shapes it receives. Equally, temporary analysis work and retained semantic facts have different lifetimes, and verification evidence and completion policy must be stated separately.

The author can incorporate justified feedback directly. This report requests no re-review campaign.
