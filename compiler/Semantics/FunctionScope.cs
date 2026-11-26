using System;

namespace Lyric;

internal sealed partial class SemanticScope
{
    internal partial SemanticScope(SemanticSymbol function)
    {
        bool initializer = function.Kind == SymbolKind.Field;
        SemanticScope? parent = initializer
            ? function.Scope?.MemberOwner?.Scope
            : function.Scope;
        SemanticAnalysis? analysis = parent?.Analysis;
        if (function.Destroyed || function.Kind is not (SymbolKind.Function or SymbolKind.Class or SymbolKind.Struct or SymbolKind.MemberNamespace or SymbolKind.Field)
            || function.FunctionScope is not null || function.MemberScope is not null
            || function.InitializerScope is not null || parent is null || parent.Destroyed
            || analysis is null || analysis.Destroyed
            || (initializer && (function.Scope?.MemberOwner?.Kind != SymbolKind.Struct
                || function.Scope.Analysis != analysis)))
        {
            throw new InvalidOperationException("Cannot construct scope for an invalid function or nominal declaration.");
        }
        analysis.AddScope(this);
        try
        {
            parent.AddChild(this);
            function.Specialization?.AddScope(this);
            function.MultiClass?.AddScope(this);
            if (initializer)
            {
                function.AddInitializerScope(this);
            }
            else if (function.Kind is SymbolKind.Class or SymbolKind.Struct or SymbolKind.MemberNamespace)
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
    internal partial void AddInitializerScope(SemanticScope scope)
    {
        SemanticSymbol? owner = Scope?.MemberOwner;
        if (Destroyed || Kind != SymbolKind.Field || owner?.Kind != SymbolKind.Struct
            || owner.Destroyed || Scope is null || Scope.Destroyed
            || InitializerScope is not null || scope.Destroyed
            || scope.InitializerField is not null || scope.Function is not null
            || scope.MemberOwner is not null || scope.Analysis is null
            || scope.Analysis != Scope.Analysis || scope.Parent != owner.Scope)
        {
            throw new InvalidOperationException("Cannot attach field initializer scope.");
        }
        InitializerScope = scope;
        scope.InitializerField = this;
    }

    internal partial void RemoveInitializerScope(SemanticScope scope)
    {
        if (InitializerScope != scope || scope.InitializerField != this)
        {
            throw new InvalidOperationException("Initializer scope has a different field owner.");
        }
        // The generated body depends on this execution scope. Retire it before
        // removing the semantic relationship, including direct scope destruction.
        Scope?.Analysis?.ClearIr();
        InitializerScope = null;
        scope.InitializerField = null;
    }

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
