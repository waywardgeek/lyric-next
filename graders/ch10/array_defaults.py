#!/usr/bin/env python3
"""Retained Ch10 array-default checks; run from root with COMPILER [ARG ...].

All cases emit real C and run at O0/O2 in safe and unsafe modes. Success requires
exit 0, exact stdout and empty stderr. Runtime rejection requires exit 1, empty
stdout, and the exact located LYR011 diagnostic plus the sole main frame.
The shared tuple runner implements these comparisons. No normalization/downloads.
"""
import sys
import tuples

# Every identifier is retained. These are language defaults, not empty cleanup
# holders. In particular, reserving capacity must not invoke field initializers.
CASES = [
    ('default-tuple-scalars', '''func main() {
        var rows:[(i32,bool,(u8,))]=[]
        rows.resize(2)
        assert(len(rows)==2u64)
        assert(rows[0].0==0)
        assert(rows[1].1==false)
        assert(rows[1].2.0==0u8)
        print("ok")
    }''', b'ok'),
    ('default-tuple-regrowth', '''func main() {
        var rows:[(i32,bool)]=[(7,true),(8,true)]
        rows.reserve(8)
        rows.truncate(1)
        rows.resize(2)
        assert(rows[0].0==7)
        assert(rows[1].0==0)
        assert(rows[1].1==false)
        rows.resize(0)
        assert(capacity(rows)==0u64)
        rows.resize(1)
        assert(rows[0].0==0)
        print("ok")
    }''', b'ok'),
    ('default-tuple-owned-regrowth', '''func main() {
        var rows:[([i32],bool)]=[([7,8],true)]
        rows.reserve(4)
        rows.truncate(0)
        rows.resize(2)
        assert(len(rows[0].0)==0u64)
        assert(capacity(rows[1].0)==0u64)
        assert(rows[0].1==false)
        print("ok")
    }''', b'ok'),
    ('default-array-boundary', '''struct Point { x:i32 }
    func main() {
        var rows:[([Point],i32)]=[]
        rows.resize(2)
        assert(len(rows[0].0)==0u64)
        assert(rows[1].1==0)
        print("ok")
    }''', b'ok'),
    ('default-struct-explicit-operations', '''func next()->i32 { print("default"); return 9 }
    struct Point { x:i32=next() }
    func main() {
        var rows:[Point]=[]
        rows.reserve(8)
        rows.resize(0)
        rows.push(Point {x:7})
        rows.resize(1)
        assert(rows[0].x==7)
        rows.truncate(0)
        rows.push(Point {x:8})
        rows.resize(0)
        assert(capacity(rows)==0u64)
        print("ok")
    }''', b'ok'),
    ('default-handle-tuple-explicit-operations', '''value class Box {
        func Box(self) {}
        final func finish(self) { print("drop") }
    }
    func main() {
        var rows:[(Box,bool)]=[]
        rows.reserve(4)
        rows.push((Box(),true))
        rows.resize(1)
        rows.resize(0)
        print("ok")
    }''', b'dropok'),
]
RUNTIME_CASES = [
    ('default-struct-forbidden', '''struct Point { x:i32 }
    func main() { var rows:[Point]=[]; rows.resize(1) }''', None),
    ('default-declared-initializers-forbidden', '''func next()->i32 { print("wrong"); return 9 }
    struct Point { x:i32=next() }
    func main() { var rows:[Point]=[]; rows.resize(1) }''', None),
    ('default-empty-struct-forbidden', '''struct Empty {}
    func main() { var rows:[Empty]=[]; rows.resize(1) }''', None),
    ('default-tuple-struct-forbidden', '''struct Point { x:i32=0 }
    func main() { var rows:[(bool,(Point,))]=[]; rows.resize(1) }''', None),
    ('default-tuple-handle-forbidden', '''class Item { func Item(self) {} }
    func main() { var rows:[(bool,(Item,))]=[]; rows.resize(1) }''', None),
    ('default-tuple-counted-forbidden', '''value class Box { func Box(self) {} }
    func main() { var rows:[(Box,)]=[]; rows.resize(1) }''', None),
]

if __name__ == '__main__':
    failed = 0
    for unsafe in (False, True):
        options = ('--unsafe',) if unsafe else ()
        mode = 'unsafe' if unsafe else 'safe'
        failed |= tuples.main(CASES, [], 'array-default-' + mode,
                              runtime_cases=RUNTIME_CASES, emission_options=options)
    sys.exit(failed)
