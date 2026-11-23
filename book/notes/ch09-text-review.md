# Consolidated Chapter 9 text review

## Conclusion

**Corrections required, narrowly in evidence and public command/comparison accuracy.** The chapter substantially satisfies its teaching purpose. It explains why execution belongs between checked semantics and C, distinguishes compiler-model ownership from runtime ownership, and gives a usable construction sequence without introducing generators, optimizations, or source features. `Yield` is consistently a region result.

This is one privileged read-only review. I edited nothing, ran no tests, recruited nobody, and do not request re-review or a passing-student rerun.

## Required corrections

### R1 — Date the verification claims to the version actually exercised

**Locations:** `book/chapter-09.md:9,232–233`; `book/notes/ch09-review-follow-through.md:29–32`; `book/notes/ch09-checks/ir-c-only-milestone.md:31–50`.

The opening says the reference implementation “passes cumulative Debug/Release checks and the recorded sanitizer executions,” and the walkthrough says “We ran this walkthrough successfully.” Those are defensible historical statements, but currently read as verification of the corrected version. The follow-through explicitly says the cumulative/corrected-spin job is pending and the sanitizer results precede the fixes. The older successful walkthrough did not enforce the new child-link/order/operand checks.

**Concrete distinction:** the current `spin09.py:32–46,65–69` now checks the correct sequence and relationships, but its successful execution is not established by the old spin receipt. The inspected model log does establish 175 checks in each configuration, not completion of corrected spin or a post-fix sanitizer campaign.

**Correction invariant:** qualify the historical cumulative/sanitizer and walkthrough evidence, or update the claims only after the parent obtains the appropriate terminal receipt. Keep the pre-fix sanitizer date/limit even if the current cumulative/spin finishes successfully. No additional test campaign is requested. At review time I rely on the supplied status: Debug cumulative completed, Release progressing, corrected spin queued—not verified.

### R2 — Publish the actual C-output normalization rule

**Locations:** `graders/ch09/README.md:413–418`; `graders/ch09/c_emission.py:59–60,84–95`; related chapter wording at `book/chapter-09.md:97,172,196`.

The guide says “No normalization is applied,” while the grader captures subprocess output using `text=True`. Python decodes the output and applies universal-newline translation before comparing strings. For example, `abcabc\r\nabc` can compare equal to the fixture's `abcabc\nabc`; this is not a raw-byte comparison. The chapter's “check exact bytes” recommendation is stronger than what this particular grader establishes.

**Why it matters:** the public comparison contract is part of what an independent implementation must reproduce. It should not promise byte-level enforcement while the executable grader compares decoded, newline-normalized text.

**Correction invariant:** make the guide and evidence description match the actual comparison (including decoding/newline treatment), or deliberately align the grader with the intended byte contract. This finding does not require changing the language's output semantics. Note that `spin09.py:80–82` already uses byte capture and really does compare the walkthrough output as bytes.

### R3 — Narrow the unconditional offline-build claim

**Locations:** `book/chapter-09.md:217–220`; `Makefile:10–11,38–39,45–46`; `compiler/Lyric.csproj:1–10`.

The walkthrough says the command performs “no installations or network actions.” The Python script itself does neither, but the documented `make spin09` first invokes `dotnet build` without `--no-restore`. Implicit restore is therefore allowed. This project has no explicit package references, which makes the normal prepared-host path uncomplicated, but the command does not itself enforce an offline boundary for every installed SDK/NuGet configuration.

**Correction invariant:** distinguish “the script does not install or contact services” from the build's restore behavior, and document the already-restored/prepared environment assumption if making an offline guarantee. Alternatively use an explicitly offline/no-restore build path after prerequisite preparation. This is a command-description correction, not evidence that the observed run actually accessed the network.

## Teaching and contract assessment

- **Problem and reasons:** lines 11–19 establish the responsibility being moved, preserve the working backend, and correctly decline a speed claim.
- **Model ownership:** lines 21–40 explain authoritative semantic identities, strong arenas versus weak region membership, bidirectional relationships, refusal without mutation, and complete destruction. These are enough to guide equivalent design rather than copying identifier spellings.
- **Execution model:** lines 42–75 distinguish values, fixed addresses, and saved-handle Places; explain snapshots and left-to-right effects; and connect iterative construction to the public 1,500-operation case.
- **Runtime ownership:** lines 77–150 cover transfers versus holders, borrowed output arguments, cleanup through reference replacement, overlapping loop-condition/body lifetimes, generated destruction ordering, and bounded validation. These are meaningful reasons, not merely opcode inventories.
- **Migration:** lines 152–172 explicitly remove the fallback and emitter ownership inventory while preserving runtime/numerical helpers. The source spot-check of `Emission/Compilation.cs:16–27` confirms the current entry lowers before emission and uses buffered output.
- **Integrated earlier fixes:** publication-state validation is taught at line 134 and present in `IR/Inspection.cs:12–22`; constant payload/type/range validation is taught at line 140 and present in `IR/Validation.cs:190–212`; walkthrough child-link order is taught at lines 224–226 and implemented in `spin09.py:32–46`. Do not repeat the earlier code review's resolved findings.
- **Public reconstruction inputs:** the chapter points to readable Python graders and their fixture contracts, not private notes or a target solution. The model probe is correctly supplementary rather than a portable behavioral entry point. The public commands match actual script arguments and Make targets. Public scalar/ownership/C fixtures supply concrete source examples; the chapter need not duplicate their full inventory.
- **Prerequisite caveat:** the accepted frozen Chapter 8 student predates doubling. Current Chapter 8 teaches class-buffer doubling at lines 15–19 and 116. The parent’s promised disclosure should accompany reconstruction: implement that public prerequisite refresh from the older student's own source before treating cumulative allocation failures as Chapter 9 regressions. Do not imply the old passing student verified the refresh. A short public cross-reference near Chapter 9 line 176 would make this distinction durable, but the disclosed execution setup can supply it; no rerun demand follows.

## Preferences / small precision improvements

1. A compact worked source-to-IR trace beside the snapshot or branch-lifetime section could make the dense construction prose easier to follow. This is a preference, not a missing executable contract: public fixtures and the final walkthrough already provide examples.
2. `chapter-09.md:229–232` says the walkthrough checks the located LYC301 observations. `spin09.py:89–91` checks exit 1, empty stdout, and presence of `LYC301`, not diagnostic location or exact message. Narrow that sentence to the checks actually made; the scalar grader independently checks the detailed located semantic diagnostic (`ir.py:273–289`). No extra test is necessary for this wording fix.
3. The scalar evaluator's display-name dispatch (`ir.py:97–104,134–135`) remains a consistency preference already identified by the code reviewer. Its fixtures use unique names; the ownership evaluator dispatches by symbol ID and checks same-named methods. I do not elevate this to a new blocking requirement.

## Coverage and verification limits

Read **all 233 lines of Chapter 9**, all 305 lines of current Chapter 8, the full 426-line Chapter 9 grader guide, and the full public `ir.py` (296 lines), `ownership.py` (643 lines), `c_emission.py` (105 lines), `spin09.py`, and Makefile. Read the complete supplied follow-through/code-review notes, `ch09-checks/ir-c-only-milestone.md`, and `ch09-checks/review-model.txt`. Consulted `book/notes/authoring.md` and the preface for authoring/voice context; no separate named voice guide was discovered in the inspected book listing. A broad book search was report-clipped; I do not claim exhaustive coverage of its omitted matches.

Implementation consultation was deliberately bounded: `Common/IR.cs:1–180`, `IR/Inspection.cs:1–55`, `IR/Validation.cs:115–225`, `Emission/Compilation.cs:1–45`, and the project file—not a second exhaustive code review. Historical cumulative/sanitizer results come from the supplied facts and retained milestone/reviewer reports, not a new execution or fresh inspection of all original logs. The model log directly shows clean Debug/Release builds and 175 checks each.

Text review supports reconstruction readiness but does not prove independent reconstruction, human use, general ownership safety, performance, or another-platform execution. Those remain separate evidence, as the chapter itself correctly explains.

**Lessons:** validation certifies a graph state, not future edits; execution follows ordered region links, not arena serialization; historical runs must remain distinct from verification of later fixes; and exact-output promises must describe the actual subprocess comparison boundary.
