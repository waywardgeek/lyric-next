# Independent Chapter 5 reconstruction — complete

## Scope and provenance

Chapter 5 ONLY, frozen public `ch05-student-input` bundle; continued the passing Chapter 4 prerequisite compiler. No reference implementation, private history, other child workspace or reviewer report was inspected. No delegation, network, downloads, installs, git, deployment, or Chapter 6 work. Explicit activation of spec-execution, coding, visible-reasoning and code-tools completed before implementation. No parent implementation assistance or patches were requested; supplied toolchain/environment setup guidance was used.

Sandbox/candidate root: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/lyric-ch05-student-01/work`.
Readonly import: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch05-student-input`.

All 212 INPUTS.sha256 entries verified before copying layout, and reverified at completion. Input evidence: `logs/input-integrity.txt`, `logs/final-integrity.txt`. Final work snapshot matches 209 original entries; the three differing entries are authorized compiler modifications. Public teaching, graders, fixtures, Makefile, PEG, offline Peggy dependencies, and runtime helpers remain unchanged. `candidate.sha256` lists every candidate compiler source/project file and both built DLLs. Hash paths are relative to work/.

## Implementation

Modified:
- `compiler/Common/Semantics.cs`: shared BuiltinIdentity vocabulary, declaration builtin identity, executable-analysis completion state.
- `compiler/Semantics/Check.cs`: assign builtin identity on standard declarations and mark successful executable checks only at successful completion.
- `compiler/Driver/Program.cs`: --emit-c pipeline, check before emission, publish buffered output only on success, preserve finally destruction.

Added:
- `compiler/Common/Emission.cs`: declared Compilation.EmitC and SourceFile.LiteralBytes contracts.
- `compiler/Source/LiteralBytes.cs`: binary search ordered tokens for exact source span; return borrowed read-only decoded bytes.
- `compiler/Emission/Compilation.cs`: real checked-graph C11 lowering and generated runtime.

The emitter requires a live successful executable analysis; it never silently rechecks. The driver owns I/O and compilation destruction. Semantic facts and symbols remain authoritative. Generated symbol names, work stacks, temporary values, and the source-ordered function list are local emission data, not persistent backend state. Resolved symbols determine calls and builtin dispatch; signatures use ordered parameter declarations. String data comes from existing decoded tokens, not repeated escape parsing.

Expression lowering is iterative postorder: push parent completion then children in reverse so their actual evaluation is left-to-right. Each effect is a statement and each variable read is snapshotted. Constants use exact semantic BigInteger facts. Return values are evaluated before unlinking runtime activation records. Condition temporaries remain in their actual branch, including else-if alternatives. Unit has a real C struct representation, floating signature/value paths map to float/double, and integer destination widening does not change the operation's checked type.

Runtime byte strings use static const byte arrays plus lengths, including embedded zero/invalid UTF-8. Diagnostic display strings are JSON-quoted then encoded with three-digit octal escapes. Runtime helpers check arithmetic before performing operations; type-cast bounds prevent unsigned promotion from corrupting signed checks. Print checks fwrite, newline, and fflush; startup ignores SIGPIPE where defined so closed pipes reach LYR002. Automatic activation records link on function entry and unlink on explicit return and implicit unit fallthrough; no permanent call history is retained. Buffer publication occurs only after all lowering succeeds; unsupported lowering reports LYC401 with null output.

## Commands and results

Run commands from the sandbox root unless a subshell explicitly changes to work. `env.sh` captures the sandbox-local HOME/DOTNET_CLI_HOME/NUGET_PACKAGES/XDG_CACHE_HOME/TMPDIR and required .NET flags. DOTNET is `/Users/bill/.local/share/ensemble-dotnet10/dotnet`. CC is explicit Apple Clang with `-isysroot /Library/Developer/CommandLineTools/SDKs/MacOSX26.4.sdk -B/Library/Developer/CommandLineTools/usr/bin`. No host cache or config writes were requested. Node setup is isolated in `node-env.sh`, using the supplied resolved Cellar library directories and sandbox `openssl-empty.cnf`.

1. Initial Debug build failed: unintended duplicate ExecutableChecked field was inserted into LexicalScope by an overbroad replacement; compiler CS0649 exposed it. Corrected only that duplicate. Preserved `logs/build-debug-first.txt`.
2. Corrected Debug build: exit 0, zero warnings/errors. `logs/build-debug-second.txt`.
3. Initial targeted immutable Chapter 5 grader, Debug assembly: exit 0, 75/75. `logs/ch05-debug-first.txt`. Command shape: `python3 "$IMPORT/graders/ch05/run.py" "$DOTNET" work/compiler/bin/Debug/net10.0/Lyric.dll` after sourcing env.sh.
4. Clean cumulative verification (job 644, exit 0):
   ```sh
   . ./env.sh
   "$DOTNET" clean work/compiler/Lyric.csproj -c Debug --nologo --tl:off
   "$DOTNET" clean work/compiler/Lyric.csproj -c Release --nologo --tl:off
   (cd work && make test DOTNET="$DOTNET" GRADERS=/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch05-student-input/graders)
   ```
   Combined log `logs/cumulative-clean-test.txt`; extracted summary `logs/cumulative-summary.txt`. Clean/build operations had zero warnings/errors. Grader comparison self-check passed. In EACH Debug and Release:
   - Chapter 1: 82 passed, zero failed.
   - Chapter 2 Unicode: 99,826 NFC vectors, XID classification of all 1,112,064 Unicode scalars, seven malformed requests passed.
   - Chapter 2 tokens: 58/58.
   - Chapter 3 trees: 66/66; recognition: 143/143; boundaries: 7/7.
   - Chapter 4 semantic cases: 79/79.
   - Chapter 5: 75/75: 73 executable cases each at -O0 and -O2 plus two emission rejections.
5. Separate PEG (job 651, exit 0):
   ```sh
   . ./env.sh
   . ./node-env.sh
   /opt/homebrew/Cellar/node@24/24.20.0/bin/node work/tools/peg/test.mjs /Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch05-student-input/graders/ch03/cases.json
   ```
   `logs/peg143.txt`: compiled all 176 canonical rules and adapter, negative controls passed, 143/143 fixtures passed. Unchanged adjacent grammar/compiler layout and offline Peggy; adapter invokes this candidate's Release assembly.
6. Spin (job 652, exit 0): `. ./env.sh; (cd work && make spin05 DOTNET="$DOTNET")`. `logs/spin05.txt`: Release build clean; hello output, ABonetwo effect order, and expected located i8 overflow with calc/main trace passed. Temporary programs removed by walkthrough.
7. Final manifest/hash verification (job 653, exit 0): `logs/final-integrity.txt`, `candidate.sha256`.

Pipeline logging used `set -o pipefail` and tee, so successful log capture did not conceal a failed test exit. Full tool reports additionally remain under jobs/ in this sandbox. A command invocation was refused before execution for missing fresh explanation (job 643), then executed as job 644. A mistaken read glob `work/book/ch05*` failed (job 649); corrected exact read of `work/book/chapter-05.md` succeeded. No actual C or Node sandbox execution refusal occurred.

## Structural self-review and lessons

- All new persistent state is declared in Common. Functional implementations operate through Common partial declarations. No functional module imports another module's implementation-only helper; TypeRules operations are existing Common declarations.
- No new compiler ownership relationships were introduced. Existing source/syntax mutation invalidates analysis; lexer ClearSyntax precedes token replacement. Literal spans are borrowed only during immediate byte serialization. EmitC validates analysis ownership/completion/liveness.
- Destruction remains compilation-owned and driver finally-driven; emission introduces no owned work or persistent references requiring additional destruction.
- Runtime activations are separate from the compiler graph. Every generated explicit return restores its parent after expression evaluation, including nested returns, and unit fallthrough restores it too. Retained returned-frame and overflow trace tests passed.
- Arithmetic sign guards were reviewed: short circuit excludes zero divisors; chosen division forms avoid MIN/-1; guarded result fits the original Lyric type before C operation. This is design review plus tests, not exhaustive formal proof.
- Lesson: broad text replacement can alter multiple structurally similar declarations; check all matches and prefer exact anchors. The initial compiler failure was corrected without hiding its log.
- Lesson: integrity proves starting provenance, not compiler behavior; actual C compile/run and PEG checks are separate evidence.
- Lesson: checked stdio alone does not handle POSIX closed pipes unless SIGPIPE policy lets it return failure; startup policy is explicit rather than a hidden library effect.
- Lesson: temporary expression maps solve sequencing without duplicating semantic authority; runtime frame cleanup must follow return expression evaluation but precede return.

## Limits and teaching feedback

No consequential unclear requirement or suspected grader defect required parent clarification. The supplied Chapter 5 instructions were sufficient to reconstruct this increment from the allowed prerequisite. They clearly distinguished float signature preservation from unsupported float arithmetic, and the no-rechecking boundary required explicit completion-mode state in this prerequisite model.

Actual target evidence is the provided macOS/Apple Clang environment; no universal C host portability claim, Windows binary-stream guarantee, general mutable-array semantics, threading claim, or whole-runtime memory-safety proof. Optional sanitizer suite and supplemental diagnostic harness were not run. Lifecycle/LYC401 internal API paths were reviewed in source, not separately invoked by a new internal test harness. Human acceptance was not performed. Nothing installed or published. All mandatory checks passed; stopped without rerunning a fully passing trial or starting Chapter 6. No live jobs remain.
