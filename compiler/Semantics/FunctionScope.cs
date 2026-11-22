using System;

namespace Lyric;

internal sealed partial class SemanticScope
{
    internal partial SemanticScope(SemanticSymbol function)
    {
        SemanticScope? parent = function.Scope;
        SemanticAnalysis? analysis = parent?.Analysis;
        if (function.Destroyed || function.Kind is not (SymbolKind.Function or SymbolKind.Class)
            || function.FunctionScope is not null || function.MemberScope is not null || parent is null || parent.Destroyed
            || analysis is null || analysis.Destroyed)
        {
            throw new InvalidOperationException("Cannot construct scope for an invalid function or class.");
        }
        analysis.AddScope(this);
        try
        {
            parent.AddChild(this);
            if (function.Kind == SymbolKind.Class)
            {
                function.AddMemberScope(this);
            }
            else
            {
                function.AddFunctionScope(this);
            }
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    internal partial int ParameterCount()
    {
        int count = 0;
        foreach (SemanticSymbol symbol in Symbols)
        {
            if (symbol.Kind == SymbolKind.Parameter)
            {
                count += 1;
            }
        }
        return count;
    }

    internal partial SemanticSymbol ParameterAt(int index)
    {
        foreach (SemanticSymbol symbol in Symbols)
        {
            if (symbol.Kind == SymbolKind.Parameter)
            {
                if (index == 0)
                {
                    return symbol;
                }
                index -= 1;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial void AddFunctionScope(SemanticScope scope)
    {
        if (Destroyed || Kind != SymbolKind.Function || Scope is null
            || FunctionScope is not null || scope.Destroyed || scope.Function is not null
            || scope.Analysis != Scope.Analysis || scope.Parent != Scope)
        {
            throw new InvalidOperationException("Cannot attach function scope.");
        }
        FunctionScope = scope;
        scope.Function = this;
    }

    internal partial void RemoveFunctionScope(SemanticScope scope)
    {
        if (FunctionScope != scope || scope.Function != this)
        {
            throw new InvalidOperationException("Function scope has a different owner.");
        }
        FunctionScope = null;
        scope.Function = null;
    }
}

internal sealed partial class SemanticFailure
{
    internal partial SemanticFailure(Diagnostic diagnostic)
    {
        Diagnostic = diagnostic;
    }
}
