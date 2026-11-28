"""Retained compile-only call-admission boundary for the safety milestone.

Unknown effects must not silently become no effects. This endpoint supports
statically resolved bodies, not runtime function values or opaque declarations.
These refusals establish admission only, not foreign-call safety support.
"""
from safety_calls import main

CASES = [
    ("known-direct-body", """
        func update(value:ref i32) { value=7 }
        func use() { var value=1; update(ref value) }
    """, None),
    ("function-value-contract-unsupported", """
        func invoke(callback:fn(i32)->i32) {}
    """, "LYC306: Unsupported semantic construct: FunctionType."),
    ("opaque-body-missing", """
        func update(value:ref i32);
        func use() { var value=1; update(ref value) }
    """, "LYC306: Unsupported semantic construct: Function."),
]

if __name__ == "__main__":
    raise SystemExit(main(CASES, "call admission safety"))
