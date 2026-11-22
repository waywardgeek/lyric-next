using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Lyric;

// This module consumes checked syntax and semantic identities. It does not
// resolve names or infer types. All maps below are temporary emission work;
// the existing compilation graph remains the only persistent semantic model.
internal sealed partial class Compilation
{
    internal partial Diagnostic? EmitC(TextWriter output, bool structureOfArrays)
    {
        if (Analysis is null || Analysis.Destroyed)
        {
            throw new InvalidOperationException("Check before emitting C.");
        }
        using StringWriter code = new(CultureInfo.InvariantCulture);
        Dictionary<SemanticSymbol, string> names = new();
        List<SemanticSymbol> functions = new();
        List<SemanticSymbol> classes = new();
        // Emission-only ownership inventory. Each list corresponds to a real C
        // scope; transfers empty descriptors so cleanup cannot free moved storage.
        List<List<(string Name, SemanticType Type)>> owned = new();
        Stack<int> loopScopes = new();
        int temporary = 0;
        int indentation = 0;
        SemanticSymbol? entry = null;
        foreach (SourceFile source in Sources)
        {
            SyntaxNode root = source.SyntaxRoot()!;
            for (int i = 0; i < root.ChildCount(); i += 1)
            {
                SyntaxNode declaration = root.ChildAt(i);
                if (declaration.Kind == SyntaxKind.Class)
                {
                    SemanticSymbol classSymbol = declaration.DeclaredSymbol!;
                    classes.Add(classSymbol);
                    Name(classSymbol);
                    for (int m = 0; m < declaration.ChildCount(); m += 1)
                    {
                        SyntaxNode member = declaration.ChildAt(m);
                        if (member.Kind == SyntaxKind.Modifier && Text(member) == "value")
                        {
                            return source.SemanticDiagnostic(member, "LYC306",
                                "Counted class emission is not implemented yet.");
                        }
                        if (member.DeclaredSymbol is not SemanticSymbol symbol)
                        {
                            continue;
                        }
                        if (symbol.Kind == SymbolKind.Field && symbol.Type!.Value.ArrayDepth > 0)
                        {
                            return source.SemanticDiagnostic(member, "LYC306",
                                "Owned array field emission is not implemented yet.");
                        }
                        if (symbol.Kind == SymbolKind.Function)
                        {
                            functions.Add(symbol);
                            Name(symbol);
                        }
                    }
                    continue;
                }
                SemanticSymbol function = declaration.DeclaredSymbol!;
                functions.Add(function);
                Name(function);
                // Entry selection is orchestration, not call resolution. The
                // checker already established the executable entry contract.
                if (function.Name == "main")
                {
                    entry = function;
                }
            }
        }
        try
        {
            code.WriteLine("""
                #include <stdint.h>
                #include <stddef.h>
                #include <stdbool.h>
                #include <stdio.h>
                #include <stdlib.h>
                #include <signal.h>
                #include <string.h>

                /* Lyric requires checked arithmetic builtins, not plain C11 alone. */
                #ifndef __has_builtin
                #error "Lyric requires a compiler with checked arithmetic builtin detection"
                #else
                #if !__has_builtin(__builtin_add_overflow) || !__has_builtin(__builtin_sub_overflow) || !__has_builtin(__builtin_mul_overflow)
                #error "Lyric requires add/sub/mul overflow builtins"
                #endif
                #endif

                typedef struct { unsigned char unused; } ly_unit;
                typedef struct { void *data; uint64_t length; uint64_t capacity; } ly_array;

                /* Automatic frames form a single-threaded Lyric call trace.
                   Fatal exits deliberately do not unwind language cleanup. */
                typedef struct ly_frame {
                    const char *name;
                    struct ly_frame *parent;
                } ly_frame;
                static ly_frame *ly_current_frame;

                static void ly_overflow(const char *file, int line, int column, const char *operation)
                {
                    fprintf(stderr, "%s:%d:%d: runtime error LYR001: integer overflow in %s\n",
                        file, line, column, operation);
                    for (ly_frame *frame = ly_current_frame; frame != NULL; frame = frame->parent)
                    {
                        fprintf(stderr, "  in %s\n", frame->name);
                    }
                    exit(1);
                }

                static void ly_zero_divisor(const char *file, int line, int column)
                {
                    fprintf(stderr, "%s:%d:%d: runtime error LYR003: integer division or remainder by zero\n",
                        file, line, column);
                    for (ly_frame *frame = ly_current_frame; frame != NULL; frame = frame->parent)
                    {
                        fprintf(stderr, "  in %s\n", frame->name);
                    }
                    exit(1);
                }

                static void ly_shift_count(const char *file, int line, int column)
                {
                    fprintf(stderr, "%s:%d:%d: runtime error LYR004: integer shift count outside operand width\n",
                        file, line, column);
                    for (ly_frame *frame = ly_current_frame; frame != NULL; frame = frame->parent)
                    {
                        fprintf(stderr, "  in %s\n", frame->name);
                    }
                    exit(1);
                }

                static void ly_conversion(const char *file, int line, int column, const char *type)
                {
                    fprintf(stderr, "%s:%d:%d: runtime error LYR005: integer conversion outside %s range\n",
                        file, line, column, type);
                    for (ly_frame *frame = ly_current_frame; frame != NULL; frame = frame->parent)
                    {
                        fprintf(stderr, "  in %s\n", frame->name);
                    }
                    exit(1);
                }

                /* Metadata is unsigned 64-bit, but allocations must also fit the
                   target size_t. Validate before narrowing or multiplying. */
                static void ly_array_failure(const char *file, int line, int column, const char *message)
                {
                    fprintf(stderr, "%s:%d:%d: runtime error %s\n", file, line, column, message);
                    for (ly_frame *frame = ly_current_frame; frame != NULL; frame = frame->parent)
                    {
                        fprintf(stderr, "  in %s\n", frame->name);
                    }
                    exit(1);
                }

                static ly_array ly_array_new(uint64_t count, size_t stride,
                    const char *file, int line, int column)
                {
                    if (count > SIZE_MAX / stride)
                    {
                        ly_array_failure(file, line, column, "LYR007: array allocation size overflow");
                    }
                    ly_array result = {NULL, 0, count};
                    if (count != 0)
                    {
                        result.data = malloc((size_t)count * stride);
                        if (result.data == NULL)
                        {
                            ly_array_failure(file, line, column, "LYR008: out of memory");
                        }
                    }
                    return result;
                }

                /* Geometric growth is an implementation policy, not array semantics.
                   Keep the original pointer until realloc succeeds. Spare capacity
                   contains no live values and is never traversed by cleanup. */
                static void ly_array_reserve(ly_array *array, uint64_t required, size_t stride,
                    const char *file, int line, int column)
                {
                    uint64_t limit = SIZE_MAX / stride;
                    if (required > limit)
                    {
                        ly_array_failure(file, line, column, "LYR007: array allocation size overflow");
                    }
                    if (required <= array->capacity)
                    {
                        return;
                    }
                    uint64_t increment = array->capacity / 2;
                    if (increment == 0)
                    {
                        increment = 1;
                    }
                    uint64_t capacity = increment > limit - array->capacity
                        ? limit : array->capacity + increment;
                    if (capacity < required)
                    {
                        capacity = required;
                    }
                    void *replacement = realloc(array->data, (size_t)capacity * stride);
                    if (replacement == NULL)
                    {
                        ly_array_failure(file, line, column, "LYR008: out of memory");
                    }
                    array->data = replacement;
                    array->capacity = capacity;
                }

                /* Only initialized elements are traversed; spare slots never own
                   children. Resetting after release makes moved/empty slots safe. */
                static void ly_array_drop(ly_array *array, unsigned depth)
                {
                    if (depth > 1)
                    {
                        ly_array *children = array->data;
                        for (uint64_t i = 0; i < array->length; i += 1)
                        {
                            ly_array_drop(&children[i], depth - 1);
                        }
                    }
                    free(array->data);
                    *array = (ly_array){0};
                }

                static ly_array ly_array_copy(ly_array source, unsigned depth, size_t scalar_size,
                    const char *file, int line, int column)
                {
                    size_t stride = depth > 1 ? sizeof(ly_array) : scalar_size;
                    ly_array result = ly_array_new(source.length, stride, file, line, column);
                    if (depth > 1)
                    {
                        ly_array *from = source.data;
                        ly_array *to = result.data;
                        for (uint64_t i = 0; i < source.length; i += 1)
                        {
                            to[i] = ly_array_copy(from[i], depth - 1, scalar_size, file, line, column);
                            result.length += 1;
                        }
                    }
                    else if (source.length != 0)
                    {
                        memcpy(result.data, source.data, (size_t)source.length * stride);
                        result.length = source.length;
                    }
                    return result;
                }

                /* Length-based output preserves arbitrary bytes, including NUL.
                   Flush here so a buffered write failure cannot become success. */
                static ly_unit ly_print(ly_array text, bool newline, const char *file, int line, int column)
                {
                    if ((text.length != 0 && fwrite(text.data, 1, text.length, stdout) != text.length)
                        || (newline && fputc('\n', stdout) == EOF)
                        || fflush(stdout) == EOF)
                    {
                        fprintf(stderr, "%s:%d:%d: runtime error LYR002: output failure\n",
                            file, line, column);
                        for (ly_frame *frame = ly_current_frame; frame != NULL; frame = frame->parent)
                        {
                            fprintf(stderr, "  in %s\n", frame->name);
                        }
                        exit(1);
                    }
                    return (ly_unit){0};
                }
                """);
            ArithmeticHelpers();
            foreach (SemanticSymbol classSymbol in classes)
            {
                ClassStorage(classSymbol);
            }
            foreach (SemanticSymbol function in functions)
            {
                Line(Signature(function) + ";");
            }
            foreach (SemanticSymbol function in functions)
            {
                Line(Signature(function));
                SyntaxNode declaration = function.Declaration!;
                for (int i = 0; i < declaration.ChildCount(); i += 1)
                {
                    if (declaration.ChildAt(i).Kind == SyntaxKind.Block)
                    {
                        Block(declaration.ChildAt(i), function.Type == SemanticType.Unit, function);
                    }
                }
            }
            Line("int main(void)");
            Line("{");
            indentation += 1;
            // POSIX may signal before stdio can report a broken pipe. This is
            // executable startup, not a hidden library-wide signal change.
            Line("#ifdef SIGPIPE");
            Line("if (signal(SIGPIPE, SIG_IGN) == SIG_ERR)");
            Line("{");
            Line("    fputs(\"Lyric runtime error: cannot configure output signal handling\\n\", stderr);");
            Line("    return 1;");
            Line("}");
            Line("#endif");
            Line($"(void){Name(entry!)}();");
            Line("return 0;");
            indentation -= 1;
            Line("}");
        }
        catch (SemanticFailure failure)
        {
            return failure.Diagnostic;
        }
        output.Write(code.ToString());
        return null;

        void Line(string text)
        {
            code.Write(new string(' ', indentation * 4));
            code.WriteLine(text);
        }
        string Name(SemanticSymbol symbol)
        {
            if (!names.TryGetValue(symbol, out string? name))
            {
                name = "ly_s" + names.Count.ToString(CultureInfo.InvariantCulture);
                names.Add(symbol, name);
            }
            return name;
        }
        string CType(SemanticType type)
        {
            if (type.ArrayDepth > 0)
            {
                return "ly_array";
            }
            if (type.Class is not null)
            {
                return "uint64_t";
            }
            return type.Scalar switch
            {
                ScalarKind.Unit => "ly_unit",
                ScalarKind.Bool => "bool",
                ScalarKind.F32 => "float",
                ScalarKind.F64 => "double",
                _ => (SemanticTypes.IsSigned(type) ? "int" : "uint")
                    + SemanticTypes.IntegerWidth(type).ToString(CultureInfo.InvariantCulture) + "_t",
            };
        }
        string Location(SyntaxNode node)
        {
            Diagnostic location = node.Source!.SemanticDiagnostic(node, "", "");
            return $"{CString(JsonSerializer.Serialize(node.Source.Path))}, {location.Line}, {location.Column}";
        }
        // These lists and emitted names are temporary backend work, not another
        // semantic owner. Every field identity comes from its checked declaration.
        List<SemanticSymbol> Fields(SemanticSymbol owner)
        {
            List<SemanticSymbol> fields = new();
            SyntaxNode declaration = owner.Declaration!;
            for (int i = 0; i < declaration.ChildCount(); i += 1)
            {
                SemanticSymbol? field = declaration.ChildAt(i).DeclaredSymbol;
                if (field?.Kind == SymbolKind.Field)
                {
                    fields.Add(field);
                }
            }
            return fields;
        }
        string FieldStorage(SemanticSymbol field, string index)
        {
            string owner = Name(field.Scope!.Class!);
            if (structureOfArrays)
            {
                return $"(({CType(field.Type!.Value)} *){Name(field)}_storage.data)[{index}]";
            }
            return $"(({owner}_record **){owner}_chunks.data)[{index} / 64][{index} % 64].{Name(field)}";
        }
        void ClassStorage(SemanticSymbol owner)
        {
            string name = Name(owner);
            List<SemanticSymbol> fields = Fields(owner);
            Line($"static uint64_t {name}_next;");
            // Metadata is private allocator storage: moving it cannot invalidate
            // source field borrows. Both payload layouts share this free list.
            Line($"typedef struct {name}_slot {{ uint64_t next_free; bool live; }} {name}_slot;");
            Line($"static ly_array {name}_slots;");
            Line($"static uint64_t {name}_free = UINT64_MAX;");
            if (structureOfArrays)
            {
                foreach (SemanticSymbol field in fields)
                {
                    Line($"static ly_array {Name(field)}_storage;");
                }
            }
            else
            {
                // The directory can move, but each separately allocated fixed
                // chunk remains at its original address for its entire lifetime.
                Line($"typedef struct {name}_record {{");
                indentation += 1;
                if (fields.Count == 0)
                {
                    Line("unsigned char unused;");
                }
                foreach (SemanticSymbol field in fields)
                {
                    Line($"{CType(field.Type!.Value)} {Name(field)};");
                }
                indentation -= 1;
                Line($"}} {name}_record;");
                Line($"static ly_array {name}_chunks;");
            }
            Line($"static uint64_t {name}_allocate(const char *file, int line, int column)");
            Line("{");
            indentation += 1;
            Line("uint64_t index;");
            Line($"if ({name}_free != UINT64_MAX)");
            Line("{");
            indentation += 1;
            Line($"index = {name}_free;");
            Line($"{name}_free = (({name}_slot *){name}_slots.data)[index].next_free;");
            indentation -= 1;
            Line("}");
            Line("else");
            Line("{");
            indentation += 1;
            Line($"if ({name}_next == UINT64_MAX)");
            Line("{");
            Line("    ly_array_failure(file, line, column, \"LYR007: class allocation size overflow\");");
            Line("}");
            Line($"index = {name}_next;");
            Line($"ly_array_reserve(&{name}_slots, index + 1, sizeof({name}_slot), file, line, column);");
            if (structureOfArrays)
            {
                foreach (SemanticSymbol field in fields)
                {
                    Line($"ly_array_reserve(&{Name(field)}_storage, index + 1, sizeof({CType(field.Type!.Value)}), file, line, column);");
                }
            }
            else
            {
                Line("if (index % 64 == 0)");
                Line("{");
                indentation += 1;
                Line($"ly_array_reserve(&{name}_chunks, index / 64 + 1, sizeof({name}_record *), file, line, column);");
                Line($"ly_array chunk = ly_array_new(64, sizeof({name}_record), file, line, column);");
                Line($"(({name}_record **){name}_chunks.data)[index / 64] = chunk.data;");
                indentation -= 1;
                Line("}");
            }
            Line($"{name}_next = index + 1;");
            indentation -= 1;
            Line("}");
            Line($"(({name}_slot *){name}_slots.data)[index].live = true;");
            foreach (SemanticSymbol field in fields)
            {
                Line($"{FieldStorage(field, "index")} = ({CType(field.Type!.Value)}){{0}};");
            }
            Line("return index;");
            indentation -= 1;
            Line("}");
            Line($"static ly_unit {name}_destroy(uint64_t index, const char *file, int line, int column)");
            Line("{");
            indentation += 1;
            // This guard is defensive; static analysis must reject stale source
            // uses. Slot reuse does not make an old source handle valid again.
            Line($"if (index >= {name}_next || !(({name}_slot *){name}_slots.data)[index].live)");
            Line("{");
            Line("    ly_array_failure(file, line, column, \"LYR009: invalid object destruction\");");
            Line("}");
            Line($"(({name}_slot *){name}_slots.data)[index].live = false;");
            Line($"(({name}_slot *){name}_slots.data)[index].next_free = {name}_free;");
            Line($"{name}_free = index;");
            Line("return (ly_unit){0};");
            indentation -= 1;
            Line("}");
        }
        string Copy(string value, SemanticType type, SyntaxNode node)
        {
            string scalar = CType(new SemanticType(type.Scalar, 0));
            return $"ly_array_copy({value}, {type.ArrayDepth}, sizeof({scalar}), {Location(node)})";
        }
        void Own(string name, SemanticType type)
        {
            if (type.ArrayDepth > 0)
            {
                owned[^1].Add((name, type));
            }
        }
        void Moved(string name, SemanticType type)
        {
            if (type.ArrayDepth > 0)
            {
                Line($"{name} = (ly_array){{0}};");
            }
        }
        void Cleanup(int firstScope, string? except = null)
        {
            for (int i = owned.Count - 1; i >= firstScope; i -= 1)
            {
                for (int j = owned[i].Count - 1; j >= 0; j -= 1)
                {
                    var value = owned[i][j];
                    if (value.Name != except)
                    {
                        Line($"ly_array_drop(&{value.Name}, {value.Type.ArrayDepth});");
                    }
                }
            }
        }
        // Select existing storage, never a value-copy temporary. Index expressions
        // are evaluated once, left to right, before the assignment RHS.
        string Storage(SyntaxNode node)
        {
            if (node.Kind == SyntaxKind.Name)
            {
                SemanticSymbol symbol = node.Fact!.Symbol!;
                return symbol.IsReference ? $"(*{Name(symbol)})" : Name(symbol);
            }
            if (node.Kind == SyntaxKind.Member)
            {
                // Preserve identity across RHS effects, not an interior address.
                string receiver = Expression(node.ChildAt(0));
                return FieldStorage(node.Fact!.Symbol!, receiver);
            }
            string array = Storage(node.ChildAt(0));
            string index = Expression(node.ChildAt(1));
            Bounds(array, index, node);
            string pointer = "ly_p" + temporary.ToString(CultureInfo.InvariantCulture);
            temporary += 1;
            string element = CType(node.Fact!.Type);
            Line($"{element} *{pointer} = &(({element} *)({array}).data)[(uint64_t){index}];");
            return "(*" + pointer + ")";
        }
        void Bounds(string array, string index, SyntaxNode node)
        {
            string invalid = $"(uint64_t){index} >= ({array}).length";
            if (SemanticTypes.IsSigned(node.ChildAt(1).Fact!.Type))
            {
                invalid = $"{index} < 0 || " + invalid;
            }
            Line($"if ({invalid})");
            Line("{");
            indentation += 1;
            Line($"ly_array_failure({Location(node)}, \"LYR006: array index out of bounds\");");
            indentation -= 1;
            Line("}");
        }
        string Signature(SemanticSymbol function)
        {
            List<string> parameters = new();
            SemanticScope scope = function.FunctionScope!;
            for (int i = 0; i < scope.ParameterCount(); i += 1)
            {
                SemanticSymbol parameter = scope.ParameterAt(i);
                string pointer = parameter.IsReference ? " *" : " ";
                parameters.Add(CType(parameter.Type!.Value) + pointer + Name(parameter));
            }
            string arguments = parameters.Count == 0 ? "void" : string.Join(", ", parameters);
            return $"static {CType(function.Type!.Value)} {Name(function)}({arguments})";
        }
        void Unsupported(SyntaxNode node)
        {
            Diagnostic diagnostic = node.Source!.SemanticDiagnostic(node,
                "LYC401", $"Unsupported C emission construct: {node.Kind}.");
            throw new SemanticFailure(diagnostic);
        }
        string Text(SyntaxNode node)
        {
            return node.Source!.SourceText(node.TextOffset, node.TextLength);
        }
        string Integer(BigInteger value)
        {
            if (value == long.MinValue)
            {
                // The positive magnitude is not a signed C constant.
                return "(-INT64_C(9223372036854775807) - INT64_C(1))";
            }
            if (value < 0)
            {
                return "(-INT64_C(" + BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture) + "))";
            }
            return "UINT64_C(" + value.ToString(CultureInfo.InvariantCulture) + ")";
        }
        string CString(string text)
        {
            // Fixed three-digit octal escapes cannot absorb following digits.
            // Encode bytes explicitly so C source/execution character sets cannot
            // change diagnostic paths or names. JSON quoting makes display safe.
            StringBuilder escaped = new("\"");
            foreach (byte value in Encoding.UTF8.GetBytes(text))
            {
                escaped.Append('\\');
                escaped.Append(Convert.ToString(value, 8).PadLeft(3, '0'));
            }
            escaped.Append('"');
            return escaped.ToString();
        }
        void ArithmeticHelpers()
        {
            foreach (SemanticType type in SemanticTypes.Predefined())
            {
                if (!SemanticTypes.IsInteger(type))
                {
                    continue;
                }
                string kind = SemanticTypes.Name(type);
                string ctype = CType(type);
                string maximum = Integer(SemanticTypes.Maximum(type));
                string minimum = Integer(SemanticTypes.Minimum(type));
                // Cast representable bounds to the actual operand type. In
                // particular a UINT64_C positive spelling must not turn signed
                // comparisons or divisions into unsigned arithmetic.
                string low = $"(({ctype}){minimum})";
                int width = SemanticTypes.IntegerWidth(type);
                string mask = Integer((BigInteger.One << width) - 1);
                // Unsigned arithmetic is defined modulo 2^64. Masking preserves
                // exactly the low N bits, also for the narrower Lyric types.
                Line($"static {ctype} ly_{kind}_bits(uint64_t bits)");
                Line("{");
                indentation += 1;
                Line($"bits &= {mask};");
                if (SemanticTypes.IsSigned(type))
                {
                    Line($"if (bits > {maximum})");
                    Line("{");
                    indentation += 1;
                    // The complement fits the signed positive range. Unlike a
                    // direct cast of bits, this conversion is always representable.
                    Line($"return ({ctype})(-INT64_C(1) - (int64_t)((~bits) & {mask}));");
                    indentation -= 1;
                    Line("}");
                }
                Line($"return ({ctype})bits;");
                indentation -= 1;
                Line("}");
                foreach (string operation in new[] { "add", "sub", "mul" })
                {
                    string symbol = operation == "add" ? "+" : operation == "sub" ? "-" : "*";
                    Line($"static {ctype} ly_{kind}_wrap_{operation}({ctype} a, {ctype} b)");
                    Line("{");
                    indentation += 1;
                    Line($"return ly_{kind}_bits((uint64_t)a {symbol} (uint64_t)b);");
                    indentation -= 1;
                    Line("}");
                }
                // The expression site validates the independently typed count
                // before conversion. Helpers therefore receive only 0 <= count < N.
                foreach (string operation in new[] { "shl", "shr" })
                {
                    Line($"static {ctype} ly_{kind}_{operation}({ctype} a, uint64_t count)");
                    Line("{");
                    indentation += 1;
                    Line($"uint64_t bits = ((uint64_t)a) & {mask};");
                    if (operation == "shl")
                    {
                        Line("bits <<= count;");
                    }
                    else
                    {
                        Line("bits >>= count;");
                        if (SemanticTypes.IsSigned(type))
                        {
                            Line("if (a < 0 && count != 0)");
                            Line("{");
                            indentation += 1;
                            Line($"bits |= {mask} ^ ({mask} >> count);");
                            indentation -= 1;
                            Line("}");
                        }
                    }
                    Line($"return ly_{kind}_bits(bits);");
                    indentation -= 1;
                    Line("}");
                }
                // Guard both C division and remainder: MIN % -1 is undefined
                // in C too, although Lyric defines its result as zero.
                foreach (string operation in new[] { "div", "rem" })
                {
                    string symbol = operation == "div" ? "/" : "%";
                    Line($"static {ctype} ly_{kind}_{operation}({ctype} a, {ctype} b, const char *file, int line, int column)");
                    Line("{");
                    indentation += 1;
                    Line("if (b == 0) { ly_zero_divisor(file, line, column); }");
                    if (SemanticTypes.IsSigned(type))
                    {
                        Line($"if (a == {low} && b == -1)");
                        Line("{");
                        indentation += 1;
                        if (operation == "rem")
                        {
                            Line("return 0;");
                        }
                        else
                        {
                            Line($"ly_overflow(file, line, column, \"{kind} division\");");
                        }
                        indentation -= 1;
                        Line("}");
                    }
                    Line($"return ({ctype})(a {symbol} b);");
                    indentation -= 1;
                    Line("}");
                }
                foreach (string operation in new[] { "add", "sub", "mul" })
                {
                    string description = operation switch
                    {
                        "add" => "addition",
                        "sub" => "subtraction",
                        _ => "multiplication"
                    };
                    // The destination pointer sets the checked width, even when
                    // C would promote narrow operands. The builtin reports overflow
                    // without executing an undefined signed arithmetic operation.
                    Line($"static {ctype} ly_{kind}_{operation}({ctype} a, {ctype} b, const char *file, int line, int column)");
                    Line("{");
                    indentation += 1;
                    Line($"{ctype} result;");
                    Line($"if (__builtin_{operation}_overflow(a, b, &result))");
                    Line("{");
                    indentation += 1;
                    Line($"ly_overflow(file, line, column, \"{kind} {description}\");");
                    indentation -= 1;
                    Line("}");
                    Line("return result;");
                    indentation -= 1;
                    Line("}");
                }
            }
        }
        // Receiver selection precedes argument evaluation. The checker protects
        // nested selections against invalidation during that interval.
        string ArrayMethod(SyntaxNode node)
        {
            SyntaxNode receiver = node.ChildAt(0).ChildAt(0);
            SemanticType arrayType = receiver.Fact!.Type;
            SemanticType elementType = SemanticTypes.ElementOf(arrayType);
            string element = CType(elementType);
            string storage = Storage(receiver);
            string pointer = "ly_a" + temporary.ToString(CultureInfo.InvariantCulture);
            temporary += 1;
            Line($"ly_array *{pointer} = &{storage};");
            string argument = Expression(node.ChildAt(1));
            ArrayOperation operation = node.Fact!.ArrayOperation;
            if (operation == ArrayOperation.Push)
            {
                Line($"if ({pointer}->length == UINT64_MAX)");
                Line("{");
                indentation += 1;
                Line($"ly_array_failure({Location(node)}, \"LYR007: array allocation size overflow\");");
                indentation -= 1;
                Line("}");
                Line($"ly_array_reserve({pointer}, {pointer}->length + 1, sizeof({element}), {Location(node)});");
                Line($"(({element} *){pointer}->data)[{pointer}->length] = {argument};");
                Line($"{pointer}->length += 1;");
                Moved(argument, elementType);
            }
            else if (operation == ArrayOperation.Reserve)
            {
                Line($"ly_array_reserve({pointer}, {argument}, sizeof({element}), {Location(node)});");
            }
            else
            {
                // Shrink first, releasing each removed owning child exactly once.
                Line($"while ({pointer}->length > {argument})");
                Line("{");
                indentation += 1;
                Line($"{pointer}->length -= 1;");
                if (elementType.ArrayDepth > 0)
                {
                    Line($"ly_array_drop(&((ly_array *){pointer}->data)[{pointer}->length], {elementType.ArrayDepth});");
                }
                indentation -= 1;
                Line("}");
                if (operation == ArrayOperation.Resize)
                {
                    Line($"ly_array_reserve({pointer}, {argument}, sizeof({element}), {Location(node)});");
                    Line($"while ({pointer}->length < {argument})");
                    Line("{");
                    indentation += 1;
                    Line($"(({element} *){pointer}->data)[{pointer}->length] = ({element}){{0}};");
                    Line($"{pointer}->length += 1;");
                    indentation -= 1;
                    Line("}");
                }
            }
            string result = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
            temporary += 1;
            Line($"ly_unit {result} = {{0}};");
            return result;
        }
        string Expression(SyntaxNode root)
        {
            Dictionary<SyntaxNode, string> values = new();
            Stack<(SyntaxNode Node, int Stage)> work = new();
            work.Push((root, 0));
            while (work.Count != 0)
            {
                var item = work.Pop();
                SyntaxNode node = item.Node;
                SemanticFact fact = node.Fact!;
                if (node.Kind == SyntaxKind.Unary && Text(node) == "ref")
                {
                    // Address acquisition is not a value read or an owned copy.
                    string pointer = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
                    temporary += 1;
                    Line($"{CType(fact.Type)} *{pointer} = &{Storage(node.ChildAt(0))};");
                    values.Add(node, pointer);
                    continue;
                }
                if (fact.ClassOperation == ClassOperation.Destroy)
                {
                    string receiver = Expression(node.ChildAt(0).ChildAt(0));
                    string destroyed = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
                    temporary += 1;
                    Line($"ly_unit {destroyed} = {Name(fact.Symbol!)}_destroy({receiver}, {Location(node)});");
                    values.Add(node, destroyed);
                    continue;
                }
                if (fact.ArrayOperation != ArrayOperation.None)
                {
                    values.Add(node, ArrayMethod(node));
                    continue;
                }
                if (node.Kind == SyntaxKind.If)
                {
                    string branchResult = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
                    temporary += 1;
                    Line($"{CType(fact.Type)} {branchResult} = {{0}};");
                    Own(branchResult, fact.Type);
                    Conditional(node, branchResult);
                    values.Add(node, branchResult);
                    continue;
                }
                if (node.Kind == SyntaxKind.Binary && Text(node) is "&&" or "||")
                {
                    // The result outlives the conditional C block. Only the
                    // right operand's statements belong inside that block.
                    if (item.Stage == 0)
                    {
                        work.Push((node, 1));
                        work.Push((node.ChildAt(0), 0));
                    }
                    else if (item.Stage == 1)
                    {
                        string logicalResult = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
                        temporary += 1;
                        values.Add(node, logicalResult);
                        Line($"bool {logicalResult} = {values[node.ChildAt(0)]};");
                        string condition = Text(node) == "&&" ? logicalResult : "!" + logicalResult;
                        Line($"if ({condition})");
                        Line("{");
                        indentation += 1;
                        owned.Add(new());
                        work.Push((node, 2));
                        work.Push((node.ChildAt(1), 0));
                    }
                    else
                    {
                        Line($"{values[node]} = {values[node.ChildAt(1)]};");
                        Cleanup(owned.Count - 1);
                        owned.RemoveAt(owned.Count - 1);
                        indentation -= 1;
                        Line("}");
                    }
                    continue;
                }
                // A known constant has no effects in this supported language.
                // Its checked exact value also avoids host/C literal overflow.
                if (item.Stage == 0 && fact.IntegerConstant is null
                    && node.Kind is SyntaxKind.Call or SyntaxKind.Group or SyntaxKind.Binary or SyntaxKind.Unary or SyntaxKind.Cast or SyntaxKind.Array or SyntaxKind.Index or SyntaxKind.Member)
                {
                    work.Push((node, 1));
                    int first = node.Kind == SyntaxKind.Call ? 1 : 0;
                    // A cast's type child has no runtime evaluation or value fact.
                    int last = node.Kind == SyntaxKind.Cast ? 0 : node.ChildCount() - 1;
                    for (int i = last; i >= first; i -= 1)
                    {
                        work.Push((node.ChildAt(i), 0));
                    }
                    if (node.Kind == SyntaxKind.Call && node.ChildAt(0).Kind == SyntaxKind.Member)
                    {
                        // LIFO scheduling: receiver executes before explicit arguments.
                        work.Push((node.ChildAt(0).ChildAt(0), 0));
                    }
                    continue;
                }
                string expression;
                if (fact.IntegerConstant is not null)
                {
                    expression = Integer(fact.IntegerConstant.Value);
                }
                else
                {
                    switch (node.Kind)
                    {
                        case SyntaxKind.Name:
                            expression = fact.Type.ArrayDepth > 0
                                ? Copy(Storage(node), fact.Type, node)
                                : Storage(node);
                            break;
                        case SyntaxKind.Member:
                            expression = FieldStorage(fact.Symbol!, values[node.ChildAt(0)]);
                            break;
                        case SyntaxKind.String:
                            ReadOnlySpan<byte> bytes = node.Source!.LiteralBytes(node.TextOffset, node.TextLength);
                            List<string> elements = new();
                            foreach (byte value in bytes)
                            {
                                elements.Add(value.ToString(CultureInfo.InvariantCulture));
                            }
                            // A sentinel makes the empty array legal C; it is not
                            // part of the Lyric value and never controls output.
                            elements.Add("0");
                            string storage = "ly_b" + temporary.ToString(CultureInfo.InvariantCulture);
                            temporary += 1;
                            Line($"static const unsigned char {storage}[] = {{ {string.Join(", ", elements)} }};");
                            expression = Copy($"(ly_array){{(void *){storage}, {bytes.Length.ToString(CultureInfo.InvariantCulture)}, {bytes.Length.ToString(CultureInfo.InvariantCulture)}}}", fact.Type, node);
                            break;
                        case SyntaxKind.Array:
                            SemanticType elementType = SemanticTypes.ElementOf(fact.Type);
                            string array = "ly_a" + temporary.ToString(CultureInfo.InvariantCulture);
                            temporary += 1;
                            Line($"ly_array {array} = ly_array_new({node.ChildCount()}, sizeof({CType(elementType)}), {Location(node)});");
                            for (int i = 0; i < node.ChildCount(); i += 1)
                            {
                                string child = values[node.ChildAt(i)];
                                Line($"(({CType(elementType)} *){array}.data)[{i}] = {child};");
                                Moved(child, elementType);
                                Line($"{array}.length += 1;");
                            }
                            expression = array;
                            break;
                        case SyntaxKind.Index:
                            string container = values[node.ChildAt(0)];
                            string subscript = values[node.ChildAt(1)];
                            Bounds(container, subscript, node);
                            string selected = $"(({CType(fact.Type)} *){container}.data)[(uint64_t){subscript}]";
                            expression = fact.Type.ArrayDepth > 0 ? Copy(selected, fact.Type, node) : selected;
                            break;
                        case SyntaxKind.Literal:
                            expression = Text(node);
                            break;
                        case SyntaxKind.Unit:
                            expression = "(ly_unit){0}";
                            break;
                        case SyntaxKind.Cast:
                            string castValue = values[node.ChildAt(0)];
                            string destination = SemanticTypes.Name(fact.Type);
                            if (Text(node) == "!as")
                            {
                                // Signed-to-unsigned conversion is defined modulo
                                // 2^64, preserving negative mathematical values for
                                // subsequent masking to any destination width.
                                expression = $"ly_{destination}_bits((uint64_t){castValue})";
                            }
                            else
                            {
                                string castHigh = Integer(SemanticTypes.Maximum(fact.Type));
                                string outside = $"(uint64_t){castValue} > {castHigh}";
                                if (SemanticTypes.IsSigned(node.ChildAt(0).Fact!.Type))
                                {
                                    if (SemanticTypes.IsSigned(fact.Type))
                                    {
                                        string castLow = Integer(SemanticTypes.Minimum(fact.Type));
                                        outside = $"{castValue} < 0 ? (int64_t){castValue} < {castLow} : ({outside})";
                                    }
                                    else
                                    {
                                        outside = $"{castValue} < 0 || ({outside})";
                                    }
                                }
                                Diagnostic castLocation = node.Source!.SemanticDiagnostic(node, "", "");
                                string castFile = CString(JsonSerializer.Serialize(node.Source.Path));
                                Line($"if ({outside})");
                                Line("{");
                                indentation += 1;
                                Line($"ly_conversion({castFile}, {castLocation.Line}, {castLocation.Column}, {CString(destination)});");
                                indentation -= 1;
                                Line("}");
                                // The guard establishes representability before C
                                // performs any potentially narrowing signed cast.
                                expression = $"({CType(fact.Type)}){castValue}";
                            }
                            break;
                        case SyntaxKind.Group:
                            expression = values[node.ChildAt(0)];
                            break;
                        case SyntaxKind.Unary when Text(node) == "-":
                            Diagnostic negationLocation = node.Source!.SemanticDiagnostic(node, "", "");
                            string negationFile = CString(JsonSerializer.Serialize(node.Source.Path));
                            expression = $"ly_{SemanticTypes.Name(fact.Type)}_sub(0, {values[node.ChildAt(0)]}, "
                                + $"{negationFile}, {negationLocation.Line}, {negationLocation.Column})";
                            break;
                        case SyntaxKind.Unary when Text(node) == "!-":
                            expression = $"ly_{SemanticTypes.Name(fact.Type)}_wrap_sub(0, {values[node.ChildAt(0)]})";
                            break;
                        case SyntaxKind.Unary when Text(node) == "!":
                            expression = "!" + values[node.ChildAt(0)];
                            break;
                        case SyntaxKind.Binary when Text(node) is "==" or "!=" or "<" or "<=" or ">" or ">=":
                            string leftValue = values[node.ChildAt(0)];
                            string rightValue = values[node.ChildAt(1)];
                            bool leftSigned = SemanticTypes.IsSigned(node.ChildAt(0).Fact!.Type);
                            bool rightSigned = SemanticTypes.IsSigned(node.ChildAt(1).Fact!.Type);
                            string comparison = Text(node);
                            if (node.ChildAt(0).Fact!.Type == SemanticType.Bool)
                            {
                                expression = $"({leftValue} {comparison} {rightValue})";
                            }
                            else if (leftSigned == rightSigned)
                            {
                                string wideType = leftSigned ? "int64_t" : "uint64_t";
                                expression = $"(({wideType}){leftValue} {comparison} ({wideType}){rightValue})";
                            }
                            else
                            {
                                // Test negativity before converting to magnitude.
                                // Every nonnegative signed value fits uint64_t.
                                string signedValue = leftSigned ? leftValue : rightValue;
                                bool negativeResult = comparison switch
                                {
                                    "!=" => true,
                                    "<" or "<=" => leftSigned,
                                    ">" or ">=" => rightSigned,
                                    _ => false,
                                };
                                string negativeText = negativeResult ? "true" : "false";
                                expression = $"({signedValue} < 0 ? {negativeText} : "
                                    + $"((uint64_t){leftValue} {comparison} (uint64_t){rightValue}))";
                            }
                            break;
                        case SyntaxKind.Binary when Text(node) is "<<" or ">>":
                            string count = values[node.ChildAt(1)];
                            int shiftWidth = SemanticTypes.IntegerWidth(fact.Type);
                            string invalidCount = $"(uint64_t){count} >= UINT64_C({shiftWidth})";
                            if (SemanticTypes.IsSigned(node.ChildAt(1).Fact!.Type))
                            {
                                invalidCount = $"{count} < 0 || " + invalidCount;
                            }
                            Diagnostic shiftLocation = node.Source!.SemanticDiagnostic(node, "", "");
                            string shiftFile = CString(JsonSerializer.Serialize(node.Source.Path));
                            Line($"if ({invalidCount})");
                            Line("{");
                            indentation += 1;
                            Line($"ly_shift_count({shiftFile}, {shiftLocation.Line}, {shiftLocation.Column});");
                            indentation -= 1;
                            Line("}");
                            string shiftOperation = Text(node) == "<<" ? "shl" : "shr";
                            expression = $"ly_{SemanticTypes.Name(fact.Type)}_{shiftOperation}("
                                + $"{values[node.ChildAt(0)]}, (uint64_t){count})";
                            break;
                        case SyntaxKind.Binary when Text(node) is "&" or "|" or "^":
                            // Unsigned conversion preserves low two's-complement
                            // bits. Reconstruction masks to the selected width
                            // and avoids an out-of-range signed C conversion.
                            expression = $"ly_{SemanticTypes.Name(fact.Type)}_bits("
                                + $"(uint64_t){values[node.ChildAt(0)]} {Text(node)} "
                                + $"(uint64_t){values[node.ChildAt(1)]})";
                            break;
                        case SyntaxKind.Binary when Text(node) is "!+" or "!-" or "!*":
                            string wrappingOperation = Text(node) switch
                            {
                                "!+" => "add",
                                "!-" => "sub",
                                _ => "mul",
                            };
                            expression = $"ly_{SemanticTypes.Name(fact.Type)}_wrap_{wrappingOperation}("
                                + $"{values[node.ChildAt(0)]}, {values[node.ChildAt(1)]})";
                            break;
                        case SyntaxKind.Binary:
                            string operation = Text(node) switch
                            {
                                "+" => "add",
                                "-" => "sub",
                                "*" => "mul",
                                "/" => "div",
                                "%" => "rem",
                                _ => throw new InvalidOperationException("Unchecked arithmetic operator."),
                            };
                            Diagnostic location = node.Source!.SemanticDiagnostic(node, "", "");
                            string file = CString(JsonSerializer.Serialize(node.Source.Path));
                            expression = $"ly_{SemanticTypes.Name(fact.Type)}_{operation}("
                                + $"{values[node.ChildAt(0)]}, {values[node.ChildAt(1)]}, "
                                + $"{file}, {location.Line}, {location.Column})";
                            break;
                        case SyntaxKind.Call:
                            SemanticSymbol target = fact.Symbol!;
                            List<string> arguments = new();
                            for (int i = 1; i < node.ChildCount(); i += 1)
                            {
                                arguments.Add(values[node.ChildAt(i)]);
                            }
                            if (target.Builtin == BuiltinKind.Length)
                            {
                                expression = $"{arguments[0]}.length";
                            }
                            else if (target.Builtin != BuiltinKind.None)
                            {
                                string newline = target.Builtin == BuiltinKind.Println ? "true" : "false";
                                Diagnostic callLocation = node.Source!.SemanticDiagnostic(node, "", "");
                                string callFile = CString(JsonSerializer.Serialize(node.Source.Path));
                                expression = $"ly_print({arguments[0]}, {newline}, {callFile}, "
                                    + $"{callLocation.Line}, {callLocation.Column})";
                            }
                            else
                            {
                                SemanticSymbol? owner = target.Scope!.Class;
                                if (owner is not null && node.ChildAt(0).Kind == SyntaxKind.Name)
                                {
                                    string instance = "ly_object" + temporary.ToString(CultureInfo.InvariantCulture);
                                    temporary += 1;
                                    Line($"uint64_t {instance} = {Name(owner)}_allocate({Location(node)});");
                                    arguments.Insert(0, instance);
                                    Line($"(void){Name(target)}({string.Join(", ", arguments)});");
                                    expression = instance;
                                }
                                else
                                {
                                    if (owner is not null)
                                    {
                                        arguments.Insert(0, values[node.ChildAt(0).ChildAt(0)]);
                                    }
                                    expression = $"{Name(target)}({string.Join(", ", arguments)})";
                                }
                            }
                            break;
                        default:
                            Unsupported(node);
                            return "";
                    }
                }
                string result = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
                temporary += 1;
                Line($"{CType(fact.Type)} {result} = {expression};");
                Own(result, fact.Type);
                if (node.Kind == SyntaxKind.Group && fact.Type.ArrayDepth > 0)
                {
                    Moved(values[node.ChildAt(0)], fact.Type);
                }
                if (node.Kind == SyntaxKind.Call && fact.Symbol!.Builtin == BuiltinKind.None)
                {
                    // The callee owns the independent argument values, including
                    // cleanup on return. Empty caller slots after that transfer.
                    for (int i = 1; i < node.ChildCount(); i += 1)
                    {
                        if (!fact.Symbol.FunctionScope!.ParameterAt(i - 1 + (fact.Symbol.Scope!.Class is null ? 0 : 1)).IsReference)
                        {
                            Moved(values[node.ChildAt(i)], node.ChildAt(i).Fact!.Type);
                        }
                    }
                }
                values.Add(node, result);
            }
            return values[root];
        }
        void Conditional(SyntaxNode node, string? valueTarget = null)
        {
            string condition = Expression(node.ChildAt(0));
            Line($"if ({condition})");
            Block(node.ChildAt(1), false, valueTarget: valueTarget);
            if (node.ChildCount() == 3)
            {
                Line("else");
                SyntaxNode alternative = node.ChildAt(2);
                if (alternative.Kind == SyntaxKind.If)
                {
                    // The alternative's condition temporaries belong inside the
                    // else, not before the original condition is evaluated.
                    Line("{");
                    indentation += 1;
                    owned.Add(new());
                    Conditional(alternative, valueTarget);
                    Cleanup(owned.Count - 1);
                    owned.RemoveAt(owned.Count - 1);
                    indentation -= 1;
                    Line("}");
                }
                else
                {
                    Block(alternative, false, valueTarget: valueTarget);
                }
            }
        }
        void Block(SyntaxNode node, bool unitFallthrough, SemanticSymbol? function = null,
            string? valueTarget = null)
        {
            bool wroteValue = false;
            owned.Add(new());
            Line("{");
            indentation += 1;
            if (function is not null)
            {
                string display = CString(JsonSerializer.Serialize(function.Name));
                Line($"ly_frame ly_activation = {{{display}, ly_current_frame}};");
                Line("ly_current_frame = &ly_activation;");
                for (int p = 0; p < function.FunctionScope!.ParameterCount(); p += 1)
                {
                    SemanticSymbol parameter = function.FunctionScope.ParameterAt(p);
                    if (!parameter.IsReference)
                    {
                        Own(Name(parameter), parameter.Type!.Value);
                    }
                }
            }
            for (int i = 0; i < node.ChildCount(); i += 1)
            {
                SyntaxNode statement = node.ChildAt(i);
                switch (statement.Kind)
                {
                    case SyntaxKind.DiscardResult:
                        break;
                    case SyntaxKind.Binding:
                        SemanticSymbol symbol = statement.DeclaredSymbol!;
                        int next = 1;
                        if (next < statement.ChildCount() && statement.ChildAt(next).Kind == SyntaxKind.Annotation)
                        {
                            next += 1;
                        }
                        string? initialValue = next < statement.ChildCount()
                            ? Expression(statement.ChildAt(next)) : null;
                        string initializer = initialValue is not null ? " = " + initialValue
                            : symbol.Type!.Value.ArrayDepth > 0 ? " = {0}" : "";
                        string pointerMarker = symbol.IsReference ? " *" : " ";
                        Line($"{CType(symbol.Type!.Value)}{pointerMarker}{Name(symbol)}{initializer};");
                        if (!symbol.IsReference)
                        {
                            Own(Name(symbol), symbol.Type.Value);
                        }
                        if (initialValue is not null && !symbol.IsReference)
                        {
                            Moved(initialValue, symbol.Type.Value);
                        }
                        break;
                    case SyntaxKind.Assignment:
                        SemanticFact targetFact = statement.ChildAt(0).Fact!;
                        string targetName = Storage(statement.ChildAt(0));
                        string assignment = Text(statement);
                        string? oldValue = null;
                        if (assignment != "=")
                        {
                            // Read the selected destination before right-hand effects.
                            // Storage has already evaluated indices; borrow checking
                            // ensures that this selected slot remains valid.
                            oldValue = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
                            temporary += 1;
                            Line($"{CType(targetFact.Type)} {oldValue} = {targetName};");
                        }
                        string value = Expression(statement.ChildAt(1));
                        if (assignment != "=")
                        {
                            string operation = assignment switch
                            {
                                "+=" or "!+=" => "add",
                                "-=" or "!-=" => "sub",
                                "*=" or "!*=" => "mul",
                                "/=" => "div",
                                _ => throw new InvalidOperationException("Unchecked compound assignment."),
                            };
                            string typeName = SemanticTypes.Name(targetFact.Type);
                            if (assignment.StartsWith('!'))
                            {
                                value = $"ly_{typeName}_wrap_{operation}({oldValue}, {value})";
                            }
                            else
                            {
                                Diagnostic location = statement.Source!.SemanticDiagnostic(statement, "", "");
                                string file = CString(JsonSerializer.Serialize(statement.Source.Path));
                                value = $"ly_{typeName}_{operation}({oldValue}, {value}, "
                                    + $"{file}, {location.Line}, {location.Column})";
                            }
                        }
                        if (targetFact.Type.ArrayDepth > 0)
                        {
                            Line($"ly_array_drop(&{targetName}, {targetFact.Type.ArrayDepth});");
                        }
                        Line($"{targetName} = {value};");
                        Moved(value, targetFact.Type);
                        break;
                    case SyntaxKind.ExpressionStatement:
                        SyntaxNode expression = statement.ChildAt(0);
                        if (valueTarget is not null && i == node.ChildCount() - 1)
                        {
                            string branchValue = Expression(expression);
                            Line($"{valueTarget} = {branchValue};");
                            Moved(branchValue, expression.Fact!.Type);
                            wroteValue = true;
                        }
                        else if (expression.Kind == SyntaxKind.If)
                        {
                            Conditional(expression);
                        }
                        else
                        {
                            Line($"(void){Expression(expression)};");
                        }
                        break;
                    case SyntaxKind.While:
                        // Condition effects must run on every iteration, including
                        // after continue. C's nearest-loop targets match Lyric here.
                        Line("while (true)");
                        Line("{");
                        indentation += 1;
                        loopScopes.Push(owned.Count);
                        owned.Add(new());
                        string condition = Expression(statement.ChildAt(0));
                        Line($"if (!{condition})");
                        Line("{");
                        indentation += 1;
                        Cleanup(loopScopes.Peek());
                        Line("break;");
                        indentation -= 1;
                        Line("}");
                        Block(statement.ChildAt(1), false);
                        Cleanup(loopScopes.Pop());
                        owned.RemoveAt(owned.Count - 1);
                        indentation -= 1;
                        Line("}");
                        break;
                    case SyntaxKind.Break:
                        Cleanup(loopScopes.Peek());
                        Line("break;");
                        break;
                    case SyntaxKind.Continue:
                        Cleanup(loopScopes.Peek());
                        Line("continue;");
                        break;
                    case SyntaxKind.Return:
                        string returned = statement.ChildCount() == 0
                            ? "(ly_unit){0}" : Expression(statement.ChildAt(0));
                        Cleanup(0, returned);
                        Line("ly_current_frame = ly_activation.parent;");
                        Line($"return {returned};");
                        break;
                    default:
                        Unsupported(statement);
                        break;
                }
            }
            if (valueTarget is not null && !wroteValue && node.Fact!.CanContinue)
            {
                // A continuing branch with no final expression (including an
                // explicit trailing semicolon) produces unit, not an old value.
                Line($"{valueTarget} = (ly_unit){{0}};");
            }
            Cleanup(owned.Count - 1);
            owned.RemoveAt(owned.Count - 1);
            if (unitFallthrough)
            {
                Line("ly_current_frame = ly_activation.parent;");
                Line("return (ly_unit){0};");
            }
            indentation -= 1;
            Line("}");
        }
    }
}
