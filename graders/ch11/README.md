# Chapter 11 retained checks — increment in development

Run from the repository root with the installed .NET 10 SDK. No packages or
external services are required by these runners. Supply the compiler command as
positional arguments; the grader code and fixtures are independent of its source.

```sh
DOTNET=/path/to/dotnet
"$DOTNET" build compiler/Lyric.csproj -c Debug --no-restore -tl:off
python3 graders/ch11/generics.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch11/typeof.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch11/matchtype.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Repeat with `Release` in both build configuration and assembly path. These checks
supplement, not replace, the retained Chapters 1–10 checks in `make test`.
Chapter 11 remains in development; these runners are included in `make test`,
but passing them alone does not establish chapter completion.

All three runners reuse the public Chapter 10 `tuples.py` execution harness. Successful
cases require compiler exit 0 and empty stderr, generated C that builds and runs
at both `-O0` and `-O2`, exact stdout bytes, and successful execution. Rejections
require compiler exit 1, empty stdout, and the named located diagnostic. A crash,
timeout, parser rejection, or unrelated diagnostic does not pass a semantic
rejection case. Read the runner and shared harness for the complete comparison.

All named cases in all three runners are **retained**, including the visibly generated
breadth and depth fixtures. No cases are chapter-only. `generics.py` currently
checks explicit and inferred specialization, reference and value arguments,
structural binding conflicts, evaluation order, and discovery limits. The limit
is discovery depth 1,024 from a root at depth zero, not a total-function count;
existing specializations are reused before applying the limit.

`typeof.py` checks unevaluated static typing, uninitialized and expired-reference
operands, type errors, flow-state isolation, generic use, forward signature
dependencies, and cycle rejection. Its generated C must not execute the queried
operand. Development model probes under `tools/probes` are separate evidence,
not replacement public graders.

`type-syntax.json` contains retained PEG syntax checks. With the repository's
already installed Peggy dependencies, run:

```sh
DOTNET=/path/to/dotnet node tools/peg/test.mjs graders/ch11/type-syntax.json
```

The PEG harness uses the Release compiler for lexical tokens, but recognizes the
syntax independently from the canonical grammar. Missing dependencies are setup
requirements, not permission to install them silently. Malformed queries also
run through the real compiler in `typeof.py`, requiring `LYC201` rather than a
crash or arbitrary failure.

`matchtype.py` checks explicit pattern binders, first-match selection, fresh
per-arm bindings, typo rejection even in later patterns, no-shadowing, binder
visibility, unevaluated subjects, selected-only effects, results, initialization,
loop transfers, cleanup ordering, and safe/unsafe lifetime distinctions. Unsafe
lifetime examples are compile-time rejections, never executed by this runner.
Malformed headers, arrows and unselected bodies require the parser diagnostic.

Run the independent PEG syntax fixtures with:

```sh
DOTNET=/path/to/dotnet node tools/peg/test.mjs graders/ch11/matchtype-syntax.json
```

These fixtures are retained. They cover syntax recognition, not semantic checking.

The matchtype runner also invokes `--inspect-ir`: it requires exit 0, empty
stderr, and valid `lyric-ir-1` JSON. In its named fixture, `main` must contain
exactly the `selected` call, no subject or unselected call, and no runtime `If`.
Three deliberate document corruptions verify these comparisons reject defects.
Function/node IDs and formatting are not compared. This inspection check and
its controls are retained alongside the execution and rejection cases.
