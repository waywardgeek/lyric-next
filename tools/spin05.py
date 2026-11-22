"""Chapter 5 walkthrough: emit, compile, and execute disposable programs."""

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
    ('Our first executable.',
     'func main() { println("Hello, Ken and Tom!") }', b'Hello, Ken and Tom!\n'),
    ('Arguments produce their effects left to right.',
     'func first() -> string { print("A"); return "one" }\n'
     'func second() -> string { print("B"); return "two" }\n'
     'func consume(a: string, b: string) { print(a); println(b) }\n'
     'func main() { consume(first(), second()) }', b'ABonetwo\n'),
    ('Runtime overflow fails before an invalid C operation.',
     'func calc(a: i8, b: i8) -> i8 { return a + b }\n'
     'func main() { calc(127i8, 1i8) }', None),
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
        if emitted.returncode != 0 or emitted.stderr or not emitted.stdout:
            raise SystemExit(f'Emission failed: {emitted}')
        c_source.write_bytes(emitted.stdout)
        built = subprocess.run(cc + ['-std=c11', '-O2', str(c_source), '-o', str(binary)],
                               capture_output=True, timeout=20)
        if built.returncode != 0:
            raise SystemExit(f'C compilation failed: {built.stderr!r}')
        result = subprocess.run([str(binary)], capture_output=True, timeout=20)
        if expected is not None:
            if (result.returncode, result.stdout, result.stderr) != (0, expected, b''):
                raise SystemExit(f'Unexpected execution: {result}')
            print(result.stdout.decode('utf-8'), end='', flush=True)
        else:
            column = text.index(' + ') + 2
            diagnostic = (json.dumps(str(source))
                + f':1:{column}: runtime error LYR001: integer overflow in i8 addition\n'
                + '  in "calc"\n  in "main"\n').encode()
            if (result.returncode, result.stdout, result.stderr) != (1, b'', diagnostic):
                raise SystemExit(f'Expected located overflow: {result}')
            print(result.stderr.decode('utf-8'), end='', flush=True)
            print('Expected runtime failure observed.', flush=True)
        print(flush=True)
print('Temporary source, generated C, and executables removed.')
