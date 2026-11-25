# Consolidated text review — restored original Chapter 10

## Judgment

The chapter substantially teaches the demonstrated design accurately, including its reasons and important limits. I found no major architectural contradiction or need for a new development cycle. Three bounded documentation corrections are recommended below; two are small construction/invocation gaps and one is stale completion metadata. None requires a compiler change, expanded language scope, another reviewer, or a re-review loop.

This is the one authorized privileged author text review. Student reconstruction has NOT run; human acceptance remains pending.

## Findings: errors and teaching gaps

### 1. State that declared defaults are checked even when overridden

**Location:** `book/chapter-10.md:155–165`, especially the contrast between per-construction evaluation and declaration-scoped resolution.

**Problem:** The section explains when a default executes and that supplied fields skip it, but does not explicitly distinguish that execution rule from unconditional declaration checking. A reader constructing the checker could reasonably check a default only when an omitted field needs it.

**Evidence:** `graders/ch10/README.md:140–143` explicitly requires every declared default to be type-checked even when overridden. The public `struct-default-type` fixture at `graders/ch10/struct_values.py:172` declares `x:i32=false`, supplies `x:1` at its construction, and still requires `LYC304`. `compiler/Semantics/Check.cs:523–551` implements checking across every declared field initializer independently of construction use.

**Why it matters:** This is an already-published requirement, not a proposed new feature. Readers should not have to discover the distinction by failing a fixture after implementing the prose literally. The public guide prevents it from being a hidden rule, so severity is low.

**Suggested correction:** Add a sentence in “Defaults belong to declarations, not callers”: check every declared default in its declaration scope even if no construction uses it or every construction overrides it; overriding skips runtime evaluation, not validation. The existing public negative fixture is a sufficient concrete illustration; no private source needs to be reproduced.

### 2. Carry the external grader path through the cumulative invocation

**Location:** `book/chapter-10.md:381–389`.

**Problem:** The focused-check instructions tell students with an external read-only grader tree to replace `graders` with its absolute path. The following cumulative command is only `make test DOTNET="$DOTNET"`; it does not explain how that same external tree is selected by Make.

**Evidence:** `Makefile:6` defaults `GRADERS` to the local `graders` directory. `Makefile:44–72` consistently uses `$(GRADERS)` for the cumulative commands, so the supported solution already exists. Chapter 10 requires graders outside the writable solution authority at lines 354–355.

**Why it matters:** In the explicitly supported external-tree workspace, the shown command can use a different local tree or fail to find the graders. The reader should not need private setup knowledge to preserve the stated grading authority boundary. The ordinary repository-root command is correct when its local tree is the assigned public tree; this is a conditional invocation gap, not a generally broken command.

**Suggested correction:** Show the external-tree variant, for example `make test DOTNET="$DOTNET" GRADERS=/absolute/path/to/read-only/graders`, while retaining the simpler command for the normal layout. No Makefile edit is needed.

### 3. Refresh the chapter’s evidence/status banner

**Location:** `book/chapter-10.md:54`.

**Problem:** The banner still says “Independent reviews ... remain pending” and frames the verification only as saved results that are “not a new regression run.” That is stale relative to the completed code review and fresh author verification provided for this review. Its general caution against inferring student success remains correct.

**Evidence:** `book/notes/ch10-final-author-verification.md:3–5` records fresh full Debug/Release cumulative job 904. Lines 82–90 record the sole completed code review and the later focused job 1009: five canonical cases and eighteen corruption controls in both configurations against unchanged assemblies. `book/notes/ch10-code-review.md` is the completed review, not a pending assignment.

**Why it matters:** Readers need a precise distinction between verified reference behavior, reviewed prose/source, and unperformed reconstruction/acceptance. Vague or stale status can obscure the stronger actual evidence without making the remaining limits clearer.

**Suggested correction:** After incorporating this review, describe the full author run and subsequent grader-only focused verification separately, state the completed single reviews accurately, and leave student reconstruction and human acceptance explicitly pending. Do not rewrite the historical four-defect job 904 receipt as though it had run the corrected eighteen-defect grader.

## What is taught well and agrees with the inspected evidence

- **Mental model first:** The four revised model views and adjacent guides introduce analysis-owned canonical identities, tuple-owned occurrence links, type users, co-owned member/initializer scopes, and program-owned generated bodies before the algorithms. `book/figures/ch10-relationships.md:12–76` makes reverse links and historical predecessors explicit. The revision/context labels avoid pretending historical figures describe the current source.
- **Identity versus occurrence:** Sections 1 and 6 distinguish full canonical-key equality from ordered tuple positions and lifetime occurrences. `Common/SemanticTypes.cs:31–87,131–169`, the inspected canonical lookup/destruction paths, and `Check.cs:1961–2040` support those distinctions. Dependency retirement is toward users, not ownership of shared ingredients.
- **Execution order versus storage order:** Section 3’s explicit-input snapshots, declaration-ordered omitted defaults, and delayed transfers match `IR/Lowering.cs:797–842`. Destructuring’s named-owner-before-discard-cleanup teaching matches `Lowering.cs:936–980` and the atomic proposed-binding check at `Check.cs:3567–3628`.
- **Separate traversal questions:** Layout, owned contents, array-growth defaults, and contained borrows have separate contracts and clearly explained stopping edges. Cleanup-safe empty storage is not conflated with a valid source default. These extend Chapter 8’s ownership/default rules and Chapter 9’s explicit IR boundary rather than adding a competing execution policy.
- **Lifetime construction:** Sections 6’s snapshot/live-storage distinction, conservative writes, by-value root exception, finite ancestry folding, batched caller replacement, returning-path bottom, and initializer result transport explain reasons rather than just naming helpers. The inspected `FormalStorage`, `SnapshotContained`, and referent-read construction agree. The conservative recursive policy and limited recursive fixture evidence are disclosed rather than advertised as exact analysis.
- **Public contracts:** The chapter correctly points to the precise schema and comparison guide, preserves earlier inspection fields, distinguishes normal checking from `--check-library`, requires intended diagnostics rather than arbitrary failure, and describes O0/O2 byte comparisons. The shared tuple harness at `tuples.py:339–398` supports those runtime/comparison claims. `canonical.py:185–192` runs the selfcheck before both standalone and normal invocations; the chapter’s corrected explanation at line 389 is accurate. The guide’s eighteen-corruption count agrees with the current selfcheck construction and supplied focused receipt.
- **Scope limits:** Single-file qualified bodies are not advertised as a package loader. Ordinary-object default controls are not called reclamation evidence. Automated passes are not described as collision/lifecycle proof, sanitizer proof, or human acceptance. The walkthrough explains independent value storage versus alias writes and does not imply that field defaults make structs defaultable array elements.

## Optional preference, not a completion defect

`book/chapter-10.md:241–293` is conceptually dense. A short public source-level trace showing an old value snapshot alongside a live reference after holder replacement could make the distinction easier to retain. The existing prose already explains the algorithm and reasons, and public fixtures provide examples; this is an optional pedagogical aid, not a requirement for additional tests, diagrams, or private solution details.

## Scope, evidence, and limits

I explicitly loaded `codebook-text-reviewer` before substantive inspection. I read all 449 lines of Chapter 10 and all of the Chapter 10 grader README, the final author verification note, and the sole code review. I read relevant Chapter 8/9 prerequisite teaching, the project README and recorded authoring agreements; I found no separate voice guide in the searched public project material. I inspected the revised relationship companion and SVG source/labels, selected public fixtures and runner implementations, the Make recipe, Common canonical declarations, canonical lookup/retirement, relevant aggregate lowering, and targeted checker paths. This was a text-accuracy review, not a second exhaustive compiler review.

I did not render the PDF or visually certify diagram layout. Some broad searches returned clipped reports; conclusions above rely on subsequently read source ranges or explicit visible matches, not on unseen search output. I made no edits, installed nothing, ran no builds or tests, recruited no workers, and did not inspect abandoned reconstruction/archive material. The supplied job receipts are retained evidence, not executions independently repeated by me.

The full job 904 result precedes the grader-only schema correction; focused job 1009 verifies the corrected canonical grader in both configurations. Neither establishes student reconstruction. There are no Chapter 10 student questions/answers to validate as reconstruction evidence. Further testing is a human decision, not an automatic consequence of this prose review.

**Lessons:** canonical identity must not collapse distinct lifetime occurrences; skipped default execution must not imply skipped declaration validation; and consistent serialized links do not by themselves establish schema validity. The current chapter already teaches the first well and correctly incorporates the canonical-selfcheck correction. The small default-checking addition would make the second equally explicit.

This completes the single consolidated text review.
