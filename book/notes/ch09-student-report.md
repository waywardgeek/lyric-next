# Chapter 9 independent reconstruction

Project: `work/compiler` (accepted Chapter 8 prerequisite extended personally; no descendants or reviewers).

## Design and self-review

- `Common/IR.cs` owns shared declarations, persistent state and partial method signatures. `IR/Model.cs`, `Lowering.cs`, `Validation.cs`, and `Inspection.cs` implement those operations in the same assembly.
- Strong ownership is analysis -> program -> functions -> arena nodes. Region children are a separate ordered tree; operands are explicit two-ended use objects; selected declarations, nominal type declarations and source syntax maintain dependency backlinks. Destroying a definition disconnects uses without destroying consumers. Function destruction visits all arena nodes, including detached drafts. Semantic/syntax retirement invalidates dependent IR.
- Lowering uses an action stack, actual checked symbols/types and lexical holder inventories. It emits transfers and cleanup explicitly. Constructor arguments precede allocation. Condition lifetimes span the loop body and false-path cleanup occurs in the condition, never unconditionally after the loop. Fallthrough cleanup is emitted only for continuing blocks.
- Validation separately checks structure, operation contracts, and reachable definition/pending-transfer order. Publication revalidates the current graph. This is NOT a full alias-sensitive liveness, storage initialization or ownership-balance proof.
- C body emission uses only IR nodes and ordered children. It predeclares temporaries, assigns them at execution positions, resolves a Place through its saved handle, captures an Address once, and emits cleanup only from explicit operations. Runtime helpers implement mechanisms; they do not keep a second scope-owner inventory. Generated destruction IR calls the final hook, releases fields, then reclaims. Runtime wrappers dispatch to that body.
- Chapter 8 refresh: checked class backing requests double; ordinary arrays retain their existing reserve policy. AoS chunks use raw allocation and only live records are initialized.

## Local diagnostics

`work/probes/Probe.cs` compiles the actual implementation modules with a separate diagnostic entry point. `logs/model-probe-02.log`: 17 checks passed, including cross-function refusal without mutation, detached publication refusal, matching literal payload, use-before-definition, definition/use disconnection, detached arena destruction, syntax and semantic identity invalidation, analysis destruction, pending-transfer abandonment, duplicate transfer consumer, and repair/revalidation.

## Commands and evidence

Every shell call sources `env.sh` from this assigned root. It sets the supplied offline tools, isolated HOME/cache/temp and GRADERS pointing to the unchanged read-only bundle.

- Offline restore: `"$DOTNET" restore work/compiler/Lyric.csproj --configfile work/NuGet.Config`; `logs/restore.log`.
- Initial focused public runs: `python3 "$GRADERS/ch09/ir.py" "$DOTNET" work/compiler/bin/Debug/net10.0/Lyric.dll` (21/21), analogous `ownership.py` (92/92), `c_emission.py` (33/33 at O0/O2 with body markers). Logs `ir-02.log`, `ownership-01.log`, `c-emission-01.log`. These preceded final cleanup fixes; final cumulative run is authoritative.
- Model: `"$DOTNET" run --project work/probes/Probe.csproj -c Debug`; `logs/model-probe-02.log` (17 checks).
- Walkthrough: `make -C work spin09`; `logs/spin09-02.log` (exit 0, exact helper ownership sequence, expected C output, invalid source diagnostic without partial JSON).
- Independent grammar: `(cd work; "$NODE" tools/peg/test.mjs)` after sourcing env; `logs/grammar-direct-01.log` (50/50 and negative controls). Final no-build run also passed; see `logs/grammar-final.log` and `.exit`.
- Full unchanged public campaign: `make -C work test`; `logs/full-test-02.log` and `.exit` (exit 0). It builds/tests both Debug and Release, including all retained suites and all Chapter 9 graders.

## Failures and lessons

- Initial build failed due to a guessed SourceFile owner-field name and C# switch pattern-name collision; corrected to actual `Parent` and a distinct name. Preserved `build-ir-01.log`.
- Initial scalar inspection exposed an unnecessary unit constant from statement-block completion; removed. Preserved `ir-01.log`.
- Initial spin09 found an extra unreachable FreeArray after Return. Execution evaluators stop at Return, so this required the inspection walkthrough. Corrected fallthrough cleanup to respect checked continuation facts. Preserved `spin09-01.log`.
- A self-review tightening initially treated cleanup storage like Move temporary ownership; corrected before final campaign because field Places must also support explicit release. Lesson: storage holders and owning temporary values are distinct categories.
- `make grammar-check` aborted starting Node because macOS make stripped DYLD_LIBRARY_PATH; preserved `grammar-spin09-01.log`. Direct execution with the prescribed environment passed. Parent confirmed this unchanged-script equivalent is acceptable; no imports widened, grader edits, downloads or host changes.
- First cumulative campaign was deliberately stopped before cleanup corrections; `full-test-01.log` is partial, not a pass. Initial grammar/spin builds may have overlapped that discarded campaign. Final campaign has no competing product builds.

## Assistance and teaching feedback

No implementation patch, target source, private review/history, other student directory, descendant, reviewer, network download or paid call was used. Parent assistance concerned only the Node loader setup/equivalent command. Public teaching, fixtures and graders were inspected freely. Current Chapter 8 made the disclosed storage-growth prerequisite correction clear. The bounded validator contract and explicit Place/Address distinction were essential; the spin09 exact sequence usefully checks unreachable lowering residue beyond execution behavior.

## Limits

Human acceptance remains separate. Optional sanitizer run not performed. No optimizer, CFG, SSA, new language features, generator, permanent AST fallback or Chapter 10 work added. Final evidence follows.

## Final frozen campaign

`make -C work test` completed with exit 0 (`logs/full-test-02.exit`). Both Debug and Release passed every unchanged public retained suite and Chapter 9: 21/21 scalar IR, 92/92 ownership IR, 33/33 C-emission cases including body markers and O0/O2 execution. Builds reported zero errors. See full log for individual retained counts.

After the campaign, without rebuilding, `(cd work; "$NODE" tools/peg/test.mjs)` passed 50/50 fixtures and its negative controls (`logs/grammar-final.log`, exit 0); `(cd work; python3 tools/spin09.py "$DOTNET")` passed (`logs/spin09-final.log`, exit 0). Compiler sources stayed frozen throughout this campaign and these checks.

`logs/input-integrity.log` verifies all 33 frozen product input hashes and all 246 bundle SHA256SUMS entries. `SHA256SUMS.final` records implementation sources, Debug/Release artifacts, local probe sources/artifacts, execution resources, environment, this report and retained logs; it excludes mutable caches, obj, temporary files, and itself. No work remains running. Human acceptance is not implied.
