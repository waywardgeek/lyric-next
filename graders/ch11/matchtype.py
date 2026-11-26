#!/usr/bin/env python3
"""Retained compile-time type-selection checks; same public C/diagnostic contract.
Run from repository root: python3 graders/ch11/matchtype.py COMPILER [ARG ...]
"""
from pathlib import Path
import sys
import copy
import json
import subprocess
import tempfile
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch10'))
from tuples import main

CASES = [
    ('matchtype-concrete-and-unchecked-body', '''func classify<T>(value:T)->i32 {
    matchtype value {
        i32 => { return value+1 }
        bool => { return 2 }
        _ => { return 3 }
    }
}
func main() { assert(classify(7)==8); assert(classify(true)==2); print("ok") }
''', b'ok'),
    ('matchtype-structural-arm-bindings', '''func classify<T>(value:T)->i32 {
    matchtype<U> value {
        (U,) => { let copy:U=value.0; return 1 }
        [U] => { let copy:U=value[0]; return 2 }
        _ => { return 3 }
    }
}
func main() { assert(classify((true,))==1); assert(classify([7])==2); assert(classify(false)==3); print("ok") }
''', b'ok'),
    ('matchtype-repeated-binding', '''func classify<T>(value:T)->i32 {
    matchtype<U> value {
        (U,U) => { return 1 }
        _ => { return 2 }
    }
}
func main() { assert(classify((3,4))==1); assert(classify((3,true))==2); print("ok") }
''', b'ok'),
    ('matchtype-unevaluated-subject', '''func noisy()->i32 { print("wrong"); return 7 }
func main() {
    matchtype noisy() {
        i32 => { print("ok") }
        _ => { print("wrong") }
    }
}
''', b'ok'),
    ('matchtype-value-result', '''func main() {
    var source:i32
    let result=matchtype source {
        i32 => { 7 }
        _ => { missing }
    }
    assert(result==7)
    print("ok")
}
''', b'ok'),
]

NEGATIVE_CASES = [
    ('matchtype-typo-in-later-pattern', '''func main() {
    matchtype 7 {
        i32 => { print("ok") }
        Typo => { print("wrong") }
    }
}
''', 'LYC301'),
    ('matchtype-no-arm', 'func main() { matchtype 7 { bool => { print("wrong") } } }\n', 'LYC346'),
    ('matchtype-duplicate-variable', 'func main() { matchtype<U,U> 7 { _ => {} } }\n', 'LYC302'),
]

# These cases exercise the existing flow/lifetime pipeline, not just selection.
CASES += [
    ('matchtype-initialization', 'func main() { var n:i32; matchtype 7 { i32 => { n=9 }; _ => { missing } }; assert(n==9); print("ok") }', b'ok'),
    ('matchtype-loop-transfers', 'func main() { var n=0; while n<4 { n+=1; matchtype n { i32 => { if n==2 { continue }; if n==3 { break } }; _ => { missing } } }; assert(n==3); print("ok") }', b'ok'),
    ('matchtype-nested', 'func main() { let n=matchtype<U> (7,) { (U,) => { matchtype<V> [true] { [V] => { let x:U=9; let flag:V=true; assert(flag); x } } } }; assert(n==9); print("ok") }', b'ok'),
    ('matchtype-array-result-copy', 'func main() { var original=[3]; var result=matchtype original { [i32] => { let copy=original; copy } }; result[0]=9; assert(original[0]==3); assert(result[0]==9); print("ok") }', b'ok'),
    ('matchtype-tuple-result', 'func main() { let result=matchtype 7 { i32 => { let a=[3]; (a,true) } }; assert(result.0[0]==3); assert(result.1); print("ok") }', b'ok'),
    ('matchtype-local-cleanup', 'value class Box { func Box(self) {}; final func finish(self) { print("drop") } }; func main() { matchtype 7 { i32 => { let local=Box(); print("arm") } }; print("after") }', b'armdropafter'),
    ('matchtype-owned-result-survives', 'value class Box { func Box(self) {}; final func finish(self) { print("drop") } }; func use() { let result=matchtype 7 { i32 => { let local=Box(); (local,) } }; print("live") }; func main() { use(); print("done") }', b'livedropdone'),
    ('matchtype-unselected-effects', 'func main() { var a=[7]; let p=ref a[0]; matchtype a { [i32] => { assert(p==7) }; _ => { a.resize(9) } }; assert(p==7); print("ok") }', b'ok'),
    ('matchtype-first-match', 'func main() { matchtype<U> 7 { U => { print("first") }; i32 => { print("wrong") }; _ => { print("wrong") } } }', b'first'),
    ('matchtype-live-borrow-result', 'struct Borrowed { target:ref i32 }; func main() { var n=7; let result=matchtype n { i32 => { Borrowed { target:ref n } } }; n=9; assert(result.target==9); print("ok") }', b'ok'),
]
NEGATIVE_CASES += [
    ('matchtype-unselected-assignment', 'func main() { var n:i32; matchtype 7 { i32 => {}; _ => { n=9 } }; let read=n }', 'LYC307'),
    ('matchtype-selected-invalidation', 'func main() { var a=[7]; let p=ref a[0]; matchtype a { [i32] => { a.resize(9) } }; let n=p }', 'LYC326'),
    ('matchtype-borrow-local-escape', 'struct Borrowed { target:ref i32 }; func main() { let result=matchtype 7 { i32 => { var local=9; Borrowed { target:ref local } } }; let n=result.target }', 'LYC326'),
    ('matchtype-header-shadow', 'func f<T>(value:T) { matchtype<T> value { T => {} } }; func main() { f(7) }', 'LYC302'),
    ('matchtype-empty-header', 'func main() { matchtype<> 7 { _ => {} } }', 'LYC201'),
    ('matchtype-missing-arrow', 'func main() { matchtype 7 { i32 {} } }', 'LYC201'),
    ('matchtype-malformed-unselected-body', 'func main() { matchtype 7 { i32 => {}; bool => { let = } } }', 'LYC201'),
]

CASES += [
    ('matchtype-result-arithmetic-context', 'func main() { var n:i64=7; let result=(matchtype n { i64 => { n } })+1; assert(result==8); print("ok") }', b'ok'),
    ('matchtype-return-cleanup', 'value class Box { func Box(self) {}; final func finish(self) { print("drop") } }; func f()->i32 { let outer=Box(); matchtype 7 { i32 => { let inner=Box(); return 9 } } }; func main() { assert(f()==9); print("done") }', b'dropdropdone'),
    ('matchtype-bound-name-reused-in-sibling', 'func main() { matchtype<U> (3,true) { (U,U) => { missing }; (i32,U) => { let flag:U=true; assert(flag); print("ok") } } }', b'ok'),
]
NEGATIVE_CASES += [
    ('matchtype-unbound-header-variable', 'func main() { matchtype<U> 7 { i32 => { let n:U=3 }; _ => {} } }', 'LYC301'),
    ('matchtype-binding-does-not-escape', 'func main() { matchtype<U> 7 { U => {} }; let n:U=3 }', 'LYC301'),
    ('matchtype-selected-body-error', 'func main() { matchtype 7 { i32 => { missing }; _ => {} } }', 'LYC301'),
]

def verify_selected_ir(document):
    """The subject and unselected arm must not become executable main IR."""
    assert document['format'] == 'lyric-ir-1'
    function = next(f for f in document['functions'] if f['name'] == 'main')
    nodes = function['nodes']
    assert [n['symbol'] for n in nodes if n['kind'] == 'Call'] == ['selected']
    assert not any(n['kind'] == 'If' for n in nodes), 'runtime branch for static selection'


def inspect_selection(compiler):
    source = """func subject()->i32 { return 7 }
func selected() { print("ok") }
func unselected() { print("wrong") }
func main() { matchtype subject() { i32 => { selected() }; _ => { unselected() } } }
"""
    with tempfile.TemporaryDirectory(prefix='lyric-matchtype-ir-') as directory:
        path = Path(directory) / 'selection.ly'
        path.write_text(source)
        result = subprocess.run(compiler + ['--inspect-ir', str(path)],
                                capture_output=True, timeout=30)
        assert result.returncode == 0 and result.stderr == b'', result.stderr
        document = json.loads(result.stdout)
        verify_selected_ir(document)
        # Verify that the public comparison rejects plausible publication defects.
        for defect in ('subject', 'unselected', 'branch'):
            broken = copy.deepcopy(document)
            function = next(f for f in broken['functions'] if f['name'] == 'main')
            call = next(n for n in function['nodes'] if n['kind'] == 'Call')
            extra = copy.deepcopy(call)
            if defect == 'branch':
                extra['kind'] = 'If'
            else:
                extra['symbol'] = defect
            function['nodes'].append(extra)
            try:
                verify_selected_ir(broken)
            except AssertionError:
                continue
            raise AssertionError(f'IR checker accepted {defect} corruption')
    print('1/1 matchtype IR selection check passed; 3/3 corruption controls rejected')


if __name__ == '__main__':
    status = main(CASES, NEGATIVE_CASES, 'matchtype')
    if status == 0:
        try:
            inspect_selection(sys.argv[1:])
        except (AssertionError, KeyError, ValueError, StopIteration, subprocess.TimeoutExpired) as error:
            print('FAIL matchtype IR selection:', error)
            status = 1
    sys.exit(status)
