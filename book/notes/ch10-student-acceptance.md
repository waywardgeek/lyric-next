# Original Chapter 10 reconstruction — acceptance receipt

## Status

Assigned reconstruction and applicable automated acceptance COMPLETE. Actual unchanged Make `test` target completed Debug and Release, job1533 terminal exit0. No source edits after this run began. Stop at Chapter10; human acceptance remains pending. No deployment, installation, commit, publication, additional agent, or Chapter11 work performed.

Workspace: `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/children/original-ch10-student` (ROOT below). Implementation: `candidate/compiler`. Public copied Makefile: `candidate/Makefile`. Immutable public bundle: `public`. Original 141-file input manifest: `receipts/input-sha256.json`; `public-integrity.log` verifies all106 public files match it. Earlier accepted Chapter09 prerequisite and original Chapter10 scope unchanged.

## Reproduce the cumulative command

Working directory ROOT. Environment setup `environment.sh` confines HOME, CFFIXED_USER_HOME, TMPDIR, DOTNET_CLI_HOME and NUGET_PACKAGES to ROOT; sets SDKROOT and direct CLT clang/Python. After `. ./environment.sh`:

```sh
/usr/bin/make -C candidate test \
  DOTNET="$ROOT/dotnet-offline" \
  PYTHON="$PYTHON" \
  GRADERS="$ROOT/public/graders" > cumulative-make.log 2>&1
```

`dotnet-offline` adds `--no-restore` only to SDK build commands and forwards compiler commands unchanged. Offline prerequisite restore had already succeeded. SDK `/Users/bill/.local/share/ensemble-dotnet10/dotnet`, version10.0.403; direct `/Library/Developer/CommandLineTools/usr/bin/python3`, Python3.9.6; Apple clang21.0.0, arm64-apple-darwin25.3.0. See `tool-versions.txt`.

Terminal receipt: job1533 done, exit0. Full log `cumulative-make.log` (2675 lines). Debug build at line4; Release at1340; each zero warnings/errors. The Make launcher itself reports a denied system xcrun cache write on line1; sandbox prevented that write and Make continued successfully. This is not a warning-free tool environment.

## Exact results — each configuration independently

The Make recipe invokes25 public runners per configuration (correction to earlier spoken26), plus one comparison selfcheck before the loop. Both Debug and Release passed the following identical counts:

| Public runner | Result per configuration |
|---|---|
| ch01/grade.py |82 passed,0 failed|
| ch02/unicode.py |99,826 NFC vectors; XID classification all1,112,064 Unicode scalars;7 malformed requests|
| ch02/tokens.py |58/58|
| ch03/trees.py |70/70|
| ch03/recognition.py |155/155|
| ch03/boundaries.py |7/7|
| ch04/check.py --chapter=8 |82/82|
| ch05/run.py |80/80 execution|
| ch06/run.py |250/250 execution|
| ch06/unsafe.py --chapter=8 |87/87|
| ch07/run.py |112/112 execution|
| ch08/run.py |6/6 execution|
| ch08/check.py |154/154|
| ch08/layout.py |212/212 layout executions|
| ch08/allocation.py |4/4 free-slot and4/4 class-growth comparisons|
| ch08/ownership.py |12/12 owned-field/array balance comparisons|
| ch09/ir.py |21/21; snapshot-order selfcheck|
| ch09/ownership.py |92/92;13 explicit mutation selfchecks|
| ch09/c_emission.py |33/33|
| ch10/canonical.py |5/5; selfcheck rejects18 corrupted documents|
| ch10/tuples.py |22/22 execution;26/26 rejection|
| ch10/structs.py |20/20 declaration/layout|
| ch10/struct_values.py |34/34 execution;39/39 rejection|
| ch10/array_defaults.py |12/12 safe and12/12 unsafe-mode execution; each reports0/0 rejection cases|
| ch10/borrowed_structs.py |20/20 execution;23/23 rejection|

Ownership mutation selfchecks: counted-retain, counted-release, owned-field-release, final-omit, final-duplicate, final-early-reclaim, class-constructor, generated-reclamation, class-growth-store, class-method-identities, index-order, copy, replace. Comparison selfcheck passed once before both builds. Public runner exit/diagnostic/stdout rules unchanged.

## Implementation and structural evidence

Canonical identities belong to SemanticAnalysis, with full-key comparisons inside hash buckets, ordered tuple occurrence links, nominal declaration identity and reciprocal symbol/fact/IR users. Published keys do not mutate. Real tuple/struct checking, explicit aggregate IR and recursive C copy/drop/default construction extend the prerequisite compiler. Qualified bodies use a declaration/implementation relationship; field defaults have initializer scopes and generated IR roles. Contained references use the existing borrow event graph/fixedpoint, finite ancestry-folded formal occurrences, snapshots versus live referents, batched overlapping call writes, returned initializer graphs, and alias-local mutation causes. Common stores declarations/fields; spoke partials implement behavior.

Reviewed operation locations and return guarantees:
- `Semantics/CanonicalTypes.cs`: analysis AddType70/RemoveType129, type constructor148, Destroy233, array/component links270–317, occurrence constructor330/Destroy344, nominal links364–375. Construction attaches ingredients before publishing and rolls back refusal; removal clears both ends; retirement cascades dependents/users, not shared ingredients. Direct occurrence destruction retires a live immutable tuple.
- `Semantics/CanonicalUsers.cs`: Add/RemoveSymbol12/22, Add/RemoveFact32/42, Add/RemoveIrType56/66. Validate ownership before mutation; paired removal detaches both ends. Type replacement reserves before detaching old relationships.
- `Semantics/InitializerScope.cs` Add9/Remove23; `Semantics/FunctionScope.cs` constructor7: field/analysis/lexical-parent validation, symmetric attachment, construction rollback. Direct scope destruction detaches initializer field.
- `IR/InitializerModel.cs` Add9/Remove25; `IR/Model.cs` IrFunction constructor82/Destroy107: generated role and same-analysis validation, program/field attachment, owned IR teardown, reciprocal detach without destroying field parent.
- `Semantics/SemanticModel.cs` analysis Destroy42 and scope Destroy158; `Semantics/SemanticSymbol.cs` symbol Destroy154: analysis clears IR before semantic teardown; scope destruction detaches its weak child-scope relationships and destroys its owned symbols; symbol destruction retires its owned scopes and detaches users/declarations/parent links. `Semantics/SemanticSymbol.cs` constructor121 restricts reference fields to struct member scopes.
- `Emission/Compilation.cs` AggregateDeclarations1181 separates discovery through references from inline dependency ordering. Aggregate selection validator checks actual field/position correspondence.
- `Semantics/Check.cs` mutation handling709, CopyFlow1297, indexed class destination2382 preserve legacy scalar conversion and class descriptor/payload/index lifetime distinctions.

`diagnostics/formal-storage/Probe.csproj` compiles actual candidate sources, not a substitute fixture model. Job1494 exit0, `structural-final-probe-build.log` and `structural-final-probe.log`: real hash collision distinct-key retrieval; duplicate-key and foreign-owner refusal rollback; canonical identity, ordered/repeated occurrences, dependent retirement, ingredient survival, reinterning, analysis teardown; direct initializer/scope detach without field destruction; isolated clone, union join, weak/strong writes, stale snapshot; sibling separation, deterministic positions, ancestry folding, private-by-value-root exception and summarized descendants. Probe predates the final narrow Check.cs regression fixes; canonical/initializer/formal-state implementations unchanged thereafter. `mixed-layout-probe.log` verifies mixed inline/reference recursion O0/O2.

Published walkthrough: `walkthrough.sh` extracted unchanged from Chapter10. Job1517 exit0, `walkthrough-final.log` exact lines: `value copy stays independent`, `alias copy reaches the same storage`, `tuple growth constructs defaults`. This refreshed walkthrough predates only the final direct-class-field destination correction, unrelated to its source. Do not rerun solely for that distinction.

## Cumulative failures investigated and corrected

1. Job1476 exit2, valid integer widening crashed at strict CopyFlow identity check. Scalar values without address slots carry no alias state; allow the already-validated scalar conversion. `cumulative-make-first-failure.log`; targeted build/check82/82.
2. Job1488 exit2, Chapter8 checking152/154. Class-table growth incorrectly invalidated array payloads; restrict growth to descriptor depth0, retain all-depth destruction. Assignment contained transfer preceded existing destination/object check, losing LYC330; validate saved destination after RHS before transfer. `cumulative-make-second-failure.log`; job1510 clean build and154/154.
3. Job1512 exit2 at layout `array-field-rhs-growth`. Direct class-field store saves an object index, not a pointer. Omit pointer-staleness check only for this destination; preceding object-use check still rejects destruction, aggregate/reference destinations remain checked. `cumulative-make-third-failure.log`; job1529 clean build and212/212. Final actual cumulative job1533 passes both configurations.

Lessons: canonical identity does not replace legal scalar conversion; table movement, payload movement and destruction are different lifetime events; indexed destinations are not escaping pointers. Focused Chapter10 passes alone missed these retained regressions. Discovery through pointer referents and inline definition order are also distinct traversals.

## Assistance, feedback, limits

Assisted independent reconstruction, not unassisted success. Parent confirmed active instruction/access model and approved direct CLT executables; clarified singleton comma anchor already documented in tree-shapes.md (my missed contract, not a teaching defect); clarified ancestry-only formal folding and private-by-value-root exception already in Chapter10. No parent candidate edits, target solution inspection, or grader changes. Earlier guessed AST/API names and source paths were corrected by actual inspection. Original public inputs remained unchanged.

Actionable teaching feedback: emphasize the cumulative gate before completion; remind implementers that new contained-flow analysis must preserve existing scalar conversions and distinguish class-table descriptor, independent array backing, saved object index, and true pointer lifetimes. These were implementation mistakes; no public specification/grader defect was established.

Automated checks and direct structural probes do not prove all possible program safety, sanitizer coverage, self-hosting, or live human usability. No additional sanitizer or whole-system/deployment claims. Required command-line acceptance is100% passing; manual human acceptance remains pending. All owned command jobs terminal as of completion; no child agents or pending questions. No further reruns or next chapter authorized by this receipt.
