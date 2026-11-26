#!/usr/bin/env python3
"""Publish Chapter 11 additions without rewriting historical figures or manifests."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

import generate as g

PIN = '0ea3d8e617bbb3c15680b7fabd2e7df415ced1dd'
BEGIN = '<!-- ch11-model:start -->'
END = '<!-- ch11-model:end -->'


def validate_changes(old, current):
    """Require exact preservation of old relationships and one drawing per addition."""
    g.require(current['historical_commit'] == PIN, 'Wrong Chapter 10 prerequisite')
    before = {edge['id']: edge for edge in old['relationships']}
    after = {edge['id']: edge for edge in current['relationships']}
    g.require(set(before) <= set(after), 'Removed historical relationship')
    for key, edge in before.items():
        g.require(edge == after[key], 'Changed historical relationship: ' + key)
    added = set(after) - set(before)
    g.require(added == set(current['changes']), 'Unaccounted new relationship')
    g.require(set(current['reviewed_files']) == set(old['reviewed_files']) |
              {'compiler/Semantics/GenericSpecialization.cs'}, 'Incomplete lifecycle review set')
    drawn = set()
    names = set()
    for view in current['views']:
        g.require(view['id'] not in names and view['id'].startswith('ch11-'), 'Invalid view ID')
        names.add(view['id'])
        g.require(view['chapter'] == 11 and view['title'].strip(), 'Invalid chapter view')
        g.require(not view['context'] and not view['previous_views'], 'New-only views required')
        g.require(len(view['nodes']) == len(set(view['nodes'])), 'Duplicate view node')
        for key in view['edges']:
            g.require(key in added and key not in drawn, 'Repeated or historical drawn edge')
            drawn.add(key)
            edge = after[key]
            g.require({edge['owner'], edge['child']} <= set(view['nodes']), 'Missing endpoint')
    g.require(drawn == added, 'Undrawn new relationship')
    for key in added:
        change = current['changes'][key]
        g.require(not change['previous'] and change['reason'].strip(), 'Invalid addition explanation')
    for name in set(current['types']) - set(old['types']):
        g.require(current['types'][name]['introduced'] == 11 and
                  any(name in view['nodes'] for view in current['views']), 'Undrawn new type')


def load_sources(root, dotnet):
    tool = root / 'tools/common-diagrams'
    current = json.loads((tool / 'chapter11.json').read_text())
    extractor = tool / '.build/artifacts/bin/Extractor/debug/Extractor.dll'
    with tempfile.TemporaryDirectory(prefix='lyric-ch11-diagrams-') as directory:
        historical = Path(directory)
        old = json.loads(subprocess.check_output(
            ['git', 'show', PIN + ':tools/common-diagrams/chapter10.json'], cwd=root))
        names = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', PIN,
                                        '--', 'compiler/Common'], cwd=root, text=True).splitlines()
        for name in names + list(old['reviewed_files']):
            destination = historical / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(subprocess.check_output(['git', 'show', PIN + ':' + name], cwd=root))
        inventories = []
        for label, source in [('historical', historical), ('current', root)]:
            output = historical / (label + '.json')
            subprocess.run([dotnet, str(extractor), str(source / 'compiler/Common'), str(output)], check=True)
            inventories.append(json.loads(output.read_text()))
        g.validate(historical, inventories[0], old, check_views=False)
        types, edges = g.validate(root, inventories[1], current, check_views=False)
        validate_changes(old, current)
    return old, current, types, edges


def prepare(root, current, edges):
    """Render and validate everything in memory before admitting publication."""
    files = {}
    block = [BEGIN, '', 'These views introduce only Chapter 11 relationships. Earlier source, scope,',
             'and analysis ownership remains in force; the companion links to those figures.', '']
    guide = ['# Chapter 11 — specialization relationships', '',
             'Prerequisite source: `' + PIN + '`. Current source is freshly extracted and lifecycle fingerprints are checked.', '',
             g.LEGEND, '',
             'These figures draw each new relationship once. Existing co-owners are not duplicated:',
             '[source-owned syntax](ch03-common.svg), [scope-owned symbols and analysis-owned scopes](ch04-common.svg),',
             'and [canonical dependencies](ch10-type-users.svg). Lexical scope children remain non-owning.', '',
             'The two SemanticSymbol roles in the first view are different objects: a lookup-visible template',
             'and its concrete callable. Both directions of each relationship are maintained by the named add/remove operations.', '']
    for view in current['views']:
        marked = copy.deepcopy(edges)
        for key in view['edges']:
            marked[key]['label'] = marked[key]['label'].replace(' ', '\n')
        dot = g.dot(view, current, marked)
        # Opposite ports keep the template and concrete-callable roles legible.
        if view['id'] == 'ch11-specializations':
            dot = dot.replace('id="template-specializations",', 'id="template-specializations", tailport=w, headport=w,')
            dot = dot.replace('id="specialized-function",', 'id="specialized-function", tailport=e, headport=e, constraint=false,')
        svg = subprocess.check_output(['dot', '-Tsvg'], input=dot.encode())
        tree = ET.fromstring(svg)
        width = float(tree.attrib['viewBox'].split()[2])
        texts = tree.findall('.//{http://www.w3.org/2000/svg}text')
        minimum = min(float(text.attrib['font-size']) for text in texts) * min(1, 360 / width)
        g.require(minimum >= 10, f'Unreadable five-inch figure {view["id"]}: {minimum:.2f}pt')
        stem = root / 'book/figures' / view['id']
        files[stem.with_suffix('.dot')] = dot.encode()
        files[stem.with_suffix('.svg')] = svg
        block += ['**' + view['title'] + '.**', '',
                  '![' + view['title'] + '.](figures/' + view['id'] + '.svg)', '']
        for name in view['nodes']:
            block.append('- **' + name + ':** ' + current['types'][name]['description'])
        block.append('')
        guide += ['## ' + view['title'], '']
        for key in view['edges']:
            edge = edges[key]
            guide += ['- **' + key + '**: `' + edge['owner'] + '.' + edge['field'] + '` → `' + edge['child'] + '`; ' + edge['kind'] + '.',
                      '  Reverse: `' + edge['child'] + '.' + edge['back'] + '`. ' + current['changes'][key]['reason']]
        guide.append('')
    block += ['**SemanticFact** also gains a selected-arm child index. It is value data, not another object relationship.', '',
              '[Exact fields, reverse links, co-owners, and retirement](figures/ch11-relationships.md).', '', END]
    guide += ['## Retirement order', '',
              'Clear derived IR first, mark the specialization retired, then destroy its callable, all concrete-body scopes,',
              'copied syntax, and binding occurrences. Finally detach from the template and analysis.',
              'Destroying a live owned part first retires its specialization; the retired flag prevents reentrant cascades.',
              'Type retirement follows bindings toward specializations, never toward other shared type arguments.',
              'Copied syntax retains source offsets and source ownership, but does not become a parsed root.',
              'Its facts and declaration links require explicit cleanup because it cannot clear the analysis constructing it.', '']
    chapter = root / 'book/chapter-11.md'
    text = chapter.read_text()
    g.require(text.count(BEGIN) == text.count(END) == 1, 'Damaged Chapter 11 model markers')
    start, rest = text.split(BEGIN)
    _, finish = rest.split(END)
    files[chapter] = (start + '\n'.join(block) + finish).encode()
    files[root / 'book/figures/ch11-relationships.md'] = '\n'.join(guide).encode()
    return files


def main():
    root = Path(__file__).resolve().parents[2]
    _, current, types, edges = load_sources(root, sys.argv[1])
    files = prepare(root, current, edges)
    print(f'Validated pinned Chapter 10 and current {len(types)} types/{len(edges)} relationships.')
    if '--publish' not in sys.argv[2:]:
        preview = root / 'tools/common-diagrams/.build/chapter11'
        preview.mkdir(parents=True, exist_ok=True)
        for path, data in files.items():
            (preview / path.name).write_bytes(data)
        print('Preview only:', preview)
        return
    saved = {path: path.read_bytes() if path.exists() else None for path in files}
    try:
        for path, data in files.items():
            path.write_bytes(data)
        for path, data in files.items():
            g.require(path.read_bytes() == data, 'Publication readback failed: ' + str(path))
    except BaseException:
        for path, data in saved.items():
            if data is None:
                path.unlink(missing_ok=True)
            else:
                path.write_bytes(data)
        raise
    print(f'Published/read back {len(files)} Chapter 11 files; no earlier figures rewritten.')


if __name__ == '__main__':
    main()
