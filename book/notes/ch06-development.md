# Chapter 6 development — decisions and repetition

## Approved increment

Bill approved integer comparisons, boolean negation and short-circuit operators, while loops, break/continue, semantic flow analysis, and C emission. Functions already exist. Dynamic arrays and classes remain later increments. Chapter 4 human walkthrough was explicitly deferred; Chapter 5 reconstruction passed, human acceptance pending. No commit/push authorized.

## Expression construction

Kept real semantic facts authoritative; new work introduces no persistent model relationship. Extended the existing explicit-stack hint/check passes rather than reintroducing recursive expression traversal. Comparison results are bool but their destination does not supply integer context; neighboring operand hints do. Both logical operands are statically checked even when runtime evaluation skips one.

Emitter now uses staged short-circuit work: evaluate left; declare result outside the conditional; open an if for the required right-hand case; emit right inside; assign result and close. Arithmetic/comparisons still evaluate both operands left-to-right into temporaries. Mixed comparisons test signed negativity first, then compare nonnegative magnitudes as uint64_t; same-signed operands widen to int64_t or uint64_t. No C usual mixed-sign conversion determines Lyric comparison behavior.

Initial 9-case baseline 0/9 intended unsupported diagnostics. First build failed CS0136 from reused C# local name result; renamed logicalResult. Corrected Debug build zero warnings/errors and 9/9 execution cases at O0/O2 (job670). Retained Debug 79 semantic and 75 execution cases passed (job671). The Chapter5 runner accepts optional explicit case lists; defaults preserve all its former cases, enabling actual shared invocation/compilation/comparison logic rather than a new framework.

## Loop flow and emission

Temporary per-loop records retain the enclosing scope, possible-assignment sets for back-edges and breaks, and outer immutable assignment sites. Break goes to innermost loop exits; continue goes to innermost back-edge. Return goes to neither. Body fallthrough also contributes to the back-edge. Outer immutable assignment is rejected when that symbol can reach a back-edge already assigned. Body-local storage is fresh each iteration; ancestry distinguishes it from preexisting storage. Nested assignments are recorded for each applicable enclosing loop. Only symbols visible before a loop propagate to its outgoing possible set.

Zero iterations preserve the incoming definite set. Outgoing possible assignments union entry, condition-return paths and breaks; returns do not propagate. Constant condition truth is not used to prove definite entry/nontermination: even while true retains a conservative zero-iteration path for analysis. This is a stated implementation limitation, not runtime semantics. Existing unreachable-statement rejection remains unsupported behavior rather than a new language ban. Bare break/continue lack syntax anchors, so outside-loop diagnostics use the containing function declaration, matching prior bare-return location practice.

Generated while(true) contains condition evaluation then a guarded break at its top, followed by the source body block. Native C break/continue target the correct nearest generated loop; continue repeats condition effects. Existing function return emission restores activation frames, including returns inside loops.

Expanded baseline 13/27: all expression/operand rejection cases passed, loop cases unsupported. Job676 implemented loop checking/lowering and passed 27/27. Further width, branch-transfer, nested-storage and runtime-skipping cases are in progress (job677), not yet recorded as passed. No Chapter6 chapter body, reviews, student, or walkthrough yet.

## Verified control-flow milestone and expanded scope

Job677 expanded fixtures initially passed37/46; eight width fixtures omitted a declaration separator and empty-loop omitted a statement separator. Corrected fixtures, not parser; job680 passed46/46. Added graders/ch06/README exact contract and Chapter6 runner to make test. Job683 full cumulative Debug/Release plus separate PEG and whitespace exited0, collected after Bill's dictation interruption. Saved cumulative.txt confirms both configurations clean builds, Ch1 82, Unicode99826/all1112064/7 malformed, tokens58, trees66, recognition143, boundaries7, semantics79, Ch5 75, Ch6 46. PEG143/143. Job685 make spin06 passed; helper and target added, saved spin06.txt. Parent extracted summaries job686. No live jobs or children at this boundary.

Bill requested fleshing out expressions before arrays/classes and approved expanded Chapter6: division/remainder/computed negation, bitwise AND/OR/XOR and shifts, explicit wrapping operations, specified compound assignments, integer as/!as conversions, boolean equality, and value-producing if. Floating-point expressions, arrays/classes remain deferred. A dictated 'please do not proceed' was immediately clarified by Bill as 'please proceed', reconfirmed explicitly; work is authorized to continue. No reviews/student or chapter body yet. Do not freeze the earlier46-case scope as the completed chapter.

Next arithmetic slice: exact spec1784–1808 read. Division truncates toward zero, remainder dividend sign, zero failure in all modes; safe MIN/-1 division overflow, MIN%-1 zero before C instruction. Shifts result retains left width, safe count0<=c<N, bit-preserving unsigned lowering/sign extension without C UB or out-of-range signed casts; zero-count must not create width shift. Full unsafe execution mode not yet implemented, do not conflate explicit wrapping operations with adding an unsafe compiler mode. Other contracts require bounded reread when implementing. Prior standalone probe is historical, not verification of these unimplemented paths.

## Division/remainder and computed negation increment

Added 17 public cases before implementation: unchanged compiler passed 46/63; implemented checker/emitter passed 63/63. Added 24 runtime failure cases: Debug compiler passed 87/87, each executable tested at C -O0/-O2. Logs: division-baseline.txt, division.txt, division-runtime.txt in ch06-checks. Cumulative run still pending at this note.

Checker keeps explicit traversal, extends arithmetic hint propagation, computes exact BigInteger constants, diagnoses known zero divisor LYC316 before division, and preserves direct-negative-literal handling. Computed negation checks the operand's type. Emitter uses guarded division/remainder helpers; zero divisor LYR003, signed MIN/-1 overflow LYR001, MIN%-1 returns zero before C evaluation. Computed negation reuses checked subtraction from zero, including its diagnostic description. This avoids a duplicate overflow algorithm.

Lesson: C remainder is also undefined for MIN%-1; a mathematically correct zero result must be selected before executing C %, not afterward. Shared grader now accepts explicit runtime diagnostic/location/trace records alongside unchanged Chapter5 overflow cases. No review/student yet; expanded Chapter6 remains unfinished.


## Verified division and wrapping slices

Division cumulative job697 exited0: Debug/Release clean builds and all retained suites, including75 Chapter5 and87 Chapter6 cases. Separate PEG was not rerun for that slice.

Wrapping fixtures added before implementation: baseline job706 passed87/103, all16 new cases unsupported. Checker now uses exact modulo normalization and signed-bit interpretation; prefix !- does not use the direct-negative-literal shortcut. Runtime helpers use uint64 arithmetic, width masking and representable signed reconstruction (-1 minus the masked complement), avoiding C signed overflow and out-of-range signed casts. Job710 clean Debug build and103/103 execution cases at O0/O2. This is not yet a new full cumulative/Release verification. No model relationships added.

Lesson: a direct oversized unsigned-to-signed cast is not a portable definition of signed wrapping. Explicit reconstruction preserves the specified result, including i64 minimum, using only representable signed operations. Remaining chapter features and reviews/trial are unfinished.


## Shift increment

Added28 public cases: baseline103/131 (job713). First implementation build failed CS0128/CS0165 because a switch-local count collided with existing call-argument count. Renamed shiftCount; no tests ran from the failed build. Corrected job718 clean Debug build and131/131 Chapter6 cases at C O0/O2. Logs shifts-baseline.txt, shifts-build.txt, shifts.txt. Checker keeps left width and types count independently; known invalid count LYC317. Emission checks signed negativity then uint64 magnitude before invoking unsigned-bit helpers; runtime LYR004. Signed right shift explicitly fills high bits; zero avoids fill, never shifts by width. Reuses wrapping bit reconstruction. Full cumulative run follows, not yet established here.

Bitwise mixed-type result selection was not explicit in the spec search. Asked Bill whether to use existing whole-range operand-type rule; no answer recorded yet. Proceeding on settled shifts/casts rather than silently adopting that policy. No bitwise implementation yet.

### Wrapping/shift cumulative verification and cast construction

Job 721 completed successfully: both configurations passed the retained suites, including 75/75 Chapter 5 and 131/131 Chapter 6; separate Peggy comparison passed 143/143. Logs are `ch06-checks/wrapping-shifts-cumulative.txt` and `wrapping-shifts-peg.txt`.

Casts reuse ReadType, exact constant facts and WrapInteger. Hint discovery still visits the operand subtree, but its result hint comes from the destination type; actual operand checking receives no destination context. Emission evaluates only the value child once. Checked conversion branches on source negativity before comparing bounds; the final C cast occurs only after representability is established. Modular conversion uses defined uint64 conversion and the destination bit-reconstruction helper. No new persistent model state.

The unchanged binary passed 131/165 new-suite cases (job 728). The changed Debug build had zero warnings/errors, but the first run passed 157/165: eight authored fixtures accidentally tested unary minus AFTER a cast because postfix casts bind tighter. Corrected negative operands with parentheses; no parser/compiler change for those failures. Job 735 is the corrected test run, still pending at this note. No cast cumulative result claimed yet.

Mixed-type bitwise behavior is still awaiting Bill's answer: recommendation is the existing whole-range operand-selection rule; it has not been silently adopted.

Cast follow-up: corrected fixture run 735 passed 165/165; complementary signed-low/unsigned-high runtime bounds, nested arithmetic/hint traversal and operand-overflow-before-cast checks raised the suite to 173/173 (job 737, exit 0). These are Debug compiler/C O0+O2 results, not yet cumulative Release verification.

Boolean equality uses the existing eager left-to-right expression traversal. Only bool/bool == and != are added; mixed comparisons and bool ordering remain rejected. Baseline job 739 passed 175/177 (the two success fixtures failed). Implementation adds the narrow checker acceptance and direct C bool comparison, leaving mathematical integer comparison unchanged. Full cumulative build/test plus PEG job 742 is running with logs `casts-boolean-cumulative.txt` and `casts-boolean-peg.txt`; do not infer a pass from launch.

Next remaining work: compound assignments, value-producing conditionals, and bitwise after the unanswered mixed-type rule is settled. Existing CheckIf joins only continuing branches; value-producing conditionals must preserve these flow states. Existing CheckAssignment treats = target as nonread; compound assignment must instead require definite initialization and snapshot the old value before RHS evaluation. No Chapter 6 reviewer/student has been launched.

### Cast/boolean cumulative completion and compound assignments

Job 742 completed successfully: Debug and Release cumulative suites passed with 177 Chapter 6 cases; separate PEG comparison passed 143/143. Logs: casts-boolean-cumulative.txt and casts-boolean-peg.txt.

Compound assignment now resolves its local-name target once, checks initialized reads and mutability, checks arithmetic compatibility and no implicit narrowing, and retains the existing target fact. Emission snapshots the old value before RHS evaluation and reuses checked/wrapping helpers. No synthetic AST, duplicated lookup, or persistent state was added. Array/reference targets remain unsupported.

Public fixtures grew to 195. Unchanged-binary baseline job 749 passed 177/195; all 18 new cases failed as unsupported. A too-short authored overflow expectation was corrected to the existing `integer overflow in i8 addition` diagnostic before the implementation run. Job 753 built Debug with zero warnings/errors and passed 195/195 at both C optimization levels. Covers all widths/operators, effects, widening/narrowing, wrap boundary, initialization/mutability, bool rejection, known/runtime zero divisor, and runtime overflow. This is bounded evidence, not all compound operand combinations.

Lesson: compound assignment is an initialized read followed by arithmetic and a write, not an alternative initialization form. Preserve old-value evaluation before RHS effects and preserve the ordinary operation width.

Asked Bill to settle mixed bitwise and uncontextualized integer branch-result selection using existing whole-range operand-type inclusion; no response recorded yet. Do not promote this recommendation into a language rule. Cumulative compound verification follows in compound-cumulative.txt and compound-peg.txt; starting it is not a pass.

Live verification at decision boundary: job 757 owns the cumulative Debug/Release run, then PEG comparison and whitespace check. Collect this same handle; do not rerun merely to recover status. No other job or child is live. Targeted job 753 already exited 0 at 195/195; cumulative 757 is not yet a claimed pass.

Job 757 collected exit 0: both configurations 195/195 Chapter 6 and all retained checks; PEG143/143; whitespace passed. Bill explicitly adopted the recommended whole-range selection for bitwise and uncontextualized integer conditional results, and safe conversion to an expected conditional result type. Integrated at spec Sections 8/14 (control-flow and numeric rules), not a private fixture-only policy.

Bitwise baseline job762:195/211. Implementation job766: clean Debug build,211/211 at C O0/O2, exit0. Existing explicit expression stacks and arithmetic type selection extended for &,|,^; exact BigInteger bit operations for constants, uint64 bit operations plus existing width/signed reconstruction at runtime. No new persistent model state.

### Value-producing conditionals under verification

Public baseline job768 passed211/230;19 new cases failed. Added SemanticFact.CanContinue (readonly value; declaration in Common, constructor implementation matches) so block/conditional reachability accompanies checked type, without a second table or ownership changes. CheckBlock identifies final expression only when it is the final child (DiscardResult prevents this), retains scope and flow cleanup; CheckIf selects expected/common type across continuing arms. Early return/break arm excluded; all-transferring value-position If explicitly unsupported until full expression noncontinuation propagation. Statement position still supports both arms transferring. Emission keeps one result temporary outside branches and computations inside; continuing unit arm supplies unit, transferring arm does not assign.

Short-circuit checker moved to explicit stages: after left, snapshot definite/possible, check right, intersect definite/union possible with skipped path. This became necessary because value-branches can assign locals inside expressions.

Initial build772 clean,227/230: three failures were fixture locations, not semantic behavior. If has no keyword source anchor; diagnostics start at condition. Corrected fixtures to true, preserving parser model.

Focused runtime probes then found a REAL defect: conditional-result hints missing, so typed u8 branch plus unsuffixed1 widened accidentally to i32. Job775 failed0/3 (normal exit instead of expected overflow), including left/right operand order and branch-local tail. Extracted DiscoverHints (iterative arithmetic traversal) and added temporary conditional/branch-local hint analysis, no semantic symbols/facts/effects there. Actual checking still performs scope, name, flow and result checks once. Shared branch facts remain authoritative. Clean hint build777; corrected probes and full233-case suite live job778. Logs value-if-width(-baseline).txt, value-if-corrected.txt. Await actual result before claim.

Lesson: value-producing control flow needs consistent result inference, branch reachability, and skipped-path initialization joins; correct C branch placement alone is insufficient.

Corrected conditional job778 collected exit0: focused width3/3, complete233/233 at C O0/O2. First-run location-fixture failures and real missing-hint failure remain preserved separately. Expanded cumulative Debug/Release + PEG + whitespace verification now live job780, logs expanded-cumulative.txt and expanded-peg.txt. No full cumulative pass claimed yet. Next authorized work: complete those checks, expand spin06 and write Chapter6 teaching from actual implementation, then exactly one code review, one text review, fresh student from preserved Chapter5 candidate. No Chapter6 reviewer/student launched; no commit/push.

Expanded cumulative780 exited2 at Chapter4 semantic suite78/79: branch-value-unsupported now succeeds because Bill authorized that feature. This is an intentional contract transition, not an implementation regression. Original rejection fixture retained for chapter4/5; explicit --chapter=6 requires same source success as branch-value-supported. Make test selects6. Inventory documents old boundary milestone-only/new positive retained. Other checks unchanged; rerun required.

Bill suggested target compiler overflow support (interpreted dictation as Clang). Disposable probe job782 on Apple Clang21 ARM64 passed14 builtin add/sub/mul boundary cases at O0 and O2 with UBSan, no diagnostics. Probe /tmp/lyric-overflow-builtins.g0QS76. No code switched: recommended simpler builtin-based backend with explicit compiler-support prerequisite, asked Bill whether to adopt instead of maintaining portable fallback. Await answer. Division/shifts/casts still need separate guards.

### Expanded cumulative verification and practical walkthrough

Corrected cumulative job 788 exited zero: Debug and Release builds each had zero warnings/errors; each passed the retained suites including 79 semantic cases with the Chapter 6 selection, 75 Chapter 5 execution cases, and 233 Chapter 6 execution cases. The separate PEG comparison passed 143/143. Logs: `ch06-checks/expanded-cumulative-corrected.txt` and `expanded-peg-corrected.txt`. The earlier milestone-expectation failure remains recorded separately.

Expanded `tools/spin06.py` demonstrates compound loop updates, a string-valued conditional with remainder, and explicit wrapping/bitwise/shift/modular-cast behavior, alongside the existing short-circuit and loop rejection examples. Job 793 ran `make spin06 DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet` successfully; output is in `ch06-checks/expanded-spin06.txt`. This is the author's command verification, not Bill's human acceptance.

The proposal to use checked-arithmetic compiler builtins remains awaiting Bill's decision on the supporting-C-compiler prerequisite. No builtin-based lowering has been installed, and no review or fresh student trial has begun.

### Approved checked-arithmetic compiler requirement

Bill agreed to require a compiler supporting the facilities we need. Replaced manual checked add/subtract/multiply guards with typed-result overflow builtins. Generated C checks `__has_builtin` for all three; no portable fallback retained. Exact-width output pointers enforce narrow overflow despite C promotions. Lyric diagnostics, evaluation order, constant checking, division/shift/conversion guards remain unchanged. Apple Clang 21 is the actual tested C compiler; .NET SDK 10.0.403 builds our C# compiler.

Job 797 exited zero: Debug build zero warnings/errors, Chapter 5 75/75 and Chapter 6 233/233 at O0/O2. Logs `builtin-build.txt`, `builtin-ch05.txt`, `builtin-ch06.txt`. Job 803 is the separate full Debug/Release, PEG, walkthrough, and supplemental UBSan run; do not treat its launch as success. Chapter 6 teaching now written from the constructed increment, reviews/student still pending.

### Loop condition effects found during author readback

Job 803 completed all builtin-backend cumulative, PEG, expanded walkthrough, and supplemental UBSan checks at 233 cases. Author readback then found the loop was registered after checking its condition. Since value-if permits statements in conditions, repeated immutable writes there escaped back-edge checking. Three focused retained cases gave baseline 2/3 (invalid source incorrectly emitted C), then 3/3 after registering the loop before the condition.

A second focused check showed condition-break exits were missing from postloop definite joins: an uninitialized read was incorrectly accepted by --check (saved exit0 baseline). Added temporary definite snapshots on break and intersected them with the continuing condition path. This preserves initialization by the mandatory first condition evaluation but does not assume every exit passed that arm. Added rejecting and accepting condition-break cases. Job 813 clean build and focused corrected checks exited zero; full suite now 238 cases, new cumulative verification next. Lessons integrated into the original loop section and public contract, not deferred to a patch appendix.

Pre-review job 814 exited zero after the condition-flow fixes: full Debug/Release retained suite, Chapter 6 238/238 each, separate PEG 143/143, expanded spin06. Saved pre-review summary links the exact logs. Reviewer phase begins only now; no independent reconstruction yet.

## Single code review and corrections

The single confined code reviewer completed and was stopped. Its report is retained in ch06-code-review.md. It independently built cleanly and passed the previous 238-case suite, then demonstrated three missing combinations: RHS effects bypassed an outer immutable store check; context replaced typed conditional operand widths; and branch preview missed noncontinuation through discarded statement-if.

Added 12 retained cases: unchanged reference passed only 1/12 (returning-arm control). Corrected CheckAssignment with post-RHS immutability validation, operand scheduling with intrinsic hints before neighbor/destination context, and BranchHint with statement-if continuation. No new persistent state or emitter re-inference. Clean build and focused 12/12 verified in review-build.txt and review-focused.txt. Baseline retained in review-baseline.txt. New inventory 133 successes +52 rejections +65 runtime failures =250. Teaching integrated at assignment/hint construction points. Cumulative post-code-review run job846 is pending; not yet a full pass. No re-review will be requested.

Post-code-review job846 exited 0: both configurations clean, 250/250 Ch6 and all retained suites; separate PEG143/143 and spin06 passed. Summary/logs preserved under ch06-checks/post-code-review-*. No second code review. Next single text review.

## Single text review incorporated

No required construction or supported-contract correction. Fixed stale review status in the public grader README. Incorporated optional worked condition-break definite-set join and repeated the inherited student source-access boundary. Report preserved in ch06-text-review.md. Prose-only changes; post-code-review cumulative evidence remains current. Lesson: a concrete pair of exit sets makes the distinction between continuation and definite initialization easier to reconstruct. Independent student reconstruction and human acceptance remain pending.

## Fresh reconstruction admitted

Child lyric-ch06-student-01 admitted as job877, confined; no reference target or private notes supplied. Frozen bundle /Users/bill/ensemble-edition3-cadence/data/agents/Cadence/ch06-student-input has219 hashed files including26 byte-identical prerequisite files from preserved Chapter5 student. Manifest SHA256 da3b27f4a16c39dfebfae855be0a63960fe321bdeb0af1bc8534d8ae809d06af. Public chapters/contracts/graders/Unicode/offlinePeggy and runtime-only SDK/CLT/resolvedNode imports supplied. Existing sandbox-local cache and Node environment guidance supplied as setup, not implementation help. Student owns candidate changes; author must not edit it. Both reviewer children stopped. Reconstruction and human acceptance pending; no commit/push.

### Student setup clarification

The Chapter 6 student verified the frozen manifest and all 219 inputs, then asked for the exact already-imported Homebrew library directories needed for `DYLD_LIBRARY_PATH`; the launch instructions referred to those directories without listing them. Cadence supplied the existing versioned `lib` paths and repeated the sandbox-local empty `OPENSSL_CONF` setup. This was setup assistance only: no new access grant, language clarification, grader change, or implementation guidance. Lesson: sandbox authorization and reproducible tool setup instructions are separate obligations. Student completion remains pending.

### Independent reconstruction complete

Fresh student `lyric-ch06-student-01` submitted and was stopped after passing; its candidate and logs remain preserved in its assigned directory. The complete report is retained in `ch06-student-report.md`. Parent read the saved full cumulative log, retained PEG log, walkthrough, integrity log, and failed PEG setup log without rerunning the candidate. Debug and Release builds each have zero warnings/errors and pass 250 Chapter 6 cases, 75 Chapter 5 cases, 79 semantic cases with `--chapter=6`, and all retained source/Unicode/token/parser suites. Separate PEG passes 143/143; spin06 passes, including its expected static rejection.

Parent independently verified the frozen manifest digest, all 219 frozen entries, all 193 copied public noncompiler entries, and all 26 final candidate hashes. Only four candidate compiler files changed. No grader changes, requirement clarifications, or implementation assistance were supplied during this trial. Runtime setup assistance comprised the listed Homebrew library paths. The student independently corrected a failed system-make Node launch by invoking Node directly with its authorized loader environment; the failed log is preserved. A default 50-fixture PEG run was not counted as the required 143-fixture run, which was subsequently executed and verified.

Lessons: exact runtime setup matters even when grants are correct; a passing default checker invocation does not establish that the required fixture inventory ran. One successful independent reconstruction is useful bounded teaching evidence, not full-language correctness or human acceptance. Working status pages are updated; the frozen bundle remains unchanged. Human `make spin06` and acceptance remain pending. No commit, push, or Chapter 7 work was performed.

## Human acceptance

Bill reported, “Chapter 6 looks good.” Chapter 6 is accepted and closed after his reading. This records acceptance, not a claim that he ran the manual walkthrough. Earlier pending statements describe historical milestones. No student rerun or compiler change accompanied acceptance.

## Unchecked C backend and performance direction

Bill requested the unsafe flag now to expose straightforward C operations, plus static-inline small helpers. He clarified the contract: undefined or implementation-defined behavior is permitted only for cases safe mode would reject or diagnose; valid computations retain their meaning. This supersedes the earlier unsafe wrapping, normalized-shift, and retained-zero-divisor guarantees. Explicit wrapping operators remain defined. Safe shift counts still fail outside the operand width; the proposed count-normalization redesign was not adopted. CPU masking is not a C-language guarantee.

The selected mode is stored once on SemanticAnalysis and used by both constant checking and emission. Invalid unsafe numeric constants remain unfolded, avoiding host divide-by-zero or fabricated wrapped facts. The backend emits direct ordinary arithmetic/casts, omits index/shift/range checks, preserves unsigned left-shift lowering and MIN remainder -1 equal to zero, and marks small numeric/count helpers static inline. Ownership, borrow analysis, allocation failures and allocation-size checks remain. Root run/emit-c scripts accept --unsafe and --soa before the filename. The specification and construction teaching are updated at their original teaching points; no passing student was rerun.

The initial target scope is Clang on x86-64 and ARM64. Actual checks here use Apple Clang on ARM64; no x86-64 execution or performance gain is claimed. Performance on representative applications is an acceptance goal, not inferred from correctness tests.

Initial unsafe grader failed at the absent CLI option. First implementation built clean and passed 43 cases, then 51 with retained borrow/type checks and emission-shape checks. The chapter-aware version passes 83/83 in Debug and Release, and 44/44 at the Chapter 6 boundary. Release retained suites pass 79 semantics, 75 Chapter 5, 250 Chapter 6, 102 Chapter 7, and 136 Chapter 8 layout executions. Launcher tests passed marked C, no checked-add calls, exact output, paths with spaces, and duplicate-option refusal. Debug retained suites also passed 79/75/250/102; valid-input UBSan passed the 83-case mode suite. The updated make test completed successfully in both configurations, including the retained source, Unicode, token, parser, semantic, execution, and unsafe suites. Receipts and summaries are retained under ch06-checks/unsafe/. The separate PEG comparison was not rerun for this numeric/backend change. Exceptional unsafe cases are emission-only, never executed as a test with an expected undefined result.
