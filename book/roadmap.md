# Agreed next construction increments

This is the dependency spine agreed with Bill, not a fixed table of contents or authorization to skip verification. Chapters 8 through 11 are complete through reviews, independent reconstruction, and Bill’s acceptance of their walkthroughs. Chapter 10 supplies composite types and canonical identity; Chapter 11 supplies generic functions, typeof, and matchtype. Chapter 12 implements explicit multi-class generic instantiation; its reviews are incorporated and independent reconstruction has passed cumulative checks, five PEG comparisons, and the walkthrough with runtime setup assistance. Bill has accepted the Chapter 12 text and `spin12` walkthrough. Later relationship chapter boundaries remain provisional.

## Chapter 8 — Standalone classes and lifetime policies

Implement ordinary classes and reference-counted `value class` objects, constructors, fields, methods, and final hooks. Begin with scalar fields; build real counted ownership through local bindings, calls, returns and arrays. Ordinary objects have explicit destruction; counted objects release references and cannot be forcibly destroyed. Neither copied object reference clones its payload. No object-to-object field graph, relations, fallible construction or general generics in this increment.

Use chunked nonmoving AoS with free-slot reuse; allocate from the last chunk's unused slots before adding another chunk. SoA uses growable per-field arrays. Object handles are indices. Potential class-storage growth invalidates still-needed field borrows conservatively in both layouts; ordinary handles survive growth, not destruction. Analyze class accesses using the existing storage-selection/effect model, rather than generating allocator syntax for a second analysis pass. Final hooks and generated counted releases have visible transitive effects. Initial backend uses immediate retain/release and scope cleanup; language-wide counted-finalization timing remains broader.

## IR, then accelerated multi-class interfaces

The agreed architecture introduces an IR for explicit execution and ownership
operations before C emission. Semantic resolution selects complete interface
bindings; specialization/IR construction lowers abstract operations to concrete
calls and storage, and generated bodies participate in effect/lifetime checks.
The C emitter does not resolve interfaces. See the [IR and interface design
record](notes/ir-interface-design.md) for decisions, reasons and open details.

Chapter 9's structured execution/ownership IR and IR-only C backend are complete
and human-accepted, including `spin09`. Keep Chapter 5's early C backend in place:
it earns the later refactor rather than making the reader wait for executable code.

## Chapter 10 — Composite types and canonical type identity

Bill approved discussing and constructing this increment before generic
specialization and multi-class interfaces. Introduce tuples and nominal structs,
including their construction, methods, recursive finite layouts, copying, cleanup,
and nonescaping contained borrows, together with canonical type interning.
The accepted language decisions are integrated into Section 4 of the specification.
The bounded development plan is [Chapter 10 construction notes](notes/ch10-development.md).

Use one semantic owner and a collision-safe canonical type table. Composite keys
use canonical component identities; nominal keys use declaration identities.
Type equality then compares canonical identities. This is a foundation for richer
types, not a measured optimization of today's constant-size flat descriptor.
A provisional design sketch may help construction, but finalize the chapter's
source-checked diagrams only after the implementation works.

Enums, tagged unions, general generics, interface specialization, and new object
relationship machinery are outside this increment. Do not add a second type
checker or an AST-emission fallback to implement aggregate behavior.

## After composite types — specialization and multi-class interfaces

[Chapter 12](chapter-12.md) implements multi-class generics with participant
class blocks and explicit, optionally labeled empty impls. The demonstrated
DoublyLinkedList contributes fields and methods to Graph and Node, including
independent labeled memberships. Nullable handles, initialization, and counted
field ownership are implemented. Callers explicitly detach before destroying
ordinary endpoints; this intermediate chapter is not fully ordinary-link safe.
Relation statements, managed owns/refs policy, and relationship-aware teardown
remain later work. Generated membership is not already an owning relationship.
See the [development record](notes/ch12-development.md) and
[current reconstruction status](notes/ch12-student-status.md).
Interface conformance requires behavior, not implicit storage injection or runtime
wrappers. Preserve every participant and endpoint-role mapping in binding and
specialization identity. Type interning does not merge specializations that differ
in declaration, concrete arguments, selected implementation or participant roles.

## Chapter 13 — Maintained relationships and generated teardown

[Chapter 13](chapter-13.md) implements the bounded scalar relationship increment. The reference passes the cumulative Debug/Release target, five independent PEG suites, and `spin13` in both layouts. The single code and text reviews are incorporated. Fresh isolated reconstruction passed the cumulative Debug/Release checks, five PEG suites and walkthrough; public hashes verified and student stopped. Bill has accepted Chapter 13 and the `spin13` walkthrough. See [author verification](notes/ch13-author-verification.md) and [development decisions](notes/ch13-development.md). These results do not establish completed single-threaded memory safety.

Use explicit parent/child participants, managed contributed object links, owns/refs on the implementation, designated add/remove operations with prepare blocks, and partial owns destroy(self) / partial refs destroy(self) fragments. owns accepts ordinary children only; each refs membership to a counted value retains once. Scalar participant values inside the matching maintained relationship borrow; unrelated counted values and owning application boundaries retain ordinary accounting. All code in the relationship interface is privileged to maintain its own managed links, without disabling ordinary lifetime/type checks. Finalization comes first; detach from all parents before cleaning up children; ordinary field cleanup follows, and free is last. Trusted teardown fragments supersede the former safe-iterator prerequisite for generated destruction. Safe generators remain separate application-iteration work.

Demonstrated lists cover owning and reference memberships, independent bindings, and cross-membership cascades. The earlier three-role graph/self-loop proposal is not a completion claim or a reason to expand this chapter. Unmanaged persistent ordinary links are rejected, including container concealment; ordinary counted fields remain permitted. Explicit Chapter 13 successors preserve the affected Chapter 12 behavior checks. Direct borrowed-participant aggregates are temporarily rejected, while explicit concrete owners and ordinary application containers remain supported.

## Future chapter sequence — current planning order

After completing and accepting Chapter 13, use this dependency order. Only Chapter
14 has a fixed number; later topics may split into chapters when development earns
that boundary. This supersedes the older broad sketch in the book index.

1. **Chapter 14: single-threaded safety closure.** Ordinary containers own counted
   elements, including values read from participant links; layout-specific handles
   and the focused lifetime/ownership audit support the safety boundary. Mandatory
   relationship construction is deferred beyond this book.
2. **Enums, tagged unions, and pattern matching.** Implement active-variant storage,
   construction, discriminant checking, exhaustive matching, payload binding,
   copying/moving and destruction of only the active payload. Include generic
   tagged types needed for Result<T>; do not make compiler clients maintain a
   disconnected tag and payload convention. Existing language contracts guide the
   work; enum parsing is not semantic or runtime implementation.
3. **Generators and iteration.** Ordinary reusable traversal, then explicit safe
   current-item destruction with cleanup on early exit. Split safe traversal if
   needed; read-only graph algorithms need not wait for deletion-safe iteration.
4. **Composing multi-class libraries: a reusable graph.** Compose the existing
   doubly linked list into graph/node/edge roles, preserving endpoint and binding
   identity under application-chosen class names and labels. Demonstrate one cycle
   algorithm on two user-defined models, including a social network, with explicit
   directed/undirected semantics, self-loops, parallel edges, disconnected parts,
   and teardown. Keep arbitrary existing-representation adapters out unless a real
   example requires them. Current compile-only probes show nested impl is rejected
   by parsing and generic top-level impl is unsupported. The preliminary concrete
   graph only emitted C; it is not the requested generic graph demonstration.
5. **Modules and standard-library foundations.** Multi-file compilation, public and
   private names, imports, standard-library visibility and initialization/shutdown;
   preserve Common/spoke architecture in Lyric. Establish Result<T>, Status and
   checked error propagation before fallible library APIs. Generic tagged types
   reuse the specialization foundation without requiring generic classes first.
6. **Standard-library relationships and Sym.** Rune-like reusable relationships,
   hashing/equality contracts, symbol interning, and compiler-oriented byte/string
   utilities. The detailed inventory below is a requirement guide, not a promise
   that all of it fits in one chapter.
7. **Generic classes, demonstrated by Dict<K,V>.** Build on the library relationships;
   verify concrete type identity, entry lifetimes, collision handling, replacement,
   rehash and cleanup. HashedList<Table,Entry> is optional, not mandated.
8. **Floating-point values and operations.** Dedicated implementation chapter for
   f32/f64 literals, arithmetic, comparisons, conversions, function boundaries and
   aggregate storage/cleanup. Follow existing IEEE contracts, including NaN,
   infinities, signed zero and rounding/conversion boundaries; preserve valid
   behavior without blanket fast-math. Verify emitted execution and diagnostics,
   not merely parsing or C type mappings. This is confirmed missing: the current
   Release compiler rejects `let x:f64 = 1.5f64` with LYC306 (Float). Complete it
   before numeric formatting/I/O and bootstrap readiness.
9. **Practical I/O and typed formatting.** Standard streams, files, binary bytes,
   buffering, explicit errors, command-line arguments and the process operations
   needed to invoke the C toolchain. Typed formatting/interpolation, no variadics.
10. **Bootstrap-readiness and guided Lyric reconstruction.** Audit real compiler
   needs against supported features; build representative compiler components,
   then reconstruct in Lyric under Bill's guidance. Improve the C# bootstrap and
   Edition 1 whenever a demonstrated gap warrants it. Add chapters rather than
   force architectural workarounds. Self-compilation and conformance/fixed-point
   verification remain distinct milestones, followed by human use and crossover.

This is a construction plan, not an implemented-feature list. In particular,
audit numeric-library coverage beyond the floating-point chapter, byte parsing,
StringBuilder/bulk append, sorting/search, hash/key stability, filesystem/process
failure cleanup, and deterministic build/diagnostic behavior against actual
compiler use. Some belong within the chapters above; a missing substantive
capability deserves its own increment. Closures are not automatically required
for self-hosting; add nonescaping callbacks if a concrete API needs them.
Concurrency, dynamic dispatch, escaping closures, variadics, and the advanced
compacting allocator are not automatic prerequisites.

## Chapter 14 — Bounded patch chapter and safety closure

The [Chapter 14 draft](chapter-14.md) starts from the accepted Chapter 13 endpoint and the [starting plan](notes/ch14-starting-plan.md). Ordinary containers own their counted elements; scalar participant borrowing does not propagate into containers. Production AoS uses C pointers with NULL; production SoA uses u32 indices with null zero, slot zero unused, and indices 1 through 2^32 - 1. Nonnullable value fields must be initialized before the constructor returns; cleanup-safe empty bits do not satisfy that rule. Mandatory relationship construction remains unsupported and is deferred beyond this book.

Close or explicitly reject remaining unsafe paths, and verify lifetime effects across calls, ownership boundaries, cleanup and cascades before claiming conditional single-threaded memory safety. The claim assumes a correct compiler/runtime and correct trusted relationship implementations, excludes unsafe/foreign contract violations, and promises neither leak freedom nor concurrency safety. The accepted gap in raw borrowed-address debug detection is not a substitute for ordinary safe-language protection. The author audit, post-migration cumulative Debug/Release checks, and scripted walkthrough have passed. Both independent reviews and fresh isolated reconstruction are complete. Bill has approved the prose and `spin14`; commit permission remains separate. See the [reconstruction receipt](notes/ch14-student-acceptance.md).

## After Chapter 14 — traversal, then later I/O and formatting

Bill supports generators and iteration as the next direction after the bounded
safety chapter. Start with ordinary reusable traversal, then explicit safe
current-item-destruction iteration; split into separate increments if needed.
Chapter numbering and precise boundaries remain provisional.

Near the end of the book, add practical I/O and richer `print`/`println`: standard
streams, file and byte operations, buffering and error handling as required by
compiler use. String interpolation supplies formatting, not the entire I/O
contract. Prefer shared formatting operations rather than separate conversion
rules for interpolation and printing. This is planned work, not implementation.

Variable-length parameter lists are deferred. Bill explicitly dropped them from
the current plan: add them later only when a concrete need justifies them.
Typed formatting and interpolation should not depend on variadic tuple packing.

## Standard-library relationships before generic classes and Dict

Bill requests a standard-library chapter covering reusable relationships before
the generic-class/Dict chapter. Use Rune's builtin library as a source of concrete
requirements, not as an implementation to copy blindly. Read-only source inventory
at `/Users/bill/projects/rune`, commit
`a8fb12f974736f20eaefb1f614df68411dac7bc8`, identifies LinkedList, TailLinked,
DoublyLinked, OneToOne, ArrayList, Hashed, HashedClass, and HeapqList transformers.
`Heapq` and `Dict` are containers layered on relationships; `array.rn` adds array
iteration rather than declaring an Array relationship. Source:
https://github.com/google/rune/tree/main/builtin .

Plan a comparable Lyric set: singly linked, tail-linked and doubly linked lists;
one-to-one; indexed array membership; hash-keyed membership; and priority-queue
membership. Decide whether the two Rune hashing forms need separate Lyric interfaces
or one appropriate key/hash/equality contract. Preserve automatic detachment,
owns/refs semantics, labeled independent memberships, collision-correct equality,
and traversal/cleanup behavior. Ordinary and safe iteration must obey Lyric's own
contracts; source inventory is not an audit or execution of Rune's algorithms.

The same standard-library chapter must include proper `Sym` support, as Bill
explicitly requests. Follow the existing specification's Symbols and interning
contract: counted value class, private immutable byte name and cached hash,
canonical program-lifetime interning, full-name equality under hash collisions,
valid empty names, independent name copies, and backtick literals equivalent to
`sym(text)`. The intern table releases counted references at shutdown rather than
force-destroying retained symbols. Its initialization/shutdown mechanism must be
made concrete and tested, not hidden behind an assumed global runtime service.
A direct hash-relationship intern table can precede generic Dict; do not introduce
a circular prerequisite merely for implementation convenience. `Dict<Sym,V>` then
becomes a compiler-relevant test of the generic container.

A following chapter introduces generic classes with `Dict<K,V>` as its demanding
example. Bill previously used a HashedList relation between Table and Entry; this
is an option, not a requirement. Rune's dict.rn uses a generic nested Entry and a
Hashed relationship; choose explicit generic entry classes versus nested generic
classes based on the demonstrated Lyric design, not accidental source imitation.
Verify specialization identity, hashing/equality collisions, resize/rehash, key
stability, entry identity/lifetime, value replacement, removal and cleanup.
Retained Lyric dictionary decisions remain authoritative unless explicitly revised.
Module/library foundations and reusable relationship composition must be scheduled
where their actual dependencies require them. Exact chapter numbers remain open;
this plan does not expand the current Chapter13 reconstruction inputs.

## First-edition readiness means a good self-hosted compiler

The goal is a clean, maintainable compiler developed in Lyric under Bill's guidance,
not merely the earliest executable that can compile its own source. Bill reports
that the original Lyric compiler self-hosted too early and left an implementation
he now wants to replace. Preserve that lesson without treating self-compilation
alone as evidence of architecture, safety, or maintainability.

Edition 1 must supply the language, library, bootstrap compiler, and construction
teaching needed to do this well. The Lyric reconstruction may reveal missing
capabilities or inadequate designs. When it does, develop and verify the needed
increment in the C# bootstrap, add or revise the corresponding first-edition
chapter and public graders, then resume the Lyric construction. Whole new chapters
are appropriate when the demonstrated gap warrants them. Do not force permanent
workarounds into the self-hosted compiler merely to keep the port moving, and do
not add speculative features without a concrete compiler use case.

Preserve the Common/functional-module architecture, authoritative relationships,
complete lifetimes, and readable teaching code in idiomatic Lyric rather than
mechanically translating C#. Working bootstrap, compiler written in Lyric,
self-compilation, and repeatable conformance/fixed-point evidence are separate
milestones. Edition 2 teaches the Lyric construction as it is developed; Edition 1
remains a maintained foundation, not a frozen artifact that cannot accept lessons.
This planning decision does not authorize starting that reconstruction now.

## Performance storage after bootstrap

Ordinary C-heap array payloads are permitted for the bootstrap and do not block self-hosting. They are not the intended final allocation strategy. The specification’s field-specific payload heaps, append-at-end fast allocation with roughly 50% heap growth, two-index hole headers, alignment, generated compaction, bounded array counts, and selectable object-reference widths are planned upgrades needed for the benchmark-winning design. Measure representative applications rather than infer a gain from layout alone.

This work must reuse storage-effect checking: field-heap relocation can invalidate payload borrows across multiple owners, while descriptor borrows depend on whether their own storage moves. Compaction moves values without artificial retain/release or finalization. Source syntax, width limits/units, and hole policies must be settled before implementing the compact allocator. Capacity queries return u64; independent copies have capacity equal to length recursively, while references observe original capacity. Empty arrays may reserve capacity; resize(0) explicitly releases their block and resets capacity. The bootstrap implements that contract through the ordinary capacity query and resize cleanup path; verification is recorded separately from this future allocator roadmap. Preserve lazy zeroed allocation opportunities: reserve must not eagerly initialize spare capacity. Do not expand the current Chapter 8 bootstrap slice to implement this allocator.

## Future version: mandatory relationships

Outside this book: a mandatory parent exists throughout the child's live lifetime.
The intended child constructor receives the parent and calls its attachment
function before returning, establishing both sides rather than assigning a
backlink alone. Removal and parent-teardown policy still need design; no policy
restriction or implementation is implied by this plan. Chapter 14 retains
protected-write and definite-initialization rejection for unsupported construction.

Bill's motivation includes speed: a genuine mandatory-parent guarantee can avoid
null checks when accessing that parent. This is a design opportunity, not a
measured performance result; the lifetime guarantee must justify removing checks.
