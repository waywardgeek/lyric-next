"""Supplementary declaration-stage checks; not the completed chapter grader."""
import pathlib
import subprocess
import sys
import tempfile

cases = [
    ('constructor-result', 'class C { func C(self)->i32 { return 1 } }\nfunc main() {}', 'LYC327'),
    ('missing-receiver', 'class C { func C() {} }\nfunc main() {}', 'LYC306'),
    ('late-receiver', 'class C { func C(n:i32,self) {} }\nfunc main() {}', 'LYC306'),
    ('empty-constructor', 'class C { func C(self) {} }\nfunc main() {}', None),
    ('nominal-parameter', 'class C { func C(self) {} }\nfunc take(x:C) {}\nfunc main() {}', None),
    ('forward-nominal', 'func take(x:C) {}\nclass C { func C(self) {} }\nfunc main() {}', None),
    ('duplicate-field', 'class C { x:i32; x:i32 }\nfunc main() {}', "LYC302: Name 'x' is already visible."),
    ('bare-field', 'class C { x:i32; func read(self)->i32 { return x } }\nfunc main() {}', "LYC301"),
    ('body-not-skipped', 'class C { func C(self) { missing() } }\nfunc main() {}', 'LYC301'),
    ('object-field-deferred', 'class C { other:C }\nfunc main() {}', 'LYC306'),
]
with tempfile.TemporaryDirectory() as directory:
    path = pathlib.Path(directory) / 'probe.ly'
    for name, source, diagnostic in cases:
        path.write_text(source)
        result = subprocess.run(sys.argv[1:] + ['--check', str(path)], capture_output=True, timeout=20)
        if diagnostic is None:
            assert (result.returncode, result.stdout, result.stderr) == (0,b'',b''), (name,result)
        else:
            assert result.returncode == 1 and not result.stdout and diagnostic.encode() in result.stderr, (name,result)
        print('PASS', name)
    path.write_text(next(source for name, source, _ in cases if name == 'empty-constructor'))
    result = subprocess.run(sys.argv[1:] + ['--emit-c', str(path)], capture_output=True, timeout=20)
    assert result.returncode == 1 and not result.stdout and b'Class storage emission is not implemented yet.' in result.stderr, result
    print('PASS explicit-emission-boundary')
print('PASS 11 declaration-stage checks')
