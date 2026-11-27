#!/usr/bin/env python3
"""Chapter 13 successors to Chapter 12's temporary ordinary-link allowance.

Run from repository root: python3 graders/ch13/multi_class_migration.py COMPILER [ARG ...]
The unchanged Chapter 10 harness checks exact output at O0/O2 and located compiler
rejections. Original Chapter 12 fixtures remain runnable at their chapter endpoint.
"""
import importlib.util
from pathlib import Path

from relationships import LIST, main

spec = importlib.util.spec_from_file_location(
    "chapter12_multi_class", Path(__file__).resolve().parents[1] / "ch12" / "multi_class.py")
previous = importlib.util.module_from_spec(spec)
spec.loader.exec_module(previous)

# This is a one-child relationship, not an alias for the doubly linked list.
# Refusal precedes both writes; both endpoints can detach during destruction.
LINK = """interface Link<parent P, child C> {
    class P {
        peer:C?
        pub func value(self)->i32 { return 3 }
        pub add func attach(self, child:C) {
            prepare { assert(self.peer==null); assert(child.parent==null) }
            self.peer=child
            child.parent=self
        }
        pub remove func detach(self, child:C) {
            prepare { assert(self.peer==child); assert(child.parent==self) }
            self.peer=null
            child.parent=null
        }
        pub func read(self)->i32 {
            let inferred=self.peer
            let explicit:C?=inferred
            return explicit!.value()
        }
        pub func roundtrip(self)->i32 { return self.peer!.parent!.value() }
        partial refs destroy(self) {
            if self.peer!=null { self.detach(self.peer!) }
        }
    }
    class C {
        parent:P?
        pub func value(self)->i32 { return 7 }
        partial refs destroy(self) {
            if self.parent!=null { self.parent!.detach(self) }
        }
    }
}
"""

DISTINCT_LINK = LINK + """class Graph {
    n:i32
    func Graph(self) { self.n=3 }
}
class Node {
    n:i32
    func Node(self) { self.n=7 }
}
impl Link<Graph:links,Node:back> refs {}
"""
SAME_CLASS_LINK = LINK + """class Graph { func Graph(self) {} }
impl Link<Graph:left,Graph:right> refs {}
"""
MAINTAINED_LIST = LIST + """class Graph { func Graph(self) {} }
class Node { func Node(self) {} }
impl DoublyLinkedList<Graph:nodes,Node:graph> refs {}
"""
TWO_LISTS = MAINTAINED_LIST + "impl DoublyLinkedList<Graph:selected,Node:selection> refs {}\n"

REPLACEMENTS = {
    "multi-class-participant-link-fields": DISTINCT_LINK,
    "multi-class-nullable-role-chain": SAME_CLASS_LINK,
    "multi-class-list-transitions": MAINTAINED_LIST,
    "multi-class-independent-list-memberships": TWO_LISTS,
}

CASES = []
NEGATIVE_CASES = []
seen = set()
for name, source, expected in previous.CASES:
    if name not in REPLACEMENTS:
        CASES.append((name, source, expected))
        continue
    # Preserve every original application assertion; only the contributing
    # implementation and initialization boundary change at this chapter.
    marker = "func main()"
    assert source.count(marker) == 1, name
    application = source[source.index(marker):]
    migrated = REPLACEMENTS[name] + application
    CASES.append((name + "-maintained", migrated, expected))
    NEGATIVE_CASES.append((name + "-unmanaged-rejected", source, "LYC304"))
    seen.add(name)
    if name == "multi-class-nullable-role-chain":
        assert migrated.count("let explicit:C?=inferred") == 1
        NEGATIVE_CASES.append((
            "multi-class-nullable-concrete-view-maintained",
            migrated.replace("let explicit:C?=inferred", "let explicit:Graph?=inferred"),
            "LYC329"))
assert seen == set(REPLACEMENTS), "Every migration must match an existing fixture"

for case in previous.NEGATIVE_CASES:
    if case[0] != "multi-class-nullable-concrete-view":
        NEGATIVE_CASES.append(case)

if __name__ == "__main__":
    raise SystemExit(main(CASES, NEGATIVE_CASES, "multi-class-migration"))
