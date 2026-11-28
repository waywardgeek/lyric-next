#!/usr/bin/env python3
"""Retained Chapter14 successor to the Chapter13 allocator invariant contract.

Preserves every mode, execution and missing-hold control; only generated-C
metadata addressing changes for the agreed production layout migration.

Run from repository root: python3 graders/ch14/debug_mode.py COMPILER [ARG ...]
Requires Python3 and C11 compiler (CC, default clang). No downloads.
Checks generated metadata/guards in both layouts and executes a valid counted
program at O0/O2 in both modes. Debug assertions are not language safety checks.
"""
import json
import os
import re
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile

SOURCE = '''value class Box {
    func Box(self) {}
    pub func show(self) { print("live") }
    pub func keep(self)->Box { return self }
    final func finish(self) { print("drop") }
}
func main() {
    if true {
        let box=Box()
        box.show()
    }
    print("done")
}
'''
NULL_SOURCE = '''class Box { func Box(self) {} }
func main() { let box:Box?=null; box!.destroy() }
'''


def run(command):
    return subprocess.run(command, capture_output=True, timeout=30)


def main():
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit("supply compiler command")
    passed = 0
    with tempfile.TemporaryDirectory(prefix="lyric-debug-mode-") as directory:
        root = Path(directory)
        source = root / "case.ly"
        source.write_text(SOURCE)
        null_source = root / "null.ly"
        null_source.write_text(NULL_SOURCE)
        # Ownership convention must be explicit in the published IR, not inferred
        # again by C emission. Returning self is deliberately not observation-only.
        inspected = run(compiler + ["--inspect-ir", str(source)])
        assert inspected.returncode == 0 and not inspected.stderr, inspected.stderr
        functions = json.loads(inspected.stdout)["functions"]
        show = next(function for function in functions if function["name"] == "show")
        keep = next(function for function in functions if function["name"] == "keep")
        assert show["borrowedReceiver"] is True
        assert keep["borrowedReceiver"] is False
        receiver = next(node for node in show["nodes"]
                        if node["kind"] == "Local" and node["symbol"] == "self")
        assert not any(node["kind"] in ("Ref", "Unref", "Move")
                       and receiver["id"] in node["operands"] for node in show["nodes"])
        print("PASS observing-versus-escaping receiver IR")
        for layout in ([], ["--soa"]):
            for debug in (False, True):
                options = layout + (["--debug"] if debug else [])
                label = ("soa" if layout else "aos") + ("-debug" if debug else "-default")
                try:
                    emitted = run(compiler + ["--emit-c", *options, str(source)])
                    assert emitted.returncode == 0 and not emitted.stderr, emitted.stderr
                    text = emitted.stdout.decode()
                    for marker in ("unsigned char state;", "_destroying_state", "invalid object retain",
                                   "invalid object release", "invalid object destruction",
                                   "empty object reference count", "unbalanced destruction references",
                                   "_identities;", "_identity_next;", "_resolve(uint64_t handle"):
                        assert (marker in text) == debug, (label, marker)
                    # Null unwrap remains a required checked operation in either mode.
                    null = run(compiler + ["--emit-c", *options, str(null_source)])
                    assert null.returncode == 0 and not null.stderr, null.stderr
                    assert b"LYR013: null handle unwrap" in null.stdout
                    c_source = root / "case.c"
                    c_source.write_bytes(emitted.stdout)
                    for optimization in ("-O0", "-O2"):
                        executable = root / "case"
                        built = run(shlex.split(os.environ.get("CC", "clang")) +
                                    ["-std=c11", optimization, str(c_source), "-o", str(executable)])
                        assert built.returncode == 0, built.stderr
                        result = run([str(executable)])
                        assert (result.returncode, result.stdout, result.stderr) == (0, b"livedropdone", b""), result
                    # Exercise the real generated helpers independently of source
                    # effect restrictions and mandatory IR ownership simplification.
                    # This is runtime instrumentation, not a source-language pass.
                    pattern = r"static ly_unit (\w+)_destroy_body\(([^)\n]+) index\)\n\{"
                    matches = list(re.finditer(pattern, text))
                    assert len(matches) == 1, "expected one Box destruction body"
                    match = matches[0]
                    name = match.group(1)
                    # Runtime debug handles are allocation identities, not slots.
                    # Resolve freshly after calls rather than retain a metadata pointer.
                    metadata_index = (f'{name}_resolve(index, __FILE__, __LINE__, 1, "probe")'
                                      if debug else 'index')
                    if debug:
                        count = f"(({name}_slot *){name}_slots.data)[{metadata_index}].references"
                        empty = "UINT64_MAX"
                    elif layout:
                        count = f"((uint64_t *){name}_references.data)[index]"
                        empty = "UINT32_C(0)"
                    else:
                        count = "index->references"
                        empty = "NULL"
                    holder_type = match.group(2)
                    probe = f"""
    if ({count} != 1) exit(91);
    {holder_type} probe_holder = {name}_retain(index, __FILE__, __LINE__, 1);
    {name}_release(&probe_holder, __FILE__, __LINE__, 1);
    if (probe_holder != {empty}) exit(92);
    if ({count} != 1) exit(93);
"""
                    instrumented = text[:match.end()] + probe + text[match.end():]
                    for broken in (False, True):
                        checked = instrumented
                        if broken:
                            # A deliberate defect must be detected before retain;
                            # never execute an uncontrolled recursive destruction.
                            restoration = "slot->references = 1;" if debug else "*references = 1;"
                            assert checked.count(restoration) == 1
                            checked = checked.replace(restoration, "/* missing hold */")
                        c_source.write_text(checked)
                        for optimization in ("-O0", "-O2"):
                            built = run(shlex.split(os.environ.get("CC", "clang")) +
                                        ["-std=c11", optimization, str(c_source), "-o", str(executable)])
                            assert built.returncode == 0, built.stderr
                            result = run([str(executable)])
                            expected = (91, b"live", b"") if broken else (0, b"livedropdone", b"")
                            assert (result.returncode, result.stdout, result.stderr) == expected, result
                    passed += 1
                    print("PASS", label, "including destruction hold and missing-hold control")
                except (AssertionError, subprocess.TimeoutExpired) as error:
                    print("FAIL", label, error)
    print(f"{passed}/4 debug-mode contracts passed")
    return 0 if passed == 4 else 1


if __name__ == "__main__":
    raise SystemExit(main())
