# Chapter 4 — consolidated code review

## Scope and conclusion

Reviewed the bounded Chapter 4 semantic increment, not runtime execution, C generation, or full Lyric semantics. The reviewer role and `coding` instructions were explicitly activated. Project inspection was read-only, through command tools rooted in this reviewer's sandbox. No project files were edited, no reviewers were recruited, and no SDK was installed or compiler execution performed.

**Conclusion:** the principal semantic and ownership design is sound on the inspected paths and is supported by meaningful saved behavioral evidence. One introduced robustness issue requires correction: expression checking has unbounded native-stack recursion and repeated subtree hint scans on parser-supported arithmetic chains. The remaining recommendations below are optional evidence/maintenance improvements, not new chapter features or a further review gate.

## Required correction

### R1 — Bound semantic traversal depth and avoid repeatedly rescanning arithmetic subtrees

**Category:** introduced implementation robustness defect; medium priority. Static finding, not a reproduced runtime crash.

**Locations:** `compiler/Semantics/Check.cs:465–507` (`DeterminedIntegerType`), especially 493–504; `Check.cs:526–532` (`CheckExpression` binary case). Compare `compiler/Parser/SourceFile.cs:299–318`, `compiler/Syntax/SyntaxNode.cs:83–134`, and `graders/ch03/boundaries.py:100–113`.

The parser builds equal-precedence chains in a loop, making `1 + 1 + ...` shallow in parser recursion but deep in its resulting left-associated tree. Chapter 3 explicitly accommodates that distinction: syntax serialization uses explicit traversal frames, and the retained boundary grader constructs a 4,000-term chain.

The new semantic checker recursively descends that left spine twice in different ways: it discovers hints recursively, then checks the left expression recursively. At each binary node it starts another hint scan over the remaining left subtree. For an n-term literal chain this produces quadratic hint-walk work (roughly n + (n−1) + ... visits) and native-stack depth proportional to n. The parser's nesting limit does not constrain this input shape. Sufficiently long expressions therefore exhaust the checker stack rather than producing controlled compiler behavior; shorter ones incur avoidable repeated work. The source-level subset is ordinary supported integer addition, not an unsupported language feature.

**Correction:** make the semantic traversal safe for these parser-produced trees and avoid repeated full-subtree hint discovery. Preserve the existing contract: hints are read-only and do not create facts or evaluate operands; actual checking visits left before right, creates each checked fact once, and checks each intermediate overflow. An explicit traversal/work-stack approach fits the existing syntax traversal precedent. Any temporary hint results should remain algorithm work, not a second authoritative persistent semantic model. If an implementation resource boundary is chosen instead, document and diagnose it deliberately rather than relying on native stack exhaustion; do not weaken the retained Chapter 3 inspection contract.

**Verification:** add a focused check-mode regression wrapping a long chain in `func main() { let total = ... }`, with a small nonoverflowing result (zeros are convenient), and run Debug and Release. Include both an unsuffixed/contextual chain and a typed-neighbor variation. Retain the existing arithmetic overflow, neighbor-context, and branch suites. This recommendation does not assert that 4,000 terms currently crash: no threshold or timing was measured in this review. The unbounded recursion and quadratic repeated walk are directly visible in the source.

## Findings that support the implementation

### Architecture and authoritative state

- `compiler/Common/SemanticModel.cs:6–179` declares the persistent semantic state, relationships, constructors, and cross-module operations. `Common/SemanticTypes.cs:3–44` explicitly makes type identities value-like and declares pure type operations. Matching Semantics partial implementations compile with the Common parts in one assembly (`compiler/Lyric.csproj:3–8`). I found no runtime service glue, copied signature array, or dependency on another spoke's implementation-only helper in the reviewed semantic paths.
- Function signatures use `SemanticSymbol.Type` plus ordered parameter symbols in `FunctionScope`, accessed by `ParameterCount`/`ParameterAt` (`Semantics/FunctionScope.cs:30–57`). Calls resolve ordinary symbols, including print/println; their spelling is not special-cased in call checking (`Semantics/Check.cs:82–87,588–622`).
- The three passes collect user names, construct signatures, then check bodies (`Check.cs:88–180`). Local registration follows initializer checking (`328–370`). Definite and possible assignment remain temporary symbol-identity sets rather than persistent flags on declarations (`71–74`).

### Relationships, destruction, and invalidation

- Analysis/scope/symbol/fact constructors register through relationship methods and unwind later attachment failures through normal destruction (`Semantics/SemanticModel.cs:88–105`, `SemanticSymbol.cs:48–64`, `SemanticFact.cs:33–50`, `FunctionScope.cs:7–27`). Admission checks precede ordinary relationship mutation and add/remove methods update both ends.
- Facts detach symbol uses and syntax backlinks before leaving analysis (`SemanticFact.cs:53–63`). Analysis destroys facts before scopes and rereads the collections as children remove themselves (`SemanticModel.cs:41–58`). Symbol destruction retires its function-owned scope, declaration link, and dependent facts (`SemanticSymbol.cs:67–82`). Scope destruction removes the function owner and lexical parent, detaches weak lexical children without deleting them, destroys owned symbols, and leaves the analysis (`SemanticModel.cs:137–154`). This respects the deliberately multiply owned function scope rather than treating the graph as a tree.
- Model and syntax mutations retire analysis through Common-declared operations. Source removal validates membership before invalidation; successful source attachment invalidates after attachment (`compiler/Model/Model.cs:11–35`). Syntax edge operations validate membership/cycles before clearing analysis (`compiler/Syntax/SyntaxNode.cs:19–51`); node destruction and registration also invalidate (`64–80,141–150`). Adding semantic annotations does not accidentally clear their own analysis. Relexing clears syntax first (`compiler/Lexer/SourceFile.cs:11–23`).
- These observations establish inspected implementation behavior, not exhaustive ownership proof. The noted 62 lifecycle checks are supplementary historical evidence; their disposable probe source and output were not inspected or rerun here.

### Types, flow, diagnostics, and teaching quality

- Range inclusion, not current variable contents, controls integer widening; exact BigInteger boundaries avoid host overflow (`Semantics/SemanticTypes.cs:59–112`). `string` and `[u8]` share ByteArray identity (`Common/SemanticTypes.cs:3–21`, `Check.cs:400–416`). Direct negative integer literals apply the sign before range checking (`Check.cs:418–463,520–525`). Constant arithmetic checks each operation in the selected operand type (`526–557`), not the destination type.
- Branch alternatives start from the same incoming assignment state. Continuing alternatives intersect definite assignment and union possible assignment; returning paths are excluded. Block-local flow entries expire without destroying persistent declarations (`Check.cs:193–295`). Assignment targets are resolved storage facts rather than uninitialized reads (`372–398`). The implementation and comments clearly teach why those distinctions matter.
- The driver preserves the source/lex/syntax inspection boundaries and calls semantic checking only for the two new modes. Successful checks return silently; diagnostics use the existing filename-safe format; compilation cleanup is in `finally` (`compiler/Driver/Program.cs:47–95`). Located semantic failures clear partial analysis before returning their value-like diagnostic (`Check.cs:183–186`). CRLF location handling relies on already validated source, which forbids lone CR.
- The chapter's ownership explanation, read-only hint distinction, literal-versus-typed-value rule, and continuing-path examples accurately describe these paths. The source has readable control flow and useful local rationale. I found no reason to request a general decomposition framework or more abstraction merely because `Check` is a long method with local helpers.

## Public graders and saved evidence

`graders/ch04/check.py:9–88` contains 77 independent source expectations. Its runner (`91–150`) requires the expected exit status, empty stdout, silent success, or an exact diagnostic with a correctly decoded filename and independently calculated byte/line/scalar-column position. A crash, timeout, unrelated rejection, or success with output cannot satisfy the expected result. This is real black-box compiler testing, not mocked internal-call expectations.

`Makefile:28–40` builds Debug and Release and runs the comparison-helper self-check and all retained Chapter 1–4 graders, stopping on failure. `tools/spin04.py:9–48` invokes the real compiler, checks three silent successes and the intentional unassigned-read rejection. It explicitly does not execute Lyric output.

Inspected saved evidence:

- `book/notes/ch04-checks/branch-flow.txt:1–200`: comparison self-check; clean Debug and Release builds; each configuration reports 82 source cases, Unicode NFC/XID and seven malformed requests, 58 token cases, 66 syntax trees, 143 recognition cases, seven parser boundaries, and 77 semantic cases passing.
- `branch-final.txt:1–16`: final readability rebuilds, zero warnings/errors, and 77/77 semantic cases in Debug and Release. This file is not a second complete cumulative run.
- `spin04.txt:1–38`: successful published walkthrough, including intended LYC307 at line 4, column 13, byte 78. It is agent-run evidence, not Bill's human acceptance.
- `mutations.txt:1–47`: three mutants built successfully and were rejected by the grader: definite union (76/77), omitted possible union (76/77), and omitted constant overflow rejection (73/77). These demonstrate discrimination for those specific behaviors, not completeness of the suite.
- `book/notes/ch04-development.md:64–71,96–101`: supplementary model/lifecycle evidence, including the 62-check claim and a corrected probe compilation failure. This is not substituted for retained public tests.

## Optional suggestions

1. **Retain a small reproducible lifecycle probe.** The public CLI cannot expose all graph invariants. Preserving the already-developed real-object refusal/constructor-unwind/destruction checks would make the valuable lifecycle evidence repeatable. This is a maintainability recommendation, not a claim that the current chapter promised those probes as public graders.
2. **Broaden contract-focused source coverage when maintaining the suite.** `graders/ch04/check.py:9–88` does not directly demonstrate several already-stated contracts, including `[u8]`/string interchangeability, f32-to-f64 signature widening, a parameter/local colliding with a later top-level function, generic/loop/reference rejection, direct self-initialization, and typed direct-call result hints. Small positive/negative examples would protect these contracts without adding features. The current 77 tests are meaningful, but passing them is not proof of every advertised boundary.
3. **Keep walkthrough checks distinct from graders.** `tools/spin04.py:45–47` checks a diagnostic substring rather than the complete located diagnostic. That is adequate for this labeled demonstration because the public grader separately checks exact locations; do not describe it as an independent exact-diagnostic oracle.

## Evidence limits and baseline distinction

Read the full Chapter 4 teaching text, public grader README and source, Common semantic declarations, all Semantics implementations, Model/Syntax/Driver integration, Makefile and spin helper, relevant Chapter 3 prerequisite/resource material, and language-spec passages on declarations, types/unit/string, no-shadowing/assignment, ordinary parameters/control flow, numeric context/widening, standard display and executable entry.

No compiler was run in this review. Saved evidence is historical and is not independently reproduced here. No runtime, C backend, full-language support, or human acceptance is inferred. The working tree includes uncommitted Chapter 3 and Chapter 4 material, while HEAD is the Chapter 2 commit, so git HEAD is not a clean accepted-Chapter-3 comparison point. R1 is specifically in the newly introduced Semantics traversal; it does not allege a regression of the old inspection modes. The retained cumulative results support preservation of those earlier observable contracts.

**Lesson:** parser recursion depth and semantic-tree depth are different quantities. A safe iterative parser and serializer do not make a later recursive tree walk safe. Review every new pass against the actual shapes the previous pass can produce.
