#!/usr/bin/env python3
"""Retained Chapter 11 generic-function vertical-slice checks.

Run from the repository root:
  python3 graders/ch11/generics.py COMPILER [ARG ...]

Reuse the public Chapter 10 execution contract: C emission succeeds with empty
stderr, generated C compiles and executes at O0/O2, and stdout matches exactly.
A negative case requires exit 1, empty stdout, and the specified located error.
All named cases are retained. This initial slice is not full Chapter 11 coverage.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ch10'))
from tuples import main

CASES = [
    ('generic-explicit-two-types', '''func identity<T>(value:T)->T { return value }
func main() {
    assert(identity<i32>(7)==7)
    assert(identity<bool>(true))
    assert(identity<i32>(9)==9)
    print("ok")
}
''', b'ok'),
    # Checking relay must discover identity's concrete signature and body.
    ('generic-discovers-generic', '''func identity<T>(value:T)->T { return value }
func relay<U>(value:U)->U { return identity<U>(value) }
func main() { assert(relay<i32>(11)==11); print("ok") }
''', b'ok'),
    # A template is checked for operations only when instantiated. Parsing is
    # still mandatory, and this does not waive declaration-contract validation.
    ('generic-unused-dependent-body', '''func addOne<T>(value:T)->T { return value+1 }
func main() { print("ok") }
''', b'ok'),
]

CASES += [
    ('generic-recursive-reuse', '''func countdown<T>(value:T)->T {
    if value==0 { return value }
    return countdown<T>(value-1)
}
func main() { assert(countdown<i32>(4)==0); print("ok") }
''', b'ok'),
    ('generic-aggregate-independent-copy', '''func identity<T>(value:T)->T { return value }
func main() {
    var original=[1,2]
    var copied=identity<[i32]>(original)
    copied[0]=9
    assert(original[0]==1)
    assert(copied[0]==9)
    let pair=identity<(i32,bool)>((3,true))
    assert(pair.0==3)
    assert(pair.1)
    print("ok")
}
''', b'ok'),
    ('generic-field-default-discovery', '''func identity<T>(value:T)->T { return value }
struct Box { value:i32=identity<i32>(7) }
func main() { let box=Box{}; assert(box.value==7); print("ok") }
''', b'ok'),
]

# Inference matches actual argument types, never the result context.
CASES += [
    ('generic-inferred-scalars', '''func identity<T>(value:T)->T { return value }
func main() { let n:i32=7; assert(identity(n)==7); assert(identity(true)); print("ok") }
''', b'ok'),
    ('generic-inferred-composites', '''func first<T>(values:[T])->T { return values[0] }
func unwrap<T>(value:(T,))->T { return value.0 }
func left<T,U>(value:(T,U))->T { return value.0 }
func main() {
    assert(first([3,4])==3)
    assert(unwrap((true,)))
    assert(left((8,false))==8)
    print("ok")
}
''', b'ok'),
    ('generic-inferred-repeated-binding', '''func choose<T>(a:T,b:T)->T { return a }
func main() { assert(choose(3,4)==3); print("ok") }
''', b'ok'),
    ('generic-inferred-nested-discovery', '''func identity<T>(value:T)->T { return value }
func relay<U>(value:U)->U { return identity(value) }
func main() { assert(relay(11)==11); print("ok") }
''', b'ok'),
]

CASES += [
    ('generic-inferred-argument-order', '''func mark(value:i32)->i32 { if value==1 { print("1") } else { print("2") }; return value }
func choose<T>(a:T,b:T)->T { return a }
func main() { assert(choose(mark(1),mark(2))==1) }
''', b'12'),
    ('generic-inferred-reference', '''func replace<T>(target:ref T,value:T) { target=value }
func main() { var n=1; replace(ref n,9); assert(n==9); print("ok") }
''', b'ok'),
    ('generic-inferred-nested-shape', '''func extract<T>(value:([T],(T,)))->T { return value.1.0 }
func main() { assert(extract(([1,2],(3,)))==3); print("ok") }
''', b'ok'),
    ('generic-inferred-grouped-type', '''func identity<T>(value:(T))->T { return value }
func main() { assert(identity(3)==3); print("ok") }
''', b'ok'),
    ('generic-explicit-conversion', '''func choose<T>(a:T,b:T)->T { return a }
func main() { let small:i32=3; let wide:i64=4; assert(choose<i64>(small,wide)==3); print("ok") }
''', b'ok'),
]

NEGATIVE_CASES = [
    ('generic-unknown-explicit-name', 'func main() { absent<i32>(7) }\n', 'LYC301'),
    ('generic-inferred-no-widening-negotiation', '''func choose<T>(a:T,b:T)->T { return a }
func main() { let small:i32=3; let wide:i64=4; let value=choose(small,wide) }
''', 'LYC304'),
    ('generic-inferred-nested-conflict', '''func extract<T>(value:([T],(T,)))->T { return value.1.0 }
func main() { let value=extract(([1,2],(true,))) }
''', 'LYC304'),
    ('generic-inferred-reference-explicit', '''func replace<T>(target:ref T,value:T) { target=value }
func main() { var n=1; replace(n,9) }
''', 'LYC325'),
    ('generic-inferred-reference-writable', '''func replace<T>(target:ref T,value:T) { target=value }
func main() { let n=1; replace(ref n,9) }
''', 'LYC308'),
    ('generic-inferred-conflict', '''func choose<T>(a:T,b:T)->T { return a }
func main() { let value=choose(1,true) }
''', 'LYC304'),
    ('generic-inferred-wrong-shape', '''func unwrap<T>(value:(T,))->T { return value.0 }
func main() { let value=unwrap(1) }
''', 'LYC304'),
    ('generic-inferred-missing-binding', '''func make<T>(value:i32)->T { return value }
func main() { let value:i32=make(1) }
''', 'LYC303'),
    ('generic-selected-body-type-error', '''func wrong<T>(value:T)->T { return false }
func main() { let result=wrong<i32>(1) }
''', 'LYC304'),
]

# A breadth case above the former proposed total cap: these requests are
# independent roots, not one deep discovery chain. Unique nominal declarations
# provide distinct canonical keys without constructing deeply nested types.
BROAD_ROOT_COUNT = 1025
broad_declarations = "\n".join(
    f"struct Kind{index} {{ value:i32 }}" for index in range(BROAD_ROOT_COUNT))
broad_calls = "\n".join(
    f"touch<Kind{index}>()" for index in range(BROAD_ROOT_COUNT))
CASES.append(('generic-independent-roots-not-total-cap',
    broad_declarations + "\nfunc touch<T>() {}\nfunc main() {\n"
    + broad_calls + '\nprint("ok")\n}\n', b'ok'))

# Roots have depth zero. Depth 1024 is allowed; reusing an existing key
# there must not be mistaken for creating a new specialization at depth 1025.
MAX_DISCOVERY_DEPTH = 1024
chain_declarations = "\n".join(
    f"func step{index}<T>() {{ step{index + 1}<T>() }}"
    for index in range(MAX_DISCOVERY_DEPTH))
chain_declarations += (
    f"\nfunc step{MAX_DISCOVERY_DEPTH}<T>() {{ if false {{ step0<T>() }} }}\n")
CASES.append(('generic-discovery-depth-boundary-reuse', chain_declarations
    + 'func main() { step0<i32>(); print("ok") }\n', b'ok'))

# This is rejected while checking, never emitted/executed as infinite recursion.
NEGATIVE_CASES.append(('generic-growing-discovery-depth',
    'func grow<T>() { grow<[T]>() }\nfunc main() { grow<i32>() }\n', 'LYC344'))

if __name__ == '__main__':
    sys.exit(main(CASES, NEGATIVE_CASES, 'generic'))
