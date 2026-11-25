# Chapter 10 graders — under development

The retained suite covers canonical types, tuples, structs, array-growth defaults,
and contained borrows while preserving earlier chapters. Chapter 10 teaching,
independent review, reconstruction, and human acceptance remain unfinished; passing
these graders alone does not complete the chapter.

From the repository root, using Python 3 and an already built compiler:

```sh
python3 graders/ch10/canonical.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch10/canonical.py --selfcheck
```

The first command accepts an independently supplied compiler command as its
remaining arguments. Each invocation adds `--inspect-ir FILE`. No compiler source
or private diagnostic probe is imported. The implementation must retain earlier
inspection modes and all applicable retained checks through Chapter 9.

## Additive canonical inspection contract

`lyric-ir-1` retains existing fields and readable node `type` strings. Every node
also supplies `typeId`, null exactly when `type` is null. A top-level `types` array
contains exactly the referenced canonical types and their transitive components.
IDs are document-local integers, not persistent model state; order and numeric
values are unrestricted. Serialize actual canonical identities, not types rebuilt
or deduplicated by display name for inspection.

Every type record has `id`, `kind`, `name`, `scalar`, `elementTypeId`, and `classId`.
Records also have `componentTypeIds`, an ordered list (empty for non-tuples).
For the current slice:

- `Scalar`: `scalar` is its ScalarKind spelling (for example `I32`), with null
  element and class links.
- `Array`: `elementTypeId` names the canonical element; scalar/class are null.
- `Nominal`: `classId` is the actual declaration's document-local symbol ID,
  using the same identity mapping as functions/nodes; scalar/element are null.

- `Tuple`: `componentTypeIds` contains the ordered canonical component IDs,
  including repeated occurrences; scalar/element/class are null. Tuple identity
  is the complete ordered sequence, not its display name or an unordered set.

Complete keys are unique. Two different nominal declarations remain distinct;
array identity uses element identity. The existing node `classId` continues to
identify its base nominal declaration, including through arrays. Type records'
`classId` is nonnull only for the nominal record itself.

## Comparison and inventory

All five cases below are **retained**. Positive cases require exit 0, empty stderr,
and one JSON document on stdout satisfying the structural/link assertions and
fixture-specific expected types. JSON formatting and record ordering are ignored;
no type-name normalization is permitted. Python text capture uses universal-newline
translation. Each process has a 60-second limit; timeout/crash is failure.

- `canonical-scalars`: reuse across functions and distinct integer types.
- `canonical-arrays`: nested element links and reuse of scalar/array components.
- `canonical-nominal-growth`: 100 distinct nominal declarations, retaining their
  distinct identities across table growth in this implementation.
- `canonical-tuples`: repeated component identities, nested tuples, and distinct
  identities for different component orders.
- `canonical-invalid-source`: unknown call must exit 1 with a located LYC301
  diagnostic on stderr and empty stdout. Arbitrary rejection does not pass.

The selfcheck must reject 18 synthetic corrupt documents: the four original identity/link defects, missing or null component lists, and string/Boolean substitutions at each ID-bearing position. It is not a compiler
case. Public output exposes identity relationships but cannot prove the internal
lookup algorithm, pointer equality, collision handling, or lifecycle invariants.
Those remain implementation review obligations; development-only model probes
provide additional evidence and are not retained reconstruction graders.


## Tuple execution slice

```sh
python3 graders/ch10/tuples.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

Requires Python 3 and a C11 compiler (`CC`, default `clang`). Each of the twenty-two
retained positive fixtures must emit C with exit 0 and empty stderr; the generated
C must compile at both `-O0` and `-O2` and execute with exit 0, exact expected stdout
bytes, and empty stderr. No newline normalization is allowed. Each subprocess has
a 30-second timeout. Twenty-six retained negative fixtures require exit 1, empty stdout,
and the fixture-specified located diagnostic (`LYC301`, `LYC302`, `LYC304`, `LYC308`, `LYC326`, `LYC330`, or `LYC334`) on stderr; a crash or unrelated rejection fails.
The fixture identifiers and complete inputs are public in `tuples.py`.

These checks run in both configurations as part of `make test`. The current
source slice supports scalars, nested tuples, owned array contents, ordinary and
counted class handles, and writable component borrows. Checks cover independent
array copies, counted finalization, saved object identities across later arguments
and cleanup, and rejection of self-finalization hidden inside a tuple. Destructuring checks
cover nested and singleton patterns, one initializer evaluation, mutable versus
immutable bindings, independent array copies, immediate discard cleanup, retained
counted leaves, cleanup invalidation of named leaves, and duplicate/hidden names.

Struct and contained-reference checks are described below. Tuple array-growth
defaults exist exactly when every component has a default; structs never have
implicit array-growth defaults, even when their fields declare defaults.
Cleanup-safe internal empty holders do not imply public defaults.

## Struct declarations and finite layout

Run `python3 graders/ch10/structs.py COMPILER [ARG ...]` from the repository
root (for example, supply `dotnet compiler/bin/Debug/net10.0/Lyric.dll`).
All 20 named cases in that file are retained and included in `make test`.
They invoke `--check-library`: success requires exit 0 and empty stdout/stderr;
failure requires exit 1, empty stdout, and one correctly located diagnostic
with the expected code. `LYC341` rejects infinite inline struct layouts;
`LYC302` rejects duplicate declarations. Crashes are failures, not rejections.

Names are registered before field resolution, and all fields are resolved
before layout validation. Inline struct/tuple cycles are invalid; array
backing storage and class handles do not contribute recursively to inline size.
Every struct is checked, including one reached only through an array.
This declaration suite also checks inline method signatures and bodies. It does
not by itself establish execution or contained-borrow support; the value suite
below exercises construction, defaults, and method execution.

## Struct values and explicit field defaults

```sh
python3 graders/ch10/struct_values.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

All named cases in `struct_values.py` are retained and included in `make test`.
The runner shares the tuple suite's process harness, not its fixtures. It requires
Python 3 and a C11 compiler (`CC`, default `clang`). Positive cases require successful
C emission with empty stderr, then successful C compilation and execution at both
`-O0` and `-O2`, with exact expected stdout bytes and empty stderr. Negative cases
require checker exit 1, empty stdout, and the specified located diagnostic; a crash,
timeout, or unrelated error is not an intended rejection. Each subprocess has a
30-second limit. Fixture names, inputs, expected bytes, and diagnostic codes are
public in that file.

Coverage includes nominal field matching, written-order snapshots, independent
owned copies, mutable versus immutable field storage, parameters and returned
values, references to caller storage, counted finalization, and stale-handle or
borrow rejection across replacement and cleanup. Named construction supplies every
field unless that field declares an explicit default. Supplied expressions execute
left-to-right as written; omitted defaults then execute in declaration order.
Each omitted default is evaluated afresh, while a supplied field skips its default.
Defaults resolve in declaration scope, without sibling fields, partial self, or
caller locals. Every declared default is type-checked even when overridden.
Default calls participate in transitive destruction and borrow-invalidation checks;
conditional defaults also preserve returned ownership through local cleanup.

Method checks cover value-copy receivers, explicit writable reference receivers,
static calls, receiver snapshots, and later-argument borrow invalidation. Separate
qualified bodies match the original member by canonical parameter/result types,
reference modes, and receiver status. Ordinary parameter names may differ: the
body uses its own names, while both declarations and bodies obey no-shadowing.
All signatures must match exactly; there is one callable, not a wrapper or a new
overload. `LYC342` rejects missing, duplicate, undeclared, or mismatched bodies.
These fixtures place declarations and bodies in one input file; they do not
establish configured multi-file package support.

Array-growth defaults and aggregate-contained references have separate retained
suites below; the struct-value suite alone does not establish those behaviors.


## Aggregate array-growth defaults

From repository root, after building the supplied implementation:

```sh
python3 graders/ch10/array_defaults.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

Requires Python 3 and a C11 compiler (`CC`, default `clang`); no downloads. All
12 stable fixture identifiers in `array_defaults.py` are **retained**, and this
command is included in `make test` for Debug and Release. Each fixture is emitted
in safe and unsafe mode, compiled at both `-O0` and `-O2`, and run with a 30-second
limit per subprocess. Emission requires exit 0 and empty stderr; C compilation
requires exit 0. Six successful fixtures require exit 0, exact stdout bytes, and
empty stderr. Six runtime rejection fixtures require exit 1, empty stdout, and
exactly the JSON-quoted source pathname, positive line and column, the diagnostic
`runtime error LYR011: array element has no default value`, and the sole `main`
frame. No normalization, crash, arbitrary failure, or unrelated diagnostic passes.
The shared runner reports both kinds as execution cases: runtime rejection is a
successful check when its intended failure is observed.

A tuple defaults exactly when every component defaults, recursively. Array
components default to empty without constructing elements; therefore `([Point],
i32)` defaults even though `(Point, i32)` does not. Structs never implicitly
default for array growth, including empty structs and structs whose fields all
have declared defaults. Those defaults run for named construction, not resize.
Nonnullable handles likewise prevent a containing tuple from defaulting. Unsafe
mode retains all of these initialization requirements.

The cases cover nested scalar tuples, shrink/regrowth over dirty slots, owned
array components, the empty-array boundary, reserve/push/no-growth/shrink controls,
and rejection of structs and handle-bearing tuples. The effectful-default fixture
also ensures a refused growth does not invoke a struct field initializer. These
checks are not an allocation-leak or sanitizer proof.


## Contained-reference structs

From the repository root, after building the supplied implementation:

```sh
python3 graders/ch10/borrowed_structs.py /path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

All 20 execution and 23 rejection fixtures are retained, with stable identifiers
and complete public inputs in `borrowed_structs.py`. They run in both build
configurations through `make test`, using the tuple runner and its Python 3/C11
prerequisites. Positive cases require compiler exit 0 and empty stderr, C compilation
at both `-O0` and `-O2`, and execution with exit 0, exact stdout bytes, and empty
stderr. Each subprocess has a 30-second timeout. Negative cases require compiler
exit 1, empty stdout, and a located error containing the fixture's expected code;
the harness does not compare the entire stderr byte-for-byte. A crash, timeout,
or unrelated diagnostic is not a passing rejection.

A holder copy copies aliases, not their referents or lifetimes. The controls cover
local copies, nested inline occurrences, live references to other holders, earlier
value snapshots, branch/destructuring results, synchronous helper effects, caller
replacement and swaps, and write-through from by-value wrappers. An immutable
wrapper cannot have its inline storage replaced, but its reference field still
permits writes to the writable storage borrowed at construction. Reference defaults
require explicit reference syntax and exact referent types, and their returned
aliases are checked after initializer cleanup. Separate controls exercise direct address defaults and ordinary value defaults returning an aggregate that contains an alias. Return and array escapes, including
wrappers and forward-declared array element types, require `LYC343`; invalidated
or expired borrows require the fixture's lifetime diagnostic.

Recursive formal reachability uses a finite graph: distinct parameter/field
occurrences remain separate, while repeated referent types along a path fold into
cycles. Writes to locations representing multiple possibilities preserve untouched
alternatives. This conservative policy may reject safe programs. The recursive
fixture checks analysis of an uncalled helper, not runtime traversal or construction
of a cyclic value. Each positive ordinary-object default control allocates one
process-lived object; it is not object-reclamation or sanitizer evidence.
