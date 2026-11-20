# Lyric Language Specification

**Draft 3 — KISS revision, for Bill and Cadence to develop together.**

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

Reference counting is available for classes whose lifetime is not controlled by owning relations. It is not an excuse to omit relationships between components.

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

Newlines terminate statements, except inside parentheses and square brackets. A line ending in an operator continues the expression. Examples place declaration clauses on the same line as their declaration; no implicit continuation before a new clause is assumed.

Braces delimit blocks. `//` starts a line comment. Block comments are not necessary for the initial language.

```lyric
func add(a: i32, b: i32) -> i32 {
    return a + b
}

let x = 42
let mut count: i32 = 0
```

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

String escapes, triple-string whitespace, numeric conversion rules, and overflow behavior need one precise lexical/numeric appendix before implementation. They should not inherit accidental backend behavior. Nothing in this draft depends on selecting a surprising new rule for them.

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

There is one ordinary `class` construct. This draft adds neither `root class` nor immutable-only `value class`. A class is not forbidden from being a child merely because some use of it begins a graph.

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

## 9. Lifetime and reference counting

Classes participating as children in owning relations use relation-managed lifetime. Other classes may use reference counting, following Lyric's existing design direction. The compiler knows the lifetime regime from declarations; detachment must not silently change an object's regime.

Reference counting does not require immutability. It does require correct retain, release, and alias behavior. Value-like objects may omit holder backlinks where reference counting provides the appropriate lifetime.

When an array or field releases a reference-counted object, destruction at count zero has the same transitive destruction effects as any other destruction. The compiler cannot treat release as harmless merely because it was generated.

The interaction between explicit `destroy()` and counted aliases is not settled by class-wide local invalidation alone: stored counted aliases and their eventual releases also need a valid rule. Before supporting explicit destruction of counted objects, resolve that interaction. Two candidates are restricting explicit destruction to relation-managed objects or defining a checked alias-clearing mechanism. Neither is silently selected here.

A reference-count cycle can retain memory. Leak freedom is not identical to memory safety, and a ban on recursive types is not a justified substitute for a reclamation design. The spec must not claim that generated destruction alone prevents every leak.

The lifetime of independently created objects must be explicit, but does not require a special `root class` syntax. We should establish it alongside the counted-object rule above.

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

The earlier documents disagree about whether assignment copies array backing storage or shares it. This revision does not choose a new sharing model just to simplify writing the spec. That one behavior must be decided with Bill and demonstrated by a short example. Header copying, element copying, and parameter passing must not be conflated.

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

Closures and generators must obey the same lifetime rules as other code. Capturing or suspending a borrowed reference does not make it immortal. Nonescaping use can be checked locally; escaping captures and suspended state need a defined lifetime relationship.

Do not introduce a new capture taxonomy before testing those cases. Until an implementation can establish their safety, it must reject unsupported escapes rather than store a pointer to a dead local. This is an implementation gate, not a permanent assertion that useful closures are outside the language.

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
        let mut count: u64 = 0
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

- **Counted lifetime:** explicit destruction with counted aliases, independent object lifetime, and generated release behavior.
- **Arrays:** copy versus sharing on assignment, root retention, and the source read/write syntax for root-and-index references.
- **Destruction:** exact cascade/detach ordering, cycles, and reentrant final-hook behavior.
- **Construction:** how the checker verifies attachment and failed-construction cleanup without hiding effects.
- **Escapes and concurrency:** safe captured/suspended references and concurrent lifetime control.
- **Language details:** numeric/lexical appendix, module assembly, and a visible iteration contract for abstract relations.

Each should be resolved with a small positive example, a negative example, and a reason for the chosen rule. We should not add a new type category merely because a paragraph is difficult to write.

The design's core remains small: ordinary classes and values; declared relations; ordinary constructors with checked outcomes; generated destruction and final hooks; inferred invalidation; safe iteration; checked root-relative array access; and interfaces that bind cooperating types and roles.

## 23. Provenance and current status

The original reference, full spec, and newer interface redesign are preserved unchanged in `background/`. Earlier drafts are retained in `drafts/01-reconciled-design.md` and `drafts/02-expanded-proposal.md`.

This version was rewritten after Bill explicitly reaffirmed KISS. It removes the earlier root/value class taxonomy, attach clauses, finalization-view type, privileged Collection contract, separate view types, new declaration extension, and broad effect vocabulary. It also avoids treating a first implementation schedule as a permanent language restriction.

No compiler has been implemented or changed for this document. Full memory safety is the design goal, not an accomplished or formally proved result. The next work is to settle the focused semantic cases with Bill and then build bounded experiments that earn the final rules.
