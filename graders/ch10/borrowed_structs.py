#!/usr/bin/env python3
"""Retained contained-borrow contract cases, included in make test.

Run from repository root:
  python3 graders/ch10/borrowed_structs.py COMPILER [ARG ...]
Uses the public tuple runner's exact-byte O0/O2 execution and located rejection
contract. An unrelated unsupported-feature rejection is not a passing result.
"""
import sys
from tuples import main

CASES = [
    # Value-root default results must transport aliases as well as direct
    # address-root defaults. The ordinary object is process-lived, not reclaimed.
    ('borrow-struct-value-default-ordinary-owner', '''class Box {
    n:i32
    func Box(self) { self.n=7 }
}
struct Borrowed { target:ref i32 }
struct Wrapper { inner:Borrowed=Borrowed { target:ref Box().n } }
func main() {
    let holder=Wrapper {}
    let copied=holder
    assert(copied.inner.target==7)
    print("ok")
}
''', b'ok'),
    # One ordinary object lives until this short-lived test process exits. This
    # tests address survival, not explicit object reclamation or leak freedom.
    ('borrow-struct-default-ordinary-owner', '''class Box {
    n:i32
    func Box(self) { self.n=7 }
}
struct Borrowed { target:ref i32=ref Box().n }
func main() {
    let holder=Borrowed {}
    let copied=holder
    assert(copied.target==7)
    print("ok")
}
''', b'ok'),
    # Uncalled bodies are checked: this tests recursive analysis, not construction
    # or runtime traversal of a recursively borrowed value.
    ('borrow-struct-recursive-formal-analysis', '''struct Link { next:ref Link; target:ref i32 }
func readDepth(link:Link, depth:i32)->i32 {
    if depth==0 { return link.target }
    return readDepth(link.next,depth-1)
}
func main() { print("ok") }
''', b'ok'),
    # A value copy isolates wrapper fields, not the referenced holder's storage.
    ('borrow-struct-value-parameter-write-through', '''struct Borrowed { target:ref i32 }
struct Wrapper { holder:ref Borrowed }
func replace(wrapper:Wrapper, replacement:Borrowed) { wrapper.holder=replacement }
func main() {
    var old=[3]
    var number=7
    var holder=Borrowed { target:ref old[0] }
    let wrapper=Wrapper { holder:ref holder }
    replace(wrapper,Borrowed { target:ref number })
    old.resize(9)
    assert(holder.target==7)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-local-copy', '''struct Borrowed { target:ref i32 }
func main() {
    var number=7
    let holder=Borrowed { target:ref number }
    let copied=holder
    number=9
    assert(copied.target==9)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-synchronous', '''struct Borrowed { target:ref i32 }
func read(holder:Borrowed)->i32 { return holder.target }
func main() { var number=7; let holder=Borrowed { target:ref number }; assert(read(holder)==7); print("ok") }
''', b'ok'),
    ('borrow-struct-nested-copy', '''struct Borrowed { target:ref i32 }
struct Wrapper { inner:Borrowed }
func main() {
    var number=7
    let value=(Wrapper { inner:Borrowed { target:ref number } },)
    let copied=value
    number=8
    assert(copied.0.inner.target==8)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-target-write', '''struct Borrowed { target:ref i32 }
func main() { var number=7; var holder=Borrowed { target:ref number }; holder.target=9; assert(number==9); print("ok") }
''', b'ok'),
    ('borrow-struct-last-use', '''struct Borrowed { target:ref i32 }
func main() { var items=[7]; let holder=Borrowed { target:ref items[0] }; assert(holder.target==7); items.resize(9); print("ok") }
''', b'ok'),
    ('borrow-struct-distinct-nested-targets', '''struct Borrowed { target:ref i32 }
struct Pair { first:Borrowed; second:Borrowed }
func readSecond(pair:Pair)->i32 { return pair.second.target }
func main() {
    var first=3
    var second=7
    let pair=Pair { first:Borrowed { target:ref first }, second:Borrowed { target:ref second } }
    assert(readSecond(pair)==7)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-call-use-before-growth', '''struct Borrowed { target:ref i32 }
func readThenGrow(holder:Borrowed,items:ref [i32])->i32 {
    let saved=holder.target
    items.resize(9)
    return saved
}
func main() {
    var items=[7]
    let holder=Borrowed { target:ref items[0] }
    assert(readThenGrow(holder,ref items)==7)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-value-if', '''struct Borrowed { target:ref i32 }
func choose()->bool { return false }
func main() {
    var first=3
    var second=7
    let holder=if choose() { Borrowed { target:ref first } } else if choose() { Borrowed { target:ref first } } else { Borrowed { target:ref second } }
    second=9
    assert(holder.target==9)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-destructure-offset', '''struct Borrowed { target:ref i32 }
func main() {
    var first=3
    var second=7
    let (_, (holder,))=(Borrowed { target:ref first }, (Borrowed { target:ref second },))
    second=9
    assert(holder.target==9)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-caller-replacement', '''struct Borrowed { target:ref i32 }
func replace(holder:ref Borrowed, replacement:Borrowed) { holder=replacement }
func main() {
    var old=[3]
    var number=7
    var holder=Borrowed { target:ref old[0] }
    replace(ref holder,Borrowed { target:ref number })
    old.resize(9)
    assert(holder.target==7)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-transitive-replacement', '''struct Borrowed { target:ref i32 }
func forward(holder:ref Borrowed, replacement:Borrowed) { replace(ref holder,replacement) }
func replace(holder:ref Borrowed, replacement:Borrowed) { holder=replacement; return }
func main() {
    var old=[3]
    var number=7
    var holder=Borrowed { target:ref old[0] }
    forward(ref holder,Borrowed { target:ref number })
    old.resize(9)
    assert(holder.target==7)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-caller-swap', '''struct Borrowed { target:ref i32 }
struct Pair { first:Borrowed; second:Borrowed }
func swap(pair:ref Pair) {
    let saved=pair.first
    pair.first=pair.second
    pair.second=saved
}
func main() {
    var first=3
    var second=7
    var pair=Pair { first:Borrowed { target:ref first }, second:Borrowed { target:ref second } }
    swap(ref pair)
    assert(pair.first.target==7)
    assert(pair.second.target==3)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-live-referent', '''struct Borrowed { target:ref i32 }
struct View { holder:ref Borrowed }
func main() {
    var first=3
    var second=7
    var holder=Borrowed { target:ref first }
    let view=View { holder:ref holder }
    holder=Borrowed { target:ref second }
    assert(view.holder.target==7)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-referent-value-snapshot', '''struct Borrowed { target:ref i32 }
struct View { holder:ref Borrowed }
func main() {
    var first=3
    var second=7
    var holder=Borrowed { target:ref first }
    let view=View { holder:ref holder }
    let saved=view.holder
    holder=Borrowed { target:ref second }
    assert(saved.target==3)
    assert(view.holder.target==7)
    print("ok")
}
''', b'ok'),
    ('borrow-struct-referent-helper', '''struct Borrowed { target:ref i32 }
struct View { holder:ref Borrowed }
func read(view:View)->i32 { return view.holder.target }
func main() {
    var first=3
    var second=7
    var holder=Borrowed { target:ref first }
    let view=View { holder:ref holder }
    holder=Borrowed { target:ref second }
    assert(read(view)==7)
    print("ok")
}
''', b'ok'),

]

CASES.append(('borrow-struct-write-through-referent', '''struct Borrowed { target:ref i32 }
struct View { holder:ref Borrowed }
func main() {
    var old=[3]
    var number=7
    var holder=Borrowed { target:ref old[0] }
    let view=View { holder:ref holder }
    view.holder=Borrowed { target:ref number }
    old.resize(9)
    assert(holder.target==7)
    assert(view.holder.target==7)
    print("ok")
}
''', b'ok'))

NEGATIVE_CASES = [
    # A value-root result must preserve expiry after temporary-owner cleanup.
    ('borrow-struct-value-default-temporary-owner', '''value class Box {
    n:i32
    func Box(self) { self.n=7 }
}
struct Borrowed { target:ref i32 }
struct Wrapper { inner:Borrowed=Borrowed { target:ref Box().n } }
func main() { let holder=Wrapper {}; assert(holder.inner.target==7) }
''', 'LYC326'),
    # A counted receiver's temporary owner ends in the initializer; returning
    # its field address must not extend ownership or refresh the stale borrow.
    ('borrow-struct-default-temporary-owner', '''value class Box {
    n:i32
    func Box(self) { self.n=7 }
}
struct Borrowed { target:ref i32=ref Box().n }
func main() { let holder=Borrowed {}; assert(holder.target==7) }
''', 'LYC326'),
    ('borrow-struct-default-needs-explicit-ref', '''struct Borrowed { target:ref i32=7 }
func main() {}
''', 'LYC325'),
    # Type-level restrictions must hold without constructing an element, and
    # after all forward nominal fields have been collected.
    ('borrow-struct-empty-typed-array', '''struct Borrowed { target:ref i32 }
func main() { let values:[Borrowed]=[] }
''', 'LYC343'),
    ('borrow-struct-forward-array-field', '''struct Storage { values:[Borrowed] }
struct Borrowed { target:ref i32 }
func main() {}
''', 'LYC343'),
    ('borrow-struct-wrapped-array', '''struct Borrowed { target:ref i32 }
struct Wrapped { value:Borrowed }
func main() { let values:[(Wrapped,)]=[] }
''', 'LYC343'),
    # Caller-visible writes through a value parameter must retain invalidation.
    ('borrow-struct-value-parameter-write-invalidated', '''struct Borrowed { target:ref i32 }
struct Wrapper { holder:ref Borrowed }
func replace(wrapper:Wrapper, replacement:Borrowed) { wrapper.holder=replacement }
func main() {
    var number=3
    var items=[7]
    var holder=Borrowed { target:ref number }
    let wrapper=Wrapper { holder:ref holder }
    replace(wrapper,Borrowed { target:ref items[0] })
    items.resize(9)
    assert(holder.target==7)
}
''', 'LYC326'),
    ('borrow-struct-ambiguous-referent-write', '''struct Borrowed { target:ref i32 }
struct View { holder:ref Borrowed }
func choose()->bool { return true }
func main() {
    var old=[3]
    var number=7
    var first=Borrowed { target:ref old[0] }
    var second=Borrowed { target:ref old[0] }
    var view=View { holder:ref first }
    if choose() { view=View { holder:ref second } }
    view.holder=Borrowed { target:ref number }
    old.resize(9)
    assert(first.target==7)
}
''', 'LYC326'),
    ('borrow-struct-live-referent-invalidated', '''struct Borrowed { target:ref i32 }
struct View { holder:ref Borrowed }
func main() {
    var first=3
    var items=[7]
    var holder=Borrowed { target:ref first }
    let view=View { holder:ref holder }
    holder=Borrowed { target:ref items[0] }
    items.resize(9)
    assert(view.holder.target==7)
}
''', 'LYC326'),
    ('borrow-struct-return', '''struct Borrowed { target:ref i32 }
func escape()->Borrowed { var number=7; return Borrowed { target:ref number } }
func main() {}
''', 'LYC343'),
    ('borrow-struct-wrapped-return', '''struct Borrowed { target:ref i32 }
func escape()->(Borrowed,) { var number=7; return (Borrowed { target:ref number },) }
func main() {}
''', 'LYC343'),
    ('borrow-struct-array', '''struct Borrowed { target:ref i32 }
func main() { var number=7; let values=[Borrowed { target:ref number }]; }
''', 'LYC343'),
    ('borrow-struct-copy-invalidated', '''struct Borrowed { target:ref i32 }
func main() { var items=[7]; let holder=Borrowed { target:ref items[0] }; let copied=holder; items.resize(9); assert(copied.target==7); }
''', 'LYC326'),
    ('borrow-struct-call-invalidated', '''struct Borrowed { target:ref i32 }
func readAfter(holder:Borrowed,items:ref [i32])->i32 { items.resize(9); return holder.target }
func main() { var items=[7]; let holder=Borrowed { target:ref items[0] }; let result=readAfter(holder,ref items); }
''', 'LYC326'),
    ('borrow-struct-inner-local-escape', '''struct Borrowed { target:ref i32 }
func choose()->bool { return true }
func main() {
    var stable=3
    var holder=Borrowed { target:ref stable }
    if choose() {
        var temporary=7
        holder=Borrowed { target:ref temporary }
    }
    assert(holder.target==7)
}
''', 'LYC326'),
    ('borrow-struct-reference-parameter-escape', '''struct Borrowed { target:ref i32 }
func replace(holder:ref Borrowed) {
    var temporary=7
    holder=Borrowed { target:ref temporary }
}
func main() {
    var stable=3
    var holder=Borrowed { target:ref stable }
    replace(ref holder)
    assert(holder.target==7)
}
''', 'LYC326'),
    ('borrow-struct-second-nested-target-invalidated', '''struct Borrowed { target:ref i32 }
struct Pair { first:Borrowed; second:Borrowed }
func readSecond(pair:Pair)->i32 { return pair.second.target }
func main() {
    var first=[3]
    var second=[7]
    let pair=Pair { first:Borrowed { target:ref first[0] }, second:Borrowed { target:ref second[0] } }
    second.resize(9)
    let result=readSecond(pair)
}
''', 'LYC326'),
    ('borrow-struct-transitive-call-invalidated', '''struct Borrowed { target:ref i32 }
func grow(items:ref [i32]) { items.resize(9) }
func readAfter(holder:Borrowed,items:ref [i32])->i32 {
    grow(ref items)
    return holder.target
}
func main() {
    var items=[7]
    let holder=Borrowed { target:ref items[0] }
    let result=readAfter(holder,ref items)
}
''', 'LYC326'),
    ('borrow-struct-branch-replacement-invalidated', '''struct Borrowed { target:ref i32 }
func choose()->bool { return true }
func main() {
    var first=[3]
    var second=[7]
    var holder=Borrowed { target:ref first[0] }
    if choose() { holder=Borrowed { target:ref second[0] } }
    second.resize(9)
    assert(holder.target==7)
}
''', 'LYC326'),
    ('borrow-struct-later-argument-invalidated', '''struct Borrowed { target:ref i32 }
func grow(items:ref [i32])->i32 { items.resize(9); return 0 }
func read(holder:Borrowed,ignored:i32)->i32 { return holder.target }
func main() {
    var items=[7]
    let holder=Borrowed { target:ref items[0] }
    let result=read(holder,grow(ref items))
}
''', 'LYC326'),
    ('borrow-struct-value-if-invalidated', '''struct Borrowed { target:ref i32 }
func choose()->bool { return true }
func main() {
    var first=[3]
    var second=[7]
    let holder=if choose() { Borrowed { target:ref first[0] } } else { Borrowed { target:ref second[0] } }
    first.resize(9)
    assert(holder.target==3)
}
''', 'LYC326'),
    ('borrow-struct-value-if-local-expired', '''struct Borrowed { target:ref i32 }
func choose()->bool { return true }
func main() {
    var stable=3
    let holder=if choose() { var temporary=7; Borrowed { target:ref temporary } } else { Borrowed { target:ref stable } }
    assert(holder.target==7)
}
''', 'LYC326'),
    ('borrow-struct-destructure-invalidated', '''struct Borrowed { target:ref i32 }
func main() {
    var first=[3]
    var second=[7]
    let (_, (holder,))=(Borrowed { target:ref first[0] }, (Borrowed { target:ref second[0] },))
    second.resize(9)
    assert(holder.target==7)
}
''', 'LYC326'),
]

if __name__ == '__main__':
    sys.exit(main(CASES, NEGATIVE_CASES, 'borrow-containing-struct'))
