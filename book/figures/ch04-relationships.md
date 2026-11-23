# Chapter 4 — diagram relationships

Heavy solid arrows: strong ownership; deleting the parent destroys its children. Thin dashed arrows: non-owning links; deleting the holder does not destroy the target. Gray dotted arrows: stored value data. Purple dashed arrows: dependencies retired when their declaration is destroyed. Open arrowhead: one slot, possibly absent; filled arrowhead: potentially many. Back-references are maintained but not drawn twice.

## Scopes, declarations and checked expressions

- `Compilation.Analysis` → `SemanticAnalysis`: strong, one slot; reverse `SemanticAnalysis.Compilation`.
- `SemanticAnalysis.Scopes` → `SemanticScope`: strong, many; reverse `SemanticScope.Analysis`.
- `SemanticAnalysis.Facts` → `SemanticFact`: strong, many; reverse `SemanticFact.Analysis`.
- `SemanticScope.Children` → `SemanticScope`: weak, many; reverse `SemanticScope.Parent`.
- `SemanticScope.Symbols` → `SemanticSymbol`: strong, many; reverse `SemanticSymbol.Scope`.
- `SemanticSymbol.FunctionScope` → `SemanticScope`: strong, one slot; reverse `SemanticScope.Function`.
- `SemanticSymbol.Declaration` → `SyntaxNode`: weak, one slot; reverse `SyntaxNode.DeclaredSymbol`.
- `SemanticFact.Node` → `SyntaxNode`: weak, one slot; reverse `SyntaxNode.Fact`.
- `SemanticFact.Symbol` → `SemanticSymbol`: weak, one slot; reverse `SemanticSymbol.Uses`.
- `SemanticFailure.Diagnostic` → `Diagnostic`: data, one slot.
