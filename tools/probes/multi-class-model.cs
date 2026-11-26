using System;
using System.Collections.Generic;
using System.Text;
using Lyric;

// Development model diagnostic, not the public language execution grader.
// All mutations use the real model operations; no mocked ownership methods.
internal static class MultiClassModelProbe
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
        catch (ArgumentException)
        {
            Checks += 1;
            return;
        }
        catch (InvalidOperationException)
        {
            Checks += 1;
            return;
        }
        throw new Exception(reason);
    }

    private static void Main()
    {
        foreach (bool destroyClass in new[] { false, true })
        {
            Compilation compilation = new();
            SemanticAnalysis analysis = new(compilation);
            SemanticScope global = new(analysis, null);
            SemanticSymbol nominal = new(global, null, "Layer", SymbolKind.Class, null, false);
            SemanticScope members = new(nominal);
            SemanticSymbol label = new(members, null, "items", SymbolKind.MemberNamespace, null, false);
            SemanticScope contributions = new(label);
            SemanticType integer = analysis.ScalarType(ScalarKind.I32);
            SemanticSymbol field = new(contributions, null, "count", SymbolKind.Field, integer, true);
            Check(label.Scope == members && members.LookupMember("items") == label,
                "label lacks reciprocal membership");
            Check(label.MemberScope == contributions && contributions.MemberOwner == label,
                "namespace scope lacks reciprocal ownership");
            Check(contributions.Class == nominal, "physical class navigation lost");
            Check(contributions.Lookup("count") is null, "qualified field escaped into lexical lookup");
            List<SemanticSymbol> fields = new(members.Fields());
            Check(fields.Count == 1 && fields[0] == field, "namespace field omitted from physical inventory");
            Refuses(() => _ = new SemanticSymbol(members, null, "items",
                SymbolKind.MemberNamespace, null, false), "duplicate label admitted");
            Refuses(() => _ = new SemanticSymbol(members, null, "items",
                SymbolKind.Field, integer, true), "ordinary member replaced label");
            Refuses(() => _ = new SemanticSymbol(global, null, "invalid",
                SymbolKind.MemberNamespace, null, false), "global namespace admitted");
            Refuses(() => _ = new SemanticSymbol(contributions, null, "nested",
                SymbolKind.MemberNamespace, null, false), "nested contribution namespace admitted");
            Check(!label.Destroyed && !contributions.Destroyed && label.MemberScope == contributions,
                "refusal damaged existing namespace");
            Check(members.LookupMember("items") == label && contributions.LookupMember("count") == field,
                "refusal changed existing members");
            if (destroyClass)
            {
                nominal.Destroy();
            }
            else
            {
                label.Destroy();
            }
            Check(label.Destroyed && contributions.Destroyed && field.Destroyed,
                "owner destruction omitted namespace contents");
            Check(label.Scope is null && label.MemberScope is null
                && contributions.MemberOwner is null && contributions.Analysis is null
                && field.Scope is null, "destroyed namespace retained parent links");
            if (!destroyClass)
            {
                Check(!nominal.Destroyed && members.LookupMember("items") is null,
                    "namespace retirement destroyed its class or left its name occupied");
                Check(new List<SemanticSymbol>(members.Fields()).Count == 0,
                    "retired field remained in physical inventory");
                SemanticSymbol replacement = new(members, null, "items",
                    SymbolKind.MemberNamespace, null, false);
                Check(members.LookupMember("items") == replacement, "retired name cannot be reused");
            }
            compilation.Destroy();
            Check(analysis.Destroyed && global.Destroyed && nominal.Destroyed,
                "compilation retirement left model owners live");
        }
        foreach (string retirement in new[] { "instance", "endpoint", "class", "template", "analysis", "source", "syntax", "contribution", "scope" })
        {
            Compilation compilation = new();
            SourceFile source = new(compilation, "multi-class-model.ly", Encoding.UTF8.GetBytes(
                "interface Pair<P,C> {\nclass P {}\nclass C {}\n}"));
            Check(source.Lex() is null && source.Parse(false) is null, "interface parse failed");
            SemanticAnalysis analysis = new(compilation);
            SemanticScope global = new(analysis, null);
            SemanticSymbol template = new(global, source.SyntaxRoot()!.ChildAt(0),
                "Pair", SymbolKind.Interface, null, false);
            SemanticSymbol nominal = new(global, null, "Graph", SymbolKind.Class, null, false);
            SemanticScope members = new(nominal);
            SemanticSymbol[] classes = { nominal, nominal };
            string?[] labels = { "left", "right" };
            MultiClassInstantiation instance = new(template, classes, labels);
            SyntaxNode original = source.SyntaxRoot()!;
            int originalChildren = original.ChildCount();
            SyntaxNode copy = new(source, SyntaxKind.Block, -1, 0, multiClass: instance);
            SyntaxNode copiedChild = new(source, SyntaxKind.Unit, -1, 0, multiClass: instance);
            copy.AddChild(copiedChild);
            Check(analysis.Compilation == compilation && !analysis.Destroyed,
                "derived construction cleared current analysis");
            Check(original.ChildCount() == originalChildren && copy.Parent is null,
                "copy changed parsed tree");
            Refuses(() => instance.AddSyntaxNode(original, source), "adopted original syntax");
            Refuses(() => original.AddChild(copy), "derived tree attached to original");
            SemanticSymbol contributedLabel = new(members, null, "left",
                SymbolKind.MemberNamespace, null, false, multiClass: instance);
            SemanticScope contributedMembers = new(contributedLabel);
            SemanticSymbol method = new(contributedMembers, copiedChild, "value",
                SymbolKind.Function, analysis.ScalarType(ScalarKind.I32), false, multiClass: instance);
            SemanticScope methodScope = new(method);
            SemanticScope nestedScope = new(analysis, methodScope);
            SemanticScope annotations = new(instance);
            Check(annotations.Analysis == analysis && annotations.MultiClass == instance
                && annotations.Parent == global, "annotation scope owners/lexical parent absent");
            Check(contributedLabel.MultiClass == instance && method.MultiClass == instance
                && contributedMembers.MultiClass == instance && methodScope.MultiClass == instance
                && nestedScope.MultiClass == instance, "contribution/checking scope ownership absent");
            instance.SealSyntax();
            Refuses(() => copy.RemoveChild(copiedChild), "sealed tree was edited");
            Refuses(() => _ = new SyntaxNode(source, SyntaxKind.Unit, -1, 0,
                multiClass: instance), "sealed instance admitted new syntax");
            Check(copiedChild.Parent == copy && copy.ChildCount() == 1,
                "refused edit changed tree");
            ParticipantBinding left = instance.ParticipantAt(0);
            ParticipantBinding right = instance.ParticipantAt(1);
            SemanticType graphType = analysis.ClassType(nominal);
            method.SetType(graphType);
            left.AddMember(method);
            right.AddViewSymbol(method);
            SemanticFact viewFact = new(analysis, copiedChild, graphType, method, null, true);
            right.AddViewFact(viewFact);
            Check(method.ContributingParticipant == left && method.ParticipantView == right
                && viewFact.ParticipantView == right && left.FindMember("value") == method,
                "contribution role and result view collapsed");
            Refuses(() => left.AddViewSymbol(method), "second symbol view admitted");
            Refuses(() => left.AddViewFact(viewFact), "second fact view admitted");
            Check(method.ParticipantView == right && viewFact.ParticipantView == right,
                "view refusal replaced existing relationship");
            method.SetType(graphType);
            Check(method.ParticipantView == right, "unchanged type erased view");
            SemanticType nullableGraph = analysis.NullableType(graphType);
            method.SetType(nullableGraph);
            Check(method.ParticipantView == right, "nullable wrapping erased view");
            method.SetType(graphType);
            Check(method.ParticipantView == right, "nullable unwrapping erased view");
            SemanticSymbol otherClass = new(global, null, "Other", SymbolKind.Class, null, false);
            SemanticType otherType = analysis.ClassType(otherClass);
            SemanticSymbol viewCandidate = new(annotations, null, "candidate",
                SymbolKind.Local, nullableGraph, false);
            right.AddViewSymbol(viewCandidate);
            Check(viewCandidate.ParticipantView == right, "nullable symbol view refused");
            right.RemoveViewSymbol(viewCandidate);
            foreach (SemanticType invalid in new[] { otherType, analysis.NullableType(otherType),
                SemanticTypes.ArrayOf(graphType), analysis.TupleType(new[] { graphType }) })
            {
                viewCandidate.SetType(invalid);
                Refuses(() => right.AddViewSymbol(viewCandidate), "wrong class/container view admitted");
                Check(viewCandidate.ParticipantView is null, "refusal attached candidate view");
            }
            viewCandidate.SetType(nullableGraph);
            right.AddViewSymbol(viewCandidate);
            SemanticFact nullableFact = new(analysis, copy, nullableGraph, null);
            right.AddViewFact(nullableFact);
            Check(nullableFact.ParticipantView == right, "nullable fact view refused");
            method.SetType(analysis.ScalarType(ScalarKind.I32));
            Check(method.ParticipantView is null && method.ContributingParticipant == left,
                "type change failed to clear only result view");
            Refuses(() => right.AddViewSymbol(method), "scalar accepted class participant view");
            method.SetType(graphType);
            right.AddViewSymbol(method);
            right.RemoveViewFact(viewFact);
            Check(viewFact.ParticipantView is null, "fact detach left backlink");
            left.AddViewFact(viewFact);
            Check(instance.Analysis == analysis && instance.Template == template,
                "instance owners absent");
            Check(analysis.FindMultiClassInstantiation(template, classes, labels) == instance,
                "full key lookup failed");
            Check(left != right && left.Position == 0 && right.Position == 1,
                "equal class roles collapsed");
            Check(left.ConcreteClass == nominal && right.ConcreteClass == nominal
                && left.Instantiation == instance && right.Instantiation == instance,
                "endpoint backlinks absent");
            Check(nominal.FindParticipantLabel("left") == left
                && nominal.FindParticipantLabel("right") == right, "class dependents absent");
            Refuses(() => _ = new MultiClassInstantiation(template, classes, labels),
                "duplicate full key accepted");
            Refuses(() => _ = new MultiClassInstantiation(template, classes,
                new string?[] { "fresh", "right" }), "late endpoint collision accepted");
            Check(nominal.FindParticipantLabel("fresh") is null && !instance.Destroyed,
                "failed construction reserved a name or damaged existing instance");
            Refuses(() => _ = new MultiClassInstantiation(template, classes,
                new string?[] { "same", "same" }), "same-class duplicate label accepted");
            Check(nominal.FindParticipantLabel("same") is null, "internal collision left an endpoint");
            MultiClassInstantiation independent = new(template, classes,
                new string?[] { "selected", "selection" });
            SyntaxNode otherCopy = new(source, SyntaxKind.Block, -1, 0, multiClass: independent);
            Refuses(() => otherCopy.AddChild(copy), "cross-instance tree edge admitted");
            Check(independent != instance && !independent.Matches(template, classes, labels),
                "independent key collapsed");
            independent.Destroy();
            Check(!instance.Destroyed && !nominal.Destroyed, "independent retirement cascaded to peers");
            switch (retirement)
            {
                case "instance": instance.Destroy(); break;
                case "endpoint": left.Destroy(); break;
                case "class": nominal.Destroy(); break;
                case "template": template.Destroy(); break;
                case "analysis": analysis.Destroy(); break;
                case "source": source.Destroy(); break;
                case "syntax": copiedChild.Destroy(); break;
                case "contribution": method.Destroy(); break;
                case "scope": nestedScope.Destroy(); break;
            }
            Check(contributedLabel.Destroyed && method.Destroyed && contributedMembers.Destroyed
                && methodScope.Destroyed && nestedScope.Destroyed
                && contributedLabel.MultiClass is null && method.MultiClass is null
                && contributedMembers.MultiClass is null && methodScope.MultiClass is null
                && nestedScope.MultiClass is null && method.Scope is null
                && nestedScope.Analysis is null, "retirement left contribution/scopes attached");
            Check(annotations.Destroyed && annotations.Analysis is null
                && annotations.MultiClass is null && viewCandidate.Destroyed
                && viewCandidate.ParticipantView is null && nullableFact.Destroyed
                && nullableFact.ParticipantView is null, "nullable annotation users survived retirement");
            Check(copy.Destroyed && copiedChild.Destroyed && copy.Source is null
                && copiedChild.Source is null && copy.MultiClass is null
                && copiedChild.MultiClass is null && copiedChild.Parent is null,
                "retirement left derived nodes or backlinks");
            Check(method.ContributingParticipant is null && method.ParticipantView is null
                && viewFact.Destroyed && viewFact.ParticipantView is null
                && left.FindMember("value") is null, "retirement retained participant views");
            Check(instance.Destroyed && left.Destroyed && right.Destroyed,
                "retirement omitted endpoints");
            Check(instance.Analysis is null && instance.Template is null
                && left.Instantiation is null && right.Instantiation is null
                && left.ConcreteClass is null && right.ConcreteClass is null,
                "retirement left owner or dependency links");
            Check(nominal.FindParticipantLabel("left") is null
                && nominal.FindParticipantLabel("right") is null, "class retained dead endpoints");
            if (retirement is "instance" or "endpoint")
            {
                Check(!nominal.Destroyed && !template.Destroyed, "instance owns its class/template");
                MultiClassInstantiation replacement = new(template, classes, labels);
                replacement.Destroy();
                Check(replacement.Destroyed, "retired labels could not be reused");
            }
            compilation.Destroy();
        }
        Console.WriteLine($"{Checks} multi-class model checks passed");
    }
}
