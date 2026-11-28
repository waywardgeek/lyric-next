# Chapter 14 — single-threaded safety and supporting patches

Status: author implementation and verification complete; independent review and
reconstruction are in progress. Human acceptance is pending.

Run from the repository root using installed Python3, the .NET10 SDK and a C11
compiler. No downloads or network services are needed. Supply the implementation
as an external compiler command; this grader does not inspect compiler source.

```sh
DOTNET=/path/to/dotnet
"$DOTNET" build compiler/Lyric.csproj -c Debug --no-restore --nologo --tl:off
python3 graders/ch14/aggregate_participants.py --runtime-debug "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch14/aggregate_participants.py --soa --runtime-debug "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Override `CC` when needed to select your installed C compiler. Repeat with Release
and without `--runtime-debug` for the corresponding compiler/runtime modes.
Compiler Debug/Release and generated runtime debug mode are separate choices.

All named cases are retained. The suite contains16 execution cases and2 compile-only
rejection. Each execution must emit C successfully with empty stderr, compile at
both O0/O2, and exit0 with exact expected stdout bytes and empty stderr. Each negative
must exit1 with empty stdout and its intended LYC304 type or LYC306 unsupported-field diagnostic, not crash or
time out. The shared public `graders/ch10/tuples.py` harness defines comparisons;
there is no output normalization. Temporary artifacts are removed by the harness.

Ordinary arrays/tuples retain their counted elements, including copies from borrowed
relationship observations. The tests cover inferred/annotated/nested literals,
array push, element replacement, mixed sources, independent copies, helper
arguments/results, and application containers. A separate negative retains the older exclusion of
direct class-handle-array fields such as `held:[C]`; this is not a recursive
ban on tuple-nested counted arrays, which remain subject to other admission and
lifetime checks.
Finalization timing distinguishes retained container contents from structural links.
These output tests do not directly measure every intermediate reference count.

The accepted Chapter13 suite `participant_aggregates.py` deliberately rejected
participant aggregates while their semantics were unsettled. Bill explicitly
resolved the contract for Chapter14: ordinary containers own, not borrow, their
counted elements. The Chapter14 cumulative Make target uses this successor;
Chapter13's historical tests and four imported owner controls remain intact.
The cumulative target runs this successor in production/debug × AoS/SoA modes.
This is an agreed contract change, not a weakening to disguise a failed grader.
The scalar relationship and participant-IR suites remain retained unchanged.

No complete safety, representation-migration, independent-review, student or human
acceptance claim follows from these focused cases.

## Ordered cross-call safety audit

Run the retained compile-only safety suite against each compiler configuration:

```sh
python3 graders/ch14/safety_calls.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch14/safety_calls.py "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
```

The 18 cases test direct and forwarded owner destruction, nested-array
replacement through overlapping arguments, nested aggregate aliases, finalizer
cleanup, generic specialization, loop back edges with fresh-acquisition controls, and direct/mutual recursion with safe descent versus unsafe unwinding. Each unsafe ordering requires exit1,
empty stdout and `LYC326: Borrowed storage may have been invalidated.` in stderr.
The corresponding use-before-mutation control requires exit0 and empty streams.
Programs are checked, never executed. All cases are retained and included in the
cumulative Make target. These tests supplement, not replace, the required source
audit in Chapter14 and earlier retained lifetime checks.

## Initialization and cleanup admission audit

```sh
python3 graders/ch14/safety_initialization.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch14/safety_initialization.py "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
```

Nine retained compile-only cases challenge labeled contributed-field initialization
at branches, explicit returns, field reads and partial-receiver escape, plus a
nested counted-container finalizer cycle and two unsupported mandatory-parent
construction refusals. Positives require exit0 and empty streams;
negatives require exit1, empty stdout and the specified full LYC308, LYC328 or LYC334 message.
The public runner in `safety_calls.py` implements this comparison. Its rejection
contract requires exactly one diagnostic line: the exact quoted input path,
positive line and column, `error`, the exact code and message, and a nonnegative
byte offset, ending in LF. Additional diagnostics or trailing output fail. These cases never
execute invalid programs. Earlier Chapter8 ordinary-field checks remain required.

## Initialized-length runtime access audit

Run `python3 graders/ch14/safety_access.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll`
and repeat with Release. All four cases are retained in `make test`.
The regrowth control requires exit0 and exact stdout `ok`. Three safe-mode failure
cases attempt a read in reserved-empty storage, a read in a truncated tail, and a
write past initialized length despite spare capacity. They require exit1, empty
stdout, the exact located `LYR006: array index out of bounds` diagnostic and main
frame, using the public tuple harness's positive source-coordinate contract.
Each runs at C O0/O2. Invalid cases are never executed in unsafe mode. The retained
Chapter12 nullable runner and Chapter13 relationship runtime suites remain required.

## Supported-call admission audit

Run `python3 graders/ch14/safety_boundaries.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll`
and repeat with Release. Three retained compile-only cases require a successful
known direct body, an exact LYC306 FunctionType refusal for function-valued
parameters, and an exact LYC306 Function refusal for a missing body. Exit/stream
checks use `safety_calls.py`. No opaque-call or callback safety support is claimed.

## Required cumulative audit checkpoint

From the repository root, with the installed tools above:

```sh
make DOTNET="$DOTNET" PYTHON=python3 test
```

For external read-only graders, additionally supply `GRADERS=/absolute/path/to/graders`.
The target builds Debug and Release and runs all retained suites through this
increment, including the Chapter13 migration runners. Success requires terminal
exit0 and every required suite passing; a partial log or an individual PASS is not
completion. The Chapter14 source audit in the chapter is also required. These
commands do not constitute the still-unfinished whole-chapter acceptance process.


## Production handle representation and preserved debug contracts

Run from the repository root; repeat with the Release assembly:

```sh
python3 graders/ch14/handles.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch14/debug_mode.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch13/debug_identity.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

All cases are retained and registered in `make test`. `handles.py` checks both
production layouts: actual typed AoS pointers with in-record metadata, and u32 SoA
handles with separate next-free/count arrays. Valid C executions at O0/O2 require
exit0, exact stdout and empty stderr. A generated-C allocator probe verifies
nonnull initial allocation and slot reuse through the real emitted helpers.
For SoA it sets the private high-water counter beyond the u32 range, verifies a
free slot is still reusable, keeps that slot occupied, then requests fresh storage.
That request must exit1 with stdout `X` and exactly
`probe:1:1: runtime error LYR007: class allocation size overflow\n` on stderr.
No billion-element allocation or invalid stale access is attempted. These two
layout contracts do not measure performance or prove every representable index
can physically be allocated.

`debug_mode.py` is the Chapter14 successor to the Chapter13 file. It preserves
all four layout/mode combinations, observing-versus-escaping receiver IR checks,
normal execution, destruction count/empty-holder checks, and the deliberately
removed destruction-hold control at O0/O2. Only metadata addressing in the injected
probe changes. The original file is preserved, not silently weakened. The unchanged
64-case debug-identity suite remains required; debug identities need not have the
production representation. Debug instrumentation is not production safety proof.

## Layout-specific allocation growth

Run `python3 graders/ch14/allocation.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll`
and repeat with Release. This retained successor preserves Chapter8's one-versus-257
reuse workload and 257 simultaneously live ordinary objects, both layouts at O0/O2.
Reuse must make the same positive number of allocation calls. Growth must make
nine calls for AoS (four directory allocations and five chunks; metadata embedded)
and eighteen for SoA (next-free and field arrays each grow through capacities
2,4,...,512 because slot zero is reserved). Exact exit0, empty stdout and measured
count on stderr are required. The historical Chapter8 grader remains unchanged;
`make test` selects this successor. Counts test the growth strategy, not elapsed
time, peak memory, or leak freedom.
