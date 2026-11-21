# Independent compiler graders

These graders and their fixtures are readable by students but not student-editable.
They accept a separately supplied compiler command. They do not read its source,
invoke the table generator, or depend on a reference solution location.

## Chapter 1

Prerequisites: Python 3.10+; a built implementation supporting the contract below.
The reference bootstrap uses .NET SDK 10.0.403 / C# 14 on macOS ARM64; no NuGet
packages are needed. No credentials or network access are used during grading.

From the repository root, build the reference with:

```sh
dotnet build compiler/Lyric.csproj --nologo --tl:off
python3 graders/ch01/grade.py -- dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

`dotnet` must name the installed .NET 10 SDK/host. If it is not on PATH, substitute
its absolute path in both commands. To grade another implementation, replace the
command after `--` with that implementation's executable and any fixed arguments.
The grader appends operation arguments; it never invokes a shell for those calls.

A passing run exits 0 and reports `82 passed; 0 failed`. Every case must pass.
The grader creates source fixtures in a temporary directory and removes that
directory afterward. Each compiler invocation has a 10-second timeout. A timeout,
crash, unrelated exception, or arbitrary nonzero exit fails the case.

The retained grader-comparison self-check is separate from compiler conformance:

```sh
python3 graders/ch01/check_comparisons.py
```

Run it when maintaining or reproducing the graders. It rejects boolean positions
and unsafe/incorrect diagnostics while accepting equivalent JSON escaping.

### Retained inventory

All Chapter 1 cases are **retained**; there are no chapter-only checks. Stable case
identifiers are printed by `grade.py`. Groups are:

- `version`, `usage-*`, `missing-file`, `empty-path`: operation and input-error contracts.
- Named positive cases from `empty` through `allowed-property-neighbors`: actual
  decoded scalars, byte spans, scalar columns, LF/CRLF, and EOF locations.
- `malformed-*`: illegal/truncated UTF-8, overlong encodings, surrogate encodings,
  values above U+10FFFF, and invalid continuation bytes.
- `forbidden-*`: endpoints of all forbidden ranges, selected interior points, and
  comment/string/raw/symbol-looking contexts using upstream Unicode 16.0.0 data.
- `diagnostic-unusual-filename`: equivalent safe JSON filename escaping.
- `lone-cr-*`, `earliest-error`: newline rejection and first-error ordering.

Later chapters must retain this inspection operation even after introducing syntax
checking. It tests the real source reader, not whether a fixture is a valid program.

## Source inspection contract

`--unicode-version` writes `16.0.0` and LF to stdout, with no stderr, and exits 0.

`--inspect-source FILE` reads the complete file as bytes. Strictly decode UTF-8;
reject invalid encodings rather than replace them. Reject Unicode 16.0.0
Default_Ignorable_Code_Point, Bidi_Control, and General_Category Cc, except HT/LF
and CR as part of CRLF. Reject a literal BOM even at the start. Diagnose the first
error in byte order, at the start of its encoding sequence. Lone CR is an error.
Do not impose identifier NFC, syntax validity, or extra script restrictions here.

Valid input exits 0, writes no stderr, and emits one JSON object per line to stdout:

```json
{"kind":"scalar","byteOffset":0,"byteLength":2,"line":1,"column":1,"scalar":"U+00E9"}
{"kind":"end","byteOffset":2,"line":1,"column":2}
```

Offsets are zero-based UTF-8 byte offsets. Line and column are one-based. Columns
count Unicode scalars, including tabs, not display cells or encoded bytes. CRLF
emits one `U+000A` event spanning both bytes; LF emits the same scalar spanning one
byte. Either advances to the next line, column 1. Scalar spelling uses uppercase
hexadecimal with at least four digits. The final end event always appears, including
for an empty file. It reports the position immediately after the final event.

The grader compares parsed JSON records: object key order and JSON whitespace are
irrelevant, but values, keys, record order, and number of records must agree.
Position fields must be JSON numbers, never booleans; equivalent number spellings
(such as `0` and `0.0`) compare by value.
No banners, extra events, or diagnostic output may appear on stdout.

Invalid source exits 1 with **empty stdout** and one stderr line:

```text
"FILE":LINE:COLUMN: error CODE: MESSAGE (byte OFFSET)
```

FILE is the supplied path encoded as a JSON string, so filenames cannot introduce
unescaped control characters. Equivalent JSON escape spellings compare by decoded
filename, not raw spelling; the remaining diagnostic text is exact. Locations follow the rules above. The messages are:

- `LYC001`: `Malformed UTF-8 sequence.`
- `LYC002`: `Forbidden source character U+XXXX (PROPERTY).` Here PROPERTY is
  `General_Category=Cc` for controls, otherwise `Default_Ignorable_Code_Point`.
  All Unicode 16.0.0 bidi controls also have the latter property.
- `LYC003`: `Lone U+000D; use LF or CRLF.`

Do not reproduce forbidden characters in diagnostics. An unreadable input file, including an explicitly supplied empty or invalid path,
exits 1, writes no stdout, and emits `error LYC004: Cannot read source file.` plus
LF to stderr. Missing/unknown/excess arguments exit 2, write no stdout, and emit
`Usage: Lyric --inspect-source FILE | --unicode-version` plus LF to stderr.

These are compiler diagnostics, not Lyric's runtime Status or fatal-error API.
Exhausted memory and output-device failures are not source rejection successes.
EOF positions and scan results must come from the actual input, not fixture answers.
