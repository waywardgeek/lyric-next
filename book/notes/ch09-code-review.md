# Consolidated Chapter 9 code review

## Assessment

The reference implementation substantially achieves the intended refactor: structured lowering owns execution order and cleanup, and C emission translates IR rather than falling back to syntax-body execution. The relationship model is coherent, and the retained evidence is broad. I found two confirmed implementation-boundary defects and one walkthrough/teaching defect requiring correction. These do not establish a failing checked-source program in the current CLI; they do undermine the stated editable-model/publication contract and walkthrough evidence.

This was one privileged, read-only consolidated review. I did not patch repository files, install tools, commit, inspect credentials, or launch reviewers. Two disposable diagnostic builds were attempted; the first failed because the harness incorrectly accessed a private field, and the corrected harness compiled and ran successfully. Temporary project artifacts were removed on exit.

## Required corrections

### R1 — Revalidate the current graph before inspection publication
**Category:** implementation/API contract. **Priority:** medium.

**Locations:** `compiler/IR/Inspection.cs:12–17,46–124`; `compiler/IR/Lowering.cs:65–67`; `compiler/Common/IR.cs:123–156,252–254`; Chapter 9 §6.

`LowerIr` runs all three validators, but `WriteIr` only fetches `Analysis.Ir` and serializes it. The model explicitly allows incomplete draft edits after construction. Nothing prevents those edits after successful lowering, and no immutable validation state protects publication.

**Confirmed trace:** the disposable harness checked/lowered `func value()->bool { return true }`, obtained its function through `DeclaredSymbol.IrBody`, and constructed an additional, unattached bool Constant in that function's arena. `ValidateStructure()` rejected the detached draft, as intended. Calling `WriteIr(StringWriter)` on the same graph nevertheless published a complete `lyric-ir-1` document containing the draft. Diagnostic job 2894 exited 0 and reported both observations.

**Consequence:** the declared publication boundary can export a graph known to violate its structural contract. Other edits can instead produce incidental serialization exceptions. The ordinary driver currently avoids this sequence because it lowers immediately before writing (`Driver/Program.cs:91–97`), so this is not evidence that ordinary CLI output currently contains detached nodes.

**Correction:** run structural, operation, and execution validation against the current graph before serializing/publishing it. Preserve buffered output and refusal without mutation. Add a direct API probe that lowers, makes a legal incomplete edit, calls `WriteIr`, and requires refusal with an empty writer; repair the graph and require successful publication. Revalidation is simpler than introducing mutable validation certificates or another authority field.

### R2 — Constant payload/type agreement is missing from operation validation
**Category:** implementation validator, with supplementary probe correction. **Priority:** medium.

**Locations:** `compiler/IR/Validation.cs:116–170` and the remaining operation cases; `compiler/Emission/Compilation.cs:617–624`; `compiler/IR/Inspection.cs:86–92`; `tools/probes/ir-model.cs:354–356,533–541`.

Constant validation checks zero operands, Value category, and a non-null type, but never checks the literal kind or whether its payload represents that type. This is a local opcode/type obligation, not alias-sensitive ownership analysis or an optimization requirement.

**Confirmed trace:** in the same disposable diagnostic, I replaced the valid function's bool literal with a Constant having `Type = Bool` and `Literal = Integer(7)`, preserved its return operand and correct region order, and ran `ValidateStructure`, `ValidateOperations`, and `ValidateExecution`. All accepted it. Job 2894 reports: `boolean Constant with integer payload 7: all validators accepted`.

**Consequence:** supposedly validated IR can contain a bool literal serialized as `"7"`. Emission chooses the C expression from the literal kind rather than validating its agreement with the result type. The same missing check admits non-unit Constants with null payloads; the emitter's fallback is `(ly_unit){0}`, which is not an integer/bool value. This is not a claim that current normal lowering creates these malformed Constants.

**Correction:** define and enforce the implemented Constant contract: unit's appropriate absent payload, bool/Boolean agreement, integer/Integer agreement and representable range, and refusal of types/payloads without a Constant execution contract. Byte-array literal payloads belong to NewArray. Add isolated malformed fixtures and valid repairs.

The current operation probe deliberately builds `i32`, `i64`, `u64`, and bool Constants without literal payloads and treats them as otherwise valid operands. Supply valid payloads in those baseline fixtures when tightening validation; otherwise an unrelated malformed Constant could satisfy a later expected refusal and conceal what the test was intended to prove. No public grader expectations need to be weakened.

### R3 — The walkthrough presents arena order as execution order and does not check the claimed cleanup sequence
**Category:** teaching/walkthrough tool. **Priority:** low, but required for accurate evidence.

**Locations:** `tools/spin09.py:43–52`; `book/chapter-09.md`, “Take it for a Spin”; `graders/ch09/README.md:19–20,45–50,55–66`.

The walkthrough builds its printed operation sequence by iterating `make['nodes']`, the flat arena serialization. Execution order is defined by the body's ordered `children` edges, not by arena allocation order. The script then checks only membership of NewArray, Move, and Return; it does not require FreeArray, their order, or the relevant operand relationships.

The retained reference run does print `NewArray -> Move -> FreeArray -> Return`, and the inspected reference implementation produces the intended execution. The defect is in what the script claims to demonstrate/check: a different valid arena ordering may print a misleading execution sequence, and absent or misplaced cleanup can still satisfy its check. The chapter says the script checks these observations.

**Correction:** derive the displayed sequence from the helper body's ordered execution children, and check the claimed transfer/cleanup/return relationships, including FreeArray of the moved-from holder and Return of the transfer. Alternatively narrow the prose claim, but do not teach arena order as execution order. This is a small walkthrough repair, not an additional language requirement.

## Architecture and behavior verified by source tracing

- **Common/spoke structure:** `Common/IR.cs` holds persistent state and partial declarations, not algorithm bodies. The new implementation files implement those same concrete partial types. Operations used across modules—including semantic class queries, parameter queries, lowering, and emission—are declared in Common. Lowering's action stack, storage map, and owner lists are temporary construction work, not duplicate persistent ownership authority (`Lowering.cs:156–185`).
- **Construction/refusal:** `IrFunction` registers with its program and then its actual declaration/class; failure destroys the first membership (`Model.cs:82–101`). `AddIrDestruction` validates class kind, analysis, existing relationships, and function role before setting both endpoints (`437–459`). `IrOperand` similarly rolls back consumer attachment if definition registration refuses (`225–246`). Cross-function uses are refused before the definition's reverse list is changed (`383–400`).
- **Destruction:** direct node destruction removes body/parent membership, detaches weak children, destroys strongly owned operand objects, disconnects incoming uses without deleting consumers, detaches loop transfers and semantic/source/type dependencies, then leaves the arena (`Model.cs:163–192`). Function destruction iterates the whole arena, including detached drafts (`103–116`). Analysis destruction clears IR before facts/scopes; symbol destruction/type replacement and syntax edits retire derived IR through the existing parent paths (`Semantics/SemanticModel.cs:42–60`, `SemanticSymbol.cs:153–180`, `ClassModel.cs:32–61`, `Syntax/SyntaxNode.cs:19–80`). Literal byte arrays are defensively cloned.
- **Values versus storage:** `SelectStorage`, `Receiver`, and `SelectIndex` distinguish saved handle Places from captured addresses and ordinary value reads (`Lowering.cs:326–398,434–460`). Explicit references resolve Places via AddressOf; writable nested selection avoids copying the destination (`595–610`). Emission saves a handle for a Place and resolves current field storage at use, while fixed-address results are pointer variables (`Emission/Compilation.cs:422–448,488–524`).
- **Ordering and ownership:** compound assignment loads its old destination before RHS evaluation; ordinary calls evaluate arguments before generating transfers; output builtins do not consume their owning arguments (`Lowering.cs:621–677,781–803`). Replacement transfers the new value, releases old storage, then stores (`887–899`). Conditional result ownership is registered only after branch lowering (`466–499`). Explicit return cleanup includes overlapping loop-condition lifetimes (`281–307`). False-condition cleanup is inside the false path, with body/backedge and transfer cleanup handled separately (`825–873`).
- **Generated destruction:** the generated receiver is an unnamed Local with the nominal instance type. Lowering calls the actual final hook, selects/releases array fields afterward in declaration order, reclaims, and returns unit (`Lowering.cs:93–147`). Runtime wrappers guard/mark destruction then dispatch to this body; they do not duplicate field cleanup (`Emission/Compilation.cs:955–968`). Counted release clears the holder and avoids retaining a metadata pointer across destruction (`989–1013`).
- **IR-only C emission:** `EmitC` always calls `LowerIr`, emits source and destruction prototypes, and emits every body via `EmitIr`; output is buffered until success (`Emission/Compilation.cs:16–27,314–367`). Region emission is iterative, and no separate scope-owner cleanup inventory remains. Numerical and runtime helpers preserve the established backend responsibilities rather than adding source execution policy.
- **Bounded validation:** structure, local operations, and reachable execution are separate. Execution tracks definitions and pending transfers, intersects loop exits, and distinguishes Return from Yield. Unreachable suffixes remain subject to structural/operation checks. I am not asking this chapter to prove storage initialization, full ownership balance/provenance, alias-sensitive lifetime safety, or termination; its documented exclusions are appropriate.

## Grader and evidence assessment

I read the Chapter 9 scalar, ownership, and C graders and their public guide, along with the supplementary model probe/runner. The independent ownership evaluator uses separate frames, heap allocations, capacities, counted identities, fixed-address generations, and deliberately frequent class relocation. Its expected object/hook counts are fixture expectations, not derived from the emitted cleanup calls. Mutation self-checks cover copy/release removal, early address capture, method misbinding, missing destruction/reclamation, hook omission/duplication/reordering, and counted retain/release removal. These are meaningful independent checks, not mocks of compiler internals.

The C grader compiles emitted C at O0/O2, checks body-marker counts, exact successful stdout, and designated runtime failures/traces. Its Chapter 9 runs use default layout; the retained Chapter 8 layout grader provides the separate AoS/SoA evidence. Body markers alone would not prove an IR-only implementation; source tracing supplies that additional evidence.

Inspected retained evidence:

- `ir-c-only-cumulative.txt`: successful Debug and Release builds, zero warnings/errors, retained suite summaries in both configurations, including 21/21 scalar IR, 92/92 ownership IR, and 33/33 IR C cases.
- `ir-c-only-sanitizer-layout.txt`: 212/212 layout executions.
- `ir-c-only-sanitizer-c.txt`: 33/33 IR C cases.
- `ir-c-only-model.txt`: 153 model checks in each configuration.
- `spin09-make.txt`: successful Release build, expected output and LYC301 rejection.

I inspected these files; I did not rerun the cumulative or sanitizer suites or independently observe the original jobs' process exits. The logs support the reported results but are not a content-hash attestation for the present source. The fresh diagnostic did compile the current compiler sources in Debug with zero warnings/errors and confirmed R1/R2.

## Preferences and remaining limits

- The scalar evaluator dispatches calls by display name (`graders/ch09/ir.py:97–104,134–135`), unlike the declared symbol-ID execution model. Its current scalar fixtures have unique names, while the ownership evaluator does use symbol IDs and tests same-named methods. Aligning the scalar evaluator would improve consistency, but I am not treating this as a demonstrated false pass of the complete current suite.
- Correct stale “immediate consumer” wording in `Common/IR.cs:127–128`, `Lowering.cs:231–232`, and `graders/ch09/README.md:122–124`: cleanup may intervene between Move and its single designated consumer. The chapter and later guide correctly explain that distinction.
- I found no new installation/setup defect. The initial diagnostic compilation failure was my harness's private-field access, not a compiler defect; the corrected harness used the real declaration relationship.
- This review did not independently reconstruct the chapter, measure performance, test another platform, or establish human acceptance. Those remain separate evidence.

Git inspection identified the four untracked IR implementation files, Chapter 9 graders/chapter/walkthrough, tracked model/emitter/driver changes, and substantial retained evidence. Final status/stat inspection showed the same substantive working-tree inventory; unrelated `arith.ly` and `hello.ly` were left untouched.

**Lessons:** validation is a property of a particular graph state, not a permanent consequence of having lowered once; malformed-fixture baselines must themselves satisfy every unrelated contract; and execution-order evidence must follow region relationships rather than serialization/arena order. No re-review loop is requested—incorporate the bounded corrections and verify them through focused probes plus retained checks.
