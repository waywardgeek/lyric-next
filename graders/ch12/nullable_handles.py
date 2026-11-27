#!/usr/bin/env python3
"""Retained Chapter 12 nullable class-handle contracts.

From the repository root: python3 graders/ch12/nullable_handles.py COMPILER [ARG ...]
Uses the public tuple harness: exact output, empty stderr, exit 0 at O0/O2.
Static negatives require exit 1 and the specified located compiler diagnostic.
Null unwrap requires exit 1, empty stdout, exact LYR013/path/main frame,
and positive line/column values (not exact source coordinates).
These contracts precede implementation; their presence is not a passing result.
"""
from pathlib import Path
import sys
import subprocess
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch10'))
from tuples import main

BOX = '''class Box {
    n:i32
    func Box(self, n:i32) { self.n=n }
    func value(self)->i32 { return self.n }
}
'''
COUNTED = '''value class Box {
    func Box(self) {}
    final func finish(self) { print("drop") }
}
'''
CASES = [
    ('nullable-identity-comparison', BOX + """func main() {
        let first=Box(7)
        let second=Box(7)
        let link:Box?=first
        let empty:Box?=null
        assert(empty==null)
        assert(null==empty)
        assert(link!=null)
        assert(null!=link)
        assert(link==first)
        assert(first==link)
        assert(first==first)
        assert(first!=second)
        assert(link!=second)
        assert(empty!=link)
        print("ok")
    }""", b'ok'),
    ('nullable-counted-comparison', COUNTED + """func use() {
        let live=Box()
        let link:Box?=live
        let empty:Box?=null
        assert(live==link)
        assert(link!=empty)
        assert(empty==null)
        print("live")
    }
    func main() { use(); print("done") }
    """, b'livedropdone'),
    ('nullable-wrap-unwrap', BOX + '''func main() {
        let item:Box?=Box(7)
        assert(item!.value()==7)
        print("ok")
    }''', b'ok'),
    ('nullable-parameter-result', BOX + '''func wrap(item:Box)->Box? { return item }
    func read(item:Box?)->i32 { return item!.n }
    func main() { assert(read(wrap(Box(9)))==9); print("ok") }
    ''', b'ok'),
    # Empty counted holders must not attempt to retain the empty sentinel.
    ('nullable-counted-empty-copy', COUNTED + '''func consume(item:Box?) {}
    func main() {
        let empty:Box?=null
        let copy=empty
        consume(copy)
        print("ok")
    }''', b'ok'),
    ('nullable-nested-counted-copy', COUNTED + """func use() {
        let empty:Box?=null
        let live:Box?=Box()
        let rows:[[Box?]]=[[empty,live],[empty]]
        let copy=rows
        let item=copy[0][1]!
        print("live")
    }
    func main() { use(); print("done") }
    """, b'livedropdone'),
    ('nullable-counted-growth-regrowth', COUNTED + """func main() {
        var rows:[Box?]=[]
        rows.resize(2)
        let copy=rows
        rows.truncate(0)
        rows.resize(3)
        rows.resize(0)
        var tuples:[(Box?,i32)]=[]
        tuples.resize(2)
        let other=tuples
        assert(other[0].1==0)
        print("ok")
    }""", b'ok'),
    ('nullable-counted-live-copy', COUNTED + '''func use() {
        let item:Box?=Box()
        let copy=item
        let live=copy!
        print("live")
    }
    func main() { use(); print("done") }
    ''', b'livedropdone'),
]
NEGATIVE_CASES = [
    ('nullable-ordering-forbidden', BOX + 'func main() { let a:Box?=null; assert(a<a) }', 'LYC313'),
    ('nullable-other-class-comparison', BOX + """class Other { func Other(self) {} }
    func main() { let a:Box?=null; let b:Other?=null; assert(a==b) }
    """, 'LYC313'),
    ('nullable-comparison-does-not-unwrap', BOX + """func main() {
        let a:Box?=Box(1)
        if a!=null { let n=a.n }
    }""", 'LYC304'),
    ('nullable-null-to-nonnull', BOX + 'func main() { let item:Box=null }', 'LYC304'),
    ('nullable-to-nonnull', BOX + '''func main() {
        let item:Box?=Box(1)
        let nonnull:Box=item
    }''', 'LYC304'),
    ('nullable-field-needs-unwrap', BOX + '''func main() {
        let item:Box?=Box(1)
        let value=item.n
    }''', 'LYC304'),
    ('nullable-method-needs-unwrap', BOX + '''func main() {
        let item:Box?=Box(1)
        item.value()
    }''', 'LYC304'),
    ('nullable-destroy-needs-unwrap', BOX + '''func main() {
        let item:Box?=Box(1)
        item.destroy()
    }''', 'LYC304'),
    ('nonnull-cannot-unwrap', BOX + '''func main() {
        let item=Box(1)
        let value=item!
    }''', 'LYC304'),
    # A new expression snapshot must not erase the handle's destruction history.
    ('nullable-unwrap-after-destroy', BOX + '''func main() {
        let owner=Box(1)
        let item:Box?=owner
        owner.destroy()
        let value=item!.n
    }''', 'LYC330'),
]
RUNTIME_CASES = [
    ('nullable-grown-entry-is-null', BOX + """func main() {
        let live=Box(7)
        var rows:[Box?]=[live]
        rows.truncate(0)
        rows.resize(1)
        let item=rows[0]!
    }""", None),
    ('nullable-grown-tuple-entry-is-null', BOX + """func main() {
        let live=Box(7)
        var rows:[(Box?,i32)]=[(live,9)]
        rows.truncate(0)
        rows.resize(1)
        let item=rows[0].0!
    }""", None),
    ('nullable-null-unwrap', BOX + '''func main() {
        let item:Box?=null
        let live=item!
    }''', None),
]

def unchecked_unwrap_contract(compiler):
    # Invalid unsafe input is inspected, NEVER executed. Valid unsafe programs
    # below establish unchanged behavior on inputs satisfying safe preconditions.
    with tempfile.TemporaryDirectory(prefix='lyric-unsafe-unwrap-') as directory:
        for name, source, unused in RUNTIME_CASES:
            path = Path(directory) / (name + '.ly')
            path.write_text(source)
            result = subprocess.run(
                compiler + ['--emit-c', '--unsafe', str(path)],
                capture_output=True, timeout=30)
            if (result.returncode != 0 or result.stderr
                    or b'LYR013: null handle unwrap' in result.stdout):
                print('FAIL unsafe unwrap-check omission', name)
                return 1
    print('3/3 unsafe unwrap-check omission cases passed (not executed)')
    return 0


if __name__ == '__main__':
    safe = main(CASES, NEGATIVE_CASES, 'nullable-handle',
                runtime_cases=RUNTIME_CASES,
                runtime_diagnostic=b'LYR013: null handle unwrap')
    omitted = unchecked_unwrap_contract(sys.argv[1:])
    unsafe = main(CASES, (), 'nullable-handle-unsafe-valid',
                  emission_options=('--unsafe',))
    raise SystemExit(0 if safe == 0 and omitted == 0 and unsafe == 0 else 1)
