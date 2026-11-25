#!/usr/bin/env python3
"""Retained Chapter 10 struct declaration/layout checks.

Run from the repository root: python3 graders/ch10/structs.py COMPILER [ARG ...].
These check-library cases exercise source admission, not aggregate execution.
Success requires exit 0 and empty stdout/stderr. Rejection requires exit 1,
empty stdout, and exactly one located diagnostic with the specified code.
No normalization, compiler imports, network, or external packages are used.
"""
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile


# Stable identifiers; every case is retained for later cumulative executions.
CASES = [
    ('struct-empty', 'struct Empty {}', None),
    ('struct-scalars', 'struct Point { x:i32; y:i32 }', None),
    ('struct-forward', 'struct Outer { inner:Inner }\nstruct Inner { n:i32 }', None),
    ('struct-sharing', 'struct Pair { a:Leaf; b:Leaf }\nstruct Leaf { n:i32 }', None),
    ('struct-array-recursion', 'struct Tree { children:[Tree] }', None),
    ('struct-mutual-array-recursion', 'struct A { b:B }\nstruct B { a:[A] }', None),
    ('struct-tuple-array-recursion', 'struct Tree { data:(i32,[Tree]) }', None),
    ('struct-class-handle', 'class Object { func Object(self) {} }\nstruct Holder { object:Object }', None),
    ('struct-direct-cycle', 'struct Bad { selfValue:Bad }', 'LYC341'),
    ('struct-mutual-cycle', 'struct A { b:B }\nstruct B { a:A }', 'LYC341'),
    ('struct-tuple-cycle', 'struct Bad { wrapped:(i32,Bad) }', 'LYC341'),
    ('struct-cycle-behind-array', 'struct Root { children:[Bad] }\nstruct Bad { value:Bad }', 'LYC341'),
    ('struct-duplicate-field', 'struct Bad { x:i32; x:i32 }', 'LYC302'),
    ('struct-duplicate-name', 'struct Same {}\nstruct Same {}', 'LYC302'),
    ('struct-value-method-body', 'struct P { x:i32; func get(self)->i32 { return self.x } }', None),
    ('struct-ref-method-body', 'struct P { x:i32; func set(self:ref P,n:i32) { self.x=n } }', None),
    ('struct-static-method-body', 'struct P { x:i32; func origin()->P { return P { x:0 } } }', None),
    ('struct-method-wrong-receiver', 'struct Q {}; struct P { func get(self:ref Q) {} }', 'LYC304'),
    ('struct-method-duplicate', 'struct P { func get(self) {}; func get(self) {} }', 'LYC302'),
    ('struct-method-body-type', 'struct P { func get(self)->i32 { return false } }', 'LYC304'),
]


def main():
    command = sys.argv[1:]
    if not command:
        raise SystemExit('usage: structs.py COMPILER [ARG ...]')
    failures = 0
    with tempfile.TemporaryDirectory() as directory:
        for identifier, source, code in CASES:
            path = Path(directory) / (identifier + '.ly')
            path.write_text(source, encoding='utf-8')
            try:
                result = subprocess.run(
                    command + ['--check-library', str(path)],
                    capture_output=True, timeout=20)
                if code is None:
                    passed = (result.returncode == 0
                              and result.stdout == b'' and result.stderr == b'')
                else:
                    error = result.stderr.decode('utf-8')
                    filename, end = json.JSONDecoder().raw_decode(error)
                    match = re.fullmatch(
                        r':([0-9]+):([0-9]+): error ' + code
                        + r': ([^\n]+) \(byte ([0-9]+)\)\n', error[end:])
                    passed = (result.returncode == 1 and result.stdout == b''
                              and filename == str(path) and match is not None)
                    if match is not None:
                        line, column, message, offset = match.groups()
                        offset = int(offset)
                        prefix = source.encode('utf-8')[:offset].decode('utf-8')
                        passed = (passed and 0 <= offset < len(source.encode('utf-8'))
                                  and int(line) == prefix.count('\n') + 1
                                  and int(column) == len(prefix.rsplit('\n', 1)[-1]) + 1)
                if not passed:
                    raise ValueError(
                        f'exit={result.returncode}, stdout={result.stdout!r}, stderr={result.stderr!r}')
            except (ValueError, UnicodeError, subprocess.TimeoutExpired) as error:
                failures += 1
                print(f'FAIL {identifier}: {error}')
    print(f'{len(CASES) - failures}/{len(CASES)} struct declaration/layout checks passed')
    return int(failures != 0)


if __name__ == '__main__':
    raise SystemExit(main())
