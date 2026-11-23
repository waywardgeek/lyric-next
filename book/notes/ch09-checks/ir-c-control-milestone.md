# Structured scalar IR-to-C milestone

The emitter now accepts scalar If, While, Yield, Break and Continue in addition to the earlier straight-line operations. It declares IR value/storage temporaries at function scope, but emits assignments at their actual execution positions. This preserves condition definitions used in sibling bodies without moving evaluation earlier.

An iterative action stack emits nested regions. Each region has an end label; Yield assigns its enclosing result and jumps to that end, while Return restores the runtime frame and returns from the function. Each loop has condition and exit labels. Break and Continue follow the actual IR Target, including transfers from conditions. These C labels implement structured IR, not a new arbitrary-edge IR. No cleanup is inferred: later owning operations must be emitted explicitly from lowering.

The body marker remains a chapter-only migration assertion. Six new retained runtime cases cover condition break/continue/return, recursive branch return, branch values and short-circuit suppression of a failing call. Debug built with no reported command failure and all 15 C-emission cases passed at O0 and O2 (job 2799). Job 2800 subsequently exited zero: Debug expression suite 250/250, Release build zero warnings/errors, Release C-emission 15/15 at O0/O2, Release scalar inspection 21/21, and Git diff whitespace check passed. These are focused backend checks, not a new full cumulative run.

Arrays, references, classes and generated destruction still use the temporary syntax-emission fallback. Removing that fallback remains required. No new sanitizer or cumulative-backend verification is implied.
