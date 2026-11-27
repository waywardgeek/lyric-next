#!/usr/bin/env python3
"""Retained Chapter 13 successors for all four Chapter 12 list refusals.

Run: python3 graders/ch13/list_refusals.py COMPILER [ARG ...]
Requires exit 1, empty stdout, and exact assertion location/message/call frames
at O0/O2. Guard-before-write ordering is inspected in the public LIST fixture;
a terminating assertion alone cannot expose post-failure heap state.
"""
import importlib.util
from pathlib import Path
import sys

from multi_class_migration import MAINTAINED_LIST, previous

# Import the old public fixture without changing it. Its module-level import
# expects its sibling multi_class; bind exactly the already loaded old module.
sys.modules["multi_class"] = previous
spec = importlib.util.spec_from_file_location(
    "chapter12_list_refusals", Path(__file__).resolve().parents[1] / "ch12" / "list_refusals.py")
old = importlib.util.module_from_spec(spec)
spec.loader.exec_module(old)

FAILURES = []
for name, original, location, message, frames in old.FAILURES:
    assert original.startswith(previous.LIST), name
    source = MAINTAINED_LIST + original[len(previous.LIST):]
    guard = "assert(child.parent == null)" if frames[0] == "append" else "assert(child.parent == self)"
    assert source.count(guard) == 1, name
    offset = source.index(guard)
    line = source.count("\n", 0, offset) + 1
    column = offset - source.rfind("\n", 0, offset)
    FAILURES.append((name + "-maintained", source, (line, column), message, frames))
assert len(FAILURES) == 4

if __name__ == "__main__":
    raise SystemExit(old.execution.main(
        cases=[], overflows=[], rejections=[], runtime_failures=FAILURES))
