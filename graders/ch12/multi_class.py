#!/usr/bin/env python3
"""Retained Chapter 12 executable multi-class binding contracts.

Run from the repository root:
  python3 graders/ch12/multi_class.py COMPILER [ARG ...]

Uses the public Chapter 10 harness: emit C successfully with empty stderr,
compile/run at O0 and O2, and compare exact stdout bytes. Negative cases require
exit 1, empty stdout and the specified located diagnostic. No normalization.
Method and scalar/array storage cases exercise role mapping and initialization;
list transitions and refusal checks remain distinct from automatic teardown safety.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch10'))
from tuples import main

PAIR = '''interface Pair<P,C> {
    class P {
        pub func value(self)->i32 { return 3 }
        pub func read(self, other:C)->i32 { return other.value() }
    }
    class C {
        pub func value(self)->i32 { return 7 }
        pub func read(self, other:P)->i32 { return other.value() }
    }
}
class Graph { func Graph(self) {} }
class Node { func Node(self) {} }
'''

# Labels affect external lookup placement, not the participant's runtime type.
# Each named fixture checks both roles, including calls across the role boundary.
CASES = [('multi-class-label-receivers', '''interface Values<P,C> {
    class P { pub func value(self)->i32 { return 3 } }
    class C { pub func value(self)->i32 { return 7 } }
}
class Graph { func Graph(self) {} }
class Node { func Node(self) {} }
impl Values<Graph:nodes,Node:graph> {}
func main() {
    let owner=Graph()
    let child=Node()
    assert(owner.nodes.value()==3)
    assert(child.graph.value()==7)
    assert(Graph().nodes.value()==3)
    print("ok")
}
''', b'ok')]
for name, parent_label, child_label in (
    ('both-labels', 'nodes', 'graph'),
    ('parent-label', 'nodes', ''),
    ('child-label', '', 'graph'),
    ('flat', '', ''),
):
    parent_binding = 'Graph' + (':' + parent_label if parent_label else '')
    child_binding = 'Node' + (':' + child_label if child_label else '')
    parent_path = 'owner.' + (parent_label + '.' if parent_label else '')
    child_path = 'item.' + (child_label + '.' if child_label else '')
    source = PAIR + f'''impl Pair<{parent_binding}, {child_binding}> {{}}
func main() {{
    let owner=Graph()
    let item=Node()
    assert({parent_path}read(item)==7)
    assert({child_path}read(owner)==3)
    print("ok")
}}
'''
    CASES.append(('multi-class-' + name, source, b'ok'))

# Namespace selection must not acquire a second runtime holder. Named owners
# remain alive across calls; temporary owners finalize once after their call.
CASES.append(('multi-class-counted-label-receivers', '''interface Values<P,C> {
    class P { pub func value(self)->i32 { return 3 } }
    class C { pub func value(self)->i32 { return 7 } }
}
value class Graph {
    func Graph(self) {}
    final func finish(self) { print("G") }
}
value class Node {
    func Node(self) {}
    final func finish(self) { print("N") }
}
impl Values<Graph:nodes,Node:graph> {}
func use() {
    let owner=Graph()
    assert(owner.nodes.value()==3)
    assert(owner.nodes.value()==3)
    assert(Node().graph.value()==7)
    print("live")
}
func main() { use(); print("done") }
''', b'NliveGdone'))

# Equal runtime types must not collapse the two participant roles. Each method
# still resolves the opposite role's value declaration through this binding.
CASES.append(('multi-class-same-class-roles', PAIR + '''impl Pair<Graph:left, Graph:right> {}
func main() {
    let first=Graph()
    let second=Graph()
    assert(first.left.read(second)==7)
    assert(second.right.read(first)==3)
    print("ok")
}
''', b'ok'))

# Separate complete bindings contribute independent namespaces on the same pair.
CASES.append(('multi-class-independent-bindings', PAIR + '''impl Pair<Graph:nodes, Node:graph> {}
impl Pair<Graph:selected, Node:selection> {}
func main() {
    let owner=Graph()
    let item=Node()
    assert(owner.nodes.read(item)==7)
    assert(owner.selected.read(item)==7)
    assert(item.graph.read(owner)==3)
    assert(item.selection.read(owner)==3)
    print("ok")
}
''', b'ok'))

# Local annotations choose the static participant view without changing handles.
VIEW_PAIR = '''interface Pair<P,C> {
    class P {
        pub func value(self)->i32 { return 3 }
        pub func inferred(self, other:C)->i32 {
            let copy=other
            return copy.value()
        }
        pub func explicit(self, other:C)->i32 {
            let copy:P=other
            return copy.value()
        }
    }
    class C { pub func value(self)->i32 { return 7 } }
}
class Graph { func Graph(self) {} }
class Node { func Node(self) {} }
'''
CASES.append(('multi-class-local-view-precedence', VIEW_PAIR + '''impl Pair<Graph:left, Graph:right> {}
func main() {
    let first=Graph()
    let second=Graph()
    assert(first.left.inferred(second)==7)
    assert(first.left.explicit(second)==3)
    print("ok")
}
''', b'ok'))

# Declared result views survive calls/grouping; an explicit concrete annotation
# deliberately exposes ordinary members instead of retaining the participant view.
CASES.append(('multi-class-result-and-concrete-views', """interface Views<P,C> {
    class P {
        pub func value(self)->i32 { return 3 }
        pub func echo(self, other:C)->C { return other }
        pub func concrete(self, other:C)->i32 {
            let plain:Graph=other
            return plain.value()
        }
        pub func grouped(self, other:C)->i32 { return (other).value() }
    }
    class C { pub func value(self)->i32 { return 7 } }
}
class Graph {
    func Graph(self) {}
    pub func value(self)->i32 { return 11 }
}
impl Views<Graph:left,Graph:right> {}
func main() {
    let first=Graph()
    let second=Graph()
    let returned=first.left.echo(second)
    assert(returned.value()==7)
    assert(first.left.echo(second).value()==7)
    assert(first.left.concrete(second)==11)
    assert(first.left.grouped(second)==7)
    assert((first.left).value()==3)
    print("ok")
}
""", b'ok'))

NEGATIVE_CASES = [
    # Participant bindings require a nominal class, not its nullable wrapper.
    ('multi-class-nullable-participant-labeled', PAIR +
        'impl Pair<Graph?:nodes,Node:graph> {}\nfunc main() {}\n', 'LYC304'),
    ('multi-class-nullable-participant-flat', PAIR +
        'impl Pair<Graph?,Node> {}\nfunc main() {}\n', 'LYC304'),
    # An explicit view cannot make incompatible concrete handles assignable.
    ('multi-class-local-view-conflict', VIEW_PAIR + '''impl Pair<Graph:left, Node:right> {}
func main() {
    let owner=Graph()
    let item=Node()
    print(owner.left.explicit(item))
}
''', 'LYC304'),
    # A scope label is itself a member name; its contents are not merged.
    ('multi-class-label-collision', PAIR + '''impl Pair<Graph:nodes, Node:graph> {}
impl Pair<Graph:nodes, Node:selection> {}
func main() {}
''', 'LYC302'),
    ('multi-class-label-member-collision', PAIR.replace(
        'class Graph { func Graph(self) {} }',
        'class Graph { func Graph(self) {}; pub func nodes(self)->i32 { return 9 } }')
        + 'impl Pair<Graph:nodes,Node:graph> {}\nfunc main() {}\n', 'LYC302'),
    # A participant block must name an actual parameter, not introduce a class.
    ('multi-class-unknown-participant', '''interface Pair<P,C> {
    class Missing { pub func value(self)->i32 { return 1 } }
}
class Graph {}
class Node {}
impl Pair<Graph,Node> {}
func main() {}
''', 'LYC301'),
    # Flat contributions must not replace an ordinary existing member.
    ('multi-class-member-collision', PAIR.replace(
        'class Graph { func Graph(self) {} }',
        'class Graph { func Graph(self) {}; pub func value(self)->i32 { return 9 } }')
        + 'impl Pair<Graph,Node> {}\nfunc main() {}\n', 'LYC302'),
]

# Contributed storage uses ordinary constructor obligations and physical layout.
for label in ('', 'data'):
    binding = ':' + label if label else ''
    access = label + '.' if label else ''
    source = f"""interface Storage<P,C> {{
    class P {{
        number:i32
        values:[i32]
        pub func read(self)->i32 {{ return self.number + self.values[0] }}
    }}
    class C {{
        number:i32
        pub func read(self)->i32 {{ return self.number }}
    }}
}}
value class Graph {{
    func Graph(self) {{
        self.{access}number=3
        self.{access}values=[4]
    }}
    final func finish(self) {{ assert(self.{access}number==3); print("G") }}
}}
class Node {{ func Node(self) {{ self.{access}number=8 }} }}
impl Storage<Graph{binding},Node{binding}> {{}}
func main() {{
    let owner=Graph()
    let other=Graph()
    let child=Node()
    assert(owner.{access}read()==7)
    owner.{access}values[0]=6
    assert(owner.{access}read()==9)
    assert(other.{access}read()==7)
    assert(child.{access}read()==8)
    print("ok")
}}
"""
    CASES.append(('multi-class-storage-' + (label or 'flat'), source, b'okGG'))

FIELD_TEMPLATE = """interface Storage<P,C> {
    class P { number:i32 }
    class C {}
}
class Node {}
impl Storage<Graph:data,Node> {}
"""
NEGATIVE_CASES.extend([
    ('multi-class-storage-missing', FIELD_TEMPLATE + """class Graph {
    func Graph(self) {}
}
func main() { let owner=Graph() }
""", 'LYC328'),
    ('multi-class-storage-read-before-write', FIELD_TEMPLATE + """class Graph {
    func Graph(self) { self.data.number=self.data.number }
}
func main() { let owner=Graph() }
""", 'LYC328'),
])

CASES.append(('multi-class-same-class-storage', """interface Storage<P,C> {
    class P {
        number:i32
        pub func read(self)->i32 { return self.number }
    }
    class C {
        number:i32
        pub func read(self)->i32 { return self.number }
    }
}
class Graph {
    func Graph(self) { self.left.number=3; (self.right).number=7 }
}
impl Storage<Graph:left,Graph:right> {}
func main() {
    let owner=Graph()
    assert(owner.left.read()==3)
    assert(owner.right.read()==7)
    owner.left.number=9
    assert(owner.left.read()==9)
    assert(owner.right.read()==7)
    print("ok")
}
""", b'ok'))
NEGATIVE_CASES.append(('multi-class-partial-namespace-escape', FIELD_TEMPLATE + """class Graph {
    func Graph(self) { inspect(self.data); self.data.number=3 }
}
func inspect(owner:Graph) {}
func main() { let owner=Graph() }
""", 'LYC328'))

# Explicit removal is required in this intermediate chapter. These links are
# not yet relationship-managed and endpoint destruction does not auto-detach.
CASES.append(('multi-class-participant-link-fields', '''interface Link<P,C> {
    class P {
        peer:C?
        pub func attach(self, child:C) { self.peer=child; child.parent=self }
        pub func detach(self, child:C) { self.peer=null; child.parent=null }
    }
    class C { parent:P? }
}
class Graph {
    n:i32
    func Graph(self) { self.n=3; self.links.peer=null }
}
class Node {
    n:i32
    func Node(self) { self.n=7; self.back.parent=null }
}
impl Link<Graph:links,Node:back> {}
func main() {
    let owner=Graph(); let child=Node()
    owner.links.attach(child)
    assert(owner.links.peer!.n==7)
    assert(child.back.parent!.n==3)
    owner.links.detach(child)
    child.destroy(); owner.destroy(); print("ok")
}
''', b'ok'))

# Nullable stored links preserve endpoint lookup identity, even for equal classes.
CASES.append(('multi-class-nullable-role-chain', '''interface Link<P,C> {
    class P {
        peer:C?
        pub func value(self)->i32 { return 3 }
        pub func attach(self, child:C) { self.peer=child; child.parent=self }
        pub func detach(self, child:C) { self.peer=null; child.parent=null }
        pub func read(self)->i32 {
            let inferred=self.peer
            let explicit:C?=inferred
            return explicit!.value()
        }
        pub func roundtrip(self)->i32 { return self.peer!.parent!.value() }
    }
    class C {
        parent:P?
        pub func value(self)->i32 { return 7 }
    }
}
class Graph {
    func Graph(self) { self.left.peer=null; self.right.parent=null }
}
impl Link<Graph:left,Graph:right> {}
func main() {
    let owner=Graph(); let child=Graph()
    owner.left.attach(child)
    assert(owner.left.read()==7)
    assert(owner.left.roundtrip()==3)
    owner.left.detach(child)
    // Same-class destruction precision is outside this lookup fixture.
    print("ok")
}
''', b'ok'))

# An explicit concrete annotation removes the role; it cannot discover a label
# implicitly just because the stored runtime handle came from that role.
NEGATIVE_CASES.append(('multi-class-nullable-concrete-view', CASES[-1][1].replace(
    'let explicit:C?=inferred', 'let explicit:Graph?=inferred'), 'LYC329'))

# Ordinary links are manually maintained here, not relationship-managed.
# Every refusal guard precedes mutation; remove clears both membership directions.
LIST = '''interface DoublyLinkedList<P,C> {
    class P {
        first:C?
        last:C?
        pub func append(self, child:C) {
            assert(child.parent==null)
            child.previous=self.last
            child.next=null
            child.parent=self
            if self.last!=null { self.last!.next=child }
            else { self.first=child }
            self.last=child
        }
        pub func remove(self, child:C) {
            assert(child.parent==self)
            if child.previous!=null { child.previous!.next=child.next }
            else { self.first=child.next }
            if child.next!=null { child.next!.previous=child.previous }
            else { self.last=child.previous }
            child.parent=null
            child.previous=null
            child.next=null
        }
    }
    class C { parent:P?; previous:C?; next:C? }
}
class Graph {
    func Graph(self) { self.nodes.first=null; self.nodes.last=null }
}
class Node {
    func Node(self) {
        self.graph.parent=null
        self.graph.previous=null
        self.graph.next=null
    }
}
impl DoublyLinkedList<Graph:nodes,Node:graph> {}
'''

CASES.append(('multi-class-list-transitions', LIST + '''func main() {
    let owner=Graph()
    let a=Node(); let b=Node(); let c=Node()
    assert(owner.nodes.first==null); assert(owner.nodes.last==null)
    owner.nodes.append(a)
    assert(owner.nodes.first==a); assert(owner.nodes.last==a)
    assert(a.graph.parent==owner)
    assert(a.graph.previous==null); assert(a.graph.next==null)
    owner.nodes.remove(a)
    assert(owner.nodes.first==null); assert(owner.nodes.last==null)
    assert(a.graph.parent==null)
    owner.nodes.append(a); owner.nodes.append(b); owner.nodes.append(c)
    assert(a.graph.next==b); assert(b.graph.previous==a)
    assert(b.graph.next==c); assert(c.graph.previous==b)
    owner.nodes.remove(b)
    assert(a.graph.next==c); assert(c.graph.previous==a)
    assert(b.graph.parent==null)
    assert(b.graph.previous==null); assert(b.graph.next==null)
    owner.nodes.remove(a)
    assert(owner.nodes.first==c); assert(c.graph.previous==null)
    assert(a.graph.parent==null); assert(a.graph.next==null)
    owner.nodes.append(b)
    owner.nodes.remove(b)
    assert(owner.nodes.last==c); assert(c.graph.next==null)
    owner.nodes.remove(c)
    assert(owner.nodes.first==null); assert(owner.nodes.last==null)
    assert(c.graph.parent==null)
    // All links are detached. Multiple same-class destruction precision is
    // outside this fixture; ordinary nodes remain allocated until process exit.
    owner.destroy()
    print("ok")
}
''', b'ok'))

# The same algorithm supports counted participants, but ordinary counted fields
# retain both directions. Explicit removal must break that cycle before scope exit.
COUNTED_LIST = LIST.replace('class Graph {', 'value class Graph {').replace(
    'class Node {', 'value class Node {').replace(
    'func Graph(self)', 'final func finish(self) { print("G") }\n    func Graph(self)').replace(
    'func Node(self)', 'final func finish(self) { print("N") }\n    func Node(self)')
CASES.append(('multi-class-counted-list-detach', COUNTED_LIST + '''func use() {
    let owner=Graph(); let child=Node()
    owner.nodes.append(child)
    assert(child.graph.parent==owner)
    owner.nodes.remove(child)
    assert(owner.nodes.first==null); assert(owner.nodes.last==null)
    assert(child.graph.parent==null)
    assert(child.graph.previous==null); assert(child.graph.next==null)
    print("live")
}
func main() { use(); print("done") }
''', b'liveNGdone'))

# One concrete object can carry two independent list memberships. Neither the
# shared template nor the same concrete classes may collapse their role identities.
TWO_LISTS = LIST.replace(
    'self.nodes.first=null; self.nodes.last=null',
    'self.nodes.first=null; self.nodes.last=null; '
    'self.selected.first=null; self.selected.last=null').replace(
    'self.graph.next=null',
    'self.graph.next=null; self.selection.parent=null; '
    'self.selection.previous=null; self.selection.next=null')
TWO_LISTS += 'impl DoublyLinkedList<Graph:selected,Node:selection> {}\n'
CASES.append(('multi-class-independent-list-memberships', TWO_LISTS + '''func main() {
    let owner=Graph(); let child=Node()
    owner.nodes.append(child); owner.selected.append(child)
    owner.nodes.remove(child)
    assert(owner.nodes.first==null); assert(owner.nodes.last==null)
    assert(child.graph.parent==null)
    assert(owner.selected.first==child); assert(owner.selected.last==child)
    assert(child.selection.parent==owner)
    owner.selected.remove(child)
    assert(owner.selected.first==null); assert(owner.selected.last==null)
    assert(child.selection.parent==null)
    child.destroy(); owner.destroy(); print("ok")
}
''', b'ok'))

if __name__ == '__main__':
    sys.exit(main(CASES, NEGATIVE_CASES, 'multi-class'))
