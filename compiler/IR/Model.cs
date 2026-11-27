using System;
using System.Collections.Generic;
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
        type?.ReserveIrNode(this);
        Type?.RemoveIrNode(this);
        type?.AddIrNode(this);
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
        bool source = Kind == SymbolKind.Function && function.Kind == IrFunctionKind.Source;
        bool initializer = Kind == SymbolKind.Field && function.Kind == IrFunctionKind.FieldInitializer
            && Scope?.MemberOwner?.Kind == SymbolKind.Struct
            && InitializerScope is not null && !InitializerScope.Destroyed
            && InitializerScope.InitializerField == this;
        if (Destroyed || (!source && !initializer) || IrBody is not null
            || function.DestructionClass is not null
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

internal sealed partial class IrFunction
{
    internal partial bool BorrowsReceiver()
    {
        SemanticSymbol? declaration = Declaration;
        SemanticSymbol? nominal = declaration?.Scope?.Class;
        return Kind == IrFunctionKind.Destruction
            || (nominal is not null && declaration is not null
                && (ObservesReceiver || declaration.Name == nominal.Name
                    || declaration.IsFinalHook() || declaration.IsDestructorFragment()));
    }
}
internal sealed partial class IrNode
{
    internal partial void ReplaceChild(IrNode oldChild, IrNode newChild)
    {
        int index = Children.IndexOf(oldChild);
        if (Destroyed || Function is null || index < 0 || oldChild.Parent != this
            || newChild.Destroyed || newChild.Function != Function
            || newChild.Parent is not null || newChild.BodyOwner is not null)
        {
            throw new InvalidOperationException("Invalid ordered IR replacement.");
        }
        for (IrNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == newChild)
            {
                throw new InvalidOperationException("Cyclic ordered IR replacement.");
            }
        }
        Children[index] = newChild;
        oldChild.Parent = null;
        newChild.Parent = this;
    }
    internal partial void InsertAfter(IrNode previous, IrNode child)
    {
        int index = Children.IndexOf(previous);
        if (Destroyed || Function is null || index < 0 || previous.Parent != this
            || child.Destroyed || child.Function != Function
            || child.Parent is not null || child.BodyOwner is not null)
        {
            throw new InvalidOperationException("Invalid ordered IR insertion.");
        }
        for (IrNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == child)
            {
                throw new InvalidOperationException("Cyclic ordered IR insertion.");
            }
        }
        Children.Insert(index + 1, child);
        child.Parent = this;
    }
}
internal sealed partial class IrOperand
{
    internal partial void SetDefinition(IrNode definition)
    {
        if (Destroyed || Consumer is null || definition.Destroyed
            || definition.Function != Consumer.Function || definition.Result == IrResult.None)
        {
            throw new InvalidOperationException("Invalid IR definition replacement.");
        }
        if (Definition == definition)
        {
            return;
        }
        // Reserve before detaching so allocation failure preserves the old edge.
        definition.ReserveUse();
        Definition?.RemoveUse(this);
        definition.AddUse(this);
    }
}

internal sealed partial class IrNode
{
    internal partial void ReserveUse()
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Destroyed IR definition.");
        }
        Uses.EnsureCapacity(Uses.Count + 1);
    }
}

internal sealed partial class IrNode
{
    internal partial bool IsManagedHandleStorage()
    {
        // Address aliases preserve the field's ownership policy. Do not infer it
        // from the value type: ordinary fields of the same type own their values.
        IrNode current = this;
        HashSet<IrNode> visited = new();
        while (current.Kind is IrKind.Borrow or IrKind.AddressOf)
        {
            // A relationship's writable participant parameter is a borrowed
            // structural slot, even though its caller's address is not a local
            // operand. Use the declared endpoint occurrence, not concrete class
            // equality: unrelated counted types keep ordinary owning stores.
            if (current.Kind == IrKind.Borrow && current.OperandCount() == 0
                && current.Symbol is SemanticSymbol parameter
                && parameter.Kind == SymbolKind.Parameter && parameter.IsReference
                && parameter.ParticipantView is ParticipantBinding participant
                && participant.Instantiation is MultiClassInstantiation instance
                && instance.Policy != RelationshipPolicy.None
                && current.Function?.Declaration?.ContributingParticipant?.Instantiation == instance
                && participant.AcceptsViewType(current.Type))
            {
                return true;
            }
            if (!visited.Add(current) || current.OperandCount() != 1)
            {
                return false;
            }
            IrNode? origin = current.OperandAt(0).Definition;
            if (origin is null)
            {
                return false;
            }
            current = origin;
        }
        if (current.Kind == IrKind.Local
            && current.Function?.Declaration?.BorrowsParticipantLocal(current.Symbol) == true)
        {
            return true;
        }
        return current.Kind == IrKind.SelectField
            && current.Symbol is not null
            && current.Symbol.IsManagedHandle();
    }
}

internal sealed partial class IrNode
{
    internal partial bool IsBorrowedHandleValue()
    {
        if (Type is null || Type.ArrayDepth != 0 || Type.Class?.IsCountedClass() != true)
        {
            return false;
        }
        IrNode current = this;
        HashSet<IrNode> visited = new();
        while (current.Kind is IrKind.WrapNullable or IrKind.UnwrapNullable)
        {
            // Incomplete drafts and cycles are not evidence of borrowing.
            if (!visited.Add(current) || current.OperandCount() != 1)
            {
                return false;
            }
            IrNode? input = current.OperandAt(0).Definition;
            if (input is null)
            {
                return false;
            }
            current = input;
        }
        return current.Kind == IrKind.Load && current.Result == IrResult.Value;
    }
}
