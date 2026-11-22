# Chapter 8 class-buffer doubling follow-through

Bill selected 2x growth for class backing arrays while reviewing Chapter 8. Ordinary source-level arrays retain approximately 50% growth. Bill separately accepted Chapter 7 and reported running spin07.

## Implementation and reason

`compiler/Emission/Compilation.cs` generates class storage through the shared array reservation helper; searching only for direct realloc calls initially missed these callers. The existing helper leaves spare capacity untouched. Class allocation initializes only the selected slot, and growth follows exhaustion of the old high-water capacity.

The new generated `ly_class_reserve` computes a doubled capacity, clamps it against `SIZE_MAX / stride`, and delegates to the existing checked reservation helper. Metadata, SoA field buffers, and the AoS chunk directory use this policy. The AoS record chunks remain 64 objects. No new persistent state or second allocation/error implementation was introduced.

Lesson: lazy spare capacity required preserving existing behavior, not adding eager zeroing or a new allocator. This preserves an opportunity for demand paging; realloc behavior and physical-memory savings remain platform/workload questions.

## Retained check and observed failure

`graders/ch08/allocation.py` now retains `class-doubling-growth`: 257 simultaneously live ordinary one-field objects, with reads checked by assert, both layouts at O0/O2. Exact successful execution requires empty stdout and allocation-count stderr of 19 for AoS or 20 for SoA. This is an explicit bootstrap allocator-policy contract, not a private-helper-name assertion.

Before the implementation change, job 2233 failed the new AoS check as intended: successful program execution counted 25 allocation calls instead of 19. After the change, job 2234 built cleanly and passed all four reuse and four growth comparisons.

## Final verification

Job 2236 completed with exit code 0:

```
make test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet
```

Full output is retained in `ch08-checks/class-doubling-cumulative.txt`. Both Debug and Release builds had zero warnings/errors and passed all retained suites through Chapter 8, including 112 array executions, 154 class checking cases, 212 layout executions, four reuse plus four doubling comparisons, and 12 ownership-balance comparisons per configuration. Earlier source, Unicode, lexer, parser, semantic, execution and unsafe checks also passed as recorded there.

Chapter 8 construction teaching and the public grader contract were updated. No RSS measurement, speed benchmark, new sanitizer run, separate Peggy run, or passing-student reconstruction rerun was performed. Human acceptance of Chapter 8 remains separate. Compact references and field-specific payload heaps remain future work. No installation or publication occurred.

## Subsequent human acceptance

Bill accepted Chapter 8 and reported running `spin08` after this follow-through. This closes the previously pending human-use boundary; it does not add a performance measurement or rerun the independent student.
