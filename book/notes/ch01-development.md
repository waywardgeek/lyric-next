# Chapter 1 development notes

These are authoring evidence, not a replacement for the eventual student chapter.

## Agreed boundary

Bill approved UTF-8 decoding and source-wide forbidden-character checks together
with source diagnostics. No tokenizer, AST, or generated code in this increment.
Approved Unicode 16.0.0, LF/CRLF with lone-CR rejection, zero-based byte offsets and
one-based scalar-counted lines/columns. Downloads of needed tools/data authorized.

## Decisions and reasons

- Preserve original bytes in SourceFile. Decode scalars using the strict .NET Rune
  API; do not use replacement decoding, locale-dependent classification, or NFC
  normalization on whole files. Combining/RTL letters remain allowed here.
- Pin official Unicode data, provenance, license, and hashes. Generate a 21-range
  table. Independently read official property fixtures in the Python grader.
- Compilation owns sources; both ends change in each add/remove operation. Failed
  attachment leaves membership unchanged. Destruction does not pre-detach children.
- Inspection is a real source-scanner mode, not a valid-program verdict. CRLF is
  one LF event with a two-byte span; original bytes are not rewritten.
- Validate before emitting events. Reuse the same scanner twice rather than store
  a second text copy or buffer all output. Revisit only with actual performance need.
- Structured stdout supports exact public grading. Diagnostics go to stderr with
  stable codes and visible code points; no control-character source excerpt.

## Observations and corrections

- .NET SDK 10.0.403 / C# 14 on macOS ARM64 builds matching partial constructors and
  methods successfully; no additional SDK/package installation needed.
- Initial 80-case grader passed; self-inspection then found missing Unicode property
  labels in diagnostics and missing refusal of destroyed-source reattachment.
  Corrected both and reran: 80 passed, zero failed. The lifecycle correction was
  source-reviewed, not established by the CLI behavioral suite.
- Reconciled stale open-decision prose for Unicode version and lone CR in the spec
  and grammar review. Early broad search output was clipped; relevant source-character
  requirements were read directly instead of treating that search as full evidence.
- No source acceptance test establishes identifier NFC, parser correctness, whole-
  language memory safety, or student reproducibility. These are separate milestones.

## Additional verification

- Disposable mutants all built, then failed the unchanged grader: byte-counted
  columns 23 failures, disabled bans 43, accepted lone CR 3. Copies were removed;
  the reference was untouched by mutation experiments.
- Release build also passed 80/80. A disposable C# check exercised the real model's
  constructor attachment, duplicate/wrong-parent refusal, complete removal,
  child/cascade destruction, idempotence, and refusal after destruction. It passed.
  This extra development test is not a preserved cross-execution grader.
- A real UTF-8 example (`é`, CRLF, CJK, tab, emoji) produced expected positions; a
  bidi control in comment-looking text was rejected at byte 3, line 1, column 4.
  Official Unicode hashes remained unchanged.
- The walkthrough exposed default JSON escaping of `+` as `\u002B`. Semantically
  correct but needlessly hard to read, it was changed only for scanner-generated
  ASCII JSON records. Filename diagnostics retain stricter escaping. Semantic JSON
  comparison rightly permits both forms; readability required actual inspection.
- The first readability-edit script had a quoting syntax error and changed nothing;
  it was corrected before rerunning builds and graders.

Final readability revision: Debug and Release builds both succeeded with zero warnings/errors; each passed all 80 graders. Readback confirmed literal `U+00E9` in inspection JSON.


## Single code review and incorporated corrections

The sandboxed reference code reviewer inspected the complete chapter, compiler,
public graders and Unicode inputs. Its full typed submission is retained under
Cadence child `lyric-ch01-review-fresh`. This was source review plus independent
Unicode/hash/comparison checks, not another compiler execution. It confirmed all
21 generated intervals match all 4,236 forbidden scalars, and traced complete
ownership/refusal/destruction operations. No scanner or ownership defect was found.

Three required corrections were incorporated:

- Empty filenames could throw ArgumentException outside the file-read filter.
  Invalid/empty supplied paths now produce LYC004; missing arguments remain usage
  errors. Only path/I/O exceptions are converted, not arbitrary compiler failures.
- Diagnostic filename comparison incorrectly required Python's JSON spelling.
  The grader now decodes the leading JSON filename and compares the suffix exactly,
  retaining control-character rejection. Strict .NET filename escaping stays intact.
- Python bool/number equality could admit false/true as source positions.
  JSON comparison now excludes booleans from numeric fields, while permitting
  equivalent JSON numeric spellings and object key order.

The expanded retained compiler suite adds empty-path and unusual-filename cases:
82/82 passed in Debug and Release, each build with zero warnings/errors (job 19
in the post-restart session). Separate retained grader comparison self-checks passed.
These are grader-maintenance checks, not additional compiler conformance cases.
The optional suggestion to retain model lifetime tests later was not made a new
chapter gate. No second code review was requested. Text review, fresh student
reproduction, and Bill's walkthrough remain outstanding at this point.

Lesson: semantic JSON comparison must accept equivalent escaping without accepting
incorrect types. A source-reader pass, ownership review, grader self-check, and
human acceptance are separate claims.

## Single text review

The confined text reviewer found no blocking construction-teaching mismatch. It
identified stale reference README review status, an ambiguous reference-generator
command in the Unicode README, and a walkthrough export that overwrote DOTNET.
The status and generator boundary are now explicit; the export preserves an
existing host path. The reviewer inspected source and prose without running builds
or graders. No re-review was requested. Fresh-student reproduction and human
acceptance remain outstanding.

The revised published walkthrough was executed with DOTNET already set to the
absolute installed host path; it preserved that setting and produced the documented
events and rejection. All hashes of the frozen public student input bundle were
verified before reconstruction. The student has read-only imports of that bundle
and the SDK, with no reference source, generator, or private notes supplied.

## Fresh student reconstruction

The confined fresh student `lyric-ch01-student-01` independently built Chapter 1
from the frozen public teaching/fixture bundle and read-only SDK. Both Debug and
Release builds succeeded with zero warnings/errors, and each public grader run
reported 82 passed, 0 failed. The author read the saved completion report and build/
grader summaries; all frozen input hashes still matched. The student also reported
16 passing model assertions/refusal checks and a structural self-check. Those
student diagnostics are not retained requirements or independent code-review proof.

No implementation assistance, requirement clarification, grader repair, or target
source was supplied during the trial. The student found the teaching sufficient.
It noted that Chapter 0 links to a public grammar review absent from its bundle;
no Chapter 1 parsing decision depended on it. Future input bundles should include
that public background document. This setup observation does not justify repeating
a passing trial. The student was shut down after submission; its implementation
and completion logs remain in its assigned workspace. No student code was patched
or merged into the reference, and no repeat execution was requested.

The first SDK build printed a workload-verification notice, but succeeded; no
workload installation/update was needed or performed. Human walkthrough and
acceptance remain pending. No Chapter 2 work has begun.

Lesson: the chapter transferred enough construction knowledge for this independent
reconstruction without private implementation help. That is useful evidence from
one trial, not a guarantee about every student or exhaustive correctness.


## Human walkthrough convenience

Bill requested a short Make-based walkthrough and one command for all tests.
Moved the existing disposable-file demonstration to tools/spin01.py and replaced
the Chapter 1 embedded Python block with make release / make spin01. The spin
target builds Release first. Added make test, directly invoking the public
comparison self-check and all current Chapter 1/2 retained graders in Debug and
Release, stopping on failure. DOTNET, PYTHON and GRADERS remain overridable.
No compiler semantics or grader expectations changed; frozen student inputs and
the passing student's implementation were not touched or rerun.

Executed all three published Make targets successfully: clean builds, expected
source positions/bidi rejection, and the full cumulative suite in both configs.
A failing DOTNET command verified nonzero make test exit before compiler graders.
Lesson: simple human entry points can preserve transparent public test contracts
and failure propagation without making the reader copy an orchestration script.
Human walkthrough acceptance remains unobserved.


## Human acceptance

Bill reported running the Chapter 1 spin successfully and explicitly stated:
“CH01 fully accepted.” Chapter 1 human walkthrough and acceptance are complete.
Chapter 2 remains pending his reading and walkthrough; this does not authorize
Chapter 3 or a repeat student trial.
