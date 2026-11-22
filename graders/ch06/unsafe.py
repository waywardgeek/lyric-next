"""Retained unsafe-mode checks. Usage: python3 unsafe.py --chapter=6|7|8 DOTNET ASSEMBLY.

Valid programs must emit C and run with exit 0, stdout b'ok', empty stderr at
-O0/-O2. Unsafe exceptional inputs are emission-only: no runtime result is
required for undefined behavior. CC selects the C compiler as in run.py.
"""
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

arguments = sys.argv[1:]
chapter = 6
if arguments and arguments[0].startswith('--chapter='):
    chapter = int(arguments.pop(0).split('=', 1)[1])
if chapter not in (6, 7, 8) or not arguments:
    raise SystemExit('Usage: unsafe.py --chapter=6|7|8 COMPILER [ARG ...]')
compiler = arguments
layouts = ([], ['--soa']) if chapter >= 8 else ([],)
cc = shlex.split(os.environ.get('CC', 'cc'))
valid = [
    ('arithmetic', 'func calc(a:i32,b:i32)->i32 { var n=a+b; n*=b; n-=a; n/=b; return -n }; func main() { if calc(6,3)==-7 { print("ok") } }'),
    ('shifts', 'func shift(a:i32,n:i32)->i32 { return (a<<n)>>n }; func main() { if shift(-8,2)==-8 { print("ok") } }'),
    ('sign-bit', 'func shift(a:i32)->i32 { return a<<31 }; func main() { if shift(1)==-2147483648i32 { print("ok") } }'),
    ('remainder-min', 'func rem(a:i64,b:i64)->i64 { return a%b }; func main() { if rem(-9223372036854775808i64,-1i64)==0i64 { print("ok") } }'),
    ('wrapping', 'func calc(a:i8)->i8 { return a !+ 1i8 }; func main() { if calc(127i8)==-128i8 { print("ok") } }'),
    ('cast', 'func cast(a:i64)->i8 { return a as i8 }; func main() { if cast(-7i64)==-7i8 { print("ok") } }'),
    ('wrapping-cast', 'func cast(a:i64)->i8 { return a !as i8 }; func main() { if cast(255i64)==-1i8 { print("ok") } }'),
    ('mixed-compare', 'func cmp(a:i64,b:u64)->bool { return a<b }; func main() { if cmp(-1i64,1u64) { print("ok") } }'),
    ('unsigned-promotion', 'func mul(a:u16,b:u16)->u16 { return a*b }; func main() { if mul(255u16,255u16)==65025u16 { print("ok") } }'),
    ('array', 'func main() { var a=[1,2]; a[0]=3; if a[0]==3 { print("ok") } }'),
]
exceptional = [
    ('overflow', 'let n=127i8+1i8', 'LYC311'),
    ('zero-div', 'let n=1/0', 'LYC316'),
    ('zero-rem', 'let n=1%0', 'LYC316'),
    ('shift-count', 'let n=1<<32', 'LYC317'),
    ('cast-range', 'let n=300 as u8', 'LYC318'),
    ('computed-negation', 'let n=-(-128i8)', 'LYC311'),
]
if chapter < 7:
    valid = [case for case in valid if case[0] != 'array']
for width in (8, 16, 32, 64):
    for signed in (False, True):
        kind = ('i' if signed else 'u') + str(width)
        value = '-1' if signed else str((1 << width) - 1)
        expected = '-1' if signed else '1'
        source = (
            f'func right(a:{kind},n:u64)->{kind} {{ return a>>n }}; '
            f'func left(a:{kind},n:u64)->{kind} {{ return a<<n }}; '
            f'func main() {{ if right({value}{kind},{width-1}u64)=={expected}{kind} '
            f'&& left({value}{kind},0u64)=={value}{kind} {{ print("ok") }} }}'
        )
        valid.append((f'shift-width-{kind}', source))
passed = 0
with tempfile.TemporaryDirectory(prefix='lyric-unsafe-') as directory:
    folder = Path(directory)
    source = folder / 'case.ly'
    c = folder / 'case.c'
    binary = folder / 'case'
    for label, text in valid:
        source.write_text(text)
        for layout in layouts:
            result = subprocess.run(compiler + ['--emit-c', '--unsafe'] + layout + [str(source)], capture_output=True, timeout=30)
            assert result.returncode == 0 and not result.stderr, (label, result)
            c.write_bytes(result.stdout)
            assert b'/* UNSAFE:' in result.stdout
            if label == 'array':
                assert b'array index out of bounds' not in result.stdout
                safe = subprocess.run(compiler + ['--emit-c'] + layout + [str(source)], capture_output=True, timeout=30)
                assert safe.returncode == 0 and b'array index out of bounds' in safe.stdout
            # Helper identifiers and unused-definition emission are not public
            # contracts. Source review verifies inlining and unchecked lowering;
            # behavioral checks below do not claim to prove either property.
            for optimization in ['-O0', '-O2']:
                subprocess.run(cc + ['-std=c11', optimization, str(c), '-o', str(binary)], check=True, capture_output=True, timeout=30)
                execution = subprocess.run([str(binary)], capture_output=True, timeout=30)
                assert (execution.returncode, execution.stdout, execution.stderr) == (0, b'ok', b''), (label, execution)
                passed += 1
        print('PASS', label)
    source.write_text('func main() { assert(false) }')
    for layout in layouts:
        result = subprocess.run(compiler + ['--emit-c', '--unsafe'] + layout + [str(source)], capture_output=True, timeout=30)
        assert result.returncode == 0 and not result.stderr, result
        c.write_bytes(result.stdout)
        for optimization in ['-O0', '-O2']:
            subprocess.run(cc + ['-std=c11', '-DNDEBUG', optimization, str(c), '-o', str(binary)], check=True, capture_output=True, timeout=30)
            execution = subprocess.run([str(binary)], capture_output=True, timeout=30)
            import json
            expected = (json.dumps(str(source)) + ':1:15: runtime error LYR010: assertion failed\n  in "main"\n').encode()
            assert (execution.returncode, execution.stdout, execution.stderr) == (1, b'', expected), execution
            passed += 1
    print('PASS assertion retained under unsafe and NDEBUG')
    for label, statement, diagnostic in exceptional:
        source.write_text('func main() { '+statement+' }')
        safe = subprocess.run(compiler + ['--emit-c', str(source)], capture_output=True, timeout=30)
        assert safe.returncode == 1 and not safe.stdout and diagnostic.encode() in safe.stderr, (label, safe)
        unsafe = subprocess.run(compiler + ['--emit-c', '--unsafe', str(source)], capture_output=True, timeout=30)
        assert unsafe.returncode == 0 and unsafe.stdout and not unsafe.stderr, (label, unsafe)
        passed += 1
        print('PASS emission-only', label)
    source.write_text('func main() { println(true) }')
    rejected = subprocess.run(compiler + ['--emit-c', '--unsafe', str(source)], capture_output=True, timeout=30)
    assert rejected.returncode == 1 and not rejected.stdout and b'LYC304' in rejected.stderr, rejected
    passed += 1
    print('PASS type checking retained')
    if chapter >= 7:
        source.write_text('func main() { var a=[1]; let r=ref a[0]; a.push(2); r=3 }')
        rejected = subprocess.run(compiler + ['--emit-c', '--unsafe', str(source)], capture_output=True, timeout=30)
        assert rejected.returncode == 1 and not rejected.stdout and b'LYC326' in rejected.stderr, rejected
        passed += 1
        print('PASS borrow checking retained')
    for options in [['--unsafe', '--unsafe'], ['--soa', '--soa'], ['--unknown']]:
        rejected = subprocess.run(compiler + ['--emit-c'] + options + [str(source)], capture_output=True, timeout=30)
        assert rejected.returncode == 2 and not rejected.stdout, rejected
        passed += 1
    print('PASS unknown and duplicate options rejected')
print(f'{passed}/{(len(valid)+1)*len(layouts)*2+len(exceptional)+4+(1 if chapter >= 7 else 0)} unsafe checks passed')
