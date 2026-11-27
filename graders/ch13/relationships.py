#!/usr/bin/env python3
"""Retained Chapter 13 relationship execution contracts.

Invoke from repository root with COMPILER [ARG ...]. Uses the public tuple
harness: emit C with exit 0/empty stderr, compile/run at O0 and O2, then
require exit 0, exact stdout bytes, and empty stderr. No normalization.
These first cases exercise real teardown, not parser-only acceptance.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ch10"))
from tuples import main

# Standalone public fixture source, not an import of an implementation or notes.
LIST = """interface DoublyLinkedList<parent P, child C> {
    class P {
        first: C?
        last: C?

        pub add func append(self, child: C) {
            prepare {
                assert(child.parent == null)
            }
            child.parent = self
            child.previous = self.last
            child.next = null
            if self.last != null {
                self.last!.next = child
            } else {
                self.first = child
            }
            self.last = child
        }

        pub remove func remove(self, child: C) {
            prepare {
                assert(child.parent == self)
            }
            if child.previous != null {
                child.previous!.next = child.next
            } else {
                self.first = child.next
            }
            if child.next != null {
                child.next!.previous = child.previous
            } else {
                self.last = child.previous
            }
            child.parent = null
            child.previous = null
            child.next = null
        }

        partial owns destroy(self) {
            while self.first != null {
                self.first!.destroy()
            }
        }

        partial refs destroy(self) {
            while self.first != null {
                self.remove(self.first!)
            }
        }
    }
    class C {
        parent: P?
        previous: C?
        next: C?

        partial owns destroy(self) {
            if self.parent != null {
                self.parent!.remove(self)
            }
        }

        partial refs destroy(self) {
            if self.parent != null {
                self.parent!.remove(self)
            }
        }
    }
}

"""

CLASSES = """class Graph {
    func Graph(self) {}
    final func finish(self) {
        assert(self.nodes.first != null)
        print("G")
    }
}
class Node {
    func Node(self) {}
    final func finish(self) {
        assert(self.graph.parent != null)
        print("N")
    }
}
impl DoublyLinkedList<Graph:nodes, Node:graph> owns {}
"""

CASES = [
    # An ordinary caller owns a returned participant independently of attachment.
    ("refs-counted-return-to-application", LIST.replace(
        "        first: C?", "        pub func first_child(self)->C { return self.first! }\n        first: C?", 1) + """class Graph {
        func Graph(self) {}
    }
    value class Node {
        func Node(self) {}
        pub func show(self) { print("held") }
        final func finish(self) {
            assert(self.graph.parent == null)
            print("N")
        }
    }
    impl DoublyLinkedList<Graph:nodes, Node:graph> refs {}
    func main() {
        let graph=Graph()
        if true {
            let child=Node()
            graph.nodes.append(child)
        }
        if true {
            let saved=graph.nodes.first_child()
            graph.nodes.remove(saved)
            saved.show()
        }
        print("done")
        graph.destroy()
    }
    """, b"heldNdone"),
    # The membership, not the expired local holder, keeps Node alive. Its final
    # must observe detached links because remove releases after structural edits.
    ("refs-counted-membership-lifetime", LIST + """class Graph {
        func Graph(self) {}
        final func finish(self) {
            assert(self.nodes.first == null)
            assert(self.nodes.last == null)
        }
    }
    value class Node {
        func Node(self) {}
        final func finish(self) {
            assert(self.graph.parent == null)
            assert(self.graph.previous == null)
            assert(self.graph.next == null)
            print("N")
        }
    }
    impl DoublyLinkedList<Graph:nodes, Node:graph> refs {}
    func main() {
        let graph=Graph()
        if true {
            let child=Node()
            graph.nodes.append(child)
        }
        print("live")
        graph.nodes.remove(graph.nodes.first!)
        print("done")
        graph.destroy()
    }
    """, b"liveNdone"),
    ("refs-counted-independent-holder", LIST + """class Graph {
        func Graph(self) {}
    }
    value class Node {
        func Node(self) {}
        pub func show(self) { print("held") }
        final func finish(self) {
            assert(self.graph.parent == null)
            print("N")
        }
    }
    impl DoublyLinkedList<Graph:nodes, Node:graph> refs {}
    func main() {
        let graph=Graph()
        if true {
            let child=Node()
            graph.nodes.append(child)
            graph.nodes.remove(child)
            child.show()
        }
        print("done")
        graph.destroy()
    }
    """, b"heldNdone"),
    ("refs-counted-parent-teardown-held-child", LIST + """class Graph {
        func Graph(self) {}
        final func finish(self) {
            assert(self.nodes.first != null)
            print("G")
        }
    }
    value class Node {
        func Node(self) {}
        pub func show(self) { print("held") }
        final func finish(self) {
            assert(self.graph.parent == null)
            assert(self.graph.previous == null)
            assert(self.graph.next == null)
            print("N")
        }
    }
    impl DoublyLinkedList<Graph:nodes, Node:graph> refs {}
    func main() {
        if true {
            let graph=Graph()
            let child=Node()
            graph.nodes.append(child)
            graph.destroy()
            assert(child.graph.parent == null)
            assert(child.graph.previous == null)
            assert(child.graph.next == null)
            child.show()
        }
        print("done")
    }
    """, b"GheldNdone"),
    ("refs-counted-two-memberships", LIST + """class Graph {
        func Graph(self) {}
    }
    value class Node {
        func Node(self) {}
        pub func show(self) { print("held") }
        final func finish(self) {
            assert(self.graph.parent == null)
            assert(self.selection.parent == null)
            print("N")
        }
    }
    impl DoublyLinkedList<Graph:nodes, Node:graph> refs {}
    impl DoublyLinkedList<Graph:selected, Node:selection> refs {}
    func main() {
        let graph=Graph()
        if true {
            let child=Node()
            graph.nodes.append(child)
            graph.selected.append(child)
        }
        graph.nodes.remove(graph.nodes.first!)
        assert(graph.nodes.first == null)
        assert(graph.nodes.last == null)
        if true {
            // Observation reads create normal counted temporaries. End their
            // scope before testing release of the last membership reference.
            assert(graph.selected.first != null)
            assert(graph.selected.first!.graph.parent == null)
            assert(graph.selected.first!.selection.parent == graph)
            graph.selected.first!.show()
        }
        graph.selected.remove(graph.selected.first!)
        print("done")
        graph.destroy()
    }
    """, b"heldNdone"),
    ("refs-counted-parent-zero-count-teardown", LIST + """value class Graph {
        func Graph(self) {}
        final func finish(self) {
            assert(self.nodes.first != null)
            print("G")
        }
    }
    value class Node {
        func Node(self) {}
        pub func show(self) { print("held") }
        final func finish(self) {
            assert(self.graph.parent == null)
            print("N")
        }
    }
    impl DoublyLinkedList<Graph:nodes, Node:graph> refs {}
    func main() {
        if true {
            let child=Node()
            if true {
                let graph=Graph()
                graph.nodes.append(child)
            }
            assert(child.graph.parent == null)
            child.show()
        }
        print("done")
    }
    """, b"GheldNdone"),
    ("relationship-prepare-deferred-cleanup", """interface Link<parent P, child C> {
        class P {
            pub add func attach(self, child:C) {
                prepare {
                    let held=Held()
                    var index=0u32
                    while index<2u32 { index=index+1u32 }
                    assert(index==2u32)
                }
                held.show()
            }
        }
        class C {}
    }
    interface Cleanup<parent P, child C> {
        class P {
            first:C?
            partial refs destroy(self) { self.first=null; print("drop") }
        }
        class C {}
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    value class Held {
        func Held(self) {}
        pub func show(self) { print("body") }
    }
    impl Link<Graph:nodes, Node:graph> owns {}
    impl Cleanup<Held:links, Node:held> refs {}
    func main() {
        let graph=Graph()
        let node=Node()
        graph.nodes.attach(node)
        print("done")
        node.destroy()
        graph.destroy()
    }
    """, b"bodydropdone"),
    ("relationship-prepare-local-lifetime", """interface Link<parent P, child C> {
        class P {
            pub add func attach(self, child: C) {
                prepare { let held=Held(); let number=7u32 }
                assert(number==7u32)
                held.show()
                print("body")
            }
        }
        class C {}
    }
    value class Held {
        func Held(self) {}
        pub func show(self) { print("live") }
        final func finish(self) { print("drop") }
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {
        let graph=Graph()
        let node=Node()
        graph.nodes.attach(node)
        print("done")
        node.destroy()
        graph.destroy()
    }
    """, b"livebodydropdone"),
    ("relationship-detached-storage", """interface Link<parent P, child C> {
        class P { first: C?; pub func clear(self) { self.first=null } }
        class C { parent: P? }
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {
        let graph=Graph()
        let node=Node()
        graph.nodes.clear()
        assert(graph.nodes.first==null)
        assert(node.graph.parent==null)
        node.destroy()
        graph.destroy()
        print("detached")
    }
    """, b"detached"),
    # Source order is parent contribution first, but teardown must detach the
    # child occurrence first. Unselected refs bodies must never execute.
    ("relationship-fragment-phase-order", """interface Link<parent P, child C> {
        class P {
            partial owns destroy(self) { print("P") }
            partial refs destroy(self) { print("wrong") }
        }
        class C {
            partial owns destroy(self) { print("C") }
            partial refs destroy(self) { print("wrong") }
        }
    }
    class Hub {
        func Hub(self) {}
        final func finish(self) { print("F") }
    }
    class Other { func Other(self) {} }
    impl Link<Hub, Other> owns {}
    impl Link<Other:up, Hub:down> owns {}
    func main() {
        let hub=Hub()
        hub.destroy()
        print("done")
    }
    """, b"FCPdone"),
    # B belongs to both Graph and A. Destroying A drains B too, so Graph's
    # destructor must reread its maintained first link rather than save B.
    ("relationship-cross-membership-cascade", LIST + """class Graph {
        func Graph(self) {}
        final func finish(self) {
            assert(self.nodes.first != null)
            print("G")
        }
    }
    class Node {
        id:u32
        func Node(self, id:u32) { self.id=id }
        final func finish(self) {
            assert(self.graph.parent != null)
            if self.id == 1u32 {
                assert(self.children.first != null)
                print("A")
            } else {
                assert(self.owner.parent != null)
                print("B")
            }
        }
    }
    impl DoublyLinkedList<Graph:nodes, Node:graph> owns {}
    impl DoublyLinkedList<Node:children, Node:owner> owns {}
    func main() {
        let graph=Graph()
        let first=Node(1u32)
        let second=Node(2u32)
        graph.nodes.append(first)
        graph.nodes.append(second)
        first.children.append(second)
        graph.destroy()
        print("done")
    }
    """, b"GABdone"),
    # Both node finals must observe their membership. Parent final precedes them.
    ("relationship-owning-parent-cascade", LIST + CLASSES + """func main() {
        let graph=Graph()
        let first=Node()
        let second=Node()
        assert(graph.nodes.first==null)
        assert(first.graph.parent==null)
        graph.nodes.append(first)
        graph.nodes.append(second)
        graph.destroy()
        print("done")
    }
""", b"GNNdone"),
    # A surviving parent must be updated by the child's generated fragment.
    ("relationship-child-detaches", LIST + CLASSES.replace(
        'assert(self.nodes.first != null)',
        'assert(self.nodes.first == null); assert(self.nodes.last == null)') + """func main() {
        let graph=Graph()
        let child=Node()
        graph.nodes.append(child)
        child.destroy()
        assert(graph.nodes.first==null)
        assert(graph.nodes.last==null)
        graph.destroy()
        print("done")
    }
""", b"NGdone"),
]

# Same lifetime contract through an ordinary relationship helper. This challenges
# transitive receiver simplification rather than a special case for remove calls.
_counted_parent = next(source for name, source, expected in CASES
                       if name == "refs-counted-parent-zero-count-teardown")
_helper_parent = _counted_parent.replace(
    "        partial refs destroy(self) {\n            while self.first != null {\n                self.remove(self.first!)\n            }\n        }",
    "        pub func drain_one(self) { self.remove(self.first!) }\n"
    "        partial refs destroy(self) {\n            while self.first != null {\n"
    "                self.drain_one()\n            }\n        }")
assert _helper_parent != _counted_parent
CASES.append(("refs-counted-parent-helper-teardown", _helper_parent, b"GheldNdone"))

# Compile-only failures: a crash or unrelated diagnostic is not a pass.
# A local writable alias must preserve managed-link storage semantics. It
# neither acquires a second membership nor releases the previous structural link.
_alias_source = next(source for name, source, expected in CASES
                     if name == "refs-counted-membership-lifetime")
_alias_source = _alias_source.replace(
    "self.first = child\n", "let first_slot = ref self.first; first_slot = child\n")
CASES.append(("refs-counted-local-managed-alias", _alias_source, b"liveNdone"))
_direct_source = next(source for name, source, expected in CASES
                      if name == "refs-counted-membership-lifetime")
_remove_alias = _direct_source.replace(
    "self.first = child.next",
    "let first_slot = ref self.first; first_slot = child.next")
CASES.append(("refs-counted-managed-removal-alias", _remove_alias, b"liveNdone"))
_chain_alias = _alias_source.replace(
    "first_slot = child\n",
    "let second_slot = ref first_slot; second_slot = child\n")
CASES.append(("refs-counted-chained-managed-alias", _chain_alias, b"liveNdone"))


# A relationship helper forwards a writable participant link. Insertion through
# this parameter and direct removal must not accidentally acquire a second owner.
_helper_link = _direct_source.replace(
    "        pub add func append(self, child: C) {",
    "        func write_link(self, slot: ref C?, child: C) { slot = child }\n"
    "        pub add func append(self, child: C) {")
_helper_link = _helper_link.replace(
    "self.first = child\n", "self.write_link(ref self.first, child)\n")
assert _helper_link != _direct_source
CASES.append(("refs-counted-helper-managed-link", _helper_link, b"liveNdone"))


# An unrelated counted type is not a structural participant. Assignment through
# its reference parameter must establish an owner that survives the inner scope.
CASES.append(("relationship-helper-unrelated-counted-slot", """interface Link<parent P, child C> {
    class P {
        func put(self, slot: ref Held?, value: Held) { slot = value }
        pub func exercise(self) {
            var slot: Held? = null
            if true {
                let value = Held()
                self.put(ref slot, value)
            }
            slot!.show()
            print("body")
        }
    }
    class C {}
}
value class Held {
    func Held(self) {}
    pub func show(self) { print("live") }
    final func finish(self) { print("drop") }
}
class Graph { func Graph(self) {} }
class Node { func Node(self) {} }
impl Link<Graph:nodes, Node:graph> refs {}
func main() {
    let graph = Graph()
    graph.nodes.exercise()
    print("done")
    graph.destroy()
}
""", b"livebodydropdone"))

# Participant views survive inference, explicit annotations, nested assignment,
# and writable aliases. These locals observe the child; attachment owns it.
_participant_locals = _direct_source.replace(
    "self.first = child\n",
    "let observed = child; var selected: C?; if true { selected = observed } "
    "else { selected = child }; let slot = ref selected; slot = child; "
    "self.first = slot\n")
assert _participant_locals != _direct_source
CASES.append(("refs-counted-participant-locals", _participant_locals, b"liveNdone"))


# A contributed payload is protected relationship storage, but it is not a
# participant link. Its ordinary counted owner survives assignment, is replaced,
# and is released on parent destruction. No potentially stale value is read.
CASES.append(("unrelated-counted-contributed-field", """value class Payload {
    func Payload(self) {}
    final func finish(self) { print("X") }
}
class Graph { func Graph(self) {} }
class Node { func Node(self) {} }
interface Link<parent P, child C> {
    class P {
        payload: Payload?
        pub func save(self, value: Payload) { self.payload = value }
        partial refs destroy(self) {}
    }
    class C { partial refs destroy(self) {} }
}
impl Link<Graph:links, Node:link> refs {}
func main() {
    let graph = Graph()
    if true { let value = Payload(); graph.links.save(value) }
    print("saved")
    if true { let value = Payload(); graph.links.save(value) }
    print("replaced")
    graph.destroy()
    print("done")
}
""", b"savedXreplacedXdone"))

NEGATIVE_CASES = [
    ("persistent-ordinary-nested-container", """class Node { func Node(self) {} }
class Holder { items:([[Node?]], u32) }
func main() {}
""", "LYC304"),
    # Struct-valued class fields are not admitted yet. This checks that existing
    # boundary, not execution of the persistent-reference traversal through structs.
    ("persistent-struct-field-unsupported", """class Node { func Node(self) {} }
struct Payload { item:Node? }
class Holder { items:(Payload, u32) }
func main() {}
""", "LYC306"),
    ("persistent-unrelated-relationship-field", """class Other { func Other(self) {} }
interface Link<parent P,child C> {
    class P { unrelated:Other? }
    class C {}
}
class Holder {}
class Node {}
impl Link<Holder:links,Node:owner> refs {}
func main() {}
""", "LYC304"),
    # Persistent ordinary handles need maintained links even when no function
    # happens to exercise the dangling access. Never execute these negatives.
    ("persistent-ordinary-field", """class Node { func Node(self) {} }
class Holder { item:Node? }
func main() {}
""", "LYC304"),
    ("persistent-ordinary-tuple", """class Node { func Node(self) {} }
value class Holder { items:(Node?, u32) }
func main() {}
""", "LYC304"),
    ("persistent-policy-free-contribution", """interface Link<P,C> {
    class P { item:C? }
    class C {}
}
class Holder {}
class Node { func Node(self) {} }
impl Link<Holder:links,Node:owner> {}
func main() {}
""", "LYC304"),
    ("relationship-fragment-stale-child-storage", """interface Link<parent P, child C> {
        class P {
            first:C?
            partial owns destroy(self) {
                if self.first != null {
                    let saved=ref self.first!.id
                    self.first!.destroy()
                    assert(saved == 7u32)
                }
            }
        }
        class C {}
    }
    class Node {
        id:u32
        func Node(self) { self.id=7u32 }
    }
    impl Link<Node:children, Node:owner> owns {}
    func main() {}
    """, "LYC326"),
    ("relationship-fragment-destroys-self", """interface Link<parent P, child C> {
        class P { partial owns destroy(self) { self.destroy() } }
        class C {}
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:children, Node:owner> owns {}
    func main() {}
    """, "LYC334"),
    ("relationship-fragment-stale-child", """interface Link<parent P, child C> {
        class P {
            first:C?
            partial owns destroy(self) {
                if self.first != null {
                    let saved=self.first!
                    saved.destroy()
                    saved.touch()
                }
            }
        }
        class C {}
    }
    class Node {
        func Node(self) {}
        pub func touch(self) {}
    }
    impl Link<Node:children, Node:owner> owns {}
    func main() {}
    """, "LYC330"),
    ("relationship-signature-missing-child", """interface Link<parent P, child C> {
    class P { pub add func attach(self) { prepare {} } }
    class C {}
}
class Node { func Node(self) {} }
impl Link<Node:parentRole, Node:childRole> owns {}
func main() {}
""", "LYC304"),
    ("relationship-signature-extra-argument", """interface Link<parent P, child C> {
    class P { pub add func attach(self, child:C, extra:u32) { prepare {} } }
    class C {}
}
class Node { func Node(self) {} }
impl Link<Node:parentRole, Node:childRole> owns {}
func main() {}
""", "LYC304"),
    ("relationship-signature-nullable-child", """interface Link<parent P, child C> {
    class P { pub add func attach(self, child:C?) { prepare {} } }
    class C {}
}
class Node { func Node(self) {} }
impl Link<Node:parentRole, Node:childRole> owns {}
func main() {}
""", "LYC304"),
    ("relationship-signature-parent-view", """interface Link<parent P, child C> {
    class P { pub add func attach(self, child:P) { prepare {} } }
    class C {}
}
class Node { func Node(self) {} }
impl Link<Node:parentRole, Node:childRole> owns {}
func main() {}
""", "LYC304"),
    ("relationship-signature-nonunit-result", """interface Link<parent P, child C> {
    class P { pub add func attach(self, child:C) -> u32 { prepare {} } }
    class C {}
}
class Node { func Node(self) {} }
impl Link<Node:parentRole, Node:childRole> owns {}
func main() {}
""", "LYC304"),
    ("relationship-prepare-nested-cleanup", """interface Link<parent P, child C> {
        class P {
            pub add func attach(self, child:C) {
                prepare { if true { let held=Held() } }
            }
        }
        class C {}
    }
    interface Cleanup<parent P, child C> {
        class P {
            first:C?
            partial refs destroy(self) { self.first=null }
        }
        class C {}
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    value class Held { func Held(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    impl Cleanup<Held:links, Node:held> refs {}
    func main() {}
    """, "LYC308"),
    ("relationship-prepare-direct-write", """interface Link<parent P, child C> {
        class P {
            first:C?
            pub add func attach(self, child:C) {
                prepare { self.first=null }
                self.first=child
            }
        }
        class C {}
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {}
    """, "LYC308"),
    ("relationship-prepare-helper-write", """interface Link<parent P, child C> {
        class P {
            first:C?
            pub add func attach(self, child:C) {
                prepare { self.clear() }
                self.first=child
            }
            func clear(self) { self.clearAgain() }
            func clearAgain(self) { self.first=null }
        }
        class C {}
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {}
    """, "LYC308"),
    ("relationship-nonnullable-not-initialized", """interface Link<parent P, child C> {
        class P { first: C }
        class C {}
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {}
    """, "LYC328"),
    ("relationship-managed-external-write", """interface Link<parent P, child C> {
        class P { first: C? }
        class C { parent: P? }
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {
        let graph=Graph()
        graph.nodes.first=null
    }
    """, "LYC308"),
    ("relationship-managed-external-reference", """interface Link<parent P, child C> {
        class P { first: C? }
        class C { parent: P? }
    }
    class Graph { func Graph(self) {} }
    class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {
        let graph=Graph()
        let alias=ref graph.nodes.first
    }
    """, "LYC308"),
    ("relationship-operation-without-roles", """interface Link<P, C> {
        class P { pub add func append(self, child: C) { prepare {} } }
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-operation-missing-prepare", """interface Link<parent P, child C> {
        class P { pub add func append(self, child: C) {} }
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-operation-late-prepare", """interface Link<parent P, child C> {
        class P { pub add func append(self, child: C) { print("late"); prepare {} } }
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-operation-second-prepare", """interface Link<parent P, child C> {
        class P { pub remove func remove(self, child: C) { prepare {}; prepare {} } }
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-operation-structural-return", """interface Link<parent P, child C> {
        class P {
            pub remove func remove(self, child: C) {
                prepare {}
                if true { return }
            }
        }
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-fragment-not-callable", """interface Link<parent P, child C> {
        class P { partial owns destroy(self) {} }
        class C { partial owns destroy(self) {} }
    }
    class Hub { func Hub(self) {} }
    class Other { func Other(self) {} }
    impl Link<Other:up, Hub:down> owns {}
    func main() {
        let hub=Hub()
        hub.down.destroy()
    }
    """, "LYC329"),
    ("relationship-fragment-effects-reach-caller", """interface Link<parent P, child C> {
        class P {
            partial owns destroy(self) {
                let temporary=Other()
                temporary.destroy()
            }
        }
        class C { partial owns destroy(self) {} }
    }
    class Hub { func Hub(self) {} }
    class Other { func Other(self) {} }
    impl Link<Hub, Other> owns {}
    func main() {
        let saved=Other()
        let hub=Hub()
        hub.destroy()
        saved.destroy()
    }
    """, "LYC330"),
    ("relationship-duplicate-fragment", """interface Link<parent P, child C> {
        class P {
            partial owns destroy(self) {}
            partial owns destroy(self) {}
        }
        class C {}
    }
    func main() {}
    """, "LYC302"),
    ("relationship-fragment-without-roles", """interface Link<P, C> {
        class P { partial owns destroy(self) {} }
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-needs-parent-and-child", """interface Link<parent P, C> {
        class P {}
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-two-parents", """interface Link<parent P, parent C> {
        class P {}
        class C {}
    }
    func main() {}
    """, "LYC303"),
    ("relationship-owns-counted-child", """interface Link<parent P, child C> {
        class P {}
        class C {}
    }
    class Graph { func Graph(self) {} }
    value class Node { func Node(self) {} }
    impl Link<Graph:nodes, Node:graph> owns {}
    func main() {}
    """, "LYC304"),
]

if __name__ == "__main__":
    # This option belongs to the grader; the independent compiler command follows.
    runtime_debug = len(sys.argv) > 1 and sys.argv[1] == "--runtime-debug"
    if runtime_debug:
        del sys.argv[1]
    sys.exit(main(CASES, NEGATIVE_CASES, "relationships",
                  emission_options=("--debug",) if runtime_debug else ()))
