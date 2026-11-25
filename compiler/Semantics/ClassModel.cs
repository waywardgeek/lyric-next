using System;

namespace Lyric;

// Class and struct declarations own member scopes. Canonical types now hold persistent type
// users; no name lookup reconstructs their nominal dependencies.
internal sealed partial class SemanticSymbol
{
    internal partial void AddMemberScope(SemanticScope scope)
    {
        if (Destroyed || Kind is not (SymbolKind.Class or SymbolKind.Struct) || Scope is null
            || MemberScope is not null || scope.Destroyed || scope.MemberOwner is not null
            || scope.Function is not null || scope.Analysis != Scope.Analysis
            || scope.Parent != Scope)
        {
            throw new InvalidOperationException("Cannot attach nominal member scope.");
        }
        MemberScope = scope;
        scope.MemberOwner = this;
    }

    internal partial void RemoveMemberScope(SemanticScope scope)
    {
        if (MemberScope != scope || scope.MemberOwner != this)
        {
            throw new InvalidOperationException("Member scope has a different nominal owner.");
        }
        MemberScope = null;
        scope.MemberOwner = null;
    }

    internal partial void SetType(SemanticType? type)
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Cannot type a destroyed symbol.");
        }
        // Refuse/reserve before changing either the old link or the current IR.
        type?.ReserveSymbol(this);
        Scope?.Analysis?.ClearIr();
        Type?.RemoveSymbol(this);
        type?.AddSymbol(this);
    }
}

internal sealed partial class SemanticScope
{
    internal partial SemanticSymbol? Class
    {
        get
        {
            return MemberOwner?.Kind == SymbolKind.Class ? MemberOwner : null;
        }
    }
}
