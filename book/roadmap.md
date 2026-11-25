# Agreed next construction increments

This is the dependency spine agreed with Bill, not a fixed table of contents or authorization to skip verification. Chapters 8 through 10 are complete through reviews, independent reconstruction, and Bill’s acceptance of their walkthroughs. Composite types and canonical type identity now form Chapter 10; specialization and multi-class interfaces follow that foundation. Later relationship chapter boundaries remain provisional.

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

Start with a method-based example that does not require persistent relationships.
Nullable references, Result/Status/propagation and generators are prerequisites
for later relationship features, not for that first multi-class contract.
Interface conformance requires behavior, not implicit storage injection or runtime
wrappers. Preserve every participant and endpoint-role mapping in binding and
specialization identity. Type interning does not merge specializations that differ
in declaration, concrete arguments, selected implementation or participant roles.

## Relationship prerequisites and the graph

Place nullable references, Result/Status/propagation, and iteration/generators deliberately before the relationship features that require them. These may take separate chapters; do not compress them to preserve provisional chapter numbers.

Return to a directed graph with three strong relationships: Graph:nodes to Node:graph, Node:outgoing to Edge:source, and Node:incoming to Edge:target. Both roles form binding identity, including self-loops. Relationship declarations provide maintained storage, two-way operations and ownership; introduce basic generated detach-and-cascade destruction in the same increment, not a leaking interim graph. Final hooks witness required ordering and exactly-once destruction. Later increments can improve representations and generic algorithms while preserving the behavioral tests.

Chapter 8's local cleanup (final, owned value fields, slot reclamation) is not yet relationship-aware structural destruction. Do not add a manual structural-destructor facility or reference-counted graph workaround merely for a chapter example.

## Performance storage after bootstrap

Ordinary C-heap array payloads are permitted for the bootstrap and do not block self-hosting. They are not the intended final allocation strategy. The specification’s field-specific payload heaps, append-at-end fast allocation with roughly 50% heap growth, two-index hole headers, alignment, generated compaction, bounded array counts, and selectable object-reference widths are planned upgrades needed for the benchmark-winning design. Measure representative applications rather than infer a gain from layout alone.

This work must reuse storage-effect checking: field-heap relocation can invalidate payload borrows across multiple owners, while descriptor borrows depend on whether their own storage moves. Compaction moves values without artificial retain/release or finalization. Source syntax, width limits/units, and hole policies must be settled before implementing the compact allocator. Capacity queries return u64; independent copies have capacity equal to length recursively, while references observe original capacity. Empty arrays may reserve capacity; resize(0) explicitly releases their block and resets capacity. The bootstrap implements that contract through the ordinary capacity query and resize cleanup path; verification is recorded separately from this future allocator roadmap. Preserve lazy zeroed allocation opportunities: reserve must not eagerly initialize spare capacity. Do not expand the current Chapter 8 bootstrap slice to implement this allocator.
