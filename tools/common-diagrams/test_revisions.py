#!/usr/bin/env python3
"""Readable migration regressions, outside the compiler implementation."""
import copy
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

import generate as g
import revisions as r

root = Path(__file__).resolve().parents[2]
old, current, types, edges = r.load_sources(root, sys.argv[1], historical_tests=True)
count = 1


def refuses(name, mutate):
    global count
    candidate = copy.deepcopy(current)
    mutate(candidate)
    try:
        r.validate_changes(old, candidate)
    except (ValueError, KeyError):
        print('PASS refusal:', name)
        count += 1
        return
    raise AssertionError('Accepted malformed migration: ' + name)


refuses('wrong historical source', lambda p: p.update(historical_commit='HEAD'))
refuses('missing lifecycle file', lambda p: p['reviewed_files'].pop('compiler/Semantics/TupleTypes.cs'))
refuses('extra lifecycle file', lambda p: p['reviewed_files'].update({'arbitrary': '0' * 64}))
refuses('unexplained canonical ownership', lambda p: p['changes'].pop('canonical-types'))
refuses('unchanged spelling but changed IR role', lambda p: p['changes'].pop('ir-declaration'))
refuses('missing reason', lambda p: p['changes']['nominal'].update(reason=''))
refuses('missing predecessor', lambda p: p['changes']['nominal'].update(previous=[]))
refuses('invented predecessor', lambda p: p['changes']['nominal'].update(previous=['fictional']))
refuses('removed field edge not accounted', lambda p: p['changes']['canonical-facts'].update(previous=['typed-facts']))
refuses('duplicate predecessor', lambda p: p['changes']['canonical-ir']['previous'].append('typed-facts'))
refuses('unchanged edge falsely retired', lambda p: p['changes']['canonical-ir']['previous'].append('sources'))
refuses('duplicate view', lambda p: p['views'].append(copy.deepcopy(p['views'][0])))
refuses('missing revised edge', lambda p: p['views'][0]['edges'].remove('array-types'))
refuses('duplicate introduction', lambda p: p['views'][0]['edges'].append('canonical-types'))
refuses('missing endpoint', lambda p: p['views'][0]['nodes'].remove('SemanticAnalysis'))
refuses('changed edge called context', lambda p: p['views'][0]['context'].append('canonical-types'))
refuses('unmarked context', lambda p: p['views'][2]['context'].remove('scopes'))
refuses('missing historical citation', lambda p: p['views'][1].update(previous_views=[]))
refuses('new type absent', lambda p: p['types'].update(Fictional={'description':'new','introduced':10}))

# Full current validation must retain stored-field/backlink and lifecycle checks.
inventory = json.loads((root / 'tools/common-diagrams/.build/inventory.json').read_text())
g.validate(root, inventory, current, check_views=False)
for name, mutate in [
    ('stale lifecycle', lambda p: p['reviewed_files'].update({'compiler/IR/Model.cs':'0' * 64})),
    ('missing stored relation', lambda p: p['relationships'].pop()),
    ('wrong backlink', lambda p: p['relationships'][0].update(back='Missing')),
    ('historical policy on canonical source', lambda p: p.update(type_policy='historical')),
    ('canonical value misclassification', lambda p: p['types']['SemanticType'].update(value=True)),
]:
    candidate = copy.deepcopy(current)
    mutate(candidate)
    try:
        g.validate(root, inventory, candidate, check_views=False)
    except ValueError:
        count += 1
        print('PASS refusal:', name)
    else:
        raise AssertionError(name)

# Rendering proves actual edge IDs, visible statuses and five-inch typography.
# Preparing twice must not write book files, even when rendering is successful.
paths = list((root / 'book/figures').glob('*')) + [root / 'book' / f'chapter-{n:02d}.md' for n in (4, 7, 8, 9, 10)]
saved = {p: p.read_bytes() for p in paths if p.is_file()}
files = r.prepare(root, old, current, edges)
assert files == r.prepare(root, old, current, edges)
assert saved == {p: p.read_bytes() for p in saved}
count += 2
ns = {'s':'http://www.w3.org/2000/svg'}
for view in current['views']:
    tree = ET.fromstring(files[root / 'book/figures' / (view['id'] + '.svg')])
    groups = tree.findall('.//s:g', ns)
    assert {n.attrib['id'] for n in groups if n.get('class') == 'edge'} == set(view['edges'])
    labels = [n.text for n in tree.findall('.//s:text', ns)]
    assert 'Chapter 10 — REVISED MODEL' in labels
    for key in view['edges']:
        change = current['changes'].get(key)
        status = 'CONTEXT' if change is None else 'CHANGED' if change['previous'] else 'NEW'
        group = next(n for n in groups if n.get('id') == key)
        assert status in [n.text for n in group.findall('.//s:text', ns)]
    count += 1
print(f'{count} migration checks passed; source coverage, revision refusals, nonmutation and rendered labels checked.')

# A late unreadable figure must refuse before any chapter/figure write.
bad_edges = copy.deepcopy(edges)
bad_edges['implementation']['label'] = 'X' * 500
try:
    r.prepare(root, old, current, bad_edges)
except ValueError as failure:
    assert 'Unreadable five-inch figure' in str(failure)
else:
    raise AssertionError('Oversized last figure was accepted')
assert saved == {p: p.read_bytes() for p in saved}
print('PASS late-render refusal preserves every manuscript/figure input')
