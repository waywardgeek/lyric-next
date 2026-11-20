# Lyric Language Specification

**Draft 3.1 — KISS revision with explicit class lifetimes, for Bill and Cadence to develop together.**

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

A package is a directory. Its files share one namespace and are compiled together. Declaration and file order do not affect meaning. A module is a project rooted at `lyric.mod`.

Small programs may keep declarations and bodies in one file. Larger programs use shared declarations and functional implementations of those declarations:

```text
common/model.lyric
parser/parser.ly
checker/checker.ly
application/main.ly
```

The exact cross-directory assembly mechanism must be settled against a small compiler example. The architectural requirement is already fixed: Common is the shared declaration dependency; functional units consume it rather than each other's implementation internals; application code composes the program. Splitting a type's declaration and implementation must not create distinct objects or runtime forwarding glue.

Names are private to their package unless marked `pub`. Import access is qualified:

```lyric
import ast
import parse from "parser"
```

The compiler resolves imports recursively and diagnoses unresolved or ambiguous declarations. Dependency policy and `lyric.mod` contents are toolchain design, not reasons to add restrictions to object graphs.

Every callable declaration has a matching implementation or an explicitly declared external implementation. A bodyless declaration is not an empty function.

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

`let` declares an immutable value binding; `var` declares a mutable one. There is no alternative `let mut` declaration. For an array value, `let` prohibits changing its elements or length as well as rebinding the variable. Neither declaration freezes separately referenced class objects. `mut` remains the explicit mutable-reference marker on parameters and call arguments, not a variable-declaration modifier.

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

### Composite types

```text
T?               optional T
T | U            tagged union
[T]              array
(T, U)           tuple
fn(T) -> U       callable type
gen T            generator result
arrayref<T>      reference to an array slot by root and index path
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

They are copied as values. Mutable reference parameters use `mut` at the declaration and call site:

```lyric
func translate(mut point: Point, dx: f64) {
    point.x = point.x + dx
}
translate(mut p, 3.0)
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

Single-child relationships need the same both-end maintenance. They use a single-slot representation, not an untracked pointer field. Their exact library hint name is not important to the language mechanism.

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

The standard representations begin with ArrayList, DoublyLinked, and HashedList. Every representation supports both `owns` and `refs`.

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

Failed construction must not leave partial memberships or published references to an incomplete object. Ordinary code may explicitly undo earlier attachments. If automatic failed-construction cleanup is later introduced, it must have a separately verified contract; the compiler cannot silently claim to undo arbitrary external effects.

The compiler checks definite initialization, parent attachment on successful paths, and cleanup on ordinary failure paths. It rejects escape of an incompletely initialized object. How those checks are represented internally should be developed with small examples, not hidden behind the word “transaction.”

Parent add/remove operations update both ends. Invalid operations either fail before mutation or follow a documented infallible precondition. Removing an object from a relation does not, by itself, mean destroying it. Destruction and detachment are distinct operations.

## 8. Generated destruction and `final`

Application programmers do not write structural destructors. The compiler generates them from the object's fields and relations.

Generated destruction:

- Detaches the object from all its parents.
- Destroys children reached through owning relations.
- Unlinks weak child relationships without destroying their children.
- Releases reference-counted stored values as appropriate.
- Releases the object's storage.

A class may provide one `final` method:

```lyric
class Connection {
    fd: i32
    final func close_resource(self) {
        close_fd(self.fd)
    }
}
```

The hook runs before that object's structural destruction. Its relationships have not already been removed by its caller. The hook handles application/resource cleanup; it does not replace generated graph maintenance.

The hook is analyzed like another method. If its calls can destroy objects, their effects participate in the enclosing destruction operation. This draft does not impose the previous blanket prohibition on graph effects or introduce a “finalization view” type.

Ordinary static invalidation applies inside the hook. Reusing `self` after a call that may destroy its class is invalid. Recursive destruction or resurrection of an object already being destroyed cannot be accepted merely because the call occurs in a hook.

The exact ordering of detach and cascade must be verified against multiple parents, self-relations, ownership cycles, and final-hook calls. Do not promise an arbitrary hook can observe a fully live ancestor graph during a cascade. These are focused destruction-contract questions, not reasons to invent a new class hierarchy.

A generated traversal must consult current memberships rather than retain pointers that a previous cascade may have destroyed. In particular, two parents must not each free the same child.

## 9. Declared lifetime policies

### Ordinary classes

An ordinary `class` has explicit lifetime. Its generated `destroy()` may be called directly or through an owning relation's cascade. Holding a temporary reference does not prevent destruction. The static invalidation rules protect those references, and maintained relations protect persistent component links.

An ordinary class may be independently created or attached to owning parents. No `root` modifier is needed. Detachment does not free the object automatically or convert it to reference counting. Independent allocation still needs eventual explicit destruction; retaining an object indefinitely is a leak concern distinct from dangling-reference safety.

### Value classes

A `value class` is reference-counted and may be mutable. Copying an owning reference retains it; overwriting or dropping that reference releases it. Count zero invokes its final hook and generated cleanup before freeing storage.

Ordinary code cannot call `destroy()` on a value-class instance. It releases references instead. This prevents a forced deletion from leaving other counted aliases pointing at freed storage and later attempting to release it again. Compiler-generated destruction at count zero is not a user-callable escape hatch.

The modifier does not require deep-copy assignment or immutable contents. Value-like vectors, matrices, and symbols need not record each holder as a parent merely for memory reclamation. Their chosen sharing/copy operations must still be documented.

A direct owning relation cannot forcibly cascade-delete a retained value-class child. A container holding values retains/releases them; that operation is not reinterpreted as explicit destruction. How relation-library APIs expose counted value collections should follow this distinction, without silently selecting a different lifetime policy.

### Generated releases are observable lifetime operations

When an array or field releases a value-class object, destruction at count zero has the same transitive effects as any other destruction. Those effects include its final hook and any owned-resource cleanup. The compiler cannot treat release as harmless merely because it was generated.

Calls may borrow value-class arguments rather than retain them only when analysis proves that their use remains safe. The exact parameter-passing and move-elision rules still need a complete contract; an optimization must not invalidate a reference or change specified finalization timing.

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

Ordinary array assignment copies the array value. Mutating the destination's elements or length does not change the source array value:

```lyric
let original = [1, 2]
var working = original
working[0] = 9
assert_eq(original[0], 1)
assert_eq(working[0], 9)
```

For an array of class references, copying copies the references, not the referenced objects. Replacing a destination slot leaves the source slot unchanged; mutating an object referenced by both arrays is visible through both references. Copies of counted references retain their objects; ordinary-class references remain subject to destruction invalidation.

This specifies observable behavior, not mandatory eager allocation. Moves or copy-on-write may avoid physical copies only when they preserve value semantics, reference validity, and specified lifetime behavior. A shared backing store must not make mutation of one ordinary array value change another.

`let` prevents mutation of its array value, including elements and length. `var` permits it. Explicit mutable-reference access requires mutable storage; it cannot be used to bypass `let`. Parameter passing, explicit views, and root-relative slot references still need their own contracts rather than being inferred from assignment.

### Object reference versus slot reference

A reference to an object stored in an array differs from a reference to the slot holding it. Resizing may move the slot without moving or destroying that object.

A safe array reference stores:

1. The root array reference.
2. The indices from that root to the selected slot.

For a multidimensional array, the path includes every level. Resolving the reference follows current storage and bounds-checks each index. It never follows an address into an obsolete allocation.

Use `arrayref<T>` as the descriptive type name. The existing `ref` vocabulary should supply its source syntax; this is preferable to introducing an unrelated general pointer operator. A candidate spelling is:

```lyric
let ref slot = matrix[row][column]
```

The final read/write spelling and root-retention rule must be chosen together. This is the one new reference mechanism Bill explicitly requested, not a family of array, relation, and finalization view types.

If shrinking removes the path, access fails the bounds check. Whether replacing an inner array makes a saved reference follow the new inner array is a semantic decision to confirm; the root-and-indices representation naturally supports positional interpretation, not old-element identity.

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

Errors use the existing tuple-return approach. To keep nullability explicit, this draft proposes `(T, error?)`, rather than making `error` secretly nullable:

```lyric
func divide(a: i32, b: i32) -> (i32, error?) {
    if b == 0 {
        return (0, new_error("division by zero"))
    }
    return (a / b, null)
}
```

The `?` operator propagates a nonnull error and otherwise extracts the value. `error` is an ordinary interface with `message(self) -> string`, not an exceptional loophole in the type system. The lifetime of concrete error values follows the ordinary class rules.

Early returns and propagated errors execute required generated cleanup. Recovery from panic or assertion failure must not continue with skipped cleanup and a damaged runtime. Whether panic terminates a process or has a safe recovery boundary belongs in the runtime contract, not an assumed consequence of C `longjmp`.

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

`Collection` names the declared relation capability: iteration, count, and parent access. Its library declaration must visibly specify those operations and the iteration contract. The compiler does not infer hidden capabilities merely from the identifier `Collection`.

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

Numeric overflow, mixed signedness, and narrowing must have defined behavior independent of C undefined behavior. Checked operations and explicitly named wrapping operations are reasonable candidates; this draft does not make an unreviewed global performance policy part of the relationship design.

Strings remain byte-oriented for indexing and length. Unicode-aware iteration and transformations are separately specified library operations. A byte is not a Unicode character.

`Sym` interns strings and caches hashes. Hash collisions are resolved by actual key equality; interning does not make hash equality sufficient. `Dict<K,V>` requires consistent hashing and equality.

Library containers obey the same lifetime, effect, and relationship rules as user code. They are not exceptions that can hide stale references from the checker.

## 19. Concurrency and external code

The initial compiler experiments can be sequential without defining Lyric as permanently single-threaded.

Concurrency needs a precise rule preventing another thread from destroying an object while it is accessed, or relocating array storage during an unprotected access. Sequential class-wide invalidation does not prove that property. Shared mutation, transfer, task lifetime, and joining must be designed together before concurrent programs are accepted as safe.

Likewise, a foreign function needs a truthful contract for ownership, destruction, mutation, and escape. The compiler cannot infer these from an unavailable native body. A trusted runtime or foreign implementation that violates its contract can violate the language's guarantees; that boundary must be visible.

No new concurrency machinery is selected in this draft merely to fill a section. Until the rules are established, implementations reject unsupported concurrent/escaping cases rather than quietly falling back to unsafe pointers.

## 20. Compiler structure and explanations

The compiler should follow the architecture the language supports:

- Shared declarations hold types, relations, and callable contracts.
- AST nodes have their structural parents.
- Symbols know their scopes.
- Bindings retain all participating types and endpoint roles.
- Functional passes navigate those relationships.
- There is one authoritative representation of each fact, not competing lookup tables compensating for absent links.

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

Tests may use `test_` functions and compiler-provided assertions. Test-runner isolation is a tooling choice, not a language feature to overdesign now. A failed test must not resume other tests in corrupted runtime state.

Self-hosting, fixed-point compilation, and benchmarks supplement these checks. They do not replace them.

## 22. Decisions to settle through examples

This revision removes speculative answers, not safety obligations. The remaining focused decisions are:

- **Counted lifetime details:** owning versus borrowed parameter passing, move/release timing, and value-container contracts. The class declaration and the prohibition on user-forced destruction of value objects are now settled.
- **Arrays:** parameter passing, slice/view semantics, root retention, and source read/write syntax for root-and-index references. Ordinary assignment has settled value semantics; mutable declarations use `var`.
- **Destruction:** exact cascade/detach ordering, cycles, and reentrant final-hook behavior.
- **Construction:** how the checker verifies attachment and failed-construction cleanup without hiding effects.
- **Closures and concurrency:** detailed capture-selection rules and concurrent lifetime control. Nonescaping concrete closures and loop-only single-yield generators are now the accepted initial model; escaping closures and independently resumable generators are deferred.
- **Language details:** numeric/lexical appendix, module assembly, and a visible iteration contract for abstract relations.

Each should be resolved with a small positive example, a negative example, and a reason for the chosen rule. We should not add a new type category merely because a paragraph is difficult to write.

The design's core remains small: ordinary classes and values; declared relations; ordinary constructors with checked outcomes; generated destruction and final hooks; inferred invalidation; safe iteration; checked root-relative array access; and interfaces that bind cooperating types and roles.

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
| 9 | `+`, `-` |
| 10 | `*`, `/`, `%` |
| 11 | unary `-`, `!`; `~` when explicitly implemented |
| 12 | member access, calls, indexing, postfix `!`, postfix `?`, `is`, `as` |

Binary arithmetic is left-associative. Logical operators short-circuit. Parentheses override precedence. Assignment is a statement, not a value-producing expression. The exact grammar of postfix casts/tests must disambiguate type expressions from neighboring operators; a precedence table is not a substitute for that grammar.

The baseline compound assignments are `+=`, `-=`, `*=`, `/=`. Additional bitwise and remainder compound assignments are specified extensions, not silently accepted placeholders. Every compound assignment evaluates its destination once and preserves lifetime checks across evaluation of its right side.

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

These forms define selection. Ordinary array assignment has value semantics as specified in Section 12. Slice/view sharing and retaining a slice across resize remain separate lifetime questions in Section 22. A compiler cannot implement unchecked interior pointers and claim that syntax alone makes them safe.

## Appendix B. Bindings, construction expressions, and control flow

### Bindings

```lyric
let x = 42
var count: i32 = 0
let x: i32
let _ = operation()
let (value, err) = operation()
```

A declaration without an initializer is permitted locally only when definite-assignment analysis proves initialization before use. A `let` binding may be initialized once, not subsequently changed. Module-level declarations require an initializer. `let` prevents changing the bound value, including array elements and length; `var` permits value mutation and rebinding. Neither makes referenced class objects immutable. `mut` remains reserved for mutable-reference parameters and call arguments. Constants and mutable globals need visibility and initialization-order rules before cross-module use is accepted.

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

An empty impl requests exact unambiguous derivation of remaining requirements. Mixed-mode impls explicitly bind some members and derive the rest. Diagnostics list every unsatisfied requirement and its expected signature. No fuzzy names, arbitrary widening, or declaration-order selection resolves ambiguity.

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
| `panic(message)` | Checked failure; recovery/termination contract must be fixed |
| `exit(code)` | Terminate process with explicit status |
| `itoa(i64)` | Decimal string |
| `atoi(string)` | `(i64, bool)` indicating conversion success |
| `parse_float(string)` | `(f64, bool)` indicating conversion success |
| `char_to_string(u8)` | One-byte string |
| `string_to_bytes(string)` | `[u8]`; sharing/write permission must follow settled string/array rules |
| `bytes_to_string([u8])` | String conversion with a specified ownership contract |
| `hash_string(string)` | `u64` hash; not equality |
| `sym(string)` | Interned symbol |
| `new_error(string)` | Concrete error value satisfying `error` |

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

`assert(condition, message)` fails the current test if false. `assert_eq(actual, expected, message?)` compares values and reports both, with the source file and line. A failing assertion stops that test; other tests may continue only in a runtime state known to be safe. Exit status is 0 if all selected tests pass and nonzero if any fail or compilation fails.

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

The comparison against the original specification is recorded in `spec-comparison.md`. The appendices above restore substantial concrete coverage but do not make this draft a finished specification. Outstanding details include a complete grammar, numeric conversions, array ownership, generator and concurrency semantics, final-hook reentrancy, foreign contracts, and library edge behavior.

A full specification must answer how to parse, type-check, evaluate, and clean up every supported construct, including interactions. It must state exclusions and errors, not require readers to discover behavior from the old compiler. KISS governs the number and interaction of mechanisms, not the amount of explanation needed to specify them accurately.
