#!/usr/bin/env python3
"""Retained safe-access audit: allocated capacity is not initialized length.

Supply COMPILER [ARG ...]. Invalid accesses run only in checked safe mode.
The public tuple harness enforces exact diagnostics and O0/O2 execution.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ch10"))
from tuples import main

CASES = [
    ("regrowth-initializes-before-access", """func main() {
        var items=[7,8]
        items.reserve(32u64)
        items.truncate(1u64)
        items.resize(2u64)
        assert(items[0]==7 && items[1]==0)
        print("ok")
    }""", b"ok"),
]
RUNTIME_CASES = [
    ("reserved-empty-is-not-readable", """func main() {
        var items:[i32]=[]
        items.reserve(32u64)
        let value=items[0]
    }""", None),
    ("truncated-tail-is-not-readable", """func main() {
        var items=[7,8]
        items.reserve(32u64)
        items.truncate(1u64)
        let value=items[1]
    }""", None),
    ("reserved-tail-is-not-writable", """func main() {
        var items=[7]
        items.reserve(32u64)
        items[1]=9
    }""", None),
]

if __name__ == "__main__":
    raise SystemExit(main(
        CASES, (), "safe-initialized-length",
        runtime_cases=RUNTIME_CASES,
        runtime_diagnostic=b"LYR006: array index out of bounds"))
