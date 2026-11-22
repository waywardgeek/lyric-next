"""Chapter 8: shared identity, counted array ownership, and field stability.

Run after a Release build, from the repository root. Uses installed tools and
local disposable files only; never installs tools or contacts a service.
"""
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
import tempfile

compiler = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
cc = shlex.split(os.environ.get('CC', 'cc'))
success = '''value class Counter {
    count: i32
    func Counter(self, n: i32) { self.count = n }
    func add(self, n: i32) { self.count = self.count + n }
    final func finish(self) { println("Finalized once") }
}
func demonstrate() {
    let object = Counter(1)
    var first = [object]
    let second = first
    second[0].add(4)
    assert(first[0].count == 5)
    first.resize(0u64)
    assert(capacity(first) == 0u64)
    assert(second[0].count == 5)
    println("Independent arrays, shared object")
}
func main() {
    demonstrate()
    println("Helper returned")
}
'''
rejected = '''class Counter {
    count: i32
    func Counter(self, n: i32) { self.count = n }
}
func main() {
    let object = Counter(1)
    let field = ref object.count
    let other = Counter(2)
    field = 3
}
'''
expected = b'Independent arrays, shared object\nFinalized once\nHelper returned\n'
with tempfile.TemporaryDirectory(prefix='lyric-classes-') as directory:
    folder = Path(directory)
    source = folder / 'example.ly'
    c_source = folder / 'example.c'
    binary = folder / 'example'
    for layout in ('AoS', 'SoA'):
        options = ['--emit-c']
        if layout == 'SoA':
            options.append('--soa')
        print(f'=== {layout}: shared identity and counted ownership ===', flush=True)
        print(success, flush=True)
        source.write_text(success, encoding='utf-8')
        emitted = subprocess.run(compiler + options + [str(source)],
                                 capture_output=True, timeout=20)
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
        print(result.stdout.decode(), end='', flush=True)
        print('A field borrow cannot cross class growth:', flush=True)
        print(rejected, flush=True)
        source.write_text(rejected, encoding='utf-8')
        result = subprocess.run(compiler + options + [str(source)],
                                capture_output=True, timeout=20)
        diagnostic = rb'"[^\n]+":9:5: error LYC326: Borrowed storage may have been invalidated\. \(byte [0-9]+\)\n'
        if (result.returncode != 1 or result.stdout
                or re.fullmatch(diagnostic, result.stderr) is None):
            raise SystemExit(f'Expected located lifetime rejection: {result}')
        print(result.stderr.decode(), end='', flush=True)
        print('Expected static rejection observed.\n', flush=True)
print('Temporary sources, generated C, and executables removed.')
