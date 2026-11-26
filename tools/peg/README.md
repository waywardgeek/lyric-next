# Executable PEG checker

This developer tool executes the syntax rules in `../../lyric-grammar.peg`
with [Peggy](https://peggyjs.org/documentation.html) 5.1.1. It does not replace
Lyric's hand-written parser or perform name resolution, type checking, or safety
analysis. It is separate from `make test`. Chapters 1–2 do not require it; Chapter 3
and later require the retained 155-fixture comparison documented in Chapter 3
(`DOTNET=dotnet node tools/peg/test.mjs graders/ch03/cases.json`). The base
grammar-development checks below are distinct from that completion command.

## Run

Requirements: Node 22+, npm, Make, and the existing .NET 10 compiler build.
From the repository root:

```sh
make grammar-setup
make grammar-check
make syntax FILE=path/to/example.ly
```

Setup downloads the exact dependency lockfile into tools/peg/node_modules, with
package install scripts disabled. No global packages are installed. The other
commands build Release but do not install JavaScript dependencies or make network
calls through the checker. DOTNET, NODE and NPM may be overridden as Make variables.
Missing dependencies are setup errors, not syntax rejections. Lexing is bounded
to 30 seconds and 32 MiB of output. PEG execution has no separate timeout or
recursion-depth guarantee; this is a local developer tool, not an untrusted-input
service or a performance benchmark.

`make syntax` checks a complete file, not a bare expression. Success prints
`Syntax accepted` and exits zero. Lexical or located syntax rejection exits one
from the Node command; setup/invocation failures exit two. Make returns its own
nonzero failure status. Syntax acceptance does not imply semantic validity.

## One canonical grammar, explicit token boundary

The adapter reads all canonical rules. It mechanically translates `<-` to `=`
and lexical hooks to consumption placeholders, then asks Peggy to compile the
original rule graph. That first parser is never used to recognize source. Its
hooks do not independently validate Unicode or lexing.

The executable recognizer substitutes token-kind predicates for lexical entry
rules such as Ident and IntegerLiteral. Grammar terminal strings match complete
token spellings. Peggy input positions represent immutable views of the real
`--inspect-tokens` output. Interpolation executes the canonical expression rule
between real lexer markers. The canonical syntax productions are not duplicated.
Source spelling and decoded string bytes are never confused.

Normally one view represents one token. A `>>` token has two views referring to
the same original token. Only TypeClose can consume one half; the shift terminal
must consume both halves from that same token together. Other terminals cannot
consume a half. Peggy backtracking restores the view position without mutating
the token stream. This permits nested generic closes, preserves ordinary shifts,
and does not turn separate `> >` tokens into a shift or split `>=`. A failure on
the second half maps to that character's original byte offset and scalar column.
Named lexical/terminal rules make predicate failures visible to Peggy diagnostics;
whole syntax rules are not relabeled to conceal deeper errors.

## Evidence and limits

`make grammar-check` compiles all 181 canonical rules and passes 50 readable
source fixtures in cases.json. Four malformed-grammar controls test unknown rules,
left recursion, nullable repetition and unknown hooks. Negative source cases must
fail in the specified lexer/syntax phase; arbitrary crashes do not pass. The
extra-close case also checks its precise byte position. Lexer records are frozen
during recognition. Temporary source files are removed after the run.

This shares the reference lexer: it independently executes syntax rules, not
lexical conformance. Acceptance fixtures alone do not establish operator grouping
or the exact chosen parse tree. Unresolved syntax proposals in the draft remain
proposals; execution does not approve them.

## Lessons from the experiment

The first run passed 38/39 fixtures: adjacent nested closes failed while spaced
closes passed. The author initially called this a missing policy, but a focused
read found that grammar-review.md already required contextual splitting and
backtracking. The executable adapter was incomplete. Bill reaffirmed the rule;
TypeClose now makes its boundary explicit and the adapter implements it.

An initial adapter defect embedded literal braces in Peggy JavaScript code blocks;
Peggy's brace balancing swallowed a generated rule. JavaScript Unicode escapes
fixed that translation without changing token matching. A later location test
correctly rejected extra `>` but exposed an unhelpful byte-zero error: unnamed
predicates had not registered expected-token failures. Terminal labels corrected
that diagnostic; the second-angle location check now passes. Grammar compilation,
recognition, and useful diagnostics required separate checks.
