# Lyric Next — Language Specification

**Status: fresh design draft for discussion; no implementation or safety proof claimed.**

This document describes a successor to Lyric that makes explicit relationships, deterministic destruction, static lifetime checking, and readable generic algorithms central language features. It is intended to stand on its own, not to require reconstructing a sequence of patches.

The design is Bill Cox's Lyric and relationship-oriented programming discipline, developed here with Cadence. The older documents credit Bill Cox and CodeRhapsody; their history remains theirs. “Lyric Next” and the directory name are working names, not a release designation.

## 1. Authority, scope, and reading conventions

This is a specification draft, not a report of an existing compiler's behavior. Unless explicitly identified as a historical fact, an unqualified rule describes intended behavior, not implemented behavior.

Three sources are preserved in `background/`:

- `lyric-language-spec.md`: the earlier full design and bootstrap status.
- `multi-class-interface-redesign.md`: the newer interface design, including subsequent implementation notes.
- `lyric-language-reference.md`: the earlier daily-use reference.

The redesign supersedes the old textual-prefix model of relation identity. The present discussion additionally establishes:

1. No garbage collector.
2. Static destruction-effect analysis invalidates local references by class type.
3. A safe foreach body may destroy only its current iteration object of the iterated class, not another object of that class.
4. Array-slot references retain a root array and an index path rather than a potentially stale element address.
5. All array indexing is bounds checked by default. An explicitly unsafe benchmarking flag may disable those checks.
6. Destructors are generated. A class may provide a `final` method that runs before structural destruction.
7. Reacquiring a child through a still-valid parent after an invalidating call is a normal programming pattern.

**Open decision** identifies a material question not yet settled with Bill. **Proposed rule** identifies a recommendation made to complete the draft; it is not silently elevated to an approved language change. Examples marked **schematic** communicate semantics without committing to new syntax.

The draft preserves the language's breadth but does not pretend all semantic questions are closed. Section 27 collects the decisions needed before this can serve as a complete implementation contract. In particular, concurrency, escaping captures, and array-root lifetime require further work before any full-memory-safety claim.

This is not authorization to rewrite Ensemble, modify the original Lyric project, or implement every feature described here.

## 2. Goals and non-goals

### 2.1 Goals

- Express cooperating classes and their relationships directly.
- Establish and maintain both ends of relationships through language-supported operations.
- Support multiple owning parents; ownership is not restricted to a tree.
- Generate structural destruction from declarations.
- Prevent access through invalid object references by static analysis.
- Keep array references safe across storage relocation through root-relative indexing and bounds checks.
- Allow efficient representation choices, including array-of-structures and structure-of-arrays, without changing application semantics.
- Make programs easy for humans and AI collaborators to inspect, explain, modify, and verify.
- Make errors refer to the programmer's declarations and calls, not merely generated C.
- Permit self-hosting, while treating self-hosting as a milestone rather than a correctness proof.

### 2.2 Non-goals

- A garbage collector.
- Classical implementation inheritance.
- Requiring a Rust-style programmer-visible lifetime-parameter discipline.
- Accepting unsupported syntax and emitting placeholder runtime behavior.
- Inferring that a successful benchmark proves general superiority or memory safety.
- Carrying historical compiler defects forward as normative language semantics.

Reference counting is permitted for appropriate non-relation-managed classes. It does not replace explicit relationship destruction and is not a tracing collector.

## 3. Architectural programming model

The language should make the intended object graph explicit and reconstructible.

A shared declaration layer defines vocabulary, state, relationship declarations, and externally callable operations. Functional implementation units provide behavior for those same declarations. They do not create wrapper types or copied state merely to attach behavior. Top-level orchestration composes the functional pieces.

This is the intended organizational model. **Open decision:** its exact package syntax and which dependency constraints are compiler-enforced, rather than tooling conventions. The old package model alone does not establish this architecture.

A relationship is a stored fact with two maintained ends, not an agreement between unrelated callers. A child reaches shared facilities through its actual parent relationships rather than stored copies of siblings or global lookup tables substituting for missing structure.

Value-like data need not have parent back-pointers merely because it occurs inside another value. Identity-bearing components participating in declared relationships do.

## 4. Source, packages, and modules

- Executable source uses `.ly`.
- A package is a directory of source files sharing a namespace.
- A module is a project identified by `lyric.mod`.
- Declaration and file order do not change name resolution.
- Bare top-level declarations are allowed. An optional `lyric Name { ... }` wrapper groups declarations but does not redefine the package's identity.
- Newlines terminate statements except inside parentheses and square brackets.
- Line comments begin with `//`.

```lyric
import ast
import parse from "parser"

func main() {
    println("hello")
}
```

Imported names are qualified. `pub` exports declarations; other declarations remain package-private. Recursive import resolution must either succeed or produce a source diagnostic; it must not silently ignore an imported package's own imports.

**Open decisions:** module-file contents, external dependency resolution, permitted import cycles, and the declaration/implementation-unit mechanism. The old documents simultaneously permit circular designs and propose cycle detection; detection alone does not decide whether cycles are legal.

Declaration-only `.lyric` files may describe the same types and signatures without bodies. CDD annotations and three-zone documentation are an external tool concern, not core language syntax. Whether to retain `.lyric` as a separately supported compiler input is open.

## 5. Lexical structure and literals

Identifiers are case-sensitive. Capitalization conventions have no type, visibility, or pattern-disambiguation semantics.

Core declaration and control keywords include `func`, `struct`, `class`, `enum`, `interface`, `impl`, `relation`, `type`, `where`, `let`, `mut`, `self`, `pub`, `owns`, `refs`, `if`, `else`, `match`, `for`, `in`, `while`, `return`, `break`, `continue`, `spawn`, `select`, and `yield`. `constraint` belongs to the revised constraint-alias design. A final grammar must reconcile this inventory with contextual names such as `final`, `trusted`, `field`, `lock`, and `gen`.

- Boolean literals: `true`, `false`.
- Null literal: `null`, not `nil`.
- Decimal integer literals allow separators, such as `1_000_000`.
- Unsuffixed integer literals default to `i32` when context does not determine another valid type.
- Floating literals include a decimal fraction, such as `1.5`; their default type is `f64`.
- Byte character literals include `'a'` and `'\n'`.
- Strings use double quotes; escapes include `\n`, `\r`, `\t`, `\\`, `\'`, `\"`, `\0`, and `\xHH`.
- Backticks intern symbols: `` `name` `` is equivalent to `sym("name")`.
- F-strings interpolate expressions: `f"count={count}"`; doubled braces produce literal braces.
- Triple-quoted strings preserve internal newlines. The older language trims boundary whitespace; preserving or changing that behavior is an explicit compatibility decision.

Hexadecimal, octal, binary, numeric suffixes, Unicode escapes, and block comments are extensions to specify before implementation, not features to accept as stubs.

## 6. Types

### 6.1 Scalar types

The initial numeric vocabulary is `i8`, `i16`, `i32`, `i64`, `u8`, `u16`, `u32`, `u64`, `f32`, and `f64`. Other basic types include `bool`, `string`, and `unit`.

`unit` is the no-result type; a function with no declared result returns `unit`. `error` is an interface, not a numeric or storage primitive.

Wider numeric types can be added when their semantics and implementation are defined. `int` and `uint` require an explicit interop width contract; the earlier documents disagree between platform width and C integer width.

### 6.2 Composite types

```text
T?                 optional T
T | U              tagged union
[T]                dynamic array/slice family; see Section 14
(T, U)             positional tuple
fn(T, U) -> V      callable type
gen T              generator result
channel<T>         channel
```

Tuple elements use `._0`, `._1`, and so forth. Destructuring uses `let (a, b) = pair()`.

Transparent aliases use `type Names = [string]`. An alias does not introduce a distinct nominal type.

The unfinished legacy `map[K]V` is not a supported runtime type in this design. Associative containers use `Dict<K,V>`.

### 6.3 Assignability and casts

Types must not become interchangeable merely because their generated representations happen to match. An `as` expression is a specified conversion, not an arbitrary reinterpretation escape.

The exact numeric promotion and narrowing table remains to be settled. It must define mixed signedness, overflow, division by zero, invalid shifts, and floating-to-integer conversion. The old compiler's permissive cross-sign assignments and unrestricted casts are not normative.

Optional wrapping from `T` to `T?` is implicit. A null literal needs a nullable target type. **Proposed rule:** nonoptional class references are not implicitly nullable; revise the old blanket class-null allowance explicitly if retaining it.

## 7. Bindings, values, and functions

```lyric
let answer = 42
let mut count: i32 = 0
let (value, err) = compute()
let _ = operation()

func add(a: i32, b: i32) -> i32 {
    return a + b
}
```

Bindings are immutable unless declared `mut`. Binding immutability and the mutability of an identity-bearing referenced object are distinct. Class methods may mutate the receiver without a separate `mut self` distinction.

Structs and tuples have value semantics. Passing a struct as `mut` permits mutation of the caller's value and requires `mut` at both declaration and call:

```lyric
func translate(mut p: Point, dx: i32) {
    p.x = p.x + dx
}
translate(mut point, 3)
```

A class parameter already denotes an object. Adding `mut` must never accidentally create a different pointer depth or an unsafe backend call. Whether it is rejected or accepted redundantly is a syntax decision.

External methods attach behavior to an existing type:

```lyric
func Node.name(self) -> string { ... }
```

Lambdas have explicit parameter types and may capture enclosing values. **Open safety decision:** escaping capture representation and lifetime restrictions. The earlier capture-by-pointer implementation is not a safe-language contract. A compiler must reject unsupported escaping cases rather than retain pointers to dead stack storage.

A single authoritative rule for array assignment is still required; Section 14 isolates the decision instead of repeating the old contradictory descriptions.

## 8. Structs, enums, and classes

### 8.1 Structs

Structs are identity-free values with named fields:

```lyric
struct Point { x: f64, y: f64 }
let origin = Point { x: 0.0, y: 0.0 }
```

They are copied as values and are not relation endpoints. The earlier language excludes methods on structs; whether to retain that restriction is a language-design decision, not necessary to relationship safety.

### 8.2 Enums and unions

```lyric
enum Shape {
    Circle(radius: f64)
    Rect(width: f64, height: f64)
    Point
}
```

Variants may carry typed payloads. Matching supports literals, variants, tuples, wildcards, bindings, alternatives, and guards. Nested patterns are permitted where their types are defined.

`is` tests a variant without binding. `if let` conditionally extracts a pattern. `let Pattern = value else { ... }` requires a diverging else branch.

Expression branches must have compatible result types. Nonexhaustive matching must not produce an uninitialized result. **Open decision:** whether all matches require exhaustiveness or some statement matches permit an implicit no-op case.

### 8.3 Classes

Classes have identity, state, methods, and explicit lifetime behavior. There is no classical inheritance.

```lyric
class Team { name: string }
class Player { name: string }
relation ArrayList Team:roster owns [Player:team]
```

Field-literal construction is suitable for objects with no required attachment or custom initialization. Explicit constructors initialize objects with invariants.

Constructors for components with required owning parents receive those parents and return with both relationship ends established. There is no caller-visible “construct now, remember to attach later” requirement. Failed construction must not leave a partially attached object visible.

**Open decisions:** syntax for required versus optional parent roles, fallible constructor results, and the compiler mechanism that enforces construction completion. The relationship declarations alone do not specify these distinctions.

## 9. Relationships and identity

A relation binds two endpoint roles. Its identity includes the participating types and their role labels, not merely the type pair.

```lyric
relation DoublyLinked Node:outgoing owns [Edge:source]
relation DoublyLinked Node:incoming owns [Edge:target]
```

An edge may have both owning endpoints. Destroying either node destroys its owned edge; destruction of that edge removes it from both endpoint memberships. Destroying an edge does not destroy its parents.

Ownership and representation are independent:

- `owns`: destruction of the parent cascades to children in this relation.
- `refs`: destruction of the parent unlinks the relationship without destroying its children.
- Destruction of a child detaches it from every parent relationship, owning or weak.

Relationship mutation updates both ends. Refused mutation leaves both ends unchanged. Direct writes that bypass those invariants are not part of the safe public relationship API.

### 9.1 Labels are scopes

```lyric
team.roster.append(player)
player.team.parent
```

`roster` and `team` identify scopes on their respective classes. They are not independently allocated objects and cannot be stored as ordinary values. `let view = team.roster` is not a reference to an adapter object.

Same labels on distinct classes are legal. Multiple roles on the same class must not collide. Unlabeled endpoints inject members directly into the class namespace. At most one fully unlabeled relation on a given type pair is allowed, and actual member collisions must be rejected even when the type pairs differ.

A self-relation is legal when its endpoint member names or labels remain distinct.

### 9.2 No implicit projection

`player.team` is a scope, not its parent. Write `player.team.parent`. Context must not silently convert a role into the object at the other end.

### 9.3 Representation hints

The initial library representations are:

- `ArrayList`: dense child array, parent backlink, child index; swap removal.
- `DoublyLinked`: parent head/tail, child next/previous and parent backlink.
- `HashedList`: dense members plus a hash index and maintained backlink/index.

Each representation supports both `owns` and `refs`. Cardinality is independent of destruction semantics. Single-child relationships need an explicit representation contract rather than being disguised as ordinary unmanaged fields; the precise one-to-one vocabulary remains open.

## 10. Generated destruction and finalization

Application programmers do not handwrite structural destructors. The compiler derives them from declared fields and relationships.

A class may supply a finalization method:

```lyric
class Connection {
    fd: i32
    final func cleanup(self) {
        close_fd(self.fd)
    }
}
```

The intended sequence is:

1. Execute the class's `final` hook while structural relationships still exist.
2. Perform generated cascade and unlink operations.
3. Release stored values according to their lifetime rules.
4. Release the object's storage.

A hook handles resources or application cleanup; it does not take over backlink maintenance. Its calls and destruction effects are analyzed like other code.

Relationship-library authors may define paired cleanup templates selected by `owns` or `refs`. This is different from asking every application class author to maintain a destructor. Such templates are part of the mechanism whose invariants must be verified.

Destruction must remove every membership exactly once, including when an object has multiple owning parents. **Open decisions:** allowed ownership cycles, reentrant destruction from final hooks, ordering among several relations, hook failure, and whether multiple final methods are permitted. These need a consistent state-transition contract; “generate destructors” alone does not settle them.

## 11. Static destruction effects

### 11.1 Class-wide invalidation

A function that can destroy an instance of class `C` has a destruction effect for `C`. Calls propagate that effect transitively.

After such a call, previously acquired ordinary local references to `C` are invalid. Using one is a compile error, even when the programmer believes a different instance was destroyed.

```text
let node = graph.nodes.first()
work()                       // may destroy Node
use(node)                    // rejected
```

This intentionally trades precision for simplicity. The compiler need not prove which arbitrary alias survived.

Effects include:

- Explicit destruction.
- Generated ownership cascades.
- Effects of final methods.
- Reference-count decrements that can destroy objects.
- Container removal or replacement that can cause such decrements.
- Calls through interfaces and callable values.

A class effect denotes the relevant concrete class, including generic instantiation. Whether a coarser generic-family effect is useful is an optimization/diagnostic question, not permission to omit concrete effects.

### 11.2 Control flow and calls

Validity is tracked through branches, loops, and calls. A reference is valid after a join only if valid on every incoming path. Recursive call groups require a fixed-point effect computation. An unknown callee must not be assumed harmless.

Function parameters, receiver references, aliases carried in local values, and references captured by closures must not escape the invalidation rule merely because their syntactic container differs. Exact tracking for aggregates and captures is an open part of the checker design.

An effectful function cannot return an invalid reference as a valid result. References freshly obtained after the destructive operation can be valid, provided their source is valid.

**Open decision:** effect annotation syntax and whether exported declarations expose inferred effect summaries, require written contracts, or both. `destroys C` is explanatory notation here, not a finalized grammar.

### 11.3 Reacquisition

A still-valid parent of another class can be used to reacquire a child after invalidation:

```text
work()                       // destroys Node, not Graph
let current = graph.nodes.first()
use_if_present(current)
```

This relies on the relationship having been updated by destruction. Reusing a stale child to rediscover its parent is not reacquisition.

### 11.4 Persistent references

Long-lived references between components belong in declared relationships so destruction updates both ends. Arbitrary stored references must not be allowed to evade lifetime enforcement.

**Open decision:** classification and syntax for ordinary object fields, relation-managed references, reference-counted fields, and borrowed transient values. The old language permits plain class fields; the new safety contract must specify which are legal and how each is protected.

## 12. Safe foreach

A safe iteration body may destroy only its current iteration object of the iterated class. It may not destroy another object of that class, directly, through a helper, through a cascade, or through reference-count release.

```text
foreach node in graph.nodes {
    if unwanted(node) {
        node.destroy()
        // node cannot be used again on this path
    }
}
```

The spelling `foreach` above is schematic; the original language uses `for`. The safety distinction needs a final syntax or a rule applying to all relation iteration.

The compiler needs an identity-restricted effect such as “destroys this argument,” in addition to a class-wide effect. A helper that only deletes the current object should be usable; an unrestricted `destroys Node` helper is insufficient proof.

Destroying the current object does not automatically make a cascade safe. If it owns another object of the same class, the cascade may violate the loop rule.

The iterator implementation must independently preserve continuation:

- A linked representation can save the next member before executing the body, provided that saved member cannot be destroyed or its required state invalidated.
- Swap-removal arrays must process the element moved into the removed slot rather than skip it.
- Hash-backed iteration must define its continuation through removal and rehash.

**Open decisions:** insertion, reparenting, arbitrary collection reorder, nested iteration, and collection destruction during iteration. Restrict unsupported mutations rather than claim that current-object-only deletion solves every iterator invalidation case.

## 13. Reference-counted and permanent objects

No tracing collector is used. Reference counting is available for appropriate classes outside relation-managed ownership, including selected value-like heap objects.

Removing a stored reference decrements its count; destruction at zero has ordinary destruction effects. Assigning or transferring references must preserve counts without exposing stale objects. Inferred moves may remove retain/release pairs only when semantics remain unchanged.

The earlier spec classifies lifetime mode by class participation in owning relations. **Open decision:** retain that exact class-level classification or define a more explicit declaration rule. Do not silently make objects switch lifetime regimes when detached.

Reference-count cycles, permanent allocation, and intentional retention are separate from use-after-free safety. If `permanent class` is retained, its non-reclaiming semantics must be explicit. Its interaction with owning relations must be specified, not merely warned about.

The language does not promise freedom from all memory leaks merely because destruction is generated.

## 14. Arrays, views, and array references

### 14.1 Resizing

All array indexing is bounds checked by default.

Growing an array initializes new elements to the defined empty value. Reference slots are nulled. Shrinking releases removed elements, decrementing counts for reference-counted class values. A decrement capable of destruction contributes destruction effects.

**Open decision:** how newly empty slots are typed when the element type is a nonoptional class reference. Runtime null initialization must not make a supposedly nonnull read unsound.

### 14.2 Object versus slot references

A reference to the object stored in `a[i]` differs from a reference to the slot `a[i]` itself. Relocating the array can invalidate the slot address while leaving the object alive.

A safe array-reference value contains:

- A reference to the root array object.
- The index path from that root to the selected slot.

For nested arrays, a reference to `a[i][j]` stores the root and both indices. Dereferencing resolves the current path and bounds-checks every step. It does not retain a raw interior pointer across possible relocation.

If shrinking removes the path, access fails the bounds check. This is a defined checked failure, not a use-after-free.

**Open decisions:** concrete type syntax, root lifetime enforcement, mutable versus read-only slot references, and behavior when a nested array is replaced. The natural positional interpretation follows the current contents at the saved path; that interpretation must be explicitly accepted rather than assumed to preserve the identity of an old element.

### 14.3 Optimization

When effects prove that intervening code cannot resize, replace, or otherwise invalidate the relevant storage along the path, the compiler may cache an address and eliminate repeated resolution. Bounds checks may be removed when proven redundant.

For multidimensional structures the proof covers every traversed level, not only the outer array. Optimizations preserve the checked program's semantics.

### 14.4 Unsafe benchmarking

An explicitly unsafe compiler flag may disable bounds checks for benchmarking. Its exact spelling is not selected here. Such executions are outside the safe-language guarantee. Build metadata and benchmark reports must distinguish them from checked executions.

### 14.5 Copy and view semantics — unresolved

The old documents variously describe assignment as copying backing storage, copying only the descriptor, and sharing on parameter passing. This draft does not choose among those conflicting rules.

Before implementation, define independently:

- Array assignment and nested value copying.
- Slice creation and backing-store ownership.
- Parameter passing.
- Element mutation versus descriptor mutation.
- `let ref` and `let mut ref`.
- Escape of a slice or view from its defining scope.

The root-and-index reference mechanism solves relocation of a slot; it does not by itself decide the copy semantics of every array value.

## 15. Strings and symbols

The initial string model is byte-oriented, with UTF-8 text conventions. Indexing produces a byte, and `len` counts bytes. Unicode code-point and grapheme operations must be separately named or explicitly specified; a byte index is not a character index.

Concatenation, slicing, searching, splitting, joining, trimming, and replacement are supported library operations. Their sharing/copying behavior follows the settled array/string storage contract, not an undocumented backend shortcut.

`Sym` is an interned symbol with a cached hash and stable identity for equal names. Interning can make equality cheap, but hash equality alone is never proof that two distinct names are equal. Collision resolution must compare actual keys or correctly interned identities.

`Dict<K,V>` requires hashing and equality. The exact `Hashable`/`Equatable` interface composition is open. The old deliberate preference for `sym()` rather than repeatedly hashing string keys is retained as a design direction, not an excuse for incorrect collision handling.

## 16. Interfaces: cooperating types

An interface can describe operations on several types:

```lyric
interface DirectedGraph<G, N, E> {
    pub func G.nodes(self) -> [N]
    pub func N.outgoing_edges(self) -> [E]
    pub func N.incoming_edges(self) -> [E]
    pub func E.src(self) -> N
    pub func E.dst(self) -> N
}
```

The interface describes a contract among types, not an inheritance hierarchy. Type parameters are explicitly declared. Each method identifies its receiver side.

Interfaces may supply default bodies that use their abstract operations. Binding must carry the complete set of participating types and roles into those bodies; inferring only the receiver's type is insufficient.

Effects are part of callable contracts. Implementations cannot introduce destruction effects that callers were promised absent. The exact effect-polymorphism mechanism is open.

## 17. Storage interfaces and labeled implementations

Interfaces can declare injected fields and default relationship operations. No separate `hint interface` keyword is needed.

```lyric
interface ArrayList<P, C> {
    field P.children: [C]
    field C.parent: P?
    field C.index: i32
    pub func P.append(self, child: C) { ... }
    pub func P.remove(self, child: C) { ... }
}
```

A relation hint has exactly two type parameters in parent/child order. Receiver-side declarations determine where members belong. Ownership-sensitive cleanup templates select cascade versus unlink behavior.

Labels attach to individual implementation type arguments:

```lyric
impl ArrayList<Team:roster, Player:team> owns { }
```

This is the underlying form of:

```lyric
relation ArrayList Team:roster owns [Player:team]
```

The relation spelling remains the convenient way to declare the graph. Lowering it must preserve ownership, labels, and cardinality; dropping a property during desugaring is not a valid simplification.

**Clarification requiring approval:** the redesign's “same-side state only” rule cannot prohibit a parent-side append operation from updating an explicitly supplied child's backlink. The intended useful restriction is receiver-correct access: a member on `P` is accessed through a `P` instance, a member on `C` through a `C` instance, with the same binding selecting both role scopes. This clarification is necessary to implement atomic two-ended mutations but has not yet been reviewed with Bill.

Whether capability requirements inject storage implicitly is unresolved. This draft recommends explicit storage-providing declarations and observational capability constraints, but does not label that recommendation as an accepted change.

## 18. Capabilities, constraint aliases, and bindings

The newer redesign separates storage from what algorithms require:

```lyric
interface Collection<P, C> {
    pub func P.iter(self) -> gen C
    pub func P.count(self) -> i32
    pub func C.parent(self) -> P?
}

constraint DirectedGraph<G, N, E> =
    Collection<G:nodes, N:graph> +
    Collection<N:out, E:source> +
    Collection<N:in, E:target>
```

A constraint alias names a conjunction of capabilities. It is not a new runtime type and supplies no storage by itself.

Role labels in constraints identify particular relationships. Type matching alone cannot choose between incoming and outgoing edges of identical endpoint types.

When application names differ, explicit bindings adapt vocabulary:

```lyric
impl Collection<Route:out, Via:source> = Collection<Route:a, Via:a>
```

A grouped implementation can collect the bindings for an algorithmic category. Alias bindings delegate operations; they do not duplicate the underlying relationship state.

The older per-member form remains useful:

```lyric
impl Graph<Net, Route, Via> {
    G.nodes = Net.all_routes
    E.weight = Via.weight
}
```

Field binding uses `<->` to identify existing storage, not to create a synchronized second copy. Inline bodies provide adapters where pure aliasing is insufficient.

Automatic satisfaction follows the redesign's order:

1. An explicit applicable implementation.
2. A unique exact name-and-signature match in the required scope.
3. Otherwise a diagnostic listing missing or ambiguous members.

No fuzzy matching or accidental numeric coercion chooses an implementation. Overlapping explicit implementations also need an ambiguity rule; “explicit wins” does not resolve two explicit candidates.

**Composition decision still open:** the later design retains `extends` and default algorithms inside interfaces, while the earlier redesign centers constraint aliases and constrained free functions. Both are preserved here as design material; the final language must decide which combinations are supported and why. `extends`, if retained, copies/substitutes contract surface; it does not introduce runtime object inheritance.

## 19. Methods, properties, and generic syntax

Methods are class-scoped functions. Presence of `self` distinguishes an instance method from a static function. Static factories use class-qualified calls.

The revised design proposes equivalent field/zero-argument getter forms:

```lyric
widget.name
widget.name()
Widget.greeting(widget)
```

A field and an unrelated user method cannot claim the same member name. Assignment invokes the appropriate setter where one exists.

**Open decisions:** whether all zero-argument methods admit property syntax, how to obtain a bound callable without invoking it, and how callable-valued fields are disambiguated. In particular, accidental property-style execution of an effectful method must have a deliberate answer.

Free functions can have explicit generic call arguments. The redesign prohibits `obj.method<T>(...)` while permitting `Class<T>(...)` and `Class<T>.factory(...)`. This rule is retained as proposed redesign syntax, with diagnostics directing users to explicit free-function calls when inference is insufficient.

Generic declaration syntax is inconsistent between the source documents (`func name<T>` and `func<T> name`). **Open decision:** select one canonical spelling before writing a grammar or mass-migrating examples. All examples of new generic algorithm declarations remain schematic until then.

Generic parameters are never invented from identifier capitalization or undeclared names.

## 20. Control flow, errors, and generators

Blocks introduce lexical scopes. `if`, `while`, `for`, `match`, `break`, `continue`, and `return` have structured control flow. Shadowing does not evade lifetime validity checks.

Error-returning functions use `(T, error)` by convention:

```lyric
func divide(a: i32, b: i32) -> (i32, error) {
    if b == 0 { return (0, new_error("division by zero")) }
    return (a / b, null)
}
```

The `?` operator propagates a nonnull error and otherwise extracts the success value. Every early exit participates in generated cleanup and lifetime analysis. Result recognition is based on resolved types, not accidental field-name strings.

There is no general `try`/`catch`/`throw` mechanism in the inherited design. Checked bounds failures and null unwrap failures need a precisely specified trap/panic contract; recovery behavior must not accidentally bypass required cleanup.

A generator returns `gen T` and yields values. Its suspended state owns or safely references what it needs. **Open decisions:** cleanup when iteration stops early, yielded-reference validity across resumes, and generator destruction effects. A Duff's-device implementation is one backend technique, not the semantic definition.

## 21. Operators and evaluation

Arithmetic, comparison, logical, bitwise, indexing, calls, casts, and optional operators are retained. Bitwise operators belong above comparisons, correcting the old C-style precedence trap in `a & 1 == 0`.

**Open decisions:** the full precedence table, order of operand and argument evaluation, overflow behavior, and the interaction between side effects and reference invalidation within one expression. These are safety-relevant: an expression that destroys an object and then accesses it cannot be accepted merely because the backend happens to evaluate operands in a favorable order.

Proposed principle: lower expressions into an explicit evaluation order before lifetime checking, preserving source locations. Reject unsafe ordering rather than depend on C's unspecified evaluation order.

## 22. Concurrency

The inherited vocabulary includes `spawn`, `channel<T>`, `select`, and scoped `lock`.

Full memory safety in concurrent code requires more than sequential destruction effects. Another thread must not free an object while a reference is being used. Likewise, array relocation and root/path resolution must not race without a defined synchronization discipline.

**Open design:** thread confinement, transfer, shared ownership, lock capabilities, or another statically enforced rule. Copying a class reference into a spawned task is not by itself safe sharing. `guarded_by` must either be checked or be clearly non-enforcing documentation.

Channel closure, send-after-close, draining buffered values, cancellation, task joining, and destruction of channel contents require explicit contracts. The old zero-value-only close behavior and polling select implementation are not adopted as settled semantics.

A safe initial implementation may reject shared mutable concurrency while this design is developed. That is preferable to claiming full safety for unchecked captures.

## 23. Compiler architecture and specialization

The compiler should demonstrate the language's relationship discipline. Its AST, scopes, symbols, bindings, intermediate representations, and passes must have declared ownership and necessary backlinks. Missing parents must not be replaced with global registries or repeated whole-program searches.

Recommended conceptual stages:

1. Parse source with precise locations.
2. Resolve modules and declaration identities.
3. Establish scopes, role bindings, and implementation identities.
4. Elaborate relationship and interface declarations.
5. Specialize concrete bindings and residual generic uses as required.
6. Check types, effects, construction, and reference validity.
7. Lower to typed executable representation with explicit cleanup.
8. Optimize while preserving proved obligations.
9. Emit a backend representation and build.

This is a conceptual dependency order, not a commitment to one pass count. Concrete relation bindings may elaborate early; remaining generics specialize when types and roles are known. Every executable specialization must be checked. Calling specialization “text substitution” must not excuse loss of declaration identity, role identity, hygiene, or diagnostic provenance.

Specialization keys include the relevant binding identity, not only a receiver type. Two different labeled relationships between identical classes must never reuse the wrong specialization.

Generated names are an emission detail. Semantic resolution uses explicit declarations and scopes.

The C backend is a practical starting point. AoS and SoA are representation choices with equivalent observable semantics in safe programs. The compiler must not depend on C undefined behavior to implement valid Lyric operations.

## 24. Standard library boundary

The initial library vocabulary includes:

- Relationship implementations: ArrayList, DoublyLinked, HashedList.
- Capabilities for collections, numeric operations, hashing, and equality.
- Dict and interned Sym.
- Strings and StringBuilder.
- Error values.
- File and process operations sufficient to build a compiler.

Library contracts specify ownership, destruction effects, mutation, error results, and bounds behavior, not only function names.

Minimal whole-file I/O is sufficient for bootstrap. Handle-based Reader/Writer and networking can follow. Resource-owning classes use final hooks for external handles and generated destruction for their internal graph.

Foreign functions and runtime helpers belong to an explicit trusted boundary. A trusted implementation must satisfy its declared safety/effect contract. Safe callers must not be able to forge arbitrary pointers or suppress destruction effects through ordinary casts. The exact FFI surface remains open.

## 25. Diagnostics and testing

A diagnostic should identify:

- The source operation that failed.
- The relevant declaration or earlier invalidating operation.
- The violated rule.
- A concrete legal alternative where one is known.

For stale references, point to both the use and the effectful call. For a safe-loop violation, identify the path that may destroy a noncurrent object. For an ambiguous binding, list the candidate roles rather than guessing.

Built-in tests retain `test_` discovery, `assert`, and `assert_eq`. The runner must define isolation and cleanup after assertion failure. A `longjmp` that skips live managed values is not automatically a valid implementation of test failure.

Minimum conformance cases include:

- Both-end attachment and mutation-free refusal.
- Destruction from either endpoint of a multiply owned child.
- Weak detach without cascade.
- Final hook ordering and transitive effects.
- Direct, indirect, recursive, interface-mediated, and RC-triggered destruction invalidation.
- Reacquisition through a valid parent.
- Branch joins and aggregate-contained aliases.
- Safe deletion of current items for each collection representation.
- Rejection of deletion of another same-class item, including hidden cascades.
- Array-slot references across growth, shrink, and multidimensional relocation.
- Bounds failures at every level of an index path.
- Optimized versus unoptimized equivalence.
- Same type pair under several relationship labels.
- Complete multi-class default-method specialization.
- Hash collisions between distinct keys.
- Invalid casts, nullability errors, and branch-type mismatches.
- Escaping captures and concurrent lifetime cases once those rules are designed.

Negative tests are part of the language contract. A compiler that emits unsafe code for a rejected example has not implemented that contract, even if it self-compiles.

## 26. Safety claim and development method

The intended guarantee is: a program accepted in safe mode, using conforming trusted runtime/library boundaries, cannot access freed or out-of-bounds storage through language operations. Bounds violations may terminate through a defined checked failure. Static analysis need not predict every out-of-range integer.

That guarantee has not been proved or implemented in this new project. It requires complete rules for all reference-producing and reference-invalidating operations, not merely success on examples.

Develop through bounded experiments. Implement a small behavior, test positive and negative cases, explain what the result establishes, then update this specification at the original construction point. Do not accumulate a separate patch chronicle as the only account of the language.

A useful first experiment is a small relationship graph with two owning parents, weak membership, static local invalidation, and generated destruction. A separate array-reference experiment can establish relocation and bounds behavior. These are suggestions for discussion, not an automatically authorized implementation schedule.

## 27. Decisions still required

The following are deliberately unresolved rather than hidden behind implementation assumptions:

1. Shared declarations and functional implementation syntax and dependency enforcement.
2. Required/optional parent roles and fallible construction semantics.
3. Ownership cycles, reentrant destruction, final-hook failures, and cleanup ordering.
4. Lifetime classification of ordinary fields, locals, RC classes, and permanent classes.
5. Effect syntax, exported contracts, parameter-specific destruction, aggregate aliases, and callable effects.
6. Array copy/view semantics, root lifetime, empty slots, nested replacement, and array-reference syntax.
7. Safe-loop syntax and permitted non-deletion mutations.
8. Escaping closures, generators, and early-exit cleanup.
9. Concurrent lifetime and synchronization discipline.
10. Constraint aliases versus extends/default-method composition; implicit satisfaction versus explicit impl requirements.
11. Field requirement versus storage injection; receiver-correct cross-side mutation.
12. Zero-argument property sugar, bound callable values, and generic declaration spelling.
13. Numeric conversions, overflow, full precedence, and expression evaluation order.
14. Nullable class references, runtime panic/trap semantics, and test isolation.
15. Modules, import cycles, foreign interfaces, and trusted boundary contracts.

These decisions do not negate the core design. They delimit what remains before “full specification” and “full memory safety” can be verified claims rather than goals.
