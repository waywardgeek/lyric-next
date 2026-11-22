# Chapter 7 array and reference graders

The retained cases currently exercise array construction, contextual empty arrays,
u64 lengths, integer/boolean/nested elements, the string/[u8] alias, returned arrays, recursive
copy independence, calls and control-flow ownership, immutable-storage rejection,
checked indexing, reserve/resize/truncate/push, typed default growth, and temporary
storage-selection protection, fixed reference aliases, scalar and array reference
parameters, and stable-storage lifetime checks across branches, loops and calls.
The current Chapter 7 suite contains 112 retained cases. Before the nine capacity additions, the reference
passed the earlier 102 cases and all earlier retained suites in Debug and Release, plus
the separate 143-case PEG comparison. The single code review is incorporated, including twelve alias-preview
regressions. The single text review and independent reconstruction are complete. The fresh student passed all required cumulative checks, PEG143, and the scripted walkthrough without implementation assistance or grader changes. Helper checks include ordered overlap,
forwarding, recursive effects and later-argument invalidation. No required checks
are waived.

From the repository root, supply an independent compiler command:

```sh
python3 graders/ch07/run.py "$DOTNET" PATH/TO/Lyric.dll
```

Use the Chapter 5 harness prerequisites and comparison contract: successful
emission has exit 0, nonempty C and empty stderr; programs compile and execute at
-O0 and -O2 with exact bytes and exit status. Each process has a 20-second bound.
Bounds failures require exit 1, exact located `LYR006: array index out of bounds`,
and the prescribed Lyric call trace. A crash or unrelated diagnostic is a failure.
Allocation-size overflow requires exit 1 and the exact located `LYR007: array
allocation size overflow` diagnostic and trace. Static `LYC324` cases reject
mutation of a root whose nested storage is selected during argument/RHS evaluation.
Lifetime rejections require the exact located `LYC326: Borrowed storage may have
been invalidated.` diagnostic. Direct invalidation reports the unsafe subsequent
use; helper overlap reports the call establishing that overlap. A generic
unsupported-reference diagnostic does not satisfy these negative cases.
The index expression uses its first anchored child for location. Tests and fixtures
are readable but student-read-only. All cases here are retained; none are
Chapter 7-only. See [Chapter 7](../../book/chapter-07.md) for construction,
required cumulative commands, and the walkthrough. Human walkthrough and acceptance remain pending.

The nine retained `capacity-*` cases require `capacity(array) -> u64` without
copying the selected descriptor, tight independent copies, reference observation,
and resize(0) releasing even reserved-empty backing. Truncate retains capacity.
Historical student results predate these nine cases; no passing student was rerun.

`capacity-selection-invalidation` is a retained rejection: a later index cannot
release backing holding an already-selected descriptor. Require LYC326 at the
indexed expression anchor, not a crash or a different diagnostic. The current
112-case suite includes this regression in addition to the nine capacity cases.
