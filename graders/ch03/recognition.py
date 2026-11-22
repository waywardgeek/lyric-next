#!/usr/bin/env python3
"""Published syntax expectations, not acceptance inferred from another parser."""
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile


def valid_tree(node):
    return (isinstance(node, dict) and set(node) == {'kind', 'text', 'children'}
            and isinstance(node['kind'], str)
            and (node['text'] is None or isinstance(node['text'], str))
            and isinstance(node['children'], list)
            and all(valid_tree(child) for child in node['children']))


def main():
    command = sys.argv[1:]
    if command[:1] == ['--']:
        command.pop(0)
    if not command:
        raise SystemExit('usage: recognition.py -- COMMAND ...')
    cases = json.loads(Path(__file__).with_name('cases.json').read_text())
    failures = 0
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / 'case.ly'
        for case in cases:
            path.write_text(case['source'], encoding='utf8')
            mode = '--inspect-expression' if case.get('expression') else '--inspect-syntax'
            try:
                run = subprocess.run(command + [mode, str(path)], capture_output=True, text=True, timeout=10)
                if case['accept']:
                    tree = json.loads(run.stdout)
                    passed = (run.returncode == 0 and not run.stderr and valid_tree(tree)
                              and tree['kind'] == ('expression' if case.get('expression') else 'file'))
                else:
                    quoted, end = json.JSONDecoder().raw_decode(run.stderr)
                    diagnostic = re.fullmatch(r':([0-9]+):([0-9]+): error (LYC[0-9]+): ([^\n]+) \(byte ([0-9]+)\)\n', run.stderr[end:])
                    passed = run.returncode == 1 and not run.stdout and quoted == str(path) and diagnostic is not None
                    if diagnostic:
                        line, column, code, message, offset = diagnostic.groups()
                        offset = int(offset)
                        source = case['source'].encode()
                        # Check locations against source bytes, independently of
                        # either parser. A crash or an unrelated diagnostic fails.
                        prefix = source[:offset].decode().replace('\r\n', '\n')
                        expected_code = {'bad-suffix': 'LYC102', 'decomposed-identifier': 'LYC101'}.get(case['id'], 'LYC201')
                        passed = (passed and code == expected_code and 0 <= offset <= len(source)
                                  and int(line) == prefix.count('\n') + 1
                                  and int(column) == len(prefix.rsplit('\n', 1)[-1]) + 1
                                  and ('errorByte' not in case or offset == case['errorByte']))
                if not passed:
                    raise ValueError(f'exit={run.returncode}, stdout={run.stdout[:160]!r}, stderr={run.stderr!r}')
            except (ValueError, UnicodeError, subprocess.TimeoutExpired) as error:
                failures += 1
                print(f"FAIL {case['id']}: {error}")
    print(f'{len(cases)-failures}/{len(cases)} syntax recognition cases passed')
    return failures != 0


if __name__ == '__main__':
    raise SystemExit(main())
