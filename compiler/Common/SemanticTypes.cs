namespace Lyric;

// Scalar vocabulary for the current type system. Array nesting belongs to the
// immutable SemanticType value, not to a growing list of special enum cases.
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

// Immutable type descriptor: scalar or nominal class identity, plus array depth.
// Scalar descriptions are value-like. A nominal description carries a weak class
// relationship: persistent symbol/fact holders register reverse dependencies on
// that class. Temporary algorithmic descriptors do not outlive their analysis.
// Record equality includes class identity; equal spelling is not equal identity.
internal readonly partial record struct SemanticType
{
    internal readonly ScalarKind Scalar;
    internal readonly int ArrayDepth;
    internal readonly SemanticSymbol? Class;

    // Requires a nonnegative depth. The default struct value denotes unit.
    internal partial SemanticType(ScalarKind scalar, int arrayDepth, SemanticSymbol? nominalClass = null);

    internal static readonly SemanticType Unit = new(ScalarKind.Unit, 0);
    internal static readonly SemanticType Bool = new(ScalarKind.Bool, 0);
    internal static readonly SemanticType U8 = new(ScalarKind.U8, 0);
    internal static readonly SemanticType U16 = new(ScalarKind.U16, 0);
    internal static readonly SemanticType U32 = new(ScalarKind.U32, 0);
    internal static readonly SemanticType U64 = new(ScalarKind.U64, 0);
    internal static readonly SemanticType I8 = new(ScalarKind.I8, 0);
    internal static readonly SemanticType I16 = new(ScalarKind.I16, 0);
    internal static readonly SemanticType I32 = new(ScalarKind.I32, 0);
    internal static readonly SemanticType I64 = new(ScalarKind.I64, 0);
    internal static readonly SemanticType F32 = new(ScalarKind.F32, 0);
    internal static readonly SemanticType F64 = new(ScalarKind.F64, 0);
    internal static readonly SemanticType ByteArray = new(ScalarKind.U8, 1);
}

// Pure type operations, shared by literal checking, operators, and calls. No
// source values are inspected when deciding conversions of already-typed values.
internal static partial class SemanticTypes
{
    // Fresh temporary enumeration for registering the predefined type names.
    internal static partial SemanticType[] Predefined();
    internal static partial SemanticType ArrayOf(SemanticType element);
    // Requires an array; removes exactly one layer, never flattens nested values.
    internal static partial SemanticType ElementOf(SemanticType array);
    internal static partial string Name(SemanticType type);
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
