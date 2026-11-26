using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Lyric;

// Development diagnostic only: exercises actual relationships and retirement.
// The external Python language grader remains the retained execution contract.
internal static class GenericModelProbe
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

    private static string Tree(SourceFile source)
    {
        using StringWriter text = new();
        source.WriteSyntax(text);
        return text.ToString();
    }

    private static void Main()
    {
        foreach (bool selectedBody in new[] { false, true })
        {
            foreach (string retirement in new[] { "specialization", "type", "template", "node", "scope", "analysis", "source" })
            {
                Compilation compilation = new();
                string functionSource = selectedBody
                    ? "func identity<T,U>(value:T)->T { matchtype<V> value { V => { let copy:V=value; return copy }; bool => { missing } } }"
                    : "func identity<T,U>(value:T)->T { return value }";
                SourceFile source = new(compilation, "generic-model.ly", Encoding.UTF8.GetBytes(functionSource + "\n" + """
                    func main() {
                        assert(identity<i32,i32>(7)==7)
                        assert(identity<i32,i32>(9)==9)
                    }
                    """));
                Check(source.Lex() is null, "source lexing failed");
                Check(source.Parse(false) is null, "source parsing failed");
                string original = Tree(source);
                SyntaxNode declaration = source.SyntaxRoot()!.ChildAt(0);
                Check(compilation.Check(true) is null, "generic checking failed");
                SemanticSymbol template = declaration.DeclaredSymbol!;
                SemanticAnalysis analysis = template.Scope!.Analysis!;
                SemanticType integer = analysis.ScalarType(ScalarKind.I32);
                GenericSpecialization specialization = analysis.FindSpecialization(template,
                    new[] { integer, integer })!;
                Check(specialization is not null, "specialization absent");
                Check(specialization!.BindingCount() == 2, "repeated positions collapsed");
                Check(specialization.BindingAt(0) == integer && specialization.BindingAt(1) == integer,
                    "canonical binding identity lost");
                Check(new List<GenericSpecialization>(analysis.ConcreteFunctions()).Count == 1,
                    "identical calls duplicated specialization");
                SemanticSymbol function = specialization.Function!;
                // Diagnostic-only reflection observes the private ownership collection;
                // all mutations still use the actual relationship operations.
                FieldInfo scopeField = typeof(GenericSpecialization).GetField("Scopes",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                List<SemanticScope> ownedScopes = new((List<SemanticScope>)scopeField.GetValue(specialization)!);
                Check(ownedScopes.Count >= 2, "function/block scopes lack specialization ownership");
                foreach (SemanticScope scope in ownedScopes)
                {
                    Check(scope.Specialization == specialization, "scope owner backlink absent");
                }
                Check(function != template && function.Specialization == specialization,
                    "concrete callable identity lost");
                Check(template.Scope.Lookup(template.Name) == template, "concrete callable shadows template");
                Check(function.Declaration != declaration && function.Declaration!.Source == source,
                    "derived source provenance lost");
                Check(Tree(source) == original, "derived syntax changed parsed tree");
                List<SyntaxNode> copied = new();
                Stack<SyntaxNode> pending = new();
                pending.Push(specialization.Root());
                while (pending.Count != 0)
                {
                    SyntaxNode node = pending.Pop();
                    copied.Add(node);
                    Check(node.Specialization == specialization, "derived node lacks owner backlink");
                    if (node.Kind == SyntaxKind.MatchType)
                    {
                        Check(node.Fact!.SelectedArmIndex == 2, "chosen arm position lost");
                        Check(node.ChildAt(3).ChildAt(1).Fact is null,
                            "unselected body acquired semantic facts");
                    }
                    for (int index = 0; index < node.ChildCount(); index += 1)
                    {
                        pending.Push(node.ChildAt(index));
                    }
                }
                Check(compilation.LowerIr() is null, "specialized IR lowering failed");
                IrProgram ir = analysis.Ir!;
                Refuses(() => _ = new GenericSpecialization(template, new[] { integer, integer }),
                    "duplicate specialization accepted");
                Refuses(() => _ = new GenericBinding(specialization, integer),
                    "published key mutation accepted");
                Compilation otherCompilation = new();
                SemanticAnalysis other = new(otherCompilation);
                Refuses(() => _ = new GenericSpecialization(template,
                    new[] { integer, other.ScalarType(ScalarKind.I32) }), "foreign binding accepted");
                // Invalid admissions must not retire an existing unpublished
                // specialization or the unrelated executable IR in its analysis.
                GenericSpecialization draft = new(template, new[] { integer, analysis.ScalarType(ScalarKind.Bool) });
                SyntaxNode draftRoot = draft.Root();
                SemanticScope foreignScope = new(other, null);
                Refuses(() => _ = new SemanticSymbol(foreignScope, draftRoot, template.Name,
                    SymbolKind.Function, integer, false, specialization: draft),
                    "foreign callable scope accepted");
                Refuses(() => _ = new SemanticSymbol(template.Scope!, draftRoot, template.Name,
                    SymbolKind.Function, other.ScalarType(ScalarKind.I32), false, specialization: draft),
                    "foreign callable type accepted");
                Refuses(() => _ = new SemanticSymbol(template.Scope!, declaration, template.Name,
                    SymbolKind.Function, integer, false, specialization: draft),
                    "original callable declaration accepted");
                SourceFile foreignSource = new(otherCompilation, "foreign.ly",
                    Encoding.UTF8.GetBytes("func foreign() {}"));
                Check(foreignSource.Lex() is null && foreignSource.Parse(false) is null,
                    "foreign syntax setup failed");
                SyntaxNode foreignRoot = foreignSource.SyntaxRoot()!;
                string foreignTree = Tree(foreignSource);
                Refuses(() => draft.AddSyntaxNode(foreignRoot, foreignSource),
                    "foreign parsed syntax adopted");
                Refuses(() => draft.AddSyntaxNode(declaration, source),
                    "same-compilation parsed syntax adopted");
                Check(!draft.Destroyed && draft.Function is null && draft.Root() == draftRoot
                    && draftRoot.Specialization == draft && draftRoot.DeclaredSymbol is null,
                    "refused callable changed draft ownership");
                Check(!foreignRoot.Destroyed && foreignRoot.Specialization is null
                    && Tree(foreignSource) == foreignTree && Tree(source) == original,
                    "refused syntax adoption changed original trees");
                Check(analysis.Ir == ir && !ir.Destroyed, "refusal destroyed valid IR");
                draft.Destroy();
                // Successful draft retirement intentionally invalidates IR.
                Check(compilation.LowerIr() is null, "IR restoration after draft retirement failed");
                ir = analysis.Ir!;
                otherCompilation.Destroy();
                Check(analysis.Ir == ir && !ir.Destroyed, "foreign teardown destroyed valid IR");
                Check(analysis.FindSpecialization(template, new[] { integer, integer }) == specialization,
                    "refusal changed canonical key");
                switch (retirement)
                {
                    case "specialization":
                        specialization.Destroy();
                        break;
                    case "type":
                        integer.Destroy();
                        Check(integer.IsUnlinked(), "type retains generic dependencies");
                        break;
                    case "template":
                        template.Destroy();
                        break;
                    case "node":
                        specialization.Root().Destroy();
                        break;
                    case "scope":
                        ownedScopes[ownedScopes.Count - 1].Destroy();
                        break;
                    case "analysis":
                        analysis.Destroy();
                        break;
                    case "source":
                        Check(source.Lex() is null, "source relex failed");
                        break;
                }
                Check(specialization.Destroyed && specialization.Analysis is null
                    && specialization.Template is null && specialization.Function is null,
                    "specialization retains owner memberships");
                Check(function.Destroyed && function.Scope is null && function.Specialization is null,
                    "concrete callable survives retirement");
                foreach (SyntaxNode node in copied)
                {
                    Check(node.Destroyed && node.Source is null && node.Specialization is null
                        && node.Parent is null && node.ChildCount() == 0 && node.Fact is null
                        && node.DeclaredSymbol is null, "derived node retains relationships");
                }
                foreach (SemanticScope scope in ownedScopes)
                {
                    Check(scope.Destroyed && scope.Analysis is null && scope.Specialization is null
                        && scope.Parent is null, "derived scope retains memberships");
                }
                Check(ir.Destroyed, "dependent IR survived retirement");
                if (retirement != "source")
                {
                    Check(Tree(source) == original, "retirement changed original parsed syntax");
                }
                specialization.Destroy();
                compilation.Destroy();
            }
        }
        Console.WriteLine($"{Checks} generic model checks passed");
    }
}
