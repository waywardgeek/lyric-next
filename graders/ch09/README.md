# Chapter 9 — structured IR and C emission contracts

Implementation languages are C# and the planned Lyric edition. Grading logic is
Python, outside both solution languages. Students may read this entire directory.
The grader accepts an independently supplied compiler command; it does not read
implementation source, use generated symbol spellings, or invoke a reference compiler.

From the repository root, with Python 3 and a built compiler:

```sh
python3 graders/ch09/ir.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

`make test DOTNET=/path/to/dotnet` includes the retained cases in both configurations.
The historical `--scalar-milestone` option remains accepted, but its temporary
rejection cases are now retired; it runs the same retained scalar checks.

Success requires exit 0, empty stderr, and one complete JSON document on stdout.
Object-key order, whitespace, and the particular node ID numbers are immaterial.
Array order is significant. Failure requires exit 1, empty stdout, and the specified
single located diagnostic with the exact code/message and a self-consistent byte,
line, and column. Crash, timeout, or unrelated failure does not count as rejection.
Each compiler invocation has a 30-second timeout. All cases must pass.

## Inspection contract

`--inspect-ir [--unsafe] FILE` checks a library (main is optional), lowers it, then
writes format `lyric-ir-1`. No output is published if checking or lowering fails.
Unsafe policy is explicit in the top-level `unsafe` flag. Layout selection does not
belong to this representation; `--soa` is not an inspection option.

`functions` contains source functions, class methods, and generated destruction bodies.
Each has `kind` (`Source` or `Destruction`), `name`, document-wide `symbolId`,
`ownerClass` (null for top-level functions),
`body` (a node ID), and a flat `nodes` array. Each node has:

- `id`, `kind`, `role`, `result`, and `type` (null for no typed result).
- `symbol`: the selected declaration's name for display, never re-resolution.
- `symbolId`: selected declaration ID, `ownerClass`: that declaration's class ID,
  and `classId`: the result type's nominal class ID; null where absent.
- `operator`: lexical operator spelling, or `widen` for implicit conversion.
- `offset`: original syntax byte anchor; -1 denotes unanchored structural syntax.
- `literal`: decimal integer, `true`/`false`, or null (including unit).
- `bytes`: numeric byte payload for a literal NewArray; empty for other nodes.
- `children`: ordered structural children; `operands`: ordered definition IDs.
- `target`: actual loop ID for break/continue; otherwise null.

Node IDs have meaning only within one function. Symbol IDs are document-wide.
All IDs are output labels, not
persistent model identity. Model relationships use actual objects. Function identity
is the selected semantic declaration, not the display name or a parameter signature.
Calls select functions by symbolId, never by display name: different classes may
have identically named methods. These output IDs do not add a compiler name table.

A function body begins with parameter Local or Borrow address definitions in declaration
order. Local/Load/Store distinguish storage from value snapshots. Calls evaluate
arguments left to right. Binary operands also retain their own types: comparisons,
shifts and checked arithmetic must use the language's existing numeric contracts,
not blindly apply C's usual conversions. Implicit whole-range conversions at binding,
argument, return and branch-result boundaries are explicit Convert/widen operations.

If consumes a boolean operand, executes only its Then or Else region, and receives
that region's Yield value if it continues. A returning/transferring arm does not
yield. Short-circuit boolean operations use the same structure. While has Condition
and Body regions: Condition yields a boolean on every test, Body repeats on ordinary
completion, continue repeats the test, and break exits its actual target loop.
Function Return differs from region Yield. A for-loop vocabulary exists, but the
current semantic language subset does not yet admit for-loops; this refactor does
not add them.

The grader's small scalar interpreter tests the control-flow contract independently
of C emission. It is not a full Lyric interpreter or evidence that ownership,
numeric failure behavior, or IR-backed C emission is implemented. Structural checks
and concrete execution expectations are both required. A deliberately reordered
snapshot must fail the grader self-check.

## Inventory

Stable retained IDs (prefixed `ch09-ir-`): constant, snapshot, call-order, short-and,
short-or, value-if, else-if, loop, compound, widen, cast, unit, branch-return,
recursive, skipped-call, assert, deep, semantic-first, condition-break,
condition-continue, condition-return. `deep` lowers 1,500 nonconstant
operations; constant folding cannot disguise recursive traversal.

The milestone-only unsupported-array, all-class, counted-class, and object-array
rejections are retired in favor of positive ownership checks. Their historical
baselines remain preserved; they were implementation limits, not language rules.
All source fixtures, expected execution results and comparison rules are in `ir.py`.

No full Chapter 9 completion claim: ownership lowering and IR-only C emission are
implemented and reference-tested, with bounded validation. Chapter prose is drafted;
reviews, independent reconstruction and human acceptance remain.


## Initial array ownership contract

Run from the same repository root, supplying any independently built compiler:

```sh
python3 graders/ch09/ownership.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

The same exit, stream, JSON and timeout contract applies. All cases in `ownership.py` are
retained, with stable prefix `ch09-ownership-`: literal, copy, replace, return,
parameter, nested, branch, early-return, loop-repeat, loop-break, loop-continue,
loop-return, loop-nested-return, loop-false, loop-nested, index-order, index-read,
index-write, index-copy, index-nested, index-replace, index-call, ref-local,
ref-forward, ref-array, ref-copy, ref-return, ref-index-order, push, push-copy,
reserve, resize, shrink, truncate, resize-zero, mutate-ref, mutate-nested,
capacity-reserve, capacity-copy, capacity-truncate, capacity-resize-zero,
capacity-nested, capacity-computed, string-bytes, string-utf8, string-empty,
string-copy, string-return, string-nested, string-loop, plus the thirteen class
cases listed below. `make test` runs them in both builds.
The evaluator counts simulated live allocations, recursively copies nested
arrays, and rejects leaks, double releases and replacement without prior release.
Self-checks must reject a shared read substituted for Copy and removed cleanup.
This is independent IR evidence, not evidence about the C allocator.

Array Local, NewArray, Copy, Call and If results represent owning holders. Local
starts empty except a parameter, which receives ownership from its caller. Copy
recursively duplicates the source's live contents into independent storage.
Move takes one holder operand, empties it, and produces a transfer value; its
designated consumer is Store, Call, NewArray, ArrayPush, Return or Yield. NewArray consumes
its element operands in order (array elements are transferred). Store takes the
destination address and transferred value, after a FreeArray of old storage.
FreeArray releases recursively and empties its holder; empty is harmless.
ArrayLength observes its one operand without consuming it.

Ordinary region exits transfer their yielded result before releasing region-owned holders
in reverse declaration order. Return additionally releases enclosing region holders.
A conditional result becomes owned by its enclosing region only after the
selected branch yields. This preserves early returns from inside a branch.
Ordinary array reads create Copy operations; ordinary value call arguments transfer
to parameters. Output builtins borrow evaluated values; their argument owners remain
in the enclosing region for explicit cleanup. Reference arguments forward addresses.
No optimization removes empty-holder cleanup yet.

A true while condition keeps its holders through the selected body. Explicit cleanup
follows the body on a normal backedge and precedes break/continue. The false-condition
path releases its holders before yielding false. Cleanup is not unconditional after
the loop: a condition break may bypass later holder definitions entirely. An empty
holder can be cleaned harmlessly; a nonexistent holder cannot. Return releases all
enclosing body and condition scopes. Nested-loop transfers clean only exited scopes.
The grader has a 10,000-node execution budget and rejects overwriting a live holder
on another iteration. Structural checks prevent premature true-path cleanup.

SelectIndex takes an array holder or descriptor address and an evaluated index.
It checks initialized-length bounds in safe mode and captures the actual element
address, without owning or copying the array. Load reads a scalar; Copy reads an
array element independently. Writable nested selection traverses original storage,
not copied values, and captures each intermediate address before the next index.
Assignment selects its destination before evaluating its RHS. The index-order
structural check and deliberately reordered self-check enforce that ordering even
when a particular test's numeric result would not expose it. Indexed compound
assignment remains outside the current semantic subset, not an IR extension.

This bounded implementation supports array literals, local bindings/replacement,
ordinary parameters/calls, returns, conditional results, indexed reads/writes and
while-loop ownership, reference aliases/parameters, array mutations and capacity.
String literals and ordinary/counted classes with scalar/owned-array fields are supported. Object arrays are also supported. Interpolation remains outside the current
semantic subset, not a new Chapter 9 feature. The IR-backed C emitter preserves the previous full supported subset;
this refactor adds no source-language feature.

### Non-owning references

Six retained reference cases are: `ref-local`,
`ref-forward`, `ref-array`, `ref-copy`, `ref-return`, and `ref-index-order`.
`Borrow` has an Address result. At function entry a parameter Borrow receives
an address from the caller; otherwise its single operand is an already selected
address, which it aliases. It does not own, move, or release the referent.
Reading through it uses Load for scalars and Copy for arrays. Assignment through
it releases an old array value before installing the replacement, without
changing which storage the reference denotes. Reference arguments forward
addresses, unlike owning value arguments. The checked parameter symbol is the
authority for this distinction. Ref/Unref remain counted-object operations.

The evaluator passes mutable addresses between independent call frames. The
index-order case captures an element reference before a later argument changes
the index variable; the original element must still be updated. The grader also
rejects Move consuming Borrow directly. FreeArray through a Borrow is valid
when replacing its owned referent, but never as reference-scope cleanup. These checks exercise checked
programs, not a replacement lifetime checker or evidence about the C emitter.

### Array mutation and metadata

ArrayReserve, ArrayResize, ArrayTruncate and ArrayPush consume a descriptor
address selected before their second operand is evaluated. Their result is unit.
Push receives a supplied element, transferring ownership for array elements.
Reserve changes neither length nor live values. Resize initializes newly live
scalar/nested values with typed defaults; shrink releases removed owned values.
Resize(0) releases backing storage even after reserve on an empty array. Truncate
only shrinks, retains capacity, and is a no-op above current length.

ArrayCapacity reads selected names/indexed descriptors without Copy; grouped
expressions preserve that selection. Computed expressions are evaluated once and
retain ordinary temporary ownership. Ordinary value copies retain live contents
and length, not spare reservation. ArrayLength still observes its normally
evaluated operand without transferring it. These choices preserve the current
backend's behavior; avoiding additional length-read copies is not this refactor.

The evaluator models reservation separately from live values. It does not model
real allocation growth, OOM, numeric overflow, or C invalidation. Those remain
existing runtime/checker obligations exercised separately by C execution graders.

### Byte-array literals

A string literal lowers to NewArray with an immutable Bytes payload and no
element operands. The payload contains already-decoded source bytes, not source
spelling or Unicode text. Each execution creates a fresh owning [u8] with exactly
those bytes and no trailing sentinel. JSON exposes numeric bytes, preserving NUL
and invalid UTF-8 without encoding ambiguity. Empty payloads create empty arrays.
The immutable IR payload is not shared mutable runtime storage. Copies, moves,
indexing, mutation and cleanup use the ordinary array operations. The loop fixture
checks repeated execution; the copy fixture checks independent mutable storage.


### Ordinary classes and selected fields

Retained cases: `class-constructor`, `class-alias`, `class-method`, `class-return`,
`class-field-ref`, `class-growth-store`, `class-growth-snapshot`,
`class-receiver-snapshot`, `class-method-identities`, `class-branch`,
`class-method-receiver`, `class-constructor-argument`, `class-method-array`.

NewObject allocates an ordinary class identity after explicit constructor arguments
have evaluated. Its class symbol and nominal result type must agree. A subsequent
Call invokes the selected constructor with that handle as its first argument;
the body returns unit, while the source constructor expression yields the handle.
Method calls snapshot their receiver before explicit arguments, then pass it as
self. Ordinary copies share identity and do not retain or implicitly destroy it.
DestroyObject invokes the generated Destruction body selected by the actual class
symbol. Its sole parameter is an unnamed Local containing the incoming handle.
ReclaimObject releases that class slot at the end of this body; it does not invoke
destruction recursively. The body returns an explicit unit constant like any other
unit-returning function. Generated functions use their class identity as symbolId
and ownerClass, never a fabricated source declaration. Source calls and destruction
requests dispatch through their distinct function kinds. Owned array fields are
released explicitly after the final hook and before reclamation. Final hooks execute
as explicit Calls to the checked hook symbol before ReclaimObject, receiving a
loaded value handle rather than a reference to the generated parameter slot.
Tests require exactly one hook invocation and live field access across allocation;
defect checks omit, duplicate, or move the hook after reclamation.
The evaluator also removes generated reclamation in a self-check and must detect
the surviving object; merely emitting an unused generated body is not sufficient.

SelectField consumes a saved handle and the actual field symbol, yielding a Place.
A Place identifies logical storage, not an interior pointer. Load and Store resolve
it when they execute. Direct assignment can therefore survive class-buffer growth
during RHS evaluation while still targeting the original receiver. Reading a field
before a later call takes a value snapshot; resolving the destination later must
not reread the old value. AddressOf resolves a Place once to a fixed Address, which
Borrow can forward. A borrowed field must never turn back into a resolving path.
Existing SelectIndex addresses remain fixed too.

The independent evaluator relocates each class on every allocation, more often
than the production allocator. It checks fixed field-address generations, object
identity and field ownership, and expected explicit destruction counts. Some
fixtures intentionally leave ordinary objects alive; ordinary classes have no
implicit scope cleanup. This is not a C allocator or layout performance test.
Self-checks capture a destination address too early, misbind identically named
methods, and remove explicit destruction; each defect must fail. Field compound
assignment is not supported by existing semantics: the snapshot case uses the
accepted `b.n = b.n + grow()` form instead.

### Owned array fields

Generated destruction calls the final hook, then selects and frees owned array
fields in declaration order, then reclaims the slot. Each selection uses the
stable receiver handle after the hook. Constructor stores replace initially empty
array holders; subsequent stores release the old array. Ordinary reads copy the
field value recursively. Four retained cases cover independent copies, nested
replacement, multiple fields, and final-hook access/replacement. A deliberately
removed generated release must be detected as a leak by the independent evaluator.

### Counted scalar holders

The eight `counted-` cases cover alias identity, replacement, returned holders,
method receivers, branch transfer, loop cleanup, reference replacement, and owned
array fields. They require no remaining objects and the specified final-hook
count. Deliberate missing-retain and missing-release graphs must be rejected.

A node's `counted` inspection flag exposes its nominal type's checked class role.
`Ref` reads a live handle and increments its count, producing an owning holder.
`Move` empties the source holder without a count update. `Unref` empties its operand
before decrementing; an empty holder is harmless and zero calls the generated
class destruction body. The body calls the hook, releases fields, then reclaims.
`Ref` is not a storage borrow; `Borrow` remains the non-owning address operation.

Value parameters consume transferred holders. Constructor and final-hook `self`
are borrowed handles; ordinary methods consume receiver holders. Named field
receivers snapshot a handle without retaining, while computed receivers keep
their ordinary temporary ownership. This prevents field access in a final hook
from resurrecting its object. Array elements use the leaf ownership rules below.

### Object-array holders

Eight retained cases cover ordinary-object survival after removal, counted copies,
nested copies, removal, returns, push after reservation, element replacement, and
resize-to-zero. Arrays keep independent element storage; copying a counted leaf
retains its object, whereas copying an ordinary handle does not prolong lifetime.
Recursive release drops counted leaves through the same generated destruction
body. Element replacement releases the old holder before transferring the new one.

Non-nullable objects have no implicit default. Reserve adds no live elements;
push supplies one. Resize may shrink a direct object array, but growth must fail
with LYR011 before mutation. Nested-array growth may add empty subarrays. The
independent evaluator models this rule; the current positive inspection cases do
not themselves certify the IR-backed C diagnostic path; retained runtime graders
exercise that failure separately.

### Validation boundary during construction

`LowerIr` validates structure, operation contracts and reachable execution order
before publishing its graph.
Structure checks require complete arena/tree membership, live operand definitions,
implemented region shapes, and enclosing-loop transfer targets. Operation checks
require arity, result categories, typed results, and appropriate storage operands.
Exact types are required across Load, AddressOf, Copy, Move, Ref, and alias Borrow;
Store requires a value of the selected storage's exact type. NewArray requires
matching value elements (or a byte payload for `[u8]`); SelectIndex requires an
array, integer value index, and the exact type one array layer below its parent.
Semantic type equality includes nominal declaration identity, not class spelling.
Conversions must be explicit operations, not hidden effects of these operations.
Array mutations require selected array storage and return unit. Push receives an
exactly typed element value; reserve, resize, and truncate receive integer values.
Length and capacity require arrays and return u64. Length consumes an evaluated
value, while capacity may observe selected storage or a computed array owner;
copying the descriptor's contents first would incorrectly discard reservation.

Class operations preserve actual nominal identities. SelectField requires a value
handle of the field's owning class and the field's exact declared type. NewObject
must produce the selected class's instance type. DestroyObject receives that
class's value handle and returns unit. ReclaimObject receives receiver storage and
is permitted only inside the matching generated destruction function.

Call validation uses the selected function's actual parameter scope: exact arity,
result type, argument types, and fixed addresses for reference parameters versus
values for ordinary parameters. Constructor body calls return unit; the separately
allocated object handle remains the source construction expression's result.

Ownership operation types are checked separately: Copy and FreeArray require
arrays; Ref and Unref require a scalar counted-class instance; Move requires an
array or counted scalar. Move additionally requires an owning holder definition,
not Borrow, selected storage, a non-owning Load, or another Move. Constructor/final
self and generated destruction parameters are borrowed, not owning locals. Each
transfer has exactly one ownership-consuming use: Store/Push value, array element,
ordinary value argument, Return, or Yield. Conversely, every ownership-consuming
operand for an array or counted scalar must receive an explicit Move. Cleanup may
intervene between the Move and its consumer; adjacency is not required. These local checks do not prove the
holder is live or that ownership is balanced on every path. Releasing old storage
through a reference during replacement is legal, so a borrowed address is not by
itself evidence of an invalid release.

Unary operators require value operands: logical negation is bool-to-bool;
ordinary and wrapping negation preserve the integer operand type. Conversion
operators distinguish `widen` (the shared whole-range inclusion rule) from `as`
and `!as` (integer-to-integer). Unknown tags are refused; wrapping semantics
must not be silently replaced by checked arithmetic.

Binary operations require typed values. Integer arithmetic selects the shared
ArithmeticType; shifts preserve the left integer type. Integer comparisons allow
mixed ranges and return bool; boolean operands allow only equality/inequality.
Unknown tags and eager short-circuit operators are refused: `&&` and `||` use
structured If regions. Checked and wrapping tags remain distinct.

Control value contracts require a boolean Value condition for If. Return carries
an exact Value of the declared function result type (unit for generated destruction).
Yield carries the owning If result type from a Then/Else region, or bool from a
While Condition region. Other yield destinations are refused. These checks do not
establish whether all required paths yield or return.

The execution pass requires definitions before reachable uses. Branch-local definitions
remain private; the explicit If result becomes available on continuing paths. Loop
condition definitions remain available to the body and after the loop only when
all relevant exits execute them. Lowering releases condition holders in the false
condition arm, not unconditionally after the loop: a condition break can bypass
later holder definitions. True conditions retain holders through the body. Break exits intersect with the false-condition
exit; continue paths do not exit the loop. Body-only definitions do not escape. Reachable function exits require Return, and branch/condition
exits require Yield unless control transfers elsewhere. Loop bodies may fall through.
Unreachable suffixes still receive structure/type checks, but not execution-order checks.
The iterative walk and queued completions do not recurse through nested regions.

The execution pass separately tracks pending Move results. A consuming operand
removes its pending transfer. Return must leave none; Yield cannot abandon a
transfer created within that region, although inherited transfers may remain
pending for a later enclosing-region consumer. Continuing branches merge pending
obligations; terminated branches do not. Loop conditions, backedges, break and
continue preserve the transfers pending at loop entry. Transfers created during
an iteration must therefore be consumed before leaving or repeating it. These are
structured-IR contracts, not constant-condition reasoning or general ownership
proofs. Ordinary calls evaluate arguments before generating their transfers.

These are bounded checks, not a complete validator. Storage initialization, ownership
liveness/balance, full provenance, and eventual loop termination are not established. The real-model probe in `tools/probes/check_ir_model.py` tests
malformed graph refusal and repair separately from executing inspected programs.

## C-emission migration checks

From the repository root, after building the selected configuration:

```sh
python3 graders/ch09/c_emission.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

Requires Python 3 and supported Clang (`CC`, default `clang`). The grader invokes
the supplied compiler, compiles temporary emitted C at `-O0` and `-O2`, runs it,
and removes temporary files. No dependencies are downloaded. Each subprocess has
a 30-second timeout. `make test` runs this grader in both configurations.

Every named case in `c_emission.py` is a stable retained runtime check with prefix
`ir-c/`. The current 33 cases cover scalar operations and failures, structured
control, array ownership and mutation, fixed references/indexing, ordinary and
counted objects, and generated finalization. The fixture list is the exact inventory.
Successful compilation requires exit zero and empty stderr; C compilation must exit
zero. Positive programs require exit zero and exact stdout (empty except explicitly
specified output cases), with empty stderr. `assertion` requires exit one, empty
stdout, LYR010 assertion-failure text and both fail/main trace names. `index-bounds`
requires exit one, empty stdout, LYR006 bounds text and both fail/main trace names.
Subprocess streams are captured with Python text mode: decoding uses the host default
text encoding and universal-newline translation maps CRLF and CR to LF. Comparisons
then require exact strings; no additional stripping or whitespace normalization is
applied. These are not raw-byte comparisons. A crash or unrelated nonzero exit is
not a pass. The spin09 walkthrough separately compares its program output as bytes.

The exact emitted-body marker counts are **chapter-only** checks: they establish
that all bodies exercised the newly constructed path, including generated destruction.
The temporary syntax fallback has been removed; these markers are not a permanent
public C ABI or a requirement on later optimization. Runtime expectations remain
retained. Numeric success and final-hook counts alone do not prove leak freedom;
the independent ownership interpreter and retained allocation/balance checks provide
separate evidence, with their documented limits.
