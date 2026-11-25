#!/usr/bin/env python3
"""Development-only real IR-to-C tuple probe; no retained grader claim.
Run from repository root with the offline dotnet executable as the sole argument.
"""
from pathlib import Path
import os
import re
import shlex
import subprocess
import sys
import tempfile
from xml.sax.saxutils import escape

root = Path(__file__).resolve().parents[2]
mode = sys.argv[2] if len(sys.argv) > 2 else "tuple"
if mode not in ("tuple", "struct", "empty", "recursive", "initializer", "borrowed", "address-initializer"):
    raise ValueError(f"Unknown probe mode: {mode}")
structure = mode in ("struct", "initializer")
with tempfile.TemporaryDirectory(prefix="lyric-tuple-emission-") as directory:
    work = Path(directory)
    sources = [p for p in sorted((root / "compiler").rglob("*.cs"))
               if "bin" not in p.parts and "obj" not in p.parts]
    sources.append(root / "tools/probes/tuple-emission.cs")
    includes = "\n".join(f'<Compile Include="{escape(str(p))}" />' for p in sources)
    (work / "Probe.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<LangVersion>14</LangVersion><Nullable>enable</Nullable><ImplicitUsings>disable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<StartupObject>TupleEmissionProbe</StartupObject></PropertyGroup><ItemGroup>{includes}</ItemGroup></Project>''')
    (work / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    subprocess.run([sys.argv[1], "build", str(work / "Probe.csproj"), "-tl:off"],
                   cwd=work, check=True, timeout=120)
    generated = work / "generated.c"
    subprocess.run([sys.argv[1], str(work / "bin/Debug/net10.0/Probe.dll"), str(generated), mode],
                   cwd=work, check=True, timeout=30)
    # One canonical tuple in this graph: (i32, [i32]). Its generated helper names
    # are backend-local, deliberately not a public source-language requirement.
    harness = work / "test.c"
    harness.write_text('''#define main lyric_generated_main
#include "generated.c"
#undef main
#include <assert.h>
int main(void)
{
    assert(lyric_generated_main() == 0);
    ly_tuple0 original = ly_tuple0_empty();
    original.item0 = 17;
    original.item1 = ly_array_new(1, sizeof(int32_t), "probe", 1, 1);
    ((int32_t *)original.item1.data)[0] = 7;
    original.item1.length = 1;
    ly_tuple0 copy = ly_tuple0_copy(original, "probe", 1, 1);
    assert(copy.item0 == 17 && copy.item1.data != original.item1.data);
    ((int32_t *)original.item1.data)[0] = 99;
    ly_tuple0_drop(&original, "probe", 1, 1);
    assert(original.item1.data == NULL && original.item1.length == 0);
    assert(((int32_t *)copy.item1.data)[0] == 7);
    ly_array outer = ly_array_new(1, sizeof(ly_array), "probe", 1, 1);
    ly_array inner = ly_array_new(1, sizeof(ly_tuple0), "probe", 1, 1);
    ((ly_tuple0 *)inner.data)[0] = copy;
    inner.length = 1;
    ((ly_array *)outer.data)[0] = inner;
    outer.length = 1;
    ly_array second = ly_tuple0_array_copy(outer, 2, "probe", 1, 1);
    ly_tuple0_array_drop(&outer, 2, "probe", 1, 1);
    assert(outer.data == NULL && outer.capacity == 0);
    ly_array remaining = ((ly_array *)second.data)[0];
    ly_tuple0 leaf = ((ly_tuple0 *)remaining.data)[0];
    assert(leaf.item0 == 17 && ((int32_t *)leaf.item1.data)[0] == 7);
    ly_tuple0_array_drop(&second, 2, "probe", 1, 1);
    return 0;
}
''')
    if structure:
        harness.write_text(harness.read_text().replace("ly_tuple0", "ly_struct0"))
    if mode == "initializer":
        # Inspect the emitted function name, not a second implementation of defaults.
        initializer = re.search(r"static ly_array (ly_s[0-9]+_initialize)\(void\);", generated.read_text())
        if initializer is None:
            raise RuntimeError("No field initializer prototype emitted")
        invocation = initializer.group(1) + "()"
        checks = f"""
    ly_array default_first = {invocation};
    ly_array default_second = {invocation};
    assert(default_first.length == 1 && default_second.length == 1);
    assert(default_first.data != default_second.data);
    ((int32_t *)default_first.data)[0] = 99;
    assert(((int32_t *)default_second.data)[0] == 7);
    free(default_first.data);
    free(default_second.data);
"""
        harness.write_text(harness.read_text().replace("    return 0;", checks + "    return 0;"))
    if mode == "borrowed":
        harness.write_text('''#define main lyric_generated_main
#include "generated.c"
#undef main
#include <assert.h>
int main(void)
{
    assert(lyric_generated_main() == 0);
    ly_array target = ly_array_new(1, sizeof(int32_t), "probe", 1, 1);
    target.length = 1;
    ((int32_t *)target.data)[0] = 7;
    ly_struct0 original = ly_struct0_empty();
    original.item0 = &target;
    original.item2 = &original;
    original.item1 = ly_array_new(1, sizeof(int32_t), "probe", 1, 1);
    original.item1.length = 1;
    ((int32_t *)original.item1.data)[0] = 11;
    ly_struct0 copy = ly_struct0_copy(original, "probe", 1, 1);
    assert(copy.item0 == &target);
    assert(copy.item2 == &original);
    assert(copy.item1.data != original.item1.data);
    ((int32_t *)target.data)[0] = 99;
    ly_struct0_drop(&original, "probe", 1, 1);
    assert(((int32_t *)copy.item0->data)[0] == 99);
    assert(((int32_t *)copy.item1.data)[0] == 11);
    ly_struct0_drop(&copy, "probe", 1, 1);
    assert(target.length == 1 && ((int32_t *)target.data)[0] == 99);
    free(target.data);
    return 0;
}
''')
    if mode == "address-initializer":
        initializer = re.search(r"static ly_array \*(ly_s[0-9]+_initialize)\(void\);", generated.read_text())
        if initializer is None:
            raise RuntimeError("No pointer-returning initializer prototype emitted")
        harness.write_text(f'''#define main lyric_generated_main
#include "generated.c"
#undef main
#include <assert.h>
int main(void)
{{
    assert(lyric_generated_main() == 0);
    ly_array *target = {initializer.group(1)}();
    assert(target->length == 1);
    assert(((int32_t *)target->data)[0] == 7);
    ((int32_t *)target->data)[0] = 19;
    assert(((int32_t *)target->data)[0] == 19);
    free(target->data);
    *target = (ly_array){{0}};
    return 0;
}}
''')
    if mode in ("empty", "recursive"):
        # Generated main constructs, copies and drops the real aggregate IR.
        # C compilation also verifies recursive record/prototype ordering.
        harness.write_text('#include "generated.c"\n')
    for optimization in ("-O0", "-O2"):
        binary = work / "test"
        subprocess.run([*shlex.split(os.environ.get("CC", "cc")), "-std=c11", optimization,
                        str(harness), "-o", str(binary)], cwd=work, check=True, timeout=60)
        result = subprocess.run([str(binary)], cwd=work, capture_output=True, timeout=30)
        if result.returncode != 0 or result.stdout or result.stderr:
            raise RuntimeError(f"{optimization}: {result.returncode}, {result.stdout!r}, {result.stderr!r}")
        behavior = "address return/call and category rejection" if mode == "address-initializer" else "IR emission and recursive copy/drop"
        print(f"PASS {mode} {behavior} {optimization}")
