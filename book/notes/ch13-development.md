# Chapter 13 — Maintained relationships and generated teardown

## Status

Design agreed with Bill after Chapter 12 acceptance, November 26, 2026. This is a plan, not an implemented chapter or grammar. Documentation work only: no compiler changes, new graders, reviewers, student, or commit.

Target single-threaded memory safety for the supported subset, conditional on correct compiler/runtime and trusted relationship implementations. The standard library supplies core relationship algorithms. The compiler does not prove arbitrary add/remove algorithms correct. Do not revive the discarded general heap-attachment checker or add a structural-proof system. Concurrency remains later work.

## Agreed syntax

```lyric
interface DoublyLinkedList<parent P, child C> {
    class P {
        first: C?
        last: C?

        pub add func append(self, child: C) {
            prepare {
                assert(child.parent == null)
            }
            child.parent = self
            child.previous = self.last
            child.next = null
            if self.last != null {
                self.last!.next = child
            } else {
                self.first = child
            }
            self.last = child
        }

        pub remove func remove(self, child: C) {
            prepare {
                assert(child.parent == self)
            }
            if child.previous != null {
                child.previous!.next = child.next
            } else {
                self.first = child.next
            }
            if child.next != null {
                child.next!.previous = child.previous
            } else {
                self.last = child.previous
            }
            child.parent = null
            child.previous = null
            child.next = null
        }

        partial owns destroy(self) {
            while self.first != null {
                self.first!.destroy()
            }
        }

        partial refs destroy(self) {
            while self.first != null {
                self.remove(self.first!)
            }
        }
    }
    class C {
        parent: P?
        previous: C?
        next: C?

        partial owns destroy(self) {
            if self.parent != null {
                self.parent!.remove(self)
            }
        }

        partial refs destroy(self) {
            if self.parent != null {
                self.parent!.remove(self)
            }
        }
    }
}

impl DoublyLinkedList<Graph:nodes, Node:graph> owns {}
```

Agreed syntax, not parser-tested. The impl modifier selects partial owns destroy or partial refs destroy at specialization. owns requires an ordinary child; binding it to a counted value child is rejected. refs to an ordinary child only detaches; refs to a counted child contributes exactly one retain per membership and releases it on removal. No ordinary/counted owns variants are needed. The old destructor owns P / destructor refs C spelling is superseded. Bill grounded this choice in his DataDraw code generators: use explicit policy-selected code, not a general policy mechanism.

Roles are explicit, including when one concrete class fills both. owns/refs belongs to the implementation; relation syntax is shorthand for that machinery. Initial recommendation: one parent role, one child role, one designated removal method; multiple add methods may support append/prepend. Exact signature and duplicate-admission rules need contracts.

## Mutation and counting

prepare is first, checks preconditions and may prepare storage, but changes no logical membership. Its locals remain in scope for structural updates. The structural body has no normal early return or recoverable propagation. Invalid membership check-fails before mutation; Result/Status is not a prerequisite. All interface helpers share its managed-link privilege. The trusted algorithm must sequence operations correctly during temporary inconsistency; ordinary lifetime effects still apply.

For a refs membership to a counted child, insert one retain after preparation and before structural updates. Insert one release after removal reconnects neighbors and clears membership. Release may finalize at zero; subsequent accesses and ordinary local cleanup must respect its effects. refs to ordinary children adds no count. owns accepts only ordinary children and cascades destruction. No source manual ref/unref.

All object-reference fields contributed by an explicitly parent/child relationship interface are managed automatically. The compiler initializes detached links. Applications may read subject to visibility/lifetime checks but cannot write or obtain writable references that bypass the relationship operations. All code in the declaring multi-class relationship interface is privileged to maintain its managed links, including helpers, getters and iterators. Privilege follows the specific relationship binding and does not grant arbitrary application code access or waive type/initialization/lifetime checks. Correct link manipulation is part of the trusted library contract.

Only structural storage suppresses ordinary per-field counting. Locals, arguments, results and unrelated fields keep normal ownership. Backlinks and siblings do not each retain endpoints. General multi-class generics remain ordinary contributions, not implicit relationships.

The library author must maintain reciprocal links, unique membership, reachability and valid teardown. An annotation is not certification. Standard implementations require adversarial tests and source review, not claims of compiler proof.

## Destruction phases

1. Mark destruction in progress; run final only for a completed object.
2. **Detach from parents:** fragments declared in child participant blocks, in any order within this phase.
3. **Clean up children:** fragments declared in parent participant blocks, in any order within this phase.
4. Release remaining ordinary fields.
5. Free storage last.

All parent detachments finish before child cleanup. Name phases by their action, avoiding ambiguous parent-role/child-role terminology. A concrete class may contribute to both phases. Fragments return unit, are called only by generated destruction, and cannot cancel destruction or free self.

Do not pre-detach an ordinary owned child before invoking its destructor: its final runs before its own detachment. Partial construction skips its own final; completed children finalize normally. Every fragment reads current membership because earlier fragments/cascades may already have removed members. Counted children retained elsewhere must not be forcibly destroyed.

## Why representation-specific fragments

The cross-relationship example discussed with Bill: P contains A and B through R; A owns B through S. Destroying A may remove and destroy B. A saved successor can therefore be stale even with correct maintenance.

The doubly linked fragment rereads first after each destruction instead of retaining next. A slot-preserving array can read each slot afresh and skip nulls. Swap-removal requires different traversal to avoid skipping moved elements. No stale interior addresses may survive invalidating storage mutation.

This explicitly supersedes the earlier requirement to use safe iterators for generated teardown and the earlier rejection of repeatedly reading first. Application generators remain explicitly safe func ... -> gen C when they promise current-child removable traversal; no automatic upgrade. Their consumer/effect contract is separate from destructor fragments.

A true tree under one exclusive-parent relationship protects siblings from descendant-only cascades. Same-class destruction alone is too coarse to describe that protection. Cross-relationship cascades require separate treatment; do not silently relax application safe-iterator rules before deciding their revised contract.

Lesson: representation-specific teardown belongs beside trusted add/remove algorithms. Compiler orchestration need not infer a universal traversal strategy. Use action-based phase names to prevent ordering confusion.

## Latest decisions and remaining engineering work

Bill clarified: there is no owns relationship with counted children. Each refs membership to a value object contributes one reference, not zero and not one per structural link. This supersedes the earlier owning-counted and weak-counted questions. All code in the multi-class interface is privileged; separate helper privilege annotations are unnecessary.

Persistent ordinary references outside maintained relationships are rejected in the safe subset, including container/aggregate hiding. Counted ordinary fields remain permitted. Bill confirmed this was already the intended end-to-end rule. Chapter 12's temporary allowance must be closed through explicit teaching and retained-test updates, preserving historical evidence rather than rewriting it.

The substantive policy questions are settled. Remaining engineering work includes checking teardown receiver validity across same-class cascades, complete effects, reentrancy, partial construction, self-relations, and exactly-once membership release. Do not manufacture a zero-count-but-still-retained-membership case: refs membership itself holds a counted child alive. Finalization of a counted parent can still release children, with all transitive effects accounted for.

Resolve minor source admissions with concrete contracts: contextual keywords, duplicate fragment rules, extra add parameters, and relation shorthand mapping. References to nonparticipant classes still require actual maintained lifetime protection; privilege and managed-field naming alone do not establish it.

Lesson: owns is cascading ordinary-object ownership; refs follows the target lifetime category. A counted membership retains once. Trust the whole relationship implementation to maintain its representation, while continuing compiler lifetime and type enforcement.

## Next step and evidence limits

Cadence recommended waiting until morning to implement: this is a substantial lifetime/compiler increment, not a small overnight task. Bill requested documentation preservation and a local commit regardless; no autonomous compiler campaign is started. Next, review small cascade/counting traces and public positive/negative contracts with Bill, then follow the normal development-driven chapter process.

Planned checks: list transitions, multiple memberships, graph source/target edges and self-loops, shared-child cascades, final/detach/free ordering, exactly one membership count, refs cleanup, partial construction, external link-write/writable-borrow refusal, and stale local-reference rejection. Tests establish demonstrated behavior, not formal whole-language proof. No live jobs or workers created by this note.
