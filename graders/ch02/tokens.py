#!/usr/bin/env python3
"""Retained token cases, supplied executable after --. No solution imports."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile

# Reuse the retained JSON type-aware comparator and quoted diagnostic parser.
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch01'))
from grade import same_json


def records(source, pieces):
    """Locate explicitly specified tokens; this helper does not classify source."""
    result = []
    cursor = 0
    for kind, text, suffix, value in pieces:
        index = source.index(text, cursor)
        prefix = source[:index].replace('\r\n', '\n')
        result.append(dict(kind=kind, text=text,
                           byteOffset=len(source[:index].encode()), byteLength=len(text.encode()),
                           line=prefix.count('\n') + 1,
                           column=len(prefix.rsplit('\n', 1)[-1]) + 1, suffix=suffix, value=value))
        cursor = index + len(text)
    prefix = source.replace('\r\n', '\n')
    result.append(dict(kind='end', text='', byteOffset=len(source.encode()), byteLength=0,
                       line=prefix.count('\n') + 1,
                       column=len(prefix.rsplit('\n', 1)[-1]) + 1, suffix=None, value=None))
    return result


def piece(kind, text, suffix=None, value=None):
    return (kind, text, suffix, value)


def main():
    command = sys.argv[1:]
    if command[:1] == ['--']:
        command.pop(0)
    if not command:
        raise SystemExit('usage: tokens.py -- COMMAND ...')
    cases = []
    def good(name, text, pieces):
        cases.append((name, text, records(text, pieces), None))
    def bad(name, text, code, message, at=0):
        prefix = text[:at].replace('\r\n', '\n')
        location = (prefix.count('\n') + 1, len(prefix.rsplit('\n', 1)[-1]) + 1,
                    len(text[:at].encode()))
        cases.append((name, text, None, (code, message, location)))

    good('empty', '', [])
    names = 'café 温度 Δtime _ _a value final safe field gen fn let letπ'
    good('identifiers', names, [piece('discard' if n == '_' else 'keyword' if n == 'let'
                                    else 'identifier', n) for n in names.split()])
    keywords = ('func class struct enum interface relation destructor import impl as is type where '
                'owns refs ref self from true false null pub let var if else for in while match '
                'return break continue yield extends').split()
    good('hard-keywords', ' '.join(keywords), [piece('keyword', x) for x in keywords])
    numbers = '0 1_000 1.25 123u16 -128i8 123f32 1_2.3_4f64 999999999999999999999999999999'
    good('numbers', numbers, [piece('integer', '0'), piece('integer', '1_000'),
         piece('float', '1.25'), piece('integer', '123u16', 'u16'), piece('punctuation', '-'),
         piece('integer', '128i8', 'i8'), piece('float', '123f32', 'f32'),
         piece('float', '1_2.3_4f64', 'f64'), piece('integer', '999999999999999999999999999999')])
    suffixes = 'u8 u16 u32 u64 i8 i16 i32 i64 f32 f64'.split()
    good('all-suffixes', ' '.join('42'+s for s in suffixes),
         [piece('float' if s.startswith('f') else 'integer', '42'+s, s) for s in suffixes])
    good('tuple-selectors', 'nested.0.1 1.25 1.', [piece('identifier','nested'),
         piece('punctuation','.'), piece('tuple-index','0'), piece('punctuation','.'),
         piece('tuple-index','1'), piece('float','1.25'), piece('integer','1'), piece('punctuation','.')])
    operators = '!+= !-= !*= <-> !as -> => == != <= >= << >> && || += -= *= /= !+ !- !* ( ) [ ] { } . , : ; ? ! + - * / % = < > & | ^'.split()
    good('operators', ' '.join(operators), [piece('punctuation', x) for x in operators])
    good('word-boundary', '!assert !asπ !as u32', [piece('punctuation','!'),
         piece('identifier','assert'),piece('punctuation','!'),piece('identifier','asπ'),
         piece('punctuation','!as'),piece('identifier','u32')])
    good('comments', '// café\r\nlet x //end', [piece('newline','\r\n'),piece('keyword','let'),piece('identifier','x')])
    good('nested-newlines', '(\n{\nx\n}\n)\n[\ny\n]', [piece('punctuation','('),
         piece('punctuation','{'),piece('newline','\n'),piece('identifier','x'),piece('newline','\n'),
         piece('punctuation','}'),piece('punctuation',')'),piece('newline','\n'),piece('punctuation','['),
         piece('identifier','y'),piece('punctuation',']')])
    for text in ['123u16name','123u128','123abc','123_u16','1.5u16','1__2','1_',
                 '1.2_','0x12','1e4','42U16','42é','42_','a.0u8','a.0_1']:
        at = 2 if text.startswith('a.') else 0
        bad('bad-number-'+text, text, 'LYC102', 'Malformed numeric literal.', at)
    bad('non-nfc', 'let cafe\u0301', 'LYC101', 'Identifier is not NFC; use "caf\\u00E9".', 4)
    bad('unknown', 'let x = @', 'LYC103', 'Unexpected source character U+0040.', 8)
    bad('source-first', '//\u202e', 'LYC002', 'Forbidden source character U+202E (Default_Ignorable_Code_Point).', 2)

    # Literal values are explicitly authored, not derived through a lexer oracle.
    good('strings', '"é" "\\xFF\\0" \'a\' \'\\xFF\' `a\\n` ``', [
         piece('string', '"é"', value=[195,169]),
         piece('string', '"\\xFF\\0"', value=[255,0]),
         piece('byte', "'a'", value=[97]), piece('byte', "'\\xFF'", value=[255]),
         piece('symbol', '`a\\n`', value=[97,92,110]),piece('symbol','``',value=[])])
    good('unicode-escapes', '"\\u{202E}\\u{1F600}\\u{0}"', [
         piece('string','"\\u{202E}\\u{1F600}\\u{0}"',value=[226,128,174,240,159,152,128,0])])
    good('raw-trim', '""" \r\n  a\n b\t """', [
         piece('string','""" \r\n  a\n b\t """',value=[97,10,32,98])])
    good('raw-no-escape', '"""\\n"""', [piece('string','"""\\n"""',value=[92,110])])
    good('fstring', 'f"hi {{x}}={x}\\u{7B}"', [piece('fstring-start','f"'),
         piece('string-text','hi {{x}}=',value=list(b'hi {x}=')),
         piece('interpolation-start','{'),piece('identifier','x'),piece('interpolation-end','}'),
         piece('string-text','\\u{7B}',value=[123]),piece('fstring-end','"')])
    good('nested-fstring', 'f"{f"{x}"}"', [piece('fstring-start','f"'),
         piece('interpolation-start','{'),piece('fstring-start','f"'),piece('interpolation-start','{'),
         piece('identifier','x'),piece('interpolation-end','}'),piece('fstring-end','"'),
         piece('interpolation-end','}'),piece('fstring-end','"')])
    good('interpolation-newlines', 'f"{\n{\nx\n}\n}"', [piece('fstring-start','f"'),
         piece('interpolation-start','{'),piece('punctuation','{'),piece('newline','\n'),
         piece('identifier','x'),piece('newline','\n'),piece('punctuation','}'),
         piece('interpolation-end','}'),piece('fstring-end','"')])
    for escape in ['u{}','u{D800}','u{110000}','u{0000000}','u{12_3}','u123']:
        bad('unicode-'+escape, '"\\'+escape+'"', 'LYC104','Invalid Unicode escape.',1)
    for escape in ['x','xG0','x0G']:
        bad('byte-'+escape, '"\\'+escape+'"', 'LYC104','Invalid byte escape.',1)
    bad('unknown-escape','"\\q"','LYC104','Unknown escape.',1)
    bad('byte-unicode', "'\\u{41}'", 'LYC104','Unknown escape.',1)
    for literal in ["''", "'ab'", "'é'"]:
        bad('byte-size-'+literal,literal,'LYC104','Byte literal must contain exactly one byte.')
    for literal in ['"abc','`abc','"""abc',"'a"]:
        bad('unterminated-'+literal,literal,'LYC104','Unterminated literal.')
    bad('literal-newline','"a\n"','LYC104','Newline in literal.',2)
    bad('fstring-brace','f"}"','LYC104','Invalid interpolated string text.',2)
    bad('fstring-open','f"{x','LYC104','Unterminated interpolated string.',4)
    bad('fstring-text-open','f"abc','LYC104','Unterminated interpolated string.',5)
    bad('triple-fstring','f"""a"""','LYC104','Triple interpolated strings are not supported.')

    failures = []
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / 'input.ly'
        for name, text, expected, error in cases:
            path.write_bytes(text.encode())
            try:
                run = subprocess.run(command + ['--inspect-tokens', str(path)], capture_output=True,
                                     text=True, timeout=10)
                if error is None:
                    actual = [json.loads(x) for x in run.stdout.splitlines()]
                    passed = run.returncode == 0 and not run.stderr and same_json(actual, expected)
                else:
                    code, message, (line, column, offset) = error
                    # Filename JSON spelling is representation-independent; suffix is exact.
                    quoted, end = json.JSONDecoder().raw_decode(run.stderr)
                    suffix = f':{line}:{column}: error {code}: {message} (byte {offset})\n'
                    actual_suffix = run.stderr[end:]
                    if code == 'LYC101':
                        # The NFC suggestion is itself JSON. Compare its decoded
                        # spelling, not a serializer's optional ASCII escapes.
                        lead = f':{line}:{column}: error LYC101: Identifier is not NFC; use '
                        if actual_suffix.startswith(lead):
                            suggestion, consumed = json.JSONDecoder().raw_decode(actual_suffix[len(lead):])
                            wanted = json.loads(message.removeprefix('Identifier is not NFC; use ')[:-1])
                            rest = actual_suffix[len(lead) + consumed:]
                            diagnostic_matches = (suggestion == wanted and rest == f'. (byte {offset})\n')
                        else:
                            diagnostic_matches = False
                    else:
                        diagnostic_matches = actual_suffix == suffix
                    passed = (run.returncode == 1 and not run.stdout and quoted == str(path)
                              and diagnostic_matches)
                if not passed:
                    failures.append(name + ': ' + repr((run.returncode, run.stdout, run.stderr)))
            except (ValueError, OSError, subprocess.TimeoutExpired) as exc:
                failures.append(name + ': ' + str(exc))
    for failure in failures:
        print('FAIL', failure)
    print(f'{len(cases)-len(failures)}/{len(cases)} token cases passed')
    return bool(failures)

if __name__ == '__main__':
    sys.exit(main())
