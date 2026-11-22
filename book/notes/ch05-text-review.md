# Consolidated Chapter 5 text review

## Verdict and scope

One required, bounded teaching correction: complete the inherited type-mapping explanation for floating-point signature identities. The chapter otherwise teaches the demonstrated C-emission increment coherently and accurately. Optional clarifications below are not new features, acceptance gates, or a request for another review cycle.

I explicitly activated `codebook-text-reviewer` and `coding`. This was source/evidence review only: no compiler, grader, generated executable, installation, child agent, or project edit. Project files were read by absolute-path commands from my own sandbox. I read all of `book/chapter-05.md` and Chapter 4, the Chapter 5 grader README and run.py, Makefile, tools/spin05.py, compiler/Common/Emission.cs, compiler/Emission/Compilation.cs, relevant semantic declarations/checking/type conversion, literal access, driver wiring, and the specified prior review and retained logs. The requested Common path is actually under compiler/. No separate voice-guide file was located; book/README.md and the Preface supplied the construction/voice guidance. I did not inspect private earlier student history or derive requirements from it.

## Required correction

### R1 — Complete the mapping for already-supported signature types

**Location:** `book/chapter-05.md:9,48–50` (subset description and translation-unit construction).

**Problem:** The chapter says to emit Chapter 4's checked subset and all user-function signatures, but its concrete mapping explains integers, bool, unit, and later strings only. Chapter 4 already includes f32/f64 type identities in signatures, while expressly excluding floating literals and arithmetic. A reader following only the new mapping can omit these accepted signatures or infer that floating types are all deferred.

**Evidence:** `book/chapter-04.md:23` distinguishes floating signature identities from unsupported floating expressions; `:98` retains f32-to-f64 widening. `compiler/Common/SemanticTypes.cs:19–20` declares F32/F64. `compiler/Emission/Compilation.cs:150–173` maps them to `float`/`double` and uses that mapping for every function signature. Its function loops at `:96–110` do not eliminate unused functions. For example, an otherwise empty executable may also declare `func identity(x: f32) -> f32 { return x }`; the declaration still needs a C signature and body even without a floating literal or a call from main. This example is reasoned from the existing contracts/source, not newly executed evidence.

**Why it matters:** Reconstruction must preserve the previous accepted subset, not merely satisfy the new fixtures. This is a missing instruction about implemented behavior, not a request for floating arithmetic or broader language support.

**Suggested correction:** At the type-mapping paragraph, explicitly retain f32-to-C-float and f64-to-C-double for the already-checked signature/value paths, and repeat that floating literals and operations remain unsupported. A short clause in the opening subset description can avoid implying otherwise. Keep the existing verified-host qualification rather than turning this into a claim about every C implementation.

## Optional teaching improvements

### O1 — Explain where widening happens in generated C

**Location:** `book/chapter-05.md:48–52,80,86,108–110`.

The chapter correctly says to use checked types and that a wider receiver does not widen an operation. One connecting explanation would make that easier to reconstruct: choose the arithmetic helper from the binary expression's fact, not the destination or either child's type; its typed parameters convert operands to that operation type. Bindings, assignments, calls, and returns use their declared destination types. Ordinary C conversions suffice for the accepted integer cases because Chapter 4 permits only whole-range widening, not value-dependent narrowing.

**Evidence:** `compiler/Emission/Compilation.cs:344–348,375–377,424–454` and `compiler/Semantics/SemanticTypes.cs:83–112`; the prerequisite rules are already in `book/chapter-04.md:98–100`. Therefore this is optional reinforcement, not a missing language contract or a demand for conversion nodes/a second backend model.

### O2 — Make the case/execution distinction explicit

**Location:** `book/chapter-05.md:130`.

The 75 named retained cases are correctly counted. The following general statement that they compile and execute at both optimization levels is slightly imprecise: two are emission rejections and deliberately never reach C compilation. `graders/ch05/README.md:69–71` already states the exception; `run.py:120–178` implements 73 executable cases at O0/O2 plus two rejection cases per compiler configuration.

Suggested wording: “The 73 executable cases compile and run at both -O0 and -O2; the two rejection cases require the existing diagnostic and no C output.” This improves interpretation of evidence without changing the suite. Do not describe the 75/75 summary as 75 distinct generated executables.

### O3 — Refresh progress metadata when incorporating this review

**Location:** `graders/ch05/README.md:3` and `book/README.md:17`.

The former still says no chapter review has occurred; the latter lists code review as pending, although `book/notes/ch05-code-review.md` exists and its required compiler README correction is present. Updating these status lines during normal incorporation would keep the public roadmap consistent. Preserve the actual outstanding student and human acceptance states; this is bookkeeping, not an additional review gate.

## Assessment of the core teaching

- **Problem and architecture:** `chapter-05.md:11–44` explains why C is a target rather than semantic authority, why publication is buffered, and why the emitter consumes existing symbols/facts rather than resolving again. Common declares the operations (`compiler/Common/Emission.cs:6–19`); Emission and Model supply bodies. Temporary names/work stacks/spans introduce no persistent ownership graph. The existing graph and destruction prerequisites are taught in Chapter 4, so Chapter 5 need not reprint them.
- **Literal access and strings:** The exact-span read-only accessor explanation matches `compiler/Model/Model.cs:94–121`: binary search of ordered tokens, complete-span/value validation, no escape reparsing. Static immutable unsigned-byte storage, explicit lengths, empty-array sentinel, and returned-string lifetime match `Emission/Compilation.cs:312–325`. The distinction from future mutable array value semantics is explicit and important, not deferred silently.
- **Evaluation order:** `chapter-05.md:66–92` gives a reconstructible postorder algorithm: completion marker, reverse child pushes, argument-only call traversal, typed result temporaries, and capture of variable reads before later siblings. This matches `Emission/Compilation.cs:277–380`. Constant bypass is justified by checker facts (`Semantics/Check.cs:581–649,651–684`): no call or remembered local value is treated as an exact constant. Else-if conditions stay inside the alternative; return expressions run before trace unlinking. These are reasons and construction steps, not merely desired outputs.
- **Arithmetic:** `chapter-05.md:94–110` correctly gives unsigned guards, signed addition/subtraction guards, all four signed multiplication quadrants, zero handling, and the MIN/-1 division hazard. It also explains typed bounds and C promotions. The generated helpers at `Emission/Compilation.cs:212–275` implement these formulas before computing the operation. The signed-64 minimum spelling is separately explained. No substantive guard or evaluation-order correction found.
- **Runtime lifetime and failures:** `chapter-05.md:112–124` accurately distinguishes automatic C activation links from persistent C# model relationships, explicit return from implicit unit fallthrough, and fatal exit from cleanup/unwinding. Length-based output, flush failure, safe diagnostic display, and executable-startup SIGPIPE policy match the source. The broken-pipe lesson is integrated where readers build output handling rather than left as a detached patch diary. The newly incorporated host text-stream limitation appropriately avoids claiming Windows byte equivalence.
- **Commands and retained checks:** `Makefile:31–44` builds Debug and Release and runs the comparison self-check and retained suites through Chapter 5. The separately required PEG command is correctly retained in the prose. Public `run.py` accepts an independent compiler command, checks exact status/bytes, rejects crashes/timeouts/arbitrary failures, and does not demand C formatting or generated names. The sanitizer invocation is explicitly supplementary. No hidden requirement or new class/runtime feature is introduced.
- **Walkthrough:** `make spin05` depends on Release (`Makefile:26–27`); the helper performs separate emission, C compilation, and execution with status checks and disposable files (`tools/spin05.py:26–59`). The three advertised demonstrations and exact intentional overflow match the helper. The human walkthrough is correctly distinguished from automated execution. There are no quoted student questions/answers in Chapter 5 to audit, and no private authoring conversation is required to understand its construction instructions.

## Evidence and limits

Inspected retained evidence, not rerun results:

- `book/notes/ch05-checks/output-cumulative.txt:7–9,182–184`: successful builds with zero warnings/errors. `:95–101,270–276`: retained source, Unicode, token, syntax, parser-boundary, and semantic results. `:177,352`: 75/75 Chapter 5 cases for each compiler configuration. Harness source establishes the dual optimization checks for the 73 executable cases.
- `book/notes/ch05-checks/peg-corrected.txt` ends with 143/143 grammar fixtures passed.
- `book/notes/ch05-checks/spin05.txt:12–31` records greeting, ABonetwo, located intended overflow with calc/main trace, and cleanup. This is author walkthrough evidence, not human acceptance.
- `book/notes/ch05-checks/ubsan.txt` ends with the historical 72/72 result. It predates the final 75-case suite; no current 75-case sanitizer pass is claimed.

The Chapter 5 student trial follows this review. This prose/source review cannot establish independent reconstruction. Human Chapter 5 walkthrough is not done; Chapter 4 human walkthrough was explicitly deferred. Neither status is silently converted into a pass or made a new prerequisite to this authorized review. The checks do not establish exhaustive arithmetic correctness, all ownership failure paths, every host's I/O behavior, or universal runtime memory safety.

## Lessons

1. Preserving a checked subset includes emitting its accepted type signatures even when literals of those types are not yet implemented; absence of floating arithmetic does not remove floating signature identities.
2. Overflow safety requires safe guard arithmetic as well as a safe accepted operation, including C promotion and signedness behavior.
3. Test totals describe cases, not necessarily executions; negative emission cases intentionally stop before generating an executable.

The author can incorporate R1 directly and choose whether the optional clarifications help. This is the one consolidated text review; no re-review or new execution campaign is requested.
