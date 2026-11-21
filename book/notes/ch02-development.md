# Chapter 2 development notes

## Agreed scope and numeric correction

Build a parser-independent lexer on Chapter 1. Bill confirmed that typed numeric
literals such as 123u16 belong in lexical tokens. Inspection found the current
spec/PEG excluded suffixes; corrected spec, grammar, and review before coding.
Integer width suffixes and f32/f64 are now explicit; signs remain operators and
range checking remains semantic (including signed minima). Base prefixes and
exponents remain deferred. No token implementation is complete yet.

## Pinned identifier foundation

Do not use host-dependent NFC/XID behavior. Downloaded official Unicode16
DerivedNormalizationProps and NormalizationTest, verified headers and recorded
hashes. Generated immutable Common tables from upstream XID, combining classes,
canonical decomposition and full composition exclusions. Unicode implementation
recursively decomposes, stably orders marks, and composes with blocking rules;
Hangul uses the standard arithmetic rules. Normalization returns a value, never
rewrites source spelling. Planned lexer compares original identifier to NFC and
diagnoses with a suggestion.

The initial build failed because a signed bitwise composition-key expression
triggered CS0675 under warnings-as-errors. Replaced it with collision-free
arithmetic packing first*2^21+second. Debug then built cleanly. The independent
Python grader passed 99,826 official NFC vectors and XID classification of all
1,112,064 Unicode scalars. All82 Chapter1 cases also passed. Seven malformed inspection
request cases also passed, each requiring the exact diagnostic and no partial stdout.

This establishes a Unicode component, not a lexer, parser, safety proof, or
completed chapter. Token model proposed: SourceFile owns tokens with backlinks;
tokens hold kind/span/decoded literal values, not duplicate source spellings.
No reviewers or students launched for Chapter2. No commits or pushes.

## Token and literal increment

SourceFile now strongly owns Token objects; constructors attach both directions,
refusal is mutation-free, and source destruction detaches itself before cascading
through tokens that remove their own memberships. Original bytes remain the only
source-text authority. Tokens retain spans, kind, explicit suffix, and decoded
literal bytes. Inspection reconstructs spelling from the source, not a second copy.

Lexer invocation state is local to SourceFile.Lex: byte/scalar cursor, delimiter
stack, and prior-dot state. Whole-source validation precedes lexing. NFC rejection
suggests rather than repairs spelling. Numeric suffixes are consumed with their
whole identifier-continuation boundary; no range conversion occurs. Interpolation
uses stack markers for text and expression openings alongside ordinary delimiters,
so nested braces restore significant newlines. Escaped data is never rescanned as
source; raw string trimming cannot conceal banned source characters.

Debug builds passed, followed by 28 initial token cases and then all 58 expanded
literal/interpolation cases. A generated Python fixture snippet had a quote error
caught by readback before insertion/execution; corrected its outer quotes. This
was a test-authoring issue, not a lexer defect. Release/cumulative and deliberate
mutant checks are next; no completed Chapter 2 claim yet.

## Verified reference milestone

After correcting the NFC grader to compare the suggestion's decoded JSON spelling,
both Debug and Release passed all 58 token cases, 99,826 official NFC vectors,
XID classification for all 1,112,064 Unicode scalars, seven malformed Unicode
requests, and all 82 Chapter 1 cases. Full local logs are in ch02-checks/.
Three independently copied mutants built successfully and were rejected: bypassing
NFC failed one token case, accepting unknown suffixes failed seven, and suppressing
newlines inside braces failed two. The actual reference was not mutated.

Removed redundant linear membership search on token attachment: the source
backlink already proves detachment. Removal searches backward, making reverse
teardown linear overall while preserving token order for arbitrary removals.
This was an algorithmic correction found by inspection, not a benchmark claim.

Lesson: a parser-independent lexer still needs explicit lexical state. Its mode
stack handles interpolation and newline context without parser feedback. Syntax
validity and numeric range remain later responsibilities. No Chapter 2 teaching,
review, student reconstruction, or human walkthrough has yet been completed.

## Teaching draft and published commands

Wrote Chapter 2 from the verified increment: source-backed token ownership,
pinned NFC construction, scalar cursor, numeric boundaries, newline/mode stack,
literal bytes, exact inspection boundary, cumulative exercises and local walkthrough.
Updated the book index/compiler README and removed the grader README's stale
claim that token checks were still to be added; test contracts did not change.

Regenerated IdentifierRules.Data.cs and compared bytes with the prior file: identical.
Verified all unicode/SHA256SUMS input hashes. Extracted and executed all three
published shell blocks using the installed absolute SDK and public graders: both
configurations' cumulative checks and the walkthrough exited zero. Logs are
ch02-checks/published-block-1.txt through published-block-3.txt. Links, fence
balance and git diff whitespace checks passed. These are reference demonstrations,
not a fresh-student result or human walkthrough.

The single confined code reviewer lyric-ch02-code-review was launched with
read-only repository access. Review result, text review and student trial pending.

## Single code review

The confined `lyric-ch02-code-review` completed one consolidated review with no required corrections. Its saved submission retains the full report. It traced architecture and lifecycle, inspected our actual check logs, and independently checked local Unicode hashes and range ordering; it did not rerun .NET graders. The optional generator readability suggestion was incorporated with local rationale comments and expanded decomposition formatting. Job 119 regenerated byte-identical tables and passed whitespace checks. The reviewer is stopped.

Lessons: exhaustive Unicode checks do not establish lexical combinations or lifecycle behavior. The old CLI usage text is pinned by retained Chapter 1 tests; changing it requires a coordinated contract decision rather than unilateral cleanup.

## Single text review and frozen reconstruction inputs

The confined `lyric-ch02-text-review` returned no required corrections and is stopped. Its saved submission retains the full report. Incorporated optional record-order wording and a concrete equal-class blocked-composition example. Job 134 checked the example against pinned UnicodeData and the real Release Unicode inspector: A/overline/ring remains [65,773,778], while A/ring composes to [197]. No executable chapter blocks or contracts changed. Local pinpoint links were optional and not added.

The isolated student bundle contains public chapters, language/grammar/review, graders and adjacent Unicode fixtures, and only the nine accepted Chapter 1 compiler files from commit 53156a3. It excludes current compiler, reference generators, Git, and private notes. A first packaging attempt stopped on an incorrect grammar-review filename; corrected to the actual grammar-review.md before launch. Inputs are hashed before reconstruction.


## Independent reconstruction result

Fresh confined student `lyric-ch02-student-01` completed Chapter 2 using only the
frozen public inputs and accepted Chapter 1 prerequisite. No clarification,
implementation assistance, grader correction, or student-code edit was needed.
Its REPORT.md and logs remain in the agent-owned student workspace. Parent read
the full report and saved build/grader/walkthrough/lifecycle summaries; it did not
rerun the passing implementation. Both builds report zero warnings/errors; each
configuration passed 82 source cases, 58 token cases, 99,826 NFC vectors,
1,112,064 XID scalars, and seven malformed Unicode requests. The retained comparison
selfcheck passed. Twenty-two supplementary student-owned structural checks and
the published walkthrough also passed; these checks do not become retained graders.

All 32 frozen input hashes still matched after submission. The student reported
that suffix boundaries, decoded-escape separation, consumed-mark blocking,
nearest-delimiter newline policy, and end-first destruction made the chapter
reconstructible. No teaching correction was requested. The Debug SDK workload
advisory did not prevent a clean build and caused no installation.

Lesson: this is one successful unassisted reconstruction, not exhaustive lexer
proof. Unicode classification coverage, representative lexical checks, structural
checks, and human usability remain distinct evidence. Human walkthrough and
acceptance are still outstanding. No Chapter 3 work, commit, or push followed.


## Human acceptance and convenience commands

At Bill's request, extracted the Chapter 2 walkthrough into tools/spin02.py and
added make spin02, depending on the Release build. The chapter now presents that
single command and points to make test for the cumulative retained suite. The
actual make spin02 invocation built cleanly and demonstrated suffixes, decoded
bytes, interpolation, and the expected NFC rejection. No compiler or grader
contract changed, and the passing student was not rerun.

Bill then said “Chapter 2 LGTM” and requested a commit, leaving the push to him.
Chapter 2 is human-accepted. This records his acceptance, not a separately
reported human execution of spin02. Chapter 1's successful human spin and full
acceptance were already recorded. No Chapter 3 work is included.
