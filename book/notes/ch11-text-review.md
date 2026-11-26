# Chapter 11 — sole consolidated privileged text review

## Conclusion

The chapter substantially teaches the demonstrated design, rather than merely listing features or preserving a repair diary. Its identity, ownership, growing discovery queue, once-checked inference, unevaluated checking, and selected-block cleanup explanations agree with the inspected implementation. I recommend one bounded construction-scope clarification before reconstruction and one factual status correction. Neither calls for new language features, compiler changes, another review, or a broader development cycle.

This was privileged author review, not student reconstruction. No files were edited, no builds or programs were run, and no agents were launched.

## Required corrections

### 1. State the actual generic declaration-admission boundary, separately from deferred body checking

**Locations:** `book/chapter-11.md:42`, `76–90`, and `212`.

**Problem:** The introduction excludes “mandatory capability constraints,” which does not clearly exclude optional named constraints. Section 1 explains why an unused template body remains unchecked, but does not explicitly teach the declaration checks that still run before specialization. This matters because the public full-language specification includes constrained declarations, and the parser's type-parameter helper already accepts constraint syntax. The reference to that helper at line 212 makes the distinction particularly relevant.

**Evidence:** `compiler/Semantics/Check.cs:435–469` validates templates before removing them from the ordinary checking queue. It rejects member/separate-implementation templates, rejects type parameters with constraint children, and checks generic and value-parameter names for duplicates/shadowing, including collisions between those two parameter groups. These checks occur even when the template is never instantiated. `lyric-language-spec.md:1002–1022` describes a broader eventual generic/constraint language; the chapter must identify its implemented subset.

**Required clarification:** Say plainly that this increment supports unconstrained ordinary generic functions; optional named capability constraints also remain later work. At the discussion of unused bodies, add that deferring body checking does not defer declaration admission: validate the type-parameter/name contract and ordinary duplicate/shadowing rules before putting a template aside. Preserve the useful distinction between a legal unused dependent body and an invalid declaration. This is documentation of existing behavior, not a request to add constraints or invent additional rules.

**Why it matters:** A student should not have to inspect private construction history to decide whether optional constraints belong in the increment, or accidentally postpone all declaration validation merely because body checking is demand-driven.

### 2. Correct the book index's stale code-review status

**Location:** `book/README.md:78–83`, especially lines 81–82.

**Problem:** The index still says both single reviews are pending. The code review is complete and its fixes have been verified.

**Evidence:** `book/notes/ch11-code-review.md` contains the consolidated review; `book/notes/ch11-development.md:252–273` records incorporated corrections and the post-review evidence. The regression log independently contains both clean builds and the updated Chapter 11 totals.

**Required correction:** Distinguish completed code review/corrections from the text-review incorporation, reconstruction, and human acceptance stages. Once this report is recorded, describe the text review's actual state without implying that reconstruction or human acceptance has occurred.

**Why it matters:** The index is a reader-facing account of earned verification, and currently contradicts the detailed evidence. This is low-priority status maintenance, not a reason to repeat tests.

## Optional precision improvements — not acceptance blockers

1. **Name the copied root precisely.** At `chapter-11.md:94–100`, “copy the template's body syntax” can initially suggest copying only its block, although the next sentence refers to removing the root's type-parameter list. `Semantics/GenericSpecialization.cs:37–60` copies the callable's full declaration-shaped syntax, including parameter/result syntax and body, omitting only the root type-parameter list. Calling it the “callable declaration subtree, including its signature and body” would make the construction less ambiguous. The existing surrounding explanation is enough to recover the intended design, so this is precision rather than a new requirement.

2. **Make exact pattern matching explicit for concrete leaves too.** At `chapter-11.md:214–218`, the repeated-variable rule is explicit, but a short sentence that concrete patterns use exact canonical identity, not widening, would distinguish selection from the argument-compatibility operation taught in section 4. `Check.cs:3597–3624` structurally checks arrays/tuples and compares concrete leaves by identity; `lyric-language-spec.md:1045–1050` already says structural pattern matching has no conversions. A simple `i32` subject versus `i64` pattern example would be sufficient; no additional feature or test campaign is requested.

3. **Connect result-query scope to the Chapter 10 rule.** `chapter-11.md:147–149` correctly explains the new result environments. One sentence could make explicit that parameter type annotations retain their earlier outer-scope resolution, while result annotations can inspect parameter declarations. This reconciles the broad wording in `chapter-10.md:201` with the extension and matches `Check.cs:121–139, 198–227`. The current Chapter 11 text already demonstrates the intended result behavior, so this is a helpful cross-reference rather than a blocker.

## Positive findings

- **Ownership is reconstructible.** The chapter and relationship companion distinguish the lookup-visible template from concrete callables, ordered binding occurrences from shared canonical types, source provenance from specialization lifetime, and lexical ancestry from strong scope ownership. The explicit retirement order matches `GenericSpecialization.Destroy`. Fresh-node admission and callable preflight are explained at their construction points, including why reciprocal bookkeeping alone is insufficient.
- **Discovery has the necessary integration points.** Signature completion is demand-driven and cycle-checked; body discovery grows by index; field defaults can discover callables; existing keys are reused before the 1,024-depth bound. The prose distinguishes depth from breadth and does not claim termination proof. Separate declaration/implementation result environments and discovery during implementation-result validation are integrated into the original explanation.
- **Inference does not quietly become a solver.** The chapter describes structural matching, canonical equality for repeated variables, no destination-result inference, ordinary explicit-call conversions, and one-time reference/value checking. These agree with `MatchTypePattern`, `Specialize`, and `CheckArguments`.
- **Static selection preserves safety obligations.** The prose correctly separates parsing every body, validating every pattern, checking only the selected body, and executing neither the subject nor the unselected bodies. It explains outgoing-result ownership and post-cleanup borrow checks rather than treating static selection as permission to skip lifetime analysis. The inspected borrow traversal and `IR/Lowering.cs:930–985` support that account.
- **Commands and comparison contracts are usable.** The Make target includes all three Chapter 11 runners in both configurations. The separate PEG commands and Release-token prerequisite are appropriate. The walkthrough script matches the five published lines, checks exit/stdout/stderr, and uses temporary files. The Chapter 10 shared harness checks emitted-C success, O0/O2 execution, exact positive stdout bytes, and empty runtime stderr. Negative cases require exit 1, empty stdout, and a regex match for the named located diagnostic; they do not compare exact diagnostic prose or coordinates. The public README directs readers to this readable harness. IR inspection checks selected-only calls and absence of `If`, with three corruption controls, without requiring stable IDs or JSON formatting.
- **Evidence is not overstated as reconstruction.** The chapter explicitly labels model probes as supplementary author evidence and leaves reconstruction/human use outstanding. There are no quoted student conversations to authenticate or sanitize in this chapter.

## Scope and verification limits

Read all 291 lines of `book/chapter-11.md`, book README/authoring guidance, relevant Chapter 10 prerequisite sections, the Chapter 11 relationship companion, public README, all three runners and both syntax fixtures, the shared execution/diagnostic harness, relevant language-specification and PEG sections, Make/spin11 wiring, current specialization declarations/operations, and relevant checker/parser/syntax/scope/fact/IR/emission paths. Read both requested development/review records.

Independently inspected the supplied cumulative log's clean Debug/Release build summaries and updated per-configuration results: generics 17 execution/10 rejection, typeof 17/8, matchtype 18/13, plus one IR check and three controls. Full cumulative success, direct model 1108-per-configuration checks, diagram 20-type/46-relationship validation and byte preservation, PEG1873 applicability, and corrected spin11 completion are recorded author evidence; I did not rerun them. Diagram source/relationship text was inspected, but this review did not render figures or establish physical print acceptance.

No fresh student has run. This review supports the construction explanation's plausibility and accuracy; it cannot prove that an unassisted student will reconstruct it. No rerun or re-review is demanded.

**Lesson:** Deferring generic body checking is not the same as deferring declaration admission. Keep that boundary explicit, just as the chapter already keeps source provenance distinct from semantic ownership and static selection distinct from ordinary block cleanup.
