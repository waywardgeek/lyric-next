#!/usr/bin/env python3
"""Retained Chapter 4 source checks. Supply the compiler command after this file."""
import json
import pathlib
import subprocess
import sys
import tempfile

# Independent source expectations, never obtained from compiler output.
# Deep left-associated trees are permitted by the parser. These cases guard
# subsequent semantic passes, independently of parser recursion limits.
CASES = [
    ('assert-bool', 'func main() { assert(true); assert(false) }', None),
    ('assert-type', 'func main() { assert(1) }', ('LYC304', 'Expected bool, got i32.')),
    ('assert-arity', 'func main() { assert() }', ('LYC303', "Function 'assert' expects 1 argument(s), got 0.")),
    ('long-context', 'func main() { let total: u8 = ' + ' + '.join(['0'] * 4000) + ' }', None),
    ('long-neighbor', 'func main() { let total = ' + ' + '.join(['0'] * 3999 + ['0u8']) + ' }', None),
    ('branch-return-else', 'func main() { let x: string; if true { x = "A" } else { return }; print(x) }', None),
    ('branch-isolation', 'func main() { let x: string; if true { x = "A" } else { print(x) } }', ('LYC307', "Local 'x' is not definitely assigned.")),
    ('branch-nested', 'func main() { let x: string; if true { if false { x = "A" } else { x = "B" } } else { x = "C" }; print(x) }', None),
    ('branch-mutable', 'func main() { var x: string; if true { x = "A" }; x = "B"; print(x) }', None),
    ('branch-value-unsupported', 'func main() { let x = if true { "A" } else { "B" } }', ('LYC306', 'Unsupported semantic construct: If.')),
    ('branch-unreachable-unsupported', 'func main() { if true { return } else { return }; print("A") }', ('LYC306', 'Unsupported semantic construct: ExpressionStatement.')),
    ('branch-both', 'func main() { let x: string; if true { x = "A" } else { x = "B" }; println(x) }', None),
    ('branch-one', 'func main() { let x: string; if true { x = "A" }; println(x) }', ('LYC307', "Local 'x' is not definitely assigned.")),
    ('branch-possible', 'func main() { let x: string; if true { x = "A" }; x = "B" }', ('LYC308', "Cannot assign immutable binding 'x' again.")),
    ('branch-return-path', 'func main() { let x: string; if true { return } else { x = "B" }; println(x) }', None),
    ('branch-return-assigned', 'func main() { let x: string; if true { x = "A"; return }; x = "B"; println(x) }', None),
    ('branch-return-both', 'func value(flag: bool) -> u8 { if flag { return 1 } else { return 2 } }\nfunc main() {}', None),
    ('branch-return-missing', 'func value(flag: bool) -> u8 { if flag { return 1 } }\nfunc main() {}', ('LYC309', "Function 'value' may finish without returning u8.")),
    ('branch-siblings', 'func main() { if true { let x = "A"; print(x) } else { let x = "B"; print(x) } }', None),
    ('branch-escape', 'func main() { if true { let x = "A" }; print(x) }', ('LYC301', "Unknown name 'x'.")),
    ('branch-shadow', 'func main() { let x = "A"; if true { let x = "B" } }', ('LYC302', "Name 'x' is already visible.")),
    ('branch-condition', 'func main() { if 1 { print("A") } }', ('LYC304', 'Expected bool, got i32.')),
    ('branch-else-if', 'func value(a: bool, b: bool) -> u8 { if a { return 1 } else if b { return 2 } else { return 3 } }\nfunc main() {}', None),
    ("arithmetic-default", 'func value() -> i32 { let x = 1 + 2; return x }\nfunc main() {}', None),
    ("arithmetic-neighbor-before-return", 'func value(x: u8) -> i64 { return x + 256 }\nfunc main() {}', ("LYC310", "Integer literal is outside u8 range.")),
    ("arithmetic-default-overflow", 'func main() { let x = 2147483647 + 1 }', ("LYC311", "Integer constant operation is outside i32 range.")),
    ("arithmetic-return-context", 'func value() -> u8 { return 1 + 2 * 3 }\nfunc main() {}', None),
    ("arithmetic-left-context", 'func value(x: u8) -> u8 { return x + 1 }\nfunc main() {}', None),
    ("arithmetic-right-context", 'func value(x: u8) -> u8 { return 1 + x }\nfunc main() {}', None),
    ("arithmetic-nested-context", 'func value(x: u8) -> u8 { return (1 + 2) * x }\nfunc main() {}', None),
    ("arithmetic-binding-context", 'func main() { let x: u8 = 1 + 2 }', None),
    ("arithmetic-argument-context", 'func take(x: u8) {}\nfunc main() { take(1 + 2) }', None),
    ("arithmetic-widen", 'func value(a: i64, b: u32) -> i64 { return a + b }\nfunc main() {}', None),
    ("arithmetic-subtract", 'func value() -> i8 { return 2i8 - 3i8 }\nfunc main() {}', None),
    ("arithmetic-overflow", 'func main() { let x = 127i8 + 1i8 }', ("LYC311", "Integer constant operation is outside i8 range.")),
    ("arithmetic-no-rescue", 'func value() -> i64 { return 127i8 + 1i8 }\nfunc main() {}', ("LYC311", "Integer constant operation is outside i8 range.")),
    ("arithmetic-nested-overflow", 'func main() { let x = (100i8 + 20i8) * 2i8 }', ("LYC311", "Integer constant operation is outside i8 range.")),
    ("arithmetic-incompatible", 'func value(a: i32, b: u32) { let x = a + b }\nfunc main() {}', ("LYC312", "No integer arithmetic type contains both i32 and u32.")),
    ("arithmetic-neighbor-overflow", 'func value(x: u8) { let y = x + 256 }\nfunc main() {}', ("LYC310", "Integer literal is outside u8 range.")),
    ("arithmetic-noninteger", 'func main() { let x = true + false }', ("LYC312", "Integer arithmetic requires integer operands.")),
    ("local-inferred", 'func main() { let message = "Hi"; println(message) }', None),
    ("local-late-let", 'func main() { let message: string; message = "Hi"; println(message) }', None),
    ("local-var", 'func main() { var message = "A"; message = "B"; println(message) }', None),
    ("value-return", 'func echo(text: string) -> string { return text }\nfunc main() { println(echo("Hi")) }', None),
    ("local-unassigned", 'func main() { let message: string; println(message) }', ("LYC307", "Local 'message' is not definitely assigned.")),
    ("local-reassign", 'func main() { let message = "A"; message = "B" }', ("LYC308", "Cannot assign immutable binding 'message' again.")),
    ("parameter-reassign", 'func echo(text: string) { text = "B" }\nfunc main() {}', ("LYC308", "Cannot assign immutable binding 'text' again.")),
    ("local-mismatch", 'func main() { let message: string = true }', ("LYC304", "Expected [u8], got bool.")),
    ("return-mismatch", 'func value() -> string { return true }\nfunc main() {}', ("LYC304", "Expected [u8], got bool.")),
    ("return-missing", 'func value() -> string {}\nfunc main() {}', ("LYC309", "Function 'value' may finish without returning [u8].")),
    ("return-bare", 'func value() -> string { return }\nfunc main() {}', ("LYC304", "Expected [u8], got unit.")),
    ("local-forward", 'func main() { println(message); let message = "Hi" }', ("LYC301", "Unknown name 'message'.")),
    ("local-shadow", 'func main() { let print = "Hi" }', ("LYC302", "Name 'print' is already visible.")),
    ("integer-default", 'func value() -> i32 { return 42 }\nfunc main() { let x = value() }', None),
    ("integer-context", 'func byte_value(x: u8) -> u8 { return x }\nfunc main() { let small: u8 = 255; let copy = byte_value(12) }', None),
    ("integer-late-context", 'func main() { let small: u8; small = 255 }', None),
    ("integer-minimum", 'func main() { let small = -128i8; let big = -9223372036854775808i64 }', None),
    ("integer-maximum", 'func main() { let big = 18_446_744_073_709_551_615u64 }', None),
    ("integer-widen", 'func main() { let small = 12u8; let big: i64 = small }', None),
    ("integer-too-large", 'func main() { let big = 2147483648 }', ("LYC310", "Integer literal is outside i32 range.")),
    ("integer-context-overflow", 'func main() { let small: u8 = 256 }', ("LYC310", "Integer literal is outside u8 range.")),
    ("integer-positive-minimum", 'func main() { let small = 128i8 }', ("LYC310", "Integer literal is outside i8 range.")),
    ("integer-below-minimum", 'func main() { let small = -129i8 }', ("LYC310", "Integer literal is outside i8 range.")),
    ("integer-negative-unsigned", 'func main() { let small: u8 = -1 }', ("LYC310", "Integer literal is outside u8 range.")),
    ("integer-no-narrow", 'func main() { let wide = 12; let small: u8 = wide }', ("LYC304", "Expected u8, got i32.")),
    ("integer-suffix-fixed", 'func main() { let small: u8 = 12i32 }', ("LYC304", "Expected u8, got i32.")),
    ("hello", 'func main() { println("Hello, world!") }', None),
    ("forward", 'func main() { greet() }\nfunc greet() { print("Hi") }', None),
    ("empty", 'func main() {}', None),
    ("explicit-unit", 'func main() -> unit {}', None),
    ("unknown", 'func main() { missing() }', ("LYC301", "Unknown name 'missing'.")),
    ("builtin-shadow", 'func println() {}\nfunc main() {}', ("LYC302", "Name 'println' is already visible.")),
    ("duplicate", 'func main() {}\nfunc main() {}', ("LYC302", "Name 'main' is already visible.")),
    ("no-main", 'func greet() {}', ("LYC305", "Executable requires a main function.")),
    ("main-parameter", 'func main(x: string) {}', ("LYC305", "main must have no parameters and return unit.")),
    ("argument-count", 'func main() { print() }', ("LYC303", "Function 'print' expects 1 argument(s), got 0.")),
    ("argument-type", 'func main() { print(true) }', ("LYC304", "Expected [u8], got bool.")),
    ("parameter", 'func greet(text: string) { println(text) }\nfunc main() { greet("Hi") }', None),
    ("library", 'func greet() { println("Hi") }', None),
    ("unicode-location", 'func main() { println("é"); missing() }', ("LYC301", "Unknown name 'missing'.")),
    ("crlf-location", 'func main() {\r\n missing()\r\n}', ("LYC301", "Unknown name 'missing'.")),
    ("unsupported-class", 'class Node {}\nfunc main() {}', ("LYC306", "Unsupported semantic construct: Class.")),
]


def main():
    command = sys.argv[1:]
    chapter = 4
    if command and command[0].startswith("--chapter="):
        option = command.pop(0)
        if option not in {"--chapter=4", "--chapter=5", "--chapter=6", "--chapter=7", "--chapter=8"}:
            raise SystemExit("Supported chapter selections: 4, 5, 6, 7, 8")
        chapter = int(option.split("=", 1)[1])
    if not command:
        raise SystemExit("Usage: check.py [--chapter=4|5|6|7|8] COMPILER [ARG ...]")
    cases = list(CASES)
    if chapter >= 8:
        # Empty class declarations are now checked. This does not assert that
        # construction, lifetime checking or either C storage layout works.
        cases = [("class-declaration-supported", source, None)
                 if name == "unsupported-class" else (name, source, expected)
                 for name, source, expected in cases]
    if chapter >= 6:
        # Chapter 6 intentionally implements this former milestone boundary.
        # Keep the same source as a retained success, not an omitted check.
        cases = [("branch-value-supported", source, None)
                 if name == "branch-value-unsupported" else (name, source, expected)
                 for name, source, expected in cases]
    failures = []
    with tempfile.TemporaryDirectory(prefix="lyric-check-") as directory:
        for name, source, expected in cases:
            path = pathlib.Path(directory) / (name + ".ly")
            path.write_text(source, encoding="utf-8")
            result = subprocess.run(command + ["--check-library" if name == "library" else "--check", str(path)], capture_output=True,
                                    text=True, timeout=15)
            valid = result.returncode == (0 if expected is None else 1) and result.stdout == ""
            if expected is None:
                valid = valid and result.stderr == ""
            else:
                # Parse JSON filename independently; do not demand one escape style.
                try:
                    filename, end = json.JSONDecoder().raw_decode(result.stderr)
                    import re
                    match = re.fullmatch(r":([1-9][0-9]*):([1-9][0-9]*): error (LYC[0-9]+): (.*) \(byte ([0-9]+)\)\n", result.stderr[end:])
                    valid = valid and filename == str(path) and match is not None
                    if match is not None:
                        valid = valid and (match[3], match[4]) == expected
                        anchors = {
                            "assert-type": "1", "assert-arity": "assert",
                            "branch-isolation": "x", "branch-value-unsupported": "true",
                            "branch-unreachable-unsupported": "print",
                            "branch-one": "x", "branch-possible": "x",
                            "branch-return-missing": "value", "branch-escape": "x",
                            "branch-shadow": "x", "branch-condition": "1",
                            "arithmetic-neighbor-before-return": "256", "arithmetic-default-overflow": "+",
                            "arithmetic-overflow": "+", "arithmetic-no-rescue": "+",
                            "arithmetic-nested-overflow": "*", "arithmetic-incompatible": "+",
                            "arithmetic-neighbor-overflow": "256", "arithmetic-noninteger": "+",
                            "integer-too-large": "2147483648", "integer-context-overflow": "256",
                            "integer-positive-minimum": "128i8", "integer-below-minimum": "-",
                            "integer-negative-unsigned": "-", "integer-no-narrow": "wide",
                            "integer-suffix-fixed": "12i32", "local-unassigned": "message", "local-reassign": "message",
                            "parameter-reassign": "text", "local-mismatch": "true",
                            "return-mismatch": "true", "return-missing": "value",
                            "return-bare": "value", "local-forward": "message",
                            "local-shadow": "print", "unknown": "missing", "builtin-shadow": "println",
                            "duplicate": "main", "no-main": "greet",
                            "main-parameter": "main", "argument-count": "print",
                            "argument-type": "true", "unsupported-class": "Node",
                            "unicode-location": "missing", "crlf-location": "missing",
                        }
                        anchor = anchors[name]
                        index = source.rindex(anchor) if name in {"branch-isolation", "branch-one", "branch-possible", "branch-escape", "branch-shadow", "duplicate", "local-unassigned", "local-reassign", "parameter-reassign", "integer-no-narrow"} else source.index(anchor)
                        prefix = source[:index]
                        byte_offset = len(prefix.encode("utf-8"))
                        line = prefix.count("\n") + 1
                        column = len(prefix.rsplit("\n", 1)[-1]) + 1
                        valid = valid and (int(match[1]), int(match[2]), int(match[5])) == (line, column, byte_offset)
                except (ValueError, TypeError):
                    valid = False
            if not valid:
                failures.append(name)
                print(f"FAIL {name}: exit={result.returncode}, stdout={result.stdout!r}, stderr={result.stderr!r}")
    print(f"{len(cases) - len(failures)}/{len(cases)} semantic cases passed")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
