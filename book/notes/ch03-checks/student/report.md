# Chapter 3 independent reconstruction report

## Result and locations

Completed the assigned Chapter 3 syntax increment, extending only the supplied accepted Chapter 2 compiler. No Chapter 4, semantic approval, execution/code generation, installation, publication, or human acceptance is claimed.

Base directory: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/lyric-ch03-student-01`

- Implementation: `workspace/compiler/`
- Public copied layout: `workspace/`
- Evidence: `logs/`
- Supplemental real-component probes: `diagnostics/Lifecycle.cs` and `diagnostics/Lifecycle.csproj`
- Runtime environment: `runtime-env.sh`

No delegation, reviewers, reference solution, private repair history, git, downloads, or network work was used. Public specification/grammar/graders were inspected as authorized. This is one reconstruction, with a local correction before final cumulative success.

## Actual verification

`make test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet` exited 0. Complete log: `logs/make-test.txt`.

| Required check | Debug | Release |
|---|---:|---:|
| Build | 0 warnings/errors | 0 warnings/errors |
| Chapter 1 source | 82 passed | 82 passed |
| Unicode NFC vectors | 99,826 passed | 99,826 passed |
| Unicode scalar XID classifications | 1,112,064 checked | 1,112,064 checked |
| Unicode malformed requests | 7 passed | 7 passed |
| Chapter 2 token cases | 58/58 | 58/58 |
| Chapter 3 exact trees | 66/66 | 66/66 |
| Chapter 3 recognition | 143/143 | 143/143 |
| Chapter 3 boundaries | 7/7 | 7/7 |

The comparison helper self-check also passed. The boundary suite includes deep serialization and parser resource diagnostics, distinct from ordinary grammar rejection.

The required Node command, using this workspace's adjacent Release assembly, exited 0 with **143/143 grammar fixtures passed**:

```
. ./runtime-env.sh
cd workspace
OPENSSL_CONF=/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/lyric-ch03-student-01/diagnostics/openssl-empty.cnf \
DOTNET="$DOTNET" "$NODE" tools/peg/test.mjs graders/ch03/cases.json
```

Complete successful log: `logs/peg.txt`. Startup-blocker log retained as `logs/peg-startup-blocked.txt`.

`make spin03 DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet` exited 0. Complete log: `logs/spin03.txt`. It displays hello-world syntax, multiplication grouped inside addition, and an unfinished expression rejected with LYC201 and no partial tree. This is an automated walkthrough, not a claim that Bill tried or accepted it.

The supplemental real-component Release probe exited 0 with **18 lifecycle assertions passed** (`logs/lifecycle.txt`). It compiles actual compiler sources, excluding the driver; it uses no mocks. Assertions cover backlinks, reparse destruction, atomic cycle/duplicate/foreign/source-transfer refusals, refusal to remove a live source-owned node, weak child detachment without cascade, source clearing, failed parse cleanup, generic rollback, owner cascade and idempotence.

## Construction and architecture

`Common/Syntax.cs` declares the closed 108-kind syntax vocabulary, persistent node/source fields, relationship operations, parsing/serialization interfaces, and a value-like parse-failure diagnostic carrier. Implementations are matching partial types in `Syntax/Model.cs` and `Parser/SourceFile.cs`. The driver orchestrates inspection and destroys the compilation in its existing finally block.

Every syntax node is strongly registered with one source through AddNode; exact spelling remains an offset/length into original bytes. Ordered tree edges are weak and retain a single parent backlink. Add/remove methods validate before mutating both ends. Live source transfer is refused. Destruction detaches tree edges, marks the node destroyed, and unregisters it; source cleanup iterates its ownership collection rather than recursively deleting weak children. Failed parses clear unfinished nodes and publish no root.

Parser state is local to a Parse invocation. A checkpoint records token position, the pending second closing angle from a `>>` token, and source-owned node count. Ordinary syntax failure restores the cursor and destroys the node suffix. Resource exhaustion does not backtrack. Speculative generic suffixes finish and pass continuation lookahead before attaching a pre-existing expression base. Tokens are not rewritten to split closing angles.

Binary expressions use precedence climbing with the next-higher right minimum for left association. Recursive calls have a budget; the serializer uses an explicit stack and an explicit independent JSON nesting limit. This matters because a long flat source expression can produce a deep left-associated tree even with shallow active parsing recursion. Separators preserve semicolon discard markers; declarations, types, patterns, interface/relation roles and bindings remain syntax, not semantic decisions.

The source lexer clears earlier syntax before rebuilding tokens. Reparse therefore cannot leave nodes pointing into stale token objects; syntax anchors refer to the authoritative source bytes instead. Inspection modes call the real lexer/parser and retain earlier source/token/Unicode inspection modes.

## Correction and lessons

The first focused Debug run passed 66/66 trees and 7/7 boundaries but recognized only 141/143 cases correctly. It incorrectly accepted raw triple strings as import paths and pattern literals. The lexer intentionally uses the same string token kind for ordinary and raw strings. The correction was an OrdinaryString predicate consulting anchored original spelling, used only where the grammar specifies ordinary StringLiteral. Expressions still accept raw strings. No fixture or grammar was changed. Full cumulative Debug/Release success followed this correction.

Lesson: token category equality does not imply every grammar context accepts every literal spelling in that category. The grammar/tree contracts were sufficient to diagnose this; no hidden implementation assistance was needed.

Lesson: rollback must restore all cursor state and owned speculative nodes, while preserving existing edges; testing only accepted trees would miss refusal/cleanup obligations.

## Setup assistance and teaching feedback

.NET 10.0.403 ran. Its initial first-use output included development-certificate setup and a workload verification notice; no manual install, download, or workload update was attempted. Writable HOME and CLI caches are confined to this sandbox, and subsequent commands disable telemetry/first-time setup.

Node initially aborted on inaccessible libuv aliases. The parent supplied the authorized DYLD_LIBRARY_PATH consisting only of already-granted actual library directories. That made `node --version` report v24.20.0. Running the real PEG workload then exposed an OpenSSL configuration read denied at `/opt/homebrew/etc/openssl@3/openssl.cnf`. The parent explicitly authorized an empty sandbox-local configuration file, with OPENSSL_CONF set only for the offline PEG subprocess. That command then passed. No host configuration or grants were changed. Already-passing suites were not rerun for this environment-only adjustment.

Lesson: a version probe does not exercise all runtime initialization; validate the real offline workload early. A useful setup note for future sandbox students is to probe Node's actual JavaScript startup as well as its version and document bounded library/config environment requirements.

No genuine language-contract ambiguity or grader defect required a correction. The raw-string distinction is a useful teaching emphasis, not a request to broaden grammar acceptance. The chapter's public contracts supported reconstruction without hidden code.

## Integrity and limits

`logs/public-before.sha256` and `logs/public-after.sha256` cover the same **186 noncompiler public input files**, including public graders and bundled PEG dependencies. `logs/public-integrity.txt` records **0 changed**. Only compiler implementation files were modified within the copied bundle; diagnostics/logs/environment/report are outside it.

These are bounded tests, not a proof of every production combination or unbounded input capacity. PEG shares the compiler's lexer, so it is independent syntax recognition evidence, not an independent lexer. Retained source/token/Unicode checks provide the separate lexical evidence. Lifecycle probes are supplementary evidence, not an exhaustive state-space proof or retained public grader change. No semantic validity, target execution, human walkthrough acceptance, installation, or deployment is claimed. Stop boundary: Chapter 3 complete.
