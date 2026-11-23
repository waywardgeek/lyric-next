# Chapter 9 code-review follow-through

Exactly one fresh privileged code reviewer, lyric-ch09-code-review-01, submitted
the consolidated report saved in ch09-code-review.md and was shut down. No re-review.

R1: WriteIr previously relied on LowerIr's earlier validation despite editable drafts.
It now revalidates structure, operations and execution before serialization. Direct
publication probe lowers real source, adds a detached draft, requires refusal with
empty output and unchanged draft, repairs it and publishes successfully.

R2: Constant validation now requires absent unit payload, Boolean bool payload, or
representable Integer payload for an integer type. Other Constant types are refused;
byte payloads remain NewArray. Uses shared SemanticTypes.Represents, not copied bounds.
Four synthetic baseline Constants now have valid literal payloads. Isolated malformed
cases cover missing/mismatched payloads, ranges and unsupported types; valid signed
and unsigned boundary controls are included. No public requirements were weakened.

R3: spin09 now follows body child relationships rather than arena allocation order.
It checks NewArray/Move/FreeArray/Return order and their operand relationships, and
checks that reversing serialized arena order does not affect the sequence. Chapter
teaching integrates publication-state validation, Constant contracts, and this
execution-order distinction at their original construction points.

Also corrected stale immediate-consumer comments: cleanup may intervene before the
one designated consumer. Scalar evaluator name-based dispatch is a reviewer preference,
not a required correction: the ownership evaluator already tests semantic identities.
No additional reviewer campaign or language feature was introduced.

Verification: job2907 exit0, real-model175 checks each Debug/Release, zero warnings/errors
(review-model.txt). Job2909 exited 0: cumulative make test in Debug and Release,
corrected make spin09, and whitespace checking passed. Saved summaries show clean
builds, 21 scalar IR, 92 ownership IR, 33 IR-C and 212 layout executions per
configuration, with all earlier retained suites. Inputs remained frozen through
the terminal receipt; review-cumulative.txt and review-spin09.txt retain results. Prior sanitizer
212/33 results predate these local validation/walkthrough fixes; not claimed rerun.

Lessons: validation certifies a graph state, not all future edits; negative fixtures
must satisfy unrelated contracts; execution order follows region links, not arena order.

One text reviewer lyric-ch09-text-review-01 submitted and was shut down. Exact report
is ch09-text-review.md. Incorporated R1 evidence timing, R2 actual text-mode decoding
and universal-newline comparison, R3 implicit-restore/offline setup distinction,
and narrowed walkthrough diagnostic-check wording. Disclosed Chapter8 doubling
prerequisite refresh in chapter and isolated setup. These are documentation-only
corrections; no executable grader or compiler change and no re-review.
