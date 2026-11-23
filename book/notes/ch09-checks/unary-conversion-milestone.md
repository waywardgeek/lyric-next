# Unary and conversion validation milestone

The validator now checks value operands and the exact unary/conversion tags.
Logical negation requires bool; integer negation preserves its operand type.
`widen` uses the shared CanWiden contract; `as` and `!as` require integers.
Unknown tags are rejected without collapsing wrapping and ordinary operations.
These checks do not re-evaluate constants or prove execution/ownership safety.

Six isolated malformed real-model nodes are refused, removed, and followed by
successful revalidation. The probe passes 80 checks each in Debug and Release.
Both compiler configurations build with zero warnings/errors and pass 85/85
ownership inspection cases and 18/18 retained scalar inspection cases. Logs are
`unary-conversion-{probe,build-debug,build-release,ownership-debug,ownership-release,scalar-debug,scalar-release}.txt`.
Jobs 2670 and 2671 exited zero; summaries were read back in 2671 and 2673.
No full cumulative run, C-emitter migration, review, or reconstruction occurred.

Next: binary arithmetic uses ArithmeticType, shifts preserve the left type, but
integer comparisons accept mixed ranges without requiring an arithmetic common
type. Short-circuit operations must remain structured If, not eager Binary.
Then control contracts, execution availability and ownership validation remain.
