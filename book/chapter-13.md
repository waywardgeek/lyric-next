# Chapter 13 — Maintained Relationships and Generated Teardown

## The model we extend

Use [Chapter 12's model guide](chapter-12.md) for the existing relationships.
This increment adds no new stored compiler-object edges. It extends what the
existing objects mean and what operations may do:

- **`MultiClassInstantiation`** remains the analysis/template-owned specialization.
  Its immutable `RelationshipPolicy` now distinguishes a policy-free binding from
  `owns` or `refs`. Its ordered `ParticipantBinding` objects preserve endpoint
  occurrence identity, even when both endpoints use the same class.
- **`SemanticSymbol` and `SemanticScope`** continue to own contributed declarations
  and their lexical structure. Queries over those relationships identify protected
  links, participant parameters/locals, designated operations, and hidden teardown
  fragments. Do not copy those answers into independent mutable flags.
- **`IrFunction`, `IrNode`, and `IrOperand`** remain the executable graph.
  Membership accounting is explicit in this graph. The scalar `ObservesReceiver`
  calling contract records proven receiver borrowing; it is not a new object link.
- **`ParticipantRole`, `RelationshipPolicy`, and `RelationshipOperation`** are
  value-like enum designations, not allocated relationship objects. Runtime debug
  allocation identities belong to emitted C storage, not the compiler's semantic
  ownership graph.

## Introduction

This is where we bring relationship declarations to life.

In the previous chapter, we taught Lyric how one generic algorithm could contribute
fields and methods to several cooperating classes. We could put a linked list
directly into our own objects, without wrapping them in somebody else's container.
That was exciting, but the programmer still had to coordinate the relationship's
lifetime. Now the compiler takes on that coordination.

I don't want to write another error-prone destructor, and I don't want you to have
to write one either. I want to declare how my objects relate and let the compiler
assemble the cleanup. Destroying an object should detach it from all its parents,
clean up its children according to the declared ownership policy, release its
ordinary owned fields, and reclaim its storage—in the right order, every time.
Adding another relationship should not mean remembering to patch a destructor
somewhere else.

This is especially important when an LLM is writing the code. The hard part of a
destructor is often remembering some pointer to the object in an obscure part of
the program. Miss that connection, and the destructor can look perfectly sensible
while leaving a dangling reference behind. An LLM has trouble keeping every detail
of a growing codebase in context at once. I don't want correctness to depend on
whether it happened to remember that one connection. I want the relationships
recorded in the program, where the compiler can use them.

An owning relationship destroys its ordinary children when the parent is destroyed.
A reference relationship keeps counted children alive while they are attached.
Removing a child updates the links before releasing that attachment's ownership.
Destroying an object runs its final hook before taking apart its relationships,
so application-specific final actions remain possible without making that hook
responsible for orchestrating memory cleanup.

We still have to write and test the reusable relationship algorithms. The compiler
does not invent a correct linked list for us: its author supplies the operations
that connect, disconnect, and drain it. But application programmers using that
relationship should not have to write another custom destructor for every class
that participates. We concentrate the difficult code in a reusable implementation
instead of repeating it throughout the application.

That is a big deal to me. Relationships are everywhere in the programs I write,
but most languages make me express them as a collection of fields and methods
whose connection exists mainly in my head. Here, ownership and teardown become
part of the relationship's contract.

We are not finishing every part of Lyric's memory-safety story in this chapter.
This increment supports scalar participant handles; aggregate participant borrowing
and the remaining single-threaded safety work belong to the next patch chapter.
But we are making one of its most important pieces work.

## TL;DR

- Declare exactly one parent and one child role; choose `owns` or `refs` at binding.
- Check refusal conditions in `prepare`, before changing links or attachment counts.
- Let one counted `refs` attachment own one reference. Structural links and scalar
  participant values inside that relationship borrow; ordinary owners still count.
- Emit teardown as final hook, all parent detachments, child cleanup, ordinary
  field cleanup, then reclamation. Reread maintained endpoints after cascades.
- Keep nullable checking, definite initialization, ownership, and write permission
  separate. Empty cleanup storage is not an initialized nonnullable value.
- Add debug allocation identities to detect stale object handles after slot reuse,
  without changing production handles or claiming raw-address tracking.
- Preserve earlier behavior with explicit migration tests, and run the cumulative
  suite. Focused list success is not whole-language memory-safety evidence.

## From contributed members to a maintained relationship

Chapter 12 gave cooperating classes coordinated fields and methods. This chapter
adds a lifetime contract to an explicit binding. A relationship is not a hidden
runtime object: its state is the contributed fields in the actual participants.
For example:

```lyric
interface DoublyLinkedList<parent P, child C> {
    // P contributes first/last and append/remove.
    // C contributes parent/previous/next.
}
impl DoublyLinkedList<Graph:nodes, Node:graph> owns {}
```

The roles belong to endpoint occurrences, not concrete classes. The same class
can occupy both roles, and independently labeled bindings remain independent.
Keep the template, ordered participant bindings, labels and selected policy in
specialization identity. Reuse Chapter 12's atomic placement checks: validate all
contributions before publishing any of them. An unsuccessful binding must not
leave a field, scope, or reverse membership behind.

`owns` permits ordinary children and cascades destruction to them. `refs` does
not destroy ordinary children; when the child is counted, each attachment owns
exactly one reference. `owns` with a counted child is rejected. A backlink never
keeps the parent alive. These policies do not make arbitrary list algorithms
correct: the relationship author must preserve reciprocal links, uniqueness and
reachability. The compiler supplies accounting and lifecycle orchestration around
that trusted algorithm.

### Separate preparation from structural mutation

An `add` or `remove` method has parent-role `self`, one nonnullable child-role
argument, and a unit result. Require its first statement to be `prepare`:

```lyric
pub add func append(self, child: C) {
    prepare {
        assert(child.parent == null)
    }
    // Update the child's links, neighbors, and the parent's endpoints.
}
```

The corresponding removal guard is `assert(child.parent == self)`. Check these
before any link changes. Preparation may create locals needed by the structural
body; they belong to the surrounding function scope and are not cleaned up at the
closing brace of `prepare`. Do not admit a second preparation block or an early
normal return from the structural portion. Analyze helpers and cleanup effects
as well as direct assignments when checking that preparation does not mutate the
relationship. Merely searching for assignment syntax misses a local finalizer
that calls a mutating helper.

In `IR/Lowering.cs`, the `Membership` helper emits result-free
`MembershipRetain` or `MembershipRelease` only for a counted child under `refs`.
The function's contributor identifies the binding; the child parameter is the
operand. Do not duplicate the relationship identity in every instruction.
Lower preparation inline, then emit the add retain before structural writes.
For removal, reconnect neighbors and endpoints, clear the child's parent,
previous and next links, then emit the release before ordinary local cleanup.
That release can immediately run the child's finalizer: nothing afterward may
assume the detached child is still alive without an independent owner.

The lowering work stack runs last-in, first-out. Schedule the membership operation
in the reverse order required to obtain this execution order. Inspect the actual
IR and validate the cardinality and placement of these operations; source order
alone does not prove the generated accounting is correct. Validate the actual
outer-region child sequence: preparation work precedes accounting, structural
work follows retain or precedes release, and final operation cleanup follows
accounting. Existing syntax ancestry classifies the work; the accounting node's
own origin is not proof of its position. Nested work executes with its enclosing
conditional or loop. Keep temporary statement cleanup distinct from final
operation cleanup. Test reordered instructions as well as missing or duplicated
ones, without emitting or executing corrupted IR. The emitter releases a
private handle copy for membership accounting, because its runtime release helper
empties its argument. It must not empty a caller's independent owning holder.

### Generate teardown in phases, not declaration order

A selected `partial owns destroy(self)` or `partial refs destroy(self)` is a
contributed teardown fragment, not an application-callable method. Keep the
selected fragment in the ordinary semantic function and IR inventories, borrowing
its receiver. Do not create a second registry of executable cleanup bodies.

`LowerDestruction` builds the completed object's destruction body in this order:

1. Call its final hook while its fields and memberships are still observable.
2. Call every child-role fragment to detach it from all parents.
3. Call every parent-role fragment to clean its children.
4. Release ordinary owned fields, excluding the managed scalar links.
5. Reclaim the object's storage last.

Select phases by the contributed endpoint role, not by class identity or source
declaration order. A class can be a parent in one binding and a child in another.
An owned child must enter its own destruction while still attached, so its final
hook sees the membership; its child-role fragment performs the detach. Do not
pre-detach it in the parent's drain.

The list's owning drain repeatedly destroys `self.first!`; its reference drain
repeatedly calls `self.remove(self.first!)`. Both reread the maintained endpoint
after each operation. A child may detach from several parents or trigger another
cascade, so a saved successor is not a reliable continuation. This drain is
trusted teardown code, not an implementation of the deferred safe-iterator feature.

The lifetime checker must also understand why the active fragment receiver survives
that cascade. Generated destruction keeps this object's storage allocated until all
fragments return and reclamation runs. When entering a fragment, record the exact
receiver parameter's object identity in the existing lifetime analysis. Exclude
only that identity from class-wide destruction invalidation; do not exempt its
class, other participant values, or borrowed addresses into a child. Otherwise a
same-class drain would either be rejected unnecessarily or wrongly protect a
saved, destroyed child. Direct destruction of the active receiver is refused with
`LYC334`. The public cross-membership cascade exercises the valid reread; separate
controls require `LYC330` for a saved destroyed child and `LYC326` for its stale
borrowed storage. This narrow receiver rule is not a general alias-safety proof.

Keep a handle across callbacks, not a cached pointer into class storage. Hooks
and fragments may allocate and relocate metadata or SoA fields. Resolve each field
again when releasing it, and resolve reclamation metadata after callbacks. The
existing partial-construction path must still skip the incomplete object's final
hook while cleaning successfully constructed owned values; this phase sequence
must not turn incomplete storage into an initialized source value.

## Keep persistent ordinary handles in maintained relationships

A local lifetime check cannot clear a stale handle hidden in another object's
field. Reject unmanaged persistent ordinary-object handles at declaration checking,
even if the program never exercises that field. This is the deliberate end of
Chapter 12's temporary allowance for manually detached ordinary links. Counted
fields remain legal: their holders retain their targets. Local arrays and aggregates
are not automatically persistent fields and remain subject to their own lifetime checks.

In `Semantics/Check.cs`, perform this check after ordinary fields and explicit
multi-class bindings have been registered, before method bodies. Enumerate the
actual physical class fields, including contributed fields under labels. Walk each
field's canonical stored type with a stack and a visited set: descend through array
elements, tuple components and admitted inline struct fields. A class handle ends
the walk; do not traverse its target object's fields from here. Those fields have
their own declaration check. The visited set terminates recursive stored shapes.

For an ordinary handle, require the field to belong to an explicit maintained
relationship and the target class to be one of that instance's participants.
A policy-free interface does not establish maintenance, and an unrelated ordinary
class inside a relationship is not protected by that relationship. This is a
necessary admission rule, not proof that a trusted author implemented correct
attachment and teardown algorithms. No runtime tracking is added by this check.

The public rejection controls cover scalar, nullable, tuple and nested-array
ordinary handles, policy-free contributions, and unrelated relationship fields.
The current compiler rejects struct-valued class fields earlier as unsupported;
that control is not evidence of executed struct-field traversal. Counted-field
and maintained-list execution controls must continue to pass. Chapter 12's
historical fixtures remain intact. Explicit Chapter 13 successors preserve lookup,
initialization, counted ownership, alias removal and refusal checks under the new
storage rule; see the public grader guide. The author cumulative Make target has
passed in both configurations; review, independent reconstruction and human
walkthrough acceptance remain separate completion evidence.

## Initialize nullable links and require nonnullable construction

Protected nullable relationship fields begin as source-language null. Allocation
must write the null representation into fresh and reused slots before constructor
code can observe them. At constructor entry, definite-initialization analysis marks
those nullable protected fields assigned, using the actual field symbols and the
existing `IsManagedLink` query. That is why the list's empty constructors are valid:
their nullable links already describe a detached object. The public
`relationship-detached-storage` case checks that state.

This is a specific bridge between runtime initialization and the source language,
not permission to treat every empty slot as initialized. Nonnullable fields receive
no such exemption; the retained missing-initialization and invalid-null controls
must still reject them.

Every nonnullable field must be initialized before its constructor returns,
including fields contributed by a relationship. Reads before initialization are
illegal. Allocation's internal empty sentinel permits safe cleanup of incomplete
objects; it is not an initialized source-language value and cannot satisfy this
obligation. This rule is independent of the backend handle representation.

Use the existing definite-initialization facts rather than runtime flags: mark a
field only after its initializer or assignment succeeds, require initialization
on every continuing branch, and check both explicit constructor returns and
fallthrough. Do not permit an object to leave construction with nonnullable
fields awaiting a later attachment operation.

Initialization does not by itself grant permission to modify protected links.
The relationship's construction path must also preserve its maintenance rules.
The current concrete-constructor write gate rejects direct writes to contributed
managed links; completing a valid nonnullable relationship construction path is
still unresolved. Do not mistake that implementation gap for a nullable-only
language rule, or weaken read-before-initialization checks to accommodate it.

## Borrow participant handles; own the attachment

A counted child attached through `refs` has one reference for that attachment.
The list's `first`, `last`, `previous`, and `next` fields describe it together;
they do not each own another reference. Backlinks do not retain the parent.
The same rule applies to scalar participant parameters and locals inside that
relationship implementation: they observe participants, rather than prolonging
an object's life merely because the algorithm names it. Unrelated counted
objects still use ordinary ownership.

### Decide from the semantic relationships, not the class name

Keep the participant view on each checked symbol. Inferred locals preserve the
initializer's view; explicit participant annotations select that role. An explicit
concrete annotation drops the role view. Two roles can name the same concrete
class, so comparing class names cannot establish this contract.

For a borrowed parameter, require its participant view to belong to the enclosing
function's relationship instance, its type to match that view, and its scope to
be the function's parameter scope. For a borrowed value local, require the same
instance and type agreement, then follow lexical scopes to its owning function.
Stop at the first function boundary. Exclude reference locals from this value-local
query: their storage policy comes from the actual address they alias.

For a contributed field, distinguish permission from ownership. `IsManagedLink`
protects handle-containing fields against outside writes. `IsManagedHandle`
requires an actual scalar participant view belonging to the same relationship
instance. An unrelated `Payload?` field is protected but still owns its counted
value: assignment retains, replacement releases the old value, and parent
teardown releases the field after fragments. Its cleanup effects must remain
visible to lifetime analysis. Use this narrower query consistently in store
lowering, destruction lowering, and destruction-effect propagation. A regression
should observe payload survival after the assigning scope, replacement cleanup,
and final release at parent destruction; a local-slot test alone misses this.

This is a query over existing symbols, scopes and participant bindings. It does
not need a second persistent ownership flag. A policy-free multi-class interface
is not a maintained relationship and does not gain this exception.

### Lower storage and cleanup together

A borrowed scalar local still needs a `Local` address, definite initialization,
and ordinary assignment. Do not register it as an owning cleanup holder. Reading
it produces `Load`, not `Ref`. Storing into it neither consumes a `Move` nor
releases the previous handle. The algorithm is changing which participant it
observes, not changing the attachment count.

Use one storage query for both reads and writes. Follow `Borrow` and `AddressOf`
operands to the original field or local; recognize writable participant parameters
through their declared relationship identity. Reject malformed or cyclic address
chains as evidence of borrowing. This preserves the rule through local writable
aliases without guessing from their names or suppressing ordinary reference-slot
accounting. Permissions to acquire and write these addresses remain the checker's
responsibility; this query does not grant access to another relationship's links.

Nullable conversion changes the type, not ownership. A counted scalar `Load`
remains borrowed through `WrapNullable` and `UnwrapNullable`. Such conversions
neither consume an owner nor register cleanup. Preserve safe-mode null checking
when unwrapping. At an owning boundary, explicitly acquire `Ref` before `Move`:
for example, an ordinary caller must own a returned child independently of its
attachment. Keep caller-owned argument temporaries alive through borrowed calls,
then release them immediately afterward, not at the end of an unrelated block.

Validate both directions of this contract. A borrowed local or value cannot be
an `Unref` target or an owning `Move` source. A borrowed store does not consume a
transfer; an ordinary owning store still requires one. C emission follows these
IR operations rather than rediscovering ownership from source names.

### Check the distinction, not just balanced counts

The public `graders/ch13/participant_ir.py` inspects the actual list implementation
and a nullable-local helper. It requires the attachment release, rejects ordinary
parameter/local cleanup, checks borrowed field reads and nullable conversions,
and exercises local reassignment plus an address alias. The execution suite also
routes insertion through inferred and annotated locals, nested assignment and an
alias, requiring `liveNdone`. An ordinary returned-child control requires
`heldNdone`: removal must not destroy a child still owned by application code.
An unrelated counted-slot control ensures the exception is not applied to every
counted value inside a relationship function. Commands and exact comparison
contracts are in [the public grader guide](../graders/ch13/README.md).

Do not infer safety from a borrowed handle surviving in a local. If the trusted
algorithm removes the last attachment and then uses that handle without another
owner, the local must not conceal the bug by secretly retaining it. Static
lifetime checks and the required debug diagnostics address mistakes; implicit
ownership is not a substitute for the relationship algorithm's lifetime contract.
The checks above cover scalar participants, not general aggregate/container
borrowing or complete single-threaded memory safety.

### Simplify ownership without depending on the optimization

Run structural, operation and execution validation before `SimplifyOwnership`,
then run all three again before publishing IR. The transformation changes real
operand/use relationships through `ReplaceChild`, `SetDefinition`, and
`InsertAfter`; each graph edit must preserve reciprocal links and reserve capacity
before detaching the old relationship. Do not rewrite only the emitted C.

`IR/Ownership.cs` scans snapshots because it may delete nodes while visiting a
function. It replaces an eligible `Ref` and its matching cleanup uses with `Load`
only when the remaining uses are proven observations in the same region. Its
intervening-instruction check rejects effectful operations; an observation after
a call or cleanup cannot justify erasing the owner that kept the value alive.
The proof window must include the matching cleanup itself, not stop at the last
ordinary use. Even a retained value with no ordinary uses can delay finalization;
unused ownership is not unobservable ownership. A field-loaded holder is not
automatically the same as the borrowed receiver.

Infer the scalar `ObservesReceiver` contract only for counted instance methods
whose actual self uses are nonescaping observations. A method returning self must
remain owning. Propagate known borrowed helper calls to a fixed point; unknown or
recursive dependencies without a proof remain conservative. When a newly borrowed
callee no longer consumes a receiver `Move`, pass the original holder, remove the
transfer, and insert its release immediately after the call. Keeping that holder
through argument evaluation and the call preserves both lifetime and finalization
timing. Leaving it until the enclosing block ends would change observable behavior.

Correct teardown must not depend on eliminating every temporary self reference.
At last counted release, clear the releasing holder first, decrement the count,
and, on zero, restore one destruction hold before entering cleanup. Temporary
references can then balance around that hold. Reclaim directly after teardown;
do not release the hold through the ordinary release helper and recurse into
destruction again. Debug verifies the final count and rejects repeated teardown.
This is a correctness rule, not a measured performance claim for simplification.

### Keep the scalar participant boundary explicit

This chapter does not yet carry participant views through aggregate elements.
Reject participant tuple/array annotations and direct insertion of borrowed
participant-view values into tuple/array literals, element assignments or `push`
with `LYC304`. Check annotations while resolving their child types and values
while their semantic facts still retain the participant view. Restrict value
checks to the matching maintained relationship, not every use of the concrete
class. Nested aggregate construction is checked at each element boundary.

Do not remove aggregate cleanup as a shortcut: array backing storage and ordinary
counted elements still need ownership. A relationship may explicitly bind a value
to its concrete class to obtain an ordinary owner, then put that owner in an
aggregate. Application-side containers remain ordinary owners too. The public
controls remove the child's attachment while keeping an ordinary aggregate owner,
and require finalization only when that owner leaves scope.

The next patch chapter will extend participant identity through aggregates and
replace these temporary rejection contracts with borrowed-element behavior.
The observed current aggregate defect was extra references and delayed
finalization, not a demonstrated use-after-free. Chapter 13 deliberately makes
no completed single-threaded memory-safety claim; that closure belongs to
Chapter 14. That patch chapter also plans distinct production handles—C pointers
with `NULL` for AoS, and 32-bit indices with reserved null slot zero for SoA—and
resolution of nonnullable relationship construction. Those are future changes,
not this chapter's representation or acceptance requirements. Its focused lifetime,
ownership, teardown, and access-boundary audit must precede the broader safety
milestone.

## Safe, unsafe, and debug execution

The modes differ in what we trust:

- **Unsafe mode assumes user code is bug-free.** Use it for benchmarks and for
  speed-critical code after establishing that the omitted checks cannot fail.
  Report the mode with benchmark results. Unsafe execution is not evidence that
  those assumptions hold.
- **Normal safe mode assumes the compiler, runtime, and relationship-generated
  code are bug-free.** It does not assume that ordinary application code avoids
  every invalid array index, null unwrap, or arithmetic input.
- **Debug mode helps find bugs even in the compiler, runtime, and relationship
  implementations.** It checks internal invariants in addition to the applicable
  language checks. Its purpose is broad detection, not a proof that every bug is
  detectable.

### User errors versus broken implementation invariants

Array indexing is bounds-checked in safe mode, including arrays whose elements
are object references. An invalid index is an application error that the compiler
has not promised to rule out. Explicit nullable unwrap (`node!`) is also checked
in safe mode. Unsafe mode omits these checks, as it omits the ordinary numeric
checks specified by the language. Invalid unsafe execution need not produce a
controlled diagnostic. Static typing, initialization and ownership analysis are
not runtime checks and are not disabled by this distinction.

Object-field access is different. Under the safe-mode assumptions, an object
reference cannot identify an out-of-range or freed object. Checking object-handle
bounds and lifetime therefore checks an implementation invariant, not an ordinary
array-index precondition. Such runtime checks belong only in debug mode. Normal
safe output must contain no automatically generated internal-invariant checking
code or debug-only lifecycle tracking. Unsafe output without debug instrumentation
likewise must not acquire this overhead.

### Required debug lifetime detection

Debug mode must check object-handle validity and detect use-after-free before an
invalid access, including stale references after an allocator slot is reused.
Merely checking whether a slot is occupied is insufficient: its new occupant is
not the object named by the old reference. Checking only retain/release calls does not cover reads or writes through
nonowning handles. Acquiring a borrowed field address validates its object handle,
but later dereferences through that raw C pointer are not instrumented by this
increment. This is an accepted debug-detection gap, not a relaxation of static
lifetime requirements for safe programs.

Debug checks must distinguish legitimate access during destruction from access
after reclamation. Final hooks and relationship cleanup run while the object
still exists. Repeated destruction and unbalanced destruction ownership are bugs,
not permission to run finalization twice or reclaim a still-used object.

Verification must include both layouts, owning and nonowning handle access,
reclaimed slots and reused slots, plus valid teardown controls. Do not label
these checks as detection of stale raw borrowed addresses. Invalid diagnostic
fixtures must never be executed in uninstrumented safe or unsafe builds. Verify
separately that normal output omits debug metadata and invariant checks, that safe
array/unwrap checks remain, and that unsafe output omits those user-error checks.
Do not treat a crash, arbitrary nonzero exit, or a compiler rejection as proof of
runtime use-after-free detection.

### Keep allocation identity distinct from physical storage

In debug output, give each class an append-only table mapping allocation identities
to physical slot indices. A handle is an identity, still represented as a `uint64_t`;
the allocator's free list contains slots, never identities. Allocate or reuse a
slot, initialize its storage, append its slot index to the identity table, and
return the new identity. Never recycle that identity. Reserve `UINT64_MAX` as the
existing null sentinel and fail on identity exhaustion instead of wrapping.

A resolver checks the handle against the identity count before reading the table.
It rejects a tombstone, an out-of-range slot, or a free slot. It permits the
live and destroying states, because final hooks and relationship cleanup must
still access the object. Destruction entry separately requires the live state,
so recursive destruction does not acquire this permission.

`SelectField` continues to save a handle, not an interior pointer. Generate a
resolver call whenever its storage expression is used, including address
acquisition, reads and writes. Checking only at selection would miss destruction
between selection and assignment. The same identity flows through nullable
conversions, fields, arrays, parameters and returns without a second ownership
mechanism. Retain and release resolve it before changing slot counts and preserve
the identity when returning a handle or invoking the generated destruction body.

After all cleanup, reclamation resolves the identity again: user code may have
allocated objects and moved metadata. Check the final destruction hold, mark the
slot free, tombstone the identity, then link the physical slot into the free list.
Do not keep a metadata pointer across user cleanup. A later allocation can reuse
the slot, but cannot make the old identity valid again.

This design deliberately spends debug memory proportional to total allocations,
not just live objects. Tombstones last until process exit; normal allocation
failure checks still apply. Default output emits neither the table nor resolver,
and retains the current production representation. The later AoS-pointer/SoA-u32
redesign is separate. The table supplies runtime debug identity, not another
semantic owner or a replacement for the authoritative ownership IR.

### Current evidence and remaining decisions

Public runtime-boundary probes exercise real emitted accessors and helpers.
The source program is valid; the test injects invalid C calls only into debug
output. Reclaimed and reused handles must produce exact `LYR009` diagnostics,
empty stdout and exit 1. Valid access, table growth and finalizer field reads
must succeed. These probes are not evidence that an invalid Lyric program is
accepted, nor a general memory-safety proof. Nullable unwrap now omits
the null check under unsafe mode. The revised Chapter 12 grader executes null
failures only in safe mode, inspects unsafe check omission without executing
invalid input, and runs valid nullable programs in both modes. Previous Chapter 12
results remain evidence for the previous contract; the development notes record
fresh focused verification of this agreed change.

The combination of `--debug` with `--unsafe` has not been certified by these
probes. The treatment of resource-exhaustion guards (allocation failure,
allocation-size overflow and reference-count overflow) needs explicit resolution.
Do not silently remove these existing guards or describe their current behavior
as verification of the new mode contract.

## Exercises and completion checks

Start from your accepted Chapter 12 compiler, not the author's Chapter 13 solution.
Use an installed .NET 10 SDK, Python 3, a C11 compiler, Node, and the already
installed Peggy dependency under `tools/peg`. Run commands from the solution
repository root. Do not install dependencies as a side effect of a test command.
The public graders are Python/JavaScript, independent of both C# implementation
and the planned Lyric implementation edition; neither C# nor Lyric is a grader
language here.

1. Extend parsing and specialization for endpoint roles, binding policy, designated
   add/remove methods, preparation blocks and policy-selected teardown fragments.
   Preserve atomic refusal and the existing two-way compiler-model relationships.
2. Implement protected scalar links and borrowed participant values. Make accounting
   explicit in IR, including caller-owned argument cleanup and owning return
   boundaries. Validate before and after ownership simplification.
3. Generate the teardown phases and preserve the last-release destruction hold.
   Exercise ordinary owning cascades, counted reference memberships, aliases,
   independent holders, and multiple memberships. Do not implement these as
   fixture-specific source recognition or C-only accounting exceptions.
4. Enforce persistent-field admission, definite initialization, and this chapter's
   scalar participant boundary. Keep ordinary owning containers legal. Preserve
   the public Chapter 12 fixtures; use the named migration successors rather than
   changing old expected results to conceal a new rejection.
5. Add the debug-only allocation-identity lookup and test both live access and
   stale access after slot reuse. Keep production output free of that machinery.
   Document the accepted raw-address detection limit.

Set the tool paths for your environment, then run the retained suite:

```sh
export DOTNET=/path/to/installed/dotnet
export CC=clang
make test DOTNET="$DOTNET" PYTHON=python3
```

If graders are supplied outside your solution, use their read-only root instead:

```sh
make test DOTNET="$DOTNET" PYTHON=python3 GRADERS=/absolute/path/to/public/graders
```

This command must exit zero after both Debug and Release builds and every listed
runner. The exhaustive Unicode sweep is required, even though it is slower than
focused checks. The target includes both runtime modes for the relationship
suite, debug identity controls, participant IR inspection, aggregate-boundary
controls, and the explicit Chapter 12 migration. A successful build or newest
runner alone does not complete the exercise. Keep grader source and expectations
read-only; diagnostic experiments in your own workspace do not replace them.

Run the independent grammar contracts too:

```sh
node tools/peg/test.mjs
node tools/peg/test.mjs graders/ch11/type-syntax.json
node tools/peg/test.mjs graders/ch11/matchtype-syntax.json
node tools/peg/test.mjs graders/ch12/multi-class-syntax.json
node tools/peg/test.mjs graders/ch13/relationship-syntax.json
```

Use the actual external grader path in these commands when applicable. These
commands check syntax, not ownership or executable safety. Required fixtures and
exact stream/exit contracts are documented in
[the Chapter 13 grader guide](../graders/ch13/README.md). Successful execution
requires exit zero and exact expected stdout with the stated stderr conditions;
a negative case requires its intended diagnostic, not an arbitrary failure or
crash. Never execute the deliberately invalid unsafe examples.

Behavioral cases are retained unless the public guide explicitly marks a chapter
boundary replacement. Chapter 13's temporary participant-aggregate rejections
must be deliberately replaced alongside Chapter 14's accepted contract; they
must not silently disappear. Structural self-review is also required: trace
actual identity, backlink maintenance, refusal without mutation, teardown, and
IR ownership rather than infer those properties from passing output tests.

Human walkthrough acceptance is separate. Until it occurs, report it as pending;
a student or author running the same commands is useful evidence, not the human's
acceptance. This chapter does not certify completed single-threaded memory safety.

## Take it for a Spin

From the repository root, with the installed SDK and C compiler used above:

```sh
make spin13 DOTNET="$DOTNET" PYTHON=python3
```

The target builds Release, prints the complete Lyric example from
`tools/spin13.py`, emits C with runtime debug enabled, and compiles/runs it at
O2 in both AoS and SoA layouts. It uses local temporary files only: no credentials,
network calls, installation, or external services. Override `CC` if your compiler
is not named `cc`. The temporary directory is removed on success or failure.

For each layout, after the printed source and build messages, expect:

```text
attachment keeps child alive
parent final sees attached child
child final sees cleared links
parent teardown complete
```

Read the source while following the output. The child local leaves its block
before the first message, but its attachment keeps the counted object alive.
Destroying the ordinary parent first runs its final hook, which can still observe
the attachment. The parent's reference-drain fragment removes the child, clears
its links, then releases the attachment count. Only then does the child finalize.
The last message is after parent reclamation. The script requires exact output,
exit zero and empty stderr for each executable; it does not treat a crash as a
successful diagnostic.

Try explaining why moving the release before clearing links would change what the
child finalizer observes, and why saving the next child before a cascade is not
substitutable for rereading `first`. These are discussion prompts, not invitations
to run known-invalid memory accesses. This example demonstrates the counted
reference policy; the retained suite separately covers ordinary owning cascades.

The author running this walkthrough does not replace your human acceptance.
Aggregate participant borrowing and the layout-specific pointer/u32 representation
change remain Chapter 14 work, not hidden requirements of this walkthrough.
