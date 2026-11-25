using System;

namespace Lyric;

internal sealed partial class SemanticScope
{
    internal partial SemanticSymbol? Lookup(string name)
    {
        for (SemanticScope? scope = this; scope is not null; scope = scope.Parent)
        {
            // Fields and methods require a receiver or nominal qualification.
            // Their scope remains in the parent graph for ownership/navigation,
            // but is not an unqualified lexical namespace.
            if (scope.MemberOwner is not null)
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

    internal partial System.Collections.Generic.IEnumerable<SemanticSymbol> Fields()
    {
        foreach (SemanticSymbol symbol in Symbols)
        {
            if (symbol.Kind == SymbolKind.Field)
            {
                yield return symbol;
            }
        }
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
        SemanticSymbol? existing = MemberOwner is null
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
            || symbol.Declaration is not null || symbol.Implementation is not null)
        {
            throw new InvalidOperationException("Only a destroyed symbol can leave its scope.");
        }
        Symbols.RemoveAt(index);
        symbol.Scope = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial SyntaxNode? BodySyntax()
    {
        return Implementation ?? Declaration;
    }

    internal partial bool IsCountedClass()
    {
        if (Kind != SymbolKind.Class || Declaration is null)
        {
            return false;
        }
        for (int i = 0; i < Declaration.ChildCount(); i += 1)
        {
            SyntaxNode child = Declaration.ChildAt(i);
            if (child.Kind == SyntaxKind.Modifier
                && child.Source!.SourceText(child.TextOffset, child.TextLength) == "value")
            {
                return true;
            }
        }
        return false;
    }

    internal partial bool IsFinalHook()
    {
        if (Kind != SymbolKind.Function || Declaration is null)
        {
            return false;
        }
        for (int i = 0; i < Declaration.ChildCount(); i += 1)
        {
            SyntaxNode child = Declaration.ChildAt(i);
            if (child.Kind == SyntaxKind.Modifier
                && child.Source!.SourceText(child.TextOffset, child.TextLength) == "final")
            {
                return true;
            }
        }
        return false;
    }

    internal partial SemanticSymbol? FinalHook()
    {
        if (Kind != SymbolKind.Class || Declaration is null)
        {
            return null;
        }
        for (int i = 0; i < Declaration.ChildCount(); i += 1)
        {
            SemanticSymbol? member = Declaration.ChildAt(i).DeclaredSymbol;
            if (member is not null && member.IsFinalHook())
            {
                return member;
            }
        }
        return null;
    }

    internal partial SemanticSymbol(SemanticScope scope, SyntaxNode? declaration,
        string name, SymbolKind kind, SemanticType? type, bool mutable,
        BuiltinKind builtin, bool acceptsArray, bool isReference)
    {
        // Reference fields belong only to value structs. Object fields still
        // cannot retain nonowning storage aliases beyond a synchronous use.
        bool referenceStorage = kind is SymbolKind.Local or SymbolKind.Parameter
            || (kind == SymbolKind.Field && scope.MemberOwner?.Kind == SymbolKind.Struct);
        if (isReference && (!referenceStorage
            || type is null || acceptsArray || !mutable))
        {
            throw new ArgumentException("A writable storage alias requires a concrete local, parameter, or struct field type.");
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
        // IR borrows declarations and types; retire it before either disappears.
        Scope?.Analysis?.ClearIr();
        MemberScope?.Destroy();
        FunctionScope?.Destroy();
        InitializerScope?.Destroy();
        NominalType?.Destroy();
        SetType(null);
        Implementation?.RemoveImplementedSymbol(this);
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
    internal partial void AddImplementedSymbol(SemanticSymbol symbol)
    {
        SemanticAnalysis? analysis = symbol.Scope?.Analysis;
        if (Destroyed || Kind != SyntaxKind.Function || Source is null || Source.Destroyed
            || DeclaredSymbol is not null || ImplementedSymbol is not null
            || symbol.Destroyed || symbol.Kind != SymbolKind.Function
            || symbol.Declaration is null || symbol.Declaration == this
            || symbol.Implementation is not null || analysis is null || analysis.Destroyed
            || analysis.Compilation is null || analysis.Compilation != Source.Parent)
        {
            throw new InvalidOperationException("Cannot attach a separate function implementation.");
        }
        analysis.ClearIr();
        ImplementedSymbol = symbol;
        symbol.Implementation = this;
    }

    internal partial void RemoveImplementedSymbol(SemanticSymbol symbol)
    {
        if (ImplementedSymbol != symbol || symbol.Implementation != this)
        {
            throw new InvalidOperationException("Implementation has a different symbol.");
        }
        symbol.Scope?.Analysis?.ClearIr();
        ImplementedSymbol = null;
        symbol.Implementation = null;
    }

    internal partial void AddDeclaredSymbol(SemanticSymbol symbol)
    {
        SemanticAnalysis? analysis = symbol.Scope?.Analysis;
        if (Destroyed || Source is null || Source.Destroyed || ImplementedSymbol is not null || DeclaredSymbol is not null
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
