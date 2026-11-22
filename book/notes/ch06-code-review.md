# Chapter 6 — consolidated code review

## Disposition

Three required correctness corrections were demonstrated against a fresh Debug build of a sandbox copy of the current compiler. The implementation is not yet correct for the agreed Chapter 6 contract despite the author's retained 238-case passes. This is one consolidated review, not a request for another review or an approval loop. No project files were edited.

## Required corrections

### R1 — RHS effects bypass immutable-assignment validation (high)

**Location:** `compiler/Semantics/Check.cs:545–554,578–591`, `CheckAssignment`; actual stores are emitted at `compiler/Emission/Compilation.cs:725–749`.

```lyric
func main() {
    let x: i32
    x = if true { x = 1; 2 } else { 3 }
    if x == 2 { print("twice") }
}
```

**Observed:** emission succeeds with empty stderr. Generated C builds without diagnostics and exits 0 printing `twice` at both `-O0` and `-O2`. Thus the immutable storage receives 1 and then 2 on the same execution path.

**Cause:** the outer assignment checks `possiblyAssigned` before checking the RHS. Checking the conditional RHS updates that set, but the outer assignment never validates immutability against the resulting continuing state before marking/storing the binding. Chapter 6 introduces precisely the expression effects that make the earlier ordering insufficient.

**Required correction:** preserve the read-before-RHS rule for compounds, but ensure the eventual immutable store is valid after all RHS effects on continuing paths. Do not mark a plain-assignment destination initialized before checking its RHS: that would incorrectly permit reads before initialization. Retain a regression for this source and for a conditional RHS where only one continuing arm initializes the same binding. The expected result is a located LYC308 rejection, empty C output, exit 1—not an executed second write. An arm that initializes then returns should remain excluded from the outer store's continuing state.

**Lesson:** resolve the destination once, but do not confuse resolving it with validating a later write against earlier flow state.

### R2 — conditional operands confuse intrinsic type with supplied context (high)

**Location:** `compiler/Semantics/Check.cs:904–910,949–952,985,415–425`; retained coverage gap at `graders/ch06/run.py:377–385`.

```lyric
func add(b: bool) {
    let x: u16 = (if b { 255u8 } else { 0u8 }) + 1
    if x == 256u16 { print("widened") }
}
func main() { add(true) }
```

**Observed:** emission succeeds, and C exits 0 printing `widened` at both optimization levels. The contract explicitly requires the typed conditional to supply u8 context to neighboring unsuffixed 1 even with a wider destination. The addition should therefore fail with the located u8-addition LYR001, not produce 256.

**Cause:** discovery correctly finds the conditional's u8 hint, but the arithmetic scheduler sends the destination's u16 expectation to the conditional because its opposite operand has no hint. `CheckIf` converts the conditional result type to that expected u16. The literal is u8, yet the completed arithmetic selects u16. The existing three conditional-width tests have unannotated receiving bindings, so they miss this interaction.

The same distinction also produces a false rejection:

```lyric
func main() {
    let x = (if true { 256u16 } else { 0u16 }) + 1u8
    if x == 257u16 { print("ok") }
}
```

**Observed:** emission rejects with LYC304 `Expected u8, got u16` at `256u16` (the retained one-line probe reports column 34, byte 33). Whole-range arithmetic should select u16 for the existing u16 and u8 operand types, and the program should print `ok`.

**Required correction:** distinguish context for genuinely untyped expressions from the intrinsic type of an already-typed conditional operand. Do not let destination or neighbor context replace that intrinsic type before arithmetic selection. Preserve the deliberately adopted safe expected-branch conversion for a conditional used directly in a typed receiving position. Retain both probes, reversed operand order, and the branch-local-tail form; no new numeric policy is requested.

**Lesson:** a hint can be correct while its consumer is wrong. Contextual literal typing and a required conversion of a complete value are not interchangeable operations.

### R3 — branch preview misses transfer through a non-tail statement-if (high)

**Location:** `compiler/Semantics/Check.cs:792–817,831–874`, especially transfer recognition at 860–862 versus the actual block checker at 236–238.

```lyric
func add(b: bool, c: bool) {
    let x = (if b {
        if c { return } else { return };
    } else { 255u8 }) + 1
    print("widened")
}
func main() { add(false, false) }
```

**Observed:** emission succeeds and C exits 0 printing `widened` at both optimization levels. The outer conditional has a continuing u8 arm and a noncontinuing arm. Its addition must overflow at u8, as with a directly returning arm.

**Cause:** `BranchHint` recognizes direct return/break/continue and a final expression-if, but not a statement-if whose arms both transfer when a discard marker follows it. It reports continuing unit for that branch. `ConditionalHint` combines unit with u8 into an unknown hint; neighboring 1 consequently defaults to i32 and widens the arithmetic. The real checker correctly regards the nested statement-if as noncontinuing, so the preview and real result disagree.

This source is inside the supported boundary: the all-transferring inner if is in **statement position**, and the value-position outer if has a continuing arm. Rejecting all-transferring value-if expressions is not a reason to reject this source.

**Required correction:** make preview continuation reflect supported statement-level transfers, including conditional statements followed by discard markers, without introducing semantic objects or rechecking effects. Preserve iterative binary traversal. Retain this regression and analogous break/continue arms within loops. Do not make emission compensate by re-inferring types.

**Lesson:** a pure preview must still model the relevant control-flow shape; looking only for direct transfer tokens is not equivalent to determining whether a branch continues.

## Structural, arithmetic, and integration assessment

- `Common/SemanticModel.cs:120–140` declares the sole persistent continuation fact. `Semantics/SemanticFact.cs:33–64` initializes it and unwinds failed registration through complete destruction. Analysis, syntax, and symbol-use memberships have reverse links and validation-before-mutation operations. The added boolean is value state, not a new relationship.
- The inspected Common semantic/emission files contain declarations, persistent vocabulary, and documented value-like types rather than algorithm bodies. Semantics and Emission implement partial declarations in one C# 14/.NET 10 assembly. Emitter name maps, hint dictionaries, flow snapshots, loop records, and expression stacks are temporary algorithm work, not duplicated model owners.
- Analysis destroys facts before scopes; facts detach symbol and syntax links; scopes detach weak lexical edges and destroy symbols; function-scope ownership is maintained in both directions. Constructors unwind completed registrations. I found no Chapter 6 ownership correction to require in these paths.
- `CheckWhile` registers its loop before checking its condition (305), tracks condition/body transfers, and intersects break-exit definite snapshots (332–335). This addresses the stated recent defects. Short-circuit checking snapshots after the left operand and joins the skipped-right path at 957–962. Continuing-arm joins in `CheckIf` exclude transferring arms. These observations do not erase R1 or R3.
- The emitter sequences operands and call arguments through temporaries, places the right operand of logical operators inside a conditional, places else-if computation within else, reads compound destinations before RHS effects, and reevaluates while conditions within the actual C loop. Native break/continue therefore target the intended nearest loop even from condition branches. Return unlinks the activation frame.
- Runtime add/sub/mul use exact-width result pointers to the required overflow builtins, with emitted capability checks. Division checks zero and signed minimum/-1 before C arithmetic; remainder handles that signed pair as zero. Bit reconstruction uses a representable complement rather than out-of-range unsigned-to-signed conversion. Shifts operate on unsigned masked bits with validated counts and explicit sign fill. Cast guards distinguish negative signed values from nonnegative magnitudes. Mixed-sign comparisons test negativity before unsigned magnitude comparison. I found no additional demonstrated undefined-C-arithmetic defect in these paths.
- Constants are exact `BigInteger` values from effect-free integer expressions, not remembered variable initializers or folded effectful conditionals. The emitter's constant shortcut is consistent with that restriction.
- The Chapter 4 selector changes only the approved value-if milestone case to required success. The shared Chapter 5 harness requires exact exit/stdout/stderr, compile/run at O0 and O2, explicit rejection diagnostics, and bounded subprocesses. Chapter 6 reuses it; no fixture-shaped compiler dispatch was found. `Makefile:34–48` retains both configurations and all earlier suites, and `spin06.py:62–86` verifies both successful programs and the specific immutable-loop rejection. Separate PEG remains separately required.

## Teaching quality and optional suggestion

The declarations explain lifetime and value roles, and the important lowering steps explain why unsigned reconstruction, exact-width builtin results, continuation joins, and ordering are necessary. This is generally readable teaching code, not merely output-oriented implementation. The chapter accurately teaches the intended invariant that the three findings violate; these are implementation defects and regression gaps, not a request to weaken the prose contract.

Optional: document the local preview helpers' precise contract beside `ConditionalHint`/`BranchHint`—especially what null means (contextual/unknown, not unit) and how continuation differs from missing type information. This would help students avoid conflating the two facts when correcting R2/R3. No new abstraction or persistent preview model is requested.

## Inspection and verification limits

Read in full: `book/chapter-06.md`, `graders/ch06/README.md`, Common semantic declarations and emission declarations, all of `Semantics/Check.cs`, `SemanticFact.cs`, `SemanticModel.cs`, `SemanticSymbol.cs`, `FunctionScope.cs`, `SemanticTypes.cs`, `Emission/Compilation.cs`, public Chapter 4 checker, Chapter 5 shared harness, Chapter 6 cases, Makefile, and spin06. Reviewed the pre-review summary and named cumulative/PEG/spin logs (summary lines and log tails rather than independently replaying all earlier suites).

Author-retained logs report clean Debug and Release builds, 238/238 Chapter 6, 75/75 Chapter 5, 79/79 Chapter 4 with the chapter selector, earlier Unicode/token/parser suites, separate PEG143, and spin06. The reported earlier UBSan run covers 233 cases, not the final 238; I did not rerun or independently inspect a UBSan log. No human walkthrough or independent student reconstruction has occurred.

Reviewer evidence: fresh sandbox Debug build succeeded with zero warnings/errors; all four probe sources were executed against that build, and successful emissions were compiled/executed at both O0 and O2. Sources, generated C, binaries, build output, probe script and results are retained under this agent's `work/`. Retained Chapter 6 rerun: job 829 completed successfully with **238/238 execution cases passed** against the fresh sandbox Debug assembly; the shared harness compiled and ran executable cases at both O0 and O2. Full output is retained in `work/ch06.txt`. No broad git diff, secrets, private history, delegation, project writes, downloads, or commits were used.

SDK setup caveat: the initial build with a fresh sandbox CLI home printed first-run messages including `Installed an ASP.NET Core HTTPS development certificate`, plus a workload-verification notice. Build itself succeeded. No trust command or installation was requested; I did not independently verify a host certificate-store effect. Subsequent execution disabled first-run setup and telemetry. This is not evidence of compiler failure.
