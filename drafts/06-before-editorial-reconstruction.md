# Lyric Language Specification

**Draft 3.3 — explicit lifetimes and shared/exclusive per-class lifetime/allocation protection.**

Lyric is a typed language built around explicit relationships, generated destruction, and readable programs. Its compiler should enforce mechanical invariants instead of asking programmers to reconstruct them by convention.

This is a design specification, not a description of a finished compiler. It preserves Bill's requirements and the central ideas of the earlier Lyric documents. It removes speculative machinery from the previous draft. Unsettled safety details are named at the end; they are not claims of implemented guarantees.

## 1. Principles

### Keep it simple

KISS is a requirement. Prefer an ordinary function, class, or relation over a new category or special case. A feature earns its place by solving a demonstrated problem. Fewer lines of syntax are not a simplification if they introduce more hidden rules.

The compiler must not accept unsupported features and emit placeholders. It should explain errors in terms of source declarations, not require the programmer to decipher generated C.

### Make relationships explicit

Shared declarations define types, stored state, relationships, and externally callable operations. Functional units implement those same declarations. They do not introduce wrappers, service bags, or duplicate mutable state merely to connect the program.

Constructors receive their owning parents and establish both ends of their relationships before returning. Relationship mutations maintain both ends. Refusal leaves the graph unchanged. Children navigate to services through their actual parents.

An object can have several owning parents. One authoritative home for a fact does not mean one parent per object.

### Make lifetime behavior predictable

- No garbage collector.
- Structural destruction is generated from declarations.
- A class may define a `final` method called before that object's structural destruction.
- Static analysis invalidates local references after calls that may destroy objects of their class.
- Safe relationship iteration permits deletion only of the current object of the iterated class.
- Array-slot references retain the root and indices, not a potentially stale interior address.
- Indexing is bounds checked by default.

A `value class` uses reference counting. An ordinary `class` uses explicit/cascading destruction. The declaration states the lifetime policy; the compiler does not discover or change it by searching for owning relations. Value-like holders do not require backlinks solely for reclamation; this is not an excuse to omit relationships between components.

### Distinguish design from evidence

Self-hosting is useful evidence of capability, not proof of correctness or memory safety. A safe compiler must reject invalid programs as reliably as it runs valid ones. The compiler's own object model should demonstrate the discipline it implements.

## 2. Source organization

Executable source uses `.ly`. Declaration-only source uses the existing `.lyric` extension. There is no new file extension or `implement` keyword.

A package is an explicitly configured group of source files sharing one namespace. It may span several directories; a directory does not automatically create a package or access boundary. Declaration and file order do not affect meaning. A module is a project rooted at `lyric.mod`; its configuration identifies package membership and dependencies. The exact configuration-file grammar remains toolchain work, but membership must be explicit and deterministic rather than inferred from implementation discovery.

Small programs may keep declarations and bodies in one file. Larger programs use shared declarations and functional implementations of those declarations:

```text
common/model.lyric
parser/parser.ly
checker/checker.ly
application/main.ly
```

These directories may belong to one logical package. Common owns the shared declarations; functional files supply bodies for those declarations using ordinary qualified names, such as `func Node.parse(...)`. The body and declaration identify one callable on one type, not an extension object, duplicate storage, or forwarding wrapper. Package-private declarations are accessible across the package's files regardless of their directories.

Implementation matching uses the resolved package, declaring type/interface where applicable, callable name, and generic parameter/signature contract. Parameter and return types, reference modes, generic arity and constraints, and instance/static status must agree after resolving names and corresponding type parameters. A differently spelled but equivalent alias does not create a different type. Signature mismatch is a diagnostic, not a new overload silently invented to satisfy the declaration. Every required callable has exactly one body or explicitly declared external implementation; missing and duplicate implementations are errors. A body included directly with its declaration counts as that implementation. Interface abstract requirements and defaults follow their own satisfaction rules, rather than requiring a body for an abstract slot in the declaring package.

The architectural requirement remains that Common supplies shared vocabulary, state, relationships, and callable declarations; functional units consume those declarations rather than each other's implementation internals; application code composes the program. Package privacy alone does not enforce this internal dependency discipline. Any compiler/tooling check of Common-versus-functional-unit dependencies needs its own explicit contract; directory organization is not evidence that such a check exists.

Names are private to their package unless marked `pub`. Import access is qualified:

```lyric
import ast
import parse from "parser"
```

The compiler resolves imports recursively and diagnoses unresolved or ambiguous declarations. Dependency policy and `lyric.mod` contents are toolchain design, not reasons to add restrictions to object graphs.

Every concrete callable declaration has a matching implementation or an explicitly declared external implementation under the matching rules above. A bodyless concrete declaration is not an empty function. Imported packages expose only public declarations; an implementation in a different package cannot claim package-private access merely by naming the original type.

## 3. Basic syntax

Identifiers are case-sensitive. Capitalization does not determine visibility, type variables, or pattern meaning.

Newlines terminate statements, except inside parentheses and square brackets. Wrap a continued expression in parentheses; there is no new implicit continuation rule merely because the preceding line ends in an operator. Declaration clauses remain on the same logical line. This retains the original newline rule.

Braces delimit blocks. `//` starts a line comment. Block comments are not necessary for the initial language.

```lyric
func add(a: i32, b: i32) -> i32 {
    return a + b
}

let x = 42
var count: i32 = 0
```

`let` declares an immutable value binding; `var` declares a mutable one. There is no alternative `let mut` declaration. For an array value, `let` prohibits changing its elements or length as well as rebinding the variable. Neither declaration freezes separately referenced class objects. `ref` denotes a reference type or a reference expression. Parameters use `name: ref T`, and arguments use `ref storage`. `mut` is not part of the new syntax.

Parameters and return values are typed. An omitted return type means `unit`. Local types may be inferred. Generic type variables are explicitly declared:

```lyric
func identity<T>(value: T) -> T {
    return value
}
```

They are never inferred from a capitalized identifier or a misspelled type name.

The canonical function declaration keyword is `func`; the callable type spelling is `fn(T) -> U`. One spelling for each purpose is sufficient.

## 4. Types and literals

### Scalars

The initial scalar types are:

```text
bool
u8 u16 u32 u64
i8 i16 i32 i64
f32 f64
string
unit
```

Larger widths may be added when needed. Foreign interfaces must specify ABI widths explicitly rather than make ordinary code depend silently on C's integer sizes.

Integer literals default to `i32` unless context determines another representable integer type. Float literals default to `f64`. Decimal integers permit underscores. Hexadecimal, octal, and binary forms are useful straightforward additions, but not prerequisites for the relationship model.

`true`, `false`, and `null` have their usual literal meanings. `nil` is not an alternative spelling.

Strings use double quotes. Byte characters use single quotes. F-strings interpolate expressions, as in `f"count={count}"`. Backticks construct interned symbols, as in `` `name` ``.

The concrete lexical baseline appears in Appendix A. Numeric conversion and overflow rules that remain undecided are listed separately; an implementation must not infer them from accidental backend behavior.

### Unit and absence

`unit` has exactly one value, written `()`, which carries no information. It is the ordinary result type of a function with no declared return type; there is no separate `void` type. Bare `return` or reaching the end of a unit-returning function supplies this value. `null` instead denotes optional absence and is not the unit value.

Empty parentheses in expression position form a unit value. They use the ordinary separate `(` and `)` tokens, not a special two-character token; whitespace between them is permitted. Parentheses following a callable expression remain the call syntax:

```lyric
work()        // call with no arguments
let value = () // unit value
work(())      // call with one unit argument
```

The unit payload need not occupy runtime storage. A containing tagged type such as `Result<unit>` still distinguishes its success and failure alternatives. Section 14 specifies unit success for fallible functions.

### Composite types

```text
T?               optional T
T | U            tagged union
[T]              array
(T, U)           tuple
fn(T) -> U       callable type
gen T            generator result
ref T            reference to existing T storage; array paths use a fat representation
```

Tuple fields use `._0`, `._1`, and so on. Destructuring uses `let (a, b) = pair()`.

Transparent aliases use `type Name = Type`. An alias does not create a second nominal type.

Null is accepted only in nullable positions. Optional wrapping is implicit; unwrapping uses `!` or a checked conditional. No unsafe cast may manufacture an arbitrary live object reference.

## 5. Structs, enums, and classes

### Structs

Structs are values without identity:

```lyric
struct Point {
    x: f64
    y: f64
}
```

They are copied as values. Writable reference parameters use `ref` in the type, with an explicit reference expression at the call site:

```lyric
func translate(point: ref Point, dx: f64) {
    point.x = point.x + dx
}
translate(ref p, 3.0)
```

Copying a struct does not exempt object references stored inside it from lifetime checking.

### Enums

Enums describe alternatives, with optional payloads:

```lyric
enum Shape {
    Circle(radius: f64)
    Rect(width: f64, height: f64)
}
```

Pattern matching supports variants, tuples, literals, bindings, guards, and `_`. Expression branches must produce compatible result types. A match producing a value must be exhaustive. The compiler must not use the first branch's type without checking the others.

### Classes

Classes have identity. Two objects with identical fields are still different objects.

```lyric
class Node {
    name: string
    func Node(self, graph: Graph, name: string)
}
```

The declaration makes lifetime policy explicit:

```lyric
value class Matrix {
    rows: i32
    columns: i32
    // Mutable, reference-counted value-like object.
}

class Node {
    // Explicitly destroyed or cascade-deleted through owning relations.
}
```

A value class may be mutable. The modifier does not imply stack allocation, deep copying, or immutability. An ordinary class does not become a value class merely because it currently has no parent. There is no separate `root class` category. Both kinds may have methods and a `final` hook.

There is no classical inheritance. Interfaces describe capabilities; relations describe stored relationships.

Methods may be declared in a class and defined separately using qualified names. For a constructor declared as `func Node(self, ...)` inside Node, its external definition is `func Node.Node(self, ...)`. This uses the ordinary qualified-member rule, not a separate constructor grammar.

## 6. Relations

A relation describes both endpoint roles, representation, cardinality, and destruction behavior.

```lyric
relation ArrayList Team:roster owns [Player:team]
relation DoublyLinked Room:guests refs [Guest:room]
```

- The hint, such as `ArrayList`, chooses representation.
- `owns` means parent destruction cascades to its children.
- `refs` means parent destruction only unlinks the relationship.
- Brackets mean multiple children.
- Labels name roles on each participating class.

The child's destruction removes it from every parent relationship, owning or weak. Destroying a child does not destroy its parents.

### Single-child relationships

`Single` is the representation for a maintained relation with at most one child. The child endpoint is unbracketed:

```lyric
relation Single Server:connection owns Connection:server
```

The parent role exposes `server.connection.child: Connection?`; the child role exposes `connection.server.parent: Server?`. Both start null. Application code reads these endpoints but cannot independently assign the backing links.

`server.connection.set(child)` establishes both ends. Before mutation it requires that the parent's slot is empty and the child is detached through this child role. If either condition fails, it refuses and leaves both ends unchanged. In particular, setting an already-occupied slot is not a silent replacement or an idempotent success, even if it contains that same child. Other relationship roles remain independent: this rule does not prohibit multiple parents through distinct roles.

`server.connection.remove(child)` detaches both ends without destroying the child. It requires that this exact child occupies this parent's slot with the corresponding backlink; otherwise it refuses without mutation. It does not detach a child from some other parent as a side effect.

Both `set(child)` and `remove(child)` return `Result<unit>`. Success supplies unit. An occupied parent slot, an already-attached child role, or removal of the wrong membership returns a non-OK `FailedPrecondition` Status with a diagnostic string. Each refusal leaves both relationship ends unchanged. Callers handle the result explicitly or propagate it with `?`; these operations are not unchecked pointer assignments.

Generated destruction must establish the removal preconditions through the maintained relationship state. It must not ignore a failed removal and reclaim an object while stale memberships remain. An unexpected refusal during generated teardown is an invariant failure to handle under the checked-failure runtime contract, not ordinary successful cleanup.

A constructor establishes its required single-parent membership by calling the parent's `set(self)` operation and handling any refusal before successful return. Replacing a child is explicit: detach or destroy the old child, then attach the new child. These separate operations are not implicitly an atomic replacement transaction.

The `owns`/`refs` policy remains orthogonal to cardinality. Parent destruction through `owns` destroys the current child using its generated destructor; `refs` detaches it without destroying it. Child destruction removes its membership from the parent under the normal final-hook/destruction ordering. No new lifetime category, manual structural destructor, or unmaintained pointer exception is introduced.

### Multiple parents

```lyric
relation DoublyLinked Graph:nodes owns [Node:graph]
relation DoublyLinked Node:outgoing owns [Edge:source]
relation DoublyLinked Node:incoming owns [Edge:target]
```

An Edge has two owning Node roles. Destroying either endpoint destroys the edge; the edge's generated cleanup removes both memberships. The two endpoints may be the same node instance without becoming the same relationship role.

### Labels are scopes

```lyric
team.roster.append(player)
player.team.parent
```

A label is a compile-time scope on the class, not an allocated adapter object. Two relations between the same types remain distinct through their endpoint roles.

Unlabeled endpoints inject their members directly into the class namespace. Any actual name collision is an error. Same labels on different classes are legal; colliding labels on the same class are not.

A scope does not implicitly convert to its parent. `player.team.parent` is explicit. A scope is not a first-class value that can be assigned to a variable.

### Representation is independent of lifetime

The standard representations begin with Single for an unbracketed zero-or-one child endpoint, and ArrayList, DoublyLinked, and HashedList for bracketed many-child endpoints. Every representation supports both `owns` and `refs`. A representation/cardinality mismatch is a diagnostic, not permission to silently change cardinality.

ArrayList uses an array and child index. DoublyLinked uses links at both ends and on each child. HashedList adds a hash index. All maintain backlinks and provide their operations through the same relation mechanism.

Application code cannot mutate the injected backing storage independently of the relationship operations. Such a write would bypass the very invariant the declaration establishes.

## 7. Construction and mutation

Constructors are ordinary functions with checked postconditions. There is no `attach` clause or new construction language.

A constructor takes its owning parents and establishes their relationships before successful return:

```lyric
func Node.Node(self, graph: Graph, name: string) {
    self.name = name
    graph.nodes.append(self)
}
```

The example assumes a relation whose append cannot fail after its preconditions are checked. A fallible relation operation must return or propagate its error; ignoring it cannot satisfy the constructor's postcondition.

```lyric
func Edge.Edge(self, source: Node, target: Node) {
    source.outgoing.append(self)
    target.incoming.append(self)
}
```

The constructor body may initialize fields before establishing relationships. The requirement is that its returned object has all required state and both relationship ends established. A caller must not perform an additional attachment step.

A constructor can express an optional parent with a nullable parent argument. Its body must consistently establish the relationship when the argument is present. This is a constructor contract, not a blanket rule that every instance of a class must always have every possible parent.

### Fallible construction

A fallible construction expression has type `Result<ClassName>` (with the class's concrete generic arguments when applicable): successful construction supplies `Ok(object)`; failed construction cleans up the partial object and supplies `Err(problem)`, with no usable object payload. For example, `let connection = Connection(server, address)?` extracts the successfully constructed Connection or propagates failure through Section 14's Result rule.

The constructor body's success status is distinct from the construction expression's object result. A fallible constructor body declares `Result<unit>`:

```lyric
class Connection {
    func Connection(self, owner: Server) -> Result<unit> {
        owner.connection.set(self)?
        return ()
    }
}
```

Here the example uses the fallible Single.set operation with a Result<unit> contract. The body reports success, not `self`. `return ()`, bare `return`, or reaching the end supplies unit success. Only after checking definite initialization and required parent attachment does successful construction produce `Ok(object)` at the call site. Failure propagates Err after the partial-object cleanup below. An infallible constructor body returns unit and its construction expression supplies the object directly. The earlier `error?`/null-success constructor proposal is superseded, not an alternate propagation mechanism.

The object is safe for generated destruction immediately after allocation. Relationship links start detached, reference fields start null, and containers start empty. Internal cleanup-safe defaults are not permission for application code to read a required field before initialization. Every completed initialization step leaves valid state for generated cleanup.

Returning an error, including propagation by `?`, automatically invokes that generated destructor. It detaches any established parent memberships, destroys already-attached owned children, releases stored value references, and frees the partial object's storage. It does **not** call the failed object's `final` hook. Fully constructed children destroyed during this cleanup run their own final hooks normally.

No separate rollback ledger or second destructor algorithm is needed. External effects not represented by initialized fields or relationships are not magically undone. Constructors must not publish a usable reference to a partial object or leave unowned work running after failure.

The compiler checks definite initialization and parent attachment on successful paths, rejects escape of an incompletely initialized object, and inserts the specified generated destruction on error paths. Construction-complete status determines whether final runs; it is not a per-field initialization ledger.

Parent add/remove operations update both ends. Invalid operations either fail before mutation or follow a documented infallible precondition. Removing an object from a relation does not, by itself, mean destroying it. Destruction and detachment are distinct operations.

## 8. Generated destruction and `final`

Application programmers do not write structural destructors. The compiler generates them from fields and relations. The destruction order is:

1. If construction completed successfully, call the object's `final` hook.
2. Remove the object from **all** parent relations, owning and weak, through the two-way remove operations. Those operations also clear the object's parent backlinks.
3. Destroy the object's strongly owned children.
4. Detach weak child relationships without destroying those children.
5. Release remaining fields and reclaim the object's storage.

Failed construction uses steps 2–5 and skips its own final hook. Parent code invoking a child's destruction must not detach that child first merely to simplify iteration: the child's final hook precedes the child's own structural teardown.

```lyric
class Connection {
    fd: i32
    final func close_resource(self) {
        close_fd(self.fd)
    }
}
```

A class has at most one final hook. Its object and fields remain allocated during the hook. Its own relationship teardown has not yet begun, but other objects reachable from it may already be partway through an enclosing cascade. The hook must obey ordinary reference-validity rules; it cannot assume every ancestor still satisfies a fully attached invariant.

Removing incoming relationships before destroying children prevents those maintained links from returning to a freed parent during the cascade. It does not erase temporary local references: static destruction analysis invalidates those. The implementation reexamines current child membership after recursive destruction, rather than preserving a stale list of pointers across callbacks or cascades.

Multiple parents, self-relations, and cyclic ownership must use these complete two-way operations. Each object must be reclaimed at most once. The ordering is decided; it still requires adversarial conformance cases rather than being declared correct merely from prose.

### Final-hook return and failure contract

A final hook returns `unit`; a declared Result or other return type is rejected. Bare return or normal completion ends the hook and continues generated structural destruction. The hook cannot veto destruction or return a recoverable failure to a cascade caller or counted-release site.

Fallible resource operations whose failures require caller decisions, such as flush or close, should be explicit ordinary methods returning `Result<unit>`. Callers invoke and handle them while the object is live. A final hook performs remaining cleanup under the resource's documented state contract; it does not retroactively turn an unsuccessful explicit operation into success.

A final hook may call fallible functions, but must handle their results locally. `?` cannot propagate from the hook because its return type is unit. Handling may report an operational failure as appropriate for the resource contract; an invariant violation invokes the checked-failure behavior. No generic promise of successful external I/O follows from finalization. Every normally returning hook proceeds to structural teardown, and checked process termination has the previously specified lack of cleanup guarantees.

### Final-hook effects and reentrancy

Final hooks may call ordinary functions. Their transitive destruction, mutation, and lock requirements are part of the generated destructor's requirements. The language does not impose the previous blanket prohibition on graph effects.

While a final hook for class C is executing, it may not call an operation whose transitive effects can destroy any instance of C. Such a call is a compile-time error, even if the programmer believes it would destroy a different instance. This deliberately uses the existing conservative class-wide destruction analysis rather than requiring instance-sensitive proof of reentrancy.

The rule includes direct destroy calls, indirect helper calls, callbacks, ownership cascades, other final hooks, and generated counted releases/cleanup that may destroy C during the hook. Infer effects through recursive call groups and generic specializations. Opaque boundaries require conservative contracts; unknown effects cannot be treated as harmless. Deferring a release cannot authorize a C finalizer to execute inside a C final hook.

The generated destructor's ordinary structural cascade is distinct from user final-hook execution. After the hook returns, generated teardown still detaches the current object from all parents before destroying its strong children, including same-class children where permitted by the maintained-graph rules. The prohibition does not accidentally ban every recursive same-class ownership structure. Generated cascade correctness and alias invalidation still require their specified checks.

A hook cannot publish a new usable alias to an object whose lifetime is ending. Destruction-in-progress state is established before invoking the hook, so any reentrant attempt reaching generated destruction is a checked invariant failure, never a second final call, double reclamation, or silently successful no-op. This defensive runtime guard does not replace the compile-time class-wide prohibition or justify accepting a program with unproved reference safety.

Class locks required by any cascade or final hook must be acquired before the destruction operation begins, as specified in Section 19.

## 9. Declared lifetime policies

### Ordinary classes

An ordinary `class` has explicit lifetime. Its generated `destroy()` may be called directly or through an owning relation's cascade. Holding a temporary reference does not prevent destruction. The static invalidation rules protect those references, and maintained relations protect persistent component links.

An ordinary class may be independently created or attached to owning parents. No `root` modifier is needed. Detachment does not free the object automatically or convert it to reference counting. Independent allocation still needs eventual explicit destruction; retaining an object indefinitely is a leak concern distinct from dangling-reference safety.

### Value classes

A `value class` is reference-counted and may be mutable. Copying an owning reference retains it; overwriting or dropping that reference releases it. Once no use or retention requires it to remain alive, it becomes eligible for automatic destruction. Destruction invokes its final hook and generated cleanup before freeing storage; the timing is specified below.

Ordinary code cannot call `destroy()` on a value-class instance. It releases references instead. This prevents a forced deletion from leaving other counted aliases pointing at freed storage and later attempting to release it again. Compiler-generated destruction at count zero is not a user-callable escape hatch.

The modifier does not require deep-copy assignment or immutable contents. Value-like vectors, matrices, and symbols need not record each holder as a parent merely for memory reclamation. Their chosen sharing/copy operations must still be documented.

A direct owning relation cannot forcibly cascade-delete a retained value-class child. A container holding values retains/releases them; that operation is not reinterpreted as explicit destruction. How relation-library APIs expose counted value collections should follow this distinction, without silently selecting a different lifetime policy.

### Generated releases are observable lifetime operations

When an array or field releases a value-class object, destruction at count zero has the same transitive effects as any other destruction. Those effects include its final hook and any owned-resource cleanup. The compiler cannot treat release as harmless merely because it was generated.

### Value-finalizer timing

A value object is destroyed when it is no longer used, but not necessarily at the earliest possible point. Eligible objects are eventually destroyed during continuing normal runtime execution; the language provides no exact scheduling or relative-order guarantee. This freedom does not permit a runtime to retain every eligible object indefinitely.

When destruction occurs, its final hook is invoked exactly once if construction succeeded, while the object remains valid, and before storage is reclaimed. Diagnostic output from value finalizers may therefore vary between builds or optimization levels. That is unspecified timing, not undefined behavior.

Reference-count cycles may prevent eligibility. Abnormal termination does not guarantee cleanup. The exact orderly-process-shutdown guarantee remains to be specified; do not use value-finalizer timing as a substitute for explicit timely resource management.

Retain/release elision, borrowing, and moves are permitted when reference validity and observable values are preserved. They may change finalizer timing within this contract. An optimizer need not preserve one exact debugging-output order.

A deferred finalizer cannot run at an arbitrary point that bypasses static effects or class locking. Every point at which it may execute must account for its transitive destruction effects and hold all required locks. If the compiler/runtime cannot establish that, it must defer to a suitable boundary or reject the unsafe use. No concurrent background reclamation mechanism is implied by unspecified timing.

Reference-count cycles can retain memory. Leak freedom is not identical to memory safety, and a blanket ban on recursive types is not justified. No tracing collector is introduced to reclaim cycles.

### Both categories

Both have generated cleanup and may have a final hook. Neither changes lifetime policy when attached, detached, passed to a function, or stored in an array. Persistent links to ordinary component objects still require maintained relationships; placing an unchecked ordinary-class pointer inside a value class is not a safety exception.

## 10. Static destruction analysis

### Class-wide invalidation

If a function can destroy an object of class C, calling it invalidates previously acquired local references to C.

```lyric
let node = graph.nodes.first()
prune(graph)
println(node!.name)     // rejected if prune may destroy Node
```

This conservative rule does not need to prove which particular Node was destroyed. It is deliberately simpler than general alias analysis.

Effects include explicit destruction, ownership cascades, final hooks, and releases that may reach reference-count zero. Effects propagate through ordinary calls, recursive call groups, generic specialization, callbacks, and interface implementations.

The compiler infers these effects from bodies. It must include generated bodies and cleanup paths, not just user-written statements.

### No large effect language by default

This design does not require `pure`, `reads`, or `mutates` annotations on every operation. Track destruction and storage/iteration invalidation because they are needed for safety. Additional effect categories must earn their place.

For statically selected generic implementations, the compiler can analyze the actual specialized call graph. It does not need an explicit destruction annotation on every abstract getter.

Opaque boundaries are different. Foreign functions and runtime-selected callbacks must expose conservative effect contracts, or the compiler must reject operations whose safety depends on unknown effects. The spelling of these boundary annotations is secondary to the requirement that unknown effects never mean “none.”

### Track every held reference

Invalidation covers parameters, receivers, optionals, temporary expressions, references nested in local structs/tuples/enums, and captured borrows. Copying an invalid reference is not a way to restore it.

At a control-flow join, a reference is valid only if valid on all incoming paths. Loops and recursion need fixed-point analysis. A returned reference must still be valid at the return point.

Evaluation order is defined left to right. In `f(node, destructive_call())`, the first argument is held across evaluation of the second and is invalidated if the second destroys Node. Compiler-generated temporaries are subject to the same rule.

### Reacquire through surviving structure

```lyric
prune(graph)
let current = graph.nodes.first()
if let node = current {
    println(node.name)
}
```

This is valid if graph remains valid. Reacquisition navigates maintained relationships; it does not dereference the old node to rediscover its parent.

### Persistent references are a separate obligation

Local invalidation does not clear a stale pointer hidden in an unrelated object's field. Persistent component references therefore require maintained relations. Value-like counted references follow the counted lifetime rules.

Arrays of borrowed object references are not automatically forbidden. If they are local, their elements can participate in invalidation as a group. If they escape or persist, their references need maintained links or appropriate reference counting. Distinguish lifetime obligations from a proliferation of collection types.

## 11. Safe relationship iteration

Relationship iteration must remain correct when the current object is deleted:

```lyric
for node in graph.nodes {
    if unwanted(node) {
        node.destroy()
    }
}
```

A safe loop may destroy only the current object of the iterated class. It may not destroy another object of that class through an ordinary call, a cascade, a final hook, or a reference-count release.

After deletion, the iteration variable is invalid. Other existing references of that class are invalidated as usual. The compiler-controlled continuation is protected by the narrower current-object rule, not by pretending ordinary references remain valid.

The checker must infer when a helper destroys only a specified argument. A helper that deletes the current item should not require inlining by hand. An opaque helper must provide equivalent evidence through its contract.

Representation-specific continuation matters:

- A doubly linked traversal can save the successor before deleting the current item, provided no permitted operation destroys or invalidates that successor.
- Swap-removal arrays must visit the element moved into the removed slot.
- Hashed collections need a continuation contract that survives the permitted removal.

Deleting the current Node is not safe if it cascades to another Node. Deleting incident Edges may be safe because those have a different class, provided their cleanup cannot destroy another Node.

Insertion, reparenting, and reordering require their own iterator-validity rules. Initially, reject mutations not covered by the representation's safe traversal contract. Do not infer that a deletion rule permits arbitrary collection mutation.

Nested loops must satisfy every active loop's obligations.

## 12. Arrays and array references

### Elements and resizing

All indexing is bounds checked by default. Growing an array initializes new elements; new reference slots are null. Shrinking releases removed elements, decrementing reference counts where applicable. Any resulting destruction contributes its effects.

Element types and initialization must agree: an array cannot expose null as a valid nonnull element. Nondefaultable elements need explicit initialization before access.

### Assignment has value semantics

Ordinary array assignment copies the array value at every array level. Mutating the destination's elements or length, including nested arrays, does not change the source array value:

```lyric
let original = [1, 2]
var working = original
working[0] = 9
assert_eq(original[0], 1)
assert_eq(working[0], 9)
```

For an array of class references, copying copies the references, not the referenced objects. Replacing a destination slot leaves the source slot unchanged; mutating an object referenced by both arrays is visible through both references. Copies of counted references retain their objects; ordinary-class references remain subject to destruction invalidation.

An array property belongs to its containing object. Different objects do not share a mutable array property or mutable backing allocation; assignment creates an independent array value, recursively through nested arrays. Explicit references into an array do not change its owner.

```lyric
let original = [[1, 2], [3, 4]]
var working = original
working[0][0] = 9
assert_eq(original[0][0], 1)
```

This is a semantic copy, not a demand for avoidable allocations. Moves and copy elision are allowed when they preserve independence and lifetime behavior. This initial concurrency design does not rely on shared copy-on-write backing between object owners; that would require an additional synchronization contract and is not silently assumed.

`let` prevents mutation of its array value, including nested array elements and length. `var` permits it. Writable reference access requires mutable storage and cannot bypass `let`. Ordinary parameters follow assignment's value semantics; explicit reference parameters borrow the caller's storage, as specified below. Ordinary slices produce independent array values; explicit reference slices borrow fixed-length windows.

### Ordinary value parameters

An ordinary parameter is an immutable local binding, like `let`. Argument evaluation follows the ordinary left-to-right rule. Passing a value has the same semantics as assigning that value: arrays are independent at every nested array level, structs copy their values, and class-reference elements copy references rather than clone objects. Counted references retain/release as required; ordinary object references remain subject to lifetime checking.

To modify a local array or struct value, create a mutable local copy with `var`. To modify the caller's storage, declare a `ref` parameter and supply an explicit reference argument. An ordinary class-reference parameter does not freeze its referenced object; methods may mutate that object under the usual lifetime and locking rules.

```lyric
func changed_copy(items: [i32]) -> [i32] {
    var working = items
    working[0] = 99
    return working
}

func change_original(items: ref [i32]) {
    items[0] = 99
}

var numbers = [1, 2]
let changed = changed_copy(numbers)
assert_eq(numbers[0], 1)
assert_eq(changed[0], 99)
change_original(ref numbers)
assert_eq(numbers[0], 99)
```

Assigning to `items[0]` inside `changed_copy` is rejected because its ordinary parameter binding is immutable. Passing `ref` to immutable array storage is also rejected. Passing an array containing class references does not prevent mutation of the shared objects; replacing an array slot and mutating its referenced object are different operations.

These are value semantics, not a requirement to allocate and copy at every call. The compiler may borrow backing storage, move values, or eliminate copies when it proves the observable values, mutation permissions, reference validity, and permitted cleanup behavior are preserved. Ordinary array parameters do not inherit the original specification's shared-mutable-backing exception.

### Slices and borrowed ranges

An ordinary array slice `array[low:high]` produces an independent array value, recursively through nested array elements. Class-reference elements copy references, not objects. Missing low means zero; missing high means the array's length when the expression is evaluated. Bounds are half-open, and the evaluated endpoints must satisfy `0 <= low <= high <= length`. An empty range is valid. Invalid ranges produce a checked bounds failure.

```lyric
var numbers = [10, 20, 30, 40]
var middle = numbers[1:3]
middle[0] = 99
assert_eq(numbers[1], 20)

let window = ref numbers[1:3]
window[0] = 99
assert_eq(numbers[1], 99)
```

An explicit `ref array[low:high]` borrows a fixed-length window of existing mutable storage. Evaluate the root/path and endpoints once, left to right, and retain the root path, starting offset, and length rather than an interior address. The endpoints do not change when the original array's length changes. Element accesses resolve current storage and check bounds; a resize cannot leave a dangling pointer. Access outside the window or to an element whose underlying index no longer exists produces a checked bounds failure.

A borrowed window permits element reads and writes but cannot resize the source through the window. Operations that change the window's length, such as push, pop, clear, or length-changing replacement, are rejected. Resizing the original array through another permitted access does not retarget or extend the window. Owner-lifetime checks, nonescape rules, and continuous class-lock requirements apply just as for other borrowed references.

The window uses the `ref` mechanism, not a new user-visible view category. Its fixed-length permission remains enforced through helper calls using the inferred requirements below. Replacement follows the path-identity rules below.

### Inferred resize requirements at function boundaries

Whole-array references and borrowed windows use the same source spelling, `ref [T]`. The compiler infers whether a function may resize storage denoted by each reference parameter. Element reads and writes alone require no resize permission. Operations such as push, pop, clear, or whole-array replacement that may change length require it. A whole-window assignment remains equal-length under the preceding window contract and does not grant resize permission.

```lyric
func fill(items: ref [i32], value: i32) {
    for item, index in items {
        items[index] = value
    }
}

func grow(items: ref [i32]) {
    items.push(0)
}

var numbers = [1, 2, 3, 4]
let window = ref numbers[1:3]
fill(ref window, 9)  // accepted: element writes only
grow(ref numbers)   // accepted: resizable whole-array storage
// grow(ref window) // rejected: requires resize permission
```

Requirements propagate transitively through calls, with actual reference arguments mapped to the storage paths they denote. Recursive call groups require fixed-point analysis; generic specialization and callbacks must not hide a resize requirement. The requirement belongs to the affected parameter/path, not indiscriminately to every array argument. Resizing an independently owned local copy does not require permission to resize the caller's storage. Nested-array operations must identify the affected level; permission to write window elements does not mean permission to change the window's own length.

At a call, the compiler checks that each actual argument supplies the inferred permissions. An ordinary mutable whole-array reference can supply resize permission; a fixed-length borrowed window cannot. No additional source keyword or separate slice type is required for ordinary functions. For external or otherwise opaque code, the conservative reference/resize requirements must be explicitly declared; unknown effects are not assumed harmless. The spelling of opaque boundary contracts remains part of the foreign-interface work.

These requirements complement rather than replace lifetime analysis, bounds checks, and locking. A helper that does not resize may still destroy an owner through another argument, and must be checked for that effect.

### Whole-window assignment

Assignment to a borrowed window copies an equal-length array value into its existing slots without resizing or retargeting the window. The copy is overlap-safe: it has `memmove`, not `memcpy`, semantics for plain elements.

```lyric
var numbers = [1, 2, 3, 4]
let destination = ref numbers[1:4]
destination = numbers[0:3]
assert_eq(numbers, [1, 1, 2, 3])
```

Evaluate the destination root/path and endpoints once, then evaluate the right-hand array value before writing destination elements. After right-hand evaluation, resolve and check the destination's current bounds and require equal lengths. A bounds or length failure occurs before the assignment changes any destination element. Effects already performed while evaluating the right-hand expression are not rolled back. If those effects invalidate the destination's owner, the ordinary static lifetime rules reject the access.

The result must be as if the right-hand value were preserved before destination writes, including when source and destination overlap. No temporary array allocation is required when an overlap-safe copy or another optimization preserves that behavior. Nested arrays retain independent value semantics; class-reference elements copy references rather than objects. Counted elements require correct retains/releases and their destruction effects, so raw bytewise `memmove` is not sufficient for every element type. This rule specifies overlap and pre-write bounds/length checks, not a general transaction or rollback guarantee for arbitrary finalizer effects.

Ordinary slice values may be moved or have copies elided when value independence and lifetime behavior are preserved. No mutable sharing is implied by ordinary slice syntax.

### Object reference versus slot reference

A reference to an object stored in an array differs from a reference to the slot holding it. Resizing may move the slot without moving or destroying that object.

A safe array reference stores:

1. The root array reference.
2. The indices from that root to the selected slot.

For a multidimensional array, the path includes every level. Resolving the reference follows current storage and bounds-checks each index. It never follows an address into an obsolete allocation.

The source type is `ref T`, not a separate user-visible `arrayref<T>` category. For example:

```lyric
var matrix = [[1, 2], [3, 4]]
let slot = ref matrix[0][1]

func increment(value: ref i32) {
    value += 1
}

increment(ref matrix[0][1])
```

The reference expression is explicit. The compiler chooses an ordinary stable-address or root-and-indices representation from the storage it denotes.

### Named references are fixed aliases

A named reference denotes fixed existing storage. Reading it in a value context reads the referenced value; assignment and compound assignment write through to that storage. A `let` reference binding fixes the storage it denotes, not the contents of mutable source storage. It cannot grant write permission to immutable storage.

```lyric
var count = 1
let slot = ref count
slot = 2
let snapshot = slot
let alias = ref slot
alias += 1
assert_eq(count, 3)
assert_eq(snapshot, 2)
```

Here `snapshot` is an ordinary value copy. `ref slot` aliases the same storage as `slot`; it does not create a reference-to-reference. Forwarding that alias to a reference parameter uses the ordinary explicit argument form, such as `increment(ref slot)`. The alias carries the original storage provenance, permissions, and lifetime obligations; creating it does not revive an invalid reference.

The initial language has no rebindable reference variables. A reference local is declared with `let`, not `var`; `var slot: ref T` and inferred `var slot = ref storage` are rejected. Reference parameters are fixed aliases too. Assignment never retargets an existing reference binding, and a reference expression is not accepted as a retargeting assignment. To denote another location, introduce another binding. In a value context, ordinary reference reads still copy according to the referenced type's value semantics.

For an array reference, fixed storage identity means the recorded root and index path, not a cached address or the identity of an object currently stored at that path. Relocation, bounds checks, replacement permissions, and owner invalidation continue to follow the rules below.

The root's lifetime is checked, not automatically extended by inventing a new heap owner. References to local array storage cannot outlive that storage. References to object-owned arrays require the object to remain alive and, in concurrent code, its class lock to remain held. Global roots have program lifetime but still require protection against concurrent mutation.

### Path identity across replacement and resizing

An array reference denotes its recorded root storage and index path, not the identity of the value originally occupying that path. Replacing the root array's value or an intermediate array preserves the reference's path and permissions, provided the root storage remains valid. Each access resolves the current arrays and checks every index.

```lyric
var matrix = [[1, 2], [3, 4]]
let slot = ref matrix[0][1]
matrix[0] = [10, 20]
slot = 99
assert_eq(matrix[0][1], 99)
```

If shrinking or replacement removes an index from the path, an attempted access produces a checked bounds failure. The path reference itself is not permanently invalidated by that change: if a later permitted resize or replacement makes the indices exist again, access denotes the current slot. This does not imply recovery from a bounds failure; code can restore the path before attempting access. Sorting, insertion, and removal may change which value occupies an index. A reference follows that index, not the moved value.

The same rule applies to a borrowed range's fixed root path, offset, and length. Replacement does not retarget or resize the window; accesses resolve its current underlying slots and check bounds.

Destroying the owning object is different from replacing its array value. Owner destruction statically invalidates the reference; reuse of an allocation slot cannot revive it. Lock release likewise ends an affected shared-object borrow under Section 19. Replacement operations may release counted elements or trigger other destruction effects: those effects still invalidate any affected object references. Preserving an array path never exempts its owner or a separately acquired element-object reference from lifetime checks.

### References into optional and tagged payloads

References into replaceable value payloads use the same root-and-checked-path model as array references. An optional unwrap in the path requires a present payload; an enum or tagged-union payload step requires the expected alternative. Check these conditions when acquiring the reference and on subsequent access unless intervening effects prove the checks redundant. A missing payload or mismatched alternative causes a checked failure, never access to inactive storage.

```lyric
struct Point {
    x: i32
    y: i32
}

var point: Point? = Point { x: 1, y: 2 }
let x = ref point!.x
point = null
// Access through x here would produce a checked failure.
point = Point { x: 10, y: 20 }
x = 99
assert_eq(point!.x, 99)
```

The reference denotes the current value at the recorded path, not the payload that occupied it when the reference was created. Replacing a present optional or restoring the expected tagged alternative therefore makes the corresponding current payload accessible through the path. An intervening absence does not permanently invalidate the path itself. This does not require or imply recovery after a checked failure: the program may restore a valid payload before accessing it.

Array indices, struct fields, and optional/tagged-payload steps may compose in one path. Every step must be valid for the current storage on access. Mutable access still requires mutable source storage; owner lifetime, nonescape, transitive destruction effects, and locking rules continue to apply. Compiler optimizations may cache resolved addresses or remove checks only when relevant intervening changes are ruled out.

A path through value storage is distinct from acquiring a reference to a separate class object. Copying a class-reference value selects that object; it does not follow later replacement of the slot that held it. Likewise a reference into that object's fields depends on that object's lifetime. Replacing an array, optional, or union slot containing a class reference does not silently retarget already-acquired object references. Destruction invalidates affected object borrows even if a new object later occupies the same slot or has the same tag.

This defines validity for tagged-payload references; the concrete pattern syntax for explicitly borrowing an enum/union payload still belongs in the pattern grammar. Ordinary value-pattern bindings must not silently become writable aliases.

### Reference parameters and escape

```lyric
func increment(value: ref u32) {
    value += 1
}

var count: u32 = 0
increment(ref count)
```

A ref parameter borrows existing storage. A function cannot assume that storage is global or heap-owned; it treats the reference as potentially stack-backed. It cannot return that reference or store it in a persistent location. Passing it to a helper is allowed only when the helper preserves the same nonescape obligation.

In a value context, reading through a reference produces a value. Returning that value or assigning it to a global value slot copies according to ordinary type semantics; it does not make a stack pointer escape. In a reference context, an escaping borrowed reference is rejected rather than silently allocating replacement storage to satisfy a `ref T` result.

```lyric
func copy_value(value: ref u32) -> u32 {
    return value
}

// Rejected: the caller may have passed a reference to its stack.
func escape_reference(value: ref u32) -> ref u32 {
    return value
}
```

An array value copied out of a scope has independent value semantics; the implementation may move storage when safe. Copying a class-reference value does not clone its object and does not waive that object's lifetime rules.

### Storage categories

- **Stack:** the defining activation remains alive during a synchronous call. No reference into it may escape that lifetime. Same-thread replacement/destruction of a containing value can still invalidate an interior reference and must be analyzed.
- **Object-owned:** the object must outlive the reference. Calls that may destroy its class invalidate held references; no later access is allowed without reacquisition. Class locking prevents concurrent deletion, not same-thread invalidation.
- **Global:** the slot lives for the program's lifetime. Objects or arrays reached through its contents still have their own destruction and relocation rules. A global slot is not a guarantee that its current target lives forever.

For a reference into an object, invalidation follows the **owner's** class, not the scalar type of the field. A `ref u32` into a Node becomes invalid when Node may be destroyed. This provenance is compiler metadata even though the source type remains `ref u32`.

### Optimization

When intervening code cannot resize or replace any relevant array along the path, the compiler can cache the resolved address and eliminate repeated traversal. Bounds checks can be removed when proven redundant.

An explicit unsafe benchmarking flag may disable bounds checks. Such builds are outside the safe-language guarantee and must be identified as unsafe in benchmark results.

## 13. Functions, closures, and generators

Ordinary functions, external methods, static functions, and generic functions use the same `func` mechanism. Instance methods declare `self`; static functions do not.

Method calls use parentheses. Direct field access does not implicitly execute an arbitrary zero-argument action. A field can still satisfy an abstract getter requirement when the compiler binds an interface; that does not require universal property-call syntax.

Generic call arguments may be inferred or supplied explicitly when needed. Unlike the previous draft, this version does not impose a special ban on explicit type arguments in method calls. If `object.method<T>()` is unambiguous and useful, the same generic rules should apply. No additional exception is needed solely because the call has a receiver.

### Nonescaping closures

The initial closure model uses a concrete compiler-generated environment type for each closure expression and an ordinary function implementing its body. Nonescaping closures may reference caller storage. Their environments can live on the stack or be eliminated by optimization; heap allocation is not required merely because a closure captures variables.

```lyric
let offset = 10
let add_offset = (x: i32) -> i32 {
    return x + offset
}
```

A statically known closure call is specialized for that concrete environment and body. It may become a direct call or be inlined. This is an opportunity for zero additional overhead, not a guarantee that every closure is free or every call is inlined.

The compiler must prove nonescape through helper calls: a borrowed closure cannot be returned or stored where it can outlive its captured storage. Capture does not freeze a referenced object or prevent destruction. Reference validity, mutation permissions, and transitive destruction effects apply to captures exactly as to other held references. Detailed capture-selection syntax remains to be specified; no ownership guarantee is inferred merely from the surface callable type.

Escaping closures are deferred. Unsupported escaping uses are rejected, not silently heap-boxed with unspecified lifetime semantics. Ordinary `return` inside an ordinary closure returns from that closure; the special consumer-loop behavior below does not change ordinary function semantics.

### Loop-only generators

A generator is consumed directly by a `for` loop. It is not an independently resumable object and cannot be stored, returned as suspended state, or manually advanced. Its body contains exactly one syntactic `yield` statement. That statement may execute repeatedly inside loops, or not execute on a particular path.

```lyric
func range(start: i32, end: i32) -> gen i32 {
    var i = start
    while i < end {
        yield i
        i += 1
    }
}

for number in range(0, 10) {
    println(number)
}
```

The generator is expanded at the consuming loop. At `yield`, the yielded expression is evaluated and bound to the consumer's iteration variable, then the consumer body runs. After that body completes normally, execution resumes immediately after `yield`. Generator arguments are evaluated once, in ordinary left-to-right order, before iteration; expansion must not duplicate their effects.

The compiler may implement this expansion by representing the consumer body as a statically known nonescaping closure, specializing the producer and consumer together, and inlining. No independently allocated suspended-generator frame is required. The old Duff's-device state-machine description is not the contract for this initial design.

### Consumer control flow

- A `continue` targeting the consuming loop finishes the current consumer body and resumes the generator immediately after `yield`. It does not skip producer code such as the increment in `range`.
- A `break` targeting the consuming loop stops the entire generated iteration.
- A `return` in the consumer body returns from the enclosing consumer function, not from a synthetic callback.
- Control flow in nested consumer loops retains its normal lexical target. Producer-local `break`, `continue`, and `return` retain their producer targets; a producer return ends generation.

Every exit performs the required cleanup for the scopes it leaves. Ordinary callback substitution without preserving these control-flow targets is not a correct implementation.

### Safety and initial limits

Analyze producer, consumer, captures, and calls together under the same reference-validity and destruction rules as ordinary code. In particular, a consumer's destructive effects apply to producer references held across `yield`. A producer that uses a deleted iteration object after `yield` is rejected; an audited safe traversal must establish its continuation before permitting current-item deletion.

The single-yield restriction counts yield statements belonging to the generator body, not the number of values produced. Multiple yield sites and independently resumable generators are deferred extensions. The language does not promise general coroutine behavior from the `gen T` spelling.

## 14. Control flow and errors

Blocks introduce lexical scope. `if`, `while`, `for`, `match`, `break`, `continue`, and `return` have structured behavior.

`if let` conditionally extracts an optional or pattern. `let Pattern = expression else { ... }` requires a diverging else branch. Pattern recognition must use resolved declarations, not capitalization.

Optional access is explicit. `x!` checks for null and unwraps; unsafe implicit nullable dereference is not a language feature.

Fallible value-producing functions return the ordinary tagged type `Result<T>`:

```lyric
enum Result<T> {
    Ok(value: T)
    Err(status: Status)
}

func divide(a: i32, b: i32) -> Result<i32> {
    if b == 0 {
        return invalid_argument("division by zero")  // unambiguously Err
    }
    return a / b                             // unambiguously Ok
}
```

Explicit `Ok(value)` and `Err(problem)` remain available. The implicit returns above use the general expected-type tagged wrapping rule below, not Result-specific conversion magic. Only the active alternative has a payload; failure contains no dummy success value. `Result<unit>` represents success without additional information or failure with an error. `return ()` wraps as `Ok(())`; bare `return` and reaching the end also supply unit success for functions declared to return `Result<unit>`. Functions returning other Result success types must supply a success value or return/propagate an error; falling through is a diagnostic.

The postfix `?` operator evaluates its operand once. Its operand must have type `Result<T>`: `Ok(value)` yields T, while `Err(status)` returns `Err(status)` from the enclosing function, performing required cleanup. That enclosing function must return `Result<U>` for some U; success types may differ, but the propagated Status is unchanged. Propagation explicitly selects Err even for `Result<Status>`, where implicit wrapping would be ambiguous. Ordinary tuples, optional values, and bare Status values are not operands for this propagation rule.

### Result handling and explicit discard

Silently discarding a Result<T> is a compile-time error. An expression statement whose value is Result must propagate it with ?, return it, retain it for use, handle it, or explicitly discard it. A Result bound to a local but never used is also a diagnostic; merely introducing a name does not acknowledge the outcome.

```lyric
// save_file(path, data)             // rejected: Result ignored
save_file(path, data)?              // propagate failure
let result = save_file(path, data)  // must subsequently be used
let _ = try_optional_cleanup()      // intentional discard
```

The existing `let _ = expression` syntax explicitly discards a result and performs the ordinary cleanup of its value. Returning, storing, or passing a Result can transfer responsibility for handling it; it does not assert that the operation succeeded. No new discard keyword or per-function annotation is required for Result-returning operations.

Explicit discard is not proof of success and does not waive other checks. In particular, discarding a relation-attachment result cannot establish a constructor's required-parent postcondition. Generated structural destruction may not use explicit discard to bypass its invariant that memberships are removed before storage is reclaimed. The compiler diagnoses implicit omission, not the wisdom of every deliberate discard.

### Concrete Status values

Failures use one concrete `Status` value type, not an open-ended `error` interface or a separate error hierarchy for each subsystem. Status carries exactly a standard code and a human-readable diagnostic string. Code-specific error factories take a string; ordinary string formatting includes additional diagnostic context, for example `not_found(f"symbol '{name}' was not found in {filename}:{line}")`.

Status has no structured-payload or attachment API. Callers make recovery decisions from the standard code, never by parsing message text. Rare operations needing additional machine-readable information return it separately through an explicitly declared function result contract, using ordinary types. That contract must define when the additional information is available, including failure cases; it is not smuggled through a diagnostic string. This keeps ordinary errors small and uniform without forbidding richer results where genuinely needed.

### Standard status codes

The standard code set is closed: projects and libraries cannot add enum cases. Diagnostic context belongs in the formatted message; additional machine-readable return data belongs in the specific function's declared result, not in Status. The codes describe failure categories shared across subsystems, not the identity of the subsystem that failed.

- `Ok`: success, never a valid Err payload.
- `Cancelled`: the operation was cancelled.
- `Unknown`: a failure occurred but no more specific category is known.
- `InvalidArgument`: a supplied argument is invalid independently of the current system state; the caller should correct it.
- `DeadlineExceeded`: the operation did not complete within its deadline.
- `NotFound`: the requested entity does not exist.
- `AlreadyExists`: an entity the operation would create already exists.
- `PermissionDenied`: the caller lacks permission for the operation.
- `Unauthenticated`: valid authentication credentials are absent or invalid.
- `ResourceExhausted`: a required resource or quota is exhausted.
- `FailedPrecondition`: the current system state does not permit the operation; that state must change before a retry can succeed.
- `Aborted`: the operation was aborted, for example because of a concurrency conflict; recovery may require restarting the larger operation or transaction rather than retrying its last step.
- `OutOfRange`: the requested position or value is outside an operation's valid range; unlike an intrinsically invalid argument, changing the relevant state may make it valid.
- `Unimplemented`: the requested operation or capability is unsupported.
- `Internal`: an internal invariant or expectation failed.
- `Unavailable`: the service or resource is presently unavailable; retry may succeed later.
- `DataLoss`: unrecoverable data loss or corruption was detected.

These codes categorize outcomes, not automatic retry policy. Cancellation, deadline expiry, or unavailability does not prove that an operation performed no effects. A caller must use the operation's contract to decide whether retry is safe. Messages supply useful context without becoming machine-parsed control flow. Standard codes are not replaced by per-library error enums and conversion hierarchies.

The source names above are normative; numeric serialization values and foreign-code mappings require a separate interoperability contract. Choosing these categories does not assert Abseil ABI compatibility.

### Status representation and Result invariants

Status has safe value semantics. Failure details may use shared reference-counted backing storage internally; copying a Status does not require copying its diagnostic message. Success requires no error allocation. No exact byte size, allocation count for failures, or backend ABI is promised here. Shared backing must preserve logical value independence and ordinary lifetime/thread-safety guarantees; it is not mutable application state shared without protection.

`Result<T>` contains either a T or a non-OK Status. Every entry into Err, including implicit tagged wrapping, enforces this invariant. If the supplied status is statically known to be OK, compilation fails. Otherwise Err construction checks the code and produces a checked programming failure if it is OK; it never creates an invalid Result, silently supplies a missing T, or substitutes an Internal status. This check follows the runtime's checked-failure contract, not ordinary Result error propagation.

A factory guaranteed to return a non-OK status, such as `invalid_argument(message)`, permits elimination of the check when that guarantee is proved. Success construction needs no invalid-OK check. The small validation cost belongs on the failure-construction path; exact performance remains an implementation measurement, not a specification claim.

Explicit `Ok(status)` remains valid for a function successfully returning a Status as data, including an OK status. It does not enter the failure alternative.

Fallible operations without a success value use `Result<unit>`. Bare Status is not a second propagation protocol: `?` accepts Result only and propagates its failure status unchanged with required cleanup. This deliberately adopts the efficient concrete-status design direction associated with Abseil Status/StatusOr without claiming API or ABI compatibility. It supersedes the earlier proposal for arbitrary value-class implementations of an error interface.

### Unambiguous tagged wrapping

When an assignment, argument, or return supplies an expected enum or tagged-union type, an untagged value may be wrapped implicitly if exactly one alternative accepts its payload type. Enum inference applies to alternatives with one payload value; explicit variant construction remains available for every variant. A value already of the expected tagged type is passed normally, not wrapped again.

Match resolved payload types using ordinary type compatibility, including checked interface satisfaction. Do not search arbitrary conversion chains or use speculative numeric widening to choose a variant. Multiple matching alternatives are an ambiguity error; no match is a type error. Without an expected tagged type, do not search for an enum to invent as the expression's type. This rule only wraps values; it never implicitly unwraps a tagged value.

```lyric
enum Measurement {
    Width(value: i32)
    Height(value: i32)
}

// let ambiguous: Measurement = 10  // rejected: both variants match
let width: Measurement = Width(10)
```

Similarly a Status value returned as `Result<Status>` requires explicit `Ok(status)` or `Err(status)`; Err still requires non-OK status. Tagged unions use the same unique-alternative requirement. Implicit wrapping preserves the ordinary value-copy, ownership, and effect rules of the selected payload; it does not create a new lifetime policy.

Early returns and propagated Result failures execute required generated cleanup.

### Checked programming failures and stack traces

In the initial runtime, a checked programming failure reports a diagnostic and terminates the whole process with a nonzero exit status. This applies to failed bounds/payload checks, invalid Err(OK) construction, explicit panic, assertion failure, and detected teardown invariant failure. The invalid access or invalid Result construction must not occur. Such failures are not catchable exceptions and do not become ordinary Status returns.

The diagnostic includes the failure kind, a useful message, the source location of the failed check, and a stack trace of the calling chain. Diagnostic builds must retain the metadata needed to present Lyric function names and source locations. Optimized/inlined or stripped builds may have fewer recoverable frames or locations; report available frames and clearly indicate missing/truncated trace information rather than invent it. Stack-trace formatting and collection are diagnostic operations, not application stack unwinding. The default trace does not dump local variable values.

Reporting must not depend on successfully allocating large diagnostic objects or acquiring application class locks. If resource exhaustion or diagnostic-output failure prevents a complete trace, emit whatever minimal diagnostic is possible and terminate anyway. Failure to report must not authorize continuation. Exact backend unwinding/symbolization machinery is an implementation choice; source-level trace quality requires conformance checks, not just a native address dump called a complete trace.

There is no application cleanup guarantee on this abnormal path: no promised final hooks, lock-release sequence, or stack unwinding. Expected recoverable failures use Result<T> and ordinary cleanup instead. A failure in any thread terminates the process, not merely that thread while leaving damaged shared state running. Future recoverable-panic support would require an explicit new safety contract; C longjmp is not a substitute.

An assertion failure therefore terminates its test process. A test runner that continues after failure must isolate tests in separate processes and collect their diagnostics and exit statuses. It cannot skip cleanup and run another test in the same process.

## 15. Interfaces over cooperating types

A multi-class interface describes a contract among several participants. It is not an inheritance hierarchy.

A minimal method-based example is:

```lyric
interface Endpoint<E, N> {
    pub func E.target(self) -> N
}
```

It can be bound explicitly:

```lyric
impl Endpoint<Edge, Node> {
    E.target = Edge.target_node
}
```

A complete binding must supply every required operation, either explicitly, through an exact unambiguous match, or through a default body. Missing members are errors. Two applicable explicit implementations are ambiguous, not an invitation to pick the last one registered.

### Structural satisfaction

An interface can be satisfied without an explicit `impl` declaration. For the requested participant types and endpoint roles, resolve satisfaction as follows:

1. Use an applicable explicit implementation to select the intended bindings. Multiple applicable explicit implementations are an ambiguity error; declaration order does not decide.
2. In the absence of an explicit implementation, match requirements against accessible existing members and maintained relations by exact name, resolved type/signature, and endpoint role. Each required binding must be unambiguous. A field may satisfy a getter requirement under the ordinary field-binding rules, but a getter alone does not supply writable storage.
3. Use interface default bodies for otherwise unsupplied operations where the interface defines them. All requirements used by those defaults must themselves be satisfied, and all participant/role bindings must reach the specialized body.
4. Diagnose missing or ambiguous requirements. Do not guess by fuzzy names, implicit widening, declaration order, or receiver type alone.

```lyric
interface Named<T> {
    pub func T.name(self) -> string
}

class Widget {
    pub func name(self) -> string { return "widget" }
}

// Widget satisfies Named without a registration declaration.
// An optional checked assertion of the same conformance is:
impl Named<Widget> { }
```

An empty impl is a checked assertion of conformance using the same derivation/default rules, not mandatory registration or permission to accept missing members. Mixed impls supply selected explicit mappings and derive the remaining requirements. Explicit mappings can adapt different concrete names or choose particular relation roles; they do not waive type, visibility, lifetime, or effect checks.

Structural satisfaction inspects existing capabilities. It does not inject fields, create relationships, or change destruction policy merely because a generic algorithm is used. Such changes require an explicit relation or storage-providing implementation declaration. Signature satisfaction is not proof that an implementation preserves behavioral invariants such as both-end relationship maintenance.

### Interface-qualified calls and ambiguity

A short interface method call is accepted only when the receiver, explicit type arguments if supplied, ordinary arguments, and applicable constraints determine one binding. The receiver's type alone must not silently select the other participants. If multiple bindings remain, compilation fails with the candidate bindings and a suggested qualified call.

Interface qualification explicitly selects the contract and its participants:

```lyric
// Given bindings for both graph interpretations:
// DirectedGraph<Network, Person, Follow>
// DirectedGraph<Network, City, Road>

DirectedGraph<Network, Person, Follow>.edge_count(network)
DirectedGraph<Network, City, Road>.edge_count(network)
// network.edge_count() is ambiguous when both remain applicable.
```

For an instance method, the qualified call supplies its receiver as the first ordinary argument. Static interface operations have no receiver argument. Qualification selects a compile-time binding; it allocates no adapter and performs no runtime interface conversion. The selected implementation, all participant types, and all relation-role mappings accompany specialization and effect checking.

Qualification uses the same satisfaction rules as an unqualified call and can select a structural binding without an explicit impl. It does not bypass visibility, type, lifetime, or effect requirements. Conflicting explicit implementations of the same fully specified binding remain an error; spelling the interface explicitly does not resolve that conflict. Declaration order never breaks ties.

Default algorithms belong to the same interface contract and use its abstract operations. Their definitions can be separated from declarations like other functions. All participant bindings must reach the specialized body; the receiver's type alone is insufficient.

## 16. Abstract relations in interfaces

Use the redesign's abstract relation declarations rather than inventing a compiler-privileged Collection with invisible requirements:

```lyric
interface DirectedGraph<G, N, E> {
    relation Collection G:nodes [N:graph]
    relation Collection N:outgoing [E:source]
    relation Collection N:incoming [E:target]

    pub func G.edge_count(self) -> u64 {
        var count: u64 = 0
        for node in self.nodes {
            for edge in node.outgoing {
                count = count + 1
            }
        }
        return count
    }
}
```

This compact example includes its default body. A declaration-only file omits the body and the functional implementation supplies it.

`Collection` is the ordinary declared capability for traversing a maintained parent/children relationship:

```lyric
interface Collection<P, C> {
    pub func P.iter(self) -> gen C
    pub func P.count(self) -> i32
    pub func C.parent(self) -> P?
}
```

All three operations refer to the same bound endpoint roles, not merely matching parent and child types:

- `iter()` visits the children belonging to the receiver through that relationship. It is a loop-only generator under Section 13, not an independently resumable iterator object.
- `count()` returns the current number of children in that relationship. This contract does not promise constant-time counting.
- `parent()` returns the child's parent through that relationship, or null when detached. A maintained parent field may satisfy this getter under the ordinary binding rules.
- Traversal permits destruction of the current child under Section 11's safe-loop rules. The representation must preserve a valid continuation; signature matching alone does not prove this behavioral property. Other mutations require the representation's explicit traversal contract and are otherwise rejected.
- Traversal order is defined by the representation. An algorithm constrained only by Collection cannot assume insertion, sorted, or any other particular order.

The capability requires no append/remove operations and no particular storage layout. Both owning and weak relationships can satisfy it. Required lifetime and lock checks remain in force throughout access and traversal.

For a relation scope satisfying this capability, `for child in owner.children` invokes that scope's `iter()`, and `len(owner.children)` invokes its `count()`. These are compile-time scope operations; the scope itself remains non-first-class. They select the complete relation binding, including both endpoint roles. The compiler does not infer capabilities from the identifier `Collection` alone: satisfaction requires the visible declarations and their contract.

An abstract relation without `owns` or `refs` accepts either destruction policy when the algorithm only needs traversal. An algorithm requiring owning behavior declares that requirement explicitly.

Concrete bindings map whole relations:

```lyric
impl DirectedGraph<Net, Route, Via> {
    G:nodes/N:graph = Net:routes/Route:net
    N:outgoing/E:source = Route:a/Via:a_source
    N:incoming/E:target = Route:b/Via:b_target
}
```

Each line binds both endpoint roles together. It does not copy state or create a second relationship. This is the important semantic distinction: identical types with different roles are different bindings.

Per-member bindings remain available for nonrelational operations or adapters. There is no requirement to write five forwarding methods when one relationship binding already conveys the meaning.

### Composition

Retain interface defaults and single-parent `extends` from Lyric's later design. Composition substitutes the parent's type parameters and carries its requirements/defaults; it introduces no runtime object inheritance.

Do not add a separate constraint-alias mechanism initially. Functions can state the interface requirements they need. A naming shorthand can be added later if real examples show repeated combinations that are not well expressed by interfaces.

This deliberately chooses one principal composition mechanism rather than carrying both historical directions into the first implementation.

## 17. Relationship hints are interfaces

A storage hint is an interface that supplies endpoint fields, operations, and cleanup templates. There is no separate `hint interface` keyword.

```lyric
interface ArrayList<P, C> {
    field P.children: [C]
    field C.parent: P?
    field C.index: i32

    pub func P.append(self, child: C)
    pub func P.remove(self, child: C)
}
```

A relation binds both type variables and their labels. Members on P are injected into the parent role; members on C into the child role.

A parent-side append operation must be able to update the child's backlink through its child argument. The correct rule is that each access uses the right instance and the same relation binding, not a blanket prohibition on cross-side updates.

Ownership-sensitive cleanup templates distinguish `owns` from `refs`. Application classes still do not handwrite structural destructors.

The compiler checks hint shape, types, roles, and effects. That does not prove an arbitrary implementation maintains its data-structure invariants. Low-level representation code belongs to a small audited boundary. Merely spelling a function `trusted` must not grant arbitrary application code permission to bypass safety.

Capability requirements inspect an existing implementation. They do not silently add fields when an unrelated algorithm is called. Storage injection happens at the explicit relation or storage-implementation declaration.

## 18. Operators and ordinary library behavior

Keep one documented precedence table. Bitwise arithmetic belongs above comparisons, so `a & 1 == 0` means `(a & 1) == 0`. Logical operators short-circuit. Operands and call arguments evaluate left to right.

Compound assignment evaluates its destination once. If that destination is an array slot, lowering must not retain an unchecked address across a resizing operation in the right-hand side.

### Floating-point values and arithmetic

`f32` and `f64` use IEEE 754 binary32 and binary64 behavior respectively. Ordinary floating-point arithmetic does not use the integer-overflow failure rule: overflow produces appropriately signed infinity, invalid operations such as 0.0/0.0 produce NaN, and underflow follows IEEE rounding with subnormal values rather than silently flushing them to zero. Positive and negative zero compare equal.

NaN compares unequal to every value, including itself. Equality with NaN is false, inequality is true, and ordered comparisons involving NaN are false. These rules apply in safe and unsafe modes; disabling integer/bounds checks does not authorize different floating-point values.

The backend must not silently enable fast-math assumptions that discard NaNs, infinities, signed zero, or subnormal behavior. C is a suitable lowering language on conforming supported targets, but portable C alone does not promise these properties. A C backend must verify that its float/double representations match binary32/binary64 and select compiler/runtime settings preserving the specified behavior. Targets or configurations that cannot provide it must be rejected or use an explicitly specified conforming implementation, not silently change the language.

### Initial C behavior and preferred contraction implementation

Initially, Lyric may use the selected C compiler's ordinary floating-point contraction and intermediate-evaluation behavior on a supported IEEE target. The toolchain must record the compiler/version and relevant floating-point options so this behavior can be reproduced. It does not initially promise identical last-bit results across compilers, optimization settings, or targets. In particular, `a * b + c` may be fused when the selected C configuration permits it. This is an explicit initial implementation allowance, not a claim that separate and fused evaluation are numerically identical.

The preferred later implementation is controlled contraction: preserve separately written operations by disabling implicit multiply-add contraction, and expose explicit `fma(a, b, c)` for one-rounding fused semantics. A C backend can map binary32/binary64 operations to C's `fmaf`/`fma` facilities and use compiler options such as `-ffp-contract=off` for ordinary expressions. Hardware acceleration is target-dependent; a library implementation may provide the semantics. This is a preferred implementation direction, not a tested or required initial intrinsic/API.

The initial allowance does not authorize general fast-math assumptions that violate the NaN, infinity, signed-zero, or subnormal requirements above. Detailed rounding-mode exposure and excess-precision policy remain to be specified. No initial cross-target bit-identical evaluation or local floating-point lowering verification is claimed. Explicit floating-point/integer casts, implicit conversions, and mixed operations follow the rules below.

### Implicit floating-point conversions

`f32` converts implicitly to `f64`. Finite binary32 values are exactly representable in binary64; widening preserves infinities and signed zero and preserves NaN classification without promising preservation of a particular NaN payload encoding. Mixed f32/f64 arithmetic and comparisons widen the f32 operand to f64 and use f64 operations.

Conversions between integer and floating-point types require an explicit cast in either direction, even when a particular value or a particular source integer type could be represented exactly. Mixed integer/float arithmetic and comparisons are rejected until the programmer explicitly chooses a conversion. This is a simple category boundary, not an attempt to infer whether the current value happens to fit. Likewise f64-to-f32 narrowing requires an explicit cast.

```lyric
let wider: f64 = narrow_f32
let approximate = count as f64
let integer = measurement as i64
```

These examples specify when conversion must be explicit; the rounding and failure rules follow below. Ordinary literal contextual typing remains distinct from converting an already-typed integer variable. No implicit conversion changes writable reference or array-storage types.

### Explicit floating-point conversions

An explicit integer-to-float `as` conversion rounds to the destination floating-point precision. Low-order precision loss is permitted; exact representation is not required. Explicit f64-to-f32 conversion likewise rounds to f32, allowing sufficiently large finite magnitudes to become signed infinity and sufficiently small magnitudes to become subnormal or zero. Existing infinities and NaN classification are preserved where applicable; no particular NaN payload-bit mapping is promised. Detailed rounding-mode policy follows the floating-point environment contract rather than introducing a separate per-cast mode here.

A float-to-integer `as` conversion truncates toward zero, then requires that the resulting mathematical integer be representable in the destination type. For example, `3.9 as i32` is 3 and `-3.9 as i32` is -3. NaN, infinity, or an unrepresentable truncated result produces checked failure. A statically known invalid conversion is a compile-time diagnostic.

These float-to-integer checks remain enabled in unsafe mode. They are an explicit exception to the integer-overflow/bounds opt-out, like zero-divisor checks. `!as` is restricted to integer-to-integer conversion; it does not define floating-point wrapping, saturation, or reinterpretation.

The C backend must establish finiteness and the representable range of the truncated value before executing a float-to-integer cast. It may not cast first and inspect the result afterward, or rely on an out-of-range C conversion. Boundary tests must include destination extrema, nearby fractional values, NaN, infinities, and the fact that converting a large integer bound to a float can round that bound outward. A naive comparison against a rounded integer maximum is not sufficient. These are required lowering/conformance cases, not a claim that a float-cast probe has been run.

### Integer arithmetic and explicit wrapping

In safe builds, ordinary integer addition, subtraction, multiplication, and negation are checked against the result type's representable range. Statically known overflow is a compile-time error; dynamic overflow produces a checked programming failure under Section 14. Signed and unsigned arithmetic follow this rule. Debug and optimized safe builds have the same semantics. The compiler may remove checks proved redundant, but may not assume overflow cannot occur merely because the C backend would otherwise give it undefined behavior.

Intentional modular arithmetic uses wrapping operators rather than method-call ceremony:

```lyric
let total = a + b       // checked addition
let hash = a !+ b      // wrapping addition
let prior = a !- b     // wrapping subtraction
let mixed = a !* b     // wrapping multiplication
hash !+= contribution
prior !-= amount
mixed !*= factor
```

Wrapping arithmetic computes modulo 2^N for an N-bit integer type, interpreting the resulting bits in that type. `!+`, `!-`, and `!*` have the precedence and associativity of `+`, `-`, and `*` respectively. Their compound assignments evaluate the destination once and obey ordinary reference/lifetime rules. These explicit infix and assignment forms are distinct tokens; they do not change postfix optional unwrap.

Prefix `!-value` is wrapping integer negation in every mode. It retains the operand's type and width and computes zero minus the value modulo 2^N. The minimum signed value therefore negates to itself; for unsigned values it computes the modular additive inverse. Prefix `!-` has the precedence of ordinary unary minus. The parser distinguishes prefix negation from infix wrapping subtraction by position, just as for ordinary `-`. It is not logical-not applied to a negation and does not introduce a floating-point wrapping operation.

```lyric
let opposite = -value  // checked integer negation in safe mode
let wrapped = !-value  // modular negation in every mode
```

Numeric conversions, integer comparisons, shifts, division, and remainder are specified in this section.

Explicit unsafe mode disables automatic integer-overflow checks as well as the separately specified bounds-check opt-out. Ordinary integer addition, subtraction, multiplication, and negation then wrap modulo 2^N for an N-bit result type, with the resulting bits interpreted in that type. These operations have the same modular results as explicit wrapping arithmetic. Their result is defined even for signed overflow: the optimizer may not assume that overflow never occurs. The backend must implement these semantics deliberately rather than emit undefined signed C overflow and inherit its consequences.

Unchecked bounds access remains outside the safe-language guarantee, and unsafe execution must be identified in benchmark results. Defined arithmetic wraparound does not restore memory safety to unchecked indexing. Shifts normalize counts in unsafe mode as specified below; division and remainder retain zero-divisor checks. Explicit wrapping operators retain their defined modular meaning in every mode.

### Implicit integer conversions and mixed arithmetic

An already-typed integer value converts implicitly to another integer type only when the destination's representable range contains the source type's entire range. This is a type-level rule, not a value-dependent guess: u8 to u32, i16 to i64, and u32 to i64 are allowed; i32 to u32, u32 to i32, and i64 to i32 require explicit conversion. Transparent aliases retain their underlying type's rules.

Integer literals remain context-sensitive. A literal may take an expected integer type when its value is representable, so `let byte: u8 = 12` is valid. This does not authorize narrowing an already-typed variable merely because its current value happens to fit. Without a determining context, the existing i32 literal default applies.

For ordinary binary integer arithmetic, equal operand types retain that type. If the types differ and one operand's type can represent the entire range of the other, convert the latter to the former and perform the operation in that type. Otherwise reject the expression and require explicit conversion. The compiler does not invent a third wider type or let an assignment's destination silently widen an already-typed arithmetic operation.

For example, i64 plus u32 uses i64; i32 plus u32 is rejected even though both ranges fit in i64. To choose that calculation width, explicitly convert the operands to i64. Result overflow is checked or wrapped in the selected operation type under the arithmetic rules above. The wrapping addition/subtraction/multiplication forms use the same operand-type selection as their ordinary counterparts.

Shift counts are the specified exception: the count may have a different integer type from the left operand, and never changes the result width. Implicit integer/float conversion is not permitted, as specified above. Mixed integer comparisons and explicit integer casts are specified below. Numeric widening does not supply writable reference compatibility: a ref u8 cannot become ref u32, because that would describe different storage. Nor does this rule authorize element-wise conversion of array storage or numeric conversion searches to disambiguate tagged alternatives.

### Mixed integer comparisons

Integer `==`, `!=`, `<`, `<=`, `>`, and `>=` compare mathematical values across signedness and widths. The result is bool; no common numeric result type or explicit cast is required. This rule applies even when neither operand type contains the other's entire range, such as i64 versus u64. Safe and unsafe modes have identical comparison semantics.

A negative signed value is less than every unsigned value and is never equal to one. Nonnegative signed values compare with unsigned values by magnitude, without truncation or conversion to a type that cannot represent them. For example, signed -1 compares less than unsigned 1, never as a large positive integer.

Operands evaluate once, left to right, under ordinary lifetime/effect rules. The backend must implement value-correct comparison rather than inherit C's usual mixed signed/unsigned conversions. A sign test followed by a suitable magnitude comparison can handle the mixed case without requiring a wider integer type. This specifies behavior, not a tested lowering implementation. Floating-point comparisons and integer/float mixtures are separate contracts.

### Explicit integer conversions

`value as Type` converts an integer value to the named integer type. The right operand is a type expression, including a declared generic type parameter, not a value expression whose type is inferred as the target. Expression-derived targets and a typeof facility are not part of the initial language.

In safe mode, `as` preserves the mathematical value when it is representable in the destination type. A statically known out-of-range conversion is a compile-time error; a dynamic out-of-range conversion produces checked failure. In unsafe mode, integer `as` converts modulo 2^N for the N-bit destination and interprets the resulting bits in its signed or unsigned type.

`value !as Type` explicitly requests that same modulo-width conversion in every mode, including safe mode. It is appropriate for intentional truncation or signedness reinterpretation. It does not report overflow merely because bits are discarded. Widening a negative integer under modular conversion uses its mathematical value modulo the destination width, not zero-extension of the source bit pattern.

```lyric
let small = large as u8       // checked in safe mode
let low_byte = hash !as u8    // explicitly modulo 256
```

Evaluate the converted operand once. The target type expression is resolved statically and has no runtime evaluation. `as` and `!as` have the same cast precedence and association; `!as` is the wrapping-cast operator, not a postfix unwrap followed by a cast. Parenthesize an optional unwrap when needed, for example `(optional!) as u8`. No `!as` wrapping behavior is implied for floating-point, reference, or arbitrary class conversions.

The C backend must preserve the defined result for every accepted integer conversion without relying on implementation-defined out-of-range signed casts. Integer casts do not manufacture object references or bypass reference/lifetime checks. Generic casts are valid only when the actual constrained/specialized operand and target types support the operation.

### Integer shifts

Shifts manipulate fixed-width bit patterns rather than perform checked multiplication or division. For an N-bit left operand, the result has the same integer type and width:

- `<<` discards bits shifted beyond the width and fills low bits with zeros, for signed and unsigned operands. Discarded bits do not cause arithmetic-overflow failure.
- `>>` on unsigned operands fills high bits with zeros.
- `>>` on signed operands replicates the sign bit (arithmetic right shift).

The shift count must be an integer. Safe mode requires `0 <= count < N`; a statically known invalid count is a diagnostic, and a dynamic invalid count produces checked failure. Unsafe mode normalizes the count to the unique value in `[0, N)` congruent to it modulo N, including negative counts. The lowering explicitly performs this normalization rather than relying on target-specific instruction masking. Operand evaluation remains left to right and occurs once.

```lyric
// Division and arithmetic right shift intentionally differ for negatives:
assert_eq(-7 / 2, -3)
assert_eq(-7 >> 1, -4)
```

The backend must preserve these semantics without invoking C undefined behavior or depending on implementation-defined signed shifts. Left shift operates on an unsigned representation of the required width; small integer promotions must not accidentally turn it into overflowing signed arithmetic. Signed right shift must preserve sign extension explicitly where the backend's guarantees do not establish it. Reinterpretation of result bits must preserve the source signed type's defined representation, not rely on an unchecked out-of-range C conversion. Zero-count paths must not construct an auxiliary shift by N. These are lowering obligations, not evidence that a shift implementation has been tested.

### Integer division and remainder

Integer division truncates toward zero. A nonzero remainder has the dividend's sign: `-7 / 3` is -2 and `-7 % 3` is -1. Division or remainder by zero produces a checked programming failure in both safe and unsafe modes; the absence of a meaningful modular result makes this an explicit retained check in unsafe mode.

For signed minimum divided by -1, safe division produces checked overflow failure; unsafe division returns the signed minimum by modulo-width wraparound. The remainder for signed minimum and -1 is zero in either mode. These outcomes must be implemented before executing a backend instruction or C operation that has undefined behavior for the exceptional operands.

A C11 lowering can first reject a zero divisor, then handle the signed-minimum/-1 pair explicitly, and only then evaluate C `/` or `%`. Both C operators require this guard, not just division. The ordinary defined C cases provide truncation toward zero and the required remainder sign. The failure branches call the runtime's non-returning checked-failure diagnostic; they do not return a fake arithmetic value.

A standalone 64-bit C probe of this guarded arithmetic recipe passed 271 checks each under Apple Clang 21 on ARM64 at -O0, -O3, and -O3 with undefined-behavior sanitization. It checked failure classification rather than executing Lyric's not-yet-implemented fatal diagnostic. The local `gcc` executable is also Apple Clang, so this is not independent GNU GCC or cross-target verification. The test's wider-integer oracle is not required by the lowering recipe.

Overflow checks have a real potential cost, especially for signed arithmetic; no zero-cost claim is made. Safe default behavior and explicit wrapping/unsafe choices make that tradeoff visible rather than dependent on optimization level.

Strings remain byte-oriented for indexing and length. Unicode-aware iteration and transformations are separately specified library operations. A byte is not a Unicode character.

`Sym` interns strings and caches hashes. Hash collisions are resolved by actual key equality; interning does not make hash equality sufficient. `Dict<K,V>` requires consistent hashing and equality.

Library containers obey the same lifetime, effect, and relationship rules as user code. They are not exceptions that can hide stale references from the checker.

## 19. Thread safety and class locks

### One per-class lifetime and allocation lock

The agreed starting point is one reader–writer lock per class, aligned with its allocation storage. It protects object lifetimes and allocator state, not every shared payload mutation:

- **Shared access** permits obtaining and using references to existing objects of the class. Multiple threads may hold shared access simultaneously. None may create or destroy an instance of that class while holding only shared access.
- **Exclusive access** permits creating, using, and destroying objects of the class. It excludes other threads' shared and exclusive access for the duration of the protected region.

“Reader” describes lifetime access, not field immutability. Shared lifetime access may accompany field writes when separate data-synchronization rules permit them. It does not by itself make conflicting reads and writes safe.

Creation and destruction both require exclusive class access. Allocation/free-list operations, slot reuse, allocator metadata growth, and SoA backing-storage updates occur under that protection. Generated allocator helpers therefore need no additional per-class allocator mutex or recursive locking: they require the caller's exclusive access and perform ordinary allocator-state updates within it. Create/delete are thread-safe complete operations because protection is established around them, not because each internal helper locks independently. Construction failure cleanup obeys the same access requirements.

Different classes have distinct locks. Generic specializations with distinct allocation storage have distinct lock identities; if an implementation shares allocator storage, it must also share its protection. The same physical mutable allocator state must never be accessed under unrelated locks.

This intentionally serializes creation against all existing users of the class, even though stable addresses mean creation alone need not invalidate their references. A region creating Nodes needs exclusive Node access, just like a region destroying Nodes. The simplicity trades away allocation/read concurrency; no throughput or latency benefit is claimed without measurement. Separating allocator synchronization later would be a deliberate design revision, not an implicit second lock in this model.

### Payload and relationship synchronization is separate

Shared lifetime access prevents another thread from deleting an object; it does not prevent other shared holders from mutating it. Shared mutable fields, relationship links, and object-owned dynamic arrays need independently checked data-synchronization contracts, such as guarded-by/must-hold rules, confinement, or supported atomic operations. The exact guard syntax and allowed mechanisms remain to be specified. Unknown protection is not permission to accept a data race.

A relationship update must preserve both ends under the relevant data guards as well as hold the required endpoint lifetime access. Following a relationship to acquire a reference must protect the relationship read too. Dynamic array resolution and access must be protected against concurrent resize/replacement; root-and-index paths alone do not close a race between address resolution and use. Stable SoA property storage does not make ordinary owned-array backing stable.

Value classes follow the same lifetime/allocation modes. Concurrent counted retains/releases still require a safe count-update protocol: shared lifetime access alone does not serialize count changes. Any release that may run destruction requires exclusive access for the affected classes, or a specified deferred-finalization boundary that obtains it. Whether count updates use atomics or another checked protocol remains runtime design; no unsynchronized count updates or implicit shared-to-exclusive upgrade are authorized. An owning counted handle may preserve an object's lifetime across access regions, but it does not authorize unprotected mutable-payload access.

Thread-confined local scalar and array values need no class lifetime lock. Shared global mutable storage requires its own data guard. A global slot's lifetime does not guarantee the lifetime of the object it currently names. Object/class confinement-based lock elision requires proof covering actual shared allocator storage and lifetimes; it is not implied merely because two threads use different objects.

### Reference acquisition and use

Acquire at least shared class access **before** obtaining a borrowed reference to a shared object or object-owned storage. Reading a raw pointer first and locking afterward is unsafe: deletion and slot reuse could occur in between. Obtain references through a protected maintained relationship or another explicitly lifetime-safe source; root publication and acquisition contracts remain to be completed.

Lifetime protection remains continuously held across use of the borrow. Releasing it ends the validity of affected borrowed references, including references in aggregates, closures, and array paths. Reacquiring access does not resurrect them. Reacquire references through still-valid maintained state under the appropriate lifetime and data protection.

A reference passed into a helper carries its owner provenance and access requirements. Helpers reuse access already held rather than independently reacquiring the same lock. The checker must preserve these obligations through calls and specialization; opaque calls need conservative contracts. Exclusive access satisfies a shared requirement, but shared access cannot satisfy an exclusive requirement.

A region that may create or destroy instances must obtain exclusive access before those operations and before acquiring borrows it will retain across them. Do not upgrade shared access while retaining it: two upgrading readers could deadlock. Ending a shared region and starting an exclusive one is possible only after ending its borrows and then reacquiring/rechecking objects under the new protection; that gap is not an atomic upgrade.

The intended efficiency comes from amortizing protection across a region of work rather than locking at every field access or reference copy. Region boundaries and their source spelling remain to be finalized. Long-lived regions can delay creation/destruction, and class-wide exclusion affects unrelated objects of the same class. Writer progress and scheduling/fairness policy need an explicit implementation contract; a particular reader–writer lock's behavior must not be assumed.

### Stable class storage

Both AoS and SoA use slab allocation with a free list. Allocation removes a slot and deallocation returns it under exclusive class access. Generated helpers do not acquire an additional allocator mutex. Any shared runtime allocator used beneath class storage retains its own separate contract; the class lock does not purport to protect unrelated runtime allocations.

AoS object storage does not move during an object's lifetime. In SoA, each class property array has its own stable virtual-address region, reserved large enough to cover the full 32-bit class-index space. For an element stride S, reserve at least 2^32 times S bytes, with appropriate alignment and checked address/size calculations. A one-byte property reserves 4 GiB; an eight-byte property reserves 32 GiB. Growing the population must not move the property array's base address or copy it into a replacement allocation.

Virtual address reservation is distinct from physical backing. Use operating-system virtual-memory mapping facilities to reserve the region and provide physical backing as pages are needed. The property base and slot-index address remain stable as population grows. Exact reserve/commit/decommit calls, mapping flags, page initialization/publication, and resource-failure handling must be specified for supported targets; the design does not assume that every platform exposes the same mmap2 syscall. It requires an adequate virtual-address space, not a 32-bit process address space. Address-space exhaustion must be handled explicitly rather than silently falling back to relocating live property arrays.

The intended compilation/deployment scale is a program or shared library, with its class property reservations. Bill reports having tested this reservation strategy successfully; this document records that design input, not an independent measurement of a new Lyric runtime. Shared-library unload and allocator-region teardown must not release regions while live objects or accesses remain; exact module lifetime coordination remains to be specified.

Stable property arrays eliminate the need to re-read a moving SoA base solely because another object was allocated. They do not prevent slot reuse after deletion or concurrent payload mutation. Lifetime exclusion handles deletion; data guards handle conflicting payload operations. Exclusive creation is the chosen synchronization simplification, not a requirement caused by property-array relocation.

An ordinary dynamic array owned by an object may still resize or relocate under Section 12. Its descriptor's stable property address does not stabilize its backing elements. References retain root/index paths, bounds checks, and required data protection across resolution and access. Same-thread potentially relocating operations still require re-resolution. This reservation policy does not reserve a full class-index-sized virtual region for every ordinary array value.

### Cross-class requirements and acquisition proposal

The compiler infers conservative transitive class-access requirements from object use, creation, destruction, generated cleanup, callbacks, and final hooks. A class used without creation/destruction requires shared access; a class that may be created or destroyed requires exclusive access. Exclusive mode wins when requirements combine. Recursive call groups require a fixed point, and generic specialization must carry all participant/role bindings. Unknown external effects cannot be treated as no effects.

A constructor or destructor may touch several classes. Destruction needs exclusive access for every class it may destroy, and at least shared access for surviving parent/other objects it uses. Detaching links also requires the appropriate data guards; exclusive access to the destroyed child's class alone does not protect every parent field. Generated counted releases and final hooks contribute their full requirements.

**Proposed initial acquisition discipline:** acquire the complete required lifetime-lock set and modes before a region, in one process-wide stable order. Helpers reuse held access. Nested regions use subsets with no stronger modes; no new class lock or shared-to-exclusive upgrade is acquired midway through the region. This is a proposed protocol, not an approved source grammar. Explicit outer regions with compiler-inferred sets/modes are the current recommendation; exact boundaries, syntax, and diagnostics remain for review.

Lock identities and their order must be consistent across dependencies and actual allocator storage. Ordering the lifetime locks alone does not establish safe ordering with payload guards, foreign locks, channels, or joins. Their combined acquisition/blocking contract remains to be specified. Do not infer deadlock freedom from the lifetime-lock ordering proposal alone.

### Same-thread destruction and safe loops

Lifetime locks exclude interfering threads; they do not make the current thread's destructive operations harmless. Ordinary class-wide invalidation applies inside exclusive regions. Creation alone does not invalidate existing object references, though constructor calls can have other destructive or mutating effects.

A safe relation loop may still destroy only its current object of the iterated class. Such a loop requires exclusive access to that class from the outset. Deletion invalidates the iteration variable and references into it; the representation preserves continuation under its stated traversal contract. Neither exclusive lifetime access nor a data guard permits same-thread peer deletion that violates that contract. A non-destructive loop can share lifetime access with other threads, but its collection traversal also needs protection against conflicting structural mutation by other shared holders.

### Finalizers, blocking, and thread communication

Every deferred-finalizer execution point must account for transitive destruction effects, exclusive lifetime access to classes it may destroy, and applicable data guards. A finalizer needing stronger modes or new locks must defer to a suitable specified boundary rather than upgrade or reclaim without protection. Exact boundaries and orderly-shutdown guarantees remain open.

Blocking waits, joins, foreign callbacks, and lock release need explicit checked contracts. In particular, a thread must not hold lifetime access while waiting for work that needs conflicting access without a protocol establishing safe progress. Compiler inference of lock requirements is not a proof of arbitrary external wait behavior.

Lifetime/allocation locking does not by itself specify publication, thread entry, spawn, channels, cancellation, or joining. These operations must transfer values or use maintained relationships/other explicitly safe lifetime mechanisms; borrowed pointers cannot be placed in queues as unchecked escapes. Full concurrent-language support is not established merely by specifying a class lock.

Foreign functions need truthful creation, destruction, mutation, escape, and lifetime/data-lock contracts. Unknown effects are not harmless. Native code violating its contracts is outside the safe-language guarantee; keep that trust boundary small and explicit.

## 20. Compiler structure and explanations

The compiler should follow the architecture the language supports:

- Shared declarations hold types, relations, and callable contracts.
- AST nodes have their structural parents.
- Symbols know their scopes.
- Bindings retain all participating types and endpoint roles.
- Functional passes navigate those relationships.
- There is one authoritative representation of each fact, not competing lookup tables compensating for absent links.

### Syntax grammar and parser implementation

Lyric uses a normative PEG grammar specifying its syntax and ordered parsing choices. Ordered alternatives are attempted in written order; the first syntactically successful alternative wins. Generic syntax is attempted before comparison syntax. Later name-resolution or type-checking failure does not cause the parser to reinterpret that successful syntax choice.

PEG supplies an inspectable recursive-descent roadmap. The implementation may execute, generate, or directly follow that grammar; a particular parser generator and a packrat/memoization strategy are not selected yet. A manually written parser must preserve the PEG's ordered choices, lookahead, and backtracking behavior rather than independently redefine the syntax. LLM assistance reduces maintenance cost but does not replace the grammar contract or conformance tests.

The grammar specifies lexical boundaries, declaration and expression forms, precedence/associativity, and newline handling. It must distinguish prefix/infix wrapping operators, !as from unwrap-plus-cast syntax, generic angle brackets from comparisons, unit expressions from calls, and labeled interface bindings. Prose specifies name resolution, type compatibility, lifetime/effect checks, and other semantic constraints that syntax grammar alone does not establish.

The parser implements the grammar rather than silently defining additional language rules. Maintain accepted and rejected conformance examples tied to grammar rules, including ambiguous-looking boundaries and useful source diagnostics. Update the grammar, explanatory prose, and corresponding tests together when syntax changes. The existence of handwritten parser code is not a substitute for the normative grammar, and a grammar document alone is not proof of parser conformance.

Bill reports that he originally required Rune-style PEG parsing for earlier Lyric, but Opus implemented recursive descent. After considering handwritten-parser maintenance and LALR(1) conflict checking, Bill selected PEG for this design because its ordered choices make the recursive-descent roadmap explicit. This is a parsing-semantics choice, not a claim that PEG proves semantic correctness or guarantees performance.

The active consolidated syntax draft is [lyric-grammar.peg](lyric-grammar.peg), with [grammar-review.md](grammar-review.md) documenting lexical conventions, remaining proposed syntax choices, coverage gaps, and example expectations. Generic-first ordering is approved; unrelated draft choices are not automatically approved by selecting PEG. The [PEG structural receipt](peg-check.txt) checks production integrity and recursion/repetition hazards, not parser conformance. No Lyric parser or PEG generator has been run. The earlier [EBNF draft](lyric-grammar.ebnf), [original review](drafts/05-ebnf-grammar-review.md), and [EBNF receipt](grammar-check.txt) remain historical artifacts.

Parsing, resolution, elaboration, specialization, effect inference, safety checking, lowering, optimization, and emission are distinct responsibilities. Their exact pass arrangement is an implementation choice. No generated specialization may escape the safety checks.

Specialization substitutes resolved declarations and bindings, not ambiguous text. Generated name mangling is not the identity of a relationship.

Compiler explanations should answer concrete questions:

```text
Show Node's parents and children.
Show what Graph.prune may destroy, and why.
Show which relation satisfies this abstract graph role.
Show the structural cleanup generated for Edge.
```

These answers come from the authoritative model. A separate tool should not have to guess them from text.

The C backend is a practical starting point. AoS and SoA are layout choices with the same source semantics. Bounds checks, arithmetic rules, evaluation order, and lifetime checks must survive lowering and optimization. Debug UAF sentinels are diagnostic aids, not a substitute for static safety.

## 21. Verification

A specification must state both accepted and rejected behavior. Useful conformance cases include:

1. Constructors establish both endpoints, including two same-class parent roles.
2. Refused relationship changes leave both ends unchanged.
3. Destroying either parent of an edge removes both memberships exactly once.
4. Weak relationships detach without cascading destruction.
5. Final hooks run before their own object's structural teardown.
6. Direct, indirect, recursive, generated, and counted-release destruction invalidates local references.
7. Reacquisition through a surviving parent is accepted.
8. Aggregate-contained aliases and generated temporaries cannot evade invalidation.
9. Safe loops accept current-item deletion and reject deletion of another same-class item.
10. Each relation representation continues iteration correctly after permitted removal.
11. Array references survive relocation and fail safely when an index path disappears.
12. Multidimensional array references check every path level.
13. Two same-type relations specialize independently under distinct roles.
14. Multi-class default methods receive all type and relation bindings.
15. Hash collisions preserve distinct keys.
16. Incomplete constructors, unsafe escapes, invalid casts, and unsupported concurrent lifetimes are rejected.
17. A failed constructor detaches partial memberships and destroys initialized children without calling its own final hook; fully constructed children's hooks still run.
18. A shared-object reference is acquired only after shared or exclusive lifetime access; multiple threads may use existing objects under shared access. Payload accesses must separately satisfy their data guards.
19. A relationship update missing either endpoint's required lifetime access or the data protection needed for both ends is rejected.
20. Unlocking and reacquiring a class lock does not make a previously borrowed object or array-slot reference valid again.
21. Shared lifetime access alone does not protect fat-reference resolution against another shared holder's resize. Required data protection covers resolution and access; same-thread resizing still requires re-resolution.
22. A cascade's requirements include exclusive lifetime access for all classes it may destroy, at least shared access for other objects used, and the data guards required by detachment, releases, and final hooks.
23. Under the proposed complete-set discipline, nested regions cannot acquire new lifetime locks or stronger modes. Helpers reuse held access; shared-to-exclusive upgrades are not allowed.
24. Nested array assignment preserves independence at every array level; two object owners never require unrelated locks for shared mutable backing storage.
25. A C final hook calling a helper that may destroy C is rejected at compile time, including indirect cascades, callbacks, and counted releases; claiming a different C instance does not bypass the rule.
26. A final hook may perform checked operations on other classes whose transitive effects do not destroy its own class. Generated same-class child cascades after the hook remain possible under the normal detachment and lifetime rules.
27. A final hook declaring Result or another non-unit return type is rejected, as is using ? to propagate a fallible call from the hook. A locally handled failure may permit normal hook completion, after which structural destruction continues.
28. An implicitly discarded Result or a local Result that is never used is rejected. Explicit let _ discard is accepted but does not satisfy a required-success postcondition or permit generated teardown to ignore failed detachment.
29. Growing a SoA class population leaves every property-array base and existing live slot address unchanged. Reservations cover the full 32-bit index range with checked stride calculations; backing newly used pages does not relocate the region. Ordinary object-owned dynamic arrays retain their separate resize/path rules.

30. Creation or deletion with only shared class access is rejected. Exclusive access excludes all other class users and permits allocator/free-list updates without an additional per-class allocator mutex.
31. A non-destructive traversal can run alongside other shared class users; a traversal creating or destroying instances of that class requires exclusive access. Neither case bypasses payload/collection data guards.
32. Concurrent value-class count updates are not accepted solely on the basis of shared lifetime access. A potentially final release must satisfy exclusive destruction requirements or a specified deferred-finalization protocol.

Tests may use `test_` functions and compiler-provided assertions. Checked failures terminate the test process under Section 14. Continuing a suite after such a failure requires separate test processes; the runner must not resume tests in the failed process.

Self-hosting, fixed-point compilation, and benchmarks supplement these checks. They do not replace them.

## 22. Remaining decisions and verification boundaries

The following are now decided: explicit value/ordinary class lifetimes; let/var/ref syntax; multilevel array value assignment; object-owned arrays; no escape of borrowed stack/reference-parameter storage; flexible but valid value finalization; generated cleanup of failed construction without its final hook; final-before-parent-detach-before-child-cascade ordering; nonescaping closures; loop-only single-yield generators; and per-class reader–writer lifetime/allocation protection (shared use, exclusive creation/destruction, no additional per-class allocator mutex).

Focused remaining work:

- **References and arrays:** optional and tagged value-payload references follow checked paths, with presence/tag checks on access; separate class-object references retain object identity and lifetime obligations. Explicit enum/union payload-borrowing syntax remains to be specified in the pattern grammar. Function-boundary resize requirements are inferred per parameter/storage path and propagated through calls; fixed-length windows cannot satisfy resize requirements. Opaque boundary contract syntax remains open. Whole-window assignment is equal-length and overlap-safe, with normal element value/lifetime semantics and bounds/length checks before destination writes. Array paths survive value replacement and reordering, resolve current storage, and bounds-check each access; owner destruction still invalidates them. Ordinary slices are independent array values; explicit reference slices borrow fixed-length windows. Ordinary parameters are immutable value bindings with assignment semantics; explicit `ref` borrows caller storage. Named references and reference parameters are fixed aliases: reads produce values, assignment writes through, and `ref` of an alias denotes the same storage. Rebindable reference variables are not supported. Source syntax is `ref T` and `ref expression`; no `mut` is needed.
- **Finalization:** deferred-finalizer boundaries and orderly-shutdown guarantees remain open. Final hooks may not transitively destroy their own class; this is a compile-time rule, with a defensive reentry guard rather than a substitute for static checking. The timing/order contract and partial-construction behavior are no longer open choices.
- **Locking:** shared/exclusive class lifetime/allocation modes and exclusive create/delete are settled. Finalize region boundaries/syntax, complete-set/global-order acquisition, payload/global guards and their ordering, counted-update synchronization, writer progress, safe publication/transfer, and opaque callback contracts. The lock-set acquisition protocol remains a proposal, not implemented safety.
- **Closures and generators:** complete capture-selection and checked expansion rules; escaping closures and independently resumable generators remain deferred.
- **Language completeness:** numeric/lexical appendix, package-membership/configuration grammar, many-child relation API edge contracts, and library edge behavior. Single's cardinality and set/remove Result<unit> behavior are specified in Section 6. Collection's iteration/count/parent capability is specified in Section 16; representation-specific traversal implementations still require verification. Logical packages spanning directories and same-package declaration/body association are settled; an internal architectural dependency checker is not yet specified.

Each remaining mechanism needs a positive example, a rejected example, and a reason. The absence of implementation evidence must not be confused with ambiguity about decisions already made.

## 23. Provenance and current status

The original reference, full spec, and newer interface redesign are preserved unchanged in `background/`. Earlier drafts are retained in `drafts/01-reconciled-design.md` and `drafts/02-expanded-proposal.md`.

This version was rewritten after Bill explicitly reaffirmed KISS. It removes root classes, attach clauses, finalization-view types, privileged Collection contracts, separate view types, the new declaration extension, and broad effect vocabulary. Bill then approved an explicit distinction between reference-counted `value class` and ordinary cascade-deleted `class`, without the earlier immutability restrictions. This distinction is now restored. The first implementation schedule is not a permanent language restriction.

No compiler has been implemented or changed for this document. Full memory safety is the design goal, not an accomplished or formally proved result. The next work is to settle the focused semantic cases with Bill and then build bounded experiments that earn the final rules.

## Appendix A. Lexical and expression baseline

This appendix restores concrete rules from the original specification. It is the compatibility baseline unless a numbered section explicitly changes a rule. Original implementation bugs and roadmap-only syntax are not silently adopted.

### Tokens and literals

Names are case-sensitive. A name must resolve to a declaration; the checker does not infer type variables from capitalization or guess a similarly spelled field. Keyword/contextual-keyword classification still needs a single formal grammar, including the newly accepted `value` class modifier.

The baseline literal forms are:

| Kind | Form | Meaning |
|---|---|---|
| Integer | `42`, `1_000` | Decimal integer; default `i32` when context does not select another representable type |
| Float | `1.5` | Default `f64`; `1.` is not a float literal |
| Byte character | `'a'`, `'\n'`, `'\xFF'` | A `u8` byte |
| String | `"hello"` | Byte string, normally UTF-8 text |
| Triple string | `"""..."""` | Preserves internal newlines and trims boundary whitespace, retaining the original baseline |
| Interpolated string | `f"value={value}"` | Evaluate expressions and stringify; `{{` and `}}` escape braces |
| Symbol | `` `name` `` | Equivalent to `sym("name")` |
| Boolean | `true`, `false` | Boolean values |
| Absence | `null` | Optional absence; needs a nullable target type |

Supported escapes are `\n`, `\r`, `\t`, `\\`, `\'`, `\"`, `\0`, and `\xHH`. Unicode escapes, numeric base prefixes, numeric type suffixes, and exponent notation must be explicitly added to the grammar before acceptance. They are not established merely by describing them as useful future additions.

F-string interpolation accepts expressions, not a second restricted expression language. Nested braces belonging to an expression must be parsed correctly. Evaluation follows ordinary left-to-right expression order. Invalid escapes and unterminated literals are diagnostics, not malformed runtime strings.

Line comments begin with `//`. Block comments are outside this baseline. Newlines inside `()` and `[]` are whitespace; newlines inside `{}` separate statements. A semicolon may explicitly separate statements on one line, as in the original examples. There is no backslash line continuation.

### Precedence

The following table retains the original arithmetic structure while applying Bill's requirement that integer bitwise operators bind more tightly than comparisons. Lowest precedence is first.

| Level | Operators |
|---|---|
| 1 | `||` |
| 2 | `&&` |
| 3 | `==`, `!=` |
| 4 | `<`, `<=`, `>`, `>=` |
| 5 | bitwise `|` |
| 6 | `^` |
| 7 | `&` |
| 8 | `<<`, `>>` |
| 9 | `+`, `-`, `!+`, `!-` |
| 10 | `*`, `/`, `%`, `!*` |
| 11 | unary `-`, `!-`, `!`; `~` when explicitly implemented |
| 12 | member access, calls, indexing, postfix `!`, postfix `?`, `is`, `as`, `!as` |

Binary arithmetic is left-associative. Logical operators short-circuit. Parentheses override precedence. Assignment is a statement, not a value-producing expression. The exact grammar of postfix casts/tests must disambiguate type expressions from neighboring operators; a precedence table is not a substitute for that grammar.

The compound assignments are `+=`, `-=`, `*=`, `/=`, plus explicit wrapping `!+=`, `!-=`, and `!*=`. Additional bitwise and remainder compound assignments are specified extensions, not silently accepted placeholders. Every compound assignment evaluates its destination once and preserves lifetime checks across evaluation of its right side. Wrapping forms follow Section 18's modular arithmetic rules.

### Indexing, slicing, and tuple access

```lyric
items[index]
items[low:high]
items[low:]
items[:high]
items[:]
pair._0
let (left, right) = pair
```

Slice endpoints are half-open: low is included, high excluded. Missing low means zero; missing high means current length. Invalid indices or ranges fail a checked bounds operation. An empty range is valid. Strings support the same byte-range forms.

These forms define selection. Ordinary array slices produce independent array values; `ref array[low:high]` borrows a fixed-length window, as specified in Section 12. Borrowed windows re-resolve current storage and bounds-check access after resize. String ownership and conversion contracts remain library work; byte-range syntax alone does not authorize writable string references.

## Appendix B. Bindings, construction expressions, and control flow

### Bindings

```lyric
let x = 42
var count: i32 = 0
let x: i32
let _ = operation()
let (value, err) = operation()
```

A declaration without an initializer is permitted locally only when definite-assignment analysis proves initialization before use. A `let` binding may be initialized once, not subsequently changed. Module-level declarations require an initializer. `let` prevents changing the bound value, including array elements and length; `var` permits value mutation and rebinding. Neither makes referenced class objects immutable. Reference parameters use `name: ref T`; reference arguments use `ref storage`. The earlier `mut` spelling is removed. Constants and mutable globals need visibility and initialization-order rules before cross-module use is accepted.

Block scope permits shadowing. A shadowed declaration remains a different binding; shadowing cannot restore the validity of a stale reference copied from the outer binding.

### Construction expressions

Named-field struct construction uses `Point { x: 1.0, y: 2.0 }`. Field names must match exactly, fields cannot be specified twice, and omitted fields require a defined default or explicit initialization rule. Field defaults belong to the declaration.

The original permits positional struct construction in expression contexts where braces are unambiguous. Retaining that second form is a compatibility choice, not necessary for KISS. Until that choice is made, examples in this specification use named fields only.

A class with an explicit constructor uses `ClassName(arguments)`. Generic construction uses `ClassName<T>(arguments)`. Field-literal class construction is allowed only when it cannot bypass a required constructor, parent attachment, or initialization invariant. The exact eligibility check must be specified rather than treating all class literals as harmless records.

### Conditionals and loops

```lyric
if condition {
    action()
} else {
    alternative()
}

while condition {
    action()
}

for item in collection {
    use(item)
}

for item, index in array {
    use(item, index)
}
```

Conditions have boolean type. A two-variable array loop yields the element followed by its zero-based index. `break` exits the nearest loop; `continue` begins its next iteration. Required cleanup and validity analysis apply on both paths.

An if-expression requires an else branch and compatible branch result types:

```lyric
let name = if enabled { "enabled" } else { "disabled" }
```

Safe relation iteration follows Section 11. Ordinary array iteration still needs the array storage/mutation contract; it is not automatically interchangeable with relation iteration merely because both use `for`.

### Patterns and optionals

```lyric
match shape {
    Circle(radius) => { use(radius) }
    Rect(width, height) => { use(width, height) }
}

if let Circle(radius) = shape {
    use(radius)
}

let Circle(radius) = shape else { return }
```

Qualified variant names disambiguate variants from different enums. Nested variant and tuple patterns recursively bind their payloads. Literal leaves match their values. `_` discards. Guards run only after the structural pattern matches. Pattern-bound variables exist only in the arm, except successful let-else bindings, which continue in the enclosing scope.

Alternatives in one arm must bind compatible names and types. `is` tests a variant without binding. A let-else failure branch must diverge; this is checked, not merely a convention.

`isnull(x)`, `x == null`, and `x != null` test optional absence. `x!` unwraps or produces a checked failure. The original lvalue form `object.data!.field = value` writes through an optional struct rather than modifying a discarded copy. Its receiver path is evaluated once and lifetime-checked.

A `T` can wrap implicitly into `T?`. `let x = null` without a determinable target type is an error. There is no implicit null dereference on class optionals.

### Unions

`T | U` is a tagged union. Each alternative injects into the union without reinterpretation. Extraction requires a type/variant match, not an unchecked cast. A union-to-union assignment requires every source alternative to be accepted by the target; it does not silently discard alternatives. Matching a union must respect its actual tag.

## Appendix C. Generics and implementation forms

### Generic declarations and constraints

```lyric
func first<T>(items: [T]) -> T?
func transform<T, U>(items: [T], operation: fn(T) -> U) -> [U]
func clamp<T: Comparable>(value: T, low: T, high: T) -> T
func count<P, C>(parent: P) -> i32 where Collection<P, C>
```

Type variables are declared explicitly. The canonical placement remains after the function name, not `func<T> name`. Both explicit call arguments and inference are supported:

```lyric
identity<i32>(42)
identity(42)
```

Inference matches parameter and argument type structure, including arrays, optionals, tuples, functions, and generic class arguments. Repeated occurrences of a variable must agree; the first match cannot silently override a conflicting later match. Ambiguity or an uninferred variable is a diagnostic. Implicit conversions must not be used as arbitrary guesses between implementations.

Nested generic closing brackets must parse correctly, including `Outer<Inner<T>>`, without confusing them with a shift in expression context.

Inline `T: Capability` is shorthand for the corresponding single-participant interface requirement. A relational `where Capability<P, C>` names all participants. Multiple requirements can be listed in a where-clause; adding a separate top-level constraint-alias declaration is deferred.

### Endpoint labels in constraints and qualified calls

A participating type argument may carry an endpoint label in a constraint, just as in an impl. Both endpoint roles are part of the binding identity:

```lyric
func outgoing_count<N, E>(node: N) -> i32 where Collection<N:outgoing, E:source> {
    return Collection<N:outgoing, E:source>.count(node)
}
```

The constraint selects the parent role `outgoing` on N and child role `source` on E. Exact existing roles can satisfy it structurally. Different concrete role names require an explicit binding mapping abstract roles to concrete roles; neither type coincidence nor a unique-looking unrelated relation authorizes guessing that mapping.

Omitting a label selects the unlabeled, flat scope. It is not a wildcard search across all relationships on the participating class. A label selects a compile-time scope, not a runtime argument or adapter object. Distinct labels on the same participating types remain distinct bindings throughout inference, satisfaction, specialization, and effect checking.

Interface-qualified calls accept the same labeled type arguments:

```lyric
Collection<Node:outgoing, Edge:source>.count(node)
```

All participant types and labels must select one valid binding under the ordinary rules. Whole-relation mappings in an enclosing interface impl carry both endpoint roles into its algorithms; per-member mappings must remain consistent with the selected binding. A constraint checks existing capabilities and never creates relation storage.

An interface's type parameters are in scope for all its member declarations and default bodies. A generic class's parameters are in scope for its fields and methods. Type-variable scope must be explicit across separately declared implementations.

### Generic classes and partial impls

```lyric
value class Box<T> {
    value: T
}

impl<T> Container<Box<T>, T> { }
```

The impl's explicit type variables remain open and specialize at concrete use sites. Concrete type arguments, endpoint labels, and implementation identity all belong in the specialization key. Reusing a specialization with the same receiver but different other participants is incorrect.

The class category is part of generic checking: a value class instantiated with stored ordinary-class references cannot silently evade the relationship/lifetime obligations. This example describes generic syntax, not a waiver for every possible T.

### Method, field, and inline bindings

An impl may bind abstract members to concrete operations:

```lyric
impl Printable<Widget> {
    P.text = Widget.text
}
```

It may define an adapter inline:

```lyric
impl Printable<Widget> {
    P.text = (self) -> string { return self.name }
}
```

For storage-providing interfaces, `<->` binds an abstract field to existing concrete storage:

```lyric
impl ArrayList<Team:roster, Player:team> {
    P.children <-> Team.members
    C.parent <-> Player.team_parent
    C.index <-> Player.team_index
}
```

A field binding is not a second copy or a synchronization callback. Read/write permission, types, and lifetime category must match. A getter alone cannot satisfy a writable field requirement.

An empty impl is an optional checked assertion requesting exact unambiguous derivation of remaining requirements; structurally matching types do not need one merely to satisfy a capability. Mixed-mode impls explicitly bind some members and derive the rest under Section 15's precedence rules. Diagnostics list every unsatisfied requirement and its expected signature. No fuzzy names, arbitrary widening, or declaration-order selection resolves ambiguity.

### Ownership-bearing impls and cleanup templates

The underlying storage-binding form remains available:

```lyric
impl ArrayList<Team:roster, Player:team> owns { }
```

A relation is the convenient spelling for the corresponding labeled, ownership-bearing binding. Its shape must satisfy the relation rules. A relation that names an unknown or malformed hint is an error, not a silently omitted declaration.

Cleanup templates use the inherited explicit spelling:

```lyric
interface ExampleRelation<P, C> {
    destructor owns P { ... }
    destructor owns C { ... }
    destructor refs P { ... }
    destructor refs C { ... }
}
```

The ellipses indicate omitted library implementation, not executable syntax. The modifier selects the matching pair. Application classes do not copy these blocks into handwritten destructors. The old bare `destructor P` shorthand should be either documented as `owns` or rejected consistently; it is not needed in new examples.

### Composition and static requirements

`extends Parent<...>` substitutes the parent's participants and carries its requirements and defaults. Same-signature override is explicit in the child declaration; incompatible signatures are errors. Initial composition is single-parent. Runtime subtype conversion is not implied.

An interface can require a static operation by omitting `self`, such as `T.zero()`. Calls on a type variable resolve through its bound capability. Numeric capabilities must list their supported operations and types; they must not be recognized only through a suggestive name.

The old `implements` clause is a declaration of conformance, not evidence of it. If retained, it must be checked using the same satisfaction rules as impls. Whether it remains useful alongside explicit impls is a simplification decision, not permission to leave an unchecked assertion in the language.

## Appendix D. Library surface to preserve and specify

These declarations restore the original library inventory so it cannot disappear through summarization. Signatures below are the compatibility baseline, not proof of a new implementation. A complete library contract also states errors, ownership, mutation, and edge cases. The coverage report identifies remaining gaps rather than treating a signature table as sufficient.

### Core and conversion

| Operation | Baseline contract |
|---|---|
| `len(array_or_string)` | Return byte/element count as `i32`; changing count width requires a deliberate decision |
| `append(array, element)` | Return resulting array; assignment of the result follows value semantics. The function's own source-mutation contract must be specified separately |
| `print`, `println`, `eprint`, `eprintln` | Format supplied values; select stdout/stderr and newline behavior |
| `to_string(value)` | Generate or dispatch a textual representation |
| `format(pattern, ...)` | Format values; exact format grammar must be defined |
| `isnull(optional)` | Return whether absent |
| `panic(message)` | Report a checked programming failure with source location and stack trace, then terminate the process under Section 14 |
| `exit(code)` | Terminate process with explicit status |
| `itoa(i64)` | Decimal string |
| `atoi(string)` | `(i64, bool)` indicating conversion success |
| `parse_float(string)` | `(f64, bool)` indicating conversion success |
| `char_to_string(u8)` | One-byte string |
| `string_to_bytes(string)` | `[u8]`; sharing/write permission must follow settled string/array rules |
| `bytes_to_string([u8])` | String conversion with a specified ownership contract |
| `hash_string(string)` | `u64` hash; not equality |
| `sym(string)` | Interned symbol |
| `invalid_argument(string)` | Non-OK Status for an invalid argument; the full standard-code factory inventory remains to be specified |

The original used `any...` to describe printing, but that does not by itself specify a general dynamic `any` type. Formatting can be compiler-specialized. Dynamic interface representation and lifetime must be explicit wherever runtime erasure is supported.

### Arrays

Preserve the operations `len`, `is_empty`, `push`, `append`, `pop`, `contains`, `index_of`, `first`, `last`, `sort`, `reverse`, `clear`, `remove`, `extend`, `slice`, and string-array `join`.

Baseline results: `first` and `last` return `T?`; `index_of` returns `i32` with -1 for not found; `pop` returns T; mutators return `unit`. Empty pop, equality requirements, sort ordering/stability, invalid ranges, and remove-not-found behavior need explicit rules rather than backend defaults.

Aliases such as `length`/`len`, `find`/`index_of`, and `push`/`append` are compatibility conveniences. KISS can select one canonical spelling, but removal must be documented rather than accidental.

### Strings

Preserve byte-oriented `len`, `is_empty`, `contains`, `has_prefix`, `has_suffix`, `index_of`, `substring`, `trim`, `to_lower`, `to_upper`, `replace`, `repeat`, `char_at`, `split`, and `join`.

`char_at` in the original returns a one-byte string, not a Unicode character. `to_lower`/`to_upper` are initially ASCII operations. Explicit Unicode operations can coexist without changing byte indices implicitly.

The original `str_*` free-function utilities provide these operations plus trim-left/right and bounded splitting. Their duplication with methods needs a documented canonical API, not two slightly different semantics.

Byte predicates `char_is_digit`, `char_is_alpha`, `char_is_space`, `char_is_upper`, `char_is_lower`, and `char_is_alnum` take `u8` and return bool. Their classification is ASCII, not locale-dependent.

### Containers and builders

`Dict<K,V>` provides `set`, `get`, `has`, `remove`, `keys`, and `len`. `get` returns an optional entry with key/value fields. Its entry lifetime after mutation must be specified; returning an entry reference is not automatically safe merely because the dictionary itself survives.

`Hashable` must support collision resolution together with equality. `Sym` exposes its name and cached hash. Equal hashes alone never identify a key. The older restriction against raw string keys is a hash-once policy to discuss, not a memory-safety requirement.

`StringBuilder` provides `write(string)`, `write_byte(u8)`, `to_string()`, and `len()`. The accepted mutable value-class category is suitable if its ownership and conversion behavior are defined. No immutable-only restriction is imposed.

### Files and processes

The original minimal I/O surface is retained for coverage:

```text
read_file(path: string) -> (string, bool)
write_file(path: string, content: string) -> bool
os_args() -> [string]
os_getwd() -> string
exec_command(name: string, args: [string]) -> (string, bool)
path_join(parts: [string]) -> string
path_dir(path: string) -> string
path_base(path: string) -> string
path_ext(path: string) -> string
list_dir(path: string) -> ([string], bool)
file_exists(path: string) -> bool
mkdtemp() -> string
```

These signatures are not a complete operating-system contract. Define whether bool means success, how exit status differs from spawn failure, byte encoding, subprocess cancellation, resource cleanup, and failure of functions with no error result. A uniform error-returning API may be preferable, but that is a deliberate API revision.

Handle-based Reader/Writer, buffering, directory mutation, and network I/O remain the original planned library work. Their concrete classes may use final hooks for handles; their relationship and value lifetimes must follow the same language rules.

## Appendix E. Tests and toolchain contracts

The baseline test is a no-argument, no-result function whose name starts with `test_`. Test files are ordinary `.ly` files. Functions named otherwise may serve as helpers but are not automatically tests.

```lyric
func test_add() {
    assert_eq(add(2, 3), 5)
}
```

`assert(condition, message)` produces a checked programming failure if false. `assert_eq(actual, expected, message?)` compares values and reports both, with the source file and line. A failing assertion reports a stack trace and terminates the test process under Section 14. A runner continuing the suite executes other tests in separate processes, not by recovering within the failed process. Suite exit status is 0 if all selected tests pass and nonzero if any fail or compilation fails.

The original command surface is:

```text
lyric compile <files-or-module> -o output
lyric test <files>
lyric fmt <files>
lyric help
```

Keep these names unless deliberately changed. The old formatter handled `.lyric` documentation zones; the new compiler formatter must not claim to maintain an external tool's annotation layer. Formatting plain language source and formatting CDD metadata are separate contracts.

`--soa` selects layout without changing language behavior. `--lir-dump` provides diagnostic output. A UAF-debug option is diagnostic support, not a substitute for static checking. Bounds-check disabling is an explicitly unsafe benchmark mode. Old RC-disable flags cannot silently change the accepted lifetime contract in an ordinary safe build.

A whole-module compile includes the declared files and resolves dependencies deterministically. Parser/checker errors prevent executable output. An example appearing in this document is not tested merely because it is fenced as Lyric; compiler validation remains future work.

## Appendix F. Completeness status

The comparison against the original specification is recorded in `spec-comparison.md`. The appendices above restore substantial concrete coverage but do not make this draft a finished specification. Outstanding details include a complete grammar, numeric conversions, reference/range edge behavior, checked generator expansion, thread publication and communication, deferred-finalizer scheduling and shutdown, foreign contracts, and library edge behavior. Array ownership and the shared/exclusive class lifetime/allocation protection unit are specified in the main sections; payload synchronization remains a separate contract.

A full specification must answer how to parse, type-check, evaluate, and clean up every supported construct, including interactions. It must state exclusions and errors, not require readers to discover behavior from the old compiler. KISS governs the number and interaction of mechanisms, not the amount of explanation needed to specify them accurately.
