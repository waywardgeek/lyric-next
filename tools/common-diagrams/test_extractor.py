#!/usr/bin/env python3
"""Exercise stored-member classification through the real source extractor.

Usage: python3 test_extractor.py DOTNET EXTRACTOR_DLL
No application assembly is loaded; the temporary C# is parsed as source only.
"""
import json
from pathlib import Path
import subprocess
import sys
import tempfile

if len(sys.argv) != 3:
    raise SystemExit(__doc__)

dotnet, extractor = sys.argv[1:]
with tempfile.TemporaryDirectory(prefix='lyric-diagram-extractor-') as directory:
    root = Path(directory)
    source = root / 'source'
    source.mkdir()
    (source / 'Model.cs').write_text('''
internal sealed class Child { }
internal partial class Parent
{
    internal Child? Stored;
    internal Child? Automatic { get; set; }
    internal Child? Computed => Stored;
    internal Child? Accessor { get { return Stored; } }
    // A partial declaration has no implementation here, but is not storage.
    internal partial Child? Navigation { get; }
}
''')
    output = root / 'inventory.json'
    result = subprocess.run(
        [dotnet, extractor, str(source), str(output)],
        capture_output=True, text=True, timeout=30)
    if result.returncode != 0:
        raise AssertionError(f'Extractor failed: {result.stdout}\n{result.stderr}')
    inventory = json.loads(output.read_text())
    parent = next(item for item in inventory['types'] if item['id'] == 'Parent')
    fields = {item['name'] for item in parent['fields']}
    assert fields == {'Stored', 'Automatic'}, fields
    print('PASS extractor: real fields and auto-properties only; computed/partial access paths excluded')
