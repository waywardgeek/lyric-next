#!/usr/bin/env python3
"""Chapter maps from stored C# fields and reviewed relationships, not guessed ownership.

The Roslyn extractor is shared with Ensemble's source-only diagram approach.
Chapter selections and teaching are reviewed input. Never run the compiler itself.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

COLORS = {'strong': '#164f86', 'weak': '#a74b00', 'data': '#687682', 'dependency': '#704090'}
BEGIN = '<!-- common-diagram:start -->'
END = '<!-- common-diagram:end -->'
LEGEND = ('Heavy solid arrows: strong ownership; deleting the parent destroys its children. '
          'Thin dashed arrows: non-owning links; deleting the holder does not destroy the target. '
          'Gray dotted arrows: stored value data. Purple dashed arrows: dependencies retired when their declaration is destroyed. Open arrowhead: one slot, possibly absent; '
          'filled arrowhead: potentially many. Back-references are maintained but not drawn twice.')

def require(condition, message):
    if not condition:
        raise ValueError(message)

def field(types, owner, name):
    matches = [item for item in types[owner]['fields'] if item['name'] == name]
    require(len(matches) == 1, f'Expected one stored field: {owner}.{name}')
    return matches[0]

def validate(root, inventory, plan):
    types = {t['id']: t for t in inventory['types'] if not t['isStatic']}
    actual = {p.name for p in (root / 'compiler/Common').glob('*.cs')}
    require(actual == {s['file'] for s in inventory['files']}, 'Stale Common file set')
    for source in inventory['files']:
        p = root / 'compiler/Common' / source['file']
        require(hashlib.sha256(p.read_bytes()).hexdigest() == source['sha256'], f'Stale inventory: {p}')
    for name, digest in plan['reviewed_files'].items():
        require(hashlib.sha256((root / name).read_bytes()).hexdigest() == digest,
                f'Review changed lifecycle before refreshing fingerprint: {name}')
    require(set(types) == set(plan['types']), 'New/missing Common type needs chapter classification')
    edges = {}
    for rel in plan['relationships']:
        require(rel['id'] not in edges, 'Duplicate relationship ID')
        require(rel['kind'] in COLORS, 'Unknown relationship kind')
        f = field(types, rel['owner'], rel['field'])
        require(not f['deferred'] and rel['child'] in f['references'], f'Invalid stored edge: {rel["id"]}')
        if rel['back']:
            owner = rel['child']
            parts = rel['back'].split('.')
            for index, part in enumerate(parts):
                back = field(types, owner, part)
                if index + 1 < len(parts):
                    require(len(back['references']) == 1, 'Ambiguous indirect backlink')
                    owner = back['references'][0]
            require(rel['owner'] in back['references'], f'Missing backlink: {rel["id"]}')
        require(rel['chapter'] >= max(plan['types'][n]['introduced'] for n in [rel['owner'], rel['child']]),
                f'Relationship precedes its types: {rel["id"]}')
        edges[rel['id']] = dict(rel, many=f['many'], source=f['source'])
    # Nominal values carry an indirect dependency. The persistent holders, not
    # each value copy, register the reverse memberships on the class declaration.
    for holder, reverse in [('SemanticSymbol', 'TypedSymbols'), ('SemanticFact', 'TypedFacts'), ('IrNode', 'IrTypes')]:
        require('SemanticType' in field(types, holder, 'Type')['references'], f'Missing type holder: {holder}')
        require(holder in field(types, 'SemanticSymbol', reverse)['references'], f'Missing nominal reverse: {reverse}')
    used = set()
    covered_fields = set()
    for e in edges.values():
        covered_fields.add((e["owner"], e["field"], e["child"]))
        if e["back"] and "." not in e["back"]:
            covered_fields.add((e["child"], e["back"], e["owner"]))
    for owner, declaration in types.items():
        for f in declaration["fields"]:
            if f["deferred"]:
                continue
            for target in f["references"]:
                if target in types:
                    require((owner, f["name"], target) in covered_fields,
                            f"Unmapped stored relationship: {owner}.{f['name']} -> {target}")
    for view in plan['views']:
        require(len(view['nodes']) == len(set(view['nodes'])), 'Duplicate node')
        for name in view['nodes']:
            require(name in types and plan['types'][name]['introduced'] <= view['chapter'], f'Future type in {view["id"]}')
        for name in view['edges']:
            e = edges[name]
            require(e['chapter'] <= view['chapter'], f'Future edge in {view["id"]}')
            require(e['owner'] in view['nodes'] and e['child'] in view['nodes'], 'Missing edge endpoint')
            require(name not in used, f'Relationship drawn twice: {name}')
            require(e['chapter'] == view['chapter'], f'Relationship not in introduction chapter: {name}')
            used.add(name)
    require(used == set(edges), 'Relationship omitted from every diagram')
    for name, entry in plan['types'].items():
        require(entry['description'].strip(), f'Missing class guide: {name}')
        require(any(v['chapter'] == entry['introduced'] and name in v['nodes'] for v in plan['views']),
                f'New type absent from introduction: {name}')
    return types, edges

def quote(text):
    return json.dumps(text)

def ports(key):
    # Parallel role edges need opposite attachment sides, not overlapping labels.
    loops = {'ir-tree': ('nw', 'sw'), 'loop-target': ('ne', 'se')}
    if key in loops:
        tail, head = loops[key]
        return f', tailport={tail}, headport={head}'
    sides = {'nodes': 'w', 'body': 'e',
             'operands': 'w', 'uses': 'e'}
    side = sides.get(key)
    return f', tailport={side}, headport={side}' if side else ''


def dot(view, plan, edges):
    lines = ['digraph Common {',
             f'graph [rankdir={view.get("rankdir", "TB")}, bgcolor="white", pad=0.15, nodesep=0.3, ranksep=0.45, splines=spline, outputorder=edgesfirst];',
             'node [shape=box, style="rounded,filled", fontname="Helvetica", fontsize=17, margin="0.12,0.08", color="#415466"];',
             'edge [fontname="Helvetica", fontsize=13, arrowsize=1.05];']
    for name in view['nodes']:
        info = plan['types'][name]
        suffix = 'value' if info['value'] else ''
        if info['introduced'] < view['chapter']:
            suffix = ', '.join(x for x in ['existing', suffix] if x)
        label = name + ('\n(' + suffix + ')' if suffix else '')
        fill = '#edf4fb' if info['introduced'] == view['chapter'] else '#ffffff'
        lines.append(f'{quote(name)} [label={quote(label)}, fillcolor={quote(fill)}];')
    for key in view['edges']:
        e = edges[key]
        kind = e['kind']
        style = {'strong': 'solid', 'weak': 'dashed', 'data': 'dotted', 'dependency': 'dashed'}[kind]
        # One role label may distinguish parallel relationships; never repeat a
        # target type name as decoration. Multiplicity is independent of strength.
        arrow = 'normal' if e['many'] else 'onormal'
        tip = f'{e["owner"]}.{e["field"]} -> {e["child"]}'
        if e['back']:
            tip += f'; reverse {e["child"]}.{e["back"]}'
        lines.append(f'{quote(e["owner"])} -> {quote(e["child"])} [id={quote(key)}, '
                     f'color={quote(COLORS[kind])}, penwidth={2 if kind == "strong" else 1.2}, '
                     f'style={style}, arrowhead={arrow}, label={quote(e["label"].replace(" ", chr(10)) if key in ["ir-declaration", "ir-destruction", "selected"] else e["label"])}, tooltip={quote(tip)}{ports(key)}];')
    return '\n'.join(lines + ['}']) + '\n'

def chapter_block(chapter, views, plan):
    lines = [BEGIN, '']
    for view in views:
        lines += [f'![{view["title"]}. Text relationships and class descriptions follow.](figures/{view["id"]}.svg)', '']
    if chapter == 8:
        lines += ['**Changed classes.** No new class-definition object is introduced: a class is a `SemanticSymbol`.', '',
                  '- **SemanticSymbol:** now owns a member scope and records reverse dependencies from symbols and facts that use its class identity.',
                  '- **SemanticScope:** a member scope has both analysis and class owners; it retains both back-references.',
                  '- **SemanticType:** now carries an optional class declaration identity. Equality compares that identity, scalar kind and array depth, not the class name or a recursive type tree.',
                  '- **SemanticFact:** registers its nominal type dependency with the class. Removing the class invalidates dependent semantic objects.', '']
    else:
        lines += ['**New data structures.**', '']
        new = [name for name, entry in plan['types'].items() if entry['introduced'] == chapter]
        for name in new:
            lines.append(f'- **{name}:** {plan["types"][name]["description"]}')
        lines += ['']
    if chapter == 1:
        lines += ['`Diagnostic` is returned as value data, so there is no ownership arrow from a source to its diagnostics.', '']
    if chapter == 4:
        lines += ['Types are scalar/byte-array enum values at this stage. The structured type descriptor arrives in Chapter 7. Non-owning semantic dependencies still participate in invalidation: deleting a declaration retires facts that depended on it.', '']
    if chapter == 7:
        lines += ['`SemanticSymbol` and `SemanticFact` now store this descriptor. Type equality compares scalar kind and nesting depth; there is no recursive hierarchy or intern table in this chapter.', '']
    if chapter == 9:
        lines += ['The two views separate execution ownership from checked meaning. Nominal type dependencies also maintain reverse links on their class symbol; changing semantic inputs retires the derived IR.', '']
    lines += [f'[Text relationships and source fields](figures/ch{chapter:02d}-relationships.md).', '', END]
    return '\n'.join(lines)

def main(root):
    tool = root / 'tools/common-diagrams'
    inventory = json.loads((tool / '.build/inventory.json').read_text())
    plan = json.loads((tool / 'chapters.json').read_text())
    types, edges = validate(root, inventory, plan)
    output = root / 'book/figures'
    output.mkdir(exist_ok=True)
    # The fictional teaching example shares rendering, not compiler coverage.
    example = json.loads((tool / 'example.json').read_text())
    render_views = [(view, plan, edges) for view in plan['views']]
    render_views.append((example['view'], example, example['edges']))
    for view, render_plan, render_edges in render_views:
        stem = output / view['id']
        stem.with_suffix('.dot').write_text(dot(view, render_plan, render_edges))
        subprocess.run(['dot', '-Tsvg', str(stem.with_suffix('.dot')), '-o', str(stem.with_suffix('.svg'))], check=True)
        subprocess.run(['dot', '-Tpng', '-Gdpi=140', str(stem.with_suffix('.dot')), '-o', str(tool / '.build' / (view['id'] + '.png'))], check=True)
        svg = ET.parse(stem.with_suffix('.svg')).getroot()
        print(view['id'], svg.attrib.get('viewBox'))
    for chapter in sorted({v['chapter'] for v in plan['views']}):
        views = [v for v in plan['views'] if v['chapter'] == chapter]
        p = root / 'book' / f'chapter-{chapter:02d}.md'
        text = p.read_text()
        block = chapter_block(chapter, views, plan)
        if BEGIN in text:
            require(text.count(BEGIN) == text.count(END) == 1, 'Duplicate chapter blocks')
            start, rest = text.split(BEGIN)
            _, finish = rest.split(END)
            text = start.rstrip('\n') + '\n\n' + finish.lstrip('\n')
        title, rest = text.split('\n', 1)
        require(title.startswith('# Chapter '), 'Missing chapter title')
        text = title + '\n\n' + block + '\n\n' + rest.lstrip('\n')
        p.write_text(text)
        rows = [f'# Chapter {chapter} — diagram relationships', '', LEGEND, '']
        for view in views:
            rows += [f'## {view["title"]}', '']
            for key in view['edges']:
                e = edges[key]
                back = f'; reverse `{e["child"]}.{e["back"]}`' if e['back'] else ''
                rows.append(f'- `{e["owner"]}.{e["field"]}` → `{e["child"]}`: {e["kind"]}, '
                            f'{"many" if e["many"] else "one slot"}{back}.')
            rows += ['']
        (output / f'ch{chapter:02d}-relationships.md').write_text('\n'.join(rows))
    print('Generated eight compiler figures, one illustrative key, and seven chapter guides from reviewed Common relationships.')

if __name__ == '__main__':
    main(Path(sys.argv[1]).resolve())
