using System;
using System.Numerics;

namespace Lyric;

internal static partial class SemanticTypes
{
    internal static partial SemanticType ArrayOf(SemanticType element)
    {
        SemanticAnalysis analysis = element.Analysis
            ?? throw new InvalidOperationException("Retired array element type.");
        return analysis.ArrayType(element);
    }

    internal static partial SemanticType ElementOf(SemanticType array)
    {
        if (array.Destroyed || array.Kind != SemanticTypeKind.Array)
        {
            throw new ArgumentException("A live array type is required.", nameof(array));
        }
        return array.Element!;
    }

    internal static partial string Name(SemanticType type)
    {
        System.Text.StringBuilder name = new();
        System.Collections.Generic.Stack<(SemanticType? Type, string? Text)> pending = new();
        pending.Push((type, null));
        while (pending.Count != 0)
        {
            (SemanticType? current, string? text) = pending.Pop();
            if (current is null)
            {
                name.Append(text);
                continue;
            }
            if (current.Kind == SemanticTypeKind.Array)
            {
                name.Append('[');
                pending.Push((null, "]"));
                pending.Push((current.Element!, null));
                continue;
            }
            if (current.Kind == SemanticTypeKind.Tuple)
            {
                name.Append('(');
                int count = current.ComponentCount();
                pending.Push((null, count == 1 ? ",)" : ")"));
                for (int index = count - 1; index >= 0; index -= 1)
                {
                    pending.Push((current.ComponentAt(index), null));
                    if (index != 0)
                    {
                        pending.Push((null, ", "));
                    }
                }
                continue;
            }
            string scalarName = current.Declaration?.Name ?? (current.ScalarTag switch
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
            name.Append(scalarName);
        }
        return name.ToString();
    }

    internal static partial bool HasDefaultValue(SemanticType type)
    {
        if (type.Destroyed || type.Analysis is null)
        {
            throw new ArgumentException("A live canonical type is required.", nameof(type));
        }
        System.Collections.Generic.Stack<SemanticType> pending = new();
        System.Collections.Generic.HashSet<SemanticType> visited = new();
        pending.Push(type);
        while (pending.Count != 0)
        {
            SemanticType current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }
            if (current.Kind == SemanticTypeKind.Nominal)
            {
                // Neither a handle nor a struct gains a language default from
                // its cleanup representation or declared field initializers.
                return false;
            }
            if (current.Kind == SemanticTypeKind.Tuple)
            {
                for (int index = 0; index < current.ComponentCount(); index += 1)
                {
                    pending.Push(current.ComponentAt(index));
                }
            }
            // Scalars have typed zero/unit defaults. Arrays stop here: their
            // empty default constructs no elements, even if those cannot default.
        }
        return true;
    }

    internal static partial bool HasOwnedContents(SemanticType type)
    {
        if (type.Destroyed || type.Analysis is null)
        {
            throw new ArgumentException("A live canonical type is required.", nameof(type));
        }
        System.Collections.Generic.Stack<SemanticType> pending = new();
        System.Collections.Generic.HashSet<SemanticType> visited = new();
        pending.Push(type);
        while (pending.Count != 0)
        {
            SemanticType current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }
            // An array owns its allocation regardless of its element type. Do not
            // expand its element: descriptor indirection bounds recursive layouts.
            if (current.Kind == SemanticTypeKind.Array)
            {
                return true;
            }
            if (current.Kind == SemanticTypeKind.Nominal
                && current.Declaration!.IsCountedClass())
            {
                return true;
            }
            if (current.Kind == SemanticTypeKind.Nominal
                && current.Declaration!.Kind == SymbolKind.Struct)
            {
                SemanticScope fields = current.Declaration.MemberScope
                    ?? throw new InvalidOperationException("Struct member scope is incomplete.");
                foreach (SemanticSymbol field in fields.Fields())
                {
                    // Stored references are aliases, not ownership of their targets.
                    if (!field.IsReference)
                    {
                        pending.Push(field.Type
                            ?? throw new InvalidOperationException("Struct field type is incomplete."));
                    }
                }
            }
            if (current.Kind == SemanticTypeKind.Tuple)
            {
                for (int index = 0; index < current.ComponentCount(); index += 1)
                {
                    pending.Push(current.ComponentAt(index));
                }
            }
        }
        return false;
    }

    internal static partial bool HasContainedBorrows(SemanticType type)
    {
        if (type.Destroyed || type.Analysis is null)
        {
            throw new ArgumentException("A live canonical type is required.", nameof(type));
        }
        System.Collections.Generic.Stack<SemanticType> pending = new();
        System.Collections.Generic.HashSet<SemanticType> visited = new();
        pending.Push(type);
        while (pending.Count != 0)
        {
            SemanticType current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }
            if (current.Kind == SemanticTypeKind.Array)
            {
                // Unlike layout checking, escape classification must inspect
                // elements. The visited set terminates recursive struct arrays.
                pending.Push(current.Element!);
            }
            else if (current.Kind == SemanticTypeKind.Tuple)
            {
                for (int index = 0; index < current.ComponentCount(); index += 1)
                {
                    pending.Push(current.ComponentAt(index));
                }
            }
            else if (current.Declaration?.Kind == SymbolKind.Struct)
            {
                SemanticScope fields = current.Declaration.MemberScope
                    ?? throw new InvalidOperationException("Struct member scope is incomplete.");
                foreach (SemanticSymbol field in fields.Fields())
                {
                    if (field.IsReference)
                    {
                        return true;
                    }
                    pending.Push(field.Type
                        ?? throw new InvalidOperationException("Struct field type is incomplete."));
                }
            }
        }
        return false;
    }

    internal static partial bool HasFiniteLayout(SemanticType type)
    {
        if (type.Destroyed || type.Analysis is null)
        {
            throw new ArgumentException("A live canonical type is required.", nameof(type));
        }
        System.Collections.Generic.Stack<(SemanticType Type, bool Exit)> pending = new();
        System.Collections.Generic.HashSet<SemanticType> active = new();
        System.Collections.Generic.HashSet<SemanticType> complete = new();
        pending.Push((type, false));
        while (pending.Count != 0)
        {
            (SemanticType current, bool exit) = pending.Pop();
            if (exit)
            {
                active.Remove(current);
                complete.Add(current);
                continue;
            }
            if (complete.Contains(current))
            {
                continue;
            }
            if (!active.Add(current))
            {
                return false;
            }
            pending.Push((current, true));
            if (current.Kind == SemanticTypeKind.Tuple)
            {
                for (int index = current.ComponentCount() - 1; index >= 0; index -= 1)
                {
                    pending.Push((current.ComponentAt(index), false));
                }
            }
            else if (current.Kind == SemanticTypeKind.Nominal
                && current.Declaration!.Kind == SymbolKind.Struct)
            {
                SemanticScope fields = current.Declaration.MemberScope
                    ?? throw new InvalidOperationException("Struct member scope is incomplete.");
                foreach (SemanticSymbol field in fields.Fields())
                {
                    if (!field.IsReference)
                    {
                        SemanticType fieldType = field.Type
                            ?? throw new InvalidOperationException("Struct field type is incomplete.");
                        pending.Push((fieldType, false));
                    }
                }
            }
            // Scalars, arrays, class handles and borrowed fields have fixed-size
            // storage. Following their targets would wrongly reject legal recursion.
        }
        return true;
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
        return IsFloat(from) && IsFloat(to)
            && from.Scalar == ScalarKind.F32 && to.Scalar == ScalarKind.F64;
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

internal sealed partial class SemanticAnalysis
{
    internal partial SemanticType ScalarType(ScalarKind kind)
    {
        if (Destroyed || Compilation is null)
        {
            throw new InvalidOperationException("Cannot obtain types from a retired analysis.");
        }
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return InternType(SemanticTypeKind.Scalar, kind, null, null);
    }

    internal partial SemanticType ClassType(SemanticSymbol declaration)
    {
        if (Destroyed || Compilation is null || declaration.Destroyed
            || declaration.Kind != SymbolKind.Class || declaration.Scope?.Analysis != this)
        {
            throw new InvalidOperationException("Class type belongs to an invalid or different analysis.");
        }
        return InternType(SemanticTypeKind.Nominal, ScalarKind.Unit, null, declaration);
    }

    internal partial SemanticType StructType(SemanticSymbol declaration)
    {
        if (Destroyed || Compilation is null || declaration.Destroyed
            || declaration.Kind != SymbolKind.Struct || declaration.Scope?.Analysis != this)
        {
            throw new InvalidOperationException("Struct type belongs to an invalid or different analysis.");
        }
        return InternType(SemanticTypeKind.Nominal, ScalarKind.Unit, null, declaration);
    }

    internal partial SemanticType[] PredefinedTypes()
    {
        // Keep the language's registration order explicit; enum growth must not
        // silently publish an unimplemented built-in type name.
        return [ScalarType(ScalarKind.Unit), ScalarType(ScalarKind.Bool),
            ScalarType(ScalarKind.U8), ScalarType(ScalarKind.U16),
            ScalarType(ScalarKind.U32), ScalarType(ScalarKind.U64),
            ScalarType(ScalarKind.I8), ScalarType(ScalarKind.I16),
            ScalarType(ScalarKind.I32), ScalarType(ScalarKind.I64),
            ScalarType(ScalarKind.F32), ScalarType(ScalarKind.F64),
            SemanticTypes.ArrayOf(ScalarType(ScalarKind.U8))];
    }
}
