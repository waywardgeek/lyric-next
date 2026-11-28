"""Chapter 14 walkthrough: owning containers and the checked safety boundary.

Run from the repository root with an installed SDK and C compiler. All generated
files are disposable. No network, installation, or persistent application state.
"""
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

compiler = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
cc = shlex.split(os.environ.get('CC', 'cc'))
source_text = r'''interface List<parent P, child C> {
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
        pub func save_and_remove(self) {
            let saved=[self.first!]
            self.remove(saved[0])
            assert(self.first == null)
            println("container keeps detached child alive")
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
        partial refs destroy(self) {
            if self.parent != null {
                self.parent!.remove(self)
            }
        }
    }
}
class Graph {
    func Graph(self) {}
    final func finish(self) {
        assert(self.nodes.first == null)
        println("parent final sees empty relationship")
    }
}
value class Node {
    func Node(self) {}
    final func finish(self) {
        assert(self.graph.parent == null)
        assert(self.graph.previous == null)
        assert(self.graph.next == null)
        println("container cleanup finalizes detached child")
    }
}
impl List<Graph:nodes, Node:graph> refs {}
func main() {
    let graph = Graph()
    if true {
        let child = Node()
        graph.nodes.append(child)
    }
    graph.nodes.save_and_remove()
    println("owning container scope complete")
    var items=[7,8]
    items.reserve(32u64)
    items.truncate(1u64)
    items.resize(2u64)
    assert(items[0] == 7 && items[1] == 0)
    println("regrown elements initialized")
    graph.destroy()
    println("parent teardown complete")
}
'''
expected = (
    b'container keeps detached child alive\n'
    b'container cleanup finalizes detached child\n'
    b'owning container scope complete\n'
    b'regrown elements initialized\n'
    b'parent final sees empty relationship\n'
    b'parent teardown complete\n'
)

# This candidate is checked only. Never emit or run a stale-borrow experiment.
rejected_source = """class Owner {
    value:i32
    func Owner(self) { self.value=0 }
}
func update(value:ref i32, owner:Owner) {
    owner.destroy()
    value=7
}
func main() {
    let owner=Owner()
    update(ref owner.value,owner)
}
"""


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


# Read the source once, then observe the same behavior in both actual layouts.
# Runtime debug checks are independent of the compiler's Release configuration.
print('=== Source ===', flush=True)
print(source_text, flush=True)
with tempfile.TemporaryDirectory(prefix='lyric-spin14-') as directory:
    folder = Path(directory)
    source = folder / 'relationships.ly'
    source.write_text(source_text, encoding='utf-8')
    for layout, flags in [
        ('AoS-production', []),
        ('SoA-production', ['--soa']),
        ('AoS-debug', ['--debug']),
        ('SoA-debug', ['--soa', '--debug']),
    ]:
        emitted = run(compiler + ['--emit-c'] + flags + [str(source)])
        if emitted.returncode != 0 or emitted.stderr or not emitted.stdout:
            raise SystemExit(f'{layout} emission failed: {emitted}')
        c_source = folder / f'{layout}.c'
        binary = folder / layout
        c_source.write_bytes(emitted.stdout)
        built = run(cc + ['-std=c11', '-O2', str(c_source), '-o', str(binary)])
        if built.returncode != 0:
            raise SystemExit(f'{layout} C compilation failed: {built.stderr!r}')
        result = run([str(binary)])
        if (result.returncode, result.stdout, result.stderr) != (0, expected, b''):
            raise SystemExit(f'{layout} unexpected execution: {result}')
        print(f'=== {layout} executed C ===', flush=True)
        print(result.stdout.decode(), end='', flush=True)
    invalid = folder / 'rejected-borrow.ly'
    invalid.write_text(rejected_source, encoding='utf-8')
    print('=== Compile-only rejected source ===', flush=True)
    print(rejected_source, flush=True)
    checked = run(compiler + ['--check', str(invalid)])
    diagnostic = b'LYC326: Borrowed storage may have been invalidated.'
    if checked.returncode != 1 or checked.stdout or diagnostic not in checked.stderr:
        raise SystemExit(f'Expected lifetime rejection: {checked}')
    print(checked.stderr.decode(), end='', flush=True)
    print('Invalid borrow rejected; program was not executed.', flush=True)
print('Temporary source, C, and executables removed.', flush=True)
