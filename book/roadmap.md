# Agreed next construction increments

This is the dependency spine agreed with Bill after Chapter 7, not a fixed table of contents or authorization to skip verification. Chapter 8 is authorized for implementation. Later chapter boundaries will follow demonstrated increments.

## Chapter 8 — Standalone classes and lifetime policies

Implement ordinary classes and reference-counted `value class` objects, constructors, fields, methods, and final hooks. Begin with scalar fields; build real counted ownership through local bindings, calls, returns and arrays. Ordinary objects have explicit destruction; counted objects release references and cannot be forcibly destroyed. Neither copied object reference clones its payload. No object-to-object field graph, relations, fallible construction or general generics in this increment.

Use chunked nonmoving AoS with free-slot reuse; allocate from the last chunk's unused slots before adding another chunk. SoA uses growable per-field arrays. Object handles are indices. Potential class-storage growth invalidates still-needed field borrows conservatively in both layouts; ordinary handles survive growth, not destruction. Analyze class accesses using the existing storage-selection/effect model, rather than generating allocator syntax for a second analysis pass. Final hooks and generated counted releases have visible transitive effects. Initial backend uses immediate retain/release and scope cleanup; language-wide counted-finalization timing remains broader.

## Interfaces, then cooperating types

Next teach ordinary interfaces and the generic specialization needed for a useful algorithm. Interface conformance requires behavior, not implicit storage injection or runtime wrappers. Then extend the same mechanism to multi-class interfaces and explicit participant mappings; start with an example that does not secretly require persistent relationships.

## Relationship prerequisites and the graph

Place nullable references, Result/Status/propagation, and iteration/generators deliberately before the relationship features that require them. These may take separate chapters; do not compress them to preserve provisional chapter numbers.

Return to a directed graph with three strong relationships: Graph:nodes to Node:graph, Node:outgoing to Edge:source, and Node:incoming to Edge:target. Both roles form binding identity, including self-loops. Relationship declarations provide maintained storage, two-way operations and ownership; introduce basic generated detach-and-cascade destruction in the same increment, not a leaking interim graph. Final hooks witness required ordering and exactly-once destruction. Later increments can improve representations and generic algorithms while preserving the behavioral tests.

Chapter 8's local cleanup (final, owned value fields, slot reclamation) is not yet relationship-aware structural destruction. Do not add a manual structural-destructor facility or reference-counted graph workaround merely for a chapter example.
