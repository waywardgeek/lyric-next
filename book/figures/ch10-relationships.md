# Chapter 10 — revised relationships

Historical source: `6073e136c772019dc8dbe4ca11211340904cbf9e`. Current source: the checked Chapter 10 working tree.

Heavy solid lines own lifetime; thin dashed lines are non-owning links.
Dependency lines mean retirement propagates to users, not ownership of shared ingredients.
Open heads denote one slot, potentially absent; filled heads denote many. Reverse links are listed below, not drawn twice.

## Canonical identity and tuple positions


- **canonical-types**: `SemanticAnalysis.TypeBuckets` → `SemanticType`; strong.
  Reverse: `Analysis`. Predecessors: none (new). Canonical identities are now strongly owned by analysis buckets.

- **array-types**: `SemanticType.Arrays` → `SemanticType`; dependency.
  Reverse: `Element`. Predecessors: none (new). An array key depends on its canonical element; element retirement retires dependent arrays.

- **tuple-positions**: `SemanticType.Components` → `SemanticTypeComponent`; strong.
  Reverse: `Tuple`. Predecessors: none (new). The tuple owns one occurrence per position.

- **component-users**: `SemanticType.ComponentUses` → `SemanticTypeComponent`; dependency.
  Reverse: `Component`. Predecessors: none (new). Shared component retirement invalidates dependent tuple keys, not vice versa.
## Canonical type users

Historical view: [ch07-common](ch07-common.svg), Chapter 7.
Historical view: [ch08-common](ch08-common.svg), Chapter 8.
Historical view: [ch09-meaning](ch09-meaning.svg), Chapter 9.

- **canonical-symbols**: `SemanticType.Symbols` → `SemanticSymbol`; dependency.
  Reverse: `Type`. Predecessors: symbol-type, typed-symbols. Canonical types replace flat values and class-only reverse symbol lists.

- **canonical-facts**: `SemanticType.Facts` → `SemanticFact`; dependency.
  Reverse: `TypeLink`. Predecessors: fact-type, typed-facts. TypeLink replaces the stored flat fact type and class-only reverse facts.

- **canonical-ir**: `SemanticType.IrNodes` → `IrNode`; weak.
  Reverse: `Type`. Predecessors: ir-type, typed-ir. Every IR type now registers on its canonical identity; graph teardown precedes semantic retirement.

- **nominal**: `SemanticType.Declaration` → `SemanticSymbol`; weak.
  Reverse: `NominalType`. Predecessors: nominal. The canonical nominal type now has a reciprocal declaration dependency for classes and structs.
## Member and initializer scopes

Historical view: [ch04-common](ch04-common.svg), Chapter 4.
Historical view: [ch08-common](ch08-common.svg), Chapter 8.

- **scopes**: `SemanticAnalysis.Scopes` → `SemanticScope`; strong.
  Reverse: `Analysis`. Predecessors: scopes. Unchanged context; authoritative introduction remains in its earlier chapter.

- **symbols**: `SemanticScope.Symbols` → `SemanticSymbol`; strong.
  Reverse: `Scope`. Predecessors: symbols. Unchanged context; authoritative introduction remains in its earlier chapter.

- **member-scope**: `SemanticSymbol.MemberScope` → `SemanticScope`; strong.
  Reverse: `MemberOwner`. Predecessors: member-scope. MemberOwner generalizes the former class-only owner to classes and structs.

- **initializer-scope**: `SemanticSymbol.InitializerScope` → `SemanticScope`; strong.
  Reverse: `InitializerField`. Predecessors: none (new). A defaulted struct field co-owns its execution scope with the analysis.
## Separate syntax and generated bodies

Historical view: [ch04-common](ch04-common.svg), Chapter 4.
Historical view: [ch09-ownership](ch09-ownership.svg), Chapter 9.
Historical view: [ch09-meaning](ch09-meaning.svg), Chapter 9.

- **implementation**: `SemanticSymbol.Implementation` → `SyntaxNode`; weak.
  Reverse: `ImplementedSymbol`. Predecessors: none (new). Separate implementation syntax links to the original callable identity, not a duplicate symbol.

- **declaration**: `SemanticSymbol.Declaration` → `SyntaxNode`; weak.
  Reverse: `DeclaredSymbol`. Predecessors: declaration. Unchanged context; authoritative introduction remains in its earlier chapter.

- **ir-declaration**: `IrFunction.Declaration` → `SemanticSymbol`; weak.
  Reverse: `IrBody`. Predecessors: ir-declaration. The existing weak Declaration/IrBody link now also identifies a generated field-initializer body; IrProgram still owns it.

- **functions**: `IrProgram.Functions` → `IrFunction`; strong.
  Reverse: `Program`. Predecessors: functions. Unchanged context; authoritative introduction remains in its earlier chapter.

A tuple owns its occurrence links, not the shared component types. Component retirement retires dependent tuples; array-element retirement retires dependent array keys. Semantic type users are invalidated before their dependencies disappear. IR is retired first by semantic teardown; an editable IR type link can be detached without inventing another semantic owner.

Initializer scopes are co-owned by their field and analysis. Their lexical parent is the enclosing declaration scope, not the struct member namespace. Generated initializer IR bodies remain program-owned and use the existing Declaration/IrBody link with FieldInitializer kind. Separate implementation syntax remains source-owned.
