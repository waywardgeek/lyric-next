using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lyric;

// Interning is a model operation: a successful lookup returns the existing
// object, while a miss constructs one object with all dependency links attached.
internal sealed partial class SemanticAnalysis
{
    internal partial SemanticType ArrayType(SemanticType element)
    {
        if (Destroyed || Compilation is null || element.Destroyed || element.Analysis != this)
        {
            throw new InvalidOperationException("Array element belongs to an invalid or different analysis.");
        }
        return InternType(SemanticTypeKind.Array, ScalarKind.Unit, element, null);
    }

    private partial SemanticType InternType(SemanticTypeKind kind, ScalarKind scalar,
        SemanticType? element, SemanticSymbol? declaration)
    {
        // Runtime identity hashes are only bucket selectors. Every candidate is
        // compared against the COMPLETE key, including actual object identities.
        int hash = SemanticType.KeyHash(kind, scalar, element, declaration);
        int bucket = (int)((uint)hash % (uint)TypeBuckets.Length);
        List<SemanticType>? entries = TypeBuckets[bucket];
        if (entries is not null)
        {
            foreach (SemanticType candidate in entries)
            {
                if (candidate.Kind == kind && candidate.ScalarTag == scalar
                    && candidate.Element == element && candidate.Declaration == declaration)
                {
                    return candidate;
                }
            }
        }
        return new SemanticType(this, kind, scalar, element, declaration);
    }

    internal partial void AddType(SemanticType type)
    {
        if (Destroyed || Compilation is null || type.Destroyed || type.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach canonical type.");
        }
        // Grow the index first. No type object or relationship changes during
        // rehashing; a failed allocation leaves the previous table intact.
        if (TypeCount >= TypeBuckets.Length * 2)
        {
            List<SemanticType>[] replacement = new List<SemanticType>[checked(TypeBuckets.Length * 2)];
            foreach (List<SemanticType>? entries in TypeBuckets)
            {
                if (entries is null)
                {
                    continue;
                }
                foreach (SemanticType entry in entries)
                {
                    int destination = (int)((uint)entry.Hash % (uint)replacement.Length);
                    replacement[destination] ??= new List<SemanticType>();
                    replacement[destination].Add(entry);
                }
            }
            TypeBuckets = replacement;
        }
        int bucket = (int)((uint)type.Hash % (uint)TypeBuckets.Length);
        List<SemanticType> values = TypeBuckets[bucket] ??= new List<SemanticType>();
        // Constructor payload links are already established. Refuse duplicate
        // direct construction too, not just duplicates through the factory.
        foreach (SemanticType existing in values)
        {
            if (existing.SameKey(type))
            {
                throw new InvalidOperationException("Duplicate canonical type key.");
            }
        }
        values.Add(type);
        type.Analysis = this;
        TypeCount += 1;
    }

    internal partial void RemoveType(SemanticType type)
    {
        int bucket = (int)((uint)type.Hash % (uint)TypeBuckets.Length);
        List<SemanticType>? values = TypeBuckets[bucket];
        if (type.Analysis != this || !type.Destroyed || !type.IsUnlinked()
            || values is null || !values.Contains(type))
        {
            throw new InvalidOperationException("Canonical type has a different analysis owner.");
        }
        values.Remove(type);
        type.Analysis = null;
        TypeCount -= 1;
    }

    private partial void DestroyTypes()
    {
        // Destruction may recursively remove entries from other buckets. Always
        // reread the current bucket instead of enumerating a changing collection.
        for (int bucket = 0; bucket < TypeBuckets.Length; bucket += 1)
        {
            List<SemanticType>? entries = TypeBuckets[bucket];
            while (entries is not null && entries.Count != 0)
            {
                entries[entries.Count - 1].Destroy();
            }
        }
    }
}

internal sealed partial class SemanticType
{
    internal static partial int KeyHash(SemanticTypeKind kind, ScalarKind scalar,
        SemanticType? element, SemanticSymbol? declaration)
    {
        return HashCode.Combine(kind, scalar,
            element is null ? 0 : RuntimeHelpers.GetHashCode(element),
            declaration is null ? 0 : RuntimeHelpers.GetHashCode(declaration));
    }

    internal partial SemanticType(SemanticAnalysis analysis, SemanticTypeKind kind,
        ScalarKind scalar, SemanticType? element, SemanticSymbol? declaration)
    {
        if (analysis.Destroyed || analysis.Compilation is null || !Enum.IsDefined(kind)
            || kind == SemanticTypeKind.Tuple
            || !Enum.IsDefined(scalar)
            || (kind == SemanticTypeKind.Array) != (element is not null)
            || (kind == SemanticTypeKind.Nominal) != (declaration is not null)
            || (kind != SemanticTypeKind.Scalar && scalar != ScalarKind.Unit)
            || (element is not null && (element.Destroyed || element.Analysis != analysis))
            || (declaration is not null && (declaration.Destroyed
                || declaration.Kind is not (SymbolKind.Class or SymbolKind.Struct) || declaration.Scope?.Analysis != analysis
                || declaration.NominalType is not null)))
        {
            throw new InvalidOperationException("Invalid canonical type payload or owner.");
        }
        Kind = kind;
        ScalarTag = scalar;
        Hash = KeyHash(kind, scalar, element, declaration);
        try
        {
            // Dependency methods establish their complete reverse links. The
            // analysis admits the completed key last, never a partially keyed type.
            element?.AddArray(this);
            declaration?.AddNominalType(this);
            analysis.AddType(this);
        }
        catch
        {
            Destroyed = true;
            Element?.RemoveArray(this);
            Declaration?.RemoveNominalType(this);
            throw;
        }
    }

    internal partial ScalarKind Scalar
    {
        get
        {
            SemanticType current = this;
            while (current.Element is not null)
            {
                current = current.Element;
            }
            return current.ScalarTag;
        }
    }

    internal partial int ArrayDepth
    {
        get
        {
            int depth = 0;
            SemanticType current = this;
            while (current.Element is not null)
            {
                depth = checked(depth + 1);
                current = current.Element;
            }
            return depth;
        }
    }

    internal partial SemanticSymbol? Class
    {
        get
        {
            SemanticType current = this;
            while (current.Element is not null)
            {
                current = current.Element;
            }
            return current.Declaration?.Kind == SymbolKind.Class ? current.Declaration : null;
        }
    }

    internal partial void AddArray(SemanticType array)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || array.Destroyed
            || array.Kind != SemanticTypeKind.Array || array.Element is not null
            || array.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach array element dependency.");
        }
        Arrays.Add(array);
        array.Element = this;
    }

    internal partial void RemoveArray(SemanticType array)
    {
        int index = Arrays.IndexOf(array);
        if (!array.Destroyed || array.Element != this || index < 0)
        {
            throw new InvalidOperationException("Different array element dependency.");
        }
        Arrays.RemoveAt(index);
        array.Element = null;
    }

    internal partial void ReserveSymbol(SemanticSymbol symbol)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || symbol.Destroyed
            || symbol.Scope?.Analysis != Analysis
            || (Class is not null && symbol.Kind == SymbolKind.Class))
        {
            throw new InvalidOperationException("Invalid canonical symbol type dependency.");
        }
        Symbols.EnsureCapacity(Symbols.Count + 1);
    }

    internal partial void AddSymbol(SemanticSymbol symbol)
    {
        ReserveSymbol(symbol);
        if (symbol.Type is not null)
        {
            throw new InvalidOperationException("Symbol already has a type.");
        }
        Symbols.Add(symbol);
        symbol.Type = this;
    }

    internal partial void RemoveSymbol(SemanticSymbol symbol)
    {
        int index = Symbols.IndexOf(symbol);
        if (symbol.Type != this || index < 0)
        {
            throw new InvalidOperationException("Different canonical symbol type dependency.");
        }
        Symbols.RemoveAt(index);
        symbol.Type = null;
    }

    internal partial void AddFact(SemanticFact fact)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || fact.Destroyed
            || fact.Analysis != Analysis || fact.TypeLink is not null)
        {
            throw new InvalidOperationException("Invalid canonical fact type dependency.");
        }
        Facts.Add(fact);
        fact.TypeLink = this;
    }

    internal partial void RemoveFact(SemanticFact fact)
    {
        int index = Facts.IndexOf(fact);
        if (fact.TypeLink != this || index < 0)
        {
            throw new InvalidOperationException("Different canonical fact type dependency.");
        }
        Facts.RemoveAt(index);
        fact.TypeLink = null;
    }

    internal partial void ReserveIrNode(IrNode node)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || node.Destroyed
            || node.Function?.Program?.Analysis != Analysis)
        {
            throw new InvalidOperationException("Invalid canonical IR type dependency.");
        }
        IrNodes.EnsureCapacity(IrNodes.Count + 1);
    }

    internal partial void AddIrNode(IrNode node)
    {
        ReserveIrNode(node);
        if (node.Type is not null)
        {
            throw new InvalidOperationException("IR node already has a type.");
        }
        IrNodes.Add(node);
        node.Type = this;
    }

    internal partial void RemoveIrNode(IrNode node)
    {
        int index = IrNodes.IndexOf(node);
        if (node.Type != this || index < 0)
        {
            throw new InvalidOperationException("Different canonical IR type dependency.");
        }
        IrNodes.RemoveAt(index);
        node.Type = null;
    }

    internal partial bool IsUnlinked()
    {
        return Element is null && Declaration is null && Arrays.Count == 0
            && Components.Count == 0 && ComponentUses.Count == 0
            && Symbols.Count == 0 && Facts.Count == 0 && IrNodes.Count == 0
            && GenericBindings.Count == 0;
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        Analysis?.ClearIr();
        Destroyed = true;
        // Detach the declaration first: invalidating a member that uses this
        // type must not reenter the same nominal dependency through its owner.
        Declaration?.RemoveNominalType(this);
        while (GenericBindings.Count != 0)
        {
            GenericBindings[GenericBindings.Count - 1].Destroy();
        }
        // Dependency retirement cascades toward users, never toward components.
        while (ComponentUses.Count != 0)
        {
            ComponentUses[ComponentUses.Count - 1].Tuple!.Destroy();
        }
        while (Components.Count != 0)
        {
            Components[Components.Count - 1].Destroy();
        }
        while (Arrays.Count != 0)
        {
            Arrays[Arrays.Count - 1].Destroy();
        }
        while (Facts.Count != 0)
        {
            Facts[Facts.Count - 1].Destroy();
        }
        while (Symbols.Count != 0)
        {
            Symbols[Symbols.Count - 1].Destroy();
        }
        while (IrNodes.Count != 0)
        {
            RemoveIrNode(IrNodes[IrNodes.Count - 1]);
        }
        Element?.RemoveArray(this);
        Analysis?.RemoveType(this);
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial void AddNominalType(SemanticType type)
    {
        if (Destroyed || Kind is not (SymbolKind.Class or SymbolKind.Struct) || Scope?.Analysis is null
            || Scope.Analysis.Destroyed || NominalType is not null || type.Destroyed
            || type.Kind != SemanticTypeKind.Nominal || type.Declaration is not null
            || type.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach canonical nominal type.");
        }
        NominalType = type;
        type.Declaration = this;
    }

    internal partial void RemoveNominalType(SemanticType type)
    {
        if (!type.Destroyed || NominalType != type || type.Declaration != this)
        {
            throw new InvalidOperationException("Different canonical nominal declaration.");
        }
        NominalType = null;
        type.Declaration = null;
    }
}
