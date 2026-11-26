"""Chapter 11 walkthrough: inferred generics and unevaluated type selection.

Run from the repository root after a Release build. Uses installed local tools
and disposable files; no installation, network access, or persistent artifacts.
"""
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

compiler = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
cc = shlex.split(os.environ.get('CC', 'cc'))
source_text = r'''func identity<T>(value:T)->T {
    return value
}

func describe<T>(value:T) {
    matchtype<U> value {
        (U,) => { println("singleton tuple") }
        [U] => { println("array") }
        _ => { println("other type") }
    }
}

func noisy()->i32 {
    println("ERROR: type query executed")
    return 99
}

func main() {
    var original=[3]
    var copied=identity(original)
    copied[0]=9
    assert(original[0]==3)
    assert(copied[0]==9)
    println("generic array copy stays independent")

    let number:typeof(noisy())=7
    assert(number==7)
    println("type query did not execute")
    describe((true,))
    describe(original)
    describe(number)
}
'''
expected = (
    b'generic array copy stays independent\n'
    b'type query did not execute\n'
    b'singleton tuple\n'
    b'array\n'
    b'other type\n'
)


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


# Keep generated files disposable even when compilation or execution fails.
with tempfile.TemporaryDirectory(prefix='lyric-spin11-') as directory:
    folder = Path(directory)
    source = folder / 'generics.ly'
    c_source = folder / 'generics.c'
    binary = folder / 'generics'
    source.write_text(source_text, encoding='utf-8')
    print('=== Source ===', flush=True)
    print(source_text, flush=True)

    emitted = run(compiler + ['--emit-c', str(source)])
    if emitted.returncode != 0 or emitted.stderr or not emitted.stdout:
        raise SystemExit(f'Emission failed: {emitted}')
    c_source.write_bytes(emitted.stdout)
    built = run(cc + ['-std=c11', '-O2', str(c_source), '-o', str(binary)])
    if built.returncode != 0:
        raise SystemExit(f'C compilation failed: {built.stderr!r}')

    # Successful assertions alone are insufficient: check the process contract too.
    result = run([str(binary)])
    if (result.returncode, result.stdout, result.stderr) != (0, expected, b''):
        raise SystemExit(f'Unexpected execution: {result}')
    print('=== Executed C ===', flush=True)
    print(result.stdout.decode(), end='', flush=True)
print('Temporary source, C, and executable removed.', flush=True)
