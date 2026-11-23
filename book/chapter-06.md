# Chapter 6 — Expressions and Control Flow

## Introduction

I want Lyric to win the benchmarks that matter: average real applications, with real memory usage. Not tiny, hand-tuned demonstrations designed to make a language look fast. If we build a language that wins those demonstrations and loses on the programs people actually write, we have missed the point.

Structure-of-arrays storage is a large part of my bet. When a program processes a few fields across many objects, it should not have to drag every other field through the cache with them. That advantage matters when the data stops fitting in cache. Not every access pattern favors SoA, but I expect it to give us an important advantage in real applications. If we do well enough on the rest of the language, at least in unsafe mode, I expect us to win realistic benchmarks. That is the goal, not a result we have already measured.

“Do well enough” requires vigilance. We cannot give away that advantage one convenient language feature or unnecessary operation at a time. Performance has to remain part of the design conversation throughout this book.

Clang gives us wonderful controls for expressing what we want. Its checked-arithmetic builtins compute an integer result and tell us whether it overflowed. We can branch to our own handler, report the Lyric source location, and show the active call trace. We do not have to choose between unchecked arithmetic and a trap that tells the programmer almost nothing. Nor do we have to reinvent overflow detection when the compiler already knows how to lower it efficiently.

But choosing C also forces uncomfortable decisions. Why should shifting an integer by its width or more be undefined behavior? I would rather have an obvious, defined answer than leave a hole in the language. C does not give us that answer, and making one portable can require extra work around the shift. We want fast shifts. Lyric therefore rejects invalid counts in safe mode, but does not repair or normalize them in unsafe mode. There, an invalid count inherits unchecked C behavior. It is not a clever way to obtain zero or a masked count.

The same discipline applies to other checks. Safe mode checks ordinary integer overflow, conversion ranges, division by zero, and shift counts; when arrays arrive, it checks their bounds too. Unsafe mode removes those checks. It does not remove the type system, ownership rules, or static lifetime analysis. It does not remove explicit assertions or the allocation-size and allocation-failure checks needed to manage storage. “Unsafe” is a specific contract, not an instruction to throw away everything we know about the program.

Here is the future I want: an LLM examines every place in a Lyric codebase where one of those removable checks could fail, follows the relevant control flow and inputs, and establishes why it cannot. Not just for the benchmark input. For the program's supported inputs and operating conditions. If a sufficiently capable system can establish that for the whole program, we should be able to ship unsafe mode to production without giving up safety. The justification has to hold; a model's confidence alone is not enough. We do not have that capability established today.

That is a different ambition from asking programmers to turn off checks and hope. We build the checked behavior first, make its requirements explicit, and leave room for better tools to establish when the checks are unnecessary.

AI changes everything.

## TL;DR

Retain Chapter 5 assertions in every mode: `assert(bool) -> unit` evaluates once and fails with located `LYR010` and the active trace on false. Optimization and `--unsafe` must not remove assertions.


Extend the existing checker and C emitter together. Add integer division, remainder, computed negation, explicit wrapping, bitwise operations, shifts, and integer casts; boolean comparisons and short-circuit operators; compound assignment; value-producing conditionals; and while loops with break and continue. Preserve left-to-right effects, checked integer widths, definite assignment, and located failures.

Use a C compiler that provides checked addition, subtraction, and multiplication builtins. The verified toolchain is Apple Clang 21 on macOS ARM64. Requiring suitable target facilities is simpler than maintaining both manual guards and a builtin implementation. Lyric still owns its semantics: compiler builtins do not choose our types, sequence expressions, or produce our diagnostics.

Start from your Chapter 5 compiler. The public [Chapter 6 grader contract](../graders/ch06/README.md) defines the supported behavior, exact failures, and comparison rules. This chapter does not add arrays, classes, floating expressions, generic functions, or new assignment destinations. Unsupported constructs still reject explicitly.

## Three questions, not one

An expression has a type. A control-flow path may or may not reach its successor. A binding may be definitely or possibly initialized on that path. These are different facts.

For example, `if ready { return 1 } else { 2 }` can supply an integer value from its continuing arm. The returning arm supplies no value at that join. Conversely, `false && if ready { x = 1; true } else { false }` cannot establish that `x` was initialized: execution may skip the entire right operand. The checker must still validate its source.

Keep the existing division of responsibility. Common declares persistent semantic facts and relationships. Semantics checks syntax and owns the authoritative result. Emission consumes those facts without repeating name resolution. The driver still validates the whole input before publishing buffered C. No new compiler pipeline or runtime interpreter is needed.

Extend the expression fact with a boolean indicating whether a checked block or conditional can continue. Its type records the continuing value, including unit; a unit value is not a substitute for noncontinuation. Keep the fact's existing analysis ownership, syntax backlink, optional symbol relationship, and exact integer constant. Declare the additional field and constructor parameter in Common, and initialize it through the matching implementation. Existing constructors can default continuation to true. Destruction and failed-constructor unwind remain unchanged.

Stacks, type-hint maps, branch snapshots, loop records, and generated temporary names remain local algorithmic work. Do not turn them into a second persistent symbol table. Reach the enclosing function through the existing lexical scope and function backlinks.

## Extend integer meaning before C syntax

Preserve Chapter 4's type selection. A typed neighbor supplies context to an unsuffixed literal; otherwise an expected integer type can supply it, and otherwise it defaults to i32. Already-typed values never narrow because of their current values. Arithmetic chooses one existing operand type that contains the other's entire range, or rejects. Do not invent a third type. A wider receiving variable cannot repair overflow inside a narrower operation.

Use that rule for division, remainder, wrapping arithmetic, and `&`, `|`, `^` as well. Thus u8 with u16 selects u16; i32 with u32 requires an explicit conversion. Bitwise results are fixed-width bit patterns, not checked mathematical sums. Unary `~` is not introduced.

Shifts are different: the left operand alone determines the result width. The count must be an integer, but even a u64 count does not widen an i8 left operand. Comparisons are different again: any supported signed/unsigned integer pair can be compared mathematically without a common arithmetic type. Their result is bool, which must not become numeric context for their operands.

Continue exact constant evaluation with arbitrary-precision integers. Constants must obey the same operation widths and failure rules as runtime values. Do not fold a variable's remembered initializer or an effectful conditional into an apparent effect-free constant. The emitter's shortcut for a constant fact depends on that guarantee.

### Safe defaults and the unchecked mode

Add `--unsafe` to `--emit-c`, `--check`, and `--check-library`, before the filename. Reject unknown or duplicate options. Default checking and emission remain safe. Store this policy once on the analysis created for a compilation; emission reads it there rather than accepting a potentially different choice. Later class layout options remain independent.

Unsafe mode removes ordinary arithmetic overflow, integer-conversion range, shift-count, and zero-divisor checks. When arrays arrive in Chapter 7, it also removes index bounds checks. Type, initialization, ownership, and static lifetime rules remain. Every operation accepted without a diagnostic in safe mode must retain its result. Only exceptional inputs that safe mode rejects may inherit unchecked C behavior. Emit a conspicuous unsafe comment in the generated translation unit and disclose the mode in benchmarks.

Keep valid constant folding. In unsafe mode, leave an overflowing ordinary operation, out-of-range ordinary cast, invalid shift count, or zero division unfolded instead of assigning it a guaranteed result. In particular, do not invoke the host arbitrary-precision divide operation on zero while checking. Explicit wrapping operations still fold modulo the selected width. Literal representability and type compatibility are not disabled.

Use direct C `+`, `-`, `*`, `/`, unary minus, and ordinary casts for unchecked operations, converting operands to the selected Lyric operation type as needed. Preserve the existing left-to-right snapshots. For `%`, retain the signed-minimum/-1 zero case before C remainder. Left shift retains unsigned bit lowering and width reconstruction; it is valid for negative signed operands and may discard high bits. On the supported arithmetic-right-shift C target, valid `>>` can use the native operator. Do not normalize invalid unsafe shift counts. Mixed comparisons and explicit wrapping operators retain their established semantics in both modes.

Small, frequently used numeric helpers should be `static inline`, including the remaining bit-reconstruction and wrapping helpers. This makes the intended use explicit without forcing large allocation or failure routines to inline. We choose straightforward unchecked C for performance; passing correctness checks alone does not establish a speedup.

Generated helper names are implementation details, not a public interface.
Review the generated definitions and call paths to establish that small helpers
are inline and unchecked arithmetic bypasses checked helpers. Do not infer
those properties by counting a reference compiler's identifier spellings or
requiring unused helper definitions to remain in the output. Behavioral grader
passes and this source review establish different evidence.

### Division, remainder, and negation

Division truncates toward zero; a nonzero remainder has the dividend's sign. In default safe mode, check a known zero divisor during semantic analysis, and a dynamic divisor before evaluating C arithmetic. Signed minimum divided by -1 overflows. Signed minimum remainder -1 is valid and equals zero, but C's `%` is unsafe for that pair too. Return zero before executing `%`.

Computed unary minus retains its operand type. Negating signed minimum overflows; negating a nonzero unsigned value with ordinary minus does too. Keep the direct negative-literal rule from Chapter 4: parse the magnitude and sign together before checking representability, so `-128i8` remains legal. That special case is not permission for a computed operand to overflow.

Runtime computed negation can reuse checked subtraction from zero. Its diagnostic consequently identifies subtraction. The public contract specifies this wording rather than letting a test accept any failure.

### Delegate checked arithmetic, not language semantics

Replace Chapter 5's manual add/subtract/multiply guards with typed `static inline` helpers using `__builtin_add_overflow`, `__builtin_sub_overflow`, and `__builtin_mul_overflow`. Each helper declares an exact-width result and passes its address to the builtin. For example, the addition helper for i8 uses an `int8_t` output, not a C `int` output. The pointer type establishes the overflow boundary even when C normally promotes narrow operands.

On a true overflow flag, invoke the existing located `LYR001` failure path with the same operation name and active Lyric trace. Otherwise return the result. Do not execute ordinary signed arithmetic first and inspect it afterward. Do not replace these calls with a process-wide trapping flag: we need our own diagnostic and location.

Emit a preprocessing capability check using `__has_builtin` for all three operations. An unsupported C compiler must fail clearly, not silently compile an unchecked fallback. The backend now requires those facilities in addition to C11 and exact-width integer types. Apple Clang 21 is the tested implementation; another compiler must satisfy the requirement rather than merely accept the `-std=c11` flag.

This refinement removes a second arithmetic implementation to maintain. It does not remove explicit checks for zero division, signed division overflow, shifts, or conversions. The earlier Chapter 5 construction remains a useful explanation of why ordinary C arithmetic cannot simply be trusted.

### Modular results and signed reconstruction

Binary `!+`, `!-`, `!*` and prefix `!-` reduce the mathematical result modulo 2^N at the operation width. In constant evaluation, take the remainder, adjust it into the nonnegative interval if necessary, then subtract 2^N when interpreting a signed result whose sign bit is set.

At runtime, convert operands to uint64_t, perform defined unsigned arithmetic, and mask to the selected width. This works for narrower widths because reduction modulo 2^64 preserves their low bits. For a signed result, do not cast an oversized unsigned pattern directly to the signed type. If the bits exceed the signed maximum, compute `-1 - complement`, where complement is the masked bitwise complement. That complement fits the signed positive range, including at 64 bits; the reconstructed result is representable. Otherwise cast the nonnegative value normally.

Reuse this small bits-to-value operation for bitwise operators and modular casts. Keep each checked operation's chosen type authoritative. Sharing the reconstruction algorithm does not make every operation wrapping.

### Shifts without host assumptions

Reject counts outside `[0, width)` before shifting. For signed counts, reject negatives before conversion to uint64_t. Known invalid counts are semantic diagnostics; dynamic ones use a located runtime failure.

Start from the masked unsigned representation of the left operand. Left shift then mask, discarding high bits. For unsigned right shift, use the ordinary unsigned shift. For signed right shift, use the same unsigned shift, then explicitly fill the high bits if the original operand was negative. One convenient fill mask is the width mask XOR that mask shifted right by the count. A zero count needs no fill.

This avoids signed-shift assumptions and C integer-promotion overflow. Never construct an auxiliary shift by the full width. Tests with zero counts and wide count types protect those boundaries separately from ordinary results.

### Integer casts

A cast resolves its target through the existing type reader. It schedules only its value child for runtime evaluation; a type syntax child is not an operand. Check the operand independently before applying the conversion. The destination type fixes the result, not the arithmetic width inside the operand.

Checked `as` preserves the mathematical value or fails if the destination cannot represent it. For constants, compare exact values against destination bounds. At runtime, evaluate once into a temporary, then perform signedness-aware guards before the C cast: test negative source values with signed comparisons; compare nonnegative magnitudes with unsigned comparisons. Only a successful guard permits the cast.

Modular `!as` reduces the mathematical value modulo the destination width. A negative i8 converted to u64 must not first become a zero-extended eight-bit pattern. Convert its mathematical signed value to uint64_t using C's defined unsigned conversion, then use the destination bit reconstruction.

Casts bind more tightly than unary minus. Write `(-1i8) !as u64` when the negative value is the operand. Our first negative-cast fixtures omitted the parentheses and tested a different syntax tree. Correcting those fixtures, rather than changing the adopted parser precedence, preserved the intended language contract.

## Comparisons and short-circuit execution

For same-signedness integer comparisons, widen both operands to int64_t or uint64_t respectively. For mixed signedness, first test whether the signed operand is negative. A negative value is below every unsigned value; determine the comparison result accordingly. Otherwise compare the two nonnegative magnitudes as uint64_t. Do not allow C's implicit unsigned conversion to turn a negative integer into a large positive one.

Bool supports `==` and `!=`, evaluating both operands once, left-to-right. Ordered bool comparisons and mixed bool/integer comparisons reject. Prefix `!` requires bool. These operations neither introduce boolean arithmetic nor implicit conversions.

The existing ready/not-ready expression stack is insufficient for `&&` and `||`, because emission must act between children. Use explicit stages:

1. Schedule the left operand.
2. Declare a bool result temporary outside the conditional and initialize it from the left value. Open `if (result)` for `&&`, or `if (!result)` for `||`. Schedule the right operand inside that region.
3. Assign the right value to the result and close the region.

The result remains visible to the surrounding expression, while right-side calls and failures execute only when needed. Nested operators follow the same stack discipline; long arithmetic chains remain iterative rather than recursive.

Semantic checking must still inspect both operands. After checking the left, save its definite and possible assignment sets as the skipped-right path. Check the right using the current state. At the join, intersect definite assignments and unite possible assignments with the saved path. This matters now that a right operand can contain a value-producing conditional whose branch statements assign locals. Blindly keeping the right operand's final state would accept a read that is uninitialized whenever execution skips it.

## Compound assignment reads before it writes

Support the adopted local-name operators `+=`, `-=`, `*=`, `/=`, `!+=`, `!-=`, and `!*=`. Do not silently add remainder or bitwise compounds. Resolve the destination once and retain its symbol fact. Unlike plain assignment, compound assignment reads the old value, so require definite initialization before checking its right-hand side. Enforce mutability through the existing possible-assignment rules.

For plain assignment, resolving the destination is not yet a write. Check the RHS without marking an uninitialized target assigned, then recheck immutability against the resulting continuing-path possible-assignment set before storing. An RHS branch can initialize the same binding: `x = if b { x = 1; 2 } else { 3 }` must reject the outer write. A branch that initializes x and then returns does not reach that outer write and must remain excluded from the join. This final validation is needed even when the target was unassigned before RHS checking.

Use the target's type as context for an unsuffixed right operand. Select the operation type by the ordinary whole-range rule, and require its result to fit the destination without narrowing. A wider right operand cannot turn a narrow destination into a wider one. A known zero divisor still rejects statically.

In C, copy the old target to a temporary before evaluating the right expression. Invoke the appropriate checked or wrapping helper, then store the result. Keep the compound operator's location for failures. Reuse the existing loop write tracking; compound syntax must not bypass immutable-storage analysis.

Only local names are supported destinations here. Later array slots, fields, and references must preserve their storage borrow across right-hand effects. Repeating a lookup afterward would not satisfy single evaluation or lifetime safety.

## Branch values and flow joins

Extend the existing block checker rather than create a second expression-only scope system. A block still constructs a lexical scope, checks statements in order, and removes its locals from temporary flow sets when leaving scope. The semantic symbols and facts remain owned by the existing analysis graph.

In value mode, a final expression supplies the block result. A trailing newline preserves it; a discard-result marker from a semicolon leaves unit. An empty continuing block also yields unit. Record both the result type and whether the block continues. Return, break, and continue retain their existing control transfer behavior and do not fabricate a value.

An if used as a value requires else. Check the condition as bool, snapshot incoming state, and check each arm from that same state. Join definite assignments by intersection and possible assignments by union over continuing arms only. A returning or loop-transferring arm is excluded. Statement conditionals still need no value and may transfer on every path.

With an expected type, require every continuing arm to convert safely to it. It can context unsuffixed literals but cannot widen an already-typed intermediate operation. Without an expected type, preserve equal types; for differing integers select an existing branch type containing the other's whole range. Reject incompatible results rather than inventing a third integer type.

This increment explicitly rejects a value-position conditional with no continuing arm. Propagating noncontinuation through arbitrary enclosing arguments and operators is later work. This is an unsupported boundary, not a claim that the language forbids such programs.

### Discover type context without checking effects twice

A conditional can be the typed neighbor of an unsuffixed arithmetic literal. For example, if both continuing arms yield u8, adding unsuffixed 1 must use u8 even when the surrounding destination is wider. Our initial implementation missed this hint: three programs incorrectly completed instead of reporting u8 overflow.

Extend the nonmutating hint pass to inspect branch tails. It must understand typed literals, names, calls, casts, and nested conditional results. Within a branch, track provisional local types in source order: inspect an initializer before registering its name; use an explicit annotation where present; default an otherwise uncontextualized numeric initializer as usual. Clone temporary local-hint environments for sibling branches so one branch's names cannot leak into another. An unknown local hint must not fall through to an unrelated outer name.

Keep an operand's own intrinsic hint ahead of a neighboring hint or receiving destination. The latter provide context only where a type is still unknown. Thus an annotated `u16` binding cannot turn `(if b { 255u8 } else { 0u8 }) + 1` into u16 addition; the u8 operation must overflow first. Conversely, a typed u16 conditional plus `1u8` must select u16 rather than demand that the conditional narrow to u8. Directly assigning a conditional to annotated storage still allows the adopted safe branch conversions. Preserve this distinction through groups, shifts, and computed unary operations.

Preview continuation is separate from preview type. An unknown/contextual type is not unit, and a noncontinuing arm supplies no value. Inspect statement-level conditionals as well as direct return, break, and continue: `if c { return } else { return };` ends its branch even though a semicolon discards its value. The same applies to both-branch break and continue inside a loop. Missing these transfers can invent a unit arm and incorrectly default a neighboring literal to i32. Preview must agree with supported control-flow semantics without creating facts or replaying effects.

This preview creates no actual symbols, facts, assignments, or side effects. The real checker remains responsible for name validity, initialization, and safe conversion. Continue visiting a cast's operand subtree even though the cast itself supplies a result hint, or nested arithmetic can lack its own hint entries. Preserve iterative traversal for long arithmetic chains; bounded nested branch structure does not justify recursively walking every binary node.

### Emit the selected value, not both candidates

Declare the if result temporary outside the C conditional. Emit condition computations, then each arm's statements inside its actual branch. A continuing arm assigns its final value to that outer temporary; a continuing unit arm assigns the unit representation. A returning or breaking arm performs the transfer without assigning the result. Use the checked continuation fact instead of guessing from the syntax's last token.

Else-if conditions must remain inside the preceding else. Calls surrounding the conditional continue to materialize arguments left-to-right. Do not replace the whole construct with eagerly computed branch candidates or a ternary that bypasses its statement and scope behavior.

## Loops and initialization across iterations

A while condition is checked as bool and runs before every iteration. Maintain a stack of temporary loop records so break and continue target the innermost loop. Each record needs its entry scope, possible assignments reaching a back edge, possible assignments reaching an exit, definite-assignment snapshots for break exits, and relevant writes to outer immutable storage. Register the loop before checking its condition: the condition is repeated too, and can contain assignments or transfers inside value-producing branches.

Check the body once, with its own scope; do not construct duplicate symbols and facts to simulate iterations. Body fallthrough and continue contribute possible assignments to the back edge. Break contributes to the exit. Return contributes to neither. Existing branch joins exclude paths that transferred.

For each immutable write, record it for every enclosing loop whose entry scope can already see that storage. A declaration inside a loop body denotes fresh storage on each iteration, so it is not an outer write for that loop. However, storage local to an outer loop can still be outer storage for an inner loop.

After checking the body, reject an outer immutable write if its binding can also be possibly initialized on a back edge reaching another iteration. An assignment followed by an unconditional break or return can be valid; an assignment followed by continue cannot silently become repeatable initialization. Keep ordinary same-iteration possible-assignment checking too.

Snapshot the continuing assignment state after the first condition evaluation, which is mandatory. After checking the body, intersect that condition-false path's definite assignments with every break-exit snapshot. A break inside the condition can bypass assignments in its other arm. Add back-edge and break-exit possible assignments, filtered to bindings visible before the loop. Checking only body writes would miss repeated immutable initialization in the condition; joining only the condition's continuing arm would accept an uninitialized read after a condition break. This increment conservatively keeps a zero-iteration path even for literal true. Consequently, a function whose only return is in a while loop may still need a later return. That is a documented checker limitation, not altered runtime behavior.

For example, suppose `x` is uninitialized before a loop whose condition is `if stop { break } else { x = 1; false }`. The continuing condition arm leaves the definite set containing `x`; the break arm leaves a snapshot without `x`. Intersecting those exits removes `x`, so a read after the loop must be rejected. The fact that the other arm assigns it cannot initialize storage on the break path.

Lower while to an explicit C loop containing the condition's temporary statements followed by a false-condition break and the body. Native continue now repeats those condition computations, rather than jumping to a stale cached bool. Native break exits the nearest loop. Return continues to unlink the current Lyric activation frame normally.

## Diagnostics are part of the increment

Preserve existing errors and add the published located diagnostics for bad comparison operands, repeatable immutable initialization, transfer outside a loop, zero division, invalid shift counts, out-of-range conversion, missing else, and incompatible branch values. The [grader contract](../graders/ch06/README.md) gives exact codes, messages, and comparison rules.

Dynamic zero division, invalid shifts, and conversions use LYR003, LYR004, and LYR005 respectively, with the existing active call trace and exit 1. Fatal failures promise no cleanup. Overflow remains LYR001, output failure LYR002. A signal, crash, unrelated exception, or arbitrary nonzero exit is not an accepted rejection.

Structural if nodes do not carry a keyword anchor; their located diagnostics use the condition's first anchored token. Bare break and continue retain the existing containing-function location when used outside loops. Do not invent new syntax spans merely to match a guessed fixture column.

## Exercises and completion

Starting from your Chapter 5 solution, implement the shared continuation fact, integer and boolean rules, staged short-circuit checking/emission, compound assignment, branch values and hints, loop-flow records, checked builtins, and runtime diagnostics described here. Preserve all earlier inspection modes and real component boundaries. Keep comments beside non-obvious signed reconstruction, branch joins, and lifetime rules. Review ownership and cleanup separately from output tests.

Use your own Chapter 5 implementation and the public chapters, contracts, graders, and fixtures. Do not consult the Chapter 6 reference implementation or private author notes and repair history. The graders are readable evidence, not editable solution inputs.

Prerequisites: Python 3, Make, .NET 10/C# 14, the existing Node/Peggy setup, and a C compiler with exact-width types, `__has_builtin`, and all three checked-arithmetic builtins. Apple Clang 21 is verified. `CC` selects the compiler and optional flags; no command below installs tools or downloads packages. Work from the repository root.

Required cumulative commands:

```sh
make test
DOTNET=dotnet node tools/peg/test.mjs graders/ch03/cases.json
```

If the SDK is not on PATH, preserve the explicit choice:

```sh
make test DOTNET=/absolute/path/to/dotnet
DOTNET=/absolute/path/to/dotnet node tools/peg/test.mjs graders/ch03/cases.json
```

The current Make target builds Debug and Release and runs every retained suite through Chapter 6, including comparison self-checks. The separate PEG comparison is required and must use your compiler. Supply an independently built implementation directly when diagnosing a suite:

```sh
python3 graders/ch04/check.py --chapter=6 dotnet compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch05/run.py dotnet compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch06/run.py dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

The Chapter 4 value-if rejection is explicitly milestone-only for Chapters 4–5. `--chapter=6` requires that same source to succeed; the other semantic checks are unchanged. The selector defaults to Chapter 4, so do not omit it when grading this increment. This approved feature transition is not permission to remove other regressions.

Run the additional retained mode checks with `python3 graders/ch06/unsafe.py --chapter=6 DOTNET ASSEMBLY`, substituting the same compiler executable and assembly as above. Run this for both Debug and Release. The grader checks valid unchecked execution and emission-only exceptional cases; it never treats an arbitrary undefined-behavior outcome as an expected result. Later chapters use `--chapter=7` for bounds/borrow checks and `--chapter=8` for both layouts.

All 250 named Chapter 6 cases are retained. Read their public source and the shared Chapter 5 harness. Successful emission requires exit 0, nonempty C, and empty stderr. Executable cases compile at both `-O0` and `-O2`, then require exact output bytes and status. Rejection and runtime-failure fixtures prescribe exact diagnostics, locations, and traces. Each subprocess has a 20-second bound. C formatting and generated identifier spelling are not requirements. Graders and expected results are readable but not student-editable.

Completion requires all current and retained checks in both configurations, the separate PEG comparison, and structural adherence—not merely the newest cases. The all-width boundary cases, single-evaluation effects, and branch-flow cases provide bounded evidence rather than exhaustive correctness proof. A useful optional supplement on a supporting toolchain is:

```sh
CC='clang -fsanitize=undefined -fno-sanitize-recover=undefined' \
  python3 graders/ch06/run.py dotnet compiler/bin/Release/net10.0/Lyric.dll
```

A sanitizer pass does not replace the required runs or establish untested host portability. You may add diagnostic tests to understand your implementation, but only designated graders carry forward as retained requirements.

## Take it for a Spin

Run from the repository root:

```sh
make spin06
```

Or retain your SDK selection:

```sh
make spin06 DOTNET=/absolute/path/to/dotnet
```

This builds Release, emits C, invokes your `CC`, and executes disposable local programs. It requires the same compiler facilities as the exercises. There are no credentials, network requests, or service costs. Temporary Lyric sources, C files, and executables are removed automatically.

You will see a loop sum reported as `Sum is 55`; short-circuit effects produce `skipped called done`; and a loop using continue and break prints `reached three`. A value-producing conditional prints `even` then `odd`. The fixed-width example combines a wrapping compound update, bitwise OR, a shift, and a modular cast, reporting `Bits checked`.

Finally, an outer immutable binding assigned in a repeatable loop produces the intended LYC314 diagnostic. The walkthrough checks this specific rejection and finishes successfully. It must not accept a crash as the expected failure.

Read the printed examples and try changing them in your own disposable source file. For example, replacing an explicit wrapping addition with ordinary addition should restore overflow checking, not silently retain modular behavior. The human walkthrough and acceptance remain separate from an author's or student's automated run. Completing this chapter does not authorize publication, install a new running system, or begin the next chapter.
