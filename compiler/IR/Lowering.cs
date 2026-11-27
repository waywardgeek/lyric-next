using System;
using System.Collections.Generic;

namespace Lyric;

internal sealed partial class Compilation
{
    internal partial Diagnostic? LowerIr()
    {
        if (Analysis is null || Analysis.Destroyed)
        {
            throw new InvalidOperationException("Check before lowering IR.");
        }
        Analysis.ClearIr();
        IrProgram program = new(Analysis);
        try
        {
            // Establish every concrete body identity before lowering any calls.
            foreach (SourceFile source in Sources)
            {
                SyntaxNode root = source.SyntaxRoot()!;
                for (int i = 0; i < root.ChildCount(); i += 1)
                {
                    SyntaxNode declaration = root.ChildAt(i);
                    if (declaration.Kind == SyntaxKind.Class)
                    {
                        SemanticSymbol nominal = declaration.DeclaredSymbol!;
                        _ = new IrFunction(program, nominal, IrFunctionKind.Destruction);
                        // Contributions belong to semantic scopes, not the parsed
                        // class tree. Discover the actual concrete members once.
                        foreach (SemanticSymbol field in nominal.MemberScope!.Fields())
                        {
                            SupportedType(field.Type!, field.Declaration!);
                        }
                        foreach (SemanticSymbol method in nominal.MemberScope.Methods())
                        {
                            SupportedType(method.Type!, method.Declaration!);
                            _ = new IrFunction(program, method);
                        }
                        continue;
                    }
                    if (declaration.Kind == SyntaxKind.Struct)
                    {
                        // Methods are ordinary functions; structs do not acquire a
                        // class destruction body or allocate an object receiver.
                        for (int m = 0; m < declaration.ChildCount(); m += 1)
                        {
                            SyntaxNode member = declaration.ChildAt(m);
                            if (member.DeclaredSymbol is SemanticSymbol method
                                && method.Kind == SymbolKind.Function)
                            {
                                SupportedType(method.Type!, member);
                                _ = new IrFunction(program, method);
                            }
                        }
                        // Field defaults retain their distinct execution role.
                        foreach (SemanticSymbol field in declaration.DeclaredSymbol!.MemberScope!.Fields())
                        {
                            if (field.InitializerScope is not null)
                            {
                                _ = new IrFunction(program, field, IrFunctionKind.FieldInitializer);
                            }
                        }
                        continue;
                    }
                    if (declaration.Kind is SyntaxKind.Interface or SyntaxKind.Impl)
                    {
                        // Their concrete contributions were discovered through
                        // the owning classes; templates have no executable body.
                        continue;
                    }
                    if (declaration.Kind != SyntaxKind.Function)
                    {
                        Unsupported(declaration);
                    }
                    // The original member declaration registered this callable.
                    if (declaration.ImplementedSymbol is not null)
                    {
                        continue;
                    }
                    SemanticSymbol symbol = declaration.DeclaredSymbol!;
                    if (symbol.GenericParameters() is not null)
                    {
                        continue;
                    }
                    SupportedType(symbol.Type!, declaration);
                    _ = new IrFunction(program, symbol);
                }
            }
            foreach (GenericSpecialization specialization in Analysis.ConcreteFunctions())
            {
                SemanticSymbol function = specialization.Function!;
                SupportedType(function.Type!, function.Declaration!);
                _ = new IrFunction(program, function);
            }
            for (int i = 0; i < program.FunctionCount(); i += 1)
            {
                IrFunction function = program.FunctionAt(i);
                if (function.Kind == IrFunctionKind.Destruction)
                {
                    LowerDestruction(function);
                }
                else
                {
                    LowerFunction(function);
                }
            }
            program.ValidateStructure();
            program.ValidateOperations();
            program.ValidateExecution();
            program.SimplifyOwnership();
            program.ValidateStructure();
            program.ValidateOperations();
            program.ValidateExecution();
            return null;
        }
        catch (SemanticFailure failure)
        {
            program.Destroy();
            return failure.Diagnostic;
        }
        catch
        {
            program.Destroy();
            throw;
        }

        void Unsupported(SyntaxNode node)
        {
            throw new SemanticFailure(node.Source!.SemanticDiagnostic(node, "LYC340",
                "This construct is not supported by the current IR lowering slice."));
        }
        void SupportedType(SemanticType type, SyntaxNode node)
        {
            if (SemanticTypes.IsFloat(type))
            {
                Unsupported(node);
            }
        }
        void LowerDestruction(IrFunction function)
        {
            SemanticSymbol nominal = function.DestructionClass!;
            SemanticType receiverType = Analysis!.ClassType(nominal);
            SyntaxNode origin = nominal.Declaration!;
            IrNode body = new(function, IrKind.Region, IrRole.Body, origin: origin);
            function.AddBody(body);
            // Generated parameters have execution storage, not invented source symbols.
            IrNode receiver = new(function, IrKind.Local, result: IrResult.Address,
                type: receiverType, origin: origin);
            body.AddChild(receiver);
            if (nominal.FinalHook() is SemanticSymbol hook)
            {
                // The hook observes live fields. Keep only the stable handle across
                // its calls: class storage may grow before reclamation resumes.
                IrNode handle = new(function, IrKind.Load, result: IrResult.Value,
                    type: receiverType, origin: origin);
                body.AddChild(handle);
                _ = new IrOperand(handle, receiver);
                IrNode call = new(function, IrKind.Call, result: IrResult.Value,
                    type: Analysis!.ScalarType(ScalarKind.Unit), origin: origin, symbol: hook);
                body.AddChild(call);
                _ = new IrOperand(call, handle);
            }
            // A class may occupy both roles in independent bindings. Detach all
            // child occurrences before cleaning any parent occurrence, regardless
            // of declaration order. Calls borrow the live teardown receiver.
            foreach (ParticipantRole phase in new[] { ParticipantRole.Child, ParticipantRole.Parent })
            {
                foreach (SemanticSymbol fragment in nominal.MemberScope!.Methods())
                {
                    if (!fragment.IsDestructorFragment())
                    {
                        continue;
                    }
                    ParticipantBinding participant = fragment.ContributingParticipant!;
                    ParticipantRole role = participant.Instantiation!.Template!
                        .ParticipantRoleAt(participant.Position);
                    if (role != phase)
                    {
                        continue;
                    }
                    IrNode handle = new(function, IrKind.Load, result: IrResult.Value,
                        type: receiverType, origin: origin);
                    body.AddChild(handle);
                    _ = new IrOperand(handle, receiver);
                    IrNode call = new(function, IrKind.Call, result: IrResult.Value,
                        type: Analysis!.ScalarType(ScalarKind.Unit),
                        origin: fragment.Declaration, symbol: fragment);
                    body.AddChild(call);
                    _ = new IrOperand(call, handle);
                }
            }
            // Final observes live fields. Resolve each descriptor after the hook,
            // which may grow class storage; never retain an interior pointer across it.
            foreach (SemanticSymbol field in nominal.MemberScope!.Fields())
            {
                if (field.IsManagedHandle() || !SemanticTypes.HasOwnedContents(field.Type!))
                {
                    continue;
                }
                IrNode handle = new(function, IrKind.Load, result: IrResult.Value,
                    type: receiverType, origin: origin);
                body.AddChild(handle);
                _ = new IrOperand(handle, receiver);
                IrNode place = new(function, IrKind.SelectField, result: IrResult.Place,
                    type: field.Type, origin: field.Declaration, symbol: field);
                body.AddChild(place);
                _ = new IrOperand(place, handle);
                // Scalar counted fields own one handle; array fields own backing.
                // Both releases are explicit IR so validation and emission agree.
                IrKind releaseKind = field.Type!.ArrayDepth > 0
                    ? IrKind.FreeArray : IrKind.Unref;
                IrNode release = new(function, releaseKind, origin: field.Declaration);
                body.AddChild(release);
                _ = new IrOperand(release, place);
            }
            IrNode reclaim = new(function, IrKind.ReclaimObject, origin: origin,
                symbol: nominal);
            body.AddChild(reclaim);
            _ = new IrOperand(reclaim, receiver);
            IrNode unit = new(function, IrKind.Constant, result: IrResult.Value,
                type: Analysis!.ScalarType(ScalarKind.Unit), origin: origin);
            body.AddChild(unit);
            IrNode finish = new(function, IrKind.Return, origin: origin);
            body.AddChild(finish);
            _ = new IrOperand(finish, unit);
        }
        void LowerFunction(IrFunction function)
        {
            SemanticSymbol declaration = function.Declaration!;
            SyntaxNode source = declaration.BodySyntax()!;
            // Visibility and other declaration metadata may follow the body.
            // Select the checked block by role, not by its incidental child index.
            SyntaxNode? selectedBody = null;
            if (function.Kind == IrFunctionKind.FieldInitializer)
            {
                selectedBody = source.ChildAt(1);
            }
            else
            {
                for (int index = 0; index < source.ChildCount(); index += 1)
                {
                    SyntaxNode child = source.ChildAt(index);
                    if (child.Kind == SyntaxKind.Block)
                    {
                        selectedBody = child;
                    }
                }
            }
            if (selectedBody is null)
            {
                Unsupported(source);
            }
            SyntaxNode block = selectedBody!;
            IrNode body = new(function, IrKind.Region, IrRole.Body, origin: block);
            function.AddBody(body);
            Dictionary<SemanticSymbol, IrNode> storage = new();
            // Transient lowering bookkeeping only. The resulting Move/release
            // operations are the persistent account of ownership and cleanup.
            Dictionary<IrNode, List<IrNode>> owners = new();
            // These actions and maps are transient algorithm work, not model
            // relationships. Every scheduled action drains before this returns.
            // Scheduling avoids recursion even for deeply nested expressions.
            Stack<Action> work = new();
            SemanticScope parameters = function.Kind == IrFunctionKind.FieldInitializer
                ? declaration.InitializerScope! : declaration.FunctionScope!;
            for (int p = 0; p < parameters.ParameterCount(); p += 1)
            {
                SemanticSymbol parameter = parameters.ParameterAt(p);
                SupportedType(parameter.Type!, parameter.Declaration!);
                IrNode local = Add(body, parameter.IsReference ? IrKind.Borrow : IrKind.Local,
                    parameter.Declaration!,
                    parameter.Type, IrResult.Address, symbol: parameter);
                // Constructors/final hooks borrow self. Relationship participant
                // parameters also borrow; unrelated value parameters remain owners.
                bool borrowedSelf = p == 0 && declaration.Scope!.Class is SemanticSymbol owner
                    && (declaration.Name == owner.Name || declaration.IsFinalHook()
                        || declaration.IsDestructorFragment());
                if ((borrowedSelf || declaration.BorrowsParticipantParameter(parameter))
                    && Owned(parameter.Type))
                {
                    owners[body].Remove(local);
                }
                storage.Add(parameter, local);
            }
            if (function.Kind == IrFunctionKind.FieldInitializer)
            {
                // The final field child is its checked default expression. Use
                // the same cleanup ordering as source functions. Reference fields
                // return a fixed address instead of converting or moving a value.
                Expression(block, body, null,
                    value => End(body, IrKind.Return, block, value, declaration.Type!));
            }
            else
            {
                Block(block, body, null, IrKind.Return, declaration.Type!);
            }
            while (work.Count != 0)
            {
                work.Pop()();
            }

            bool Counted(SemanticType? type)
            {
                return type is SemanticType value && value.ArrayDepth == 0
                    && value.Class?.IsCountedClass() == true;
            }
            bool Owned(SemanticType? type)
            {
                return type is not null && SemanticTypes.HasOwnedContents(type);
            }
            IrKind ReadKind(SemanticType type)
            {
                if (type.ArrayDepth > 0 || type.Kind == SemanticTypeKind.Tuple
                    || type.Declaration?.Kind == SymbolKind.Struct)
                {
                    return IrKind.Copy;
                }
                return Counted(type) ? IrKind.Ref : IrKind.Load;
            }
            IrKind DropKind(SemanticType type)
            {
                // Both replacement and scope exit release the same kind of holder.
                // Tuple cleanup recursively releases its owned components.
                if (type.Kind == SemanticTypeKind.Tuple)
                {
                    return IrKind.DropTuple;
                }
                if (type.Declaration?.Kind == SymbolKind.Struct)
                {
                    return IrKind.DropStruct;
                }
                return Counted(type) ? IrKind.Unref : IrKind.FreeArray;
            }
            IrNode Add(IrNode region, IrKind kind, SyntaxNode origin,
                SemanticType? type = null, IrResult result = IrResult.None,
                string? operation = null, SemanticSymbol? symbol = null,
                IrLiteral? literal = null, bool registerOwner = true)
            {
                IrNode node = new(function, kind, result: result, type: type,
                    origin: origin, symbol: symbol, literal: literal, operation: operation);
                region.AddChild(node);
                bool borrowedLocal = kind == IrKind.Local
                    && declaration.BorrowsParticipantLocal(symbol);
                if (registerOwner && !borrowedLocal && Owned(type) && (kind is IrKind.Local or IrKind.Copy or IrKind.Ref
                    or IrKind.NewArray or IrKind.NewTuple or IrKind.NewStruct or IrKind.NewObject
                    or IrKind.Call or IrKind.NullHandle or IrKind.WrapNullable or IrKind.UnwrapNullable
                    || kind == IrKind.CallInitializer && result == IrResult.Value))
                {
                    Own(region, node);
                }
                return node;
            }
            void Own(IrNode region, IrNode holder)
            {
                if (!owners.TryGetValue(region, out List<IrNode>? list))
                {
                    list = new();
                    owners.Add(region, list);
                }
                list.Add(holder);
            }
            IrNode Transfer(IrNode region, SyntaxNode origin, IrNode value)
            {
                if (!Owned(value.Type))
                {
                    return value;
                }
                // A scalar Load and its nullable conversions observe without
                // owning the object. Owning boundaries acquire a holder first;
                // moving the borrowed observation would steal someone else's count.
                if (value.IsBorrowedHandleValue())
                {
                    IrNode retained = Add(region, IrKind.Ref, origin, value.Type, IrResult.Value);
                    Input(retained, value);
                    value = retained;
                }
                // Move produces a transient transfer value and empties the source
                // holder. Its designated consumer establishes the next owner; cleanup may intervene.
                IrNode move = Add(region, IrKind.Move, origin, value.Type, IrResult.Value);
                Input(move, value);
                return move;
            }
            void ReleaseBorrowedArguments(IrNode region, SyntaxNode origin,
                SemanticSymbol target, IrNode[] arguments)
            {
                // Borrowing changes the callee contract, not the lifetime of the
                // caller's evaluated argument holders. Keep those through the
                // call, then empty them at the former consuming-call boundary.
                // A direct borrowed value is not registered here and is untouched.
                if (!owners.TryGetValue(region, out List<IrNode>? holders))
                {
                    return;
                }
                for (int index = arguments.Length - 1; index >= 0; index -= 1)
                {
                    SemanticSymbol parameter = target.FunctionScope!.ParameterAt(index);
                    IrNode argument = arguments[index];
                    if (target.BorrowsParticipantParameter(parameter)
                        && holders.Contains(argument))
                    {
                        IrNode release = Add(region, DropKind(argument.Type!), origin);
                        Input(release, argument);
                    }
                }
            }
            void Cleanup(IrNode output, IrNode scope, SyntaxNode origin)
            {
                if (!owners.TryGetValue(scope, out List<IrNode>? list))
                {
                    return;
                }
                // Preserve reverse declaration order, as in the existing emitter.
                // Empty holders are valid: transfers have already cleared them.
                for (int i = list.Count - 1; i >= 0; i -= 1)
                {
                    IrNode release = Add(output,
                        DropKind(list[i].Type!), origin);
                    Input(release, list[i]);
                }
            }
            void Input(IrNode consumer, IrNode value)
            {
                _ = new IrOperand(consumer, value);
            }
            IrNode Region(IrNode owner, IrRole role, SyntaxNode origin)
            {
                IrNode region = new(function, IrKind.Region, role, origin: origin);
                owner.AddChild(region);
                return region;
            }
            IrNode Unit(IrNode region, SyntaxNode origin)
            {
                return Add(region, IrKind.Constant, origin, Analysis!.ScalarType(ScalarKind.Unit), IrResult.Value);
            }
            IrNode Widen(IrNode region, SyntaxNode origin, IrNode value, SemanticType type)
            {
                if (value.Type == type)
                {
                    return value;
                }
                if (!SemanticTypes.CanWiden(value.Type!, type))
                {
                    throw new InvalidOperationException("Checked IR widening is invalid.");
                }
                if (type.Kind == SemanticTypeKind.Nullable)
                {
                    bool borrowed = value.IsBorrowedHandleValue();
                    IrNode transferred = borrowed ? value : Transfer(region, origin, value);
                    IrNode wrapped = Add(region, IrKind.WrapNullable, origin, type,
                        IrResult.Value, registerOwner: !borrowed);
                    Input(wrapped, transferred);
                    return wrapped;
                }
                IrNode converted = Add(region, IrKind.Convert, origin, type,
                    IrResult.Value, operation: "widen");
                Input(converted, value);
                return converted;
            }
            void End(IrNode region, IrKind kind, SyntaxNode origin, IrNode value, SemanticType type)
            {
                bool addressReturn = kind == IrKind.Return
                    && function.Kind == IrFunctionKind.FieldInitializer && declaration.IsReference;
                IrNode converted = value;
                if (addressReturn)
                {
                    // The borrow survives only if semantic checking proved it valid
                    // after cleanup. Its referent never becomes an owned return value.
                    if (value.Result != IrResult.Address || value.Type != type)
                    {
                        throw new InvalidOperationException("Reference initializer requires an exact address.");
                    }
                }
                else
                {
                    converted = Widen(region, origin, value, type);
                    converted = Transfer(region, origin, converted);
                }
                // A loop condition's holders survive its yielded boolean and
                // remain live through the selected body. Other yields exit scope.
                if (region.Role != IrRole.Condition || kind == IrKind.Return)
                {
                    Cleanup(region, region, origin);
                }
                if (kind == IrKind.Return)
                {
                    for (IrNode? parent = region.Parent; parent is not null; parent = parent.Parent)
                    {
                        if (parent.Kind == IrKind.Region)
                        {
                            Cleanup(region, parent, origin);
                        }
                        else if (parent.Kind == IrKind.While)
                        {
                            Cleanup(region, parent.ChildAt(0), origin);
                        }
                    }
                }
                IrNode end = Add(region, kind, origin);
                Input(end, converted);
            }
            string Text(SyntaxNode node)
            {
                return node.Source!.SourceText(node.TextOffset, node.TextLength);
            }
            IrNode Storage(SyntaxNode node)
            {
                while (node.Kind == SyntaxKind.Group)
                {
                    node = node.ChildAt(0);
                }
                if (node.Kind != SyntaxKind.Name || node.Fact?.Symbol is not SemanticSymbol symbol
                    || !storage.TryGetValue(symbol, out IrNode? local))
                {
                    Unsupported(node);
                    throw new InvalidOperationException("Unreachable.");
                }
                return local;
            }
            // Selecting writable storage is not an ordinary array read: copying
            // here would redirect the assignment into a disposable temporary.
            // Each selection executes before the next index or assignment RHS.
            void SelectStorage(SyntaxNode node, IrNode region, IrNode? loop,
                Action<IrNode> done, bool allowComputed = false)
            {
                work.Push(() =>
                {
                    if (node.Kind == SyntaxKind.Group)
                    {
                        SelectStorage(node.ChildAt(0), region, loop, done, allowComputed);
                    }
                    else if (node.Kind == SyntaxKind.Member)
                    {
                        if (node.ChildAt(0).Fact!.Type.Kind == SemanticTypeKind.Tuple
                            || node.ChildAt(0).Fact!.Type.Declaration?.Kind == SymbolKind.Struct)
                        {
                            SelectStorage(node.ChildAt(0), region, loop, receiver =>
                            {
                                bool tuple = node.ChildAt(0).Fact!.Type.Kind == SemanticTypeKind.Tuple;
                                IrLiteral? selector = tuple
                                    ? new IrLiteral(new System.Numerics.BigInteger(int.Parse(Text(node),
                                        System.Globalization.CultureInfo.InvariantCulture)))
                                    : null;
                                IrNode selected = Add(region,
                                    tuple ? IrKind.SelectTuple : IrKind.SelectStruct, node,
                                    node.Fact!.Type, IrResult.Address,
                                    literal: selector, symbol: tuple ? null : node.Fact.Symbol);
                                Input(selected, receiver);
                                done(selected);
                            }, allowComputed);
                            return;
                        }
                        // Snapshot the handle, not a pointer into movable class storage.
                        Receiver(node.ChildAt(0), region, loop, receiver =>
                        {
                            IrNode place = Add(region, IrKind.SelectField, node,
                                node.Fact!.Type, IrResult.Place, symbol: node.Fact.Symbol);
                            Input(place, receiver);
                            done(place);
                        });
                    }
                    else if (node.Kind == SyntaxKind.Index)
                    {
                        SelectStorage(node.ChildAt(0), region, loop, container =>
                        {
                            Expression(node.ChildAt(1), region, loop, index =>
                            {
                                done(SelectIndex(node, region, container, index));
                            });
                        }, allowComputed);
                    }
                    else if (allowComputed && node.Kind != SyntaxKind.Name)
                    {
                        // Metadata may observe computed array values. They retain
                        // their ordinary ownership; writable selection has no fallback.
                        Expression(node, region, loop, done);
                    }
                    else
                    {
                        done(Storage(node));
                    }
                });
            }
            void Receiver(SyntaxNode node, IrNode region, IrNode? loop, Action<IrNode> done)
            {
                while (node.Kind == SyntaxKind.Group
                    || node.Fact?.Symbol?.Kind == SymbolKind.MemberNamespace)
                {
                    node = node.ChildAt(0);
                }
                if (node.Kind == SyntaxKind.Name)
                {
                    // Field selection snapshots identity without acquiring ownership.
                    // This also permits field access through final-hook borrowed self.
                    IrNode handle = Add(region, IrKind.Load, node, node.Fact!.Type, IrResult.Value);
                    Input(handle, Storage(node));
                    done(handle);
                }
                else
                {
                    Expression(node, region, loop, done);
                }
            }
            IrNode SelectIndex(SyntaxNode node, IrNode region, IrNode container, IrNode index)
            {
                // This operation checks initialized length and captures the actual
                // element address now. It is not a path to resolve again later.
                IrNode selected = Add(region, IrKind.SelectIndex, node,
                    node.Fact!.Type, IrResult.Address);
                Input(selected, container);
                Input(selected, index);
                return selected;
            }
            void Expression(SyntaxNode node, IrNode region, IrNode? loop, Action<IrNode> done)
            {
                work.Push(() =>
                {
                    SemanticFact fact = node.Fact!;
                    SupportedType(fact.Type, node);
                    if (fact.IntegerConstant is not null)
                    {
                        done(Add(region, IrKind.Constant, node, fact.Type, IrResult.Value,
                            literal: new IrLiteral(fact.IntegerConstant.Value)));
                        return;
                    }
                    switch (node.Kind)
                    {
                        case SyntaxKind.Unit:
                            done(Unit(region, node));
                            return;
                        case SyntaxKind.Postfix when Text(node) == "!":
                            Expression(node.ChildAt(0), region, loop, nullable =>
                            {
                                bool borrowed = nullable.IsBorrowedHandleValue();
                                IrNode transferred = borrowed ? nullable : Transfer(region, node, nullable);
                                IrNode unwrapped = Add(region, IrKind.UnwrapNullable,
                                    node, fact.Type, IrResult.Value, registerOwner: !borrowed);
                                Input(unwrapped, transferred);
                                done(unwrapped);
                            });
                            return;
                        case SyntaxKind.Literal:
                            if (Text(node) == "null")
                            {
                                done(Add(region, IrKind.NullHandle, node, fact.Type, IrResult.Value));
                                return;
                            }
                            if (fact.Type.ArrayDepth > 0)
                            {
                                Unsupported(node);
                            }
                            done(Add(region, IrKind.Constant, node, fact.Type, IrResult.Value,
                                literal: new IrLiteral(Text(node) == "true")));
                            return;
                        case SyntaxKind.String:
                            // The source already owns decoded bytes. Keep an immutable
                            // payload in IR, but allocate a fresh owning value whenever
                            // this operation executes (including on loop backedges).
                            byte[] bytes = node.Source!.LiteralBytes(
                                node.TextOffset, node.TextLength).ToArray();
                            done(Add(region, IrKind.NewArray, node, fact.Type,
                                IrResult.Value, literal: new IrLiteral(bytes)));
                            return;
                        case SyntaxKind.Name:
                            // Participant values and aliases to their borrowed
                            // storage observe without acquiring ownership. Follow
                            // the actual address chain, not the local's spelling.
                            // Transfer retains at a later owning boundary.
                            IrNode namedStorage = Storage(node);
                            IrKind read = declaration.BorrowsParticipantParameter(fact.Symbol)
                                || namedStorage.IsManagedHandleStorage()
                                ? IrKind.Load : ReadKind(fact.Type);
                            IrNode load = Add(region, read,
                                node, fact.Type, IrResult.Value);
                            Input(load, namedStorage);
                            done(load);
                            return;
                        case SyntaxKind.Member:
                            if (fact.Symbol?.Kind == SymbolKind.MemberNamespace)
                            {
                                // Namespace placement has no runtime representation.
                                Expression(node.ChildAt(0), region, loop, done);
                                return;
                            }
                            if (node.ChildAt(0).Fact!.Type.Kind == SemanticTypeKind.Tuple
                                || node.ChildAt(0).Fact!.Type.Declaration?.Kind == SymbolKind.Struct)
                            {
                                // Read from one evaluated receiver snapshot. Writable
                                // component selection has a separate storage path.
                                Expression(node.ChildAt(0), region, loop, receiver =>
                                {
                                    bool tuple = node.ChildAt(0).Fact!.Type.Kind == SemanticTypeKind.Tuple;
                                    IrLiteral? selector = tuple
                                        ? new IrLiteral(new System.Numerics.BigInteger(int.Parse(Text(node),
                                            System.Globalization.CultureInfo.InvariantCulture)))
                                        : null;
                                    IrNode selected = Add(region,
                                        tuple ? IrKind.SelectTuple : IrKind.SelectStruct, node,
                                        fact.Type, IrResult.Address,
                                        literal: selector, symbol: tuple ? null : fact.Symbol);
                                    Input(selected, receiver);
                                    IrNode value = Add(region, ReadKind(fact.Type), node, fact.Type, IrResult.Value);
                                    Input(value, selected);
                                    done(value);
                                });
                                return;
                            }
                            SelectStorage(node, region, loop, selected =>
                            {
                                // Only this relationship's participant fields are
                                // borrowed here. Application reads and unrelated
                                // counted types still acquire ordinary owners.
                                SemanticSymbol? field = selected.Symbol;
                                bool borrowed = field?.IsManagedHandle() == true
                                    && field.ParticipantView is ParticipantBinding participant
                                    && participant.Instantiation == declaration.ContributingParticipant?.Instantiation
                                    && participant.AcceptsViewType(fact.Type);
                                IrKind read = borrowed ? IrKind.Load : ReadKind(fact.Type);
                                IrNode value = Add(region, read,
                                    node, fact.Type, IrResult.Value);
                                Input(value, selected);
                                done(value);
                            });
                            return;
                        case SyntaxKind.Index:
                            Expression(node.ChildAt(0), region, loop, container =>
                            {
                                Expression(node.ChildAt(1), region, loop, index =>
                                {
                                    IrNode selected = SelectIndex(node, region, container, index);
                                    IrNode value = Add(region,
                                        ReadKind(fact.Type),
                                        node, fact.Type, IrResult.Value);
                                    Input(value, selected);
                                    done(value);
                                });
                            });
                            return;
                        case SyntaxKind.Group:
                            Expression(node.ChildAt(0), region, loop, done);
                            return;
                        case SyntaxKind.MatchType:
                            SelectedBlock(node, region, loop, done);
                            return;
                        case SyntaxKind.If:
                            Expression(node.ChildAt(0), region, loop, condition =>
                            {
                                IrNode choice = Add(region, IrKind.If, node, fact.Type, IrResult.Value);
                                Input(choice, condition);
                                IrNode yes = Region(choice, IrRole.Then, node.ChildAt(1));
                                IrNode no = Region(choice, IrRole.Else, node);
                                work.Push(() =>
                                {
                                    if (Owned(fact.Type))
                                    {
                                        Own(region, choice);
                                    }
                                    done(choice);
                                });
                                if (node.ChildCount() == 3)
                                {
                                    SyntaxNode alternative = node.ChildAt(2);
                                    if (alternative.Kind == SyntaxKind.If)
                                    {
                                        Expression(alternative, no, loop,
                                            value => End(no, IrKind.Yield, alternative, value, fact.Type));
                                    }
                                    else
                                    {
                                        Block(alternative, no, loop, IrKind.Yield, fact.Type);
                                    }
                                }
                                else
                                {
                                    work.Push(() => End(no, IrKind.Yield, node, Unit(no, node), fact.Type));
                                }
                                Block(node.ChildAt(1), yes, loop, IrKind.Yield, fact.Type);
                            });
                            return;
                        case SyntaxKind.Binary when Text(node) is "&&" or "||":
                            Expression(node.ChildAt(0), region, loop, left =>
                            {
                                IrNode choice = Add(region, IrKind.If, node, fact.Type, IrResult.Value);
                                Input(choice, left);
                                IrNode yes = Region(choice, IrRole.Then, node);
                                IrNode no = Region(choice, IrRole.Else, node);
                                bool and = Text(node) == "&&";
                                IrNode evaluated = and ? yes : no;
                                IrNode skipped = and ? no : yes;
                                // The skipped path yields the saved left value;
                                // only the other path evaluates the right operand.
                                End(skipped, IrKind.Yield, node, left, fact.Type);
                                work.Push(() => done(choice));
                                Expression(node.ChildAt(1), evaluated, loop,
                                    right => End(evaluated, IrKind.Yield, node, right, fact.Type));
                            });
                            return;
                        case SyntaxKind.Call:
                            if (fact.ClassOperation == ClassOperation.Destroy)
                            {
                                Expression(node.ChildAt(0).ChildAt(0), region, loop, receiver =>
                                {
                                    // Execute the class's explicit generated destruction body.
                                    IrNode destroy = Add(region, IrKind.DestroyObject, node,
                                        fact.Type, IrResult.Value, symbol: fact.Symbol);
                                    Input(destroy, receiver);
                                    done(destroy);
                                });
                                return;
                            }
                            if (fact.Symbol?.Scope?.Class is not null)
                            {
                                ClassCall(node, region, loop, done);
                                return;
                            }
                            if (fact.ArrayOperation != ArrayOperation.None)
                            {
                                // Capture the descriptor before argument effects.
                                // This is mutation of existing storage, not a value copy.
                                SyntaxNode receiver = node.ChildAt(0).ChildAt(0);
                                SelectStorage(receiver, region, loop, target =>
                                {
                                    Expression(node.ChildAt(1), region, loop, argument =>
                                    {
                                        IrKind kind = fact.ArrayOperation switch
                                        {
                                            ArrayOperation.Reserve => IrKind.ArrayReserve,
                                            ArrayOperation.Resize => IrKind.ArrayResize,
                                            ArrayOperation.Truncate => IrKind.ArrayTruncate,
                                            ArrayOperation.Push => IrKind.ArrayPush,
                                            _ => throw new InvalidOperationException("Unknown checked array operation."),
                                        };
                                        if (fact.ArrayOperation == ArrayOperation.Push)
                                        {
                                            SemanticType element = SemanticTypes.ElementOf(receiver.Fact!.Type);
                                            argument = Widen(region, node, argument, element);
                                            argument = Transfer(region, node, argument);
                                        }
                                        IrNode mutation = Add(region, kind, node, fact.Type, IrResult.Value);
                                        Input(mutation, target);
                                        Input(mutation, argument);
                                        done(mutation);
                                    });
                                });
                                return;
                            }
                            if (fact.Symbol?.Builtin == BuiltinKind.Length)
                            {
                                Expression(node.ChildAt(1), region, loop, value =>
                                {
                                    IrNode length = Add(region, IrKind.ArrayLength, node,
                                        fact.Type, IrResult.Value);
                                    Input(length, value);
                                    done(length);
                                });
                                return;
                            }
                            if (fact.Symbol?.Builtin == BuiltinKind.Capacity)
                            {
                                SelectStorage(node.ChildAt(1), region, loop, descriptor =>
                                {
                                    IrNode capacity = Add(region, IrKind.ArrayCapacity, node,
                                        fact.Type, IrResult.Value);
                                    Input(capacity, descriptor);
                                    done(capacity);
                                }, allowComputed: true);
                                return;
                            }
                            if (fact.Symbol is null || fact.Symbol.Kind != SymbolKind.Function
                                || (node.ChildAt(0).Kind != SyntaxKind.Name
                                    && !(node.ChildAt(0).Kind == SyntaxKind.Generic
                                        && fact.Symbol.Specialization is not null)
                                    && fact.Symbol.Scope?.MemberOwner?.Kind != SymbolKind.Struct))
                            {
                                Unsupported(node);
                            }
                            break;
                        case SyntaxKind.Unary when Text(node) == "ref":
                            SelectStorage(node.ChildAt(0), region, loop, selected =>
                            {
                                if (selected.Result == IrResult.Place)
                                {
                                    IrNode address = Add(region, IrKind.AddressOf, node,
                                        selected.Type, IrResult.Address);
                                    Input(address, selected);
                                    selected = address;
                                }
                                IrNode borrow = Add(region, IrKind.Borrow, node,
                                    fact.Type, IrResult.Address);
                                Input(borrow, selected);
                                done(borrow);
                            });
                            return;
                        case SyntaxKind.Record:
                        case SyntaxKind.Tuple:
                        case SyntaxKind.Array:
                        case SyntaxKind.Unary:
                        case SyntaxKind.Binary:
                        case SyntaxKind.Cast:
                            break;
                        default:
                            Unsupported(node);
                            break;
                    }
                    int first = node.Kind is SyntaxKind.Call or SyntaxKind.Record ? 1 : 0;
                    bool structReceiver = node.Kind == SyntaxKind.Call
                        && fact.Symbol?.Scope?.MemberOwner?.Kind == SymbolKind.Struct
                        && fact.Symbol.FunctionScope!.ParameterCount() > 0
                        && fact.Symbol.FunctionScope.ParameterAt(0).Name == "self";
                    int count = node.Kind == SyntaxKind.Cast ? 1 : node.ChildCount() - first;
                    if (structReceiver)
                    {
                        count += 1;
                    }
                    IrNode[] arguments = new IrNode[count];
                    // Push continuations in reverse so children execute left to
                    // right. Each result is a snapshot, never a deferred C read.
                    work.Push(() =>
                    {
                        IrKind kind = node.Kind switch
                        {
                            SyntaxKind.Call => IrKind.Call,
                            SyntaxKind.Array => IrKind.NewArray,
                            SyntaxKind.Tuple => IrKind.NewTuple,
                            SyntaxKind.Record => IrKind.NewStruct,
                            SyntaxKind.Cast => IrKind.Convert,
                            SyntaxKind.Unary => IrKind.Unary,
                            _ => IrKind.Binary,
                        };
                        SemanticSymbol? target = kind == IrKind.Call ? fact.Symbol : null;
                        if (target is not null)
                        {
                            for (int a = 0; a < arguments.Length; a += 1)
                            {
                                SemanticSymbol parameter = target.FunctionScope!.ParameterAt(a);
                                if (parameter.IsReference)
                                {
                                    // Checked references forward storage, never an owned value.
                                    continue;
                                }
                                arguments[a] = Widen(region, node, arguments[a], parameter.Type!);
                                // Builtins observe argument values; their temporary owners
                                // remain in this region for explicit cleanup after the call.
                                if (target.Builtin == BuiltinKind.None
                                    && !target.BorrowsParticipantParameter(parameter))
                                {
                                    arguments[a] = Transfer(region, node, arguments[a]);
                                }
                            }
                        }
                        if (kind is IrKind.NewArray or IrKind.NewTuple)
                        {
                            for (int a = 0; a < arguments.Length; a += 1)
                            {
                                SemanticType element = kind == IrKind.NewTuple
                                    ? fact.Type.ComponentAt(a) : SemanticTypes.ElementOf(fact.Type);
                                arguments[a] = Widen(region, node, arguments[a], element);
                                arguments[a] = Transfer(region, node, arguments[a]);
                            }
                        }
                        if (kind == IrKind.NewStruct)
                        {
                            // Evaluation followed written order. Only now arrange saved
                            // operands by actual field identity. Borrowed fields retain
                            // addresses; only ordinary fields transfer owned values.
                            Dictionary<SemanticSymbol, IrNode> supplied = new();
                            for (int a = 0; a < arguments.Length; a += 1)
                            {
                                supplied.Add(node.ChildAt(a + 1).Fact!.Symbol!, arguments[a]);
                            }
                            // All explicit expressions have completed. Omitted defaults
                            // now execute in declaration order; keep their owners live
                            // until every invocation has finished before transferring.
                            foreach (SemanticSymbol field in fact.Type.Declaration!.MemberScope!.Fields())
                            {
                                if (!supplied.ContainsKey(field))
                                {
                                    IrNode value = Add(region, IrKind.CallInitializer, node,
                                        field.Type, field.IsReference ? IrResult.Address : IrResult.Value,
                                        symbol: field);
                                    supplied.Add(field, value);
                                }
                            }
                            arguments = new IrNode[supplied.Count];
                            int position = 0;
                            foreach (SemanticSymbol field in fact.Type.Declaration!.MemberScope!.Fields())
                            {
                                IrNode value = supplied[field];
                                if (field.IsReference)
                                {
                                    // Copy the alias, not its referent. Widening or moving
                                    // here would turn fixed borrowed storage into a value.
                                    if (value.Result != IrResult.Address || value.Type != field.Type)
                                    {
                                        throw new InvalidOperationException(
                                            "Checked struct reference field requires an exact address.");
                                    }
                                    arguments[position] = value;
                                }
                                else
                                {
                                    IrNode widened = Widen(region, node, value, field.Type!);
                                    arguments[position] = Transfer(region, node, widened);
                                }
                                position += 1;
                            }
                        }
                        IrNode operation = Add(region, kind, node, fact.Type, IrResult.Value,
                            operation: kind is IrKind.Call or IrKind.NewArray or IrKind.NewTuple or IrKind.NewStruct
                                ? null : Text(node), symbol: target);
                        foreach (IrNode argument in arguments)
                        {
                            Input(operation, argument);
                        }
                        if (target is not null && target.Builtin == BuiltinKind.None)
                        {
                            ReleaseBorrowedArguments(region, node, target, arguments);
                        }
                        done(operation);
                    });
                    for (int i = count - 1; i >= 0; i -= 1)
                    {
                        int slot = i;
                        SyntaxNode child = structReceiver
                            ? (i == 0 ? node.ChildAt(0).ChildAt(0) : node.ChildAt(i))
                            : node.ChildAt(first + i);
                        if (node.Kind == SyntaxKind.Record)
                        {
                            child = child.ChildAt(0);
                        }
                        Expression(child, region, loop, value => arguments[slot] = value);
                    }
                });
            }
            void ClassCall(SyntaxNode node, IrNode region, IrNode? loop, Action<IrNode> done)
            {
                SemanticSymbol target = node.Fact!.Symbol!;
                SemanticSymbol nominal = target.Scope!.Class!;
                // Semantic checking already resolved constructor versus instance method.
                bool constructing = target.Name == nominal.Name;
                int count = target.FunctionScope!.ParameterCount();
                IrNode[] arguments = new IrNode[count];
                work.Push(() =>
                {
                    for (int a = 1; a < count; a += 1)
                    {
                        SemanticSymbol parameter = target.FunctionScope.ParameterAt(a);
                        if (!parameter.IsReference)
                        {
                            arguments[a] = Widen(region, node, arguments[a], parameter.Type!);
                            if (!target.BorrowsParticipantParameter(parameter))
                            {
                                arguments[a] = Transfer(region, node, arguments[a]);
                            }
                        }
                    }
                    if (constructing)
                    {
                        // Allocation occurs after argument effects, before constructor body.
                        arguments[0] = Add(region, IrKind.NewObject, node,
                            node.Fact.Type, IrResult.Value, symbol: nominal);
                    }
                    if (!constructing
                        && !target.BorrowsParticipantParameter(target.FunctionScope.ParameterAt(0)))
                    {
                        arguments[0] = Transfer(region, node, arguments[0]);
                    }
                    IrNode call = Add(region, IrKind.Call, node, target.Type,
                        IrResult.Value, symbol: target);
                    foreach (IrNode argument in arguments)
                    {
                        Input(call, argument);
                    }
                    if (!constructing)
                    {
                        ReleaseBorrowedArguments(region, node, target, arguments);
                    }
                    done(constructing ? arguments[0] : call);
                });
                for (int a = count - 1; a >= 1; a -= 1)
                {
                    int slot = a;
                    Expression(node.ChildAt(a), region, loop, value => arguments[slot] = value);
                }
                if (!constructing)
                {
                    Expression(node.ChildAt(0).ChildAt(0), region, loop,
                        receiver => arguments[0] = receiver);
                }
            }
            void SelectedBlock(SyntaxNode node, IrNode region, IrNode? loop, Action<IrNode> done)
            {
                // Compile-time selection has no runtime branch or subject evaluation.
                // Flatten only its selected block, retaining its lexical cleanup edge.
                SemanticFact fact = node.Fact!;
                SyntaxNode block = node.ChildAt(fact.SelectedArmIndex).ChildAt(1);
                if (!owners.TryGetValue(region, out List<IrNode>? holders))
                {
                    holders = new();
                    owners.Add(region, holders);
                }
                int firstLocal = holders.Count;
                IrNode? result = null;
                work.Push(() =>
                {
                    if (fact.CanContinue)
                    {
                        result ??= Unit(region, node);
                        result = Widen(region, node, result, fact.Type);
                        // Preserve the outgoing value itself before dropping local
                        // copies. No artificial branch, shallow copy, or second
                        // ownership policy is introduced for a compile-time arm.
                        for (int i = holders.Count - 1; i >= firstLocal; i -= 1)
                        {
                            if (holders[i] != result)
                            {
                                IrNode release = Add(region, DropKind(holders[i].Type!), node);
                                Input(release, holders[i]);
                            }
                        }
                    }
                    holders.RemoveRange(firstLocal, holders.Count - firstLocal);
                    if (fact.CanContinue)
                    {
                        if (Owned(result!.Type))
                        {
                            Own(region, result);
                        }
                        done(result);
                    }
                });
                bool valueTail = fact.Type != Analysis!.ScalarType(ScalarKind.Unit);
                for (int i = block.ChildCount() - 1; i >= 0; i -= 1)
                {
                    SyntaxNode statement = block.ChildAt(i);
                    if (valueTail && i == block.ChildCount() - 1
                        && statement.Kind == SyntaxKind.ExpressionStatement)
                    {
                        Expression(statement.ChildAt(0), region, loop, value => result = value);
                    }
                    else
                    {
                        work.Push(() => Statement(statement, region, loop, false,
                            Analysis!.ScalarType(ScalarKind.Unit)));
                    }
                }
            }
            void Membership(IrNode region, SyntaxNode origin, RelationshipOperation operation)
            {
                if (declaration.RelationshipOperationKind() != operation
                    || declaration.ContributingParticipant!.Instantiation!.Policy != RelationshipPolicy.Refs)
                {
                    return;
                }
                SemanticSymbol child = parameters.ParameterAt(1);
                if (!Counted(child.Type))
                {
                    return;
                }
                // Membership owns one count independently of ordinary caller holders.
                // The participant parameter borrows; accounting must not empty it.
                IrKind kind = operation == RelationshipOperation.Add
                    ? IrKind.MembershipRetain : IrKind.MembershipRelease;
                IrNode accounting = Add(region, kind, origin);
                Input(accounting, storage[child]);
            }
            void Block(SyntaxNode syntax, IrNode region, IrNode? loop, IrKind ending, SemanticType type)
            {
                bool tail = ending == IrKind.Yield && syntax.ChildCount() != 0
                    && syntax.ChildAt(syntax.ChildCount() - 1).Kind == SyntaxKind.ExpressionStatement;
                if (!tail && syntax.Fact!.CanContinue)
                {
                    work.Push(() => End(region, ending, syntax, Unit(region, syntax), type));
                    if (syntax == block)
                    {
                        // LIFO scheduling runs this after the structural statements
                        // but before End releases locals and parameter holders.
                        work.Push(() => Membership(region, syntax, RelationshipOperation.Remove));
                    }
                }
                for (int i = syntax.ChildCount() - 1; i >= 0; i -= 1)
                {
                    SyntaxNode statement = syntax.ChildAt(i);
                    bool yields = tail && i == syntax.ChildCount() - 1;
                    work.Push(() => Statement(statement, region, loop, yields, type));
                }
            }
            void Statement(SyntaxNode node, IrNode region, IrNode? loop, bool yields, SemanticType type)
            {
                switch (node.Kind)
                {
                    case SyntaxKind.Prepare:
                        // Keep preparation storage in the surrounding region;
                        // no End instruction or early cleanup belongs here.
                        SyntaxNode preparation = node.ChildAt(0);
                        // Publish membership ownership only after validation and
                        // preparation have completed, before the first link write.
                        work.Push(() => Membership(region, node, RelationshipOperation.Add));
                        for (int index = preparation.ChildCount() - 1; index >= 0; index -= 1)
                        {
                            SyntaxNode statement = preparation.ChildAt(index);
                            work.Push(() => Statement(statement, region, loop, false, type));
                        }
                        return;
                    case SyntaxKind.DiscardResult:
                        return;
                    case SyntaxKind.Binding:
                        if (node.ChildAt(0).Kind is SyntaxKind.PatternTuple or SyntaxKind.Wildcard)
                        {
                            SyntaxNode initializer = node.ChildAt(node.ChildCount() - 1);
                            Expression(initializer, region, loop, aggregate =>
                            {
                                Stack<(SyntaxNode Pattern, IrNode Value)> pending = new();
                                pending.Push((node.ChildAt(0), aggregate));
                                while (pending.Count > 0)
                                {
                                    (SyntaxNode part, IrNode value) = pending.Pop();
                                    if (part.Kind == SyntaxKind.Wildcard)
                                    {
                                        continue;
                                    }
                                    if (part.Kind == SyntaxKind.PatternTuple)
                                    {
                                        // Selection has no effects; pushing in reverse keeps
                                        // the component copies and local declarations ordered.
                                        for (int i = part.ChildCount() - 1; i >= 0; i -= 1)
                                        {
                                            IrNode selected = Add(region, IrKind.SelectTuple, part,
                                                value.Type!.ComponentAt(i), IrResult.Address,
                                                literal: new IrLiteral(new System.Numerics.BigInteger(i)));
                                            Input(selected, value);
                                            pending.Push((part.ChildAt(i), selected));
                                        }
                                        continue;
                                    }
                                    SemanticSymbol localSymbol = part.DeclaredSymbol!;
                                    IrNode copy = Add(region, ReadKind(localSymbol.Type!), part,
                                        localSymbol.Type, IrResult.Value);
                                    Input(copy, value);
                                    IrNode local = Add(region, IrKind.Local, part, localSymbol.Type,
                                        IrResult.Address, symbol: localSymbol);
                                    storage.Add(localSymbol, local);
                                    Store(local, copy, part, region);
                                }
                                // Discards are not delayed until scope exit. Named leaves
                                // now own independent copies; dropping the snapshot is safe.
                                if (Owned(aggregate.Type))
                                {
                                    IrNode release = Add(region, DropKind(aggregate.Type!), node);
                                    Input(release, aggregate);
                                }
                            });
                            return;
                        }
                        SemanticSymbol symbol = node.DeclaredSymbol!;
                        SupportedType(symbol.Type!, node);
                        int next = 1;
                        if (next < node.ChildCount() && node.ChildAt(next).Kind == SyntaxKind.Annotation)
                        {
                            next += 1;
                        }
                        if (next == node.ChildCount())
                        {
                            storage.Add(symbol, Add(region, IrKind.Local, node, symbol.Type,
                                IrResult.Address, symbol: symbol));
                        }
                        else
                        {
                            Expression(node.ChildAt(next), region, loop, value =>
                            {
                                IrNode local = Add(region,
                                    symbol.IsReference ? IrKind.Borrow : IrKind.Local,
                                    node, symbol.Type, IrResult.Address, symbol: symbol);
                                storage.Add(symbol, local);
                                if (symbol.IsReference)
                                {
                                    Input(local, value);
                                }
                                else
                                {
                                    Store(local, value, node, region);
                                }
                            });
                        }
                        return;
                    case SyntaxKind.Assignment:
                        SelectStorage(node.ChildAt(0), region, loop, destination =>
                        {
                            string assignment = Text(node);
                            IrNode? old = null;
                            if (assignment != "=")
                            {
                                old = Add(region, IrKind.Load, node.ChildAt(0), destination.Type, IrResult.Value);
                                Input(old, destination);
                            }
                            Expression(node.ChildAt(1), region, loop, value =>
                            {
                                if (old is not null)
                                {
                                    IrNode combined = Add(region, IrKind.Binary, node, destination.Type,
                                        IrResult.Value, operation: assignment[..^1]);
                                    Input(combined, old);
                                    Input(combined, value);
                                    value = combined;
                                }
                                Store(destination, value, node, region);
                            });
                        });
                        return;
                    case SyntaxKind.ExpressionStatement:
                        Expression(node.ChildAt(0), region, loop, value =>
                        {
                            if (yields)
                            {
                                End(region, IrKind.Yield, node, value, type);
                            }
                        });
                        return;
                    case SyntaxKind.Return:
                        if (node.ChildCount() == 0)
                        {
                            End(region, IrKind.Return, node, Unit(region, node), declaration.Type!);
                        }
                        else
                        {
                            Expression(node.ChildAt(0), region, loop,
                                value => End(region, IrKind.Return, node, value, declaration.Type!));
                        }
                        return;
                    case SyntaxKind.While:
                        IrNode repetition = Add(region, IrKind.While, node);
                        IrNode condition = Region(repetition, IrRole.Condition, node.ChildAt(0));
                        IrNode contents = Region(repetition, IrRole.Body, node.ChildAt(1));
                        // Normal backedges release body holders, then condition
                        // holders. A transfer bypasses this sequence and emits
                        // its own cleanup before the transfer operation.
                        work.Push(() =>
                        {
                            Cleanup(contents, contents, node);
                            Cleanup(contents, condition, node);
                        });
                        BlockStatements(node.ChildAt(1), contents, repetition);
                        Expression(node.ChildAt(0), condition, repetition, value =>
                        {
                            // False-exit cleanup belongs to the path that actually
                            // evaluated this condition. A condition break may bypass
                            // later holders entirely, so post-loop cleanup is invalid.
                            if (owners.TryGetValue(condition, out List<IrNode>? conditionOwners)
                                && conditionOwners.Count != 0)
                            {
                                IrNode choice = Add(condition, IrKind.If, node.ChildAt(0),
                                    Analysis!.ScalarType(ScalarKind.Bool), IrResult.Value);
                                Input(choice, value);
                                IrNode whenTrue = Region(choice, IrRole.Then, node.ChildAt(0));
                                IrNode whenFalse = Region(choice, IrRole.Else, node.ChildAt(0));
                                End(whenTrue, IrKind.Yield, node.ChildAt(0), value, Analysis!.ScalarType(ScalarKind.Bool));
                                Cleanup(whenFalse, condition, node);
                                End(whenFalse, IrKind.Yield, node.ChildAt(0), value, Analysis!.ScalarType(ScalarKind.Bool));
                                value = choice;
                            }
                            End(condition, IrKind.Yield, node.ChildAt(0), value, Analysis!.ScalarType(ScalarKind.Bool));
                        });
                        return;
                    case SyntaxKind.Break:
                    case SyntaxKind.Continue:
                        for (IrNode? scope = region; scope != loop; scope = scope!.Parent)
                        {
                            if (scope!.Kind == IrKind.Region)
                            {
                                Cleanup(region, scope, node);
                            }
                        }
                        // The condition is a sibling of the body, not an ancestor.
                        Cleanup(region, loop!.ChildAt(0), node);
                        IrNode transfer = Add(region,
                            node.Kind == SyntaxKind.Break ? IrKind.Break : IrKind.Continue, node);
                        loop!.AddTransfer(transfer);
                        return;
                    default:
                        Unsupported(node);
                        return;
                }
            }
            void BlockStatements(SyntaxNode syntax, IrNode region, IrNode loop)
            {
                for (int i = syntax.ChildCount() - 1; i >= 0; i -= 1)
                {
                    SyntaxNode statement = syntax.ChildAt(i);
                    work.Push(() => Statement(statement, region, loop, false, Analysis!.ScalarType(ScalarKind.Unit)));
                }
            }
            void Store(IrNode destination, IrNode value, SyntaxNode origin, IrNode region)
            {
                IrNode converted = Widen(region, origin, value, destination.Type!);
                // Structural links observe a handle; the membership owns the
                // reference. The value temporary still owns its normal copy.
                bool managed = destination.IsManagedHandleStorage();
                if (!managed)
                {
                    converted = Transfer(region, origin, converted);
                }
                if (!managed && Owned(destination.Type))
                {
                    IrNode release = Add(region,
                        DropKind(destination.Type!), origin);
                    Input(release, destination);
                }
                IrNode store = Add(region, IrKind.Store, origin);
                Input(store, destination);
                Input(store, converted);
            }
        }
    }
}
