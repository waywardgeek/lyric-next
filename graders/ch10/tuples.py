#!/usr/bin/env python3
"""Retained Chapter 10 tuple execution checks, under development.

Run from repository root: python3 graders/ch10/tuples.py COMPILER [ARG ...]
Requires Python 3 and a C11 compiler (CC, default clang). No downloads.
Each fixture must emit C with exit 0 and empty stderr, compile at O0 and O2,
then exit 0 with exact stdout bytes and empty stderr. No normalization.
Temporary sources and binaries are removed even after a failed assertion.
These checks exercise the supplied implementation, never a reference solution.
"""
import json
import os
import re
import shlex
import subprocess
import sys
import tempfile
from pathlib import Path

# Stable identifiers; all checks are retained. Assertions exercise real tuple
# operations; the final marker distinguishes successful execution from no output.
CASES = [
    ('destructure-branch-hint', """func main() {
        let result=if true { let (x,flag)=(7i64,true); assert(flag); x } else { 9i64 }
        assert(result==7i64)
        print("ok")
    }""", b'ok'),
    ('destructure-counted-kept', """value class Box {
        n:i32
        func Box(self) { self.n=7 }
        final func finish(self) { print("drop") }
    }
    func use() { let (box,_)=(Box(),true); assert(box.n==7); print("live") }
    func main() { use(); print("done") }
    """, b'livedropdone'),
    ('destructure-once', """func make(n:ref i32)->(i32,(bool,i32)) {
        n+=1
        return (7,(true,9))
    }
    func main() {
        var calls=0
        let (a,(flag,b))=make(ref calls)
        assert(calls==1)
        assert(a==7)
        assert(flag)
        assert(b==9)
        print("ok")
    }""", b'ok'),
    ('destructure-singleton-var', 'func main() { var (x,)=(1,); x=2; assert(x==2); print("ok") }', b'ok'),
    ('destructure-array-copy', """func main() {
        var original=([1,2],true)
        var (items,flag)=original
        items[0]=9
        assert(original.0[0]==1)
        assert(items[0]==9)
        assert(flag)
        print("ok")
    }""", b'ok'),
    ('destructure-discard-cleanup', """value class Box {
        func Box(self) {}
        final func finish(self) { print("drop") }
    }
    func main() { let (_,answer)=(Box(),7); assert(answer==7); print("done") }
    """, b'dropdone'),
    ('tuple-singleton', '''func main() {
        let grouped=(7)
        let single:(i32,)=(grouped,)
        assert(single.0==7)
        print("ok")
    }''', b'ok'),
    ('tuple-context', '''func main() {
        let pair:(u8,i64)=(255,9223372036854775807)
        assert(pair.0==255u8)
        assert(pair.1==9223372036854775807i64)
        print("ok")
    }''', b'ok'),
    ('tuple-nested', '''func main() {
        let value=((1,true),(3,))
        assert(value.0.0==1)
        assert(value.0.1)
        assert(value.1.0==3)
        print("ok")
    }''', b'ok'),
    ('tuple-snapshots', '''func change(x:ref i32)->i32 { x=9; return 2 }
    func main() {
        var x=1
        let pair=(x,change(ref x),x)
        assert(pair.0==1)
        assert(pair.1==2)
        assert(pair.2==9)
        print("ok")
    }''', b'ok'),
    ('tuple-call-return', '''func make()->(i32,bool) { return (7,true) }
    func read(value:(i32,bool))->i32 {
        assert(value.1)
        return value.0
    }
    func main() { assert(read(make())==7); print("ok") }''', b'ok'),
    ('tuple-array-copy', '''func make()->([i32],bool) { return ([1,2],true) }
    func main() {
        var original=make()
        let copy=original
        original.0[0]=9
        assert(copy.0[0]==1)
        assert(original.0[0]==9)
        assert(copy.1)
        print("ok")
    }''', b'ok'),
    ('tuple-component-borrow', '''func inc(x:ref i32) { x+=1 }
    func main() {
        var pair=(3,4)
        inc(ref pair.1)
        assert(pair.0==3)
        assert(pair.1==5)
        print("ok")
    }''', b'ok'),
    ('tuple-nested-borrow', '''func inc(x:ref i32) { x+=1 }
    func forward(value:ref ((i32,bool),i32)) { inc(ref value.0.0) }
    func main() {
        var value=((7,true),9)
        forward(ref value)
        assert(value.0.0==8)
        assert(value.0.1)
        assert(value.1==9)
        print("ok")
    }''', b'ok'),
    ('tuple-array-last-use', '''func main() {
        var value=([1,2],true)
        let item=ref value.0[0]
        assert(item==1)
        value.0.resize(3)
        assert(value.0[2]==0)
        print("ok")
    }''', b'ok'),
    ('tuple-ordinary-copy', '''class Item {
        n:i32
        func Item(self) { self.n=7 }
    }
    func main() {
        let item=Item()
        let original=(item,true)
        let copy=original
        assert(copy.0.n==7)
        assert(copy.1)
        item.destroy()
        print("ok")
    }''', b'ok'),
    ('tuple-mixed-counted-return', '''value class First {
        n:i32
        func First(self) { self.n=3 }
        final func finish(self) { print("X") }
    }
    value class Second {
        n:i32
        func Second(self) { self.n=5 }
        final func finish(self) { print("X") }
    }
    func make()->((First,First),[Second]) {
        return ((First(),First()),[Second()])
    }
    func choose(b:bool)->((First,First),[Second]) {
        return if b { make() } else { make() }
    }
    func use() {
        let original=choose(true)
        let copy=original
        assert(copy.0.0.n==3)
        assert(copy.0.1.n==3)
        assert(copy.1[0].n==5)
        print("live")
    }
    func main() { use(); print("done") }''', b'liveXXXdone'),
    ('tuple-counted-copy', '''value class Box {
        n:i32
        func Box(self) { self.n=7 }
        final func finish(self) { print("final") }
    }
    func use() {
        let original=(Box(),)
        let copy=original
        assert(copy.0.n==7)
        print("live")
    }
    func main() { use(); print("done") }''', b'livefinaldone'),
]


CASES.extend([
    ('tuple-direct-nested', '''func main() {
        var value=((1,true),9)
        value.0.0=7
        value.1=value.0.0
        assert(value.0.0==7)
        assert(value.0.1)
        assert(value.1==7)
        print("ok")
    }''', b'ok'),
    ('tuple-direct-array-copy', '''func main() {
        var pair=([1],true)
        var replacement=[4,5]
        pair.0=replacement
        replacement[0]=9
        assert(pair.0[0]==4)
        assert(pair.1)
        print("ok")
    }''', b'ok'),
    ('tuple-direct-counted', '''value class Box {
        n:i32
        func Box(self,n:i32) { self.n=n }
        final func finish(self) { if self.n==1 { print("A") } else { print("B") } }
    }
    func use() {
        var pair=(Box(1),true)
        pair.0=Box(2)
        assert(pair.0.n==2)
        assert(pair.1)
        print("live")
    }
    func main() { use(); print("done") }''', b'AliveBdone'),
])


# Public fixture vocabulary. Drop's finalizer has a class-wide lifetime effect,
# just like the retained Chapter 8 finalization tests; it does not store a handle.
OBJECT = 'class Item { n:i32; func Item(self) { self.n=7 } }; '
FINALIZER = ('value class Drop { func Drop(self) {}; '
             'final func finish(self) { let other=Item(); other.destroy() } }; ')


# Mutating an array component must preserve a counted sibling's cleanup.
# The object is used before that cleanup here, so these are valid controls.
CASES.append(('tuple-partial-holder-valid', OBJECT + FINALIZER + '''func use() {
    var pair=([1],Drop())
    let alias=ref pair.0
    let forwarded=ref alias
    forwarded=[]
    pair.0.resize(0)
    let item=Item()
    assert(item.n==7)
}
func main() { use(); print("ok") }
''', b'ok'))

# Intended located semantic rejections, not crashes or arbitrary nonzero exits.
NEGATIVE_CASES = [
    ('destructure-discard-invalidates-kept', OBJECT + FINALIZER + """func main() {
        let (item,_)=(Item(),Drop())
        assert(item.n==7)
    }""", 'LYC330'),
    ('destructure-shape', 'func main() { let (a,b)=(1,) }', 'LYC304'),
    ('destructure-duplicate', 'func main() { let (a,a)=(1,2) }', 'LYC302'),
    ('destructure-shadow', 'func main() { let a=0; let (a,b)=(1,2) }', 'LYC302'),
    ('destructure-initializer-scope', 'func main() { let (a,b)=(1,a) }', 'LYC301'),
    ('destructure-immutable', 'func main() { let (a,b)=(1,2); a=3 }', 'LYC308'),
    ('tuple-direct-sibling-cleanup', OBJECT + FINALIZER + '''func make()->Item {
        var pair=([1],Drop())
        pair.0=[]
        return Item()
    }
    func main() { let result=make(); assert(result.n==7) }
    ''', 'LYC330'),
    ('tuple-direct-immutable', 'func main() { let pair=(1,true); pair.0=2 }', 'LYC308'),
    ('tuple-direct-type', 'func main() { var pair=(1,true); pair.0=false }', 'LYC304'),
    ('tuple-partial-alias-empty', OBJECT + FINALIZER + '''func make()->Item {
        var pair=([1],Drop())
        let alias=ref pair.0
        alias=[]
        return Item()
    }
    func main() { let result=make(); assert(result.n==7) }
    ''', 'LYC330'),
    ('tuple-partial-alias-forward', OBJECT + FINALIZER + '''func make()->Item {
        var pair=([1],Drop())
        let alias=ref pair.0
        let forwarded=ref alias
        forwarded=[]
        return Item()
    }
    func main() { let result=make(); assert(result.n==7) }
    ''', 'LYC330'),
    ('tuple-partial-resize-zero', OBJECT + FINALIZER + '''func make()->Item {
        var pair=([1],Drop())
        pair.0.resize(0)
        return Item()
    }
    func main() { let result=make(); assert(result.n==7) }
    ''', 'LYC330'),
    ('tuple-partial-alias-resize', OBJECT + FINALIZER + '''func make()->Item {
        var pair=([1],Drop())
        let alias=ref pair.0
        alias.resize(0)
        return Item()
    }
    func main() { let result=make(); assert(result.n==7) }
    ''', 'LYC330'),
    ('tuple-second-class-invalidated', OBJECT + '''class Other {
        n:i32; func Other(self) { self.n=9 }
    }
    func main() {
        let other=Other(); let saved=(Item(),other)
        other.destroy(); assert(saved.1.n==9)
    }''', 'LYC330'),
    ('tuple-final-self-copy', '''value class Recursive {
        func Recursive(self) {}
        final func finish(self) { let hidden=(self,) }
    }
    func main() { let item=Recursive() }
    ''', 'LYC334'),
    ('tuple-object-invalidated', OBJECT + '''func main() {
        let item=Item(); let saved=(item,true)
        item.destroy(); assert(saved.0.n==7)
    }''', 'LYC330'),
    ('tuple-later-argument', OBJECT + '''func remove(item:Item)->i32 {
        item.destroy(); return 1
    }
    func consume(saved:(Item,bool), later:i32) { assert(saved.0.n==7) }
    func main() { let item=Item(); consume((item,true),remove(item)) }
    ''', 'LYC330'),
    ('tuple-return-cleanup', OBJECT + FINALIZER + '''func make()->(Item,bool) {
        let cleanup=(Drop(),); return (Item(),true)
    }
    func main() { let result=make() }
    ''', 'LYC330'),
    ('tuple-branch-cleanup', OBJECT + FINALIZER + '''func choose(b:bool)->(Item,bool) {
        return if b { let cleanup=(Drop(),); (Item(),true) } else { (Item(),false) }
    }
    func main() { let result=choose(true) }
    ''', 'LYC330'),
    ('tuple-selector-range', 'func main() { let x=(1,); print(x.1) }', 'LYC304'),
    ('tuple-selector-name', 'func main() { let x=(1,true); print(x.nope) }', 'LYC304'),
    ('tuple-shape', 'func main() { let x:(i32,bool)=(1,) }', 'LYC304'),
    ('tuple-component-type', 'func main() { let x:(i32,bool)=(1,2) }', 'LYC304'),
    ('tuple-order', 'func use(x:(i32,bool)) {}; func main() { use((true,1)) }', 'LYC304'),
    ('tuple-array-invalidated', 'func main() { var value=([1,2],true); let item=ref value.0[0]; value.0.resize(3); assert(item==1) }', 'LYC326'),
    ('tuple-immutable-borrow', 'func inc(x:ref i32) { x+=1 }; func main() { let pair=(1,2); inc(ref pair.0) }', 'LYC308'),
]


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


def main(cases=CASES, negative_cases=NEGATIVE_CASES, label="tuple",
         runtime_cases=(), emission_options=()):
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit('usage: tuples.py COMPILER [ARG ...]')
    passed = 0
    with tempfile.TemporaryDirectory(prefix=f'lyric-{label}-') as directory:
        root = Path(directory)
        for identifier, source, expected in [*cases, *runtime_cases]:
            try:
                lyric = root / (identifier + '.ly')
                lyric.write_text(source, encoding='utf-8')
                emitted = run(compiler + ['--emit-c', *emission_options, str(lyric)])
                assert emitted.returncode == 0, emitted.stderr.decode(errors='replace')
                assert emitted.stderr == b'', emitted.stderr
                c_source = root / (identifier + '.c')
                c_source.write_bytes(emitted.stdout)
                for optimization in ('-O0', '-O2'):
                    executable = root / (identifier + optimization)
                    command = shlex.split(os.environ.get('CC', 'clang'))
                    built = run(command + ['-std=c11', optimization, str(c_source), '-o', str(executable)])
                    assert built.returncode == 0, built.stderr.decode(errors='replace')
                    result = run([str(executable)])
                    if expected is None:
                        # Runtime rejection is a precise result, not any nonzero exit.
                        location = re.escape(json.dumps(str(lyric)).encode())
                        diagnostic = (location + rb':[1-9][0-9]*:[1-9][0-9]*: runtime error '
                                      rb'LYR011: array element has no default value\n  in "main"\n')
                        assert result.returncode == 1, (result.returncode, result.stderr)
                        assert result.stdout == b'', result.stdout
                        assert re.fullmatch(diagnostic, result.stderr), result.stderr
                    else:
                        assert result.returncode == 0, (result.returncode, result.stderr)
                        assert result.stdout == expected, (result.stdout, expected)
                        assert result.stderr == b'', result.stderr
                passed += 1
                print('PASS', identifier)
            except (AssertionError, subprocess.TimeoutExpired) as error:
                print('FAIL', identifier, str(error))
        negative_passed = 0
        for identifier, source, diagnostic in negative_cases:
            try:
                lyric = root / (identifier + '.ly')
                lyric.write_text(source, encoding='utf-8')
                result = run(compiler + ['--emit-c', str(lyric)])
                assert result.returncode == 1, (result.returncode, result.stderr)
                assert result.stdout == b'', result.stdout
                assert re.search(rb':\d+:\d+: error ' + diagnostic.encode() + rb':', result.stderr), result.stderr
                negative_passed += 1
                print('PASS', identifier)
            except (AssertionError, subprocess.TimeoutExpired) as error:
                print('FAIL', identifier, str(error))
    total = len(cases) + len(runtime_cases)
    print(f'{passed}/{total} {label} execution cases passed')
    print(f'{negative_passed}/{len(negative_cases)} {label} rejection cases passed')
    return 0 if passed == total and negative_passed == len(negative_cases) else 1


if __name__ == '__main__':
    sys.exit(main())
