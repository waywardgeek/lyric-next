# Chapter 12 reconstruction — status history

## Completed after bounded setup repair

All five outstanding PEG comparisons now pass: 50/50, 155/155, 5/5,
11/11, and 16/16, each exit 0 with grammar corruption controls. Saved final
status, numerical totals, structural review, and post-PEG integrity receipts
were inspected. The same 43 candidate source/grammar files and imported public
inputs are unchanged. The passing cumulative suite and walkthrough were not
rerun. The student submitted and was shut down; Bill has accepted both the Chapter 12 text and `spin12`.
See the [full completion receipt](ch12-student-acceptance.md).

Setup used 19 byte-verified installed runtime libraries and 13 internal aliases
copied into the student's own runtime directory, then process-local library and
empty OpenSSL configuration paths for offline PEG execution. No installation,
system configuration change, candidate patch, network use, or TLS bypass occurred.

## Earlier commit checkpoint

The following interim receipt records the state at commit 94cb532. Its PEG blocker
is resolved above; preserve it as evidence of what that commit actually claimed.

One confined student reconstructed Chapter 12 from the accepted Chapter 11
compiler (commit 5947bed06e8327b0e3c0c079c14508847be0f84f) and a public bundle
of 149 files plus its manifest. No target Chapter 12 compiler or private repair
history was supplied. The author did not edit the candidate implementation.

The student reports its final cumulative Make job 2962 completed with exit 0.
The author inspected the saved cumulative-summary receipt: Debug and Release
both show zero build warnings/errors and all 33 public runners passing,
including Chapter 12's 18 executions/11 rejections for multi-class behavior,
10/9 object-field cases, 11/10 nullable contracts, three unsafe-mode checked
null failures, and four intended list assertion failures. This is separate from
the reference implementation's verification record.

Student walkthrough job 2974 completed with exit 0. Its cleanup receipt was
inspected: the script checked the exact three output lines, exit 0, empty stderr,
and absence of leftover walkthrough temporary directories. The student reports
9/9 structural probe checks, a recorded structural review, verified input hashes,
and a frozen 43-file source/grammar hash manifest. Those reports are not a full
author source review or human walkthrough acceptance.

All five required PEG comparisons remain pending. The first attempt aborted
before grammar execution because sandboxed Node could not load its libuv
runtime dependency. This is a setup blocker, not a grammar pass or failure.
The student retains its same candidate; no passing cumulative rerun is requested.

Assistance: clarified that invalid-reference negative programs must be compiled
to check their intended rejection, but must never be executed if falsely accepted.
No grader or language contract changed. Toolchain dependency resolution remains
open. Both independent reviews of the reference are complete and incorporated.

Bill authorized a local commit of current work for his own push. That authority
does not imply full student completion or human walkthrough acceptance. No push,
installation, service restart, or Chapter 13 work is included.
