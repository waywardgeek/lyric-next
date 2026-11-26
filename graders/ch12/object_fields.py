#!/usr/bin/env python3
"""Retained object-field contracts; same exact execution/rejection contract as tuples.py.
Run from repository root: python3 graders/ch12/object_fields.py COMPILER [ARG ...]
These fixtures specify behavior; their existence does not establish implementation.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch10'))
from tuples import main

BOX = '''class Box {
    n:i32
    func Box(self, n:i32) { self.n=n }
}
'''
COUNTED = '''value class Box {
    func Box(self) {}
    final func finish(self) { print("drop") }
}
'''
CASES = [
    ('object-field-nonnull', BOX + '''class Holder {
        item:Box
        func Holder(self, item:Box) { self.item=item }
    }
    func main() { let box=Box(7); let h=Holder(box); assert(h.item.n==7); h.destroy(); assert(box.n==7); print("ok") }
    ''', b'ok'),
    ('object-field-nullable', BOX + '''class Holder {
        item:Box?
        func Holder(self) { self.item=null }
    }
    func main() { let h=Holder(); h.item=Box(9); assert(h.item!.n==9); h.item=null; h.destroy(); print("ok") }
    ''', b'ok'),
    # Replacement releases the old field owner; destruction releases its successor.
    ('object-field-counted-replacement', COUNTED + '''class Holder {
        item:Box?
        func Holder(self) { self.item=null }
    }
    func main() { let h=Holder(); h.item=Box(); print("one"); h.item=Box(); print("two"); h.destroy(); print("done") }
    ''', b'onedroptwodropdone'),
    ('object-field-counted-nonnull', COUNTED + '''value class Holder {
        item:Box
        func Holder(self) { self.item=Box() }
        final func finish(self) { print("holder") }
    }
    func use() { let h=Holder(); print("live") }
    func main() { use(); print("done") }
    ''', b'liveholderdropdone'),
]
# Ordinary links are writable at this intermediate milestone. These fixtures
# explicitly detach; they do not certify persistent attachment tracking or
# automatic teardown. Existing local-borrow and initialization rules still apply.
CASES += [
    ('object-field-detach-through-alias', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func main() {
        let box=Box(7); let h=Holder(box); let alias=h
        assert(alias.item!.n==7)
        alias.item=null
        box.destroy(); h.destroy(); print("ok")
    }
    ''', b'ok'),
    ('object-field-detach-through-call', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func detach(h:Holder) { h.item=null }
    func main() {
        let box=Box(7); let h=Holder(box)
        detach(h); box.destroy(); h.destroy(); print("ok")
    }
    ''', b'ok'),
    ('object-field-detach-and-destroy-in-call', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func dispose(h:Holder, box:Box) { h.item=null; box.destroy() }
    func main() {
        let box=Box(7); let h=Holder(box)
        dispose(h,box); h.destroy(); print("ok")
    }
    ''', b'ok'),
    ('object-field-detach-through-returned-alias', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func identity(h:Holder)->Holder { return h }
    func main() {
        let box=Box(7); let h=Holder(box); let alias=identity(h)
        alias.item=null
        box.destroy(); h.destroy(); print("ok")
    }
    ''', b'ok'),
]
CASES += [
    ('object-field-counted-copy-outlives-holder', COUNTED + '''class Holder {
        item:Box
        func Holder(self) { self.item=Box() }
    }
    func use() { let h=Holder(); let saved=h.item; h.destroy(); print("saved") }
    func main() { use(); print("done") }
    ''', b'saveddropdone'),
    ('object-field-counted-struct-holder', COUNTED + '''struct Holder { item:Box }
    func make()->Holder { return Holder { item:Box() } }
    func use() { let h=make(); let copy=h; print("saved") }
    func main() { use(); print("done") }
    ''', b'saveddropdone'),
]
COUNTED_NEGATIVE_CASES = [
    ('object-field-counted-missing-initialization', COUNTED + 'class Holder { item:Box; func Holder(self) {} }; func main() {}', 'LYC328'),
    ('object-field-counted-read-before-initialization', COUNTED + 'class Holder { item:Box; func Holder(self) { let x=self.item; self.item=Box() } }; func main() {}', 'LYC328'),
    ('object-field-counted-zero-not-handle', COUNTED + 'class Holder { item:Box; func Holder(self) { self.item=0 } }; func main() {}', 'LYC304'),
    ('object-field-counted-null-not-nonnull', COUNTED + 'class Holder { item:Box; func Holder(self) { self.item=null } }; func main() {}', 'LYC304'),
    ('object-field-counted-explicit-destroy', COUNTED + '''class Holder {
        item:Box
        func Holder(self) { self.item=Box() }
    }
    func main() { let h=Holder(); h.item.destroy() }
    ''', 'LYC331'),
]

NEGATIVE_CASES = [
    ('object-field-missing-initialization', BOX + 'class Holder { item:Box; func Holder(self) {} }; func main() {}', 'LYC328'),
    ('object-field-read-before-initialization', BOX + 'class Holder { item:Box; func Holder(self) { let x=self.item; self.item=Box(1) } }; func main() {}', 'LYC328'),
    ('object-field-zero-not-handle', BOX + 'class Holder { item:Box; func Holder(self) { self.item=0 } }; func main() {}', 'LYC304'),
    ('object-field-null-not-nonnull', BOX + 'class Holder { item:Box; func Holder(self) { self.item=null } }; func main() {}', 'LYC304'),

]

if __name__ == '__main__':
    raise SystemExit(main(CASES, NEGATIVE_CASES + COUNTED_NEGATIVE_CASES, 'object-field'))
