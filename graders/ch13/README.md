# Chapter 13 public graders

All named cases in `relationships.py` are retained. Positive execution contracts require real execution; compiler rejection does
not count as safe relationship support. Negative cases are compile-only and
require the named diagnostic, nonzero exit, and a positive source position;
a crash or unrelated diagnostic is not a passing rejection. The current cumulative
entry point is `make test`, supplemented by the five PEG suites documented in the
chapter. Independent reconstruction and human acceptance remain separate from
author verification. See the [chapter exercises](../../book/chapter-13.md)
for the complete cumulative and supplementary verification commands.

From repository root, after building the selected configuration:

```sh
DOTNET=/Users/bill/.local/share/ensemble-dotnet10/dotnet
python3 graders/ch13/relationships.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Prerequisites: installed .NET 10 SDK, Python 3, and C compiler (`CC`, default
clang). No downloads. The external harness in `graders/ch10/tuples.py` requires
compiler exit 0 with empty stderr, C builds at O0/O2, and exact program stdout,
exit 0, empty stderr. Temporary artifacts are removed by the harness. Tests
operate on an independently supplied compiler command; no reference-tree imports.

The phase-order case checks finalization, then child-role fragments, then
parent-role fragments, regardless of contribution order. It also checks that
unselected policy bodies do not execute. The two list contracts additionally
require automatic detached initialization, child removal updating a surviving
parent, and parent teardown draining children. These ordinary-list contracts now
exercise both compiler configurations. They establish those tested transitions, not general cascade
safety. The counted-membership cases additionally verify a child survives its
last local holder while attached, finalizes after removal with cleared links, and
is not finalized by removal when an independent holder remains. Parent teardown
also detaches an independently held counted child, and two labeled memberships
retain independently until both are removed. Observation temporaries in the
two-membership test end before its last removal so ordinary holders do not mask
the membership-release timing.

Syntax contracts (also retained):

```sh
python3 graders/ch03/recognition.py --fixtures graders/ch13/relationship-syntax.json -- "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
node tools/peg/test.mjs graders/ch13/relationship-syntax.json
```

Syntax success is not semantic or execution support. The current six syntax
fixtures, twenty-one execution fixtures, and thirty-two compiler-rejection fixtures are
the current increment, not full coverage.

The detached-storage case checks nullable managed links start null and an ordinary
helper contributed by the same relationship may write them. External assignment
and writable-reference acquisition are rejected with LYC308; nonnullable links
still require definite initialization (LYC328). Operation-shape negatives require
LYC303 for missing roles, missing/late/duplicate prepare, or structural return.
The preparation-local case requires locals to remain available through the
structural body and counted cleanup to run afterward. Direct managed writes and
forward helper-mediated writes during preparation require LYC308. A nested preparation cleanup that invokes a managed-writing destructor fragment
also requires LYC308. Its positive companion keeps that local alive through the
structural body, including a preparation loop, and verifies cleanup afterward. Scalar counted-child `refs` operations now use explicit membership retain/release
boundaries. Direct and helper-mediated counted-parent teardown are covered in both
runtime modes. These results do not cover aggregate managed links or all
multi-membership/cascade cases.

Operation signatures require LYC304 for missing/extra arguments, nullable child,
wrong endpoint view even for equal concrete classes, or non-unit result.

Run the same relationships with allocator invariant assertions enabled:

```sh
python3 graders/ch13/relationships.py --runtime-debug "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

The required positive counted-parent cases exercise destruction through direct
removal and an ordinary helper. Final release decrements and tests for zero, then
restores one destruction reference before calling cleanup. Reclamation frees the
object directly. Balanced temporary references are therefore correct independently
of IR ownership simplification; no production destruction-state tag is required.

Debug-only allocator invariants (retained):

```sh
python3 graders/ch13/debug_mode.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

`--emit-c --debug FILE` enables allocator state metadata and invariant assertions.
Default output omits that metadata and its checks; .NET Debug/Release builds do
not select generated-runtime debug mode. The four contracts inspect both layouts
and execute valid counted code at O0/O2. Required null-unwrap checking is retained
in both modes. Count-overflow checks are not removed: representational exhaustion
is distinct from an invariant violation. The debug-mode grader also inspects that
an observing method borrows its receiver while a method returning self preserves
its owning convention.

Cross-membership cascade coverage: Graph owns A and B; A independently owns B.
Destroying Graph finalizes G,A,B once in that order. A's cascade removes B from
Graph, whose drain loop must reread current first. The active fragment receiver
remains allocated through this cascade, even when child and receiver share a
class. This does not preserve saved children (LYC330) or their borrowed storage
(LYC326). A direct attempt to destroy the fragment's active receiver is rejected
with LYC334. These controls are compile-only and never execute invalid programs.

The debug-mode grader also instruments the emitted destruction body with a
balanced call to the actual generated retain/release helpers. It requires count
one on entry and after the pair, and an emptied temporary holder. It tests both
layouts, both runtime debug modes and O0/O2. A disposable missing-hold mutation
must exit91 before retaining/releasing; it never runs recursive invalid teardown.
This is a runtime-boundary test independent of IR count elimination, not evidence
that a source final hook may call an own-class-destroying method. The existing
source effect restriction remains in force. No performance gain is measured.

Local managed-link aliases are tested asymmetrically: alias insertion with direct
removal, direct insertion with alias removal, and a chained alias. Symmetric
incorrect retain/release operations can otherwise conceal excess ownership.
These scalar local-address cases do not certify reference-parameter forwarding
or aggregate managed storage.

The participant-return control requires an ordinary caller to own the returned
child independently of its attachment: remove, use the saved child, then finalize
once when that holder leaves scope. This guards the ownership boundary while
scalar participant borrowing is used inside relationship implementations.

### Borrowed scalar participant IR (retained)

From the repository root, run:

```sh
python3 graders/ch13/participant_ir.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Supply the installed SDK path in `DOTNET`; substitute Release to check that build.
The runner uses the public list fixture and `--inspect-ir`, requires successful
inspection with empty stderr, then checks that `remove` has exactly one attachment
release and no owning transfer/release of its borrowed child parameter. Success is
exit 0 and the reported 2/2 pass. The helper also checks initialized nullable
participant-local storage, nested reassignment, and reads through a writable
alias. No Ref, Move or Unref may treat that local as an owner. This is not
certification of aggregate borrowing or general debug use-after-free detection.

The participant IR inspection also follows borrowed child reads through nullable
wrapping and checks nullable-parameter unwrapping. These conversions must not
create or release an owner. Ordinary application-return execution remains a
separate control proving ownership acquisition at the outward boundary. This is
also coverage of scalar managed field reads and the local paths above, not all
aggregate or container paths. The additional `refs-counted-participant-locals`
execution requires `liveNdone` through inferred and annotated locals, an
uninitialized-then-assigned nullable local, and its writable alias.

### Persistent ordinary-field admission

Six additional compile-only controls cover unmanaged ordinary fields, tuple and
nested-array hiding, policy-free contributions, unrelated classes inside a
relationship, and the existing unsupported struct-valued class-field boundary.
The first five require LYC304; the struct admission control requires LYC306 and
is not proof of recursive struct-field inspection. Never execute these negatives.
The current relationship runner contains 21 executions and 32 rejections. The
original Chapter 12 ordinary-link examples intentionally no longer all compile;
the successor suites below retain their applicable behavior and explicitly test
rejection of the superseded storage premise. Run all cumulative checks, not just
these admission controls.

### Chapter 12 migration

Original Chapter 12 fixtures remain unchanged for that chapter's endpoint.
At Chapter 13, the cumulative Make target uses these explicit successors:

```sh
python3 graders/ch13/multi_class_migration.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch13/object_fields_migration.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 graders/ch13/list_refusals.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Repeat with `Release` for that configuration. All are retained checks using the
same external compiler and exact-output harnesses as their predecessors.

- Multi-class: 18 execution behaviors and 15 rejections. Four ordinary-link
  examples use maintained relationships with the original application assertions;
  their old sources now explicitly require LYC304. The concrete-view successor
  still checks LYC329 rather than accepting an earlier storage rejection.
- Object fields: 10 executions and 19 rejections. Counted ownership cases remain
  unchanged. Ordinary alias/helper detach cases now use maintained operations.
  The old unmanaged sources are rejected. Nonnullable constructor initialization,
  early-read, zero and null rules are checked on legal counted fields. This does
  not claim nonnullable relationship construction support; such fields must still
  meet the constructor rule without bypassing protected-link permissions.
- List refusals: four exact runtime assertion diagnostics at O0/O2, preserving
  call sequences and method/main frames. Locations follow the maintained source.
  Termination alone does not prove the post-failure heap state.

The unchanged Chapter 12 nullable runner remains cumulative: 11 safe executions,
10 rejections, three unsafe omission inspections and eight valid unsafe
executions. Never execute invalid unsafe inputs to require safe diagnostics.

`unrelated-counted-contributed-field` additionally preserves ordinary ownership
for a counted payload that is not a relationship participant. Its expected
`savedXreplacedXdone` output checks survival beyond assignment, old-value cleanup
on replacement, and final field cleanup at parent destruction. It never reads a
potentially stale field to establish a failing baseline. This supplements, rather
than replaces, the unrelated local-reference-slot control.

## Debug allocation identity (retained)

From repository root, with Python 3 and a C11 compiler (`CC`, default clang):

```sh
python3 graders/ch13/debug_identity.py "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Repeat with the Release assembly. This accepts an independent compiler command,
uses only temporary artifacts, requires no credentials or downloads, and is now
included in the cumulative Make target alongside `debug_mode.py`. The 64 checks
cover ordinary and counted handle access, both
layouts and C O0/O2: valid access, freed/reused handles, live access after table
growth, null/out-of-range handles, stale destruction, and counted stale retain
and release. Finalizers read fields while destruction is active. Invalid probes
are C runtime-boundary injections into debug output only, not invalid Lyric
programs or uninstrumented invalid execution. Rejections require exit 1, empty
stdout and exact diagnostic text/location/frames; a crash is not a pass.

`debug_mode.py` also verifies identity tables/resolvers are absent in default
output and present in debug output. Its destruction-hold probe resolves identity
to slot before examining metadata. Raw borrowed-address dereferences after address
acquisition remain outside this detector. Identity tombstones consume debug
memory until exit; no production representation change is made.

## Scalar participant boundary (retained until Chapter14's explicit upgrade)

```sh
python3 graders/ch13/participant_aggregates.py --runtime-debug "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Repeat with Release. The public tuple harness requires exact stdout, empty stderr,
exit0 and C O0/O2 for four owning-container controls. Eleven compile-only negatives
require `LYC304`: direct participant literals, nested values, annotations including
parameters/results/fields, array push and tuple/array element stores. These reject
unsupported borrowed-element behavior; ordinary explicit concrete owners and
application containers remain valid. The original baseline passed4/4 controls
only after expansion (initial baseline2/2) but accepted all initial8 negatives;
the implemented boundary passes4/4 and11/11 in focused Debug verification.
Chapter14 must intentionally replace the restriction and its tests together,
not silently weaken a retained check. This is not full memory-safety certification.
