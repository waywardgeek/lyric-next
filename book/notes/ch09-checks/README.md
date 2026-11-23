# Structured IR model verification

This is the initial data-model slice, not completed Chapter 9 lowering.

- Shared declarations: `compiler/Common/IR.cs`.
- Lifecycle implementation: `compiler/IR/Model.cs`.
- Development probe: `tools/probes/check_ir_model.py` and `ir-model.cs`.
  This compiles the actual model, not a mock. It is not a retained independent
  chapter grader: its assertions are written in C#.
- `ir-model.txt`: 26 lifecycle checks passed in each of Debug and Release,
  including refused cross-function relationships, nominal dependencies,
  invalidation, teardown, and duplicate function-body refusal.
- Initial build failed on mismatched partial-member parameter names; those
  names were corrected before successful builds and probe execution.
- `cumulative.txt`: `make test DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet`
  completed with exit 0 (job 2271). Both Debug and Release built with zero warnings
  and errors and passed the retained suite, including 154 class checking cases,
  212 layout executions, eight allocation-policy comparisons and twelve ownership
  balance comparisons per configuration. Inputs stayed fixed during this run.

The compiler does not lower programs into this model yet. The model admits
incomplete drafts; operation typing, region-shape and use-order validation remain
future work. No optimizer, generic specialization registry, reachability-pruning
pass, new sanitizer run, or performance measurement is claimed. Existing runtime
regressions exercise the existing emitter, not an IR-backed replacement.
