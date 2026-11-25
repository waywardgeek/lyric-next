# Chapter 3: syntax graders

These public Python graders accept an independently supplied compiler command.
They import no compiler code and use neither C# nor Lyric for grading logic.
Run from the repository root with Python 3 and your built implementation:

```sh
python3 graders/ch03/trees.py -- dotnet compiler/bin/Release/net10.0/Lyric.dll
python3 graders/ch03/recognition.py -- dotnet compiler/bin/Release/net10.0/Lyric.dll
python3 graders/ch03/boundaries.py -- dotnet compiler/bin/Release/net10.0/Lyric.dll
```

Substitute your own command after `--`. Each grader appends an inspection flag
and a temporary UTF-8 source filename. No network, credentials, or installation
is needed. Temporary source files are removed automatically.

`make test` builds the reference project and runs these plus all retained Chapter
1 and 2 graders in **both Debug and Release**. `DOTNET`, `PYTHON`, and `GRADERS`
may be overridden. A nonzero build or grader exit stops the command.

See [the complete tree-shape contract](tree-shapes.md) for anchors and child order.

Chapter 10 intentionally extends the earlier grammar to one-element tuple
expressions, types, and binding patterns. The former single-tuple rejection
cases now require acceptance; malformed doubled commas remain rejections.
Grouping stays distinct: `(T)` has null `type-group` text, while `(T,)` has
comma text. These syntax checks do not establish composite type checking or
execution support.


## Inspection contract

- `--inspect-syntax FILE` parses a complete source file, returning a `file` root.
- `--inspect-expression FILE` parses one complete expression, returning an
  `expression` root with one child. Leading/trailing separators are permitted.
- Both use the real lexer and parser. Neither performs name resolution, type
  checking, numeric range checks, lifetime checking, or execution.
- Success: exit 0, empty stderr, one JSON tree on stdout. Each node has exactly
  `kind` (string), `text` (string or null), and `children` (ordered array).
- `text` is original source spelling for a node's anchor: its name, operator, or
  literal. It is not decoded string data. Structural nodes have null text.
- Syntax failure: exit 1, empty stdout, one located `LYC201` diagnostic using
  Chapter 1's JSON-quoted filename and byte/line/scalar-column convention.
  Existing lexical diagnostics retain their codes. Usage failure remains exit 2.
- Excessive nesting: exit 1, empty stdout, located `LYC203` with message
  `Parser nesting limit exceeded.` This is a bounded implementation resource
  limit, not rejection of that syntax by the language grammar. The reference
  budgets 128 active guarded recursive calls; this is not a 128-delimiter rule.

Graders compare JSON values, allowing equivalent escapes and insignificant
whitespace. Object key order is immaterial in ordinary tree checks; child order
is significant. The long-chain check uses flat JSON tokens to avoid the grader's
own decoder recursion limit; it permits whitespace and equivalent string escapes
and checks the entire ordered structure (including the published key order
`kind`, `text`, `children`). A crash, timeout, unrelated error, or partial tree
never counts as a successful rejection.

## Retained inventory

All checks are **retained**; there are no chapter-only checks.

- `trees.py`: 66 explicitly authored expected trees. Stable IDs are the case names.
  Covers grouping, generic priority, declarations, patterns, and endpoint roles.
- `cases.json` through `recognition.py`: 143 authored accept/reject cases with
  stable `id` fields. These require the right exit/stream behavior, valid tree
  schema for success, and correctly located diagnostic codes for rejection.
  They are representative grammar-family coverage, not proof of all combinations.
- `boundaries.py`: seven stable named checks: three exact syntax diagnostics,
  three controlled nesting failures, and the complete 4,000-term arithmetic tree.
  Boundary subprocess timeout is 30 seconds; ordinary case timeout is 10 seconds.
- Retained Chapter 1 and Chapter 2 checks apply unchanged.

The supplementary direct C# model probe used during development is not a retained
independent grader. Behavioral passes do not replace architecture/ownership review.

## Independent PEG comparison

The canonical grammar checker can run the same recognition fixtures:

```sh
DOTNET=dotnet node tools/peg/test.mjs graders/ch03/cases.json
```

This requires the separately documented pinned Peggy setup (`make grammar-setup`)
and a Release compiler for lexing. It executes a different syntax recognizer,
not an independent lexer or semantic checker. The fixtures' authored expectations
remain the oracle; agreement between two implementations alone is insufficient.
The implementation resource tests are not language-syntax rejection fixtures and
are deliberately not sent to Peggy.

Chapter 10 also admits function members in struct bodies: value and explicit
reference receivers, static functions, visibility, and declarations followed by
qualified bodies. These retained recognition cases check syntax only, not
signature matching or method execution. The struct-method tree case requires
the same function-node shape used in class bodies.
