# Lyric compiler — Chapter 7 reference increment

This increment reads UTF-8 source, applies the pinned Unicode source-character
rules, normalizes/classifies identifiers using pinned Unicode data, and tokenizes
source including literals and interpolation, and parses the syntax grammar into
a source-owned tree. It resolves names and checks a bounded semantic subset:
functions, calls, locals, integer expressions and casts, returns, short-circuit
booleans, value-producing conditionals, and while/break/continue. Chapter 7 adds
integer/boolean/nested dynamic arrays, string as [u8], recursive independent values,
u64 lengths, checked growth and indexing, fixed local reference aliases and explicit
reference parameters. Temporary control-flow and ordered helper-effect analysis
checks stable storage through last use. Classes, floating expressions, borrowed
ranges, reference returns and escaping closures remain outside this increment.
It emits that checked subset as a self-contained C11 translation unit; an external
C compiler builds the executable. The current backend requires `__has_builtin` and
`__builtin_add_overflow`, `__builtin_sub_overflow`, and `__builtin_mul_overflow`;
plain C11 support alone is insufficient. Apple Clang 21 is the verified compiler. The Lyric compiler does not launch or execute it.
Syntax inspection remains a separate pipeline boundary and does not imply semantic validity.

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
- **Syntax** implements source-owned identity nodes and complete two-way tree
  relationships. Each node has a source backlink and an optional syntax-parent
  backlink; parent/child edges are weak, so source ownership controls destruction.
  Nodes retain source byte spans, not copied spelling or token-object references.
- **Parser** constructs those nodes through recursive descent and precedence
  climbing. Failed alternatives restore the cursor and discard their new nodes;
  failed complete parses clear the unfinished tree before reporting a diagnostic.
- **Semantics** owns compilation-wide analysis, lexical scopes, symbols, and
  source-linked checked facts. Signatures use ordered parameter symbols; name
  collection precedes body checking. Input changes retire the analysis. Temporary
  definite/possible assignment sets join only continuing control-flow paths.
- **Emission** consumes checked symbols and facts to produce buffered C11 output.
  Explicit temporaries preserve evaluation order; typed builtins check addition,
  subtraction, and multiplication, while explicit guards protect division, shifts,
  and conversions. Linked activations provide fatal-error traces.
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
make spin03   # Build and inspect hello world and arithmetic syntax trees.
make spin04   # Check meaning, contextual integers, and branch initialization.
make spin05   # Emit C, compile, run, and demonstrate checked runtime failure.
make spin06   # Exercise integer operations, conditionals, and loops.
make spin07   # Grow/copy arrays and demonstrate stable reference checks.
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
for lexer commands and its practical walkthrough. See [Chapter 3](../book/chapter-03.md)
and the [parser grader contract](../graders/ch03/README.md) for tree inspection,
recognition, resource checks, and the syntax-tree shape reference.

Source tests do not establish lifecycle correctness by themselves. Review the
Common/Model paths as well as running the independent behavioral graders. For Chapter 1, the single
code review and text review are complete, and their corrections are incorporated.
Fresh-student reproduction passed 82/82 in Debug and Release without implementation
assistance. Bill ran the Chapter 1 walkthrough and fully accepted that chapter.
Chapter 2 reference graders
pass in both configurations; its single code/text reviews and unassisted fresh-student reconstruction are complete.
Bill ran both walkthroughs and accepted both chapters. Chapter 3 reference checks
pass in both configurations; its reviews and fresh-student reconstruction are
complete, with runtime setup assistance only. Bill’s Chapter 3 walkthrough remains
unconfirmed. Chapter 4 passes 79 semantic cases and the retained suites in both
configurations; its single code and text reviews are incorporated. An unassisted fresh-student
reconstruction passed all required checks; human acceptance remains pending. See [Chapter 4](../book/chapter-04.md) and its
[public semantic contract](../graders/ch04/README.md).


Chapter 5 adds `--emit-c FILE`: checked executable input to a self-contained C11
translation unit, with no subprocess invocation inside the compiler. The public
execution grader compiles and runs that output at O0 and O2. All 75 cases pass in
Debug and Release; the separate retained PEG comparison passes 143/143. A C11
compiler is required for `make test` and `make spin05`; `CC` selects it.
For the current Chapter 6 backend, it must also provide the checked-arithmetic
builtins specified at the start of this document.
The single code and text reviews are incorporated; fresh-student reconstruction passed all required cumulative checks, the separate PEG comparison, and the scripted walkthrough; human acceptance remains pending. See
[Chapter 5](../book/chapter-05.md) and its
[execution contract](../graders/ch05/README.md). Classes remain deferred.

Chapter 6 extends expressions and control flow; see its [construction chapter](../book/chapter-06.md) and [public contract](../graders/ch06/README.md). Its expanded reference and independent reconstruction pass all required cumulative checks, including 250 Chapter 6 cases in both configurations. The single code and text reviews are incorporated; the student needed runtime setup assistance only. Human walkthrough and acceptance remain pending.
