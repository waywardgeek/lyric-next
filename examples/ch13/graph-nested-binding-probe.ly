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

interface DirectedGraph<G,N,E> {
    class G {}
    class N {}
    class E {}
    impl DoublyLinkedList<G:nodes,N:graph> owns {}
    impl DoublyLinkedList<N:outgoing,E:source> owns {}
    impl DoublyLinkedList<N:incoming,E:target> owns {}
}
class Network {}
class Vertex {}
class Arc {}
impl DirectedGraph<Network,Vertex,Arc> {}
func main() {}
