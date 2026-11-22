# Chapter 4 semantic checks — reference and student verified

Run from the repository root with Python 3 and the .NET 10 SDK:

```sh
make test
```

The cumulative target builds Debug and Release and runs all retained checks.
Override `DOTNET=/absolute/path/to/dotnet` when needed. To supply an independently
built implementation to this grader alone:

```sh
python3 graders/ch04/check.py /path/to/dotnet /path/to/Lyric.dll
```

Cases are **retained**, identified by name, except `branch-value-unsupported`,
a milestone-only rejection for Chapters 4–5. Chapter 6 intentionally implements
value-producing conditionals: select `--chapter=6` before the compiler command
to require the SAME source to succeed as retained `branch-value-supported`.
The default selection remains Chapter 4; `--chapter=5` preserves that boundary.
The current cumulative Make target selects Chapter 6. All other expectations
are unchanged. Source and expected diagnostics are public.
Students may read, but must not modify, graders and expected results.

## Interface and comparison

`--check FILE` parses and checks an executable. `--check-library FILE` performs
the same checking without requiring `main`. Neither executes the source or
produces C. Success means exit 0 with empty stdout and stderr. Failure means
exit 1, empty stdout, and exactly one located diagnostic on stderr:

```text
JSON_FILENAME:LINE:COLUMN: error CODE: MESSAGE (byte OFFSET)
```

A final newline is required. Filename JSON escaping may differ equivalently;
all remaining text, diagnostic code/message, and source position must match.
Offsets count UTF-8 bytes from zero. Lines/columns start at one; columns count
Unicode scalars, and CRLF is one newline. Each invocation has a 15-second limit;
a timeout, crash, unrelated error, or arbitrary nonzero exit is not a pass.

Current semantic codes: LYC301 unknown name, LYC302 already-visible declaration,
LYC303 wrong argument count, LYC304 incompatible value type, LYC305 executable
entry-point error, LYC306 parsed construct not yet semantically supported,
LYC307 unassigned local read, LYC308 repeated immutable assignment, and LYC309
nonunit function missing an explicit return. LYC310 reports an integer literal
outside its selected type range. LYC311 reports constant arithmetic overflow;
LYC312 reports incompatible integer arithmetic operands. A bare return with a missing value
is located at its function declaration because the structural return node has
no source anchor.
Source, lexical, and syntax failures retain their earlier contracts.

## Current increment and limits

The 79 cases exercise nongeneric functions, immutable typed
parameters, string/boolean/unit expressions, direct calls, forward lookup,
standard `print`/`println` declarations, no-shadowing, and executable entry checks.
Builtin printing is resolved as an ordinary function signature, not a special
callee spelling. `string` and `[u8]` denote the same type. Supported type names
also include the integer/float types. Integer expressions are supported as
described below; floating literals and operations are not yet supported. `main` must have no parameters and unit result.

Other semantic constructs must explicitly reject rather than silently pass.
This bounded increment is not evidence of a full Lyric type checker. Library exports beyond the supported standard declarations,
the remaining arithmetic operations and loops remain work in progress. Locals
now support inferred or annotated types, initialization before reads, one-time
initialization of `let`, and reassignment of `var`. Parameters start initialized
and remain immutable. Explicit typed returns and missing returns are checked.
Decimal integer literals use their suffix, an expected integer type, or the i32
default in that order. Direct unary minus is applied before range checking,
including signed minima. Typed variables convert only by whole-range widening;
a small current value does not justify narrowing.

Checked integer `+`, `-`, and `*` select an existing operand type containing both
operand ranges, or reject. An unsuffixed literal takes context from its typed
neighbor first; absent one, the expected return/binding/argument type applies.
Literal-only nested arithmetic and grouping carry that context inward. Without
context the default is i32. A wider destination never rescues overflow in an
already-typed calculation. Exact literal-expression constants are checked at
each operation; dynamic operations are typed but not executed by this chapter.
Other binary operators and general unary expressions remain unsupported.

Statement-position `if`/`else`/`else if` require boolean conditions. Each block
has a distinct lexical scope: sibling names may repeat, visible outer names may
not be shadowed, and branch locals cannot escape. Both alternatives start with
the same incoming assignment state. On continuing paths, definitely assigned
bindings intersect and possibly assigned bindings union. Returning paths do not
participate in the join; a missing else is an unchanged continuing path. A
nonunit function must return on every path. Literal conditions do not prune
branches in this increment.

Value-producing conditionals, loops, and statements after unconditional return
remain explicitly unsupported (`LYC306`), not new language prohibitions.
Semicolon discard markers after return remain accepted. These unsupported-boundary
cases are retained until an explicitly agreed increment revises their contract.
Chapter 6 revises the value-producing conditional boundary as classified above.

The retained `long-context` and `long-neighbor` cases check 4,000-term arithmetic
chains with expected-type and typed-neighbor context respectively. They require
silent success within the ordinary case timeout, not a performance benchmark.

The retained Chapter 3 PEG comparison is also required separately from `make test`: after building Release, run `DOTNET=dotnet node tools/peg/test.mjs graders/ch03/cases.json` from the solution root, substituting tool paths as needed. Require exit zero and 143/143 authored expectations. See Chapter 3 for pinned setup; prepared offline bundles need no download.

The three retained `assert-*` cases add the ordinary `assert(bool) -> unit`
signature, boolean argument checking, and arity checking. False is accepted by
semantic checking; Chapter 5 tests its runtime failure. The current suite has
82 cases; the earlier 79-case reconstruction did not exercise these additions.
