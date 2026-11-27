# Chapter 13 — consolidated text/specification review

## Conclusion

**Required teaching corrections, not a new implementation cycle.** The chapter accurately explains the central scalar relationship design, integrates the two completed code-review fixes at their construction points, and preserves the intended boundary with Chapter 14. Its new first-person Introduction should be retained. The remaining substantive issues are two underexplained construction mechanisms and the public bundle's private-note dependencies.

This is the one consolidated review. I made no edits, launched no agents, and ran no builds, tests, or reconstruction campaign. No re-review gate is requested.

## Required corrections

### 1. Teach nullable detached initialization explicitly, alongside the nonnullable rule

**Location:** `book/chapter-13.md:240–259`, especially the distinction between the allocation sentinel and source initialization; related protected-storage discussion at `:285–298`.

**Problem:** The nonnullable rule is clear, but the chapter never explicitly teaches the complementary new behavior: protected nullable relationship fields begin as source-initialized null, including in reused allocation slots. A reader starting from Chapter 12 is told that concrete-constructor writes to managed links are rejected, yet is not told in the construction section why the empty constructors in the walkthrough are valid or how to establish that fact in definite-initialization analysis.

**Evidence:** `lyric-language-spec.md:1987` explicitly requires nullable fields to begin null. `graders/ch13/README.md:50–53` requires automatic nullable initialization and continued rejection of uninitialized nonnullable fields. The retained `relationship-detached-storage` case in `graders/ch13/relationships.py:354–371` constructs Graph and Node with empty constructors and observes null links. `compiler/Semantics/Check.cs:836–846` marks fields assigned on constructor entry only when `IsManagedLink()` holds and their type is nullable; its comment explicitly ties this to allocation initialization even in reused slots. The chapter's walkthrough source uses the same empty-constructor arrangement.

**Correction:** Integrate a short construction explanation into this section: allocation supplies null for protected nullable fields; definite-initialization analysis treats those fields as assigned at constructor entry; a nonnullable empty sentinel remains cleanup-only and never receives that exemption. Mention fresh and reused storage as the runtime obligation, and point to the existing detached-storage and nonnullable controls. This is an explanation of current behavior, not a request to solve the acknowledged nonnullable relationship-construction gap.

**Why it matters:** Without the positive nullable rule, a reasonable reconstruction can either reject every intended list constructor or incorrectly mark nonnullable sentinel storage initialized. The lesson is that runtime empty storage and source-language initialization are separate facts, with a specific nullable bridge between them.

### 2. Explain the lifetime-analysis treatment of the active fragment receiver

**Location:** `book/chapter-13.md:171–203` (generated teardown and cascades), with related lifetime claims at `:342–348`.

**Problem:** The chapter correctly says to reread `first` after cascades and that fragments borrow their receiver. It does not explain the checker adjustment that makes this legal when receiver and child have the same concrete class: the generated caller keeps the exact active teardown receiver allocated until fragments finish, but this protection must not apply to every same-class value or to saved child storage. The grader guide states the contract, but the chapter does not teach how it fits the existing class-effect invalidation model.

**Evidence:** `graders/ch13/README.md:94–100` explicitly requires the cross-membership cascade and distinguishes the active receiver from saved children (`LYC330`), their borrowed storage (`LYC326`), and direct receiver destruction (`LYC334`). Corresponding negative fixtures are in `graders/ch13/relationships.py:620–668`. `compiler/Semantics/Check.cs:1146–1149`, `:1175–1186`, `:1509–1515`, and `:2220–2225` track the fragment receiver's object identity and exempt that identity from class-wide invalidation; `:2941–2943` rejects direct destruction of the active receiver. Generated destruction in `compiler/IR/Lowering.cs:164–220` borrows the receiver through the fragment calls and reclaims afterward.

**Correction:** Add the reason and algorithm at the cascade discussion: generated destruction owns the receiver's remaining storage lifetime; carry the exact fragment receiver identity through lifetime checking rather than exempting its class; ordinary destruction effects still invalidate other affected objects and borrowed storage. State the direct self-destruction refusal and reference the existing positive cascade and three negative controls. No exhaustive alias-safety claim or Chapter 14 closure is needed.

**Why it matters:** Keeping the old class-wide invalidation unchanged can reject a valid same-class drain. Exempting the whole class can admit stale children. The existing grader is testing this distinction, so its construction rationale belongs in the chapter rather than only in source or a test description.

### 3. Make the student-facing boundary independent of private author records

**Locations:** `book/chapter-13.md:3–5` and `:399–404`; the Chapter 12 acceptance-receipt dependency identified during public-bundle preparation should be handled consistently.

**Problem and evidence:** The opening draft banner sends the reader to `notes/ch13-development.md` for remaining work, and the Chapter 14 boundary links `notes/ch14-starting-plan.md`. The supplied public-bundle preparation finding establishes that neither private document will be present. A student must not need private development or repair history to determine the current exercise scope. The banner also leaves the already-demonstrated debug work framed by an old incomplete-instrumentation warning instead of a precise current status.

**Correction:** Remove the stale banner as proposed, or replace it with a self-contained current status distinguishing verified scalar implementation from pending reconstruction/human acceptance. Keep the Chapter 14 boundary in public prose: aggregate participant borrowing, the layout-specific handle redesign, and remaining single-thread safety closure are deferred; current scalar rejection contracts and the accepted later raw-pointer dereference detection gap remain Chapter 13's stated limits. Remove the private plan dependency rather than importing repair notes. Likewise preserve Chapter 12's factual acceptance statement without requiring its private receipt. I did not independently inspect that Chapter 12 link in this review; its bundle absence is the author's supplied finding.

**Why it matters:** Public exercises must define their own endpoint. Historical notes may support privileged author verification, but they must not become hidden construction inputs.

## What is accurate and should be preserved

- **Introduction and voice (`chapter-13.md:29–77`):** The first-person ambition is appropriate and consistent with the preface. It brings relationships to life, explains why scattered connections make destructors error-prone—particularly for an LLM with limited context—and distinguishes application cleanup orchestration from the reusable algorithm author's connect/disconnect/drain work. The optional final hook remains application-specific action, not a requirement for each application class to reconstruct teardown. Do not dilute this into defensive prose.
- **Authoritative ownership model:** Endpoint occurrence identity, policy-bearing specialization, existing symbol/scope relationships, and explicit IR accounting are explained without inventing a runtime relationship wrapper or duplicated mutable ownership registry.
- **Completed code-review corrections:** `chapter-13.md:285–294` now explains why protected access (`IsManagedLink`) is broader than nonowning participant storage (`IsManagedHandle`), including unrelated counted payload assignment, replacement, cleanup effects, and destruction. This matches `SemanticSymbol.cs:197–208` and the narrower field-cleanup selection in `Lowering.cs:195–199`. The field-specific regression supplements rather than replaces the unrelated-local control. `chapter-13.md:157–169` now teaches actual execution-order validation, nested-region classification, and the difference between temporary cleanup and final operation cleanup; `IR/Validation.cs:556–599` implements the described ordered scan. These corrections are integrated teaching, not merely a patch diary.
- **Teardown and accounting:** Final hook → child-role detachments → parent-role cleanup → ordinary owned fields → reclamation is consistent with inspected lowering. The explanation of attached owning-child finalization, unlink-before-release for counted reference membership, rereading maintained endpoints, and keeping handles rather than interior pointers across callbacks is strong.
- **Scope:** Scalar participant borrowing, ordinary owning aggregate escape via concrete annotations, retained temporary aggregate rejection contracts, unresolved nonnullable construction, and Chapter 14's broader work are distinguished. The accepted debug gap after raw field-address acquisition is stated explicitly at `:442–449`; it is not a Chapter 13 defect to be eliminated by this review.
- **Exercises and commands:** `make test` matches `Makefile:56–101`: Debug and Release builds, cumulative runners, Unicode sweep, explicit migration suites, relationship default/runtime-debug runs, participant IR, debug identity, aggregate boundary, and syntax checks. The supplementary five PEG commands remain separate. `make spin13` builds Release and the script emits runtime-debug C at O2 in both layouts; its exact four output lines, empty-stderr/exit-zero checks, and temporary-file behavior match the chapter. Compiler Debug/Release and generated-runtime debug mode are not conflated in the grader guide.
- **No hidden C# grader prerequisite:** The chapter correctly describes the public Python/JavaScript graders. The 255-check same-assembly C# lifecycle probe is supplementary author evidence, not an independent-language student grader or an extra required exercise command.
- **Evidence boundaries:** The chapter does not claim completed memory safety, performance improvement from ownership simplification, human acceptance, or proof from invalid uninstrumented execution. There are no quoted student questions/answers in Chapter 13 requiring authentication.

## Preferences and minor editorial maintenance

No preference-only prose expansion is necessary. If touching the public index during normal incorporation, update `book/README.md:55–58` so it no longer says all independent reviews are pending after the sole code review and this text review. Keep earlier PEG/walkthrough results milestone-qualified rather than implying those commands were rerun after behavioral corrections. This is status hygiene, not another verification requirement.

## Reviewed scope and uncertainty

I explicitly activated `codebook-text-reviewer` and read all 640 lines of `book/chapter-13.md`, the full Chapter 13 grader README, the sole code review, and the full retained author-verification note including the final job3649 section. I consulted `book/README.md`, `book/notes/authoring.md`, and the preface for authoring/voice context; no separate named voice guide was found in the inspected public material. I read relevant public language-specification sections, Makefile, the complete spin13 script, targeted relationship fixtures, and relevant semantic/IR implementation paths. This was targeted specification accuracy review, not a second exhaustive code review.

The retained record states that corrected cumulative job3649 completed with exit 0, clean Debug/Release builds, and all runners, including 21 relationship executions and 32 rejections in both runtime modes per configuration. The receipt contains selected summaries, not a complete transcript. I did not independently rerun or inspect the external full job transcript. The five PEG suites passed earlier with unchanged syntax; spin13 passed before the behavior corrections. The 255-check supplemental probe and focused regressions are likewise retained author evidence, not runs performed by this reviewer. Human acceptance remains pending, and this prose review is not reconstruction evidence.

Initial tool-guard refusals were resolved by a subsequent direct lookup. Some broad searches were report-clipped; decisive finding locations were read directly or obtained in bounded targeted searches. One attempted emitter-directory lookup used a nonexistent `compiler/Emit` path and produced no evidence; no emitter claim in this report depends on that failed lookup. No tests, implementation changes, future-chapter work, or additional review campaign are requested by this report.
