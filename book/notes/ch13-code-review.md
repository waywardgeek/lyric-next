# Chapter 13 — consolidated privileged author code review

**Conclusion: corrections required.** The scalar relationship implementation has substantial coverage and generally follows the intended architecture, but an unrelated counted contributed field loses ordinary ownership. There is also a gap between the promised membership-placement validation and the checks actually performed. Neither finding requires Chapter 14 aggregate borrowing or a complete single-thread safety proof.

Coding and code-reviewer instructions were explicitly activated. This was one read-only author review; I did not edit project files, launch children, install, commit, restart services, or implement Chapter 14.

## Required corrections

### 1. Implementation defect — unrelated counted contributed fields are incorrectly treated as nonowning structural slots

**Locations:** `compiler/Semantics/SemanticSymbol.cs:197–228` (`IsManagedHandle`, `IsManagedLink`); `compiler/IR/Model.cs:606–647` (`IsManagedHandleStorage`); `compiler/IR/Lowering.cs:1392–1410` (store lowering), `:195–199` (destruction field selection); `compiler/Semantics/Check.cs:1493–1506` (destruction-effect propagation).

**Requirement:** `book/chapter-13.md:224–225` preserves ordinary ownership for unrelated counted objects. `lyric-language-spec.md:1987` expressly says relationship privilege does not suppress ordinary ownership of unrelated fields. Matching participant links borrow; merely being declared inside a relationship does not make an unrelated counted payload a membership endpoint.

**Concrete trace:** `IsManagedLink` returns true for any nominal/nullable class handle encountered in a contributed field of a policy-bearing relationship. It does not require that the field carry a matching participant view. `IsManagedHandleStorage` uses that predicate for a selected field. Consequently `Store` skips the owning transfer and replacement release, and `LowerDestruction` skips the field altogether. Effect propagation also omits the field's counted destruction effects.

I retained a diagnostic in my own directory, `unrelated-field.ly`: a `Link<parent P, child C>` contributes `payload: Payload?` to P, where `Payload` is an unrelated value class. A public relationship helper assigns a Payload argument to the field. Graph and Node specialize the relationship under `refs`; empty selected fragments are sufficient because this diagnostic has no participant links.

The existing Release compiler accepted both `--check` and `--inspect-ir` with exit 0. The inspected `save` IR contains:

- `SelectField payload` (node 4);
- `Ref value`, `Move`, `WrapNullable` (nodes 5–7);
- `Store [4,7]` (node 8), with no transfer of the nullable owner into the field;
- `Unref [7]` during helper cleanup (node 10).

Graph's destruction IR contains receiver setup, its fragment call, `ReclaimObject`, and return; there is no payload field release. Thus the field does not retain an independent owner when the argument/local owners disappear. Reading it subsequently would risk use after destruction. I did **not** execute a stale-handle program: this conclusion is from accepted compiler IR and the lowering source.

**Required invariant:** separate protected relationship-field access from nonowning participant-storage policy. An unrelated counted field must acquire/replace/release ordinary ownership and contribute its real cleanup effects, while actual structural participant slots must remain nonowning. This is scalar behavior already in scope.

**Associated grader gap:** `graders/ch13/relationships.py:506–537` tests an unrelated counted **local reference slot**, not an unrelated contributed field. That control therefore passes despite this defect. Retain a field-specific ownership regression, including survival after the assigning scope, replacement cleanup, and owner destruction; do not replace the existing local-slot control.

**Lesson:** write protection and nonowning storage are different semantic facts. Reusing a broad access-policy predicate as an ownership predicate silently suppresses legitimate counted ownership.

### 2. Implementation/validation gap — membership phase checks verify provenance, not execution placement

**Locations:** `compiler/IR/Validation.cs:174–204` and `:527–557`; `compiler/IR/ExecutionValidation.cs:11–175`; `tools/probes/ir-model.cs:805–857`.

**Requirement:** `book/chapter-13.md:110–123` requires retain after successful preparation and before structural publication, release after unlinking and before ordinary cleanup, and validation of cardinality **and placement**. The emitter is intended to trust validated IR.

**Evidence:** operation validation correctly checks the number of membership instructions, designated refs operation, exact child parameter holder, binding role, outer-body parent, and origin metadata. Its “wrong phase boundary” check examines `node.Origin`, not the node's actual position among executed body children. Execution validation tracks available definitions and pending Move obligations; it has no relationship phase-order check.

A membership instruction can therefore be moved within the outer body while keeping its origin, operand, and count unchanged. For example, after its child parameter definition, moving a retain before a preparation guard, or moving a release before structural stores, leaves these tested properties intact. Definition availability does not establish preparation/unlink completion. The current lowering sequence I inspected is correctly ordered; this finding is about malformed/transformed IR being admitted, not evidence that today's normal lowering already emits the bad order. I did not execute a corrupted-IR program.

**Consequence:** a later lowering or optimization regression can pass validate–transform–validate and reach emission with a retain on a refused operation or a release before unlinking. Those violate the explicit accounting contract even though cardinality and provenance are valid.

**Required invariant:** validate actual executable placement relative to the preparation, structural, and cleanup boundaries, rather than accepting an origin marker as proof of scheduling.

**Associated grader gap:** `CheckMembershipAccounting` removes or duplicates instructions, using empty preparation/structural bodies. It proves cardinality rejection but not phase rejection. Add independent malformed-order cases with meaningful guards/structural work; they should be rejected without emitting or executing them.

### 3. Teaching/documentation defect — published implementation status is contradictory

**Locations:** `lyric-language-spec.md:1983`; `graders/ch13/README.md:185–188`; compare `book/notes/ch13-author-verification.md`.

The language specification still calls the selected destruction-fragment syntax “not implemented grammar,” despite the actual parser, chapter, and retained recognition evidence. The grader guide says the cumulative target has not been rerun, while the author verification receipt records the completed full cumulative Debug/Release run. The latter paragraph is milestone-qualified, but the guide does not clearly identify it as superseded historical evidence.

**Correction:** reconcile the current grammar status and clearly distinguish historical checkpoints from the latest cumulative result. Preserve historical evidence rather than rewriting it as if earlier checks had happened sooner. Also make the distinction in finding 1 explicit wherever “all contributed object-reference fields are managed links” is used: protected access does not establish that an unrelated payload is a nonowning participant link.

## Positive findings and evidence boundaries

- Common/spoke changes use shared partial declarations and functional implementations rather than a second runtime registry or wrapper architecture. Participant identity is generally reached through the authoritative binding and instantiation, not reconstructed from concrete class names; same-class distinct roles remain meaningful.
- Specialization construction preflights roles/policy/labels before attachment, and its failure path destroys partially established relationships. The inspected IR graph-edit operations maintain reverse links and reserve capacity before destructive replacement.
- The trusted list operations check refusal conditions in preparation before structural writes. Removal reconnects neighbors/endpoints and clears reciprocal child links before release. Parent draining rereads the current endpoint; owns destruction lets the child enter destruction while still attached, preserving final-hook observation.
- Destruction lowering follows final hook, child-role fragments, parent-role fragments, ordinary field drops, reclaim. Finding 1 concerns the incorrect exclusion of an ordinary field, not the overall phase architecture.
- Final counted release clears the releasing holder, decrements, restores the cold-path hold of one before destruction, and avoids reusing a metadata pointer after cleanup callbacks. Balanced temporary references during destruction are not prohibited merely because destruction is in progress.
- Debug uses fresh allocation identities and retained tombstones, including reuse; production does not acquire lifecycle instrumentation. I did not elevate the accepted later untracked raw-address debug gap into a Chapter 13 defect.
- Ownership simplification edits real IR and is surrounded by structural, operation, and execution validation. Its conservative observation/effect barriers are preferable to output-only count optimization, subject to finding 2's validation gap.
- Chapter 12 migration runners preserve original application assertions where applicable, use legal maintained/counting replacements, and retain explicit rejection checks for the withdrawn unmanaged-field allowance. Runtime refusal tests require exact failure diagnostics and acknowledge that process termination alone cannot expose post-failure heap state.
- Borrowed aggregates are explicitly rejected while concrete owning aggregate/application controls remain legal. Layout pointers, genuine aggregate borrowing, nonnullable contributed construction, and full safety closure remain deferred; no correction here asks for those features.

## Inspection and execution limits

I read Chapter 13 in full using bounded ranges, the full author verification receipt and Chapter 14 starting plan, relevant language-specification contracts, the changed Common/compiler paths and relevant baseline diffs against accepted Chapter 12 `4bc7afb`, the Make integration, Chapter 13 grader sources, and applicable migration/refusal sources and IR probes. HEAD `653d32dc` is design-only; findings apply to the actual inspected worktree, not merely that commit. Some large combined reports were clipped; decisive finding locations and the final diagnostic instruction summaries were reread or extracted in bounded form. This was targeted review, not an assertion of exhaustive inspection of every unchanged compiler path or every harness implementation.

The recorded cumulative3518 Debug/Release exit-0 results, PEG3547, and spin3553 are **author evidence read from the receipt**, not suites I reran. My independent execution was bounded compiler checking/IR inspection of the unrelated-field diagnostic; I did not repeat the cumulative suite, execute an invalid lifetime program, or run an independent corrupted-IR validation probe. No setup failure blocked this review.

## Preferences

No additional preference-only changes are needed to act on this review. Correct the required ownership, validation, and documentation issues and run focused regressions through the normal author workflow; this report does not create a re-review approval gate.
