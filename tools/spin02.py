"""Chapter 2 local walkthrough; invoked by make spin02 from the repository root."""

import json
import sys
from pathlib import Path
import subprocess
import tempfile

command = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
with tempfile.TemporaryDirectory(prefix='lyric-tokens-') as directory:
    source = Path(directory) / 'example.ly'
    source.write_text('let café = 123u16\n"\\xFF"\nf"value: {café}"\n', encoding='utf-8')
    print('Typed number, byte-valued string, and interpolation:', flush=True)
    result = subprocess.run(command + ['--inspect-tokens', str(source)],
                            capture_output=True, text=True, check=True)
    if result.stderr:
        raise SystemExit('Unexpected diagnostic on valid input.')
    for line in result.stdout.splitlines():
        token = json.loads(line)
        print(token['kind'], repr(token['text']), token['suffix'], token['value'])

    source.write_text('cafe\u0301', encoding='utf-8')
    print('A decomposed identifier must be diagnosed, not silently rewritten:', flush=True)
    rejected = subprocess.run(command + ['--inspect-tokens', str(source)],
                              capture_output=True, text=True)
    print(rejected.stderr, end='')
    if rejected.returncode != 1 or rejected.stdout or 'LYC101' not in rejected.stderr:
        raise SystemExit('Expected NFC diagnostic, exit 1, and empty stdout.')
