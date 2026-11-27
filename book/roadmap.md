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

## Chapter 13 — Maintained relationships and generated teardown (design)

Chapter 12 is accepted. Chapter 13 targets single-threaded relationship memory safety, conditional on correct compiler/runtime and trusted relationship implementations. The standard library supplies core relationship algorithms; arbitrary add/remove correctness is not proved by the compiler. See [the agreed syntax, boundaries, and open questions](notes/ch13-development.md). No Chapter 13 implementation or verification is claimed.

Use explicit parent/child participants, managed contributed object links, owns/refs on the implementation, designated add/remove operations with prepare blocks, and partial owns destroy(self) / partial refs destroy(self) fragments. owns accepts ordinary children only; each refs membership to a counted value retains once. All code in the relationship interface is privileged to maintain its managed links, without disabling ordinary lifetime/type checks. Finalization comes first; detach from all parents before cleaning up children; fragment order within each phase is unspecified; ordinary field cleanup follows, and free is last. Representation-specific teardown fragments supersede the requirement for a safe iterator in generated destruction. Explicit safe generators remain relevant to application iteration, not a mandatory teardown prerequisite. Nullable handles already exist. Invalid attachment/removal checks fail before mutation; Result/Status is not a prerequisite for this increment.

Demonstrate Graph:nodes owning Node:graph, Node:outgoing owning Edge:source, and Node:incoming owning Edge:target, with complete role identity including self-loops. Exercise cross-relationship cascades, multiple owners, counted and non-owning memberships, and partial construction. Do not promise full ordinary-link safety while leaving unrestricted persistent ordinary references as a bypass. Chapter 13 rejects unmanaged persistent ordinary links, including hiding them in containers/aggregates; ordinary counted fields remain permitted. Update contracts and retained tests explicitly when closing Chapter 12's temporary allowance.

## Performance storage after bootstrap

Ordinary C-heap array payloads are permitted for the bootstrap and do not block self-hosting. They are not the intended final allocation strategy. The specification’s field-specific payload heaps, append-at-end fast allocation with roughly 50% heap growth, two-index hole headers, alignment, generated compaction, bounded array counts, and selectable object-reference widths are planned upgrades needed for the benchmark-winning design. Measure representative applications rather than infer a gain from layout alone.

This work must reuse storage-effect checking: field-heap relocation can invalidate payload borrows across multiple owners, while descriptor borrows depend on whether their own storage moves. Compaction moves values without artificial retain/release or finalization. Source syntax, width limits/units, and hole policies must be settled before implementing the compact allocator. Capacity queries return u64; independent copies have capacity equal to length recursively, while references observe original capacity. Empty arrays may reserve capacity; resize(0) explicitly releases their block and resets capacity. The bootstrap implements that contract through the ordinary capacity query and resize cleanup path; verification is recorded separately from this future allocator roadmap. Preserve lazy zeroed allocation opportunities: reserve must not eagerly initialize spare capacity. Do not expand the current Chapter 8 bootstrap slice to implement this allocator.
