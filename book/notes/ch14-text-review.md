# Single consolidated Chapter 14 text review

## Disposition

One required precision correction identified: narrow the stated unsupported class-field shape so it agrees with the checker and the chapter’s nested-container finalizer example. No demonstrated compiler defect was found in this text review. The chapter otherwise gives a coherent construction explanation for the two increments and a bounded safety audit, without claiming proof, benchmark victory, student success, or human acceptance.

This was the single requested privileged author review. I activated codebook-text-reviewer, read all 501 lines of `book/chapter-14.md`, both requested review/evidence notes in full, the Chapter 14 grader guide, Makefile’s relevant targets, the walkthrough, selected public grader implementations, relevant Chapter 0/13 prerequisite passages and language-spec passages, and relevant actual checker/lowering/emitter paths. I made no edits, ran no builds or tests, and recruited no children. No separate voice guide was supplied or invented. Bill’s approved personal introduction is not treated as a stylistic defect.

## Required correction

### 1. State the class-handle-array field refusal at its actual structural boundary

**Location:** `book/chapter-14.md:136–140`, read together with `173–180`; also keep the corresponding explanation in `graders/ch14/README.md:31–32` precise.

**Problem:** “The older restriction against class fields containing arrays of class handles remains” reads as a recursive prohibition. That is broader than the inspected admission condition and conflicts with the later teaching example of an array of counted items nested in a class tuple field.

**Evidence:** Ordinary field admission at `compiler/Semantics/Check.cs:493–500` and contributed-field admission at `4400–4404` test `fieldType.Class is not null && fieldType.ArrayDepth > 0`; they do not implement a recursive ban on every tuple containing such an array. `graders/ch14/aggregate_participants.py:85–101` exercises the direct `held:[C]` field refusal with LYC306. Conversely, `graders/ch14/safety_initialization.py:32–52` declares `payload:([Item],)` and requires the finalizer-effect diagnostic LYC334, not an unsupported-field diagnostic. The chapter itself accurately discusses that latter effect check at lines 173–180.

**Correction:** Describe the retained refusal as a direct class-handle-array field declaration, such as `held:[C]`, in ordinary or contributed fields. Explicitly distinguish it from the tuple-nested counted-array shape used below. Do not turn this prose repair into a compiler change or imply that all nested forms are supported: other persistent-storage and lifetime checks still apply.

**Why it matters:** A reconstructing student could reasonably implement a recursive LYC306 rejection from the current wording. That would reject the retained finalizer-cycle fixture for the wrong reason and hide the transitive cleanup-effect obligation that Section 4 is teaching.

## Contract questions

No blocking new language-contract question identified. The supplied decisions are represented consistently:

- Ordinary containers own counted elements; borrowing of scalar participant values does not propagate into arrays or tuples.
- Mandatory birth-attached relationships are deferred beyond the book. Protected-write LYC308 and definite-initialization LYC328 refusals remain intact (`chapter-14.md:159–171,450–451`; initialization fixtures `54–71`; language spec `1987`).
- The goal is supported-subset single-threaded storage safety under trusted compiler/runtime and relationship algorithms, not arbitrary relationship correctness, leak freedom, concurrency, unsafe/FFI safety, or formal proof.
- Production representation is not itself the safety argument. Runtime debug keeps fresh identity indirection; production AoS pointers and SoA indices do not inherit those diagnostic guarantees.

The required correction above can be resolved by accurately describing current evidence; it does not require selecting a new support boundary.

## Nonblocking preferences

1. **Disambiguate storage references from counted handles.** At `book/chapter-14.md:208–212`, change “Aggregate copies containing references” to “Aggregate copies containing borrowed storage references (`ref` values)” or equivalent. The paragraph is correct in its alias-analysis context, and Section 7 clarifies it, but “references” can sound like the counted references discussed earlier. The distinction is substantive teaching vocabulary: copied counted handles retain objects; copied borrowed addresses preserve targets/provenance without extending storage lifetime. See also language-spec `758–779,815–821` and chapter lines `280–287`.

2. **Remove stale forward-looking terminology in the prerequisite when convenient.** `book/chapter-13.md:70–72,667–669` calls the upcoming work “aggregate participant borrowing.” Chapter 14 correctly resolves that question as ordinary owning containers. Retain Chapter 13’s historical scalar-only boundary, but consider describing the future work as “participant aggregate semantics/ownership” so its handoff does not predict the opposite policy. Chapter 14 is sufficiently explicit that this is not a hidden prerequisite or a required rerun.

3. **Small editorial cleanup:** `book/chapter-14.md:300` has “the64-bit.” The opening draft-status paragraph (`3–8`) can be refreshed as milestones finish; do not convert it into a completion claim while reconstruction and human acceptance are pending.

## Teaching and implementation agreement

**WHAT versus HOW:** The chapter states the behavioral destination first: initialized, valid, correctly typed storage or checked failure. Sections 1–3 then teach why the destination owns each counted element and why finalization timing distinguishes correct retention from borrowing or leakage. Sections 4–7 explain enforcement algorithms and audit obligations rather than offering only a checklist. Section 8 gives concrete representation construction: nominal forward declarations, stable 64-record AoS chunks, embedded metadata, zero-null u32 SoA handles, separate metadata arrays, wide high water, and free-slot reuse before exhaustion checks.

**Ownership and lifetime:** `Lowering.cs:363–382` implements the explained Ref-before-Move owning boundary; `951–960` applies it to array/tuple operands. Empty moved holders and cleanup sequencing are explained rather than left as emitter folklore. `Check.cs:5111–5146` retains rejection of array elements containing borrowed references. The text correctly distinguishes attachment accounting, structural links, ordinary owners, physical field initialization, exact teardown receiver protection, and saved receiver identity versus raw storage addresses.

**Representation:** `Compilation.cs:1097–1125,1150–1182,1184–1376` agrees with the pointer/index/null/metadata explanation. The public handle probe and its documented allocation-overflow output distinguish actual layout from a renamed common integer handle. The chapter correctly retains conservative source acceptance across AoS and SoA and does not advertise measured performance.

**Reconstructability and public prerequisites:** Starting from accepted Chapter 13 is explicit. The required audit requires the student’s own source locations and reasoning, not copying author filenames as architecture. Neither author note is needed to discover the assignment or expected outputs. Existing public prerequisite passages explain relationship maintenance, destruction phases, protected nullable initialization, and compiler ownership architecture. The chapter adds no new persistent compiler relationships requiring a new diagram. No quoted student exchanges were present to verify.

**Commands and comparisons:** `chapter-14.md:424–453` matches `Makefile:60–113`: Debug and Release builds, retained public suites, external `GRADERS` override, four aggregate mode combinations, and Chapter 14 successor runners. The public guide supplies focused commands and exact comparisons. The updated `safety_calls.py:159–172` requires a complete single diagnostic with exact path/code/message and valid coordinates/offset, and is shared by initialization/admission suites. `Makefile:54–56` and `tools/spin14.py:138–176` agree with the four O2 walkthrough modes, six output lines, compile-only LYC326 candidate, and temporary cleanup. The walkthrough checks presence of the intended rejection rather than the stricter grader full-line match; the chapter does not claim otherwise.

## Verification limits and evidence chronology

- This review performed source/text inspection, not a new execution or isolated reconstruction.
- `book/notes/ch14-safety-audit-verification.md:164–313` records cumulative 4086 passing after migration with clean Debug/Release builds. That run had seven initialization cases.
- Lines `316–326` record later initialization 4100 at 9/9 in both configurations and walkthrough 4104 across all four modes, with no compiler changes.
- Lines `328–344` record adoption of the sole code review’s two preferences and focused 4154: existing Debug/Release assemblies each passed calls18, initialization9, admission3, and aggregate16 executions/2 rejections in four modes at O0/O2. This was not a new build or full cumulative rerun.
- `book/notes/ch14-code-review.md` reports no required implementation corrections and its own bounded independent checks. I do not relabel those as my executions.
- No student has been launched; acceptance remains pending. Source review cannot establish that the original page alone is sufficient for independent reconstruction. These limits do not create an automatic rerun or re-review requirement.

**Lesson:** Ownership and physical representation are separate concerns; a narrower storage-admission condition must also remain separate from recursive lifetime-effect analysis. Precise prose preserves both distinctions and keeps a student from making a superficially safer rejection that bypasses the intended safety check.
