"""Retained Chapter 14 ordered cross-call safety contracts.

Compile only: never execute the rejected programs. Positives require exit 0 and
empty streams; negatives require exit 1, empty stdout, and the specified code
and message on stderr. Locations are not normalized. Supply compiler argv.
"""
import pathlib
import re
import subprocess
import sys
import tempfile

OBJECT = "class Node { count:i32; func Node(self) { self.count=0 } }; "
INVALID = "LYC326: Borrowed storage may have been invalidated."
CASES = [
    ("direct-destroy-before-use", OBJECT + """
        func update(value:ref i32, node:Node) { node.destroy(); value=7 }
        func use() { let node=Node(); update(ref node.count,node) }
    """, INVALID),
    ("direct-use-before-destroy", OBJECT + """
        func update(value:ref i32, node:Node) { value=7; node.destroy() }
        func use() { let node=Node(); update(ref node.count,node) }
    """, None),
    ("forwarded-destroy-before-use", OBJECT + """
        func remove(node:Node) { node.destroy() }
        func update(value:ref i32, node:Node) { remove(node); value=7 }
        func forward(value:ref i32,node:Node) { update(ref value,node) }
        func use() { let node=Node(); forward(ref node.count,node) }
    """, INVALID),
    ("forwarded-use-before-destroy", OBJECT + """
        func remove(node:Node) { node.destroy() }
        func update(value:ref i32, node:Node) { value=7; remove(node) }
        func forward(value:ref i32,node:Node) { update(ref value,node) }
        func use() { let node=Node(); forward(ref node.count,node) }
    """, None),
    ("nested-replacement-before-use", """
        func update(value:ref i32, arrays:ref [[i32]]) {
            arrays[0]=[2]; value=7
        }
        func use() { var arrays=[[1]]; update(ref arrays[0][0],ref arrays) }
    """, INVALID),
    ("nested-use-before-replacement", """
        func update(value:ref i32, arrays:ref [[i32]]) {
            value=7; arrays[0]=[2]
        }
        func use() { var arrays=[[1]]; update(ref arrays[0][0],ref arrays) }
    """, None),
]


# A nested value copy must preserve the lifetime of the address it contains.
BORROWED = "struct Borrowed { target:ref i32 }; "
for before in (True, False):
    operations = "node.destroy(); holder.0.target=7" if before else "holder.0.target=7; node.destroy()"
    CASES.append((
        "nested-alias-" + ("destroy-before-use" if before else "use-before-destroy"),
        OBJECT + BORROWED + """
            func update(holder:(Borrowed,),node:Node) { OPERATIONS }
            func use() {
                let node=Node()
                let holder=(Borrowed { target:ref node.count },)
                let copied=holder
                update(copied,node)
            }
        """.replace("OPERATIONS", operations),
        INVALID if before else None,
    ))

# Cleanup is a real destruction effect, even without an explicit destroy at
# the call site. The finalizer conservatively invalidates Node field borrows.
FINALIZER = """
value class Trigger {
    func Trigger(self) {}
    final func finish(self) { let other=Node(); other.destroy() }
}
"""
for before in (True, False):
    cleanup = "if true { let trigger=Trigger() }"
    operations = cleanup + "; value=7" if before else "value=7; " + cleanup
    CASES.append((
        "cleanup-" + ("before-borrow-use" if before else "after-last-borrow-use"),
        OBJECT + FINALIZER + """
            func update(value:ref i32) { OPERATIONS }
            func use() { let node=Node(); update(ref node.count) }
        """.replace("OPERATIONS", operations),
        INVALID if before else None,
    ))


for before in (True, False):
    operations = "node.destroy(); value=7" if before else "value=7; node.destroy()"
    CASES.append((
        "generic-" + ("destroy-before-use" if before else "use-before-destroy"),
        OBJECT + """
            func update<T>(value:ref i32,node:T) { OPERATIONS }
            func use() { let node=Node(); update(ref node.count,node) }
        """.replace("OPERATIONS", operations),
        INVALID if before else None,
    ))

CASES.extend([
    ("loop-backedge-expired-alias", """
        func use(again:bool) {
            var items=[1]
            let value=ref items[0]
            while again { value=7; items.resize(2u64) }
        }
    """, INVALID),
    ("loop-fresh-acquisition", """
        func use(again:bool) {
            var items=[1]
            while again { let value=ref items[0]; value=7; items.resize(2u64) }
        }
    """, None),
])


# Recursion must preserve temporal obligations through a fixed point. These are
# compile-only: the negative writes while unwinding after the base destroyed the
# owner. Its positive companion performs every write before descending further.
for mutual in (False, True):
    for after in (False, True):
        target = "forward" if mutual else "visit"
        call = target + "(ref value,node,depth-1)"
        operations = call + "; value=7" if after else "value=7; " + call
        forwarding = """
            func forward(value:ref i32,node:Node,depth:i32) {
                visit(ref value,node,depth)
            }
        """ if mutual else ""
        CASES.append((
            ("mutual" if mutual else "direct") + "-recursion-"
            + ("use-after-base-destruction" if after else "use-before-descent"),
            OBJECT + """
                func visit(value:ref i32,node:Node,depth:i32) {
                    if depth==0 { node.destroy(); return }
                    OPERATIONS
                }
                FORWARDING
                func use() { let node=Node(); visit(ref node.count,node,2) }
            """.replace("OPERATIONS", operations).replace("FORWARDING", forwarding),
            INVALID if after else None,
        ))


def main(cases=CASES, label="cross-call safety"):
    if len(sys.argv) < 2:
        raise SystemExit("usage: safety_calls.py COMPILER [ARGS...]")
    failures = 0
    with tempfile.TemporaryDirectory(prefix="lyric-ch14-safety-") as directory:
        path = pathlib.Path(directory) / "case.ly"
        for name, source, expected in cases:
            path.write_text(source + "\nfunc main() {}\n", encoding="utf-8")
            result = subprocess.run(
                sys.argv[1:] + ["--check", str(path)],
                capture_output=True,
                timeout=30,
            )
            if expected is None:
                passed = result.returncode == 0 and not result.stdout and not result.stderr
            else:
                passed = (
                    result.returncode == 1
                    and not result.stdout
                    and re.fullmatch(
                        re.escape(f'"{path}":'.encode())
                        + rb'[1-9][0-9]*:[1-9][0-9]*: error '
                        + re.escape(expected.encode())
                        + rb' \(byte [0-9]+\)\n',
                        result.stderr,
                    ) is not None
                )
            print(("PASS " if passed else "FAIL ") + name)
            if not passed:
                failures += 1
                print(repr((result.returncode, result.stdout, result.stderr)))
    print(f"{len(cases)-failures}/{len(cases)} Chapter 14 {label} checks passed")
    return bool(failures)


if __name__ == "__main__":
    raise SystemExit(main())
