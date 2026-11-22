# Chapter 7 — single code review

Reviewer: lyric-ch07-code-review-01. One consolidated review submitted; reviewer
shutdown reported stopped=true. Full typed submission remains preserved with the
child. This report records its findings and evidence; it does not claim re-review.

## Required correction: inferred reference preview

Check.cs DiscoverHints (reviewed lines1171–1205) omitted unary ref traversal.
BranchHint (1345–1388) then defaulted an unknown unary initializer to i32.
An explicit ref u8 annotation preserved u8, but the equivalent inferred alias did
not. In a conditional tail followed by +1, an inferred alias to255u8 could widen
arithmetic and print256 instead of overflowing; a u16 destination could also
falsely reject with expected-u16/got-i32. Explicit annotation instead produced the
required runtime LYR001 u8-addition overflow. Reviewer reproduced against existing
Debug/Release binaries with C at O0/O2. A first probe missing --emit-c received
usage exit2; corrected before conclusions. No fresh full suite run by reviewer.

Correction required: side-effect-free preview must propagate the selected
storage's referent type and must not default unknown ref initializers to i32.
Keep reference acquisition, facts and write permissions in the real checker.
Retain annotation-equivalence regressions including narrow overflow and widening.
Lesson: preview selects arithmetic widths and is semantically consequential.

## Preferences, not additional gates

Update compiler/README.md, which still presents Chapter6 only. Consider clearer
named CFG operation vocabulary instead of nested tuples/numeric tags; do not
introduce persistent duplicate state or a framework merely for shorter methods.

## Coverage and evidence limits

Reviewer read full Chapter7 and Chapter6, specification array/ref/binding contracts
(section5 lines457–770), Check.cs and Emission/Compilation.cs, relevant Common
model/syntax/semantic/emission declarations, type/symbol/fact/model/scope bodies,
driver/project/compiler README, full Ch7 grader and shared Ch5 harness. Inspected
literal byte retrieval and all five requested helper verification receipts.

Found no additional definite lifetime or ownership bug. Observed Common declarations,
matching partial implementations, value-like structural types, two-way semantic
relationships, driver finally destruction, source-owned literals, recursive value
copy/release, allocation checks, and normal/control-transfer cleanup. Passing tests
and this inspection do not prove all accepted programs safe. Root+depth sibling
conservatism and excessive read copies were disclosed, not correction requests.
No exhaustive recursive-state exploration or portability proof. No edits, network,
installation, restart, Git, deployment, or reviewer recruitment by reviewer.

## Author follow-through

Twelve public retained alias-preview cases added: scalar/indexed, inferred/annotated,
u16/i64 narrow-overflow destinations plus valid u16 widening controls. Fresh-build
baseline and correction verification will be saved under ch07-checks/review-*.
No second code review will be requested.
