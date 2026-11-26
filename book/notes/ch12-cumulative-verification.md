# Chapter 12 cumulative reference verification

Author run job 2703 completed with exit 0. Compiler, grader, and assembly inputs
remained frozen through its terminal receipt. The Make target ran 33 public
runners in each of Debug and Release, plus the comparison selfcheck before the
loop. Both builds reported zero warnings and zero errors. This is reference
behavioral evidence, not a code review, independent reconstruction, human
acceptance, or proof of full ordinary-link safety.

Command from the repository root, using the installed toolchain:

```sh
DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
/Library/Developer/CommandLineTools/usr/bin/make test \
  DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet
```

The separate PEG comparison, direct C# model/IR probes, layout-focused list runs,
and practical walkthrough are separate checks; this receipt does not claim the
Make target ran them. Their evidence belongs in the development record.

## Saved numerical summaries, in execution order

```text
Grader comparison self-check passed
Build succeeded.
    0 Warning(s)
    0 Error(s)
82 passed; 0 failed
58/58 token cases passed
70/70 syntax trees passed
155/155 syntax recognition cases passed
7/7 parser boundary cases passed
82/82 semantic cases passed
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
20/20 struct declaration/layout checks passed
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
1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
16/16 syntax recognition cases passed
18/18 multi-class execution cases passed
9/9 multi-class rejection cases passed
10/10 object-field execution cases passed
9/9 object-field rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
3/3 nullable-handle-unsafe execution cases passed
0/0 nullable-handle-unsafe rejection cases passed
4/4 execution cases passed
Build succeeded.
    0 Warning(s)
    0 Error(s)
82 passed; 0 failed
58/58 token cases passed
70/70 syntax trees passed
155/155 syntax recognition cases passed
7/7 parser boundary cases passed
82/82 semantic cases passed
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
20/20 struct declaration/layout checks passed
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
1/1 matchtype IR selection check passed; 3/3 corruption controls rejected
16/16 syntax recognition cases passed
18/18 multi-class execution cases passed
9/9 multi-class rejection cases passed
10/10 object-field execution cases passed
9/9 object-field rejection cases passed
11/11 nullable-handle execution cases passed
10/10 nullable-handle rejection cases passed
3/3 nullable-handle-unsafe execution cases passed
0/0 nullable-handle-unsafe rejection cases passed
4/4 execution cases passed
```

## Sole code review and author follow-through

The sole privileged code reviewer `ch12-code-review` submitted on join2763 and
was shut down. Its full consolidated report is retained in ch12-code-review.md.
R1 confirmed that wrapper-traversing SemanticType.Class admitted a nullable
participant. New retained labeled/flat fixtures both falsely accepted on the
unchanged Release assembly (2789 exit1, 0/2 intended rejections). BindMultiClass
now requires SemanticTypeKind.Nominal before accepting its class. The chapter
teaches this distinction at the binding introduction.

Focused author job2790 completed exit0: clean Debug and Release builds, each
18/18 multi-class executions and 11/11 intended rejections, 11/11 nullable
execution contracts, 10/10 nullable compiler rejections, and 3/3 checked unsafe
unwrap failures. Compiler/grader inputs remained frozen during this run; only
prose changed. The earlier full cumulative2703 remains baseline evidence, not a
fresh full-suite claim for this narrow admission correction. Whitespace clean.

R2 resolved by describing the actual nullable harness contract: exact message,
path, stack, exit and stdout, but positive—not exact—line/column. No grader was
weakened or runtime implementation failure inferred. The guide now names the
transition fixture as leaving detached ordinary nodes allocated, distinct from
the independent-membership fixture's explicit destruction. No re-review.
Lesson: canonical navigation is not nominal admission; a plausible positive
source position is not proof of the exact unwrap location.
