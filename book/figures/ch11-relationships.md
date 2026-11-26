# Chapter 11 — specialization relationships

Prerequisite source: `0ea3d8e617bbb3c15680b7fabd2e7df415ced1dd`. Current source is freshly extracted and lifecycle fingerprints are checked.

Heavy solid arrows: strong ownership; deleting the parent destroys its children. Thin dashed arrows: non-owning links; deleting the holder does not destroy the target. Gray dotted arrows: stored value data. Purple dashed arrows: dependencies retired when their declaration is destroyed. Open arrowhead: one slot, possibly absent; filled arrowhead: potentially many. Back-references are maintained but not drawn twice.

These figures draw each new relationship once. Existing co-owners are not duplicated:
[source-owned syntax](ch03-common.svg), [scope-owned symbols and analysis-owned scopes](ch04-common.svg),
and [canonical dependencies](ch10-type-users.svg). Lexical scope children remain non-owning.

The two SemanticSymbol roles in the first view are different objects: a lookup-visible template
and its concrete callable. Both directions of each relationship are maintained by the named add/remove operations.

## Template and concrete callable ownership

- **analysis-specializations**: `SemanticAnalysis.Specializations` → `GenericSpecialization`; strong.
  Reverse: `GenericSpecialization.Analysis`. New reciprocal ownership introduced for concrete generic functions.
- **template-specializations**: `SemanticSymbol.Specializations` → `GenericSpecialization`; strong.
  Reverse: `GenericSpecialization.Template`. New reciprocal ownership introduced for concrete generic functions.
- **specialized-function**: `GenericSpecialization.Function` → `SemanticSymbol`; strong.
  Reverse: `SemanticSymbol.Specialization`. The concrete symbol remains scope-owned but is excluded from lexical name lookup.

## Ordered bindings and canonical dependencies

- **generic-bindings**: `GenericSpecialization.Bindings` → `GenericBinding`; strong.
  Reverse: `GenericBinding.Specialization`. New reciprocal ownership introduced for concrete generic functions.
- **generic-type-users**: `SemanticType.GenericBindings` → `GenericBinding`; dependency.
  Reverse: `GenericBinding.Type`. Type retirement cascades through a binding to its specialization, never to the other bound types.

## Independent checked bodies and scope lifetimes

- **specialized-syntax**: `GenericSpecialization.Nodes` → `SyntaxNode`; strong.
  Reverse: `SyntaxNode.Specialization`. Copied nodes retain source co-ownership and offsets without becoming parsed roots.
- **specialized-scopes**: `GenericSpecialization.Scopes` → `SemanticScope`; strong.
  Reverse: `SemanticScope.Specialization`. Lexical parent links are weak; explicit co-ownership retires every concrete-body scope.

## Retirement order

Clear derived IR first, mark the specialization retired, then destroy its callable, all concrete-body scopes,
copied syntax, and binding occurrences. Finally detach from the template and analysis.
Destroying a live owned part first retires its specialization; the retired flag prevents reentrant cascades.
Type retirement follows bindings toward specializations, never toward other shared type arguments.
Copied syntax retains source offsets and source ownership, but does not become a parsed root.
Its facts and declaration links require explicit cleanup because it cannot clear the analysis constructing it.
