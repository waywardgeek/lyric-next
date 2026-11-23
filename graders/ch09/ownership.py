#!/usr/bin/env python3
"""Independent ownership execution checks for the public IR inspection stream.

The small evaluator counts live allocations and rejects use-after-transfer,
leaks, and replacement without release. It does not execute production C.
"""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile

CLASS = """class Box {
    n:i32
    func Box(self, n:i32) { self.n=n }
    func read(self)->i32 { return self.n }
    func add(self, n:i32) { self.n=self.n+n }
}
"""

COUNTED = """value class Counter {
    n:i32
    func Counter(self, n:i32) { self.n=n }
    func read(self)->i32 { return self.n }
    final func finish(self) { let n=self.n }
}
"""

CASES = [
    ('array-condition-eager-later-break', 'func yes(a:[i32])->bool { return a[0]>0 }; func use()->i32 { var n=0; while (if n==2 { break } else { true }) == yes([1]) { n+=1 }; return n }', 2),
    ('array-condition-eager-continue', 'func yes(a:[i32])->bool { return a[0]>0 }; func use()->i32 { var n=0; while (if n<2 { n+=1; continue } else { false }) == yes([1]) {}; return n }', 2),
    ('array-condition-eager-bypass', 'func yes(a:[i32])->bool { return a[0]>0 }; func use()->i32 { var n=0; while (if n==0 { break } else { true }) == yes([1]) { n+=1 }; return n }', 0),
    ('array-condition-bypass', 'func yes(a:[i32])->bool { return a[0]>0 }; func use()->i32 { var n=0; while (if n==0 { break } else { true }) && yes([1]) { n+=1 }; return n }', 0),
    ('array-condition-break', 'func use()->i32 { var n=0; while if true { let a=[7]; n=a[0]; break } else { false } {}; return n }', 7),
    ('array-condition-continue', 'func use()->i32 { var n=0; while if n<2 { let a=[1]; n+=a[0]; continue } else { false } {}; return n }', 2),
    ('array-condition-return', 'func use()->i32 { while if true { let a=[9]; return a[0] } else { false } {}; return 0 }', 9),
    ('counted-object-array-push', COUNTED+'func use()->i32 { let c=Counter(7); var a:[Counter]=[]; a.reserve(4); a.push(c); a.resize(0); return c.n }', 7),
    ('counted-object-array-replace', COUNTED+'func use()->i32 { var a=[Counter(7)]; a[0]=Counter(9); return a[0].n }', 9),
    ('counted-object-array-zero', COUNTED+'func use()->i32 { var a=[Counter(7)]; a.resize(0); return 7 }', 7),
    ('class-object-array', CLASS+'func use()->i32 { let b=Box(7); var a=[b]; a.truncate(0); let n=b.n; b.destroy(); return n }', 7),
    ('counted-object-array-copy', COUNTED+'func use()->i32 { let a=[Counter(7)]; let b=a; b[0].n=9; return a[0].n }', 9),
    ('counted-object-array-nested', COUNTED+'func use()->i32 { let a=[[Counter(7)]]; let b=a; return b[0][0].n }', 7),
    ('counted-object-array-remove', COUNTED+'func use()->i32 { var a=[Counter(7)]; a.truncate(0); return 7 }', 7),
    ('counted-object-array-return', COUNTED+'func make()->[Counter] { return [Counter(8)] }; func use()->i32 { let a=make(); return a[0].n }', 8),
    ('counted-branch', COUNTED+'func choose(b:bool)->Counter { return if b { Counter(3) } else { Counter(8) } }; func use()->i32 { let a=choose(false); return a.n }', 8),
    ('counted-loop', COUNTED+'func use()->i32 { var n=0; while n<3 { let a=Counter(n); n+=1 }; return n }', 3),
    ('counted-reference', COUNTED+'func replace(a:ref Counter) { a=Counter(9) }; func use()->i32 { var a=Counter(7); replace(ref a); return a.n }', 9),
    ('counted-array-field', 'value class C { a:[i32]; func C(self) { self.a=[7] }; final func finish(self) { let n=self.a[0] } }; func use()->i32 { let a=C(); let b=a; return b.a[0] }', 7),
    ('counted-alias', COUNTED+'func use()->i32 { let a=Counter(7); let b=a; b.n=9; return a.n }', 9),
    ('counted-replace', COUNTED+'func use()->i32 { var a=Counter(7); a=Counter(9); return a.n }', 9),
    ('counted-return', COUNTED+'func make()->Counter { return Counter(8) }; func use()->i32 { let a=make(); return a.n }', 8),
    ('counted-method', COUNTED+'func use()->i32 { let a=Counter(7); return a.read() }', 7),
    ('class-array-fields', 'class C { a:[i32]; b:[[i32]]; func C(self) { self.a=[3]; self.b=[[4]] } }; func use()->i32 { let c=C(); let n=c.a[0]+c.b[0][0]; c.destroy(); return n }', 7),
    ('class-array-copy', 'class C { a:[i32]; func C(self) { self.a=[7,8] } }; func use()->i32 { let c=C(); var a=c.a; a[0]=2; let n=c.a[0]+a[0]; c.destroy(); return n }', 9),
    ('class-array-nested', 'class C { a:[[i32]]; func C(self) { self.a=[[7],[8]] } }; func use()->i32 { let c=C(); c.a=[[3],[4]]; let n=c.a[1][0]; c.destroy(); return n }', 4),
    ('class-final-array', 'class C { a:[i32]; func C(self) { self.a=[7] }; final func finish(self) { let n=self.a[0]; self.a=[n+1] } }; func use()->i32 { let c=C(); c.destroy(); return 7 }', 7),
    ('class-final', 'class C { n:i32; func C(self) { self.n=7 }; final func finish(self) { self.n=self.n+1 } }; func use()->i32 { let c=C(); c.destroy(); return 7 }', 7),
    ('class-final-growth', 'class C { n:i32; func C(self) { self.n=7 }; final func finish(self) { grow(); let n=self.n } }; func grow() { let extra=C() }; func use()->i32 { let c=C(); c.destroy(); return 7 }', 7),
    ('class-method-receiver', CLASS+'func change(slot:ref Box, other:Box)->i32 { slot=other; return 11 }; func use()->i32 { let first=Box(7); let other=Box(3); var selected=first; selected.add(change(ref selected,other)); return first.n+other.n }', 21),
    ('class-constructor-argument', CLASS+'func tick(n:ref i32)->i32 { n+=1; return n }; func use()->i32 { var n=0; let b=Box(tick(ref n)); let result=n+b.n; b.destroy(); return result }', 2),
    ('class-method-array', 'class Pack { n:i32; func Pack(self) { self.n=4 }; func values(self, a:[i32])->[i32] { return [self.n,a[0]] } }; func use()->i32 { let p=Pack(); let a=p.values([7]); let result=a[0]+a[1]; p.destroy(); return result }', 11),
    ('class-constructor', CLASS+'func use()->i32 { let b=Box(7); let n=b.n; b.destroy(); return n }', 7),
    ('class-alias', CLASS+'func use()->i32 { let b=Box(7); let a=b; a.n=9; let n=b.n; b.destroy(); return n }', 9),
    ('class-method', CLASS+'func use()->i32 { let b=Box(7); b.add(2); let n=b.read(); b.destroy(); return n }', 9),
    ('class-return', CLASS+'func make()->Box { return Box(8) }; func use()->i32 { let b=make(); let n=b.n; b.destroy(); return n }', 8),
    ('class-field-ref', CLASS+'func set(n:ref i32) { n=12 }; func use()->i32 { let b=Box(7); set(ref b.n); let n=b.n; b.destroy(); return n }', 12),
    ('class-growth-store', CLASS+'func grow()->i32 { let extra=Box(3); return 11 }; func use()->i32 { let b=Box(7); b.n=grow(); let n=b.n; b.destroy(); return n }', 11),
    ('class-growth-snapshot', CLASS+'func grow()->i32 { let extra=Box(3); return 11 }; func use()->i32 { let b=Box(7); b.n=b.n+grow(); let n=b.n; b.destroy(); return n }', 18),
    ('class-receiver-snapshot', CLASS+'func change(slot:ref Box, other:Box)->i32 { slot=other; return 11 }; func use()->i32 { let first=Box(7); let other=Box(3); var selected=first; selected.n=change(ref selected,other); return first.n+other.n }', 14),
    ('class-method-identities', CLASS+'class Other { n:i32; func Other(self) { self.n=20 }; func read(self)->i32 { return self.n } }; func use()->i32 { let b=Box(7); let o=Other(); return b.read()+o.read() }', 27),
    ('class-branch', CLASS+'func choose(b:bool)->Box { return if b { Box(4) } else { Box(9) } }; func use()->i32 { let b=choose(false); let n=b.read(); b.destroy(); return n }', 9),
    ('string-loop', 'func use()->u64 { var n:u64=0; while n<3 { var a="ok"; a[0]=110u8; n+=1 }; return n }', 3),
    ('string-bytes', 'func use()->u8 { let a="A\\0B\\xFF"; return a[1]+a[3] }', 255),
    ('string-utf8', 'func use()->u64 { return len("é") }', 2),
    ('string-empty', 'func use()->u64 { return capacity("") }', 0),
    ('string-copy', 'func use()->u8 { let a="ok"; var b=a; b[0]=110u8; return a[0] }', 111),
    ('string-return', 'func make()->string { return "ok" }; func use()->u8 { let a=make(); return a[1] }', 107),
    ('string-nested', 'func use()->u8 { var a=["ok"]; a[0][0]=110u8; return a[0][0] }', 110),
    ('capacity-reserve', 'func use()->bool { var a=[1]; a.reserve(20); return capacity(a)>=20 }', True),
    ('capacity-copy', 'func use()->u64 { var a=[1]; a.reserve(20); let b=a; return capacity(b) }', 1),
    ('capacity-truncate', 'func use()->bool { var a=[1]; a.reserve(20); let before=capacity(a); a.truncate(0); return capacity(a)==before }', True),
    ('capacity-resize-zero', 'func use()->u64 { var a=[1]; a.reserve(20); a.resize(0); a.reserve(8); a.resize(0); return capacity(a) }', 0),
    ('capacity-nested', 'func use()->bool { var a=[[1]]; a[0].reserve(20); return capacity((a[0]))>=20 }', True),
    ('capacity-computed', 'func make()->[i32] { var a=[1]; a.reserve(20); return a }; func use()->u64 { return capacity(make()) }', 1),
    ('push', 'func use()->i32 { var a=[1]; a.push(7); return a[1] }', 7),
    ('push-copy', 'func use()->i32 { var a=[[1]]; var b=[7]; a.push(b); b[0]=9; return a[1][0] }', 7),
    ('reserve', 'func use()->u64 { var a=[1]; a.reserve(20); return len(a) }', 1),
    ('resize', 'func use()->i32 { var a=[1]; a.resize(3); return a[2] }', 0),
    ('shrink', 'func use()->u64 { var a=[[1],[2]]; a.resize(1); return len(a) }', 1),
    ('truncate', 'func use()->u64 { var a=[[1],[2]]; a.truncate(1); a.truncate(8); return len(a) }', 1),
    ('resize-zero', 'func use()->u64 { var a=[[1]]; a.resize(0); a.reserve(8); a.resize(0); return len(a) }', 0),
    ('mutate-ref', 'func append(a:ref [i32]) { a.push(8) }; func use()->i32 { var a=[1]; append(ref a); return a[1] }', 8),
    ('mutate-nested', 'func use()->i32 { var a=[[1]]; a[0].push(8); return a[0][1] }', 8),
    ('ref-local', 'func use()->i32 { var n=1; let slot=ref n; slot=2; return n }', 2),
    ('ref-forward', 'func inc(n:ref i32) { n+=1 }; func use()->i32 { var n=1; let a=ref n; let b=ref a; inc(ref b); return n }', 2),
    ('ref-array', 'func replace(a:ref [i32]) { a=[8,9] }; func use()->i32 { var a=[1]; replace(ref a); return a[1] }', 9),
    ('ref-copy', 'func use()->i32 { var a=[1]; let alias=ref a; let b=alias; alias[0]=9; return b[0] }', 1),
    ('ref-return', 'func snapshot(a:ref [[i32]])->[[i32]] { return a }; func use()->i32 { var a=[[1]]; let b=snapshot(ref a); a[0][0]=9; return b[0][0] }', 1),
    ('ref-index-order', 'func change(i:ref i32)->i32 { i=1; return 9 }; func set(a:ref i32,v:i32) { a=v }; func use()->i32 { var a=[1,2]; var i=0; set(ref a[i],change(ref i)); return a[0]+a[1] }', 11),
    ('index-order', 'func rhs()->i32 { return 9 }; func use()->i32 { var a=[4,7]; a[1]=rhs(); return a[1] }', 9),
    ('index-read', 'func use()->i32 { let a=[4,7]; return a[1] }', 7),
    ('index-write', 'func use()->i32 { var a=[4,7]; a[1]=9; return a[1] }', 9),
    ('index-copy', 'func use()->i32 { var a=[4,7]; let b=a; a[0]=9; return b[0] }', 4),
    ('index-nested', 'func use()->i32 { var a=[[4,7],[3]]; a[0][1]=9; return a[0][1] }', 9),
    ('index-replace', 'func use()->u64 { var a=[[4,7],[3]]; a[0]=[8]; return len(a[0]) }', 1),
    ('index-call', 'func index()->u64 { return 1 }; func use()->i32 { var a=[4,7]; a[index()]=9; return a[index()] }', 9),
    ('literal', 'func use()->u64 { let a=[1,2]; return len(a) }', 2),
    ('copy', 'func use()->u64 { let a=[1,2]; let b=a; return len(b) }', 2),
    ('replace', 'func use()->u64 { var a=[1,2]; a=[3]; return len(a) }', 1),
    ('return', 'func make()->[i32] { return [1,2,3] }; func use()->u64 { let a=make(); return len(a) }', 3),
    ('parameter', 'func size(a:[i32])->u64 { return len(a) }; func use()->u64 { let a=[1,2]; return size(a) }', 2),
    ('nested', 'func use()->u64 { let a=[[1,2],[3]]; let b=a; return len(b) }', 2),
    ('branch', 'func choose(b:bool)->[i32] { return if b { [1,2] } else { [3] } }; func use()->u64 { let a=choose(true); let b=choose(false); return len(a)+len(b) }', 3),
    ('early-return', 'func choose(b:bool)->[i32] { let a=[1,2]; if b { let unused=[3]; return a }; return [4] }; func use()->u64 { let a=choose(true); let b=choose(false); return len(a)+len(b) }', 3),
    ('loop-repeat', 'func use()->u64 { var i:u64=0; while i<3 { let a=[1,2]; i+=len(a) }; return i }', 4),
    ('loop-break', 'func use()->u64 { while true { let a=[1,2]; break }; return 7 }', 7),
    ('loop-continue', 'func use()->u64 { var i:u64=0; while i<3 { let a=[1]; i+=len(a); continue }; return i }', 3),
    ('loop-return', 'func use()->u64 { let outer=[1]; while len([1,2])>0 { let a=[3]; return len(a)+len(outer) }; return 0 }', 2),
    ('loop-nested-return', 'func use()->u64 { while len([1])>0 { let a=[2]; while len([3])>0 { let b=[4]; if true { return len(a)+len(b) } }; break }; return 0 }', 2),
    ('loop-false', 'func use()->u64 { while len([1])==0 { let a=[2]; break }; return 5 }', 5),
    ('loop-nested', 'func use()->u64 { var i:u64=0; while i<2 { let a=[1]; var j:u64=0; while len([2])>j { let b=[3]; j+=1; continue }; i+=len(a) }; return i }', 2),
]

class Exit(Exception):
    def __init__(self, kind, value):
        self.kind = kind
        self.value = value


def execute(document, expected_objects=None, expected_hooks=None):
    functions = {f['symbolId']: f for f in document['functions'] if f['kind'] == 'Source'}
    destructors = {f['symbolId']: f for f in document['functions'] if f['kind'] == 'Destruction'}
    objects = {}
    references = {}
    generations = {}
    object_serial = 0
    heap = {}
    capacities = {}
    array_leaves = {}
    serial = 0
    copies = 0
    hook_calls = 0
    steps = 0

    def allocate(items, depth, nominal=None, counted=False):
        nonlocal serial
        serial += 1
        heap[serial] = (items, depth)
        capacities[serial] = len(items)
        array_leaves[serial] = (nominal, counted)
        return serial

    def clone(handle):
        nonlocal copies
        if handle is None:
            return None
        assert handle in heap, 'copy of dead storage'
        copies += 1
        items, depth = heap[handle]
        nominal, counted = array_leaves[handle]
        if depth > 1:
            copied = [clone(x) for x in items]
        else:
            copied = list(items)
            if counted:
                for item in copied:
                    assert references.get(item, 0) > 0, 'array copy retained dead object'
                    references[item] += 1
        return allocate(copied, depth, nominal, counted)

    def release(handle):
        if handle is None:
            return
        assert handle in heap, 'double free or invalid holder'
        items, depth = heap.pop(handle)
        del capacities[handle]
        nominal, counted = array_leaves.pop(handle)
        if depth > 1:
            for item in items:
                release(item)
        elif counted:
            for item in items:
                release_object(item)

    def release_object(handle):
        assert references.get(handle, 0) > 0, 'release dead object'
        references[handle] -= 1
        if references[handle] == 0:
            call(objects[handle][0], [handle], destruction=True)

    def call(symbol, actuals, destruction=False):
        nonlocal hook_calls
        function = (destructors if destruction else functions)[symbol]
        if not destruction and function['name'] == 'finish':
            hook_calls += 1
        nodes = {n['id']: n for n in function['nodes']}
        values = {}
        locals_ = {}
        addresses = {}
        parameters = iter(actuals)
        parameter_ids = set(nodes[function['body']]['children'][:len(actuals)])

        def field_cell(ident):
            # A Place resolves against the current class storage; AddressOf freezes
            # its generation. Growth invalidates fixed addresses, never handles.
            reference = addresses[ident]
            handle, field = reference[:2]
            assert handle in objects, 'field access on destroyed object'
            nominal, fields = objects[handle]
            if len(reference) == 3:
                assert reference[2] == generations[nominal], 'stale captured field address'
            return fields, field

        def get(ident):
            node = nodes[ident]
            if node['kind'] in ('SelectField', 'AddressOf'):
                fields, field = field_cell(ident)
                # Allocation supplies empty array descriptors before constructor
                # stores. Scalar initialization remains mandatory and observable.
                if field not in fields and '[' in node['type']:
                    return None
                assert field in fields, 'read of uninitialized field'
                return fields[field]
            if node['result'] != 'Address':
                return values[ident]
            if node['kind'] == 'Borrow':
                return addresses[ident][0]()
            if node['kind'] == 'Local':
                return locals_[ident]
            handle, index = addresses[ident]
            assert handle in heap, 'read through dead element address'
            return heap[handle][0][index]

        def put(ident, value):
            if nodes[ident]['kind'] in ('SelectField', 'AddressOf'):
                fields, field = field_cell(ident)
                fields[field] = value
            elif nodes[ident]['result'] != 'Address':
                values[ident] = value
            elif nodes[ident]['kind'] == 'Borrow':
                addresses[ident][1](value)
            elif nodes[ident]['kind'] == 'Local':
                locals_[ident] = value
            else:
                handle, index = addresses[ident]
                assert handle in heap, 'write through dead element address'
                heap[handle][0][index] = value

        def clear(ident):
            put(ident, None)

        def region(ident):
            for child in nodes[ident]['children']:
                run(child)

        def run(ident):
            nonlocal steps, object_serial
            steps += 1
            assert steps < 10000, 'execution step budget exceeded'
            n = nodes[ident]
            kind = n['kind']
            operands = n['operands']
            if n['type'] and ('[' in n['type'] or n['counted']) and kind in ('Local', 'Copy', 'Ref', 'NewObject', 'NewArray', 'Call', 'If'):
                previous = locals_.get(ident) if kind == 'Local' else values.get(ident)
                assert previous is None, 'iteration overwrote an owning holder'
            if kind == 'Local':
                locals_[ident] = next(parameters) if ident in parameter_ids else None
            elif kind == 'Borrow':
                if ident in parameter_ids:
                    addresses[ident] = next(parameters)
                else:
                    source = operands[0]
                    addresses[ident] = (lambda: get(source), lambda value: put(source, value))
            elif kind == 'NewObject':
                object_serial += 1
                nominal = n['symbolId']
                assert nominal == n['classId'], 'allocation class/type mismatch'
                generations[nominal] = generations.get(nominal, 0) + 1
                objects[object_serial] = (nominal, {})
                values[ident] = object_serial
                if n['counted']:
                    references[object_serial] = 1
            elif kind == 'DestroyObject':
                values[ident] = call(n['symbolId'], [get(operands[0])], destruction=True)
            elif kind == 'ReclaimObject':
                assert function['kind'] == 'Destruction', 'reclamation outside destruction body'
                assert function['symbolId'] == n['symbolId'], 'wrong generated body'
                handle = get(operands[0])
                assert handle in objects, 'double destruction or invalid object'
                assert objects[handle][0] == n['symbolId'], 'wrong destruction class'
                assert references.get(handle, 0) == 0, 'reclaim with live counted holders'
                references.pop(handle, None)
                del objects[handle]
                values[ident] = None
            elif kind == 'SelectField':
                handle = get(operands[0])
                assert handle in objects, 'selection on destroyed object'
                assert objects[handle][0] == n['ownerClass'], 'field from wrong class'
                assert n['result'] == 'Place', 'field selection captured address too early'
                addresses[ident] = (handle, n['symbolId'])
            elif kind == 'AddressOf':
                source = operands[0]
                assert nodes[source]['result'] == 'Place', 'test expects a field place'
                handle, field = addresses[source]
                nominal = objects[handle][0]
                addresses[ident] = (handle, field, generations[nominal])
            elif kind == 'Constant':
                literal = n['literal']
                values[ident] = None if literal is None else (literal == 'true' if literal in ('true', 'false') else int(literal))
            elif kind == 'NewArray':
                # Literal payloads allocate ordinary mutable array storage on every
                # execution; the JSON byte sequence itself is never an owner.
                items = list(n['bytes']) if n.get('bytes') else [get(i) for i in operands]
                values[ident] = allocate(items, n['type'].count('['), n['classId'], n['counted'])
            elif kind == 'Copy':
                values[ident] = clone(get(operands[0]))
            elif kind == 'Ref':
                handle = get(operands[0])
                assert handle in objects and references.get(handle, 0) > 0, 'retain dead object'
                references[handle] += 1
                values[ident] = handle
            elif kind == 'Unref':
                handle = get(operands[0])
                clear(operands[0])
                if handle is not None:
                    release_object(handle)
            elif kind == 'Move':
                values[ident] = get(operands[0])
                clear(operands[0])
            elif kind == 'FreeArray':
                release(get(operands[0]))
                clear(operands[0])
            elif kind == 'Store':
                if '[' in nodes[operands[0]]['type'] or nodes[operands[0]]['counted']:
                    assert get(operands[0]) is None, 'replacement leaked old storage'
                put(operands[0], get(operands[1]))
            elif kind == 'SelectIndex':
                handle = get(operands[0])
                index = get(operands[1])
                assert handle in heap, 'selection from dead array'
                assert 0 <= index < len(heap[handle][0]), 'index out of bounds'
                addresses[ident] = (handle, index)
            elif kind == 'Load':
                values[ident] = get(operands[0])
            elif kind in ('ArrayReserve', 'ArrayResize', 'ArrayTruncate', 'ArrayPush'):
                target, argument = operands
                handle = get(target)
                depth = nodes[target]['type'].count('[')
                count = get(argument)
                if handle is None:
                    handle = allocate([], depth, nodes[target]['classId'], nodes[target]['counted'])
                    put(target, handle)
                items, depth = heap[handle]
                nominal, counted = array_leaves[handle]
                if kind == 'ArrayPush':
                    items.append(count)
                    capacities[handle] = max(capacities[handle], len(items))
                elif kind == 'ArrayReserve':
                    capacities[handle] = max(capacities[handle], count)
                else:
                    if kind == 'ArrayResize' and depth == 1 and nominal is not None:
                        assert count <= len(items), 'LYR011: array element has no default value'
                    while len(items) > count:
                        removed = items.pop()
                        if depth > 1:
                            release(removed)
                        elif counted:
                            release_object(removed)
                    if kind == 'ArrayResize':
                        if count == 0:
                            release(handle)
                            put(target, None)
                        else:
                            while len(items) < count:
                                items.append(None if depth > 1 else 0)
                            capacities[handle] = max(capacities[handle], count)
                values[ident] = None
            elif kind == 'ArrayCapacity':
                handle = get(operands[0])
                assert handle is None or handle in heap, 'capacity of dead storage'
                values[ident] = 0 if handle is None else capacities[handle]
            elif kind == 'ArrayLength':
                handle = get(operands[0])
                assert handle is None or handle in heap, 'length of dead storage'
                values[ident] = 0 if handle is None else len(heap[handle][0])
            elif kind == 'Call':
                values[ident] = call(n['symbolId'], [
                    (lambda i=i: get(i), lambda value, i=i: put(i, value))
                    if nodes[i]['result'] == 'Address' else get(i) for i in operands])
            elif kind == 'Binary':
                left, right = (get(i) for i in operands)
                operations = {'+': lambda: left + right, '<': lambda: left < right,
                              '>': lambda: left > right, '>=': lambda: left >= right,
                              '==': lambda: left == right}
                values[ident] = operations[n['operator']]()
            elif kind == 'Convert':
                values[ident] = get(operands[0])
            elif kind == 'While':
                roles = {nodes[i]['role']: i for i in n['children']}
                while True:
                    try:
                        region(roles['Condition'])
                    except Exit as result:
                        # Break exits the loop; continue reevaluates its condition.
                        # Return and outer-loop transfers belong to their enclosing region.
                        if result.kind == 'Break' and result.value == ident:
                            break
                        if result.kind == 'Continue' and result.value == ident:
                            continue
                        if result.kind != 'Yield':
                            raise
                        condition = result.value
                    if not condition:
                        break
                    try:
                        region(roles['Body'])
                    except Exit as result:
                        if result.kind not in ('Break', 'Continue') or result.value != ident:
                            raise
                        if result.kind == 'Break':
                            break
            elif kind in ('Break', 'Continue'):
                raise Exit(kind, n['target'])
            elif kind == 'If':
                role = 'Then' if get(operands[0]) else 'Else'
                branch = next(i for i in n['children'] if nodes[i]['role'] == role)
                try:
                    region(branch)
                except Exit as result:
                    if result.kind != 'Yield':
                        raise
                    values[ident] = result.value
            elif kind in ('Return', 'Yield'):
                raise Exit(kind, get(operands[0]))
            else:
                raise AssertionError('unsupported test operation ' + kind)

        try:
            region(function['body'])
        except Exit as result:
            assert result.kind == 'Return'
            return result.value
        raise AssertionError('missing return')

    entry = next(f['symbolId'] for f in functions.values()
                 if f['name'] == 'use' and f['ownerClass'] is None)
    result = call(entry, [])
    assert not heap, f'leaked {len(heap)} allocations'
    if expected_objects is not None:
        assert len(objects) == expected_objects, 'incorrect explicit object destruction'
    if expected_hooks is not None:
        assert hook_calls == expected_hooks, 'incorrect final-hook invocation count'
    return result, copies


def check_selection_order(document):
    function = next(f for f in document['functions'] if f['name'] == 'use')
    nodes = {n['id']: n for n in function['nodes']}
    ordered = nodes[function['body']]['children']
    rhs = next(n for n in nodes.values() if n['kind'] == 'Call' and n['symbol'] == 'rhs')
    store = next(n for n in nodes.values() if n['kind'] == 'Store' and
                 nodes[n['operands'][0]]['kind'] == 'SelectIndex')
    assert ordered.index(store['operands'][0]) < ordered.index(rhs['id']), 'destination selected after RHS'


def main():
    failures = 0
    with tempfile.TemporaryDirectory(prefix='lyric-ir-ownership-') as directory:
        for name, source, expected in CASES:
            path = Path(directory) / (name + '.ly')
            path.write_text(source)
            try:
                result = subprocess.run(sys.argv[1:] + ['--inspect-ir', str(path)], capture_output=True, text=True, timeout=30)
                assert result.returncode == 0, result.stderr
                assert result.stderr == '', result.stderr
                document = json.loads(result.stdout)
                # A condition region yields control without ending its ownership
                # scope: its own holders must survive through the selected body.
                for function in document['functions']:
                    nodes = {n['id']: n for n in function['nodes']}
                    for node in nodes.values():
                        if node['kind'] == 'Borrow' and node['operands']:
                            assert nodes[node['operands'][0]]['result'] == 'Address', 'borrow did not capture fixed address'
                        if node['kind'] == 'Move':
                            assert nodes[node['operands'][0]]['kind'] != 'Borrow', 'borrow consumed as owner'
                        if node['kind'] == 'Region' and node['role'] == 'Condition':
                            own = {i for i in node['children'] if nodes[i]['type'] and
                                   '[' in nodes[i]['type'] and nodes[i]['kind'] in
                                   ('Local', 'NewArray', 'Copy', 'Call', 'If')}
                            assert not any(nodes[i]['kind'] == 'FreeArray' and
                                           nodes[i]['operands'][0] in own
                                           for i in node['children']), 'condition holder released before body'
                if name == 'index-order':
                    check_selection_order(document)
                    broken = copy.deepcopy(document)
                    function = next(f for f in broken['functions'] if f['name'] == 'use')
                    nodes = {n['id']: n for n in function['nodes']}
                    ordered = nodes[function['body']]['children']
                    selected = next(i for i in ordered if nodes[i]['kind'] == 'SelectIndex')
                    rhs = next(i for i in ordered if nodes[i]['kind'] == 'Call')
                    ordered.remove(selected)
                    ordered.insert(ordered.index(rhs) + 1, selected)
                    try:
                        check_selection_order(broken)
                    except AssertionError:
                        print('PASS self-check index-order')
                    else:
                        raise AssertionError('grader accepted delayed selection')
                expected_objects = None
                if name.startswith('class-'):
                    expected_objects = {
                        'class-final-growth': 1, 'class-growth-store': 1, 'class-growth-snapshot': 1,
                        'class-receiver-snapshot': 2, 'class-method-receiver': 2,
                        'class-method-identities': 2,
                    }.get(name, 0)
                expected_hooks = 1 if name.startswith('class-final') else None
                if name.startswith('counted-'):
                    expected_objects = 0
                    expected_hooks = {'counted-replace': 2, 'counted-reference': 2, 'counted-loop': 3, 'counted-object-array-replace': 2}.get(name, 1)
                actual, copies = execute(document, expected_objects, expected_hooks)
                assert actual == expected, (actual, expected)
                if name == 'counted-alias':
                    for defect in ('retain', 'release'):
                        broken = copy.deepcopy(document)
                        for function in broken['functions']:
                            for node in function['nodes']:
                                if defect == 'retain' and node['kind'] == 'Ref':
                                    node['kind'] = 'Load'
                                elif defect == 'release' and node['kind'] == 'Unref':
                                    node.update(kind='Constant', literal=None)
                        try:
                            execute(broken, expected_objects=0, expected_hooks=1)
                        except AssertionError:
                            print('PASS self-check counted-' + defect)
                        else:
                            raise AssertionError('grader accepted missing counted ' + defect)
                if name in ('class-growth-store', 'class-method-identities', 'class-constructor'):
                    broken = copy.deepcopy(document)
                    if name == 'class-growth-store':
                        function = next(f for f in broken['functions'] if f['name'] == 'use')
                        nodes = {n['id']: n for n in function['nodes']}
                        ordered = nodes[function['body']]['children']
                        store = next(n for n in nodes.values() if n['kind'] == 'Store' and
                                     nodes[n['operands'][0]]['kind'] == 'SelectField')
                        place = store['operands'][0]
                        address = copy.deepcopy(nodes[place])
                        address.update(id=max(nodes)+1, kind='AddressOf', result='Address', operands=[place])
                        function['nodes'].append(address)
                        ordered.insert(ordered.index(place)+1, address['id'])
                        store['operands'][0] = address['id']
                    elif name == 'class-method-identities':
                        targets = [f['symbolId'] for f in broken['functions'] if f['name'] == 'read']
                        for function in broken['functions']:
                            for node in function['nodes']:
                                if node['kind'] == 'Call' and node['symbolId'] == targets[0]:
                                    node['symbolId'] = targets[1]
                    else:
                        for function in broken['functions']:
                            for node in function['nodes']:
                                if node['kind'] == 'DestroyObject':
                                    node.update(kind='Constant', literal=None)
                    try:
                        broken_result, _ = execute(broken, expected_objects)
                        assert broken_result == expected, 'incorrect defect output'
                    except AssertionError:
                        print('PASS self-check', name)
                    else:
                        raise AssertionError('grader accepted broken ' + name)
                if name == 'class-array-fields':
                    broken = copy.deepcopy(document)
                    for function in broken['functions']:
                        if function['kind'] == 'Destruction':
                            for node in function['nodes']:
                                if node['kind'] == 'FreeArray':
                                    node.update(kind='Constant', literal=None)
                    try:
                        execute(broken, expected_objects=0)
                    except AssertionError:
                        print('PASS self-check owned-field-release')
                    else:
                        raise AssertionError('grader accepted missing owned-field release')
                if name == 'class-final':
                    for defect in ('omit', 'duplicate', 'early-reclaim'):
                        broken = copy.deepcopy(document)
                        body = next(f for f in broken['functions'] if f['kind'] == 'Destruction')
                        nodes = {n['id']: n for n in body['nodes']}
                        ordered = nodes[body['body']]['children']
                        hook = next(n['id'] for n in nodes.values() if n['kind'] == 'Call')
                        reclaim = next(n['id'] for n in nodes.values() if n['kind'] == 'ReclaimObject')
                        if defect == 'omit':
                            ordered.remove(hook)
                        elif defect == 'duplicate':
                            ordered.insert(ordered.index(hook), hook)
                        else:
                            ordered.remove(reclaim)
                            ordered.insert(ordered.index(hook), reclaim)
                        try:
                            execute(broken, expected_objects=0, expected_hooks=1)
                        except AssertionError:
                            print('PASS self-check final-' + defect)
                        else:
                            raise AssertionError('grader accepted final-' + defect)
                if name == 'class-constructor':
                    broken = copy.deepcopy(document)
                    for function in broken['functions']:
                        if function['kind'] == 'Destruction':
                            for node in function['nodes']:
                                if node['kind'] == 'ReclaimObject':
                                    node.update(kind='Constant', literal=None)
                    try:
                        execute(broken, expected_objects)
                    except AssertionError:
                        print('PASS self-check generated-reclamation')
                    else:
                        raise AssertionError('grader accepted missing generated reclamation')
                if name in ('copy', 'nested'):
                    assert copies >= 1, 'ordinary read failed to copy'
                if name in ('copy', 'replace'):
                    broken = copy.deepcopy(document)
                    for function in broken['functions']:
                        for node in function['nodes']:
                            if name == 'copy' and node['kind'] == 'Copy':
                                node['kind'] = 'Load'
                            if name == 'replace' and node['kind'] == 'FreeArray':
                                # A unit constant is inert but preserves all IDs.
                                node['kind'] = 'Constant'
                                node['literal'] = None
                    try:
                        execute(broken)
                    except AssertionError:
                        print('PASS self-check', name)
                    else:
                        raise AssertionError('grader accepted broken ' + name)
                print('PASS', name)
            except (AssertionError, ValueError, KeyError, subprocess.TimeoutExpired) as error:
                failures += 1
                print('FAIL', name, error)
    print(f'{len(CASES)-failures}/{len(CASES)} ownership IR cases passed')
    return int(failures != 0)

if __name__ == '__main__':
    sys.exit(main())
