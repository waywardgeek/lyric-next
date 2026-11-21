# Lyric compiler — Chapter 1 reference increment

This increment reads UTF-8 source, applies the pinned Unicode source-character
rules, and exposes source inspection. It does not tokenize, parse, type-check, or
emit C. Inspection success is not a claim that the input is a valid Lyric program.

## Modules and ownership

- **Common** declares shared types, persistent state, relationships, and callable
  partial members. `SourceRules.Data.cs` contains generated immutable value data.
- **Model** implements creation and complete two-way ownership operations. A
  `Compilation` strongly owns `SourceFile` objects; each source retains its parent.
  Attachments refuse destroyed owners/children or existing memberships before
  mutation. Removal clears both ends. Destruction of the compilation repeatedly
  destroys a remaining source, letting that child detach itself.
- **Source** implements scanning and property membership using Common declarations.
  It does not import Model implementation helpers. A diagnostic is explicitly
  value-like; it does not retain the source or need holder backlinks.
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
From the repository root:

```sh
dotnet build compiler/Lyric.csproj --nologo --tl:off
python3 graders/ch01/grade.py -- dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

See [the public grader contract](../graders/README.md) for exact output, failures,
fixture access, and independent implementation invocation. Unicode data provenance
and regeneration are in [unicode/README.md](../unicode/README.md). Normal builds
use the checked-in generated table; they do not download data or run the generator.

Source tests do not establish lifecycle correctness by themselves. Review the
Common/Model paths as well as running the independent behavioral graders. The single
code review and text review are complete, and their corrections are incorporated.
Fresh-student reproduction passed 82/82 in Debug and Release without implementation
assistance. Human walkthrough and acceptance remain outstanding.
