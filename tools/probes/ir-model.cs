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

    public static void Main()
    {
        Compilation compilation = new();
        SourceFile source = new(compilation, "probe.ly", Array.Empty<byte>());
        SyntaxNode origin = new(source, SyntaxKind.Function, -1, 0);
        SemanticAnalysis analysis = new(compilation);
        SemanticScope scope = new(analysis, null);
        SemanticSymbol declaration = new(scope, null, "f", SymbolKind.Function,
            SemanticType.Unit, false);
        SemanticSymbol second = new(scope, null, "g", SymbolKind.Function,
            SemanticType.Unit, false);
        SemanticSymbol nominal = new(scope, null, "Thing", SymbolKind.Class, null, false);
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
            type: SemanticType.I32);
        Refuses(() => body.AddChild(foreign), "cross-function tree accepted");
        Refuses(() => loop.AddTransfer(foreign), "nontransfer target accepted");

        IrNode value = new(function, IrKind.Constant, result: IrResult.Value,
            type: SemanticType.I32, origin: origin, literal: new IrLiteral(new BigInteger(7)));
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
            type: new SemanticType(ScalarKind.Unit, 0, nominal));
        Compilation separateCompilation = new();
        SemanticAnalysis separateAnalysis = new(separateCompilation);
        SemanticScope separateScope = new(separateAnalysis, null);
        SemanticSymbol separateClass = new(separateScope, null, "Other", SymbolKind.Class, null, false);
        Refuses(() => typed.SetType(new SemanticType(ScalarKind.Unit, 0, separateClass)),
            "cross-analysis nominal type accepted");
        Check(typed.Type?.Class == nominal && analysis.Ir == program,
            "refused type change mutated valid IR");
        typed.SetType(SemanticType.I32);
        typed.SetType(new SemanticType(ScalarKind.Unit, 0, nominal));
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
