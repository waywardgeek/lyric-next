# Chapter 11 — Generic Functions and Compile-Time Type Selection

## Agreed increment

Bill authorized interactive construction after original Chapters 9–10 were accepted and Chapter 10 committed at 0ea3d8e617bbb3c15680b7fabd2e7df415ced1dd. The abandoned reference-safety reorganization is not a prerequisite or work to resume. Preserve its archive and the three unrelated untracked files in the active tree.

Implement ordinary generic functions with explicitly declared type parameters and explicit return types, argument-based inference and explicit semantic type arguments, typeof(expression), composite type expressions containing generic variables, and matchtype. No function overload sets, mandatory named constraints, or body-inferred return types. Generic classes, multi-class generic declarations/implementations, and optional named capability contracts follow later. A standard-library max may eventually state its comparison contract for clarity; ordinary generic bodies do not require such declarations to be admitted.

Type arguments are semantic expressions, not textual substitutions. For example, CreateContainer<typeof(input)>(input) resolves the input's static type without evaluating the typeof operand. func removeTupleNesting<T>(a: (T,)) -> T extracts the component binding from the argument shape and substitutes it into the declared result. Types containing borrow references remain subject to existing safety/escape rules.

## Accepted matchtype semantics

Use match-like arms with => and braces. The subject is an expression whose static type is inspected without runtime evaluation. Resolve dependent subjects during specialization. Arms are ordered; the first matching arm is selected. All arms must parse, but only the selected arm is specialized and type-checked. No match is a diagnostic; _ is the explicit fallback.

Bill approved explicit header declarations: `matchtype<U,V> value { (U,) => {...}; [U] => {...}; _ => {...} }`. Omit the header when no variables are needed. Each arm matches with fresh bindings; repeated variables require exact canonical identity. Other names resolve to existing types, and every pattern is validated even after an earlier match. Only bindings acquired by the selected pattern are visible in its body. Header declarations follow no-shadowing. This supersedes the initial inline `type U` proposal recorded in the historical construction notes below.

## Construction and evidence plan

First prove an executable vertical slice: explicit specialization; inferred singleton tuple wrapping/unwrapping; a generic caller discovering another specialization; and matchtype with an unselected arm invalid for the selected type. Build independent public positive/negative graders alongside implementation. Extend coverage to recursion, duplicate specialization reuse, composite matching, typeof non-evaluation, ownership and lifetime integration, and controlled refusal of runaway specialization. Preserve existing retained graders and diagnostics; do not replay broad unchanged suites after every small edit.

On a call with complete bindings, intern a specialization by generic declaration and ordered concrete semantic bindings, register its signature before enqueueing its body, and allow its caller to continue using that signature. Discover further specializations while draining the queue. Existing recursive effect/lifetime checking must include all concrete bodies before executable IR publication. Same-key recursion reuses identity; ever-growing distinct specializations require a bounded diagnostic. Future multi-class specialization keys must additionally retain complete selected binding/participant-role identity.

## Initial source inspection

Tracked tree was clean at start; arith.ly, hello.ly, and background/.multi-class-interface-redesign.md.swp are unrelated and untouched. No Chapter 11 manuscript or grader directory existed.

Compilation.Check in compiler/Semantics/Check.cs collects functions, resolves signatures, checks bodies in a fixed foreach (lines 484–522 at inspection), then computes recursive borrow/effect summaries (556–616). Ordinary call checking resolves existing symbols/signatures (4990–5155). No on-demand specialization scheduler is established by those existing worklists.

compiler/Common/SemanticModel.cs declares one Fact and DeclaredSymbol per SyntaxNode. A shared generic syntax body therefore cannot directly hold multiple specializations' facts. Inspect source ownership, cloning, retirement, and IR discovery before choosing specialization-body representation. Preserve declaration identity and original source locations; never derive semantic identity from generated names.

No compiler edit, build, or new behavior verification yet. Sole shell inspection refusal occurred before execution and was retried successfully. No agents or jobs remain live from this initial inspection. No reminders enabled or autonomous review/student campaign started.

## Vertical-slice baseline and ownership constraints

Fresh baseline job1709 built the unchanged original compiler in Debug with zero warnings/errors, then ran new public graders/ch11/generics.py: 0/3 execution cases and 0/1 intended rejection. All four stopped with LYC306 at TypeParameters; the negative case requires LYC304 after checking the selected concrete body. Cases cover two explicit concrete types plus repeated use, a generic body discovering another generic specialization, an unused dependent body, and a concrete return mismatch. Runner reuses the public Chapter10 O0/O2 exact-output harness. These tests are retained; they are not full Chapter11 coverage. No compiler edits yet.

Parser/SourceFile.cs already parses TypeParameters and Generic expressions (base expression plus TypeArguments). Type arguments currently parse types, with role bindings admitted syntactically. ReadType resolves TypeName through scope lookup. Existing syntax mutation clears the compilation's semantic analysis; derived body construction therefore needs a distinct lifetime/invalidation path. Ordinary scope AddSymbol refuses any already-visible name, so concrete specializations cannot simply be inserted as duplicate source declarations. Preserve one lookup-visible template, distinct concrete callable identities, independent checked syntax and original source locations. Keep concrete bodies in the existing checker/borrow fixed point and IR lowering rather than a second pipeline. Source lifetime must invalidate derived bodies; adding a specialization must not destroy the analysis constructing it. Canonical binding retirement must not leave a live specialization with stale key identities.

Source reads1704/1705/1710 were report-clipped; not complete review of all requested ranges. Key parser/type-resolution and syntax ownership sections were visible, but reread exact missing lifecycle/call paths before edits. No workers, no live jobs, no broad regression run, no commit.

## First executable specialization increment

Implemented explicit ordinary-function specialization through the existing checker, borrow/effect fixed point, IR and C emission. Shared declarations live in `Common/GenericSpecialization.cs`; implementations in `Semantics/GenericSpecialization.cs`. `GenericSpecialization` has analysis and template strong parents, ordered `GenericBinding` occurrences pointing to canonical types with reverse memberships, a concrete callable, copied syntax and concrete-body scopes. Source co-owns derived nodes for byte provenance; copied nodes keep original offsets but have independent facts/symbols. Once the callable is attached, copied syntax is structurally fixed. Original parsed trees are unchanged. Template lookup stays unique; concrete symbols are scope-owned but excluded from lexical lookup. Generated C names are never specialization identity.

The existing signature algorithm is now a local `CheckSignature` operation. Concrete type bindings are visible in the callable's private scope. A request installs its complete signature before appending its body to the growing checking queue. Field defaults are checked before draining this queue, so calls discovered there participate too. All concrete bodies reach the existing borrow/effect fixed point before lowering. IR skips unresolved templates and registers concrete callables before lowering any call. C prototypes/functions now come from IR's Source-function inventory, not a second source-syntax inventory; source traversal still finds classes and main.

Actual staged evidence:
- 1724: coordinated ownership declarations/constructors compiled Debug, zero warnings/errors; not behavior proof.
- 1727: intended generic return rejection passed, positives failed at IR generic-callee guard and unused-template emission (null FunctionScope crash). Corrected those downstream assumptions rather than treating checker admission as delivery.
- 1731: first 3 execution +1 intended rejection passed.
- 1733: expanded retained grader passed 6 execution +1 intended rejection; C runs use existing O0/O2 exact-output contract. Added identical recursive requests, aggregate independent copies, and field-default discovery.
- 1734 terminal0: selected existing Debug Chapter9–10 suites passed: 21 scalar IR,92 ownership IR,33 IR-C,5 canonical plus18 corruption controls,22 tuple executions/26 rejections,20 struct layout/declaration,34 struct executions/39 rejections,12 safe +12 unsafe array-default executions,20 contained-borrow executions/23 rejections. Saved log summaries inspected. This predates the following lifecycle correction; not a final cumulative claim.
- 1740: new development model probe failed because copied nodes retained semantic relationships. Ordinary syntax destruction relies on whole-analysis retirement; derived syntax cannot. Also identified weak lexical scope children as insufficient lifetime ownership.
- Correction: specialization explicitly co-owns function/block scopes, propagated through scope construction, and derived node teardown destroys facts/symbol links. Retirement marks the specialization first, then destroys callable/scopes/nodes/bindings and detaches template/analysis; cascades cannot recursively destroy the same instance. `SemanticType.IsUnlinked` includes generic dependents.
- 1742: original lifecycle probe247 checks passed Debug/Release.
- 1743 terminal0: strengthened lifecycle probe323 checks passed BOTH Debug/Release, existing canonical probe333 checks passed BOTH, all builds zero warnings/errors, refreshed Debug compiler passed6/6 generic execution +1/1 intended rejection. Diagnostic source-linked probes are NOT retained language graders. They test duplicate/foreign key refusal preserving IR, repeated positions, original parsed-tree preservation, and retirement via specialization/type/template/derived node/scope/analysis/source.

Lesson: canonical types may be shared, mutable checked syntax may not. Lexical ancestry is not lifetime ownership. Existing behavior graders did not catch the derived-node cleanup defect; direct model tests did.

Remaining Chapter11 work: inferred bindings/composite patterns, typeof non-evaluation, matchtype grammar and selected-arm checking, growing-specialization diagnostics and explicit-result/declaration-contract coverage; further lifetime/borrow/recursion tests; full cumulative final verification and earned manuscript/diagrams/reviews/student/human acceptance under appropriate authority. Current generic methods/constraints remain deliberately unsupported. No Chapter11 completion claim, commit, push, installation, worker or student campaign. No live job after1743. Original backups/abandoned reorganization and unrelated files untouched.

## Argument-driven inference increment

Bill agreed to restricted structural matching, not a general constraint solver: infer from actual argument types against declared T/[T]/tuple patterns; repeated variables require canonical identity; no widening negotiation or result-context inference. Explicit arguments retain contextual checking and ordinary conversions. Missing bindings require explicit arguments.

Check.cs now matches patterns iteratively, checks inferred arguments once in written order (CheckReference for ref parameters), and passes recorded actual types to the existing completed-signature compatibility checks. Specialization identity, registration, and body queue are unchanged. Concrete pattern leaves do not bind variables; completed-signature validation checks them.

Evidence: job1755 baseline retained all six explicit executions and original rejection, while four new inferred executions and three intended rejections failed with unsupported Name. Job1756 clean Debug build and 10/10 executions,4/4 rejections. Expanded1758 found two distinct issues: test incorrectly printed i32 instead of bytes; grouped TypeGroup lacked a text anchor and crashed Text(). Corrected fixture uses string markers; matcher and ReadType now recognize unanchored grouping before reading comma text. No global Text helper contract relaxation. Job1760 clean Debug build,15/15 executions,8/8 intended rejections. Job1762 terminal0 clean Release build, same15/15+8/8 public cases, diagnostic specialization lifecycle323 checks EACH Debug/Release, git diff --check clean. Inputs frozen during runs; no full cumulative regression claim for this increment.

Retained cases include inferred scalar/composite/grouped/nested patterns, repeated binding agreement/conflict, nested specialization discovery, runtime argument order, explicit reference acquisition/writability, no inferred widening negotiation, explicit conversion, shape mismatch and missing binding despite result context. Lesson: structural nodes can carry meaning without token text; inspect shape/anchor before reading text. Reference acquisition attaches facts and must not be repeated just to validate the now-concrete signature.

Next: bounded diagnostic for growing specialization chains, then typeof/matchtype construction. Same-key recursion already passes; arbitrary specialization termination is not established. No commit/push/deployment or chapter completion.

## Per-chain specialization discovery bound

Bill rejected a total monomorphized-function cap and approved a discovery-depth limit per chain. Root requests start at zero; each newly discovered specialization from a specialized body adds one. Maximum allowed depth is1024. Lookup/reuse happens before the check, so identical recursion and reuse from the boundary do not consume depth. This is a first-discovery resource bound, not a proof that arbitrary compile-time computation terminates or a total memory budget.

Implementation is temporary Check() state: Dictionary<GenericSpecialization,int>, keyed by actual identity; checkingFunction.Specialization supplies the requester independently of body queue scheduling. No new persistent ownership edges or generated-name identities. Reject before constructing/attaching a new object with located LYC344: Generic specialization discovery depth exceeds 1024.

Retained public grader generates1025 distinct nominal independent root requests (all execute), a finite chain from depth0 through1024 whose last body references an existing root under if false (checks reuse without runtime recursion), and unbounded grow<T> -> grow<[T]> rejected while checking, never executed. Job1768 clean Debug build16/16 executions9/9 rejections. Job1769 terminal0 final17/17 executions9/9 intended rejections EACH Debug/Release; Release build0warnings/errors; whitespace clean. No full cumulative regression or chapter completion claim. Inputs unchanged during checks. Lesson: breadth and depth are distinct; count new discovery steps, not calls or total concrete functions.


## Unevaluated typeof increment

Bill explicitly approved typeof(x) on declared but uninitialized variables. Implemented TypeOf syntax/keyword in Common, Lexer and Parser; operand is an ordinary expression in parentheses. ReadType checks it with the actual expression checker, no destination hint, caches its canonical type in a SemanticFact on the TypeOf node. PreviewType supplies only nondiagnostic hints during branch/cast preview, preserving branch-local preview names; it does not check operands or attach facts. Real checking saves/restores definite/possible assignment sets, loop records, selected-storage context and unevaluated flag in finally. Runtime initialized-read checks for names/constructor fields/compound reads are suppressed in that context, not ordinary type/writability checks. Borrow event Visit explicitly stops at TypeOf before registering values/temporaries. Ordinary binding/IR paths continue to lower actual initializers, not annotations. Original source origins/normal fact lifetime preserved.

Failures and corrections: baseline1795 all4 executions/3 intended rejections stopped at LYC201 (wrapper shell0 reflected later reads, not grader success).1797 build failed missing required SemanticFact symbol argument; passed explicit null.1798 four executions passed, invalid arithmetic correctly gave existing LYC312 rather than fixture's guessed304; corrected fixture.1800 malformed Python string delimiters blocked execution; repaired before1801 clean build8/8 executions4/4 rejections.1804 found two real signature failures: generic discovery modified foreach collection; forward ordinary call reached null parameter scope. CheckSignature now demand-driven/idempotent with temporary active/completed identity sets, checkingFunction saved/restored, indexed growing signature traversal, and CheckArguments ensuring target signature first. A cyclic explicit-signature dependency rejects LYC345; no body-derived return inference.1806 clean Debug13/13 executions5/5 rejections. Grammar typo Expression versus actual Expr corrected after frozen tests, not during them.

Public retained files: graders/ch11/typeof.py, type-syntax.json and README.md. Grammar TypeOf rule and language-spec unevaluated-query/cast contract updated; no claim matchtype or chapter completion.1809 terminal0: generic17/17 execution9/9 rejection EACH Debug/Release, Release typeof13/13+5/5, generic lifecycle323 checks EACH mode, clean builds/whitespace.1812 terminal0: canonical PEG177 rules + negative controls, new5/5 syntax fixtures and existing50/50 PEG; BOTH Debug/Release typeof13/13+7/7, syntax70/70 trees155/155 recognition7/7 boundaries, semantics82/82.1814 terminal0 added agreed explicit generic typeof argument: final14/14 executions7/7 intended rejections EACH Debug/Release, unchanged compiler assemblies, whitespace clean. No full cumulative Chapters1–10 run for this increment; lifecycle probe is model evidence, not exhaustive typeof lifecycle proof. No commits/workers/install/restarts. No live jobs or freezes remain.

Lessons: an unevaluated expression still requires real semantic validation, not hints. Its flow facts must not initialize or revive evaluated storage. Explicit result types can depend on other signatures, so forward signature completion and cycle detection are distinct from checking bodies. Querying a generic call can discover a specialization without executing the call. Existing same-key/depth-boundary generic tests remain passing after signature changes.

Next: agree concrete matchtype syntax (earlier type U binder remains provisional), then build its real selected-arm vertical slice with independent graders. All arms parse; selected arm alone checked/lowered; no match diagnostic and explicit fallback. Do not resume abandoned reorganization or start review/student campaigns.


## matchtype header design accepted; syntax vertical slice

Bill proposed and approved `matchtype<U,V> subject { i32 => {...}; (U,) => {...}; [U] => {...}; _ => {...} }` (ordinary newline/semicolon arm separators, not mandatory semicolons). This supersedes proposed inline `type U` binders AND implicit unknown-name binders. Header declares names once, omit header when unnecessary. Each arm starts fresh bindings; repeated variable matches exact canonical type; body sees only variables bound by its own pattern. Existing names resolve normally, typos fail. Follow no-shadowing. Validate names in ALL patterns even after an earlier match; only selected BODY checked/lowered. First matching arm wins, explicit fallback optional, no match diagnostic. Bill says proceed and flag similar consequential ambiguities, not routine PEG/parser plumbing.

1822 Debug build0warnings/errors: Common SyntaxKind MatchType/MatchTypeArm, Lexer matchtype keyword, Parser MatchType name-only optional header (separate from TypeParameters helper, which accepts constraints), header Expression(1,true), pattern wildcard or Type(), block arms. Fixed syntax children: parameters(always present, possibly empty), subject, source-ordered arms; each arm pattern+block. Keyword anchored match node. Canonical PEG Primary/HeaderPrimary includes MatchTypeExpr before MatchExpr, three new rules MatchTypeExpr/MatchTypeParams/MatchTypeArm; uses ordinary HeaderExpr/Type/Block/TypeClose. No new semantic support yet.

Retained graders/ch11/matchtype.py first5 execution cases +3 intended rejections.1823 terminal1 baseline all parse then Unsupported MatchType LYC306 (0/5+0/3, NOT intended negative passes). Covers concrete selected-only checking, independent structural arm binding, repeated variable identity, unevaluated calls, value-producing selection, later-pattern typo301, no-arm346(proposed new diagnostic unused check still needed), duplicate302. graders/ch11/matchtype-syntax.json11 public PEG fixtures includes all8 semantic-source cases accepted syntactically, plus malformed header/arrow/unselected-body rejection.1824 clean Release build; Peggy180 rules+negative controls+11/11 pass. Same batch handwritten diagnostic probe failed guessed serialized name matchtype; actual existing serializer uses match-type/match-type-arm.1825 corrected transient probe11/11 passes with fixed child layout checks. Probe not retained grader yet; executable cases retain many syntax behaviors, malformed cases should be integrated into retained runner. README/spec matchtype updates remain pending implementation.

Inspected actual Match parser630–720; CheckIf3479–3590 handles statement versus valueRequired blocks, merges branch assignment state and writes fact.CanContinue. CheckBlock3320+ has special If expression-statement path to preserve return/break flow; matchtype needs analogous selected-block flow without branch merge. CheckExpressionNode If5085–5115 finds function/initializer scope then CheckIf. Borrow Visit2339 now TypeOf guard then If path2422; BuildBlock handles declarations and blocks with cleanup; selected match block must reuse paths and exclude subject/unselected. IR Lowering Expression If594 creates structured If+Then/Else regions; Block927 schedules Statement and ending Yield/Return, owns cleanup per region. IR vocabulary Region/If/While/For etc, no standalone value Block opcode. Need inspect validator and lowering resource policy before choosing selected-block expression representation; do not invent runtime subject dispatch or leave unselected code in IR. SemanticFact persistent declarations176–205 include node/type/symbol/reverse relationships, IntegerConstant/CanContinue; no selected arm relationship yet. Select representation NOT implemented. Prefer real explicit relation or justified ordinal immutable syntax selector, decide with actual cleanup/invalidation contract, do not repurpose IntegerConstant. New scoped type symbols should use normal canonical backlinks and specialization scope ownership. No source changes/jobs underway; next implement semantic selected arm + flow/borrow/IR, rerun failing grader. No commit/reviewer/student/install authority.


## matchtype selected-body implementation and first execution evidence

SemanticFact now retains immutable SelectedArmIndex (syntax child position, not
an object relationship); constructor validates supplied positions and source/tree
mutation invalidates facts. CheckMatchType validates header duplicate/shadowing
and ALL concrete pattern names first, then matches each pattern iteratively with
fresh exact canonical bindings. Selected scope holds normal type symbols with
canonical backlinks; only its block is checked, with ordinary statement/value
continuation. Shared CheckUnevaluated isolates the subject using the existing
typeof flow-state save/restore. Unknown TypeName now reports LYC301 rather than
unsupported syntax, so unbound pattern variables receive a real name error.

Borrow traversal uses the selected block's existing outgoing-object/contained
reference snapshot and post-cleanup checks, not a runtime branch or subject walk.
IR SelectedBlock flattens only that arm, saves the enclosing owner-list boundary,
preserves its outgoing result holder, drops other arm-local holders, removes the
lexical ownership suffix and retains the result in the enclosing region. Return
and loop transfers see the registered holders and reuse ordinary cleanup. No new
IR opcode, synthetic boolean branch, cloned emitter or alternate borrow checker.

1839 clean Debug model/checker build;1845 terminal0 initial5/5 executions3/3
rejections. Expanded fixtures exposed author mistakes (missing same-line arm
separators and one closing brace), corrected without changing parser contract;
1848/1849 failures were NOT implementation passes.1851 terminal0 BOTH modes
15/15 matchtype executions10/10 intended rejections and typeof14/14+7/7;
323 specialization lifecycle checks BOTH modes, clean builds.1856 terminal0
clean Debug build final18/18+13/13 after added name-diagnostic and binder tests.
This includes result arithmetic context, return cleanup, fresh sibling bindings,
local cleanup timing, owned outgoing tuples/arrays, selected invalidation and
local contained-borrow escape rejection. Last diagnostic change not yet verified
in Release. Generic/full earlier regressions still owed for this final source.

Spec compile-time selection section and public README now state demonstrated
contract. Make test includes all3 Chapter11 runners. PEG180 rules and11 syntax
fixtures have prior1824 evidence; not a fresh semantic or final-source result.
Lesson: selecting the right body is only half the implementation; its value and
cleanup must follow the same flow/lifetime model as ordinary blocks. No commit,
review/student or chapter-completion claim. Next frozen regression batch then
structural/public IR inspection and remaining requirements review.


1858 terminal0: final compiler Debug/Release each matchtype18/18+13/13,
generic17/17+9/9, ownership IR92/92, contained structs20/20+23/23; clean Release
build and whitespace.1863 terminal0: added public IR selection1/1 and3/3
corruption controls EACH configuration; matchtype18/18+13/13 unchanged; expanded
source-linked lifecycle probe968 checks EACH configuration with clean builds.
Probe now runs all7 retirement paths for ordinary and matchtype-bearing generic
bodies, verifying chosen position, absent unselected block fact, copied syntax
and scope teardown. Source unchanged during these batches. A later tiny grader
control improvement injects an If while preserving the selected call, isolating
the branch assertion; probe indentation polished. These changes need their own
focused readback/run, not another compiler build claim. Next cumulative make test
through11 and PEG/public syntax checks, with sources/graders/assemblies frozen.


## Cumulative verification of the implemented Chapter 11 increment

Job1866 terminal exit0: unchanged root `make test` with the installed .NET10
SDK passed all retained runners through Chapter11 in BOTH Debug and Release.
Both builds had zero warnings/errors. This includes full Unicode conformance,
parser/semantic/runtime/ownership/IR regressions and final Chapter11 totals:
generics17 executions/9 intended rejections; typeof14/7; matchtype18/13 plus
1 IR-selection check and3 corruption controls, per configuration. Full log:
`/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch11-cumulative.log`.
Job1873 extracted and inspected both configurations' numeric summaries, then
passed independent Peggy recognition: all180 canonical rules, malformed-grammar
controls,50 base fixtures,155 retained Chapter3 fixtures,5 typeof fixtures and
11 matchtype fixtures. Whitespace check clean. Sources, graders and assemblies
were unchanged throughout these runs.

The strengthened isolated-If corruption control already passed in job1864 in
both configurations; the cumulative run also includes it. The indentation-only
probe polish after1863 did not change its logic;968 lifecycle checks per mode
remain the recorded pre-polish development evidence, not a newly executed probe.

Implementation/regression milestone complete, not whole-chapter acceptance.
Earned teaching, diagrams, walkthrough and any separately authorized review or
student reconstruction remain. No commit, push, installation, worker campaign
or human walkthrough acceptance claimed. No live jobs remain after1873.
Lesson: grammar recognition, selected-only IR, ordinary cleanup/borrow behavior,
and reciprocal model retirement are separate obligations; runtime output alone
does not establish all four.


## Chapter completion authorized; teaching draft and walkthrough

Bill approved proceeding with the proposed teaching/diagrams/walkthrough, then
one code review, one text review, and fresh isolated Chapter10-to11 student
reconstruction, followed by his human acceptance. This does not authorize commits,
pushes, installation or advancing language scope. No reviewers/students launched.
Job1897 terminal0: new tools/spin11.py and Make spin11 build Release cleanly and
verify exact five-line output for generic array-copy independence, typeof call
non-evaluation, singleton/array/fallback matchtype arms. Temporary artifacts removed.
Chapter11 first construction draft written, with explicitly pending model-diagram
block. It teaches actual identity/ownership, copied syntax, signature discovery,
inference, typeof, selected block flow/borrow/IR, public commands and walkthrough.
Not yet source-audited line by line, reviewed, or reconstructed. No compiler change.
Diagram migration currently remains Chapter10-specific; preserve its historical
manifest/fingerprints/figures and add Chapter11 coverage against accepted0ea3d8e,
not blind fingerprint refresh. Need read generator contracts in bounded ranges,
inspect all changed lifecycle paths, validate all stored fields and new edges,
render/inspect figures and replace pending model block before review.

## Chapter 11 diagram milestone

Fresh extraction validated 20 model types and 46 relationships: all 39 historical
links plus seven new specialization links. Source/lifecycle comparison used accepted
Chapter 10 commit `0ea3d8e617bbb3c15680b7fabd2e7df415ced1dd`; existing manifests and
figures were not refreshed against new source. Three new-only views show instance
ownership, ordered binding dependencies, and copied-body/scope ownership. Their
companion explains existing co-owners and the two distinct SemanticSymbol roles.

Jobs1922/1923 preview passed source coverage and ten-point-at-five-inch SVG metrics;
all three PNG previews were inspected as actual pixels. Job1929 rejected seven
migration defects, published/read back eight outputs, preserved all34 pre-existing
figure-directory files byte-for-byte, and passed whitespace checks. The same
controls are retained in test_chapter11.py. No physical print acceptance claimed.
No compiler changes or repeated behavioral suites in this diagram slice.

Lesson: copied syntax's source provenance remains an owning relationship, while
its semantic lifetime also belongs to the specialization; lexical ancestry alone
does not own all scopes. Diagrams must retain those distinctions, not force a tree.
Next: prose/source audit and book index, then sole code reviewer, sole text reviewer,
and isolated reconstruction from accepted Chapter10. No reviewers/student launched.


## Pre-review teaching audit

Read the complete Chapter11 draft and public grader contract, checked exercises
and walkthrough against the existing commands, and traced signature completion,
unevaluated state isolation, selected-arm checking and lowering against source.
Updated the book index from its stale Chapter10-next wording to the actual
Chapter11 milestone, explicitly retaining pending review/reconstruction/acceptance.
No compiler or grader change; cumulative1866 and PEG1873 remain the behavioral
evidence, spin1897 the practical walkthrough. Diagram controls1930 passed7.
The next action is the sole consolidated code review; no review result yet.

## Sole code-review corrections in progress

The consolidated report is preserved in `ch11-code-review.md`; reviewer retired, no re-review. Corrected unknown explicit generic diagnostic to use anchored callable name; separate method signatures now use declaration and implementation parameter environments; implementation-result validation uses indexed discovery and signature completion. Added one public generic rejection and three typeof executions plus one mismatch rejection. Job1984: clean Debug build,17/17 typeof executions and8/8 intended rejections.

Specialized callable constructor now preflights scope/type/declaration/specialization compatibility before attachment. Derived-node admission takes the intended source and refuses existing parsed nodes and foreign provenance before mutation. Direct probes exercise these boundaries and assert unchanged draft/trees/IR. Job1986: clean Debug/Release probe builds,1108 checks each. No new persistent model relationship. Teaching integrates reasons at construction points; PEG README stale counts corrected155/180.

Full retained `make test` job1988 is RUNNING on frozen compiler/grader/probe inputs; log `/Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch11-review-regressions.log`. No full post-review cumulative pass yet. Next collect terminal receipt, inspect bounded changed lifecycle source and update only reviewed diagram fingerprints, publish/check unchanged figures, record verification, then sole text review. Student not launched.

## Code-review corrections verified; ready for text review

Cumulative job1988 reached terminal exit0: the unchanged Make target passed
Debug and Release, each with zero warnings/errors. Each configuration passed
generics17 executions/10 intended rejections, typeof17/8, matchtype18/13,
selected-only IR1/1 and corruption controls3/3, plus all retained Chapters1–10.
Full log retained at Cadence/ch11-review-regressions.log. Model job1986 remains
1108 direct checks per configuration. No re-review requested.

Job1999: seven diagram migration controls passed; freshly extracted accepted
Chapter10/current model validated20 types/46 relationships; eight outputs
published/read back and all41 existing figure files byte-identical. Only the
three inspected lifecycle fingerprints changed, no relationship changes.
Corrected-compiler make spin11 passed exact five output lines; build clean and
whitespace clean. Prior independent PEG1873 remains applicable: grammar/parser
unchanged by these review fixes. Human walkthrough/print acceptance pending.

The chapter now teaches separate declaration/implementation parameter environments,
discovery-safe signature traversal, preflight before callable attachment and
fresh-node-only derived syntax admission at their original construction points.
Next: sole consolidated text review, then sanitized public inputs and fresh
isolated student from accepted Chapter10. No student launched yet.

## Sole text review incorporated; student inputs next

The sole text reviewer `ch11-text-review` submitted on join 2007; its exact 10,289-character report is saved in `ch11-text-review.md` and the child was shut down. Required clarification distinguishes unconstrained declaration admission/name validation from deferred dependent-body checking. Incorporated precision on full declaration-subtree copying, parameter versus result annotation environments, and exact concrete matchtype identity. Corrected index review status. These are prose-only corrections; compiler/graders remain the verified job1988 inputs, with no re-review or redundant behavioral rerun.

Lesson: deferring generic body checking is not deferring declaration admission. A legal unused dependent body and an invalid declaration must remain distinguishable.

Provisional public export contains 127 byte-verified files, excluding compiler target/private notes/history/caches; refresh final prose and verify hashes before admitting the one isolated student from accepted Chapter10. Reconstruction and human acceptance have not happened.

## Fresh isolated Chapter 11 student admitted

Final public bundle job2068: 127 files copied and SHA-256 verified against finalized public sources; chapter/relationship-companion links resolve. Inputs exclude target compiler, private notes/history, caches, and build products. Prerequisite remains the separately verified 37-file accepted Chapter10 export. Sole fresh confined student `ch11-student` admitted job2069; submission join2070 is running. No reconstruction result yet.

Student imports only those two input subtrees and installed SDK/system toolchain/PEG dependency subtree. It alone edits candidate implementation; public grader/harness/fixture copies remain explicitly read-only. Required full retained Debug/Release, four PEG runs, spin11, integrity and structural review are stated in launch task. No parent candidate patches, new reviewers, installs, commits, pushes or later chapters. Human acceptance remains pending. Stop reruns when the applicable checks all pass.

### Student setup clarification and progress

Student `ch11-student` remains active on join2070; observed History cursor189.
Skills coding/spec-execution activated. Student reports public127-file manifest verified and165 copied files recorded with source/copy hashes. Initial SDK build reported installing an ASP.NET Core HTTPS development certificate despite local CLI home/packages/tmp and skip-first-experience setting. This is reported SDK output, not independently verified host trust or outside-sandbox effects. Question5 answered: continue same reconstruction, set DOTNET_GENERATE_ASPNET_CERTIFICATE=false for subsequent SDK calls, retain exact receipt, no dev-certs cleanup/trust or host certificate-store inspection. Initial model build is not language-feature acceptance. Progress report2098 clipped329642bytes; not a full source review. No parent candidate edits, new review, or student pass claim.

### Student toolchain clarifications and cumulative run in progress

Question6: direct CLT clang could not find stdio.h. Parent job2105 verified installed /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk and header; authorized explicit SDKROOT with direct CC under existing imports, local disposable compile/run check, no downloads or grader changes. Question7: Apple make launcher reported a denied xcrun cache write outside sandbox, but cumulative recipe continued. Authorized direct /Library/Developer/CommandLineTools/usr/bin/make for remaining walkthrough, preserving existing healthy job2141 rather than restarting. Denied write is not evidence of a created host file. Lesson: direct tool paths plus explicit SDK/cache environment avoid reliance on launcher discovery; setup warnings remain separate from compiler behavior.

Student reports focused Chapter11 generics17 executions/10 rejections, typeof17/8, matchtype18/13 plus selected-only IR1/1 and3/3 controls all pass. Local student model probe9/9 is supplementary, not equivalent to author1108 checks. Full cumulative child2141 and independent PEG child2147 remain running at last observation; no cumulative acceptance yet. Asked student to reconcile128 public-file count with127 manifest entries explicitly. Same student/join2070, latest observed cursor561 (job2150 report clipped222825bytes, not full source review). No parent implementation/grader edits, no replacement student or re-review.

## Independent reconstruction complete; human acceptance pending

Sole student submitted on join2070 and shutdown confirmed. Parent2168 read full report and terminal receipts: cumulative2141 exit0 both clean configurations; PEG2147 four exit0 processes (50/155/5/11); walkthrough2163 exit0 exact five lines and empty stderr; final integrity2164 public128/128 (127 payloads plus manifest), compiler39/39 unchanged during campaign. Full report retained in ch11-student-acceptance.md. No parent candidate/grader edits, no post-pass rerun, no merge of candidate into reference.

Integrated student lessons into chapter construction/setup: preserve stable outgoing holder rather than multiply consuming transient Move; explicit macOS SDK/direct tools and certificate suppression before SDK first use. These prose-only clarifications follow assisted success, not a new reconstruction claim. Both reviews complete; human walkthrough/print acceptance and commit permission pending. No commit/push/install/later chapter work.

## Human acceptance and local commit authorization

Bill read Chapter 11 and ran `spin11`, reporting that both look good. He then explicitly authorized the scoped local commit. This supersedes pending human acceptance and commit permission above. Physical-print inspection remains unreported. No compiler changes or test reruns were made during commit preparation; the verified reference and separate passing student are preserved. No push, installation, or next-chapter work is authorized.
