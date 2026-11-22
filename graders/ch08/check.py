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
