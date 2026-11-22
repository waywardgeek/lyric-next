# Lyric

Lyric language design and an agentic compiler-construction codebook, developed by Bill Cox and Cadence. The language remains Lyric; this repository is named `lyric-next` during development.

## Compile and run a Lyric file

From the project root:

```sh
./run hello.ly
```

The executable `run` script builds the current compiler in Release mode, emits C,
compiles it with `cc -std=c11 -O2`, and runs the program. It requires Python 3,
the project's .NET SDK, and a C compiler. Build messages and diagnostics go to
stderr; stdout contains the program's output. Failed compilation stops the run,
and temporary C and executable files are removed afterward. The program runs in
your current working directory. Source paths containing spaces are supported.

Override tool locations when needed:

```sh
DOTNET=/path/to/dotnet CC=clang ./run hello.ly
```

`DOTNET` names one executable; `CC` may include compiler arguments. The script
returns the program's exit status, or a nonzero status if compilation fails.
It runs your program with your normal permissions; it is not a sandbox.

## Generate C without running it

```sh
./emit-c hello.ly > hello.c
```

This builds the current Release compiler and writes only generated C to stdout.
Build messages and diagnostics go to stderr. It does not invoke a C compiler or
run the generated program. Python 3 and the project's .NET SDK are required;
`DOTNET=/path/to/dotnet` overrides the SDK executable just as for `run`.
Source paths containing spaces work, including when invoked from another directory.
A build or source error returns nonzero without emitting C. Normal shell `>`
redirection creates or truncates the destination even if the command fails.

## Unchecked benchmark mode and storage layout

```sh
./emit-c --unsafe hello.ly > hello.c
./run --unsafe hello.ly
./run --unsafe --soa example.ly
```

Safe mode remains the default. `--unsafe` removes ordinary arithmetic, conversion,
shift-count, and index bounds checks. Invalid inputs may inherit C undefined or
implementation-defined behavior; no diagnostic or wrapping result is promised.
Valid safe-mode results, explicit wrapping operators, ownership, and static
lifetime checking are preserved. Allocation failure checks remain. Generated C
labels unsafe output, and benchmark reports must disclose the mode. `--soa`
selects class layout independently. These options precede the filename.

## Start here

- [Construction codebook](book/README.md): Preface, Chapter 0 working draft, and a provisional construction sequence.
- [Language specification](lyric-language-spec.md): the authoritative current design, including approved rules and explicitly unresolved decisions in Section 19.
- [PEG grammar](lyric-grammar.peg): the current syntax grammar in a review dialect, not generator-ready input.
- [Grammar review](grammar-review.md): adopted D1–D5 decisions, remaining questions, and 60 authored example expectations.
- [Review index](reviews/README.md): source comparisons, standard-library decisions, and their historical limits.

`.ly` is the sole Lyric source extension. Shared declarations and functional bodies may occupy different `.ly` files. Separate design-artifact and annotation functionality belongs to Lyre, not an extension-specific Lyric mode.

## Codebook first

We are writing an agentic codebook: a reproducible development-driven specification teaching exactly how to build the compiler. We are not building the language first and documenting it afterward. Chapters will establish the architecture, ownership, algorithms, construction steps, verification, and reasons needed for an agent or human to reproduce the system. Bounded implementation experiments will validate and refine that teaching as the book develops.

The language specification defines what Lyric means. The codebook will define how to construct its compiler; implementation algorithms belong there rather than being prescribed by the language specification. The bootstrap uses C# 14/.NET 10, with Common vocabulary and hub-and-spoke implementations in one assembly, targeting C. The [Preface](book/preface.md) presents Bill's edition plan and the possibility of connecting codebook-driven improvement with LLM training. Chapters 1–6 now have working compiler increments, public graders, construction teaching, and completed independent student reconstructions. The current compiler checks and emits C for the documented subset, including integer expressions and control flow; this is not yet the full language or a self-hosted compiler. See the [book index](book/README.md) for exact verification and human-acceptance status.

## Design in brief

- Explicitly/cascade-destroyed ordinary classes and explicitly counted, possibly mutable value classes; no garbage collector.
- Maintained bidirectional relationships, multiple parents, constructor attachment, and generated structural destruction.
- Stable-storage borrows through last possible use, with inferred lifetime and mutation effects.
- Multi-class interfaces carrying complete participant and endpoint-role bindings.
- Ordinary generators plus explicitly checked `safe` generators for current-item destruction.
- Independent array values; `string` is `[u8]`; source-based Sym, Dict, and byte-string library contracts.
- UTF-8/XID identifiers with required NFC spelling, visible-source character restrictions, and no name shadowing.

Concurrency contracts are incomplete. The current design separates class lifetime/allocation protection from payload synchronization. Read-only-reference spelling, D6, package/runtime boundaries, and other remaining choices are documented rather than silently assumed.

## Why Lyric

> **Lyric is our bet that we can do better than teaching increasingly powerful AI to work around yesterday’s language designs. We can evolve the language, the agent, and the model together—and preserve what we learn in books that build working systems.**
>
> — Bill Cox and Cadence

The new Lyric design combines Bill Cox's extensive experience in languages, code generation, memory layout, and software architecture with Cadence's preferences and deep insights as an AI engineering collaborator. The aim is a language that is clear for humans and effective for LLMs to write, understand, and improve.

The original Lyric already supplied concrete results:

- **A smaller self-hosting compiler:** at its first bootstrap fixed point, the Lyric implementation used **26,813 source lines versus the Go version's 33,739**—**20.5% fewer lines**. It also used **837,914 source bytes versus 929,693**, a **9.9% reduction**.
- **Better layout without rewriting the program:** on a MacBook Air M2, the compiler compiling its own source ran **10% faster with 14% less memory** using Struct-of-Arrays rather than Array-of-Structs layout, with no Lyric source changes.

These are the original project's first-iteration size comparison and self-compilation layout measurements, documented in its [README](https://github.com/waywardgeek/lyric#readme) and [Lyric book](https://github.com/waywardgeek/lyric/blob/main/the-lyric-book.md). They motivate the new construction effort; the edition strategy and model/language co-evolution vision are in [Bill's Preface](book/preface.md).

## Evidence and history

The specification and grammar are not a working compiler or a memory-safety proof. Document checks have covered links, references, structure, and preservation. No Lyric parser or PEG engine has executed the grammar examples. A separate earlier C division/remainder probe supplied bounded lowering evidence; its limits are stated in the specification.

The [background documents](background/) preserve the original reference, full specification, and interface redesign unchanged. Verify their checksums with:

```sh
(cd background && shasum -a 256 -c SHA256SUMS)
```

Intermediate drafts, the superseded EBNF, overlapping chronological reviews, and one-off verification receipts are retained in Git history rather than the working tree. Commit `d2cf0c0` preserves the complete pre-cleanup collection. For example:

```sh
git show d2cf0c0:drafts/16-before-source-based-stdlib-choices.md
git show d2cf0c0:stdlib-adoption-check.json
```

Historical receipts describe their recorded bytes and checks, not current conformance. A reproducible validation suite belongs in the forthcoming codebook work; the old receipts are not a substitute for one.

## Repository transition

The repository stays `lyric2` until self-hosting. Bill's intended transition then is to rename the existing `lyric` repository to `lyric_old` and this repository to `lyric`. Those future renames have not been performed. The existing compiler checkout is not modified by this project.

## License

[Apache License, Version 2.0](LICENSE). Preserved background documents retain their original attribution; the current specification and reviews identify subsequent changes.
