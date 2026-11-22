#!/usr/bin/env python3
"""Retained initial class layout checks. Trailing arguments are the compiler command.

Requires an installed C compiler (CC, default cc). Each successful compilation
must exit zero with empty stderr; emitted executables must exit zero and write
exactly b'ok' to stdout with empty stderr. Both layouts run at O0 and O2.
These cases test indexed identity, growth, and explicit destruction. They do not prove allocation balance.
"""
import os
import json
import re
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

CLASS = '''class Counter {
 count: i32
 func Counter(self, n: i32) { self.count = n }
 func read(self) -> i32 { return self.count }
 func add(self, n: i32) { self.count = self.count + n }
}
'''
CASES = [
    ('array-field-reference-write', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func main() { let c=C(); let r=ref c.a; r.push(3); let element=ref c.a[0]; element=7; c.a[1]=8; assert(c.a[0]==7 && c.a[1]==8 && c.a[2]==3); c.destroy(); print("ok") }'),
    ('array-field-payload-survives-growth', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func grow() { var i=0; while i<257 { let c=C(); i=i+1 } }; func main() { let c=C(); let r=ref c.a[0]; grow(); r=9; assert(c.a[0]==9); print("ok") }'),
    ('array-field-descriptor-replacement', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func main() { let c=C(); let r=ref c.a; c.a=[9]; r.push(7); assert(c.a[0]==9 && c.a[1]==7); c.destroy(); print("ok") }'),
    ('array-field-other-field-helper', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func change(c:C,r:ref i32) { c.a=[9]; r=7 }; func main() { let c=C(); change(c,ref c.b[0]); assert(c.a[0]==9 && c.b[0]==7); c.destroy(); print("ok") }'),
    ('array-field-capacity-reset', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func main() { let c=C(); c.a.reserve(100u64); assert(capacity(c.a)>=100u64); let copy=c.a; assert(capacity(copy)==2u64); let r=ref c.a; r.resize(0u64); assert(capacity(c.a)==0u64 && len(c.a)==0u64); c.destroy(); print("ok") }'),
    ('array-field-nested-descriptor', 'class C { a:[[i32]]; func C(self) { self.a=[[1]] } }; func grow() { var i=0; while i<257 { let c=C(); i=i+1 } }; func main() { let c=C(); let r=ref c.a[0]; grow(); r.push(7); assert(c.a[0][1]==7); print("ok") }'),
    ('field-reference-write', CLASS + 'func set(r:ref i32) { r=19 }; func main() { let c=Counter(7); let r=ref c.count; set(ref r); if c.count==19 { c.destroy(); print("ok") } }'),
    ('field-reference-last-use', CLASS + 'func readThenGrow(r:ref i32)->i32 { let answer=r; let other=Counter(9); return answer }; func main() { let c=Counter(7); let answer=readThenGrow(ref c.count); let fresh=ref c.count; if answer==7 && fresh==7 { print("ok") } }'),
    ('counted-assignment-receiver', 'value class V { x:i32; func V(self) { self.x=1 }; final func finish(self) { if self.x==2 { print("ok") } } }; func main() { V().x=2 }'),
    ('counted-replacement', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func main() { var v=V("o"); v=V("k") }'),
    ('counted-empty-and-self-assignment', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func main() { var empty:V; var v:V; v=V("ok"); v=v }'),
    ('counted-return-alias', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func create()->V { let v=V("ok"); return (v) }; func main() { let v=create() }'),
    ('counted-loop-continue', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func main() { var i=0; while i<2 { let v=V(if i==0 { "o" } else { "k" }); i=i+1; continue } }'),
    ('counted-loop-break', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func main() { while true { let v=V("ok"); break } }'),
    ('counted-computed-field', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func main() { let b=(V("ok")).b }'),
    ('counted-short-cleanup-order', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func main() { let b=true && V("o").b; print("k") }'),
    ('counted-else-if-cleanup-order', 'value class V { label:string; b:bool; func V(self,label:string) { self.label=label; self.b=true }; final func finish(self) { print(self.label) } }; func main() { if false {} else if V("o").b {}; print("k") }'),
    ('counted-alias', 'value class V { x:i32; func V(self) { self.x=7 }; final func finish(self) { if self.x==9 { print("ok") } } }; func main() { let a=V(); let b=a; b.x=9 }'),
    ('counted-method-holder', 'value class V { x:i32; func V(self) { self.x=7 }; func set(self) { self.x=9 }; final func finish(self) { if self.x==9 { print("ok") } } }; func main() { let a=V(); a.set() }'),
    ('counted-return-holder', 'value class V { func V(self) {}; final func finish(self) { print("ok") } }; func create()->V { return V() }; func main() { let a=create() }'),
    ('counted-conditional-holder', 'value class V { func V(self) {}; final func finish(self) { print("ok") } }; func choose(b:bool)->V { return if b { V() } else { V() } }; func main() { let a=choose(true) }'),
    ('array-field-rhs-growth', 'class C { a:[i32]; func C(self) { self.a=[7] } }; func grow()->[i32] { var i=0; while i<257 { let c=C(); i=i+1 }; return [19] }; func main() { let c=C(); c.a=grow(); let a=c.a; c.destroy(); if a[0]==19 { print("ok") } }'),
    ('final-owned-field-growth', 'class C { a:string; func C(self) { self.a="ok" }; final func finish(self) { grow(); print(self.a) } }; func grow() { var i=0; while i<257 { let c=C(); i=i+1 } }; func main() { let c=C(); c.destroy() }'),
    ('array-field-copy', 'class C { a:[i32]; func C(self) { self.a=[7,9] } }; func main() { let c=C(); var a=c.a; a[0]=19; let b=c.a; if b[0]==7 { c.destroy(); print("ok") } }'),
    ('nested-field-copy', 'class C { a:[[i32]]; func C(self) { self.a=[[7]] } }; func main() { let c=C(); var a=c.a; a[0][0]=19; let b=c.a; if b[0][0]==7 { c.destroy(); print("ok") } }'),
    ('field-return-survives', 'class C { a:[i32]; func C(self) { self.a=[7] }; func read(self)->[i32] { return self.a } }; func main() { let c=C(); let a=c.read(); c.destroy(); if a[0]==7 { print("ok") } }'),
    ('field-self-assignment', 'class C { a:[[i32]]; func C(self) { self.a=[[7]] } }; func main() { let c=C(); c.a=c.a; let a=c.a; c.destroy(); if a[0][0]==7 { print("ok") } }'),
    ('final-string-field', 'class C { a:string; func C(self) { self.a="ok" }; final func finish(self) { print(self.a) } }; func main() { let c=C(); c.destroy() }'),
    ('array-field-reuse', 'class C { a:[[i32]]; func C(self,n:i32) { self.a=[[n]] } }; func main() { var i=0; var sum=0; while i<257 { let c=C(i); let a=c.a; sum=sum+a[0][0]; c.destroy(); i=i+1 }; if sum==32896 { print("ok") } }'),
    ('final-reads-field', 'class C { x:i32; func C(self) { self.x=7 }; final func finish(self) { if self.x==7 { print("ok") } } }; func main() { let c=C(); c.destroy() }'),
    ('final-return', 'class C { func C(self) {}; final func finish(self) { print("ok"); return } }; func main() { let c=C(); c.destroy() }'),
    ('final-other-class', 'class D { func D(self) {}; final func finish(self) { print("ok") } }; class C { func C(self) {}; final func finish(self) { let d=D(); d.destroy() } }; func main() { let c=C(); c.destroy() }'),
    ('final-growth', 'class C { x:i32; func C(self,n:i32) { self.x=n }; final func finish(self) { grow(); if self.x==7 { print("ok") } } }; func grow() { var i=0; while i<257 { let c=C(i); i=i+1 } }; func main() { let c=C(7); c.destroy() }'),
    ('destroy-reconstruct', CLASS + 'func main() { let c = Counter(7); c.destroy(); let d = Counter(19); if d.read() == 19 { d.destroy(); print("ok") } }'),
    ('repeated-reuse', CLASS + 'func main() { var i = 0; var sum = 0; while i < 257 { let c = Counter(i); sum = sum + c.read(); c.destroy(); i = i + 1 }; if sum == 32896 { print("ok") } }'),
    ('helper-destroy', CLASS + 'func dispose(c: Counter) { c.destroy() }; func main() { let c = Counter(7); dispose(c); let d = Counter(9); if d.count == 9 { dispose(d); print("ok") } }'),
    ('construct-read', CLASS + 'func main() { let c = Counter(7); if c.read() == 7 { print("ok") } }'),
    ('shared-handle', CLASS + 'func main() { let c = Counter(7); let d = c; d.add(2); if c.count == 9 { print("ok") } }'),
    ('growth-keeps-identity', CLASS + '''func main() {
 let first = Counter(7); var i = 0
 while i < 257 { let next = Counter(i); i = i + 1 }
 first.add(2); if first.read() == 9 { print("ok") }
}'''),
    ('field-rhs-growth', CLASS + '''func grow() -> i32 {
 var i = 0; while i < 257 { let next = Counter(i); i = i + 1 }; return 19
}
func main() { let c = Counter(7); c.count = grow(); if c.count == 19 { print("ok") } }
'''),
]

def run(command):
    return subprocess.run(command, capture_output=True, timeout=60)


# Object-array ownership: slot copies preserve identity, not object clones.
CASES += [('object-array-shared-identity',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'main() { let c=C(1); var a=[c]; var b=a; b[0].set(7); assert(a[0].x==7); b=[]; '
  'assert(len(a)==1u64); c.destroy(); print("ok") }'),
 ('object-array-reserve-push-shrink',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'main() { let c=C(3); var a:[C]=[]; a.reserve(8u64); a.push(c); assert(a[0].x==3); '
  'a.resize(0u64); assert(capacity(a)==0u64); c.destroy(); print("ok") }'),
 ('object-array-nested-empty-growth',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'main() { let c=C(4); var a:[[C]]=[]; a.resize(2u64); a[0].push(c); let b=a; assert(b[0][0].x==4 '
  '&& len(b[1])==0u64); c.destroy(); print("ok") }'),
 ('counted-array-copies',
  'value class V { x:i32; func V(self,n:i32) { self.x=n }; final func finish(self) { print("o") } '
  '}; func use() { let v=V(1); var a=[v]; var b=a; b[0].x=7; assert(a[0].x==7); a=[]; '
  'assert(b[0].x==7) }; func main() { use(); print("k") }'),
 ('counted-array-nested',
  'value class V { x:i32; func V(self,n:i32) { self.x=n }; final func finish(self) { print("o") } '
  '}; func use() { let a=[[V(2)]]; let b=a; assert(b[0][0].x==2) }; func main() { use(); '
  'print("k") }'),
 ('counted-array-return',
  'value class V { x:i32; func V(self,n:i32) { self.x=n }; final func finish(self) { print("o") } '
  '}; func make()->[V] { return [V(3)] }; func use() { var a=make(); assert(a[0].x==3); '
  'a.resize(0u64) }; func main() { use(); print("k") }'),
 ('counted-array-element-self-assignment',
  'value class V { x:i32; func V(self,n:i32) { self.x=n }; final func finish(self) { print("o") } '
  '}; func use() { var a=[V(5)]; a[0]=a[0]; assert(a[0].x==5); a.truncate(0u64) }; func main() { '
  'use(); print("k") }'),
 ('counted-array-owned-parameter',
  'value class V { x:i32; func V(self,n:i32) { self.x=n }; final func finish(self) { print("o") } '
  '}; func take(a:[V]) { assert(a[0].x==6) }; func use() { let a=[V(6)]; take(a); '
  'assert(a[0].x==6) }; func main() { use(); print("k") }')]


# Default growth may not invent null/noninitialized object handles. The same
# initialized-storage rule applies in unsafe mode. These failures are executed.
FAILURES = [
    ("ordinary-array-default-growth",
     "class C { func C(self) {} }; func main() { var a:[C]=[]; var n=1u64; a.resize(n) }"),
    ("counted-array-default-growth",
     "value class V { func V(self) {} }; func main() { var a:[V]=[]; var n=1u64; a.resize(n) }"),
]


CASES += [
    ("ordinary-array-fresh-replacement", "class C { x:i32; func C(self,n:i32) { self.x=n } }; "
     "func main() { let c=C(1); var a=[c]; c.destroy(); a=[C(2)]; "
     "assert(a[0].x==2); a[0].destroy(); print(\"ok\") }"),
]


def main():
    command = sys.argv[1:]
    if not command:
        raise SystemExit('Supply the compiler command after layout.py')
    total = 0
    with tempfile.TemporaryDirectory(prefix='lyric-layout-') as temporary:
        root = Path(temporary)
        cases = [(name, text, False, False) for name, text in CASES]
        cases += [(name + ("-unsafe" if unsafe else "-safe"), text, True, unsafe)
                  for name, text in FAILURES for unsafe in (False, True)]
        for name, text, fails, unsafe in cases:
            source = root / (name + '.ly')
            source.write_text(text + '\n')
            for layout in ('aos', 'soa'):
                options = ['--emit-c'] + (['--soa'] if layout == 'soa' else [])
                options += ['--unsafe'] if unsafe else []
                compiled = run(command + options + [str(source)])
                assert compiled.returncode == 0 and not compiled.stderr, (name, layout, compiled.stderr)
                cfile = root / (name + '-' + layout + '.c')
                cfile.write_bytes(compiled.stdout)
                for optimization in ('-O0', '-O2'):
                    executable = root / 'program'
                    built = run(shlex.split(os.environ.get('CC', 'cc')) + ['-std=c11', optimization, str(cfile), '-o', str(executable)])
                    assert built.returncode == 0, (name, layout, built.stderr)
                    result = run([str(executable)])
                    if fails:
                        # Exact diagnostic and sole main frame, with a positive
                        # source line/column. A crash or unrelated failure is not a pass.
                        location = re.escape(json.dumps(str(source)).encode())
                        expected = (location + rb':[1-9][0-9]*:[1-9][0-9]*: runtime error '
                                    rb'LYR011: array element has no default value\n  in "main"\n')
                        assert result.returncode == 1 and result.stdout == b'', (name, result)
                        assert re.fullmatch(expected, result.stderr), (name, result.stderr)
                    else:
                        assert (result.returncode, result.stdout, result.stderr) == (0, b'ok', b''), (name, layout, optimization, result)
                    total += 1
                    print('PASS', name, layout, optimization)
    print(f'{total}/{(len(CASES) + len(FAILURES) * 2) * 4} layout executions passed')

if __name__ == '__main__':
    main()
