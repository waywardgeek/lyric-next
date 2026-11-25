"""Chapter 10 walkthrough: independent values, copied aliases, and tuple defaults.

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
source_text = r'''struct Point { x:i32; y:i32=4 }
struct Borrowed { target:ref i32 }
func main() {
    var point=Point { x:3 }
    let saved=point
    point.x=8
    assert(saved.x==3)
    assert(saved.y==4)
    assert(point.x==8)
    print("value copy stays independent\n")

    var number=7
    let holder=Borrowed { target:ref number }
    let copied=holder
    copied.target=9
    assert(number==9)
    assert(holder.target==9)
    print("alias copy reaches the same storage\n")

    var pairs:[(i32,i32)]=[]
    pairs.resize(2)
    let (first,second)=pairs[1]
    assert(first==0)
    assert(second==0)
    print("tuple growth constructs defaults\n")
}
'''
expected = (
    b'value copy stays independent\n'
    b'alias copy reaches the same storage\n'
    b'tuple growth constructs defaults\n'
)


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


# Keep generated files disposable even when compilation or execution fails.
with tempfile.TemporaryDirectory(prefix='lyric-spin10-') as directory:
    folder = Path(directory)
    source = folder / 'composites.ly'
    c_source = folder / 'composites.c'
    binary = folder / 'composites'
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
