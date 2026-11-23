#!/usr/bin/env python3
"""Public structured-IR inspection checks. Compiler command follows this script."""
import copy
import json
import re
from pathlib import Path
import subprocess
import sys
import tempfile

CASES = [
    ('constant', 'func value()->i32 { return 7 }', 'constant'),
    ('snapshot', 'func later()->i32 { return 2 }; func use(x:i32)->i32 { return x+later() }', 'snapshot'),
    ('call-order', 'func first()->i32 { return 1 }; func second()->i32 { return 2 }; func pair(a:i32,b:i32)->i32 { return a+b }; func use()->i32 { return pair(first(),second()) }', 'call-order'),
    ('short-and', 'func right()->bool { return true }; func use(b:bool)->bool { return b && right() }', 'short'),
    ('short-or', 'func right()->bool { return false }; func use(b:bool)->bool { return b || right() }', 'short'),
    ('value-if', 'func use(b:bool)->i32 { return if b { 3 } else { 4 } }', 'if'),
    ('else-if', 'func later()->bool { return true }; func use(b:bool)->i32 { return if b { 1 } else if later() { 2 } else { 3 } }', 'else-if'),
    ('loop', 'func use(n:i32)->i32 { var x:i32=0; while x<n { x+=1; if x==2 { continue }; if x==4 { break } }; return x }', 'loop'),
    ('compound', 'func later()->i32 { return 1 }; func use()->i32 { var x:i32=3; x+=later(); return x }', 'compound'),
    ('widen', 'func use(x:u8)->u32 { return x }', 'widen'),
    ('cast', 'func use(x:i32)->u8 { return x !as u8 }', 'cast'),
    ('unit', 'func use() {}', 'unit'),
]

def verify(doc, kind, name):
    assert doc['format'] == 'lyric-ir-1'
    functions = doc['functions']
    f = next((f for f in functions if f['name'] == 'use'), functions[0])
    nodes = {n['id']: n for n in f['nodes']}
    def children(n): return [nodes[i] for i in n['children']]
    def subtree(n):
        out = []
        pending = [n]
        while pending:
            item = pending.pop()
            out.append(item)
            pending.extend(reversed(children(item)))
        return out
    body = nodes[f['body']]
    ordered = children(body)
    for n in nodes.values():
        assert all(i in nodes for i in n['operands'] + n['children'])
        assert n['target'] is None or n['target'] in nodes
        assert n['offset'] is not None
    def of(k): return [n for n in nodes.values() if n['kind'] == k]
    if kind == 'constant':
        assert of('Constant')[0]['literal'] == '7'
        assert nodes[of('Return')[0]['operands'][0]]['kind'] == 'Constant'
    elif kind == 'snapshot':
        assert [n['kind'] for n in ordered].index('Load') < [n['kind'] for n in ordered].index('Call')
        binary = of('Binary')[0]
        assert [nodes[i]['kind'] for i in binary['operands']] == ['Load', 'Call']
    elif kind == 'call-order':
        assert [n['symbol'] for n in ordered if n['kind'] == 'Call'] == ['first', 'second', 'pair']
    elif kind == 'short':
        branch = of('If')[0]
        assert not any(n['kind'] == 'Call' for n in ordered)
        right_role = 'Then' if 'short-and' == name else 'Else'
        right = next(n for n in children(branch) if n['role'] == right_role)
        assert any(n['kind'] == 'Call' and n['symbol'] == 'right' for n in subtree(right))
        assert len(of('Yield')) == 2
    elif kind == 'if':
        assert {n['role'] for n in children(of('If')[0])} == {'Then', 'Else'}
        assert sorted(n['literal'] for n in of('Constant')) == ['3', '4']
        assert len(of('Yield')) == 2
    elif kind == 'else-if':
        outer = next(n for n in ordered if n['kind'] == 'If')
        alt = next(n for n in children(outer) if n['role'] == 'Else')
        assert any(n['kind'] == 'Call' and n['symbol'] == 'later' for n in subtree(alt))
        assert not any(n['kind'] == 'Call' for n in ordered)
    elif kind == 'loop':
        loop = of('While')[0]
        assert {n['role'] for n in children(loop)} == {'Condition', 'Body'}
        condition = next(n for n in children(loop) if n['role'] == 'Condition')
        assert children(condition)[-1]['kind'] == 'Yield'
        assert all(n['target'] == loop['id'] for n in of('Break') + of('Continue'))
        assert len(of('Break')) == len(of('Continue')) == 1
    elif kind == 'compound':
        call_pos = next(i for i,n in enumerate(ordered) if n['kind'] == 'Call')
        assert ordered[call_pos - 1]['kind'] == 'Load'
        binary = of('Binary')[0]
        assert nodes[binary['operands'][0]] is ordered[call_pos - 1]
    elif kind == 'widen':
        assert of('Convert')[0]['operator'] == 'widen' and of('Convert')[0]['type'] == 'u32'
    elif kind == 'cast':
        assert of('Convert')[0]['operator'] == '!as'
    elif kind == 'unit':
        assert ordered[-1]['kind'] == 'Return'

# Small independent evaluator of the published scalar inspection contract. It
# follows child order, not arena/ID order; each call gets distinct local storage.
class Transfer(Exception):
    def __init__(self, kind, value=None, target=None):
        self.kind, self.value, self.target = kind, value, target

def evaluate(doc, function_name, arguments):
    functions = {f['name']: f for f in doc['functions']}
    budget = [100000]
    def call(name, args):
        if name == 'assert':
            assert args == [True], 'IR assertion failed'
            return None
        function = functions[name]
        nodes = {n['id']: n for n in function['nodes']}
        values, storage = {}, {}
        parameters = iter(args)
        body = nodes[function['body']]
        # Parameter locals are the initial address definitions in a body.
        for ident in body['children'][:len(args)]:
            node = nodes[ident]
            assert node['kind'] == 'Local'
            storage[ident] = next(parameters)
        def region(ident):
            for child in nodes[ident]['children']:
                run(child)
        def run(ident):
            budget[0] -= 1
            assert budget[0] > 0, 'IR execution exceeded step budget'
            node = nodes[ident]
            kind = node['kind']
            args = [values[i] for i in node['operands']]
            if kind == 'Local':
                values[ident] = ident
                storage.setdefault(ident, None)
            elif kind == 'Constant':
                text = node['literal']
                values[ident] = None if text is None else (text == 'true' if text in ('true','false') else int(text))
            elif kind == 'Load':
                values[ident] = storage[args[0]]
                assert values[ident] is not None
            elif kind == 'Store':
                storage[args[0]] = args[1]
            elif kind == 'Call':
                values[ident] = call(node['symbol'], args)
            elif kind == 'Convert':
                value = args[0]
                if node['operator'] == '!as':
                    width = int(node['type'][1:])
                    value %= 1 << width
                    if node['type'][0] == 'i' and value >= 1 << (width-1):
                        value -= 1 << width
                values[ident] = value
            elif kind == 'Unary':
                values[ident] = {'!': lambda: not args[0], '-': lambda: -args[0],
                                 '!-': lambda: -args[0]}[node['operator']]()
            elif kind == 'Binary':
                a, b = args
                operations = {'+': lambda: a+b, '-': lambda: a-b, '*': lambda: a*b,
                              '==': lambda: a==b, '!=': lambda: a!=b,
                              '<': lambda: a<b, '<=': lambda: a<=b,
                              '>': lambda: a>b, '>=': lambda: a>=b}
                values[ident] = operations[node['operator']]()
            elif kind == 'If':
                role = 'Then' if args[0] else 'Else'
                arm = next(i for i in node['children'] if nodes[i]['role'] == role)
                try:
                    region(arm)
                except Transfer as transfer:
                    if transfer.kind != 'Yield':
                        raise
                    values[ident] = transfer.value
            elif kind == 'While':
                condition = next(i for i in node['children'] if nodes[i]['role'] == 'Condition')
                body_id = next(i for i in node['children'] if nodes[i]['role'] == 'Body')
                while True:
                    try:
                        region(condition)
                        raise AssertionError('Loop condition failed to yield')
                    except Transfer as transfer:
                        # Condition transfers obey the same loop target as body transfers.
                        if transfer.kind == 'Break' and transfer.target == ident:
                            break
                        if transfer.kind == 'Continue' and transfer.target == ident:
                            continue
                        if transfer.kind != 'Yield':
                            raise
                        if not transfer.value:
                            break
                    try:
                        region(body_id)
                    except Transfer as transfer:
                        if transfer.target != ident:
                            raise
                        if transfer.kind == 'Break':
                            break
                        assert transfer.kind == 'Continue'
            elif kind in ('Yield','Return','Break','Continue'):
                raise Transfer(kind, args[0] if args else None, node['target'])
            else:
                raise AssertionError('Unimplemented grader operation: '+kind)
        try:
            region(function['body'])
        except Transfer as transfer:
            assert transfer.kind == 'Return'
            return transfer.value
        raise AssertionError('Function fell through without return')
    return call(function_name, arguments)

def execute_cases(doc, name):
    expectations = {
        'constant': [('value', [], 7)],
        'snapshot': [('use', [5], 7)],
        'call-order': [('use', [], 3)],
        'short-and': [('use', [False], False), ('use', [True], True)],
        'short-or': [('use', [False], False), ('use', [True], True)],
        'value-if': [('use', [False], 4), ('use', [True], 3)],
        'else-if': [('use', [False], 2), ('use', [True], 1)],
        'loop': [('use', [0], 0), ('use', [3], 3), ('use', [10], 4)],
        'condition-break': [('use', [], 2)],
        'condition-continue': [('use', [], 2)],
        'condition-return': [('use', [], 9)],
        'compound': [('use', [], 4)],
        'widen': [('use', [255], 255)],
        'cast': [('use', [-1], 255)],
        'unit': [('use', [], None)],
        'branch-return': [('use', [True], 5), ('use', [False], 6)],
        'recursive': [('use', [6], 720)],
        'skipped-call': [('use', [False], False)],
        'assert': [('use', [], None)],
        'deep': [('use', [2], 1502)],
    }
    for function, arguments, expected in expectations[name]:
        assert evaluate(doc, function, arguments) == expected, (name, arguments, expected)

CASES.extend([
    ('condition-break', 'func use()->i32 { var x:i32=0; while if x==2 { break } else { true } { x+=1 }; return x }', 'execution'),
    ('condition-continue', 'func use()->i32 { var x:i32=0; var bodies:i32=0; while if x<2 { x+=1; continue } else { x<4 } { bodies+=1; x+=1 }; return bodies }', 'execution'),
    ('condition-return', 'func use()->i32 { while if true { return 9 } else { false } {}; return 0 }', 'execution'),
    ('branch-return', 'func use(b:bool)->i32 { if b { return 5 }; return 6 }', 'execution'),
    ('recursive', 'func use(n:i32)->i32 { if n==0 { return 1 }; return n*use(n-1) }', 'execution'),
    ('skipped-call', 'func fail()->bool { assert(false); return true }; func use(b:bool)->bool { return b && fail() }', 'execution'),
    ('assert', 'func use() { assert(true) }', 'execution'),
    ('deep', 'func use(x:i32)->i32 { return x' + '+1'*1500 + ' }', 'execution'),
])

# Optional milestone-only checks are not retained language prohibitions.
milestone = '--scalar-milestone' in sys.argv
compiler = [arg for arg in sys.argv[1:] if arg != '--scalar-milestone']
failed = 0
self_checks = set()
with tempfile.TemporaryDirectory(prefix='lyric-ir-grader-') as directory:
    path = Path(directory) / 'case.ly'
    for current, text, kind in CASES:
        path.write_text(text)
        try:
            result = subprocess.run(compiler + ['--inspect-ir', str(path)], capture_output=True, text=True, timeout=30)
            assert result.returncode == 0, result.stderr
            assert result.stderr == ''
            doc = json.loads(result.stdout)
            verify(doc, kind, current)
            execute_cases(doc, current)
            # Verify that the grader rejects a plausible lowering defect rather
            # than merely accepting any well-formed graph with the right tags.
            if current == 'snapshot':
                broken = copy.deepcopy(doc)
                function = next(f for f in broken['functions'] if f['name'] == 'use')
                nodes = {n['id']: n for n in function['nodes']}
                order = nodes[function['body']]['children']
                left = next(i for i,n in enumerate(order) if nodes[n]['kind'] == 'Load')
                right = next(i for i,n in enumerate(order) if nodes[n]['kind'] == 'Call')
                order[left], order[right] = order[right], order[left]
                try:
                    verify(broken, kind, current)
                except AssertionError:
                    self_checks.add('snapshot-order')
                else:
                    raise AssertionError('Grader accepted reordered snapshot')
            print('PASS', current)
        except (AssertionError, KeyError, ValueError, StopIteration, subprocess.TimeoutExpired) as error:
            failed += 1
            print('FAIL', current, str(error))
    negative = [
        ('semantic-first', 'func use()->i32 { return missing }', 'LYC301'),
    ]
    for current, text, code in negative:
        path.write_text(text)
        result = subprocess.run(compiler + ['--inspect-ir', str(path)], capture_output=True, text=True, timeout=30)
        match = re.fullmatch(r'".*":(\d+):(\d+): error (LYC\d+): (.*) \(byte (\d+)\)\n', result.stderr)
        message = ("Unknown name 'missing'." if code == 'LYC301' else
                   'This construct is not supported by the current IR lowering slice.')
        ok = result.returncode == 1 and result.stdout == '' and match is not None
        if match is not None:
            line, column, actual_code, actual_message, offset = match.groups()
            prefix = text.encode()[:int(offset)].decode()
            ok = ok and actual_code == code and actual_message == message
            ok = ok and 0 <= int(offset) < len(text.encode())
            ok = ok and int(line) == prefix.count('\n') + 1
            ok = ok and int(column) == len(prefix.rsplit('\n', 1)[-1]) + 1
        print('PASS' if ok else 'FAIL', current, '' if ok else result.stderr)
        failed += not ok
assert self_checks == {'snapshot-order'}
print('PASS grader snapshot-order self-check')
total = len(CASES) + len(negative)
print(f'{total-failed}/{total} IR cases passed')
sys.exit(bool(failed))
