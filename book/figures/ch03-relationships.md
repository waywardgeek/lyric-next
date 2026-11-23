# Chapter 3 — diagram relationships

Heavy solid arrows: strong ownership; deleting the parent destroys its children. Thin dashed arrows: non-owning links; deleting the holder does not destroy the target. Gray dotted arrows: stored value data. Purple dashed arrows: dependencies retired when their declaration is destroyed. Open arrowhead: one slot, possibly absent; filled arrowhead: potentially many. Back-references are maintained but not drawn twice.

## Syntax ownership is not tree nesting

- `SourceFile.SyntaxNodes` → `SyntaxNode`: strong, many; reverse `SyntaxNode.Source`.
- `SyntaxNode.Children` → `SyntaxNode`: weak, many; reverse `SyntaxNode.Parent`.
- `SyntaxFailure.Diagnostic` → `Diagnostic`: data, one slot.
