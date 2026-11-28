#!/usr/bin/env python3
"""Chapter14 successor: retained free-slot reuse and layout growth contracts.

Preserves the Chapter8 workloads and comparisons after metadata migration.
Trailing arguments are the compiler command.

Compile real emitted C with counted malloc/realloc wrappers. One cycle and 257
cycles must allocate equally often, in both layouts at O0/O2. This measures
allocation calls, not leak freedom or peak memory. Requires CC (default cc).
"""
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch08'))
from layout import CLASS

PREFIX = r'''
#include <stdlib.h>
#include <stdio.h>
static unsigned long allocation_calls;
static void *counted_malloc(size_t bytes)
{
    allocation_calls += 1;
    return malloc(bytes);
}
static void *counted_realloc(void *old, size_t bytes)
{
    allocation_calls += 1;
    return realloc(old, bytes);
}
#define malloc counted_malloc
#define realloc counted_realloc
#define main lyric_program_main
'''
SUFFIX = r'''
#undef main
int main(void)
{
    int result = lyric_program_main();
    fprintf(stderr, "%lu\n", allocation_calls);
    return result;
}
'''


def run(command):
    return subprocess.run(command, capture_output=True, timeout=60)


def main():
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit('Supply the compiler command after allocation.py')
    with tempfile.TemporaryDirectory(prefix='lyric-reuse-') as temporary:
        root = Path(temporary)
        for layout in ('aos', 'soa'):
            for optimization in ('-O0', '-O2'):
                counts = []
                for cycles in (1, 257):
                    source = root / 'input.ly'
                    source.write_text(CLASS + f'''func main() {{
 var i = 0
 while i < {cycles} {{ let c = Counter(i); c.add(1); c.destroy(); i = i + 1 }}
}}
''')
                    options = ['--emit-c'] + (['--soa'] if layout == 'soa' else [])
                    emitted = run(compiler + options + [str(source)])
                    assert (emitted.returncode, emitted.stderr) == (0, b''), emitted
                    cfile = root / 'input.c'
                    cfile.write_text(PREFIX + emitted.stdout.decode() + SUFFIX)
                    executable = root / 'program'
                    built = run(shlex.split(os.environ.get('CC', 'cc')) +
                                ['-std=c11', optimization, str(cfile), '-o', str(executable)])
                    assert built.returncode == 0, built.stderr
                    result = run([str(executable)])
                    assert result.returncode == 0 and result.stdout == b'', result
                    assert result.stderr.strip().isdigit(), result.stderr
                    counts.append(int(result.stderr))
                assert counts[0] > 0 and counts[0] == counts[1], (layout, optimization, counts)
                print('PASS', layout, optimization, 'allocation calls', counts)
                # Keep all objects live: growth must follow class doubling, not
                # ordinary-array 50% growth. No source-level array adds allocations.
                source.write_text(CLASS + """func main() {
 var i = 0
 while i < 257 { let c = Counter(i); assert(c.read() == i); i = i + 1 }
}
""")
                emitted = run(compiler + options + [str(source)])
                assert (emitted.returncode, emitted.stderr) == (0, b''), emitted
                cfile.write_text(PREFIX + emitted.stdout.decode() + SUFFIX)
                built = run(shlex.split(os.environ.get('CC', 'cc')) +
                            ['-std=c11', optimization, str(cfile), '-o', str(executable)])
                assert built.returncode == 0, built.stderr
                result = run([str(executable)])
                # Ordinary Counter has no reference-count array. SoA grows its
                # next-free array and one field array nine times each (capacities
                # 2 through 512: the first request includes reserved slot zero). AoS embeds metadata
                # in records: only four directory growths and five chunks remain.
                expected = 18 if layout == 'soa' else 9
                assert (result.returncode, result.stdout, result.stderr) == (
                    0, b'', f'{expected}\n'.encode()), (layout, optimization, result)
                print('PASS', layout, optimization, 'class growth calls', expected)
    print('4/4 free-slot allocation and 4/4 class growth comparisons passed')


if __name__ == '__main__':
    main()
