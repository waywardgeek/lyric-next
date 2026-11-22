# Chapter 3 — consolidated code review

Project: `/Users/bill/projects/lyric-next` (read-only import).
Reviewer: lyric-ch03-code-review. No project edits, installations, delegates, student trials, or re-review campaign.

## Conclusion

**Required corrections remain.** The implementation has the intended Common/spoke structure and a substantial, readable full-grammar parser rather than a fixture-shaped calculator. Its published checks are meaningful bounded evidence. Nevertheless, two PEG rollback boundaries reject valid current syntax, and the source-membership API permits an existing syntax edge to violate its same-source invariant.

The author should incorporate these findings and verify the corrections with retained tests. This report does not create another review gate.

## Required corrections

### R1 — Preserve failed optional type arguments at the cast/comparison boundary

**Implementation correctness; high priority.**

Locations: `compiler/Parser/SourceFile.cs:1061–1072` (`NamedType`), called from `:374–378` (cast suffix), through `:112–143` and `:168–211`. Contract: `lyric-grammar.peg:107–118,140,148–157`; `lyric-language-spec.md:225–235`.

`NamedType` sees `<` and unconditionally invokes `TypeArguments(false)`. The PEG instead specifies `NamedType <- QualifiedName (TypeArgs)?`. Failure of the optional TypeArgs must restore the position and retain the successfully parsed qualified name. This is observable, not merely a diagnostic preference:

```lyric
x as T < y
```

The correct syntax is a `<` binary expression whose left operand is `cast(x, T)` and right operand is `y`. The current implementation tries to finish `T<y`, reaches EOF expecting `>`, and rejects the whole expression. A complete type-argument list still takes priority when it succeeds; this correction must not reinterpret successfully selected syntax after a later failure.

**Evidence:** direct source trace establishes the failure. The author additionally reported independently reproducing rejection by the current Release hand parser and acceptance by Peggy for this exact input while the source remained unchanged. That execution was author-reported, not run successfully in this review sandbox.

**Required outcome:** preserve the PEG optional-list rollback semantics, including both cursor components and speculative node cleanup. Do not attach pre-checkpoint nodes to speculative wrappers before success. Add an authored recognition case and exact expected tree for this boundary, with a valid generic cast control and a failed nested-angle attempt that exercises restoration of the half-`>>` state. Extend the teaching at `book/chapter-03.md:90–100` to explain that PEG optionals, not only named ordered alternatives, can require rollback.

### R2 — A failed qualified-name segment must leave the dot for a postfix tuple selector

**Implementation correctness; medium priority.**

Locations: `compiler/Parser/SourceFile.cs:423–431` (`QualifiedName`) and `:1063–1068` (`NamedType`); consumers `:374–378` and `:411–414`. Contract: `lyric-grammar.peg:112–113,148–158`; tuple token boundary in `grammar-review.md:61–63`.

Both loops consume `.` before requiring an identifier. PEG's repeated sequence `("." Ident)*` restores the position if the whole sequence fails. A tuple-index token is not an Ident, but it is valid in the enclosing MemberSuffix. Consequently both expressions below are syntactically valid:

```lyric
x as T.0
x is T.0
```

The first is member `0` of a cast to named type `T`; the second is member `0` of the variant-test expression `x is T`. Their eventual type validity is outside Chapter 3. The hand parser currently fails at `Name()` after consuming the dot. Qualified identifier suffixes such as `T.member` must continue to be consumed as part of the qualified name, not turned into outer member access.

**Evidence:** direct source trace, plus the author's independent report that current Release rejects both while Peggy accepts both. Local reviewer execution was blocked as documented below.

**Required outcome:** consume a qualified-name segment only when the complete dot-plus-identifier sequence succeeds, with refusal leaving cursor/model unchanged. Apply this consistently to both paths. Add exact trees and recognition cases for both examples and a qualified-identifier control. Teach repetition-sequence rollback alongside R1; requiring users to parenthesize these expressions would silently tighten the current grammar.

### R3 — Source detach/attach must not bypass the same-source syntax-edge invariant

**Model API correctness; medium priority; not a failure observed in the current parser path.**

Locations: `compiler/Syntax/SyntaxNode.cs:136–157` (`AddSyntaxNode` / `RemoveSyntaxNode`), contrasted with `:19–37` (`AddChild`) and `:62–75` (`Destroy`). Shared declarations: `compiler/Common/SyntaxNode.cs:120–146,151–158`. Model contract: `book/chapter-03.md:56–70`.

`AddChild` correctly refuses foreign-source edges, but source removal checks only the membership being removed. The following sequence uses declared relationship methods, without direct field/list mutation:

1. Construct live parent `p` and child `c` in source A; call `p.AddChild(c)`.
2. Call `A.RemoveSyntaxNode(c)`.
3. Call `B.AddSyntaxNode(c)` for another live source B.

Every current check accepts this sequence. After step 2, a source-owned tree still retains a child with no source. After step 3, the original parent/child relationship remains intact across different sources, bypassing the invariant enforced by AddChild. An anchored child's existing byte span is now read against B's bytes by `Write` (`:108–115`), potentially producing a different spelling or a range exception. This is a concrete API-level counterexample from the source, not a claim that Parse currently performs such a transfer.

Normal `Destroy` is correctly ordered: it removes the parent edge and all child edges before source membership. The correction therefore need not introduce a general tree-transfer mechanism.

**Required outcome:** make the admitted source-membership operations preserve structural/source invariants before returning. A simple refusal of source removal while structural edges remain fits the current destruction sequence; define and enforce the detached-node/re-attachment contract so original source anchors cannot silently acquire a different meaning. Alternatively constrain membership APIs to the actual construction/destruction lifecycle. Document these preconditions in Common and exercise refusal atomicity directly. Do not require callers to remember unspecified repair work after an admitted operation.

## Positive findings and architecture assessment

- **Common is the declaration hub.** `compiler/Common/SyntaxNode.cs:6–175` contains the closed kind vocabulary, persistent node state, source arena, cross-module operation declarations, and SyntaxFailure data. `Common/Model.cs` contains compilation/source/diagnostic state. Syntax, Model, Parser, and Driver use those concrete declarations; no service bags, duplicate parser owners, virtual adapter layer, or functional-module private dependency was found. The parser's local functions/cursor and serialization frames are invocation-local algorithm state, not hidden persistent declarations. Driver is top-level orchestration with no added persistent model state.
- **Identity and source ownership are explicit.** Syntax nodes are ordinary identity-bearing objects, not records disguised as values. Source spans replace token references and copied spelling. The source arena owns unfinished nodes immediately. Parent/child collections and backlinks form weak structural edges. Diagnostics and SyntaxFailure diagnostic payloads are explicitly value-like; temporary traversal references do not need reverse links.
- **Ordinary relationship mutation is careful.** AddChild checks liveness, shared source, existing parent, and ancestor cycles before mutation (`Syntax/SyntaxNode.cs:19–37`). List insertion precedes backlink assignment. RemoveChild validates actual membership before clearing both ends (`:40–49`). R3 concerns the separate source-membership API, not these correctly implemented edge operations.
- **Destruction and current speculation paths are coherent.** Node destruction detaches weak children rather than deleting them, then source membership. Reverse arena cleanup is iterative (`:159–169`). Source destruction clears syntax before tokens (`Model/Model.cs:60–73`); Lex clears old syntax before re-tokenizing (`Lexer/SourceFile.cs:11–29`). Parse clears syntax on reported syntax/resource failures (`Parser/SourceFile.cs:1436–1466`). Attempt restores index, half-token state, and new-node suffix only for LYC201 (`:237–255`); TryGeneric validates lookahead before the old operand is attached (`:258–274,340–343`). No current speculative helper was found making lasting edits to pre-checkpoint nodes before it can fail.
- **Grammar coverage is broad.** I traced the complete current PEG syntax families through types, patterns, expressions, statements, declarations, interfaces, and implementation bindings. Precedence climbing, left associativity, contextual gen/fn fallback, bodyless functions, role preservation, header restrictions, global/local binding separation, and semicolon markers follow the intended structure. The findings above are interaction defects in optional/repeated boundaries, not missing whole grammar families. Syntax does not pretend to resolve names, enforce lifetimes, approve D6, or execute Lyric.
- **Error/resource boundaries are deliberately separated.** Lexer failures pass through; complete parses require EOF. LYC203 is not swallowed as an alternative mismatch. Recursive expression/type/prefix/pattern/if/block paths are guarded. Iterative serialization handles deep left-associated trees, and output is buffered until serialization finishes (`Syntax/SyntaxNode.cs:78–129,188–195`). Driver uses `finally` for compilation cleanup and narrowly catches source-read errors (`Driver/Program.cs:31–85`). These are not guarantees against arbitrary memory exhaustion, output-device failure, or unlimited-input cost; the chapter does not require such guarantees.
- **Teaching quality is generally good.** Ownership vocabulary, literal anchors, ordering rationale, rollback, and deep-tree serialization are explained near their mechanisms. Statements and cleanup paths are visibly expanded. The large local-function parser is direct and does not need a speculative parser framework to become acceptable. R1/R2 expose a specific missing teaching rule; R3 exposes an underspecified membership contract, rather than a reason to replace the design.

## Graders and PEG adaptation

Inspected `graders/ch03/README.md`, `tree-shapes.md`, all of `trees.py`, `recognition.py`, `boundaries.py`, and the recognition cases. Trees are authored expectations, not output copied at test time from a second implementation. Recognition checks exit/streams/schema and source-consistent diagnostic positions. Boundaries check three exact diagnostics, three controlled resource failures, and a complete 4,000-term tree using a flat JSON-token comparison. Crashes/timeouts are not accepted as normal syntax rejection. Most recognition failures do not prescribe one exact offending byte; exact diagnostic tests cover only the published subset.

Inspected `tools/peg/grammar.mjs`, `check.mjs`, `test.mjs`, and README. The adapter mechanically reads canonical rules, structurally validates them, substitutes explicit lexical token boundaries, and uses immutable two-position views for `>>`. Only TypeClose consumes a half; ordinary `>>` requires both matching views. The tests include four malformed-grammar controls and freeze lexer records. This is genuinely separate syntax recognition but shares the lexer and does not prove AST shape. Its documented lack of a separate PEG execution time/depth guarantee is a limitation, not a new Chapter 3 compiler defect.

No fixture-answer shortcut or demonstrated grader defect was found. Existing coverage did not include the three counterexamples above. Passing 136 recognition cases therefore does not establish complete PEG equivalence.

## Optional suggestions (not additional completion gates)

1. **Retain a small direct lifecycle test.** `book/notes/ch03-development.md:114–117` records 22 supplementary checks but explicitly says the probe is not a retained grader. Keeping the small real-component probe would make the ownership claims reproducible and protect cases such as R3. Do not replace the real model with mocked relationship calls.
2. **Improve usage discoverability.** `compiler/Driver/Program.cs:24–28` accepts four file inspection modes but prints only source/version usage. Listing token and syntax modes would help users; current chapter commands and valid-mode dispatch already work by inspection.
3. **Clarify the scope of SyntaxRoot/WriteSyntax.** `Common/SyntaxNode.cs:165–166` says the root is null before successful construction, while `Syntax/SyntaxNode.cs:183–195` uses the first arena element with no completed-parse marker. This is adequate for the synchronous driver, which calls WriteSyntax only after Parse succeeds. Document that precondition explicitly rather than adding a duplicate root cache or a speculative concurrency protocol.

## Evidence and verification limits

Read the actual chapter, grammar, grammar review, relevant language-specification parsing/control/binding sections, earlier chapter model/lexer contracts, compiler declarations and relevant implementations, public Chapter 3 graders, PEG adaptation, project/build configuration, and development notes/logs. Broad specification output was clipped in some reads; this review does not claim a line-by-line audit of the entire language's unrelated runtime/library semantics.

The retained `book/notes/ch03-checks/make-test.txt:3–100,101–198` shows successful Debug and Release builds with zero warnings/errors and, in each configuration, 82 source checks, 99,826 NFC vectors, all 1,112,064 Unicode scalar XID classifications, seven malformed Unicode requests, 58 token cases, 59 exact syntax trees, 136 recognition cases, and seven parser boundaries. `peg-comparison.txt` reports 176 canonical rules, negative controls, and 136/136 grammar fixtures. Retained mutant build/grader logs show successful mutant builds followed by 58/59 for right association and 57/59 for lost semicolon markers. These are useful sensitivity checks, not exhaustive mutation coverage.

The 22 lifecycle checks are supported here by the development record, not by an inspected retained probe source/run log. I did not rerun the cumulative suite or rebuild the project. Initial PATH lookup found neither runtime. After the parent supplied existing absolute executable paths, this sandbox's .NET process exited 133 before loading Lyric with `Failed to resolve full path of the current executable []`; Node execution was denied with `Operation not permitted`. Reviewer job 261 therefore establishes only a setup failure, **not nine parser failures**. No permission workaround or installation was attempted.

The author separately confirmed all three R1/R2 example outcomes against current Release and Peggy. That confirmation is explicitly attributed; the code-level explanations above are independently grounded in the inspected source and canonical productions. R3 is a source-level API counterexample and was not executed in this sandbox. No claim of full grammar conformance, live semantic execution, human acceptance, or successful correction is made.

## Lessons

- PEG rollback belongs to failed optionals and repeated sequences as well as explicit ordered alternatives; first-token dispatch alone can incorrectly commit valid surrounding syntax.
- Correct backlinks are necessary but not sufficient: every admitted relationship operation must preserve cross-relationship invariants, including the source identity that gives byte anchors their meaning.
- Retained passing examples and mutation controls demonstrate particular behaviors. Source inspection and boundary combinations remain necessary, and runtime setup failures must never be reported as compiler evidence.
