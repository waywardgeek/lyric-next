# Chapter 3 development record

## Scope and decisions

Bill approved a recursive-descent parser, then broadened the initial arithmetic
proposal to hello world, then explicitly requested the entire grammar. Build the
real parser and independent graders before writing the chapter. No semantic
checking, C emission, execution, or language-policy changes are implied by syntax
acceptance. D6 remains a documented proposal: recognition of its existing PEG
syntax does not settle its semantic questions. No Chapter 3 review or student
trial has started.

The independently executable PEG milestone preceded this work. Peggy 5.1.1 is a
pinned local development dependency. The canonical grammar is mechanically adapted
to real lexer tokens; this does not independently test the shared lexer. All 176
rules compile, four malformed-grammar controls fail as intended, and the original
50 source cases pass. The grammar already specified contextual splitting of >>;
Cadence initially misidentified it as missing language policy, then corrected that
assessment on reading L4. The adapter now implements the existing policy, including
restoration on failed speculation and no splitting of >=. That work is uncommitted.

## Shared model

All shared declarations are in Common. SyntaxNode is an identity-bearing class,
not a value-like object. SourceFile strongly owns its arena of syntax nodes,
including unfinished or speculative nodes; node.Source is the backlink. A syntax
parent's ordered Children and node.Parent form a weak two-way structural edge.
Add/remove validates before mutation and maintains both ends. Node destruction
detaches its parent, children, and source; source destruction deletes every node
before deleting tokens. Re-lexing also clears previous syntax. Temporary parser
variables and traversal stacks are not persistent model relationships.

Bill specifically asked whether the data structures already existed, whether they
belonged in Common, and whether ordinary nodes have backlinks to all referents.
Cadence had not explained the tree model sufficiently before implementing it.
Future persistent semantic relationships require their own reverse links; current
Source and Parent backlinks do not excuse untracked future references.

The initial general node stored arbitrary string kinds and token indices. These
were replaced during development with the closed SyntaxKind enum and original
source byte spans for spelling anchors. An integer token index still creates a
token-lifetime dependency; calling it value-like was an inadequate explanation.
Direct source byte regions avoid that relationship and duplicate spelling. Nodes
retain their real source parent. Structural nodes use offset -1/no text. The
read-only ChildCount/ChildAt and SyntaxRoot operations support later passes without
exposing collection mutation. The arena's first node is the inspection root.

The parser creates one general node type with explicit kind/ordered-child shapes,
not a hierarchy of specialized classes. These shapes must be taught and published.
The inspector emits nested {kind,text,children} JSON from the actual tree. It uses
an explicit traversal stack: long left-associative chains can make deep trees even
when parsing itself is shallow. Original literal spelling is not decoded payload;
lexer inspection retains its separate decoded-byte contract.

## Parser construction

SourceFile.Parse first invokes Lex, then uses invocation-local recursive-descent
functions and a cursor (token index, second-half flag for >>). Precedence climbing
implements all binary levels with right threshold precedence+1 for left
associativity. Prefix operators recurse rightward. Generic suffixes are attempted
before comparisons, require a following call/member boundary, and do not consult
name/type resolution. Speculation saves the full cursor and arena length; failure
removes only newly allocated nodes and restores the cursor. Resource failures are
not alternative-selection failures.

Header expressions exclude direct record/dictionary primaries but ordinary
parentheses and call arguments restore the full grammar. Block nodes retain an
explicit discard-result marker when any semicolon follows a statement, even after
newlines. Global bindings do not admit local let-else. Contextual gen can fall back
to a type name when its prefix alternative fails. Function types allow trailing
argument commas. Relation trees retain both endpoint roles and ownership kind.

There is a provisional recursion guard (LYC203, 128 nested parser calls), not yet
fully documented/tested. It is a compiler resource boundary, not a language rule.
Full source-location and stress tests remain to be added. The parser does not yet
have a published Chapter 3 contract or prose chapter.

## Evidence and failures

- Job199: initial parser Debug build, zero warnings/errors, no behavioral evidence yet.
- Job201: 17 independent expected trees passed (hello world, arithmetic, generic
  priority/backtracking, return/newline and semicolon behavior).
- Jobs202/203: expanded expression/declaration builds and those 17 trees passed.
- Job205: fixture-construction Python quote error; no recognition fixture file was
  produced. Dependent commands ran and failed because fail-fast was omitted. Not
  parser-failure evidence. Corrected with proper quoting and fail-fast.
- Job206: 129/130 recognition cases. Authored `f<A><B>()` expectation was wrong:
  GenericSuffix requires ( or . immediately after its arguments.
- Job208: canonical Peggy also 129/130 for that same fixture; written grammar
  confirmed rejection. Retained it as a negative and added `f<A>()<B>()` positive.
- Job207: enum/source-span model build and 17 trees passed.
- Job209: boundary fixes build and 17 trees passed.
- Job210: clean Debug build; hand-written parser 136/136 recognition cases;
  canonical PEG 176 rules, four controls, 136/136 source cases. Full PEG stdout in
  /tmp/lyric-ch03-peg-final.txt is temporary, not a retained publication artifact.
- Job211: added read-only traversal and expanded independent trees; clean Debug
  build and 59/59 exact trees. All recognition cases are retained by default.

These are bounded checks, not full language conformance or semantic safety proof.
Current implementation has not run cumulative Chapter 1/2 regressions since these
changes, Release parser tests, mutation checks, or direct model/lifecycle checks.
No reviewers or students have been launched for Chapter 3. No commit/push.

## Next work

Publish the exact tree shapes, complete diagnostic/resource/lifecycle checks,
review grammar coverage, run Debug/Release and cumulative regressions, and test
plausible defects. Then write the chapter and simple spin03 walkthrough from the
verified result; one code review, one text review, and fresh student reconstruction
follow the established workflow. Do not restart Chapter 1/2 reviews or students.

## Lifecycle and resource milestone

Job217 compiled the actual model/parser sources into a disposable Release C#
probe: 22 relationship/lifecycle checks passed (attachment, refusal atomicity,
cycles, foreign source, weak detachment, reparse, failed parse, source cascade).
This is supplementary evidence, not an independent retained grader.
Job218 successfully printed a 4,000-term left-associated expression and rejected
200 nested groups/300 prefixes with controlled LYC203 and empty stdout.
Retained Python boundaries.py now covers three exact diagnostics, 2,048-deep
groups/prefixes/blocks with controlled LYC203, and the complete 4,000-term tree.
Job222 initially failed its overly strict raw JSON comparison; equivalent escaping
was a grader defect, not parser evidence. Corrected to flat JSON-token comparison
which tolerates whitespace/escaped strings and avoids recursive decoding.
Job223 passed all seven boundaries in Debug; make test passed all Chapter1/2
checks in Debug/Release; Release then passed 59 trees, 136 recognition cases,
and seven boundaries. Both builds had zero warnings/errors.

The public ch03 README and complete tree-shapes.md are now written; make test
now includes all three new graders. Its expanded command verification is pending.
The implementation still needs coverage audit/mutation evidence, teaching, the
single code/text reviews, and fresh student reconstruction. No acceptance implied.

## Teaching and command verification

- Expanded `make test` completed successfully in Debug and Release: clean builds, retained 82 source / 99,826 NFC / 1,112,064 scalar XID / seven malformed Unicode / 58 token checks, plus 59 trees / 136 recognition / seven parser boundaries. Full log: `ch03-checks/make-test.txt`.
- Two disposable, successfully built mutants were rejected by the unchanged tree grader: right-association 58/59; lost semicolon markers 57/59. Reference compiler unchanged. Build and grader logs are retained in `ch03-checks`.
- Chapter 3 teaching draft and `make spin03` now exist. The published walkthrough succeeded: hello-world tree, arithmetic grouping, and located LYC201 with empty stdout on failure. No Lyric execution implied.
- Published Peggy comparison completed successfully; see `ch03-checks/peg-comparison.txt`.
- Expanded 50 dense fixture expressions for readability, verified identical Python AST and reran 59/59 tree checks. Chapter links/fences and whitespace checked.
- Independent code review, text review, fresh student reconstruction, and human acceptance remain pending at this writing.

## Consolidated code review and corrections

The single code reviewer found two PEG-consumption defects and a source-membership invariant gap. Its executable probes were sandbox-blocked, so its findings are source-review evidence. Parent-side prepatch reproduction confirmed that `x as T < y`, `x as T.0`, and `x is T.0` were rejected by the hand-written parser but accepted by Peggy. Three new exact trees failed before the patch and passed afterward. Optional type arguments now use the existing cursor/node checkpoint; dotted-name segments use lookahead for the whole dot-plus-identifier unit. Four positive/rollback controls extend the suites to 66 trees and 143 recognition cases. Final cumulative results follow separately.

Syntax nodes cannot move between sources: source removal is now destruction-only, after parent and child edges are detached. Insertion rejects destroyed nodes. This preserves the source identity that gives byte anchors meaning, without redundant original-source state. A supplementary probe of actual classes passed 11 refusal/destruction checks; it is not a retained C# grader. Root access and serialization are documented for successful completed parses, not intermediate construction. These lessons are integrated into the ownership and ordered-choice construction sections rather than left only here. No second code review requested.

Post-review verification: job 285 completed `make test` with exit zero. Debug and Release each passed the retained source/Unicode/token suites, 66 exact trees, 143 recognition cases, and seven boundary cases; both builds had zero warnings/errors. Job 284 passed 66 exact trees and Peggy 143/143. Saved logs: `ch03-checks/make-test-after-review.txt` and `ch03-checks/peg-review-corrections.txt`. Chapter links/fences and whitespace checks passed. Text review and fresh reconstruction remain pending.

The single text review completed with one required clarification (serializer nesting configuration, independent of iterative traversal) and one optional prefix/postfix-order clarification. Both are integrated into Chapter 3 at their original teaching points. No implementation or grader change; no repeat review. Reviewer inspected source and saved evidence, did not rerun tests. Fresh student reconstruction remains pending.


## Fresh student reconstruction completed

One confined student extended only the accepted Chapter 2 prerequisite. Saved evidence confirms clean Debug and Release builds and all retained checks: 82 source cases, 99,826 NFC vectors, 1,112,064 scalar XID classifications, seven malformed Unicode requests, 58 token cases, 66 exact trees, 143 recognition cases, and seven parser boundaries. The comparison helper self-check passed. Peggy passed all 143 fixtures using the student’s own lexer assembly; spin03 and 18 supplementary real-component lifecycle assertions passed. The parent inspected saved evidence without rerunning the passing student.

The student independently corrected acceptance of raw triple strings in imports and patterns, where ordinary StringLiteral is required. No parser clarification, hidden solution, grader edit, or implementation patch was supplied. The literal-classification distinction is now integrated into the chapter’s expression construction section.

Setup assistance was required: Node’s Homebrew opt aliases were blocked despite granted Cellar libraries. An explicit DYLD_LIBRARY_PATH restricted to those granted libraries enabled loading. Actual JavaScript startup additionally needed OPENSSL_CONF pointing to an empty sandbox-local file for this offline command. No host configuration, access grants, installation, or network use was changed. Lesson: a runtime version probe is not enough; exercise the actual workload early.

Parent SHA-256 comparison confirmed all 201 frozen input files and all 186 noncompiler public files copied by the student unchanged. The student is shut down; implementation and original logs remain preserved in its child directory. Its report and principal logs are copied into ch03-checks/student/. This is one successful reconstruction with setup assistance, not exhaustive proof. Human walkthrough and acceptance remain pending; no Chapter 4, commit, or push performed.
