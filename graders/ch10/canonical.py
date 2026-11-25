#!/usr/bin/env python3
"""Retained Chapter 10 canonical-type inspection checks (independent Python)."""
import copy
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile


def verify(document):
    """Check the published schema, complete-key uniqueness, and identity links."""
    assert document['format'] == 'lyric-ir-1'
    records = document['types']
    assert isinstance(records, list), 'types must be a list'
    # Validate before dictionary lookup: bool is an int subclass in Python but
    # is not a JSON integer ID, and malformed links must not masquerade as IDs.
    for record in records:
        assert type(record['id']) is int, 'type ID must be an integer'
        for field in ('elementTypeId', 'classId'):
            value = record[field]
            assert value is None or type(value) is int, field + ' must be integer or null'
        components = record['componentTypeIds']
        assert isinstance(components, list), 'componentTypeIds must be a list'
        assert all(type(value) is int for value in components), 'component IDs must be integers'
    types = {record['id']: record for record in records}
    assert len(types) == len(records), 'duplicate type ID'
    keys = set()
    for record in records:
        kind = record['kind']
        element = record['elementTypeId']
        declaration = record['classId']
        scalar = record['scalar']
        if kind == 'Scalar':
            assert element is None and declaration is None and isinstance(scalar, str)
            key = (kind, scalar)
            assert record['name'] == scalar.lower()
        elif kind == 'Array':
            assert element in types and declaration is None and scalar is None
            key = (kind, element)
            assert record['name'] == '[' + types[element]['name'] + ']'
        elif kind == 'Tuple':
            components = record['componentTypeIds']
            assert components and all(component in types for component in components)
            assert element is None and declaration is None and scalar is None
            key = (kind, tuple(components))
            names = [types[component]['name'] for component in components]
            spelling = ', '.join(names) + (',' if len(names) == 1 else '')
            assert record['name'] == '(' + spelling + ')'
        else:
            assert kind == 'Nominal' and declaration is not None
            assert element is None and scalar is None
            key = (kind, declaration)
        assert key not in keys, 'duplicate canonical key'
        keys.add(key)
        if kind != 'Tuple':
            assert record['componentTypeIds'] == []
    # Component graphs are DAGs. Shared identities and repeated positions are
    # legal; returning to an active ancestor is not.
    complete = set()
    active = set()
    for identity in types:
        pending = [(identity, False)]
        while pending:
            current, leaving = pending.pop()
            if leaving:
                active.remove(current)
                complete.add(current)
                continue
            assert current not in active, 'cyclic component graph'
            if current in complete:
                continue
            active.add(current)
            pending.append((current, True))
            record = types[current]
            children = ([record['elementTypeId']] if record['kind'] == 'Array'
                        else record['componentTypeIds'])
            pending.extend((child, False) for child in reversed(children))
    used = set()
    for function in document['functions']:
        for node in function['nodes']:
            identity = node['typeId']
            assert identity is None or type(identity) is int, 'node typeId must be integer or null'
            declaration = node['classId']
            assert declaration is None or type(declaration) is int, 'node classId must be integer or null'
            if node['type'] is None:
                assert identity is None
                continue
            assert identity in types, 'missing type record'
            record = types[identity]
            assert node['type'] == record['name'], 'wrong node type identity'
            pending = [identity]
            while pending:
                current = pending.pop()
                if current in used:
                    continue
                used.add(current)
                component = types[current]
                if component['kind'] == 'Array':
                    pending.append(component['elementTypeId'])
                else:
                    pending.extend(component['componentTypeIds'])
            while record['kind'] == 'Array':
                record = types[record['elementTypeId']]
            assert node['classId'] == record['classId']
    assert used == set(types), 'unreferenced inspection records'
    return types


def selfcheck():
    good = {'format': 'lyric-ir-1', 'types': [
        {'id': 4, 'kind': 'Scalar', 'scalar': 'I32', 'name': 'i32',
         'elementTypeId': None, 'classId': None, 'componentTypeIds': []},
        {'id': 9, 'kind': 'Array', 'scalar': None, 'name': '[i32]',
         'elementTypeId': 4, 'classId': None, 'componentTypeIds': []}],
        'functions': [{'nodes': [{'typeId': 9, 'type': '[i32]', 'classId': None}]}]}
    verify(good)
    broken = []
    duplicate = copy.deepcopy(good)
    duplicate['types'].append(dict(duplicate['types'][0], id=12))
    broken.append(duplicate)
    missing = copy.deepcopy(good)
    missing['types'][1]['elementTypeId'] = 77
    broken.append(missing)
    wrong = copy.deepcopy(good)
    wrong['functions'][0]['nodes'][0]['typeId'] = 4
    broken.append(wrong)
    tuple_document = copy.deepcopy(good)
    tuple_document['types'].append({
        'id': 12, 'kind': 'Tuple', 'scalar': None, 'name': '(i32, i32)',
        'elementTypeId': None, 'classId': None, 'componentTypeIds': [4, 4]})
    tuple_document['functions'][0]['nodes'].append(
        {'typeId': 12, 'type': '(i32, i32)', 'classId': None})
    verify(tuple_document)
    wrong_tuple = copy.deepcopy(tuple_document)
    wrong_tuple['types'][-1]['componentTypeIds'] = [4, 9]
    broken.append(wrong_tuple)

    # Keep valid examples complete, so malformed controls test the declared
    # schema rather than relying on permissive defaults in the validator.
    nominal = {'format': 'lyric-ir-1', 'types': [
        {'id': 4, 'kind': 'Nominal', 'scalar': None, 'name': 'Point',
         'elementTypeId': None, 'classId': 8, 'componentTypeIds': []}],
        'functions': [{'nodes': [{'typeId': 4, 'type': 'Point', 'classId': 8}]}]}
    verify(nominal)
    missing_components = copy.deepcopy(good)
    del missing_components['types'][0]['componentTypeIds']
    broken.append(missing_components)
    null_components = copy.deepcopy(good)
    null_components['types'][0]['componentTypeIds'] = None
    broken.append(null_components)
    for malformed in ('4', True):
        # Rewrite matching links too: internal consistency cannot excuse a
        # wrong ID type. These controls include Python's bool/int equality trap.
        wrong_id = copy.deepcopy(good)
        wrong_id['types'][0]['id'] = malformed
        wrong_id['types'][1]['elementTypeId'] = malformed
        broken.append(wrong_id)
        wrong_element = copy.deepcopy(good)
        wrong_element['types'][1]['elementTypeId'] = malformed
        broken.append(wrong_element)
        wrong_component = copy.deepcopy(tuple_document)
        wrong_component['types'][-1]['componentTypeIds'][0] = malformed
        broken.append(wrong_component)
        wrong_declaration = copy.deepcopy(nominal)
        wrong_declaration['types'][0]['classId'] = malformed
        wrong_declaration['functions'][0]['nodes'][0]['classId'] = malformed
        broken.append(wrong_declaration)
        wrong_node = copy.deepcopy(good)
        wrong_node['functions'][0]['nodes'][0]['typeId'] = malformed
        broken.append(wrong_node)
        wrong_node_declaration = copy.deepcopy(nominal)
        wrong_node_declaration['functions'][0]['nodes'][0]['classId'] = malformed
        broken.append(wrong_node_declaration)
    for document in broken:
        try:
            verify(document)
        except (AssertionError, KeyError):
            continue
        raise AssertionError('selfcheck accepted malformed type document')
    return len(broken)


def main():
    defects = selfcheck()
    if sys.argv[1:] == ['--selfcheck']:
        print(f'PASS canonical inspection selfcheck ({defects} defects)')
        return
    command = sys.argv[1:]
    if not command:
        raise SystemExit('usage: canonical.py COMPILER [ARGS...] | --selfcheck')
    classes = '\n'.join(
        f'class C{i} {{ n:i32; func C{i}(self) {{ self.n={i} }} }}; '
        f'func make{i}()->C{i} {{ return C{i}() }}'
        for i in range(100))
    cases = [
        ('canonical-scalars', 'func one(x:i32)->i32 { return x }; func two(x:i32)->i32 { return x }; func wide(x:u8)->u64 { return x }', {'i32', 'u8', 'u64'}),
        ('canonical-arrays', 'func one(x:[[i32]])->[[i32]] { return x }; func two(x:[i32])->[i32] { return x }', {'i32', '[i32]', '[[i32]]'}),
        ('canonical-nominal-growth', classes, {f'C{i}' for i in range(100)}),
        ('canonical-tuples', 'func one(x:(i32,i32))->(i32,i32) { return x }; func two(x:((bool,i32),(i32,bool)))->((bool,i32),(i32,bool)) { return x }', {'(i32, i32)', '(bool, i32)', '(i32, bool)', '((bool, i32), (i32, bool))'}),
    ]
    with tempfile.TemporaryDirectory(prefix='lyric-ch10-') as directory:
        path = Path(directory) / 'case.ly'
        for name, source, expected in cases:
            path.write_text(source, encoding='utf-8')
            result = subprocess.run(command + ['--inspect-ir', str(path)],
                                    capture_output=True, text=True, timeout=60)
            assert result.returncode == 0, (name, result.stderr)
            assert result.stderr == '', (name, result.stderr)
            types = verify(json.loads(result.stdout))
            names = [record['name'] for record in types.values()]
            assert expected <= set(names), (name, expected - set(names))
            assert all(names.count(item) == 1 for item in expected), name
            print('PASS', name)
        path.write_text('func broken()->i32 { return missing() }', encoding='utf-8')
        result = subprocess.run(command + ['--inspect-ir', str(path)],
                                capture_output=True, text=True, timeout=60)
        assert result.returncode == 1 and result.stdout == '', result
        assert re.search(r':\d+:\d+:.*LYC301', result.stderr), result.stderr
        print('PASS canonical-invalid-source')
    print(f'5/5 canonical cases passed; selfcheck rejected {defects} defects')


if __name__ == '__main__':
    main()
