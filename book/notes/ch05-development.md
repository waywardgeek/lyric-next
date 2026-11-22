# Chapter 5 development

Bill authorized Chapter 5 C emission and execution, explicitly deferring classes. Chapter 4 human walkthrough is deferred by agreement, not accepted by inference. Preserve all uncommitted Chapter 3 and 4 work; no commit or push authorized.

Start with --emit-c FILE: semantic checking precedes emission, stdout contains one self-contained C translation unit, C compilation remains an explicit external action. The initial independent Python execution grader compiles generated C at -O0 and -O2 and checks exact bytes, status and stderr. No emitted-source formatting oracle.

Evaluation-order experiment requested by Bill: Apple Clang 21 ARM64 produced first then second for receive(first(), second()) at both -O0 and -O2. TCC was absent from PATH, not tested or installed. Probe used separately sequenced printing inside functions, not conflicting unsequenced scalar writes. This result is bounded to the tested compiler/target/options. Lyric requires left-to-right effects; emit temporaries rather than assume C argument order. Calling convention and expression evaluation order are distinct.

Initial execution cases cover hello world, empty main, byte-preserving output including NUL/0xFF/UTF-8, forward calls, and observable argument ordering. They precede implementation. Classes, loops, general arrays and other unsupported semantic forms remain deferred; checked runtime arithmetic is part of this chapter's intended increment, not yet implemented.

## First executable milestone

Implemented `--emit-c` in the driver, Common/Emission declarations, Emission/Compilation implementation, source-owned read-only decoded-literal lookup, and an explicit BuiltinKind on semantic symbols. C emission buffers before stdout, follows checked symbols/facts, assigns C-safe temporary names, emits prototypes for forward calls, and uses an explicit expression work stack to materialize effects left to right. Conditional expression work stays inside its branch. Unit is a tiny C value; immutable byte spans preserve arbitrary bytes and may be returned safely because literal storage is static. This is NOT mutable array value semantics; mutation remains unsupported.

Generated per-type addition/subtraction/multiplication guards test bounds BEFORE the operation. Signed bound casts prevent accidental unsigned comparison/conversion; signed multiplication guards split by operand signs and avoid zero or MIN/-1 division. Checked constants use exact facts with a special INT64_MIN spelling. Automatic generated function frames provide a Lyric call trace and are removed on normal returns; fatal overflow exits without cleanup. Output checks fwrite/fputc/fflush, but closed-pipe signals and located output diagnostics still need finishing/testing.

Evidence: initial five execution cases all passed at O0/O2; expanded ten (branches, returned bytes, Unicode names, unit values) passed. Arithmetic baseline 10/42 explicitly rejected Binary; implementation 42/42 passed. Expanded boundary suite initially 71/72 because the authored constant-overflow fixture incorrectly expected column27; actual '+' is column29, independently counted, fixture corrected. Final `make test` Job565 exit0 (both Debug and Release, all retained suites plus72/72 execution cases per config), and Clang UBSan Job566 exit0 (same72 cases at O0/O2). Logs in ch05-checks. Initial edit had a missing closing C# string quote; build refused and execution did not run until corrected. All builds after correction zero warnings/errors. No claim of GNU GCC or TCC runtime verification; local cc is Apple Clang21 ARM64.

No chapter body/reviews/student yet. Next: retained closed-pipe output failure test and runtime handling, more effect-order/trace-lifetime controls and bounded long-expression check as needed, then verified walkthrough and teaching. Cumulative Make now includes ch05/run.py; separate retained PEG comparison remains required. No commit/push. Chapter4 human walkthrough deferred by agreement.


### Output failure and operating-system signals

A retained closed-pipe fixture closes the reader before child launch. The old
binary passed 72/73 and died with signal 13, empty stderr: stdio return checks
alone did not deliver the promised diagnostic. Generated executable startup now
ignores SIGPIPE where available (checking signal setup failure), allowing checked
writes/flush to report located LYR002 and the active Lyric trace. The rebuilt
Debug compiler had zero warnings/errors and passed 73/73 at both C optimization
levels. Logs: `ch05-checks/closed-pipe-baseline.txt`, `output-build.txt`,
`output-execution.txt`. This is real pipe behavior, not a fake output stream.
Added operand-effect order and completed-frame removal controls afterward;
those additions still await the next run. Diagnostic output is best-effort if
stderr is itself broken. No cleanup guarantee on fatal exit.


### Verified execution boundary and teaching draft

Job575 `make test` exited zero: clean Debug/Release builds, all retained suites,
and 75/75 execution cases in each. Saved `ch05-checks/output-cumulative.txt`.
The two added controls passed: binary operand effects AB and completed-function
frame removal before later overflow. Job577 `make spin05` passed; output saved
in `ch05-checks/spin05.txt` shows hello Ken/Tom, ABonetwo, and located i8 overflow.
This is author verification, not Bill's walkthrough. Separate PEG invocation
first used wrong filename recognition.json (setup failure, log `peg.txt`), then
corrected to the published cases.json: Job579 exit0,143/143 in `peg-corrected.txt`.
Chapter5 teaching now exists with ownership/access paths, iterative expression
lowering, arithmetic guard formulas, trace cleanup, SIGPIPE lesson, exercises,
and tested walkthrough. Link/fence checks and whitespace pass. No reviews or
student trial launched yet; no commit/push. Earlier UBSan72/72 predates output
failure changes and the final75-case suite; do not relabel it as current75/75.

## Single code review incorporated

The source-only reviewer found no required compiler/runtime correction for the supported subset on the verified host. Its required stale compiler README correction was independently found and fixed while the review ran: Chapter 5 emits C but does not launch compilation or execution. Full report preserved in ch05-code-review.md; reviewer stopped. Added the optional host-I/O caveat at the chapter printing explanation: Windows text-stream translation is not verified or implemented. Optional extra fixtures remain suggestions, not new gates. Existing 75-case cumulative and 143-case PEG evidence remains current; no executable source changed during review. Next: single text review, then fresh student reconstruction.

## Single text review incorporated

Required correction explains inherited f32/f64 signature and value paths in the C type-mapping construction; floating literals/arithmetic remain unsupported. Incorporated optional explanation of operation types versus receiving conversions, exact 73 executable plus two rejection case counts, and progress metadata. No executable source or graders changed. Full report ch05-text-review.md preserved. Lesson: preserve accepted unused signatures, not just executable examples. Next freeze public input bundle and launch fresh reconstruction from preserved Chapter 4 student sources.

## Fresh reconstruction admitted

Student lyric-ch05-student-01 admitted (job626), confined with only frozen public bundle and runtime read-only imports. Bundle /Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch05-student-input has 212 hashed inputs including 23 byte-identical prerequisite files from preserved Chapter4 student, not reference Ch5 compiler. Manifest SHA256 72216adda7133818d8544e6d5ccad86d3509f4897a3080e2752611cc93b9e2a5. No private notes/reviews/build artifacts or symlinks. Runtime imports: existing .NET SDK, Apple CommandLineTools, resolved Node/Homebrew libraries. Supplied prior Node DYLD/empty local OpenSSL setup guidance. New sandbox C compilation still needs actual verification. Student owns implementation; author may clarify requirements and correct teaching/graders, never edit candidate. Both reviewers stopped. Public status edits after freeze are bookkeeping only; frozen inputs preserved.

## Fresh reconstruction passed

Student lyric-ch05-student-01 submitted through join635 and was stopped by job658. Saved report: ch05-student-report.md; original candidate and logs remain in its preserved child directory. Parent inspected the report and saved cumulative, PEG, walkthrough, and integrity evidence without rerunning or editing the passing candidate. Both configurations built clean and passed all retained suites, including 75 Chapter5 cases (73 executable at O0/O2 plus two emission rejections), 79 semantic cases, and full earlier Unicode/parser checks. Separate PEG143 and spin05 passed. Actual sandboxed C and Node execution worked with supplied runtime setup; no requirement clarification, implementation assistance, or grader correction was needed. Initial duplicate-field build error was corrected by the student and retained in its logs. Optional sanitizer/internal API probes were not run by the student.

Parent independently verified all212 frozen manifest entries, manifest hash unchanged, and all189 noncompiler public candidate files byte-identical. Both reviews were already complete. No candidate rerun, commit, push, installation, or Chapter6 work. Human walkthrough and acceptance remain pending; Chapter4 walkthrough remains explicitly deferred. Lesson: input hashes establish provenance, while saved compiler/execution results establish behavior; neither substitutes for human acceptance.

## Human acceptance and performance direction

Bill reported, “Chapter 5 looks good.” Chapter 5 is accepted and closed after his reading. This does not claim a human-run walkthrough; earlier pending statements describe historical milestones. No student rerun or compiler change accompanied acceptance.

During reading, Bill corrected the backend rationale: C is the intended long-term performance backend, not a temporary convenience before LLVM IR. The chapter now explains that direction, the ambition for compiler-managed SoA in real applications, and his reported Rune experience, without presenting those expectations as measurements from this increment.

Bill considered selective temporary emission, then explicitly deferred it until measurements show that optimization is needed. Keep the current explicit left-to-right evaluation and ownership behavior. Lesson: do not add effect-analysis complexity to solve an unmeasured performance problem; any later elision must preserve value snapshots, checked failures, and ownership as well as visible mutations.

## Generated C readability correction

Bill found numeric literal output difficult to inspect. Inspection showed the greeting once as decimal bytes, while the repeated octal strings were diagnostic source filenames. The emitter now spells ordinary ASCII visibly, uses conventional escapes and bounded hex escapes for other bytes, protects against C11 trigraphs, and declares program literal templates as uint8_t arrays. Chapter 5 and Chapter 7 teach the corrected representation. Ownership is unchanged: the Chapter 7 literal copy creates an owned array from the static template; eliminating it remains deferred optimization. Lesson: exact byte preservation does not require unreadable text.

Verification: Release build zero warnings/errors; 75/75 Chapter 5 and 102/102 Chapter 7 execution cases. A focused Apple Clang probe at O0/O2 checked recognizable hello-world text appearing once, readable diagnostic paths, all 256 bytes each followed by A9, and the trigraph sequence ??/ with trigraphs enabled. No student rerun or full cumulative-suite claim.
