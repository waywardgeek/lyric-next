#!/usr/bin/env python3
"""Explicit Chapter 13 migration of Chapter 12 object-field contracts.

Run from repository root: python3 graders/ch13/object_fields_migration.py COMPILER [ARG ...]
Unmanaged ordinary fields are rejected. Counted fields retain constructor and
ownership contracts; ordinary alias/helper cases now use maintained links.
"""
import importlib.util
from pathlib import Path

from multi_class_migration import LINK, main

spec = importlib.util.spec_from_file_location(
    "chapter12_object_fields", Path(__file__).resolve().parents[1] / "ch12" / "object_fields.py")
previous = importlib.util.module_from_spec(spec)
spec.loader.exec_module(previous)

# Ordinary endpoints, actual reciprocal links, and no application link writes.
# Keep Box.n so aliases still observe the same object and payload.
LINKED = LINK + previous.BOX + """class Holder { func Holder(self) {} }
impl Link<Holder:link,Box:owner> refs {}
"""
BODIES = {
    "object-field-nullable": """func main() {
        let h=Holder(); let box=Box(9)
        assert(h.link.peer==null)
        h.link.attach(box)
        assert(h.link.peer!.n==9)
        h.link.detach(box)
        assert(h.link.peer==null)
        h.destroy(); print("ok")
    }""",
    "object-field-detach-through-alias": """func main() {
        let box=Box(7); let h=Holder(); h.link.attach(box); let alias=h
        assert(alias.link.peer!.n==7)
        alias.link.detach(box)
        box.destroy(); h.destroy(); print("ok")
    }""",
    "object-field-detach-through-call": """func detach(h:Holder, box:Box) {
        h.link.detach(box)
    }
    func main() {
        let box=Box(7); let h=Holder(); h.link.attach(box)
        detach(h,box); box.destroy(); h.destroy(); print("ok")
    }""",
    "object-field-detach-and-destroy-in-call": """func dispose(h:Holder, box:Box) {
        h.link.detach(box); box.destroy()
    }
    func main() {
        let box=Box(7); let h=Holder(); h.link.attach(box)
        dispose(h,box); h.destroy(); print("ok")
    }""",
    "object-field-detach-through-returned-alias": """func identity(h:Holder)->Holder { return h }
    func main() {
        let box=Box(7); let h=Holder(); h.link.attach(box); let alias=identity(h)
        alias.link.detach(box)
        box.destroy(); h.destroy(); print("ok")
    }""",
}

CASES = []
NEGATIVE_CASES = list(previous.COUNTED_NEGATIVE_CASES)
seen = set()
for name, source, expected in previous.CASES:
    if name in BODIES:
        CASES.append((name + "-maintained", LINKED + BODIES[name], expected))
        NEGATIVE_CASES.append((name + "-unmanaged-rejected", source, "LYC304"))
        seen.add(name)
    elif name == "object-field-nonnull":
        # Preserve nonnullable constructor assignment, field observation, and a
        # target outliving its holder using a now-legal owning counted field.
        # This does NOT claim nonnullable managed construction is implemented.
        assert source.count(previous.BOX) == 1
        counted_box = previous.BOX.replace("class Box", "value class Box", 1)
        CASES.append((name + "-counted-target", source.replace(previous.BOX, counted_box, 1), expected))
        NEGATIVE_CASES.append((name + "-unmanaged-rejected", source, "LYC304"))
    else:
        CASES.append((name, source, expected))
assert seen == set(BODIES)

for name, source, diagnostic in previous.NEGATIVE_CASES:
    # The old unmanaged declaration is now illegal before body checking. Use a
    # legal counted field to continue checking initialization/type diagnostics.
    assert source.count(previous.BOX) == 1
    counted_box = previous.BOX.replace("class Box", "value class Box", 1)
    migrated = source.replace(previous.BOX, counted_box, 1)
    NEGATIVE_CASES.append((name + "-counted-target", migrated, diagnostic))
    NEGATIVE_CASES.append((name + "-unmanaged-rejected", source, "LYC304"))

if __name__ == "__main__":
    raise SystemExit(main(CASES, NEGATIVE_CASES, "object-field-migration"))
