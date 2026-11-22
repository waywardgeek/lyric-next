# IR and multi-class interface construction decisions

## Status and motivation

Bill and Cadence agreed to introduce an **IR**, without committing to separate
HIR/LIR layers. This is a place for Lyric-specific algorithms: reference-count
optimization, array copy-to-move conversion, and other transformations that need
language ownership and value semantics. C remains the durable backend for the
human-guided project. Clang is not expected to reconstruct those high-level facts
from arbitrary generated counter manipulation and cleanup branches.

Bill corrected the Rune history: he regretted **not having an LIR** when writing
Rune's LLVM IR generator. Do not describe the extra representation as machinery
Rune already had. His benchmark experience motivates keeping C's frontend and
optimization pipeline; it is not a general proof that LLVM IR produces slower code.

The current compiler has checked syntax, symbols and semantic facts, temporary
analysis graphs, and emitter-local ownership expansion. It does not already have
the proposed optimizable execution IR. The decisions below are design direction,
not an implementation or performance claim. Chapter 5's author-voice opening now uses the corrected account: Rune lacked
the desired IR, and direct LLVM emission did not automatically provide
Clang-quality optimizer input. No controlled backend comparison is claimed.

## Agreed responsibility boundary

1. Parse and resolve source declarations, types, constraints, and complete
   interface bindings. The semantic model owns the resolution result.
2. Specialize the selected bodies and construct concrete IR. Abstract calls and
   fields resolve to actual declarations; defaults and inline adapters become
   concrete bodies. IR construction consumes bindings rather than repeating name
   lookup or conformance inference.
3. Complete effect/lifetime checking with generated ownership and cleanup visible.
   Generated specializations are not exempt. Recursive calls and specialization
   may require worklists rather than a single linear sweep.
4. Optimize IR while preserving the accepted program's behavior, ownership,
   lifetime, evaluation, failure and permitted cleanup contracts.
5. Emit C from resolved operations. The C backend must not select interface
   implementations, solve participant mappings, or invent ownership semantics.

These are responsibilities, not a mandate to put all semantics in today's Check
function or create five independent whole-program passes. Functional modules use
the Common vocabulary. Owned IR objects need explicit parents, complete backlinks,
and invalidation/destruction; copied symbol/service tables are not a substitute.
The precise IR object model and pass algorithms remain development work.

## What the IR must make expressible

Retain checked types, concrete symbol/class identities, source locations, and
storage/owner provenance. Make execution order and copy, move, borrow, retain,
release, calls, checks, branching, returns and cleanup explicit enough for the
passes that transform them. Keep one authoritative lowered account of execution
rather than separate optimizer and emitter reconstructions of cleanup.

A retain/release pair cannot be cancelled merely because its arithmetic nets to
zero: the temporary holder may keep an object alive across a call. Replacing an
array copy with a move must respect later source use, aliases, independent value
semantics, observable capacity, and cleanup. Preserve failure behavior and effects
where required; compiler-created operations are not invisible to safety analysis.
A successful refactor is not a speedup claim. Measurements should select and
justify subsequent optimization passes.

No separate machine IR, mandatory SSA conversion, general-purpose optimization
framework, or comprehensive optimization suite has been approved as a prerequisite.
Structured control flow versus basic blocks, generic IR templates versus earlier
specialization, and precise ownership-analysis reuse remain implementation choices.
The spec's existing --lir-dump spelling is not evidence that an LIR exists; any
rename to an IR-oriented inspection command needs an explicit contract decision.

## Multi-class binding identity survives lowering

Section 13 already requires every participant, concrete implementation identity,
and both endpoint roles to participate in satisfaction and specialization. The
receiver alone is insufficient. Preserve actual declaration references and role
mappings, not only emitted names or participant type names.

Collection<Node:outgoing, Edge:source> and
Collection<Node:incoming, Edge:target> are distinct bindings even though the
participant classes match. A default method must receive the whole selected
binding. Per-member adapters must stay consistent with that binding. Ordinary
interface requirements do not acquire stronger safe-generator permissions just
because a selected concrete implementation has them.

Capability satisfaction selects existing behavior and storage; it injects none.
An explicit storage-providing impl or relation declaration may supply fields,
operations and cleanup templates. Elaborate these into concrete model declarations
and bodies, and route them through the same IR and safety pipeline. A relation
must not become a runtime wrapper or a second copy of maintained state.

## Keep early execution — agreed editorial decision

Bill asked whether current Chapter 5 C emission should move after the IR, or
whether early working code justifies replacing some implementation later.
Bill accepted keeping Chapter 5. It establishes executable behavior and retained
checks early, before arrays/classes introduce the ownership operations that make
the IR's purpose concrete. A later IR chapter should migrate the backend boundary,
not throw away the working C target or change accepted source behavior.

Preserve runtime helpers, target contracts, class storage policies and behavioral
graders. Move evaluation-order, ownership and cleanup decisions from direct
syntax-to-text traversal into explicit operations; C emission becomes a consumer
of those operations. Some traversal and temporary bookkeeping will be replaced.
This is intentional, bounded scaffolding with an observable refactor, not a second
permanent backend or duplicated semantic authority.

Do not claim that all current code can be reused unchanged. The IR needs its own
ownership/invalidation contracts and integration with existing safety analysis.
Use cumulative behavior as a regression oracle, not as a substitute for checking
those structural changes. Chapter 5 stays in its current position; no existing
chapter is moved.

## Agreed next chapters — implementation not started

**Chapter 9: Explicit execution and ownership IR.** Refactor the
existing supported language through the IR with unchanged semantics, AoS/SoA
acceptance and retained behavior. Make the operations inspectable and provide a
concrete target for specialization. Do not delay interfaces until every prospective
optimization is implemented. Choose any demonstrated optimization as a bounded
separate scope decision, not an automatic chapter requirement.

**Chapter 10: Generic specialization and multi-class interfaces.**
Treat a one-participant interface as the small first example of the same mechanism,
not a separate chapter that postpones cooperating types. Start with concrete
classes and generic functions/defaults over their method capabilities. Exercise
multiple bindings sharing a receiver but differing in another participant, exact
selection, ambiguity rejection, adapters/defaults, and safety effects. Keep
participant/role identity complete from the beginning, even before maintained
relations are implemented. The exact supported source forms and graders still
need a bounded development agreement.

The real prerequisites are type-variable substitution/inference, complete binding
resolution, specialization identity/worklists, and checking generated bodies.
They can be built within the interface chapter. Generic classes, persistent graph
fields, nullable values, Result/Status/?, generators, concurrency, and maintained
relations are not prerequisites for a method-only cooperating-types example.

Relations follow multi-class interfaces. Their own prerequisites (nullable
backlinks, mutation failure contracts, traversal and structural destruction) remain
necessary and must be placed before the relation increment. Do not introduce
manual destructors or leaking graphs merely to preserve chapter numbers.

Combining the IR and interfaces in Chapter 9 is possible, but joins a substantial
backend refactor with new language semantics and makes failures harder to isolate.
Bill chose a short focused IR chapter, then the full cooperating-type mechanism
without an intervening ordinary-interface-only chapter. Chapters 9 and 10 are the
agreed sequence. This records the construction plan; implementation has not begun.
Bill is currently considering further openings for existing chapters.
