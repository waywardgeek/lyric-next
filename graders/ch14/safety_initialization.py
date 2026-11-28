"""Retained compile-only contributed-field initialization contracts.

Use the same exact exit/stream/diagnostic contract as safety_calls.py.
Neither positive nor negative programs are executed.
"""
from safety_calls import main

PREFIX = """interface Data<P,C> {
    class P { x:i32 }
    class C {}
}
class Child { func Child(self) {} }
"""
SUFFIX = "impl Data<Parent:data,Child:owner> {}"
ERROR = "LYC328: Field 'x' is not definitely initialized."

# Placement is a namespace, not a separate object with independent readiness.
# All paths leaving the actual constructor must initialize its physical fields.
CASES = []
for name, body, expected in [
    ("contributed-both-branches", "if b { self.data.x=1 } else { self.data.x=2 }", None),
    ("contributed-missing-branch", "if b { self.data.x=1 }", ERROR),
    ("contributed-early-return", "if b { return }; self.data.x=1", ERROR),
    ("contributed-initialized-return", "if b { self.data.x=1; return }; self.data.x=2", None),
    ("contributed-read-before-write", "self.data.x=self.data.x+1", ERROR),
    ("contributed-partial-self-escape", "take(self); self.data.x=1", ERROR),
]:
    source = (PREFIX + "class Parent { func Parent(self,b:bool) { " + body
              + " } }; func take(parent:Parent) {}; " + SUFFIX)
    CASES.append((name, source, expected))

# A finalizer that allocates a container of its own class participates in that
# container's cleanup dependency. Reject before a callback could invalidate a
# cached descriptor during shrinking. This candidate is never executed.
CASES.append((
    "nested-container-finalizer-cycle",
    """class Container {
        payload:([Item],)
        func Container(self) { self.payload=([],) }
    }
    value class Item {
        func Item(self) {}
        final func finish(self) { let other=Container() }
    }
    func use() {
        let container=Container()
        container.payload.0.push(Item())
        container.payload.0.truncate(0u64)
        container.destroy()
    }""",
    "LYC334: A final hook may not transitively destroy its own class.",
))

# Mandatory relationships are future work, not a partial-self exemption.
# The current compiler must refuse both bypassing protected writes and leaving
# a required physical field uninitialized. Neither program is executed.
for name, body, expected in [
    ("mandatory-parent-direct-write", "self.owner.parent=parent",
     "LYC308: Managed links are writable only inside their relationship implementation."),
    ("mandatory-parent-omitted", "",
     "LYC328: Field 'parent' is not definitely initialized."),
]:
    source = """interface RequiredParent<parent P, child C> {
        class P {}
        class C { parent:P }
    }
    class Parent { func Parent(self) {} }
    class Child { func Child(self,parent:Parent) { BODY } }
    impl RequiredParent<Parent:children,Child:owner> refs {}
    """.replace("BODY", body)
    CASES.append((name, source, expected))

if __name__ == "__main__":
    raise SystemExit(main(CASES, "initialization safety"))
