#!/usr/bin/env python3
"""Small mutation controls for Chapter 11's relationship migration contract."""
import copy
import json
from pathlib import Path
import subprocess

import chapter11 as c

root = Path(__file__).resolve().parents[2]
old = json.loads(subprocess.check_output(
    ['git', 'show', c.PIN + ':tools/common-diagrams/chapter10.json'], cwd=root))
current = json.loads((root / 'tools/common-diagrams/chapter11.json').read_text())
c.validate_changes(old, current)
mutations = [
    ('wrong prerequisite', lambda p: p.update(historical_commit='wrong')),
    ('missing change', lambda p: p['changes'].pop('generic-bindings')),
    ('omitted drawing', lambda p: p['views'][1]['edges'].pop()),
    ('duplicate drawing', lambda p: p['views'][1]['edges'].append('generic-bindings')),
    ('missing endpoint', lambda p: p['views'][1]['nodes'].remove('GenericBinding')),
    ('changed historical relationship', lambda p: p['relationships'][0].update(kind='weak')),
    ('missing lifecycle review', lambda p: p['reviewed_files'].pop('compiler/Semantics/GenericSpecialization.cs')),
]
for name, mutate in mutations:
    broken = copy.deepcopy(current)
    mutate(broken)
    try:
        c.validate_changes(old, broken)
    except ValueError:
        print('REJECT:', name)
    else:
        raise AssertionError('Accepted deliberate defect: ' + name)
print('7 Chapter 11 migration controls passed.')
