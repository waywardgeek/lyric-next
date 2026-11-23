# Pending transfer execution milestone

Local operation validation now checks both directions: every Move has one
ownership-consuming use, and every ownership-consuming operand for an array or
counted scalar receives a Move. Constructor/final self and reference parameters
remain borrowed. A missing-transfer Store probe increased the model checks from
145 to 146 in each configuration (jobs 2742/2748, transfer-consumer logs).

Execution validation separately tracks pending Move results alongside definition
availability. Consumption removes the obligation. Return requires no pending
transfers; Yield may preserve inherited pending transfers but cannot abandon a
new one. Continuing branches merge obligations; terminated branches do not.
Loop conditions and backedges preserve the transfers pending on entry, as do
break and continue. This is a structured-IR validation contract, not reasoning
about constant loop conditions. Calls evaluate all arguments before transferring
them, avoiding a stranded earlier argument when later argument evaluation exits.

The real-model probes first establish structure and operation validity, then test
that a sole consumer after Return is insufficient. A positive case puts empty
source cleanup between Move and Store, demonstrating that adjacency is not
required. Additional cases strand transfers with break and continue before their
sole store. Refusal preserves the graph. These four scenarios add seven checks,
including refusal nonmutation checks, reaching 153 per configuration.

Jobs 2756 and 2758 passed the initial focused implementation. Expanded job 2760
exited zero; saved summaries read in job 2764 report 153 model checks in Debug
and Release, 92/92 ownership inspection and 21/21 scalar inspection in both.
Builds report zero warnings/errors. Logs are pending-transfer-expanded-*.txt;
initial pending-transfer logs are retained. Diff and explicit changed-file
trailing-whitespace checks passed. The public validator boundary was updated.

Lesson: a transfer needs a legitimate source, a consuming operand, and a path
that actually reaches consumption. Each check addresses a different obligation.
None proves source-holder liveness, initialized storage, alias-safe replacement,
reference-count balance, or complete cleanup on every path. Those remain open.
No cumulative runtime rerun, C-emitter switch, review, student reconstruction,
commit, push, or tool-redaction change occurred.
