# Chapter 12 public checks

Status: participant syntax, explicit method instantiation, scalar/array storage,
scalar object fields, nullable participant views, same-class handle equality, and
explicit doubly linked list append/remove are implemented and exercised. Chapter 12
construction teaching is drafted; the reference cumulative Debug/Release suite and
walkthrough pass. The sole code review is incorporated with focused Debug/Release
checks. The sole text review is incorporated. Independent reconstruction and human
acceptance remain pending.

Chapter 12 intentionally does not provide complete memory safety for regular-object
links. Ordinary code may write these fields while implementing relationships;
examples remove links explicitly before endpoint destruction. Automatic detachment
and restrictions on ordinary link mutation arrive with relation statements. Existing
initialization, nullable access, counted ownership, and earlier borrow rules remain.
No automatic nulling of arbitrary fields is claimed.

From the repository root, with Node 22+ and the already-installed pinned Peggy dependency, and an existing Release compiler for lexing:

```sh
DOTNET=/path/to/dotnet node tools/peg/test.mjs graders/ch12/multi-class-syntax.json
```

All 16 cases are retained syntax checks, identified by their JSON ids. The shared harness requires acceptance for positive cases and a located syntax rejection for negatives; setup errors and arbitrary crashes do not pass. It also runs four malformed-grammar controls. There are no permitted output normalizations or implementation-specific expected answers. Do not change fixtures to fit a candidate.

The PEG uses real lexer tokens but not the handwritten parser. Run the same independent expectations against a supplied compiler with:

```sh
python3 graders/ch03/recognition.py --fixtures graders/ch12/multi-class-syntax.json -- /path/to/dotnet /path/to/Lyric.dll
```

The recognition harness checks the public JSON tree schema for accepted inputs and exact syntax diagnostic kind, exit status, and source location for rejected inputs. Parser shape and behavioral checks will supplement recognition; syntax acceptance is not semantic success. The handwritten-parser checks run in the cumulative Make target; PEG checks remain a separate command. Unknown participant names, duplicate contributions, role collisions, and lifetime safety require semantic checks, not grammar rejection.

No downloads are required by this command. The dependency setup command in tools/peg/README.md is separate and is not authorized by this instruction.

## Binding and storage execution contracts

From the repository root, supply an independently built compiler:

```sh
python3 graders/ch12/multi_class.py /path/to/dotnet /path/to/Lyric.dll
```

All eighteen execution cases and eleven semantic rejection cases are retained, identified
by their names in the readable runner. The shared Chapter 10 harness emits C,
compiles and runs it at both `-O0` and `-O2`, and requires exact expected stdout,
empty stderr, and exit zero. Rejections require exit one, empty stdout, and the
specified located diagnostic; a crash or unsupported-feature error is not a pass.

Coverage includes all label combinations, same-class roles, independent bindings,
counted named/temporary receivers, inferred versus explicit local views, grouped
receivers, declared result views and chained calls, concrete-class lookup, and
scope/member collisions. Nullable stored participant links preserve their role through
checked unwrap and inferred/participant-annotated locals; an explicit concrete
annotation discards that view. Same-class traversal tests leave detached ordinary
objects alive until process exit and do not certify reclamation. Storage checks cover flat/labeled fields, independent
objects and same-class roles, array mutation, finalizer access, missing initialization,
read-before-initialization, and attempted partial-self escape through a label.
Finalizer output is not a complete payload-leak proof. These checks do not certify
relationship-aware teardown, composite-contained views, or complete Chapter 12. All four Chapter 12 executable runners run in the cumulative Make target.

## Object-field contracts

```sh
python3 graders/ch12/object_fields.py /path/to/dotnet /path/to/Lyric.dll
```

Ten retained executions and nine intended rejections cover scalar ordinary and
counted fields, nullable replacement, explicit detachment through aliases/calls,
counted copies, constructor initialization, and rejection of explicit counted
destruction. They use the same exact emitted-C contract above. They do not certify
persistent-link lifetime safety or automatic relationship teardown. Provisional
attachment-safety experiments were removed from this chapter's acceptance contract
following Bill's scope correction; unsafe examples are not executed.

## Nullable-handle contracts

```sh
python3 graders/ch12/nullable_handles.py /path/to/dotnet /path/to/Lyric.dll
```

Eleven retained execution contracts and ten intended compile-time rejections cover
nullable wrapping, checked unwrap, scalar/nested counted copies, typed empty array
growth, and same-class identity equality. Both operand orders accept a contextually
typed null; different nominal classes and handle ordering are rejected. Equality
does not narrow a nullable variable or consume counted ownership. Three null-unwrap failures run only in safe mode. Under `--unsafe`, their generated
C is inspected for omitted unwrap checks but is never executed. Eight valid
nullable programs execute under unsafe mode at O0/O2. The shared harness checks the exact LYR013 message, source path and main
stack frame, a positive line and column, exit one and empty stdout—not an arbitrary
crash. It does not compare the line and column to the particular unwrap expression. All execution contracts run at both C optimization levels.

## Explicit doubly linked list

`multi_class.py` contains the readable `LIST` source, not a runtime library or a
special compiler list primitive. `P` contributes nullable `first` and `last` fields
and `append`/`remove`; `C` contributes nullable `parent`, `previous`, and `next`.
The explicit binding places these directly in `Graph.nodes` and `Node.graph`.

Append first asserts that the child has no parent. It sets the child's previous
link to the old tail, clears its next link, and sets its parent. It then updates
that tail's next link (or the empty list's first link) and publishes the new tail.
Remove first asserts that the child belongs to this parent. It reconnects the
previous neighbor or updates first, reconnects the next neighbor or updates last,
then clears all three child links. Both assertion guards precede every write.
These methods assume links have been maintained consistently; they do not repair
arbitrary field corruption by other user code.

The transition fixture checks empty, singleton, three-node adjacency, middle
removal, head removal, tail removal, and reattachment of a detached node. A separate
fixture places one child in two labeled memberships and proves removing one does
not change the other; it explicitly destroys both endpoints after both removals.
The transition fixture leaves detached ordinary nodes allocated until process exit because separate destruction
of multiple same-class objects encounters the existing conservative lifetime rule.
The counted fixture removes its child explicitly, checks all five empty slots, and
observes one finalizer per endpoint. Ordinary counted fields retain their targets;
attachment can form cycles, and no cycle collector or automatic removal is claimed.

Run the separate retained refusal contracts as well:

```sh
python3 graders/ch12/list_refusals.py /path/to/dotnet /path/to/Lyric.dll
```

All four named cases are retained: duplicate append, append to another parent,
remove from the wrong parent, and remove a detached child. Each must compile and
then exit one, produce empty stdout, and emit exactly the located `LYR010`
assertion diagnostic and method/main stack at both optimization levels. The public
Chapter 5 harness accepts explicit line/column locations for these multiline
fixtures; existing single-line contracts are unchanged. A terminating failure
cannot expose the post-failure heap: guard-before-write ordering is a separate
source property, not a claim inferred merely from exit status.
