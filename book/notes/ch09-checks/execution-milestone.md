# Reachable execution validation milestone

`IrProgram.ValidateExecution` runs after structural and operation checks in LowerIr.
An iterative action stack walks regions with sets of executed definitions. Branches
receive independent sets and export only their explicit If result. Loop conditions
execute first; their definitions can reach body and post-loop cleanup. Break exits
intersect with the false-condition exit. Body-only definitions cannot escape.
Continue is a backedge, not an exit. Every reachable use requires an available
producer; function/branch/condition fallthrough without its required exit is refused.
Loop-body fallthrough is permitted. Transfer suffixes are unreachable for this pass,
while the existing structural/type passes still inspect them. Completion callbacks
are queued too, avoiding hidden recursive unwinding through nested regions.

Correction: the first version rejected break/continue from loop conditions because
the independent evaluator did not handle them. Actual CheckWhile (Check.cs 1456ff)
registers the loop before checking the condition and explicitly supports condition
breaks. The checker, not that evaluator limitation, defines accepted source behavior.
The restriction was removed; exit-set intersection preserves the existing contract.
Source-level condition-transfer coverage was subsequently added as recorded below;
model coverage alone was not treated as end-to-end evidence.

Initial Debug acceptance job 2696 passed 85 ownership and 18 scalar cases. Job 2698
passed both configurations and 136 model checks. After correcting condition transfers,
job 2704 passed Debug/Release builds (zero warnings/errors), 85/85 ownership and 18/18
scalar cases. Job 2706 added isolated break/continue condition probes, passing 140
model checks in each configuration. The execution-transfer-* logs retain this evidence.
Seven malformed executable graphs pass shape/type validation before execution refusal;
positive controls include branch results, loop-condition exports, unreachable suffixes,
and condition transfers. Refusal checks preserve the program/body relationships.

This is not storage-initialization, ownership-provenance/liveness/balance, or eventual
loop-termination proof. Constant conditions are not used to prove unreachable exits.
No new cumulative campaign (last 2567), C backend migration, review, reconstruction,
commit, or push. Next inspect and extend source-level condition-transfer grading,
then continue ownership validation and the remaining Chapter 9 work.

## Source condition transfers

Job 2709 retained the scalar baseline: 19/21, with condition break and continue
failing in the evaluator while condition return already passed. Both evaluators
now handle matching condition Break/Continue at the loop and propagate other
transfers. Three scalar cases and three array-owning branch cases cover condition
break, continue, and return. Jobs 2711 (Debug) and 2712 (Release) exited zero:
21/21 scalar and 88/88 ownership cases in each configuration. Existing compiler
assemblies were unchanged; this slice changes public graders only. Saved evidence
is condition-transfer-{baseline,Debug-scalar,Debug-ownership,Release-scalar,Release-ownership}.txt.
The array cases verify cleanup for owners created within condition branches.
An early break bypassing a later condition-owned temporary remains a separate
edge case to investigate. No full cumulative or C-backend execution claim is added.

## Bypassed condition owners: lowering correction

The short-circuit bypass passed (job 2715), but the eager comparison baseline
(job 2716, collected on resumption) failed: 89/90, with ValidateExecution refusing
an operand whose definition never executed. Post-loop cleanup assumed every
condition holder existed. A break in the condition can skip later allocations;
empty-holder cleanup is safe, but nonexistent-holder cleanup is not.
Lowering now puts false-condition cleanup inside an explicit false arm before
Yield. The true arm retains condition owners through the body; backedges and
transfers retain their explicit cleanup. No post-loop condition cleanup remains.
Only owner-bearing conditions need the added If. Availability validation was
not weakened. Tests include first-iteration and later-iteration break, early
continue, branch-local array cleanup, and the prior condition-lifetime cases.
Jobs 2720/2721 exited zero: Debug/Release 92/92 ownership and 21/21 scalar cases,
140 model checks per configuration. Build summaries report zero warnings/errors.
Evidence: condition-cleanup-* and condition-eager-bypass-baseline.txt. This is
focused IR verification, not new cumulative or IR-backed C evidence.
