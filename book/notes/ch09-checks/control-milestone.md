# Control value validation milestone

Control operands are values, not storage addresses. If requires bool. Return
uses the source function result, or unit for a generated destruction body. Yield
follows its immediate region relationship: Then/Else returns the owning If type;
While Condition returns bool. Function-body and loop-body yields are refused.
The destination relationship is authoritative; expression type alone is insufficient.
No cleanup or control execution behavior changed.

Six isolated malformed probes and two repaired-region checks bring the real-model
probe to 121 checks each in Debug and Release. Both compiler configurations build
with zero warnings/errors and pass 85/85 ownership and 18/18 scalar IR cases.
Job 2689 exited zero; summaries were read in 2690/2691. Logs use control- prefixes.
The operation probes isolate contracts, not complete executable control paths.
No full cumulative run, emitter migration, review or reconstruction occurred.

Next: execution availability and path termination, then ownership provenance and
balance. Loop condition definitions may be used by sibling body and post-loop
cleanup. Lowering can append unreachable cleanup after transfers; availability
must reason about executable paths rather than impose a blanket lexical ban.
