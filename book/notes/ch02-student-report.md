# Chapter 2 independent reconstruction report

## Location and scope

Workspace: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/lyric-ch02-student-01`
Implementation: `compiler/Lyric.csproj`, with source in Common, Model, Source, Unicode,
Lexer, and Driver. Own Unicode generator: `tools/generate_unicode.py`.
Copied only the accepted prerequisite/compiler from the authorized input bundle.
Implemented Chapter 2 independently using its public teaching, contracts, lexical
specification/PEG/review, official Unicode files, and read-only graders.
No Chapter 2 solution, private history, external services, network, installations,
delegated work, reviewers, or Chapter 3 work were used.

## Exact checks and evidence

Commands ran from this workspace after `source tools/environment.sh`, which sets:

- DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet
- GRADERS=/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch02-student-input/graders
- HOME, DOTNET_CLI_HOME, NUGET_PACKAGES, TMPDIR inside this workspace.
- PYTHONDONTWRITEBYTECODE=1, telemetry/first-time experience disabled.

```sh
"$DOTNET" build compiler/Lyric.csproj --nologo --tl:off
python3 "$GRADERS/ch01/grade.py" -- "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 "$GRADERS/ch02/unicode.py" -- "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 "$GRADERS/ch02/tokens.py" -- "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
"$DOTNET" build compiler/Lyric.csproj -c Release --nologo --tl:off
python3 "$GRADERS/ch01/grade.py" -- "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
python3 "$GRADERS/ch02/unicode.py" -- "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
python3 "$GRADERS/ch02/tokens.py" -- "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
python3 "$GRADERS/ch01/check_comparisons.py"
"$DOTNET" run --project diagnostics/Structural.csproj -c Release --tl:off
bash tools/spin.sh
```

Both builds exited 0 with zero compiler warnings/errors. Debug printed an SDK
workload-verification advisory; no installation was attempted or needed.
All six compiler grader runs exited 0:

| Suite | Debug | Release |
|---|---:|---:|
| Chapter 1 source cases | 82/82 | 82/82 |
| Token cases | 58/58 | 58/58 |
| NFC vectors | 99,826 | 99,826 |
| XID scalars | 1,112,064 | 1,112,064 |
| Malformed Unicode requests | 7/7 | 7/7 |

The retained comparison selfcheck passed. The separate structural harness passed
22 assertions/refusal checks, exit 0. It compiles the actual production component
sources, excluding Driver. This is supplementary evidence, not a retained grader.

`tools/spin.sh` is the exact public Take it for a Spin block, executed with the
sandbox environment. It showed integer 123u16 with suffix u16, string value [255],
separate f-string text/interpolation markers, then LYC101 at byte 0 / line 1 /
column 1 for decomposed cafe+accent, with composed café suggested. No success output
was emitted for rejection. Temporary source files were removed by the walkthrough.

Evidence: logs/build-{debug,release}.log, logs/{debug,release}-ch01-grade.log,
logs/{debug,release}-ch02-{unicode,tokens}.log, logs/comparison-selfcheck.log,
logs/structural.log, logs/spin.log, logs/generate.log. Full tool command evidence
also remains under jobs/. No failed behavioral checks or grader defects observed.

## Architecture and construction explanation

Common declares shared vocabulary, persistent fields, constructors and callable
partial methods. Functional implementation directories supply the bodies in one
assembly. No runtime service glue or sibling implementation dependencies are used.

Compilation strongly owns sources; SourceFile strongly owns ordered tokens. Every
child has a backlink. Source bytes remain the sole source authority. Token spelling
is derived on demand from its byte span; decoded literal bytes represent a different
fact and are value-like, immutable by transfer contract rather than holder-tracked.

Construction initializes token data then calls AddToken. AddToken refuses destroyed
objects or an existing parent before mutation, appends, then sets the backlink.
It does not rescan a growing token list. Wrong-parent removal refuses unchanged;
valid removal searches from the end, removes membership and clears the backlink.
A detached live token can attach elsewhere. Token destruction removes itself and
marks itself destroyed, with an early return on repeated calls.

Lex first destroys previous tokens, then validates the entire original source.
It never lets comments or literal trimming hide forbidden source characters.
Recognition keeps byte cursor, scalar position, member-dot state and a lexical
stack local. Typed numbers consume their complete suffix boundary, so 123u16 needs
no parser feedback; magnitude conversion/range checking is deliberately deferred.
F-string text and expression markers nest on the same stack as ordinary delimiters.
Decoded escapes are never rescanned as delimiters or source characters.

Failed lexing can leave partial tokens, still completely owned. Driver emits
records only after full success and always destroys its compilation in finally.
Source destruction first detaches from Compilation and then destroys remaining
tokens from the end. Tokens perform their own complete detach. Reverse removal
makes the normal cascade linear; repeated destruction is harmless.

Unicode tables are generated from the pinned official Unicode 16 input, not host
normalization APIs. XID uses binary range search plus explicit underscore. NFC
recursively performs canonical decomposition (including arithmetic Hangul), stable
combining-class ordering, then blocked composition. For A + U+0305 + U+030A, both
marks have class 230. Overline does not compose with A and remains an intervening
class-230 blocker, so the ring cannot compose. Without overline, A+ring becomes Å.
Consumed marks do not become blockers. Both cases have direct structural checks.

For `(\n{\nx\n}\n)`, the top parenthesis suppresses the first newline; the nearer
brace admits the next two; closing that brace reveals the parenthesis and suppresses
the following newline. The harness confirms exactly two newline tokens. No parser
or unconditional outer-parenthesis suppression is involved.

## Assistance and teaching feedback

No implementation assistance or clarification was required beyond the public inputs.
The chapter supplied sufficient guidance to reconstruct the increment and pass all
published checks on the first implementation run. Its especially useful details were
complete suffix consumption, no rescan of decoded escapes, consumed-mark blocking,
nearest-delimiter newline policy, and end-first token destruction.

The copied prerequisite README was stale for Chapter 2; it was replaced with an
accurate module/lifetime/verification explanation. No grader changes were made.
One intentionally retained interface oddity is the Chapter 1 usage diagnostic:
it still lists only source inspection and Unicode version even though new modes
exist, because the retained public contract requires that exact text. The new modes
are documented in the Chapter 2 contract rather than changing that diagnostic.

Lessons: source validity and Unicode-data validity are separate boundaries; decoded
literal values are not duplicate source authority; complete ownership plus deferred
output makes failed-lex cleanup simple; CLI conformance alone cannot prove lifecycle
correctness, so direct refusal/detach/destruction checks are necessary.

## Limits and stopping boundary

Agent execution of the walkthrough is not human acceptance. Human collaborator use
remains outstanding. Representative token tests and structural checks do not prove
all possible input combinations. This remains inspection, not parsing/type checking
or code generation. No installation or publication occurred. The passing trial is
complete within the agent's assigned scope; no automatic rerun or Chapter 3 advance.
