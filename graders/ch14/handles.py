#!/usr/bin/env python3
"""Chapter 14 production handle layout contract (retained).

Run: python3 graders/ch14/handles.py COMPILER [ARG ...]
Uses installed CC (default clang); no downloads. Emission must succeed with empty
stderr. C11 compilation must succeed. Execution requires exit 0, exact stdout,
and empty stderr at O0/O2. Layout assertions inspect real generated declarations.
Debug identity behavior is covered separately, not required to use these formats.
"""
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
import tempfile

SOURCE = '''value class Box {
    func Box(self) {}
    final func finish(self) { print("X") }
}
func main() {
    if true { let first = Box(); let copies = [first, first]; print("held") }
    if true { let reused = Box(); print("reuse") }
    print("done")
}
'''


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


def main():
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit("supply compiler command")
    passed = 0
    with tempfile.TemporaryDirectory(prefix="lyric-handles-") as directory:
        root = Path(directory)
        source = root / "handles.ly"
        source.write_text(SOURCE)
        for soa in (False, True):
            label = "soa" if soa else "aos"
            try:
                emitted = run(compiler + ["--emit-c"] + (["--soa"] if soa else []) + [str(source)])
                assert emitted.returncode == 0 and not emitted.stderr, emitted.stderr
                text = emitted.stdout.decode()
                # One nominal class: locate its actual allocator, not a source-name
                # guess or a parallel implementation provided by the grader.
                allocators = re.findall(r"static\s+([^\n]+?)\s+(\w+)_allocate\(const char \*file", text)
                assert len(allocators) == 1, allocators
                result_type, name = allocators[0]
                if soa:
                    assert result_type == "uint32_t", ("SoA handle type", result_type)
                    assert f"static ly_array {name}_next_free;" in text, "missing separate free-link array"
                    assert f"static ly_array {name}_references;" in text, "missing separate count array"
                    assert f"{name}_slot" not in text, "AoS metadata remains in SoA output"
                else:
                    assert result_type.replace(" ", "") == f"{name}_record*", ("AoS handle type", result_type)
                    record = re.search(r"struct " + re.escape(name) + r"_record\s*\{(.*?)\}", text, re.S)
                    assert record, "missing object record"
                    assert "uint64_t references;" in record[1], "count is not in object record"
                    assert re.search(re.escape(name) + r"_record\s*\*\s*next_free;", record[1]), "free link is not in object record"
                    assert f"{name}_slots" not in text, "separate production AoS metadata remains"
                c_source = root / "handles.c"
                c_source.write_bytes(emitted.stdout)
                for optimization in ("-O0", "-O2"):
                    executable = root / "handles"
                    built = run(shlex.split(os.environ.get("CC", "clang")) +
                                ["-std=c11", optimization, str(c_source), "-o", str(executable)])
                    assert built.returncode == 0, built.stderr
                    executed = run([str(executable)])
                    assert (executed.returncode, executed.stdout, executed.stderr) == (0, b"heldXreuseXdone", b""), executed
                # Exercise the real emitted allocator at a bounded artificial
                # high-water limit. Reuse must precede exhaustion; no enormous
                # allocation, stale dereference, or replacement allocator is used.
                assert text.count("int main(void)") == 1
                probe = text.replace("int main(void)", "int original_main(void)")
                empty = "UINT32_C(0)" if soa else "NULL"
                first_index_check = "if (first != UINT32_C(1)) return 94;" if soa else ""
                exhaust = f"{name}_next = UINT64_C(4294967296);" if soa else ""
                fresh = (f'(void){name}_allocate("probe", 1, 1); return 95;'
                         if soa else f'{name}_release(&reused, "probe", 1, 1); return 0;')
                probe += f'''
int main(void) {{
    {result_type} first = {name}_allocate("probe", 1, 1);
    if (first == {empty}) return 91;
    {first_index_check}
    {result_type} saved = first;
    {name}_release(&first, "probe", 1, 1);
    if (first != {empty}) return 92;
    {exhaust}
    {result_type} reused = {name}_allocate("probe", 1, 1);
    if (reused != saved) return 93;
    {fresh}
}}
'''
                c_source.write_text(probe)
                for optimization in ("-O0", "-O2"):
                    executable = root / "allocator-boundary"
                    built = run(shlex.split(os.environ.get("CC", "clang")) +
                                ["-std=c11", optimization, str(c_source), "-o", str(executable)])
                    assert built.returncode == 0, built.stderr
                    executed = run([str(executable)])
                    expected_error = (b"probe:1:1: runtime error LYR007: class allocation size overflow\n"
                                      if soa else b"")
                    expected = (1 if soa else 0, b"X" if soa else b"XX", expected_error)
                    assert (executed.returncode, executed.stdout, executed.stderr) == expected, executed
                passed += 1
                print("PASS", label, "production representation, counted reuse and allocator boundary")
            except (AssertionError, subprocess.TimeoutExpired) as error:
                print("FAIL", label, error)
    print(f"{passed}/2 production handle contracts passed")
    return 0 if passed == 2 else 1


if __name__ == "__main__":
    raise SystemExit(main())
