"""Visible Chapter 8 checking cases. Supply the compiler command as trailing args.

Success requires exit 0 and empty stdout/stderr. Rejection requires exit 1,
empty stdout, and the specified diagnostic code AND message in stderr.
Paths/locations are printed by the compiler; these cases do not normalize them.
These behavioral checks are retained; class emission is tested separately.
"""
import pathlib
import subprocess
import sys
import tempfile

CASES = [
    ('review-tail-scalar', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { let result=if b { let v=V(); D() } else { D() }; assert(result.x==1) }', 'LYC330: Object reference may have been invalidated.'),
    ('review-tail-array', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { let result=if b { let v=V(); [D()] } else { [D()] }; assert(result[0].x==1) }', 'LYC330: Object reference may have been invalidated.'),
    ('review-tail-safe', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { let result=if b { D() } else { D() }; assert(result.x==1) }', None),
    ('review-tail-else-if', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool,c:bool) { let result=if b { D() } else if c { let v=V(); D() } else { D() }; assert(result.x==1) }', 'LYC330: Object reference may have been invalidated.'),
    ('review-populate-alias', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { var d:D; if b { var a:[V]=[]; let r=ref a; r=[V()]; d=D() } else { d=D() }; assert(d.x==1) }', 'LYC330: Object reference may have been invalidated.'),
    ('review-populate-chain', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { var d:D; if b { var a:[V]=[]; let r=ref a; let s=ref r; s=[V()]; d=D() } else { d=D() }; assert(d.x==1) }', 'LYC330: Object reference may have been invalidated.'),
    ('review-populate-nested', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { var d:D; if b { var a:[[V]]=[[]]; let r=ref a[0]; r=[V()]; d=D() } else { d=D() }; assert(d.x==1) }', 'LYC330: Object reference may have been invalidated.'),
    ('review-populate-direct', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { var d:D; if b { var a:[V]=[]; a=[V()]; d=D() } else { d=D() }; assert(d.x==1) }', 'LYC330: Object reference may have been invalidated.'),
    ('review-alias-empty', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { var d:D; if b { var a:[V]=[]; let r=ref a; r=[]; d=D() } else { d=D() }; assert(d.x==1) }', None),

    ('array-field-constructor-local-invalid', 'class C { a:[i32]; func C(self) { self.a=[1]; let r=ref self.a[0]; self.a=[2]; r=3 } }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-nested-descriptor-growth', 'class C { a:[[i32]]; func C(self) { self.a=[[1]] } }; func use() { let c=C(); let r=ref c.a[0]; let other=C(); r.push(2) }', None),
    ('array-field-nested-outer-replace', 'class C { a:[[i32]]; func C(self) { self.a=[[1]] } }; func use() { let c=C(); let r=ref c.a[0]; c.a=[[2]]; r.push(2) }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-reference', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a; r.push(3) }', None),
    ('array-field-element-write', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a[0]; r=7; c.a[1]=8 }', None),
    ('array-field-descriptor-growth', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a; let other=C(); r.push(3) }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-payload-growth', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a[0]; let other=C(); r=7 }', None),
    ('array-field-payload-resize', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a[0]; c.a.push(3); r=7 }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-payload-replace', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a[0]; c.a=[9]; r=7 }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-descriptor-replace', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a; c.a=[9]; r.push(3) }', None),
    ('array-field-helper-effect', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func grow(c:C) { c.a.push(3) }; func use() { let c=C(); let r=ref c.a[0]; grow(c); r=7 }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-helper-order', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func change(c:C,r:ref i32) { c.a=[9]; r=7 }; func use() { let c=C(); change(c,ref c.a[0]) }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-helper-other-field', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func change(c:C,r:ref i32) { c.a=[9]; r=7 }; func use() { let c=C(); change(c,ref c.b[0]) }', None),
    ('array-field-payload-destroy', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a[0]; c.destroy(); r=7 }', 'LYC326: Borrowed storage may have been invalidated.'),
    ('array-field-last-use', 'class C { a:[i32]; b:[i32]; func C(self) { self.a=[1,2]; self.b=[3] } }; func use() { let c=C(); let r=ref c.a[0]; r=7; c.a.push(3) }', None),
    ('counted-assignment-receiver-cleanup', 'class D { func D(self) {} }; value class V { x:i32; func V(self) { self.x=1 }; final func finish(self) { let dead=D(); dead.destroy() } }; func use()->D { let d=D(); V().x=2; return d }', 'LYC330: Object reference may have been invalidated.'),
    ('counted-else-if-cleanup', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { b:bool; func V(self) { self.b=true }; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool)->i32 { let d=D(); if b {} else if V().b {}; return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('counted-else-if-named', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { b:bool; func V(self) { self.b=true }; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { let d=D(); let v=V(); if b {} else if v.b {}; let x=d.x }', None),
    ('counted-short-and-cleanup', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { b:bool; func V(self) { self.b=true }; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool)->i32 { let d=D(); let result=b && V().b; return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('counted-short-or-cleanup', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { b:bool; func V(self) { self.b=true }; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool)->i32 { let d=D(); let result=b || V().b; return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('counted-short-named-borrow', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { b:bool; func V(self) { self.b=true }; final func finish(self) { let dead=D(); dead.destroy() } }; func use(b:bool) { let d=D(); let v=V(); let result=b && v.b; let x=d.x }', None),
    ('counted-final-self-copy', 'value class V { func V(self) {}; final func finish(self) { let alias=self } }', 'LYC334: A final hook may not transitively destroy its own class.'),
    ('counted-final-self-argument', 'value class V { func V(self) {}; final func finish(self) { consume(self) } }; func consume(v:V) {}', 'LYC334: A final hook may not transitively destroy its own class.'),
    ('counted-final-field-read', 'value class V { x:i32; func V(self) { self.x=7 }; final func finish(self) { let x=self.x } }', None),
    (
        "counted-conditional-transfer",
        "class D { x:i32; func D(self) { self.x=1 } }; "
        "value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; "
        "func use(b:bool) { let d=D(); let v=if b { V() } else { V() }; let x=d.x }",
        None,
    ),
    (
        "counted-conditional-discard",
        "class D { x:i32; func D(self) { self.x=1 } }; "
        "value class V { func V(self) {}; final func finish(self) { let dead=D(); dead.destroy() } }; "
        "func use(b:bool) { let d=D(); if b { V(); } else { V(); }; let x=d.x }",
        "LYC330: Object reference may have been invalidated.",
    ),
    ('final-valid', 'class C { func C(self) {}; final func finish(self) {} }', None),
    ('final-direct-call', 'class C { func C(self) {}; final func finish(self) {} }; func use() { let c=C(); c.finish() }', 'LYC332: A final hook may only be invoked by generated destruction.'),
    ('final-result', 'class C { func C(self) {}; final func finish(self)->i32 { return 1 } }', 'LYC333: A final hook must take only self and return unit.'),
    ('final-argument', 'class C { func C(self) {}; final func finish(self,n:i32) {} }', 'LYC333: A final hook must take only self and return unit.'),
    ('final-duplicate', 'class C { func C(self) {}; final func first(self) {}; final func second(self) {} }', 'LYC333: A class may have only one final hook.'),
    ('final-constructor', 'class C { final func C(self) {} }', 'LYC333: A constructor cannot be a final hook.'),
    ('final-destroys-self', 'class C { func C(self) {}; final func finish(self) { self.destroy() } }', 'LYC334: A final hook may not transitively destroy its own class.'),
    ('final-helper-destroys-class', 'class C { func C(self) {}; final func finish(self) { remove(self) } }; func remove(c:C) { c.destroy() }', 'LYC334: A final hook may not transitively destroy its own class.'),
    ('final-other-class', 'class D { func D(self) {} }; class C { func C(self) {}; final func finish(self) { let d=D(); d.destroy() } }', None),
    ('final-effects-reach-caller', 'class D { x:i32; func D(self) { self.x=1 } }; class C { func C(self) {}; final func finish(self) { let d=D(); d.destroy() } }; func use()->i32 { let d=D(); let c=C(); c.destroy(); return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('final-cross-class-cycle', 'class D { func D(self) {}; final func finish(self) { let c=C(); c.destroy() } }; class C { func C(self) {}; final func finish(self) { let d=D(); d.destroy() } }', 'LYC334: A final hook may not transitively destroy its own class.'),
    ("initialized-field", "class C { x:i32; func C(self,n:i32) { self.x=n }; func read(self)->i32 { return self.x } }", None),
    ("mutable-object-parameter", "class C { x:i32; func C(self) { self.x=0 } }; func change(c:C) { c.x=3 }", None),
    ("both-branches", "class C { x:i32; func C(self,b:bool) { if b { self.x=1 } else { self.x=2 } } }", None),
    ("one-branch", "class C { x:i32; func C(self,b:bool) { if b { self.x=1 } } }", "LYC328: Field 'x' is not definitely initialized."),
    ("early-return", "class C { x:i32; func C(self,b:bool) { if b { return }; self.x=1 } }", "LYC328: Field 'x' is not definitely initialized."),
    ("initialized-return", "class C { x:i32; func C(self,b:bool) { if b { self.x=1; return }; self.x=2 } }", None),
    ("read-before-write", "class C { x:i32; func C(self) { self.x=self.x+1 } }", "LYC328: Field 'x' is not definitely initialized."),
    ("partial-self-copy", "class C { x:i32; func C(self) { let alias=self; self.x=1 } }", "LYC328: Field 'x' is not definitely initialized."),
    ("partial-self-helper", "class C { x:i32; func C(self) { take(self); self.x=1 } }; func take(c:C) {}", "LYC328: Field 'x' is not definitely initialized."),
    ("initialized-self-helper", "class C { x:i32; func C(self) { self.x=1; take(self) } }; func take(c:C) {}", None),
    ("other-object-not-self", "class C { x:i32; func C(self,other:C) { other.x=1 } }", "LYC328: Field 'x' is not definitely initialized."),
    ("owned-array-field", "class C { values:[i32]; func C(self) { self.values=[1,2] }; func read(self)->[i32] { return self.values } }", None),
    ("field-hint", "class C { x:u8; func C(self) { self.x=1 }; func read(self)->u8 { return self.x+1 } }", None),
    ("unknown-field", "class C { func C(self) {}; func read(self)->i32 { return self.missing } }", "LYC329: No field 'missing' exists on C."),
    ("constructor-call", "class C { x:i32; func C(self,n:i32) { self.x=n } }; func use() { let c=C(7); c.x=8 }", None),
    ("method-call", "class C { x:u8; func C(self) { self.x=1 }; func read(self)->u8 { return self.x } }; func use()->u8 { let c=C(); return c.read()+1 }", None),
    ("returned-handle", "class C { func C(self) {} }; func make()->C { return C() }; func use() { let c=make() }", None),
    ("constructor-arity", "class C { func C(self,n:i32) {} }; func use() { let c=C() }", "LYC303: Function 'C' expects 1 argument(s), got 0."),
    ("no-constructor", "class C {}; func use() { let c=C() }", "LYC329: Class 'C' has no constructor."),
    ("no-reinitialization", "class C { func C(self) {} }; func use() { let c=C(); c.C() }", "LYC329: No instance method 'C' exists on C."),
    ("method-ref-offset", "class C { func C(self) {}; func grow(self,a:ref [i32]) { a.push(2) } }; func use() { let c=C(); var a=[1]; c.grow(ref a) }", None),
    ("method-borrow-effect", "class C { func C(self) {}; func grow(self,a:ref [i32]) { a.push(2) } }; func use()->i32 { let c=C(); var a=[1]; let r=ref a[0]; c.grow(ref a); return r }", "LYC326: Borrowed storage may have been invalidated."),
    ("partial-self-method", "class C { x:i32; func C(self) { self.read(); self.x=1 }; func read(self)->i32 { return self.x } }", "LYC328: Field 'x' is not definitely initialized."),
    ("destroy-last-use", "class C { func C(self) {} }; func use() { let c=C(); c.destroy() }", None),
    ("destroy-stale-field", "class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { let c=C(); c.destroy(); return c.x }", "LYC330: Object reference may have been invalidated."),
    ("destroy-stale-alias", "class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { let c=C(); let other=c; c.destroy(); return other.x }", "LYC330: Object reference may have been invalidated."),
    ("destroy-twice", "class C { func C(self) {} }; func use() { let c=C(); c.destroy(); c.destroy() }", "LYC330: Object reference may have been invalidated."),
    ("destroy-fresh-object", "class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { let c=C(); c.destroy(); let fresh=C(); return fresh.x }", None),
    ("destroy-does-not-revive-alias", "class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { let c=C(); let old=c; c.destroy(); let fresh=C(); return old.x }", "LYC330: Object reference may have been invalidated."),
    ("destroy-helper-effect", "class C { x:i32; func C(self) { self.x=1 } }; func remove(c:C) { c.destroy() }; func use()->i32 { let c=C(); remove(c); return c.x }", "LYC330: Object reference may have been invalidated."),
    ("destroy-helper-internal-use", "class C { x:i32; func C(self) { self.x=1 } }; func remove(c:C,other:C)->i32 { c.destroy(); return other.x }", "LYC330: Object reference may have been invalidated."),
    ("destroy-conditional-effect", "class C { x:i32; func C(self) { self.x=1 } }; func use(b:bool)->i32 { let c=C(); if b { c.destroy() }; return c.x }", "LYC330: Object reference may have been invalidated."),
    ("destroy-during-field-rhs", "class C { x:i32; func C(self) { self.x=1 } }; func remove(c:C)->i32 { c.destroy(); return 2 }; func use() { let c=C(); c.x=remove(c) }", "LYC330: Object reference may have been invalidated."),
    ("destroy-during-later-argument", "class C { func C(self) {} }; func remove(c:C)->i32 { c.destroy(); return 2 }; func consume(c:C,n:i32) {}; func use() { let c=C(); consume(c,remove(c)) }", "LYC330: Object reference may have been invalidated."),
    ("growth-keeps-handle", "class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { let c=C(); let other=C(); return c.x }", None),
    ('destroy-constructor-self', 'class C { func C(self) { self.destroy() } }', 'LYC330: Object reference may have been invalidated.'),
    ('destroy-constructor-return', 'class C { func C(self) { self.destroy(); return } }', 'LYC330: Object reference may have been invalidated.'),
    ('destroy-delayed-binding', 'class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { var c:C; c=C(); c.destroy(); return c.x }', 'LYC330: Object reference may have been invalidated.'),
    ('destroy-reassign-fresh', 'class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { var c=C(); c.destroy(); c=C(); return c.x }', None),
    ('destroy-transitive', 'class C { x:i32; func C(self) { self.x=1 } }; func remove(c:C) { c.destroy() }; func forward(c:C) { remove(c) }; func use()->i32 { let c=C(); forward(c); return c.x }', 'LYC330: Object reference may have been invalidated.'),
    ('destroy-counted-forbidden', 'value class C { func C(self) {} }; func use() { let c=C(); c.destroy() }', 'LYC331: Counted value objects cannot be explicitly destroyed.'),
    ('destroy-arity', 'class C { func C(self) {} }; func use() { let c=C(); c.destroy(1) }', 'LYC303: Destruction expects no arguments.'),
    ('destroy-other-class', 'class C { func C(self) {} }; class D { x:i32; func D(self) { self.x=1 } }; func use()->i32 { let c=C(); let d=D(); c.destroy(); return d.x }', None),
]

# Implicit counted cleanup must participate in ordinary class invalidation.
CASES += [
    ('release-helper-fallthrough', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func drop() { let v=V() }; func use()->i32 { let d=D(); drop(); return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-helper-return', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func drop() { let v=V(); return }; func use()->i32 { let d=D(); drop(); return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-branch-exit', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use(b:bool)->i32 { let d=D(); if b { let v=V() }; return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-replacement', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->i32 { let d=D(); var v=V(); v=V(); return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-break', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->i32 { let d=D(); while true { let v=V(); break }; return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-continue', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use(b:bool)->i32 { let d=D(); while b { let v=V(); continue }; return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-discarded-temporary', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->i32 { let d=D(); V(); return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-retained-alias', 'value class V { x:i32; func V(self) { self.x=7 } }; func use(b:bool)->i32 { let v=V(); if b { let alias=v }; return v.x }', None),
]

CASES += [
    ('release-invalidates-returned-object', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->D { let d=D(); let v=V(); return d }', 'LYC330: Object reference may have been invalidated.'),
    ('release-owned-parameter', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func drop(v:V) {}; func use()->i32 { let d=D(); drop(V()); return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-final-same-class', 'value class V { func V(self) {}; final func finish(self) { let other=V() } }', 'LYC334: A final hook may not transitively destroy its own class.'),
]

CASES += [
    ('release-temporary-method-receiver', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; func read(self)->i32 { return 7 }; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->i32 { let d=D(); V().read(); return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-temporary-field-receiver', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=7 }; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->D { let d=D(); let n=V().x; return d }', 'LYC330: Object reference may have been invalidated.'),
    ('temporary-field-control', 'value class V { x:i32; func V(self) { self.x=7 } }; func use()->i32 { return V().x }', None),
    ('retained-method-receiver-control', 'value class V { x:i32; func V(self) { self.x=7 }; func read(self)->i32 { return self.x } }; func use()->i32 { let v=V(); v.read(); return v.x }', None),
]

CASES += [
    ('release-condition-false-exit', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { more:bool; func V(self) { self.more=false }; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->i32 { let d=D(); while V().more {}; return d.x }', 'LYC330: Object reference may have been invalidated.'),
    ('release-condition-back-edge', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { more:bool; func V(self) { self.more=true }; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use() { let d=D(); while V().more { let n=d.x } }', 'LYC330: Object reference may have been invalidated.'),
    ('release-condition-continue', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { more:bool; func V(self) { self.more=true }; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use() { let d=D(); while V().more { let n=d.x; continue } }', 'LYC330: Object reference may have been invalidated.'),
]

CASES += [
    ('empty-counted-local-no-final', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func empty() { var v:V }; func use()->i32 { let d=D(); empty(); return d.x }', None),
    ('empty-counted-local-in-final', 'value class V { func V(self) {}; final func finish(self) { var unused:V } }', None),
    ('first-counted-assignment-no-old-release', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use()->i32 { let d=D(); var v:V; v=V(); let answer=d.x; return answer }', None),
]

CASES += [
    ('conditional-holder-release-invalidates-return', 'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func finish(self) { let temporary=D(); temporary.destroy() } }; func use(b:bool)->D { let d=D(); var v:V; if b { v=V() }; return d }', 'LYC330: Object reference may have been invalidated.'),
    ('conditional-holder-empty-path-control', 'value class V { func V(self) {} }; func use(b:bool) { var v:V; if b { v=V() } }', None),
]

# Stable field borrows depend on class backing, not the scalar field type.
# Allocation is conservatively invalidating in both output layouts. A borrow
# whose final use precedes the effect remains valid.
FIELD_CLASS = "class C { x:i32; func C(self) { self.x=1 } }; "
CASES += [
    ("field-ref-read", FIELD_CLASS + "func use()->i32 { let c=C(); let r=ref c.x; return r }", None),
    ("field-ref-write", FIELD_CLASS + "func use()->i32 { let c=C(); let r=ref c.x; r=7; return c.x }", None),
    ("field-ref-growth", FIELD_CLASS + "func use()->i32 { let c=C(); let r=ref c.x; let other=C(); return r }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-last-use", FIELD_CLASS + "func use()->i32 { let c=C(); let r=ref c.x; let answer=r; let other=C(); return answer }", None),
    ("field-ref-helper-growth", FIELD_CLASS + "func grow() { let other=C() }; func use()->i32 { let c=C(); let r=ref c.x; grow(); return r }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-destroy", FIELD_CLASS + "func use()->i32 { let c=C(); let r=ref c.x; c.destroy(); return r }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-other-class-growth", FIELD_CLASS + "class D { func D(self) {} }; func use()->i32 { let c=C(); let r=ref c.x; let d=D(); return r }", None),
    ("field-ref-reacquire", FIELD_CLASS + "func use()->i32 { let c=C(); let old=ref c.x; let other=C(); let fresh=ref c.x; return fresh }", None),
]

# Ordered helper obligations preserve use-before-growth while rejecting the
# inverse order, even when only the caller knows the storage's owning class.
CASES += [
    ("field-ref-helper-internal-use", FIELD_CLASS + "func grow(r:ref i32)->i32 { let c=C(); return r }; func use()->i32 { let c=C(); return grow(ref c.x) }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-helper-use-first", FIELD_CLASS + "func grow(r:ref i32)->i32 { let answer=r; let c=C(); return answer }; func use()->i32 { let c=C(); return grow(ref c.x) }", None),
    ("field-ref-helper-local-control", FIELD_CLASS + "func grow(r:ref i32)->i32 { let c=C(); return r }; func use()->i32 { var x=7; return grow(ref x) }", None),
    ("field-ref-helper-forward", FIELD_CLASS + "func inner(r:ref i32)->i32 { let c=C(); return r }; func outer(r:ref i32)->i32 { return inner(ref r) }; func use()->i32 { let c=C(); return outer(ref c.x) }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-constructor-argument", "class C { x:i32; func C(self,r:ref i32) { self.x=r } }; func use() { var x=1; let c=C(ref x); let other=C(ref c.x) }", "LYC326: Borrowed storage may have been invalidated."),
]

CASES += [
    ("field-ref-later-argument", FIELD_CLASS + "func grow()->i32 { let other=C(); return 1 }; func read(r:ref i32,n:i32)->i32 { return r }; func use()->i32 { let c=C(); return read(ref c.x,grow()) }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-final-growth", FIELD_CLASS + "class D { func D(self) {}; final func finish(self) { let other=C() } }; func use()->i32 { let c=C(); let d=D(); let r=ref c.x; d.destroy(); return r }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-release-growth", FIELD_CLASS + "value class V { func V(self) {}; final func finish(self) { let other=C() } }; func use()->i32 { let c=C(); let r=ref c.x; if true { let v=V() }; return r }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-loop-growth", FIELD_CLASS + "func use(b:bool) { let c=C(); let r=ref c.x; while b { let n=r; let other=C() } }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-alias-does-not-revive", FIELD_CLASS + "func use()->i32 { let c=C(); let r=ref c.x; let other=C(); let alias=ref r; return alias }", "LYC326: Borrowed storage may have been invalidated."),
    ("field-ref-counted-control", "value class C { x:i32; func C(self) { self.x=1 } }; func use()->i32 { let c=C(); let r=ref c.x; r=7; return r }", None),
]

# Object-array ownership: slot copies preserve identity, not object clones.
CASES += [('object-array-explicit',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'use() { let c=C(1); let a:[C]=[c]; let n=a[0].x }',
  None),
 ('object-array-inferred',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'use() { let a=[C(1)]; let n=a[0].x }',
  None),
 ('object-array-stale-element',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'use() { let c=C(1); let a=[c]; c.destroy(); let n=a[0].x }',
  'LYC330: Object reference may have been invalidated.'),
 ('object-array-stale-copy',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'use() { let c=C(1); let a=[c]; c.destroy(); let b=a }',
  'LYC330: Object reference may have been invalidated.'),
 ('object-array-helper-destroy',
  'class C { x:i32; func C(self,n:i32) { self.x=n }; func set(self,n:i32) { self.x=n } }; func '
  'kill(c:C) { c.destroy() }; func use() { let c=C(1); let a=[c]; kill(c); let n=a[0].x }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-final-self',
  'value class V { func V(self) {}; final func finish(self) { let a=[self] } }',
  'LYC334: A final hook may not transitively destroy its own class.')]


# Array releases participate in the existing finalization effects.
CASES += [('counted-array-shrink-effect',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=2 }; '
  'final func finish(self) { let d=D(); d.destroy() } }; func use() { let d=D(); var a=[V()]; '
  'a.truncate(0u64); let x=d.x }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-replace-effect',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=2 }; '
  'final func finish(self) { let d=D(); d.destroy() } }; func use() { let d=D(); var a=[V()]; '
  'a=[]; let x=d.x }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-scope-effect',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=2 }; '
  'final func finish(self) { let d=D(); d.destroy() } }; func use()->D { let d=D(); let a=[V()]; '
  'return d }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-temporary-effect',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=2 }; '
  'final func finish(self) { let d=D(); d.destroy() } }; func use(b:bool)->D { let d=D(); let '
  'result=b && [V()][0].x==2; return d }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-nested-shrink-effect',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=2 }; '
  'final func finish(self) { let d=D(); d.destroy() } }; func use()->D { let d=D(); var a=[[V()]]; '
  'a.resize(0u64); return d }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-return-transfer',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=2 }; '
  'final func finish(self) { let d=D(); d.destroy() } }; func make()->[V] { return [V()] }; func '
  'use() { let d=D(); let a=make(); let x=d.x }',
  None),
 ('counted-array-branch-transfer',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { x:i32; func V(self) { self.x=2 }; '
  'final func finish(self) { let d=D(); d.destroy() } }; func use(b:bool) { let d=D(); let a=if b '
  '{ [V()] } else { [V()] }; let x=d.x }',
  None),
 ('counted-array-empty-final',
  'value class V { func V(self) {}; final func finish(self) { let a:[V]=[] } }',
  None),
 ('counted-array-nested-empty-final',
  'value class V { func V(self) {}; final func finish(self) { let a:[[V]]=[[]] } }',
  None)]

CASES += [('counted-array-empty-then-push',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func '
  'finish(self) { let d=D(); d.destroy() } }; func use()->D { let d=D(); var a:[V]=[]; '
  'a.push(V()); return d }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-empty-then-helper',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func '
  'finish(self) { let d=D(); d.destroy() } }; func fill(a:ref [V]) { a.push(V()) }; func use()->D '
  '{ let d=D(); var a:[V]=[]; fill(ref a); return d }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-empty-then-nested-store',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func '
  'finish(self) { let d=D(); d.destroy() } }; func use()->D { let d=D(); var a:[[V]]=[[]]; '
  'a[0]=[V()]; return d }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-empty-control',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func '
  'finish(self) { let d=D(); d.destroy() } }; func use()->D { let d=D(); var a:[V]=[]; '
  'a.reserve(9u64); return d }',
  None)]

CASES += [('counted-array-length-temporary',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func '
  'finish(self) { let d=D(); d.destroy() } }; func use(b:bool)->D { let d=D(); let result=b && '
  'len([V()])==1u64; return d }',
  'LYC330: Object reference may have been invalidated.'),
 ('counted-array-capacity-temporary',
  'class D { x:i32; func D(self) { self.x=1 } }; value class V { func V(self) {}; final func '
  'finish(self) { let d=D(); d.destroy() } }; func use(b:bool)->D { let d=D(); let result=b && '
  'capacity([V()])==1u64; return d }',
  'LYC330: Object reference may have been invalidated.')]

CASES += [('object-array-whole-replacement',
  'class C { x:i32; func C(self,n:i32) { self.x=n } }; func use() { let c=C(1); var a=[c]; '
  'c.destroy(); a=[C(2)]; let x=a[0].x }',
  None),
 ('object-array-slot-reuse-not-revival',
  'class C { x:i32; func C(self,n:i32) { self.x=n } }; func use() { let c=C(1); let a=[c]; '
  'c.destroy(); let fresh=C(2); let x=a[0].x }',
  'LYC330: Object reference may have been invalidated.')]

def main():
    if len(sys.argv) < 2:
        raise SystemExit("usage: python3 graders/ch08/check.py COMPILER [ARGS...]")
    with tempfile.TemporaryDirectory(prefix="lyric-ch08-check-") as directory:
        path = pathlib.Path(directory) / "case.ly"
        failures = 0
        for name, source, expected in CASES:
            path.write_text(source + "\nfunc main() {}\n", encoding="utf-8")
            result = subprocess.run(sys.argv[1:] + ["--check", str(path)],
                                    capture_output=True, timeout=30)
            passed = (result.returncode == 0 and not result.stdout and not result.stderr
                      if expected is None else
                      result.returncode == 1 and not result.stdout
                      and expected.encode() in result.stderr)
            print(("PASS " if passed else "FAIL ") + name)
            if not passed:
                failures += 1
                print(repr((result.returncode, result.stdout, result.stderr)))
        print(f"{len(CASES)-failures}/{len(CASES)} Chapter 8 checking cases passed")
        return bool(failures)

if __name__ == "__main__":
    raise SystemExit(main())
