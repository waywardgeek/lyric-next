#!/usr/bin/env python3
"""Retained parser diagnostics and bounded resource checks; no solution imports."""
import json
import re
from pathlib import Path
import subprocess
import sys
import tempfile


def run_case(command, directory, name, source, expression=True):
    path = Path(directory) / (name + '.ly')
    path.write_text(source, encoding='utf-8')
    mode = '--inspect-expression' if expression else '--inspect-syntax'
    result = subprocess.run(
        command + [mode, str(path)], capture_output=True, text=True, timeout=30)
    return path, result


def check_error(path, result, source, code, message, offset=None):
    if result.returncode != 1 or result.stdout:
        raise ValueError('Expected exit 1 and empty stdout, not a crash or partial tree')
    filename, end = json.JSONDecoder().raw_decode(result.stderr)
    match = re.fullmatch(
        r':([0-9]+):([0-9]+): error (LYC[0-9]+): (.+) \(byte ([0-9]+)\)\n',
        result.stderr[end:])
    if filename != str(path) or match is None:
        raise ValueError('Malformed or incorrectly attributed diagnostic')
    line, column, actual_code, actual_message, actual_offset = match.groups()
    actual_offset = int(actual_offset)
    data = source.encode('utf-8')
    if not 0 <= actual_offset <= len(data):
        raise ValueError('Diagnostic offset outside source')
    prefix = data[:actual_offset].decode('utf-8').replace('\r\n', '\n')
    expected_line = prefix.count('\n') + 1
    expected_column = len(prefix.rsplit('\n', 1)[-1]) + 1
    if (int(line), int(column), actual_code, actual_message) != (
            expected_line, expected_column, code, message):
        raise ValueError('Incorrect diagnostic kind, message, or location')
    if offset is not None and actual_offset != offset:
        raise ValueError(f'Expected byte {offset}, got {actual_offset}')



def json_tokens(text):
    """Compare deep JSON without recursion; decode strings, ignore whitespace.

    Exact agreement with the independently constructed valid expected token
    sequence checks structure too. No permissive JSON repair is performed.
    """
    position = 0
    tokens = []
    pattern = re.compile(r'\s+|"(?:[^"\\]|\\.)*"|null|[{}\[\],:]')
    while position < len(text):
        match = pattern.match(text, position)
        if match is None:
            raise ValueError('Unexpected JSON token')
        token = match.group()
        position = match.end()
        if token.isspace():
            continue
        if token.startswith('"'):
            tokens.append(('string', json.loads(token)))
        else:
            tokens.append(('syntax', token))
    return tokens


def main():
    command = sys.argv[1:]
    if command[:1] == ['--']:
        command.pop(0)
    if not command:
        raise SystemExit('usage: boundaries.py -- COMMAND ...')
    cases = [
        ('missing-expression', '1 +', True,
         'LYC201', 'Expected an expression.', 3),
        ('missing-close', 'func main() {\n    print("Hello")\n', False,
         'LYC201', "Expected '}'.", 33),
        ('missing-parameter-type', 'func f(x:) {}', False,
         'LYC201', 'Expected an identifier.', 9),
        # These are implementation resource failures, not invalid Lyric syntax.
        # The exact exhaustion position depends on recursive-descent structure.
        ('nested-groups', '(' * 2048 + '1' + ')' * 2048, True,
         'LYC203', 'Parser nesting limit exceeded.', None),
        ('nested-prefix', '-' * 2048 + '1', True,
         'LYC203', 'Parser nesting limit exceeded.', None),
        ('nested-blocks', 'func f() {' + '{' * 2048 + '}' * 2049, False,
         'LYC203', 'Parser nesting limit exceeded.', None),
    ]
    failures = 0
    with tempfile.TemporaryDirectory(prefix='lyric-parser-boundaries-') as directory:
        for name, source, expression, code, message, offset in cases:
            try:
                path, result = run_case(command, directory, name, source, expression)
                check_error(path, result, source, code, message, offset)
            except (ValueError, UnicodeError, subprocess.TimeoutExpired) as error:
                failures += 1
                print(f'FAIL {name}: {error}')
        try:
            # A left-associated chain has deep output but shallow parser recursion.
            # Build the expected JSON text iteratively, without another parser.
            count = 4000
            source = ' + '.join(['1'] * count)
            path, result = run_case(command, directory, 'long-chain', source)
            leaf = '{"kind":"integer","text":"1","children":[]}'
            binary = '{"kind":"binary","text":"+","children":['
            expected = ('{"kind":"expression","text":null,"children":['
                        + binary * (count - 1) + leaf
                        + (',' + leaf + ']}') * (count - 1) + ']}\n')
            if (result.returncode != 0 or result.stderr
                    or json_tokens(result.stdout) != json_tokens(expected)):
                raise ValueError('Long-chain output differs from the complete expected tree')
        except (ValueError, subprocess.TimeoutExpired) as error:
            failures += 1
            print(f'FAIL long-chain: {error}')
    total = len(cases) + 1
    print(f'{total - failures}/{total} parser boundary cases passed')
    return failures != 0


if __name__ == '__main__':
    raise SystemExit(main())
