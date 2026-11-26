#!/usr/bin/env python3
"""Retained list precondition failures, checked at O0/O2 with exact diagnostics.

Run: python3 graders/ch12/list_refusals.py COMPILER [ARG ...]
Each source must compile, then exit 1 with empty stdout and the exact assertion
location and method/main stack. Guard-before-write ordering is separately visible
in LIST; a terminating assertion does not expose post-failure heap state.
"""
import importlib.util
from pathlib import Path
import sys

from multi_class import LIST

# Reuse the existing public execution harness without depending on a generic
# module name such as 'run' already being present in the importing process.
spec = importlib.util.spec_from_file_location(
    'chapter5_execution', Path(__file__).resolve().parents[1] / 'ch05' / 'run.py')
execution = importlib.util.module_from_spec(spec)
spec.loader.exec_module(execution)

FAILURES = []
for name, body, method, guard in [
    ('list-already-attached',
     'let p=Graph(); let n=Node(); p.nodes.append(n); p.nodes.append(n)',
     'append', 'assert(child.parent==null)'),
    ('list-other-parent-append',
     'let p=Graph(); let q=Graph(); let n=Node(); p.nodes.append(n); q.nodes.append(n)',
     'append', 'assert(child.parent==null)'),
    ('list-wrong-parent-remove',
     'let p=Graph(); let q=Graph(); let n=Node(); p.nodes.append(n); q.nodes.remove(n)',
     'remove', 'assert(child.parent==self)'),
    ('list-detached-remove',
     'let p=Graph(); let n=Node(); p.nodes.remove(n)',
     'remove', 'assert(child.parent==self)'),
]:
    source = LIST + 'func main() { ' + body + ' }\n'
    offset = source.index(guard)
    line = source.count('\n', 0, offset) + 1
    column = offset - source.rfind('\n', 0, offset)
    FAILURES.append((name, source, (line, column),
                     'LYR010: assertion failed', [method, 'main']))

if __name__ == '__main__':
    sys.exit(execution.main(cases=[], overflows=[], rejections=[],
                            runtime_failures=FAILURES))
