#!/usr/bin/env python3
"""Retained execution checks. Usage: run.py COMPILER [ARG ...]. CC defaults to cc."""
import os
import json
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

# Authored observable expectations, independent of emitted C formatting.
CASES = [
    ('closed-pipe', 'func main() { println("unwritable") }', b''),
    ('hello', 'func main() { println("Hello, Ken and Tom!") }', b'Hello, Ken and Tom!\n'),
    ('empty', 'func main() {}', b''),
    ('bytes', 'func main() { print("A\\0B\\xFF"); println("é") }', b'A\x00B\xff\xc3\xa9\n'),
    ('forward', 'func main() { greet() }\nfunc greet() { println("Hi") }', b'Hi\n'),
    ('branch-assignment', '''func choose(flag: bool) -> string {
    let result: string
    if flag { result = "yes" } else { result = "no" }
    return result
}
func main() { println(choose(true)); println(choose(false)) }''', b'yes\nno\n'),
    ('nested-else-effects', '''func condition() -> bool { print("C"); return true }
func main() {
    if true { print("A") } else if condition() { print("B") }
    if false { print("X") } else if condition() { print("Y") }
}''', b'ACY'),
    ('returned-bytes', '''func value() -> string { return "A\\0B" }
func main() { var x = value(); print(x); x = "!"; println(x) }''', b'A\x00B!\n'),
    ('unicode-identifiers', 'func café(élan: string) { println(élan) }\nfunc main() { café("ok") }', b'ok\n'),
    ('unit-values', 'func work() {}\nfunc main() { let x = work(); let y: unit = (); y; x }', b''),
    ('argument-order', '''func first() -> string { print("A"); return "one" }
func second() -> string { print("B"); return "two" }
func consume(a: string, b: string) { print(a); println(b) }
func main() { consume(first(), second()) }''', b'ABonetwo\n'),
]


# Runtime operands are parameters so these are not just constant-checker tests.
# Success checks make the result observable without adding numeric printing or
# comparison operators to the language: difference must be exactly zero for
# both endpoint additions to remain within range.
OVERFLOWS = []
for signed in (False, True):
    for width in (8, 16, 32, 64):
        kind = ('i' if signed else 'u') + str(width)
        minimum = -(1 << (width - 1)) if signed else 0
        maximum = (1 << (width - (1 if signed else 0))) - 1
        for operator, name, a, b in [('+', 'addition', maximum, 1),
                                     ('-', 'subtraction', minimum, 1),
                                     ('*', 'multiplication', maximum, 2)]:
            source = (f'func calc(a: {kind}, b: {kind}) -> {kind} {{ return a {operator} b }}\n'
                      f'func main() {{ calc({a}{kind}, {b}{kind}) }}')
            OVERFLOWS.append((kind + '-' + name, source, kind, name))
        if signed:
            for label, operator, name, a, b in [
                ('negative-add', '+', 'addition', minimum, -1),
                ('negative-sub', '-', 'subtraction', maximum, -1),
                ('negative-positive-mul', '*', 'multiplication', minimum, 2),
                ('positive-negative-mul', '*', 'multiplication', 2, minimum),
                ('negative-negative-mul', '*', 'multiplication', minimum, -1),
            ]:
                source = (f'func calc(a: {kind}, b: {kind}) -> {kind} {{ return a {operator} b }}\n'
                          f'func main() {{ calc({a}{kind}, {b}{kind}) }}')
                OVERFLOWS.append((kind + '-' + label, source, kind, name))
        boundary_pairs = [(minimum, 1), (maximum, 1), (minimum, 0), (0, maximum)]
        if signed:
            boundary_pairs += [(-3, 2), (3, -2), (-3, -2)]
        calls = []
        for a, b in boundary_pairs:
            calls.append(f'verify(calc({a}{kind}, {b}{kind}), {a*b}{kind})')
        source = (f'func calc(a: {kind}, b: {kind}) -> {kind} {{ return a * b }}\n'
                  f'func verify(value: {kind}, expected: {kind}) {{\n'
                  f'let difference = value - expected\n'
                  f'let upper = difference + {maximum}{kind}\n'
                  f'let lower = difference + {minimum}{kind}\n}}\n'
                  + 'func main() { ' + '; '.join(calls) + '; println("ok") }')
        CASES.append((kind + '-multiplication-boundaries', source, b'ok\n'))
        source = (f'func calc(a: {kind}, b: {kind}) -> {kind} {{ return (a + b) * 2{kind} - 4{kind} }}\n'
                  f'func verify(value: {kind}) {{\n'
                  f'let difference = value - 10{kind}\n'
                  f'let upper = difference + {maximum}{kind}\n'
                  f'let lower = difference + {minimum}{kind}\n}}\n'
                  f'func main() {{ verify(calc(3{kind}, 4{kind})); println("ok") }}')
        CASES.append((kind + '-arithmetic-result', source, b'ok\n'))


CASES.append(('operand-order', """func first() -> i32 { print("A"); return 3 }
func second() -> i32 { print("B"); return 4 }
func main() { let result = first() + second(); println("ok") }""", b'ABok\n'))
# A completed activation must not remain in a later failure's linked trace.
OVERFLOWS.append(('returned-frame',
    'func calc(a: i8, b: i8) -> i8 { return a + b }\n'
    'func finished() { if true { return } }\n'
    'func main() { finished(); calc(127i8, 1i8) }', 'i8', 'addition'))


REJECTIONS = [
    ('unknown-name', 'func main() { missing() }', 15,
     "LYC301: Unknown name 'missing'."),
    ('constant-overflow', 'func main() { let x = 127i8 + 1i8 }', 29,
     'LYC311: Integer constant operation is outside i8 range.'),
]


def run(command, **options):
    return subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                          timeout=20, **options)


def main(cases=CASES, overflows=OVERFLOWS, rejections=REJECTIONS, runtime_failures=()):
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit(__doc__)
    cc = shlex.split(os.environ.get('CC', 'cc'))
    failures = 0
    with tempfile.TemporaryDirectory(prefix='lyric-execute-') as temporary:
        folder = Path(temporary)
        all_cases = [(name, source, expected, None) for name, source, expected in cases]
        all_cases += [(name, source, b'', (kind, operation)) for name, source, kind, operation in overflows]
        all_cases += [(name, source, b'', (column, diagnostic, trace))
                      for name, source, column, diagnostic, trace in runtime_failures]
        for name, source, expected, failure in all_cases:
            try:
                path = folder / (name + '.ly')
                path.write_text(source, encoding='utf-8')
                emitted = run(compiler + ['--emit-c', str(path)])
                if emitted.returncode != 0 or emitted.stderr or not emitted.stdout:
                    raise AssertionError(f'emission failed: exit {emitted.returncode}, {emitted.stderr!r}')
                c_path = folder / (name + '.c')
                c_path.write_bytes(emitted.stdout)
                # Both configurations must preserve the same language behavior.
                for optimization in ['-O0', '-O2']:
                    binary = folder / (name + optimization)
                    built = run(cc + ['-std=c11', optimization, str(c_path), '-o', str(binary)])
                    if built.returncode != 0:
                        raise AssertionError(f'C build failed: {built.stderr!r}')
                    if name == 'closed-pipe':
                        # No reader exists before launch, so failure is not a race.
                        read_end, write_end = os.pipe()
                        os.close(read_end)
                        try:
                            executed = subprocess.run([str(binary)], stdout=write_end,
                                                      stderr=subprocess.PIPE, timeout=20)
                        finally:
                            os.close(write_end)
                        executed.stdout = b''
                    else:
                        executed = run([str(binary)])
                    expected_status = 0
                    expected_error = b''
                    if name == 'closed-pipe':
                        expected_status = 1
                        expected_error = (json.dumps(str(path))
                            + ':1:15: runtime error LYR002: output failure\n'
                            + '  in "main"\n').encode()
                    if failure is not None and len(failure) == 3:
                        column, diagnostic, trace = failure
                        expected_status = 1
                        expected_error = (json.dumps(str(path)) + f':1:{column}: runtime error {diagnostic}\n'
                            + ''.join('  in ' + json.dumps(name) + '\n' for name in trace)).encode()
                    elif failure is not None:
                        kind, operation = failure
                        # Every authored expression is on line one; locate its
                        # operator, not a second implementation of source parsing.
                        operator = {'addition': '+', 'subtraction': '-', 'multiplication': '*'}[operation]
                        column = source.index(' ' + operator + ' ') + 2
                        expected_status = 1
                        expected_error = (json.dumps(str(path)) + f':1:{column}: runtime error LYR001: integer overflow in {kind} {operation}\n'
                                          + '  in "calc"\n  in "main"\n').encode()
                    if (executed.returncode, executed.stdout, executed.stderr) != (expected_status, expected, expected_error):
                        raise AssertionError(f'wrong execution: {executed.returncode}, {executed.stdout!r}, {executed.stderr!r}')
                print(f'PASS {name}')
            except (AssertionError, OSError, subprocess.TimeoutExpired) as error:
                failures += 1
                print(f'FAIL {name}: {error}')
        for name, source, column, diagnostic in rejections:
            try:
                path = folder / (name + '.ly')
                path.write_text(source, encoding='utf-8')
                emitted = run(compiler + ['--emit-c', str(path)])
                expected_error = (json.dumps(str(path)) + f':1:{column}: error {diagnostic} (byte {column-1})\n').encode()
                if (emitted.returncode, emitted.stdout, emitted.stderr) != (1, b'', expected_error):
                    raise AssertionError(f'wrong rejection: {emitted.returncode}, {emitted.stdout!r}, {emitted.stderr!r}')
                print(f'PASS {name}')
            except (AssertionError, OSError, subprocess.TimeoutExpired) as error:
                failures += 1
                print(f'FAIL {name}: {error}')
    total = len(all_cases) + len(rejections)
    print(f'{total - failures}/{total} execution cases passed')
    return 1 if failures else 0


if __name__ == '__main__':
    raise SystemExit(main())
