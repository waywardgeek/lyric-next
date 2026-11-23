# Structured scalar lowering milestone

The first lowering slice constructs real structured IR from checked semantic
facts and exposes it through `--inspect-ir`. C emission remains unchanged.
Arrays, classes and reference parameters are intentionally rejected by this
inspection slice with located LYC340; this is temporary, not language policy.

## Evidence

- Initial inspection baseline: 0/15, because the command did not yet exist.
- First implementation: 14/15. The remaining fixture expected LYC303 for an
  unknown name, but the established semantic diagnostic is LYC301. Corrected
  the expectation, not the compiler's semantic behavior.
- Expanded Debug and Release milestone checks: 20/20 each, including two
  chapter-only unsupported-construct checks. Public retained suite: 18/18.
- Independent Python evaluator follows ordered regions and verifies scalar
  results, recursive calls, short-circuit paths, branch returns and loop exits.
  A 1,500-operation nonconstant expression exercises iterative lowering. A
  deliberate snapshot-order defect is rejected by the grader self-check.
- Real-object lifecycle development probe: 26 checks in Debug and Release.
- `make test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet` completed
  with exit code 0 (job 2302). Both builds report zero warnings/errors; both
  configurations include 154 class-checking cases, 212 layout executions and
  the new 18 retained IR cases. Full log: `lowering-cumulative.txt`.

This does not establish ownership lowering, IR-backed C emission, optimization,
performance gains, or Chapter 9 completion. The Python evaluator deliberately
covers only its documented scalar subset, not every arithmetic failure contract.
No reviewer or student reconstruction was launched for this development slice.

## Lessons carried into the next slice

Preserving control syntax is insufficient unless region execution is explicit:
conditions repeat, short-circuit operands stay conditional, and transfers escape
nested regions. Separate source syntax from these concrete execution operations.

Direct class-field stores save a receiver index and re-resolve field storage after
RHS effects; borrowed references capture fixed addresses instead. These cannot
share an undifferentiated address operation. Existing loop-condition temporaries
remain alive through the selected body, with cleanup on false exit, backedge and
transfers. The construction plan records these ownership compatibility constraints.
