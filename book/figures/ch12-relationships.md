# Chapter 12 — participant and nullable relationships

Prerequisite source: `5947bed06e8327b0e3c0c079c14508847be0f84f`. Current declarations are freshly extracted; lifecycle fingerprints cover the reviewed operations.

Heavy solid arrows: strong ownership; deleting the parent destroys its children. Thin dashed arrows: non-owning links; deleting the holder does not destroy the target. Gray dotted arrows: stored value data. Purple dashed arrows: dependencies retired when their declaration is destroyed. Open arrowhead: one slot, possibly absent; filled arrowhead: potentially many. Back-references are maintained but not drawn twice.

Each new relationship appears once. Existing source, analysis, scope and canonical ownership remains in force:
[source-owned syntax](ch03-common.svg), [scopes and symbols](ch04-common.svg), and [canonical dependencies](ch10-type-users.svg).

SemanticSymbol serves distinct interface-template, concrete-class and contributed-member roles in these views.
A lookup namespace is a symbol with a member scope, not a runtime wrapper. ParticipantBinding position and label are value data.

## Application ownership

- **analysis-multiclass**: `SemanticAnalysis.MultiClassInstantiations` → `MultiClassInstantiation`; strong.
  Reverse: `MultiClassInstantiation.Analysis`. Analysis and template co-own each application; retirement removes both memberships.
- **template-multiclass**: `SemanticSymbol.MultiClassInstantiations` → `MultiClassInstantiation`; strong.
  Reverse: `MultiClassInstantiation.Template`. The interface declaration owns applications of its body; retiring it retires them.

## Generated material

- **multiclass-syntax**: `MultiClassInstantiation.Nodes` → `SyntaxNode`; strong.
  Reverse: `SyntaxNode.MultiClass`. Copied syntax is also source-owned under the earlier source relationship; teardown clears facts before endpoint identities.
- **multiclass-contributions**: `MultiClassInstantiation.Contributions` → `SemanticSymbol`; strong.
  Reverse: `SemanticSymbol.MultiClass`. Physical members and lookup namespaces are also scope-owned; destroying a live contribution retires the entire application.
- **multiclass-scopes**: `MultiClassInstantiation.Scopes` → `SemanticScope`; strong.
  Reverse: `SemanticScope.MultiClass`. Analysis remains a strong co-owner. Template lexical ancestry is the existing non-owning scope relationship.

## Participant occurrences and class dependencies

- **multiclass-participants**: `MultiClassInstantiation.Participants` → `ParticipantBinding`; strong.
  Reverse: `ParticipantBinding.Instantiation`. Each endpoint occurrence has its own identity even when concrete classes repeat.
- **participant-classes**: `SemanticSymbol.ParticipantBindings` → `ParticipantBinding`; dependency.
  Reverse: `ParticipantBinding.ConcreteClass`. Retiring a concrete class retires dependent applications; retiring an application never destroys shared concrete classes.

## Declared and expression value views

- **participant-symbol-views**: `ParticipantBinding.ViewSymbols` → `SemanticSymbol`; weak.
  Reverse: `SemanticSymbol.ParticipantView`. A symbol view can name this endpoint nominal type or its nullable wrapper; retirement clears the view without destroying the symbol.
- **participant-fact-views**: `ParticipantBinding.ViewFacts` → `SemanticFact`; weak.
  Reverse: `SemanticFact.ParticipantView`. Expression lookup identity is separate from canonical type and does not authorize nullable dereference.

## Member contribution role

- **participant-members**: `ParticipantBinding.Members` → `SemanticSymbol`; weak.
  Reverse: `SemanticSymbol.ContributingParticipant`. The contributor identifies where a member was declared, not the participant view of its result. Strong contribution ownership is drawn separately.

## Nullable type dependencies

- **nullable-dependents**: `SemanticType.NullableUses` → `SemanticType`; dependency.
  Reverse: `SemanticType.Element`. A class nominal type retains reverse wrapper dependencies. Base retirement cascades to wrappers; wrapper retirement only detaches from its base. Element is shared with the earlier array-key representation, not an extra base-type owner.

## Retirement order

Clear derived IR, mark the application retired, then destroy contributions, private scopes, copied syntax and participant occurrences.
Finally detach the application from its template and analysis. Destroying a live owned part redirects to the application first;
the retired flag prevents recursive re-entry. Endpoint retirement clears contributor and value-view links without destroying their users.
Concrete classes are dependencies, not owned children. Removing the application preserves them.

Nullable wrappers depend on their canonical class type. Base retirement retires wrappers and their users;
wrapper retirement removes its Element/NullableUses pair without destroying the base.

These are compiler-model lifetimes. They do not supply runtime relationship-aware teardown to Chapter 12 programs.
