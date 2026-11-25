namespace Lyric;

// Scalar vocabulary for the current type system. Array nesting belongs to the
// canonical SemanticType structure, not to a growing list of special enum cases.
internal enum ScalarKind
{
    Unit,
    Bool,
    U8,
    U16,
    U32,
    U64,
    I8,
    I16,
    I32,
    I64,
    F32,
    F64,
}

// Canonical forms supported by this migration. Each analysis interns its own
// identities; a type from another analysis is never interchangeable.
internal enum SemanticTypeKind
{
    Scalar,
    Array,
    Nominal,
    Tuple,
}

// Identity-bearing canonical type. The analysis strongly owns its lifetime.
// Element and Declaration are weak dependencies with reverse memberships.
// Payloads never change while live; removal clears relationships during teardown.
internal sealed partial class SemanticType
{
    internal SemanticAnalysis? Analysis;
    internal SemanticType? Element;
    internal SemanticSymbol? Declaration;
    internal readonly SemanticTypeKind Kind;
    internal readonly ScalarKind ScalarTag;
    internal readonly int Hash;
    internal bool Destroyed;
    private readonly System.Collections.Generic.List<SemanticType> Arrays = new();
    // Ordered occurrences are strongly owned; repeated component types use
    // distinct links. Reverse uses invalidate tuples, not their shared elements.
    private readonly System.Collections.Generic.List<SemanticTypeComponent> Components = new();
    private readonly System.Collections.Generic.List<SemanticTypeComponent> ComponentUses = new();
    internal partial SemanticType(SemanticAnalysis analysis,
        System.Collections.Generic.IReadOnlyList<SemanticType> components);
    internal static partial int TupleHash(System.Collections.Generic.IReadOnlyList<SemanticType> components);
    internal partial bool MatchesComponents(System.Collections.Generic.IReadOnlyList<SemanticType> components);
    internal partial bool SameKey(SemanticType other);
    internal partial int ComponentCount();
    internal partial SemanticType ComponentAt(int index);
    internal partial void AddComponent(SemanticTypeComponent link, SemanticType component);
    internal partial void RemoveComponent(SemanticTypeComponent link);

    private readonly System.Collections.Generic.List<SemanticSymbol> Symbols = new();
    private readonly System.Collections.Generic.List<SemanticFact> Facts = new();
    private readonly System.Collections.Generic.List<IrNode> IrNodes = new();

    // Called by interning after lookup. Registration refuses duplicate keys and
    // cross-analysis components, and unwinds any completed links on failure.
    internal partial SemanticType(SemanticAnalysis analysis, SemanticTypeKind kind,
        ScalarKind scalar, SemanticType? element, SemanticSymbol? declaration);
    internal partial void Destroy();
    internal partial bool IsUnlinked();
    internal static partial int KeyHash(SemanticTypeKind kind, ScalarKind scalar,
        SemanticType? element, SemanticSymbol? declaration);
    // Compatibility queries derive from canonical structure, not a second stored
    // flat descriptor. Equality itself is ordinary object identity.
    internal partial ScalarKind Scalar { get; }
    internal partial int ArrayDepth { get; }
    internal partial SemanticSymbol? Class { get; }
    // Attach only during construction; detach only during retirement. Published
    // keys cannot be edited in place without invalidating the hash-table invariant.
    internal partial void AddArray(SemanticType array);
    internal partial void RemoveArray(SemanticType array);
    internal partial void ReserveSymbol(SemanticSymbol symbol);
    internal partial void AddSymbol(SemanticSymbol symbol);
    internal partial void RemoveSymbol(SemanticSymbol symbol);
    internal partial void AddFact(SemanticFact fact);
    internal partial void RemoveFact(SemanticFact fact);
    internal partial void ReserveIrNode(IrNode node);
    internal partial void AddIrNode(IrNode node);
    internal partial void RemoveIrNode(IrNode node);
}

// Pure type operations, shared by literal checking, operators, and calls. No
// source values are inspected when deciding conversions of already-typed values.
internal static partial class SemanticTypes
{
    internal static partial SemanticType ArrayOf(SemanticType element);
    // Requires an array; removes exactly one layer, never flattens nested values.
    internal static partial SemanticType ElementOf(SemanticType array);
    internal static partial string Name(SemanticType type);
    // True when copying/retiring a value requires ownership operations: backing
    // storage, counted handles, or tuple/struct components containing either. Ordinary
    // object handles do not prolong lifetime. Requires a live canonical type.
    internal static partial bool HasOwnedContents(SemanticType type);
    // Observable array-growth default, distinct from cleanup-safe empty storage.
    // Scalars and empty arrays default; tuples require every component to default.
    // Nominal structs/classes do not default, regardless of field initializers.
    // Requires a live canonical type; computed from immutable type components.
    internal static partial bool HasDefaultValue(SemanticType type);
    // True for direct or nested stored aliases. Arrays are traversed here so
    // escape checks cannot hide borrows behind descriptors; class handles stop
    // traversal. Requires a live type and resolved struct fields. No cached state.
    internal static partial bool HasContainedBorrows(SemanticType type);
    // Requires live, resolved inline field types. False means an inline cycle;
    // arrays and class handles bound storage and do not expand their targets.
    // This checks layout only, not borrow safety or runtime ownership balance.
    internal static partial bool HasFiniteLayout(SemanticType type);
    internal static partial bool IsInteger(SemanticType type);
    internal static partial bool IsFloat(SemanticType type);
    internal static partial bool IsSigned(SemanticType type);
    internal static partial int IntegerWidth(SemanticType type);
    // Invalid for noninteger types. BigInteger also expresses the u64 maximum
    // without depending on host signed-integer overflow or floating conversion.
    internal static partial System.Numerics.BigInteger Minimum(SemanticType type);
    internal static partial System.Numerics.BigInteger Maximum(SemanticType type);
    internal static partial bool Represents(SemanticType type, System.Numerics.BigInteger value);
    // Identity, whole-range integer inclusion, and f32->f64 only. Literal
    // contextual typing is a separate operation, not an extra narrowing rule.
    internal static partial bool CanWiden(SemanticType from, SemanticType to);
    // Select one existing operand type. Never invent a wider third type or let
    // an assignment destination change the width of an already-typed operation.
    internal static partial SemanticType? ArithmeticType(SemanticType left, SemanticType right);
}

// One authoritative custom hash table per analysis. Buckets are the ownership
// collection, not a second cache. Rehashing preserves objects and all user links.
internal sealed partial class SemanticAnalysis
{
    private System.Collections.Generic.List<SemanticType>[] TypeBuckets =
        new System.Collections.Generic.List<SemanticType>[16];
    private int TypeCount;
    internal partial SemanticType ScalarType(ScalarKind kind);
    internal partial SemanticType ClassType(SemanticSymbol declaration);
    // Nominal value identity; does not confer object-handle lifetime semantics.
    internal partial SemanticType StructType(SemanticSymbol declaration);
    internal partial SemanticType ArrayType(SemanticType element);
    internal partial SemanticType TupleType(System.Collections.Generic.IReadOnlyList<SemanticType> components);
    internal partial SemanticType[] PredefinedTypes();
    private partial SemanticType InternType(SemanticTypeKind kind, ScalarKind scalar,
        SemanticType? element, SemanticSymbol? declaration);
    internal partial void AddType(SemanticType type);
    internal partial void RemoveType(SemanticType type);
    private partial void DestroyTypes();
}

internal sealed partial class SemanticSymbol
{
    // The unique nominal type depends on this declaration; deleting the type
    // does not delete the declaration. Declaration retirement invalidates it.
    internal SemanticType? NominalType;
    internal partial void AddNominalType(SemanticType type);
    internal partial void RemoveNominalType(SemanticType type);
}

// One ordered occurrence in a tuple key, not a separate type identity. The tuple
// strongly owns this link; the component is a non-owning dependency. Construction
// and removal update both memberships together before returning.
internal sealed partial class SemanticTypeComponent
{
    internal SemanticType? Tuple;
    internal SemanticType? Component;
    internal partial SemanticTypeComponent(SemanticType tuple, SemanticType component);
    internal partial void Destroy();
}
