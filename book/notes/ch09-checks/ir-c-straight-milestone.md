# First IR-backed C bodies

Before this edit, cumulative job 2767 exited zero. Its saved
transfer-validation-cumulative.txt covers Debug/Release with clean builds,
212 layout executions, allocation/balance checks, 21 scalar IR and 92 ownership
IR cases per configuration. C emission was still syntax-based in that run.

EmitC now calls LowerIr before writing output. Eligible straight-line scalar
functions use their actual IrBody: Constant, Local, Load, Store, Call and Return.
The existing function ABI and runtime frame contract are preserved. Parameter
locals navigate actual symbol identities; each read/call becomes a separate C
statement, retaining snapshots and left-to-right effects. No ownership inventory
is added to the IR emitter. Other bodies temporarily use old emission; this
migration fallback must be removed before chapter completion.

Public c_emission.py checks body markers and compiles/runs five fixtures at O0/O2.
Identity, mutable storage, ordered calls, expected assertion failure with trace,
and mixed-path ABI pass Debug (2784) and Release (2785). The latter job also
passes retained 21 scalar and 92 ownership IR cases in each build and diff check.
Builds are clean. Saved logs are ir-c-straight-*.txt. These are focused results,
not a full cumulative verification of the new emitter.

Lesson: valid C alone cannot establish use of the intended emission path. During
migration explicit path evidence is needed, alongside exact runtime and failure
contracts. The full alias-sensitive holder-state proof remains unfinished;
existing structural/type/definition/transfer validation and independent ownership
execution tests support this baseline, not a general memory-safety proof.

Next: scalar arithmetic/conversions, structured control, array/reference/object
operations and generated destruction; remove syntax emission and its ownership
reconstruction, then cumulative/runtime validation and finished teaching.

## Scalar arithmetic extension

Unary/binary/Convert now emit from resolved IR operation tags, using existing
runtime helpers. Widening casts are explicit, narrowing safe casts check bounds,
wrapping casts reconstruct low bits. Mixed signed/unsigned comparisons test
negativity before magnitude conversion. Safe shifts check counts; explicit
wrapping arithmetic remains modular in unsafe mode. Values remain sequenced
C temporaries. The old syntax-path formulas are temporary migration duplication,
not the intended final architecture.

Nine C-emission cases pass Debug (2788) and Release (2789), each at O0/O2.
Debug retained expression suite passes 250/250 and unsafe suite 87/87 (2789).
Logs ir-c-arithmetic-*.txt retain evidence; job 2789 exited zero. This is not a
full cumulative run after the emission change. The earlier mixed-body fixture
now emits both bodies from IR; its runtime expectation is unchanged.
