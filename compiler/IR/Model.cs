using System;
using System.Numerics;

namespace Lyric;

internal sealed partial class IrLiteral
{
    internal partial IrLiteral(BigInteger value)
    {
        Kind = IrLiteralKind.Integer;
        Integer = value;
        Bytes = Array.Empty<byte>();
    }
    internal partial IrLiteral(bool value)
    {
        Kind = IrLiteralKind.Boolean;
        Boolean = value;
        Bytes = Array.Empty<byte>();
    }
    internal partial IrLiteral(byte[] value)
    {
        Kind = IrLiteralKind.Bytes;
        Bytes = (byte[])value.Clone();
    }
    internal partial int ByteCount()
    {
        return Bytes.Length;
    }
    internal partial byte ByteAt(int index)
    {
        return Bytes[index];
    }
}
internal sealed partial class SemanticAnalysis
{
    internal partial void AddIr(IrProgram program)
    {
        if (Destroyed || Compilation is null || Ir is not null
            || program.Destroyed || program.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach IR program.");
        }
        Ir = program;
        program.Analysis = this;
    }
    internal partial void RemoveIr(IrProgram program)
    {
        if (Ir != program || program.Analysis != this || !program.Destroyed)
        {
            throw new InvalidOperationException("Only a destroyed IR program can detach.");
        }
        Ir = null;
        program.Analysis = null;
    }
    internal partial void ClearIr()
    {
        Ir?.Destroy();
    }
}
internal sealed partial class IrProgram
{
    internal partial IrProgram(SemanticAnalysis analysis)
    {
        analysis.AddIr(this);
    }
    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        while (Functions.Count != 0)
        {
            Functions[Functions.Count - 1].Destroy();
        }
        Destroyed = true;
        Analysis?.RemoveIr(this);
    }
}
internal sealed partial class IrFunction
{
    internal partial IrFunction(IrProgram program, SemanticSymbol declaration, IrFunctionKind kind)
    {
        Kind = kind;
        program.AddFunction(this);
        try
        {
            if (kind == IrFunctionKind.Destruction)
            {
                declaration.AddIrDestruction(this);
            }
            else
            {
                declaration.AddIrFunction(this);
            }
        }
        catch
        {
            Destroy();
            throw;
        }
    }
    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        while (Nodes.Count != 0)
        {
            Nodes[Nodes.Count - 1].Destroy();
        }
        Declaration?.RemoveIrFunction(this);
        DestructionClass?.RemoveIrDestruction(this);
        Destroyed = true;
        Program?.RemoveFunction(this);
    }
    internal partial void AddBody(IrNode node)
    {
        if (Destroyed || Body is not null || node.Destroyed || node.Function != this
            || node.BodyOwner is not null || node.Parent is not null
            || node.Kind != IrKind.Region || node.Role != IrRole.Body)
        {
            throw new InvalidOperationException("Invalid function body.");
        }
        Body = node;
        node.BodyOwner = this;
    }
    internal partial void RemoveBody(IrNode node)
    {
        if (Body != node || node.BodyOwner != this)
        {
            throw new InvalidOperationException("Different function body owner.");
        }
        Body = null;
        node.BodyOwner = null;
    }
}
internal sealed partial class IrNode
{
    internal partial IrNode(IrFunction function, IrKind kind, IrRole role,
        IrResult result, SemanticType? type, SyntaxNode? origin,
        SemanticSymbol? symbol, IrLiteral? literal, string? operation)
    {
        Kind = kind;
        Role = role;
        Result = result;
        Literal = literal;
        Operator = operation;
        function.AddNode(this);
        try
        {
            SetType(type);
            origin?.AddIrOrigin(this);
            symbol?.AddIrSymbol(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }
    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        BodyOwner?.RemoveBody(this);
        Parent?.RemoveChild(this);
        while (Children.Count != 0)
        {
            RemoveChild(Children[Children.Count - 1]);
        }
        while (Operands.Count != 0)
        {
            Operands[Operands.Count - 1].Destroy();
        }
        while (Uses.Count != 0)
        {
            RemoveUse(Uses[Uses.Count - 1]);
        }
        Target?.RemoveTransfer(this);
        while (Transfers.Count != 0)
        {
            RemoveTransfer(Transfers[Transfers.Count - 1]);
        }
        Symbol?.RemoveIrSymbol(this);
        Origin?.RemoveIrOrigin(this);
        SetType(null);
        Destroyed = true;
        Function?.RemoveNode(this);
    }
    internal partial void SetType(SemanticType? type)
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Destroyed IR node.");
        }
        SemanticSymbol? nominal = type?.Class;
        if (nominal is not null)
        {
            if (nominal.Destroyed || nominal.Kind != SymbolKind.Class
                || Function?.Program?.Analysis is null
                || nominal.Scope?.Analysis != Function.Program.Analysis)
            {
                throw new InvalidOperationException("Invalid IR nominal type.");
            }
            // Reserve BEFORE removing old membership, including self replacement.
            nominal.ReserveIrType();
        }
        Type?.Class?.RemoveIrType(this);
        if (nominal is null)
        {
            Type = type;
        }
        else
        {
            nominal.AddIrType(this, type!.Value);
        }
    }
}
internal sealed partial class IrOperand
{
    internal partial IrOperand(IrNode consumer, IrNode definition)
    {
        consumer.AddOperand(this);
        try
        {
            definition.AddUse(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }
    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        Definition?.RemoveUse(this);
        Destroyed = true;
        Consumer?.RemoveOperand(this);
    }
}

internal sealed partial class IrProgram
{
    internal partial void AddFunction(IrFunction function)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || function.Destroyed || function.Program is not null)
        {
            throw new InvalidOperationException("Cannot attach Function relationship.");
        }
        Functions.Add(function);
        function.Program = this;
    }
    internal partial void RemoveFunction(IrFunction function)
    {
        int index = Functions.IndexOf(function);
        if (function.Program != this || index < 0 || !function.Destroyed)
        {
            throw new InvalidOperationException("Different Function relationship owner.");
        }
        Functions.RemoveAt(index);
        function.Program = null;
    }
    internal partial int FunctionCount()
    {
        return Functions.Count;
    }
    internal partial IrFunction FunctionAt(int index)
    {
        return Functions[index];
    }
}

internal sealed partial class IrFunction
{
    internal partial void AddNode(IrNode node)
    {
        if (Destroyed || Program?.Analysis is null || Program.Analysis.Destroyed || node.Destroyed || node.Function is not null)
        {
            throw new InvalidOperationException("Cannot attach Node relationship.");
        }
        Nodes.Add(node);
        node.Function = this;
    }
    internal partial void RemoveNode(IrNode node)
    {
        int index = Nodes.IndexOf(node);
        if (node.Function != this || index < 0 || !node.Destroyed)
        {
            throw new InvalidOperationException("Different Node relationship owner.");
        }
        Nodes.RemoveAt(index);
        node.Function = null;
    }
    internal partial int NodeCount()
    {
        return Nodes.Count;
    }
    internal partial IrNode NodeAt(int index)
    {
        return Nodes[index];
    }
}

internal sealed partial class IrNode
{
    internal partial void AddChild(IrNode child)
    {
        if (Destroyed || Function is null || child.Destroyed || child.Function != Function || child.Parent is not null || child.BodyOwner is not null)
        {
            throw new InvalidOperationException("Cannot attach Child relationship.");
        }
        for (IrNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == child)
            {
                throw new InvalidOperationException("IR tree cycle.");
            }
        }
        Children.Add(child);
        child.Parent = this;
    }
    internal partial void RemoveChild(IrNode child)
    {
        int index = Children.IndexOf(child);
        if (child.Parent != this || index < 0)
        {
            throw new InvalidOperationException("Different Child relationship owner.");
        }
        Children.RemoveAt(index);
        child.Parent = null;
    }
    internal partial int ChildCount()
    {
        return Children.Count;
    }
    internal partial IrNode ChildAt(int index)
    {
        return Children[index];
    }
}

internal sealed partial class IrNode
{
    internal partial void AddOperand(IrOperand operand)
    {
        if (Destroyed || Function is null || operand.Destroyed || operand.Consumer is not null)
        {
            throw new InvalidOperationException("Cannot attach Operand relationship.");
        }
        Operands.Add(operand);
        operand.Consumer = this;
    }
    internal partial void RemoveOperand(IrOperand operand)
    {
        int index = Operands.IndexOf(operand);
        if (operand.Consumer != this || index < 0 || !operand.Destroyed)
        {
            throw new InvalidOperationException("Different Operand relationship owner.");
        }
        Operands.RemoveAt(index);
        operand.Consumer = null;
    }
    internal partial int OperandCount()
    {
        return Operands.Count;
    }
    internal partial IrOperand OperandAt(int index)
    {
        return Operands[index];
    }
}

internal sealed partial class IrNode
{
    internal partial void AddUse(IrOperand operand)
    {
        if (Destroyed || Function is null || Result == IrResult.None || operand.Destroyed || operand.Definition is not null || operand.Consumer?.Function != Function)
        {
            throw new InvalidOperationException("Cannot attach Use relationship.");
        }
        Uses.Add(operand);
        operand.Definition = this;
    }
    internal partial void RemoveUse(IrOperand operand)
    {
        int index = Uses.IndexOf(operand);
        if (operand.Definition != this || index < 0)
        {
            throw new InvalidOperationException("Different Use relationship owner.");
        }
        Uses.RemoveAt(index);
        operand.Definition = null;
    }
    internal partial int UseCount()
    {
        return Uses.Count;
    }
    internal partial IrOperand UseAt(int index)
    {
        return Uses[index];
    }
}

internal sealed partial class IrNode
{
    internal partial void AddTransfer(IrNode transfer)
    {
        if (Destroyed || Function is null || Kind is not (IrKind.While or IrKind.For) || transfer.Destroyed || transfer.Function != Function || transfer.Target is not null || transfer.Kind is not (IrKind.Break or IrKind.Continue))
        {
            throw new InvalidOperationException("Cannot attach Transfer relationship.");
        }
        Transfers.Add(transfer);
        transfer.Target = this;
    }
    internal partial void RemoveTransfer(IrNode transfer)
    {
        int index = Transfers.IndexOf(transfer);
        if (transfer.Target != this || index < 0)
        {
            throw new InvalidOperationException("Different Transfer relationship owner.");
        }
        Transfers.RemoveAt(index);
        transfer.Target = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial void AddIrDestruction(IrFunction function)
    {
        // Validate every relationship before publishing either endpoint. There is
        // no allocation between these two assignments, so refusal leaves both intact.
        if (Destroyed || Kind != SymbolKind.Class || IrDestruction is not null
            || Scope?.Analysis is null || Scope.Analysis.Destroyed || function.Destroyed
            || function.Kind != IrFunctionKind.Destruction
            || function.Declaration is not null || function.DestructionClass is not null
            || function.Program?.Analysis != Scope.Analysis)
        {
            throw new InvalidOperationException("Cannot attach IR destruction body.");
        }
        IrDestruction = function;
        function.DestructionClass = this;
    }
    internal partial void RemoveIrDestruction(IrFunction function)
    {
        if (IrDestruction != function || function.DestructionClass != this)
        {
            throw new InvalidOperationException("Different IR destruction body owner.");
        }
        IrDestruction = null;
        function.DestructionClass = null;
    }
    internal partial void AddIrFunction(IrFunction function)
    {
        if (Destroyed || Kind != SymbolKind.Function || IrBody is not null
            || function.Kind != IrFunctionKind.Source || function.DestructionClass is not null
            || Scope?.Analysis is null || Scope.Analysis.Destroyed || function.Destroyed
            || function.Declaration is not null || function.Program?.Analysis != Scope.Analysis)
        {
            throw new InvalidOperationException("Cannot attach IrFunction relationship.");
        }
        IrBody = function;
        function.Declaration = this;
    }
    internal partial void RemoveIrFunction(IrFunction function)
    {
        if (function.Declaration != this || IrBody != function)
        {
            throw new InvalidOperationException("Different IrFunction relationship owner.");
        }
        IrBody = null;
        function.Declaration = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial void AddIrSymbol(IrNode node)
    {
        if (Destroyed || Scope?.Analysis is null || Scope.Analysis.Destroyed || node.Destroyed || node.Symbol is not null || node.Function?.Program?.Analysis != Scope.Analysis)
        {
            throw new InvalidOperationException("Cannot attach IrSymbol relationship.");
        }
        IrSymbols.Add(node);
        node.Symbol = this;
    }
    internal partial void RemoveIrSymbol(IrNode node)
    {
        int index = IrSymbols.IndexOf(node);
        if (node.Symbol != this || index < 0)
        {
            throw new InvalidOperationException("Different IrSymbol relationship owner.");
        }
        IrSymbols.RemoveAt(index);
        node.Symbol = null;
    }
}

internal sealed partial class SyntaxNode
{
    internal partial void AddIrOrigin(IrNode node)
    {
        if (Destroyed || Source?.Parent is null || node.Destroyed || node.Origin is not null || node.Function?.Program?.Analysis?.Compilation != Source.Parent)
        {
            throw new InvalidOperationException("Cannot attach IrOrigin relationship.");
        }
        IrOrigins.Add(node);
        node.Origin = this;
    }
    internal partial void RemoveIrOrigin(IrNode node)
    {
        int index = IrOrigins.IndexOf(node);
        if (node.Origin != this || index < 0)
        {
            throw new InvalidOperationException("Different IrOrigin relationship owner.");
        }
        IrOrigins.RemoveAt(index);
        node.Origin = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial void ReserveIrType()
    {
        IrTypes.EnsureCapacity(IrTypes.Count + 1);
    }
    internal partial void AddIrType(IrNode node, SemanticType type)
    {
        if (Destroyed || Kind != SymbolKind.Class || type.Class != this
            || Scope?.Analysis is null || Scope.Analysis.Destroyed || node.Destroyed
            || node.Type?.Class is not null || node.Function?.Program?.Analysis != Scope.Analysis)
        {
            throw new InvalidOperationException("Invalid nominal IR dependency.");
        }
        IrTypes.Add(node);
        node.Type = type;
    }
    internal partial void RemoveIrType(IrNode node)
    {
        int index = IrTypes.IndexOf(node);
        if (index < 0 || node.Type?.Class != this)
        {
            throw new InvalidOperationException("Different nominal IR dependency.");
        }
        IrTypes.RemoveAt(index);
        node.Type = null;
    }
}
