using System;
using System.Collections.Generic;

namespace Lyric;

// Ownership simplification is a graph transformation, not an emitter convention.
// In particular, teardown self has valid storage but no counted owning holder.
// Observing it must not manufacture ownership merely to call a helper.
internal sealed partial class IrProgram
{
    internal partial void SimplifyOwnership()
    {
        bool changed;
        do
        {
            changed = false;
            for (int f = 0; f < FunctionCount(); f += 1)
            {
                IrFunction function = FunctionAt(f);
                foreach (IrNode node in Snapshot(function))
                {
                    if (node.Destroyed || node.Kind != IrKind.Ref || !OnlyObservations(node))
                    {
                        continue;
                    }
                    IrNode load = new(function, IrKind.Load, result: IrResult.Value,
                        type: node.Type, origin: node.Origin);
                    _ = new IrOperand(load, node.OperandAt(0).Definition!);
                    node.Parent!.ReplaceChild(node, load);
                    while (node.UseCount() != 0)
                    {
                        IrOperand use = node.UseAt(0);
                        if (use.Consumer!.Kind == IrKind.Unref)
                        {
                            use.Consumer.Destroy();
                        }
                        else
                        {
                            use.SetDefinition(load);
                        }
                    }
                    node.Destroy();
                    changed = true;
                }
                if (!function.BorrowsReceiver() && CanObserveReceiver(function))
                {
                    function.ObservesReceiver = true;
                    IrNode receiver = Receiver(function)!;
                    for (int u = receiver.UseCount() - 1; u >= 0; u -= 1)
                    {
                        IrNode consumer = receiver.UseAt(u).Consumer!;
                        if (consumer.Kind == IrKind.Unref)
                        {
                            consumer.Destroy();
                        }
                    }
                    changed = true;
                }
            }
            // A borrowed callee no longer consumes its receiver. Keep the original
            // caller holder live through all argument effects and the call, then
            // release it immediately, preserving the old finalization boundary.
            for (int f = 0; f < FunctionCount(); f += 1)
            {
                IrFunction function = FunctionAt(f);
                foreach (IrNode call in Snapshot(function))
                {
                    if (call.Kind != IrKind.Call || call.Symbol?.IrBody?.ObservesReceiver != true
                        || call.OperandCount() == 0)
                    {
                        continue;
                    }
                    IrOperand argument = call.OperandAt(0);
                    IrNode transfer = argument.Definition!;
                    if (transfer.Kind != IrKind.Move)
                    {
                        continue;
                    }
                    IrNode holder = transfer.OperandAt(0).Definition!;
                    argument.SetDefinition(holder);
                    transfer.Destroy();
                    IrNode release = new(function, IrKind.Unref, origin: call.Origin);
                    _ = new IrOperand(release, holder);
                    call.Parent!.InsertAfter(call, release);
                    changed = true;
                }
            }
        }
        while (changed);

        static List<IrNode> Snapshot(IrFunction function)
        {
            List<IrNode> nodes = new();
            for (int n = 0; n < function.NodeCount(); n += 1)
            {
                nodes.Add(function.NodeAt(n));
            }
            return nodes;
        }

        static IrNode? Receiver(IrFunction function)
        {
            SemanticSymbol? declaration = function.Declaration;
            SemanticSymbol? nominal = declaration?.Scope?.Class;
            SemanticScope? signature = declaration?.FunctionScope;
            if (function.Kind != IrFunctionKind.Source || nominal is null
                || signature is null || signature.ParameterCount() == 0
                || !nominal.IsCountedClass())
            {
                return null;
            }
            SemanticSymbol parameter = signature.ParameterAt(0);
            // The checker reserves self as the declared instance receiver. A static
            // member's first ordinary parameter is not a receiver contract.
            if (parameter.Name != "self" || parameter.IsReference
                || parameter.Type?.Kind != SemanticTypeKind.Nominal
                || parameter.Type.Class != nominal)
            {
                return null;
            }
            for (int n = 0; n < function.NodeCount(); n += 1)
            {
                IrNode node = function.NodeAt(n);
                if (node.Kind == IrKind.Local && node.Symbol == parameter)
                {
                    return node;
                }
            }
            return null;
        }

        static bool CanObserveReceiver(IrFunction function)
        {
            IrNode? receiver = Receiver(function);
            if (receiver is null)
            {
                return false;
            }
            for (int u = 0; u < receiver.UseCount(); u += 1)
            {
                IrNode use = receiver.UseAt(u).Consumer!;
                if (use.Kind == IrKind.Unref)
                {
                    continue;
                }
                if (use.Kind == IrKind.Ref && OnlyObservations(use, prospectiveReceiver: true))
                {
                    continue;
                }
                if (use.Kind != IrKind.Load)
                {
                    return false;
                }
                for (int v = 0; v < use.UseCount(); v += 1)
                {
                    if (!Observes(use.UseAt(v)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        static bool Observes(IrOperand operand)
        {
            IrNode consumer = operand.Consumer!;
            if (consumer.Kind == IrKind.Binary && consumer.Operator is "==" or "!=")
            {
                return true;
            }
            if (consumer.Kind == IrKind.SelectField && consumer.OperandAt(0) == operand)
            {
                return true;
            }
            return consumer.Kind == IrKind.Call && consumer.OperandCount() != 0
                && consumer.OperandAt(0) == operand
                && consumer.Symbol?.IrBody?.BorrowsReceiver() == true;
        }

        static bool OnlyObservations(IrNode node, bool prospectiveReceiver = false)
        {
            if (node.Parent?.Kind != IrKind.Region)
            {
                return false;
            }
            bool observed = false;
            for (int u = 0; u < node.UseCount(); u += 1)
            {
                IrOperand operand = node.UseAt(u);
                IrNode consumer = operand.Consumer!;
                if (consumer.Kind == IrKind.Unref)
                {
                    continue;
                }
                if (!Observes(operand) || consumer.Parent != node.Parent
                    || consumer.Kind == IrKind.SelectField)
                {
                    return false;
                }
                if (consumer.Kind == IrKind.Call)
                {
                    IrNode source = node.OperandAt(0).Definition!;
                    if ((!prospectiveReceiver && !node.Function!.BorrowsReceiver())
                        || source != Receiver(node.Function!))
                    {
                        return false;
                    }
                }
                // Do not cancel across effects in another operand's evaluation.
                // These pure operations may check/fail but cannot change ownership
                // of the original storage or call user code.
                bool after = false;
                bool found = false;
                for (int i = 0; i < node.Parent.ChildCount(); i += 1)
                {
                    IrNode between = node.Parent.ChildAt(i);
                    if (between == node)
                    {
                        after = true;
                        continue;
                    }
                    if (!after)
                    {
                        continue;
                    }
                    if (between == consumer)
                    {
                        found = true;
                        break;
                    }
                    if (between.Kind is not (IrKind.Load or IrKind.Ref or IrKind.Move
                        or IrKind.SelectField or IrKind.Constant or IrKind.NullHandle
                        or IrKind.WrapNullable or IrKind.UnwrapNullable or IrKind.Binary))
                    {
                        return false;
                    }
                }
                if (!found)
                {
                    return false;
                }
                observed = true;
            }
            return observed;
        }
    }
}
