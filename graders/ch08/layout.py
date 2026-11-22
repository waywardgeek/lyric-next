#!/usr/bin/env python3
"""Retained initial class layout checks. Trailing arguments are the compiler command.

Requires an installed C compiler (CC, default cc). Each successful compilation
must exit zero with empty stderr; emitted executables must exit zero and write
exactly b'ok' to stdout with empty stderr. Both layouts run at O0 and O2.
These cases test indexed identity, growth, and explicit destruction. They do not prove allocation balance.
"""
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

CLASS = '''class Counter {
 count: i32
 func Counter(self, n: i32) { self.count = n }
 func read(self) -> i32 { return self.count }
 func add(self, n: i32) { self.count = self.count + n }
}
'''
CASES = [
    ('destroy-reconstruct', CLASS + 'func main() { let c = Counter(7); c.destroy(); let d = Counter(19); if d.read() == 19 { d.destroy(); print("ok") } }'),
    ('repeated-reuse', CLASS + 'func main() { var i = 0; var sum = 0; while i < 257 { let c = Counter(i); sum = sum + c.read(); c.destroy(); i = i + 1 }; if sum == 32896 { print("ok") } }'),
    ('helper-destroy', CLASS + 'func dispose(c: Counter) { c.destroy() }; func main() { let c = Counter(7); dispose(c); let d = Counter(9); if d.count == 9 { dispose(d); print("ok") } }'),
    ('construct-read', CLASS + 'func main() { let c = Counter(7); if c.read() == 7 { print("ok") } }'),
    ('shared-handle', CLASS + 'func main() { let c = Counter(7); let d = c; d.add(2); if c.count == 9 { print("ok") } }'),
    ('growth-keeps-identity', CLASS + '''func main() {
 let first = Counter(7); var i = 0
 while i < 257 { let next = Counter(i); i = i + 1 }
 first.add(2); if first.read() == 9 { print("ok") }
}'''),
    ('field-rhs-growth', CLASS + '''func grow() -> i32 {
 var i = 0; while i < 257 { let next = Counter(i); i = i + 1 }; return 19
}
func main() { let c = Counter(7); c.count = grow(); if c.count == 19 { print("ok") } }
'''),
]

def run(command):
    return subprocess.run(command, capture_output=True, timeout=60)


def main():
    command = sys.argv[1:]
    if not command:
        raise SystemExit('Supply the compiler command after layout.py')
    total = 0
    with tempfile.TemporaryDirectory(prefix='lyric-layout-') as temporary:
        root = Path(temporary)
        for name, text in CASES:
            source = root / (name + '.ly')
            source.write_text(text + '\n')
            for layout in ('aos', 'soa'):
                options = ['--emit-c'] + (['--soa'] if layout == 'soa' else [])
                compiled = run(command + options + [str(source)])
                assert compiled.returncode == 0 and not compiled.stderr, (name, layout, compiled.stderr)
                cfile = root / (name + '-' + layout + '.c')
                cfile.write_bytes(compiled.stdout)
                for optimization in ('-O0', '-O2'):
                    executable = root / 'program'
                    built = run(shlex.split(os.environ.get('CC', 'cc')) + ['-std=c11', optimization, str(cfile), '-o', str(executable)])
                    assert built.returncode == 0, (name, layout, built.stderr)
                    result = run([str(executable)])
                    assert (result.returncode, result.stdout, result.stderr) == (0, b'ok', b''), (name, layout, optimization, result)
                    total += 1
                    print('PASS', name, layout, optimization)
    print(f'{total}/{len(CASES) * 4} layout executions passed')

if __name__ == '__main__':
    main()
