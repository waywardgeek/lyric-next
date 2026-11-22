"""Chapter 7 walkthrough: independent arrays and stable storage references.

Run from the repository root after a Release build. Uses installed tools only;
creates disposable local files and invokes no network services.
"""

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
    ('Grow, sum, copy, and update only the copy through a reference helper.',
     'func change(a: ref [i32]) { a[0] = 99; a.push(6) }\n'
     'func main() {\n'
     '    var numbers: [i32] = []\n'
     '    var i = 1\n'
     '    while i <= 5 { numbers.push(i); i += 1 }\n'
     '    var total = 0\n'
     '    var index = 0u64\n'
     '    while index < len(numbers) { total += numbers[index]; index += 1u64 }\n'
     '    var copy = numbers\n'
     '    change(ref copy)\n'
     '    if total == 15 { println("Sum is 15") }\n'
     '    if numbers[0] == 1 && len(numbers) == 5u64 { println("Original unchanged") }\n'
     '    if copy[0] == 99 && len(copy) == 6u64 { println("Copy changed") }\n'
     '}', b'Sum is 15\nOriginal unchanged\nCopy changed\n'),
    ('A fresh element reference after growth is valid.',
     'func main() {\n'
     '    var a = [1]\n'
     '    let old = ref a[0]\n'
     '    old = 7\n'
     '    a.push(2)\n'
     '    let fresh = ref a[0]\n'
     '    fresh += 1\n'
     '    if a[0] == 8 { println("Fresh reference works") }\n'
     '}', b'Fresh reference works\n'),
    ('An old element reference cannot be used after potentially relocating growth.',
     'func main() { var a = [1]; let slot = ref a[0]; a.push(2); slot = 7 }', None),
]
with tempfile.TemporaryDirectory(prefix='lyric-arrays-') as directory:
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
            column = text.index('slot = 7') + 1
            diagnostic = (json.dumps(str(source))
                + f':1:{column}: error LYC326: Borrowed storage may have been invalidated. (byte {column - 1})\n').encode()
            if (emitted.returncode, emitted.stdout, emitted.stderr) != (1, b'', diagnostic):
                raise SystemExit(f'Expected lifetime rejection: {emitted}')
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
