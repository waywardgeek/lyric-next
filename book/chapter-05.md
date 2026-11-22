# Chapter 5 — Run the Program

## TL;DR

Turn the checked Chapter 4 subset into a real executable. Add `--emit-c FILE`, which checks an executable and writes one self-contained C11 translation unit. Compile that output with a C compiler and run it. The Lyric compiler does not launch another compiler itself.

Use resolved semantic facts, not a second round of name lookup. Emit explicit temporaries in Lyric's left-to-right evaluation order. Represent the supported byte strings with length-bearing immutable storage. Generate checked integer arithmetic that rejects overflow **before** C performs an invalid operation. Report runtime failures with a source location and the active Lyric function trace.

This chapter adds execution, not class semantics or a broader expression language. The supported source language remains Chapter 4's functions, calls, parameters, locals, assignments, statement conditionals, returns, unit, booleans, byte strings, and integer literals and `+`, `-`, `*`. Preserve the already-checked floating-point signature/value paths too; floating literals and arithmetic remain unsupported. Unsupported semantics still reject. Passing syntax inspection is not permission to emit unchecked code.

## From meaning to behavior

The preceding chapter can establish that a call resolves, its arguments fit, and every read has an initialized value. It cannot print hello world. Now those facts become instructions that another compiler can execute.

C is a useful first target because it supplies native compilation without requiring us to build a machine-code backend. It is not Lyric's semantic authority. Its expression order, integer promotions, signed-overflow behavior, and string conventions differ from ours. A mechanical source-to-source translation would therefore be wrong even when the resulting C compiles.

Keep the boundary inspectable:

1. Read, validate, lex, parse, and check the entire Lyric executable.
2. Generate the C translation unit in a temporary text buffer.
3. Publish that buffer to stdout only after emission succeeds.
4. Compile and execute it through separate commands.

A source or semantic rejection keeps its existing diagnostic and emits no C. A supported semantic construct without a lowering reports located `LYC401: Unsupported C emission construct: KIND.` and also emits nothing. Buffering prevents a failed emitter from leaving plausible-looking partial output. It does not promise atomic recovery from an operating-system failure while writing the final buffer.

## Extend the shared vocabulary, not the ownership graph

Declare the compilation's emission operation in Common and implement it in an Emission module. The module depends on Common declarations, not the semantic checker's private helpers. It requires a successful, live executable analysis. The driver performs that prerequisite; emission does not silently recheck or repair the program.

The existing graph already supplies everything persistent:

- A syntax declaration points to its declared function or local symbol.
- An expression fact gives its checked type and optional exact integer constant.
- A call fact points to its resolved function symbol.
- A function symbol reaches its ordered parameter symbols through its function scope.
- Syntax nodes reach their owning source and byte anchors.

Keep generated C names in a temporary symbol-to-name dictionary for this emission. Repeated references to one symbol receive one generated name; distinct symbols receive distinct names even when their source names match in independent scopes. Do not store a second signature table or copy semantic types into a backend model. Names such as `ly_s0` also avoid C keywords and Unicode-identifier differences. Their exact spelling is not a public output contract.

Add an explicit builtin identity to the symbol vocabulary: none, print, or println. The semantic pass sets it when constructing the standard declarations. The emitter dispatches on the resolved symbol's identity, not the text of a call. This preserves ordinary name resolution and keeps implementation behavior attached to its authoritative declaration.

String syntax retains a source span rather than a token reference. Expose a declared source operation that returns read-only decoded literal bytes for that exact span. In this implementation it binary-searches the source's ordered token collection and verifies the complete span and decoded-value presence. Do not parse escapes again or add a second persistent decoded-string store. The returned span is temporary and must not survive token/source mutation. Emission neither mutates the compilation nor retains the span after producing its bytes.

These dictionaries, stacks, and spans are local algorithmic work, not new persistent model relationships. Existing compilation destruction and analysis invalidation continue to own the graph's lifetime.

## Assemble a translation unit

Emit runtime includes and types, runtime helpers, user-function prototypes, user-function bodies, then a C `main` wrapper. Prototypes permit forward calls without changing source declaration order. Derive parameter order and types from the checked function scope, and derive the result type from the function symbol. The executable checker has already established the unique nongeneric, no-argument, unit-result Lyric `main`.

Map integers to C's exact-width `int8_t` through `int64_t` and unsigned counterparts, and booleans to `bool`. The target must provide the exact-width types. Map `f32` to C `float` and `f64` to C `double` on the verified target. These types already occur in checked function signatures, parameters, returns, and widening paths, even though floating literals and arithmetic are not supported. Emit such functions even when main does not call them; for example, `func identity(x: f32) -> f32 { return x }` still needs its signature and body. Unit needs a small real value representation, such as a struct containing one unused byte. Using C `void` everywhere would lose the distinction between no arguments and an ordinary unit argument, and would break unit-valued bindings and calls. A zero-parameter function uses C's `(void)` parameter list; a parameter of Lyric type unit uses the unit struct.

A checked integer constant must be printed without overflowing the host formatter or producing an invalid C token. Use its arbitrary-precision semantic value. Positive magnitudes can use `UINT64_C`; negative values can negate a representable `INT64_C` magnitude. Spell the signed 64-bit minimum as negative maximum minus one, because its positive magnitude does not fit signed 64-bit C. The destination is the checked exact-width type; literal spelling does not choose the Lyric type anew.

Choose each arithmetic helper from the binary expression fact, not its receiving destination or an individual child type. The helper's typed parameters convert operands to that operation type. Bindings, assignments, function arguments, and returns use their declared destination types. Ordinary C conversions preserve the accepted integer values because the checker permits only whole-range widening, not value-dependent narrowing. A wider destination never rescues overflow in a narrower operation.

Generate the wrapper to invoke the resolved Lyric entry function and return zero on normal completion. It is orchestration, not ordinary call lookup. No subprocess launcher, package loader, or alternate compilation pipeline is needed.

## Byte strings are not C strings

The supported language can bind, return, pass, and print strings but cannot mutate their byte arrays yet. Emit each literal as static constant unsigned-byte storage and represent its value with a pointer and a length. Static storage remains valid when a function returns a literal value.

Write bytes as numeric initializer elements. A sentinel byte can make an empty C array legal, but it is not part of the Lyric length. Do not use `strlen`, `%s`, or a terminating zero to decide output length. `"A\0B\xFF"` must preserve all four bytes; neither embedded NUL nor invalid standalone UTF-8 is a truncation condition.

This shared immutable storage implements the currently observable operations. It is **not** an implementation of general mutable array value semantics. When later chapters add array mutation, assignments and parameter passing must preserve independent value storage as the language requires. Do not quietly keep shared backing merely because this chapter's programs cannot expose it.

For diagnostic paths and function names, JSON-quote the logical text so control characters are displayed safely. Then encode those UTF-8 bytes into C string literals with fixed three-digit octal escapes. This prevents the C execution character set or a following digit from changing the intended diagnostic bytes. Literal program data and diagnostic display strings have different contracts.

## Evaluation order must be emitted

C does not guarantee our left-to-right argument evaluation. An observed compiler order is not a portable contract; even the machine calling convention does not determine when argument expressions execute.

Instead of emitting a direct nested C call, emit separate statements:

```c
ly_bytes first_value = first();
ly_bytes second_value = second();
ly_unit result = consume(first_value, second_value);
```

Their observable effects are sequenced before the final call, regardless of how C passes the resulting values. Apply the same rule to binary operands and nested expressions, not just top-level call arguments. Read a variable into a temporary at its evaluation point, before evaluating its later sibling.

Use an explicit postorder work stack so a long left-associated expression does not become deep emitter recursion. Each item contains a node and whether its children are ready. On first visit, push a completion item, then push relevant children in reverse order. Popping now evaluates them left-to-right. A call's callee is a resolved symbol, not a value expression in this subset, so schedule its arguments only. On completion, obtain child temporaries from a local node-to-value map, emit one typed temporary for the parent, and record its name. Groups forward their child value; binary expressions invoke the typed checked helper; calls invoke the resolved function or builtin.

An exact integer constant fact may be emitted directly without visiting its arithmetic children. That is valid because the current checker assigns these constants only to effect-free expressions, not calls or a local's remembered value. Preserve that precondition when extending constant folding.

## Statements keep control flow visible

A binding first emits its initializer, then declares the generated local initialized from that temporary. An uninitialized declaration emits storage without a fabricated default; the checker already prevents invalid reads. Assignment emits the right-hand expression before storing to the resolved target. An expression statement evaluates and explicitly discards its result.

For a conditional, emit the condition's temporaries at the point where the condition executes, followed by C `if` and the corresponding body. Emit alternative statements inside the `else`. In particular, an else-if condition must be inside that alternative, not hoisted ahead of the original condition. Otherwise an untaken branch could print or fail.

Statement blocks can use recursive structure within the existing bounded parser depth; expression depth needs its own iterative traversal. These are distinct limits. Source discard-result markers are structural, not runtime operations.

Return values must be fully evaluated before returning. This matters for both side effects and the function trace described below: a failing return expression must still see the current function as active.

## Check arithmetic before doing arithmetic

The semantic checker rejects constant overflow. Runtime operands need guards too. Never perform a potentially overflowing signed C operation and then inspect the result: C's undefined behavior has already occurred, and optimization may remove the proposed check.

Generate type-specific helpers for addition, subtraction, and multiplication for all eight integer types. Let `MIN` and `MAX` be that type's bounds and `a`, `b` its operands. Refuse these conditions before the operation:

- Unsigned addition: `a > MAX - b`.
- Unsigned subtraction: `a < b`.
- Unsigned multiplication: `b != 0 && a > MAX / b`.
- Signed addition: positive `b` with `a > MAX - b`, or negative `b` with `a < MIN - b`.
- Signed subtraction: positive `b` with `a < MIN + b`, or negative `b` with `a > MAX + b`.

For signed multiplication, split by signs. With both positive, reject `a > MAX / b`. With positive `a` and negative `b`, reject `b < MIN / a`. With negative `a` and positive `b`, reject `a < MIN / b`. With both negative, reject `a < MAX / b`. A zero operand needs no multiplication-overflow rejection. Short-circuit conditions must prevent division by zero; these selected divisions also avoid signed `MIN / -1`.

Cast bounds to the operand type. A positive bound spelled with `UINT64_C` must not accidentally turn a signed comparison or division into unsigned arithmetic. C promotions of smaller integer types are safe here because each guarded result fits the original Lyric type; nevertheless, the guard must enforce that original type's bounds, not merely C `int` bounds.

Only after the guard succeeds compute and return the operation. A wider caller result or receiving local does not widen this operation. The helper receives the source file, line, and column of the operator for failure reporting.

## Fatal errors and the active call trace

Give each generated function an automatic activation record containing its display name and parent activation. A single-threaded runtime pointer identifies the current record. Link the record on entry, and restore its parent on every normal explicit return and implicit unit fallthrough. Nested conditional returns use the same function record; blocks do not manufacture activations.

Evaluate the return expression before unlinking. A later failure must not traverse a record belonging to a function that already returned. These are runtime stack relationships, not C# semantic objects or an excuse for a permanent call-history list. The stack chain needs no heap allocation, and this chapter makes no concurrency claim.

Overflow reports `LYR001`, its source operator location, and the active function names from innermost to outermost, then exits 1. Fatal failure does not promise language cleanup or unwinding. The public grader contract gives the exact diagnostic format. Do not dump locals or raw control characters.

Printing uses length-based `fwrite`, optionally writes LF, and checks `fflush` so buffered failure cannot become successful program completion. Failure reports located `LYR002: output failure` at the print call with the same active trace, then exits 1. Diagnostic delivery is best-effort if stderr is also unavailable.

The verified target for this increment is macOS ARM64 with Apple Clang. Exact byte output on hosts with translating text streams, notably Windows CRT defaults, is not established: those targets need explicit binary-stream handling and their own verification. A C11 translation unit alone does not establish identical host I/O behavior.

There is an operating-system boundary too: on POSIX, a broken output pipe can deliver `SIGPIPE` before stdio reports failure. At executable startup, ignore that signal where it is defined, checking setup failure. This lets the checked write path deliver the diagnostic. Do not install such process-wide policy as a hidden library initialization effect. Our retained test closes the reader before starting the program; the original runtime terminated by signal 13 with no diagnostic, while the corrected runtime reported the intended failure. Checking stream return values alone was insufficient.

## Exercises and completion

Start from your Chapter 4 compiler, not this chapter's reference implementation. Extend Common declarations and the responsible modules following the existing architecture. Implement the emission boundary, builtin identity, read-only literal access, C runtime, expression sequencing, statement lowering, checked arithmetic, and trace lifetime described above. Preserve every earlier inspection mode and diagnostic contract.

Read the [Chapter 5 grader contract](../graders/ch05/README.md) and its public `run.py`. Its 75 named cases are retained; there are no chapter-only checks. The 73 executable cases compile and run emitted C at both `-O0` and `-O2`, comparing exact status and output bytes. The two rejection cases instead require the existing semantic diagnostic and no C output; they never invoke the C compiler. The graders do not require particular C formatting or generated identifiers. Source rejection must emit no C. Crashes, signals, timeouts, and unrelated nonzero exits do not count as intended runtime failure.

Prerequisites are Python 3, Make, .NET 10/C# 14 for this edition, a C11 compiler with exact-width integer types, and the existing Node/Peggy setup for the retained syntax comparison. No new package installation is performed by these commands. Run from the repository root:

```sh
make test
DOTNET=dotnet node tools/peg/test.mjs graders/ch03/cases.json
```

If `dotnet` is not on PATH, supply its executable path to both commands:

```sh
make test DOTNET=/absolute/path/to/dotnet
DOTNET=/absolute/path/to/dotnet node tools/peg/test.mjs graders/ch03/cases.json
```

`make test` builds Debug and Release and runs all retained graders through this chapter, including the grader comparison self-check. The separate PEG command remains required and must use your compiler, not a reference assembly. `CC` defaults to `cc`; set it to the selected C compiler and any required flags. The external grader entry point also accepts an independently supplied compiler command:

```sh
python3 graders/ch05/run.py dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

Completion requires every applicable retained check, clean builds, and adherence to the model and architecture—not just the newest execution cases. The tests cover real generated behavior but are not exhaustive arithmetic proof. Review the sign-dependent guards and both sides of each activation link/unlink yourself. On a toolchain supporting it, the supplementary sanitizer command is:

```sh
CC='clang -fsanitize=undefined -fno-sanitize-recover=undefined' \
  python3 graders/ch05/run.py dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

That is additional evidence, not a hidden requirement for every C toolchain. It must not replace ordinary configurations or be described as universal portability verification.

## Take it for a Spin

From the repository root, run:

```sh
make spin05
```

Or preserve your explicit SDK choice:

```sh
make spin05 DOTNET=/absolute/path/to/dotnet
```

This builds Release first, then uses Python and your `CC` C compiler to create and run disposable local executables. There are no credentials, network requests, or external service costs. It writes temporary source and C files and removes them and their executables afterward. It does execute the three supplied programs locally.

First you will see `Hello, Ken and Tom!` from a real executable. Next, argument-producing functions print `A` and `B`, and their receiver prints `onetwo`, producing `ABonetwo` in that order. Finally, a function adds runtime `i8` operands 127 and 1. You should see a located `LYR001` overflow diagnostic with `calc` and `main` in its trace. The walkthrough recognizes this intentional exit 1 and finishes successfully; an unrelated failure is not accepted.

For a manual view of generated code, use a disposable directory of your own, write a hello-world `.ly` source there, invoke `--emit-c` with stdout redirected to a `.c` file, and compile it with `cc -std=c11 -O2`. Only compile when emission succeeded. The automated walkthrough performs those same separate steps and checks each status before proceeding.

The human walkthrough remains a practical acceptance step distinct from a student's or author's successful execution. Completing this chapter does not add classes, prove the entire runtime memory-safe, or authorize installation or publication. It establishes an executable foundation to which later chapters can add features and their checks together.
