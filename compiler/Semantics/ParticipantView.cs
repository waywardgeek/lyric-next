using System;

namespace Lyric;

// Participant views carry lookup identity without changing runtime types. These
// weak relationships are independent of the instantiation's strong ownership.
internal sealed partial class ParticipantBinding
{
    internal partial bool AcceptsViewType(SemanticType? type)
    {
        if (Destroyed || ConcreteClass?.NominalType is not SemanticType nominal || type is null)
        {
            return false;
        }
        return type == nominal
            || (type.Kind == SemanticTypeKind.Nullable && type.Element == nominal);
    }

    internal partial void AddViewSymbol(SemanticSymbol symbol)
    {
        SemanticAnalysis? analysis = Instantiation?.Analysis;
        if (Destroyed || Instantiation is null || Instantiation.Destroyed
            || analysis is null || analysis.Destroyed || symbol.Destroyed
            || symbol.Scope?.Analysis != analysis || symbol.ParticipantView is not null
            || !AcceptsViewType(symbol.Type))
        {
            throw new InvalidOperationException("Incompatible participant symbol view.");
        }
        ViewSymbols.EnsureCapacity(ViewSymbols.Count + 1);
        analysis.ClearIr();
        ViewSymbols.Add(symbol);
        symbol.ParticipantView = this;
    }

    internal partial void RemoveViewSymbol(SemanticSymbol symbol)
    {
        if (symbol.ParticipantView != this || !ViewSymbols.Contains(symbol))
        {
            throw new InvalidOperationException("Different participant symbol view.");
        }
        Instantiation?.Analysis?.ClearIr();
        ViewSymbols.Remove(symbol);
        symbol.ParticipantView = null;
    }

    internal partial void AddViewFact(SemanticFact fact)
    {
        SemanticAnalysis? analysis = Instantiation?.Analysis;
        if (Destroyed || Instantiation is null || Instantiation.Destroyed
            || analysis is null || analysis.Destroyed || fact.Destroyed
            || fact.Analysis != analysis || fact.ParticipantView is not null
            || !AcceptsViewType(fact.TypeLink))
        {
            throw new InvalidOperationException("Incompatible participant fact view.");
        }
        ViewFacts.EnsureCapacity(ViewFacts.Count + 1);
        analysis.ClearIr();
        ViewFacts.Add(fact);
        fact.ParticipantView = this;
    }

    internal partial void RemoveViewFact(SemanticFact fact)
    {
        if (fact.ParticipantView != this || !ViewFacts.Contains(fact))
        {
            throw new InvalidOperationException("Different participant fact view.");
        }
        Instantiation?.Analysis?.ClearIr();
        ViewFacts.Remove(fact);
        fact.ParticipantView = null;
    }

    internal partial void AddMember(SemanticSymbol symbol)
    {
        if (Destroyed || Instantiation is null || Instantiation.Destroyed
            || symbol.Destroyed || symbol.MultiClass != Instantiation
            || symbol.ContributingParticipant is not null
            || symbol.Scope?.Class != ConcreteClass
            || symbol.Kind is not (SymbolKind.Function or SymbolKind.Field)
            || (!symbol.IsDestructorFragment() && FindMember(symbol.Name) is not null))
        {
            throw new InvalidOperationException("Invalid participant contribution.");
        }
        Members.EnsureCapacity(Members.Count + 1);
        Instantiation.Analysis?.ClearIr();
        Members.Add(symbol);
        symbol.ContributingParticipant = this;
    }

    internal partial void RemoveMember(SemanticSymbol symbol)
    {
        if (symbol.ContributingParticipant != this || !Members.Contains(symbol))
        {
            throw new InvalidOperationException("Different contributing participant.");
        }
        Instantiation?.Analysis?.ClearIr();
        Members.Remove(symbol);
        symbol.ContributingParticipant = null;
    }

    internal partial SemanticSymbol? FindMember(string name)
    {
        foreach (SemanticSymbol member in Members)
        {
            if (member.Name == name && !member.IsDestructorFragment())
            {
                return member;
            }
        }
        return null;
    }
}
