using System;
using System.Numerics;

namespace Lyric;

internal sealed partial class SemanticAnalysis
{
    internal partial void AddFact(SemanticFact fact)
    {
        if (Destroyed || Compilation is null || fact.Destroyed || fact.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach semantic fact.");
        }
        Facts.Add(fact);
        fact.Analysis = this;
    }

    internal partial void RemoveFact(SemanticFact fact)
    {
        int index = Facts.LastIndexOf(fact);
        if (fact.Analysis != this || index < 0 || !fact.Destroyed
            || fact.Node is not null || fact.Symbol is not null)
        {
            throw new InvalidOperationException("Only an unlinked destroyed fact can leave its analysis.");
        }
        Facts.RemoveAt(index);
        fact.Analysis = null;
    }
}

internal sealed partial class SemanticFact
{
    internal partial SemanticFact(SemanticAnalysis analysis, SyntaxNode node,
        SemanticType type, SemanticSymbol? symbol, BigInteger? integerConstant, bool canContinue,
        ArrayOperation arrayOperation, ClassOperation classOperation)
    {
        ArrayOperation = arrayOperation;
        ClassOperation = classOperation;
        CanContinue = canContinue;
        IntegerConstant = integerConstant;
        analysis.AddFact(this);
        try
        {
            if (type.Class is not null)
            {
                type.Class.AddTypedFact(this, type);
            }
            else
            {
                Type = type;
            }
            node.AddFact(this);
            symbol?.AddUse(this);
        }
        catch
        {
            // Each completed operation is two-way. Destroy can therefore unwind
            // any prefix of construction without a separate registration ledger.
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
        Type.Class?.RemoveTypedFact(this);
        Symbol?.RemoveUse(this);
        Node?.RemoveFact(this);
        Destroyed = true;
        Analysis?.RemoveFact(this);
    }
}

internal sealed partial class SyntaxNode
{
    internal partial void AddFact(SemanticFact fact)
    {
        SemanticAnalysis? analysis = fact.Analysis;
        if (Destroyed || Source is null || Source.Destroyed || Fact is not null
            || fact.Destroyed || fact.Node is not null || analysis is null
            || analysis.Destroyed || analysis.Compilation is null
            || analysis.Compilation != Source.Parent)
        {
            throw new InvalidOperationException("Cannot attach semantic fact to syntax.");
        }
        Fact = fact;
        fact.Node = this;
    }

    internal partial void RemoveFact(SemanticFact fact)
    {
        if (Fact != fact || fact.Node != this)
        {
            throw new InvalidOperationException("Syntax has a different semantic fact.");
        }
        Fact = null;
        fact.Node = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial void AddUse(SemanticFact fact)
    {
        SemanticAnalysis? analysis = Scope?.Analysis;
        if (Destroyed || analysis is null || analysis.Destroyed || fact.Destroyed
            || fact.Analysis != analysis || fact.Symbol is not null)
        {
            throw new InvalidOperationException("Cannot attach resolved use from another analysis.");
        }
        Uses.Add(fact);
        fact.Symbol = this;
    }

    internal partial void RemoveUse(SemanticFact fact)
    {
        int index = Uses.LastIndexOf(fact);
        if (fact.Symbol != this || index < 0)
        {
            throw new InvalidOperationException("Resolved use has a different symbol.");
        }
        Uses.RemoveAt(index);
        fact.Symbol = null;
    }
}
