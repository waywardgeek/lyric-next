# Chapter 14 — Closing the Single-Threaded Safety Boundary

This chapter is accepted. Its central goal is single-threaded memory safety
for the supported safe language, assuming a correct compiler/runtime and correct
trusted relationship algorithms. The ownership correction below supports that
milestone; completing a patch list is not sufficient evidence of safety. The bounded author audit, post-migration cumulative checks, and automated walkthrough
have passed. Both independent reviews and fresh isolated reconstruction are complete. Bill has approved the prose and the `spin14` walkthrough. The [reconstruction receipt](notes/ch14-student-acceptance.md) records the checks and their limits. Layout-specific production handles
are a separate representation improvement, not a prerequisite for this guarantee.

## Introduction

Until now, to my knowledge, Rust has been the only practical memory-safe
systems programming language that doesn't require a tracing garbage collector.
Unfortunately, it comes with a straitjacket. You organize your program around
its ownership rules, or you pay for a way around them—often with reference
counting.

That is not how I want to build software. Both the Ensemble AI coding agent and
the Lyric compiler use a hub-and-spoke module layout with explicit relationships
between objects. Ordinary objects keep back-pointers to their parents. An object
can belong to more than one parent, and those connections are part of the model,
not something to hide or reconstruct when needed. This style of interconnected
objects runs against the grain of idiomatic Rust. I do not want to redesign my
data structures to please a borrow checker, or reference-count objects whose
lifetimes their parents already determine.

In short, Rust is a pain. Lyric is the solution.

This chapter takes on the milestone that makes that statement matter:
single-threaded memory safety. Not just fewer bugs, and not just a debug mode
that catches some dangling references. I want to build the data structures I
actually need and have the compiler enforce safe access to them. I have never
succeeded at this myself before. Getting it right here takes us beyond what I
have been able to build so far.

We are not stopping at a demonstration. We'll self-host within a week. I want
to use Lyric to build Lyric, with the same explicit relationships and readable
architecture that brought us this far.

Lyric does not have all the support Rust has today. But in the age of AI, that
matters far less. Have your bot wrap or port what you need, test it, and publish
it for others to use on the upcoming lyriclang.org site. Every useful library
we share makes the next project easier. We do not need to wait for someone else
to build the ecosystem we want.

## TL;DR

- The goal is single-threaded memory safety for the supported safe language:
  valid, initialized, correctly typed storage, or failure before invalid access.
- Establish that guarantee across initialization, lifetime effects inside calls,
  ownership transfer and cleanup, relationship teardown, and checked runtime access.
  Audit actual enforcement paths and challenge them with positive and negative tests.
- Unsupported features must be rejected explicitly; debug diagnostics and passing
  tests alone do not replace the safety argument.
- Structural links represent a relationship; they do not each own a reference.
- A counted `refs` attachment owns one reference, accounted for by add/remove.
- Ordinary containers own their counted elements, even when those elements were
  copied from structural links inside relationship maintenance.
- Scalar participant borrowing inside maintenance remains unchanged. It does not
  propagate into ordinary array or tuple elements.
- No new canonical types, per-element runtime ownership tags or mixed-view
  restriction are required for this increment.

## 1. The destination determines ownership

Suppose one counted child is attached and no other holder owns it. The attachment
contributes one reference. `first`, `last`, `next`, `previous` and the parent
backlink are structural links; assigning those links does not add independent
counts. The relationship's add/remove operations account for attachment instead.

Inside maintenance, an inferred scalar participant local can borrow a handle.
Ordinary application copies, however, acquire ownership. An ordinary container
also acquires ownership, even when constructed inside maintenance:

```lyric
let saved = [self.first!, self.first!]
```

Both entries identify the same object; neither clones it. They contribute two
ordinary references in addition to the attachment. Removing the attachment leaves
the array keeping the child alive. Cleaning up the array releases both elements.
The null assertion `!` checks the source handle; it does not decide the ownership
of the destination.

An explicitly concrete scalar owner followed by an array copy has another owner:

```lyric
let owned: Node = self.first!
let saved = [self.first!, owned]
```

If all those holders remain live, the logical owners are the attachment, `owned`,
and the two array elements. Ownership-preserving transfers may avoid redundant
operations, but must not change observable finalization or permit invalid access.
There is no need to reject the array because its two inputs arrived through
different lookup views.

## 2. Reuse the existing ownership transfer

The checker formerly called `RequireAggregateElementView` for tuple/array type
annotations, literals, element assignment and array push. That temporary gate
rejected participant aggregates before ordinary typing and lowering could handle
them. Remove that gate and its calls; retain type compatibility, writable-root
protection and lifetime checks.

`IR/Lowering.cs` already has the required owning-boundary operation in `Transfer`:

1. Values with no owned contents need no ownership transfer.
2. A borrowed counted scalar observation first acquires a holder through `Ref`.
3. `Move` transfers that holder to its installing consumer and empties the source.
4. Cleanup can safely visit the emptied temporary without releasing another count.

`NewArray` and `NewTuple` already transfer their operands through this path. A
container therefore owns each counted element without changing the canonical
class type or teaching the emitter a separate ownership rule. Existing recursive
copy/drop handles nested storage and retained class handles normally. Ordinary
aggregate helper parameters/results also use ordinary ownership; the scalar
participant-parameter exception does not apply to an array or tuple.

This increment adds no stored compiler relationships and needs no new model
diagram. Existing semantic endpoint identity still governs scalar maintenance
privileges; it is not reconstructed from concrete class names.

## 3. Check lifetimes through finalization timing

The public fixtures remove the last attachment while a container retains its
child, print `after`, then let the container clean up. The expected output includes
`after` before the child's finalizer marker. A borrowed-container implementation
would finalize too early. A leaked owner would omit or delay the finalizer beyond
its expected point. Copy controls clear the original and retain the independent
copy; helper controls return an owned aggregate to their caller.

These are behavioral checks, not direct measurements of every intermediate count.
They do not prove arbitrary relationship algorithms correct. Invalid lifetime
programs must not be executed merely to look for a crash.

The older restriction against direct class-handle-array fields, such as
`held:[C]`, remains: both ordinary and contributed declarations reject that
storage form with LYC306. This is not a recursive ban on tuple-nested counted
arrays, such as the finalizer-effect example below. Those forms still face
persistent-storage and lifetime checks; this distinction does not promise that
all nested forms are supported. Broadening direct field support requires an
explicit initialization, storage and lifetime audit.

## 4. Initialization follows physical fields, not labels

A contribution label is a namespace, not another object with its own readiness.
Constructors must initialize every physical nonnullable field, including fields
contributed under labels. Walk the actual field inventory; do not reconstruct it
from source declarations alone. Only nullable managed links receive the compiler's
initialized null state. A cleanup sentinel in other storage is not an initialized
language value.

Check assignment's right-hand side and type before marking the field assigned.
Reading a field needs that field initialized; copying or passing partial `self`
needs the complete receiver initialized. Check both explicit returns and normal
fallthrough. At a branch join, intersect the definitely assigned sets of paths
that continue; an exiting branch cannot supply initialization to the other path.
The public initialization suite tests these rules through labeled contributions,
while the earlier ordinary-field tests remain regressions.

Mandatory relationships are deferred to a future version, outside this book.
A mandatory parent must exist throughout the child's live lifetime, not merely
have a nonnull handle at constructor return. The intended future construction
rule passes the parent to the child constructor and calls the parent's attachment
function to establish both sides before returning. Removal and parent teardown
must also preserve that lifetime contract; those rules are not implemented here.

For this chapter, attempting direct initialization of a protected nonnullable
parent link is rejected with LYC308. Omitting the required field is rejected with
LYC328. Neither protected writes nor partial-self escape is relaxed to make such
a class constructible. These are explicit unsupported-construction boundaries,
not a nullable-only rule for all contributed fields: permitted nonnullable value
fields still require and support ordinary constructor initialization.

Generated cleanup also belongs in the effect graph. A counted element's finalizer
can run during array truncation, not only at a source-level destroy statement.
One examined candidate nested an array of counted items in a class tuple field,
then made an item's finalizer allocate that containing class. This is rejected by
the transitive own-class-finalization rule. Do not treat a cached pointer in the
emitter as safe merely because its last source-level use has passed: callbacks
and generated loop accesses remain uses. Conversely, a hypothetical emitter hazard
is not an accepted-language hole when a required checker rule already rejects it.

## 5. Preserve order through calls and cleanup

A summary saying “this function destroys a Node” is sufficient to invalidate a
borrow after a call, but not to protect a use inside that call. Consider two
parameters: one is a reference into an object's field, and the other identifies
the object. The caller may supply overlapping storage even though the callee
uses different parameter names. Destruction followed by a field write must fail;
the same write before destruction can be valid.

Retain ordered mutation/use obligations over formal storage paths. When checking
a call, substitute actual argument storage for those paths. If overlap remains
unknown because the caller is also generic or parameterized, export the obligation
rather than declaring the arguments disjoint. Iterate function summaries to a
fixed point before issuing the final diagnostics. Local control-flow analysis must
likewise merge invalidation history at joins and revisit loop back edges: acquiring
a reference once before a loop does not make it valid after an earlier iteration
resized its storage. Reacquiring it each iteration is a different program.

Recursive groups need the same ordered proof. A base case can destroy an owner:
using a borrowed field while descending can be valid, while using it during
unwinding is not. Begin returning-result summaries at bottom (no established
returning path), not an assumed unchanged alias. Grow effects, alias replacements
and ordered obligations together to a fixed point before final diagnostics. The
public direct and mutually recursive controls distinguish those two orders.


Aggregate copies containing borrowed storage references (`ref` values) preserve
their targets, not extend their
lifetimes. Track their occurrence paths through copies and helper arguments. The
public cross-call cases test nested aliases, generic specialization, forwarding,
and finalizer-induced mutation as well as direct calls. Safe-order controls are
essential: rejecting every overlapping call would conceal an inadequate analysis.

A saved field receiver illustrates why storage identities matter. A saved object
handle may survive relocation of its class table, while a saved pointer into that
table does not. Selecting a field for assignment records the receiver identity
before evaluating the right-hand side. At the eventual store, check that object's
lifetime separately from whether its physical field storage moved. Do not turn the
relocation allowance into permission to store into a destroyed receiver.

Cleanup is executable behavior. Returning an owning result first transfers it out
of its holder, then cleans the exited scopes. Break and continue clean the regions
they exit, including the loop-condition holders when appropriate. Empty moved
holders must be harmless to visit. The outgoing owner protects its object during
those cleanups; merely having no later source-level use does not justify deleting
that owner, because doing so can move finalization earlier.

Keep this sequence in typed IR. Validate its structure, operation types and
execution contracts before ownership simplification, after simplification, and
before emitting mutable IR. The emitter follows those transfers and releases; it
does not reconstruct a second ownership policy from syntax. The existing ownership
interpreter challenges this IR, while runtime output/finalizer tests challenge the
generated C. Neither substitutes for the other or for reviewing the execution paths.

## 6. Check initialized storage, not merely allocated memory

Storage safety starts before the allocation. Given an element count and a nonzero
C element size, compare the count with `SIZE_MAX / sizeof(element)` before
multiplying. Check a push or class-slot increment before adding one, and bound
geometric capacity growth before addition. Otherwise a wrapped allocation size
could produce a small buffer followed by a large write. Keep the old pointer until
`realloc` succeeds; report allocation failure rather than publishing a null
replacement. The retained Chapter7 oversized-reserve case checks the intended
size-overflow diagnostic without trying to exhaust the machine's memory. That
case is not an allocator-failure injection test.

An array descriptor separates initialized length from allocated capacity. Reserving
space changes capacity but creates no readable elements. Truncation ends element
lifetimes even when backing memory remains allocated. Safe indexing must check
negative signed indices and compare the index against length before forming the
element address, for both reads and writes. Growth must initialize each new element
before publishing it in the length. Test spare-capacity reads and stores as intended
runtime failures, and regrowth as a successful initialized-value control.

Nullable unwrap likewise checks for null before exposing the object handle in safe
mode. A checked runtime failure must have the intended diagnostic and exit status;
a crash or arbitrary nonzero exit is not evidence of protection. Never execute
these invalid inputs in unsafe mode to demand safe-mode behavior.

Debug object identities can help expose errors in trusted code, including reuse of
a freed slot. They do not establish the safe-language lifetime guarantee: an already
acquired raw field pointer is not checked by that resolver at every dereference.
Static lifetime and ordered-effect checks remain necessary in both runtime modes.

During generated teardown, only the exact active receiver's object identity is
protected until its fragments return. A child with the same class is not that
receiver. Its saved handle and borrowed field storage must still expire when it is
destroyed. Keep the retained same-class cascade success and stale-child negatives
alongside this exception; a class-wide exemption would make the example run by
silently admitting invalid accesses.

## 7. Make unsupported effect boundaries explicit

The current checker analyzes statically selected bodies. A function-valued parameter
is rejected at type admission, and a function without an implementation is rejected
before body analysis and lowering. Do not interpret either as an opaque call with
no effects. Future callbacks or foreign functions must supply a conservative effect
contract or remain rejected wherever safety depends on unknown effects.

Reference-containing local structs and tuples carry their alias targets through
copies and calls; they do not own those targets. The current endpoint rejects
borrow-containing function results and array elements at type admission, including
empty arrays and generic specializations. For permitted initializers, preserve the
result graph through cleanup: a field address obtained from a temporary counted
owner expires when that owner is cleaned up. Returning an ordinary object result
also requires checking its validity after local finalizers run, not only when the
return expression is evaluated.

## 8. Make the physical layout match its purpose

The previous backend used a 64-bit index for every object in both layouts. User
fields followed AoS or SoA, but allocator metadata always lived in an array of
structs. This chapter replaces that compromise. Ownership remains expressed by
neutral IR; the backend changes where a handle points, not who owns a reference.

For production AoS, forward-declare each nominal record type before defining
aggregate layouts or function signatures. A class value becomes a pointer to that
record. This permits mutually referring classes and tuples containing handles
without erasing nominal types through `void *`. Put the next-free pointer and,
for counted classes, the 64-bit reference count in the record beside its fields.
There is no separate production AoS metadata table.

Keep the existing fixed 64-record chunks. The allocator's high-water position
selects a chunk and record only when extending the population. Return that record's
address. Normal field access, retain and release follow the pointer directly;
they never recover an integer index from the address. Reclamation links the record
into the free list only after all cleanup finishes. Allocation takes a free record
before extending the chunks. Directory growth does not move existing records.

For production SoA, use 32-bit unsigned handles, reserve index zero, and start the
high-water mark at one. Store each declared field in its own array. Apply the same
layout rule to generated metadata: next-free links have a 32-bit array, and counted
classes have a separate 64-bit count array. The free-list head is one scalar per
class. A metadata struct per object would defeat this layout decision.

Keep the high-water counter wide enough to represent one past `UINT32_MAX`.
Check exhaustion before narrowing; permit every nonzero 32-bit index. Reuse a free
slot before checking whether a fresh slot can be allocated. This matters at the
representation limit: existing free slots are still usable. The public probe
sets that private counter to its exhausted value in generated C, verifies reuse,
then keeps the reused object live while requesting a fresh slot. Expect the exact
allocation-overflow diagnostic without trying to allocate billions of objects.

Centralize physical handle type and empty-value selection in the emitter. Apply
those selections to parameters/results, saved field receivers, membership-release
temporaries, nullable conversions, cleanup holders, and aggregate components.
Counted-array leaf strides must use the selected handle type, not a hard-coded
`sizeof(uint64_t)`. Array lengths, reference counts and arithmetic operands do not
become 32-bit merely because object indices do. Production null/empty is `NULL`
for AoS and zero for SoA; cleanup-safe bits still do not initialize a source
nonnullable value. Typed field initialization and dirty-slot regrowth remain required.

Debug mode deliberately retains monotonically fresh 64-bit allocation identities,
its identity-to-slot map, and the old empty sentinel. Its metadata is diagnostic
storage, not the production format or a benchmark configuration. Preserve the
identity checks after reuse and allocation growth. The Chapter 14 successor to the
debug-mode grader changes only how its injected C probe reaches metadata: it still
checks destruction enters with count one, balanced temporary owners leave count
one, release empties its holder, and removing the destruction hold is detected.
The historical Chapter 13 grader remains available for that earlier endpoint.

The goal is winning representative benchmarks. Direct pointers and compact indices
are design choices toward that goal, not evidence that we have won. The current
checks establish representation, finalization and allocator behavior at C O0/O2;
they do not measure speed or peak memory. Source acceptance remains conservative
across layouts: AoS pointer stability does not exempt a field borrow from the
allocation effects that the same program must respect in SoA.

## Required safety audit

The central milestone of this chapter is single-threaded memory safety for the
supported safe subset, not merely completion of its patches. Assuming correct
compiler/runtime and trusted relationship algorithms, accepted programs must access
valid, initialized, correctly typed storage or fail before an invalid access.
Explicit unsafe operations and foreign-code contract violations are excluded.
This is not leak freedom, concurrency safety, or a formal proof.

As part of reconstruction, audit your actual implementation against Sections 5
and 12 of the language specification. For each obligation below, identify the
checker and lowering/runtime paths that enforce it, the public positive and
negative tests that challenge it, and any remaining gap:

- Initialization, nullable access, initialized-length bounds, and typed storage.
- Allocation-size arithmetic and failure: check count increments and byte-size
  multiplication before allocation or access, and publish replacement storage only
  after allocation succeeds. Spare capacity is not initialized storage.
- Ordinary-object invalidation and maintained persistent relationships.
- Counted ownership at copies, transfers, replacement, and every cleanup exit.
- Stable storage through aliases, nested aggregates, and backing-storage changes.
- Left-to-right evaluation, saved receivers and arguments, branches, loop back
  edges, and recursive calls.
- Owner provenance and ordered effects inside callees, not merely invalidation
  after they return; include generic specialization and reference parameters.
- Effects of generated cleanup, final hooks, membership release, and destruction
  cascades. Protection of an active teardown receiver must not protect destroyed
  children or other objects of the same class.

Read declarations and execution paths; passing tests alone is not this audit.
Challenge uncertain paths with small positive controls and compile-only unsafe
cases. A negative passes only with its intended diagnostic, never a crash or an
unrelated rejection. Do not execute a potentially invalid program to see whether
it crashes. Unsupported features may be rejected explicitly; partially accepting
an unsafe feature does not satisfy the milestone. Pointer versus index handle
representation is not itself evidence of safety.

Record the findings with source locations and actual test receipts. Resolve each
identified hole by correcting enforcement or by an explicitly agreed rejection
boundary. Report unresolved obligations rather than claiming chapter completion.
Use the exact commands in the [public grader guide](../graders/ch14/README.md).
Run the complete retained suite in both compiler configurations after the focused
checks. In your audit record, map at least these enforcement paths to evidence:

- Physical-field initialization and persistent-field admission in the checker:
  Chapter8 initialization/lifetime cases, Chapter 13 persistent-field refusals, and
  Chapter 14 `safety_initialization.py`.
- Ordered effects, recursive summaries and contained aliases in the checker:
  Chapter8 lifetime cases, Chapter10 `borrowed_structs.py`, and Chapter 14
  `safety_calls.py`. Include valid last-use controls, not only refusals.
- Ownership transfers, cleanup exits, membership accounting and destruction order
  in typed IR: Chapter9 ownership checks and Chapter 13 participant/relationship
  tests, plus Chapter 14 ordinary-container executions.
- Runtime initialized-length and nullable checks in emission: Chapter7 array
  failures, Chapter12 nullable cases, and Chapter 14 `safety_access.py`.
- Unsupported opaque/indirect calls at admission: Chapter 14
  `safety_boundaries.py`. Do not report rejected syntax as supported functionality.

Use the corresponding paths in your own implementation, not these filenames as
an architectural substitute. Explain why the paths enforce the requirement and
what the tests leave unproved. This record is a required exercise deliverable;
it is not a formal proof or a substitute for the other chapter checks.

## Exercises — demonstrated increments

Starting from accepted Chapter 13, implement the owning-container rule and the
layout-specific production handles above. Complete the required safety audit.
Use the documented commands and exact comparison contract in
[the Chapter 14 grader guide](../graders/ch14/README.md). All named cases in that
suite are retained. Preserve the Chapter 13 scalar relationship and participant-IR
checks. The cumulative Make target uses Chapter 14 successors for the temporary aggregate
restriction and the metadata-specific debug probe. Historical Chapter 13 graders
remain available for that endpoint; their behavior controls are preserved by the
successors. Run the new handle contract in both compiler configurations too.

### Run the required checks

Work from the project root, starting with the accepted Chapter 13 compiler and
readable public graders through Chapter 14. Use installed Python 3, the .NET 10
SDK, a C11 compiler, and the already-installed grammar tooling described in
Chapter 0. No download or installation is part of these commands. Set `DOTNET`
to your SDK executable and `CC` if your C compiler is not the default `cc`:

```sh
DOTNET=/path/to/dotnet
make DOTNET="$DOTNET" PYTHON=python3 test
```

For graders outside your solution workspace, add
`GRADERS=/absolute/path/to/graders` to the same command. Supply your own compiler
implementation, not an existing chapter solution. The target builds both Debug
and Release and must finish with exit 0 and every retained suite passing. The
[public guide](../graders/ch14/README.md) gives focused commands, comparison rules,
and retained case inventories. An intended rejection needs its specified
nonzero exit and diagnostic; a crash or timeout is not a passing rejection.
Do not run known-invalid unsafe programs.

Submit your implementation, the required source-audit enforcement map above,
actual test results, and explicit unresolved limits. Required work includes the
ordinary-container ownership cases, layout and allocator checks, preserved debug
contracts, initialization and ordered-lifetime checks, and all earlier retained
regressions. The mandatory-parent rejection cases preserve an unsupported boundary;
implementing mandatory relationships is not an exercise in this book. The source
audit and walkthrough are required alongside graders, not replacements for them.
Human acceptance remains separate from automated success.

## Take it for a Spin

Run from the project root with the same installed SDK and C compiler:

```sh
make DOTNET=/path/to/dotnet PYTHON=python3 spin14
```

For Bill's installed SDK, the command is:

```sh
make DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet spin14
```

This builds Release and prints the complete example from `tools/spin14.py`.
The example attaches a counted child, lets its original local owner leave scope,
and enters a relationship method that copies the child into an ordinary array.
Removing membership releases the attachment's reference, but the array still owns
its element. The final hook therefore runs at array cleanup, not at removal.
The example then truncates and regrows an integer array, checking that its regrown
element is initialized, and destroys the now-empty parent.

The script emits and runs C at O2 for AoS and SoA, each in production and runtime
debug mode. Expect these six lines once under each of the four mode headings:

```text
container keeps detached child alive
container cleanup finalizes detached child
owning container scope complete
regrown elements initialized
parent final sees empty relationship
parent teardown complete
```

Finally, the script prints a separate program that passes a field borrow and its
owner to a function which destroys the owner before writing through the borrow.
It checks that program only: expect LYC326, followed by
`Invalid borrow rejected; program was not executed.` The diagnostic includes the
temporary source path. No C executable is generated or run for that candidate.

The script verifies exact successful output and empty stderr, plus the intended
compile-time rejection, and exits nonzero if either differs. All source, C, and
executables created for the demonstration live in a temporary directory and are
removed on exit. The ordinary Release build products remain in the compiler build
directory. There are no credentials, network services, paid calls, installations,
or persistent application data. The walkthrough is not a performance benchmark,
formal proof, or exhaustive test of trusted relationship algorithms.
