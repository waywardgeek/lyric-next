# Consolidated privileged author code review — Chapter 12

## Disposition

The implementation substantially follows the Chapter 12 design and its intentionally incomplete ordinary-link safety boundary. One bounded semantic admission defect needs correction. A narrower verification claim also needs correction or stronger checking. Neither requires a heap attachment checker, automatic relation teardown, allocator changes, object-array fields, reference reorganization, or a further review round.

This was the single consolidated read-only author review. I made no project edits, commits, downloads, or child-agent requests. The author should incorporate justified corrections and verify them directly; no re-review approval gate is requested.

## Required corrections

### R1 — Reject nullable types as concrete participant arguments

**Category:** implementation defect; medium priority.

**Locations:** `compiler/Semantics/Check.cs:3901–3906` (`BindMultiClass`); `compiler/Semantics/CanonicalTypes.cs`, `SemanticType.Class` getter (approximately lines 205–216). Contract: `book/chapter-12.md` sections 1–3, which bind ordered participant occurrences to concrete classes, while section 5 treats `Box?` as a distinct canonical wrapper.

`BindMultiClass` currently admits a type when `type.Class` is present and `ArrayDepth == 0`. `SemanticType.Class` intentionally follows `Element` links to the underlying nominal declaration. Consequently a nullable argument passes admission, and `endpoints.Add(type.Class!)` silently discards its nullable wrapper. The private participant aliases are subsequently reconstructed as nonnullable class types.

**Observed diagnostic evidence:** reviewer job 2776 invoked the existing Release assembly with `--check` on three otherwise identical temporary programs. `Graph` passed with exit 0; `Graph?` also passed with exit 0; `[Graph]` failed with the intended LYC304 concrete-class diagnostic. Both accepted cases had empty stdout/stderr. No generated program was executed.

Minimal accepted-but-invalid example:

```lyric
interface Pair<P,C> {
    class P { func value(self)->i32 { return 3 } }
    class C {}
}
class Graph { func Graph(self) {} }
class Node { func Node(self) {} }
impl Pair<Graph?:nodes,Node:graph> {}
func main() { let g=Graph(); assert(g.nodes.value()==3) }
```

**Consequence/reason:** an explicitly distinct canonical type is silently normalized in an admission boundary that promises concrete classes. This also makes nullable and nonnullable spellings collapse onto the same complete instance key without a specified conversion rule. The navigation helper is suitable for storage/effect queries, not sufficient for source admission.

**Correction:** require the resolved argument to be a nominal class type, not a nullable/container wrapper, before publishing endpoints. Preserve ordinary transparent aliases that resolve to the actual nominal class. Add a retained semantic rejection for a nullable participant argument (including a labeled case), with a valid nominal control; an alias-to-nullable case is useful if supported. Existing participant and nullable suites are suitable focused regression checks.

### R2 — Align nullable runtime diagnostic evidence with what the grader actually checks

**Category:** verification/documentation mismatch; low priority, not an observed runtime implementation failure.

**Locations:** `graders/ch10/tuples.py:367–374`; `graders/ch12/nullable_handles.py:143–172`; `graders/ch12/README.md:85–88`.

The README describes exact located LYR013 output. The shared nullable runtime harness matches the exact file path, message, main frame, exit status and empty stdout, but permits **any positive line and column** through `[1-9][0-9]*`. The fixture tuples supply no expected position. It therefore does not establish that the failure points to the particular unwrap expression.

**Consequence/reason:** an emitter reporting LYR013 at an unrelated positive source position would still pass. This does not invalidate the checked-null failure evidence, but the current exact-location claim exceeds it.

**Correction options:** either explicitly describe the evidence as exact diagnostic text/path/stack with a positive source position (not exact line/column), or supply fixture-specific expected locations and compare those exactly. If choosing stronger checking, keep the established Chapter 10 default behavior compatible and add a wrong-position comparison control. The Chapter 12 list-refusal harness already supplies actual `(line, column)` expectations and does not have this issue.

## Preferences / small clarity improvements (not blockers)

- `graders/ch12/README.md:106–111` has an ambiguous “It leaves detached ordinary nodes allocated” immediately after saying the separate independent-membership fixture explicitly destroys its endpoints. Name the transition fixture explicitly as the one that leaves same-class nodes allocated. The actual fixtures and chapter distinguish these correctly.
- The model probe's compact retirement switch and several fixture constructors use dense one-line statements. Expanding these when next touched would better match the teaching-code convention. This is not a demand for broad formatting or reference reorganization.

## Positive implementation findings

- **Common/spoke structure:** the new persistent state and callable signatures reside in Common partial declarations; semantic spokes implement them, and the project compiles those parts together under C# 14/.NET 10. I found no new service bag, runtime declaration/body glue, or copied authority in this slice.
- **Authoritative identities:** `MultiClassInstantiation` retains template plus ordered participant occurrences. Same-class roles remain separate objects. `ContributingParticipant` and `ParticipantView` are distinct reciprocal relationships, so a parent-contributed method can return the child view without changing its contributor.
- **Admission and cleanup:** source registration preflights all proposed names, sharing a reserved-name set across same-class roles. Candidate construction has failure cleanup. `AddParticipant` reserves its owning list before attaching the concrete-class dependency. View attachment reserves before IR invalidation/mutation. `SetType` validates/reserves the new type before removing an incompatible view.
- **Retirement:** instance destruction clears IR, marks retirement before cascades, destroys contributions/scopes/copied syntax/occurrences, and removes template/analysis memberships. Owned-part destruction redirects to the instance; peer concrete classes are not destroyed by instance retirement. The direct model probe exercises nine retirement entry points and unchanged-state assertions after refusal using real model operations, not mocks.
- **Derived syntax:** copied nodes retain original source offsets and co-ownership without becoming parsed roots. Generic and multi-class ownership are mutually exclusive; sealed copied syntax cannot be structurally changed through the normal operations.
- **Lookup and lowering:** participant annotations use private environments; views survive scalar nullable fields, inferred locals, grouping and unwrap. Concrete annotations deliberately discard the view. Namespace selection forwards the underlying receiver rather than allocating a wrapper, and lowering inventories physical members from semantic scopes rather than the original parsed class children.
- **Nullable canonical/IR behavior:** nullable wrappers have reverse dependencies and distinct canonical identity. The new IR operations check operand counts, exact payload/wrapper types, result categories and counted Move protocols. Emission performs an explicit sentinel check for unwrap even under unsafe mode. Counted nullable copies skip null retention; recursive array copying carries the nullable leaf policy; resize initializes typed empty elements before publishing length.
- **Counted fields:** destruction emits explicit field release in IR, after finalizer invocation and with a fresh selection from the stable handle. The effect closure follows counted field classes and finalizer summaries with a visited set. This is appropriately distinct from runtime heap-link checking.
- **List algorithms:** reading `LIST` and `tools/spin12.py` confirms append/remove guards precede writes. Removal repairs neighboring links/endpoints and clears all three child links. Independent bindings and counted detach fixtures exercise separate memberships and cycle breaking. Fatal assertion results alone are not presented as heap rollback proof.

## Inspection and verification scope

Reviewed the actual Chapter 12 chapter, relevant language-spec sections and spec changes, public Chapter 12 README and all four executable fixture runners plus syntax integration, tracked compiler diffs against accepted baseline `5947bed06e8327b0e3c0c079c14508847be0f84f`, and new untracked Common/semantic Chapter 12 sources. HEAD-only comparison was empty: this candidate is the working-tree increment, so tracked working changes and untracked Chapter 12 files were included.

Also inspected the actual multi-class direct model probe, nullable additions to canonical and IR probes, their offline same-assembly runner helper, changed public Chapter 3/5/10 harness behavior, cumulative Make wiring, the walkthrough source, diagram manifest and relationship guide, and the cumulative/development verification records. Earlier chapter runners were assessed through relevant harness/integration changes and the retained cumulative receipt, not re-audited line by line as a new whole-compiler review.

The saved cumulative receipt reports author job 2703 exit 0, 33 public runners per Debug/Release and both builds with zero warnings/errors. The development record separately reports model 485/configuration, IR 221/configuration, focused layouts and PEG, and spin12 job 2723's exact three execution lines. I treat these as recorded author evidence, not fresh reviewer executions. I did not demand or perform a redundant full suite rerun.

Fresh reviewer checks:

- Compile-only admission comparison described in R1, using existing Release assembly and temporary files under my own workspace. The first version of that diagnostic had a missing participant-block separator and produced LYC201 for all variants; it was corrected before drawing any semantic conclusion.
- `git diff --check` passed.
- Every lifecycle fingerprint in `tools/common-diagrams/chapter12.json` matched its current source file. Manifest inventory is 22 types, 57 relationships, six views; the eleven new relationship records match the inspected declarations/operations.
- Final Git status showed no reviewer-created project changes. Temporary diagnostic inputs were disposed.

Some large inspection outputs were clipped; findings above rely on subsequently targeted reads/diffs and bounded checks, not a claim that every line of every historical note or prior grader was inspected. Two early Git commands opened pagers; they were closed and the substantive diffs were rerun without paging. One closed pager pipeline exited 141; that was an inspection interruption, not a build/test failure. No review jobs remain running.

## Limits and deliberate non-findings

This review does not establish full safety for arbitrary ordinary-object field writes, cycle collection, automatic detachment, universal leak freedom, physical print legibility, fresh independent reconstruction, or human acceptance. It does not require them for Chapter 12. Ordinary fields remain nonowning and writable; examples must detach explicitly. Counted fields retain normally and may cycle until explicit removal. Existing definite initialization, nullable access, counted ownership and earlier borrow requirements remain applicable.

I did not read the unrelated `arith.ly`, `hello.ly`, or `test.ly`, private/archive inputs, or other projects. No dangling/unsafe-negative program was executed.

**Lesson:** canonical navigation and canonical admission are different operations. `SemanticType.Class` deliberately reaches a class through wrappers; a boundary requiring a nominal class must check the kind explicitly. Also, an exact error message plus a plausible location is weaker evidence than an exact source-location comparison.
