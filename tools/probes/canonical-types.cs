using System;
using System.Collections.Generic;
using System.Reflection;
using Lyric;

// Development diagnostic, not a retained language grader. Reflection observes
// bucket occupancy only; all mutations go through the real model operations.
internal static class CanonicalTypeProbe
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

    private static List<SemanticType>[] Buckets(SemanticAnalysis analysis)
    {
        FieldInfo field = typeof(SemanticAnalysis).GetField("TypeBuckets",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<SemanticType>[])field.GetValue(analysis)!;
    }

    private static void FiniteStructLayouts()
    {
        foreach (string shape in new[] { "scalar", "repeated", "direct", "mutual",
            "tuple-cycle", "array-cycle", "tuple-array-cycle", "handle" })
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope root = new(analysis, null);
            SemanticSymbol first = new(root, null, "First", SymbolKind.Struct, null, false);
            SemanticSymbol second = new(root, null, "Second", SymbolKind.Struct, null, false);
            SemanticScope firstFields = new(first);
            SemanticScope secondFields = new(second);
            SemanticType a = analysis.StructType(first);
            SemanticType b = analysis.StructType(second);
            SemanticType integer = analysis.ScalarType(ScalarKind.I32);
            SemanticType contents = integer;
            if (shape == "direct")
            {
                contents = a;
            }
            else if (shape == "mutual" || shape == "tuple-cycle")
            {
                contents = shape == "mutual" ? b : analysis.TupleType(new[] { integer, b });
                new SemanticSymbol(secondFields, null, "back", SymbolKind.Field, a, true);
            }
            else if (shape == "array-cycle" || shape == "tuple-array-cycle")
            {
                SemanticType array = analysis.ArrayType(a);
                contents = shape == "array-cycle" ? array : analysis.TupleType(new[] { integer, array });
            }
            else if (shape == "repeated")
            {
                contents = analysis.TupleType(new[] { b, b });
                new SemanticSymbol(secondFields, null, "number", SymbolKind.Field, integer, true);
            }
            else if (shape == "handle")
            {
                SemanticSymbol owner = new(root, null, "Object", SymbolKind.Class, null, false);
                contents = analysis.ClassType(owner);
            }
            new SemanticSymbol(firstFields, null, "value", SymbolKind.Field, contents, true);
            bool expected = shape is not ("direct" or "mutual" or "tuple-cycle");
            Check(SemanticTypes.HasFiniteLayout(a) == expected, "incorrect finite layout: " + shape);
            // Invalid inline layouts must remain destructible model drafts.
            compilation.Destroy();
            Check(a.Destroyed && b.Destroyed, "layout draft teardown failed: " + shape);
        }
    }

    private static void ContainedBorrowTypes()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope root = new(analysis, null);
        SemanticType integer = analysis.ScalarType(ScalarKind.I32);
        SemanticSymbol structure = new(root, null, "Borrowed", SymbolKind.Struct, null, false);
        SemanticScope fields = new(structure);
        SemanticType type = analysis.StructType(structure);
        Check(!SemanticTypes.HasContainedBorrows(type), "empty struct invented a borrow");
        SemanticSymbol alias = new(fields, null, "target", SymbolKind.Field,
            integer, true, isReference: true);
        Check(alias.Scope == fields && alias.IsReference && alias.Type == integer,
            "reference field lost its actual scope or referent type");
        Check(SemanticTypes.HasContainedBorrows(type), "direct stored borrow was hidden");
        Check(!SemanticTypes.HasOwnedContents(type), "stored alias acquired ownership");
        Check(SemanticTypes.HasFiniteLayout(type), "stored alias has infinite layout");
        SemanticType tuple = analysis.TupleType(new[] { integer, type, type });
        Check(SemanticTypes.HasContainedBorrows(tuple), "tuple wrapper hid a borrow");
        Check(SemanticTypes.HasContainedBorrows(analysis.ArrayType(tuple)),
            "array descriptor hid a contained borrow");
        SemanticSymbol wrapper = new(root, null, "Wrapper", SymbolKind.Struct, null, false);
        SemanticScope wrapperFields = new(wrapper);
        SemanticType wrapped = analysis.StructType(wrapper);
        SemanticSymbol nested = new(wrapperFields, null, "inner", SymbolKind.Field, type, true);
        Check(SemanticTypes.HasContainedBorrows(wrapped), "nominal wrapper hid a borrow");
        // This cycle crosses an array descriptor. Queries must terminate without
        // confusing layout finiteness, ownership, and contained alias presence.
        SemanticSymbol recursive = new(wrapperFields, null, "children", SymbolKind.Field,
            analysis.ArrayType(wrapped), true);
        Check(SemanticTypes.HasFiniteLayout(wrapped), "array recursion became inline recursion");
        Check(SemanticTypes.HasContainedBorrows(wrapped), "recursive wrapper hid a borrow");
        alias.Destroy();
        Check(!SemanticTypes.HasContainedBorrows(wrapped), "borrow query cached a retired field");
        SemanticSymbol selfAlias = new(fields, null, "selfAlias", SymbolKind.Field,
            type, true, isReference: true);
        Check(SemanticTypes.HasFiniteLayout(type) && !SemanticTypes.HasOwnedContents(type),
            "borrowed self-reference was treated as inline ownership");
        Check(SemanticTypes.HasContainedBorrows(type), "borrowed self-reference was hidden");
        SemanticSymbol objectType = new(root, null, "Object", SymbolKind.Class, null, false);
        SemanticScope objectFields = new(objectType);
        bool refused = false;
        try
        {
            _ = new SemanticSymbol(objectFields, null, "bad", SymbolKind.Field,
                integer, true, isReference: true);
        }
        catch (ArgumentException)
        {
            refused = true;
        }
        Check(refused && objectFields.LookupMember("bad") is null,
            "object reference field was admitted or partially registered");
        Check(!SemanticTypes.HasContainedBorrows(analysis.ClassType(objectType)),
            "object handle was classified as a storage alias");
        compilation.Destroy();
        Check(selfAlias.Destroyed && recursive.Destroyed && nested.Destroyed,
            "contained-borrow model left owned fields alive");
    }

    private static void StructIdentities()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope root = new(analysis, null);
        SemanticSymbol tree = new(root, null, "Tree", SymbolKind.Struct, null, false);
        SemanticSymbol other = new(root, null, "OtherTree", SymbolKind.Struct, null, false);
        SemanticSymbol handle = new(root, null, "Handle", SymbolKind.Class, null, false);
        SemanticScope fields = new(tree);
        SemanticType type = analysis.StructType(tree);
        SemanticType array = analysis.ArrayType(type);
        SemanticType tuple = analysis.TupleType(new[] { type, array });
        SemanticSymbol children = new(fields, null, "children", SymbolKind.Field, array, true);
        Check(analysis.StructType(tree) == type && tree.NominalType == type
            && type.Declaration == tree, "struct identity was not canonical and bidirectional");
        Check(analysis.StructType(other) != type, "distinct nominal structs were merged");
        Check(type.Class is null && array.Class is null, "struct was exposed as a class handle");
        Refuses(() => analysis.ClassType(tree), "class factory accepted a struct");
        Refuses(() => analysis.StructType(handle), "struct factory accepted a class");
        Compilation foreignCompilation = new();
        SemanticAnalysis foreign = new(foreignCompilation);
        Refuses(() => foreign.StructType(tree), "foreign analysis accepted struct declaration");
        Check(tree.NominalType == type && !type.Destroyed, "refusal retired the valid struct type");
        Check(SemanticTypes.HasOwnedContents(type), "recursive array field lost ownership");
        SemanticScope otherFields = new(other);
        SemanticType otherType = analysis.StructType(other);
        SemanticSymbol number = new(otherFields, null, "number", SymbolKind.Field,
            analysis.ScalarType(ScalarKind.I32), true);
        Check(!SemanticTypes.HasOwnedContents(otherType), "scalar field invented ownership");
        SemanticSymbol nested = new(otherFields, null, "nested", SymbolKind.Field, type, true);
        Check(SemanticTypes.HasOwnedContents(otherType), "nested struct lost array ownership");
        // Remove the dependency before testing unrelated-declaration survival below.
        nested.Destroy();
        tree.Destroy();
        Check(fields.Destroyed && children.Destroyed, "struct did not destroy owned members");
        Check(type.Destroyed && array.Destroyed && tuple.Destroyed && tree.NominalType is null,
            "recursive struct dependency retirement was incomplete");
        Check(!other.Destroyed && !other.NominalType!.Destroyed,
            "retirement destroyed an unrelated nominal type");
        foreignCompilation.Destroy();
        compilation.Destroy();
    }

    private static void NominalMemberScopes()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope root = new(analysis, null);
        SemanticType integer = analysis.ScalarType(ScalarKind.I32);
        SemanticSymbol outer = new(root, null, "x", SymbolKind.Local, integer, false);
        foreach (SymbolKind kind in new[] { SymbolKind.Class, SymbolKind.Struct })
        {
            SemanticSymbol owner = new(root, null, kind.ToString(), kind, null, false);
            SemanticScope members = new(owner);
            Check(owner.MemberScope == members && members.MemberOwner == owner,
                "nominal member scope did not attach both ends");
            Check(members.Class == (kind == SymbolKind.Class ? owner : null),
                "struct scope was exposed as class storage");
            SemanticSymbol field = new(members, null, "x", SymbolKind.Field, integer, true);
            Check(members.LookupMember("x") == field && members.Lookup("x") == outer,
                "qualified members leaked into lexical lookup");
            SemanticSymbol other = new(root, null, "Other" + kind, kind, null, false);
            Refuses(() => other.AddMemberScope(members), "second owner accepted attached scope");
            Check(owner.MemberScope == members && members.MemberOwner == owner
                && other.MemberScope is null, "refusal mutated nominal ownership");
            SemanticScope? initializer = null;
            if (kind == SymbolKind.Struct)
            {
                initializer = new SemanticScope(field);
                Check(field.InitializerScope == initializer && initializer.InitializerField == field,
                    "initializer did not attach both owners");
                Check(initializer.Parent == root && initializer.Lookup("x") == outer
                    && initializer.Function is null && initializer.MemberOwner is null,
                    "initializer lookup exposed a sibling or fabricated function");
                Refuses(() => new SemanticScope(field), "duplicate initializer scope accepted");
                Check(field.InitializerScope == initializer && initializer.InitializerField == field,
                    "duplicate refusal changed initializer ownership");
                initializer.Destroy();
                Check(field.InitializerScope is null && initializer.InitializerField is null
                    && !field.Destroyed, "scope teardown retained initializer relationship");
                initializer = new SemanticScope(field);
            }
            else
            {
                Refuses(() => new SemanticScope(field), "class field initializer scope accepted");
                Check(field.InitializerScope is null, "class refusal left initializer scope");
            }
            owner.Destroy();
            Check(initializer is null || (initializer.Destroyed && initializer.InitializerField is null
                && field.InitializerScope is null), "field teardown leaked initializer scope");
            Check(members.Destroyed && members.MemberOwner is null && owner.MemberScope is null,
                "nominal destruction left a member relationship");
            Check(field.Destroyed && !outer.Destroyed, "member destruction escaped its ownership");
        }
        compilation.Destroy();
    }

    private static void FieldInitializerIrContracts()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope root = new(analysis, null);
        SemanticType integer = analysis.ScalarType(ScalarKind.I32);
        SemanticSymbol nominal = new(root, null, "Defaults", SymbolKind.Struct, null, false);
        SemanticScope members = new(nominal);
        SemanticSymbol field = new(members, null, "number", SymbolKind.Field, integer, true);
        SemanticSymbol callerDeclaration = new(root, null, "caller", SymbolKind.Function,
            integer, false);
        IrProgram program = new(analysis);
        Refuses(() => new IrFunction(program, field, IrFunctionKind.FieldInitializer),
            "initializer body accepted without initializer scope");
        SemanticScope initializer = new(field);
        IrFunction function = new(program, field, IrFunctionKind.FieldInitializer);
        Check(field.IrBody == function && function.Declaration == field,
            "initializer body lost actual field identity");
        Refuses(() => new IrFunction(program, field, IrFunctionKind.FieldInitializer),
            "duplicate initializer body accepted");
        Refuses(() => new IrFunction(program, field), "field accepted as source function");
        Check(field.IrBody == function && !function.Destroyed,
            "body refusal changed existing initializer");
        IrNode body = new(function, IrKind.Region, IrRole.Body);
        function.AddBody(body);
        IrNode number = new(function, IrKind.Constant, result: IrResult.Value,
            type: integer, literal: new IrLiteral(new System.Numerics.BigInteger(7)));
        body.AddChild(number);
        IrNode returned = new(function, IrKind.Return);
        new IrOperand(returned, number);
        body.AddChild(returned);
        program.ValidateStructure();
        program.ValidateOperations();
        program.ValidateExecution();
        Check(!function.Destroyed, "valid initializer body rejected");
        IrFunction caller = new(program, callerDeclaration);
        IrNode callerBody = new(caller, IrKind.Region, IrRole.Body);
        caller.AddBody(callerBody);
        IrNode call = new(caller, IrKind.CallInitializer, result: IrResult.Value,
            type: integer, symbol: field);
        callerBody.AddChild(call);
        IrNode callerReturn = new(caller, IrKind.Return);
        new IrOperand(callerReturn, call);
        callerBody.AddChild(callerReturn);
        program.ValidateStructure();
        program.ValidateOperations();
        program.ValidateExecution();
        Check(call.Symbol == field, "initializer call lost field identity");
        call.SetType(analysis.ScalarType(ScalarKind.Bool));
        Refuses(program.ValidateOperations, "initializer call accepted wrong result type");
        call.SetType(integer);
        IrNode argument = new(caller, IrKind.Constant, result: IrResult.Value,
            type: integer, literal: new IrLiteral(new System.Numerics.BigInteger(1)));
        IrOperand extra = new(call, argument);
        Refuses(program.ValidateOperations, "initializer call accepted an argument");
        extra.Destroy();
        argument.Destroy();
        IrNode wrongTarget = new(caller, IrKind.CallInitializer, result: IrResult.Value,
            type: integer, symbol: callerDeclaration);
        Refuses(program.ValidateOperations, "initializer call accepted a function target");
        wrongTarget.Destroy();
        program.ValidateStructure();
        program.ValidateOperations();
        program.ValidateExecution();
        initializer.Destroy();
        Check(program.Destroyed && function.Destroyed && field.IrBody is null
            && field.InitializerScope is null && !field.Destroyed,
            "initializer scope removal did not retire dependent IR");
        compilation.Destroy();
    }

    private static void TupleIrContracts()
    {
        foreach (string variant in new[] { "valid", "wrong-order", "missing-component",
            "negative-selector", "large-selector", "wrong-result" })
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope scope = new(analysis, null);
            SemanticType integer = analysis.ScalarType(ScalarKind.I32);
            SemanticType boolean = analysis.ScalarType(ScalarKind.Bool);
            SemanticType tuple = analysis.TupleType(new[] { integer, boolean });
            SemanticSymbol declaration = new(scope, null, "tuple", SymbolKind.Function, integer, false);
            IrProgram program = new(analysis);
            IrFunction function = new(program, declaration);
            IrNode body = new(function, IrKind.Region, IrRole.Body);
            function.AddBody(body);
            IrNode number = new(function, IrKind.Constant, result: IrResult.Value,
                type: integer, literal: new IrLiteral(new System.Numerics.BigInteger(7)));
            body.AddChild(number);
            IrNode flag = new(function, IrKind.Constant, result: IrResult.Value,
                type: boolean, literal: new IrLiteral(true));
            body.AddChild(flag);
            IrNode aggregate = new(function, IrKind.NewTuple, result: IrResult.Value, type: tuple);
            new IrOperand(aggregate, variant == "wrong-order" ? flag : number);
            if (variant != "missing-component")
            {
                new IrOperand(aggregate, variant == "wrong-order" ? number : flag);
            }
            body.AddChild(aggregate);
            int index = variant == "negative-selector" ? -1 : variant == "large-selector" ? 2 : 0;
            SemanticType selectedType = variant == "wrong-result" ? boolean : integer;
            IrNode selection = new(function, IrKind.SelectTuple, result: IrResult.Address,
                type: selectedType, literal: new IrLiteral(new System.Numerics.BigInteger(index)));
            new IrOperand(selection, aggregate);
            body.AddChild(selection);
            IrNode load = new(function, IrKind.Load, result: IrResult.Value, type: selectedType);
            new IrOperand(load, selection);
            body.AddChild(load);
            IrNode result = new(function, IrKind.Return);
            // Keep Return valid in the wrong-result case, isolating selection's error.
            new IrOperand(result, variant == "wrong-result" ? number : load);
            body.AddChild(result);
            program.ValidateStructure();
            if (variant == "valid")
            {
                program.ValidateOperations();
                program.ValidateExecution();
                Check(true, "valid tuple IR rejected");
            }
            else
            {
                Refuses(program.ValidateOperations, "invalid tuple IR accepted: " + variant);
            }
            compilation.Destroy();
        }
    }

    private static void TupleOwnershipIrContracts()
    {
        foreach (string variant in new[] { "valid", "missing-move", "wrong-drop" })
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope scope = new(analysis, null);
            SemanticType unit = analysis.ScalarType(ScalarKind.Unit);
            SemanticType array = analysis.ArrayType(analysis.ScalarType(ScalarKind.I32));
            SemanticType tuple = analysis.TupleType(new[] { array });
            SemanticSymbol declaration = new(scope, null, "tuple", SymbolKind.Function, unit, false);
            IrProgram program = new(analysis);
            IrFunction function = new(program, declaration);
            IrNode body = new(function, IrKind.Region, IrRole.Body);
            function.AddBody(body);
            IrNode allocation = new(function, IrKind.NewArray, result: IrResult.Value, type: array);
            body.AddChild(allocation);
            IrNode incoming = allocation;
            if (variant != "missing-move")
            {
                incoming = new(function, IrKind.Move, result: IrResult.Value, type: array);
                new IrOperand(incoming, allocation);
                body.AddChild(incoming);
            }
            IrNode aggregate = new(function, IrKind.NewTuple, result: IrResult.Value, type: tuple);
            new IrOperand(aggregate, incoming);
            body.AddChild(aggregate);
            IrNode cleanup = new(function, IrKind.DropTuple);
            new IrOperand(cleanup, variant == "wrong-drop" ? allocation : aggregate);
            body.AddChild(cleanup);
            IrNode nothing = new(function, IrKind.Constant, result: IrResult.Value, type: unit);
            body.AddChild(nothing);
            IrNode result = new(function, IrKind.Return);
            new IrOperand(result, nothing);
            body.AddChild(result);
            program.ValidateStructure();
            if (variant == "valid")
            {
                program.ValidateOperations();
                program.ValidateExecution();
                Check(true, "valid owning tuple IR rejected");
            }
            else
            {
                Refuses(program.ValidateOperations, "invalid owning tuple IR accepted: " + variant);
            }
            compilation.Destroy();
        }
    }

    private static void StructIrContracts()
    {
        foreach (string variant in new[] { "valid", "wrong-order", "missing-component",
            "foreign-field", "missing-field", "wrong-result" })
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope scope = new(analysis, null);
            SemanticType integer = analysis.ScalarType(ScalarKind.I32);
            SemanticType boolean = analysis.ScalarType(ScalarKind.Bool);
            SemanticSymbol nominal = new(scope, null, "Pair", SymbolKind.Struct, null, false);
            SemanticScope fields = new(nominal);
            SemanticSymbol first = new(fields, null, "first", SymbolKind.Field, integer, true);
            new SemanticSymbol(fields, null, "second", SymbolKind.Field, boolean, true);
            SemanticSymbol other = new(scope, null, "Other", SymbolKind.Struct, null, false);
            SemanticScope otherFields = new(other);
            SemanticSymbol foreign = new(otherFields, null, "first", SymbolKind.Field, integer, true);
            SemanticType tuple = analysis.StructType(nominal);
            SemanticSymbol declaration = new(scope, null, "tuple", SymbolKind.Function, integer, false);
            IrProgram program = new(analysis);
            IrFunction function = new(program, declaration);
            IrNode body = new(function, IrKind.Region, IrRole.Body);
            function.AddBody(body);
            IrNode number = new(function, IrKind.Constant, result: IrResult.Value,
                type: integer, literal: new IrLiteral(new System.Numerics.BigInteger(7)));
            body.AddChild(number);
            IrNode flag = new(function, IrKind.Constant, result: IrResult.Value,
                type: boolean, literal: new IrLiteral(true));
            body.AddChild(flag);
            IrNode aggregate = new(function, IrKind.NewStruct, result: IrResult.Value, type: tuple);
            new IrOperand(aggregate, variant == "wrong-order" ? flag : number);
            if (variant != "missing-component")
            {
                new IrOperand(aggregate, variant == "wrong-order" ? number : flag);
            }
            body.AddChild(aggregate);
            SemanticType selectedType = variant == "wrong-result" ? boolean : integer;
            IrNode selection = new(function, IrKind.SelectStruct, result: IrResult.Address,
                type: selectedType, symbol: variant == "foreign-field" ? foreign
                    : variant == "missing-field" ? null : first);
            new IrOperand(selection, aggregate);
            body.AddChild(selection);
            IrNode load = new(function, IrKind.Load, result: IrResult.Value, type: selectedType);
            new IrOperand(load, selection);
            body.AddChild(load);
            IrNode result = new(function, IrKind.Return);
            // Keep Return valid in the wrong-result case, isolating selection's error.
            new IrOperand(result, variant == "wrong-result" ? number : load);
            body.AddChild(result);
            program.ValidateStructure();
            if (variant == "valid")
            {
                program.ValidateOperations();
                program.ValidateExecution();
                Check(true, "valid struct IR rejected");
            }
            else
            {
                Refuses(program.ValidateOperations, "invalid struct IR accepted: " + variant);
            }
            compilation.Destroy();
        }
    }

    private static void BorrowedStructIrContracts()
    {
        foreach (string variant in new[] { "address", "value", "move" })
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope scope = new(analysis, null);
            SemanticType unit = analysis.ScalarType(ScalarKind.Unit);
            SemanticType array = analysis.ArrayType(analysis.ScalarType(ScalarKind.I32));
            SemanticSymbol nominal = new(scope, null, "Borrowed", SymbolKind.Struct, null, false);
            SemanticScope fields = new(nominal);
            SemanticSymbol field = new(fields, null, "items", SymbolKind.Field, array, true,
                isReference: true);
            SemanticType structure = analysis.StructType(nominal);
            SemanticSymbol declaration = new(scope, null, "borrow", SymbolKind.Function, unit, false);
            IrProgram program = new(analysis);
            IrFunction function = new(program, declaration);
            IrNode body = new(function, IrKind.Region, IrRole.Body);
            function.AddBody(body);
            IrNode target = new(function, IrKind.Local, result: IrResult.Address, type: array);
            body.AddChild(target);
            IrNode incoming = target;
            if (variant != "address")
            {
                incoming = new(function, IrKind.NewArray, result: IrResult.Value, type: array);
                body.AddChild(incoming);
                if (variant == "move")
                {
                    IrNode transfer = new(function, IrKind.Move, result: IrResult.Value, type: array);
                    new IrOperand(transfer, incoming);
                    body.AddChild(transfer);
                    incoming = transfer;
                }
            }
            IrNode aggregate = new(function, IrKind.NewStruct, result: IrResult.Value, type: structure);
            new IrOperand(aggregate, incoming);
            body.AddChild(aggregate);
            IrNode selected = new(function, IrKind.SelectStruct, result: IrResult.Address,
                type: array, symbol: field);
            new IrOperand(selected, aggregate);
            body.AddChild(selected);
            // Only the original descriptor owns storage. The aggregate's alias
            // does not need a transfer and does not acquire cleanup responsibility.
            IrNode cleanup = new(function, IrKind.FreeArray);
            new IrOperand(cleanup, target);
            body.AddChild(cleanup);
            IrNode nothing = new(function, IrKind.Constant, result: IrResult.Value, type: unit);
            body.AddChild(nothing);
            IrNode returned = new(function, IrKind.Return);
            new IrOperand(returned, nothing);
            body.AddChild(returned);
            program.ValidateStructure();
            if (variant == "address")
            {
                program.ValidateOperations();
                program.ValidateExecution();
                Check(true, "borrowed struct address operand rejected");
            }
            else
            {
                Refuses(program.ValidateOperations, "borrowed struct accepted owning value: " + variant);
            }
            compilation.Destroy();
        }
    }

    private static void StructOwnershipIrContracts()
    {
        foreach (string variant in new[] { "valid", "copy-owner", "return-owner", "missing-move", "wrong-drop" })
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope scope = new(analysis, null);
            SemanticType unit = analysis.ScalarType(ScalarKind.Unit);
            SemanticType array = analysis.ArrayType(analysis.ScalarType(ScalarKind.I32));
            SemanticSymbol nominal = new(scope, null, "Owner", SymbolKind.Struct, null, false);
            SemanticScope fields = new(nominal);
            new SemanticSymbol(fields, null, "items", SymbolKind.Field, array, true);
            SemanticType tuple = analysis.StructType(nominal);
            SemanticSymbol declaration = new(scope, null, "tuple", SymbolKind.Function,
                variant == "return-owner" ? tuple : unit, false);
            IrProgram program = new(analysis);
            IrFunction function = new(program, declaration);
            IrNode body = new(function, IrKind.Region, IrRole.Body);
            function.AddBody(body);
            IrNode allocation = new(function, IrKind.NewArray, result: IrResult.Value, type: array);
            body.AddChild(allocation);
            IrNode incoming = allocation;
            if (variant != "missing-move")
            {
                incoming = new(function, IrKind.Move, result: IrResult.Value, type: array);
                new IrOperand(incoming, allocation);
                body.AddChild(incoming);
            }
            IrNode aggregate = new(function, IrKind.NewStruct, result: IrResult.Value, type: tuple);
            new IrOperand(aggregate, incoming);
            body.AddChild(aggregate);
            if (variant == "return-owner")
            {
                // The whole completed record is an owner, not merely its array input.
                IrNode transfer = new(function, IrKind.Move, result: IrResult.Value, type: tuple);
                new IrOperand(transfer, aggregate);
                body.AddChild(transfer);
                IrNode returned = new(function, IrKind.Return);
                new IrOperand(returned, transfer);
                body.AddChild(returned);
                program.ValidateStructure();
                program.ValidateOperations();
                program.ValidateExecution();
                Check(true, "struct owner return rejected");
                compilation.Destroy();
                continue;
            }
            if (variant == "copy-owner")
            {
                // Validation establishes legal ownership operations, not deep-copy execution.
                IrNode copy = new(function, IrKind.Copy, result: IrResult.Value, type: tuple);
                new IrOperand(copy, aggregate);
                body.AddChild(copy);
                IrNode dropCopy = new(function, IrKind.DropStruct);
                new IrOperand(dropCopy, copy);
                body.AddChild(dropCopy);
            }
            IrNode cleanup = new(function, IrKind.DropStruct);
            new IrOperand(cleanup, variant == "wrong-drop" ? allocation : aggregate);
            body.AddChild(cleanup);
            IrNode nothing = new(function, IrKind.Constant, result: IrResult.Value, type: unit);
            body.AddChild(nothing);
            IrNode result = new(function, IrKind.Return);
            new IrOperand(result, nothing);
            body.AddChild(result);
            program.ValidateStructure();
            if (variant is "valid" or "copy-owner")
            {
                program.ValidateOperations();
                program.ValidateExecution();
                Check(true, "valid owning struct IR rejected");
            }
            else
            {
                Refuses(program.ValidateOperations, "invalid owning struct IR accepted: " + variant);
            }
            compilation.Destroy();
        }
    }

    private static void SeparateMethodRelationships()
    {
        Compilation compilation = new();
        SourceFile source = new(compilation, "separate.ly",
            System.Text.Encoding.UTF8.GetBytes(
                "struct P { func f(n:i32)->i32; }\n"
                + "func P.f(value:i32)->i32 { return value }\n"));
        Check(source.Parse(false) is null && compilation.Check(false) is null,
            "separate body fixture failed checking");
        SyntaxNode declaration = source.SyntaxRoot()!.ChildAt(0).ChildAt(0);
        SyntaxNode implementation = source.SyntaxRoot()!.ChildAt(1);
        SemanticSymbol method = declaration.DeclaredSymbol!;
        Check(method.Declaration == declaration && method.Implementation == implementation
            && implementation.ImplementedSymbol == method && implementation.DeclaredSymbol is null,
            "separate body replaced or duplicated the declaration");
        Check(method.FunctionScope!.ParameterCount() == 1
            && method.FunctionScope.ParameterAt(0).Name == "value",
            "body parameter spelling did not bind to the single parameter slot");
        Check(compilation.LowerIr() is null, "separate body IR failed lowering");
        IrFunction body = method.IrBody!;
        Refuses(() => implementation.AddImplementedSymbol(method),
            "duplicate implementation attachment accepted");
        Check(!body.Destroyed && method.IrBody == body
            && method.Implementation == implementation,
            "refused attachment changed the existing body");

        Compilation foreign = new();
        SourceFile foreignSource = new(foreign, "foreign.ly",
            System.Text.Encoding.UTF8.GetBytes("func foreign() {}"));
        Check(foreignSource.Parse(false) is null, "foreign syntax fixture failed parsing");
        SyntaxNode foreignBody = foreignSource.SyntaxRoot()!.ChildAt(0);
        implementation.RemoveImplementedSymbol(method);
        Check(body.Destroyed && method.IrBody is null && method.Implementation is null
            && implementation.ImplementedSymbol is null && method.Declaration == declaration,
            "detaching implementation did not retire IR and preserve declaration");
        Refuses(() => foreignBody.AddImplementedSymbol(method),
            "foreign implementation attachment accepted");
        Check(method.Implementation is null && foreignBody.ImplementedSymbol is null,
            "foreign refusal left partial implementation links");
        implementation.AddImplementedSymbol(method);
        Check(method.BodySyntax() == implementation, "body selection lost explicit relationship");
        method.Destroy();
        Check(method.Declaration is null && method.Implementation is null
            && declaration.DeclaredSymbol is null && implementation.ImplementedSymbol is null,
            "method destruction leaked syntax backlinks");
        compilation.Destroy();
        foreign.Destroy();

        Compilation syntaxCompilation = new();
        SourceFile syntaxSource = new(syntaxCompilation, "retire.ly",
            System.Text.Encoding.UTF8.GetBytes(
                "struct P { func f(); }\nfunc P.f() {}"));
        Check(syntaxSource.Parse(false) is null && syntaxCompilation.Check(false) is null,
            "syntax retirement fixture failed checking");
        SyntaxNode syntaxBody = syntaxSource.SyntaxRoot()!.ChildAt(1);
        SemanticSymbol retired = syntaxBody.ImplementedSymbol!;
        syntaxBody.Destroy();
        Check(retired.Destroyed && retired.Implementation is null
            && syntaxBody.ImplementedSymbol is null,
            "syntax destruction left an implementation relationship");
        syntaxCompilation.Destroy();
    }

    // Nullable handles are separate canonical keys with a non-owning class
    // dependency. These checks deliberately exercise real retirement operations.
    private static void NullableIdentities()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        SemanticScope scope = new(analysis, null);
        SemanticSymbol owner = new(scope, null, "Owner", SymbolKind.Class, null, false);
        SemanticType handle = analysis.ClassType(owner);
        SemanticType nullable = analysis.NullableType(handle);
        Check(nullable != handle && nullable == analysis.NullableType(handle),
            "nullable identity merged with its element or failed interning");
        Check(nullable.Element == handle && nullable.Declaration is null
            && nullable.Class == owner && nullable.ArrayDepth == 0,
            "nullable wrapper changed nominal identity or array depth");
        Check(SemanticTypes.Name(nullable) == "Owner?", "nullable name lost its suffix");
        SemanticType array = analysis.ArrayType(nullable);
        Check(array.ArrayDepth == 1 && array.Class == owner
            && SemanticTypes.Name(array) == "[Owner?]", "nullable array was flattened");
        Check(SemanticTypes.HasDefaultValue(nullable)
            && !SemanticTypes.HasDefaultValue(handle), "nullable default changed nonnullable policy");
        Check(!SemanticTypes.HasOwnedContents(nullable)
            && !SemanticTypes.HasContainedBorrows(nullable)
            && SemanticTypes.HasFiniteLayout(nullable), "ordinary nullable handle classified incorrectly");
        Refuses(() => handle.RemoveNullable(nullable), "live nullable key was editable");
        Refuses(() => new SemanticType(analysis, SemanticTypeKind.Nullable,
            ScalarKind.Unit, handle, null), "duplicate nullable key admitted");
        Check(analysis.NullableType(handle) == nullable && nullable.Element == handle,
            "duplicate refusal damaged existing nullable dependency");
        SemanticType integer = analysis.ScalarType(ScalarKind.I32);
        SemanticSymbol structure = new(scope, null, "Record", SymbolKind.Struct, null, false);
        SemanticType record = analysis.StructType(structure);
        foreach (SemanticType invalid in new[] { integer, record, array, nullable })
        {
            Refuses(() => analysis.NullableType(invalid), "non-class nullable factory payload admitted");
            Refuses(() => new SemanticType(analysis, SemanticTypeKind.Nullable,
                ScalarKind.Unit, invalid, null), "non-class nullable constructor payload admitted");
        }
        Compilation foreignCompilation = new();
        SemanticAnalysis foreign = new(foreignCompilation);
        Refuses(() => foreign.NullableType(handle), "foreign nullable dependency admitted");
        foreignCompilation.Destroy();
        // Retiring a wrapper cascades to its users, never to the base class.
        nullable.Destroy();
        Check(nullable.IsUnlinked() && array.Destroyed && !handle.Destroyed && !owner.Destroyed,
            "nullable retirement destroyed its class or left dependent arrays alive");
        SemanticType replacement = analysis.NullableType(handle);
        Check(replacement != nullable && replacement.Element == handle,
            "retired nullable identity was reused");
        SemanticType tuple = analysis.TupleType(new[] { replacement, replacement });
        SemanticSymbol local = new(scope, null, "optional", SymbolKind.Local, replacement, true);
        handle.Destroy();
        Check(replacement.Destroyed && replacement.IsUnlinked() && tuple.Destroyed && local.Destroyed,
            "base retirement left nullable dependents attached");
        Refuses(() => analysis.NullableType(handle), "retired base handle admitted");
        compilation.Destroy();
    }

    private static void Main()
    {
        Compilation compilation = new();
        SemanticAnalysis analysis = new(compilation);
        Compilation otherCompilation = new();
        SemanticAnalysis other = new(otherCompilation);
        SemanticScope scope = new(analysis, null);
        SemanticType integer = analysis.ScalarType(ScalarKind.I32);
        Check(ReferenceEquals(integer, analysis.ScalarType(ScalarKind.I32)), "scalar was not reused");
        Check(integer != analysis.ScalarType(ScalarKind.U32), "different scalars merged");
        Check(integer != other.ScalarType(ScalarKind.I32), "analysis identities merged");
        Refuses(() => analysis.ArrayType(other.ScalarType(ScalarKind.I32)), "foreign element accepted");

        SemanticType array = analysis.ArrayType(integer);
        Check(array.Element == integer && SemanticTypes.ElementOf(array) == integer,
            "array did not retain canonical element");
        Check(array == SemanticTypes.ArrayOf(integer), "array was not reused");
        Refuses(() => integer.RemoveArray(array), "published array key was editable");
        Check(array.Element == integer && analysis.ArrayType(integer) == array, "refusal changed array key");
        Refuses(() => new SemanticType(analysis, SemanticTypeKind.Array, ScalarKind.Unit,
            integer, null), "duplicate direct construction accepted");
        Check(analysis.ArrayType(integer) == array, "duplicate refusal corrupted lookup");

        SemanticType boolean = analysis.ScalarType(ScalarKind.Bool);
        SemanticType tuple = analysis.TupleType(new[] { integer, boolean, integer });
        Check(tuple == analysis.TupleType(new[] { integer, boolean, integer }), "tuple was not reused");
        Check(tuple != analysis.TupleType(new[] { boolean, integer, integer }), "tuple order ignored");
        Check(tuple.ComponentCount() == 3 && tuple.ComponentAt(0) == tuple.ComponentAt(2),
            "repeated tuple component was lost");
        Check(SemanticTypes.Name(tuple) == "(i32, bool, i32)", "tuple name lost ordered components");
        SemanticType single = analysis.TupleType(new[] { integer });
        Check(single != integer && SemanticTypes.Name(single) == "(i32,)", "single tuple merged with scalar");
        Check(SemanticTypes.Name(analysis.ArrayType(tuple)) == "[(i32, bool, i32)]",
            "array tuple name flattened components");
        Refuses(() => analysis.TupleType(Array.Empty<SemanticType>()), "empty tuple key accepted");
        Refuses(() => analysis.TupleType(new[] { integer, other.ScalarType(ScalarKind.Bool) }),
            "foreign tuple component accepted");
        Refuses(() => new SemanticTypeComponent(tuple, boolean), "published tuple key was editable");
        Refuses(() => new SemanticType(analysis, new[] { integer, boolean, integer }),
            "duplicate tuple construction accepted");
        Check(tuple == analysis.TupleType(new[] { integer, boolean, integer }) && tuple.ComponentCount() == 3,
            "tuple refusal corrupted original key");

        Check(!SemanticTypes.HasOwnedContents(integer), "scalar classified as owner");
        Check(!SemanticTypes.HasOwnedContents(tuple), "plain tuple classified as owner");
        Check(SemanticTypes.HasOwnedContents(array), "array ownership lost");
        SemanticType owningTuple = analysis.TupleType(new[] { tuple, array });
        Check(SemanticTypes.HasOwnedContents(analysis.TupleType(new[] { owningTuple, tuple })),
            "nested tuple ownership lost");

        // Enough independent nominal keys to force both bucket collisions and
        // resizing. Same spelling in distinct scopes must not merge identities.
        SemanticScope secondScope = new(analysis, null);
        SemanticSymbol first = new(scope, null, "Same", SymbolKind.Class, null, false);
        SemanticSymbol second = new(secondScope, null, "Same", SymbolKind.Class, null, false);
        SemanticType nominal = analysis.ClassType(first);
        Check(nominal != analysis.ClassType(second), "nominals merged by spelling");
        Check(nominal == analysis.ClassType(first), "nominal was not reused");
        List<(SemanticSymbol Declaration, SemanticType Type)> saved = new();
        for (int index = 0; index < 100; index += 1)
        {
            SemanticSymbol declaration = new(scope, null, "C" + index,
                SymbolKind.Class, null, false);
            saved.Add((declaration, analysis.ClassType(declaration)));
        }
        List<SemanticType>[] buckets = Buckets(analysis);
        Check(buckets.Length > 16, "table did not grow");
        bool collision = false;
        foreach (List<SemanticType>? entries in buckets)
        {
            if (entries is not null && entries.Count > 1)
            {
                collision = true;
            }
        }
        Check(collision, "fixture did not exercise a bucket collision");
        foreach ((SemanticSymbol declaration, SemanticType type) in saved)
        {
            Check(analysis.ClassType(declaration) == type, "rehash changed canonical identity");
        }

        SemanticSymbol local = new(scope, null, "local", SymbolKind.Local, integer, true);
        SemanticSymbol functionSymbol = new(scope, null, "f", SymbolKind.Function, integer, false);
        IrProgram program = new(analysis);
        IrFunction function = new(program, functionSymbol);
        IrNode node = new(function, IrKind.Local, result: IrResult.Address, type: integer);
        Refuses(() => local.SetType(other.ScalarType(ScalarKind.I32)), "foreign symbol type accepted");
        Check(local.Type == integer && !program.Destroyed, "refused symbol replacement mutated state");
        Refuses(() => node.SetType(other.ScalarType(ScalarKind.I32)), "foreign IR type accepted");
        Check(node.Type == integer && !node.Destroyed, "refused IR replacement mutated state");
        local.SetType(array);
        Check(program.Destroyed && node.Destroyed && node.Type is null,
            "successful semantic edit did not retire IR");
        Check(local.Type == array, "successful type replacement failed");

        SemanticType nested = analysis.ArrayType(analysis.ArrayType(nominal));
        SemanticSymbol dependent = new(scope, null, "dependent", SymbolKind.Local, nested, true);
        SemanticType nominalTuple = analysis.TupleType(new[] { nominal, integer, nominal });
        SemanticType tupleArray = analysis.ArrayType(nominalTuple);
        SemanticType nestedTuple = analysis.TupleType(new[] { tupleArray, tuple });
        Check(!SemanticTypes.HasOwnedContents(nominalTuple), "ordinary handles classified as owners");
        first.Destroy();
        bool rejectedRetiredType = false;
        try
        {
            SemanticTypes.HasOwnedContents(nominalTuple);
        }
        catch (ArgumentException)
        {
            rejectedRetiredType = true;
        }
        Check(rejectedRetiredType, "retired type accepted for ownership classification");
        Check(nominalTuple.Destroyed && tupleArray.Destroyed && nestedTuple.Destroyed,
            "nominal retirement failed through tuple/array dependencies");
        Check(nominalTuple.ComponentCount() == 0 && nestedTuple.ComponentCount() == 0,
            "tuple retirement left occurrence links");
        Check(!tuple.Destroyed && !integer.Destroyed, "tuple teardown destroyed unrelated components");
        Refuses(() => analysis.TupleType(new[] { nominal }), "retired tuple component accepted");
        Check(nominal.Destroyed && nested.Destroyed && dependent.Destroyed,
            "nominal retirement failed to propagate through arrays");
        Check(first.NominalType is null && nominal.Declaration is null && nested.Element is null,
            "retirement left reverse or component links");
        Check(!local.Destroyed && local.Type == array, "unrelated type user was destroyed");
        Refuses(() => analysis.ArrayType(nominal), "retired component accepted");

        compilation.Destroy();
        Check(analysis.Destroyed && integer.Destroyed && array.Destroyed && integer.Analysis is null,
            "analysis teardown left canonical types live");
        Check(local.Destroyed && local.Type is null, "analysis teardown left symbol membership");
        foreach (List<SemanticType>? entries in Buckets(analysis))
        {
            Check(entries is null || entries.Count == 0, "analysis teardown left bucket membership");
        }
        otherCompilation.Destroy();

        // Check actual source facts, not only manually assembled model objects.
        Compilation checkedCompilation = new();
        SourceFile source = new(checkedCompilation, "types.ly",
            System.Text.Encoding.UTF8.GetBytes("func value()->i32 { return 7 }"));
        Check(source.Parse(false) is null && checkedCompilation.Check(false) is null,
            "fact fixture failed checking");
        SyntaxNode root = source.SyntaxRoot()!;
        SemanticType checkedInteger = root.ChildAt(0).DeclaredSymbol!.Type!;
        List<SemanticFact> typedFacts = new();
        Stack<SyntaxNode> pending = new();
        pending.Push(root);
        while (pending.Count != 0)
        {
            SyntaxNode syntax = pending.Pop();
            if (syntax.Fact is SemanticFact fact && fact.Type == checkedInteger)
            {
                typedFacts.Add(fact);
            }
            for (int index = 0; index < syntax.ChildCount(); index += 1)
            {
                pending.Push(syntax.ChildAt(index));
            }
        }
        Check(typedFacts.Count != 0, "fact fixture contained no typed facts");
        checkedInteger.Destroy();
        foreach (SemanticFact fact in typedFacts)
        {
            Check(fact.Destroyed && fact.TypeLink is null && fact.Node is null
                && fact.Symbol is null && fact.Analysis is null,
                "type retirement left a checked fact linked");
        }
        checkedCompilation.Destroy();

        // Use checked declaration policy, not a test-only counted flag.
        Compilation countedCompilation = new();
        SourceFile countedSource = new(countedCompilation, "counted.ly",
            System.Text.Encoding.UTF8.GetBytes("value class Box { func Box(self) {} }"));
        Check(countedSource.Parse(false) is null && countedCompilation.Check(false) is null,
            "counted ownership fixture failed checking");
        SemanticSymbol box = countedSource.SyntaxRoot()!.ChildAt(0).DeclaredSymbol!;
        SemanticType boxType = box.Scope!.Analysis!.ClassType(box);
        Check(SemanticTypes.HasOwnedContents(boxType), "counted handle ownership lost");
        Check(SemanticTypes.HasOwnedContents(boxType.Analysis!.TupleType(new[] { boxType })),
            "tuple counted ownership lost");
        SemanticType optionalBox = boxType.Analysis!.NullableType(boxType);
        Check(SemanticTypes.HasOwnedContents(optionalBox), "nullable counted handle lost ownership");
        Check(SemanticTypes.HasDefaultValue(optionalBox), "nullable counted handle lost null default");
        Check(SemanticTypes.HasOwnedContents(boxType.Analysis.TupleType(new[] { optionalBox })),
            "nullable counted tuple lost ownership");
        countedCompilation.Destroy();
        Check(optionalBox.Destroyed && optionalBox.IsUnlinked(), "counted nullable teardown incomplete");
        NullableIdentities();
        FiniteStructLayouts();
        StructIdentities();
        ContainedBorrowTypes();
        NominalMemberScopes();
        FieldInitializerIrContracts();
        SeparateMethodRelationships();
        TupleIrContracts();
        TupleOwnershipIrContracts();
        StructIrContracts();
        StructOwnershipIrContracts();
        BorrowedStructIrContracts();
        Console.WriteLine($"{Checks} canonical model checks passed");
    }
}
