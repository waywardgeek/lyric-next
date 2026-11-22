# Independent Chapter 8 reconstruction — complete

Solution: `work/compiler/` (28 source/project files). Accepted independent Chapter 7 prerequisite extended by this same student. No reference solution, delegation, network, installation, publication, grader/fixture changes, or Chapter 9 work.

## Implementation

- Refreshed prerequisites: resolved ordinary `assert(bool)` builtin and always-on fatal assertion helper; analysis-owned unsafe policy shared by constant checking/emission; storage-observing `capacity`, including grouped references and nested selection invalidation; resize-zero releases capacity while truncate retains it.
- Nominal classes: Common declarations and authoritative state, matching partial Semantics implementations, class-owned member scopes, field/method/constructor lookup, initialization checks, nominal type dependencies and reverse lists. Relationship replacement validates and reserves before detaching; construction failure and logical destruction use complete two-way operations.
- AoS and SoA share semantic checking and index-handle source behavior. AoS uses fixed 64-record chunks; SoA grows parallel field buffers. Per-class metadata/free lists reuse slots before growth. State distinguishes free/live/destroying. Destruction runs final then reacquires storage by index before field cleanup and reuse.
- Counted handles extend the existing emitter owner stack: retain on copy, empty source on move, release on cleanup. Arrays retain/drop class leaves. Direct field assignment saves the handle across RHS evaluation and reselects storage afterward. Discarded counted results release at the discard statement.
- Lifetime graph extends existing borrow analysis with class destruction/growth, field/formal substitution and ordered obligations, scoped holder occupancy, cleanup/final-hook effects, and validity transfer before cleanup. Empty holders contribute no release effects. Fresh constructor-self field selections carry explicit provenance through aliases and substitutions; existing-object field effects remain outward effects.

## Final commands and results

Every shell sourced `../setup-env.sh` using its absolute root path, then changed to `work/`. Pinned offline SDK and external `$GRADERS` used. Offline restore previously succeeded with `"$DOTNET" restore compiler/Lyric.csproj --configfile ../NuGet.Config`.

- Clean Debug and Release, followed by `"$DOTNET" build compiler/Lyric.csproj -c <configuration> -tl:off --no-restore`: exit 0, zero warnings/errors. Logs `final-clean-{Debug,Release}.log`, `final-build-{Debug,Release}.log`.
- `make test DOTNET="$DOTNET" GRADERS="$GRADERS"`: exit 0, final log `cumulative-final.log`, status `cumulative-final.exit`. BOTH Debug and Release passed all retained gates through Chapter 8, including comparison/Chapter 1 checks, Unicode (99,826 NFC vectors, all 1,112,064 scalar XID classifications, 7 malformed requests), token58, trees66, recognition143, parser-boundaries7, semantic82 (`--chapter=8`), Chapter5 runtime80, Chapter6 runtime250, unsafe87 (`--chapter=8`), arrays112, Chapter8 initial runtime6, static154, layout212, allocation4, ownership12.
- `DOTNET="$DOTNET" "$NODE" tools/peg/test.mjs "$GRADERS/ch03/cases.json"`: exit 0, 143/143; `peg-final.log` and `.exit`.
- `make spin08 DOTNET="$DOTNET"`: exit 0; `spin08-final.log` and `.exit`. Scripted shared-object/independent-array demonstration and expected growth-borrow rejection succeeded. This is not human acceptance.

Compiler and fixtures frozen during final runs. Intermediate failed and successful diagnostic logs retained separately. The earlier `cumulative-01.log` also passed, but predates the independently found freshness correction; the final cumulative run supersedes it.

## Review-found correction and assistance

1. Reported that public unsafe grader counted private helper spellings incompatible with accepted prerequisite naming. Parent corrected external `ch06/unsafe.py` and README, removing naming/count requirements while preserving behavioral/emission checks. Revised public manifest digest: `23122938012fb283140416a1f9e142a97ff41e35f2ca6153823e680c6e686e03`. No student edits to graders. Static-inline helper structure and unsafe direct arithmetic bypass were source-reviewed separately; passing name-independent tests does not prove them.
2. Source review after public static154/full cumulative success found overly broad constructor field-effect suppression. Asked parent whether mutation through an existing same-class constructor argument is supported; parent confirmed yes, and only proven fresh-self effects may be omitted. No implementation patch supplied. Private counterexample `diagnostics/constructor-existing-field-fixed.ly` previously accepted; after path provenance correction it rejects at stale element use with LYC326. Evidence: `constructor-gap-confirmed.log`, `constructor-gap-after.log`, `fresh-path-static.log`. Initial diagnostic mistakenly used `length`; retained setup failure separately in `constructor-gap-before.log`, corrected to `len` before drawing conclusions.

## Teaching feedback / lessons

- Storage-observing capacity requires matching emitter selection and borrow analysis; parentheses preserve identity rather than force a copy.
- Unsafe exceptional constants must decline folding, not merely suppress diagnostics and then execute an invalid host arithmetic operation.
- Existing ownership machinery can support counted objects without a parallel cleanup system, but discard statements need explicit result cleanup.
- Cleanup safety needs both value validity and possible holder occupancy. Tail/result transfer must precede scope cleanup; empty arrays/holders cannot be assumed occupied merely from type.
- Constructor fresh-self exceptions need provenance. Same-class field identity alone does not distinguish newly allocated self from an existing parameter. Public tests passed before this source-review correction; retain/teach the counterexample for future execution.
- Allocation/ownership plateau checks establish bounded reuse and balanced payload cleanup, not complete process-shutdown reclamation.

## Hashes and evidence limits

`source-sha256.txt`: 28 files; manifest SHA-256 `f343c9df9173eff2df912e06fb213b642c18fe7aa9fbfafc16d96c5a33c63ee2`.
`artifact-sha256.txt`: 10 Debug/Release files; manifest SHA-256 `81a866d3674ea6c341f8b611066311f583740b37ae1a3fd92a996815af1663e1`.
Both manifests verified after spin08 with exit 0; individual hashes and verification logs are retained.

Optional sanitizer not run. No human interactive acceptance, installation, deployment, or publication performed. No claim of exhaustive lifetime proof beyond source review, focused diagnostic, and public gates. Assignment boundary reached; no automatic rerun or Chapter 9.

## Parent verification and closure

Cadence inspected the final report, exit receipts, cumulative summaries, PEG and walkthrough output. Both configurations show the published inventories. All 28 source/project hashes and all 10 artifact hashes were freshly verified against the saved manifests without rerunning the solution. The child was then shut down with `stopped: true`. Selected evidence is retained in `ch08-checks/student/`; original complete evidence remains in the student workspace.

This is clarification-assisted independent success, not unassisted proof of the original text. The construction chapter incorporates the constructor-provenance clarification; the corrected public unsafe grader no longer requires private helper names. No student implementation was supplied or edited by the parent. No second reviewer, post-pass execution, or Chapter 9 work was requested. Human walkthrough and acceptance remain pending.
