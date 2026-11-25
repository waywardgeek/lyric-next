#!/usr/bin/env python3
"""Validate two real source versions; publish only explicitly revised Chapter 10 views.

Historical source comes from a pinned local Git object, never the working compiler.
The current inventory is freshly extracted. No compiler code is executed.
"""
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

import generate as g

PIN = '6073e136c772019dc8dbe4ca11211340904cbf9e'
BEGIN = '<!-- common-revisions:start -->'
END = '<!-- common-revisions:end -->'
REVIEWED = {
    'compiler/Model/Model.cs', 'compiler/Model/Token.cs',
    'compiler/Syntax/SyntaxNode.cs', 'compiler/Semantics/SemanticModel.cs',
    'compiler/Semantics/SemanticSymbol.cs', 'compiler/Semantics/SemanticFact.cs',
    'compiler/Semantics/FunctionScope.cs', 'compiler/Semantics/ClassModel.cs',
    'compiler/IR/Model.cs', 'compiler/Semantics/CanonicalTypes.cs',
    'compiler/Semantics/TupleTypes.cs',
}


def signature(edge):
    """Compare model meaning, not introduction dates or decorative labels."""
    return tuple(edge[key] for key in ('owner', 'field', 'child', 'kind', 'back'))


def validate_changes(old, current):
    before = {e['id']: e for e in old['relationships']}
    after = {e['id']: e for e in current['relationships']}
    changes = current['changes']
    g.require(current['historical_commit'] == PIN, 'Unexpected historical source')
    g.require(set(current['reviewed_files']) == REVIEWED, 'Incomplete lifecycle review set')
    g.require(set(changes) <= set(after), 'Unknown changed edge')
    # A relationship may change meaning without changing its stored fields.
    g.require('ir-declaration' in changes, 'Initializer IR role must be explained')
    required = {key for key in after if key not in before
                or signature(after[key]) != signature(before[key])}
    g.require(required <= set(changes), 'Unexplained relationship change')
    accounted = set()
    for key, change in changes.items():
        g.require(change['reason'].strip(), 'Missing change reason')
        for predecessor in change['previous']:
            g.require(predecessor in before, 'Unknown predecessor')
            g.require(predecessor not in accounted, 'Predecessor accounted twice')
            accounted.add(predecessor)
        if key in before:
            g.require(key in change['previous'], 'Changed edge omits own predecessor')
    g.require(set(before) - set(after) <= accounted, 'Unexplained removed relationship')
    g.require(not (accounted & (set(after) - set(changes))), 'Unchanged edge falsely replaced')
    used = set()
    view_ids = set()
    for view in current['views']:
        g.require(view['id'] not in view_ids, 'Duplicate revision view')
        view_ids.add(view['id'])
        g.require(view['title'].strip() and view['chapter'] == 10, 'Missing revision title/chapter')
        g.require(len(view['edges']) == len(set(view['edges'])), 'Duplicate drawn edge')
        g.require(len(view['nodes']) == len(set(view['nodes'])), 'Duplicate drawn node')
        context = set(view['context'])
        g.require(context <= set(view['edges']), 'Undrawn context')
        predecessors = set(context)
        for key in view['edges']:
            g.require(key in after, 'Unknown drawn edge')
            edge = after[key]
            g.require({edge['owner'], edge['child']} <= set(view['nodes']), 'Missing endpoint')
            if key in context:
                g.require(key in before and key not in changes
                          and signature(edge) == signature(before[key]), 'Changed edge marked context')
            else:
                g.require(key in changes and key not in used, 'Unmarked or duplicate introduction')
                used.add(key)
                predecessors.update(changes[key]['previous'])
        expected = {v['id'] for v in old['views'] if predecessors.intersection(v['edges'])}
        g.require(set(view['previous_views']) == expected, 'Missing historical view reference')
    g.require(used == set(changes), 'Undrawn changed relationship')
    for name, info in current['types'].items():
        g.require(info['description'].strip(), 'Missing type guide')
        if name not in old['types']:
            g.require(any(name in v['nodes'] for v in current['views']), 'Undrawn new type')


def load_sources(root, dotnet, *, historical_tests=False):
    tool = root / 'tools/common-diagrams'
    current = json.loads((tool / 'chapter10.json').read_text())
    extractor = tool / '.build/artifacts/bin/Extractor/debug/Extractor.dll'
    with tempfile.TemporaryDirectory(prefix='lyric-diagrams-') as directory:
        historical = Path(directory)
        names = subprocess.check_output(
            ['git', 'ls-tree', '-r', '--name-only', PIN, '--', 'compiler/Common',
             'tools/common-diagrams/chapters.json'], cwd=root, text=True).splitlines()
        names += sorted(REVIEWED - {'compiler/Semantics/CanonicalTypes.cs',
                                   'compiler/Semantics/TupleTypes.cs'})
        for name in names:
            destination = historical / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(subprocess.check_output(['git', 'show', f'{PIN}:{name}'], cwd=root))
        old = json.loads((historical / 'tools/common-diagrams/chapters.json').read_text())
        inventories = []
        for label, source in [('historical', historical), ('current', root)]:
            output = historical / (label + '-inventory.json')
            subprocess.run([dotnet, str(extractor), str(source / 'compiler/Common'), str(output)], check=True)
            inventories.append(json.loads(output.read_text()))
        g.validate(historical, inventories[0], old)
        if historical_tests:
            environment = dict(os.environ)
            environment['LYRIC_DIAGRAM_SOURCE'] = str(historical)
            environment['LYRIC_DIAGRAM_INVENTORY'] = str(historical / 'historical-inventory.json')
            subprocess.run([sys.executable, '-W', 'error', str(tool / 'test.py')],
                           env=environment, check=True)
        types, edges = g.validate(root, inventories[1], current, check_views=False)
        validate_changes(old, current)
    return old, current, types, edges


def render(current, edges):
    """Buffer every figure and check print size before any manuscript write."""
    artifacts = {}
    for view in current['views']:
        marked = copy.deepcopy(edges)
        for key in view['edges']:
            change = current['changes'].get(key)
            status = 'CONTEXT' if change is None else ('CHANGED' if change['previous'] else 'NEW')
            role = marked[key]['label'] or marked[key]['field']
            marked[key]['label'] = status + '\n' + role.replace(' ', '\n')
        dot = g.dot(view, current, marked)
        heading = 'Chapter 10 — REVISED MODEL'
        # DOT accepts literal UTF-8, not JSON's backslash-u escape convention.
        dot = dot.replace('digraph Common {', 'digraph Common {\nlabel=' +
                          json.dumps(heading, ensure_ascii=False) + '; labelloc=t; fontsize=17;')
        svg = subprocess.check_output(['dot', '-Tsvg'], input=dot.encode())
        tree = ET.fromstring(svg)
        texts = tree.findall('.//{http://www.w3.org/2000/svg}text')
        g.require(heading in [t.text for t in texts], 'Corrupt revision heading')
        width = float(tree.attrib['viewBox'].split()[2])
        minimum = min(float(t.attrib['font-size']) for t in texts) * min(1, 360 / width)
        g.require(minimum >= 10, f'Unreadable five-inch figure {view["id"]}: {minimum:.2f}pt')
        artifacts[view['id'] + '.dot'] = dot.encode()
        artifacts[view['id'] + '.svg'] = svg
    return artifacts


def teaching(old, current, edges):
    lines = [BEGIN, '', '**Revised model — Chapter 10.** Earlier figures describe their historical',
             'construction stages. NEW introduces a relationship here; CHANGED replaces the',
             'cited earlier relationship; CONTEXT repeats an unchanged link for orientation.',
             'Repeated context is not a second introduction. Type identity is now analysis-owned.', '']
    for view in current['views']:
        lines += [f'**{view["title"]}.**', '', f'![{view["title"]}; revised Chapter 10 model.](figures/{view["id"]}.svg)', '']
        for name in view['nodes']:
            description = current['types'][name]['description']
            if name == 'SemanticSymbol':
                description = 'A declaration identity: class/struct symbols own member scopes, defaulted fields own initializer scopes, and methods link to separate implementation syntax without duplicating the callable.'
            elif name == 'SemanticScope':
                description = 'An analysis-owned lexical scope. Member and initializer scopes additionally have their actual declaration owner; both back-references are maintained.'
            elif name == 'SemanticAnalysis':
                description = 'Owns semantic scopes, facts, and canonical identities in TypeBuckets. It retires derived IR before semantic teardown.'
            lines.append(f'- **{name}:** {description}')
        lines += ['', '[Exact source fields, predecessors, and retirement rules](figures/ch10-relationships.md).', '']
    lines += [END]
    guide = ['# Chapter 10 — revised relationships', '',
             'Historical source: `' + PIN + '`. Current source: the checked Chapter 10 working tree.', '',
             'Heavy solid lines own lifetime; thin dashed lines are non-owning links.',
             'Dependency lines mean retirement propagates to users, not ownership of shared ingredients.',
             'Open heads denote one slot, potentially absent; filled heads denote many. Reverse links are listed below, not drawn twice.', '']
    for view in current['views']:
        guide += ['## ' + view['title'], '']
        for previous in view['previous_views']:
            v = next(v for v in old['views'] if v['id'] == previous)
            guide.append(f'Historical view: [{previous}]({previous}.svg), Chapter {v["chapter"]}.')
        for key in view['edges']:
            edge = edges[key]
            change = current['changes'].get(key)
            reason = change['reason'] if change else 'Unchanged context; authoritative introduction remains in its earlier chapter.'
            predecessors = ', '.join(change['previous']) if change and change['previous'] else 'none (new)' if change else key
            guide += ['', f'- **{key}**: `{edge["owner"]}.{edge["field"]}` → `{edge["child"]}`; {edge["kind"]}.',
                      f'  Reverse: `{edge["back"]}`. Predecessors: {predecessors}. {reason}']
    guide += ['', 'A tuple owns its occurrence links, not the shared component types. Component retirement retires dependent tuples; array-element retirement retires dependent array keys. Semantic type users are invalidated before their dependencies disappear. IR is retired first by semantic teardown; an editable IR type link can be detached without inventing another semantic owner.', '',
              'Initializer scopes are co-owned by their field and analysis. Their lexical parent is the enclosing declaration scope, not the struct member namespace. Generated initializer IR bodies remain program-owned and use the existing Declaration/IrBody link with FieldInitializer kind. Separate implementation syntax remains source-owned.', '']
    return '\n'.join(lines), '\n'.join(guide)


def prepare(root, old, current, edges):
    artifacts = render(current, edges)
    block, guide = teaching(old, current, edges)
    path = root / 'book/chapter-10.md'
    text = path.read_text()
    if BEGIN in text or END in text:
        g.require(text.count(BEGIN) == text.count(END) == 1, 'Damaged revision markers')
        start, rest = text.split(BEGIN)
        _, finish = rest.split(END)
        text = start.rstrip('\n') + '\n\n' + finish.lstrip('\n')
    title, rest = text.split('\n', 1)
    g.require(title.startswith('# Chapter 10 '), 'Wrong chapter')
    chapter = title + '\n\n' + block + '\n\n' + rest.lstrip('\n')
    files = {root / 'book/figures' / name: data for name, data in artifacts.items()}
    files[root / 'book/figures/ch10-relationships.md'] = guide.encode()
    files[path] = chapter.encode()
    for number in (4, 7, 8, 9):
        earlier = root / 'book' / f'chapter-{number:02d}.md'
        original = earlier.read_text()
        marker = '<!-- canonical-forward-link -->'
        notice = (marker + '\n**Later revision:** Chapter 10 replaces the flat type descriptor and class-only '
                  'type-user links with canonical identities, and extends scopes and body identities. '
                  '[See the explicitly revised model](chapter-10.md). This figure describes the model at this chapter.\n')
        if marker not in original:
            g.require(original.count(g.END) == 1, 'Missing historical diagram boundary')
            original = original.replace(g.END, g.END + '\n\n' + notice)
        else:
            g.require(original.count(marker) == 1 and notice in original, 'Edited forward-link notice')
        files[earlier] = original.encode()
    return files


def main():
    root = Path(__file__).resolve().parents[2]
    old, current, types, edges = load_sources(root, sys.argv[1])
    files = prepare(root, old, current, edges)
    print(f'Validated historical model and current {len(types)} types/{len(edges)} relationships; {len(current["views"])} revised views.')
    if '--publish' not in sys.argv[2:]:
        print('Validation/preview only; no book files changed.')
        preview = root / 'tools/common-diagrams/.build/revisions'
        preview.mkdir(exist_ok=True)
        for path, data in files.items():
            (preview / path.name).write_bytes(data)
        return
    # All validation/rendering completed first. Roll back on ordinary write failure.
    saved = {path: path.read_bytes() if path.exists() else None for path in files}
    try:
        for path, data in files.items():
            path.write_bytes(data)
    except BaseException:
        for path, data in saved.items():
            if data is None:
                path.unlink(missing_ok=True)
            else:
                path.write_bytes(data)
        raise
    for path, data in files.items():
        g.require(path.read_bytes() == data, f'Publication readback failed: {path}')
    print(f'Published and read back {len(files)} files; no historical figures rewritten.')


if __name__ == '__main__':
    main()
