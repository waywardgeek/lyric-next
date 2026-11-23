#!/usr/bin/env python3
"""Check source coverage, deliberate invalid maps, chapter placement and SVG content."""
import copy
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
TOOL = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('diagrams', TOOL / 'generate.py')
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)
inventory = json.loads((TOOL / '.build/inventory.json').read_text())
plan = json.loads((TOOL / 'chapters.json').read_text())
types, edges = g.validate(ROOT, inventory, plan)
count = 1

def refuses(name, mutate):
    global count
    candidate = copy.deepcopy(plan)
    mutate(candidate)
    try:
        g.validate(ROOT, inventory, candidate)
    except ValueError:
        print('PASS refusal:', name)
        count += 1
        return
    raise AssertionError('Accepted malformed map: ' + name)

refuses('missing relationship', lambda p: p['views'][0]['edges'].clear())
refuses('duplicate relationship', lambda p: p['views'].append(copy.deepcopy(p['views'][0])))
refuses('wrong backlink', lambda p: p['relationships'][0].update(back='NoSuchField'))
refuses('future relationship', lambda p: p['relationships'][0].update(chapter=2))
refuses('unmapped stored field', lambda p: (p['relationships'].pop(0), p['views'][0]['edges'].clear()))
refuses('missing class description', lambda p: p['types']['Token'].update(description=''))
refuses('stale lifecycle evidence', lambda p: p['reviewed_files'].update({'compiler/IR/Model.cs': '0' * 64}))
ns = {'s': 'http://www.w3.org/2000/svg'}
for view in plan['views']:
    svg = ET.parse(ROOT / 'book/figures' / (view['id'] + '.svg'))
    groups = svg.findall('.//s:g', ns)
    node_names = {group.find('s:title', ns).text for group in groups if group.get('class') == 'node'}
    edge_ids = {group.get('id') for group in groups if group.get('class') == 'edge'}
    assert node_names == set(view['nodes']), view['id']
    assert edge_ids == set(view['edges']), view['id']
    # At a five-inch print width, all labels must remain at least ten points.
    width = float(svg.getroot().get('viewBox').split()[2])
    scale = min(1.0, 360 / width)
    font_sizes = [float(text.get('font-size')) for text in svg.findall('.//s:text', ns)]
    assert min(font_sizes) * scale >= 10, (view['id'], min(font_sizes) * scale)
    count += 1
for chapter in sorted({v['chapter'] for v in plan['views']}):
    text = (ROOT / 'book' / f'chapter-{chapter:02d}.md').read_text()
    assert text.count(g.BEGIN) == text.count(g.END) == 1
    assert text.splitlines()[2] == g.BEGIN
    assert text.index(g.END) < text.index('## TL;DR')
    for name, info in plan['types'].items():
        if info['introduced'] == chapter:
            assert f'**{name}:**' in text
    count += 1
# Teaching layout must survive regeneration without restoring repeated legends.
for chapter in [1, 2, 3, 4, 7, 8, 9]:
    text = (ROOT / 'book' / f'chapter-{chapter:02d}.md').read_text()
    assert g.LEGEND not in text
    count += 1
for chapter in [1, 2, 3, 5, 6, 8, 9]:
    text = (ROOT / 'book' / f'chapter-{chapter:02d}.md').read_text()
    assert text.count('## Introduction\n') == 1
    assert text.index('## Introduction') < text.index('## TL;DR')
    if g.END in text:
        assert text.index(g.END) < text.index('## Introduction')
    count += 1
example = json.loads((TOOL / 'example.json').read_text())
views = [(v, edges) for v in plan['views']] + [(example['view'], example['edges'])]
for view, relationships in views:
    svg = ET.parse(ROOT / 'book/figures' / (view['id'] + '.svg'))
    for group in svg.findall('.//s:g', ns):
        if group.get('class') != 'edge':
            continue
        expected = 2 if relationships[group.get('id')]['kind'] == 'strong' else 1.2
        assert float(group.find('.//s:path', ns).get('stroke-width')) == expected
    count += 1
key = ET.parse(ROOT / 'book/figures/ch00-diagram-key.svg')
width = float(key.getroot().get('viewBox').split()[2])
assert min(float(t.get('font-size')) for t in key.findall('.//s:text', ns)) * min(1, 360 / width) >= 10
text = (ROOT / 'book/chapter-00.md').read_text()
assert 'fictional document editor' in text and 'figures/ch00-diagram-key.svg' in text
count += 1
print(f'{count} diagram checks passed; every compiler relationship appears exactly once.')
