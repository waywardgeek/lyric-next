"""Chapter 1 local walkthrough; invoked by make spin01 from the repository root."""

import sys
from pathlib import Path
import subprocess
import tempfile

command = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
with tempfile.TemporaryDirectory(prefix='lyric-spin-') as directory:
    source = Path(directory) / 'example.ly'
    source.write_bytes('é\r\n温\t😀'.encode('utf-8'))
    print('Accepted characters and their source positions:', flush=True)
    subprocess.run(command + ['--inspect-source', str(source)], check=True)

    source.write_bytes(b'// ' + chr(0x202E).encode('utf-8'))
    print('A forbidden bidi control inside comment-looking text:', flush=True)
    rejected = subprocess.run(command + ['--inspect-source', str(source)])
    if rejected.returncode != 1:
        raise SystemExit('Expected a source diagnostic and exit code 1.')
