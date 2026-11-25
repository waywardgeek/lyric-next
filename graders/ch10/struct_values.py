#!/usr/bin/env python3
"""Retained Chapter 10 struct-value execution checks, under development.

Run: python3 graders/ch10/struct_values.py COMPILER [ARG ...]
Uses the tuple runner's exact-byte, exit-status, empty-stderr and O0/O2
contract. Requires Python 3 and a C11 compiler; no downloads.
"""
import sys
from tuples import main

# All cases are retained. Each assertion observes actual source-level values.
CASES = [
    ('struct-empty', 'struct Empty {}\nfunc main() { let value=Empty {}; print("ok") }', b'ok'),
    ('struct-named-order', """struct Point { x:i32; y:i32 }
func main() { let point=Point { y:9,x:4 }; assert(point.x==4); assert(point.y==9); print("ok") }
""", b'ok'),
    ('struct-snapshots', """struct Samples { first:i32; middle:i32; last:i32 }
func change(x:ref i32)->i32 { x=9; return 2 }
func main() {
    var x=1
    let value=Samples { last:x,middle:change(ref x),first:x }
    assert(value.last==1)
    assert(value.middle==2)
    assert(value.first==9)
    print("ok")
}
""", b'ok'),
    ('struct-nested', """struct Inner { n:i32 }
struct Outer { inner:Inner; flag:bool }
func main() { let value=Outer { flag:true,inner:Inner { n:7 } }; assert(value.inner.n==7); assert(value.flag); print("ok") }
""", b'ok'),
    ('struct-array-copy', """struct Buffer { items:[i32] }
func main() {
    var original=Buffer { items:[1,2] }
    var copied=original
    copied.items[0]=9
    assert(original.items[0]==1)
    assert(copied.items[0]==9)
    print("ok")
}
""", b'ok'),
]

CASES.extend([
    ('struct-field-mutation', """struct Point { x:i32 }
func main() { var p=Point { x:1 }; p.x=7; assert(p.x==7); print("ok") }
""", b'ok'),
    ('struct-counted-copy', """value class Box {
    n:i32
    func Box(self) { self.n=7 }
    final func finish(self) { print("drop") }
}
struct Holder { item:Box }
func use() { let first=Holder { item:Box() }; let second=first; assert(second.item.n==7); print("live") }
func main() { use(); print("done") }
""", b'livedropdone'),
    ('struct-last-use-before-resize', """struct Buffer { items:[i32] }
func main() { var value=Buffer { items:[1] }; let item=ref value.items[0]; assert(item==1); value.items.resize(3); print("ok") }
""", b'ok'),
])

# Located semantic diagnostics are required; unrelated failures do not pass.
NEGATIVE_CASES = [
    ('struct-missing', 'struct P { x:i32 }; func main() { let p=P {} }', 'LYC304'),
    ('struct-duplicate', 'struct P { x:i32 }; func main() { let p=P { x:1,x:2 } }', 'LYC302'),
    ('struct-unknown-field', 'struct P { x:i32 }; func main() { let p=P { y:1 } }', 'LYC329'),
    ('struct-field-type', 'struct P { x:i32 }; func main() { let p=P { x:false } }', 'LYC304'),
    ('struct-nominal-mismatch', 'struct P { x:i32 }; struct Q { x:i32 }; struct H { p:P }; func main() { let h=H { p:Q { x:1 } } }', 'LYC304'),
    ('struct-immutable-field', 'struct P { x:i32 }; func main() { let p=P { x:1 }; p.x=2 }', 'LYC308'),
    ('struct-array-borrow-invalidated', 'struct B { items:[i32] }; func main() { var b=B { items:[1] }; let item=ref b.items[0]; b.items.resize(3); assert(item==1) }', 'LYC326'),
]

# Ordinary handles do not own their objects; counted sibling cleanup can still
# invalidate them. These checks must reject during checking, never execute UB.
OBJECT = 'class Item { n:i32; func Item(self) { self.n=7 } }; '
FINALIZER = ('value class Drop { func Drop(self) {}; '
             'final func finish(self) { let other=Item(); other.destroy() } }; ')
CASES.append(('struct-ordinary-handle', OBJECT + 'struct H { item:Item }; '
    'func main() { let item=Item(); let h=H { item:item }; assert(h.item.n==7); item.destroy(); print("ok") }', b'ok'))
NEGATIVE_CASES.extend([
    ('struct-stale-handle', OBJECT + 'struct H { item:Item }; '
     'func main() { let item=Item(); let h=H { item:item }; item.destroy(); assert(h.item.n==7) }', 'LYC330'),
    ('struct-later-field-invalidates', OBJECT + 'struct H { item:Item; n:i32 }; '
     'func change(item:Item)->i32 { item.destroy(); return 0 }; '
     'func main() { let item=Item(); let h=H { item:item,n:change(item) } }', 'LYC330'),
    ('struct-partial-sibling-cleanup', OBJECT + FINALIZER + 'struct H { items:[i32]; drop:Drop }; '
     'func make()->Item { var h=H { items:[1],drop:Drop() }; h.items=[]; return Item() }; '
     'func main() { let item=make(); assert(item.n==7) }', 'LYC330'),
])

# Typed interfaces preserve nominal identity and the existing value/ref boundary.
CASES.extend([
    ('struct-typed-local', 'struct P { x:i32 }; func main() { let p:P=P { x:7 }; assert(p.x==7); print("ok") }', b'ok'),
    ('struct-value-return-copy', """struct B { items:[i32] }
func copy(value:B)->B { return value }
func main() {
    let original:B=B { items:[1,2] }
    var result:B=copy(original)
    result.items[0]=9
    assert(original.items[0]==1)
    assert(result.items[0]==9)
    print("ok")
}
""", b'ok'),
    ('struct-reference-parameter', 'struct P { x:i32 }; func change(p:ref P) { p.x=9 }; func main() { var p:P=P { x:1 }; change(ref p); assert(p.x==9); print("ok") }', b'ok'),
    ('struct-typed-reference-local', 'struct P { x:i32 }; func main() { var p:P=P { x:1 }; let alias:ref P=ref p; alias.x=8; assert(p.x==8); print("ok") }', b'ok'),
    ('struct-empty-return', 'struct E {}; func make()->E { return E {} }; func main() { let value:E=make(); print("ok") }', b'ok'),
])
NEGATIVE_CASES.extend([
    ('struct-parameter-nominal-mismatch', 'struct P { x:i32 }; struct Q { x:i32 }; func use(p:P) {}; func main() { use(Q { x:1 }) }', 'LYC304'),
    ('struct-return-nominal-mismatch', 'struct P { x:i32 }; struct Q { x:i32 }; func make()->P { return Q { x:1 } }; func main() {}', 'LYC304'),
])

# Returning an aggregate transfers its owner before parameter/local cleanup.
CASES.append(('struct-counted-return', """value class Box {
    n:i32
    func Box(self) { self.n=7 }
    final func finish(self) { print("drop") }
}
struct H { item:Box }
func make()->H { return H { item:Box() } }
func copy(value:H)->H { return value }
func use() { let first:H=make(); let second:H=copy(first); assert(second.item.n==7); print("live") }
func main() { use(); print("done") }
""", b'livedropdone'))
NEGATIVE_CASES.append(('struct-return-parameter-cleanup', OBJECT + FINALIZER +
    'struct H { item:Item }; struct D { drop:Drop }; '
    'func make(value:D)->H { return H { item:Item() } }; '
    'func main() { let h=make(D { drop:Drop() }); assert(h.item.n==7) }', 'LYC330'))

CASES.extend([
    ('struct-typed-tuple-destructure', 'struct P { x:i32 }; func main() { let (p,n):(P,i32)=(P { x:7 },2); assert(p.x==7); assert(n==2); print("ok") }', b'ok'),
    ('struct-array-value-interface', """struct B { items:[i32] }
func copy(values:[B])->[B] { return values }
func main() {
    let original:[B]=[B { items:[1] }]
    var result:[B]=copy(original)
    result[0].items[0]=9
    assert(original[0].items[0]==1)
    assert(result[0].items[0]==9)
    print("ok")
}
""", b'ok'),
])

# Defaults execute at construction, after supplied expressions, not at declaration.
CASES.extend([
    ('struct-default-order', """func first()->i32 { print("A"); return 1 }
func second()->i32 { print("B"); return 2 }
func supplied()->i32 { print("S"); return 9 }
struct P { x:i32=first(); y:i32=second(); z:i32 }
func main() {
    let a=P { z:supplied() }
    let b=P { y:supplied(),z:supplied() }
    assert(a.x==1); assert(a.y==2); assert(a.z==9)
    assert(b.x==1); assert(b.y==9); assert(b.z==9)
}
""", b'SABSSA'),
    ('struct-default-fresh-array', """func fresh()->[i32] { print("F"); return [1] }
struct B { items:[i32]=fresh() }
func main() {
    var a=B {}
    let b=B {}
    a.items[0]=9
    assert(a.items[0]==9); assert(b.items[0]==1)
    print("ok")
}
""", b'FFok'),
    ('struct-default-all-supplied', 'func fallback()->i32 { print("bad"); return 1 }; struct P { x:i32=fallback() }; func main() { let p=P { x:7 }; assert(p.x==7); print("ok") }', b'ok'),
])
NEGATIVE_CASES.extend([
    ('struct-default-type', 'struct P { x:i32=false }; func main() { let p=P { x:1 } }', 'LYC304'),
    ('struct-default-no-sibling', 'struct P { x:i32=1; y:i32=x }; func main() {}', 'LYC301'),
    ('struct-default-no-caller-local', 'struct P { x:i32=caller }; func main() { let caller=7; let p=P {} }', 'LYC301'),
])

# Initializer invocations participate in the ordinary effect fixed point.
# Negative cases are checked only: stale-handle programs must never execute.
CASES.extend([
    ('struct-default-nested', 'struct Inner { x:i32=7 }; struct Outer { inner:Inner=Inner {} }; func main() { let o=Outer {}; assert(o.inner.x==7); print("ok") }', b'ok'),
    ('struct-default-counted', 'value class Box { func Box(self) {}; final func finish(self) { print("drop") } }; struct H { item:Box=Box() }; func use() { let a=H {}; let b=a; print("live") }; func main() { use(); print("done") }', b'livedropdone'),
    ('struct-default-skipped-effect', OBJECT + 'func damage()->i32 { let other=Item(); other.destroy(); return 0 }; struct H { item:Item; n:i32=damage() }; func main() { let item=Item(); let h=H { item:item,n:7 }; assert(h.item.n==7); item.destroy(); print("ok") }', b'ok'),
])
NEGATIVE_CASES.extend([
    ('struct-default-invalidates-explicit', OBJECT + 'func damage()->i32 { let other=Item(); other.destroy(); return 0 }; struct H { item:Item; n:i32=damage() }; func main() { let item=Item(); let h=H { item:item } }', 'LYC330'),
    ('struct-default-invalidates-earlier-default', OBJECT + 'func damage()->i32 { let other=Item(); other.destroy(); return 0 }; struct H { item:Item=Item(); n:i32=damage() }; func main() { let h=H {} }', 'LYC330'),
    ('struct-default-return-cleanup', OBJECT + FINALIZER + 'func make(drop:Drop)->Item { return Item() }; struct H { item:Item=make(Drop()) }; func main() {}', 'LYC330'),
])

NEGATIVE_CASES.extend([
    ('struct-default-own-cleanup', OBJECT + FINALIZER + 'struct H { item:Item=if true { let drop=Drop(); Item() } else { Item() } }; func main() {}', 'LYC330'),
    ('struct-default-growth-borrow', OBJECT + 'struct H { item:Item=Item() }; func main() { let item=Item(); let borrowed=ref item.n; let h=H {}; assert(borrowed==7) }', 'LYC326'),
])

CASES.append(('struct-default-conditional-owner', 'struct H { items:[i32]=if true { let data=[7]; data } else { [9] } }; func main() { var a=H {}; let b=H {}; a.items[0]=3; assert(b.items[0]==7); print("ok") }', b'ok'))

# Inline methods establish receiver behavior before separate-body matching.
CASES.append(('struct-method-bodies-emitted', '''struct P {
    x:i32
    func get(self)->i32 { return self.x }
    func set(self:ref P,n:i32) { self.x=n }
    func origin()->P { return P { x:0 } }
}
func main() { print("ok") }
''', b'ok'))
CASES.extend([
    ('struct-method-value', '''struct Point {
    x:i32
    func get(self)->i32 { return self.x }
}
func main() { let p=Point { x:7 }; assert(p.get()==7); print("ok") }
''', b'ok'),
    ('struct-method-ref', '''struct Point {
    x:i32
    func set(self:ref Point,n:i32) { self.x=n }
}
func main() { var p=Point { x:1 }; (ref p).set(7); assert(p.x==7); print("ok") }
''', b'ok'),
    ('struct-method-static', '''struct Point {
    x:i32
    func origin()->Point { return Point { x:0 } }
}
func main() { let p=Point.origin(); assert(p.x==0); print("ok") }
''', b'ok'),
    ('struct-method-owned-copy', '''struct Buffer {
    items:[i32]
    func changed(self)->[i32] { var copy=self; copy.items[0]=9; return copy.items }
}
func main() {
    let original=Buffer { items:[1] }
    let result=original.changed()
    assert(original.items[0]==1)
    assert(result[0]==9)
    print("ok")
}
''', b'ok'),
])

# Receiver modes are explicit; saved addresses must survive later arguments.
NEGATIVE_CASES.extend([
    ('struct-method-no-autoborrow', '''struct P { x:i32; func set(self:ref P,n:i32) { self.x=n } }
func main() { var p=P { x:1 }; p.set(2) }
''', 'LYC325'),
    ('struct-method-no-ref-to-value', '''struct P { x:i32; func get(self)->i32 { return self.x } }
func main() { var p=P { x:1 }; (ref p).get() }
''', 'LYC325'),
    ('struct-method-immutable-ref', '''struct P { x:i32; func set(self:ref P,n:i32) { self.x=n } }
func main() { let p=P { x:1 }; (ref p).set(2) }
''', 'LYC308'),
    ('struct-method-type-needs-receiver', '''struct P { x:i32; func get(self)->i32 { return self.x } }
func main() { P.get() }
''', 'LYC304'),
    ('struct-method-later-growth', '''struct P { x:i32; func set(self:ref P,n:i32) { self.x=n } }
func grow(a:ref [P])->i32 { a.reserve(100); return 2 }
func main() { var a=[P { x:1 }]; (ref a[0]).set(grow(ref a)) }
''', 'LYC326'),
])
CASES.append(('struct-method-value-snapshot', '''struct P {
    x:i32
    func before(self,n:i32)->i32 { return self.x }
}
func change(p:ref P)->i32 { p.x=9; return 0 }
func main() { var p=P { x:1 }; assert(p.before(change(ref p))==1); assert(p.x==9); print("ok") }
''', b'ok'))

# Out-of-line bodies implement the original callable, independent of source order.
CASES.extend([
    ('struct-separate-value', """struct P { x:i32; func sum(self, amount:i32)->i32; }
func P.sum(self, delta:i32)->i32 { return self.x+delta }
func main() { let p=P { x:4 }; assert(p.sum(3)==7); print("ok") }
""", b'ok'),
    ('struct-separate-ref', """struct P { x:i32; func set(self:ref P, n:i32); }
func P.set(self:ref P, value:i32) { self.x=value }
func main() { var p=P { x:1 }; (ref p).set(8); assert(p.x==8); print("ok") }
""", b'ok'),
    ('struct-separate-static-before', """func P.make(n:i32)->P { return P { x:n } }
struct P { x:i32; func make(value:i32)->P; }
func main() { let p=P.make(6); assert(p.x==6); print("ok") }
""", b'ok'),
    ('struct-separate-owned', """struct Buffer { items:[i32]; func copy(self)->[i32]; }
func Buffer.copy(self)->[i32] { return self.items }
func main() { let b=Buffer { items:[2] }; var a=b.copy(); a[0]=9; assert(b.items[0]==2); print("ok") }
""", b'ok'),
])

# Matching failures are deliberate diagnostics, never a new overload or crash.
NEGATIVE_CASES.extend([
    ('struct-body-missing', 'struct P { func f()->i32; }\nfunc main() {}', 'LYC342'),
    ('struct-body-duplicate', 'struct P { func f() {} }\nfunc P.f() {}\nfunc main() {}', 'LYC342'),
    ('struct-body-twice', 'struct P { func f(); }\nfunc P.f() {}\nfunc P.f() {}\nfunc main() {}', 'LYC342'),
    ('struct-body-undeclared', 'struct P {}\nfunc P.f() {}\nfunc main() {}', 'LYC342'),
    ('struct-body-arity', 'struct P { func f(n:i32); }\nfunc P.f() {}\nfunc main() {}', 'LYC342'),
    ('struct-body-type', 'struct P { func f(n:i32); }\nfunc P.f(n:i64) {}\nfunc main() {}', 'LYC342'),
    ('struct-body-result', 'struct P { func f()->i32; }\nfunc P.f()->i64 { return 1 }\nfunc main() {}', 'LYC342'),
    ('struct-body-reference', 'struct P { func f(n:ref i32); }\nfunc P.f(n:i32) {}\nfunc main() {}', 'LYC342'),
    ('struct-body-receiver', 'struct P { func f(self); }\nfunc P.f(self:ref P) {}\nfunc main() {}', 'LYC342'),
    ('struct-body-static', 'struct P { func f(self); }\nfunc P.f(other:P) {}\nfunc main() {}', 'LYC342'),
])

NEGATIVE_CASES.extend([
    ('struct-body-declaration-shadow', 'func taken() {}\nstruct P { func f(taken:i32); }\nfunc P.f(value:i32) {}\nfunc main() {}', 'LYC302'),
    ('struct-body-parameter-duplicate', 'struct P { func f(a:i32,b:i32); }\nfunc P.f(value:i32,value:i32) {}\nfunc main() {}', 'LYC302'),
    ('struct-body-return-checked', 'struct P { func f()->i32; }\nfunc P.f()->i32 { return true }\nfunc main() {}', 'LYC304'),
])

if __name__ == '__main__':
    sys.exit(main(CASES, NEGATIVE_CASES, 'struct-value'))
