#!/usr/bin/env python3
"""Retained Chapter 6 execution cases. Arguments are the compiler command.

Reuse the real C compilation/execution harness, not a reference-output oracle.
Every success requires exit zero, exact stdout, and empty stderr at O0 and O2.
"""
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location("execution", Path(__file__).resolve().parents[1] / "ch05" / "run.py")
execution = importlib.util.module_from_spec(spec)
spec.loader.exec_module(execution)

CASES = []
# Explicit expected mathematical comparisons, including values C's usual
# mixed-sign conversion would compare incorrectly. Both operand orders matter.
for name, left, right, truths in [
    ("signed", "-3i32", "2i32", [False, True, True, True, False, False]),
    ("negative-unsigned", "-1i64", "18446744073709551615u64", [False, True, True, True, False, False]),
    ("unsigned-negative", "18446744073709551615u64", "-1i64", [False, True, False, False, True, True]),
    ("equal-mixed", "127i8", "127u64", [True, False, False, True, False, True]),
    ("positive-mixed", "9223372036854775807i64", "18446744073709551615u64", [False, True, True, True, False, False]),
]:
    body = []
    for operator in ["==", "!=", "<", "<=", ">", ">="]:
        body.append(f'if a {operator} b {{ print("T") }} else {{ print("F") }}')
    source = 'func main() { let a = ' + left + '; let b = ' + right + '; ' + '; '.join(body) + ' }'
    CASES.append((name, source, ''.join('T' if value else 'F' for value in truths).encode()))

CASES += [
    ("negation", 'func main() { if !false { print("T") }; if !!true { print("T") } }', b'TT'),
    ("short-circuit", """func yes() -> bool { print("Y"); return true }
func no() -> bool { print("N"); return false }
func main() {
    if no() && yes() { print("bad") }
    if yes() || no() { print("T") }
    if yes() && yes() { print("T") }
    if no() || yes() { print("T") }
}""", b'NYTYYTNYT'),
    ("nested-short-circuit", """func yes() -> bool { print("Y"); return true }
func no() -> bool { print("N"); return false }
func main() { if !(no() || (yes() && no())) { print("T") } }""", b'NYNT'),
    ("comparison-effects", """func first() -> i32 { print("A"); return 1 }
func second() -> u64 { print("B"); return 2u64 }
func main() { if first() < second() { print("T") } }""", b'ABT'),
]

# Loop cases are executable contracts, not generated-C formatting tests.
CASES += [
    ("sum", """func sum_to(n: i32) -> i32 {
    var total = 0
    var i = 1
    while i <= n { total = total + i; i = i + 1 }
    return total
}
func main() { if sum_to(10) == 55 { println("ok") } }""", b'ok\n'),
    ("zero-iterations", 'func main() { while false { print("bad") }; print("ok") }', b'ok'),
    ("continue-condition", """func condition(i: i32) -> bool { print("C"); return i < 3 }
func main() {
    var i = 0
    while condition(i) {
        i = i + 1
        if i < 3 { continue }
        print("B")
    }
}""", b'CCCBC'),
    ("nested-loop-targets", """func main() {
    var i = 0
    while i < 2 {
        i = i + 1
        var j = 0
        while j < 3 {
            j = j + 1
            if j == 1 { continue }
            print("I")
            break
        }
        print("O")
    }
}""", b'IOIO'),
    ("loop-return", """func find() -> i32 {
    while true { return 7 }
    return 0
}
func main() { if find() == 7 { print("ok") } }""", b'ok'),
    ("fresh-loop-let", """func main() {
    var i = 0
    while i < 3 {
        let next: i32
        next = i + 1
        i = next
        print("x")
    }
}""", b'xxx'),
    ("outer-let-break", """func main() {
    let once: i32
    while true { once = 1; break }
    print("ok")
}""", b'ok'),
]

# Cover each width, including same-signed comparisons and neighbor typing.
for kind in ["i8", "i16", "i32", "i64", "u8", "u16", "u32", "u64"]:
    source = f'func test(a: {kind}) {{ if 1 < a && a >= 2 {{ print("ok") }} }}; func main() {{ test(2{kind}) }}'
    CASES.append(("width-" + kind, source, b'ok'))

CASES += [
    ("skipped-runtime-overflow", """func bad(a: i8) -> bool { let overflow = a + 1; return true }
func main() { if false && bad(127i8) { print("bad") }; if true || bad(127i8) { print("ok") } }""", b'ok'),
    ("loop-short-circuit-condition", """func mark() -> bool { print("M"); return true }
func main() { var i = 0; while i < 2 && mark() { i = i + 1; print("B") } }""", b'MBMB'),
    ("branch-break-assignment", """func main() {
    var i = 0
    while i < 2 {
        let value: i32
        if i == 0 { i = 1; continue } else { value = 7 }
        if value == 7 { print("ok") }
        break
    }
}""", b'ok'),
    ("inner-let-once-per-outer", """func main() {
    var i = 0
    while i < 2 {
        let once: i32
        while true { once = 1; break }
        i = i + 1
        print("x")
    }
}""", b'xx'),
    ("outer-let-return", """func test() {
    let once: i32
    while true { once = 1; return }
}
func main() { test(); print("ok") }""", b'ok'),
    ("empty-loop", 'func main() { while false {}; print("ok") }', b'ok'),
]

REJECTIONS = []
def reject(name, source, anchor, message):
    # Authored single-line cases publish exact position and diagnostic.
    REJECTIONS.append((name, source, source.index(anchor) + 1, message))

reject("bad-logical-left", 'func main() { let b = 1 && true }', '1 &&',
       'LYC304: Expected bool, got i32.')
reject("bad-logical-right", 'func main() { let b = false && 1 }', '1 }',
       'LYC304: Expected bool, got i32.')
reject("bad-negation", 'func main() { let b = !1 }', '1 }',
       'LYC304: Expected bool, got i32.')
reject("bad-comparison", 'func main() { let b = "x" < 1 }', '<',
       'LYC313: Integer comparison requires integer operands.')
reject("bad-condition", 'func main() { while 1 { print("bad") } }', '1 {',
       'LYC304: Expected bool, got i32.')
reject("zero-path-unassigned", 'func main() { var x: i32; while false { x = 1 }; let y = x }', 'x }',
       "LYC307: Local 'x' is not definitely assigned.")
reject("repeat-immutable", 'func main() { let x: i32; while true { x = 1 } }', 'x =',
       "LYC314: Loop may assign immutable binding 'x' again.")
reject("continue-immutable", 'func main() { let x: i32; while true { x = 1; continue } }', 'x =',
       "LYC314: Loop may assign immutable binding 'x' again.")
reject("break-possible-assignment", 'func main() { let x: i32; while true { x = 1; break }; x = 2 }', 'x = 2',
       "LYC308: Cannot assign immutable binding 'x' again.")
reject("break-outside", 'func main() { break }', 'main',
       'LYC315: break requires an enclosing loop.')
reject("continue-outside", 'func main() { continue }', 'main',
       'LYC315: continue requires an enclosing loop.')

reject("nested-repeat-immutable", 'func main() { let x: i32; while true { while true { x = 1; break } } }', 'x =',
       "LYC314: Loop may assign immutable binding 'x' again.")
reject("branch-continue-immutable", 'func main() { let x: i32; while true { if true { x = 1; continue } else { break } } }', 'x =',
       "LYC314: Loop may assign immutable binding 'x' again.")
reject("loop-local-invisible", 'func main() { while false { let x = 1 }; let y = x }', 'x }',
       "LYC301: Unknown name 'x'.")
reject("first-iteration-unassigned", 'func main() { var x: i32; while true { let y = x; x = 1 } }', 'x; x',
       "LYC307: Local 'x' is not definitely assigned.")
reject("break-path-unassigned", 'func main() { var x: i32; while true { x = 1; break }; let y = x }', 'x }',
       "LYC307: Local 'x' is not definitely assigned.")

# Division truncates toward zero; remainder keeps the dividend's sign.
# Parameters force actual runtime lowering rather than constant folding.
for kind in ["i8", "i16", "i32", "i64", "u8", "u16", "u32", "u64"]:
    source = f"""func calc(a: {kind}, b: {kind}) {{
    if a / b == 2 && a % b == 1 {{ print("ok") }}
}}
func main() {{ calc(7{kind}, 3{kind}) }}"""
    CASES.append(("division-" + kind, source, b"ok"))
for kind, minimum in [("i8", -128), ("i16", -32768), ("i32", -2147483648), ("i64", -9223372036854775808)]:
    source = f"""func calc(a: {kind}, b: {kind}, minimum: {kind}) {{
    if a / b == -2 && a % b == -1 {{ print("A") }}
    if (-a) / (-b) == -2 && (-a) % (-b) == 1 {{ print("B") }}
    if minimum % -1{kind} == 0 {{ print("C") }}
}}
func main() {{ calc(-7{kind}, 3{kind}, {minimum}{kind}) }}"""
    CASES.append(("signed-division-" + kind, source, b"ABC"))

reject("constant-zero-division", 'func main() { let x = 1 / 0 }', '/',
       'LYC316: Integer division or remainder by zero.')
reject("constant-zero-remainder", 'func main() { let x = 1 % 0 }', '%',
       'LYC316: Integer division or remainder by zero.')
reject("constant-division-overflow", 'func main() { let x = -128i8 / -1i8 }', '/',
       'LYC311: Integer constant operation is outside i8 range.')
reject("constant-computed-negation-overflow", 'func main() { let x = -(-128i8) }', '-(',
       'LYC311: Integer constant operation is outside i8 range.')
CASES.append(("constant-minimum-remainder",
              'func main() { if -128i8 % -1i8 == 0 { print("ok") } }', b"ok"))

RUNTIME_FAILURES = []
for kind in ["i8", "i16", "i32", "i64", "u8", "u16", "u32", "u64"]:
    for operator in ["/", "%"]:
        source = f'func calc(a: {kind}, b: {kind}) -> {kind} {{ return a {operator} b }}; func main() {{ let x = calc(1{kind}, 0{kind}) }}'
        RUNTIME_FAILURES.append(("zero-" + kind + ("-div" if operator == "/" else "-rem"),
            source, source.index(' ' + operator + ' ') + 2,
            'LYR003: integer division or remainder by zero', ['calc', 'main']))
for kind, minimum in [("i8", -128), ("i16", -32768), ("i32", -2147483648), ("i64", -9223372036854775808)]:
    source = f'func calc(a: {kind}, b: {kind}) -> {kind} {{ return a / b }}; func main() {{ let x = calc({minimum}{kind}, -1{kind}) }}'
    RUNTIME_FAILURES.append(("division-overflow-" + kind, source, source.index(' / ') + 2,
        f'LYR001: integer overflow in {kind} division', ['calc', 'main']))
    source = f'func calc(a: {kind}) -> {kind} {{ return -a }}; func main() {{ let x = calc({minimum}{kind}) }}'
    RUNTIME_FAILURES.append(("negation-overflow-" + kind, source, source.index('-a') + 1,
        f'LYR001: integer overflow in {kind} subtraction', ['calc', 'main']))

# Wrapping operations preserve the selected width, in both constant evaluation
# and generated runtime arithmetic. Python computes fixture boundaries only.
for signed in [False, True]:
    for width in [8, 16, 32, 64]:
        kind = ("i" if signed else "u") + str(width)
        maximum = (1 << (width - 1 if signed else width)) - 1
        minimum = -(1 << (width - 1)) if signed else 0
        checks = [
            ("a !+ 1", minimum if signed else 0),
            ("b !- 1", maximum),
            ("a !* 2", -2 if signed else maximum - 1),
            ("!-a", -maximum if signed else 1),
            ("!-b", minimum if signed else 0),
        ]
        body = "; ".join(f'if {expression} == {expected}{kind} {{ print("Y") }}'
                         for expression, expected in checks)
        source = f'func calc(a: {kind}, b: {kind}) {{ {body} }}; func main() {{ calc({maximum}{kind}, {minimum}{kind}) }}'
        CASES.append(("wrapping-runtime-" + kind, source, b"YYYYY"))
        constants = body.replace("!-a", f"!-({maximum}{kind})").replace("!-b", f"!-({minimum}{kind})")
        constants = constants.replace("a !", f"{maximum}{kind} !").replace("b !", f"({minimum}{kind}) !")
        CASES.append(("wrapping-constant-" + kind, 'func main() { ' + constants + ' }', b"YYYYY"))

# Shift width belongs to the left operand, not to the count or destination.
for signed in [False, True]:
    for width in [8, 16, 32, 64]:
        kind = ("i" if signed else "u") + str(width)
        high_bit = -(1 << (width - 1)) if signed else 1 << (width - 1)
        shifted = -1 if signed else 1
        source = f"""func calc(a: {kind}, count: u64) {{
    if a << count == {high_bit}{kind} {{ print("A") }}
    if ({high_bit}{kind} >> count) == {shifted}{kind} {{ print("B") }}
    if a << 0 == a && a >> 0 == a {{ print("C") }}
    if {high_bit}{kind} << 1 == 0 {{ print("D") }}
}}
func main() {{ calc(1{kind}, {width - 1}u64) }}"""
        CASES.append(("shift-width-" + kind, source, b"ABCD"))
        for count in [-1, width]:
            source = f'func calc(a: {kind}, count: i64) -> {kind} {{ return a << count }}; func main() {{ let x = calc(1{kind}, {count}i64) }}'
            RUNTIME_FAILURES.append((f"shift-count-{kind}-{count}", source, source.index(' << ') + 2,
                                     'LYR004: integer shift count outside operand width', ['calc', 'main']))
CASES.append(("signed-right-shift-rounding", 'func main() { if -7i32 >> 1 == -4 { print("ok") } }', b"ok"))
for count in [-1, 8]:
    reject("constant-shift-count-" + str(count), f'func main() {{ let x = 1u8 << {count} }}', '<<',
           'LYC317: Integer shift count is outside operand width.')
reject("shift-count-type", 'func main() { let x = 1 << true }', '<<',
       'LYC312: Integer arithmetic requires integer operands.')

# Casts use mathematical values, not zero-extension of narrow signed bits.
for signed in [False, True]:
    for width in [8, 16, 32, 64]:
        kind = ("i" if signed else "u") + str(width)
        maximum = (1 << (width - 1 if signed else width)) - 1
        minimum = -(1 << (width - 1)) if signed else 0
        source = f"""func convert(low: i64, high: u64) {{
    if (low as {kind}) == {minimum}{kind} {{ print("L") }}
    if (high as {kind}) == {maximum}{kind} {{ print("H") }}
    if (low !as {kind}) == {minimum}{kind} {{ print("M") }}
}}
func main() {{ convert({minimum}i64, {maximum}u64) }}"""
        CASES.append(("cast-boundaries-" + kind, source, b"LHM"))
        wrapped = -1 if signed else (1 << width) - 1
        source = f'func convert(x: i8) {{ if (x !as {kind}) == {wrapped}{kind} {{ print("R") }} }}; func main() {{ convert(-1i8); if ((-1i8) !as {kind}) == {wrapped}{kind} {{ print("C") }} }}'
        CASES.append(("cast-modular-negative-" + kind, source, b"RC"))
        bad = maximum + 1 if signed else -1
        source_kind = "u64" if signed else "i64"
        source = f'func convert(x: {source_kind}) {{ let y = x as {kind} }}; func main() {{ convert({bad}{source_kind}) }}'
        RUNTIME_FAILURES.append(("cast-out-of-range-" + kind, source, source.index(' as ') + 2,
                                 f'LYR005: integer conversion outside {kind} range', ['convert', 'main']))
        reject("constant-cast-range-" + kind,
               f'func main() {{ let x = ({bad}{source_kind}) as {kind} }}', 'as',
               f'LYC318: Integer conversion is outside {kind} range.')
CASES += [
    ("cast-single-evaluation", 'func value() -> i64 { print("V"); return 255i64 }; func main() { if (value() as u8) == 255u8 { print("C") }; if (value() !as i8) == -1i8 { print("M") } }', b"VCVM"),
    ("cast-modular-truncation", 'func convert(x: u64) { if (x !as i8) == -128i8 { print("A") }; if (x !as u8) == 128u8 { print("B") } }; func main() { convert(384u64) }', b"AB"),
]

# Complementary cast bounds exercise signed-low and unsigned-high guards.
for width in [8, 16, 32]:
    for kind, source_kind, bad in [(f"i{width}", "i64", -(1 << (width - 1)) - 1),
                                   (f"u{width}", "u64", 1 << width)]:
        source = f'func convert(x: {source_kind}) {{ let y = x as {kind} }}; func main() {{ convert({bad}{source_kind}) }}'
        RUNTIME_FAILURES.append(("cast-complementary-bound-" + kind, source, source.index(' as ') + 2,
                                 f'LYR005: integer conversion outside {kind} range', ['convert', 'main']))
CASES.append(("cast-nested-arithmetic", 'func main() { if ((120 + 7) as i8) == 127i8 { print("A") }; if (1u8 + (2i64 as u8)) == 3u8 { print("B") } }', b"AB"))
reject("cast-does-not-widen-operand", 'func main() { let x = (127i8 + 1i8) as i64 }', '+',
       'LYC311: Integer constant operation is outside i8 range.')

# Boolean equality is eager, unlike && and ||; both operands execute once.
CASES += [
    ("boolean-equality", 'func main() { if true == true { print("A") }; if false == false { print("B") }; if true != false { print("C") }; if false != true { print("D") }; if true == false { print("bad") } }', b"ABCD"),
    ("boolean-equality-effects", 'func first() -> bool { print("A"); return false }; func second() -> bool { print("B"); return true }; func main() { if first() != second() { print("C") } }', b"ABC"),
]
reject("boolean-integer-equality", 'func main() { let x = true == 1 }', '==',
       'LYC313: Integer comparison requires integer operands.')
reject("boolean-ordering", 'func main() { let x = false < true }', '<',
       'LYC313: Integer comparison requires integer operands.')

# Compound assignment reads initialized storage and uses ordinary arithmetic widths.
for width in [8, 16, 32, 64]:
    for prefix in ["i", "u"]:
        kind = f"{prefix}{width}"
        source = f'func main() {{ var x = 12{kind}; x += 4; x -= 2; x *= 3; x /= 2; if x == 21{kind} {{ print("A") }}; x !+= 1; x !-= 2; x !*= 3; if x == 60{kind} {{ print("B") }} }}'
        CASES.append(("compound-operators-" + kind, source, b"AB"))
CASES.append(("compound-effects", 'func rhs() -> i32 { print("R"); return 3 }; func main() { var x = 2; x += rhs(); if x == 5 { print("V") } }', b"RV"))
CASES.append(("compound-widening", 'func main() { var x = 1i64; x += 2u32; if x == 3i64 { print("ok") } }', b"ok"))
CASES.append(("compound-wrap-boundary", 'func main() { var x = 127i8; x !+= 1; if x == -128i8 { print("A") }; x !-= 1; if x == 127i8 { print("B") }; x !*= 2; if x == -2i8 { print("C") } }', b"ABC"))
reject("compound-uninitialized", 'func main() { var x: i32; x += 1 }', 'x +=', "LYC307: Local 'x' is not definitely assigned.")
reject("compound-immutable", 'func main() { let x = 1; x += 1 }', 'x +=', "LYC308: Cannot assign immutable binding 'x' again.")
reject("compound-narrowing", 'func main() { var x = 1i8; x += 2i16 }', '2i16', 'LYC304: Expected i8, got i16.')
reject("compound-bool", 'func main() { var x = true; x += false }', '+=', 'LYC312: Integer arithmetic requires integer operands.')
reject("compound-constant-zero", 'func main() { var x = 1; x /= 0 }', '/=', 'LYC316: Integer division or remainder by zero.')
for name, operator, initial, rhs, diagnostic in [
    ("compound-overflow", "+=", "127i8", "1i8", "LYR001: integer overflow in i8 addition"),
    ("compound-zero", "/=", "1i8", "0i8", "LYR003: integer division or remainder by zero"),
]:
    source = f'func change(y: i8) {{ var x = {initial}; x {operator} y }}; func main() {{ change({rhs}) }}'
    RUNTIME_FAILURES.append((name, source, source.index(operator) + 1, diagnostic, ['change', 'main']))

# Bitwise results use an existing whole-range-compatible operand type.
for width in [8, 16, 32, 64]:
    for prefix in ["i", "u"]:
        kind = f"{prefix}{width}"
        source = f'func bits(a: {kind}, b: {kind}) {{ if (a & b) == 8{kind} {{ print("A") }}; if (a | b) == 14{kind} {{ print("O") }}; if (a ^ b) == 6{kind} {{ print("X") }} }}; func main() {{ bits(12{kind}, 10{kind}); if (12{kind} ^ 10) == 6{kind} {{ print("C") }} }}'
        CASES.append(("bitwise-" + kind, source, b"AOXC"))
    kind = f"i{width}"
    source = f'func bits(a: {kind}) {{ if (a & 1) == 1{kind} {{ print("A") }}; if (a ^ 1) == -2{kind} {{ print("X") }}; if (a | 1) == -1{kind} {{ print("O") }} }}; func main() {{ bits(-1{kind}) }}'
    CASES.append(("bitwise-signed-" + kind, source, b"AXO"))
CASES += [
    ("bitwise-widen", 'func main() { let a = 255u8; let b = 256u16; if (a | b) == 511u16 { print("A") }; let c = -1i64; if (c & b) == 256i64 { print("B") } }', b"AB"),
    ("bitwise-effects", 'func a() -> u8 { print("A"); return 12u8 }; func b() -> u8 { print("B"); return 10u8 }; func main() { if (a() ^ b()) == 6u8 { print("X") } }', b"ABX"),
]
reject("bitwise-incompatible", 'func main() { let x = 1i32 & 1u32 }', '&', 'LYC312: No integer arithmetic type contains both i32 and u32.')
reject("bitwise-bool", 'func main() { let x = true | false }', '|', 'LYC312: Integer arithmetic requires integer operands.')

# Value-producing conditionals keep effects inside the chosen branch.
CASES += [
    ("if-string", 'func main() { let name = if true { "enabled" } else { "disabled" }; println(name) }', b"enabled\n"),
    ("if-context", 'func choose(b: bool) -> u8 { return if b { 255 } else { 2 } }; func main() { let x: u8 = if false { 1 } else { 255 }; if x == choose(true) { print("ok") } }', b"ok"),
    ("if-widen", 'func main() { let x = if true { 255u8 } else { 256u16 }; if x == 255u16 { print("A") }; let y: i64 = if false { 1i32 } else { 4294967295u32 }; if y == 4294967295i64 { print("B") } }', b"AB"),
    ("if-argument-effects", 'func first() -> bool { print("C"); return false }; func use(a: string, b: string) { print(a); print(b) }; func main() { use(if first() { print("bad"); "A" } else { print("E"); "B" }, if true { print("T"); "D" } else { "bad" }) }', b"CETBD"),
    ("if-nested", 'func main() { let x = if false { 1 } else if true { if false { 2 } else { 3 } } else { 4 }; if x == 3 { print("ok") } }', b"ok"),
    ("if-branch-locals", 'func main() { let x = if true { let n = 2; n * 3 } else { let n = 3; n + 4 }; if x == 6 { print("ok") } }', b"ok"),
    ("if-flow", 'func main() { var x: i32; let y = if true { x = 2; x + 1 } else { x = 4; x + 1 }; if x + y == 5 { print("ok") } }', b"ok"),
    ("if-return-arm", 'func choose(b: bool) -> i32 { let x = if b { return 7 } else { 3 }; return x }; func main() { if choose(true) == 7 { print("A") }; if choose(false) == 3 { print("B") } }', b"AB"),
    ("if-break-arm", 'func main() { var i = 0; while i < 3 { i += 1; let x = if i == 2 { break } else { i }; if x == 1 { print("A") } }; if i == 2 { print("B") } }', b"AB"),
    ("if-newline-value", 'func main() { let x = if true { 3\n} else { 4\n}; if x == 3 { print("ok") } }', b"ok"),
    ("if-discard-unit", 'func main() { let x = if true { print("A"); } else { print("bad"); }; let y: unit = x }', b"A"),
    ("if-runtime-overflow-skipped", 'func bad(x: i8) -> i8 { return x + 1 }; func main() { let x = if true { 3i8 } else { bad(127i8) }; if x == 3i8 { print("ok") } }', b"ok"),
]
reject("if-missing-else", 'func main() { let x = if true { 1 } }', 'true', 'LYC319: Value-producing if requires an else branch.')
reject("if-mixed-types", 'func main() { let x = if true { 1i32 } else { 1u32 } }', 'true', 'LYC320: Incompatible conditional branch types: i32 and u32.')
reject("if-semicolon-discards", 'func main() { let x = if true { 1; } else { 2 } }', 'true', 'LYC320: Incompatible conditional branch types: unit and i32.')
reject("if-context-no-narrow", 'func main() { let x: u8 = if true { 1i32 } else { 2u8 } }', '1i32', 'LYC304: Expected u8, got i32.')
reject("if-branch-scope", 'func main() { let x = if true { let n = 1; n } else { n } }', 'n } }', "LYC301: Unknown name 'n'.")
reject("if-short-circuit-initialization", 'func main() { var x: i32; let b = false && (if true { x = 1; true } else { x = 2; false }); println(if x == 1 { "bad" } else { "bad" }) }', 'x ==', "LYC307: Local 'x' is not definitely assigned.")
reject("if-short-circuit-possible-write", 'func main() { let x: i32; let b = true || (if true { x = 1; true } else { x = 2; false }); x = 3 }', 'x = 3', "LYC308: Cannot assign immutable binding 'x' again.")

# A typed conditional is still a typed arithmetic neighbor, in either order.
for name, expression in [
    ("if-width-left", '(if b { 255u8 } else { 0u8 }) + 1'),
    ("if-width-right", '1 + (if b { 255u8 } else { 0u8 })'),
    ("if-width-local", '(if b { let n = 255u8; n } else { 0u8 }) + 1'),
]:
    source = f'func add(b: bool) {{ let x = {expression} }}; func main() {{ add(true) }}'
    RUNTIME_FAILURES.append((name, source, source.index('+') + 1,
                             'LYR001: integer overflow in u8 addition', ['add', 'main']))

# The condition is also repeated; its writes belong to the loop's back-edge check.
reject("loop-condition-immutable-repeat", 'func main() { let once: i32; while (if true { once = 1; true } else { false }) { } }', 'once =', "LYC314: Loop may assign immutable binding 'once' again.")
CASES += [
    ("loop-condition-write-break", 'func main() { let once: i32; while (if true { once = 1; true } else { once = 2; false }) { break }; if once == 1 { print("ok") } }', b"ok"),
    ("loop-condition-write-false", 'func main() { var x: i32; while (if true { x = 3; false } else { x = 4; false }) { print("bad") }; if x == 3 { print("ok") } }', b"ok"),
]

reject("loop-condition-break-unassigned", 'func main() { var x: i32; while (if true { break } else { x = 1; false }) { }; if x == 1 { print("bad") } }', 'x ==', "LYC307: Local 'x' is not definitely assigned.")
CASES.append(("loop-condition-break-assigned", 'func main() { var x: i32; while (if true { x = 2; break } else { x = 1; false }) { }; if x == 2 { print("ok") } }', b"ok"))

# Review regressions: RHS effects precede the eventual immutable store.
reject("assignment-rhs-double-write", 'func main() { let x: i32; x = if true { x = 1; 2 } else { 3 } }', 'x = if', "LYC308: Cannot assign immutable binding 'x' again.")
reject("assignment-rhs-one-arm-write", 'func main() { let x: i32; x = if false { 2 } else { x = 1; 3 } }', 'x = if', "LYC308: Cannot assign immutable binding 'x' again.")
CASES.append(("assignment-rhs-return-excluded", 'func choose(b: bool) { let x: i32; x = if b { x = 1; return } else { 3 }; if x == 3 { print("ok") } }; func main() { choose(true); choose(false) }', b"ok"))

# Receiving storage cannot widen a typed intermediate operation.
for name, expression in [
    ("if-annotated-width-left", '(if b { 255u8 } else { 0u8 }) + 1'),
    ("if-annotated-width-right", '1 + (if b { 255u8 } else { 0u8 })'),
    ("if-annotated-width-local", '(if b { let n = 255u8; n } else { 0u8 }) + 1'),
]:
    source = f'func add(b: bool) {{ let x: u16 = {expression} }}; func main() {{ add(true) }}'
    RUNTIME_FAILURES.append((name, source, source.index('+') + 1,
                             'LYR001: integer overflow in u8 addition', ['add', 'main']))
for name, expression in [
    ("if-typed-neighbor-left", '(if true { 256u16 } else { 0u16 }) + 1u8'),
    ("if-typed-neighbor-right", '1u8 + (if true { 256u16 } else { 0u16 })'),
    ("if-typed-neighbor-local", '(if true { let n = 256u16; n } else { 0u16 }) + 1u8'),
]:
    source = f'func main() {{ let x = {expression}; if x == 257u16 {{ print("ok") }} }}'
    CASES.append((name, source, b"ok"))

# A discarded statement-if may still transfer on every arm. Such an arm
# contributes no type to the enclosing value-if, even with a discard marker.
for transfer in ['return', 'break', 'continue']:
    expression = f'(if b {{ if c {{ {transfer} }} else {{ {transfer} }}; }} else {{ 255u8 }}) + 1'
    body = f'let x = {expression}'
    if transfer != 'return':
        body = f'while true {{ {body}; break }}'
    source = f'func add(b: bool, c: bool) {{ {body} }}; func main() {{ add(false, false) }}'
    RUNTIME_FAILURES.append(('if-statement-transfer-' + transfer, source, source.index('+') + 1,
                             'LYR001: integer overflow in u8 addition', ['add', 'main']))

if __name__ == "__main__":
    raise SystemExit(execution.main(CASES, [], REJECTIONS, RUNTIME_FAILURES))
