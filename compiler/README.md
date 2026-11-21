# Lyric compiler — Chapter 2 reference increment

This increment reads UTF-8 source, applies the pinned Unicode source-character
rules, normalizes/classifies identifiers using pinned Unicode data, and tokenizes
source including literals and interpolation. It does not parse, type-check, or
emit C. Inspection success is not a claim that the input is a valid Lyric program.

## Modules and ownership

- **Common** declares shared types, persistent state, relationships, and callable
  partial members. `SourceRules.Data.cs` and `IdentifierRules.Data.cs` contain generated immutable value data.
- **Model** implements creation and complete two-way ownership operations. A
  `Compilation` strongly owns `SourceFile` objects; each source retains its parent.
  Each source in turn strongly owns tokens with source backlinks.
  Attachments refuse destroyed owners/children or existing memberships before
  mutation. Removal clears both ends. Destruction of the compilation repeatedly
  destroys a remaining source, letting that child detach itself.
- **Source** implements scanning and property membership using Common declarations.
  It does not import Model implementation helpers. A diagnostic is explicitly
  value-like; it does not retain the source or need holder backlinks.
- **Unicode** implements pinned XID membership and canonical normalization.
- **Lexer** implements source-backed tokens, literal decoding, and lexical mode
  transitions. Inspection prints the same tokens later phases will consume.
- **Driver** owns command-line/file/console operations and the compilation lifetime.
  Its `finally` destroys the compilation on success and ordinary failure.

Matching partial declarations/bodies compile into one assembly. No separate runtime
service adapter connects them. The source buffer is authoritative and immutable by
contract; scanning uses local counters, not a second normalized text copy. The driver
validates before requesting output from the same scanner, preventing a rejected
file from producing a partial success stream. This currently trades a second
linear scan for a simple bounded-memory protocol without buffering all events.

## Build and verify

Use .NET SDK 10.0.403 (C# 14 / .NET 10); the project has no package dependencies.
From the repository root, the short commands are:

```sh
make release  # Build Release.
make spin01   # Build and run the Chapter 1 walkthrough.
make spin02   # Build and run the Chapter 2 walkthrough.
make test     # Build Debug/Release and run all retained public graders.
```

Make and Python 3.10+ are required for these helpers. Override `DOTNET`, `PYTHON`,
or `GRADERS` on the make command line when needed. The explicit commands remain:

```sh
dotnet build compiler/Lyric.csproj --nologo --tl:off
python3 graders/ch01/grade.py -- dotnet compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch02/unicode.py -- dotnet compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch02/tokens.py -- dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

See [the public grader contract](../graders/README.md) for exact output, failures,
fixture access, and independent implementation invocation. Unicode data provenance
and regeneration are in [unicode/README.md](../unicode/README.md). Normal builds
use the checked-in generated table; they do not download data or run the generator.

Repeat builds and all checks for Release; see [Chapter 2](../book/chapter-02.md)
for complete commands and its practical walkthrough.

Source tests do not establish lifecycle correctness by themselves. Review the
Common/Model paths as well as running the independent behavioral graders. For Chapter 1, the single
code review and text review are complete, and their corrections are incorporated.
Fresh-student reproduction passed 82/82 in Debug and Release without implementation
assistance. Bill ran the Chapter 1 walkthrough and fully accepted that chapter.
Chapter 2 reference graders
pass in both configurations; its single code/text reviews and unassisted fresh-student reconstruction are complete.
Bill reviewed and accepted Chapter 2.
