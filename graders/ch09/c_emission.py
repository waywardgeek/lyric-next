#!/usr/bin/env python3
"""Chapter 9 migration checks: compiler command follows this script.

Run from the repository root. Requires Python 3 and the supported C compiler
(`CC`, default clang). Temporary source/C/binaries are removed after the run.
Positive cases require exit 0, their specified stdout, and empty stderr.
Negative cases require exit 1, empty stdout, their specified runtime diagnostic,
and both source function trace names.
Body-marker checks are chapter-only migration checks; runtime behavior is retained.
"""
import os
import shlex
import subprocess
import sys
import tempfile
from pathlib import Path

CASES = [
    ('identity', 'func identity(x:bool)->bool { return x }; func main() { assert(identity(true)) }', 2, False),
    ('storage', 'func change()->bool { var x:bool=false; x=true; return x }; func main() { assert(change()) }', 2, False),
    ('calls', 'func first()->bool { return true }; func next(x:bool)->bool { return x }; func main() { assert(next(first())) }', 3, False),
    ('arithmetic', 'func calc(x:i32)->i32 { return (x*3-2)/2 }; func main() { assert(calc(8)==11) }', 2, False),
    ('signed-compare', 'func less(a:i8,b:u64)->bool { return a<b }; func main() { assert(less(-1,0)) }', 2, False),
    ('wrapping-cast', 'func wrap(x:i32)->u8 { return x !as u8 }; func main() { assert(wrap(-1)==255) }', 2, False),
    ('shift', 'func shift(x:i8,n:u8)->i8 { return x>>n }; func main() { assert(shift(-8,2)==-2) }', 2, False),
    ('assertion', 'func fail() { assert(false) }; func main() { fail() }', 2, True),
    # Originally a mixed-path ABI check; both bodies now use IR emission.
    ('mixed-body', 'func identity(x:i32)->i32 { return x }; func main() { assert(identity(17)==17) }', 2, False),
    ('condition-break', 'func use()->i32 { var x:i32=0; while if x==2 { break } else { true } { x+=1 }; return x }; func main() { assert(use()==2) }', 2, False),
    ('condition-continue', 'func use()->i32 { var x:i32=0; var bodies:i32=0; while if x<2 { x+=1; continue } else { x<4 } { bodies+=1; x+=1 }; return bodies }; func main() { assert(use()==2) }', 2, False),
    ('condition-return', 'func use()->i32 { while if true { return 9 } else { false } {}; return 0 }; func main() { assert(use()==9) }', 2, False),
    ('recursive-branch', 'func use(n:i32)->i32 { if n==0 { return 1 }; return n*use(n-1) }; func main() { assert(use(5)==120) }', 2, False),
    ('branch-value', 'func use(b:bool)->i32 { return if b { 3 } else { 4 } }; func main() { assert(use(true)==3); assert(use(false)==4) }', 2, False),
    ('short-circuit', 'func fail()->bool { assert(false); return true }; func use(b:bool)->bool { return b && fail() }; func main() { assert(!use(false)) }', 3, False),

    ('array-return', 'func make()->[i32] { return [1,2,3] }; func main() { let a=make(); let b=a; assert(len(a)==3); assert(len(b)==3) }', 2, False),
    ('array-nested', 'func main() { let a=[[1,2],[3]]; let b=a; assert(len(b)==2) }', 1, False),
    ('array-branch', 'func choose(b:bool)->[i32] { return if b { [1,2] } else { [3] } }; func main() { assert(len(choose(true))==2); assert(len(choose(false))==1) }', 2, False),
    ('array-loop', 'func main() { var n:i32=0; while n<4 { let a=[n,n]; assert(len(a)==2); n+=1 } }', 1, False),
    ('array-bytes', 'func main() { let a="abc"; let b=a; assert(len(b)==3); assert(capacity(a)==3) }', 1, False),

    ('class-place', 'class Box { n:i32; func Box(self,n:i32) { self.n=n }; func read(self)->i32 { return self.n } }; func main() { let a=Box(1); a.n=if true { let b=Box(2); b.n+7 } else { 3 }; assert(a.read()==9); let r=ref a.n; r=7; assert(a.n==7); a.destroy() }', 4, False),
    ('class-final-array', 'class Box { a:[i32]; func Box(self) { self.a=[1,2] }; final func finish(self) { assert(self.a[1]==2); print("done") } }; func main() { let a=Box(); a.destroy() }', 4, False),
    ('counted-array-final', 'value class Box { n:i32; func Box(self,n:i32) { self.n=n }; final func finish(self) { print("done") } }; func main() { let a=Box(1); var b=[a]; let c=b; b.resize(0u64); assert(c[0].n==1) }', 4, False),
    ('mutation-capacity', 'func main() { var a=[7]; a.reserve(20u64); assert(capacity(a)>=20); a.truncate(0u64); assert(len(a)==0); assert(capacity(a)>=20); a.resize(0u64); assert(capacity(a)==0); a.resize(2u64); assert(a[0]==0); assert(a[1]==0) }', 1, False),
    ('mutation-nested', 'func main() { var child=[1]; var a:[[i32]]=[]; a.push(child); child[0]=9; a[0].push(2); assert(a[0][0]==1); assert(a[0][1]==2); a.resize(3u64); assert(len(a[1])==0); a.truncate(0u64) }', 1, False),
    ('mutation-argument', 'func main() { var a=[1]; a.push(if true { a.resize(2u64); 3 } else { 4 }); assert(len(a)==3); assert(a[0]==1); assert(a[1]==0); assert(a[2]==3) }', 1, False),
    ('output-borrows', 'func main() { let a="abc"; print(a); println(a); print(a) }', 1, False),
    ('index-copy', 'func main() { var a=[[1,2],[3]]; let b=a; a[0][1]=9; assert(b[0][1]==2); assert(a[0][1]==9) }', 1, False),
    ('ref-forward', 'func inc(n:ref i32) { n+=1 }; func main() { var n=1; let a=ref n; let b=ref a; inc(ref b); assert(n==2) }', 2, False),
    ('ref-array', 'func replace(a:ref [i32]) { a=[8,9] }; func main() { var a=[1]; replace(ref a); assert(a[1]==9) }', 2, False),
    ('ref-return', 'func snapshot(a:ref [[i32]])->[[i32]] { return a }; func main() { var a=[[1]]; let b=snapshot(ref a); a[0][0]=9; assert(b[0][0]==1) }', 2, False),
    ('ref-index-order', 'func change(i:ref i32)->i32 { i=1; return 9 }; func set(a:ref i32,v:i32) { a=v }; func main() { var a=[1,2]; var i=0; set(ref a[i],change(ref i)); assert(a[0]==9); assert(a[1]==2) }', 3, False),
    ('index-bounds', 'func fail(i:i32)->i32 { let a=[1]; return a[i] }; func main() { fail(-1) }', 2, 'LYR006: array index out of bounds'),

]


def run(command):
    return subprocess.run(command, capture_output=True, text=True, timeout=30)


def main():
    command = sys.argv[1:]
    if not command:
        raise SystemExit('usage: c_emission.py COMPILER [ARG ...]')
    passed = 0
    with tempfile.TemporaryDirectory(prefix='lyric-ir-c-') as directory:
        root = Path(directory)
        for name, source, bodies, negative in CASES:
            try:
                lyric = root / (name + '.ly')
                lyric.write_text(source, encoding='utf-8')
                emitted = run(command + ['--emit-c', str(lyric)])
                assert emitted.returncode == 0, emitted.stderr
                assert not emitted.stderr, emitted.stderr
                assert emitted.stdout.count('/* Body emitted from validated structured IR. */') == bodies, 'wrong IR-emitted body count'
                c_source = root / (name + '.c')
                c_source.write_text(emitted.stdout, encoding='utf-8')
                for optimization in ('-O0', '-O2'):
                    executable = root / (name + optimization)
                    built = run(shlex.split(os.environ.get('CC', 'clang')) + ['-std=c11', optimization, str(c_source), '-o', str(executable)])
                    assert built.returncode == 0, built.stderr
                    result = run([str(executable)])
                    expected_stdout = {'output-borrows': 'abcabc\nabc',
                                       'class-final-array': 'done',
                                       'counted-array-final': 'done'}.get(name, '')
                    assert result.stdout == expected_stdout, result.stdout
                    if negative:
                        assert result.returncode == 1, result.returncode
                        diagnostic = 'LYR010: assertion failed' if negative is True else negative
                        assert 'runtime error ' + diagnostic in result.stderr, result.stderr
                        assert 'in "fail"' in result.stderr and 'in "main"' in result.stderr, result.stderr
                    else:
                        assert result.returncode == 0 and result.stderr == '', (result.returncode, result.stderr)
                print('PASS', name)
                passed += 1
            except (AssertionError, subprocess.TimeoutExpired) as error:
                print('FAIL', name, str(error))
    print(f'{passed}/{len(CASES)} IR C-emission cases passed')
    return 0 if passed == len(CASES) else 1


if __name__ == '__main__':
    sys.exit(main())
