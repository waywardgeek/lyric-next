# Consolidated Chapter 14 reference code review

## Disposition

No required correction identified in the inspected Chapter 14 implementation, contract, or evidence. This is a bounded source-and-test review, not a proof of memory safety or a new approval gate. Two nonblocking test-maintenance preferences appear below.

Coding and code-reviewer instructions were activated. I worked read-only in `/Users/bill/projects/lyric-next`, reviewed the working-tree changes against accepted HEAD `14dd2fe574b615d65e222f636d2513d454f30132`, and made no edits, commits, installations, or fresh builds. No children were recruited. Existing scratch files were preserved.

## Implementation and architectural findings

**Production representation and allocation are consistent with the contract.** `compiler/Emission/Compilation.cs:1097–1125` selects typed record pointers for production AoS and `uint32_t` for production SoA, while retaining the debug identity representation. The storage/allocation code at `1171–1376` distinguishes embedded AoS metadata from separate SoA metadata arrays. Production SoA reserves zero, keeps high-water accounting wide enough to represent one beyond the maximum handle, and checks fresh allocation against `UINT32_MAX`. Reclaimed slots are considered before exhaustion of fresh indices. AoS uses stable record storage rather than exposing pointers into a relocating record vector. These are material layout differences, not a renamed common uint64 handle.

**Copy, drop, null, and reclamation follow the representation choice.** The inspected emitter paths include typed local/temporary declarations, nullable conversions, cleanup holders, aggregate components, membership accounting, and retained-array helpers (`Compilation.cs:450–790`, `1050–1178`, `1380–1530`, and `1930–2054`). Release empties its holder before potentially invoking user cleanup. Metadata is not reused through a cached address after destruction can allocate and relocate SoA/debug storage (`1431–1466`; corresponding reclamation logic at `550–578`). Debug identities continue to be retired rather than made equivalent to reused production slots.

**Participant containers acquire ordinary ownership; scalar borrowing does not propagate into them.** The Chapter 14 semantic change removes the temporary participant-container restriction without removing the ban on contained borrowed references. `compiler/Semantics/Check.cs:5111–5146` traverses arrays, tuples, and struct fields and still rejects borrowed array elements with LYC343. `compiler/IR/Lowering.cs:951–960` widens and transfers each new array/tuple element; the inspected `Transfer` path retains a borrowed scalar when an owning destination needs its own count. `Lowering.cs:1388–1415` separates ordinary owned storage from managed structural links. Membership ownership remains independent of caller holders (`1148–1165`), and add accounting is scheduled after validation/preparation, before link publication (`1189–1204`). This supports the specified distinction between scalar relationship parameters and ordinary counted containers.

**Existing lifetime and relationship mechanisms remain the prerequisite, rather than being replaced with layout-specific shortcuts.** I inspected participant declarations and reciprocal relationship/destruction paths, IR transfer/cleanup, semantic fixed-point summaries, protected teardown checks, assignment checking, and call-effect mapping. In particular, `Check.cs:1800–1940` maps current referent targets and ordered mutation/use requirements before applying `RequireSafe`; `4969–5007` protects selected array storage while checking the assignment RHS. The changed files do not add persistent state outside Common or new cross-spoke implementation dependencies. The implementation continues to use declared shared vocabulary and existing ownership mechanisms rather than duplicating a separate Chapter 14 owner model.

## Contract and scope

`book/chapter-14.md` and the relevant root language-spec sections agree on the representation change, container ownership, and supported safety boundary. The chapter explicitly defers mandatory relationships beyond the book (`chapter-14.md:156–160`, `450–451`); their refusal tests preserve an unsupported boundary, not a missing constructor feature to implement here.

The safety milestone is appropriately conditional: supported single-threaded safe programs, trusted compiler/runtime, and correct relationship algorithms; unsafe operations and foreign-code contract violations are excluded (`chapter-14.md:351–383`). The closing limits also avoid a formal-proof or exhaustive-algorithm-test claim (`501`). I found no basis to expand this into concurrency, leak-freedom, mandatory-parent support, or arbitrary unsafe/FFI safety requirements.

## Tests and evidence

I inspected `graders/ch14/*`, their shared execution/diagnostic helpers, the relevant prior fixtures and explicit successors, `Makefile`, and `tools/spin14.py`. The successors preserve the earlier workloads and the missing-destruction-hold negative control while changing expectations that were intentionally superseded by the new representation/admission rules. The checks exercise behavior and generated layout rather than relying solely on successful compilation.

The evidence notes distinguish the historical pre-migration campaign from the post-migration cumulative run. `book/notes/ch14-safety-audit-verification.md:166–313` records cumulative 4086 as a full Debug/Release clean pass. Its initialization count is seven, not nine. Lines `316–326` explicitly record the later no-compiler-change additions: 4100 initialization 9/9 in each configuration and 4104 the four-mode O2 walkthrough, including the intended compile-only LYC326 rejection. The development notes are consistent with that chronology. I did not treat the historical first section as proof of the final layout.

I independently ran bounded checks against the existing Release assembly: production handles **2/2**, initialization **9/9**, and ordered calls **18/18**, all passing. These were not fresh-build checks or a repeat of the full campaign. `git diff --check` also passed. I did not execute known-invalid unsafe candidates.

## Required corrections

None identified within the inspected scope.

## Preferences — nonblocking

1. **Retain the aggregate ownership suite across all four modes in the cumulative target.** `Makefile:103` currently invokes `aggregate_participants.py` only with `--runtime-debug` and default AoS. Focused evidence and other suites exercise the other modes, so this is not a missing demonstration of the current change. Nevertheless, retaining production/debug × AoS/SoA here would better protect the central container-counting behavior from future representation-specific regressions.

2. **Tighten compile-only diagnostic matching if exact diagnostic output is intended.** `graders/ch14/safety_calls.py:158–165` requires success to have empty output, but rejection requires exit 1, empty stdout, and the expected diagnostic as a substring of stderr. Thus these cases prove the expected rejection is present, not that stderr contains exactly one intended diagnostic with a well-formed location. The runtime-access helper uses a stronger full match. A shared full diagnostic matcher would make the evidence more precise; the current test is still meaningful for rejection and is not a demonstrated compiler defect.

## Lessons and limits

The key design lesson is that representation and ownership are separate: pointer versus index storage changes generated layout, not the rule that ordinary containers own counted elements while scalar relationship parameters may borrow. A second lesson from the allocator evidence is that maximum high water does not itself imply failure: reclaimed slots must remain reusable, and a fresh-allocation exhaustion probe must keep them occupied.

This review inspected actual source and author evidence, with the bounded independent executions above. It did not independently reproduce the entire Debug/Release campaign, prove every safe program correct, certify trusted relationship algorithms, or establish live external/FFI behavior. No setup failure or required implementation defect was observed. The author may incorporate the preferences and verify any resulting changes without a demanded re-review.
