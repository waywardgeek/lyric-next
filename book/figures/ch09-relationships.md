# Chapter 9 — diagram relationships

Heavy solid arrows: strong ownership; deleting the parent destroys its children. Thin dashed arrows: non-owning links; deleting the holder does not destroy the target. Gray dotted arrows: stored value data. Purple dashed arrows: dependencies retired when their declaration is destroyed. Open arrowhead: one slot, possibly absent; filled arrowhead: potentially many. Back-references are maintained but not drawn twice.

## IR ownership and ordered structure

- `SemanticAnalysis.Ir` → `IrProgram`: strong, one slot; reverse `IrProgram.Analysis`.
- `IrProgram.Functions` → `IrFunction`: strong, many; reverse `IrFunction.Program`.
- `IrFunction.Nodes` → `IrNode`: strong, many; reverse `IrNode.Function`.
- `IrFunction.Body` → `IrNode`: weak, one slot; reverse `IrNode.BodyOwner`.
- `IrNode.Children` → `IrNode`: weak, many; reverse `IrNode.Parent`.
- `IrNode.Target` → `IrNode`: weak, one slot; reverse `IrNode.Transfers`.
- `IrNode.Operands` → `IrOperand`: strong, many; reverse `IrOperand.Consumer`.
- `IrNode.Uses` → `IrOperand`: weak, many; reverse `IrOperand.Definition`.

## IR retains checked meaning

- `IrFunction.Declaration` → `SemanticSymbol`: weak, one slot; reverse `SemanticSymbol.IrBody`.
- `IrFunction.DestructionClass` → `SemanticSymbol`: weak, one slot; reverse `SemanticSymbol.IrDestruction`.
- `IrNode.Origin` → `SyntaxNode`: weak, one slot; reverse `SyntaxNode.IrOrigins`.
- `IrNode.Symbol` → `SemanticSymbol`: weak, one slot; reverse `SemanticSymbol.IrSymbols`.
- `IrNode.Type` → `SemanticType`: data, one slot.
- `IrNode.Literal` → `IrLiteral`: data, one slot.
- `SemanticSymbol.IrTypes` → `IrNode`: weak, many; reverse `IrNode.Type.Class`.
