# Chapter 14 author safety-audit checkpoint

Cumulative Make job4007 terminated with exit0. Compiler and grader inputs remained
frozen through the terminal receipt. Both Debug and Release builds had zero warnings
and errors. Numerical summaries were extracted and inspected in job4021; the inline
job report alone was clipped. The first summary block below is Debug, the second
Release. The complete retained target passed, including the Chapter14 successor to
the historical aggregate-restriction suite. No invalid unsafe program was executed.

The bounded author audit traced initialization/persistent admission, ordered formal
alias effects and recursion, saved receivers, aggregate and return escapes,
ownership transfer/cleanup, relationship teardown, runtime access and allocation
arithmetic. Construction explanations and the required student enforcement map are
in chapter-14.md; inspected paths, focused experiments and limits are in
ch14-development.md. No additional demonstrated safety defect was found during this
audit. The compiler change remains removal of the temporary participant-aggregate
gate, reusing ordinary owning transfers rather than adding borrowed containers.

This is source-review and regression evidence for the supported safe subset,
assuming correct compiler/runtime and trusted relationship algorithms. It is not
formal proof, exhaustive testing, leak freedom, concurrency safety, completed
Chapter14 review/reconstruction/human acceptance, or evidence of the planned
pointer/index migration. Allocation-failure injection and class-index exhaustion
were not performed. Function-valued parameters, opaque bodies, escaping borrowed
aggregates and unsupported handle-array fields remain explicit refusal boundaries.
No reviewer/student was launched; no commit, push, installation or restart occurred.

## Extracted terminal summaries

```text
Build succeeded.
[39;49m    0 Warning(s)
[39;49m    0 Error(s)
82 passed; 0 failed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=58/58 token cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=70/70 syntax trees passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=155/155 syntax recognition cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=7/7 parser boundary cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=82/82 semantic cases passed
80/80 execution cases passed
250/250 execution cases passed
87/87 unsafe checks passed
112/112 execution cases passed
6/6 execution cases passed
154/154 Chapter 8 checking cases passed
212/212 layout executions passed
4/4 free-slot allocation and 4/4 class growth comparisons passed
12/12 owned-field/array balance comparisons passed
21/21 IR cases passed
92/92 ownership IR cases passed
33/33 IR C-emission cases passed
5/5 canonical cases passed; selfcheck rejected 18 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
17/17 generic execution cases passed
10/10 generic rejection cases passed
17/17 typeof execution cases passed
8/8 typeof rejection cases passed
18/18 matchtype execution cases passed
13/13 matchtype rejection cases passed
[?1h=1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=16/16 syntax recognition cases passed
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
10/10 object-field-migration execution cases passed
19/19 object-field-migration rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
[?1h=[?1h=[?1h=3/3 unsafe unwrap-check omission cases passed (not executed)
8/8 nullable-handle-unsafe-valid execution cases passed
0/0 nullable-handle-unsafe-valid rejection cases passed
4/4 execution cases passed
4/4 debug-mode contracts passed
64/64 debug object-identity contracts passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
18/18 Chapter 14 cross-call safety checks passed
7/7 Chapter 14 initialization safety checks passed
4/4 safe-initialized-length execution cases passed
0/0 safe-initialized-length rejection cases passed
3/3 Chapter 14 call admission safety checks passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=6/6 syntax recognition cases passed
Build succeeded.
[39;49m    0 Warning(s)
[39;49m    0 Error(s)
82 passed; 0 failed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=58/58 token cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=70/70 syntax trees passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=155/155 syntax recognition cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=7/7 parser boundary cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=82/82 semantic cases passed
80/80 execution cases passed
250/250 execution cases passed
87/87 unsafe checks passed
112/112 execution cases passed
6/6 execution cases passed
154/154 Chapter 8 checking cases passed
212/212 layout executions passed
4/4 free-slot allocation and 4/4 class growth comparisons passed
12/12 owned-field/array balance comparisons passed
21/21 IR cases passed
92/92 ownership IR cases passed
33/33 IR C-emission cases passed
5/5 canonical cases passed; selfcheck rejected 18 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
17/17 generic execution cases passed
10/10 generic rejection cases passed
17/17 typeof execution cases passed
8/8 typeof rejection cases passed
18/18 matchtype execution cases passed
13/13 matchtype rejection cases passed
[?1h=1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=16/16 syntax recognition cases passed
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
10/10 object-field-migration execution cases passed
19/19 object-field-migration rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
[?1h=[?1h=[?1h=3/3 unsafe unwrap-check omission cases passed (not executed)
8/8 nullable-handle-unsafe-valid execution cases passed
0/0 nullable-handle-unsafe-valid rejection cases passed
4/4 execution cases passed
4/4 debug-mode contracts passed
64/64 debug object-identity contracts passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
18/18 Chapter 14 cross-call safety checks passed
7/7 Chapter 14 initialization safety checks passed
4/4 safe-initialized-length execution cases passed
0/0 safe-initialized-length rejection cases passed
3/3 Chapter 14 call admission safety checks passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=6/6 syntax recognition cases passed
exit_code: 0
```

## Post-handle-migration cumulative verification

Job4086 terminal exit0; installed offline SDK and direct CommandLineTools make/Python.
Compiler and executable grader inputs frozen through terminal result. Two clean
builds, Debug and Release, zero warnings/errors; saved output contained no FAIL
markers. Numerical summaries extracted/read in4088. Prior4076 stopped in Debug
on historical allocation-count assumptions; Chapter14 successor preserves workload
and checks with layout-derived counts (AoS9, SoA18). Focused4085 passed both
configurations before this cumulative run. No compiler edit was needed.

This is regression evidence, not measured performance, formal proof, independent
review, student reconstruction or human acceptance. Input freeze lifted.

```text
Grader comparison self-check passed
  Lyric -> /Users/bill/projects/lyric-next/compiler/bin/Debug/net10.0/Lyric.dll
Build succeeded.
[39;49m    0 Warning(s)
[39;49m    0 Error(s)
82 passed; 0 failed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=58/58 token cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=70/70 syntax trees passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=155/155 syntax recognition cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=7/7 parser boundary cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=82/82 semantic cases passed
80/80 execution cases passed
250/250 execution cases passed
87/87 unsafe checks passed
112/112 execution cases passed
6/6 execution cases passed
154/154 Chapter 8 checking cases passed
212/212 layout executions passed
4/4 free-slot allocation and 4/4 class growth comparisons passed
12/12 owned-field/array balance comparisons passed
21/21 IR cases passed
92/92 ownership IR cases passed
33/33 IR C-emission cases passed
5/5 canonical cases passed; selfcheck rejected 18 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
17/17 generic execution cases passed
10/10 generic rejection cases passed
17/17 typeof execution cases passed
8/8 typeof rejection cases passed
18/18 matchtype execution cases passed
13/13 matchtype rejection cases passed
[?1h=1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=16/16 syntax recognition cases passed
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
10/10 object-field-migration execution cases passed
19/19 object-field-migration rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
[?1h=[?1h=[?1h=3/3 unsafe unwrap-check omission cases passed (not executed)
8/8 nullable-handle-unsafe-valid execution cases passed
0/0 nullable-handle-unsafe-valid rejection cases passed
4/4 execution cases passed
4/4 debug-mode contracts passed
64/64 debug object-identity contracts passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
18/18 Chapter 14 cross-call safety checks passed
7/7 Chapter 14 initialization safety checks passed
4/4 safe-initialized-length execution cases passed
0/0 safe-initialized-length rejection cases passed
3/3 Chapter 14 call admission safety checks passed
2/2 production handle contracts passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=6/6 syntax recognition cases passed
  Lyric -> /Users/bill/projects/lyric-next/compiler/bin/Release/net10.0/Lyric.dll
Build succeeded.
[39;49m    0 Warning(s)
[39;49m    0 Error(s)
82 passed; 0 failed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=58/58 token cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=70/70 syntax trees passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=155/155 syntax recognition cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=7/7 parser boundary cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=82/82 semantic cases passed
80/80 execution cases passed
250/250 execution cases passed
87/87 unsafe checks passed
112/112 execution cases passed
6/6 execution cases passed
154/154 Chapter 8 checking cases passed
212/212 layout executions passed
4/4 free-slot allocation and 4/4 class growth comparisons passed
12/12 owned-field/array balance comparisons passed
21/21 IR cases passed
92/92 ownership IR cases passed
33/33 IR C-emission cases passed
5/5 canonical cases passed; selfcheck rejected 18 defects
22/22 tuple execution cases passed
26/26 tuple rejection cases passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=20/20 struct declaration/layout checks passed
34/34 struct-value execution cases passed
39/39 struct-value rejection cases passed
12/12 array-default-safe execution cases passed
0/0 array-default-safe rejection cases passed
12/12 array-default-unsafe execution cases passed
0/0 array-default-unsafe rejection cases passed
20/20 borrow-containing-struct execution cases passed
23/23 borrow-containing-struct rejection cases passed
17/17 generic execution cases passed
10/10 generic rejection cases passed
17/17 typeof execution cases passed
8/8 typeof rejection cases passed
18/18 matchtype execution cases passed
13/13 matchtype rejection cases passed
[?1h=1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=16/16 syntax recognition cases passed
18/18 multi-class-migration execution cases passed
15/15 multi-class-migration rejection cases passed
10/10 object-field-migration execution cases passed
19/19 object-field-migration rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
[?1h=[?1h=[?1h=3/3 unsafe unwrap-check omission cases passed (not executed)
8/8 nullable-handle-unsafe-valid execution cases passed
0/0 nullable-handle-unsafe-valid rejection cases passed
4/4 execution cases passed
4/4 debug-mode contracts passed
64/64 debug object-identity contracts passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
21/21 relationships execution cases passed
32/32 relationships rejection cases passed
16/16 aggregate-participants execution cases passed
2/2 aggregate-participants rejection cases passed
18/18 Chapter 14 cross-call safety checks passed
7/7 Chapter 14 initialization safety checks passed
4/4 safe-initialized-length execution cases passed
0/0 safe-initialized-length rejection cases passed
3/3 Chapter 14 call admission safety checks passed
2/2 production handle contracts passed
[?1h=[?1h=[?1h=[?1h=[?1h=[?1h=6/6 syntax recognition cases passed
```


## Focused additions after cumulative 4086

No compiler changes. Mandatory relationships deferred by Bill beyond this book;
job4100 terminal0 verifies the initialization suite's added protected-write and
missing-parent refusals, 9/9 Debug and 9/9 Release. Cumulative4086 contained the
prior seven initialization cases, not these two additions.

Job4104 terminal0: make spin14, clean Release build, production/debug AoS/SoA
six-line expected lifecycle/regrowth output at O2, intended compile-only LYC326,
and temporary cleanup. No invalid candidate execution or performance measurement.
No broad rerun after these test/documentation additions; human acceptance pending.

## Sole code review and focused incorporation

The sole consolidated code review is saved in ch14-code-review.md. It found no
required correction. Two suggested evidence improvements were adopted without
compiler changes: Make now retains aggregate ownership in production/debug
AoS/SoA; the shared compile-only safety harness requires one complete diagnostic
with the exact source path, expected error/message, valid location and byte offset.
Historical graders remain unchanged.

Job4154 completed with exit0 after freezing compiler/grader inputs. Existing Debug
and Release assemblies each passed cross-call18/18, initialization9/9, and call
admission3/3. Each configuration also passed aggregate16/16 executions and2/2
rejections in all four runtime/layout combinations at C O0/O2. Saved output totals
were inspected (job4157). No full cumulative rerun or new compiler build is claimed
for this focused follow-up; cumulative4086 remains the post-migration baseline.
The reviewer was shut down after its single report. Text review and reconstruction
remain pending at this checkpoint; human acceptance and commit authority separate.

## Sole text review incorporated

The consolidated review is saved in ch14-text-review.md. The reviewer made no
implementation edits and ran no new tests. Corrected the direct class-handle-array
field restriction so it does not imply a recursive tuple-field ban; clarified
borrowed storage references versus counted handles, corrected prerequisite
forward terminology and a spacing typo. No compiler/grader behavior changed.
Both reviewers are shut down; no re-review. Student reconstruction and human
acceptance remain pending.
