#!/usr/bin/env python3
"""Retained Chapter 7 array cases; trailing arguments are the compiler command.

Expected results are authored here, not obtained from a reference compiler.
The shared harness checks emission, C compilation, streams, exit and timeouts.
"""
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('execution', Path(__file__).resolve().parents[1] / 'ch05' / 'run.py')
execution = importlib.util.module_from_spec(spec)
spec.loader.exec_module(execution)

CASES = [
    ('length-u64', 'func count(a: [i32]) -> u64 { return len(a) }; func main() { let n: u64 = count([1, 2]); if n == 2u64 && len("ok") == 2u64 { print("ok") } }', b'ok'),
    ('integer-literal', 'func main() { let a = [10, 20, 30]; if len(a) == 3 && a[1] == 20 { print("ok") } }', b'ok'),
    ('empty-context', 'func main() { let a: [i32] = []; if len(a) == 0 { print("ok") } }', b'ok'),
    ('bool-elements', 'func main() { let a = [true, false]; if a[0] && !a[1] { print("ok") } }', b'ok'),
    ('byte-string-alias', 'func main() { let a: [u8] = "ok"; if len(a) == 2 && a[0] == 111u8 { print(a) } }', b'ok'),
    ('nested-literal', 'func main() { let a = [[1, 2], [3]]; if len(a) == 2 && len(a[0]) == 2 && a[1][0] == 3 { print("ok") } }', b'ok'),
    ('array-return', 'func values() -> [i32] { return [4, 5] }; func main() { let a = values(); if a[1] == 5 { print("ok") } }', b'ok'),
    ('copy-independent', 'func main() { let a = [1, 2]; var b = a; b[0] = 9; if a[0] == 1 && b[0] == 9 { print("ok") } }', b'ok'),
    ('nested-copy-independent', 'func main() { let a = [[1]]; var b = a; b[0][0] = 9; if a[0][0] == 1 && b[0][0] == 9 { print("ok") } }', b'ok'),
]
# Ownership crosses real calls and control flow, not just straight-line bindings.
CASES += [
    ('parameter-copy', 'func changed(a: [[i32]]) -> [[i32]] { var b = a; b[0][0] = 9; return b }; func main() { let a = [[1]]; let b = changed(a); if a[0][0] == 1 && b[0][0] == 9 { print("ok") } }', b'ok'),
    ('nested-replacement', 'func main() { let a = [[1], [2]]; var b = a; b[0] = [9, 8]; if a[0][0] == 1 && len(b[0]) == 2u64 && b[0][1] == 8 { print("ok") } }', b'ok'),
    ('self-assignment', 'func main() { var a = [[1]]; a = a; a[0] = a[0]; if a[0][0] == 1 { print("ok") } }', b'ok'),
    ('string-copy', 'func main() { let a = "ok"; var b = a; b[0] = 110u8; print(a); print(b) }', b'oknk'),
    ('conditional-array', 'func pick(b: bool) -> [[i32]] { let a: [[i32]] = if b { [[1]] } else { [[2]] }; return a }; func main() { if pick(true)[0][0] == 1 && pick(false)[0][0] == 2 { print("ok") } }', b'ok'),
    ('early-return-array', 'func pick(b: bool) -> [i32] { let a = [1]; if b { let other = [2]; return other }; return a }; func main() { if pick(true)[0] == 2 && pick(false)[0] == 1 { print("ok") } }', b'ok'),
    ('loop-array-exits', 'func main() { var i = 0; while i < 100 { let a = [[i]]; i += 1; if i < 99 { continue }; if a[0][0] == 99 { break } }; if i == 100 { print("ok") } }', b'ok'),
    ('short-circuit-array', 'func yes(a: [i32]) -> bool { return a[0] == 1 }; func main() { if false && yes([1]) { print("bad") }; if true && yes([1]) { print("ok") } }', b'ok'),
    ('array-element-order', 'func first() -> i32 { print("A"); return 1 }; func second() -> i32 { print("B"); return 2 }; func main() { let a = [first(), second()]; if a[0] == 1 && a[1] == 2 { print("ok") } }', b'ABok'),
]
# Growth preserves independent values and initializes only newly live slots.
CASES += [
    ('reserve-preserves-size', 'func main() { var a = [7]; a.reserve(100u64); a.reserve(0u64); if len(a) == 1u64 && a[0] == 7 { print("ok") } }', b'ok'),
    ('push-geometric-growth', 'func main() { var a: [i32] = []; var i = 0; while i < 100 { a.push(i); i += 1 }; if len(a) == 100u64 && a[0] == 0 && a[99] == 99 { print("ok") } }', b'ok'),
    ('resize-integer-default', 'func main() { var a = [7]; a.resize(3u64); if a[0] == 7 && a[1] == 0 && a[2] == 0 { print("ok") } }', b'ok'),
    ('resize-bool-default', 'func main() { var a = [true]; a.resize(2u64); if a[0] && !a[1] { print("ok") } }', b'ok'),
    ('truncate-regrow', 'func main() { var a = [7, 8, 9]; a.truncate(1u64); a.truncate(99u64); a.resize(3u64); if len(a) == 3u64 && a[0] == 7 && a[1] == 0 && a[2] == 0 { print("ok") } }', b'ok'),
    ('nested-default-regrow', 'func main() { var a = [[1], [2]]; a.resize(1u64); a.resize(3u64); if a[0][0] == 1 && len(a[1]) == 0u64 && len(a[2]) == 0u64 { print("ok") } }', b'ok'),
    ('nested-push-copy', 'func main() { var child = [1]; var a: [[i32]] = []; a.push(child); child[0] = 9; a[0].push(2); if a[0][0] == 1 && a[0][1] == 2 && child[0] == 9 { print("ok") } }', b'ok'),
    ('string-growth', 'func main() { var a = "o"; a.reserve(8u64); a.push(107u8); print(a); a.resize(0u64); if len(a) == 0u64 { print("ok") } }', b'okok'),
]
CASES += [
    ('descriptor-stable-during-argument', 'func main() { var a = [1]; a.push(if true { a.resize(2u64); 3 } else { 4 }); if len(a) == 3u64 && a[0] == 1 && a[1] == 0 && a[2] == 3 { print("ok") } }', b'ok'),
    ('push-self-value', 'func main() { var a = [[1]]; a.push(a[0]); a[0][0] = 9; if a[1][0] == 1 { print("ok") } }', b'ok'),
]
# Fixed storage aliases are nonowning; ordinary reads still copy values.
CASES += [
    ('ref-scalar-parameter', 'func increment(x: ref i32) { x += 1 }; func main() { var n = 1; increment(ref n); if n == 2 { print("ok") } }', b'ok'),
    ('ref-fixed-alias', 'func main() { var n = 1; let slot = ref n; slot = 2; let snapshot = slot; let alias = ref slot; alias += 1; if n == 3 && snapshot == 2 { print("ok") } }', b'ok'),
    ('ref-forward-helper', 'func increment(x: ref i32) { x += 1 }; func forward(x: ref i32) { increment(ref x) }; func main() { var n = 1; let slot = ref n; forward(ref slot); if n == 2 { print("ok") } }', b'ok'),
    ('ref-array-growth', 'func grow(a: ref [i32]) { a.push(2) }; func main() { var a = [1]; grow(ref a); if len(a) == 2u64 && a[1] == 2 { print("ok") } }', b'ok'),
    ('ref-array-value-copy', 'func snapshot(a: ref [[i32]]) -> [[i32]] { return a }; func main() { var a = [[1]]; var b = snapshot(ref a); b[0][0] = 9; if a[0][0] == 1 && b[0][0] == 9 { print("ok") } }', b'ok'),
    ('ref-descriptor-survives-growth', 'func main() { var a = [1]; let alias = ref a; alias.push(2); a.push(3); if len(alias) == 3u64 && alias[2] == 3 { print("ok") } }', b'ok'),
    ('ref-element-last-use', 'func main() { var a = [1]; let slot = ref a[0]; slot = 7; a.push(2); if a[0] == 7 && a[1] == 2 { print("ok") } }', b'ok'),
    ('ref-nonexclusive-scalar', 'func write(x: ref i32, y: ref i32) { x = 7; y += 1 }; func main() { var a = [1]; write(ref a[0], ref a[0]); if a[0] == 8 { print("ok") } }', b'ok'),
    ('ref-nested-last-use', 'func main() { var a = [[1]]; let slot = ref a[0][0]; slot = 7; let saved = slot; a[0] = [2]; if saved == 7 && a[0][0] == 2 { print("ok") } }', b'ok'),
]
CASES += [
    ('ref-scalar-overlap', 'func write(x: ref i32, y: ref i32) { x = 7; y += 1 }; func main() { var n = 1; write(ref n, ref n); if n == 8 { print("ok") } }', b'ok'),
    ('ref-scalar-value-return', 'func snapshot(x: ref i32) -> i32 { return x }; func main() { var n = 1; let saved = snapshot(ref n); n = 9; if saved == 1 && n == 9 { print("ok") } }', b'ok'),
    ('ref-scalar-annotated', 'func main() { var n = 1; let slot: ref i32 = ref n; slot += 2; if n == 3 { print("ok") } }', b'ok'),
]
CASES += [
    ('ref-conditional-preview', 'func main() { let n = if true { var x = 1; let slot: ref i32 = ref x; slot += 1; slot } else { 0 }; if n == 2 { print("ok") } }', b'ok'),
]
CASES += [
    ('borrow-loop-fresh', 'func main() { var a = [1]; var i = 0; while i < 3 { let slot = ref a[0]; slot += 1; a.push(2); i += 1 }; if a[0] == 4 { print("ok") } }', b'ok'),
    ('borrow-loop-break', 'func main() { var a = [1]; let slot = ref a[0]; while true { slot = 7; a.push(2); break }; if a[0] == 7 { print("ok") } }', b'ok'),
    ('borrow-reacquire', 'func main() { var a = [1]; let old = ref a[0]; old = 7; a.push(2); let fresh = ref a[0]; fresh += 1; if a[0] == 8 { print("ok") } }', b'ok'),
    ('borrow-returning-arm', 'func run(flag: bool) { var a = [1]; let slot = ref a[0]; if flag { a.push(2); return }; slot = 7; if a[0] == 7 { print("ok") } }; func main() { run(false) }', b'ok'),
    ('borrow-descriptor-replacement', 'func main() { var a = [1]; let alias = ref a; a = [2, 3]; alias.push(4); if len(a) == 3u64 && alias[2] == 4 { print("ok") } }', b'ok'),
    ('borrow-child-descriptor-growth', 'func main() { var a = [[1]]; let child = ref a[0]; child.push(2); if child[1] == 2 && a[0][0] == 1 { print("ok") } }', b'ok'),
]
CASES += [
    ('borrow-helper-use-before-growth', 'func change(x: ref i32, a: ref [i32]) { x = 7; a.push(2) }; func main() { var a = [1]; change(ref a[0], ref a); if a[0] == 7 && a[1] == 2 { print("ok") } }', b'ok'),
    ('borrow-helper-distinct-owners', 'func change(x: ref i32, a: ref [i32]) { a.push(2); x = 7 }; func main() { var a = [1]; var b = [3]; change(ref a[0], ref b); if a[0] == 7 && b[1] == 2 { print("ok") } }', b'ok'),
    ('borrow-helper-forward-growth', 'func grow(a: ref [i32]) { a.push(2) }; func forward(a: ref [i32]) { grow(ref a) }; func main() { var a = [1]; forward(ref a); if a[1] == 2 { print("ok") } }', b'ok'),
    ('borrow-helper-recursive-growth', 'func grow(a: ref [i32], n: i32) { if n > 0 { grow(ref a, n - 1); a.push(n) } }; func main() { var a: [i32] = []; grow(ref a, 3); if len(a) == 3u64 && a[2] == 3 { print("ok") } }', b'ok'),
]
# Ordered summaries must survive forwarding and recursive call cycles.
CASES += [
    ('borrow-forward-safe-order', 'func change(x: ref i32, a: ref [i32]) { x = 7; a.push(2) }; func forward(x: ref i32, a: ref [i32]) { change(ref x, ref a) }; func main() { var a = [1]; forward(ref a[0], ref a); if a[0] == 7 && a[1] == 2 { print("ok") } }', b'ok'),
    ('borrow-helper-fresh-after-growth', 'func grow(a: ref [i32]) { a.push(2) }; func change(a: ref [i32]) { grow(ref a); let slot = ref a[0]; slot = 7 }; func main() { var a = [1]; change(ref a); if a[0] == 7 { print("ok") } }', b'ok'),
    ('borrow-mutual-recursion', 'func first(a: ref [i32], n: i32) { if n > 0 { second(ref a, n - 1) } else { a.push(2) } }; func second(a: ref [i32], n: i32) { first(ref a, n) }; func main() { var a = [1]; second(ref a, 2); if a[1] == 2 { print("ok") } }', b'ok'),
]
# These failures require lifetime analysis, not merely rejection of ref syntax.
# A fresh acquisition after growth is legal; using an old alias is not.
BORROW_REJECTIONS = [
    ('borrow-mutual-invalidation', 'func first(a: ref [i32], n: i32) { if n > 0 { second(ref a, n - 1) } else { a.push(2) } }; func second(a: ref [i32], n: i32) { first(ref a, n) }; func main() { var a = [1]; let slot = ref a[0]; second(ref a, 2); slot = 7 }', 'slot = 7'),
    ('borrow-helper-later-argument', 'func grow(a: ref [i32]) -> i32 { a.push(2); return 7 }; func write(x: ref i32, n: i32) { x = n }; func main() { var a = [1]; write(ref a[0], grow(ref a)) }', 'ref a[0]'),
    ('borrow-helper-local-stale', 'func change(a: ref [i32]) { let slot = ref a[0]; a.push(2); slot = 7 }; func main() { var a = [1]; change(ref a) }', 'slot = 7'),
    ('borrow-helper-forward-invalidation', 'func grow(a: ref [i32]) { a.push(2) }; func forward(a: ref [i32]) { grow(ref a) }; func main() { var a = [1]; let slot = ref a[0]; forward(ref a); slot = 7 }', 'slot = 7'),
    ('borrow-helper-forward-overlap', 'func change(x: ref i32, a: ref [i32]) { a.push(2); x = 7 }; func forward(x: ref i32, a: ref [i32]) { change(ref x, ref a) }; func main() { var a = [1]; forward(ref a[0], ref a) }', 'forward(ref a[0]'),
    ('borrow-helper-recursive-invalidation', 'func grow(a: ref [i32], n: i32) { if n > 0 { grow(ref a, n - 1); a.push(n) } }; func main() { var a = [1]; let slot = ref a[0]; grow(ref a, 2); slot = 7 }', 'slot = 7'),
    ('borrow-helper-nested-invalidation', 'func grow(a: ref [i32]) { a.push(2) }; func main() { var a = [[1]]; let slot = ref a[0][0]; grow(ref a[0]); slot = 7 }', 'slot = 7'),
    ('borrow-alias-mutation', 'func main() { var a = [1]; let owner = ref a; let slot = ref a[0]; owner.push(2); slot = 7 }', 'slot = 7'),
    ('borrow-child-descriptor-moved', 'func main() { var a = [[1]]; let child = ref a[0]; a.push([2]); child.push(3) }', 'child.push'),
    ('borrow-call-argument-effects', 'func write(x: ref i32, n: i32) { x = n }; func main() { var a = [1]; write(ref a[0], if true { a.push(2); 7 } else { 8 }) }', 'ref a[0]'),
    ('borrow-loop-continue', 'func main() { var a = [1]; let slot = ref a[0]; var i = 0; while i < 2 { slot = 7; a.push(2); i += 1; continue } }', 'slot = 7'),
    ('borrow-after-push', 'func main() { var a = [1]; let slot = ref a[0]; a.push(2); slot = 7 }', 'slot = 7'),
    ('borrow-after-replacement', 'func main() { var a = [[1]]; let slot = ref a[0][0]; a[0] = [2]; slot = 7 }', 'slot = 7'),
    ('borrow-derived-after-growth', 'func main() { var a = [1]; let slot = ref a[0]; let alias = ref slot; a.resize(2u64); alias = 7 }', 'alias = 7'),
    ('borrow-branch-invalidation', 'func main() { var a = [1]; let slot = ref a[0]; if true { a.push(2) }; slot = 7 }', 'slot = 7'),
    ('borrow-loop-backedge', 'func main() { var a = [1]; let slot = ref a[0]; var i = 0; while i < 2 { slot = 7; a.push(2); i += 1 } }', 'slot = 7'),
    ('borrow-helper-invalidation', 'func grow(a: ref [i32]) { a.push(2) }; func main() { var a = [1]; let slot = ref a[0]; grow(ref a); slot = 7 }', 'slot = 7'),
    ('borrow-helper-overlap', 'func change(x: ref i32, a: ref [i32]) { a.push(2); x = 7 }; func main() { var a = [1]; change(ref a[0], ref a) }', 'change(ref'),
]
REJECTIONS = []
for name, source, anchor in BORROW_REJECTIONS:
    REJECTIONS.append((name, source, source.index(anchor) + 1,
                       'LYC326: Borrowed storage may have been invalidated.'))
for name, source, anchor, diagnostic in [
    ('ref-missing-marker', 'func change(x: ref i32) { x = 2 }; func main() { var n = 1; change(n) }', 'n) }', 'LYC325: Reference argument requires explicit ref storage.'),
    ('ref-immutable-storage', 'func main() { let n = 1; let slot = ref n }', 'n }', "LYC308: Cannot modify immutable binding 'n'."),
    ('ref-uninitialized-storage', 'func main() { var n: i32; let slot = ref n }', 'n }', "LYC307: Local 'n' is not definitely assigned."),
    ('ref-no-widening', 'func change(x: ref i64) { x = 2i64 }; func main() { var n = 1; change(ref n) }', 'ref n', 'LYC325: Reference storage type must match exactly.'),
    ('ref-no-var-alias', 'func main() { var n = 1; var slot = ref n }', 'slot', 'LYC325: A reference local requires let and a reference initializer.'),
    ('length-no-narrow', 'func main() { let n: i32 = len([1]) }', 'len', 'LYC304: Expected i32, got u64.'),
    ('immutable-element', 'func main() { let a = [1]; a[0] = 2 }', 'a[0]', "LYC308: Cannot modify immutable binding 'a'."),
    ('immutable-nested', 'func main() { let a = [[1]]; a[0][0] = 2 }', 'a[0]', "LYC308: Cannot modify immutable binding 'a'."),
    ('noninteger-index', 'func main() { let a = [1]; let b = a[true] }', 'true', 'LYC322: Array index must be an integer.'),
    ('immutable-growth', 'func main() { let a = [1]; a.push(2) }', 'a.push', "LYC308: Cannot modify immutable binding 'a'."),
    ('signed-growth-count', 'func main() { var a = [1]; a.resize(2i32) }', '2i32', 'LYC304: Expected u64, got i32.'),
    ('nested-receiver-invalidation', 'func main() { var a = [[1]]; a[0].push(if true { a.resize(0u64); 2 } else { 3 }) }', 'a.resize', 'LYC324: Cannot modify an array while its storage is selected.'),
    ('indexed-store-invalidation', 'func main() { var a = [1]; a[0] = if true { a.resize(0u64); 2 } else { 3 } }', 'a.resize', 'LYC324: Cannot modify an array while its storage is selected.'),
    ('nested-receiver-replacement', 'func main() { var a = [[1]]; a[0].push(if true { a = []; 2 } else { 3 }) }', 'a = []', 'LYC324: Cannot modify an array while its storage is selected.'),
    ('scalar-length', 'func main() { len(1) }', '1', 'LYC323: Expected an array argument.'),
]:
    REJECTIONS.append((name, source, source.index(anchor) + 1, diagnostic))
RUNTIME_FAILURES = []
for name, index in [('negative-index', '-1'), ('past-end-index', '2')]:
    source = f'func read(i: i32) {{ let a = [1, 2]; a[i] }}; func main() {{ read({index}) }}'
    # Index nodes are structural: use the existing first-anchored-child location.
    column = source.index('a[i]') + 1
    RUNTIME_FAILURES.append((name, source, column, 'LYR006: array index out of bounds', ['read', 'main']))

source = 'func grow() { var a: [u64] = []; a.reserve(18446744073709551615u64) }; func main() { grow() }'
RUNTIME_FAILURES.append(('allocation-size-overflow', source, source.index('reserve') + 1,
                         'LYR007: array allocation size overflow', ['grow', 'main']))

# Preview must preserve an alias's referent width before destination widening.
# Redundant reference annotations must not change checked arithmetic semantics.
for storage_kind, setup, selected in [
    ('scalar', 'var x = 255u8', 'x'),
    ('element', 'var a = [255u8]', 'a[0]'),
]:
    for alias_kind, annotation in [('inferred', ''), ('annotated', ': ref u8')]:
        for destination in ['i64', 'u16']:
            source = (
                f'func main() {{ {setup}; let y: {destination} = '
                f'(if true {{ let r{annotation} = ref {selected}; r }} '
                'else { 0u8 }) + 1 }'
            )
            name = f'alias-preview-overflow-{storage_kind}-{alias_kind}-{destination}'
            RUNTIME_FAILURES.append((
                name, source, source.index('+') + 1,
                'LYR001: integer overflow in u8 addition', ['main'],
            ))
        source = (
            f'func main() {{ {setup.replace("255u8", "1u8")}; let y: u16 = '
            f'(if true {{ let r{annotation} = ref {selected}; r }} '
            'else { 0u8 }) + 1; if y == 2u16 { print("ok") } }'
        )
        CASES.append((f'alias-preview-widen-{storage_kind}-{alias_kind}', source, b'ok'))

# Capacity is observed on the selected descriptor, not an independent copy.
CASES += [
    ('capacity-reserved-empty', 'func main() { var a: [i32] = []; a.reserve(100u64); assert(len(a) == 0u64 && capacity(a) >= 100u64); print("ok") }', b'ok'),
    ('capacity-copy-tight', 'func main() { var a = [1, 2]; a.reserve(100u64); let b = a; assert(capacity(a) >= 100u64 && capacity(b) == 2u64); print("ok") }', b'ok'),
    ('capacity-empty-copy', 'func main() { var a: [i32] = []; a.reserve(100u64); let b = a; assert(capacity(b) == 0u64 && capacity(a) >= 100u64); print("ok") }', b'ok'),
    ('capacity-resize-zero', 'func main() { var a = [1]; a.reserve(100u64); a.resize(0u64); assert(len(a) == 0u64 && capacity(a) == 0u64); print("ok") }', b'ok'),
    ('capacity-empty-release', 'func main() { var a: [i32] = []; a.reserve(100u64); a.resize(0u64); a.push(7); assert(len(a) == 1u64 && a[0] == 7 && capacity(a) < 100u64); print("ok") }', b'ok'),
    ('capacity-truncate-retains', 'func main() { var a = [1]; a.reserve(100u64); a.truncate(0u64); assert(len(a) == 0u64 && capacity(a) >= 100u64); print("ok") }', b'ok'),
    ('capacity-ref-alias', 'func main() { var a = [1]; let r = ref a; r.reserve(100u64); assert(capacity((r)) >= 100u64); print("ok") }', b'ok'),
    ('capacity-nested', 'func main() { var a = [[1]]; a[0].reserve(100u64); assert(capacity(a[0]) >= 100u64); print("ok") }', b'ok'),
    ('capacity-value-parameter', 'func size(a: [i32]) -> u64 { return capacity(a) }; func main() { var a = [1]; a.reserve(100u64); assert(size(a) == 1u64 && capacity(a) >= 100u64); print("ok") }', b'ok'),
]

# A later index must not invalidate an already-selected nested descriptor.
capacity_selection = 'func change(a: ref [[[i32]]]) -> i32 { a.resize(0u64); return 0 }; func main() { var a = [[[1]]]; let n = capacity(a[0][change(ref a)]) }'
REJECTIONS.append(('capacity-selection-invalidation', capacity_selection,
                   capacity_selection.index('a[0][change') + 1,
                   'LYC326: Borrowed storage may have been invalidated.'))

if __name__ == '__main__':
    raise SystemExit(execution.main(CASES, [], REJECTIONS, RUNTIME_FAILURES))
