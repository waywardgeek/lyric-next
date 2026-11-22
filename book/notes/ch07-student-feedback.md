# Assistance and teaching record

Only the frozen public Chapter 0–7/spec/grammar/graders/tools/unicode and accepted Chapter 6 input was used. The supplied environment paths and setup constraints were followed. No target solution, private repair history, other agent workspace, network, installation, deployment, or reviewer was used. No implementation was provided by the orchestrator. All implementation edits were made by this student.

## Failures and lessons

1. After array/value/reference lowering, Chapter 7 passed 84/102. The remaining 18 cases accepted unsafe borrows because the lifetime graph had not yet been implemented. Successful pointer execution is not evidence of stable-reference safety. The separate graph with ordered helper summaries brought Chapter 7 to 102/102.
2. The first cumulative run failed Chapter 6 `loop-condition-break-assigned` (249/250 execution cases): CheckBorrows registered loop targets after traversing the condition, but condition expressions can contain break. Register the loop before traversing either condition or body. This is an implementation mistake, not a teaching/grader defect. An initial chat label called this the Chapter 4/6-mode suite; inspection corrected it to Chapter 6's execution suite. Chapter 4's separate 79-case semantic suite passed after the correction.
3. Structural review found recursive short-circuit traversal in the new graph walker. It now uses staged iterative traversal, preserving the earlier long-expression design. Resource guarantees apply to every phase, not just parsing and ordinary checking.
4. Replacing the scalar enum with a depth-bearing immutable type required updating enum switches/constant-pattern uses and default-value assumptions. Type depth and scalar kind are authoritative in one value; ordinary array conversions remain exact.
5. Dataflow tracks possible invalidations rather than lexical alias lifetime. Acquiring a fresh handle clears only its history; deriving an alias first uses the old handle. Ordered helper obligations preserve safe write-before-grow and reject grow-before-write only when actual paths overlap.

## Teaching feedback

The published distinction between descriptor location and backing selection depth was sufficient to reconstruct copy/alias behavior. The explicit rule to acquire reference formals at entry and check ordered obligations before applying possible mutations was essential for safe helper ordering. The chapter's limited precision for sibling indices is explicit and retained. The cumulative prerequisite case usefully catches condition transfers; emphasize that loop admission precedes condition traversal when teaching any new flow analysis.

No contract change or grader correction was requested. Optional sanitizer, allocator-balance, injected-OOM, portability, and human acceptance checks were not performed in this trial; historical checks described by the chapter are not claimed as this candidate's evidence.
