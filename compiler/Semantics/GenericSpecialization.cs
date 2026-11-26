using System;
using System.Collections.Generic;

namespace Lyric;

internal sealed partial class GenericSpecialization
{
    internal partial GenericSpecialization(SemanticSymbol template, IReadOnlyList<SemanticType> types)
    {
        SemanticAnalysis? analysis = template.Scope?.Analysis;
        if (template.Destroyed || template.Kind != SymbolKind.Function
            || template.Specialization is not null || template.Declaration is null
            || template.GenericParameters()?.ChildCount() != types.Count
            || analysis is null || analysis.Destroyed)
        {
            throw new InvalidOperationException("Invalid generic template.");
        }
        foreach (SemanticType type in types)
        {
            if (type.Destroyed || type.Analysis != analysis)
            {
                throw new InvalidOperationException("Invalid generic binding type.");
            }
        }
        if (analysis.FindSpecialization(template, types) is not null)
        {
            throw new InvalidOperationException("Duplicate generic specialization.");
        }
        analysis.AddSpecialization(this);
        try
        {
            template.AddSpecialization(this);
            foreach (SemanticType type in types)
            {
                _ = new GenericBinding(this, type);
            }
            SyntaxNode sourceRoot = template.BodySyntax()!;
            SyntaxNode root = new(sourceRoot.Source!, sourceRoot.Kind,
                sourceRoot.TextOffset, sourceRoot.TextLength, this);
            // Explicit frames preserve source order without recursive stack use.
            Stack<(SyntaxNode Original, SyntaxNode Copy, int Next)> pending = new();
            pending.Push((sourceRoot, root, 0));
            while (pending.Count != 0)
            {
                var frame = pending.Pop();
                if (frame.Next == frame.Original.ChildCount())
                {
                    continue;
                }
                pending.Push((frame.Original, frame.Copy, frame.Next + 1));
                SyntaxNode original = frame.Original.ChildAt(frame.Next);
                // Concrete bindings live in the callable's private scope.
                if (frame.Original == sourceRoot && original.Kind == SyntaxKind.TypeParameters)
                {
                    continue;
                }
                SyntaxNode copy = new(original.Source!, original.Kind,
                    original.TextOffset, original.TextLength, this);
                frame.Copy.AddChild(copy);
                pending.Push((original, copy, 0));
            }
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    internal partial bool Matches(SemanticSymbol template, IReadOnlyList<SemanticType> types)
    {
        if (Destroyed || Template != template || Bindings.Count != types.Count)
        {
            return false;
        }
        for (int index = 0; index < types.Count; index += 1)
        {
            if (Bindings[index].Type != types[index])
            {
                return false;
            }
        }
        return true;
    }

    internal partial int BindingCount() => Bindings.Count;
    internal partial SemanticType BindingAt(int index) => Bindings[index].Type!;
    internal partial SyntaxNode Root() => Nodes[0];

    internal partial void AddBinding(GenericBinding binding, SemanticType type)
    {
        if (Destroyed || Analysis is null || Nodes.Count != 0 || Function is not null
            || type.Destroyed || type.Analysis != Analysis
            || binding.Destroyed || binding.Specialization is not null || binding.Type is not null)
        {
            throw new InvalidOperationException("Cannot attach generic binding.");
        }
        Bindings.EnsureCapacity(Bindings.Count + 1);
        type.AddGenericBinding(binding);
        Bindings.Add(binding);
        binding.Specialization = this;
    }

    internal partial void RemoveBinding(GenericBinding binding)
    {
        if (!binding.Destroyed || binding.Specialization != this || !Bindings.Contains(binding))
        {
            throw new InvalidOperationException("Different generic binding owner.");
        }
        binding.Type?.RemoveGenericBinding(binding);
        Bindings.Remove(binding);
        binding.Specialization = null;
    }

    internal partial void AddSyntaxNode(SyntaxNode node, SourceFile source)
    {
        // This is construction admission, not ownership transfer. An existing
        // source member may be original parsed syntax and must never be adopted.
        if (Destroyed || Analysis is null || Analysis.Destroyed || Function is not null
            || source.Destroyed || source.Parent != Analysis.Compilation
            || node.Destroyed || node.Specialization is not null || node.Source is not null
            || node.Parent is not null || node.ChildCount() != 0
            || node.Fact is not null || node.DeclaredSymbol is not null
            || node.ImplementedSymbol is not null)
        {
            throw new InvalidOperationException("Cannot attach derived syntax.");
        }
        Nodes.Add(node);
        node.Specialization = this;
    }

    internal partial void RemoveSyntaxNode(SyntaxNode node)
    {
        if (!node.Destroyed || node.Specialization != this || !Nodes.Remove(node))
        {
            throw new InvalidOperationException("Different derived syntax owner.");
        }
        node.Specialization = null;
    }

    internal partial void AddScope(SemanticScope scope)
    {
        if (Destroyed || scope.Destroyed || scope.Analysis != Analysis || scope.Specialization is not null)
        {
            throw new InvalidOperationException("Cannot attach specialized scope.");
        }
        Scopes.Add(scope);
        scope.Specialization = this;
    }

    internal partial void RemoveScope(SemanticScope scope)
    {
        if (!scope.Destroyed || scope.Specialization != this || !Scopes.Remove(scope))
        {
            throw new InvalidOperationException("Different specialized scope owner.");
        }
        scope.Specialization = null;
    }

    internal partial void AddFunction(SemanticSymbol function)
    {
        if (Destroyed || Function is not null || function.Destroyed
            || function.Kind != SymbolKind.Function || function.Specialization is not null
            || function.Name != Template?.Name
            || (function.Scope is not null && function.Scope != Template.Scope)
            || (function.Declaration is not null && function.Declaration != Root()))
        {
            throw new InvalidOperationException("Cannot attach specialized callable.");
        }
        Function = function;
        function.Specialization = this;
    }

    internal partial void RemoveFunction(SemanticSymbol function)
    {
        if (Function != function || function.Specialization != this || !function.Destroyed)
        {
            throw new InvalidOperationException("Different specialized callable owner.");
        }
        Function = null;
        function.Specialization = null;
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        Analysis?.ClearIr();
        // Mark first: shared-owner cascades may return through the callable,
        // a binding, or a derived node while this teardown is in progress.
        Destroyed = true;
        Function?.Destroy();
        while (Scopes.Count != 0)
        {
            Scopes[Scopes.Count - 1].Destroy();
        }
        while (Nodes.Count != 0)
        {
            Nodes[Nodes.Count - 1].Destroy();
        }
        while (Bindings.Count != 0)
        {
            Bindings[Bindings.Count - 1].Destroy();
        }
        Template?.RemoveSpecialization(this);
        Analysis?.RemoveSpecialization(this);
    }
}

internal sealed partial class GenericBinding
{
    internal partial GenericBinding(GenericSpecialization specialization, SemanticType type)
    {
        specialization.AddBinding(this, type);
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        if (Specialization is GenericSpecialization owner && !owner.Destroyed)
        {
            owner.Destroy();
            return;
        }
        Destroyed = true;
        Specialization?.RemoveBinding(this);
    }
}

internal sealed partial class SemanticAnalysis
{
    internal partial GenericSpecialization? FindSpecialization(SemanticSymbol template,
        IReadOnlyList<SemanticType> types)
    {
        foreach (GenericSpecialization specialization in Specializations)
        {
            if (specialization.Matches(template, types))
            {
                return specialization;
            }
        }
        return null;
    }

    internal partial IEnumerable<GenericSpecialization> ConcreteFunctions()
    {
        return Specializations.AsReadOnly();
    }

    internal partial void AddSpecialization(GenericSpecialization specialization)
    {
        if (Destroyed || specialization.Destroyed || specialization.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach specialization analysis.");
        }
        Specializations.Add(specialization);
        specialization.Analysis = this;
    }

    internal partial void RemoveSpecialization(GenericSpecialization specialization)
    {
        if (!specialization.Destroyed || specialization.Analysis != this
            || !Specializations.Remove(specialization))
        {
            throw new InvalidOperationException("Different specialization analysis.");
        }
        specialization.Analysis = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial SyntaxNode? GenericParameters()
    {
        if (Declaration is null)
        {
            return null;
        }
        for (int index = 0; index < Declaration.ChildCount(); index += 1)
        {
            SyntaxNode child = Declaration.ChildAt(index);
            if (child.Kind == SyntaxKind.TypeParameters)
            {
                return child;
            }
        }
        return null;
    }

    internal partial void AddSpecialization(GenericSpecialization specialization)
    {
        if (Destroyed || Kind != SymbolKind.Function || specialization.Destroyed
            || specialization.Template is not null || specialization.Analysis != Scope?.Analysis)
        {
            throw new InvalidOperationException("Cannot attach specialization template.");
        }
        Specializations.Add(specialization);
        specialization.Template = this;
    }

    internal partial void RemoveSpecialization(GenericSpecialization specialization)
    {
        if (!specialization.Destroyed || specialization.Template != this
            || !Specializations.Remove(specialization))
        {
            throw new InvalidOperationException("Different specialization template.");
        }
        specialization.Template = null;
    }
}

internal sealed partial class SemanticType
{
    internal partial void AddGenericBinding(GenericBinding binding)
    {
        if (Destroyed || binding.Destroyed || binding.Type is not null)
        {
            throw new InvalidOperationException("Cannot attach generic type dependency.");
        }
        GenericBindings.Add(binding);
        binding.Type = this;
    }

    internal partial void RemoveGenericBinding(GenericBinding binding)
    {
        if (!binding.Destroyed || binding.Type != this || !GenericBindings.Remove(binding))
        {
            throw new InvalidOperationException("Different generic type dependency.");
        }
        binding.Type = null;
    }
}
