# Reviews and provenance

These documents preserve evidence and reasons; they are not competing language specifications. The [current specification](../lyric-language-spec.md), especially Section 19, governs current language decisions. The [grammar review](../grammar-review.md) governs the recorded syntax review state.

## Retained reviews

- [Three-source comparison](three-source-comparison.md) compares the original full specification, interface redesign, and a specific earlier Draft 4 revision. Its scope records exact hashes and line counts. It predates the later library upgrades, stable-storage borrows, Unicode decisions, no-shadowing, dot-number tuples, sole .ly extension, safe-generator declaration, and source-based stdlib choices. Its findings are historical; not every listed gap remains open.
- [Standard-library adoption](stdlib-adoption.md) records what was retained, changed, or excluded after reading the original std.ly and string.ly. These are source findings and documented choices, not executed library tests.

The three original documents remain byte-identical under [background](../background/). Local source paths in historical reviews identify what was inspected, not a requirement that another reader have that machine layout.

## Follow-ups preserved from older reviews

Consult Section 19 before treating any item as new work. These details deserve explicit accepted/rejected cases when their relevant chapter is completed; cleanup does not settle them:

- Generated same-class destruction must justify the validity of its active object and continuation across child cascades, rather than silently exempt generated code from lifetime checking.
- Struct relation-endpoint eligibility and field-default evaluation order need an explicit disposition; do not inherit either an old prohibition or a new permission by accident.
- Union flattening/duplicate alternatives and match exhaustiveness in the presence of guards need precise requirements.
- Interface-local free functions, direct impl methods, general interface-alias forms, concrete qualified calls, and field/method name collisions need retain/replace/defer decisions, as detailed in the three-source comparison.
- Custom relation hints need exact validation rules. Actual namespace collision checks versus a blanket one-unlabeled-relation-per-type-pair restriction remain distinct choices.
- Cross-call borrow/effect composition, opaque contracts, finalizer boundaries, package initialization, and concurrent publication need behavior requirements. A chosen checking algorithm is not required in the language specification.

Already-settled matters must not be reopened from old summaries: Result/Status, constructor failure cleanup, stable-storage borrowing instead of path reconstruction, shared/exclusive lifetime locks separate from data guards, explicit safe generators, and the current library choices are in the canonical specification.

## Recovering removed artifacts

Commit `d2cf0c0` contains every file present before this cleanup, including sixteen intermediate drafts, the EBNF grammar, coverage maps, paragraph audits, and individual check receipts. Retrieve an exact file without changing the working tree:

```sh
git show d2cf0c0:spec-draft4-coverage.md
git show d2cf0c0:fresh-spec-review.md
git show d2cf0c0:stdlib-adoption-check.json
```

This keeps provenance recoverable without making obsolete reports look like current instructions. Historical assertions about shipping, compiler behavior, or test results retain their original evidence limits.
