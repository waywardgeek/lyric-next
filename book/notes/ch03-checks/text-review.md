# Chapter 3 — consolidated text review

## Disposition

The chapter and its two public companion contracts provide a coherent reconstruction roadmap for the current syntax increment. **One required teaching clarification** is identified below; **one optional improvement** would make prefix/postfix construction less ambiguous. Neither requires a new feature, a new code review, or another text-review loop. Incorporate the useful feedback into the original teaching, rather than appending a review diary.

This was one read-only text review. No project files were changed, tests executed, dependencies installed, network used, or work delegated.

## Required finding R1 — account for the JSON writer's own depth limit

**Location:** `book/chapter-03.md:133–139`, especially the deep-tree serialization instructions at line 139.

**Problem:** The chapter correctly distinguishes parser recursion from tree depth and instructs the reader to serialize with an explicit stack. It does not mention that the JSON-writing library can independently reject deep nesting even when traversal uses no recursive calls. A student following that algorithm with the default .NET JSON writer can still fail the existing long-chain requirement.

**Evidence:** `compiler/Syntax/SyntaxNode.cs:80–130` implements the explicit traversal stack, but `SourceFile.WriteSyntax` additionally constructs `Utf8JsonWriter` with `JsonWriterOptions { MaxDepth = int.MaxValue }` at lines 191–198. This is a distinct, consequential configuration step. `graders/ch03/boundaries.py:100–113` requires the complete tree for a 4,000-term expression; the public grader README describes that requirement at lines 60–62. The saved cumulative test log reports the boundary suite passing in Debug and Release. I did not execute a default-writer failure probe during this review.

**Why it matters:** This is not an additional acceptance condition. It explains an implementation choice needed to meet an already published condition, and prevents the reader from mistaking a serialization-library limit for parser exhaustion or invalid Lyric syntax.

**Suggested correction:** Add a sentence beside the explicit-stack instructions: configure any JSON writer's independent nesting limit to accommodate the accepted deep trees; the C# implementation explicitly sets `JsonWriterOptions.MaxDepth`. Explain that the 4,000-term boundary check verifies the complete output, not just successful parsing. Do not prescribe a new arbitrary resource threshold or promise unlimited input handling.

**Lesson:** Parser call-stack protection, iterative tree traversal, and serializer depth configuration solve different failure modes. Fixing one does not establish the others.

## Optional finding O1 — make prefix/postfix binding order explicit

**Location:** `book/chapter-03.md:104–115`, especially “prefix/primary operand, then its postfix chain” at line 106 and the prefix-recursion explanation at line 115.

**Problem:** The section is accurate when read alongside the PEG, but the compressed first sentence could suggest parsing a unary node and then applying postfix operations to that completed node. That would build `(-f)()` for `-f()` rather than the intended `-(f())`.

**Evidence:** `lyric-grammar.peg:147–149` defines prefix recursion with `PostfixExpr` as its base. `compiler/Parser/SourceFile.cs:322–337` recursively parses a prefix operand; only its non-prefix branch parses a primary followed by the postfix loop at lines 338–420.

**Why it matters:** This is a central recursive-descent construction boundary. The linked grammar already supplies the answer, so this is an optional clarity improvement rather than a missing contract.

**Suggested correction:** Say explicitly: “A prefix operator recursively parses another prefix operand. Otherwise parse a primary and extend it through the postfix loop.” One example such as `-f()` or `ref a[i]` can demonstrate why postfix binds inside the unary operand. No additional algorithm or fixture family is requested.

## Assessment of the remaining teaching

- **Problem and scope:** Lines 5–39 explain what syntax adds and what it deliberately does not do. The hello-world tree preserves `print` as an ordinary name; no execution, library availability, numeric-range checking, or semantic approval is falsely claimed. The distinction between the complete *current grammar* and unresolved language design agrees with the specification's parsing section and decision register.
- **Shared model and ownership:** Lines 45–72 clearly identify authoritative source bytes, identity-bearing nodes, span anchors rather than token references, source strong ownership, weak structural edges, and both backlinks. The Common declarations and Syntax implementations agree. Construction uses the source add operation; child attachment validates before mutation and rejects cycles; destruction detaches structural edges before destruction-only source removal. The nontransferable source membership correction is explained with its reason: anchors derive meaning from their original source. The chapter also warns future semantic passes that persistent references require new reverse relationships.
- **Cleanup:** Reverse construction-order source cleanup matches `ClearSyntax`; weak edges do not cascade-delete peers. Parser failure clears the arena, re-lexing clears old syntax, and source destruction clears syntax before tokens. Root access is explicitly limited to a completed successful parse. These explanations do not rely on private authoring conversations.
- **Algorithms:** Cursor state, contextual halves of `>>`, complete checkpoint restoration, and ordinary-syntax-only fallback are explained concretely. Lines 94–100 integrate the code-review corrections into construction teaching: optional type arguments roll back as a unit, dot-plus-identifier repetitions consume only complete units, and speculative helpers do not retain edits to preexisting nodes. Examples agree with the corrected `NamedType`, `QualifiedName`, and generic-suffix paths. Precedence climbing and left association, header restrictions, semicolon retention, declaration/body distinctions, and complete-input consumption are adequately specified when used with the public PEG and shape inventory.
- **Contracts and examples:** The complete chapter and both public companion documents were read. The inventory agrees with the closed 108-kind vocabulary and inspected parser construction paths, including empty wrappers, visibility position, relation endpoint roles, and anchors. Exact diagnostic fixtures are readable public tests, not private rules. The public README discloses the long-chain check's key-order restriction and the ordinary checks' JSON-value comparison policy.
- **Commands:** `make test`, SDK overrides, the separate Peggy comparison, and `make spin03` agree with the Makefile and inspected tool sources. The text distinguishes setup/network installation from ordinary local checking and identifies the extra Node/Peggy prerequisites. The walkthrough's temporary files, real inspection modes, hello-world tree, arithmetic grouping, and rejection-without-partial-output match `tools/spin03.py`. This is source inspection, not a new demonstration that these commands ran.
- **Voice and evidence presentation:** The chapter is direct and instructional, with reasons attached to the design rather than a patch diary. No separate voice guide was supplied or found in the bounded filename search; `book/README.md` provided the public construction-book framing. There are no quoted student questions or answers in the chapter to authenticate or sanitize.

## Evidence inspected and limits

Read the entire `book/chapter-03.md`, `graders/ch03/README.md`, `graders/ch03/tree-shapes.md`, the three requested implementation files (`Common/SyntaxNode.cs`, `Syntax/SyntaxNode.cs`, `Parser/SourceFile.cs`), and the canonical PEG. Read relevant specification sections on precedence, PEG parsing, adopted/open syntax, control flow/patterns, and unresolved decisions, plus grammar-review guidance. Also inspected command definitions, the walkthrough and PEG runner/setup documentation, boundary grader, narrow lexer/source-destruction paths, and relevant development-note evidence. Large clipped tool reports were not treated as complete reads; the substantive grammar and code portions needed for this review were recovered in subsequent bounded reads.

The two requested saved logs support:

- Debug and Release builds with zero warnings/errors and retained Chapter 1/2 results, including 82 source cases, 58 token cases, 99,826 NFC vectors, all-scalar XID classification, and seven malformed Unicode requests.
- 66/66 exact syntax trees, 143/143 syntax recognition cases, and 7/7 parser boundaries in each configuration.
- Peggy compilation of 176 rules, its negative controls, and 143/143 authored grammar fixtures.

The saved mutation summaries support failure of the right-associative mutant (58/59) and semicolon-dropping mutant (57/59). These were historical 59-tree-suite mutation runs, not mutation runs against all 66 final trees. The source-lifetime log reports 11 checks passing. The earlier 22 lifecycle checks are supported here by the development-note account, not a separately inspected raw probe transcript. The chapter appropriately labels these as supplementary rather than retained independent graders.

These are retained reference-development observations, not tests I ran and not exhaustive conformance. Peggy shares the lexer and checks recognition, not tree shape or semantics. No Chapter 3 student reconstruction evidence was provided or found in the reviewed material; the public development status says it remains pending. This review therefore does not establish independent or assisted reconstruction success. No automatic rerun or additional reviewer cycle is requested; further testing and human acceptance remain the author's and human's decisions.
