# Chapter 14 starting plan — bounded patch chapter and single-threaded safety closure

Status: Chapter14 personal implementation is active. Ordinary container ownership
and the bounded author safety audit passed cumulative Debug/Release job4007;
see `ch14-safety-audit-verification.md`. This is not full Chapter14 acceptance.
Production handle migration has passed focused checks and post-migration cumulative
Debug/Release verification4086. See current development notes.
The original planning rationale below is retained.
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

## 1. Ordinary container ownership of participant handles

Bill clarified the rule during the first Chapter14 slice: generated structural
links do not individually count; relationship management accounts for attachment.
A copy into ordinary user storage DOES count. Ordinary arrays and tuples own their
counted elements, including when constructed inside relationship maintenance from
borrowed participant observations. Each occupied element contributes its ordinary
reference, even when several elements identify the same object. Scalar participant
locals/arguments inside maintenance retain their existing borrowed contract.

Earlier experiments jobs3499/3500 produced `liveafterNdone` after removing a child
held in a container. That is correct ordinary ownership, not a compiler defect.
The proposed per-element borrowing machinery and mixed-view rejection are rejected.
No runtime ownership tags or new participant-view model are required for this rule.

Lift Chapter13's temporary aggregate restrictions, preserving canonical typing,
ordinary copy/drop, protected-link access and lifetime checks. Test tuple/array
literals, annotations, nesting, push, assignment, copies, helper arguments/results,
application copies and mixed sources. Acquiring ordinary ownership must remain
explicit in neutral IR through its existing Ref/Move operations, not invented by
the C emitter. Retain scalar relationship accounting and unrelated-owner controls.
Do not execute invalid lifetime cases.

Relevant starting points: `Semantics/Check.cs` temporary restriction;
`IR/Lowering.cs` Transfer, NewArray/NewTuple and assignment; public Chapter14
aggregate contracts. Chapter13's historical grader remains intact; cumulative
Chapter14 checking must use the agreed successor contract instead.

## 2. Distinct AoS/SoA reference representations — agreed upgrade

At the accepted Chapter13 starting point, production output used integer indices
and `UINT64_MAX` empty/null for both layouts; slot zero could be live. The formats
below are now implemented with focused and cumulative verification4086.

- AoS references become actual C object pointers; null is C `NULL`. Production
  records hold their own count (counted classes only) and next-free pointer. Keep
  stable chunks; do not reconstruct a slot index from an object address.
- SoA metadata follows SoA too: a separate next-free array and, for counted
  classes, a separate reference-count array. The free-list head is one per-class
  scalar. No production array of metadata structs. Counts retain64-bit width;
  next-free entries use the32-bit index format below.
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

## 3. Mandatory relationships — deferred beyond this book

Bill clarified the use case and explicitly deferred implementation to a future
version. A mandatory parent exists throughout the child's live lifetime. The
future child constructor receives the parent and calls the parent's attachment
function before returning; initialization alone is not the complete contract.
Removal and parent teardown semantics remain future design, including policy
choices; no owns-only restriction was agreed.

Current direct protected-link initialization fails LYC308; omitting the required
field fails LYC328. Retain both compile-only refusal controls without adding a
partial-self exemption or calling rejection support. Ordinary permitted
nonnullable contributed value-field initialization stays supported. This is no
longer an unfinished implementation obligation for Chapter 14.

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

Chapter13 is accepted and committed. Bill authorized personal Chapter14 work;
this plan does not authorize workers, commits, pushes or installation.
