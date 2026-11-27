interface DoublyLinkedList<parent P, child C> {
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

// One list implementation, three independent role bindings. No custom destructor.
class Graph {
    func Graph(self) {}
    pub func add_node(self, id:u32)->Node {
        let node=Node(id)
        self.nodes.append(node)
        return node
    }
    pub func check(self, expected_nodes:u32, expected_edges:u32) {
        var nodes:u32=0u32
        var outgoing:u32=0u32
        var incoming:u32=0u32
        var previous:Node?=null
        var node=self.nodes.first
        while node != null {
            assert(node!.graph.parent == self)
            assert(node!.graph.previous == previous)
            var previous_edge:Edge?=null
            var edge=node!.outgoing.first
            while edge != null {
                assert(edge!.source.parent == node)
                assert(edge!.source.previous == previous_edge)
                assert(edge!.target.parent != null)
                assert(edge!.target.parent!.graph.parent == self)
                previous_edge=edge
                edge=edge!.source.next
                outgoing += 1u32
            }
            assert(node!.outgoing.last == previous_edge)
            previous_edge=null
            edge=node!.incoming.first
            while edge != null {
                assert(edge!.target.parent == node)
                assert(edge!.target.previous == previous_edge)
                assert(edge!.source.parent != null)
                assert(edge!.source.parent!.graph.parent == self)
                previous_edge=edge
                edge=edge!.target.next
                incoming += 1u32
            }
            assert(node!.incoming.last == previous_edge)
            previous=node
            node=node!.graph.next
            nodes += 1u32
        }
        assert(self.nodes.last == previous)
        assert(nodes == expected_nodes)
        assert(outgoing == expected_edges)
        assert(incoming == expected_edges)
    }
    final func finish(self) {
        println("final graph")
    }
}
class Node {
    id:u32
    func Node(self, id:u32) { self.id=id }
    pub func connect(self, target:Node, id:u32)->Edge {
        let edge=Edge(id)
        self.outgoing.append(edge)
        target.incoming.append(edge)
        return edge
    }
    final func finish(self) {
        assert(self.graph.parent != null)
        if self.id == 1u32 { println("final node 1") }
        if self.id == 2u32 { println("final node 2") }
        if self.id == 3u32 { println("final node 3") }
        if self.id == 4u32 { println("final node 4") }
    }
}
class Edge {
    id:u32
    func Edge(self, id:u32) { self.id=id }
    final func finish(self) {
        // Both memberships must still exist when final runs.
        assert(self.source.parent != null)
        assert(self.target.parent != null)
        if self.id == 1u32 { println("final edge 1") }
        if self.id == 2u32 { println("final edge 2") }
        if self.id == 3u32 { println("final edge 3") }
        if self.id == 4u32 { println("final edge 4") }
        if self.id == 5u32 { println("final edge 5") }
        if self.id == 6u32 { println("final edge 6") }
    }
}
impl DoublyLinkedList<Graph:nodes, Node:graph> owns {}
impl DoublyLinkedList<Node:outgoing, Edge:source> owns {}
impl DoublyLinkedList<Node:incoming, Edge:target> owns {}

func main() {
    let graph=Graph()
    graph.check(0u32,0u32)
    let one=graph.add_node(1u32)
    let two=graph.add_node(2u32)
    let three=graph.add_node(3u32)
    let four=graph.add_node(4u32)
    let first=one.connect(two,1u32)
    one.connect(two,2u32)
    two.connect(one,3u32)
    two.connect(two,4u32)
    two.connect(three,5u32)
    four.connect(three,6u32)
    graph.check(4u32,6u32)
    println("graph built")
    first.destroy()
    graph.check(4u32,5u32)
    println("edge removed")
    two.destroy()
    graph.check(3u32,1u32)
    println("node removed")
    graph.destroy()
    println("graph destroyed")
}
