# Consolidated Chapter 5 code review

## Verdict and scope

No required runtime/compiler implementation correction found for the agreed Chapter 4 subset on the documented verified host. One low-severity documentation correction is required below. Optional coverage and portability suggestions are not new completion gates.

This is the single consolidated source review, not a runtime execution or an approval/re-review campaign. I explicitly activated `codebook-code-reviewer` and `coding`, read project files through authorized absolute-path read-only commands from my assigned sandbox, made no project edits, launched no children, installed nothing, and ran no compiler, grader, or generated executable.

Inspected: `book/chapter-05.md`; Chapter 4 prerequisites and subset contract; relevant language-spec arithmetic, evaluation-order, byte-string, and output provisions; `graders/ch05/README.md` and `run.py`; `tools/spin05.py`; `Makefile`; all of `compiler/Emission/Compilation.cs`; Common emission/model/semantic/type/syntax/token declarations; semantic registration, expression checking, type conversion, symbol/scope/fact lifetime implementations; Model literal access and token ownership; Driver emission integration; project configuration and compiler README; the specified retained evidence files.

## Required correction

### R1 — Correct the compiler overview's stale capability statement (teaching/documentation, low severity)

**Location:** `compiler/README.md:1–9`, particularly line 8, versus its own `:38–40` and `:102–106`; actual implementation `compiler/Driver/Program.cs:51–67`.

The heading still calls this the Chapter 4 increment and the opening overview states, “It does not execute programs or emit C.” The compiler now does emit C, while deliberately not launching the C compiler or executing the resulting program. The later README text correctly describes this boundary, making the opening statement contradictory.

**Consequence:** A student using the module overview receives an incorrect account of the public capability and pipeline. Update the heading/opening to the current increment and distinguish C emission from the separate external compilation/execution steps. This is not a lowering defect and requires no architecture redesign.

## Source findings supporting the implementation

### 1. Shared vocabulary, authoritative facts, and lifetime

`compiler/Common/Emission.cs:6–19` declares both cross-module operations as accessible partial members with contracts. Their bodies reside in Emission and Model. `compiler/Lyric.csproj:3–8` selects .NET 10, C# 14, one ordinary assembly, and warnings-as-errors.

`EmitC` has local dictionaries, lists, counters, and traversal stacks (`compiler/Emission/Compilation.cs:22–27,277–280`), not a second persistent backend graph. Symbol names use identity keys (`:141–148`); signatures traverse the function's existing ordered parameter symbols (`:163–173`). No second lookup pass or copied signature owner is introduced. Calling `SemanticTypes` or using `SemanticFailure` is through Common declarations (`Common/SemanticTypes.cs:26–43`, `Common/SemanticModel.cs:184–188`), not access to private spoke implementation helpers.

Builtin identity is an explicit value-like enum and readonly symbol field (`Common/SemanticModel.cs:53–60,84–99`), assigned when standard symbols are constructed (`Semantics/Check.cs:82–87`, `Semantics/SemanticSymbol.cs:48–59`) and used through the resolved call fact (`Emission/Compilation.cs:350–368`). Source spelling is not re-resolved at emission.

The new literal accessor does exact-span binary search over source-owned tokens, validates complete length and decoded-value presence, and returns a read-only span (`Model/Model.cs:94–121`). It neither reparses escapes nor adds persistent storage. The token's decoded bytes are explicitly immutable value-like data (`Common/Token.cs:3–16`). Its lifetime restriction is stated beside the declaration.

Existing constructors register through complete two-way operations; refusal checks precede mutation, and later construction failures unwind earlier memberships. Relevant paths include `Semantics/SemanticFact.cs:33–62,68–115`, `Semantics/SemanticSymbol.cs:22–42,48–83`, and `Semantics/FunctionScope.cs:7–26,62–81`. Analysis destroys facts before scopes and re-reads shrinking ownership collections (`Semantics/SemanticModel.cs:41–58`); scope destruction detaches weak children without destroying them and removes its function/analysis memberships (`:137–154`). Compilation cleanup retires analysis before destroying sources (`Model/Model.cs:37–50`). Chapter 5 introduces no new persistent relationship requiring another destruction path.

### 2. Emission boundary and supported constructs

Driver parses, checks in executable mode, then emits (`Driver/Program.cs:51–67`) and always destroys the compilation through `finally` (`:97–100`). `EmitC` documents its successful-executable-check precondition; its live-analysis check is not a replacement checker. A library-only checked compilation is outside that precondition, not a required supported invocation.

C generation is buffered; caught backend `SemanticFailure` returns its located diagnostic before publication (`Emission/Compilation.cs:22,129–134,175–179`). All accepted statement and expression forms were compared with `Semantics/Check.cs:194–240,574–690`. General negation, unsupported syntax, and unchecked statements after return are rejected by Chapter 4 rather than silently reaching an incomplete emitter. Floating type identities in signatures are supported; floating literals/arithmetic remain outside the agreed subset. Classes and other deferred features are not missing Chapter 5 work.

### 3. Evaluation order and control flow

`Expression` uses completion frames and pushes children in reverse order (`Emission/Compilation.cs:277–299`). A call schedules arguments, not its non-value callee. Each completed expression receives a typed temporary (`:375–378`), including variable reads, so subsequent sibling effects cannot change the previously evaluated operand. Binary helpers and ordinary calls consume only these values (`:336–368`), avoiding dependence on C argument evaluation order.

Constant short-circuit lowering is justified by the actual checker: constants arise from literals, direct signed literals, groups, and arithmetic with two constant operands, not calls or remembered local initializers (`Semantics/Check.cs:581–619,631–649,651–684`). Skipping their children cannot skip supported effects.

Else-if condition generation is inside the emitted else body (`Emission/Compilation.cs:382–404`). Binding initializers and assignment right sides precede the corresponding store/declaration (`:424–437`). Unit has a real struct representation, and zero-parameter signatures use `(void)` (`:54,150–173,330–332`), preserving the difference between a unit argument and no arguments.

### 4. Integer arithmetic, every width and sign

The emitter generates add/subtract/multiply helpers for every integer enum member (`Emission/Compilation.cs:212–275`). Bounds use exact semantic values, are cast to operand type before guard arithmetic, and the actual operation occurs only after the failure branch (`:222–271`).

Unsigned subtraction checks `a < b`; addition and multiplication use subtraction/division bounds. Signed addition/subtraction use sign-dependent representable thresholds. Signed multiplication covers all four nonzero sign quadrants; short-circuit conditions exclude zero divisors, and none of the chosen divisions evaluates `MIN / -1`. For negative/negative multiplication, `a < MAX / b` correctly uses C's truncation toward zero with negative `b` to identify the overflowing region.

For small promoted operands, a successful guard guarantees the mathematical result fits the original Lyric type, hence also fits the promoted signed type when promotion is to `int`. Unsigned larger types remain guarded against Lyric overflow even though C itself would wrap. Positive bounds spelled with `UINT64_C` do not contaminate signed comparisons because of the explicit typed casts. Literal emission separately handles signed-64 minimum without spelling an out-of-range positive signed token (`:185–196`).

The operation helper is selected by the expression fact (`:344–348`), while binding/result/call conversions use the declared destination. Chapter 4 allows only whole-range integer widening (`Semantics/SemanticTypes.cs:83–112`), so ordinary C conversion preserves accepted values; a wider destination cannot rescue an overflowing narrower operation.

### 5. Byte storage, trace lifetime, and output failure

Literals become static const unsigned-byte arrays and length-bearing values (`Emission/Compilation.cs:312–325`). The sentinel is excluded from length. Static lifetime permits returning literal-backed values; copying this immutable representation is observationally adequate for the agreed nonmutating subset. It is not general mutable array value semantics, a limitation Chapter 5 explicitly teaches.

Each function links an automatic activation on entry (`:411–416`). Explicit returns evaluate their value first, restore the parent, then return (`:450–455`); unit fallthrough likewise restores the parent (`:461–464`). Conditional blocks use the containing function activation rather than introducing trace frames. This keeps the current function visible during a failing return expression and removes completed functions from later traces.

Program bytes use length-based `fwrite`, optional LF, and checked `fflush` (`:76–93`). Overflow and output failure emit located diagnostics and traverse the active trace before exit 1 (`:65–90`). Diagnostic display text is JSON-quoted then emitted as fixed three-digit octal-escaped UTF-8 bytes (`:198–210,344–345,360–361,413`). Executable startup ignores SIGPIPE where defined and checks signal setup failure (`:115–123`); it is not hidden library initialization. Stderr delivery remains best-effort, as documented.

## Tests and retained evidence

The public harness invokes an independent compiler command, checks emission status/stderr/output, builds C at O0 and O2, and checks exact runtime status/stdout/stderr (`graders/ch05/run.py:112–178`). The closed-pipe reader is closed before launch (`:137–155`), so the failure is not scheduled by a race. Arithmetic fixtures use parameter operands and result observability through checked endpoint operations (`:40–86`), rather than merely testing constant folding or accepting arbitrary failure. `operand-order` and `returned-frame` are retained controls (`:89–96`). No mocks or fixture-specific compiler branches were found.

`Makefile:31–44` builds/runs retained suites in Debug and Release with fail-fast shell behavior. PEG remains separately required, accurately documented. `spin05` depends on Release build (`Makefile:26–27`); its helper checks each stage and exact intended runtime failure and uses disposable temporary files (`tools/spin05.py:26–59`).

Inspected evidence, not newly executed evidence:

- `book/notes/ch05-checks/output-cumulative.txt:7–9,182–184`: both builds succeeded with zero warnings/errors.
- The same log `:97–101,272–276` contains retained token/syntax/semantic summaries; `:177,352` reports **75/75 execution cases passed** for each configuration. Harness source establishes that each execution case must pass both C optimization levels.
- `book/notes/ch05-checks/peg-corrected.txt` ends with **143/143 grammar fixtures passed**.
- `book/notes/ch05-checks/spin05.txt:12–31` records the greeting, `ABonetwo`, located intended overflow with calc/main trace, and temporary-file cleanup.

The assigned historical UBSan **72/72** result predates the latest signal changes and final controls. I do not claim a current 75-case sanitizer pass, nor did I re-execute the historical baseline closed-pipe failure or its correction. The log evidence is not proof of human walkthrough acceptance, student reconstruction, exhaustive arithmetic coverage, or universal portability.

## Optional suggestions and remaining limits

1. **Focused retained coverage, not a rewrite:** `graders/ch05/run.py:40–96` predominantly uses same-type arithmetic. A mixed-width case such as u32 with i64, and a narrow operation overflowing before return to a wider result type, would directly guard the conversion/destination-width distinction. A unit-parameter call would strengthen the real-unit-value test at `:32`. Source inspection currently supports these paths; missing fixtures alone are not implementation defects.
2. **State the target I/O boundary explicitly:** the runtime writes to C's initially text-mode stdout (`Emission/Compilation.cs:78–82`) while the chapter requires exact bytes. The documented verified host is macOS ARM64. This review does not establish exact byte behavior on hosts whose text streams translate LF, notably Windows CRT defaults. If broader host support is intended, explicit binary-mode handling and platform verification need a deliberate follow-on decision; otherwise document the current target boundary. This is not a request to add Windows support to the chapter.
3. **No proof beyond inspected paths:** source reasoning does not exercise signal-setup failure, simultaneous stderr failure, exhaustive operand pairs, or ownership refusal/allocation-failure paths. The public tests and logs do not claim those proofs either.

## Lessons

- Arithmetic safety requires proving the guard itself safe as well as proving the accepted operation fits the original Lyric width after C promotions. Checking the result after signed overflow would be too late.
- A checked stdio write path needs the operating-system signal policy to let it run: broken-pipe SIGPIPE can preempt the diagnostic unless executable startup handles it.
- Temporary emission maps are algorithm storage, not a competing semantic model. Existing resolved symbols and type facts remain authoritative, and no new persistent graph is necessary for this increment.

The author can incorporate the documentation correction and choose any optional tests directly; this report requests no additional review cycle.
