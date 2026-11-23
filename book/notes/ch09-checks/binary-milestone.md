# Binary operator validation milestone

Binary validation follows existing checker semantics, not a uniform equal-type
rule. Arithmetic selects one existing operand type via ArithmeticType; shifts
preserve the left operand type. Comparisons can compare integer ranges with no
common arithmetic type, including i64/u64. Boolean comparison allows equality
and inequality only. Every operand must be a typed Value. Unknown tags and eager
short-circuit operations are refused; short-circuit behavior uses structured If.
Wrapping tags remain distinct from ordinary arithmetic. No constants are evaluated
here and this does not prove overflow safety, availability, or ownership.

The real-model probe passes 113 checks in each configuration: all 19 legal integer
operator tags, two boolean equality tags, and 12 isolated malformed cases were
added to the previous 80 checks. Each malformed node is removed and the graph
revalidated. Debug and Release each pass 85/85 ownership and 18/18 scalar inspection
cases, with zero build warnings/errors. Jobs 2680 and 2681 exited zero; saved
summaries were read back. Logs use the binary- prefix in this directory.
No full cumulative campaign, emitter switch, review, or reconstruction occurred.

Next: control value contracts, then execution availability, termination and
ownership analysis. Existing while-condition sibling lifetimes and transfers
across intervening cleanup must remain valid.
