"""Chapter 13 walkthrough: an attachment owns a counted child's lifetime.

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
        assert(self.nodes.first != null)
        println("parent final sees attached child")
    }
}
value class Node {
    func Node(self) {}
    final func finish(self) {
        assert(self.graph.parent == null)
        assert(self.graph.previous == null)
        assert(self.graph.next == null)
        println("child final sees cleared links")
    }
}
impl List<Graph:nodes, Node:graph> refs {}
func main() {
    let graph = Graph()
    if true {
        let child = Node()
        graph.nodes.append(child)
    }
    println("attachment keeps child alive")
    graph.destroy()
    println("parent teardown complete")
}
'''
expected = (
    b'attachment keeps child alive\n'
    b'parent final sees attached child\n'
    b'child final sees cleared links\n'
    b'parent teardown complete\n'
)


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


# Read the source once, then observe the same behavior in both actual layouts.
# Runtime debug checks are independent of the compiler's Release configuration.
print('=== Source ===', flush=True)
print(source_text, flush=True)
with tempfile.TemporaryDirectory(prefix='lyric-spin13-') as directory:
    folder = Path(directory)
    source = folder / 'relationships.ly'
    source.write_text(source_text, encoding='utf-8')
    for layout, flags in [('AoS', []), ('SoA', ['--soa'])]:
        emitted = run(compiler + ['--emit-c', '--debug'] + flags + [str(source)])
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
        print(f'=== {layout} executed C (runtime debug) ===', flush=True)
        print(result.stdout.decode(), end='', flush=True)
print('Temporary source, C, and executables removed.', flush=True)
