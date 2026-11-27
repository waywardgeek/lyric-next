using System;
using System.Numerics;
using Lyric;

// Development-only lifecycle probe. This is not a language conformance grader.
// Compile with the actual compiler sources, replacing only the driver entry point.
internal static class IrModelProbe
{
    private static int Checks;

    private static void Check(bool condition, string reason)
    {
        if (!condition)
        {
            throw new Exception(reason);
        }
        Checks += 1;
    }

    private static void Refuses(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            Checks += 1;
            return;
        }
        throw new Exception(reason);
    }

    private static void CheckExecution()
    {
        // Every fixture is structurally and locally type-correct. Only the execution
        // walk is allowed to supply the expected refusal; repairs use fresh graphs.
        for (int scenario = 0; scenario < 16; scenario += 1)
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope scope = new(analysis, null);
            SemanticSymbol declaration = new(scope, null, "execution", SymbolKind.Function,
                analysis.ScalarType(ScalarKind.Bool), false);
            IrProgram program = new(analysis);
            IrFunction function = new(program, declaration);
            IrNode body = new(function, IrKind.Region, IrRole.Body);
            function.AddBody(body);
            IrNode initial = Boolean(body);
            IrNode result = initial;
            if (scenario == 0)
            {
                // A valid operand edge does not imply an executed definition.
                IrNode later = new(function, IrKind.Constant, result: IrResult.Value,
                    type: analysis.ScalarType(ScalarKind.Bool), literal: new IrLiteral(true));
                result = Negate(body, later);
                body.AddChild(later);
            }
            if (scenario is 1 or 2 or 6)
            {
                IrNode choice = new(function, IrKind.If, result: IrResult.Value,
                    type: analysis.ScalarType(ScalarKind.Bool));
                body.AddChild(choice);
                _ = new IrOperand(choice, initial);
                IrNode yes = new(function, IrKind.Region, IrRole.Then);
                IrNode no = new(function, IrKind.Region, IrRole.Else);
                choice.AddChild(yes);
                choice.AddChild(no);
                IrNode privateValue = Boolean(yes);
                if (scenario != 2)
                {
                    Exit(yes, IrKind.Yield, privateValue);
                }
                Exit(no, IrKind.Yield, initial);
                result = scenario == 1 ? privateValue : choice;
            }
            if (scenario is 3 or 4 or 7 or 9 or 10 or 11 or 14 or 15)
            {
                IrNode loop = new(function, IrKind.While);
                body.AddChild(loop);
                IrNode condition = new(function, IrKind.Region, IrRole.Condition);
                IrNode contents = new(function, IrKind.Region, IrRole.Body);
                loop.AddChild(condition);
                loop.AddChild(contents);
                if (scenario is 9 or 10 or 11)
                {
                    IrNode choice = new(function, IrKind.If, result: IrResult.Value,
                        type: analysis.ScalarType(ScalarKind.Bool));
                    condition.AddChild(choice);
                    _ = new IrOperand(choice, initial);
                    IrNode yes = new(function, IrKind.Region, IrRole.Then);
                    IrNode no = new(function, IrKind.Region, IrRole.Else);
                    choice.AddChild(yes);
                    choice.AddChild(no);
                    IrNode transfer = new(function,
                        scenario == 11 ? IrKind.Continue : IrKind.Break);
                    yes.AddChild(transfer);
                    loop.AddTransfer(transfer);
                    Exit(no, IrKind.Yield, initial);
                }
                IrNode conditionValue = Boolean(condition);
                if (scenario != 3)
                {
                    Exit(condition, IrKind.Yield, conditionValue);
                }
                if (scenario is 14 or 15)
                {
                    SemanticType arrayType = SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.I32));
                    IrNode slot = new(function, IrKind.Local, result: IrResult.Address, type: arrayType);
                    contents.AddChild(slot);
                    IrNode array = new(function, IrKind.NewArray, result: IrResult.Value, type: arrayType);
                    contents.AddChild(array);
                    IrNode moved = new(function, IrKind.Move, result: IrResult.Value, type: arrayType);
                    contents.AddChild(moved);
                    _ = new IrOperand(moved, array);
                    IrNode jump = new(function, scenario == 14 ? IrKind.Break : IrKind.Continue);
                    contents.AddChild(jump);
                    loop.AddTransfer(jump);
                    IrNode skippedStore = new(function, IrKind.Store);
                    contents.AddChild(skippedStore);
                    _ = new IrOperand(skippedStore, slot);
                    _ = new IrOperand(skippedStore, moved);
                }
                IrNode bodyValue = Negate(contents, conditionValue);
                result = scenario == 4 ? bodyValue : conditionValue;
                if (scenario == 10)
                {
                    result = initial;
                }
            }
            IrNode? delayedStore = null;
            if (scenario is 12 or 13)
            {
                SemanticType arrayType = SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.I32));
                IrNode slot = new(function, IrKind.Local, result: IrResult.Address, type: arrayType);
                body.AddChild(slot);
                IrNode array = new(function, IrKind.NewArray, result: IrResult.Value, type: arrayType);
                body.AddChild(array);
                IrNode moved = new(function, IrKind.Move, result: IrResult.Value, type: arrayType);
                body.AddChild(moved);
                _ = new IrOperand(moved, array);
                // Empty-source cleanup between transfer and consumption is legal.
                IrNode cleanup = new(function, IrKind.FreeArray);
                body.AddChild(cleanup);
                _ = new IrOperand(cleanup, array);
                delayedStore = new(function, IrKind.Store);
                _ = new IrOperand(delayedStore, slot);
                _ = new IrOperand(delayedStore, moved);
                if (scenario == 13)
                {
                    body.AddChild(delayedStore);
                }
            }
            if (scenario != 5)
            {
                Exit(body, IrKind.Return, result);
            }
            if (scenario == 12)
            {
                // The consumer exists and is type-correct, but never executes.
                body.AddChild(delayedStore!);
            }
            if (scenario == 8)
            {
                // This forward reference is unreachable, but still type/structure valid.
                IrNode later = new(function, IrKind.Constant, result: IrResult.Value,
                    type: analysis.ScalarType(ScalarKind.Bool), literal: new IrLiteral(false));
                _ = Negate(body, later);
                body.AddChild(later);
            }
            program.ValidateStructure();
            program.ValidateOperations();
            if (scenario < 6 || scenario is 9 or 12 or 14 or 15)
            {
                Refuses(program.ValidateExecution, "invalid execution scenario " + scenario);
                Check(!program.Destroyed && function.Body == body,
                    "execution refusal mutated graph");
            }
            else
            {
                program.ValidateExecution();
                Check(true, "valid execution scenario " + scenario);
            }
            compilation.Destroy();

            IrNode Boolean(IrNode region)
            {
                IrNode value = new(function, IrKind.Constant, result: IrResult.Value,
                    type: analysis.ScalarType(ScalarKind.Bool), literal: new IrLiteral(true));
                region.AddChild(value);
                return value;
            }
            IrNode Negate(IrNode region, IrNode operand)
            {
                IrNode value = new(function, IrKind.Unary, result: IrResult.Value,
                    type: analysis.ScalarType(ScalarKind.Bool), operation: "!");
                region.AddChild(value);
                _ = new IrOperand(value, operand);
                return value;
            }
            void Exit(IrNode region, IrKind kind, IrNode value)
            {
                IrNode exit = new(function, kind);
                region.AddChild(exit);
                _ = new IrOperand(exit, value);
            }
        }
    }

    private static void CheckStructure()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope scope = new(analysis, null);
        SemanticSymbol declaration = new(scope, null, "structure", SymbolKind.Function,
            analysis.ScalarType(ScalarKind.Unit), false);
        IrProgram program = new(analysis);
        IrFunction function = new(program, declaration);
        IrNode body = new(function, IrKind.Region, IrRole.Body);
        function.AddBody(body);
        IrNode unit = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(unit);
        IrNode finish = new(function, IrKind.Return);
        body.AddChild(finish);
        _ = new IrOperand(finish, unit);
        program.ValidateStructure();
        Check(!program.Destroyed, "validation changed valid graph");

        IrNode draft = new(function, IrKind.Constant);
        Refuses(program.ValidateStructure, "detached draft accepted");
        Check(draft.Function == function && draft.Parent is null,
            "refusal mutated detached draft");
        draft.Destroy();
        unit.Destroy();
        Refuses(program.ValidateStructure, "deleted operand definition accepted");
        finish.Destroy();

        IrNode transfer = new(function, IrKind.Break);
        body.AddChild(transfer);
        Refuses(program.ValidateStructure, "non-enclosing loop target accepted");
        transfer.Destroy();
        IrNode choice = new(function, IrKind.If);
        body.AddChild(choice);
        IrNode yes = new(function, IrKind.Region, IrRole.Then);
        choice.AddChild(yes);
        Refuses(program.ValidateStructure, "missing else region accepted");
        IrNode no = new(function, IrKind.Region, IrRole.Else);
        choice.AddChild(no);
        program.ValidateStructure();
        Check(choice.ChildCount() == 2, "repaired graph refused or mutated");
        compilation.Destroy();
    }

    private static void CheckOperations()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope scope = new(analysis, null);
        SemanticSymbol declaration = new(scope, null, "operations", SymbolKind.Function,
            analysis.ScalarType(ScalarKind.Unit), false);
        SemanticSymbol callee = new(scope, null, "callee", SymbolKind.Function,
            analysis.ScalarType(ScalarKind.Unit), false);
        SemanticSymbol nominal = new(scope, null, "Record", SymbolKind.Class, null, false);
        SemanticSymbol otherClass = new(scope, null, "OtherRecord", SymbolKind.Class, null, false);
        SemanticScope members = new(analysis, scope);
        nominal.AddMemberScope(members);
        SemanticSymbol field = new(members, null, "item", SymbolKind.Field,
            analysis.ScalarType(ScalarKind.Unit), true);
        SemanticType instance = analysis.ClassType(nominal);
        SemanticScope parameters = new(callee);
        _ = new SemanticSymbol(parameters, null, "item", SymbolKind.Parameter,
            analysis.ScalarType(ScalarKind.Unit), true, isReference: true);
        IrProgram program = new(analysis);
        IrFunction function = new(program, declaration);
        IrNode body = new(function, IrKind.Region, IrRole.Body);
        function.AddBody(body);
        IrNode unit = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(unit);
        IrNode finish = new(function, IrKind.Return);
        body.AddChild(finish);
        Refuses(program.ValidateOperations, "operand-free return accepted");
        _ = new IrOperand(finish, unit);
        program.ValidateStructure();
        program.ValidateOperations();
        Check(finish.OperandCount() == 1, "valid operation graph changed");
        IrNode local = new(function, IrKind.Local, result: IrResult.Address);
        body.AddChild(local);
        Refuses(program.ValidateOperations, "untyped local accepted");
        local.SetType(analysis.ScalarType(ScalarKind.Unit));
        program.ValidateOperations();
        Check(local.Type == analysis.ScalarType(ScalarKind.Unit), "typed local repair failed");
        IrNode load = new(function, IrKind.Load, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(load);
        _ = new IrOperand(load, unit);
        Refuses(program.ValidateOperations, "load from plain value accepted");
        load.Destroy();
        IrNode objectReceiver = new(function, IrKind.NewObject, result: IrResult.Value,
            type: instance, symbol: nominal);
        body.AddChild(objectReceiver);
        IrNode otherReceiver = new(function, IrKind.NewObject, result: IrResult.Value,
            type: analysis.ClassType(otherClass), symbol: otherClass);
        body.AddChild(otherReceiver);
        IrNode place = new(function, IrKind.SelectField, result: IrResult.Place,
            type: analysis.ScalarType(ScalarKind.Unit), symbol: field);
        body.AddChild(place);
        IrOperand selectedReceiver = new(place, otherReceiver);
        Refuses(program.ValidateOperations, "field accepted a different nominal objectReceiver");
        selectedReceiver.Destroy();
        _ = new IrOperand(place, objectReceiver);
        program.ValidateOperations();
        place.SetType(analysis.ScalarType(ScalarKind.I32));
        Refuses(program.ValidateOperations, "field selection changed declared type");
        place.SetType(analysis.ScalarType(ScalarKind.Unit));
        objectReceiver.SetType(analysis.ClassType(otherClass));
        Refuses(program.ValidateOperations, "allocation changed nominal class");
        objectReceiver.SetType(instance);
        program.ValidateOperations();
        IrNode borrow = new(function, IrKind.Borrow, result: IrResult.Address,
            type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(borrow);
        _ = new IrOperand(borrow, place);
        Refuses(program.ValidateOperations, "borrow of re-resolvable place accepted");
        borrow.Destroy();
        place.Destroy();
        IrNode reclaim = new(function, IrKind.ReclaimObject, symbol: nominal);
        body.AddChild(reclaim);
        IrNode receiverSlot = new(function, IrKind.Local, result: IrResult.Address,
            type: instance);
        body.AddChild(receiverSlot);
        _ = new IrOperand(reclaim, receiverSlot);
        Refuses(program.ValidateOperations, "source function reclaimed class slot");
        reclaim.Destroy();
        receiverSlot.Destroy();
        objectReceiver.Destroy();
        otherReceiver.Destroy();
        program.ValidateOperations();
        Check(!function.Destroyed, "class contract repairs changed function");
        IrNode wrongResult = new(function, IrKind.Constant, type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(wrongResult);
        Refuses(program.ValidateOperations, "result-free constant accepted");
        wrongResult.Destroy();
        IrNode typedLoad = new(function, IrKind.Load, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.I32));
        body.AddChild(typedLoad);
        _ = new IrOperand(typedLoad, local);
        Refuses(program.ValidateOperations, "load changed storage type");
        typedLoad.SetType(analysis.ScalarType(ScalarKind.Unit));
        program.ValidateOperations();
        typedLoad.Destroy();

        IrNode integer = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.I32), literal: new IrLiteral(BigInteger.Zero));
        body.AddChild(integer);
        IrNode store = new(function, IrKind.Store);
        body.AddChild(store);
        _ = new IrOperand(store, local);
        _ = new IrOperand(store, integer);
        Refuses(program.ValidateOperations, "store implicitly converted value");
        store.Destroy();

        IrNode array = new(function, IrKind.NewArray, result: IrResult.Value,
            type: SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.U8)));
        body.AddChild(array);
        _ = new IrOperand(array, integer);
        Refuses(program.ValidateOperations, "incompatible array element accepted");
        array.SetType(SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.I32)));
        program.ValidateOperations();
        IrNode index = new(function, IrKind.SelectIndex, result: IrResult.Address,
            type: analysis.ScalarType(ScalarKind.U8));
        body.AddChild(index);
        _ = new IrOperand(index, array);
        _ = new IrOperand(index, integer);
        Refuses(program.ValidateOperations, "index selection changed element type");
        index.SetType(analysis.ScalarType(ScalarKind.I32));
        program.ValidateOperations();
        Check(index.Type == analysis.ScalarType(ScalarKind.I32), "type repairs failed");
        IrNode call = new(function, IrKind.Call, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit), symbol: callee);
        body.AddChild(call);
        Refuses(program.ValidateOperations, "call missing argument accepted");
        IrOperand argument = new(call, unit);
        Refuses(program.ValidateOperations, "reference call accepted a value");
        argument.Destroy();
        _ = new IrOperand(call, local);
        program.ValidateOperations();
        local.SetType(analysis.ScalarType(ScalarKind.I32));
        Refuses(program.ValidateOperations, "reference argument changed type");
        local.SetType(analysis.ScalarType(ScalarKind.Unit));
        call.SetType(analysis.ScalarType(ScalarKind.I32));
        Refuses(program.ValidateOperations, "call changed result type");
        call.SetType(analysis.ScalarType(ScalarKind.Unit));
        program.ValidateOperations();
        Check(call.OperandCount() == 1, "call repair changed operands");
        // Mutations act on a descriptor, never on an unselected array value.
        IrNode arraySlot = new(function, IrKind.Local, result: IrResult.Address,
            type: SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.I32)));
        body.AddChild(arraySlot);
        IrNode resize = new(function, IrKind.ArrayResize, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(resize);
        IrOperand receiver = new(resize, array);
        IrOperand size = new(resize, integer);
        Refuses(program.ValidateOperations, "mutation accepted array value as storage");
        receiver.Destroy();
        _ = new IrOperand(resize, arraySlot);
        // Restore operand order after replacing the receiver.
        size.Destroy();
        size = new IrOperand(resize, unit);
        Refuses(program.ValidateOperations, "resize accepted noninteger count");
        size.Destroy();
        _ = new IrOperand(resize, integer);
        program.ValidateOperations();
        resize.Destroy();
        IrNode push = new(function, IrKind.ArrayPush, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(push);
        _ = new IrOperand(push, arraySlot);
        IrOperand element = new(push, unit);
        Refuses(program.ValidateOperations, "push accepted wrong element type");
        element.Destroy();
        _ = new IrOperand(push, integer);
        program.ValidateOperations();
        IrNode capacity = new(function, IrKind.ArrayCapacity, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.I32));
        body.AddChild(capacity);
        _ = new IrOperand(capacity, arraySlot);
        Refuses(program.ValidateOperations, "capacity accepted non-u64 result");
        capacity.SetType(analysis.ScalarType(ScalarKind.U64));
        program.ValidateOperations();
        Check(push.OperandCount() == 2 && capacity.Type == analysis.ScalarType(ScalarKind.U64),
            "array operation repair changed graph");
        // Unit storage cannot acquire array or counted-object ownership merely
        // because an operation has the right operand count and result category.
        foreach (IrKind kind in new[] { IrKind.Copy, IrKind.FreeArray, IrKind.Ref,
            IrKind.Unref, IrKind.Move })
        {
            bool produces = kind is IrKind.Copy or IrKind.Ref or IrKind.Move;
            IrNode invalidOwner = new(function, kind,
                result: produces ? IrResult.Value : IrResult.None,
                type: produces ? analysis.ScalarType(ScalarKind.Unit) : null);
            body.AddChild(invalidOwner);
            _ = new IrOperand(invalidOwner, local);
            Refuses(program.ValidateOperations, "ownership operation accepted unit: " + kind);
            invalidOwner.Destroy();
            program.ValidateOperations();
        }
        // Transfer provenance is independent of matching array types. The
        // graph remains editable after each refusal and repair restores validity.
        IrNode transferValue = new(function, IrKind.Move, result: IrResult.Value,
            type: arraySlot.Type);
        body.AddChild(transferValue);
        IrOperand transferSource = new(transferValue, arraySlot);
        Refuses(program.ValidateOperations, "unconsumed transfer accepted");
        IrNode transferStore = new(function, IrKind.Store);
        body.AddChild(transferStore);
        _ = new IrOperand(transferStore, arraySlot);
        _ = new IrOperand(transferStore, transferValue);
        program.ValidateOperations();
        Check(transferValue.UseAt(0).Consumer == transferStore,
            "transfer consumer backlink inaccessible");
        IrNode secondStore = new(function, IrKind.Store);
        body.AddChild(secondStore);
        _ = new IrOperand(secondStore, arraySlot);
        _ = new IrOperand(secondStore, transferValue);
        Refuses(program.ValidateOperations, "duplicate transfer consumption accepted");
        secondStore.Destroy();
        program.ValidateOperations();
        transferStore.Destroy();
        IrNode transferLength = new(function, IrKind.ArrayLength, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.U64));
        body.AddChild(transferLength);
        _ = new IrOperand(transferLength, transferValue);
        Refuses(program.ValidateOperations, "nonconsuming transfer observation accepted");
        transferLength.Destroy();
        IrNode repairedStore = new(function, IrKind.Store);
        body.AddChild(repairedStore);
        _ = new IrOperand(repairedStore, arraySlot);
        _ = new IrOperand(repairedStore, transferValue);
        program.ValidateOperations();
        IrNode borrowedArray = new(function, IrKind.Borrow, result: IrResult.Address,
            type: arraySlot.Type);
        body.AddChild(borrowedArray);
        _ = new IrOperand(borrowedArray, arraySlot);
        transferSource.Destroy();
        IrOperand borrowedTransfer = new(transferValue, borrowedArray);
        Refuses(program.ValidateOperations, "move from borrowed storage accepted");
        borrowedTransfer.Destroy();
        _ = new IrOperand(transferValue, arraySlot);
        program.ValidateOperations();
        borrowedArray.Destroy();
        repairedStore.Destroy();
        transferValue.Destroy();
        program.ValidateOperations();

        // A missing Move is invisible to checks that visit only Move nodes.
        // Copy is a correctly typed owning value, but Store must consume a transfer.
        IrNode untransferredCopy = new(function, IrKind.Copy, result: IrResult.Value,
            type: arraySlot.Type);
        body.AddChild(untransferredCopy);
        _ = new IrOperand(untransferredCopy, arraySlot);
        program.ValidateOperations();
        IrNode untransferredStore = new(function, IrKind.Store);
        body.AddChild(untransferredStore);
        _ = new IrOperand(untransferredStore, arraySlot);
        _ = new IrOperand(untransferredStore, untransferredCopy);
        Refuses(program.ValidateOperations, "store without explicit ownership transfer accepted");
        untransferredStore.Destroy();
        untransferredCopy.Destroy();
        program.ValidateOperations();

        // Each malformed operator is isolated; removal restores a valid graph.
        foreach (var item in new[]
        {
            (IrKind.Unary, "unknown", unit, analysis.ScalarType(ScalarKind.Unit)),
            (IrKind.Unary, "-", unit, analysis.ScalarType(ScalarKind.Unit)),
            (IrKind.Unary, "!", integer, analysis.ScalarType(ScalarKind.Bool)),
            (IrKind.Convert, "unknown", integer, analysis.ScalarType(ScalarKind.I32)),
            (IrKind.Convert, "widen", integer, analysis.ScalarType(ScalarKind.U8)),
            (IrKind.Convert, "as", unit, analysis.ScalarType(ScalarKind.I32)),
        })
        {
            IrNode invalidOperator = new(function, item.Item1,
                result: IrResult.Value, type: item.Item4, operation: item.Item2);
            body.AddChild(invalidOperator);
            _ = new IrOperand(invalidOperator, item.Item3);
            Refuses(program.ValidateOperations, "invalid operator accepted: " + item.Item2);
            invalidOperator.Destroy();
            program.ValidateOperations();
        }
        IrNode wideSigned = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.I64), literal: new IrLiteral(BigInteger.Zero));
        IrNode wideUnsigned = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.U64), literal: new IrLiteral(BigInteger.Zero));
        IrNode boolean = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Bool), literal: new IrLiteral(true));
        body.AddChild(wideSigned);
        body.AddChild(wideUnsigned);
        body.AddChild(boolean);
        // Exercise all legal tags with unequal integer widths where permitted.
        foreach (string tag in new[] { "+", "-", "*", "/", "%", "!+", "!-", "!*", "&", "|", "^",
            "<<", ">>", "==", "!=", "<", "<=", ">", ">=" })
        {
            bool comparison = tag is "==" or "!=" or "<" or "<=" or ">" or ">=";
            bool shift = tag is "<<" or ">>";
            IrNode validBinary = new(function, IrKind.Binary, result: IrResult.Value,
                type: comparison ? analysis.ScalarType(ScalarKind.Bool) : shift ? analysis.ScalarType(ScalarKind.I32) : analysis.ScalarType(ScalarKind.I64),
                operation: tag);
            body.AddChild(validBinary);
            _ = new IrOperand(validBinary, comparison ? wideSigned : integer);
            _ = new IrOperand(validBinary, comparison ? wideUnsigned : wideSigned);
            program.ValidateOperations();
            Check(validBinary.OperandCount() == 2, "binary validation mutated operands: " + tag);
            validBinary.Destroy();
        }
        foreach (string tag in new[] { "==", "!=" })
        {
            IrNode equality = new(function, IrKind.Binary, result: IrResult.Value,
                type: analysis.ScalarType(ScalarKind.Bool), operation: tag);
            body.AddChild(equality);
            _ = new IrOperand(equality, boolean);
            _ = new IrOperand(equality, boolean);
            program.ValidateOperations();
            Check(equality.Type == analysis.ScalarType(ScalarKind.Bool), "boolean equality rejected");
            equality.Destroy();
        }
        foreach (var item in new[]
        {
            ("unknown", integer, integer, analysis.ScalarType(ScalarKind.I32)),
            ("&&", boolean, boolean, analysis.ScalarType(ScalarKind.Bool)),
            ("||", boolean, boolean, analysis.ScalarType(ScalarKind.Bool)),
            ("+", unit, integer, analysis.ScalarType(ScalarKind.I32)),
            ("+", local, integer, analysis.ScalarType(ScalarKind.I32)),
            ("+", wideSigned, wideUnsigned, analysis.ScalarType(ScalarKind.I64)),
            ("+", integer, wideSigned, analysis.ScalarType(ScalarKind.I32)),
            ("<<", integer, wideSigned, analysis.ScalarType(ScalarKind.I64)),
            (">>", integer, boolean, analysis.ScalarType(ScalarKind.I32)),
            ("<", boolean, boolean, analysis.ScalarType(ScalarKind.Bool)),
            ("==", boolean, integer, analysis.ScalarType(ScalarKind.Bool)),
            ("==", integer, integer, analysis.ScalarType(ScalarKind.I32)),
        })
        {
            IrNode invalidBinary = new(function, IrKind.Binary, result: IrResult.Value,
                type: item.Item4, operation: item.Item1);
            body.AddChild(invalidBinary);
            _ = new IrOperand(invalidBinary, item.Item2);
            _ = new IrOperand(invalidBinary, item.Item3);
            Refuses(program.ValidateOperations, "invalid binary accepted: " + item.Item1);
            invalidBinary.Destroy();
            program.ValidateOperations();
        }
        // Isolate each malformed control value, then repair before the next case.
        foreach (var item in new[] { (IrKind.If, integer), (IrKind.Return, integer),
            (IrKind.Return, local), (IrKind.Yield, unit) })
        {
            IrNode invalidControl = new(function, item.Item1,
                result: item.Item1 == IrKind.If ? IrResult.Value : IrResult.None,
                type: item.Item1 == IrKind.If ? analysis.ScalarType(ScalarKind.Unit) : null);
            body.AddChild(invalidControl);
            _ = new IrOperand(invalidControl, item.Item2);
            Refuses(program.ValidateOperations, "invalid control value accepted: " + item.Item1);
            invalidControl.Destroy();
            program.ValidateOperations();
        }
        foreach (bool conditionRegion in new[] { false, true })
        {
            IrNode control = new(function, conditionRegion ? IrKind.While : IrKind.If,
                result: conditionRegion ? IrResult.None : IrResult.Value,
                type: conditionRegion ? null : analysis.ScalarType(ScalarKind.I32));
            body.AddChild(control);
            if (!conditionRegion)
            {
                _ = new IrOperand(control, boolean);
            }
            IrNode region = new(function, IrKind.Region,
                conditionRegion ? IrRole.Condition : IrRole.Then);
            control.AddChild(region);
            IrNode yielded = new(function, IrKind.Yield);
            region.AddChild(yielded);
            IrOperand output = new(yielded, unit);
            Refuses(program.ValidateOperations, "wrong region result accepted");
            output.Destroy();
            _ = new IrOperand(yielded, conditionRegion ? boolean : integer);
            program.ValidateOperations();
            Check(yielded.Parent == region, "valid region result mutated");
            yielded.Destroy();
            region.Destroy();
            control.Destroy();
            program.ValidateOperations();
        }
        compilation.Destroy();
    }

    private static void CheckConstantPayloads()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope scope = new(analysis, null);
        SemanticSymbol declaration = new(scope, null, "constants", SymbolKind.Function,
            analysis.ScalarType(ScalarKind.Unit), false);
        IrProgram program = new(analysis);
        IrFunction function = new(program, declaration);
        IrNode body = new(function, IrKind.Region, IrRole.Body);
        function.AddBody(body);
        IrNode unit = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit));
        body.AddChild(unit);
        IrNode finish = new(function, IrKind.Return);
        body.AddChild(finish);
        _ = new IrOperand(finish, unit);
        program.ValidateStructure();
        program.ValidateOperations();
        program.ValidateExecution();
        // The surrounding graph is valid. Each draft adds exactly one payload defect.
        foreach ((SemanticType type, IrLiteral? literal) in new (SemanticType, IrLiteral?)[]
        {
            (analysis.ScalarType(ScalarKind.Bool), new IrLiteral(new BigInteger(7))),
            (analysis.ScalarType(ScalarKind.Bool), null),
            (analysis.ScalarType(ScalarKind.I32), null),
            (analysis.ScalarType(ScalarKind.I32), new IrLiteral(true)),
            (analysis.ScalarType(ScalarKind.Unit), new IrLiteral(BigInteger.Zero)),
            (analysis.ScalarType(ScalarKind.U8), new IrLiteral(new BigInteger(-1))),
            (analysis.ScalarType(ScalarKind.U8), new IrLiteral(new BigInteger(256))),
            (analysis.ScalarType(ScalarKind.I8), new IrLiteral(new BigInteger(128))),
            (SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.U8)), new IrLiteral(new byte[] { 1 })),
            (analysis.ScalarType(ScalarKind.F32), new IrLiteral(BigInteger.Zero)),
        })
        {
            IrNode invalid = new(function, IrKind.Constant, result: IrResult.Value,
                type: type, literal: literal);
            body.AddChild(invalid);
            program.ValidateStructure();
            Refuses(program.ValidateOperations, "invalid constant payload accepted");
            invalid.Destroy();
            program.ValidateOperations();
        }
        // Boundary values must not be accidentally excluded by the new range check.
        foreach (SemanticType type in new[] { analysis.ScalarType(ScalarKind.I8), analysis.ScalarType(ScalarKind.U8),
            analysis.ScalarType(ScalarKind.I64), analysis.ScalarType(ScalarKind.U64) })
        {
            foreach (BigInteger value in new[] { SemanticTypes.Minimum(type), SemanticTypes.Maximum(type) })
            {
                IrNode valid = new(function, IrKind.Constant, result: IrResult.Value,
                    type: type, literal: new IrLiteral(value));
                body.AddChild(valid);
                program.ValidateOperations();
                Check(!valid.Destroyed, "valid constant boundary refused");
                valid.Destroy();
            }
        }
        compilation.Destroy();
    }

    private static void CheckNullableOperations()
    {
        // Validation does not execute this null unwrap. It verifies the typed
        // ownership protocol; the public runtime grader checks the null failure.
        foreach (bool counted in new[] { false, true })
        {
            for (int defect = 0; defect < 6; defect += 1)
            {
                Compilation compilation = new();
                string prefix = counted ? "value " : "";
                SourceFile source = new(compilation, "nullable-ir.ly",
                    System.Text.Encoding.UTF8.GetBytes(prefix + "class Box { func Box(self) {} }"));
                Check(source.Parse(false) is null && compilation.Check(false) is null,
                    "nullable IR fixture failed checking");
                SemanticSymbol box = source.SyntaxRoot()!.ChildAt(0).DeclaredSymbol!;
                SemanticAnalysis analysis = box.Scope!.Analysis!;
                SemanticType payload = analysis.ClassType(box);
                SemanticType nullable = analysis.NullableType(payload);
                SemanticSymbol declaration = new(box.Scope, null, "probe", SymbolKind.Function,
                    nullable, false);
                IrProgram program = new(analysis);
                IrFunction function = new(program, declaration);
                IrNode body = new(function, IrKind.Region, IrRole.Body);
                function.AddBody(body);
                IrNode empty = Add(IrKind.NullHandle, defect == 1 ? payload : nullable);
                if (defect == 0)
                {
                    // Source admission is not enough: mutated IR must preserve
                    // identity-only equality and reject other handle operators.
                    foreach (string operation in new[] { "==", "!=", "<", "+" })
                    {
                        IrNode comparison = new(function, IrKind.Binary,
                            result: IrResult.Value,
                            type: analysis.ScalarType(ScalarKind.Bool), operation: operation);
                        body.AddChild(comparison);
                        _ = new IrOperand(comparison, empty);
                        _ = new IrOperand(comparison, empty);
                        if (operation is "==" or "!=")
                        {
                            program.ValidateOperations();
                            Check(true, "nullable identity equality accepted");
                        }
                        else
                        {
                            Refuses(program.ValidateOperations, "handle operator accepted: " + operation);
                        }
                        comparison.Destroy();
                    }
                    IrNode boolean = new(function, IrKind.Constant,
                        result: IrResult.Value, type: analysis.ScalarType(ScalarKind.Bool),
                        literal: new IrLiteral(true));
                    body.AddChild(boolean);
                    IrNode mixed = new(function, IrKind.Binary,
                        result: IrResult.Value, type: analysis.ScalarType(ScalarKind.Bool), operation: "==");
                    body.AddChild(mixed);
                    _ = new IrOperand(mixed, empty);
                    _ = new IrOperand(mixed, boolean);
                    Refuses(program.ValidateOperations, "handle/bool equality accepted");
                    mixed.Destroy();
                    boolean.Destroy();
                }
                IrNode unwrapped = Add(IrKind.UnwrapNullable,
                    defect == 2 ? analysis.ScalarType(ScalarKind.I32) : payload,
                    Transfer(empty, defect == 3));
                IrNode wrapped = Add(IrKind.WrapNullable, nullable,
                    Transfer(unwrapped, defect == 4));
                IrNode exit = new(function, IrKind.Return);
                body.AddChild(exit);
                // Transfer must execute before its consumer in the region.
                body.RemoveChild(exit);
                _ = new IrOperand(exit, Transfer(wrapped, defect == 5));
                body.AddChild(exit);
                program.ValidateStructure();
                bool refused = defect is 1 or 2 || counted && defect >= 3;
                if (refused)
                {
                    Refuses(program.ValidateOperations, "nullable IR accepted defect " + defect);
                }
                else
                {
                    program.ValidateOperations();
                    program.ValidateExecution();
                    Check(true, "valid nullable ownership protocol");
                }
                compilation.Destroy();
                Check(empty.Destroyed && wrapped.Destroyed, "nullable IR teardown left nodes");

                IrNode Add(IrKind kind, SemanticType type, IrNode? input = null)
                {
                    IrNode node = new(function, kind, result: IrResult.Value, type: type);
                    body.AddChild(node);
                    if (input is not null)
                    {
                        _ = new IrOperand(node, input);
                    }
                    return node;
                }
                IrNode Transfer(IrNode holder, bool omit)
                {
                    if (!counted || omit)
                    {
                        return holder;
                    }
                    return Add(IrKind.Move, holder.Type!, holder);
                }
            }
        }
    }

    private static void CheckMembershipAccounting()
    {
        // Lower real guards, structural stores and ordinary cleanup. Corrupt
        // cardinality or order without breaking graph links; never emit or run it.
        const string text = """
            interface Link<parent P, child C> {
                class P {
                    first: C?
                    pub add func attach(self, child: C) {
                        prepare { assert(self.first == null); let token = Payload() }
                        self.first = child
                    }
                    pub remove func detach(self, child: C) {
                        prepare { assert(self.first == child); let token = Payload() }
                        self.first = null
                    }
                }
                class C {}
            }
            class Graph { func Graph(self) {} }
            value class Node { func Node(self) {} }
            impl Link<Graph:links, Node:graph> refs {}
            value class Payload { func Payload(self) {} }
            func main() {}
            """;
        for (int defect = 0; defect < 8; defect += 1)
        {
            Compilation compilation = new();
            SourceFile source = new(compilation, "membership.ly",
                System.Text.Encoding.UTF8.GetBytes(text));
            Check(source.Parse(false) is null && compilation.Check(false) is null
                && compilation.LowerIr() is null, "membership fixture failed lowering");
            SyntaxNode root = source.SyntaxRoot()!;
            IrProgram program = root.ChildAt(root.ChildCount() - 1).DeclaredSymbol!.IrBody!.Program!;
            IrKind selected = defect < 2 || defect is 4 or 5
                ? IrKind.MembershipRetain : IrKind.MembershipRelease;
            IrNode? accounting = null;
            for (int f = 0; f < program.FunctionCount(); f += 1)
            {
                IrFunction function = program.FunctionAt(f);
                for (int n = 0; n < function.NodeCount(); n += 1)
                {
                    if (function.NodeAt(n).Kind == selected)
                    {
                        accounting = function.NodeAt(n);
                    }
                }
            }
            Check(accounting is not null, "lowering omitted membership accounting");
            if (defect < 4)
            {
                if (defect % 2 == 0)
                {
                    accounting!.Destroy();
                }
                else
                {
                    IrNode duplicate = new(accounting!.Function!, selected, origin: accounting.Origin);
                    accounting.Parent!.AddChild(duplicate);
                    _ = new IrOperand(duplicate, accounting.OperandAt(0).Definition!);
                }
            }
            else
            {
                IrNode body = accounting!.Parent!;
                IrNode? anchor = null;
                for (int index = 0; index < body.ChildCount(); index += 1)
                {
                    IrNode candidate = body.ChildAt(index);
                    if (defect is 4 or 6 && candidate == accounting.OperandAt(0).Definition
                        || defect == 5 && candidate.Kind == IrKind.Store
                        || defect == 7 && candidate.Kind == IrKind.Unref
                            && candidate.Origin == body.Origin)
                    {
                        anchor = candidate;
                    }
                }
                Check(anchor is not null, "membership phase control lacks its anchor");
                body.RemoveChild(accounting);
                body.InsertAfter(anchor!, accounting);
            }
            program.ValidateStructure();
            Refuses(program.ValidateOperations, "malformed membership count or order accepted");
            compilation.Destroy();
        }
    }

    private static void CheckReceiverSimplification()
    {
        const string text = """
            value class Box {
                func Box(self) {}
                pub func show(self) { print("live") }
                pub func keep(self)->Box { return self }
            }
            func main() { let box=Box(); box.show(); let other=box.keep() }
            """;
        Compilation compilation = new();
        SourceFile source = new(compilation, "receiver.ly",
            System.Text.Encoding.UTF8.GetBytes(text));
        Check(source.Parse(false) is null && compilation.Check(false) is null
            && compilation.LowerIr() is null, "receiver fixture failed lowering");
        SemanticSymbol nominal = source.SyntaxRoot()!.ChildAt(0).DeclaredSymbol!;
        IrFunction show = nominal.MemberScope!.LookupMember("show")!.IrBody!;
        IrFunction keep = nominal.MemberScope.LookupMember("keep")!.IrBody!;
        IrProgram program = show.Program!;
        Check(show.ObservesReceiver && !keep.ObservesReceiver,
            "receiver escape classification is incorrect");
        IrNode? receiver = null;
        for (int n = 0; n < show.NodeCount(); n += 1)
        {
            IrNode node = show.NodeAt(n);
            if (node.Kind == IrKind.Local && node.Symbol == show.Declaration!.FunctionScope!.ParameterAt(0))
            {
                receiver = node;
            }
        }
        Check(receiver is not null, "observing method lost its receiver slot");
        IrNode badRelease = new(show, IrKind.Unref, origin: show.Body!.Origin);
        show.Body.AddChild(badRelease);
        _ = new IrOperand(badRelease, receiver!);
        program.ValidateStructure();
        Refuses(program.ValidateOperations, "borrowed receiver cleanup accepted");
        badRelease.Destroy();
        keep.ObservesReceiver = true;
        Refuses(program.ValidateOperations, "escaping receiver marked borrowed");
        keep.ObservesReceiver = false;
        show.ObservesReceiver = false;
        Refuses(program.ValidateOperations, "call missing receiver ownership accepted");
        show.ObservesReceiver = true;
        program.ValidateStructure();
        program.ValidateOperations();
        program.ValidateExecution();
        compilation.Destroy();
    }

    private static void CheckPublication()
    {
        Compilation compilation = new();
        SourceFile source = new(compilation, "publication.ly",
            System.Text.Encoding.UTF8.GetBytes("func value()->bool { return true }"));
        Check(source.Parse(false) is null && compilation.Check(false) is null
            && compilation.LowerIr() is null, "publication fixture failed lowering");
        IrFunction function = source.SyntaxRoot()!.ChildAt(0).DeclaredSymbol!.IrBody!;
        SemanticAnalysis analysis = function.Program!.Analysis!;
        IrNode draft = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Bool), literal: new IrLiteral(true));
        using System.IO.StringWriter output = new();
        Refuses(() => compilation.WriteIr(output), "publication accepted a detached draft");
        Check(output.ToString().Length == 0 && !draft.Destroyed,
            "refused publication wrote output or mutated the draft");
        draft.Destroy();
        compilation.WriteIr(output);
        Check(output.ToString().Contains("lyric-ir-1"), "repaired graph did not publish");
        compilation.Destroy();
    }

    public static void Main()
    {
        CheckStructure();
        CheckOperations();
        CheckExecution();
        CheckConstantPayloads();
        CheckPublication();
        CheckMembershipAccounting();
        CheckReceiverSimplification();
        CheckNullableOperations();
        Compilation compilation = new();
        SourceFile source = new(compilation, "probe.ly", Array.Empty<byte>());
        SyntaxNode origin = new(source, SyntaxKind.Function, -1, 0);
        SemanticAnalysis analysis = new(compilation);
        SemanticScope scope = new(analysis, null);
        SemanticSymbol declaration = new(scope, null, "f", SymbolKind.Function,
            analysis.ScalarType(ScalarKind.Unit), false);
        SemanticSymbol second = new(scope, null, "g", SymbolKind.Function,
            analysis.ScalarType(ScalarKind.Unit), false);
        SemanticSymbol nominal = new(scope, null, "Thing", SymbolKind.Class, null, false);
        SemanticSymbol field = new(scope, null, "field", SymbolKind.Field,
            analysis.ScalarType(ScalarKind.I32), true);
        IrProgram program = new(analysis);
        IrFunction function = new(program, declaration);
        IrFunction other = new(program, second);
        Check(analysis.Ir == program && program.Analysis == analysis
            && function.Program == program && program.FunctionCount() == 2,
            "program and function ownership");
        Refuses(() => new IrFunction(program, declaration), "duplicate concrete body accepted");
        Check(declaration.IrBody == function && program.FunctionCount() == 2,
            "duplicate concrete body changed existing membership");
        Refuses(() => new IrProgram(analysis), "duplicate program accepted");
        Check(analysis.Ir == program, "duplicate attachment mutated owner");
        Refuses(() => new IrFunction(program, nominal), "class used as function");
        Check(program.FunctionCount() == 2, "failed constructor leaked function");

        IrFunction teardown = new(program, nominal, IrFunctionKind.Destruction);
        Check(nominal.IrDestruction == teardown && teardown.DestructionClass == nominal
            && teardown.Declaration is null && teardown.Program == program,
            "generated destruction relationship missing");
        Refuses(() => new IrFunction(program, nominal, IrFunctionKind.Destruction),
            "duplicate destruction body accepted");
        Refuses(() => new IrFunction(program, declaration, IrFunctionKind.Destruction),
            "source function accepted as destruction class");
        Check(program.FunctionCount() == 3 && nominal.IrDestruction == teardown,
            "refused destruction attachment changed memberships");
        IrNode teardownNode = new(teardown, IrKind.Return);
        teardown.Destroy();
        Check(nominal.IrDestruction is null && teardown.DestructionClass is null
            && teardown.Program is null && teardownNode.Destroyed && !nominal.Destroyed,
            "destruction body teardown retained links or destroyed class");
        IrFunction replacementTeardown = new(program, nominal, IrFunctionKind.Destruction);

        IrNode body = new(function, IrKind.Region, IrRole.Body);
        function.AddBody(body);
        IrNode loop = new(function, IrKind.For);
        body.AddChild(loop);
        IrNode condition = new(function, IrKind.Region, IrRole.Condition);
        IrNode step = new(function, IrKind.Region, IrRole.Step);
        loop.AddChild(condition);
        loop.AddChild(step);
        Check(function.Body == body && body.BodyOwner == function
            && loop.ChildAt(0) == condition && condition.Parent == loop,
            "structured tree order/backlinks");
        Refuses(() => condition.AddChild(loop), "tree cycle accepted");
        Refuses(() => loop.AddChild(body), "function body reused as nested child");
        Check(loop.Parent == body && loop.ChildCount() == 2, "tree refusal mutated structure");
        IrNode transfer = new(function, IrKind.Continue);
        loop.AddTransfer(transfer);
        Check(transfer.Target == loop, "loop target not registered");
        IrNode foreign = new(other, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.I32));
        Refuses(() => body.AddChild(foreign), "cross-function tree accepted");
        Refuses(() => loop.AddTransfer(foreign), "nontransfer target accepted");

        IrNode value = new(function, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.I32), origin: origin, literal: new IrLiteral(new BigInteger(7)));
        IrNode use = new(function, IrKind.Ref, symbol: nominal);
        IrOperand operand = new(use, value);
        Check(use.OperandCount() == 1 && value.UseCount() == 1
            && operand.Consumer == use && operand.Definition == value,
            "two-ended operand");
        Refuses(() => new IrOperand(use, foreign), "cross-function operand accepted");
        Check(use.OperandCount() == 1 && foreign.UseCount() == 0,
            "failed operand constructor leaked use");
        value.Destroy();
        Check(operand.Definition is null && !use.Destroyed && use.OperandCount() == 1
            && value.Origin is null && value.Function is null,
            "definition deletion must detach, not delete consumer");
        use.Destroy();
        Check(operand.Destroyed && operand.Consumer is null && use.Symbol is null,
            "consumer did not destroy operand");

        IrNode typed = new(function, IrKind.Local, result: IrResult.Address,
            type: analysis.ClassType(nominal));
        Compilation separateCompilation = new();
        SemanticAnalysis separateAnalysis = new(separateCompilation);
        SemanticScope separateScope = new(separateAnalysis, null);
        SemanticSymbol separateClass = new(separateScope, null, "Other", SymbolKind.Class, null, false);
        Refuses(() => typed.SetType(separateAnalysis.ClassType(separateClass)),
            "cross-analysis nominal type accepted");
        Check(typed.Type?.Class == nominal && analysis.Ir == program,
            "refused type change mutated valid IR");
        typed.SetType(analysis.ScalarType(ScalarKind.I32));
        typed.SetType(analysis.ClassType(nominal));
        // Logical locations and captured addresses share ordinary operand
        // relationships, but have distinct execution contracts. This probe checks
        // the relationships only, not relocation or opcode type validation.
        IrNode place = new(function, IrKind.SelectField, result: IrResult.Place,
            type: analysis.ScalarType(ScalarKind.I32), symbol: field);
        IrOperand receiverUse = new(place, typed);
        IrNode address = new(function, IrKind.AddressOf, result: IrResult.Address,
            type: analysis.ScalarType(ScalarKind.I32));
        IrOperand placeUse = new(address, place);
        Check(place.Result == IrResult.Place && address.Result == IrResult.Address
            && place.Symbol == field && receiverUse.Definition == typed
            && placeUse.Definition == place && place.UseCount() == 1,
            "place/address identity or dependency lost");
        place.Destroy();
        Check(placeUse.Definition is null && !address.Destroyed
            && receiverUse.Destroyed && typed.UseCount() == 0,
            "place deletion cascaded to address or retained receiver dependency");
        address.Destroy();
        Check(placeUse.Destroyed && address.Function is null,
            "address destruction retained its operand");

        byte[] bytes = { 1, 2 };
        IrLiteral literal = new(bytes);
        bytes[0] = 9;
        Check(literal.ByteCount() == 2 && literal.ByteAt(0) == 1,
            "value-like literal shares mutable bytes");

        loop.Destroy();
        Check(transfer.Target is null && !transfer.Destroyed
            && condition.Parent is null && !condition.Destroyed,
            "weak tree/transfer removal cascaded");
        // Destroying an actual declaration must retire ALL dependent IR first.
        nominal.Destroy();
        Check(program.Destroyed && analysis.Ir is null && function.Destroyed
            && typed.Destroyed && typed.Type is null && condition.Destroyed
            && function.NodeCount() == 0 && body.BodyOwner is null,
            "declaration invalidation left IR dependencies");
        Check(replacementTeardown.Destroyed && nominal.IrDestruction is null
            && replacementTeardown.DestructionClass is null,
            "class invalidation left destruction body linked");
        program.Destroy();
        Check(!declaration.Destroyed && !origin.Destroyed,
            "IR destruction destroyed borrowed semantic/source objects");

        IrProgram regenerated = new(analysis);
        IrFunction regeneratedFunction = new(regenerated, declaration);
        IrNode regeneratedNode = new(regeneratedFunction, IrKind.Return, origin: origin);
        // Syntax mutation invalidates analysis, which must destroy IR first.
        origin.Destroy();
        Check(analysis.Destroyed && regenerated.Destroyed && regeneratedNode.Destroyed
            && regeneratedNode.Origin is null && regeneratedFunction.Declaration is null,
            "source invalidation left live IR");
        compilation.Destroy();
        separateCompilation.Destroy();
        Console.WriteLine($"IR model: {Checks} checks passed");
    }
}
