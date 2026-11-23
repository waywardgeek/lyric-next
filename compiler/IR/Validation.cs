using System;
using System.Collections.Generic;

namespace Lyric;

internal sealed partial class IrProgram
{
    internal partial void ValidateStructure()
    {
        // Editing can leave detached drafts. Publication cannot: every arena node
        // must occur exactly once in the executable tree. Iterate to preserve the
        // lowerer's ability to handle deep expressions without host recursion.
        Require(!Destroyed && Analysis is not null, "retired program");
        for (int f = 0; f < FunctionCount(); f += 1)
        {
            IrFunction function = FunctionAt(f);
            Require(!function.Destroyed && function.Program == this, "function membership");
            IrNode? body = function.Body;
            Require(body is not null && body.BodyOwner == function
                && body.Parent is null && body.Kind == IrKind.Region
                && body.Role == IrRole.Body, "function body");
            HashSet<IrNode> arena = new();
            for (int i = 0; i < function.NodeCount(); i += 1)
            {
                IrNode node = function.NodeAt(i);
                Require(arena.Add(node) && !node.Destroyed && node.Function == function,
                    "node membership");
            }
            HashSet<IrNode> visited = new();
            Stack<IrNode> pending = new();
            pending.Push(body!);
            while (pending.Count != 0)
            {
                IrNode node = pending.Pop();
                Require(arena.Contains(node) && visited.Add(node), "tree membership");
                for (int i = 0; i < node.OperandCount(); i += 1)
                {
                    IrOperand operand = node.OperandAt(i);
                    Require(!operand.Destroyed && operand.Consumer == node
                        && operand.Definition is not null && arena.Contains(operand.Definition),
                        "operand definition");
                }
                if (node.Kind == IrKind.If)
                {
                    Regions(node, IrRole.Then, IrRole.Else);
                }
                else if (node.Kind == IrKind.While)
                {
                    Regions(node, IrRole.Condition, IrRole.Body);
                }
                else if (node.Kind == IrKind.For)
                {
                    Require(false, "For has no implemented lowering contract");
                }
                else if (node.Kind != IrKind.Region)
                {
                    Require(node.ChildCount() == 0, "children on an operation");
                }
                if (node.Kind is IrKind.Break or IrKind.Continue)
                {
                    IrNode? ancestor = node.Parent;
                    while (ancestor is not null && ancestor != node.Target)
                    {
                        ancestor = ancestor.Parent;
                    }
                    Require(ancestor is not null && ancestor.Kind == IrKind.While,
                        "transfer must target an enclosing loop");
                }
                else
                {
                    Require(node.Target is null, "target on a non-transfer");
                }
                for (int i = 0; i < node.ChildCount(); i += 1)
                {
                    IrNode child = node.ChildAt(i);
                    Require(child.Parent == node && child.BodyOwner is null,
                        "child backlink");
                    if (node.Kind == IrKind.Region)
                    {
                        Require(child.Kind != IrKind.Region && child.Role == IrRole.None,
                            "region operation role");
                    }
                    pending.Push(child);
                }
            }
            Require(visited.Count == arena.Count, "detached executable node");
        }

        static void Regions(IrNode node, IrRole first, IrRole second)
        {
            Require(node.ChildCount() == 2, "structured region count");
            Require(node.ChildAt(0).Kind == IrKind.Region && node.ChildAt(0).Role == first
                && node.ChildAt(1).Kind == IrKind.Region && node.ChildAt(1).Role == second,
                "structured region roles");
        }
        static void Require(bool valid, string reason)
        {
            if (!valid)
            {
                throw new InvalidOperationException("Invalid IR structure: " + reason + ".");
            }
        }
    }

    internal partial void ValidateOperations()
    {
        // Run after structural validation: all definitions and arena memberships
        // exist. Keep this pass separate from execution-order/ownership analysis.
        for (int f = 0; f < FunctionCount(); f += 1)
        {
            IrFunction function = FunctionAt(f);
            for (int i = 0; i < function.NodeCount(); i += 1)
            {
                IrNode node = function.NodeAt(i);
                int count = node.OperandCount();
                int required = node.Kind switch
                {
                    IrKind.Region or IrKind.While or IrKind.Break or IrKind.Continue
                        or IrKind.Constant or IrKind.Local or IrKind.NewObject => 0,
                    IrKind.If or IrKind.Return or IrKind.Yield or IrKind.SelectField
                        or IrKind.AddressOf or IrKind.Load or IrKind.Unary or IrKind.Convert
                        or IrKind.Copy or IrKind.Move or IrKind.Ref or IrKind.Unref
                        or IrKind.FreeArray or IrKind.DestroyObject or IrKind.ReclaimObject
                        or IrKind.ArrayLength or IrKind.ArrayCapacity => 1,
                    IrKind.SelectIndex or IrKind.Store or IrKind.Binary or IrKind.ArrayReserve
                        or IrKind.ArrayResize or IrKind.ArrayTruncate or IrKind.ArrayPush => 2,
                    // These have explicit variable-arity contracts below.
                    IrKind.Borrow or IrKind.Call or IrKind.NewArray => -1,
                    _ => throw Invalid(node, "operation has no implemented contract"),
                };
                Require(required < 0 || count == required, "operand count");
                if (node.Kind == IrKind.Borrow)
                {
                    Require(count == 1 || (count == 0 && node.Symbol?.Kind == SymbolKind.Parameter),
                        "borrow must alias an address or receive a parameter");
                }
                if (node.Kind == IrKind.NewArray && node.Literal is not null)
                {
                    Require(node.Literal.Kind == IrLiteralKind.Bytes && count == 0,
                        "byte array payload cannot also have element operands");
                }
                if (node.Kind == IrKind.Call)
                {
                    SemanticSymbol? target = node.Symbol;
                    Require(target is not null && target.Kind == SymbolKind.Function
                        && target.FunctionScope is not null, "call has no selected function scope");
                    SemanticScope signature = target!.FunctionScope!;
                    Require(count == signature.ParameterCount(), "call argument count");
                    Require(node.Type == target.Type, "call result type mismatch");
                    for (int argumentIndex = 0; argumentIndex < count; argumentIndex += 1)
                    {
                        SemanticSymbol parameter = signature.ParameterAt(argumentIndex);
                        IrNode argument = node.OperandAt(argumentIndex).Definition!;
                        Require(argument.Type == parameter.Type, "call argument type mismatch");
                        IrResult category = parameter.IsReference ? IrResult.Address : IrResult.Value;
                        Require(argument.Result == category, "call argument category mismatch");
                    }
                }
                IrResult expected = node.Kind switch
                {
                    IrKind.Region or IrKind.While or IrKind.Break or IrKind.Continue
                        or IrKind.Return or IrKind.Yield or IrKind.Store or IrKind.Unref
                        or IrKind.FreeArray or IrKind.ReclaimObject => IrResult.None,
                    IrKind.Local or IrKind.Borrow or IrKind.AddressOf or IrKind.SelectIndex
                        => IrResult.Address,
                    IrKind.SelectField => IrResult.Place,
                    _ => IrResult.Value,
                };
                Require(node.Result == expected, "result category");
                Require(expected == IrResult.None || node.Type is not null, "missing result type");
                for (int operandIndex = 0; operandIndex < count; operandIndex += 1)
                {
                    IrNode definition = node.OperandAt(operandIndex).Definition!;
                    Require(definition.Result != IrResult.None, "operand produces no result");
                }
                if (count != 0)
                {
                    IrResult first = node.OperandAt(0).Definition!.Result;
                    if (node.Kind is IrKind.Load or IrKind.Store or IrKind.AddressOf)
                    {
                        Require(first is IrResult.Place or IrResult.Address,
                            "operation requires selected storage");
                    }
                    if (node.Kind == IrKind.Borrow)
                    {
                        Require(first == IrResult.Address, "borrow requires a fixed address");
                    }
                }

                if (node.Kind == IrKind.Constant)
                {
                    SemanticType type = node.Type!.Value;
                    if (type == SemanticType.Unit)
                    {
                        Require(node.Literal is null, "unit constant has a payload");
                    }
                    else if (type == SemanticType.Bool)
                    {
                        Require(node.Literal?.Kind == IrLiteralKind.Boolean,
                            "boolean constant requires a boolean payload");
                    }
                    else if (SemanticTypes.IsInteger(type))
                    {
                        Require(node.Literal?.Kind == IrLiteralKind.Integer,
                            "integer constant requires an integer payload");
                        Require(SemanticTypes.Represents(type, node.Literal!.Integer),
                            "integer constant is outside its type range");
                    }
                    else
                    {
                        Require(false, "constant type has no implemented payload contract");
                    }
                }

                // Region results and function returns have different destinations.
                // This checks their local value contract, not path termination.
                if (node.Kind is IrKind.If or IrKind.Return or IrKind.Yield)
                {
                    IrNode value = node.OperandAt(0).Definition!;
                    SemanticType? destination = null;
                    if (node.Kind == IrKind.If)
                    {
                        destination = SemanticType.Bool;
                    }
                    else if (node.Kind == IrKind.Return)
                    {
                        destination = function.Kind == IrFunctionKind.Destruction
                            ? SemanticType.Unit : function.Declaration?.Type;
                    }
                    else
                    {
                        IrNode? region = node.Parent;
                        IrNode? control = region?.Parent;
                        Require(region?.Kind == IrKind.Region, "yield requires a region");
                        if (control?.Kind == IrKind.If
                            && region!.Role is IrRole.Then or IrRole.Else)
                        {
                            destination = control.Type;
                        }
                        else if (control?.Kind == IrKind.While
                            && region!.Role == IrRole.Condition)
                        {
                            destination = SemanticType.Bool;
                        }
                    }
                    Require(destination is not null, "missing control result destination");
                    Require(value.Result == IrResult.Value && value.Type == destination,
                        "control operand must be a value of the destination type");
                }

                // Operator tags preserve semantics: a wrapping conversion is not
                // interchangeable with a checked cast, even when types coincide.
                if (node.Kind is IrKind.Unary or IrKind.Convert)
                {
                    IrNode input = node.OperandAt(0).Definition!;
                    Require(input.Result == IrResult.Value && input.Type is not null,
                        "operator requires typed value");
                    SemanticType source = input.Type!.Value;
                    SemanticType result = node.Type!.Value;
                    if (node.Kind == IrKind.Unary)
                    {
                        Require(node.Operator is "!" or "-" or "!-", "unknown unary operator");
                        if (node.Operator == "!")
                        {
                            Require(source == SemanticType.Bool && result == SemanticType.Bool,
                                "logical negation requires bool");
                        }
                        else
                        {
                            Require(SemanticTypes.IsInteger(source) && result == source,
                                "integer negation must preserve type");
                        }
                    }
                    else if (node.Operator == "widen")
                    {
                        Require(SemanticTypes.CanWiden(source, result), "invalid widening conversion");
                    }
                    else
                    {
                        Require(node.Operator is "as" or "!as", "unknown conversion operator");
                        Require(SemanticTypes.IsInteger(source) && SemanticTypes.IsInteger(result),
                            "explicit conversion requires integer types");
                    }
                }

                if (node.Kind == IrKind.Binary)
                {
                    IrNode left = node.OperandAt(0).Definition!;
                    IrNode right = node.OperandAt(1).Definition!;
                    Require(left.Result == IrResult.Value && right.Result == IrResult.Value
                        && left.Type is not null && right.Type is not null,
                        "binary operator requires typed values");
                    SemanticType leftType = left.Type!.Value;
                    SemanticType rightType = right.Type!.Value;
                    bool integers = SemanticTypes.IsInteger(leftType)
                        && SemanticTypes.IsInteger(rightType);
                    switch (node.Operator)
                    {
                        case "+" or "-" or "*" or "/" or "%" or "!+" or "!-" or "!*"
                            or "&" or "|" or "^":
                            Require(integers, "arithmetic requires integers");
                            SemanticType? common = SemanticTypes.ArithmeticType(leftType, rightType);
                            Require(common is not null && node.Type == common,
                                "arithmetic result must use selected operand type");
                            break;
                        case "<<" or ">>":
                            Require(integers && node.Type == leftType,
                                "shift must preserve left integer type");
                            break;
                        case "==" or "!=" or "<" or "<=" or ">" or ">=":
                            // Integer comparisons need no common arithmetic type:
                            // even i64 and u64 can be compared without narrowing.
                            bool booleanEquality = node.Operator is "==" or "!="
                                && leftType == SemanticType.Bool && rightType == SemanticType.Bool;
                            Require((integers || booleanEquality) && node.Type == SemanticType.Bool,
                                "comparison operand or result type mismatch");
                            break;
                        default:
                            // Short-circuit operators are structured If regions,
                            // never eager operations with two evaluated operands.
                            Require(false, "unknown or non-eager binary operator");
                            break;
                    }
                }

                // Lowering makes conversions explicit. Storage and ownership
                // operations preserve the exact type, including nominal identity.
                if (node.Kind is IrKind.Load or IrKind.AddressOf or IrKind.Copy
                    or IrKind.Move or IrKind.Ref || (node.Kind == IrKind.Borrow && count == 1))
                {
                    Require(node.Type == node.OperandAt(0).Definition!.Type,
                        "result must preserve operand type");
                }
                // These checks identify ownership-bearing types, not ownership
                // provenance. A reference may legitimately release the old value
                // during replacement; lifetime/transfer validation is separate.
                if (node.Kind is IrKind.Copy or IrKind.FreeArray or IrKind.Ref
                    or IrKind.Unref or IrKind.Move)
                {
                    SemanticType? held = node.OperandAt(0).Definition!.Type;
                    bool array = held?.ArrayDepth > 0;
                    bool counted = held is SemanticType value && value.ArrayDepth == 0
                        && value.Class?.IsCountedClass() == true;
                    if (node.Kind is IrKind.Copy or IrKind.FreeArray)
                    {
                        Require(array, "array ownership operation requires array");
                    }
                    else if (node.Kind is IrKind.Ref or IrKind.Unref)
                    {
                        Require(counted, "counted operation requires counted scalar object");
                    }
                    else
                    {
                        Require(array || counted, "move requires ownership-bearing type");
                    }
                }
                if (node.Kind == IrKind.Move)
                {
                    // A transfer is not another owning holder. Its single use
                    // must install ownership, not observe or release the transfer.
                    IrNode source = node.OperandAt(0).Definition!;
                    bool holder = source.Kind is IrKind.Local or IrKind.Copy or IrKind.Ref
                        or IrKind.NewArray or IrKind.NewObject or IrKind.Call or IrKind.If;
                    if (source.Kind == IrKind.Local)
                    {
                        SemanticSymbol? declaration = function.Declaration;
                        SemanticSymbol? nominal = declaration?.Scope?.Class;
                        bool borrowedSelf = declaration is not null && nominal is not null
                            && (declaration.Name == nominal.Name || declaration.IsFinalHook())
                            && declaration.FunctionScope!.ParameterCount() > 0
                            && source.Symbol == declaration.FunctionScope.ParameterAt(0);
                        holder = function.Kind != IrFunctionKind.Destruction && !borrowedSelf;
                    }
                    Require(holder, "move source is not an owning holder");
                    Require(node.UseCount() == 1, "transfer must have exactly one consumer");
                    IrOperand use = node.UseAt(0);
                    IrNode consumer = use.Consumer!;
                    bool consumes = consumer.Kind is IrKind.Return or IrKind.Yield or IrKind.NewArray;
                    if (consumer.Kind is IrKind.Store or IrKind.ArrayPush)
                    {
                        consumes = consumer.OperandCount() == 2 && consumer.OperandAt(1) == use;
                    }
                    if (consumer.Kind == IrKind.Call)
                    {
                        Require(consumer.Symbol?.FunctionScope is not null,
                            "transfer call lacks selected parameter declarations");
                        SemanticSymbol selected = consumer.Symbol!;
                        SemanticScope parameters = selected.FunctionScope!;
                        for (int argument = 0; argument < consumer.OperandCount(); argument += 1)
                        {
                            if (consumer.OperandAt(argument) != use)
                            {
                                continue;
                            }
                            bool borrowedSelf = argument == 0 && selected.Scope?.Class is SemanticSymbol nominal
                                && (selected.Name == nominal.Name || selected.IsFinalHook());
                            consumes = selected.Builtin == BuiltinKind.None
                                && argument < parameters.ParameterCount()
                                && !parameters.ParameterAt(argument).IsReference && !borrowedSelf;
                        }
                    }
                    Require(consumes, "transfer use does not consume ownership");
                }
                // Check the other direction too: validating existing Move nodes
                // cannot detect a missing transfer at an ownership-taking operand.
                for (int argument = 0; argument < count; argument += 1)
                {
                    IrNode value = node.OperandAt(argument).Definition!;
                    SemanticType? type = value.Type;
                    bool owned = type?.ArrayDepth > 0
                        || (type is SemanticType scalar && scalar.ArrayDepth == 0
                            && scalar.Class?.IsCountedClass() == true);
                    if (!owned)
                    {
                        continue;
                    }
                    bool consumes = node.Kind is IrKind.Return or IrKind.Yield or IrKind.NewArray;
                    if (node.Kind is IrKind.Store or IrKind.ArrayPush)
                    {
                        consumes = argument == 1;
                    }
                    if (node.Kind == IrKind.Call)
                    {
                        SemanticSymbol selected = node.Symbol!;
                        bool borrowedSelf = argument == 0 && selected.Scope?.Class is SemanticSymbol nominal
                            && (selected.Name == nominal.Name || selected.IsFinalHook());
                        consumes = selected.Builtin == BuiltinKind.None
                            && !selected.FunctionScope!.ParameterAt(argument).IsReference && !borrowedSelf;
                    }
                    if (consumes)
                    {
                        Require(value.Kind == IrKind.Move,
                            "ownership-consuming operand requires explicit transfer");
                    }
                }
                if (node.Kind == IrKind.Store)
                {
                    Require(node.OperandAt(0).Definition!.Type
                        == node.OperandAt(1).Definition!.Type, "store type mismatch");
                    Require(node.OperandAt(1).Definition!.Result == IrResult.Value,
                        "store requires a value");
                }
                if (node.Kind == IrKind.NewArray)
                {
                    SemanticType array = node.Type!.Value;
                    Require(array.ArrayDepth > 0, "array construction requires array type");
                    if (node.Literal is not null)
                    {
                        Require(array == SemanticType.ByteArray, "byte payload requires byte array");
                    }
                    else
                    {
                        SemanticType element = SemanticTypes.ElementOf(array);
                        for (int elementIndex = 0; elementIndex < count; elementIndex += 1)
                        {
                            IrNode value = node.OperandAt(elementIndex).Definition!;
                            Require(value.Type == element && value.Result == IrResult.Value,
                                "array element type or category mismatch");
                        }
                    }
                }
                if (node.Kind == IrKind.SelectIndex)
                {
                    SemanticType? array = node.OperandAt(0).Definition!.Type;
                    SemanticType? index = node.OperandAt(1).Definition!.Type;
                    Require(array is not null && array.Value.ArrayDepth > 0,
                        "index selection requires array");
                    Require(node.Type == SemanticTypes.ElementOf(array!.Value),
                        "selected element type mismatch");
                    Require(index is not null && SemanticTypes.IsInteger(index.Value)
                        && node.OperandAt(1).Definition!.Result == IrResult.Value,
                        "index requires integer value");
                }

                if (node.Kind is IrKind.ArrayLength or IrKind.ArrayCapacity
                    or IrKind.ArrayReserve or IrKind.ArrayResize or IrKind.ArrayTruncate
                    or IrKind.ArrayPush)
                {
                    IrNode arrayOperand = node.OperandAt(0).Definition!;
                    SemanticType? arrayType = arrayOperand.Type;
                    Require(arrayType is not null && arrayType.Value.ArrayDepth > 0,
                        "array operation requires array operand");
                    bool metadata = node.Kind is IrKind.ArrayLength or IrKind.ArrayCapacity;
                    if (metadata)
                    {
                        Require(node.Type == new SemanticType(ScalarKind.U64, 0),
                            "array metadata result must be u64");
                        // Length follows ordinary value evaluation. Capacity may
                        // observe storage directly so copying cannot lose reservation.
                        Require(node.Kind != IrKind.ArrayLength
                            || arrayOperand.Result == IrResult.Value,
                            "array length requires evaluated value");
                    }
                    else
                    {
                        Require(node.Type == SemanticType.Unit,
                            "array mutation result must be unit");
                        Require(arrayOperand.Result is IrResult.Place or IrResult.Address,
                            "array mutation requires selected storage");
                        IrNode argument = node.OperandAt(1).Definition!;
                        Require(argument.Result == IrResult.Value,
                            "array mutation argument must be value");
                        if (node.Kind == IrKind.ArrayPush)
                        {
                            Require(argument.Type == SemanticTypes.ElementOf(arrayType!.Value),
                                "array push element type mismatch");
                        }
                        else
                        {
                            Require(argument.Type is not null
                                && SemanticTypes.IsInteger(argument.Type.Value),
                                "array mutation count must be integer");
                        }
                    }
                }

                // Handles are not interchangeable integers. Preserve the selected
                // class and field declarations through storage and destruction.
                if (node.Kind == IrKind.SelectField)
                {
                    SemanticSymbol? field = node.Symbol;
                    IrNode receiver = node.OperandAt(0).Definition!;
                    SemanticSymbol? owner = field?.Scope?.Class;
                    Require(field?.Kind == SymbolKind.Field && owner is not null,
                        "field selection has no owning class");
                    Require(receiver.Result == IrResult.Value
                        && receiver.Type == new SemanticType(ScalarKind.Unit, 0, owner!),
                        "field receiver class mismatch");
                    Require(node.Type == field!.Type, "selected field type mismatch");
                }
                if (node.Kind is IrKind.NewObject or IrKind.DestroyObject or IrKind.ReclaimObject)
                {
                    SemanticSymbol? nominal = node.Symbol;
                    Require(nominal?.Kind == SymbolKind.Class,
                        "object operation has no selected class");
                    SemanticType instance = new(ScalarKind.Unit, 0, nominal!);
                    if (node.Kind == IrKind.NewObject)
                    {
                        Require(node.Type == instance, "allocated object class mismatch");
                    }
                    else
                    {
                        IrNode receiver = node.OperandAt(0).Definition!;
                        Require(receiver.Type == instance, "object receiver class mismatch");
                        if (node.Kind == IrKind.DestroyObject)
                        {
                            Require(receiver.Result == IrResult.Value
                                && node.Type == SemanticType.Unit,
                                "destruction requires value handle and unit result");
                        }
                        else
                        {
                            Require(receiver.Result == IrResult.Address,
                                "reclamation requires receiver storage");
                            Require(function.Kind == IrFunctionKind.Destruction
                                && function.DestructionClass == nominal,
                                "reclamation outside matching destruction body");
                        }
                    }
                }

                void Require(bool valid, string reason)
                {
                    if (!valid)
                    {
                        throw Invalid(node, reason);
                    }
                }
            }
        }

        static InvalidOperationException Invalid(IrNode node, string reason)
        {
            return new InvalidOperationException("Invalid IR operation " + node.Kind + ": " + reason + ".");
        }
    }

}
