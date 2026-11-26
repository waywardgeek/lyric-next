using System;

namespace Lyric;

// Nullable identity is a wrapper around an existing class-handle type, not a
// second nominal declaration. Source admission and runtime operations are checked
// separately; introducing this model does not grant an unchecked conversion.
internal sealed partial class SemanticAnalysis
{
    internal partial SemanticType NullableType(SemanticType element)
    {
        if (Destroyed || Compilation is null || element.Destroyed
            || element.Analysis != this || element.Kind != SemanticTypeKind.Nominal
            || element.Declaration?.Kind != SymbolKind.Class)
        {
            throw new InvalidOperationException("Nullable handles require a live class type in this analysis.");
        }
        return InternType(SemanticTypeKind.Nullable, ScalarKind.Unit, element, null);
    }
}

internal sealed partial class SemanticType
{
    // Construction attaches the weak element dependency and its reverse use in
    // one operation. No published key may be changed through this operation.
    internal partial void AddNullable(SemanticType nullable)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed
            || Kind != SemanticTypeKind.Nominal || Declaration?.Kind != SymbolKind.Class
            || nullable.Destroyed || nullable.Kind != SemanticTypeKind.Nullable
            || nullable.Element is not null || nullable.Analysis is not null)
        {
            throw new InvalidOperationException("Cannot attach nullable class dependency.");
        }
        NullableUses.EnsureCapacity(NullableUses.Count + 1);
        NullableUses.Add(nullable);
        nullable.Element = this;
    }

    // Retirement removes both ends. This never destroys the element class.
    internal partial void RemoveNullable(SemanticType nullable)
    {
        int index = NullableUses.IndexOf(nullable);
        if (!nullable.Destroyed || nullable.Element != this || index < 0)
        {
            throw new InvalidOperationException("Different nullable class dependency.");
        }
        NullableUses.RemoveAt(index);
        nullable.Element = null;
    }
}
