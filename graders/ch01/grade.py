#!/usr/bin/env python3
"""Public, retained Chapter 1 source-reader grader; Python standard library only.

Supply an independently built command after --. This runner reads official Unicode
fixtures, never compiler source or generated tables. JSON object key order is not
significant; values, record order, and stderr are checked. Every invocation has a
10-second timeout; a timeout or crash is a failure, not an expected rejection.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def load_forbidden():
    """Independent set-based interpretation of the upstream property fixtures."""
    data = ROOT / 'unicode' / '16.0.0'
    forbidden = set()
    for filename, property_name in [
        ('DerivedCoreProperties.txt', 'Default_Ignorable_Code_Point'),
        ('PropList.txt', 'Bidi_Control'),
    ]:
        for raw in (data / filename).read_text().splitlines():
            body = raw.partition('#')[0].strip()
            if not body:
                continue
            interval, name, *_ = [part.strip() for part in body.split(';')]
            if name != property_name:
                continue
            ends = interval.split('..')
            forbidden.update(range(int(ends[0], 16), int(ends[-1], 16) + 1))
    for raw in (data / 'UnicodeData.txt').read_text().splitlines():
        fields = raw.split(';')
        if fields[2] == 'Cc':
            forbidden.add(int(fields[0], 16))
    forbidden.difference_update((9, 10, 13))
    return forbidden


def expected_events(text):
    """Compute positions from Python strings, independently of the UTF-8 decoder."""
    result = []
    offset = 0
    line = 1
    column = 1
    index = 0
    while index < len(text):
        character = text[index]
        width = len(character.encode('utf-8'))
        if character == '\r':
            assert text[index:index + 2] == '\r\n'
            character = '\n'
            width = 2
            index += 1
        result.append(dict(kind='scalar', byteOffset=offset, byteLength=width,
                           line=line, column=column, scalar=f'U+{ord(character):04X}'))
        offset += width
        if character == '\n':
            line += 1
            column = 1
        else:
            column += 1
        index += 1
    result.append(dict(kind='end', byteOffset=offset, line=line, column=column))
    return result


def same_json(actual, expected):
    """Compare JSON values without Python's bool/number equality shortcut."""
    if isinstance(expected, dict):
        return (isinstance(actual, dict) and actual.keys() == expected.keys()
                and all(same_json(actual[key], value) for key, value in expected.items()))
    if isinstance(expected, list):
        return (isinstance(actual, list) and len(actual) == len(expected)
                and all(same_json(a, e) for a, e in zip(actual, expected)))
    if type(expected) is int:
        return type(actual) in (int, float) and actual == expected
    return type(actual) is type(expected) and actual == expected


def check_diagnostic(text, path, suffix):
    """JSON escape spelling is free; the decoded path and suffix are exact."""
    actual_path, end = json.JSONDecoder().raw_decode(text)
    assert isinstance(actual_path, str) and actual_path == str(path), text
    assert not any(ord(c) < 32 or 0x7f <= ord(c) <= 0x9f for c in text[:end]), text
    assert text[end:] == suffix, text


class Grader:
    def __init__(self, command, workspace):
        self.command = command
        self.workspace = workspace
        self.passed = 0
        self.failed = 0

    def invoke(self, arguments):
        return subprocess.run(self.command + arguments, stdout=subprocess.PIPE,
                              stderr=subprocess.PIPE, timeout=10, check=False)

    def case(self, name, action):
        try:
            action()
        except (AssertionError, ValueError, OSError, subprocess.TimeoutExpired) as error:
            self.failed += 1
            print(f'FAIL {name}: {error}')
        else:
            self.passed += 1
            print(f'PASS {name}')

    def positive(self, text):
        path = self.workspace / 'input.ly'
        path.write_bytes(text.encode('utf-8'))
        actual = self.invoke(['--inspect-source', str(path)])
        assert actual.returncode == 0, (actual.returncode, actual.stderr)
        assert actual.stderr == b'', actual.stderr
        rows = [json.loads(row) for row in actual.stdout.decode('utf-8').splitlines()]
        assert same_json(rows, expected_events(text)), f'Incorrect scalar events: {rows[:8]!r}'

    def negative(self, payload, code, message, offset=0, line=1, column=1, filename='input.ly'):
        path = self.workspace / filename
        path.write_bytes(payload)
        actual = self.invoke(['--inspect-source', str(path)])
        assert actual.returncode == 1, (actual.returncode, actual.stderr)
        assert actual.stdout == b'', 'Rejected input emitted partial success output'
        # The path is JSON quoted so even unusual names cannot inject terminal controls.
        suffix = f':{line}:{column}: error {code}: {message} (byte {offset})\n'
        check_diagnostic(actual.stderr.decode('utf-8'), path, suffix)

    def command_case(self, arguments, code, stdout, stderr):
        actual = self.invoke(arguments)
        assert (actual.returncode, actual.stdout, actual.stderr) == (code, stdout, stderr), actual


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command
    if command and command[0] == '--':
        command = command[1:]
    if not command:
        parser.error('Supply the compiler command after --')

    forbidden = load_forbidden()
    with tempfile.TemporaryDirectory(prefix='lyric-ch01-') as directory:
        grader = Grader(command, Path(directory))
        grader.case('version', lambda: grader.command_case(
            ['--unicode-version'], 0, b'16.0.0\n', b''))
        usage = b'Usage: Lyric --inspect-source FILE | --unicode-version\n'
        for number, arguments in enumerate([[], ['--bad'], ['--inspect-source'],
                                            ['--unicode-version', 'extra']]):
            grader.case(f'usage-{number}', lambda a=arguments: grader.command_case(a, 2, b'', usage))
        grader.case('missing-file', lambda: grader.command_case(
            ['--inspect-source', str(Path(directory) / 'absent.ly')], 1, b'',
            b'error LYC004: Cannot read source file.\n'))

        grader.case('empty-path', lambda: grader.command_case(
            ['--inspect-source', ''], 1, b'', b'error LYC004: Cannot read source file.\n'))
        grader.case('diagnostic-unusual-filename', lambda: grader.negative(
            b'\xff', 'LYC001', 'Malformed UTF-8 sequence.', filename='quote"+é.ly'))

        positives = {
            'empty': '',
            'ascii': 'let answer = 42',
            'newlines': 'a\nb\r\nc\n\r\n',
            'tabs-scalars': '\tA\té\t温😀',
            'combining-not-normalized': 'e\u0301',
            'rtl-letters': 'שלום مرحبا',
            'visible-escape-text': '// \\u{202E}\n"\\u{0000}"',
            'bom-not-stripped-from-spelling': '\\u{FEFF}',
            'utf8-width-boundaries': '\u0020\u007e\u00a0\u07ff\u0800\ud7ff\ue000\ufffd\U00010000\U0010ffff',
        }
        # Exercise both sides of every forbidden boundary with accepted scalars.
        neighbors = sorted({c + delta for c in forbidden for delta in (-1, 1)
                            if 0 <= c + delta <= 0x10ffff
                            and c + delta not in forbidden
                            and c + delta not in (13,)
                            and not 0xd800 <= c + delta <= 0xdfff})
        positives['allowed-property-neighbors'] = ''.join(map(chr, neighbors))
        for name, text in positives.items():
            grader.case(name, lambda t=text: grader.positive(t))

        malformed = [b'\x80', b'\xbf', b'\xc0\xaf', b'\xc1\xbf', b'\xc2',
                     b'\xc2A', b'\xe0\x80\x80', b'\xe0\xa0', b'\xe2\x82',
                     b'\xe2A\xac', b'\xed\xa0\x80', b'\xed\xbf\xbf',
                     b'\xf0\x80\x80\x80', b'\xf0\x90\x80', b'\xf4\x90\x80\x80',
                     b'\xf5\x80\x80\x80', b'\xfe', b'\xff']
        prefix = 'é\r\n温\t'.encode('utf-8')
        for number, payload in enumerate(malformed):
            grader.case(f'malformed-{number}', lambda p=payload: grader.negative(
                prefix + p, 'LYC001', 'Malformed UTF-8 sequence.', len(prefix), 2, 3))

        # Test each range endpoint and representative interior; do not infer bans
        # from host Unicode classifications. The official fixtures are authoritative.
        samples = {c for c in forbidden if c - 1 not in forbidden or c + 1 not in forbidden}
        samples.update((0, 0x7f, 0xad, 0x34f, 0x61c, 0x200d, 0x202e, 0xfe0f, 0xfeff, 0xe0100))
        for scalar in sorted(samples):
            grader.case(f'forbidden-{scalar:04X}', lambda c=scalar: grader.negative(
                chr(c).encode('utf-8'), 'LYC002', f'Forbidden source character U+{c:04X} (' +
                ('General_Category=Cc' if c <= 0x9f else 'Default_Ignorable_Code_Point') + ').'))

        for name, before, after in [('comment', '// ', '\n'), ('string', '"', '"'),
                                    ('raw', '"""', '"""'), ('symbol', '`', '`')]:
            payload = (before + '\u202e' + after).encode('utf-8')
            grader.case(f'forbidden-in-{name}', lambda p=payload, n=len(before): grader.negative(
                p, 'LYC002', 'Forbidden source character U+202E (Default_Ignorable_Code_Point).', n, 1, n + 1))
        for name, payload in [('at-end', b'\r'), ('before-text', b'\rA'), ('double', b'\r\r\n')]:
            grader.case(f'lone-cr-{name}', lambda p=payload: grader.negative(
                p, 'LYC003', 'Lone U+000D; use LF or CRLF.'))
        grader.case('earliest-error', lambda: grader.negative(
            b'A\x00\xff', 'LYC002', 'Forbidden source character U+0000 (General_Category=Cc).', 1, 1, 2))
        print(f'\n{grader.passed} passed; {grader.failed} failed')
        return 1 if grader.failed else 0


if __name__ == '__main__':
    sys.exit(main())
