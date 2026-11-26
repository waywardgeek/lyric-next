#!/usr/bin/env python3
"""Development probe, NOT a retained chapter grader. No downloads/packages.
Run from repository root: python3 tools/probes/check_generic_model.py /path/to/dotnet
Builds actual compiler sources and the same-assembly probe in a temporary folder.
"""
from pathlib import Path
import subprocess
import sys
import tempfile
from xml.sax.saxutils import escape

root = Path(__file__).resolve().parents[2]
# An alternate same-assembly diagnostic uses the identical offline build recipe.
if len(sys.argv) not in (2, 4):
    raise SystemExit("usage: check_generic_model.py DOTNET [PROBE_SOURCE STARTUP_TYPE]")
probe = Path(sys.argv[2]).resolve() if len(sys.argv) == 4 else root / "tools/probes/generic-model.cs"
startup = sys.argv[3] if len(sys.argv) == 4 else "GenericModelProbe"
with tempfile.TemporaryDirectory(prefix="lyric-generic-model-") as directory:
    work = Path(directory)
    sources = sorted((root / "compiler").rglob("*.cs"))
    sources = [p for p in sources if "bin" not in p.parts and "obj" not in p.parts]
    sources.append(probe)
    includes = "\n".join(f'<Compile Include="{escape(str(p))}" />' for p in sources)
    (work / "Probe.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<LangVersion>14</LangVersion><Nullable>enable</Nullable><ImplicitUsings>disable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<StartupObject>{escape(startup)}</StartupObject></PropertyGroup><ItemGroup>{includes}</ItemGroup></Project>''')
    (work / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    for configuration in ("Debug", "Release"):
        subprocess.run([sys.argv[1], "build", str(work / "Probe.csproj"), "-c", configuration,
                        "--nologo", "-tl:off"], cwd=work, check=True, timeout=120)
        subprocess.run([sys.argv[1], str(work / "bin" / configuration / "net10.0/Probe.dll")],
                       cwd=work, check=True, timeout=30)
