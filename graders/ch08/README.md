# Chapter 8 class graders — initial baseline

Six retained initial cases exercise ordinary constructors, scalar fields, shared
identity, methods, explicit destruction/final, and counted local/alias cleanup.
They are required behavior under development, not implemented-feature claims.
Counted cases use the initial backend's immediate-release cleanup boundary;
they do not mandate a total ordering of unrelated finalizers for all Lyric backends.

From the repository root:

```sh
python3 graders/ch08/run.py /absolute/path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

The Chapter 5 execution harness requires successful C emission and compilation,
then exact stdout, empty stderr and exit0 at both O0 and O2. Each process is bounded
by its existing timeout. All fixtures are readable and student-read-only. Lifetime
rejections, allocation/slot reuse, AoS/SoA equivalence and full cumulative checks
will be added as the increment is built. No Chapter 8 completion is claimed.

## Initial layout execution checks

From the repository root, after a successful Debug build:

```sh
python3 graders/ch08/layout.py /Users/bill/.local/share/ensemble-dotnet10/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

Substitute your SDK executable as needed; no installation is performed. Requires
an installed C compiler (`CC`, default `cc`). Seven retained cases each run in AoS
and SoA at `-O0` and `-O2`: 28 executions. Compiler success requires exit 0 and
empty stderr; generated executables require exit 0, stdout exactly `ok`, and empty
stderr. Temporary sources and executables are removed automatically. These cases
check construction, shared identity, growth, field selection after RHS growth,
explicit destruction, reconstruction, and helper destruction. Counting and
allocation balance are not established by these cases.
The layout option is `--emit-c --soa FILE`; ordinary `--emit-c FILE` selects AoS.

### Class-wide destruction checks

`check.py` now also checks ordinary `object.destroy()` and its class-wide lifetime effects. A subsequent use of a potentially invalidated object reports `LYC330: Object reference may have been invalidated.` This includes aliases, saved call arguments and field destinations, conditional destruction, transitive helpers, and construction's implicit receiver result. Fresh construction may establish a new usable handle but cannot revive an old alias. Allocation without destruction preserves indexed handles. Explicit destruction of a counted value object reports LYC331; destruction takes no arguments.

These are retained static checks. Separate layout tests now exercise scalar ordinary-class destruction and reconstruction in both AoS and SoA. Final hooks, counted cleanup, owned array fields, and field borrows remain under development; these checks do not establish their implementation.

## Free-slot allocation check

```sh
python3 graders/ch08/allocation.py /absolute/path/to/dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

This retained check wraps real generated-C `malloc` and `realloc` calls. One
create/destroy cycle and 257 cycles must perform the same positive number of
allocation calls, separately for AoS/SoA and O0/O2. Successful executions require
exit 0, empty stdout, and only the wrapper's decimal count on stderr. This checks
free-slot reuse without depending on private generated names or exposing indices
in Lyric. It does not establish leak freedom or shutdown storage reclamation.
Temporary files are removed; `CC` and the prerequisites above apply.
