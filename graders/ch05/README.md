# Chapter 5 — execute the checked subset

Status: reference implementation verified; single code and text reviews incorporated. Fresh-student reconstruction passed all required cumulative checks, the separate PEG comparison, and the scripted walkthrough; human walkthrough remains pending.

Run from the repository root with Python 3, .NET 10/C# 14, and a C11 compiler
providing the exact-width integer types. No packages or network access are used
by the grader. `CC` defaults to `cc` and may contain compiler flags. When grading
a Chapter 6 or later backend, also satisfy its checked-arithmetic builtin compiler
requirement; Chapter 5-only implementations need not use that lowering.

```sh
make test
# Or, after building the desired configuration:
python3 graders/ch05/run.py dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

`make test` builds and runs all retained suites in Debug and Release. Chapter 3's
separate PEG comparison remains required as documented in the preceding chapters;
this Make target does not invoke it.

## Compiler interface

`COMPILER --emit-c FILE` reads one Lyric executable source, parses it, and performs
Chapter 4 semantic checking before emission. Success exits 0, has empty stderr,
and writes a self-contained C11 translation unit to stdout. C formatting and
identifier spelling are not comparison requirements. Compiling and running the C
are separate explicit actions, not subprocesses launched by the Lyric compiler.
Source/semantic rejection retains its existing exit 1 and located diagnostic,
with **empty stdout**. Usage errors retain the prior interface. An unsupported
backend construct reports LYC401 and emits no partial translation unit.

The supported source subset is Chapter 4's: functions, direct calls, parameters,
locals and assignments, statement conditionals, returns, unit/boolean/string and
integer expressions, and checked integer `+`, `-`, and `*`. Classes, loops,
comparisons, mutable arrays, and other unsupported semantics are not added here.

## Execution contract

All 80 named cases are **retained**. Stable identifiers are the case names in
`run.py`; generated names include the integer type and operation/boundary label.
There are no chapter-only cases. The grader supplies an independent compiler
command through its arguments. It writes disposable source and C files, invokes
that compiler, then builds the emitted C with `-std=c11` at both `-O0` and `-O2`.
Each process has a 20-second timeout. Temporary files are removed afterward.
A timeout, crash, unrelated diagnostic, or arbitrary nonzero exit is a failure.

Successful programs require exit 0, exact stdout bytes and empty stderr. There
is no newline or Unicode normalization. `print` writes the argument's bytes;
`println` appends one LF. Embedded NUL, non-UTF-8 bytes, and UTF-8 must survive.
Argument and operand effects occur left to right; untaken branches do not run.
Ordinary executable completion exits 0. Unit is a real value, not a missing C
argument. Source identifiers need not be legal C identifiers.

Runtime integer overflow requires exit 1, empty stdout in the authored cases,
and the exact located diagnostic followed by the active Lyric call trace:

```text
"SOURCE_PATH":LINE:COLUMN: runtime error LYR001: integer overflow in TYPE OPERATION
  in "calc"
  in "main"
```

`SOURCE_PATH` is JSON-quoted; locations use the earlier scalar-column contract.
Operation is `addition`, `subtraction`, or `multiplication`. The diagnostic is
located at the executed operator. Runtime guards must run before the C operation
can overflow. A wider receiving destination does not widen that operation.
The current fixtures use ASCII temporary paths and ASCII function names.
Fatal failure exits the process; no cleanup/unwinding guarantee is introduced.
Normal returns must remove their activation from the trace.

The 2 emission-rejection cases require exact existing semantic diagnostics,
exit 1, and empty stdout; no C compiler is run for them. All other cases require
successful C emission and compilation before their execution can count as a pass.

Arithmetic results are observed without introducing a numeric-printing API:
parameter-based calculations are compared with expected values through checked
subtraction and endpoint addition. A nonzero difference cannot satisfy both
endpoint checks. These are real arithmetic operations, not a compiler test mode.

## Supplementary sanitizer check

On a compiler supporting Clang/GCC's undefined-behavior sanitizer, run:

```sh
CC='clang -fsanitize=undefined -fno-sanitize-recover=undefined' \
  python3 graders/ch05/run.py dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

This is supplementary, not a silent prerequisite for other C toolchains. Passing
checks the exercised paths, not universal C portability or all operand pairs.
The retained `closed-pipe` case closes the pipe reader before starting the
program. It requires exit 1 and this diagnostic, not termination by SIGPIPE:

```text
"SOURCE_PATH":1:15: runtime error LYR002: output failure
  in "main"
```

Output failures are located at the print call. Diagnostics are best-effort if
stderr itself is unavailable. On platforms with SIGPIPE, executable startup
must arrange for failed writes to reach the checked output path. The grader
uses Python's operating-system pipe support; the verified host is macOS ARM64.
The `operand-order` and `returned-frame` cases check evaluation order and removal
of completed activations from a subsequent failure trace.

## Always-on assertions (retained)

The five `assert-*` cases require the Chapter 4 standard signature
`assert(condition: bool) -> unit`. True returns unit silently; evaluate the
condition once. False reports `LYR010: assertion failed` at the assertion call,
then the active innermost-first trace, and exits 1 with no later output.
The existing runtime-failure comparison checks exact stderr bytes, quoted file
and function names, location, exit status, and empty stdout. These cases run at
both O0 and O2. Assertions must not depend on C `assert` or `NDEBUG`, and Chapter
6 retains them in unsafe mode. There are now 80 cases (78 executable, two
compile rejections). Earlier reconstruction evidence predates these additions.
