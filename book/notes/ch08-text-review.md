# Consolidated Chapter 8 text review

## Conclusion

The chapter substantially teaches the demonstrated design accurately. I found one small required completion-instruction correction and two optional teaching improvements, below. I found no substantive contradiction in the inspected ownership, storage-layout, initialization, or lifetime explanations. This is one author-reference text review, not reconstruction or a new code-review campaign; no further approval round is requested.

## Required correction

### Explicitly name both cumulative selector updates

**Location:** `book/chapter-08.md:188–200`, especially line 198.

**Problem:** The reader is told to add all five Chapter 8 entry points and to use the Chapter 8 selector for the earlier semantic suite, but the corresponding unsafe-suite selector is not named. A student extending their own Chapter 7 Make target can retain its earlier unsafe selector and silently omit the two-layout unsafe coverage.

**Evidence:** The actual `Makefile:51–54` uses `--chapter=8` for both `graders/ch04/check.py` and `graders/ch06/unsafe.py`. In `graders/ch06/unsafe.py:15–21`, the default is chapter 6 and only chapter 8 selects both layouts. Chapter 6 already anticipates this transition at `book/chapter-06.md:206`, so this is a local completion-checklist omission, not a missing prerequisite design or hidden implementation requirement.

**Correction:** At line 198, name both commands/selectors explicitly: use `graders/ch04/check.py --chapter=8` and `graders/ch06/unsafe.py --chapter=8` in each Debug/Release cumulative pass. Preserve the explanation that the semantic selector retires only the old unsupported-empty-class expectation; explain that the unsafe selector adds both layouts. This makes the local completion instructions reproduce the actual retained Make target without relying on remembering a forward reference in Chapter 6.

## Preferences, not blocking corrections

### 1. Put the minimal final-hook spelling and explicit-constructor boundary beside their teaching

**Locations:** `book/chapter-08.md:64–78` and `104–123`.

The chapter explains final-hook behavior thoroughly but never displays `final func finish(self) { ... }` in its own code fences. Similarly, its constructor discussion does not explicitly say that even an empty class needs a declared constructor before `C()` is legal. These are discoverable in the authorized public fixtures, so they are not private or hidden rules: `graders/ch08/check.py:62–67` shows hook declarations and `:91` requires the no-constructor rejection; `compiler/Semantics/Check.cs:2804–2806` implements that rejection. The specification displays final-hook syntax at `lyric-language-spec.md:1268–1277`.

A tiny final-hook example at its original teaching point and a sentence saying there is no synthesized default constructor in this increment would make the supported source surface easier to learn. No larger syntax catalogue or new feature is needed.

### 2. Add one worked cleanup trace, using public cases rather than repair history

**Location:** `book/chapter-08.md:163–175`.

The revised paragraphs correctly explain both conditional-result identity across cleanup and selected-owner occupancy. Their prose is precise but dense. One compact trace—evaluate a branch tail, transfer to the shared result identity, release a local counted holder, apply its hook effects, then validate the result—would help connect the new graph operations to observable rejection. Alternatively, trace `let r = ref a; r = [V()]` to show why `a`, not `r`, owns the later cleanup.

Evidence is already public in `graders/ch08/check.py:14–22`; the corresponding construction is visible in `compiler/Semantics/Check.cs:1146–1185` and `:1227–1253`. This is a readability preference, not a demand for a patch diary, duplicated solution, additional review, or student rerun.

## What is already sound

- **Prerequisite refresh:** `chapter-08.md:11` explicitly sends an older Chapter 7 endpoint through the revised Chapter 4/5 assertions, Chapter 6 unsafe mode, and Chapter 7 capacity/resize-zero teaching and graders. It correctly forbids treating the historical independent student result as verification of those later requirements. The refreshed assertion declarations/emission are present in Chapters 4/5; Chapter 7 teaches selected capacity and tight copies at lines 60–68 and resize-zero release at line 80. This honors `book/notes/authoring.md:5–7` without depending on private authoring conversations.
- **Architecture and model ownership:** Lines 9 and 42–52 preserve Common/spoke architecture, nominal identity, reverse dependencies, refusal-before-mutation, and explicit destruction of the compiler model. Relevant declarations in `compiler/Common/SemanticModel.cs` and relationship operations in `compiler/Semantics/ClassModel.cs` support those claims. The text clearly distinguishes this model teardown from generated Lyric destruction.
- **Runtime mental model:** Indexed identity, independent array slots versus shared objects, ordinary versus counted holders, metadata versus field backing, and descriptor versus payload invalidation are explained separately and consistently with the relevant specification sections.
- **Construction and reasons:** The collection/checking order, receiver evaluation, initialization obligations, free-slot reuse, saved-handle field assignment, post-hook re-resolution, retain/move/release behavior, and cleanup on control edges have meaningful reasons rather than merely naming implementation components.
- **Scope:** Object-leaf fields, relationship graphs, fallible construction, nullable references, generics, escaping references, concurrency, and complete structural destruction are explicitly excluded. Counted array elements do not accidentally authorize persistent object graphs. Conservative class-wide and field-borrow acceptance is stated honestly for both layouts.
- **Integrated fixes:** Lines 167 and 171 teach the two recorded code-review corrections at their original construction points. They explain the failure mechanism and correct ownership/identity rule without asking a student to read the private review. The public static cases retain negative and safe controls.
- **Commands and inventory:** The five entry-point paths and Debug/Release assembly paths match the Make target. The stated layout arithmetic is correct: 49 successes × two layouts × two optimization levels, plus two failure programs × safe/unsafe × both layouts × both optimization levels = 212. The public contract states 154 static cases, four allocation comparisons, and twelve ownership comparisons. These are inventories, not new pass claims.
- **Walkthrough:** `tools/spin08.py` matches the chapter’s programs, exact three-line output, `-O2`, two layouts, cleanup behavior, and located `LYC326` rejection. I read `book/notes/ch08-checks/spin08.txt`, which records both layouts successfully. This supports scripted evidence only, as the chapter says; it is not human acceptance.

## Evidence and limits

I explicitly activated `codebook-text-reviewer`, read all 233 lines of Chapter 8, authoring guidance, Chapter 7, the complete public Chapter 8 README, and the recorded code-review report. I inspected relevant public fixture/checking logic, Make integration, walkthrough source and receipt, selected Common/Semantics/Emission source, and specification passages on arrays, references, construction, finalization, class-wide invalidation, indexed storage, and assertions. Broad searches with clipped output were used for navigation, not treated as complete reads.

I edited nothing, recruited nobody, installed nothing, used no network, and ran no build or compiler probes. Source inspection is not independent behavioral verification. The supplied focused 154/154 post-fix static result remains parent-reported evidence, not a run performed here. The older cumulative receipt and its 145-case static result predate the fixes. The older sanitizer evidence covers 208, not all 212 current executions. I make no final-pass claim for `book/notes/ch08-checks/review/cumulative.txt` or `sanitized-layout.txt`, which were being produced by parent job 1925 and were not verified complete in this review. No fresh student reconstruction is claimed, and these limits do not automatically require a rerun.

**Lesson:** Cumulative grader selectors belong in reproducible construction instructions: an inherited selector can silently omit the new representation coverage even when every selected test passes.
