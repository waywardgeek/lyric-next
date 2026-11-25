# Consolidated code review — restored original Chapter 10

## Judgment

I found no established compiler correctness or ownership defect in the inspected Chapter 10 paths. The implementation substantially follows the chapter’s separation of canonical type identity, inline layout, ownership traversal, and contained-alias occurrence identity. The supplied fresh Debug/Release completion evidence supports the tested behavior; it does not establish exhaustive lifetime safety or independently reproducible teaching.

One bounded public-grader correction is warranted: canonical inspection validation accepts document shapes excluded by its published schema. Two optional clarity improvements are listed separately. No implementation patch, new feature campaign, or re-review is requested.

## Scope and evidence

I explicitly activated the code-reviewer and coding instructions before inspection. This was the authorized privileged author review of `/Users/bill/projects/lyric-next`, not a student reconstruction review. I made no project edits, ran no cumulative suite, installed nothing, launched no reviewers or students, and used no abandoned reconstruction/archive.

Inspected:

- All of `book/chapter-10.md`, the Chapter 10 grader README, and all six Python runners, including their shared `tuples.py` execution harness; the repository Make test recipe.
- Relevant prerequisite contracts, particularly Chapter 9’s IR ownership, storage/place, publication and cleanup contracts (chapter-09.md:75–220), and Chapter 8’s aggregate ownership, class effects, occupancy and cleanup contracts (chapter-08.md:185–264).
- Common semantic type/model declarations and their canonical type, tuple occurrence, symbol, scope, class/model relationship implementations: `compiler/Common/SemanticTypes.cs`, `Common/SemanticModel.cs`, and `Semantics/{CanonicalTypes,TupleTypes,SemanticTypes,FunctionScope,SemanticSymbol,SemanticModel,ClassModel}.cs`.
- Targeted substantial paths in `compiler/Semantics/Check.cs`: declaration/body/default setup, type queries, contained-reference graph construction and transfer, recursive summary folding, call replacement, joins, branch results, and destructuring. Targeted parser, IR lowering/validation, C emission and inspection serialization paths.
- Revised model descriptions in `tools/common-diagrams/chapter10.json` and source-validation logic in `revisions.py`; historical development notes selectively, not as fresh pass evidence.
- The completed `book/notes/ch10-final-author-verification.md` receipt. It records job 904 exit 0, actual cumulative Make target execution in Debug and Release, and zero build warnings/errors in both. Chapter 10 results in each configuration include canonical 5/5 plus four rejected synthetic corruptions; tuple 22 execution/26 rejection cases; struct declaration/layout 20/20; struct values 34 execution/39 rejection cases; array defaults 12 safe and 12 unsafe execution cases; borrowed structs 20 execution/23 rejection cases. Earlier retained suites also passed. I did not independently rerun these results.

## Required compiler corrections

None established by this inspection.

The following are positive source findings, not merely inferences from passing tests:

1. **Canonical identity is authoritative and collision-safe.** `Common/SemanticTypes.cs` and `Semantics/CanonicalTypes.cs` use the analysis-owned bucket structure and complete-key comparisons, rather than display names or a second authoritative cache. Array keys use canonical element identity; tuple keys preserve order and repetition. `Semantics/TupleTypes.cs` represents tuple component occurrences explicitly, with both the tuple relationship and component-user relationship. Removing a tuple’s positions does not destroy shared component types. Validation precedes relationship mutation in the inspected attachment paths.

2. **Lifecycle direction agrees with the revised model.** The canonical and semantic relationship implementations distinguish owned identities, dependent users, and weak IR references. Destruction retires derived users and detaches reverse relationships without treating shared ingredient types as tuple-owned objects. The diagram changes at `chapter10.json:397–484` describe those distinctions, field-owned initializer scopes, and the reciprocal separate-implementation syntax link. I found no reason to redraw historical diagrams as though they described the current source.

3. **Field defaults and separate methods preserve actual owners and declarations.** `Parser/SourceFile.cs:1073–1088` retains a field initializer in the field syntax. The inspected semantic setup gives it a field-owned initializer scope rather than an unrelated copied service context. Separate method bodies retain the original declaration identity with the implementation relationship, rather than introducing a second callable declaration. The related Common declarations make these persistent relationships visible.

4. **The alias analysis preserves occurrences and history.** The temporary flow tables in `Semantics/Check.cs` are construction/analysis data, not additional persistent model ownership. Inspected transfer paths preserve invalidation causes and storage expiry on copied values, distinguish live referent-slot reads from saved values, use finite ancestry folding for recursive formal storage, and compute call outputs before applying replacement writes. At joins (`Check.cs:1559–1664`), target sets and per-occurrence storage sets are unioned independently; inconsistent occurrence counts fail explicitly. Branch results share result slots (`2893–2932`) rather than reacquiring fresh validity after cleanup. Destructuring (`2941–3010`) installs all named aliases before discarded owners are released, so resulting finalization effects can invalidate those surviving copies.

5. **Lowering and emission preserve the relevant ordering distinctions.** The inspected aggregate paths in `IR/Lowering.cs:720–1030` keep evaluated explicit values alive while omitted defaults execute, with ownership transfers afterward. `IR/Validation.cs:445–585` checks the corresponding aggregate operations. `Emission/Compilation.cs:899–1040,1322–1420` separates discovering types from ordering inline definitions and does not copy/drop borrowed referents as owned contents. These distinctions agree with the prerequisite IR contract rather than creating a second cleanup policy in the emitter.

6. **Inspection exposes identity rather than repairing it.** `IR/Inspection.cs:36–48,149–175` maps actual canonical objects to document-local integer IDs and writes the component array for every type. It does not deduplicate types by their displayed name to manufacture a canonical-looking report.

## Required grader correction — schema acceptance gap (low severity)

**Locations:** `graders/ch10/README.md:22–30`; `graders/ch10/canonical.py:12–47,97–104`.

The published contract requires document-local integer IDs and an ordered `componentTypeIds` list, empty for non-tuples. The validator instead:

- Uses `record.get('componentTypeIds', [])` for non-tuples at line 47, accepting an absent field; its own valid selfcheck document omits that field at lines 99–102.
- Checks falsiness rather than list shape for non-tuples, so values such as null can pass that check.
- Uses IDs as dictionary keys and links without checking that they are integers. Consistently substituted string IDs can satisfy these identity/link checks despite violating the stated schema.

**Consequence:** a reconstructed compiler can pass this grader while failing the already-published JSON contract. This is not a demonstrated compiler-output defect: the inspected serializer writes integer IDs and always writes an array.

**Correction:** align `verify` and its valid selfcheck documents with the existing schema, enforcing required fields and their stated types, and add small malformed-document controls for these omissions/type substitutions. No new language behavior or expanded compiler feature is needed. If using Python integer checks, exclude Boolean values, which are a subclass of `int` but not the intended JSON identifier contract.

The current four corruption controls still provide useful evidence for the defects they actually test; this gap does not invalidate the reported compiler executions or imply they failed.

## Teaching and optional clarity improvements

1. **Clarify repeated canonical selfcheck execution.** `book/chapter-10.md:389` says the selfcheck “above” is separate from the Make target. The explicit standalone invocation is indeed separate, so I do not classify that sentence as an established false claim. However, `canonical.py:132–134` also runs `selfcheck()` unconditionally on normal grader invocations, including the Make recipe. A short clarification would prevent readers from concluding that Make omits the corruption selfcheck. My earlier running commentary characterized this too strongly; this consolidated assessment supersedes it.

2. **Optional readability improvement for lifetime events.** `Semantics/Check.cs` documents its numbered event vocabulary around lines 690–705, but numeric event tags remain difficult to follow through the large builder/solver. Named constants or a small enum could make producers and dispatch easier to cross-reference. This is a maintainability preference, not evidence of faulty semantics and not a request for a new analysis framework or persistent relationship model.

Otherwise, the inspected chapter and local source comments explain consequential distinctions well: ownership is not layout; canonical equality is not occurrence equality; saved values must retain invalidation history; and generated cleanup is an effect even when no source call spells it out.

## Limits and lessons

- This was targeted static review plus inspection of the supplied completed execution receipt, not independent runtime reproduction, a sanitizer run, or exhaustive examination of every compiler path.
- Historical lifecycle/model probes were not freshly replayed. Public JSON output cannot prove collision handling, internal pointer identity, or complete destruction; those conclusions require source inspection and remain bounded by the reviewed paths.
- The runtime suites exercise actual compiler emission and compiled C at their documented optimization/layout/mode combinations, not merely mocked internal calls. Their coverage is nevertheless finite.
- No setup failure was established. The no-restore wrapper in the supplied final run is explicitly disclosed; it is not an undocumented change to repository build policy.
- A student reconstruction has not been performed by this review, so I make no independent reconstruction-success claim.

**Lessons retained:** canonical type identity must not deduplicate lifetime occurrences; two slots with the same type can still designate different storage. Also, test-contract validation and implementation correctness are separate obligations: consistent identity links do not automatically establish a valid serialized schema.

This is the single consolidated code review. The author can incorporate the bounded grader correction and decide the optional prose/readability changes without an iterative approval loop.
