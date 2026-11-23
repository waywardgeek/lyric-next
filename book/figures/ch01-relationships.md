# Chapter 1 — diagram relationships

Heavy solid arrows: strong ownership; deleting the parent destroys its children. Thin dashed arrows: non-owning links; deleting the holder does not destroy the target. Gray dotted arrows: stored value data. Purple dashed arrows: dependencies retired when their declaration is destroyed. Open arrowhead: one slot, possibly absent; filled arrowhead: potentially many. Back-references are maintained but not drawn twice.

## Sources and diagnostics

- `Compilation.Sources` → `SourceFile`: strong, many; reverse `SourceFile.Parent`.
