"""Chapter 12 walkthrough: explicit linked-list membership and detachment.

Run from the repository root after a Release build. Uses installed local tools
and disposable files; no installation, network access, or persistent artifacts.
"""
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

compiler = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
cc = shlex.split(os.environ.get('CC', 'cc'))
source_text = r'''interface DoublyLinkedList<P,C> {
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
value class Graph {
    func Graph(self) { self.nodes.first=null; self.nodes.last=null }
}
value class Node {
    func Node(self) {
        self.graph.parent=null
        self.graph.previous=null
        self.graph.next=null
    }
}
impl DoublyLinkedList<Graph:nodes,Node:graph> {}
func main() {
    let owner=Graph()
    let first=Node()
    let second=Node()
    owner.nodes.append(first)
    owner.nodes.append(second)
    assert(owner.nodes.first==first)
    assert(owner.nodes.last==second)
    assert(first.graph.next==second)
    assert(second.graph.previous==first)
    println("two nodes linked")

    owner.nodes.remove(first)
    assert(owner.nodes.first==second)
    assert(second.graph.previous==null)
    assert(first.graph.parent==null)
    assert(first.graph.next==null)
    println("head removed; second remains")

    owner.nodes.remove(second)
    assert(owner.nodes.first==null)
    assert(owner.nodes.last==null)
    assert(second.graph.parent==null)
    assert(second.graph.previous==null)
    assert(second.graph.next==null)
    println("all links detached; list empty")
}
'''
expected = (
    b'two nodes linked\n'
    b'head removed; second remains\n'
    b'all links detached; list empty\n'
)


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


# Keep generated files disposable even when compilation or execution fails.
with tempfile.TemporaryDirectory(prefix='lyric-spin12-') as directory:
    folder = Path(directory)
    source = folder / 'links.ly'
    c_source = folder / 'links.c'
    binary = folder / 'links'
    source.write_text(source_text, encoding='utf-8')
    print('=== Source ===', flush=True)
    print(source_text, flush=True)

    emitted = run(compiler + ['--emit-c', str(source)])
    if emitted.returncode != 0 or emitted.stderr or not emitted.stdout:
        raise SystemExit(f'Emission failed: {emitted}')
    c_source.write_bytes(emitted.stdout)
    built = run(cc + ['-std=c11', '-O2', str(c_source), '-o', str(binary)])
    if built.returncode != 0:
        raise SystemExit(f'C compilation failed: {built.stderr!r}')

    # Successful assertions alone are insufficient: check the process contract too.
    result = run([str(binary)])
    if (result.returncode, result.stdout, result.stderr) != (0, expected, b''):
        raise SystemExit(f'Unexpected execution: {result}')
    print('=== Executed C ===', flush=True)
    print(result.stdout.decode(), end='', flush=True)
print('Temporary source, C, and executable removed.', flush=True)
