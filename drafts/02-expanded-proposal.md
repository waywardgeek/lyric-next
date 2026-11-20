# Lyric

## A language for explicit structure, checked lifetimes, and understandable programs

**Design proposal 2 — authored by Cadence for review with Bill Cox.**

This is the language I propose building. It is not a description of the old compiler, an implemented successor, or a completed proof of memory safety. It makes concrete choices so we can evaluate a coherent design rather than a collection of options. Choices beyond Bill's stated requirements are proposals, not retroactive claims of his approval.

The language is called **Lyric**. `lyric-next` is only this workspace's name.

---

## 1. Design contract

### 1.1 Requirements established with Bill

- Relationships are explicit and bidirectional. Objects may have multiple owning parents.
- Construction establishes required parent memberships before an object becomes usable by the caller.
- Relationship mutations maintain both ends; refusal leaves both ends unchanged.
- Structural destructors are generated, never hand-maintained by application programmers.
- A class may define a `final` hook that runs before its structural destruction.
- There is no garbage collector. Reference counting remains available for suitable value-like heap objects.
- A call that may destroy an instance of class `C` invalidates existing local references of class `C`.
- A safe foreach body may destroy only its current object of the iterated class, never another object of that class.
- A stable array-slot reference stores the root array and an index path, not an interior machine address.
- Indexing is bounds checked by default. An explicit unsafe benchmarking flag may disable those checks.
- Reacquiring a child through a surviving parent after an invalidating call is normal programming.
- The language should support shared declarations, functional implementations, and graph navigation through real parents.

### 1.2 Main proposals made here

1. Two explicit heap categories: identity-bearing `class` and immutable, reference-counted `value class`.
2. Persistent references to identity-bearing objects exist only in compiler-maintained relationships, including relationship-backed reference collections.
3. Local identity references are borrowed access paths. They do not prolong object lifetime.
4. Arrays have explicit object identity and sharing; `clone` is the explicit independent-copy operation. Array-slot references own their root storage, not the identity objects reached through its elements.
5. The initial language is sequential. It rejects shared-memory concurrency rather than claiming unimplemented safety for it.
6. Escaping closures capture owned values only; identity-object captures are permitted only in statically nonescaping calls.
7. Effects include destruction and storage invalidation. The compiler infers them, exports them, and checks explicitly stated upper bounds.
8. Multi-class interfaces support default algorithms. Constraint aliases bundle requirements but cannot contain implementations or inject storage.
9. Fields and actions remain visibly distinct. Ordinary zero-argument methods require parentheses.
10. Source expressions have a defined left-to-right evaluation order and checked integer arithmetic.

These are deliberately reviewable choices. In particular, array sharing, immutable value classes, restrictions on final hooks, and the initial concurrency exclusion differ from or narrow the older documents.

### 1.3 What safety means

For programs accepted in safe mode, using a conforming trusted runtime and foreign boundary, language operations must not access freed storage, use invalid object references, read uninitialized values, or access outside an array's bounds. A checked failure may terminate execution; memory safety does not require the compiler to predict every erroneous index.

The claim does not include freedom from leaks, deadlocks in external code, algorithmic errors, stack exhaustion, resource exhaustion, or malicious native libraries. Unsafe benchmark builds are outside this guarantee.

The rules below define the intended obligations. A formal soundness argument and compiler conformance work remain necessary before claiming those obligations are met.

## 2. A small example

Declarations describe the graph once:

```lyric
class Graph {
    name: string
    pub func prune(self)
}

class Node {
    name: string
    pub init(graph: Graph, name: string)
}

class Edge {
    pub init(source: Node, target: Node)
}

relation DoublyLinked Graph:nodes owns [Node:graph] required
relation DoublyLinked Node:outgoing owns [Edge:source] required
relation DoublyLinked Node:incoming owns [Edge:target] required
```

`required` applies to the child's parent role. A new Node requires its Graph. A new Edge requires both endpoint roles, even when both endpoints are the same Node instance.

```lyric
root class Program {
    pub init()
}
relation DoublyLinked Program:graphs owns [Graph:program] required
```

The example's constructor definitions are omitted, not implicitly implemented by these snippets. Section 8 defines how required attachments are supplied.

The relationships establish meaning:

- Destroying a graph destroys its nodes.
- Destroying a node destroys its incident edges.
- Destroying an edge removes both endpoint memberships.
- Destroying an edge never destroys its endpoints.
- No application destructor needs to remember those rules.

A class's fields and relationships are one authoritative model. A representation change from DoublyLinked to ArrayList does not change those lifetime rules.

## 3. Source organization and architectural boundaries

### 3.1 Modules

A module has a `lyric.mod` file containing a module name and explicit local dependency declarations. The initial toolchain does not fetch remote dependencies. Builds are whole-module and deterministic with respect to source contents, not directory enumeration order.

A module may use compact single-file source for small programs or architectural layout for larger programs.

### 3.2 Architectural layout

```text
lyric.mod
common/model.lyd
common/operations.lyd
parser/parse.ly
checker/check.ly
application/main.ly
```

`.lyd` contains shared declarations: types, fields, relations, interfaces, signatures, constants, and effect contracts. It contains no executable implementation bodies. Generic default bodies belong to an implementation unit, like other bodies.

A functional unit imports shared declarations and supplies matching definitions:

```lyric
implement common

pub func Graph.prune(self) {
    for node in self.nodes {
        if node.name == "unused" {
            node.destroy()
        }
    }
}
```

`implement common` attaches bodies to those declarations; it neither creates a wrapper type nor copies state. Each declared operation has exactly one implementation, except explicitly generic instantiations generated from that implementation.

Functional units cannot import implementation units merely to obtain another unit's objects or state. Calls to shared operations resolve through their declarations. Application units compose the program and select its entry point. Local private helper functions and helper value types are permitted inside a functional unit.

Dependency cycles among implementation files are irrelevant: shared callable declarations already resolve cross-functional calls. Module dependencies must be acyclic in the initial language. Cyclic object graphs within a module remain expressible.

The compiler can enforce this dependency structure. It cannot prove that an arbitrary boolean or integer duplicates another fact semantically; architectural discipline still requires judgment.

### 3.3 Compact mode

A single `.ly` file may contain declarations and their bodies for experiments and small libraries. Its semantics are identical to the split form. Splitting or combining files does not change type identity or visibility.

### 3.4 Visibility

Names are private to their declaring module unless marked `pub`. Fields are readable through allowed declarations but writable only within their owning implementation boundary or generated relationship operations. Publishing a type does not implicitly publish every field.

Mutable globals of identity-reference type are prohibited. A program stores persistent components in its root graph. Immutable module constants are allowed.

## 4. Lexical rules

Identifiers are case-sensitive. Case has no semantic role. Names do not become types, enum variants, or exports merely because they begin with a capital letter.

Newlines terminate statements. Parentheses and square brackets suppress newline termination. Semicolons may separate statements on one line. Braces delimit blocks. Comments use `//` or nestable `/* ... */`.

Literals:

- Integers: `42`, `1_000`, `0xff`, `0o755`, `0b1010`.
- Numeric suffixes: `42u64`, `1.5f32`.
- Floating point: `1.5`, `2.0e3`; `1.` is not a float literal.
- Booleans: `true`, `false`.
- Optional absence: `null`.
- Byte characters: `'a'`, `'\n'`, `'\xFF'`.
- Strings: `"text"`, with escapes `\n`, `\r`, `\t`, `\\`, `\"`, `\0`, `\xHH`, and `\u{...}`.
- Interned symbols: `` `name` ``.
- Interpolation: `f"name={name}"`, with `{{` and `}}` for literal braces.

Triple-quoted strings preserve their contents exactly, including boundary newlines. Trimming and dedentation are explicit library operations. This deliberately removes the old implicit trim.

Malformed source is rejected with a lexical or syntactic diagnostic. Unknown punctuation does not masquerade as an identifier.

## 5. Types and values

### 5.1 Scalars

The scalar types are `bool`, `i8`, `i16`, `i32`, `i64`, `u8`, `u16`, `u32`, `u64`, `f32`, and `f64`. `unit` is the no-result type. `string` is immutable byte storage, normally containing UTF-8 text.

No platform-dependent `int` appears in ordinary source. Foreign interfaces name exact widths or explicit platform ABI types.

Unsuffixed integer literals are mathematical constants during checking. Context may select a representable integer type; otherwise the default is `i32`. Unsuffixed floating literals default to `f64`.

Integer arithmetic is checked for overflow. Division by zero and invalid shift counts trap. Wrapping arithmetic is explicitly named, such as `wrapping_add`. Floating arithmetic follows IEEE behavior for its width, with no implicit fast-math mode.

Implicit widening is permitted within signed integers, within unsigned integers, and from `f32` to `f64`. Mixed signedness, integer-to-float conversion, and narrowing require explicit conversions. Checked conversions trap if the result is unrepresentable; named wrapping conversions make truncation explicit.

`as` does not reinterpret unrelated objects or bypass reference validity.

### 5.2 Composite types

```text
T?                 optional value
T | U              tagged union
[T]                dynamic array
(T, U)             tuple
fn(T) -> U         callable, including its inferred/declared effects
arrayref<T>        root-relative reference to an array slot
view<T>            read-only range view of array storage
relationview<C>    borrowed view of a declared relationship
```

Type aliases are transparent: `type Names = [string]` names an existing type expression.

Optional absence is represented explicitly. Nonoptional object references cannot be null. `x!` unwraps an optional or traps; `if let x = maybe` extracts it conditionally. There is no implicit nullable-receiver dereference.

A union's alternatives remain tagged. Matching does not reinterpret memory based on spelling or layout.

### 5.3 Structs and enums

Structs and tuples have value semantics. Fields are recursively copied according to their types; copying an array handle shares its array, copying a value-class handle retains its immutable value.

```lyric
struct Point { x: f64, y: f64 }
enum Shape {
    Circle(radius: f64)
    Rect(width: f64, height: f64)
}
```

Methods may operate on value types. Mutation of a caller's struct requires explicit `mut` at declaration and call.

A struct or enum containing an identity reference is a borrowed aggregate. It can be local or returned with valid provenance, but cannot be stored into an escaping ordinary value container. Its embedded references participate in class-wide invalidation.

## 6. Two kinds of heap object

### 6.1 Identity-bearing classes

`class C` declares identity-bearing objects whose lifetime is controlled by explicit destruction and owning relationships. Local `C` references do not retain them. Identity objects can be relation endpoints.

Persistent links to these objects are declared relationships. An ordinary field, array, closure, or global cannot hide an unmanaged `C` pointer. The compiler recursively checks aggregate element types to enforce this rule.

`C?` describes an optional temporary reference. Optional parent relationships expose `C?`; required parent roles expose `C` while their object is fully live and attached.

### 6.2 Immutable value classes

`value class C` declares an immutable reference-counted heap value. Construction produces a fully initialized value; copying retains it, releasing decrements it, and the last release invokes generated cleanup and an optional final hook.

There is no explicit `destroy()` on a value class. This avoids destroying an object while other owning value handles still exist, and avoids later decrements through freed handles. Value classes cannot contain identity references, root-relative mutable array references, or mutable arrays. They may contain scalars, immutable strings, immutable value classes, and immutable frozen collections.

The initial language prohibits recursive value-class storage cycles, including cycles through interfaces or containers. This is conservative and avoids an implicit tracing collector or leaking cyclic RC graphs. Recursive value data can instead use identity classes and explicit relations.

Value-class final hooks cannot destroy identity objects. Thus releasing ordinary value storage does not create hidden identity-graph destruction effects. The compiler still records resource-release effects and prevents resurrection.

This is a deliberately narrow, proposed role for reference counting. Mutable matrices and vectors use array-backed values or identity classes, not mutable aliased RC value objects.

### 6.3 Roots

A `root class` has no owning parent roles. It may own children and participate in weak relationships. Its instances are explicitly constructed and destroyed. A local reference to a root is still borrowed; leaving scope does not implicitly destroy it.

Root construction marks a deliberate lifetime boundary. Forgetting to destroy a root can leak memory but cannot make a reference dangle. Program termination reclaims process storage but is not promised to execute every final hook.

Root classes cannot be owning children. There is no hidden ownership token with an unverified cleanup obligation. The initial language favors explicit root cleanup over automatic scope-based root destruction.

## 7. Relationships

### 7.1 Syntax and endpoint roles

```lyric
relation ArrayList Team:roster owns [Player:team] required
relation DoublyLinked Room:guests refs [Guest:room]
relation Single Document:title owns Title:document required
```

The hint chooses representation. `owns` or `refs` chooses destruction semantics. Brackets denote multiple children. `required` means the child must have that owning parent role when construction returns.

For a `Single` relation, the parent holds at most one child. Empty parent-side slots are optional. The parent-role requirement on the child is independent of parent-side cardinality.

Each child has at most one parent instance per declared role. Several distinct owning roles are allowed, including roles with identical parent types.

### 7.2 Real scopes

A label introduces a scope on its class:

```lyric
team.roster.append(player)
player.team.parent
```

Scopes are compile-time name-resolution paths, not runtime adapter values. They cannot be stored or returned. They may be targets of defined syntax such as `for x in team.roster` and `len(team.roster)`.

No label means flat injection. All injected names must remain unambiguous. Same-letter labels on different classes are legal. Colliding roles on the same class are rejected even if their endpoint types differ.

`player.team` never silently projects to `player.team.parent`.

### 7.3 Mutation

Public relationship operations maintain both ends and return explicit errors for invalid attachment, wrong-parent removal, or cardinality conflict. Operations validate before mutation and publish no partial membership.

A required parent role cannot be left empty on a live object. Reparenting uses an atomic `move` operation. Optional owning roles may be detached without destroying the child. A detached object remains alive; it does not silently switch to reference counting. Objects with no required owning roles must be explicitly declared roots or use a future explicitly designed detached-object facility; the initial language does not allow accidental ownerless construction of ordinary classes.

Direct mutable access to relationship backing arrays, backlinks, indices, or sibling links is prohibited outside the trusted representation implementation. Public iteration exposes borrowed elements, not a mutable backing-storage escape.

### 7.4 Weak references and reference collections

A weak `refs` relation unlinks when either endpoint is destroyed but never cascades from parent to child. Weak membership is semantic, not a language-level weak-pointer object layered over unchecked raw fields.

Collections that store identity references must be relationship-backed. The standard `ReferenceArray` is a weak relation representation whose slots are cleared or removed according to its declared policy when targets die. Each membership, including duplicate references where permitted, has its own reverse-link bookkeeping. Ordinary `[Node]` storage is not a loophole: it denotes borrowed local collection data only when statically nonescaping; use a relationship-backed collection for persistent or escaping storage.

## 8. Construction

Constructors use `init`; calls use the class name. Their headers explicitly bind required parent roles before the initializer body runs:

```lyric
pub init Node(graph: Graph, name: string)
    attach graph = graph {
    self.name = name
}

pub init Edge(source: Node, target: Node)
    attach source = source, target = target {
}
```

The left name in an `attach` clause is the child's declared role; the right expression is the parent instance. Each required role appears exactly once. Both endpoints of every attachment are established before the body can navigate them.

A constructing object is not an ordinary live reference. It cannot escape, be yielded, be captured by an escaping closure, or be passed to arbitrary callbacks. Its initialization helpers must be statically nonescaping. Parent collections cannot expose the object to unrelated observers during initialization.

Constructor attachment and initialization form a compiler-controlled transaction. On ordinary returned error, initialized owned values and children are released and all attachments are reversed. The incomplete object's final hook is not called. External resource acquisition must transfer into initialized resource-owning fields or explicitly release on failure; the compiler does not undo arbitrary external effects.

Allocation failure and panic terminate the process in the initial language rather than attempting exception unwinding. Ordinary fallible construction returns `(C?, error)`; success returns a live nonnull object and null error, failure returns null and an error. A nonfallible constructor returns `C`.

Constructor checking proves definite initialization and complete required attachment. A missing attachment is a source error, not a caller convention.

## 9. Destruction

Every identity class has generated `destroy()`. There is no application-authored destructor body.

The destruction algorithm is:

1. Enter the object's finalization state.
2. Invoke its `final` hook, if present, while its relationships still exist.
3. Unlink all incoming parent memberships, both strong and weak.
4. Repeatedly select a current outgoing owning child and invoke its destruction; that child runs its final hook before detaching itself.
5. Unlink remaining outgoing weak memberships without destroying their targets.
6. Release stored values and external-storage containers.
7. Free the object storage.

The algorithm reads current membership state after each recursive destruction. It does not retain a stale list of child pointers that a previous cascade may have destroyed. A selected membership is not removed by the caller before the child's final hook; the child's generated detach performs that removal.

Unlinking incoming memberships before cascading breaks ownership cycles encountered along a destruction path. Multiple parents and diamonds are handled by the child's removal from every parent before storage is freed. The algorithm and its representation-specific implementations require explicit conformance tests for cycles, self-edges, duplicate endpoint roles, and diamonds; this text is a proposed algorithm, not a proof of its implementation.

Among distinct roles, cleanup follows declaration order. Within a relation, destruction order is representation-defined unless the relation declares an ordered contract. Programs must not rely on unspecified sibling-finalization order.

### 9.1 Final hooks

```lyric
final func Connection.close_resource(self) {
    close_fd(self.fd)
}
```

At most one final hook is allowed per class. It is nonfallible and cannot resurrect or publish `self`. It may inspect relationships and release external resources. During a cascade, a reachable ancestor may already be in structural teardown. Final-hook access therefore uses a read-only finalization view: parent-role reads are optional even for normally required roles, and no fully-live construction invariant is assumed for ancestors. Storage remains valid throughout these reads; structural mutation and escape are forbidden.

**Proposed restriction:** final hooks cannot structurally mutate or destroy the identity graph, directly or transitively. They may update their own nonrelationship scalar/resource fields and call conforming external cleanup operations. Generated destruction performs graph cleanup afterward. This restriction avoids reentrant destruction and mutation of a partially finalized graph. It narrows the earlier discussion's more general effectful hook; Bill should explicitly review it.

A panic in a final hook terminates execution. It does not resume with a half-destroyed graph.

## 10. Destruction effects and local reference validity

### 10.1 Effects

The compiler infers effects transitively. Public interfaces export effect summaries, and source may state upper bounds:

```text
pure
reads
mutates
destroys C
resizes A
```

These lines name effect concepts; the canonical source form is `effects(...)` after the result type or parameter list:

```lyric
pub func Graph.prune(self) effects(destroys Node, destroys Edge)
```

For a function with a body, a missing annotation requests inference, not an empty effect set. Abstract interface methods, foreign declarations, and callable parameters without a statically known implementation must state effect bounds explicitly. A separate declaration of a same-module function may obtain its inferred summary from its uniquely linked body. An explicitly pure function must meet its promise. Unknown effects are not assumed safe.

Recursive call groups and destruction cascades are analyzed to a fixed point. Conservative overapproximation is allowed; omitted effects are not.

### 10.2 Invalidation

After a call with `destroys C`, every previously acquired borrowed local reference to `C` becomes invalid. This includes parameters, `self`, optionals, tuple/struct/enum members, borrowed collections, and nonescaping closure captures.

```lyric
let node = graph.nodes.first()
graph.prune()
println(node!.name)     // rejected: Node references invalidated
```

A type-level kill is intentionally coarse. No claim that a different instance probably died makes a stale reference valid.

References in function results are checked against the callee's exit state. Fresh results created or reacquired after the last relevant destruction may be valid. A function cannot return a stale argument merely because returning does not dereference it.

At branches, validity is intersected across incoming paths. Loops are checked to a fixed point. Assigning a newly acquired valid reference reestablishes that variable's validity; copying an invalid reference is prohibited.

### 10.3 Reacquisition

```lyric
graph.prune()
let current = graph.nodes.first()
if let n = current {
    println(n.name)
}
```

This is valid if Graph itself is not invalidated. A cascade that may destroy Graph invalidates `graph` too.

Temporary borrows do not generate reference-count decrements at scope exit. Invalidating one therefore cannot leave a hidden future release through stale storage.

### 10.4 Evaluation order

Calls, arguments, and operands evaluate left to right. Lifetime checking occurs over this explicit order. In `f(node, destroy_something())`, the first argument is a held borrow across evaluation of the second; if the second invalidates its class, the call is rejected.

Generated temporaries obey the same rules as named locals. Lowering cannot hide a borrow from the checker.

### 10.5 Persistent graph integrity

Class-wide local invalidation complements, rather than replaces, backlink maintenance. Generated destruction removes persistent references before storage is freed. Safe code cannot store an untracked borrowed identity reference in ordinary heap storage.

These two obligations close different paths to dangling references. A compiler must enforce both.

## 11. Safe iteration

`for item in relation` is safe relationship iteration. Its body may destroy only the current `item` of the iterated class. Other local references of that class are invalidated by such a deletion as usual.

```lyric
for node in graph.nodes {
    if node.name == "unused" {
        node.destroy()
    }
}
```

Helpers may declare a parameter-restricted effect:

```lyric
func remove_node(node: Node) effects(destroys only node) {
    node.destroy()
}
```

The checker verifies that every same-class destruction is rooted in that parameter's identity. Aliases provably equal to the parameter are permitted. Arbitrary class-wide destruction does not satisfy `only`.

Effects on other classes still propagate. If deleting a Node cascades to a different Node, the function cannot claim `destroys only node`; the safe loop is rejected. Deleting incident Edges is permitted when no cascade can delete another Node.

During a safe relation loop, insertion, reordering, reparenting, resizing of the iterated collection, and destruction of the collection owner are prohibited except for generated removal of the current element. This deliberately limits the initial iterator contract. More permissive iteration modes require separately proved semantics.

The compiler/representation implementation preserves continuation. Linked iteration can cache a successor protected by the effect restriction. Swap-removal iteration revisits the slot receiving the moved element. Hash representations must provide a deletion-safe continuation algorithm. A generic “increment index” is not sufficient for every representation.

Nested loops retain separate current identities. An inner deletion must satisfy all enclosing live iteration obligations, not merely the innermost one.

## 12. Arrays and root-relative references

### 12.1 Array objects

An array `[T]` is a mutable storage object with length and capacity. Copying its handle shares the same array object and retains its storage. `clone` creates independent storage, recursively copying element values according to their types. Deep cloning of nested arrays is a separately named operation.

This is an explicit choice replacing the old contradictory “value slice” descriptions. Arrays are not silently deep-copied on ordinary assignment.

The array root's storage is reference counted. The initial type rules prohibit cycles of array ownership: array element types cannot recursively contain the array through dynamic `any`, mutable closures, or recursive container types. Identity graph cycles use relations instead. A future relaxation needs a reclamation design, not an invisible collector.

### 12.2 Resizing and initialization

All indexing checks `0 <= index < length` using the defined index integer type. New scalar/value elements receive their defined zero/default value. New reference slots are null, so nullable slot types are required where null initialization occurs. A nondefaultable element array must grow through an initializer-producing operation, not expose uninitialized entries.

Shrinking releases removed value handles. If a future permitted element type's release can destroy an identity object, its destruction effect must propagate. In this proposal's initial split, ordinary value-class releases cannot destroy identity objects; owning identity collections use relationship destruction instead.

### 12.3 Slot references

`&a[i]` produces `arrayref<T>` rather than a machine pointer. For nested indexing, `&a[i][j]` retains the root `a` and the full index path `[i,j]`. The syntax is restricted to array paths; it does not expose arbitrary addresses.

```lyric
let slot = &matrix[row][column]
resize_work(matrix)
slot.set(7)
```

The arrayref retains the root storage. Each `get()` or `set(value)` traverses the current path and bounds-checks every index. If a path no longer exists, it traps. Replacing an inner array changes what the path denotes: this is a positional reference, not an identity guarantee for the old element.

Reading a relationship-backed identity slot produces a fresh borrowed object reference to the current live target or null. The arrayref itself does not make the old target immortal. Such slots require the ReferenceArray bookkeeping described earlier; raw identity arrays cannot bypass it.

Nested array traversal retains intermediate storage for the duration of the operation. No user callbacks run between resolving a slot and completing the load/store. The sequential core prevents another thread from relocating it concurrently.

### 12.4 Views

A `view<T>` retains its root and a range. Access checks the range and the root's current bounds. Views are read-only; mutable slot access uses arrayref. Views do not promise to freeze contents.

### 12.5 Optimization and unsafe mode

An effect proof may show that no intervening operation can resize or replace any array along a path. The compiler may then cache an address and remove redundant path resolution. It may eliminate bounds checks only when proven redundant in safe mode.

`--unsafe-no-bounds-checks` is the benchmarking escape hatch. The executable and compiler report identify unsafe mode. Results obtained with that flag must not be described as measurements of checked safety overhead without also reporting the checked baseline.

## 13. Bindings, functions, and closures

`let` creates an immutable binding; `let mut` permits rebinding. An immutable binding to a mutable array or identity object does not freeze that object. Read-only access is a capability/effect contract, not a misleading binding convention.

Generic declarations use one canonical spelling:

```lyric
func identity<T>(x: T) -> T { return x }
```

Parameters and result types are explicit; local types may be inferred. Unused results may be discarded with `_`. Definite assignment is checked on every path.

Struct mutation uses `mut` on both sides. `mut` on identity-class parameters is rejected as unnecessary; it never creates a double-pointer ABI by accident.

Methods are class-scoped functions with explicit `self`. Static functions omit it. Method calls require parentheses. Field getters may satisfy zero-argument interface requirements, but that does not turn arbitrary methods into implicitly executed properties.

Free-function syntax and method syntax may select the same declared operation. Explicit generic arguments are allowed on free calls and class constructors/static functions; instance methods infer from receiver, arguments, and expected result. Ambiguous inference is a diagnostic, not a heuristic choice.

Escaping closures capture scalars, strings, frozen values, and permitted owned arrays by value/handle. They cannot capture borrowed identity references or mutable local variable addresses. Recursive closure ownership cycles are prohibited. Nonescaping callbacks may borrow identity references, and their effects are checked at each call. A `noescape` callable parameter cannot be stored, returned, or passed to an escaping callee.

## 14. Control flow, patterns, and failure

Blocks introduce lexical scope. `if`, `while`, `for`, `break`, `continue`, and `return` are structured. `if` and `match` expressions require consistent result types; branches do not silently take the first branch's type.

Pattern matching supports variants, tuples, literals, alternatives with identical bindings, guards, and `_`. Matches must be exhaustive unless an explicit wildcard handles the remainder. Guarded arms alone do not establish exhaustiveness.

`let Pattern = value else { ... }` requires a diverging else branch. Pattern recognition is based on resolved declarations, not capitalization.

Fallible operations conventionally return `(T, error)`. `error` is an immutable value interface with `message() -> string`; error values cannot retain borrowed identity references. Optional absence of error is built into the result convention and is not blanket nullable class behavior.

`?` propagates the error and releases owned local value storage along that exit. There are no recoverable exceptions in the initial language. Bounds failure, invalid unwrap, failed assertion outside tests, arithmetic trap, and `panic` terminate the current process.

The test runner executes tests in isolated processes so an assertion trap does not skip cleanup and then continue using the same potentially damaged heap.

## 15. Operators and conversions

From weakest to strongest:

1. `||`
2. `&&`
3. `==`, `!=`
4. `<`, `<=`, `>`, `>=`
5. `|`
6. `^`
7. `&`
8. `<<`, `>>`
9. `+`, `-`
10. `*`, `/`, `%`
11. Unary `-`, `!`, `~`
12. Field access, calls, indexing, optional unwrap, propagation, `is`, and `as`

Thus `a & 1 == 0` means `(a & 1) == 0`. Logical operators short-circuit. Compound assignments evaluate the destination once, including array paths and their checks. Integer operators require compatible integer types and follow the checked arithmetic rules.

Object equality compares live identity. Comparing an invalidated reference is prohibited even if no fields are read. Value equality compares the specified value semantics; hashing must be consistent with equality.

## 16. Interfaces and cooperating types

Interfaces express contracts over one or several types:

```lyric
interface GraphView<G, N, E> {
    pub func G.nodes(self) -> relationview<N> effects(reads)
    pub func N.outgoing(self) -> relationview<E> effects(reads)
    pub func E.target(self) -> N effects(reads)
    pub func G.edge_count(self) -> u64 effects(reads)
}
```

A `relationview<C>` carries a borrowed owner and a resolved relationship identity, not an owning array handle. Its result provenance is tied to the producing receiver or an explicitly named argument. It cannot be stored in escaping value storage. Owner destruction, destruction of class C, or structural mutation of that relationship invalidates an existing ordinary view. The compiler-controlled safe-loop continuation is a separate construct with the narrower rules of Section 11; it is not an ordinary view exempted from invalidation.

Default bodies are defined against the interface's abstract operations in a matching implementation unit. They are specialized for each binding. Abstract operations, defaults, and effects are one contract; a default does not erase its transitive effects.

No classical inheritance or implicit runtime IS-A relation is introduced. Generic interfaces are resolved statically. The initial language omits a general dynamic `any` container; heterogeneous values use tagged unions or explicitly erased immutable value interfaces such as `error`.

Single-parent `extends` composes interface requirements and defaults. Redeclaring a member requires the same type/effect contract or a stricter effect bound. A child default may override the inherited default explicitly. It does not create a subtype conversion between runtime identity objects.

## 17. Capabilities and constraint aliases

Small interfaces describe what an algorithm needs:

```lyric
interface Collection<P, C> {
    pub func P.count(self) -> u64 effects(reads)
    pub func C.parent(self) -> P? effects(reads)
}

constraint DirectedGraph<G, N, E> =
    Collection<G:nodes, N:graph> +
    Collection<N:outgoing, E:source> +
    Collection<N:incoming, E:target>
```

`Collection` is an initial compiler-known capability with a required relationship witness in addition to these two callable requirements. A pair of arbitrary count/parent methods alone cannot satisfy it. The witness identifies both endpoint roles and the audited representation's checked iteration operation. The language's `for` elaboration uses that operation with the lifetime and mutation rules of Section 11, rather than exposing an unrestricted escaping generator.

A constraint alias contains no fields, methods, or bodies. It is only a conjunction of requirements. This gives `extends` and aliases distinct jobs: extends creates a reusable interface contract with defaults; an alias names a set of requirements without creating a new implementation surface.

A where-clause checks an existing capability. It never silently injects storage into an argument's type.

```lyric
func count_nodes<G, N, E>(graph: G) -> u64
    where DirectedGraph<G, N, E> {
    return graph.nodes.count()
}
```

The full relation binding, including endpoint roles, is part of constraint satisfaction. The compiler must reject two plausible bindings rather than select whichever was registered last.

## 18. Implementations and storage injection

An impl binds abstract participants and operations to concrete ones:

```lyric
impl GraphView<Net, Route, Via> {
    G.nodes = Net.routes_view
    N.outgoing = Route.outputs_view
    E.target = Via.destination
}
```

A field may satisfy an immutable getter requirement. A field binding identifies existing storage; it does not maintain a second synchronized copy. Inline adapter bodies are permitted and have ordinary effects.

Explicit impls take precedence over automatic derivation. Two overlapping explicit impls are an error unless they are the same declaration reached through duplicate imports. Automatic derivation requires exact names and compatible signatures/effect bounds in the requested scope. Missing or multiple candidates produce one diagnostic listing the unresolved requirements.

Labels belong to each type argument:

```lyric
impl Collection<Route:outgoing, Via:source>
    = Collection<Route:a, Via:a>
```

This delegates a role to an existing role; it does not inject another collection. Grouped bindings may collect several such aliases under a constraint name, with no extra runtime representation.

Storage injection is explicit through a storage-providing impl or relation. Storage interfaces declare endpoint fields and methods; application capability requirements do not. The ordinary `interface` keyword remains shared, but `field` declarations that provide storage can only be instantiated at explicit storage bindings.

A relation is sugar for a storage-providing impl carrying labels, cardinality, required-parent obligations, and ownership semantics. All of these survive elaboration.

## 19. Relationship implementations and trust

Built-in hints are library definitions backed by a small trusted mechanism for atomic field/link mutation. A custom hint must declare exactly two participants and assign each field and receiver to the correct participant.

A parent-side operation may update a child-side member through an explicit child argument. Both accesses use the same relation binding. It may not pretend a child field belongs to its parent receiver.

User-defined representation code does not become trusted merely by writing `trusted`. Trust is granted by the build configuration to audited runtime modules. Safe application code cannot bypass backlink maintenance through injected-field names or manufactured casts.

A custom representation must satisfy:

- Both-end attachment/removal.
- Failure without partial mutation.
- Correct destruction templates for both owns and refs.
- No hidden unmanaged references.
- Correct iterator continuation.
- Defined collision handling where hashed.

The initial compiler may support only audited standard hints until validation/tooling for arbitrary representations is sufficient. This is a boundary on the safety guarantee, not a claim that structural shape checking proves an arbitrary linked-list implementation correct.

## 20. Standard values and containers

`string` is immutable and reference counted. Byte indexing returns `u8`; byte length is explicit. Unicode scalar iteration, normalization, and case conversion are separate library operations with documented invalid-UTF-8 behavior. Byte-oriented source strings may contain arbitrary bytes.

`Sym` interns immutable strings, caches a hash, and compares canonical identities. Interning resolves hash collisions by actual byte equality. Equal hashes do not imply equal names.

`Dict<K,V>` requires consistent hash and equality operations. Value dictionaries cannot hide borrowed identity references. Dictionaries of persistent identity objects use relation-backed storage. Mutable key state cannot change while a key is stored; keys are immutable values.

`StringBuilder` is a mutable array-backed builder, not an immutable value class. Converting it to string produces immutable storage, copying or transferring only when uniqueness permits without changing observable sharing.

File, socket, and other resource handles are identity classes attached to an explicit resource owner. Their final hooks close external resources. Reader and Writer capabilities describe byte operations and their fallible results; buffering does not change ownership invisibly.

## 21. Initial language boundary

The initial safe language includes sequential execution, ordinary functions, nonescaping callbacks, owned-value escaping closures, structured loops, and explicit graph lifetime management.

It does not include:

- Shared-memory `spawn`, channels, or locks.
- Arbitrary pointer arithmetic or address-taking outside arrayref construction.
- General resumable generators retaining borrowed identity references.
- Dynamic mutable `any` containers.
- Recoverable exceptions.
- User-defined unsafe code in ordinary modules.

These constructs are rejected, not parsed into placeholders. Concurrency and generators can be added later only with defined reference, destruction, transfer, cancellation, and cleanup contracts. Their absence is a concrete scope decision, not an assertion that they are impossible.

Foreign calls are available through audited signatures declaring ABI, ownership, escape, and effects. A lying foreign signature can violate safety, just as an incorrect runtime can; the boundary is explicit in the build and diagnostics.

## 22. Compiler pipeline and internal architecture

The compiler itself must use declared parents and relationships for its persistent model. AST children know their structural parents; symbols know their declaring scopes; bindings retain their interface and participant roles. Functional passes navigate those relationships instead of reconstructing them from global maps.

Suggested semantic stages:

1. Lex and parse with durable source locations.
2. Load module declarations and implementation units.
3. Resolve declaration identities, scopes, types, and relationship roles.
4. Elaborate constructors, relations, interfaces, and bindings.
5. Instantiate concrete generic uses while preserving binding identity.
6. Infer effects to a fixed point and validate declared bounds.
7. Check construction, borrowing, invalidation, iteration, and escape rules.
8. Lower to typed operations with explicit evaluation and cleanup order.
9. Optimize under the established proofs.
10. Emit C or another backend and link the audited runtime.

Stages may iterate or interleave, but no executable specialization escapes checking. Name mangling is a backend detail, never the semantic identity of a relationship.

Specialization keys include all participating types and relevant role/impl identities. The receiver alone cannot determine a multi-class default algorithm. Substitution operates over resolved syntax or IR, not unhygienic text replacement.

The C backend must preserve checked arithmetic, evaluation order, reference rules, and bounds behavior. C undefined behavior cannot implement a valid Lyric operation.

AoS and SoA are layout options for the same language. Slot indices must not wrap into live objects, and backend optimizations cannot omit static invalidation merely because debug UAF detection exists. A debug sentinel is diagnostic support, not the safety model.

## 23. Compiler explanations

The compiler provides structured queries as well as human diagnostics:

```text
lyric explain type Node
lyric explain effects Graph.prune
lyric explain binding Net GraphView
lyric explain destruction Edge
```

These reports come from the authoritative compiler model. They show actual parents, children, effect paths, selected bindings, and generated cleanup responsibilities, with source locations.

An invalid-reference diagnostic identifies the reference's origin, invalidating call, and attempted use. An effect diagnostic explains a transitive path. A binding diagnostic lists candidates by role and declaration. Errors in synthesized code point back to the user's binding or call and the originating interface requirement.

The goal is reconstruction: a human or AI should be able to explain why the program is accepted or rejected without reverse-engineering generated C.

## 24. Toolchain and tests

```text
lyric check <module>
lyric build <module> -o program
lyric test <module>
lyric fmt <paths>
lyric explain ...
```

`check` performs semantic and safety checks without generating a binary. `build` never treats checker errors as warnings and continues into unsafe output. Unsupported features fail with explicit diagnostics.

Tests are no-argument `test_` functions. `assert` and `assert_eq` report source positions and values. Each test runs in an isolated process in the initial runner, so a trapping test cannot leave live runtime state for the next test. Negative compile tests assert rejection and the relevant diagnostic category.

A minimum conformance suite exercises:

- Required attachment, duplicate endpoints, failed construction, and rollback.
- Parent and child destruction, weak detach, cycles, self-relations, and diamonds.
- Final-hook ordering and forbidden reentrancy effects.
- Class-wide invalidation through direct calls, recursive calls, interfaces, callbacks, and generated cascades.
- Aggregate aliases and explicit left-to-right argument evaluation.
- Fresh-result and parent-based reacquisition.
- Current-only deletion and forbidden noncurrent destruction for each representation.
- Array references across growth, shrink, nested replacement, and relocation.
- Root retention and bounds failure at every index-path level.
- Value-count release, nonescaping captures, and rejection of escape loopholes.
- Several same-type relationship roles and multi-class default specialization.
- Hash collisions, checked arithmetic, nullability, and exhaustive matching.
- Safe/optimized equivalence under AoS and SoA.

Self-hosting and fixed-point compilation are additional milestones, not substitutes for these tests.

## 25. Safety obligations and implementation gates

The design has four complementary obligations:

1. **Persistent integrity:** all stored identity references participate in maintained relationships.
2. **Temporary validity:** destructive effects invalidate every affected borrowed access path before reuse.
3. **Storage access:** root-relative references retain storage and check their current index paths.
4. **Boundary integrity:** trusted runtime and foreign implementations honor declared contracts.

The sequential restriction removes concurrent invalidation from the initial proof obligation. Restricted value storage and closure escape prevent hidden aliases from bypassing the model. Explicit root lifetime makes leaks possible but does not justify unsafe reclamation.

Before claiming full memory safety, the implementation needs a formal or comparably rigorous argument covering these obligations, adversarial negative tests, runtime audits, and evidence that each backend preserves them. This proposal supplies a candidate model; it does not certify itself.

Development should begin with small executable experiments: multiply owned graph destruction and static invalidation; then root-relative array references; then role-aware generic binding. Each experiment must include cases that must be rejected. Earn the specification through those results and revise the original semantic sections, not a stack of patch notes.

## 26. Review points for Bill

The following are concrete proposals in this version, not open placeholders. They are singled out because changing them would reshape the language:

- Immutable-only RC value classes, with no explicit destroy through an alias.
- No unmanaged persistent identity fields; relationship-backed collections for stored identity references.
- Explicit roots with no implicit scope destruction.
- Shared array-handle assignment, explicit clone, and positional arrayref behavior after inner replacement.
- Constructor attach clauses and a nonescaping construction transaction.
- Final hooks restricted to resource cleanup and nonstructural effects.
- Incoming unlink before outgoing cascade, including cyclic ownership.
- Explicit call syntax for actions, no universal zero-argument property invocation.
- Both extends/default interfaces and body-free constraint aliases, with different responsibilities.
- Initial exclusion of concurrency, borrowed escaping generators, and dynamic any.
- Checked arithmetic and defined left-to-right evaluation.
- Declaration/implementation units that enforce the shared-vocabulary architecture.

We should change any of these that do not serve the intended language. They are here to make disagreement productive and concrete.

## 27. Provenance and status

The original reference, full specification, and newer interface redesign are preserved unchanged in `background/`, with SHA-256 receipts. The first reconciled draft is preserved at `drafts/01-reconciled-design.md`.

This version replaces historical implementation-status claims with proposed semantics. It intentionally does not preserve every old feature or syntax. It incorporates Bill's stated safety and relationship requirements and Cadence's explicitly identified design choices.

No compiler has been written or modified for this version. No safety proof, benchmark result, installation, or language acceptance is claimed. The next step is Bill's review of the design, followed by a jointly selected bounded implementation experiment.
