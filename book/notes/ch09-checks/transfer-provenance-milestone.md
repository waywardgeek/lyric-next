# Local transfer provenance milestone

Move sources must be owning holders: owning Local, Copy, Ref, NewArray,
NewObject, Call, or If. Borrowed constructor/final self and generated destruction
parameters are excluded. A transfer is not a second owner. It must have exactly
one consumer in an ownership-consuming operand position: Store/ArrayPush value,
NewArray element, ordinary value Call argument, Return, or Yield. Reference
arguments and borrowed self do not consume transfers. Cleanup may intervene
between transfer and consumption; instruction adjacency is not required.

IrNode.UseAt exposes the existing reverse operand relationship directly, alongside
UseCount. No parallel use registry or whole-function user reconstruction was added.
The check is in ValidateOperations and does not mutate the graph. Borrowed-storage
release remains legal for replacement. This is not path-sensitive liveness,
initialization, complete provenance or ownership-balance verification.

Real-model probes isolate an unconsumed transfer, duplicate consumers,
nonconsuming observation, and borrowed source, repairing and revalidating each.
The positive use-backlink check raises the probe from 140 to 145 checks in each
configuration. Job 2731 (probe and Debug) and 2733 (Release) exited zero. Debug and
Release each passed 92/92 ownership inspection cases and 21/21 scalar cases.
Build/probe logs are transfer-provenance-*.txt; builds report zero warnings/errors.
No cumulative rerun, C-emitter switch, review, student run, commit or push occurred.

## Reciprocal consumer contract

Validation also visits ownership-consuming operands and requires an explicit Move
for arrays and counted scalars. Checking existing Move nodes alone cannot detect
a missing Move. Borrowed constructor/final self and reference arguments remain
nonconsuming. A new isolated probe supplies a correctly typed Copy directly to
Store, verifies refusal, removes that store/copy, and revalidates.

Jobs 2742 and 2748 exited zero. Debug and Release each pass 92/92 ownership
and 21/21 scalar inspection cases; the model probe passes 146 checks per
configuration. Builds have zero warnings/errors. Logs are transfer-consumer-*.txt.
This adds no path-sensitive liveness or balance proof and does not switch C emission.
