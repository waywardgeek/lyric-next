# IR-only C emission boundary

The C emitter now handles nominal values, Ref/Unref, allocation, logical SelectField
places, fixed AddressOf borrows, explicit destruction, and generated ReclaimObject.
A logical place stores a stable handle and resolves field storage when used; a fixed
address captures a pointer once. This preserves field assignment across growth and
prevents fixed borrows from being silently revived. Counted locals start empty;
parameter locals receive the existing caller ABI value. Generated destruction uses
its existing unnamed receiver Local mapped to the C index parameter.

Runtime destruction wrappers retain live-state/reentry checks and mark destroying,
then call the generated IR body. That body alone invokes the hook, drops fields,
and reclaims the slot. Reference-count release still clears its holder before
possibly entering destruction. No duplicate hook/field cleanup remains in wrappers.

Job 2840 exit 0: Debug retained class layouts 212/212 (AoS/SoA, O0/O2), plus prior
30 C fixtures. Then removed all syntax-body fallback, Expression/Block/Conditional,
old storage selection, and emitter-owned ownership lists/cleanup inference. Shared
runtime/layout/arithmetic helpers remain. Unsupported IR is an internal failure;
output remains buffered until successful emission.

Expanded 33 C cases initially passed 32/33: the new class-place fixture destroyed
another same-class instance and correctly received LYC330 before emission. Corrected
the fixture to test growth without class-wide destruction. Ordinary objects are not
automatically reclaimed; this case is not a leak test. Job 2846 exit 0: clean Debug
build and corrected 33/33 at O0/O2. Original failed log retained.

Lesson: execution and cleanup now have one lowering authority. Metadata still comes
from checked declarations and source origins; that is not syntax-based execution.

Full IR-only cumulative make test job 2847 exited 0. Both Debug and Release built
with zero warnings/errors and passed the retained suite, including 212 layout
executions, 12 ownership-balance comparisons, 21 scalar IR, 92 ownership IR and
33 C-emission cases per configuration. Saved log: ir-c-only-cumulative.txt.
Model probe job 2849 exited 0: 153 checks in each configuration.
Sanitizer job 2857 exited 0: Debug ASan/UBSan 212/212 layout executions and 33/33
C-emission cases. Saved logs: ir-c-only-sanitizer-layout.txt and
ir-c-only-sanitizer-c.txt. These are concrete tested executions, not a general
ownership proof or measurements of performance. No jobs remain from this campaign.
Next: complete teaching and walkthrough, then independent review and reconstruction
within human authority.
No commit, push, new language feature, optimization or completed-chapter claim.

Teaching follow-through: chapter-09.md drafted with construction rationale,
exercises, explicit validation limits and tested Take it for a Spin. Job 2864
passed direct spin09; job 2866 passed make spin09 (build first), saved in
spin09-first.txt and spin09-make.txt. This is agent demonstration, not human use.
Public grader guide's obsolete migration statements corrected. No behavioral
fixture changes followed the full cumulative/sanitizer campaign; the C grader's
module docstring was corrected to match its existing stdout/failure contract.
Independent review and reconstruction require authorization to delegate.
