"""Chapter 6 walkthrough: integer expressions, branch values, and loops."""

import json
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile


compiler = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
cc = shlex.split(os.environ.get('CC', 'cc'))
examples = [
    ('Sum a range with a loop.',
     'func sum_to(n: i32) -> i32 {\n'
     '    var total = 0\n'
     '    var i = 1\n'
     '    while i <= n { total += i; i += 1 }\n'
     '    return total\n'
     '}\n'
     'func main() { if sum_to(10) == 55 { println("Sum is 55") } }', b'Sum is 55\n'),
    ('The skipped operand has no effects.',
     'func mark() -> bool { print("called "); return true }\n'
     'func main() {\n'
     '    if false && mark() { print("bad") }\n'
     '    if true || mark() { print("skipped ") }\n'
     '    if true && mark() { println("done") }\n'
     '}', b'skipped called done\n'),
    ('Continue reevaluates the condition; break exits the loop.',
     'func main() {\n'
     '    var i = 0\n'
     '    while i < 10 {\n'
     '        i = i + 1\n'
     '        if i < 3 { continue }\n'
     '        print("reached three")\n'
     '        break\n'
     '    }\n'
     '    println("")\n'
     '}', b'reached three\n'),
    ('A conditional produces a value; only its selected branch runs.',
     'func label(n: i32) -> string {\n'
     '    return if n % 2 == 0 { "even" } else { "odd" }\n'
     '}\n'
     'func main() { println(label(10)); println(label(7)) }', b'even\nodd\n'),
    ('Wrapping and modular conversion use explicit fixed-width rules.',
     'func main() {\n'
     '    var bits: u8 = 255u8\n'
     '    bits !+= 2u8\n'
     '    let shifted = (bits | 2u8) << 2\n'
     '    let low: u8 = (-1i16) !as u8\n'
     '    if shifted == 12u8 && low == 255u8 { println("Bits checked") }\n'
     '}', b'Bits checked\n'),
    ('An outer immutable binding cannot be initialized repeatedly.',
     'func main() { let once: i32; while true { once = 1 } }', None),
]
with tempfile.TemporaryDirectory(prefix='lyric-executable-') as directory:
    folder = Path(directory)
    source = folder / 'example.ly'
    c_source = folder / 'example.c'
    binary = folder / 'example'
    for label, text, expected in examples:
        print(label, flush=True)
        print(text, flush=True)
        source.write_text(text, encoding='utf-8')
        emitted = subprocess.run(compiler + ['--emit-c', str(source)],
                                 capture_output=True, timeout=20)
        if expected is None:
            column = text.index('once =') + 1
            diagnostic = (json.dumps(str(source))
                + f":1:{column}: error LYC314: Loop may assign immutable binding 'once' again. (byte {column - 1})\n").encode()
            if (emitted.returncode, emitted.stdout, emitted.stderr) != (1, b'', diagnostic):
                raise SystemExit(f'Expected loop rejection: {emitted}')
            print(emitted.stderr.decode('utf-8'), end='', flush=True)
            print('Expected static rejection observed.\n', flush=True)
            continue
        if emitted.returncode != 0 or emitted.stderr or not emitted.stdout:
            raise SystemExit(f'Emission failed: {emitted}')
        c_source.write_bytes(emitted.stdout)
        built = subprocess.run(cc + ['-std=c11', '-O2', str(c_source), '-o', str(binary)],
                               capture_output=True, timeout=20)
        if built.returncode != 0:
            raise SystemExit(f'C compilation failed: {built.stderr!r}')
        result = subprocess.run([str(binary)], capture_output=True, timeout=20)
        if (result.returncode, result.stdout, result.stderr) != (0, expected, b''):
            raise SystemExit(f'Unexpected execution: {result}')
        print(result.stdout.decode('utf-8'), end='', flush=True)
        print(flush=True)
print('Temporary source, generated C, and executables removed.')
