#!/usr/bin/env python3
"""Public retained Chapter 8 class cases; trailing args select the compiler.

The existing independent execution harness checks exact outputs at O0 and O2.
These first cases are positive construction/lifetime baselines, not full safety
coverage. Negative lifetime and layout checks will be added with implementation.
"""
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    'execution', Path(__file__).resolve().parents[1] / 'ch05' / 'run.py')
execution = importlib.util.module_from_spec(spec)
spec.loader.exec_module(execution)

CASES = [
    ('constructor-field', '''class Counter {
    count: i32
    func Counter(self, initial: i32) { self.count = initial }
}
func main() { let c = Counter(7); if c.count == 7 { print("ok") }; c.destroy() }
''', b'ok'),
    ('shared-identity', '''class Counter {
    count: i32
    func Counter(self, initial: i32) { self.count = initial }
}
func main() { let c = Counter(7); let other = c; other.count = 9; if c.count == 9 { print("ok") }; c.destroy() }
''', b'ok'),
    ('method', '''class Counter {
    count: i32
    func Counter(self, initial: i32) { self.count = initial }
    func increment(self) { self.count = self.count + 1 }
    func read(self) -> i32 { return self.count }
}
func main() { let c = Counter(7); c.increment(); if c.read() == 8 { print("ok") }; c.destroy() }
''', b'ok'),
    ('ordinary-final', '''class Counter {
    count: i32
    func Counter(self, initial: i32) { self.count = initial }
    final func report(self) { if self.count == 7 { print("final") } }
}
func main() { let c = Counter(7); c.destroy() }
''', b'final'),
    ('counted-scope-final', '''value class Counter {
    count: i32
    func Counter(self, initial: i32) { self.count = initial }
    final func report(self) { if self.count == 7 { print("final") } }
}
func use() { let c = Counter(7); if c.count == 7 { print("live") } }
func main() { use() }
''', b'livefinal'),
    ('counted-alias-final-once', '''value class Counter {
    count: i32
    func Counter(self, initial: i32) { self.count = initial }
    final func report(self) { if self.count == 9 { print("final") } }
}
func use() { let c = Counter(7); let other = c; other.count = 9; if c.count == 9 { print("shared") } }
func main() { use() }
''', b'sharedfinal'),
]

if __name__ == '__main__':
    raise SystemExit(execution.main(CASES, [], [], []))
