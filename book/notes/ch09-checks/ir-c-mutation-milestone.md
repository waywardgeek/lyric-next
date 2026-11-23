# IR array mutation C emission

ArrayReserve/Resize/Truncate/Push reuse a single concrete descriptor-emission helper.
IR selects the receiver and evaluates arguments first. The helper performs allocation,
live-element initialization, recursive shrink cleanup, and capacity changes; it does
not track temporary owners. Push receives an explicit transferred operand. The old
syntax caller temporarily shares the helper but alone retains its inferred Moved call.
Resize(0) releases reservation, truncate(0) preserves it, and nondefaultable object
array growth retains its existing rejection (object IR emission remains next).

Verification: job 2833 exit 0, Debug build and retained Chapter 7 112/112 plus prior
27 C cases. Job 2835 exit 0: clean Debug/Release builds, expanded 30/30 C-emission
cases each at O0/O2, Release Chapter 7 112/112, git diff --check. Three new marker-
required cases cover capacity release, nested mutations/defaults, and argument effects.
No new sanitizer, balance or full cumulative evidence. No commit or backend fallback
removal yet.
