# Chapter 11 — consolidated privileged reference code review

## Scope and conclusion

Reviewed the uncommitted Chapter 11 increment in `/Users/bill/projects/lyric-next` against accepted Chapter 10 commit `0ea3d8e617bbb3c15680b7fabd2e7df415ced1dd`. This is the single authorized reference-code review, not a reconstruction review or an additional approval gate.

**Five required corrections were found:** three user-facing semantic/diagnostic failures and two specialization-model invariant failures. The broad passing evidence is valuable, but the specific counterexamples below fall outside that coverage. No implementation, grader, manuscript, or diagram was edited; no commit, installation, reconstruction, additional reviewer, or broad regression rerun was performed. Disposable diagnostics used existing compiler assemblies; the source-linked model diagnostic built in a temporary isolated directory without changing shared build outputs.

## Required corrections

### 1. Unknown explicit generic calls crash instead of producing the unknown-name diagnostic

**Classification:** implementation defect; missing negative regression.

**Location:** `compiler/Semantics/Check.cs:5624–5632`.

The lookup correctly extracts the name child from a `Generic` callee, but its failure message calls `Text(callee)` rather than using that name node. The generic wrapper does not have the required source-text anchor.

**Evidence:** an unknown call `absent<i32>(7)` produced an unhandled `ArgumentOutOfRangeException`, exit status -6, in both existing Debug and Release assemblies. The failure occurs while formatting the intended LYC301 error.

**Consequence:** ordinary invalid user input aborts the compiler rather than yielding a controlled semantic rejection. This also prevents a rejection grader from demonstrating the intended diagnostic contract.

**Required outcome:** use valid source/name information for this error path and retain a regression that requires the expected diagnostic, not merely a nonzero process exit.

### 2. `typeof(parameter)` result signatures do not preserve separately declared method scopes

**Classification:** implementation/integration defect; cross-chapter coverage gap.

**Locations:** `compiler/Semantics/Check.cs:185–215`, especially parameter creation at 196–210 and declaration result resolution at 215; implementation-result resolution at `465–491`, especially 482.

Chapter 10 permits implementation parameters to have different names from their declarations. Chapter 11 permits parameter-based `typeof` results. The new signature path does not compose these contracts: it populates the parameter scope with implementation-side names before reading the declaration-side result, and later reads the implementation result in `function.Scope`, rather than its parameter scope.

**Evidence:** a declaration shaped as `struct S { func f(x:i32)->typeof(x); }` with implementation `func S.f(y:i32)->typeof(y) { return y }` was rejected with LYC301 in both Debug and Release. Using the same parameter name allows declaration resolution to proceed, but implementation-result resolution still fails because its scope does not contain that parameter.

**Consequence:** valid separately implemented methods lose the new result-type feature; renamed parameters additionally break declaration-side lookup. This is not an unsupported generic-method request: the method itself is nongeneric.

**Required outcome:** preserve the appropriate declaration-side and implementation-side parameter name environments while comparing their resolved canonical result types. Keep regressions for both identical and renamed parameter spellings, including a genuine mismatch rejection.

### 3. Implementation result annotations can invalidate the signature traversal

**Classification:** implementation defect; missing signature-discovery regression.

**Location:** `compiler/Semantics/Check.cs:458–493`.

The first signature pass uses an index traversal explicitly because a `typeof` operand can discover specializations. The following implementation-result validation still uses `foreach (SemanticSymbol function in functions)`. Its `ReadType` call can perform the same discovery and append to that list.

**Evidence:** a nongeneric separate method declared with result `i32`, implemented with result `typeof(id(7))`, and a generic `id<T>(x:T)->T`, produced an unhandled collection-modified exception at the traversal beginning at line 465 in both Debug and Release. A representative implementation body simply returns `9`; the `typeof` operand is unevaluated but still needs signature discovery.

**Consequence:** a valid type query crashes the compiler. The discovery-safe first loop does not make all signature-related traversals discovery-safe.

**Required outcome:** make this traversal safe for discovery and ensure newly discovered functions receive all required signature/contract validation. Merely avoiding the enumerator exception is not sufficient if discovered entries can then escape validation. Retain this exact separate-implementation interaction as a regression.

### 4. Refused specialized callable construction mutates and destroys existing state

**Classification:** ownership/atomic-refusal implementation defect; model-probe coverage gap.

**Locations:** `compiler/Semantics/SemanticSymbol.cs:141–175` and `178–186`; `compiler/Semantics/GenericSpecialization.cs:153–165` and `177–201`.

The callable constructor attaches itself to the specialization at line 164 before attaching and validating its scope, type, and declaration. `AddFunction` allows null scope/declaration fields on this partially constructed object. If a subsequent parent operation rejects the object, the catch calls `Destroy`; destruction follows the specialization back-reference and destroys the existing specialization owner, which also clears analysis IR.

**Evidence:** an isolated source-linked Debug model probe created a valid compiled generic model with IR, then a fresh unpublished specialization. Attempting to construct its callable using a foreign semantic scope threw `InvalidOperationException`, but afterward the specialization was destroyed and the pre-existing IR was destroyed too.

**Consequence:** a refused operation is not unchanged. Constructor cleanup prevents an escaped half-object by cascading into an existing owning parent and unrelated compiled state. This violates the explicit relationship and failed-construction contract, even if ordinary compiler callers currently supply compatible parents.

**Required outcome:** validate the candidate parents and their compatibility before changing existing memberships; ensure failure cleans only what the attempted construction owns. Add a direct negative construction probe asserting unchanged specialization memberships, parent backlinks, and existing IR. Cover incompatible scope/declaration/type relationships as applicable, rather than only the normal checker call path.

### 5. `AddSyntaxNode` accepts foreign original syntax as specialization-owned derived syntax

**Classification:** ownership/provenance implementation defect; direct-operation coverage gap.

**Location:** `compiler/Semantics/GenericSpecialization.cs:115–123`; destruction consequences at `177–201`.

`AddSyntaxNode` checks destruction/publication state and whether the node already has a specialization, but does not establish that the node belongs to the correct compilation or is eligible derived syntax. Constructor-side checks do not protect direct callers of this declared relationship operation.

**Evidence:** the isolated source-linked Debug probe directly added another compilation's original syntax root to an unpublished specialization. The operation succeeded. Destroying that specialization then destroyed the foreign compilation's original root.

**Consequence:** a specialized body can acquire ownership of unrelated source syntax and cascade-delete it. The reciprocal backlink is present, but the relationship itself is invalid; two-way bookkeeping alone is not sufficient.

**Required outcome:** enforce the derived-syntax ownership/provenance contract at the relationship operation itself, before mutation. Retain a direct foreign-original-node refusal test that verifies both compilations remain unchanged and alive.

## Graders and teaching implications

The missing cases belong in `graders/ch11/generics.py`, `graders/ch11/typeof.py`, and `tools/probes/generic-model.cs`, with the existing `tools/probes/check_generic_model.py` runner. The current probe's duplicate/foreign-binding tests do not cover the callable-construction or direct syntax-attachment boundaries above. Its 968 checks per configuration therefore do not establish universal refusal-without-mutation. These are coverage gaps, not evidence that the recorded passing runs were fabricated or that the shared Chapter 10 tuple harness is defective.

The chapter's signature-discovery, actual-identity, and explicit-ownership explanation is substantially aligned with the intended design. Corrections must preserve those contracts rather than narrow the chapter to bless the failures. In particular, demand-driven completion needs to explain/preserve both method-signature environments, and lifecycle claims should be supported by direct operation refusal tests. I found no independent reason to require a broader manuscript rewrite. No fresh student has yet demonstrated reproducibility.

## Positive findings and design assessment

- The new model vocabulary and persistent relationships are declared in Common, with functional partial implementations. I found no new runtime identity-glue mechanism needed to connect these parts.
- Specialization identity uses template/canonical binding identities, with ordered binding occurrences and reciprocal relationships, rather than type-name text as authority.
- Normal specialization teardown explicitly retires IR and marks the specialization before shared-owner cascades, preventing recursive re-entry. The defects above concern invalid admission and failed construction, not absence of a normal teardown design.
- The inference path reuses checked arguments rather than intentionally evaluating them a second time.
- `CheckUnevaluated` (`Check.cs:4217–4243`) isolates assignment sets, loop state, protected arrays, and the unevaluated flag, restoring them in `finally`.
- `CheckMatchType` records the selected arm and checks only its body after validating patterns. Borrow analysis (`Check.cs:2372–2394`) skips `typeof` operands and follows only that selected arm. `IR/Lowering.cs:930–985` lowers the selected block without a runtime branch or subject evaluation and retains its lexical cleanup boundary. I found no additional actionable defect in those traced paths; this is not a proof covering every combination of borrow and control flow.

## Inspection and evidence limits

Inspected Chapter 11, relevant Chapter 10 prerequisite material, language specification and PEG changes, the compiler change set including both new GenericSpecialization files, Chapter 11 graders and shared Chapter 10 tuple contract, the model probe/runner, spin11, Makefile integration, diagram manifest/publisher/refusal controls and relationship companion, and `book/notes/ch11-development.md`.

Independently read the cumulative log summaries confirming the clean Debug/Release builds and recorded chapter results. The supplied development evidence records the full cumulative test pass, focused generic/typeof/matchtype totals, selected-only IR and corruption controls, PEG controls, model checks, and exact spin11 output. I did not redundantly rerun those suites. I did independently reproduce findings 1–3 against both existing configurations and findings 4–5 with an isolated source-linked Debug probe. The latter two were not independently rerun in Release.

Diagram evidence reports 20 types/46 relationships, seven new links, historical Chapter 10 pinning, refusal controls, and unchanged older figures. I inspected the relevant manifest/publisher/companion material; the supplied rendered-image inspection is not my own physical print acceptance. Physical print acceptance remains outstanding. No setup failure prevented this review.

An investigated empty-pattern hypothesis was dismissed: the scalar unit type is spelled `unit`, and the relevant control succeeded. It is not a finding.

## Preferences, separate from required corrections

No additional optional refactor is requested. In particular, these defects do not justify a new registry, callback layer, or a general framework. Focused scope-correct signature resolution, discovery-safe traversal, and prevalidated relationship operations fit the existing architecture.

**Lesson:** passing end-to-end cases cannot establish relationship safety at direct model boundaries; and making one signature traversal discovery-safe does not protect the other paths that resolve types. Preserve the existing broad checks, add these bounded counterexamples, and correct the implementation without requiring another review cycle.
