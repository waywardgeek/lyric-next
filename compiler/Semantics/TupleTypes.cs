using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lyric;

// Tuple keys are ordered identities, not names or recursively compared shapes.
internal sealed partial class SemanticAnalysis
{
    internal partial SemanticType TupleType(IReadOnlyList<SemanticType> components)
    {
        if (Destroyed || Compilation is null || components.Count == 0)
        {
            throw new InvalidOperationException("A tuple requires a live analysis and at least one component.");
        }
        foreach (SemanticType component in components)
        {
            if (component.Destroyed || component.Analysis != this)
            {
                throw new InvalidOperationException("Tuple component belongs to a different or retired analysis.");
            }
        }
        int hash = SemanticType.TupleHash(components);
        int bucket = (int)((uint)hash % (uint)TypeBuckets.Length);
        List<SemanticType>? entries = TypeBuckets[bucket];
        if (entries is not null)
        {
            foreach (SemanticType candidate in entries)
            {
                if (candidate.Kind == SemanticTypeKind.Tuple && candidate.MatchesComponents(components))
                {
                    return candidate;
                }
            }
        }
        return new SemanticType(this, components);
    }
}

internal sealed partial class SemanticType
{
    internal static partial int TupleHash(IReadOnlyList<SemanticType> components)
    {
        HashCode hash = new();
        hash.Add(SemanticTypeKind.Tuple);
        hash.Add(components.Count);
        foreach (SemanticType component in components)
        {
            hash.Add(RuntimeHelpers.GetHashCode(component));
        }
        return hash.ToHashCode();
    }

    internal partial bool MatchesComponents(IReadOnlyList<SemanticType> components)
    {
        if (Components.Count != components.Count)
        {
            return false;
        }
        for (int index = 0; index < Components.Count; index += 1)
        {
            if (Components[index].Component != components[index])
            {
                return false;
            }
        }
        return true;
    }

    internal partial bool SameKey(SemanticType other)
    {
        if (Kind != other.Kind || ScalarTag != other.ScalarTag
            || Element != other.Element || Declaration != other.Declaration
            || Components.Count != other.Components.Count)
        {
            return false;
        }
        for (int index = 0; index < Components.Count; index += 1)
        {
            if (Components[index].Component != other.Components[index].Component)
            {
                return false;
            }
        }
        return true;
    }

    internal partial SemanticType(SemanticAnalysis analysis, IReadOnlyList<SemanticType> components)
    {
        if (analysis.Destroyed || analysis.Compilation is null || components.Count == 0)
        {
            throw new InvalidOperationException("Invalid tuple owner or empty tuple key.");
        }
        // Validate the complete request before establishing any relationships.
        foreach (SemanticType component in components)
        {
            if (component.Destroyed || component.Analysis != analysis)
            {
                throw new InvalidOperationException("Invalid tuple component.");
            }
        }
        Kind = SemanticTypeKind.Tuple;
        ScalarTag = ScalarKind.Unit;
        Hash = TupleHash(components);
        try
        {
            foreach (SemanticType component in components)
            {
                _ = new SemanticTypeComponent(this, component);
            }
            analysis.AddType(this);
        }
        catch
        {
            Destroyed = true;
            while (Components.Count != 0)
            {
                Components[Components.Count - 1].Destroy();
            }
            throw;
        }
    }

    internal partial int ComponentCount()
    {
        return Components.Count;
    }

    internal partial SemanticType ComponentAt(int index)
    {
        return Components[index].Component!;
    }

    internal partial void AddComponent(SemanticTypeComponent link, SemanticType component)
    {
        if (Destroyed || Kind != SemanticTypeKind.Tuple || Analysis is not null
            || link.Tuple is not null || link.Component is not null
            || component.Destroyed || component.Analysis is null || component.Analysis.Destroyed
            || (Components.Count != 0
                && Components[0].Component!.Analysis != component.Analysis))
        {
            throw new InvalidOperationException("Cannot change a published tuple key or attach an invalid component.");
        }
        Components.EnsureCapacity(Components.Count + 1);
        component.ComponentUses.EnsureCapacity(component.ComponentUses.Count + 1);
        Components.Add(link);
        component.ComponentUses.Add(link);
        link.Tuple = this;
        link.Component = component;
    }

    internal partial void RemoveComponent(SemanticTypeComponent link)
    {
        SemanticType? component = link.Component;
        if (!Destroyed || link.Tuple != this || component is null
            || !Components.Contains(link) || !component.ComponentUses.Contains(link))
        {
            throw new InvalidOperationException("Cannot remove a live tuple key component or an unrelated link.");
        }
        Components.Remove(link);
        component.ComponentUses.Remove(link);
        link.Tuple = null;
        link.Component = null;
    }
}

internal sealed partial class SemanticTypeComponent
{
    internal partial SemanticTypeComponent(SemanticType tuple, SemanticType component)
    {
        tuple.AddComponent(this, component);
    }

    internal partial void Destroy()
    {
        // The tuple owns the occurrence. Detaching it never deletes the shared
        // component; component retirement instead asks dependent tuples to retire.
        Tuple?.RemoveComponent(this);
    }
}
