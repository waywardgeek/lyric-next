# Lyric

Lyric language design and an agentic compiler-construction codebook, developed by Bill Cox and Cadence. The language remains Lyric; the new repository is named `lyric2` during development. The current local workspace is `lyric-next`.

Start with [the language specification](lyric-language-spec.md): **Draft 4, with approved safety and library upgrades**. It describes the core design and explicitly identifies the remaining semantic decisions. It is not an implemented compiler or a memory-safety proof.

## Construction approach: codebook first

We are writing an agentic codebook: a reproducible development-driven specification that teaches exactly how to build the compiler. We are not building the language first and documenting it afterward. Chapters will establish the architecture, ownership, algorithms, construction steps, verification, and reasons needed for an agent or human to reproduce the system. Bounded implementation experiments will validate and refine that teaching as the book develops; successful code alone does not replace the reconstructible specification.

The language specification defines what Lyric means. The codebook will define how to construct its compiler; implementation algorithms belong there rather than being prescribed by the language specification. The proposed bootstrap direction is C# 14/.NET 10 with Common vocabulary and hub-and-spoke functional implementations in one assembly, targeting C. No compiler or codebook chapters have been implemented in this workspace yet.

The repository name is `lyric2` until self-hosting is achieved. Bill's intended transition then is to rename the existing `lyric` repository to `lyric_old` and this repository to `lyric`. Those future renames have not been performed.

## Source files

`.ly` is the sole Lyric source extension. Shared declarations and functional bodies may live in different `.ly` files. Separate design-artifact/annotation functionality belongs to Lyre; `.lyric` is not a Lyric source mode. See [verification](single-extension-check.json).

## Core

Explicitly reference-counted, possibly mutable `value class` and explicitly/cascade-deleted ordinary `class`; bidirectional relations; constructors that establish both ends; generated destruction with `final` hooks; inferred class-based destruction invalidation; safe current-object deletion during iteration; stable-storage borrows checked through their last possible use; multi-class interfaces with role-aware relation bindings. No garbage collector. KISS is a requirement. Shared class access permits references to existing objects; exclusive access permits creation/destruction and protects allocator state without another per-class allocator mutex. Payload and relationship synchronization is separate. Region syntax and complete-set acquisition remain proposals, not an implemented runtime.

## Syntax draft

- [PEG grammar](lyric-grammar.peg): active ordered syntax draft based on the original spec and our approved changes.
- [Grammar review](grammar-review.md): lexical conventions, adopted D1–D5 rules and remaining decisions, coverage gaps, and 60 example expectations.
- [Original PEG structural receipt](peg-check.txt): historical pre-dictionary checks. [Upgrade receipt](library-upgrades-check.json) records checks at that historical milestone; neither is a Lyric parser test.
- [Earlier EBNF draft](lyric-grammar.ebnf), [its review](drafts/05-ebnf-grammar-review.md), and [receipt](grammar-check.txt): preserved history.
- [Fresh full-spec review and decision history](fresh-spec-review.md): findings from the full reread and subsequent decisions.

PEG is the authoritative parsing direction, with generic syntax tried before comparisons. It provides the recursive-descent roadmap; generator/runtime selection remains open. The concrete D1–D5 recommendations are adopted. Two D1–D5 details remain open: initial Unicode version and lone CR. Empty symbols are valid. Name shadowing is prohibited; independent scopes and qualified namespaces may reuse names. D6 and other documented coverage gaps remain under review. See [adoption verification](D1-D5-adoption-check.json).

## Reconstruction record

- [Draft 4 coverage and review](spec-draft4-coverage.md): all 119 source subsections accounted for, example corrections, and comparisons with the original specification and interface redesign.
- [Frozen Draft 3.3](drafts/06-before-editorial-reconstruction.md): unchanged source for the reconstruction.
- [Draft 4 reconstruction snapshot](lyric-language-spec-draft4.md): identical to the canonical file at promotion; edit the canonical specification for subsequent work.
- [Candidate check receipt](draft4-check.json) and [promotion receipt](draft4-promotion-check.json): document verification only.

## Unicode identifiers

UTF-8 identifiers use Unicode XID_Start/XID_Continue and must already be NFC-normalized. Names remain case-sensitive, keywords retain ASCII spellings, and `_` remains discard/wildcard. The hard/contextual keyword classification is adopted. The initial Unicode version remains open. Visible builtin names follow the same no-shadowing rule as other names. See [verification](unicode-identifiers-check.json); no lexer/parser execution is claimed.

Literal default-ignorable characters, bidi controls, and non-whitespace controls are forbidden even in comments and raw literals. Ordinary strings can represent such data with explicit Unicode scalar escapes. See [source-character verification](source-characters-check.json); historical Unicode receipts describe their recorded versions.

## Stable-storage borrows

The canonical spec now requires borrowed storage to remain allocated, stable, and valid for its type through the last possible use. It replaces the earlier root/index re-resolution semantics; array and tagged-payload replacement cannot revive old borrows. Whole-array-variable borrows remain distinct from element/range borrows. See [the frozen previous version](drafts/08-before-stable-storage-borrows.md) and [current verification](stable-borrows-check.json). Earlier upgrade/comparison receipts describe their recorded versions, not the current bytes.

## Approved follow-up upgrades

The canonical spec now includes standard many-child relationship APIs, generic equality/order/hash/numeric capabilities, read-only-borrow requirements (spelling still open), concatenation and dictionary literals, expected-type enum constructor resolution, Sym accessors/backtick literals, builder snapshots, and retained streaming-library requirements. These are specification changes, not implemented features.

- [Fresh three-source comparison](draft4-three-source-comparison.md): findings against the exact pre-upgrade Draft 4 version identified in its scope; historical findings are not all still open.
- [Frozen pre-upgrade Draft 4](drafts/07-before-library-and-capability-upgrades.md).
- [Upgrade verification receipt](library-upgrades-check.json).

## Preserved material

- `background/` contains byte-identical copies of the original reference, full specification, and newer interface redesign. `background/SHA256SUMS` records their hashes.
- [Draft 1](drafts/01-reconciled-design.md) preserves the first reconciliation.
- [Draft 2](drafts/02-expanded-proposal.md) preserves the expanded proposal before its KISS review.
- [Draft 3 before the lifetime update](drafts/03-kiss-before-explicit-lifetimes.md) preserves the first KISS revision.
- [The pre-locking draft](drafts/04-before-lifetime-and-lock-rules.md) preserves the version before the latest reference, construction, and concurrency rulings.
- [Specification comparison](spec-comparison.md) maps the original spec and newer redesign to the current draft, with omissions, restored coverage, differences, and completion criteria.

The original `/Users/bill/projects/lyric` tree is unchanged by this work. Historical drafts are background, not competing current specifications.

## Status

No Lyric compiler or runtime has been implemented or tested in this workspace. A separate, bounded C division/remainder lowering probe was performed earlier; it is not a Lyric implementation or general safety proof. Draft 4 integrates the former coverage appendices into the main chapters and collects remaining decisions in Section 19. The reconstruction preserves approved semantics; it does not complete the open contracts or approve every proposed grammar form. New syntax is welcome when it simplifies the language; preserving old syntax is not an overriding goal.

Current [no-shadowing verification](no-shadowing-check.json) records this semantic update. Tuple fields now use `.0`, `.1`, and chained selectors; see [tuple-access verification](tuple-access-check.json). The no-shadowing receipt describes its recorded predecessor.

## Generator deletion permission

Ordinary generators reject consumer destruction of the current yielded object. `safe func ... -> gen T` explicitly grants checked current-only destruction permission. Standard relations expose ordinary `iter()` and safe `iter_removable()` through Collection and RemovableCollection; relation-scope for sugar remains ordinary. See [verification](safe-generators-check.json).

## Source-based standard library choices

String is a transparent [u8] alias. Sym uses counted values retained by a program-lifetime intern table; Dict owns stable entry objects and get returns an entry. Collision handling compares actual keys/names. Existing byte-string utilities and StringBuilder are retained with current value/borrow semantics and corrected edge behavior. See [stdlib adoption decisions](stdlib-adoption.md) and [verification](stdlib-adoption-check.json). Original stdlib files are unchanged; no new library implementation is claimed.

## License

Licensed under the [Apache License, Version 2.0](LICENSE). Preserved background documents retain their original attribution; later drafts and review records identify the reconstruction and subsequent changes.
