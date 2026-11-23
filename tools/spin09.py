"""Chapter 9 walkthrough: inspect explicit ownership, then execute emitted C.

Run from the repository root after a Release build. Uses installed local tools
and disposable files; no installation, network access, or persistent artifacts.
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
source_text = '''func make() -> [i32] { return [3, 7] }
func main() {
    var first = make()
    let second = first
    first[0] = 9
    assert(second[0] == 3)
    println("Independent copy survived")
}
'''


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)



def ownership_sequence(function):
    # Arena order is allocation order, not execution order. Follow body edges.
    nodes = {node['id']: node for node in function['nodes']}
    body = nodes[function['body']]
    interesting = {'NewArray', 'Copy', 'Move', 'FreeArray', 'Return'}
    sequence = [nodes[node_id] for node_id in body['children']
                if nodes[node_id]['kind'] in interesting]
    kinds = [node['kind'] for node in sequence]
    if kinds != ['NewArray', 'Move', 'FreeArray', 'Return']:
        raise SystemExit(f'Unexpected helper ownership execution: {kinds}')
    allocated, moved, freed, returned = sequence
    if (moved['operands'] != [allocated['id']]
            or freed['operands'] != [allocated['id']]
            or returned['operands'] != [moved['id']]):
        raise SystemExit('Return transfer and empty-holder cleanup relationships differ.')
    return kinds


with tempfile.TemporaryDirectory(prefix='lyric-ir-') as directory:
    folder = Path(directory)
    source = folder / 'example.ly'
    c_source = folder / 'example.c'
    binary = folder / 'example'
    source.write_text(source_text, encoding='utf-8')
    print('=== Source ===', flush=True)
    print(source_text, flush=True)
    inspected = run(compiler + ['--inspect-ir', str(source)])
    if inspected.returncode != 0 or inspected.stderr:
        raise SystemExit(f'Inspection failed: {inspected}')
    document = json.loads(inspected.stdout)
    print('=== Ownership operations in make ===', flush=True)
    make = next(function for function in document['functions']
                if function['name'] == 'make')
    kinds = ownership_sequence(make)
    # A serialization permutation must not change the displayed execution order.
    reordered = dict(make, nodes=list(reversed(make['nodes'])))
    if ownership_sequence(reordered) != kinds:
        raise SystemExit('Walkthrough incorrectly depends on arena order.')
    print(' -> '.join(kinds), flush=True)
    print('Move empties its holder; later empty-holder cleanup is harmless.', flush=True)

    emitted = run(compiler + ['--emit-c', str(source)])
    if emitted.returncode != 0 or emitted.stderr or not emitted.stdout:
        raise SystemExit(f'Emission failed: {emitted}')
    c_source.write_bytes(emitted.stdout)
    built = run(cc + ['-std=c11', '-O2', str(c_source), '-o', str(binary)])
    if built.returncode != 0:
        raise SystemExit(f'C compilation failed: {built.stderr!r}')
    result = run([str(binary)])
    expected = b'Independent copy survived\n'
    if (result.returncode, result.stdout, result.stderr) != (0, expected, b''):
        raise SystemExit(f'Unexpected execution: {result}')
    print('=== Executed C ===', flush=True)
    print(result.stdout.decode(), end='', flush=True)

    source.write_text('func main() { missing() }\n', encoding='utf-8')
    rejected = run(compiler + ['--inspect-ir', str(source)])
    if (rejected.returncode != 1 or rejected.stdout
            or b'LYC301' not in rejected.stderr):
        raise SystemExit(f'Unexpected rejection: {rejected}')
    print('=== Invalid source: diagnostic, no partial JSON ===', flush=True)
    print(rejected.stderr.decode(), end='', flush=True)
print('Temporary source, C, and executable removed.', flush=True)
