# Consolidated Chapter 12 text review

## Disposition

The chapter substantially teaches the demonstrated increment accurately and usefully. Its central mental model is sound: an interface application is compile-time state, participant occurrences retain identity independently of canonical class types, labels select lookup placement rather than runtime wrappers, and contributed fields retain ordinary ownership semantics. The construction sequence explains how to connect registration, private annotation environments, copied syntax, checked symbols, IR and physical field inventory rather than merely listing desired outputs.

I recommend two required documentation corrections and one small scope clarification before handing the page to the fresh student. None requires a safety redesign, allocator change, additional implementation feature, full-suite rerun, or another review. One specification-alignment question is separately identified below.

## Required corrections

### R1 — Correct the chapter’s remaining exact-runtime-location claim

**Location:** `book/chapter-12.md:257`; related wording in `graders/ch12/nullable_handles.py:7`.

**Problem:** The chapter says checked runtime failures require “the exact diagnostic, location, stack, empty stdout, and exit one.” That overstates the nullable runner’s comparison contract, even though the public guide has already been corrected.

**Evidence:** `graders/ch10/tuples.py:367–374` matches the exact source path, LYR013 text and main stack, but accepts any positive line and column through `[1-9][0-9]*`. The nullable runtime fixtures at `graders/ch12/nullable_handles.py:143–172` supply no expected position. In contrast, `graders/ch12/list_refusals.py:37–42` computes the assertion’s exact line and column and passes those to the Chapter 5 harness. `graders/ch12/README.md:85–88` correctly states this difference. The code-review follow-through explicitly chose documentation correction, not stronger nullable grading (`book/notes/ch12-cumulative-verification.md:145–148`).

**Correction:** At the chapter’s original comparison-contract paragraph, distinguish the two cases: list refusals compare the exact assertion position and method/main stack; nullable failures compare exact text/path/main stack and require a positive source position, not the exact unwrap location. Align the nullable runner’s introductory description if retaining “exact located” could imply exact coordinates. Do not change the grader merely to satisfy this prose review.

**Why it matters:** A student and later author must know what passing actually establishes. An incorrect positive unwrap position would currently pass; the chapter must not turn that evidence into proof of precise location reporting.

### R2 — Update the completion-status sentence and preserve the verification chronology

**Location:** `book/chapter-12.md:261`.

**Problem:** “Reviews ... have not yet occurred” is stale: the sole code review has occurred and its correction is incorporated. The adjacent unqualified statement that the author reference passed its cumulative suite also obscures the distinction between the pre-correction cumulative run and the post-correction focused checks.

**Evidence:** `book/notes/ch12-cumulative-verification.md:128–149` records the completed code review, nominal-only participant admission correction, and author2790’s clean Debug/Release builds with 18/18 multi-class executions, 11/11 rejections, 11/11 nullable executions, 10/10 nullable rejections, and three unsafe checked failures. It explicitly says cumulative2703 remains historical baseline evidence rather than a fresh full-suite result for the corrected compiler. The public guide’s opening status already distinguishes incorporated code review from pending text review, reconstruction and human acceptance.

**Correction:** Replace the stale sentence with a short accurate status, or move volatile workflow status to the public guide and link it. Preserve that the full cumulative suite passed before the narrow admission correction, followed by focused Debug/Release verification. Independent Chapter 12 reconstruction and human acceptance remain pending; incorporate this single text review without claiming that it is reconstruction. The walkthrough’s author-only claim at lines 295–296 is appropriately bounded and can remain.

**Why it matters:** The chapter should neither erase completed review work nor imply a full-suite receipt for a different compiler state. This is a reporting correction, not a demand to repeat cumulative testing.

## Small scope clarification recommended

### C1 — Explicitly distinguish accepted field syntax from contributed-field initializer support

**Locations:** `book/chapter-12.md:99,113,137–149,231`; `graders/ch12/multi-class-syntax.json:38–40`.

**Problem:** The chapter correctly says to reuse ordinary field/method grammar and warns that syntax acceptance is not semantic support. However, “accepts supplied field and method bodies” is ambiguous about contributed field declaration initializers. A public positive syntax fixture contains `count:i32=0`, while actual binding does not admit that field shape.

**Evidence:** `compiler/Semantics/Check.cs:4015–4018` rejects a contributed field unless it has exactly one child (its annotation), before registration. The syntax fixture deliberately accepts a field with an initializer. The executable storage and list fixtures initialize contributed fields in concrete constructors instead (`graders/ch12/multi_class.py:237–245,407–415`). The chapter already identifies other semantic exclusions such as class-array and unit fields, but does not name this one.

**Suggested clarification:** Say “field declarations and supplied method bodies,” and state near field registration that contributed field initializers are not admitted in this increment; initialize these fields in concrete constructors, even though the shared grammar recognizes the initializer form. This is a statement of the inspected boundary, not a request to implement initializers or add hidden student requirements.

**Why it matters:** The distinction prevents a student from reasonably spending time wiring contributed initializer lowering, or from interpreting the syntax fixture as an executable acceptance case. The general syntax-versus-semantics warning is correct; a concrete example here would make it operational.

## Specification-alignment question for the author

**Locations:** chapter section 1 versus `lyric-language-spec.md:1672–1719,1841–1864,1911`.

The chapter’s participant-block/storage-contribution form is clear and matches the public fixtures and inspected compiler. The corresponding language-spec sections still illustrate qualified interface members (`P.target`, `field P.children`) and describe an empty impl as a conformance assertion. They do recognize explicit storage-providing implementations, so this is not evidence that Chapter 12 should implement structural conformance or mappings. However, the inspected interface/storage sections do not explain the chapter’s `class P { ... }` form or explicitly connect empty storage-providing impls to requested contributions.

The author should decide whether to add a narrow specification cross-reference/description of the accepted Chapter 12 form, or explicitly identify the chapter’s construction-stage subset there. The chapter already excludes implicit conformance and mappings, and those exclusions should remain. This question concerns reconciling student-visible authorities, not expanding the language or reversing Bill’s safety decision.

## Positive teaching findings

- **Ownership comes before algorithms.** The opening six views and adjacent class descriptions identify analysis/template co-ownership, source co-ownership of copied syntax, class dependencies, private scopes and weak semantic users. The relationship companion provides exact fields, reverse links and retirement order, including the crucial distinction between compiler-model retirement and runtime relation teardown.
- **The construction instructions explain reasons.** Sections 2–3 explain why keys must include ordered class/label occurrences, why same-class roles share collision inventory but not identity, why private annotation scopes are needed, why copied syntax must be owned/sealed, and why failed candidate construction must clean only newly introduced parts.
- **Semantic identity survives lowering.** Sections 3–4 connect actual contributed symbols to ordinary function checking and IR, specify namespace receiver forwarding and once-only evaluation, and distinguish `ContributingParticipant` from `ParticipantView`. The inspected `BindMultiClass`, Common declarations and `ParticipantView.cs` support those explanations. The nominal-kind admission lesson at line 109 accurately teaches the incorporated code-review correction.
- **Nullable teaching is specific rather than magical.** It distinguishes the language null value from cleanup-safe uninitialized storage, names the current sentinel, preserves checked unwrap under unsafe mode, explains counted Move transfers, and extends typed emptiness to nested containers and regrowth. It does not promise optional scalars or composite participant-view propagation.
- **List teaching is reconstructible.** Append/remove are explained in write order, including singleton, head, middle and tail cases; the inspected LIST source agrees. Guard-before-write reasoning is explicitly separated from what a terminating assertion grader can observe. Counted cycles and manual detachment are described accurately.
- **Commands are runnable under the stated prerequisites.** Make builds both configurations and invokes 33 runners per configuration plus the comparison selfcheck. The separate PEG fixture commands match `tools/peg/test.mjs`; spin12 depends on Release and the script prints source, checks exact output/exit/stderr and disposes temporary artifacts. No command authorizes dependency installation. The chapter correctly distinguishes layout-focused evidence from the cumulative runner’s coverage.
- **The safety boundary is clear and should be retained.** Ordinary scalar class fields remain writable and nonowning; counted fields retain and may cycle. Existing initialization, nullable and borrow rules remain. Automatic relation attachment checks and teardown are deferred. None of those deferred mechanisms is a missing Chapter 12 acceptance criterion.

## Preferences, not blockers

The repeated class descriptions beside the six related views are somewhat repetitive, but they also let each view stand on its own. I do not require shortening them, adding more diagrams, or introducing more code snippets. The current prose plus the public fixtures gives a student a practical construction path from accepted Chapter 11; whether it is sufficient without clarification remains an empirical reconstruction question, not something this prose review can certify.

## Review scope and limits

Read the entire 297-line chapter, the authoring agreements, relevant book README material, the complete public Chapter 12 guide, all four executable Chapter 12 runners and syntax fixture file, their relevant comparison logic, Make and PEG command wiring, spin12, the relationship companion, bounded relevant language-spec sections, Common multi-class declarations and targeted admission/view implementation. Read the cumulative and code-review records, development lines 1–220 and 547–692, and used bounded searches to locate relevant material. Some broad searches were report-clipped; conclusions rely on the subsequently targeted reads, not an assertion that every search result or historical development entry was read.

No files were modified, no reviewers recruited, no tests or build launched, and no install/commit/push performed. A few early tool-result observations were rejected by the tool guard; direct reads subsequently supplied the chapter and directory information. No conclusions depend on unseen output.

Recorded author evidence—not fresh reviewer execution—establishes cumulative2703, focused2790 and spin12 success. The diagram record reports 22 types, 57 relationships, six views, all 41 historical figure files preserved, and rendered inspection; I inspected its textual companion, not a fresh physical print. No Chapter 12 student reconstruction or human acceptance is claimed. No student question/answer quotations appear in this chapter to audit for fidelity. This review does not certify general leak freedom or arbitrary persistent-link safety.

**Lesson:** Exact diagnostic text plus a positive coordinate is weaker than an exact source-location comparison; a full-suite pass before a correction and focused passes afterward are distinct evidence. Preserve both distinctions where the student reads the acceptance contract.

This is the one consolidated text review. Incorporate justified changes directly; no re-review gate or automatic rerun request is imposed.
