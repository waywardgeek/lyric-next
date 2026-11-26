using System;

namespace Lyric;

internal sealed partial class Compilation
{
    internal partial void AddAnalysis(SemanticAnalysis analysis)
    {
        if (Destroyed || Analysis is not null || analysis.Destroyed
            || analysis.Compilation is not null)
        {
            throw new InvalidOperationException("Cannot attach semantic analysis.");
        }
        Analysis = analysis;
        analysis.Compilation = this;
    }

    internal partial void RemoveAnalysis(SemanticAnalysis analysis)
    {
        if (Analysis != analysis || analysis.Compilation != this || !analysis.Destroyed)
        {
            throw new InvalidOperationException("Only a destroyed analysis can leave its compilation.");
        }
        Analysis = null;
        analysis.Compilation = null;
    }

    internal partial void ClearAnalysis()
    {
        Analysis?.Destroy();
    }
}

internal sealed partial class SemanticAnalysis
{
    internal partial SemanticAnalysis(Compilation compilation, bool unsafeMode)
    {
        Unsafe = unsafeMode;
        compilation.AddAnalysis(this);
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        ClearIr();
        Destroyed = true;
        Compilation?.RemoveAnalysis(this);
        while (Specializations.Count != 0)
        {
            Specializations[Specializations.Count - 1].Destroy();
        }
        while (MultiClassInstantiations.Count != 0)
        {
            MultiClassInstantiations[MultiClassInstantiations.Count - 1].Destroy();
        }
        // Facts borrow syntax and symbols. Retire those dependencies before
        // destroying the scopes that own their declarations.
        while (Facts.Count != 0)
        {
            Facts[Facts.Count - 1].Destroy();
        }
        while (Scopes.Count != 0)
        {
            Scopes[Scopes.Count - 1].Destroy();
        }
        DestroyTypes();
    }
}

internal sealed partial class SemanticAnalysis
{
    internal partial void AddScope(SemanticScope scope)
    {
        if (Destroyed || Compilation is null || scope.Destroyed || scope.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach semantic scope.");
        }
        Scopes.Add(scope);
        scope.Analysis = this;
    }

    internal partial void RemoveScope(SemanticScope scope)
    {
        int index = Scopes.LastIndexOf(scope);
        if (scope.Analysis != this || index < 0 || !scope.Destroyed || scope.Parent is not null)
        {
            throw new InvalidOperationException("Only a destroyed scope can leave its analysis.");
        }
        Scopes.RemoveAt(index);
        scope.Analysis = null;
    }
}

internal sealed partial class SemanticScope
{
    internal partial SemanticScope(SemanticAnalysis analysis, SemanticScope? parent)
    {
        if (parent is not null && (parent.Destroyed || parent.Analysis != analysis))
        {
            throw new InvalidOperationException("Scope parent belongs to a different analysis.");
        }
        analysis.AddScope(this);
        try
        {
            parent?.AddChild(this);
            parent?.Specialization?.AddScope(this);
            parent?.MultiClass?.AddScope(this);
        }
        catch
        {
            // Registering the second relationship can fail (including allocation).
            // Retire the first membership before letting construction fail.
            Destroy();
            throw;
        }
    }

    internal partial void AddChild(SemanticScope child)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || child.Destroyed
            || child.Analysis != Analysis || child.Parent is not null)
        {
            throw new InvalidOperationException("Cannot attach child scope.");
        }
        for (SemanticScope? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == child)
            {
                throw new InvalidOperationException("Scope edge would create a cycle.");
            }
        }
        Children.Add(child);
        child.Parent = this;
    }

    internal partial void RemoveChild(SemanticScope child)
    {
        int index = Children.LastIndexOf(child);
        if (child.Parent != this || index < 0)
        {
            throw new InvalidOperationException("Scope child has different parent.");
        }
        Children.RemoveAt(index);
        child.Parent = null;
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
        if (MultiClass is MultiClassInstantiation multiOwner && !multiOwner.Destroyed)
        {
            multiOwner.Destroy();
            return;
        }
        MemberOwner?.RemoveMemberScope(this);
        Function?.RemoveFunctionScope(this);
        InitializerField?.RemoveInitializerScope(this);
        Parent?.RemoveChild(this);
        while (Children.Count != 0)
        {
            RemoveChild(Children[Children.Count - 1]);
        }
        while (Symbols.Count != 0)
        {
            Symbols[Symbols.Count - 1].Destroy();
        }
        Destroyed = true;
        Analysis?.RemoveScope(this);
        Specialization?.RemoveScope(this);
        MultiClass?.RemoveScope(this);
    }
}
