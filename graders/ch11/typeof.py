#!/usr/bin/env python3
"""Retained Chapter 11 unevaluated typeof checks.

From the repository root: python3 graders/ch11/typeof.py COMPILER [ARG ...]
Uses the Chapter 10 public execution/diagnostic comparison contract unchanged.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch10'))
from tuples import main

CASES = [
    ('typeof-uninitialized', '''func main() {
    var original:i32
    let copy:typeof(original)=7
    assert(copy==7)
    print("ok")
}
''', b'ok'),
    ('typeof-call-not-executed', '''func noisy()->i32 { print("wrong"); return 9 }
func main() { let value:typeof(noisy())=3; assert(value==3); print("ok") }
''', b'ok'),
    ('typeof-dependent-body', '''func identity<T>(value:T)->T {
    let copy:typeof(value)=value
    return copy
}
func main() { assert(identity(7)==7); assert(identity(true)); print("ok") }
''', b'ok'),
    ('typeof-composite', '''func main() {
    var original:(i32,bool)
    let values:[typeof(original)]=[(3,true)]
    assert(values[0].0==3)
    assert(values[0].1)
    print("ok")
}
''', b'ok'),
]

NEGATIVE_CASES = [
    ('typeof-unknown-name', 'func main() { let n:typeof(missing)=3 }\n', 'LYC301'),
    ('typeof-invalid-operation', 'func main() { let n:typeof(true+1)=3 }\n', 'LYC312'),
    # The query grants no initialization to a subsequent real read.
    ('typeof-does-not-initialize', '''func main() {
    var original:i32
    let copy:typeof(original)=7
    print(original)
}
''', 'LYC307'),
]

CASES += [
    ('typeof-cast', '''func main() {
    var target:i64
    let value=7 as typeof(target)
    assert(value==7)
    print("ok")
}
''', b'ok'),
    ('typeof-expired-reference', '''func main() {
    var values=[3]
    let selected=ref values[0]
    values.resize(0)
    let n:typeof(selected)=7
    assert(n==7)
    print("ok")
}
''', b'ok'),
    ('typeof-isolated-assignment', '''func main() {
    var n=1
    let value:typeof(if true { n=9; n } else { n })=3
    assert(n==1)
    assert(value==3)
    print("ok")
}
''', b'ok'),
    ('typeof-branch-preview', '''func main() {
    var original:i64
    let n=if true { let value:typeof(original)=3; value } else { 4 }
    assert(n==3)
    print("ok")
}
''', b'ok'),
]
NEGATIVE_CASES += [
    ('typeof-assignment-does-not-escape', '''func main() {
    var original:i32
    let n:typeof(if true { original=9; original } else { 3 })=7
    let read=original
}
''', 'LYC307'),
]

CASES += [
    ('typeof-result-parameter', "func identity(value:i32)->typeof(value) { return value }\nfunc main() { assert(identity(7)==7); print(\"ok\") }\n", b'ok'),
    ('typeof-generic-result', "func identity<T>(value:T)->typeof(value) { return value }\nfunc main() { assert(identity(7)==7); assert(identity(true)); print(\"ok\") }\n", b'ok'),
    ('typeof-generic-call', "func identity<T>(value:T)->T { print(\"wrong\"); return value }\nfunc main() { let n:typeof(identity(7))=3; assert(n==3); print(\"ok\") }\n", b'ok'),
]

CASES += [
    ('typeof-signature-generic-discovery', "func identity<T>(value:T)->T { return value }\nfunc answer()->typeof(identity(7)) { return 9 }\nfunc main() { assert(answer()==9); print(\"ok\") }\n", b'ok'),
    ('typeof-signature-forward-call', "func answer()->typeof(later()) { return 9 }\nfunc later()->i32 { return 7 }\nfunc main() { assert(answer()==9); print(\"ok\") }\n", b'ok'),
]

NEGATIVE_CASES += [
    ('typeof-signature-cycle', "func first()->typeof(second()) { return 1 }\nfunc second()->typeof(first()) { return 1 }\nfunc main() {}\n", 'LYC345'),
]

NEGATIVE_CASES += [('typeof-empty', 'func main() { let x:typeof()=3 }', 'LYC201'), ('typeof-missing-close', 'func main() { let x:typeof(3=3 }', 'LYC201')]

CASES += [
    ('typeof-explicit-type-argument', "func identity<T>(value:T)->T { return value }\nfunc main() { let input=7; assert(identity<typeof(input)>(input)==7); print(\"ok\") }\n", b'ok'),
]

# Separate bodies retain distinct declaration and implementation name scopes.
CASES += [
    ('typeof-separate-same-name', 'struct S { func f(x:i32)->typeof(x); }\nfunc S.f(x:i32)->typeof(x) { return x }\nfunc main() { assert(S.f(7)==7); print("ok") }\n', b'ok'),
    ('typeof-separate-renamed-parameter', 'struct S { func f(x:i32)->typeof(x); }\nfunc S.f(y:i32)->typeof(y) { return y }\nfunc main() { assert(S.f(7)==7); print("ok") }\n', b'ok'),
    ('typeof-separate-discovers-generic', 'func id<T>(x:T)->T { return x }\nstruct S { func f()->i32; }\nfunc S.f()->typeof(id(7)) { return 9 }\nfunc main() { assert(S.f()==9); print("ok") }\n', b'ok'),
]
NEGATIVE_CASES += [
    ('typeof-separate-result-mismatch', 'struct S { func f(x:i32)->typeof(x); }\nfunc S.f(y:i32)->typeof(true) { return true }\nfunc main() {}\n', 'LYC342'),
]

if __name__ == '__main__':
    sys.exit(main(CASES, NEGATIVE_CASES, 'typeof'))
