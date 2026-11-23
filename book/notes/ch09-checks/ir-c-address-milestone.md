# Fixed-address C emission and borrowed output builtins

IR Borrow and SelectIndex now emit fixed C pointers. Local remains a real slot even
though its IR result category is Address. The transient emitter map spells pointer
results as dereferenced lvalues, so Load, Store, Copy and FreeArray operate on the
original selected storage. Reference arguments pass that storage's address. Index
selection checks negative indices before unsigned length comparison in safe mode,
then captures the pointer before subsequent effects; unsafe mode omits bounds checks.

Initial 26 C cases passed Debug O0/O2, but the retained Chapter 7 suite failed
72/112: all 40 failures were undeclared function calls for output builtins. The
assertion-only C fixtures had missed ordinary output. Inspection also found IR
lowering incorrectly transferred print's array argument, although ly_print borrows
it. Lowering now retains builtin argument owners in the region for explicit cleanup;
both transfer-validator directions exclude builtins as ownership consumers. C calls
dispatch on resolved Print/Println/Assert tags, not their names. No hidden emitter
release was added. Corrected Debug Chapter 7 run passed 112/112 (job 2827 exit 0).
Original failure log ir-c-address-arrays.txt is retained; correction is in
ir-c-address-arrays-corrected.txt.

Lesson: explicit call IR must distinguish consuming user calls from borrowed builtin
calls. Passing numeric assertion cases does not establish output ABI or ownership.

Expanded focused Debug/Release verification (27 C cases including repeated print,
21 scalar IR and 92 ownership IR) completed as job 2830, exit 0, in both configurations. Release build had no warnings or errors.
No new sanitizer, allocation-balance, model-probe or full cumulative verification.
