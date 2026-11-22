using System;
namespace Lyric;
internal static class NominalLifecycleProbe
{
    private static int checks;
    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new Exception("Lifecycle assertion " + checks + " failed.");
        }
        checks += 1;
    }
    private static void Main()
    {
        Compilation compilation = new();
        SourceFile source = new(compilation, "probe.ly", Array.Empty<byte>());
        SyntaxNode node = new(source, SyntaxKind.Name, -1, 0);
        SemanticAnalysis analysis = new(compilation);
        SemanticScope global = new(analysis, null);
        SemanticSymbol owner = new(global, null, "Counter", SymbolKind.Class, null, false);
        SemanticScope members = new(owner);
        Require(owner.MemberScope == members && members.Class == owner);
        Require(members.Parent == global && members.Analysis == analysis);
        SemanticSymbol constructor = new(members, null, "Counter", SymbolKind.Function,
            SemanticType.Unit, false);
        SemanticScope parameters = new(constructor);
        SemanticSymbol field = new(members, null, "count", SymbolKind.Field,
            SemanticType.I32, true);
        Require(members.LookupMember("Counter") == constructor);
        Require(members.LookupMember("count") == field);
        Require(parameters.Lookup("Counter") == owner);
        Require(parameters.Lookup("count") is null);
        bool duplicateRefused = false;
        try
        {
            _ = new SemanticSymbol(members, null, "count", SymbolKind.Field,
                SemanticType.I32, true);
        }
        catch (InvalidOperationException)
        {
            duplicateRefused = true;
        }
        Require(duplicateRefused && members.LookupMember("count") == field);
        SemanticType type = new(ScalarKind.Unit, 0, owner);
        Require(type != SemanticType.Unit && !SemanticTypes.IsInteger(type));
        Require(SemanticTypes.ElementOf(SemanticTypes.ArrayOf(type)) == type);
        SemanticSymbol local = new(global, null, "c", SymbolKind.Local, type, false);
        SemanticFact fact = new(analysis, node, type, local);
        Compilation otherCompilation = new();
        SemanticAnalysis otherAnalysis = new(otherCompilation);
        SemanticScope otherGlobal = new(otherAnalysis, null);
        SemanticSymbol otherClass = new(otherGlobal, null, "Counter", SymbolKind.Class, null, false);
        SemanticType otherType = new(ScalarKind.Unit, 0, otherClass);
        Require(type != otherType);
        bool refused = false;
        try
        {
            local.SetType(otherType);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }
        Require(refused && local.Type == type);
        local.SetType(SemanticType.I32);
        Require(local.Type == SemanticType.I32);
        local.SetType(type);
        owner.Destroy();
        Require(owner.Destroyed && owner.Scope is null && owner.MemberScope is null);
        Require(members.Destroyed && members.Class is null && members.Analysis is null);
        Require(local.Destroyed && local.Type is null && local.Scope is null);
        Require(fact.Destroyed && fact.Type.Class is null && fact.Analysis is null);
        Require(node.Fact is null);
        analysis.Destroy();
        compilation.Destroy();
        SemanticScope otherMembers = new(otherClass);
        SemanticSymbol second = new(otherGlobal, null, "second", SymbolKind.Local, otherType, false);
        otherAnalysis.Destroy();
        Require(otherMembers.Destroyed && otherMembers.Class is null);
        Require(otherClass.Destroyed && second.Destroyed && second.Type is null);
        otherCompilation.Destroy();
        Console.WriteLine($"PASS {checks} nominal lifecycle assertions");
    }
}
