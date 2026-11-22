# Chapter 1 reconstruction trial completion

Workspace: /Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/lyric-ch01-student-01
Solution: compiler/Lyric.csproj (C# 14, net10.0, SDK 10.0.403).
Read-only inputs: /Users/bill/ensemble-edition3-cadence/data/agents/Cadence/trials/lyric-ch01-inputs-01

## Actual verification
All commands used the workspace as cwd and sourced env.sh for sandbox-local HOME, DOTNET_CLI_HOME, NUGET_PACKAGES, TMPDIR and caches. NuGet.Config clears sources; no packages installed.
Host: /Users/bill/.local/share/ensemble-dotnet10/dotnet

- Debug build: host build compiler/Lyric.csproj -c Debug; exit 0, zero warnings/errors. logs/build-debug.log.
- Debug grader: python3 /Users/bill/ensemble-edition3-cadence/data/agents/Cadence/trials/lyric-ch01-inputs-01/graders/ch01/grade.py -- host compiler/bin/Debug/net10.0/Lyric.dll; exit 0, 82 passed, 0 failed. logs/grade-debug.log.
- Release build: host build compiler/Lyric.csproj -c Release; exit 0, zero warnings/errors. logs/build-release.log.
- Release grader: same absolute grade.py -- host compiler/bin/Release/net10.0/Lyric.dll; exit 0, 82 passed, 0 failed. logs/grade-release.log.
Here `host` abbreviates the absolute executable above, not a literal shell command.
- Additional real-model diagnostics: diagnostics/ModelChecks.csproj; 16 passing assertions/refusal checks. logs/model-checks.log. These are extra student checks, not retained public requirements.
- Table generation: tools/generate_table.py; 21 merged intervals, Bidi_Control inclusion verified; hashes/provenance/license retained in compiler/Unicode. logs/generate-table.log.
- SDK version: logs/sdk-version.log. First build printed an SDK workload verification notice; no workload update was attempted. Build succeeded.

## Structural self-check
Reviewed Common/Model.cs, Common/SourcePolicy.cs, Model/Lifetime.cs, Source/Scan.cs and Driver/Program.cs directly after implementation.
Common holds persistent state and externally callable partial declarations. Functional bodies use this vocabulary; orchestration is in Driver. Partial types/constructors compile in one assembly into the same objects, without runtime wrappers. SourcePolicy is immutable value-like shared data; Diagnostic is immutable value-like error data. Compilation and SourceFile are relationship-bearing.
Compilation alone changes collection membership and the source backlink. Addition validates both endpoints before mutation, performs potentially allocating list insertion before setting the backlink, and rejects duplicate/dead/already-owned sources. Construction prepares fields before registration. Removal validates membership and detaches both ends without destroying the source. Source destruction calls parent removal, then closes its lifetime. Compilation destruction closes admission before cascading through remaining sources. Repeated destruction is harmless. Diagnostic tests exercised these cases, including failed construction leaving no membership and wrong-parent refusal leaving both ends unchanged.
SourceFile owns original bytes by exclusive-transfer contract; it does not expose or mutate them. The caller must honor this transfer (it is not enforced by copying). Driver does so. One Rune-based scanner performs both silent validation and subsequent emission over those bytes. UTF-8 errors are strict; CRLF changes only the event scalar/width, not stored bytes. Pinned Unicode policy avoids host category changes. Scalar columns and original byte offsets remain separate. The driver catches read errors narrowly, quotes diagnostic filenames safely, and destroys the compilation in finally on success or failure. No owned running work exists.

## Lessons and teaching feedback
The supplied Chapter 1 teaching and public contract sufficed without implementation assistance. Particularly useful construction guidance: two-pass scanning prevents partial stdout without a duplicate event model; source-byte offsets differ from scalar columns; pinned Unicode membership must accept legal neighboring characters; removal and destruction are different model operations. These lessons were also stated in chat.
No target solution, reference generator or private history was inspected. No fixtures/graders were edited. No questions or parent implementation assistance were needed. Chapter 0 references a grammar review that was not in the supplied bundle; Chapter 1 required no parsing decision from it. No teaching correction or grader defect identified.

## Limits and stopping boundary
Chapter 1 only; no tokenization, Chapter 2, deployment, network calls or descendants. All 82 Chapter 1 checks are retained and passed in both configurations; Chapter 0 has no executable cases. Public tests are representative, not exhaustive UTF-8 enumeration. Agent source review and diagnostic checks do not establish human understanding or acceptance. Human Take it for a Spin walkthrough and acceptance remain separate and unperformed by the human in this trial. No live jobs remain. Stop here; no automatic repeat trial.
