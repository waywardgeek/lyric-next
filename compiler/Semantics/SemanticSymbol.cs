using System;

namespace Lyric;

internal sealed partial class SemanticScope
{
    internal partial SemanticSymbol? Lookup(string name)
    {
        for (SemanticScope? scope = this; scope is not null; scope = scope.Parent)
        {
            // Fields and methods require a receiver or class qualification.
            // Their scope remains in the parent graph for ownership/navigation,
            // but is not an unqualified lexical namespace.
            if (scope.Class is not null)
            {
                continue;
            }
            SemanticSymbol? symbol = scope.LookupMember(name);
            if (symbol is not null)
            {
                return symbol;
            }
        }
        return null;
    }

    internal partial SemanticSymbol? LookupMember(string name)
    {
        foreach (SemanticSymbol symbol in Symbols)
        {
            if (symbol.Name == name)
            {
                return symbol;
            }
        }
        return null;
    }

    internal partial void AddSymbol(SemanticSymbol symbol)
    {
        SemanticSymbol? existing = Class is null
            ? Lookup(symbol.Name)
            : LookupMember(symbol.Name);
        if (Destroyed || Analysis is null || Analysis.Destroyed || symbol.Destroyed
            || symbol.Scope is not null || existing is not null)
        {
            throw new InvalidOperationException("Cannot attach duplicate or invalid symbol.");
        }
        Symbols.Add(symbol);
        symbol.Scope = this;
    }

    internal partial void RemoveSymbol(SemanticSymbol symbol)
    {
        int index = Symbols.LastIndexOf(symbol);
        if (symbol.Scope != this || index < 0 || !symbol.Destroyed
            || symbol.Declaration is not null)
        {
            throw new InvalidOperationException("Only a destroyed symbol can leave its scope.");
        }
        Symbols.RemoveAt(index);
        symbol.Scope = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial SemanticSymbol(SemanticScope scope, SyntaxNode? declaration,
        string name, SymbolKind kind, SemanticType? type, bool mutable,
        BuiltinKind builtin, bool acceptsArray, bool isReference)
    {
        if (isReference && (kind is not (SymbolKind.Local or SymbolKind.Parameter)
            || type is null || acceptsArray || !mutable))
        {
            throw new ArgumentException("A writable storage alias requires a concrete local or parameter type.");
        }
        IsReference = isReference;
        if (acceptsArray && (kind != SymbolKind.Parameter || type is not null))
        {
            throw new ArgumentException("An array-shaped parameter has no concrete type.");
        }
        AcceptsArray = acceptsArray;
        Name = name;
        Kind = kind;
        Builtin = builtin;
        Mutable = mutable;
        scope.AddSymbol(this);
        try
        {
            SetType(type);
            declaration?.AddDeclaredSymbol(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        MemberScope?.Destroy();
        FunctionScope?.Destroy();
        while (TypedFacts.Count != 0)
        {
            TypedFacts[TypedFacts.Count - 1].Destroy();
        }
        while (TypedSymbols.Count != 0)
        {
            TypedSymbols[TypedSymbols.Count - 1].Destroy();
        }
        SetType(null);
        Declaration?.RemoveDeclaredSymbol(this);
        // Removing a declaration invalidates its checked uses. Destroy facts,
        // not their syntax nodes, so no resolved reference survives this symbol.
        while (Uses.Count != 0)
        {
            Uses[Uses.Count - 1].Destroy();
        }
        Destroyed = true;
        Scope?.RemoveSymbol(this);
    }
}

internal sealed partial class SyntaxNode
{
    internal partial void AddDeclaredSymbol(SemanticSymbol symbol)
    {
        SemanticAnalysis? analysis = symbol.Scope?.Analysis;
        if (Destroyed || Source is null || Source.Destroyed || DeclaredSymbol is not null
            || symbol.Destroyed || symbol.Declaration is not null || analysis is null
            || analysis.Destroyed || analysis.Compilation is null
            || analysis.Compilation != Source.Parent)
        {
            throw new InvalidOperationException("Cannot attach declaration from another analysis.");
        }
        DeclaredSymbol = symbol;
        symbol.Declaration = this;
    }

    internal partial void RemoveDeclaredSymbol(SemanticSymbol symbol)
    {
        if (DeclaredSymbol != symbol || symbol.Declaration != this)
        {
            throw new InvalidOperationException("Declaration has a different symbol.");
        }
        DeclaredSymbol = null;
        symbol.Declaration = null;
    }
}
