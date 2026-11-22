using System;

namespace Lyric;

// Class declarations own member scopes and retain reverse dependencies for every
// persistent use of their nominal type. No name lookup reconstructs these edges.
internal sealed partial class SemanticSymbol
{
    internal partial void AddMemberScope(SemanticScope scope)
    {
        if (Destroyed || Kind != SymbolKind.Class || Scope is null
            || MemberScope is not null || scope.Destroyed || scope.Class is not null
            || scope.Function is not null || scope.Analysis != Scope.Analysis
            || scope.Parent != Scope)
        {
            throw new InvalidOperationException("Cannot attach class member scope.");
        }
        MemberScope = scope;
        scope.Class = this;
    }

    internal partial void RemoveMemberScope(SemanticScope scope)
    {
        if (MemberScope != scope || scope.Class != this)
        {
            throw new InvalidOperationException("Member scope has a different class owner.");
        }
        MemberScope = null;
        scope.Class = null;
    }

    internal partial void SetType(SemanticType? type)
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Cannot type a destroyed symbol.");
        }
        SemanticSymbol? nominal = type?.Class;
        if (nominal is not null)
        {
            if (Kind == SymbolKind.Class || nominal.Destroyed || nominal.Kind != SymbolKind.Class
                || Scope?.Analysis is null || Scope.Analysis.Destroyed
                || nominal.Scope?.Analysis != Scope.Analysis)
            {
                throw new InvalidOperationException("Nominal type belongs to an invalid or different analysis.");
            }
            // Reserve before detaching: an allocation failure leaves the existing
            // type and its reverse membership untouched.
            nominal.TypedSymbols.EnsureCapacity(nominal.TypedSymbols.Count + 1);
        }
        Type?.Class?.RemoveTypedSymbol(this);
        if (nominal is not null)
        {
            Type = null;
            nominal.AddTypedSymbol(this, type!.Value);
        }
        else
        {
            Type = type;
        }
    }

    internal partial void AddTypedSymbol(SemanticSymbol symbol, SemanticType type)
    {
        if (Destroyed || Kind != SymbolKind.Class || type.Class != this
            || symbol.Destroyed || symbol.Kind == SymbolKind.Class || symbol.Type?.Class is not null
            || Scope?.Analysis is null || Scope.Analysis.Destroyed
            || symbol.Scope?.Analysis != Scope.Analysis)
        {
            throw new InvalidOperationException("Cannot attach nominal symbol dependency.");
        }
        TypedSymbols.Add(symbol);
        symbol.Type = type;
    }

    internal partial void RemoveTypedSymbol(SemanticSymbol symbol)
    {
        int index = TypedSymbols.LastIndexOf(symbol);
        if (symbol.Type?.Class != this || index < 0)
        {
            throw new InvalidOperationException("Symbol has a different nominal type owner.");
        }
        TypedSymbols.RemoveAt(index);
        symbol.Type = null;
    }

    internal partial void AddTypedFact(SemanticFact fact, SemanticType type)
    {
        if (Destroyed || Kind != SymbolKind.Class || type.Class != this
            || fact.Destroyed || fact.Type.Class is not null || Scope?.Analysis is null
            || Scope.Analysis.Destroyed || fact.Analysis != Scope.Analysis)
        {
            throw new InvalidOperationException("Cannot attach nominal fact dependency.");
        }
        TypedFacts.Add(fact);
        fact.Type = type;
    }

    internal partial void RemoveTypedFact(SemanticFact fact)
    {
        int index = TypedFacts.LastIndexOf(fact);
        if (fact.Type.Class != this || index < 0)
        {
            throw new InvalidOperationException("Fact has a different nominal type owner.");
        }
        TypedFacts.RemoveAt(index);
        fact.Type = SemanticType.Unit;
    }
}
