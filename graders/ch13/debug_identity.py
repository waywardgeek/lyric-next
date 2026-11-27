#!/usr/bin/env python3
"""Retained debug object-identity runtime-boundary checks.

Accepts an independent compiler command. The Lyric source is valid; injected C
intentionally violates the trusted-runtime boundary. Invalid probes run only in
runtime debug mode. This does not test raw borrowed-address lifetime detection.
"""
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
import tempfile

SOURCE = '''class Box {
    n:u32
    func Box(self) { self.n=7 }
    pub func read(self)->u32 { return self.n }
    final func finish(self) { let observed=self.n }
}
func main() { let box=Box(); let n=box.read(); box.destroy() }
'''


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


def one(pattern, text):
    matches = re.findall(pattern, text)
    assert len(matches) == 1, (pattern, matches)
    return matches[0]


def main():
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit('supply compiler command')
    passed = 0
    total = 0
    with tempfile.TemporaryDirectory(prefix='lyric-debug-identity-') as directory:
        root = Path(directory)
        source = root / 'case.ly'
        for counted, layout in ((False, []), (False, ['--soa']),
                                (True, []), (True, ['--soa'])):
            fixture = SOURCE
            if counted:
                fixture = fixture.replace('class Box', 'value class Box')
                fixture = fixture.replace('; box.destroy()', '')
            source.write_text(fixture)
            emitted = run(compiler + ['--emit-c', '--debug', *layout, str(source)])
            assert emitted.returncode == 0 and not emitted.stderr, emitted.stderr
            text = emitted.stdout.decode()
            owner = one(r'static uint64_t (\w+)_allocate\(', text)
            read = one(r'static uint32_t (\w+)\(uint64_t \w+\)\n\{', text)
            candidates = re.findall(r'static ly_unit (\w+)\(uint64_t ly_s\d+\)\n\{(.*?)\n\}', text, re.S)
            constructors = [name for name, body in candidates if r'{"\"Box\"",' in body]
            assert len(constructors) == 1, constructors
            constructor = constructors[0]
            prefix = text[:text.index('int main(void)')]
            modes = ['valid', 'freed', 'reused', 'growth', 'null', 'out-of-range', 'stale-destroy']
            if counted:
                modes += ['stale-retain', 'stale-release']
            for mode in modes:
                body = f'''int main(void)
{{
    uint64_t old = {owner}_allocate(__FILE__, __LINE__, 1);
    (void){constructor}(old);
'''
                if mode not in ('valid', 'null', 'out-of-range'):
                    body += f'    (void){owner}_destroy(old, __FILE__, __LINE__, 1);\n'
                if mode in ('reused', 'growth', 'stale-destroy', 'stale-retain', 'stale-release'):
                    # Exactly one freed slot exists; allocation takes it before
                    # fresh storage. The reference must still identify the old object.
                    body += f'''    uint64_t fresh = {owner}_allocate(__FILE__, __LINE__, 1);
    (void){constructor}(fresh);
    if ({read}(fresh) != 7) return 93;
'''
                if mode == 'growth':
                    # Move both tables while retaining a live replacement identity.
                    body += f'''    for (int i = 0; i < 80; i += 1) {{
        uint64_t extra = {owner}_allocate(__FILE__, __LINE__, 1);
        (void){constructor}(extra);
    }}
    old = fresh;
'''
                if mode == 'null':
                    body += '    old = UINT64_MAX;\n'
                if mode == 'out-of-range':
                    body += '    old = UINT64_MAX - 1;\n'
                if mode == 'stale-destroy':
                    body += f'    (void){owner}_destroy(old, "probe", 1, 1);\n'
                if mode == 'stale-retain':
                    body += f'    (void){owner}_retain(old, "probe", 1, 1);\n'
                if mode == 'stale-release':
                    body += f'    {owner}_release(&old, "probe", 1, 1);\n'
                body += f'''    uint32_t result = {read}(old);
    return result == 7 ? 0 : 94;
}}
'''
                c_source = root / 'case.c'
                c_source.write_text(prefix + body)
                for optimization in ('-O0', '-O2'):
                    total += 1
                    label = f'{"counted" if counted else "ordinary"}/{"soa" if layout else "aos"}/{mode}/{optimization}'
                    try:
                        executable = root / 'case'
                        built = run(shlex.split(os.environ.get('CC', 'clang')) +
                                    ['-std=c11', optimization, str(c_source), '-o', str(executable)])
                        assert built.returncode == 0, built.stderr
                        result = run([str(executable)])
                        if mode in ('valid', 'growth'):
                            assert (result.returncode, result.stdout, result.stderr) == (0, b'', b''), result
                        else:
                            expected = (f'"{source}":4:44: runtime error LYR009: invalid object access\n'
                                        '  in "read"\n').encode()
                            if mode == 'stale-destroy':
                                expected = b'probe:1:1: runtime error LYR009: invalid object destruction\n'
                            if mode in ('stale-retain', 'stale-release'):
                                operation = mode.removeprefix('stale-')
                                expected = f'probe:1:1: runtime error LYR009: invalid object {operation}\n'.encode()
                            assert (result.returncode, result.stdout, result.stderr) == (1, b'', expected), result
                        passed += 1
                        print('PASS', label)
                    except (AssertionError, subprocess.TimeoutExpired) as error:
                        print('FAIL', label, error)
    print(f'{passed}/{total} debug object-identity contracts passed')
    return 0 if passed == total else 1


if __name__ == '__main__':
    raise SystemExit(main())
