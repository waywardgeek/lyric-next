# Chapter 6 graders — expressions and control flow

Status: reference increment verified; single code and text reviews incorporated. Fresh-student reconstruction passed all required checks with runtime setup assistance only. Human walkthrough and acceptance remain pending.

Run from the repository root with Python 3, the built compiler, and a C11 compiler supporting `__has_builtin` and `__builtin_add_overflow`,
`__builtin_sub_overflow`, and `__builtin_mul_overflow` (verified with Apple Clang 21):

```sh
python3 graders/ch06/run.py dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

For the earlier semantic suite, Chapter 6 uses `python3 graders/ch04/check.py --chapter=6 COMPILER [ARG ...]`: the old value-if rejection becomes a required success for the same source. The current `make test` selects this explicitly; Chapter 4/5 runs retain their original rejection. All other earlier semantic checks remain unchanged.

Arguments after the script name are the compiler command. Set `CC` for a different C compiler or additional flags; shell-style quoting is parsed as in Chapter 5. `make test` builds both configurations and runs all retained suites including this one. The separate retained Chapter 3 PEG command remains required, as documented there.

## Required behavior

Integer `==`, `!=`, `<`, `<=`, `>`, `>=` compare mathematical values across all eight signed/unsigned integer types and return bool. Neither type must contain the other. A negative signed integer is less than every unsigned integer. Operands execute once, left-to-right. Neighbor context types unsuffixed integer literals; the comparison's boolean destination does not supply numeric context. Floating comparisons are not introduced by this increment.

Prefix `!` requires bool. `&&` and `||` require bool operands and return bool. The right operand executes only when the left requires it; both operands are still statically checked. A skipped runtime overflow is not executed. This is not permission to accept a statically invalid expression in an untaken operand.

`while` requires a boolean condition. Evaluate the condition before every iteration, including after `continue`; zero iterations are possible. Its body has its own lexical scope. `break` exits the nearest loop and `continue` returns to the nearest loop's condition. Return leaves the function and preserves the existing activation-frame cleanup rules.

Flow checking retains Chapter 4's definite/possible distinction. The loop body does not establish definite assignment after it: this increment conservatively retains the zero-body-iteration path even for literal `true`. The first condition evaluation is mandatory, so its continuing assignments contribute to the false-condition path. Intersect that path with definite states on every break exit, including breaks inside the condition. Therefore a nonunit function returning only inside such a loop still requires a subsequent return. This is a checker limitation, not a claim that `while true` actually executes zero times. Existing unsupported unreachable-statement handling remains unchanged.

Break paths contribute possible assignments after the loop. Condition writes belong to the repeated loop region too. Continue and body fallthrough contribute back-edge assignments; return contributes neither. An assignment to an outer immutable binding must reject if a back-edge can reach the assignment again with that binding possibly initialized. A loop-local declaration denotes fresh storage on each iteration. Nested-loop targets and lexical ownership must distinguish these cases. A read before the first assignment remains invalid even if a later iteration could have initialized the variable.

## Division, remainder, and computed negation

Integer `/` and `%` use the same operand-type selection and literal context as addition. Division truncates toward zero; a nonzero remainder has the dividend's sign. Signed minimum divided by -1 is overflow, but signed minimum remainder -1 is zero. Check both special cases before evaluating C arithmetic. Zero divisors are rejected statically when known, otherwise fail at runtime. Computed unary minus retains its operand type and checks overflow; direct signed literals retain the existing signed-magnitude parsing rule.

Constant overflow uses LYC311. A known zero divisor uses `LYC316: Integer division or remainder by zero.` at the operator. Runtime zero divisors use `LYR003: integer division or remainder by zero`; division overflow uses `LYR001: integer overflow in TYPE division`. Computed negation currently reuses checked subtraction from zero and reports `LYR001: integer overflow in TYPE subtraction`. Runtime failures require exit 1, empty stdout, the exact located message and active call trace published in each fixture, at both optimization levels—not a crash or arbitrary nonzero exit.

## Explicit wrapping arithmetic

Binary `!+`, `!-`, `!*` follow ordinary integer operand-type selection, but reduce results modulo 2^N at the selected width. Prefix `!-` retains its operand type and computes its modular additive inverse. Signed results interpret the resulting bits as two's-complement values. Both constant evaluation and runtime execution obey this contract; ordinary arithmetic remains checked. No general unsafe compiler mode is introduced.

The C lowering must avoid signed overflow and out-of-range unsigned-to-signed casts. The runtime and constant fixtures cover all eight widths, including signed-minimum negation and unsigned subtraction below zero.

## Integer shifts

`<<` and `>>` retain the left operand's type and width; the count's type does not widen it. Both operands execute once, left-to-right. Counts must be integers in `[0, width)`. Known invalid counts produce `LYC317: Integer shift count is outside operand width.` at the operator. Dynamic invalid counts produce `LYR004: integer shift count outside operand width`, exit 1, and the located active trace. Noninteger operands use LYC312 as published in the fixtures.

Left shifts discard high bits without overflow failure. Unsigned right shifts zero-fill; signed right shifts sign-extend. Zero counts are valid. Generated C must not depend on signed-shift behavior, integer-promotion overflow, or shifts by the full width. No unsafe count-normalization mode is introduced.

## Boolean equality

Bool operands support `==` and `!=`. Both operands execute exactly once, left-to-right; equality does not short-circuit. Mixed bool/integer comparisons and ordered bool comparisons remain rejected with the existing LYC313 diagnostic. No implicit bool/integer conversion is added.

## Explicit integer casts

`value as Type` preserves the mathematical value or rejects it when the destination cannot represent it. A known invalid conversion produces `LYC318: Integer conversion is outside TYPE range.` at the cast operator. A dynamic invalid conversion produces `LYR005: integer conversion outside TYPE range`, exit 1, and the located active call trace. Both the operand and target must be supported integer types in this increment; other conversions remain explicitly unsupported.

`value !as Type` reduces the mathematical value modulo the destination width, interpreting signed results as two's-complement. In particular, widening negative values is not zero-extension of their narrow source bits. The operand executes once; the type is resolved statically. Casts fix the result type, not the operand type. Parenthesize a negative operand, as in `(-1i8) !as u64`: postfix casts bind more tightly than unary minus.

The C backend must guard checked conversions before casting and reconstruct modular signed results without out-of-range signed casts.

## Integer bitwise operations

`&`, `|`, and `^` require integers and use the adopted whole-range operand selection and arithmetic literal context. Results are fixed-width two's-complement bit patterns, without arithmetic overflow. Operands execute once, left-to-right. LYC312 rejects nonintegers and incompatible type pairs. Signed C conversions must remain defined; bit reconstruction may reuse the explicit-wrapping lowering. Unary `~` is not introduced.

## Integer compound assignment

The supported local-name operators are `+=`, `-=`, `*=`, `/=`, `!+=`, `!-=`, and `!*=`. Resolve the destination once and read its old value before evaluating the right-hand side. The target must be mutable and definitely initialized: LYC308 rejects an already-initialized immutable binding; LYC307 rejects an uninitialized read. Plain `=` retains its ability to initialize an uninitialized binding.

Apply the corresponding integer arithmetic operation, then assign its result without implicit narrowing. Unsuffixed right-hand literals receive the target's integer context; already-typed values keep their type. Whole-range widening may admit a narrower right-hand type, but a wider arithmetic result cannot be stored in a narrower target. LYC312 rejects noninteger or incompatible arithmetic operands; LYC304 rejects narrowing. Known division by zero uses LYC316 at `/=`. Dynamic checked overflow and zero division use the existing LYR001 and LYR003 located diagnostics and traces at the compound operator. Wrapping compounds remain modular at the operation width.

Array slots, fields, and references are not supported assignment targets in this increment. Their later implementation must preserve the destination's needed storage borrow across right-hand evaluation, not merely repeat its lookup afterward.

## Value-producing conditionals

An `if` used as a value requires `else`. Only the selected branch executes; condition and surrounding argument/operand effects retain left-to-right order. Each branch has its own lexical scope. A final expression supplies its value; a trailing newline preserves it, while a trailing semicolon discards it and leaves unit on a continuing branch. Empty continuing branches also produce unit.

With an expected result type, every continuing branch must convert safely to it. Context types unsuffixed literals but must not widen already-typed intermediate arithmetic. Without an expected type, equal branch types retain that type. Differing integer types use one existing branch type whose entire range contains the other, otherwise require explicit conversion. Do not invent a third type. A typed conditional must provide its result type as context to a neighboring unsuffixed arithmetic literal, including when its result comes from a branch-local declaration.

A branch ending in return, break, or continue contributes neither a value nor a continuing flow path. The remaining continuing arm supplies the result. This increment explicitly rejects a value-position conditional with no continuing arm as LYC306 (unsupported If); propagation of noncontinuation through arbitrary argument/operand positions remains later work. Statement-position conditionals may already transfer on every path. This is an implementation boundary, not a language prohibition.

Definite assignments are intersected across continuing arms; possible assignments are united. The optional right side of `&&`/`||` must additionally join with the path that skips it: assignments on the right are possible, not necessarily definite. Statically check untaken arms as before, without moving their runtime effects outside the branch.

Missing else uses `LYC319: Value-producing if requires an else branch.` Incompatible inferred results use `LYC320: Incompatible conditional branch types: LEFT and RIGHT.` These structural If nodes have no keyword anchor, so the existing location rule selects the condition's first anchored token. Expected-type failures retain LYC304, located at the branch's final expression where available. This construction contract covers the stated increment, not full-language semantic coverage.

## Comparison and diagnostics

The named fixtures in `run.py` are retained behavioral checks. Each success requires emission exit 0, nonempty C stdout, empty stderr; C compilation at both `-O0` and `-O2`; and execution exit 0, exact stdout bytes, empty stderr. Timeout, crash, signal, or wrong exit status is failure. The shared Chapter 5 harness applies a 20-second bound per process. Expected C formatting is not prescribed.

Each rejection requires `--emit-c` exit 1, empty stdout, and the exact located diagnostic in the fixture. Locations are authored single-line ASCII positions: one-based column and zero-based byte offset. Temporary filename is JSON encoded using the same Chapter 5 comparison contract. Existing LYC301/304/307/308 diagnostics retain their meanings. New diagnostics:

- `LYC313: Integer comparison requires integer operands.` at the comparison operator.
- `LYC314: Loop may assign immutable binding 'NAME' again.` at the assignment target.
- `LYC315: break requires an enclosing loop.` or the corresponding continue message, at the containing function name. The parser's bare transfer nodes do not retain keyword anchors.

Graders and fixtures are readable but student-read-only. The implementation must use real semantic checking and emission; fixture recognition is not an implementation. Structural code review remains distinct from these behavioral checks. Human walkthrough remains a separate acceptance step.

The review regressions retain RHS-induced immutable double writes (and the returning-arm control), intrinsic conditional operand widths under destination/neighbor context, and noncontinuation through discarded statement conditionals with return/break/continue. These combinations use the same exact diagnostic and execution comparisons as earlier cases.
