using System;
using System.IO;
using System.Numerics;
using System.Text;
using Lyric;

// Development-only backend probe: checked declaration metadata plus explicit IR.
// No alternate tuple implementation is used; the output is the production emitter.
internal static class TupleEmissionProbe
{
    private static void Main(string[] args)
    {
        string mode = args.Length > 1 ? args[1] : "tuple";
        if (mode == "address-initializer")
        {
            AddressInitializer(args[0]);
            return;
        }
        bool structure = mode == "struct" || mode == "initializer";
        bool layout = mode == "empty" || mode == "recursive";
        string text = mode switch
        {
            "empty" => "struct Empty {}\nfunc main() {}",
            "recursive" => "struct Branch { children: [Leaf] }\nstruct Leaf { parent: Branch }\nfunc main() {}",
            "struct" or "initializer" => "struct Pair { number: i32; items: [i32] }\nfunc main() {}",
            _ => "func main() {}"
        };
        Compilation compilation = new();
        SourceFile source = new(compilation, "tuple-backend.ly",
            Encoding.UTF8.GetBytes(text));
        if (source.Parse(false) is not null || compilation.Check(true) is not null)
        {
            throw new Exception("Probe source did not check.");
        }
        SyntaxNode origin = source.SyntaxRoot()!.ChildAt(mode == "recursive" ? 2 : structure || layout ? 1 : 0);
        SemanticAnalysis analysis = origin.DeclaredSymbol!.Type!.Analysis!;
        SemanticType integer = analysis.ScalarType(ScalarKind.I32);
        SemanticType unit = analysis.ScalarType(ScalarKind.Unit);
        SemanticType array = analysis.ArrayType(integer);
        SemanticType tuple = structure || layout
            ? analysis.StructType(source.SyntaxRoot()!.ChildAt(0).DeclaredSymbol!)
            : analysis.TupleType(new[] { integer, array });
        SemanticSymbol? initializedField = mode == "initializer"
            ? source.SyntaxRoot()!.ChildAt(0).ChildAt(1).DeclaredSymbol : null;
        if (initializedField is not null)
        {
            _ = new SemanticScope(initializedField);
        }
        SemanticSymbol? borrowedField = null;
        if (mode == "borrowed")
        {
            SemanticSymbol nominal = new(origin.DeclaredSymbol!.Scope!, null,
                "BorrowedOwner", SymbolKind.Struct, null, false);
            SemanticScope fields = new(nominal);
            borrowedField = new SemanticSymbol(fields, null, "target", SymbolKind.Field,
                array, true, isReference: true);
            _ = new SemanticSymbol(fields, null, "owned", SymbolKind.Field, array, true);
            tuple = analysis.StructType(nominal);
            _ = new SemanticSymbol(fields, null, "peer", SymbolKind.Field,
                tuple, true, isReference: true);
        }
        IrProgram program = new(analysis);
        if (initializedField is not null)
        {
            IrFunction initializer = new(program, initializedField, IrFunctionKind.FieldInitializer);
            IrNode initializerBody = new(initializer, IrKind.Region, IrRole.Body, origin: origin);
            initializer.AddBody(initializerBody);
            IrNode element = new(initializer, IrKind.Constant, result: IrResult.Value,
                type: integer, origin: origin, literal: new IrLiteral(new BigInteger(7)));
            initializerBody.AddChild(element);
            IrNode fresh = new(initializer, IrKind.NewArray, result: IrResult.Value,
                type: array, origin: origin);
            new IrOperand(fresh, element);
            initializerBody.AddChild(fresh);
            IrNode moved = new(initializer, IrKind.Move, result: IrResult.Value,
                type: array, origin: origin);
            new IrOperand(moved, fresh);
            initializerBody.AddChild(moved);
            IrNode returned = new(initializer, IrKind.Return, origin: origin);
            new IrOperand(returned, moved);
            initializerBody.AddChild(returned);
        }
        IrFunction function = new(program, origin.DeclaredSymbol!);
        IrNode body = new(function, IrKind.Region, IrRole.Body, origin: origin);
        function.AddBody(body);
        IrNode Add(IrKind kind, SemanticType? type, IrResult result,
            IrLiteral? literal = null, params IrNode[] operands)
        {
            IrNode node = new(function, kind, result: result, type: type,
                origin: origin, literal: literal);
            foreach (IrNode operand in operands)
            {
                new IrOperand(node, operand);
            }
            body.AddChild(node);
            return node;
        }
        if (mode == "borrowed")
        {
            IrNode target = Add(IrKind.Local, array, IrResult.Address);
            IrNode peer = Add(IrKind.Local, tuple, IrResult.Address);
            IrNode number = Add(IrKind.Constant, integer, IrResult.Value, new IrLiteral(new BigInteger(7)));
            IrNode allocation = Add(IrKind.NewArray, array, IrResult.Value, null, number);
            IrNode transfer = Add(IrKind.Move, array, IrResult.Value, null, allocation);
            IrNode value = Add(IrKind.NewStruct, tuple, IrResult.Value, null, target, transfer, peer);
            IrNode copied = Add(IrKind.Copy, tuple, IrResult.Value, null, value);
            IrNode selected = new(function, IrKind.SelectStruct, result: IrResult.Address,
                type: array, origin: origin, symbol: borrowedField);
            new IrOperand(selected, copied);
            body.AddChild(selected);
            Add(IrKind.ArrayPush, unit, IrResult.Value, null, selected, number);
            Add(IrKind.DropStruct, null, IrResult.None, null, value);
            Add(IrKind.DropStruct, null, IrResult.None, null, copied);
            Add(IrKind.FreeArray, null, IrResult.None, null, target);
            Add(IrKind.DropStruct, null, IrResult.None, null, peer);
        }
        else if (layout)
        {
            // The recursive type graph is finite in storage: the children field
            // is a descriptor, not inline Leaf storage. An empty array provides
            // a finite value without inventing a default Leaf instance.
            IrNode value;
            if (mode == "recursive")
            {
                SemanticType children = source.SyntaxRoot()!.ChildAt(0).ChildAt(0).DeclaredSymbol!.Type!;
                IrNode allocation = Add(IrKind.NewArray, children, IrResult.Value);
                IrNode transfer = Add(IrKind.Move, children, IrResult.Value, null, allocation);
                IrNode emptyBranch = Add(IrKind.NewStruct, tuple, IrResult.Value, null, transfer);
                SemanticType leafType = SemanticTypes.ElementOf(children);
                IrNode parentTransfer = Add(IrKind.Move, tuple, IrResult.Value, null, emptyBranch);
                IrNode leaf = Add(IrKind.NewStruct, leafType, IrResult.Value, null, parentTransfer);
                IrNode leafTransfer = Add(IrKind.Move, leafType, IrResult.Value, null, leaf);
                IrNode populated = Add(IrKind.NewArray, children, IrResult.Value, null, leafTransfer);
                IrNode childrenTransfer = Add(IrKind.Move, children, IrResult.Value, null, populated);
                value = Add(IrKind.NewStruct, tuple, IrResult.Value, null, childrenTransfer);
            }
            else
            {
                value = Add(IrKind.NewStruct, tuple, IrResult.Value);
            }
            IrNode copied = Add(IrKind.Copy, tuple, IrResult.Value, null, value);
            if (mode == "recursive")
            {
                Add(IrKind.DropStruct, null, IrResult.None, null, value);
                Add(IrKind.DropStruct, null, IrResult.None, null, copied);
            }
        }
        else
        {
            IrNode number = Add(IrKind.Constant, integer, IrResult.Value, new IrLiteral(new BigInteger(7)));
            IrNode allocation;
            if (initializedField is not null)
            {
                allocation = new(function, IrKind.CallInitializer, result: IrResult.Value,
                    type: array, origin: origin, symbol: initializedField);
                body.AddChild(allocation);
            }
            else
            {
                allocation = Add(IrKind.NewArray, array, IrResult.Value, null, number);
            }
            IrNode transfer = Add(IrKind.Move, array, IrResult.Value, null, allocation);
            IrNode value = Add(structure ? IrKind.NewStruct : IrKind.NewTuple, tuple, IrResult.Value, null, number, transfer);
            IrNode copied = Add(IrKind.Copy, tuple, IrResult.Value, null, value);
            if (structure)
            {
                SemanticSymbol field = source.SyntaxRoot()!.ChildAt(0).ChildAt(1).DeclaredSymbol!;
                IrNode selected = new(function, IrKind.SelectStruct, result: IrResult.Address,
                    type: array, origin: origin, symbol: field);
                new IrOperand(selected, copied);
                body.AddChild(selected);
            }
            else
            {
                Add(IrKind.SelectTuple, array, IrResult.Address, new IrLiteral(BigInteger.One), copied);
            }
            Add(structure ? IrKind.DropStruct : IrKind.DropTuple, null, IrResult.None, null, value);
            Add(structure ? IrKind.DropStruct : IrKind.DropTuple, null, IrResult.None, null, copied);
        }
        IrNode nothing = Add(IrKind.Constant, unit, IrResult.Value);
        Add(IrKind.Return, null, IrResult.None, null, nothing);
        using StringWriter output = new();
        if (compilation.EmitIrC(output) is not null)
        {
            throw new Exception("Tuple IR emission failed.");
        }
        File.WriteAllText(args[0], output.ToString());
        compilation.Destroy();
    }
    // This exercises the IR/backend boundary while source reference fields remain
    // gated. Ordinary objects are process-lived here; array backing is released.
    private static void AddressInitializer(string outputPath)
    {
        Compilation compilation = new();
        SourceFile source = new(compilation, "address-initializer.ly", Encoding.UTF8.GetBytes(
            "class Box { items:[i32]; func Box(self) { self.items=[7] } }\nfunc main() {}"));
        if (source.Parse(false) is not null || compilation.Check(true) is not null)
        {
            throw new Exception("Address initializer setup did not check.");
        }
        SyntaxNode root = source.SyntaxRoot()!;
        SyntaxNode origin = root.ChildAt(1);
        SemanticSymbol main = origin.DeclaredSymbol!;
        SemanticAnalysis analysis = main.Type!.Analysis!;
        SemanticSymbol box = root.ChildAt(0).DeclaredSymbol!;
        SemanticSymbol items = root.ChildAt(0).ChildAt(0).DeclaredSymbol!;
        SemanticSymbol constructor = root.ChildAt(0).ChildAt(1).DeclaredSymbol!;
        SemanticType array = items.Type!;
        SemanticSymbol holder = new(main.Scope!, null, "AddressHolder", SymbolKind.Struct, null, false);
        SemanticScope fields = new(holder);
        SemanticSymbol field = new(fields, null, "target", SymbolKind.Field, array, true, isReference: true);
        _ = new SemanticScope(field);
        // All semantic relationships are established before lowering, since a
        // semantic edit invalidates existing IR. Retain the real class bodies.
        if (compilation.LowerIr() is not null)
        {
            throw new Exception("Address initializer prerequisite lowering failed.");
        }
        IrProgram program = analysis.Ir!;
        IrFunction initializer = new(program, field, IrFunctionKind.FieldInitializer);
        IrNode body = new(initializer, IrKind.Region, IrRole.Body, origin: origin);
        initializer.AddBody(body);
        IrNode allocate = new(initializer, IrKind.NewObject, result: IrResult.Value,
            type: analysis.ClassType(box), origin: origin, symbol: box);
        body.AddChild(allocate);
        IrNode construct = new(initializer, IrKind.Call, result: IrResult.Value,
            type: constructor.Type!, origin: origin, symbol: constructor);
        new IrOperand(construct, allocate);
        body.AddChild(construct);
        IrNode selected = new(initializer, IrKind.SelectField, result: IrResult.Place,
            type: array, origin: origin, symbol: items);
        new IrOperand(selected, allocate);
        body.AddChild(selected);
        IrNode address = new(initializer, IrKind.AddressOf, result: IrResult.Address,
            type: array, origin: origin);
        new IrOperand(address, selected);
        body.AddChild(address);
        IrNode returned = new(initializer, IrKind.Return, origin: origin);
        new IrOperand(returned, address);
        body.AddChild(returned);

        // Replace only main's empty IR body; the actual class implementation stays.
        main.IrBody!.Destroy();
        IrFunction caller = new(program, main);
        IrNode callerBody = new(caller, IrKind.Region, IrRole.Body, origin: origin);
        caller.AddBody(callerBody);
        IrNode call = new(caller, IrKind.CallInitializer, result: IrResult.Address,
            type: array, origin: origin, symbol: field);
        callerBody.AddChild(call);
        IrNode release = new(caller, IrKind.FreeArray, origin: origin);
        new IrOperand(release, call);
        callerBody.AddChild(release);
        IrNode unit = new(caller, IrKind.Constant, result: IrResult.Value,
            type: analysis.ScalarType(ScalarKind.Unit), origin: origin);
        callerBody.AddChild(unit);
        IrNode end = new(caller, IrKind.Return, origin: origin);
        new IrOperand(end, unit);
        callerBody.AddChild(end);
        // Deliberate malformed graphs must fail at their category contract,
        // not merely crash later in C emission.
        void RejectCategory(string diagnostic)
        {
            try
            {
                program.ValidateOperations();
            }
            catch (InvalidOperationException error) when (error.Message.Contains(diagnostic, StringComparison.Ordinal))
            {
                return;
            }
            throw new Exception("Malformed initializer category was not rejected: " + diagnostic);
        }
        IrNode wrongCall = new(caller, IrKind.CallInitializer, result: IrResult.Value,
            type: array, origin: origin, symbol: field);
        callerBody.AddChild(wrongCall);
        RejectCategory("result category");
        wrongCall.Destroy();
        IrNode loaded = new(initializer, IrKind.Load, result: IrResult.Value,
            type: array, origin: origin);
        new IrOperand(loaded, address);
        body.AddChild(loaded);
        returned.OperandAt(0).Destroy();
        new IrOperand(returned, loaded);
        RejectCategory("control operand must match");
        returned.OperandAt(0).Destroy();
        new IrOperand(returned, address);
        loaded.Destroy();
        using StringWriter output = new();
        if (compilation.EmitIrC(output) is not null)
        {
            throw new Exception("Address initializer emission failed.");
        }
        File.WriteAllText(outputPath, output.ToString());
        compilation.Destroy();
    }

}
