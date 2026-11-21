#!/usr/bin/env python3
"""Retained Unicode foundation grader. Uses official data, never solution source.

Run: python3 graders/ch02/unicode.py -- DOTNET IMPLEMENTATION.dll
The command receives --inspect-unicode FILE. JSON object order is irrelevant;
record order, exact keys, integer scalar arrays and boolean property arrays matter.
"""
import json
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'unicode/16.0.0'


def property_set(name):
    result = {95}  # Lyric explicitly admits underscore.
    for raw in (DATA / 'DerivedCoreProperties.txt').read_text().splitlines():
        fields = [part.strip() for part in raw.partition('#')[0].split(';')]
        if len(fields) < 2 or fields[1] != name:
            continue
        ends = fields[0].split('..')
        result.update(range(int(ends[0], 16), int(ends[-1], 16) + 1))
    return result


def main():
    if len(sys.argv) < 3 or sys.argv[1] != '--':
        raise SystemExit('Usage: unicode.py -- COMMAND [ARG ...]')
    command = sys.argv[2:]
    requests = [([], [])]
    # Unicode specifies NFC(c1)=NFC(c2)=NFC(c3)=c2 and NFC(c4)=NFC(c5)=c4.
    for raw in (DATA / 'NormalizationTest.txt').read_text().splitlines():
        body = raw.partition('#')[0].strip()
        if not body or body.startswith('@'):
            continue
        fields = [[int(x, 16) for x in part.split()] for part in body.split(';')[:5]]
        for i, source in enumerate(fields):
            requests.append((source, fields[1 if i < 3 else 3]))
    starts = property_set('XID_Start')
    continues = property_set('XID_Continue')
    # Exercise every scalar's property classification in bounded batches, without
    # claiming additional NFC expectations from a host's unpinned Unicode version.
    vectors = len(requests)
    valid = [s for s in range(0x110000) if not 0xD800 <= s <= 0xDFFF]
    for offset in range(0, len(valid), 256):
        requests.append((valid[offset:offset + 256], None))
    with tempfile.TemporaryDirectory(prefix='lyric-unicode-grade-') as directory:
        path = Path(directory) / 'scalars.jsonl'
        path.write_text(''.join(json.dumps(source) + '\n' for source, _ in requests))
        try:
            result = subprocess.run(command + ['--inspect-unicode', str(path)],
                                    capture_output=True, text=True, timeout=120)
        except subprocess.TimeoutExpired:
            raise SystemExit('FAIL: Unicode inspection timed out')
        if result.returncode != 0 or result.stderr:
            raise SystemExit(f'FAIL: exit={result.returncode}, stderr={result.stderr[:1000]!r}')
        lines = result.stdout.splitlines()
        if len(lines) != len(requests):
            raise SystemExit(f'FAIL: expected {len(requests)} records, received {len(lines)}')
        for index, (line, (source, expected)) in enumerate(zip(lines, requests)):
            try:
                actual = json.loads(line)
            except ValueError:
                raise SystemExit(f'FAIL record {index}: invalid JSON')
            if not isinstance(actual, dict) or set(actual) != {'nfc', 'start', 'continuation'}:
                raise SystemExit(f'FAIL record {index}: wrong keys/type')
            nfc = actual['nfc']
            if not isinstance(nfc, list) or any(type(x) is not int or x not in range(0x110000)
                    or 0xD800 <= x <= 0xDFFF for x in nfc):
                raise SystemExit(f'FAIL record {index}: invalid NFC scalar types')
            if expected is not None and nfc != expected:
                raise SystemExit(f'FAIL record {index}: NFC({source}) = {nfc}, expected {expected}')
            for key, allowed in [('start', starts), ('continuation', continues)]:
                flags = actual[key]
                if not isinstance(flags, list) or any(type(x) is not bool for x in flags):
                    raise SystemExit(f'FAIL record {index}: nonboolean {key}')
                if flags != [scalar in allowed for scalar in source]:
                    raise SystemExit(f'FAIL record {index}: wrong {key} membership')
        invalid = ['null', '[true]', '[-1]', '[1114112]', '[55296]', '{', '["a"]']
        for spelling in invalid:
            # A valid first record must not escape before a later invalid request.
            path.write_text('[65]\n' + spelling + '\n')
            result = subprocess.run(command + ['--inspect-unicode', str(path)],
                                    capture_output=True, text=True, timeout=10)
            if (result.returncode != 1 or result.stdout != '' or
                    result.stderr != 'error LYC005: Invalid Unicode inspection data.\n'):
                raise SystemExit(f'FAIL malformed inspection request {spelling!r}')
    print(f'PASS: {vectors} NFC vectors; XID classification for all {len(valid)} Unicode scalars; 7 malformed requests')


if __name__ == '__main__':
    main()
