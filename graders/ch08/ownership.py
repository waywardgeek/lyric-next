#!/usr/bin/env python3
"""Retained owned-field cleanup check; trailing arguments are the compiler command.

One and 257 cycles must leave the same positive outstanding-block count in each
layout at O0/O2. Class backing remains allocated; payload leakage must not grow.
This is bounded allocation-balance evidence, not universal leak freedom.
"""
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

CLASS = '''class Counter {
 a:[[i32]]; text:string
 func Counter(self,n:i32) { self.a=[[n,n]]; self.text="payload" }
 func replace(self) { self.a=self.a; self.a=[[7]]; self.text="replacement" }
 final func finish(self) { let snapshot=self.a; let text=self.text }
}
'''

PREFIX = r'''
#include <stdlib.h>
#include <stdio.h>
static unsigned long outstanding;
static void *counted_malloc(size_t bytes)
{
    void *result = malloc(bytes);
    if (result != NULL) { outstanding += 1; }
    return result;
}
static void *counted_realloc(void *old, size_t bytes)
{
    /* Generated allocation requests are positive; zero-size realloc has
       different ownership rules and is deliberately refused by this probe. */
    if (bytes == 0) { abort(); }
    int creates = old == NULL;
    void *result = realloc(old, bytes);
    if (creates && result != NULL) { outstanding += 1; }
    return result;
}
static void counted_free(void *value)
{
    if (value != NULL)
    {
        if (outstanding == 0) { abort(); }
        outstanding -= 1;
    }
    free(value);
}
#define free counted_free
#define malloc counted_malloc
#define realloc counted_realloc
#define main lyric_program_main
'''
SUFFIX = r'''
#undef main
int main(void)
{
    int result = lyric_program_main();
    fprintf(stderr, "%lu\n", outstanding);
    return result;
}
'''


def run(command):
    return subprocess.run(command, capture_output=True, timeout=60)


def main():
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit('Supply the compiler command after ownership.py')
    with tempfile.TemporaryDirectory(prefix='lyric-field-ownership-') as temporary:
        root = Path(temporary)
        for policy in ('ordinary', 'counted', 'counted-array'):
            declaration = CLASS if policy == 'ordinary' else 'value ' + CLASS
            destruction = 'c.destroy();' if policy == 'ordinary' else ''
            array_work = ''
            if policy == 'counted-array':
                array_work = ('var a=[[c]]; let b=a; a[0].push(c); '
                              'a[0][0]=c; a.resize(0u64); let snapshot=b[0][0].a;')
            for layout in ('aos', 'soa'):
                for optimization in ('-O0', '-O2'):
                    counts = []
                    for cycles in (1, 257):
                        source = root / 'input.ly'
                        source.write_text(declaration + f'''func main() {{
 var i = 0
 while i < {cycles} {{ let c = Counter(i); c.replace(); {array_work} {destruction} i = i + 1 }}
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
                    assert counts[0] > 0 and counts[0] == counts[1], (policy, layout, optimization, counts)
                    print('PASS', policy, layout, optimization, 'outstanding blocks', counts)
    print('12/12 owned-field/array balance comparisons passed')


if __name__ == '__main__':
    main()
