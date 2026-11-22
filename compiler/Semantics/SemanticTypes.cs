using System;
using System.Numerics;

namespace Lyric;

internal readonly partial record struct SemanticType
{
    internal partial SemanticType(ScalarKind scalar, int arrayDepth, SemanticSymbol? nominalClass)
    {
        if (arrayDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayDepth));
        }
        if (nominalClass is not null && (nominalClass.Destroyed
            || nominalClass.Kind != SymbolKind.Class || scalar != ScalarKind.Unit))
        {
            throw new ArgumentException("A nominal type requires a live class and no scalar payload.");
        }
        Class = nominalClass;
        Scalar = scalar;
        ArrayDepth = arrayDepth;
    }
}

internal static partial class SemanticTypes
{
    internal static partial SemanticType[] Predefined()
    {
        return [SemanticType.Unit, SemanticType.Bool,
            SemanticType.U8, SemanticType.U16, SemanticType.U32, SemanticType.U64,
            SemanticType.I8, SemanticType.I16, SemanticType.I32, SemanticType.I64,
            SemanticType.F32, SemanticType.F64, SemanticType.ByteArray];
    }

    internal static partial SemanticType ArrayOf(SemanticType element)
    {
        return new SemanticType(element.Scalar, checked(element.ArrayDepth + 1), element.Class);
    }

    internal static partial SemanticType ElementOf(SemanticType array)
    {
        if (array.ArrayDepth == 0)
        {
            throw new ArgumentException("An array type is required.", nameof(array));
        }
        return new SemanticType(array.Scalar, array.ArrayDepth - 1, array.Class);
    }

    internal static partial string Name(SemanticType type)
    {
        string scalarName = type.Class?.Name ?? (type.Scalar switch
        {
            ScalarKind.Unit => "unit",
            ScalarKind.Bool => "bool",
            ScalarKind.U8 => "u8",
            ScalarKind.U16 => "u16",
            ScalarKind.U32 => "u32",
            ScalarKind.U64 => "u64",
            ScalarKind.I8 => "i8",
            ScalarKind.I16 => "i16",
            ScalarKind.I32 => "i32",
            ScalarKind.I64 => "i64",
            ScalarKind.F32 => "f32",
            ScalarKind.F64 => "f64",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        });
        return new string('[', type.ArrayDepth) + scalarName + new string(']', type.ArrayDepth);
    }

    internal static partial bool IsInteger(SemanticType type)
    {
        return type.Class is null && type.ArrayDepth == 0 && type.Scalar is ScalarKind.U8 or ScalarKind.U16 or ScalarKind.U32
            or ScalarKind.U64 or ScalarKind.I8 or ScalarKind.I16
            or ScalarKind.I32 or ScalarKind.I64;
    }

    internal static partial bool IsFloat(SemanticType type)
    {
        return type.Class is null && type.ArrayDepth == 0 && type.Scalar is ScalarKind.F32 or ScalarKind.F64;
    }

    internal static partial bool IsSigned(SemanticType type)
    {
        return type.Class is null && type.ArrayDepth == 0 && type.Scalar is ScalarKind.I8 or ScalarKind.I16
            or ScalarKind.I32 or ScalarKind.I64;
    }

    internal static partial int IntegerWidth(SemanticType type)
    {
        if (!IsInteger(type))
        {
            throw new ArgumentException("An integer type is required.", nameof(type));
        }
        return type.Scalar switch
        {
            ScalarKind.I8 or ScalarKind.U8 => 8,
            ScalarKind.I16 or ScalarKind.U16 => 16,
            ScalarKind.I32 or ScalarKind.U32 => 32,
            ScalarKind.I64 or ScalarKind.U64 => 64,
            _ => throw new ArgumentException("An integer type is required.", nameof(type)),
        };
    }

    internal static partial BigInteger Minimum(SemanticType type)
    {
        int width = IntegerWidth(type);
        if (IsSigned(type))
        {
            return -(BigInteger.One << (width - 1));
        }
        return BigInteger.Zero;
    }

    internal static partial BigInteger Maximum(SemanticType type)
    {
        int width = IntegerWidth(type);
        int magnitudeBits = IsSigned(type) ? width - 1 : width;
        return (BigInteger.One << magnitudeBits) - BigInteger.One;
    }

    internal static partial bool Represents(SemanticType type, BigInteger value)
    {
        BigInteger minimum = Minimum(type);
        BigInteger maximum = Maximum(type);
        return value >= minimum && value <= maximum;
    }

    internal static partial bool CanWiden(SemanticType from, SemanticType to)
    {
        if (from == to)
        {
            return true;
        }
        if (IsInteger(from) && IsInteger(to))
        {
            return Minimum(to) <= Minimum(from) && Maximum(from) <= Maximum(to);
        }
        return from == SemanticType.F32 && to == SemanticType.F64;
    }

    internal static partial SemanticType? ArithmeticType(SemanticType left, SemanticType right)
    {
        bool integerPair = IsInteger(left) && IsInteger(right);
        bool floatPair = IsFloat(left) && IsFloat(right);
        if (!integerPair && !floatPair)
        {
            return null;
        }
        if (CanWiden(left, right))
        {
            return right;
        }
        if (CanWiden(right, left))
        {
            return left;
        }
        return null;
    }
}
