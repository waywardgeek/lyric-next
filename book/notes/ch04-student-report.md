# Chapter 4 independent reconstruction

## Delivery

Workspace: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/lyric-ch04-student-01/work`.
Compiler: `work/compiler`; Release assembly: `work/compiler/bin/Release/net10.0/Lyric.dll` (Debug adjacent).
Started from the supplied passing Chapter 3 compiler. Explicitly activated spec-execution, coding, visible-reasoning and command tools. Only authorized public teaching/contracts/graders/prerequisites were inspected. No reference implementation, private history, delegation, network, package installation, git, publication, or Chapter 5 work.

## Implementation and structural review

- Added Common/Semantics.cs declarations and semantic graph vocabulary; Semantics/Model.cs implements complete relationship operations and constructor unwind. Compilation owns analysis; analysis owns scopes/facts; scopes own declarations; function signatures additionally belong to functions. Lexical edges are weak. Facts link both to syntax and resolved declarations.
- Semantics/Check.cs performs top-level collection, complete signatures, then body checks. Builtins are ordinary declarations. Temporary definite/possible assignment sets are local algorithm state; continuing paths govern conditional joins. Block exit drops flow entries but retains declarations.
- Expression hint discovery and arithmetic checking use explicit stacks. Hints cannot diagnose or reorder actual checking. Integer exact constants live only on facts, never variable declarations. Semantics/Types.cs uses whole-range inclusion and selects an existing operand type, not a fabricated common type.
- Source/SemanticLocations.cs obtains spelling and original byte/scalar coordinates through source ownership. Driver adds --check and --check-library while preserving prior modes.
- Model/Model.cs and Syntax/Model.cs invalidate retained analysis at successful source/tree changes. Refused mutations validate first. Semantic annotation attachment does not invalidate construction.
- Self-review inspected declarations, graph constructors/destructors, mutation paths, driver cleanup, flow joins, iterative expression passes, type ranges and location calculation. Persistent semantic state is declared in Common; implementations introduce no hidden persistent fields. Destruction re-reads maintained collections, removes facts before scopes, and detaches both directions. Diagnostic values outlive failed graph cleanup.

Changed existing compiler files: Driver/Program.cs, Model/Model.cs, Syntax/Model.cs. New compiler files: Common/Semantics.cs, Semantics/Model.cs, Semantics/Types.cs, Semantics/Check.cs, Source/SemanticLocations.cs.

## Verified results

All listed final commands exited 0. Full logs are in `logs/`.

- Initial bundle INPUTS.sha256 verification: all entries passed (`input-hashes.log`).
- Clean Debug and Release (`clean-debug.log`, `clean-release.log`), then `make -C work test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet GRADERS=/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch04-student-input/graders`: both builds passed with 0 warnings/errors (`make-test.log`). Comparison-helper self-check passed. In EACH configuration: 82 source cases; 99,826 NFC vectors; all 1,112,064 Unicode scalars for XID; 7 malformed requests; 58 token cases; 66 exact trees; 143 recognition cases; 7 parser boundaries; 79 semantic cases passed.
- Separate actual Node PEG workload: `node work/tools/peg/test.mjs work/graders/ch03/cases.json`, SDK override and supplied library paths, sandbox-local empty OPENSSL_CONF: grammar negative controls and 143/143 fixtures passed (`peg.log`). Harness resolves this workspace's adjacent Release compiler assembly. Offline bundled Peggy used unchanged.
- `make -C work spin04 DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet`: passed all three silent successful checks plus expected located LYC307 rejection (`spin04.log`). This was the student's walkthrough, NOT human acceptance or program execution.
- Real-class linked-source lifecycle diagnostic: `diagnostics/Lifecycle.csproj`, 27 assertions passed (`lifecycle-final.log`). Covers both function and analysis ownership cleanup, weak lexical detach, wrong-owner refusal, constructor unwind for declarations/scopes/facts, refusal preserving analysis, source/tree invalidation, successful retained facts, reparse invalidation, idempotence, and semantic-failure cleanup preserving its diagnostic.
- Final SHA-256 comparison: all 186 noncompiler public files unchanged, including graders, teaching, fixtures, contracts, Makefile and PEG harness/dependencies (`noncompiler-integrity.log`). Compiler sources/Debug+Release assembly hashes: `compiler-sha256.txt`.

The initial focused semantic run also passed 79/79 (`semantic-initial.log`); no failing semantic case or grader correction was needed. The complete passing trial was not unnecessarily repeated.

## Lessons, feedback, assistance, limits

The public teaching was sufficient for this reconstruction without parent implementation assistance. No ambiguity or defective grader required escalation. In particular, explicit stack traversal and continuing-path joins supplied useful construction guidance; retained graph ownership required separate lifecycle checks beyond CLI success.

One local diagnostic example initially used `fn` and an invented printing builtin, failing parsing after its relationship assertions passed (exit 134 from the diagnostic's assertion). Corrected only that diagnostic to valid `func` syntax and a simple integer expression. The failed log is preserved as `lifecycle-invalid-example.log`; it was not counted as an expected compiler rejection. Lesson: test scaffolding must respect the actual language contract too. Other lessons: exact literal values do not license variable narrowing, weak lexical relationships must not cascade, and PEG syntax independence remains valuable after adding semantic checks.

Initial SDK startup printed certificate/workload first-run notices despite first-experience suppression. No explicit installation/trust/workload command was invoked; subsequent environment disables certificate generation. HOME, CLI home, caches and temporary files remained sandbox-local. Full command job outputs are preserved under jobs/ as well as named result logs.

These tests establish the published behavior and focused model invariants, not correctness for every possible program. Unsupported forms remain explicit boundaries; this chapter does not execute or lower programs. No human acceptance, deployment, or publication claimed. No unresolved required check or live job remains.
