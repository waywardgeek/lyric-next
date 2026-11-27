# Chapter 14 starting plan — bounded patch chapter and single-threaded safety closure

Status: planned, not implemented or verified. Bill requested this record while
Chapter 13 was still in progress. Start from the actual accepted Chapter 13
endpoint; inspect its final notes, code, graders and receipts before acting.
Worktree: `/Users/bill/projects/lyric-next`. Do not resume abandoned reconstruction
or alter `/Users/bill/projects/lyric`, archived attempts, private state or scratch.

## Purpose and agreed claim

Bill wants Chapter 13 kept within its intended scope. Chapter 14 is a normal
first-edition patch chapter: demonstrate upgrades, explain their reasons, retain
regressions, and later integrate the teaching into a revised construction sequence.
Do not inflate Chapter 13 to avoid having a patch chapter.

Chapter 13 does NOT claim completed single-threaded memory safety. The intended
Chapter 14 completion claim is:

> Safe Lyric is single-threaded memory-safe, assuming a correct compiler and
> runtime and correct relationship implementations generated from multi-class
> interfaces. Accepted programs access valid, initialized, appropriately typed
> storage or fail before an invalid access.

This is a conditional engineering claim, not a formal proof. Unsafe-code and
foreign-code contract violations are outside it. It does not claim concurrency
safety, leak freedom, arbitrary relationship-algorithm correctness, or complete
debug detection. Counted cycles may leak without violating memory safety.
Ordinary application code must not need debug mode to satisfy this guarantee.
The generated relationship implementation is trusted to maintain reciprocal
links, uniqueness, reachability, and correct attachment/removal/drain algorithms.
Annotations alone do not prove those algorithms correct.

The tasks below distinguish demonstrated defects from audit obligations. If a
concrete blocker requires substantial redesign or a larger chapter, bring the
example and tradeoff to Bill; do not silently expand scope or assert completion.

## 1. Aggregate participant borrowing — demonstrated ownership gap

Current scalar participant parameters, locals, field reads and nullable
conversions borrow within their own maintained relationship. Owning application
boundaries acquire ownership. Ordinary unrelated counted values remain owners.

Three diagnostic experiments (jobs3499/3500) demonstrated extra ownership:
relationship helper saves the first counted child in `(self.first!, self.first!)`,
removes `saved.0`, then prints `after`. Child finalization is delayed: actual
`liveafterNdone`, intended borrowed-participant timing `liveNafterdone`.
Array `[self.first!, self.first!]`/`saved[0]` and explicit `(C,C)` annotation have
the same result. No saved handle is read after removal. These demonstrate extra
references/delayed finalization, NOT a use-after-free defect. The harness stopped
at each mismatch; do not claim both C optimization levels passed.

At this record, Chapter 13's explicit unsupported-path rejection is planned but
NOT implemented. Inspect the final endpoint to establish its real restriction.

Required upgrade:
- Preserve endpoint/relationship identity for relevant aggregate elements through
  annotations, inference, construction, nested values, copying, selection,
  destructuring, assignment, parameters and returns. Use actual semantic identity,
  not matching concrete class names; two roles may use the same class.
- Separate aggregate storage ownership from element ownership. Arrays still free
  their backing storage; unrelated counted elements still retain/release. Mixed
  aggregates must not gain a blanket no-counting exemption.
- Establish ordinary ownership when borrowed participant elements cross an owning
  application boundary. Preserve explicit concrete-view semantics and ordinary
  application containers; do not silently turn every instance of a class into a
  participant borrow.
- Coordinate checking, ownership IR, cleanup, both validator directions and C
  emission. Do not fix only emitted C or add a second ownership authority.
- Retain tests for tuple/array/nested/mixed cases, writable aliases and helper
  boundaries, copying/replacement and exactly-once finalization. Preserve unrelated
  counted-local and owning-return controls. Invalid lifetime cases are compile-only.

Relevant starting points: `Semantics/SemanticSymbol.cs` participant queries,
`IR/Model.cs` storage/value borrowing queries, `IR/Lowering.cs` aggregate transfer
and cleanup, semantic participant views, IR validators and aggregate emitters.

## 2. Distinct AoS/SoA reference representations — agreed upgrade

Current production output uses integer indices and `UINT64_MAX` empty/null for
BOTH layouts. Slot zero can currently be live. Neither queued redesign is done.

- AoS references become actual C object pointers; null is C `NULL`.
- SoA references default to `u32` indices; null is `0u32`. Reserve/waste slot zero,
  allocate from index1, permit indices1 through `2^32 - 1`. Capacity is an upper
  representation bound, not a promise the machine can allocate that many objects.
- Keep the formats genuinely layout-specific. Audit signatures, locals, fields,
  arrays/tuples, nullable conversions, equality, copy/drop, allocation/free lists,
  retain/release, generated teardown and partial-construction cleanup.
- Empty bits do not establish source initialization. All nonnullable fields must
  be initialized before constructor completion; no early reads or implicit null
  values. Preserve nullable/defaultability and reused-slot initialization rules.
- Check allocation/index arithmetic before narrowing, exhaustion and null-slot
  exclusion. Test boundaries with bounded probes, not enormous allocations.
- Preserve stable-storage lifetime rules through last use. Evaluate same-class
  constructor arguments, nested construction, AoS address stability and SoA
  relocation explicitly. Constructor-exit preallocation was only an alternative,
  NOT an adopted policy: it can move rather than solve invalidation and can add
  unnecessary allocation failures. Do not implement speculative reservation.
- Preserve debug allocation-identity behavior while changing production formats.
  Debug representation may differ; document and test the distinction.

Rationale is simpler/faster common null checks and reference representation;
performance improvement is expected, not measured. No benchmark claim without
measurement and explicit safe/unsafe/debug mode identification.

## 3. Nonnullable contributed-field construction — unresolved path

Rule settled: initialize every nonnullable field on every returning constructor
path. Generated cleanup-safe empty storage is unreadable until a valid write.

Current concrete constructor writes to protected contributed links fail LYC308;
leaving them unwritten fails LYC328. This is a construction-permission conflict,
not difficulty with definite-initialization analysis. Find a concrete legitimate
relationship-controlled construction example, then provide the smallest path that
preserves relationship invariants. Do not grant arbitrary application writes,
invent a framework without a use case, or substitute a nullable-only language rule.
Retain missing-init, early-read, branch/return/fallthrough and null/type negatives.
If support remains intentionally excluded, make rejection and claim boundaries
explicit rather than calling rejected functionality implemented.

## 4. Focused safety closure audit — obligations, not established bugs

Use small executable positives and compile-only intended negatives to examine:
- Persistent ordinary handles: no unmanaged fields or container/aggregate hiding;
  only actual maintained endpoint links get the exception. Counted owning fields
  remain valid. Unsupported storage forms must reject before emission, not crash.
- Privilege boundaries: only the particular relationship can mutate its links;
  aliases, helper parameters, aggregate selection, return paths and explicit view
  changes cannot accidentally bypass that restriction or suppress ordinary counts.
- Lifetime effects across helpers and recursion, branches/loops, finalizers,
  membership release, generated field cleanup and cascades. Reject invalid use
  INSIDE a helper after it destroys an owner, not only subsequent caller use.
  Freshly recreating an address/index does not revive an expired borrow.
- Teardown ordering: final first, detach all parents, clean all child relationships,
  ordinary fields, reclaim last. Owned child final sees its memberships. Re-read
  maintained links after cascades; no saved-successor safety assumption. Exercise
  multiple memberships, same-class roles, self-links and indirect reentry within
  the trusted-algorithm boundary, without attempting a universal heap proof.
- Refcount ownership: one count per refs-counted attachment, none per structural
  backlink/link; counted children cannot use owns. Removal clears links before
  release. Clear releasing holders before cleanup; restore the final destruction
  hold on zero; reclaim directly; no metadata pointer survives user code.
- Initialization, array length bounds, null unwrap and valid arithmetic behavior;
  no fixture-shaped exemptions or C undefined behavior on accepted safe inputs.

Unknown ref parameters can point to stack storage; do not invent an owning heap
root, Rust-style exclusivity, concurrency machinery, or a new global heap-analysis
framework without a demonstrated need and agreement.

## 5. Debug limits and execution modes

Chapter13 implemented debug-only per-class allocation identities/tombstones,
separate from reused physical slots. Focused receipts:3490 clean Release build;
Debug/Release each64/64 identity cases and4/4 mode contracts; Release relationship
20/20 executions32/32 rejects in both runtime modes. These are focused, historical
results here, not Chapter14 evidence or a full cumulative pass.

Handle-based field access, retain/release and destruction validate identity;
cleanup permits access until reclamation. Tombstones consume memory proportional
to total allocations until exit. Default output omits the tables/resolvers.
Raw borrowed-address dereferences AFTER acquisition are not tracked. Bill
explicitly accepted that debug detection gap; closing it is NOT a Chapter14 safety
claim prerequisite. Static safe-language lifetime obligations still apply.

Safe mode checks array bounds/null unwrap and required arithmetic. Unsafe omits
those checks but not static typing/initialization/ownership analysis. Object
validity instrumentation is debug-only, not required production overhead. Resource
exhaustion/count-overflow guards remain unchanged pending explicit resolution;
do not silently remove them. Combined --unsafe/--debug behavior needs a documented
bounded check before claiming it verified. Never execute invalid uninstrumented
unsafe/lifetime probes merely to demand controlled failure.

## 6. Completion evidence and teaching

- Freeze and inspect the accepted Chapter13 prerequisite and its actual limits.
- Build public independent positive/negative contracts alongside each upgrade;
  meaningful failures require intended diagnostics, not crashes or arbitrary exits.
- Run all applicable retained checks in Debug/Release, both layouts for affected
  storage behavior, C O0/O2, plus debug absence/presence and corruption controls.
- Audit Common/spoke structure, reciprocal relationships, refusal without partial
  mutation, complete cleanup and authoritative IR separately from behavioral tests.
- Teach the changes and their reasons coherently: model/diagrams where needed,
  construction, exercises, exact commands and practical walkthrough. Preserve
  failed approaches and evidence limits in development notes, not a patch diary
  substituted for construction teaching.
- Follow the normal one-code-review/one-text-review/fresh-student process when
  authorized. Student alone edits its isolated reconstruction; stop reruns after
  full pass. Human walkthrough/acceptance remains separate from automated results.
- Only after closure state the conditional safety claim. Record exclusions and
  unresolved restrictions plainly. No formal proof, leak-freedom or concurrency
  claim. A passing debug probe is not a substitute for safe source admission.

This plan does not start Chapter14, authorize workers/commits/pushes/installation,
or waive the remaining Chapter13 completion process.
