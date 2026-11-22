# Chapter 8 class graders

Six retained initial cases exercise ordinary constructors, scalar fields, shared
identity, methods, explicit destruction/final, and counted local/alias cleanup.
They are required behavior under development, not implemented-feature claims.
Counted cases use the initial backend's immediate-release cleanup boundary;
they do not mandate a total ordering of unrelated finalizers for all Lyric backends.

From the repository root:

```sh
python3 graders/ch08/run.py /absolute/path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

The Chapter 5 execution harness requires successful C emission and compilation,
then exact stdout, empty stderr and exit0 at both O0 and O2. Each process is bounded
by its existing timeout. All fixtures are readable and student-read-only. The additional checks below cover lifetime
rejections, slot reuse, and AoS/SoA equivalence. Full cumulative verification and
Chapter 8 completion remain separate obligations.

## Initial layout execution checks

From the repository root, after a successful Debug build:

```sh
python3 graders/ch08/layout.py /Users/bill/.local/share/ensemble-dotnet10/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

Substitute your SDK executable as needed; no installation is performed. Requires
an installed C compiler (`CC`, default `cc`). Forty-nine retained successful cases each run in AoS
and SoA at `-O0` and `-O2`: 196 successful executions. Two default-growth failure
cases also run in both safe and unsafe modes, both layouts and both optimization
levels: 16 expected failures, giving 212 executions in total. Compiler success requires exit 0 and
empty stderr; generated executables require exit 0, stdout exactly `ok`, and empty
stderr. Temporary sources and executables are removed automatically. These cases
check construction, shared identity, growth, field selection after RHS growth,
explicit destruction, reconstruction, helper destruction, and final hooks
(including other-class destruction and same-class growth during the hook). Counted cases also check alias mutation, owned method receivers, returns,
conditional transfers, empty holders, self-assignment/replacement, loop exits,
and computed receiver cleanup. Allocation balance is checked separately below.
Cleanup-order cases exercise this immediate-release backend; they do not impose
a universal total order on unrelated finalizers.
The layout option is `--emit-c --soa FILE`; ordinary `--emit-c FILE` selects AoS.

### Class-wide destruction checks

`check.py` now also checks ordinary `object.destroy()` and its class-wide lifetime effects. A subsequent use of a potentially invalidated object reports `LYC330: Object reference may have been invalidated.` This includes aliases, saved call arguments and field destinations, conditional destruction, transitive helpers, and construction's implicit receiver result. Fresh construction may establish a new usable handle but cannot revive an old alias. Allocation without destruction preserves indexed handles. Explicit destruction of a counted value object reports LYC331; destruction takes no arguments.

These are retained static checks. Separate layout tests now exercise scalar ordinary-class destruction and reconstruction in both AoS and SoA. Ordinary final hooks now run before reclamation. Owned scalar/nested-array/string fields now copy recursively on reads and release after finalization. Counted scalar holders now retain on copy, transfer on moves, and release on cleanup; scalar and array-field borrows are supported. Ordinary and counted class-handle arrays are supported in locals, parameters, returns and nested arrays; object-to-object fields remain outside this chapter. These checks are bounded evidence, not a proof of the complete lifetime system.

## Free-slot allocation check

```sh
python3 graders/ch08/allocation.py /absolute/path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

This retained check wraps real generated-C `malloc` and `realloc` calls. One
create/destroy cycle and 257 cycles must perform the same positive number of
allocation calls, separately for AoS/SoA and O0/O2. Successful executions require
exit 0, empty stdout, and only the wrapper's decimal count on stderr. This checks
free-slot reuse without depending on private generated names or exposing indices
in Lyric. It does not establish leak freedom or shutdown storage reclamation.
Temporary files are removed; `CC` and the prerequisites above apply.

## Final-hook contract

A class has at most one final hook, taking only `self` and returning unit. Source
calls to the hook are rejected (LYC332); only generated destruction invokes it.
Use an ordinary method for application-controlled close operations. Invalid hook
signatures, duplicate hooks, and final constructors report LYC333. Transitive
destruction of the hook's own class reports LYC334, including cycles through
other classes' hooks. Permitted other-class destruction effects also invalidate
caller handles; generated invocation is not invisible to analysis.

Runtime layout cases verify that fields remain usable during the hook, bare return
continues teardown, and allocation during a hook does not leave stale backing or
metadata pointers. Slots are marked destroying before the hook and become reusable
only after it returns. Defensive invalid/reentrant destruction is fatal (LYR009);
this guard is not a substitute for static rejection or a generation-based handle.

## Owned-field cleanup check

```sh
python3 graders/ch08/ownership.py /absolute/path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

This retained check wraps real `malloc`, `realloc`, and `free`, counting outstanding
blocks after one and 257 construct/replace/finalize/destroy cycles. Each workload (ordinary explicit destruction, counted scope cleanup, and counted arrays),
in each layout at O0/O2, must leave the same positive count: class backing is retained, but nested
array and string payloads must not accumulate. It checks self-assignment,
replacement, and copies read inside final hooks. Each executable must exit zero,
write empty stdout, and write only the decimal count on stderr. The C compiler,
timeouts, temporary cleanup, and compiler invocation follow `allocation.py`.
This bounded balance check is not a claim of universal leak freedom or class
backing reclamation at shutdown.

## Counted checking boundaries

```sh
python3 graders/ch08/check.py /absolute/path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

The 154 retained cases require exit 0 and empty output for accepted programs, or
exit 1, empty stdout, and the listed diagnostic code and message for rejected
programs. They include implicit release effects at replacement, return, block and
loop exits, short-circuit right operands, and else-if condition cleanup. Empty
holders have no finalization effects. A retained counted alias remains usable;
ordinary objects affected by a possible final hook do not. Conservative effects
can reject programs whose exact alias counts would prevent finalization.

Optional bounded sanitizer check (requires a C compiler with these sanitizers):

```sh
CC='cc -fsanitize=address,undefined -fno-sanitize-recover=all' python3 graders/ch08/layout.py /absolute/path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

This uses the same public cases and exact result contract. It is not a leak-freedom
proof or a substitute for the required graders.

## Scalar field references

`ref object.field` selects the actual initialized scalar storage, not a copied
value. A `let` object handle does not make its fields immutable. A still-needed
field reference cannot cross allocation of its class, including allocation in a
helper, constructor, or final hook. This conservative rule is identical for AoS
and SoA, even though AoS chunks do not move. Object index handles survive growth;
plain field assignment saves an index and resolves its destination after the RHS.
Destruction invalidates owner-dependent field references. Rejections use LYC326.

The checks retain last-use acceptance, fresh acquisition, unrelated-class growth,
reference forwarding, ordered use-before-growth helpers, later-argument effects,
constructor allocation, and loop backedges. A helper receiving `ref i32` does not
know whether its caller supplied a local or a class field; ordered obligations
are checked against the actual storage at the call. Array-field descriptor and payload dependencies are specified below.

## Array-field storage dependencies

The 154 retained checking cases include array-field references and mutations.
A field descriptor is class-backed storage; an element or nested descriptor is
in separately allocated array payload. Class growth invalidates descriptor
borrows in both layouts, but not independently allocated payload borrows.
Replacement or growth of that payload invalidates its interior references;
class destruction invalidates all owner-dependent depths. Descriptor aliases
survive payload replacement. Last-use and unrelated-field controls must pass.

Helper summaries carry field identity and nesting depth, including ordered
mutation-before-use obligations when a formal reference could alias a field.
Constructor stores into fresh self remain locally checked but do not imply
replacement of older instances' payloads. The constructor-local negative case
ensures this distinction does not suppress local borrow checks.

The layout cases execute writes through field aliases, payload references across
repeated class growth, descriptor aliases across replacement, nested descriptors,
and field capacity/reset operations, in both layouts and optimization levels.

## Object arrays and non-defaultable growth

An array copy creates independent slots and nested array values, not new objects.
Ordinary handles do not retain objects; later element reads/copies must respect
class-wide destruction, including helper calls and slot reuse. Whole-array
replacement can establish a fresh value without reading stale old handles.
Counted elements retain on copy and release on removal or recursive cleanup.
Element reads produce their own counted holder before a temporary parent array
is cleaned up. Transfers into literals, bindings, calls, returns and value tails
empty the moved temporary. Inline `len` keeps its evaluated array temporary in
the caller's scope; `capacity` observes selected storage without copying it.

Non-nullable object handles have no default value. `reserve` changes capacity
without constructing objects; `push` supplies a valid handle. `resize` may shrink
but must fail before increasing length. Nested array elements default to empty
arrays, so `[[C]]` can grow its outer length without constructing a `C`.
The runtime failures require exit 1, empty stdout, and exactly the located
`LYR011: array element has no default value` diagnostic followed by the sole
`main` frame. Source line and column must be positive; no arbitrary failure,
crash or extra diagnostic is accepted. This guard remains in `--unsafe` mode.

The static fixtures test release effects from shrink, replacement, scope exit
and temporary cleanup, including short-circuit branches and builtin calls.
Known-empty literal arrays have no counted leaves to finalize, but later pushes,
nested stores and helper mutations may populate them. The analysis remains
conservative about unknown lengths, aliases and class-wide destruction; these
cases are not a proof of all lifetime behavior.
