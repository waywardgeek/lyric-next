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
            if (function.Kind == IrFunctionKind.FieldInitializer)
            {
                SemanticSymbol? field = function.Declaration;
                Require(field is not null && !field.Destroyed && field.Kind == SymbolKind.Field
                    && field.IrBody == function && field.Scope is not null
                    && field.Scope.Analysis == Analysis
                    && field.Scope.MemberOwner?.Kind == SymbolKind.Struct
                    && field.InitializerScope is not null && !field.InitializerScope.Destroyed
                    && field.InitializerScope.InitializerField == field
                    && function.DestructionClass is null, "field initializer owner");
            }
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
        static void ReceiverRequire(bool valid, string reason)
        {
            if (!valid)
            {
                throw new InvalidOperationException("Invalid IR ownership: " + reason + ".");
            }
        }
        // Run after structural validation: all definitions and arena memberships
        // exist. Keep this pass separate from execution-order/ownership analysis.
        for (int f = 0; f < FunctionCount(); f += 1)
        {
            IrFunction function = FunctionAt(f);
            if (function.ObservesReceiver)
            {
                SemanticSymbol? declaration = function.Declaration;
                SemanticScope? signature = declaration?.FunctionScope;
                ReceiverRequire(function.Kind == IrFunctionKind.Source
                    && declaration?.Scope?.Class?.IsCountedClass() == true
                    && signature is not null && signature.ParameterCount() > 0,
                    "observing receiver requires a counted instance method");
                SemanticSymbol receiverSymbol = signature!.ParameterAt(0);
                ReceiverRequire(receiverSymbol.Name == "self" && !receiverSymbol.IsReference
                    && receiverSymbol.Type?.Kind == SemanticTypeKind.Nominal
                    && receiverSymbol.Type.Class == declaration!.Scope!.Class,
                    "observing receiver is not the declared instance receiver");
                IrNode? receiver = null;
                for (int n = 0; n < function.NodeCount(); n += 1)
                {
                    IrNode candidate = function.NodeAt(n);
                    if (candidate.Kind == IrKind.Local && candidate.Symbol == receiverSymbol)
                    {
                        ReceiverRequire(receiver is null, "duplicate observing receiver");
                        receiver = candidate;
                    }
                }
                ReceiverRequire(receiver is not null, "missing observing receiver");
                for (int u = 0; u < receiver!.UseCount(); u += 1)
                {
                    IrNode load = receiver.UseAt(u).Consumer!;
                    ReceiverRequire(load.Kind == IrKind.Load,
                        "observing receiver cannot be retained, released, assigned or transferred");
                    for (int v = 0; v < load.UseCount(); v += 1)
                    {
                        IrOperand use = load.UseAt(v);
                        IrNode consumer = use.Consumer!;
                        bool observation = consumer.Kind == IrKind.Binary
                            && consumer.Operator is "==" or "!=";
                        observation |= consumer.Kind == IrKind.SelectField
                            && consumer.OperandAt(0) == use;
                        observation |= consumer.Kind == IrKind.Call
                            && consumer.OperandCount() > 0 && consumer.OperandAt(0) == use
                            && consumer.Symbol?.IrBody?.BorrowsReceiver() == true;
                        ReceiverRequire(observation, "observing receiver escapes its borrowed contract");
                    }
                }
            }
            // Account for absence as well as malformed existing instructions.
            // Ordinary holders cannot stand in for the membership's one count.
            SemanticSymbol? declaredOperation = function.Declaration;
            RelationshipOperation designation = declaredOperation?.RelationshipOperationKind()
                ?? RelationshipOperation.None;
            bool countedMembership = function.Kind == IrFunctionKind.Source
                && designation != RelationshipOperation.None
                && declaredOperation!.ContributingParticipant!.Instantiation!.Policy == RelationshipPolicy.Refs
                && declaredOperation.FunctionScope!.ParameterCount() == 2
                && declaredOperation.FunctionScope.ParameterAt(1).Type!.Class!.IsCountedClass();
            int retains = 0;
            int releases = 0;
            for (int index = 0; index < function.NodeCount(); index += 1)
            {
                IrKind kind = function.NodeAt(index).Kind;
                if (kind == IrKind.MembershipRetain)
                {
                    retains += 1;
                }
                if (kind == IrKind.MembershipRelease)
                {
                    releases += 1;
                }
            }
            int expectedRetains = countedMembership && designation == RelationshipOperation.Add ? 1 : 0;
            int expectedReleases = countedMembership && designation == RelationshipOperation.Remove
                && function.Body!.Origin!.Fact!.CanContinue ? 1 : 0;
            if (retains != expectedRetains || releases != expectedReleases)
            {
                throw new InvalidOperationException("Invalid IR operation: membership accounting count.");
            }

            for (int i = 0; i < function.NodeCount(); i += 1)
            {
                IrNode node = function.NodeAt(i);
                int count = node.OperandCount();
                int required = node.Kind switch
                {
                    IrKind.Region or IrKind.While or IrKind.Break or IrKind.Continue
                        or IrKind.Constant or IrKind.NullHandle or IrKind.Local or IrKind.NewObject
                        or IrKind.CallInitializer => 0,
                    IrKind.If or IrKind.Return or IrKind.Yield or IrKind.SelectField
                        or IrKind.AddressOf or IrKind.Load or IrKind.Unary or IrKind.Convert
                        or IrKind.WrapNullable or IrKind.UnwrapNullable
                        or IrKind.Copy or IrKind.Move or IrKind.Ref or IrKind.Unref
                        or IrKind.MembershipRetain or IrKind.MembershipRelease
                        or IrKind.FreeArray or IrKind.DestroyObject or IrKind.ReclaimObject
                        or IrKind.ArrayLength or IrKind.ArrayCapacity
                        or IrKind.SelectTuple or IrKind.DropTuple
                        or IrKind.SelectStruct or IrKind.DropStruct => 1,
                    IrKind.SelectIndex or IrKind.Store or IrKind.Binary or IrKind.ArrayReserve
                        or IrKind.ArrayResize or IrKind.ArrayTruncate or IrKind.ArrayPush => 2,
                    // These have explicit variable-arity contracts below.
                    IrKind.Borrow or IrKind.Call or IrKind.NewArray or IrKind.NewTuple or IrKind.NewStruct => -1,
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
                if (node.Kind == IrKind.CallInitializer)
                {
                    SemanticSymbol? field = node.Symbol;
                    Require(field is not null && field.Kind == SymbolKind.Field
                        && field.Scope?.MemberOwner?.Kind == SymbolKind.Struct
                        && field.InitializerScope is not null
                        && field.InitializerScope.InitializerField == field,
                        "initializer call has no selected field scope");
                    Require(field!.IrBody is IrFunction initializer
                        && initializer.Kind == IrFunctionKind.FieldInitializer
                        && initializer.Program == this && initializer.Declaration == field
                        && !initializer.Destroyed, "initializer call has no live body");
                    Require(node.Type == field.Type, "initializer call result type mismatch");
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
                        or IrKind.MembershipRetain or IrKind.MembershipRelease
                        or IrKind.FreeArray or IrKind.DropTuple or IrKind.DropStruct
                        or IrKind.ReclaimObject => IrResult.None,
                    IrKind.Local or IrKind.Borrow or IrKind.AddressOf or IrKind.SelectIndex or IrKind.SelectTuple or IrKind.SelectStruct
                        => IrResult.Address,
                    IrKind.SelectField => IrResult.Place,
                    IrKind.CallInitializer when node.Symbol!.IsReference => IrResult.Address,
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
                    SemanticType type = node.Type!;
                    if (type == Analysis!.ScalarType(ScalarKind.Unit))
                    {
                        Require(node.Literal is null, "unit constant has a payload");
                    }
                    else if (type == Analysis!.ScalarType(ScalarKind.Bool))
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

                if (node.Kind == IrKind.NullHandle)
                {
                    Require(node.Type!.Kind == SemanticTypeKind.Nullable
                        && node.Literal is null, "null requires a nullable class type and no payload");
                }
                if (node.Kind is IrKind.WrapNullable or IrKind.UnwrapNullable)
                {
                    IrNode input = node.OperandAt(0).Definition!;
                    Require(input.Result == IrResult.Value && input.Type is not null,
                        "nullable conversion requires a typed value");
                    SemanticType nullable = node.Kind == IrKind.WrapNullable
                        ? node.Type! : input.Type!;
                    SemanticType payload = node.Kind == IrKind.WrapNullable
                        ? input.Type! : node.Type!;
                    Require(nullable.Kind == SemanticTypeKind.Nullable
                        && nullable.Element == payload,
                        "nullable conversion must preserve the exact payload type");
                }

                // Region results and function returns have different destinations.
                // This checks their local value contract, not path termination.
                if (node.Kind is IrKind.If or IrKind.Return or IrKind.Yield)
                {
                    IrNode value = node.OperandAt(0).Definition!;
                    SemanticType? destination = null;
                    if (node.Kind == IrKind.If)
                    {
                        destination = Analysis!.ScalarType(ScalarKind.Bool);
                    }
                    else if (node.Kind == IrKind.Return)
                    {
                        destination = function.Kind == IrFunctionKind.Destruction
                            ? Analysis!.ScalarType(ScalarKind.Unit) : function.Declaration?.Type;
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
                            destination = Analysis!.ScalarType(ScalarKind.Bool);
                        }
                    }
                    Require(destination is not null, "missing control result destination");
                    bool addressReturn = node.Kind == IrKind.Return
                        && function.Kind == IrFunctionKind.FieldInitializer
                        && function.Declaration!.IsReference;
                    IrResult category = addressReturn ? IrResult.Address : IrResult.Value;
                    Require(value.Result == category && value.Type == destination,
                        "control operand must match the destination type and category");
                }

                // Operator tags preserve semantics: a wrapping conversion is not
                // interchangeable with a checked cast, even when types coincide.
                if (node.Kind is IrKind.Unary or IrKind.Convert)
                {
                    IrNode input = node.OperandAt(0).Definition!;
                    Require(input.Result == IrResult.Value && input.Type is not null,
                        "operator requires typed value");
                    SemanticType source = input.Type!;
                    SemanticType result = node.Type!;
                    if (node.Kind == IrKind.Unary)
                    {
                        Require(node.Operator is "!" or "-" or "!-", "unknown unary operator");
                        if (node.Operator == "!")
                        {
                            Require(source == Analysis!.ScalarType(ScalarKind.Bool) && result == Analysis!.ScalarType(ScalarKind.Bool),
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
                    SemanticType leftType = left.Type!;
                    SemanticType rightType = right.Type!;
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
                                && leftType == Analysis!.ScalarType(ScalarKind.Bool) && rightType == Analysis!.ScalarType(ScalarKind.Bool);
                            bool handleEquality = node.Operator is "==" or "!="
                                && SemanticTypes.CanCompareHandleIdentity(leftType, rightType);
                            Require((integers || booleanEquality || handleEquality) && node.Type == Analysis!.ScalarType(ScalarKind.Bool),
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
                    or IrKind.Unref or IrKind.Move or IrKind.DropTuple or IrKind.DropStruct)
                {
                    SemanticType? held = node.OperandAt(0).Definition!.Type;
                    bool array = held?.ArrayDepth > 0;
                    bool counted = held is SemanticType value && value.ArrayDepth == 0
                        && value.Class?.IsCountedClass() == true;
                    if (node.Kind == IrKind.Copy)
                    {
                        Require(array || held?.Kind == SemanticTypeKind.Tuple
                            || held?.Declaration?.Kind == SymbolKind.Struct,
                            "copy requires array, tuple, or struct");
                    }
                    else if (node.Kind == IrKind.FreeArray)
                    {
                        Require(array, "array ownership operation requires array");
                    }
                    else if (node.Kind == IrKind.DropTuple)
                    {
                        Require(held?.Kind == SemanticTypeKind.Tuple
                            && SemanticTypes.HasOwnedContents(held),
                            "tuple cleanup requires ownership-bearing tuple");
                    }
                    else if (node.Kind == IrKind.DropStruct)
                    {
                        Require(held?.Declaration?.Kind == SymbolKind.Struct
                            && SemanticTypes.HasOwnedContents(held),
                            "struct cleanup requires ownership-bearing struct");
                    }
                    else if (node.Kind is IrKind.Ref or IrKind.Unref)
                    {
                        Require(counted, "counted operation requires counted scalar object");
                        if (node.Kind == IrKind.Unref)
                        {
                            IrNode source = node.OperandAt(0).Definition!;
                            Require(!source.IsBorrowedHandleValue(),
                                "cannot release a borrowed handle value");
                            Require(source.Kind != IrKind.Local
                                || (function.Declaration?.BorrowsParticipantParameter(source.Symbol) != true
                                    && function.Declaration?.BorrowsParticipantLocal(source.Symbol) != true),
                                "cannot release borrowed participant storage");
                        }
                    }
                    else
                    {
                        Require(held is not null && SemanticTypes.HasOwnedContents(held),
                            "move requires ownership-bearing type");
                    }
                }
                if (node.Kind is IrKind.MembershipRetain or IrKind.MembershipRelease)
                {
                    SemanticSymbol? operation = function.Declaration;
                    ParticipantBinding? endpoint = operation?.ContributingParticipant;
                    MultiClassInstantiation? instance = endpoint?.Instantiation;
                    RelationshipOperation expectedOperation = node.Kind == IrKind.MembershipRetain
                        ? RelationshipOperation.Add : RelationshipOperation.Remove;
                    Require(function.Kind == IrFunctionKind.Source
                        && instance?.Policy == RelationshipPolicy.Refs
                        && operation!.RelationshipOperationKind() == expectedOperation,
                        "membership accounting requires its designated refs operation");
                    SemanticScope? signature = operation!.FunctionScope;
                    Require(signature?.ParameterCount() == 2, "membership accounting signature");
                    SemanticSymbol child = signature!.ParameterAt(1);
                    IrNode holder = node.OperandAt(0).Definition!;
                    Require(holder.Kind == IrKind.Local && holder.Symbol == child
                        && child.ParticipantView?.Instantiation == instance
                        && instance!.Template!.ParticipantRoleAt(child.ParticipantView!.Position)
                            == ParticipantRole.Child
                        && child.Type?.Kind == SemanticTypeKind.Nominal
                        && child.Type.Class?.IsCountedClass() == true,
                        "membership accounting requires its counted child parameter holder");
                    Require(node.Parent == function.Body,
                        "membership accounting must execute in the outer operation body");
                    Require(node.Kind == IrKind.MembershipRetain
                        ? node.Origin?.Kind == SyntaxKind.Prepare
                            && node.Origin.Parent == function.Body!.Origin
                        : node.Origin == function.Body!.Origin,
                        "membership accounting has the wrong phase boundary");
                    // Origin identifies the work's phase, not its scheduled
                    // position. Check the actual outer-region sequence as well:
                    // preparation precedes accounting; structural work follows
                    // retain or precedes release; final cleanup follows both.
                    // Nested work executes with its enclosing If/While node.
                    IrNode body = function.Body!;
                    SyntaxNode bodySyntax = body.Origin!;
                    bool passedAccounting = false;
                    for (int index = 0; index < body.ChildCount(); index += 1)
                    {
                        IrNode scheduled = body.ChildAt(index);
                        if (scheduled == node)
                        {
                            passedAccounting = true;
                            continue;
                        }
                        SyntaxNode? source = scheduled.Origin;
                        if (source == bodySyntax)
                        {
                            Require(passedAccounting,
                                "operation cleanup precedes membership accounting");
                            continue;
                        }
                        while (source is not null && source.Parent != bodySyntax)
                        {
                            source = source.Parent;
                        }
                        if (source is null)
                        {
                            // Parameter setup has declaration origins outside
                            // the body. Availability is checked separately.
                            continue;
                        }
                        if (source.Kind == SyntaxKind.Prepare)
                        {
                            Require(!passedAccounting,
                                "membership accounting precedes preparation");
                        }
                        else
                        {
                            Require(passedAccounting == (node.Kind == IrKind.MembershipRetain),
                                "membership accounting crosses structural work");
                        }
                    }
                    Require(node.Type is null && node.Symbol is null,
                        "membership accounting has no result or duplicate binding identity");
                }
                if (node.Kind == IrKind.Move)
                {
                    // A transfer is not another owning holder. Its single use
                    // must install ownership, not observe or release the transfer.
                    IrNode source = node.OperandAt(0).Definition!;
                    bool holder = source.Kind is IrKind.Local or IrKind.Copy or IrKind.Ref
                        or IrKind.NewArray or IrKind.NewTuple or IrKind.NewStruct or IrKind.NewObject or IrKind.Call or IrKind.CallInitializer or IrKind.If
                        or IrKind.NullHandle or IrKind.WrapNullable or IrKind.UnwrapNullable;
                    if (source.Kind == IrKind.Local)
                    {
                        SemanticSymbol? declaration = function.Declaration;
                        SemanticSymbol? nominal = declaration?.Scope?.Class;
                        bool borrowedSelf = declaration is not null && nominal is not null
                            && function.BorrowsReceiver()
                            && declaration.FunctionScope!.ParameterCount() > 0
                            && source.Symbol == declaration.FunctionScope.ParameterAt(0);
                        holder = function.Kind != IrFunctionKind.Destruction && !borrowedSelf
                            && declaration?.BorrowsParticipantParameter(source.Symbol) != true
                            && declaration?.BorrowsParticipantLocal(source.Symbol) != true;
                    }
                    Require(holder && !source.IsBorrowedHandleValue(), "move source is not an owning holder");
                    Require(node.UseCount() == 1, "transfer must have exactly one consumer");
                    IrOperand use = node.UseAt(0);
                    IrNode consumer = use.Consumer!;
                    bool consumes = consumer.Kind is IrKind.Return or IrKind.Yield or IrKind.NewArray or IrKind.NewTuple or IrKind.NewStruct
                        or IrKind.WrapNullable or IrKind.UnwrapNullable;
                    if (consumer.Kind is IrKind.Store or IrKind.ArrayPush)
                    {
                        consumes = consumer.OperandCount() == 2 && consumer.OperandAt(1) == use;
                        IrNode destination = consumer.OperandAt(0).Definition!;
                        if (consumer.Kind == IrKind.Store && destination.IsManagedHandleStorage())
                        {
                            consumes = false;
                        }
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
                            bool borrowedSelf = argument == 0
                                && selected.IrBody?.BorrowsReceiver() == true;
                            consumes = selected.Builtin == BuiltinKind.None
                                && argument < parameters.ParameterCount()
                                && !parameters.ParameterAt(argument).IsReference && !borrowedSelf
                                && !selected.BorrowsParticipantParameter(parameters.ParameterAt(argument));
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
                    bool owned = type is not null && SemanticTypes.HasOwnedContents(type);
                    if (!owned)
                    {
                        continue;
                    }
                    bool consumes = node.Kind is IrKind.Return or IrKind.Yield or IrKind.NewArray or IrKind.NewTuple or IrKind.NewStruct
                        or IrKind.WrapNullable or IrKind.UnwrapNullable;
                    if (node.Kind is IrKind.WrapNullable or IrKind.UnwrapNullable
                        && value.IsBorrowedHandleValue())
                    {
                        consumes = false;
                    }
                    // Returning a borrowed address never consumes its referent,
                    // even when that referent contains owned arrays or handles.
                    if (node.Kind == IrKind.Return && function.Kind == IrFunctionKind.FieldInitializer
                        && function.Declaration!.IsReference)
                    {
                        consumes = false;
                    }
                    if (node.Kind is IrKind.Store or IrKind.ArrayPush)
                    {
                        consumes = argument == 1;
                        IrNode destination = node.OperandAt(0).Definition!;
                        if (node.Kind == IrKind.Store && destination.IsManagedHandleStorage())
                        {
                            consumes = false;
                        }
                    }
                    if (node.Kind == IrKind.NewStruct)
                    {
                        // A borrowed field stores an address, not ownership of
                        // the referent, even when that referent is an array.
                        int fieldIndex = 0;
                        foreach (SemanticSymbol field in node.Type!.Declaration!.MemberScope!.Fields())
                        {
                            if (fieldIndex == argument)
                            {
                                consumes = !field.IsReference;
                                break;
                            }
                            fieldIndex += 1;
                        }
                    }
                    if (node.Kind == IrKind.Call)
                    {
                        SemanticSymbol selected = node.Symbol!;
                        bool borrowedSelf = argument == 0
                            && selected.IrBody?.BorrowsReceiver() == true;
                        consumes = selected.Builtin == BuiltinKind.None
                            && !selected.FunctionScope!.ParameterAt(argument).IsReference && !borrowedSelf
                            && !selected.BorrowsParticipantParameter(selected.FunctionScope.ParameterAt(argument));
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
                if (node.Kind == IrKind.NewStruct)
                {
                    SemanticType structure = node.Type!;
                    SemanticScope? fields = structure.Declaration?.MemberScope;
                    Require(structure.Declaration?.Kind == SymbolKind.Struct
                        && fields is not null && node.Literal is null,
                        "struct construction requires resolved nominal struct");
                    Require(SemanticTypes.HasFiniteLayout(structure), "infinite struct layout");
                    int fieldIndex = 0;
                    foreach (SemanticSymbol field in fields!.Fields())
                    {
                        Require(fieldIndex < count, "missing struct field operand");
                        IrNode value = node.OperandAt(fieldIndex).Definition!;
                        IrResult category = field.IsReference ? IrResult.Address : IrResult.Value;
                        Require(value.Type == field.Type && value.Result == category,
                            "struct field type or category mismatch");
                        fieldIndex += 1;
                    }
                    Require(fieldIndex == count, "extra struct field operand");
                }
                if (node.Kind == IrKind.SelectStruct)
                {
                    IrNode holder = node.OperandAt(0).Definition!;
                    SemanticSymbol? field = node.Symbol;
                    Require(holder.Type?.Declaration?.Kind == SymbolKind.Struct
                        && holder.Result is IrResult.Value or IrResult.Address,
                        "struct selection requires evaluated holder or fixed address");
                    Require(field?.Kind == SymbolKind.Field
                        && field.Scope?.MemberOwner == holder.Type!.Declaration
                        && node.Type == field.Type && node.Literal is null,
                        "struct selection requires matching nominal field identity");
                }
                if (node.Kind == IrKind.NewTuple)
                {
                    SemanticType tuple = node.Type!;
                    Require(tuple.Kind == SemanticTypeKind.Tuple && node.Literal is null,
                        "tuple construction requires tuple type and component operands");
                    Require(count == tuple.ComponentCount(), "tuple component count");
                    for (int component = 0; component < count; component += 1)
                    {
                        IrNode value = node.OperandAt(component).Definition!;
                        Require(value.Type == tuple.ComponentAt(component)
                            && value.Result == IrResult.Value,
                            "tuple component type or category mismatch");
                    }
                }
                if (node.Kind == IrKind.SelectTuple)
                {
                    IrNode holder = node.OperandAt(0).Definition!;
                    Require(holder.Type?.Kind == SemanticTypeKind.Tuple
                        && holder.Result is IrResult.Value or IrResult.Address,
                        "tuple selection requires evaluated holder or fixed address");
                    IrLiteral? selector = node.Literal;
                    Require(selector?.Kind == IrLiteralKind.Integer
                        && selector.Integer >= 0
                        && selector.Integer < holder.Type!.ComponentCount(),
                        "tuple selector outside component range");
                    Require(node.Type == holder.Type!.ComponentAt((int)selector!.Integer),
                        "tuple selection component type mismatch");
                }
                if (node.Kind == IrKind.NewArray)
                {
                    SemanticType array = node.Type!;
                    Require(array.ArrayDepth > 0, "array construction requires array type");
                    if (node.Literal is not null)
                    {
                        Require(array == SemanticTypes.ArrayOf(Analysis!.ScalarType(ScalarKind.U8)), "byte payload requires byte array");
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
                    Require(array is not null && array!.ArrayDepth > 0,
                        "index selection requires array");
                    Require(node.Type == SemanticTypes.ElementOf(array!),
                        "selected element type mismatch");
                    Require(index is not null && SemanticTypes.IsInteger(index!)
                        && node.OperandAt(1).Definition!.Result == IrResult.Value,
                        "index requires integer value");
                }

                if (node.Kind is IrKind.ArrayLength or IrKind.ArrayCapacity
                    or IrKind.ArrayReserve or IrKind.ArrayResize or IrKind.ArrayTruncate
                    or IrKind.ArrayPush)
                {
                    IrNode arrayOperand = node.OperandAt(0).Definition!;
                    SemanticType? arrayType = arrayOperand.Type;
                    Require(arrayType is not null && arrayType!.ArrayDepth > 0,
                        "array operation requires array operand");
                    bool metadata = node.Kind is IrKind.ArrayLength or IrKind.ArrayCapacity;
                    if (metadata)
                    {
                        Require(node.Type == Analysis!.ScalarType(ScalarKind.U64),
                            "array metadata result must be u64");
                        // Length follows ordinary value evaluation. Capacity may
                        // observe storage directly so copying cannot lose reservation.
                        Require(node.Kind != IrKind.ArrayLength
                            || arrayOperand.Result == IrResult.Value,
                            "array length requires evaluated value");
                    }
                    else
                    {
                        Require(node.Type == Analysis!.ScalarType(ScalarKind.Unit),
                            "array mutation result must be unit");
                        Require(arrayOperand.Result is IrResult.Place or IrResult.Address,
                            "array mutation requires selected storage");
                        IrNode argument = node.OperandAt(1).Definition!;
                        Require(argument.Result == IrResult.Value,
                            "array mutation argument must be value");
                        if (node.Kind == IrKind.ArrayPush)
                        {
                            Require(argument.Type == SemanticTypes.ElementOf(arrayType!),
                                "array push element type mismatch");
                        }
                        else
                        {
                            Require(argument.Type is not null
                                && SemanticTypes.IsInteger(argument.Type!),
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
                        && receiver.Type == Analysis!.ClassType(owner!),
                        "field receiver class mismatch");
                    Require(node.Type == field!.Type, "selected field type mismatch");
                }
                if (node.Kind is IrKind.NewObject or IrKind.DestroyObject or IrKind.ReclaimObject)
                {
                    SemanticSymbol? nominal = node.Symbol;
                    Require(nominal?.Kind == SymbolKind.Class,
                        "object operation has no selected class");
                    SemanticType instance = Analysis!.ClassType(nominal!);
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
                                && node.Type == Analysis!.ScalarType(ScalarKind.Unit),
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
