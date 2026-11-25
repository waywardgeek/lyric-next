using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Lyric;

// This module emits validated structured IR and its checked semantic identities.
// Syntax supplies declaration metadata and diagnostic origins, never execution or
// cleanup reconstruction. All emission maps are transient; the analysis owns IR.
internal sealed partial class Compilation
{
    internal partial Diagnostic? EmitC(TextWriter output, bool structureOfArrays)
    {
        if (Analysis is null || Analysis.Destroyed)
        {
            throw new InvalidOperationException("Check before emitting C.");
        }
        Diagnostic? loweringFailure = LowerIr();
        if (loweringFailure is not null)
        {
            return loweringFailure;
        }
        return EmitIrC(output, structureOfArrays);
    }

    internal partial Diagnostic? EmitIrC(TextWriter output, bool structureOfArrays)
    {
        if (Analysis?.Ir is not IrProgram program || Analysis.Destroyed)
        {
            throw new InvalidOperationException("Lower before emitting IR C.");
        }
        program.ValidateStructure();
        program.ValidateOperations();
        program.ValidateExecution();
        using StringWriter code = new(CultureInfo.InvariantCulture);
        bool unsafeMode = Analysis.Unsafe;
        if (unsafeMode)
        {
            code.WriteLine("/* UNSAFE: arithmetic and index checks disabled; invalid inputs inherit C behavior. */");
        }
        Dictionary<SemanticSymbol, string> names = new();
        List<SemanticSymbol> functions = new();
        List<SemanticSymbol> classes = new();
        // Backend names are transient views of canonical identities. Component-first
        // ordering makes inline tuple fields complete C types before their use.
        Dictionary<SemanticType, string> aggregateNames = new();
        List<SemanticType> aggregateTypes = new();
        int temporary = 0;
        int indentation = 0;
        SemanticSymbol? entry = null;
        foreach (SourceFile source in Sources)
        {
            SyntaxNode root = source.SyntaxRoot()!;
            for (int i = 0; i < root.ChildCount(); i += 1)
            {
                SyntaxNode declaration = root.ChildAt(i);
                if (declaration.Kind is SyntaxKind.Class or SyntaxKind.Struct)
                {
                    if (declaration.Kind == SyntaxKind.Class)
                    {
                        SemanticSymbol classSymbol = declaration.DeclaredSymbol!;
                        classes.Add(classSymbol);
                        Name(classSymbol);
                    }
                    for (int m = 0; m < declaration.ChildCount(); m += 1)
                    {
                        SyntaxNode member = declaration.ChildAt(m);
                        if (member.DeclaredSymbol is not SemanticSymbol symbol)
                        {
                            continue;
                        }
                        if (symbol.Kind == SymbolKind.Function)
                        {
                            functions.Add(symbol);
                            Name(symbol);
                        }
                    }
                    continue;
                }
                if (declaration.DeclaredSymbol is not SemanticSymbol function
                    || function.Kind != SymbolKind.Function)
                {
                    continue;
                }
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

                /* Language assertions are always enabled, including unsafe and
                   optimized builds. Never delegate this contract to C assert. */
                static ly_unit ly_assert(bool condition, const char *file, int line, int column)
                {
                    if (!condition)
                    {
                        fprintf(stderr, "%s:%d:%d: runtime error LYR010: assertion failed\n",
                            file, line, column);
                        for (ly_frame *frame = ly_current_frame; frame != NULL; frame = frame->parent)
                        {
                            fprintf(stderr, "  in %s\n", frame->name);
                        }
                        exit(1);
                    }
                    return (ly_unit){0};
                }

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

                /* Class buffers double independently of ordinary array policy.
                   Growth is requested only after their old slot capacity is used.
                   Do not initialize spare slots: untouched pages may remain lazy.
                   Reuse the checked allocator rather than duplicate realloc/errors. */
                static void ly_class_reserve(ly_array *array, uint64_t required, size_t stride,
                    const char *file, int line, int column)
                {
                    if (required <= array->capacity)
                    {
                        return;
                    }
                    uint64_t limit = SIZE_MAX / stride;
                    uint64_t capacity = array->capacity > limit - array->capacity
                        ? limit : array->capacity + array->capacity;
                    if (capacity < required)
                    {
                        capacity = required;
                    }
                    ly_array_reserve(array, capacity, stride, file, line, column);
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
            AggregateLayouts();
            AggregatePrototypes();
            ArithmeticHelpers();
            // Generated destructors call hooks; declarations precede storage
            // routines, while checked method bodies are emitted below them.
            foreach (SemanticSymbol function in functions)
            {
                Line(Signature(function) + ";");
            }
            // Initializers have real field identities and no source parameters.
            // Publish all prototypes before any body can invoke another body.
            for (int i = 0; i < Analysis.Ir!.FunctionCount(); i += 1)
            {
                IrFunction initializer = Analysis.Ir.FunctionAt(i);
                if (initializer.Kind == IrFunctionKind.FieldInitializer)
                {
                    SemanticSymbol field = initializer.Declaration!;
                    Line($"static {CType(field.Type!)} {(field.IsReference ? "*" : "")}{Name(field)}_initialize(void);");
                }
            }
            foreach (SemanticSymbol classSymbol in classes)
            {
                Line($"static ly_unit {Name(classSymbol)}_destroy_body(uint64_t index);");
            }
            foreach (SemanticSymbol classSymbol in classes)
            {
                ClassStorage(classSymbol);
            }
            foreach (SemanticSymbol classSymbol in classes)
            {
                if (classSymbol.IsCountedClass())
                {
                    CountedArrays(classSymbol);
                }
            }
            AggregateHelpers();
            foreach (SemanticSymbol classSymbol in classes)
            {
                Line($"static ly_unit {Name(classSymbol)}_destroy_body(uint64_t index)");
                EmitIr(classSymbol.IrDestruction!);
            }
            foreach (SemanticSymbol function in functions)
            {
                Line(Signature(function));
                EmitIr(function.IrBody!);
            }
            for (int i = 0; i < Analysis.Ir!.FunctionCount(); i += 1)
            {
                IrFunction initializer = Analysis.Ir.FunctionAt(i);
                if (initializer.Kind == IrFunctionKind.FieldInitializer)
                {
                    SemanticSymbol field = initializer.Declaration!;
                    Line($"static {CType(field.Type!)} {(field.IsReference ? "*" : "")}{Name(field)}_initialize(void)");
                    EmitIr(initializer);
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

        void EmitIr(IrFunction function)
        {
            // Validate supported vocabulary before writing a body. C is buffered
            // until all emission succeeds; incomplete output is never published.
            for (int i = 0; i < function.NodeCount(); i += 1)
            {
                IrNode node = function.NodeAt(i);
                if (node.Kind is not (IrKind.Region or IrKind.Constant or IrKind.Local
                    or IrKind.Load or IrKind.Store or IrKind.Call or IrKind.CallInitializer or IrKind.Return
                    or IrKind.Unary or IrKind.Binary or IrKind.Convert
                    or IrKind.If or IrKind.While or IrKind.Yield or IrKind.Break or IrKind.Continue
                    or IrKind.NewArray or IrKind.Copy or IrKind.Move or IrKind.FreeArray
                    or IrKind.NewTuple or IrKind.SelectTuple or IrKind.DropTuple
                    or IrKind.NewStruct or IrKind.SelectStruct or IrKind.DropStruct
                    or IrKind.ArrayLength or IrKind.ArrayCapacity
                    or IrKind.Borrow or IrKind.SelectIndex
                    or IrKind.ArrayReserve or IrKind.ArrayResize or IrKind.ArrayTruncate or IrKind.ArrayPush
                    or IrKind.SelectField or IrKind.AddressOf or IrKind.Ref or IrKind.Unref
                    or IrKind.NewObject or IrKind.DestroyObject or IrKind.ReclaimObject))
                {
                    throw new InvalidOperationException($"Unsupported validated IR operation: {node.Kind}.");
                }
            }
            SemanticSymbol? declaration = function.Declaration;
            bool generated = function.Kind == IrFunctionKind.Destruction;
            Dictionary<IrNode, string> values = new();
            Line("{");
            indentation += 1;
            Line("/* Body emitted from validated structured IR. */");
            string display = CString(JsonSerializer.Serialize(generated
                ? function.DestructionClass!.Name + ".destroy" : declaration!.Name));
            Line($"ly_frame ly_activation = {{{display}, ly_current_frame}};");
            Line("ly_current_frame = &ly_activation;");
            Dictionary<IrNode, string> labels = new();
            HashSet<IrNode> parameters = new();
            // Declare storage once, but execute each assignment only at its IR
            // position. Condition definitions can outlive their C region block.
            for (int i = 0; i < function.NodeCount(); i += 1)
            {
                IrNode node = function.NodeAt(i);
                string name = "ly_ir" + temporary.ToString(CultureInfo.InvariantCulture);
                temporary += 1;
                labels.Add(node, name);
                if (node.Result == IrResult.None)
                {
                    continue;
                }
                bool parameter = false;
                SemanticScope? scope = function.Kind == IrFunctionKind.FieldInitializer
                    ? declaration!.InitializerScope : declaration?.FunctionScope;
                if (generated && node.Kind == IrKind.Local && node.Symbol is null)
                {
                    values.Add(node, "index");
                    parameters.Add(node);
                    continue;
                }
                if (node.Kind == IrKind.SelectField)
                {
                    // A place saves a handle, not an interior pointer. Every use
                    // resolves current class storage through that saved handle.
                    Line($"uint64_t {name};");
                    values.Add(node, FieldStorage(node.Symbol!, name));
                    continue;
                }
                if (node.Kind is IrKind.Local or IrKind.Borrow)
                {
                    for (int p = 0; p < scope!.ParameterCount(); p += 1)
                    {
                        parameter |= ReferenceEquals(node.Symbol, scope.ParameterAt(p));
                    }
                }
                if (parameter)
                {
                    values.Add(node, node.Kind == IrKind.Borrow
                        ? $"(*{Name(node.Symbol!)})" : Name(node.Symbol!));
                    parameters.Add(node);
                }
                else
                {
                    // A fixed address stores a pointer; Local instead owns the slot itself.
                    bool pointer = node.Result == IrResult.Address && node.Kind != IrKind.Local;
                    Line($"{CType(node.Type!)} {(pointer ? "*" : "")}{name};");
                    values.Add(node, pointer ? $"(*{name})" : name);
                }

            }
            Stack<Action> work = new();
            void Region(IrNode region, string? destination)
            {
                work.Push(() => Line(labels[region] + "_end: ;"));
                for (int i = region.ChildCount() - 1; i >= 0; i -= 1)
                {
                    IrNode child = region.ChildAt(i);
                    work.Push(() => Instruction(child, region, destination));
                }
            }
            IrNode Part(IrNode node, IrRole role)
            {
                for (int i = 0; i < node.ChildCount(); i += 1)
                {
                    if (node.ChildAt(i).Role == role)
                    {
                        return node.ChildAt(i);
                    }
                }
                throw new InvalidOperationException("Validated IR region missing.");
            }
            void Instruction(IrNode node, IrNode region, string? destination)
            {
                string Operand(int index) => values[node.OperandAt(index).Definition!];
                if (node.Kind == IrKind.Local)
                {
                    if (Aggregate(node.Type!) && !parameters.Contains(node))
                    {
                        Line($"{values[node]} = {aggregateNames[node.Type!]}_empty();");
                    }
                    else if (node.Type!.ArrayDepth > 0 && !parameters.Contains(node))
                    {
                        Line($"{values[node]} = (ly_array){{0}};");
                    }
                    else if (Counted(node.Type!) && !parameters.Contains(node))
                    {
                        Line($"{values[node]} = UINT64_MAX;");
                    }
                    return;
                }
                if (node.Kind == IrKind.SelectField)
                {
                    Line($"{labels[node]} = {Operand(0)};");
                    return;
                }
                if (node.Kind == IrKind.ReclaimObject)
                {
                    string owner = Name(node.Symbol!);
                    Line($"(({owner}_slot *){owner}_slots.data)[{Operand(0)}].state = {owner}_free_state;");
                    Line($"(({owner}_slot *){owner}_slots.data)[{Operand(0)}].next_free = {owner}_free;");
                    Line($"{owner}_free = {Operand(0)};");
                    return;
                }
                if (node.Kind is IrKind.Borrow or IrKind.AddressOf)
                {
                    if (!parameters.Contains(node))
                    {
                        Line($"{labels[node]} = &{Operand(0)};");
                    }
                    return;
                }
                if (node.Kind == IrKind.SelectIndex)
                {
                    // Capture the fixed pointer now, before subsequent arguments or
                    // assignment effects. Never re-resolve it at the eventual use.
                    IrNode index = node.OperandAt(1).Definition!;
                    if (!unsafeMode)
                    {
                        string negative = SemanticTypes.IsSigned(index.Type!)
                            ? $"{Operand(1)} < 0 || " : "";
                        Line($"if ({negative}(uint64_t){Operand(1)} >= {Operand(0)}.length)");
                        Line("{");
                        Line($"    ly_array_failure({Location(node.Origin!)}, \"LYR006: array index out of bounds\");");
                        Line("}");
                    }
                    Line($"{labels[node]} = &(({CType(node.Type!)} *){Operand(0)}.data)[{Operand(1)}];");
                    return;
                }
                if (node.Kind == IrKind.If)
                {
                    Line($"if ({Operand(0)}) {{");
                    work.Push(() => Line("}"));
                    work.Push(() => Region(Part(node, IrRole.Else), values[node]));
                    work.Push(() => Line("} else {"));
                    Region(Part(node, IrRole.Then), values[node]);
                    return;
                }
                if (node.Kind == IrKind.While)
                {
                    string loop = labels[node];
                    Line(loop + "_condition: ;");
                    Line("{");
                    Line($"bool {loop}_test;");
                    work.Push(() => Line(loop + "_exit: ;"));
                    work.Push(() => Line("}"));
                    work.Push(() => Line($"goto {loop}_condition;"));
                    work.Push(() => Region(Part(node, IrRole.Body), null));
                    work.Push(() => Line($"if (!{loop}_test) goto {loop}_exit;"));
                    Region(Part(node, IrRole.Condition), loop + "_test");
                    return;
                }
                if (node.Kind is IrKind.Break or IrKind.Continue)
                {
                    string suffix = node.Kind == IrKind.Break ? "_exit" : "_condition";
                    Line($"goto {labels[node.Target!]}{suffix};");
                    return;
                }
                if (node.Kind == IrKind.Yield)
                {
                    if (destination is not null)
                    {
                        Line($"{destination} = {Operand(0)};");
                    }
                    Line($"goto {labels[region]}_end;");
                    return;
                }
                if (node.Kind == IrKind.Store)
                {
                    Line($"{Operand(0)} = {Operand(1)};");
                    return;
                }
                if (node.Kind == IrKind.Return)
                {
                    Line("ly_current_frame = ly_activation.parent;");
                    bool addressReturn = function.Kind == IrFunctionKind.FieldInitializer
                        && function.Declaration!.IsReference;
                    Line($"return {(addressReturn ? "&" : "")}{Operand(0)};");
                    return;
                }
                if (node.Kind is IrKind.NewTuple or IrKind.NewStruct)
                {
                    for (int i = 0; i < node.OperandCount(); i += 1)
                    {
                        string value = Operand(i);
                        if (ReferenceComponent(node.Type!, i))
                        {
                            value = $"&({value})";
                        }
                        Line($"{values[node]}.item{i} = {value};");
                    }
                    return;
                }
                if (node.Kind == IrKind.SelectStruct)
                {
                    List<SemanticSymbol> fields = Fields(node.OperandAt(0).Definition!.Type!.Declaration!);
                    int index = fields.IndexOf(node.Symbol!);
                    string field = $"({Operand(0)}).item{index}";
                    // Reference fields already contain the fixed target address.
                    string address = node.Symbol!.IsReference ? field : $"&{field}";
                    Line($"{labels[node]} = {address};");
                    return;
                }
                if (node.Kind == IrKind.SelectTuple)
                {
                    string index = node.Literal!.Integer.ToString(CultureInfo.InvariantCulture);
                    Line($"{labels[node]} = &({Operand(0)}).item{index};");
                    return;
                }
                if (node.Kind == IrKind.NewArray)
                {
                    SemanticType element = SemanticTypes.ElementOf(node.Type!);
                    bool bytes = node.Literal?.Kind == IrLiteralKind.Bytes;
                    int count = bytes ? node.Literal!.ByteCount() : node.OperandCount();
                    string array = values[node];
                    Line($"{array} = ly_array_new({count}, sizeof({CType(element)}), {Location(node.Origin!)});");
                    for (int i = 0; i < count; i += 1)
                    {
                        string value = bytes
                            ? node.Literal!.ByteAt(i).ToString(CultureInfo.InvariantCulture)
                            : Operand(i);
                        Line($"(({CType(element)} *){array}.data)[{i}] = {value};");
                        Line($"{array}.length += 1;");
                    }
                    return;
                }
                if (node.Kind is IrKind.ArrayReserve or IrKind.ArrayResize
                    or IrKind.ArrayTruncate or IrKind.ArrayPush)
                {
                    // The descriptor selection and argument effects have already
                    // executed in IR order. The helper does not acquire ownership.
                    string pointer = labels[node] + "_array";
                    Line($"ly_array *{pointer} = &{Operand(0)};");
                    string result = EmitArrayMutation(node.Kind, pointer, Operand(1),
                        node.OperandAt(0).Definition!.Type!, node.Origin!);
                    Line($"{values[node]} = {result};");
                    return;
                }
                if (node.Kind == IrKind.Move)
                {
                    Line($"{values[node]} = {Operand(0)};");
                    Moved(Operand(0), node.Type!);
                    return;
                }
                if (node.Kind is IrKind.FreeArray or IrKind.Unref or IrKind.DropTuple or IrKind.DropStruct)
                {
                    IrNode holder = node.OperandAt(0).Definition!;
                    Drop(Operand(0), holder.Type!, Location(node.Origin!));
                    return;
                }
                string expression;
                if (node.Kind == IrKind.Constant)
                {
                    expression = node.Literal?.Kind switch
                    {
                        IrLiteralKind.Integer => Integer(node.Literal.Integer),
                        IrLiteralKind.Boolean => node.Literal.Boolean ? "true" : "false",
                        _ => "(ly_unit){0}",
                    };
                }
                else if (node.Kind is IrKind.Copy or IrKind.Ref)
                {
                    expression = Copy(Operand(0), node.Type!, node.Origin!);
                }
                else if (node.Kind == IrKind.CallInitializer)
                {
                    expression = $"{Name(node.Symbol!)}_initialize()";
                }
                else if (node.Kind == IrKind.NewObject)
                {
                    expression = $"{Name(node.Symbol!)}_allocate({Location(node.Origin!)})";
                }
                else if (node.Kind == IrKind.DestroyObject)
                {
                    expression = $"{Name(node.Symbol!)}_destroy({Operand(0)}, {Location(node.Origin!)})";
                }
                else if (node.Kind is IrKind.ArrayLength or IrKind.ArrayCapacity)
                {
                    string member = node.Kind == IrKind.ArrayLength ? "length" : "capacity";
                    expression = $"{Operand(0)}.{member}";
                }
                else if (node.Kind == IrKind.Load)
                {
                    expression = Operand(0);
                }
                else if (node.Kind is IrKind.Unary or IrKind.Binary or IrKind.Convert)
                {
                    string right = node.OperandCount() == 2 ? Operand(1) : "";
                    expression = IrScalarExpression(node, Operand(0), right);
                }
                else
                {
                    SemanticSymbol target = node.Symbol!;
                    List<string> arguments = new();
                    for (int a = 0; a < node.OperandCount(); a += 1)
                    {
                        bool reference = target.FunctionScope!.ParameterAt(a).IsReference;
                        arguments.Add(reference ? $"&{Operand(a)}" : Operand(a));
                    }
                    expression = target.Builtin switch
                    {
                        BuiltinKind.Assert => $"ly_assert({arguments[0]}, {Location(node.Origin!)})",
                        BuiltinKind.Print => $"ly_print({arguments[0]}, false, {Location(node.Origin!)})",
                        BuiltinKind.Println => $"ly_print({arguments[0]}, true, {Location(node.Origin!)})",
                        _ => $"{Name(target)}({string.Join(", ", arguments)})",
                    };
                }
                // Each read/call is sequenced in its own C statement. C argument
                // evaluation order therefore cannot reorder source-language effects.
                // Address expressions are normally represented as dereferenced
                // lvalues. A reference initializer call instead assigns the pointer.
                string destinationValue = node.Kind == IrKind.CallInitializer
                    && node.Result == IrResult.Address ? labels[node] : values[node];
                Line($"{destinationValue} = {expression};");
            }
            Region(function.Body!, null);
            while (work.Count != 0)
            {
                work.Pop()();
            }
            indentation -= 1;
            Line("}");
        }

        string IrScalarExpression(IrNode node, string left, string right)
        {
            SemanticType type = node.Type!;
            SemanticType leftType = node.OperandAt(0).Definition!.Type!;
            string operation = node.Operator!;
            string typeName = SemanticTypes.Name(type);
            string location = Location(node.Origin!);
            if (node.Kind == IrKind.Convert)
            {
                if (operation == "!as")
                {
                    return $"ly_{typeName}_bits((uint64_t){left})";
                }
                if (operation != "widen" && !unsafeMode)
                {
                    string outside = $"(uint64_t){left} > {Integer(SemanticTypes.Maximum(type))}";
                    if (SemanticTypes.IsSigned(leftType))
                    {
                        outside = SemanticTypes.IsSigned(type)
                            ? $"{left} < 0 ? (int64_t){left} < {Integer(SemanticTypes.Minimum(type))} : ({outside})"
                            : $"{left} < 0 || ({outside})";
                    }
                    Line($"if ({outside})");
                    Line("{");
                    Line($"    ly_conversion({location}, {CString(typeName)});");
                    Line("}");
                }
                return $"({CType(type)}){left}";
            }
            if (node.Kind == IrKind.Unary)
            {
                return operation switch
                {
                    "!" => $"!{left}",
                    "!-" => $"ly_{typeName}_wrap_sub(0, {left})",
                    "-" when unsafeMode => $"(-{left})",
                    "-" => $"ly_{typeName}_sub(0, {left}, {location})",
                    _ => throw new InvalidOperationException("Invalid IR unary operation."),
                };
            }
            SemanticType rightType = node.OperandAt(1).Definition!.Type!;
            if (operation is "==" or "!=" or "<" or "<=" or ">" or ">=")
            {
                if (leftType == Analysis!.ScalarType(ScalarKind.Bool))
                {
                    return $"({left} {operation} {right})";
                }
                bool leftSigned = SemanticTypes.IsSigned(leftType);
                bool rightSigned = SemanticTypes.IsSigned(rightType);
                if (leftSigned == rightSigned)
                {
                    string wide = leftSigned ? "int64_t" : "uint64_t";
                    return $"(({wide}){left} {operation} ({wide}){right})";
                }
                // A negative signed value is smaller than every unsigned value.
                // Only the nonnegative case may be compared as uint64 magnitudes.
                string signed = leftSigned ? left : right;
                bool negative = operation switch
                {
                    "!=" => true,
                    "<" or "<=" => leftSigned,
                    ">" or ">=" => rightSigned,
                    _ => false,
                };
                string negativeText = negative ? "true" : "false";
                return $"({signed} < 0 ? {negativeText} : ((uint64_t){left} {operation} (uint64_t){right}))";
            }
            if (operation is "<<" or ">>")
            {
                string outside = $"(uint64_t){right} >= UINT64_C({SemanticTypes.IntegerWidth(type)})";
                if (SemanticTypes.IsSigned(rightType))
                {
                    outside = $"{right} < 0 || " + outside;
                }
                if (!unsafeMode)
                {
                    Line($"if ({outside})");
                    Line("{");
                    Line($"    ly_shift_count({location});");
                    Line("}");
                }
                string direction = operation == "<<" ? "shl" : "shr";
                return unsafeMode && operation == ">>"
                    ? $"({left} >> {right})"
                    : $"ly_{typeName}_{direction}({left}, (uint64_t){right})";
            }
            if (operation is "&" or "|" or "^")
            {
                return $"ly_{typeName}_bits((uint64_t){left} {operation} (uint64_t){right})";
            }
            bool wrapping = operation is "!+" or "!-" or "!*";
            string helper = operation switch
            {
                "+" or "!+" => "add",
                "-" or "!-" => "sub",
                "*" or "!*" => "mul",
                "/" => "div",
                "%" => "rem",
                _ => throw new InvalidOperationException("Invalid IR binary operation."),
            };
            if (wrapping)
            {
                return $"ly_{typeName}_wrap_{helper}({left}, {right})";
            }
            return unsafeMode
                ? UncheckedArithmetic(type, operation, left, right)
                : $"ly_{typeName}_{helper}({left}, {right}, {location})";
        }

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
        // These views use canonical identities and actual declaration fields.
        // They are emission-local traversal data, not a second type registry.
        bool Aggregate(SemanticType type)
        {
            return type.Kind == SemanticTypeKind.Tuple
                || type.Declaration?.Kind == SymbolKind.Struct;
        }
        List<SemanticType> Components(SemanticType type)
        {
            List<SemanticType> components = new();
            if (type.Kind == SemanticTypeKind.Tuple)
            {
                for (int i = 0; i < type.ComponentCount(); i += 1)
                {
                    components.Add(type.ComponentAt(i));
                }
            }
            else
            {
                foreach (SemanticSymbol field in type.Declaration!.MemberScope!.Fields())
                {
                    components.Add(field.Type!);
                }
            }
            return components;
        }
        bool ReferenceComponent(SemanticType type, int index)
        {
            if (type.Kind == SemanticTypeKind.Tuple)
            {
                return false;
            }
            return Fields(type.Declaration!)[index].IsReference;
        }
        void AggregateLayouts()
        {
            // Discovery crosses array descriptors; inline layout ordering does
            // not. Separating them permits A -> [B] with B -> A inline.
            HashSet<SemanticType> discovered = new();
            List<SemanticType> records = new();
            Stack<SemanticType> pending = new();
            IrProgram program = Analysis.Ir!;
            for (int f = 0; f < program.FunctionCount(); f += 1)
            {
                IrFunction function = program.FunctionAt(f);
                for (int n = 0; n < function.NodeCount(); n += 1)
                {
                    if (function.NodeAt(n).Type is SemanticType type)
                    {
                        pending.Push(type);
                    }
                }
            }
            while (pending.Count > 0)
            {
                SemanticType type = pending.Pop();
                if (!discovered.Add(type))
                {
                    continue;
                }
                if (type.Kind == SemanticTypeKind.Array)
                {
                    pending.Push(type.Element!);
                }
                else if (Aggregate(type))
                {
                    records.Add(type);
                    foreach (SemanticType component in Components(type))
                    {
                        pending.Push(component);
                    }
                }
            }
            HashSet<SemanticType> ordered = new();
            Stack<(SemanticType Type, bool Complete)> work = new();
            foreach (SemanticType root in records)
            {
                work.Push((root, false));
                while (work.Count > 0)
                {
                    (SemanticType type, bool complete) = work.Pop();
                    if (!complete)
                    {
                        if (!ordered.Add(type))
                        {
                            continue;
                        }
                        work.Push((type, true));
                        List<SemanticType> components = Components(type);
                        for (int i = 0; i < components.Count; i += 1)
                        {
                            // A pointer needs a declared name, not a complete
                            // record definition. It cannot form an inline cycle.
                            if (!ReferenceComponent(type, i) && Aggregate(components[i]))
                            {
                                work.Push((components[i], false));
                            }
                        }
                        continue;
                    }
                    string prefix = type.Kind == SemanticTypeKind.Tuple ? "ly_tuple" : "ly_struct";
                    string name = prefix + aggregateTypes.Count.ToString(CultureInfo.InvariantCulture);
                    aggregateNames.Add(type, name);
                    aggregateTypes.Add(type);
                }
            }
            // Publish every record name before defining fields. References may
            // point to an incomplete record; inline fields still require the
            // dependency order established above.
            foreach (SemanticType type in aggregateTypes)
            {
                string name = aggregateNames[type];
                Line($"typedef struct {name} {name};");
            }
            foreach (SemanticType type in aggregateTypes)
            {
                string name = aggregateNames[type];
                List<SemanticType> components = Components(type);
                Line($"struct {name} {{");
                if (components.Count == 0)
                {
                    // C11 forbids empty records. This byte is not a Lyric field.
                    Line("    unsigned char empty_storage;");
                }
                for (int i = 0; i < components.Count; i += 1)
                {
                    string pointer = ReferenceComponent(type, i) ? " *" : " ";
                    Line($"    {CType(components[i])}{pointer}item{i};");
                }
                Line("};");
            }
        }
        string CType(SemanticType type)
        {
            if (Aggregate(type))
            {
                return aggregateNames[type];
            }
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
            if (owner.Kind == SymbolKind.Struct)
            {
                fields.AddRange(owner.MemberScope!.Fields());
                return fields;
            }
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
                return $"(({CType(field.Type!)} *){Name(field)}_storage.data)[{index}]";
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
            Line($"enum {{ {name}_free_state, {name}_live_state, {name}_destroying_state }};");
            Line($"typedef struct {name}_slot {{");
            indentation += 1;
            Line("uint64_t next_free;");
            Line("unsigned char state;");
            if (owner.IsCountedClass())
            {
                Line("uint64_t references;");
            }
            indentation -= 1;
            Line($"}} {name}_slot;");
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
                    Line($"{CType(field.Type!)} {Name(field)};");
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
            Line($"ly_class_reserve(&{name}_slots, index + 1, sizeof({name}_slot), file, line, column);");
            if (structureOfArrays)
            {
                foreach (SemanticSymbol field in fields)
                {
                    Line($"ly_class_reserve(&{Name(field)}_storage, index + 1, sizeof({CType(field.Type!)}), file, line, column);");
                }
            }
            else
            {
                Line("if (index % 64 == 0)");
                Line("{");
                indentation += 1;
                Line($"ly_class_reserve(&{name}_chunks, index / 64 + 1, sizeof({name}_record *), file, line, column);");
                Line($"ly_array chunk = ly_array_new(64, sizeof({name}_record), file, line, column);");
                Line($"(({name}_record **){name}_chunks.data)[index / 64] = chunk.data;");
                indentation -= 1;
                Line("}");
            }
            Line($"{name}_next = index + 1;");
            indentation -= 1;
            Line("}");
            Line($"(({name}_slot *){name}_slots.data)[index].state = {name}_live_state;");
            if (owner.IsCountedClass())
            {
                // Construction returns the first owning holder. Reused slots
                // start a new count rather than inheriting their old occupancy.
                Line($"(({name}_slot *){name}_slots.data)[index].references = 1;");
            }
            foreach (SemanticSymbol field in fields)
            {
                Line($"{FieldStorage(field, "index")} = ({CType(field.Type!)}){{0}};");
            }
            Line("return index;");
            indentation -= 1;
            Line("}");
            Line($"static ly_unit {name}_destroy(uint64_t index, const char *file, int line, int column)");
            Line("{");
            indentation += 1;
            // This guard is defensive; static analysis must reject stale source
            // uses. Slot reuse does not make an old source handle valid again.
            Line($"if (index >= {name}_next || (({name}_slot *){name}_slots.data)[index].state != {name}_live_state)");
            Line("{");
            Line("    ly_array_failure(file, line, column, \"LYR009: invalid object destruction\");");
            Line("}");
            Line($"(({name}_slot *){name}_slots.data)[index].state = {name}_destroying_state;");
            // The validated generated body alone owns hook/field/reclaim order.
            Line($"return {name}_destroy_body(index);");
            indentation -= 1;
            Line("}");
            if (owner.IsCountedClass())
            {
                // UINT64_MAX is reserved by allocation. It denotes an empty
                // backend holder, not a nullable source-language object.
                Line($"static inline uint64_t {name}_retain(uint64_t index, const char *file, int line, int column)");
                Line("{");
                indentation += 1;
                Line($"if (index >= {name}_next || (({name}_slot *){name}_slots.data)[index].state != {name}_live_state)");
                Line("{");
                Line("    ly_array_failure(file, line, column, \"LYR009: invalid object retain\");");
                Line("}");
                Line($"{name}_slot *slot = &(({name}_slot *){name}_slots.data)[index];");
                Line("if (slot->references == UINT64_MAX)");
                Line("{");
                Line("    ly_array_failure(file, line, column, \"LYR012: object reference count overflow\");");
                Line("}");
                Line("slot->references += 1;");
                Line("return index;");
                indentation -= 1;
                Line("}");
                Line($"static inline void {name}_release(uint64_t *holder, const char *file, int line, int column)");
                Line("{");
                indentation += 1;
                Line("uint64_t index = *holder;");
                Line("if (index == UINT64_MAX)");
                Line("{");
                Line("    return;");
                Line("}");
                Line($"if (index >= {name}_next || (({name}_slot *){name}_slots.data)[index].state != {name}_live_state)");
                Line("{");
                Line("    ly_array_failure(file, line, column, \"LYR009: invalid object release\");");
                Line("}");
                Line($"{name}_slot *slot = &(({name}_slot *){name}_slots.data)[index];");
                Line("if (slot->references == 0)");
                Line("{");
                Line("    ly_array_failure(file, line, column, \"LYR009: empty object reference count\");");
                Line("}");
                Line("*holder = UINT64_MAX;");
                Line("slot->references -= 1;");
                Line("if (slot->references == 0)");
                Line("{");
                // Destruction can call user code and relocate metadata. Never
                // dereference slot again after crossing this boundary.
                Line($"    (void){name}_destroy(index, file, line, column);");
                Line("}");
                indentation -= 1;
                Line("}");
            }
        }
        // The element class is known at compile time. Recursive array ownership
        // uses its existing retain/release operations, not a runtime type registry.
        void CountedArrays(SemanticSymbol owner)
        {
            string name = Name(owner);
            code.WriteLine($$"""
                static void {{name}}_array_drop(ly_array *array, unsigned depth,
                    const char *file, int line, int column)
                {
                    if (depth > 1)
                    {
                        ly_array *children = array->data;
                        for (uint64_t i = 0; i < array->length; i += 1)
                        {
                            {{name}}_array_drop(&children[i], depth - 1, file, line, column);
                        }
                    }
                    else
                    {
                        uint64_t *elements = array->data;
                        for (uint64_t i = 0; i < array->length; i += 1)
                        {
                            {{name}}_release(&elements[i], file, line, column);
                        }
                    }
                    free(array->data);
                    *array = (ly_array){0};
                }

                static ly_array {{name}}_array_copy(ly_array source, unsigned depth,
                    const char *file, int line, int column)
                {
                    size_t stride = depth > 1 ? sizeof(ly_array) : sizeof(uint64_t);
                    ly_array result = ly_array_new(source.length, stride, file, line, column);
                    if (depth > 1)
                    {
                        ly_array *from = source.data;
                        ly_array *to = result.data;
                        for (uint64_t i = 0; i < source.length; i += 1)
                        {
                            to[i] = {{name}}_array_copy(from[i], depth - 1, file, line, column);
                            result.length += 1;
                        }
                    }
                    else
                    {
                        uint64_t *from = source.data;
                        uint64_t *to = result.data;
                        for (uint64_t i = 0; i < source.length; i += 1)
                        {
                            to[i] = {{name}}_retain(from[i], file, line, column);
                            result.length += 1;
                        }
                    }
                    return result;
                }
                """);
        }
        void AggregatePrototypes()
        {
            foreach (SemanticType type in aggregateTypes)
            {
                string name = aggregateNames[type];
                Line($"static {name} {name}_empty(void);");
                Line($"static {name} {name}_copy({name} source, const char *file, int line, int column);");
                Line($"static void {name}_drop({name} *value, const char *file, int line, int column);");
                Line($"static ly_array {name}_array_copy(ly_array source, unsigned depth, const char *file, int line, int column);");
                Line($"static void {name}_array_drop(ly_array *array, unsigned depth, const char *file, int line, int column);");
            }
        }
        void AggregateHelpers()
        {
            foreach (SemanticType type in aggregateTypes)
            {
                string name = aggregateNames[type];
                List<SemanticType> components = Components(type);
                Line($"static {name} {name}_empty(void)");
                Line("{");
                Line($"    {name} result = {{0}};");
                // Zero initializes scalars and array descriptors, but counted
                // handles need their sentinel, including inside nested tuples.
                for (int i = 0; i < components.Count; i += 1)
                {
                    SemanticType component = components[i];
                    if (!ReferenceComponent(type, i) && (Counted(component) || Aggregate(component)))
                    {
                        Moved($"result.item{i}", component);
                    }
                }
                Line("    return result;");
                Line("}");
                Line($"static {name} {name}_copy({name} source, const char *file, int line, int column)");
                Line("{");
                Line($"    {name} result = {{0}};");
                for (int i = 0; i < components.Count; i += 1)
                {
                    string source = $"source.item{i}";
                    string copied = ReferenceComponent(type, i)
                        ? source
                        : CopyAt(source, components[i], "file, line, column");
                    Line($"    result.item{i} = {copied};");
                }
                Line("    return result;");
                Line("}");
                Line($"static void {name}_drop({name} *value, const char *file, int line, int column)");
                Line("{");
                for (int i = 0; i < components.Count; i += 1)
                {
                    if (!ReferenceComponent(type, i))
                    {
                        Drop($"value->item{i}", components[i], "file, line, column");
                    }
                }
                Line($"    *value = {name}_empty();");
                Line("}");
                // Array nesting retains the existing descriptor recursion. Only
                // the leaf operation differs: each tuple copies/drops its fields.
                code.WriteLine($$"""
                    static ly_array {{name}}_array_copy(ly_array source, unsigned depth,
                        const char *file, int line, int column)
                    {
                        size_t stride = depth > 1 ? sizeof(ly_array) : sizeof({{name}});
                        ly_array result = ly_array_new(source.length, stride, file, line, column);
                        for (uint64_t i = 0; i < source.length; i += 1)
                        {
                            if (depth > 1)
                            {
                                ((ly_array *)result.data)[i] = {{name}}_array_copy(
                                    ((ly_array *)source.data)[i], depth - 1, file, line, column);
                            }
                            else
                            {
                                (({{name}} *)result.data)[i] = {{name}}_copy(
                                    (({{name}} *)source.data)[i], file, line, column);
                            }
                            result.length += 1;
                        }
                        return result;
                    }
                    static void {{name}}_array_drop(ly_array *array, unsigned depth,
                        const char *file, int line, int column)
                    {
                        for (uint64_t i = 0; i < array->length; i += 1)
                        {
                            if (depth > 1)
                            {
                                {{name}}_array_drop(&((ly_array *)array->data)[i],
                                    depth - 1, file, line, column);
                            }
                            else
                            {
                                {{name}}_drop(&(({{name}} *)array->data)[i], file, line, column);
                            }
                        }
                        free(array->data);
                        *array = (ly_array){0};
                    }
                    """);
            }
        }
        bool Counted(SemanticType type)
        {
            return type.ArrayDepth == 0 && type.Class is SemanticSymbol owner
                && owner.IsCountedClass();
        }
        string Copy(string value, SemanticType type, SyntaxNode node)
        {
            return CopyAt(value, type, Location(node));
        }
        string CopyAt(string value, SemanticType type, string location)
        {
            if (Aggregate(type))
            {
                return $"{aggregateNames[type]}_copy({value}, {location})";
            }
            if (!SemanticTypes.HasOwnedContents(type))
            {
                return value;
            }
            if (Counted(type))
            {
                return $"{Name(type.Class!)}_retain({value}, {location})";
            }
            if (type.Class is SemanticSymbol owner && owner.IsCountedClass())
            {
                return $"{Name(owner)}_array_copy({value}, {type.ArrayDepth}, {location})";
            }
            SemanticType baseType = type;
            while (baseType.ArrayDepth > 0)
            {
                baseType = SemanticTypes.ElementOf(baseType);
            }
            if (Aggregate(baseType))
            {
                return $"{aggregateNames[baseType]}_array_copy({value}, {type.ArrayDepth}, {location})";
            }
            string scalar = CType(baseType);
            return $"ly_array_copy({value}, {type.ArrayDepth}, sizeof({scalar}), {location})";
        }
        void Moved(string name, SemanticType type)
        {
            if (Aggregate(type))
            {
                Line($"{name} = {aggregateNames[type]}_empty();");
            }
            else if (Counted(type))
            {
                Line($"{name} = UINT64_MAX;");
            }
            else if (type.ArrayDepth > 0)
            {
                Line($"{name} = (ly_array){{0}};");
            }
        }
        void Drop(string name, SemanticType type, string location)
        {
            if (Aggregate(type))
            {
                Line($"{aggregateNames[type]}_drop(&{name}, {location});");
            }
            else if (Counted(type))
            {
                Line($"{Name(type.Class!)}_release(&{name}, {location});");
            }
            else if (type.ArrayDepth > 0)
            {
                if (type.Class is SemanticSymbol owner && owner.IsCountedClass())
                {
                    Line($"{Name(owner)}_array_drop(&{name}, {type.ArrayDepth}, {location});");
                }
                else
                {
                    SemanticType leaf = type;
                    while (leaf.Kind == SemanticTypeKind.Array)
                    {
                        leaf = leaf.Element!;
                    }
                    if (Aggregate(leaf))
                    {
                        Line($"{aggregateNames[leaf]}_array_drop(&{name}, {type.ArrayDepth}, {location});");
                    }
                    else
                    {
                        Line($"ly_array_drop(&{name}, {type.ArrayDepth});");
                    }
                }
            }
        }
        string Signature(SemanticSymbol function)
        {
            List<string> parameters = new();
            SemanticScope scope = function.FunctionScope!;
            for (int i = 0; i < scope.ParameterCount(); i += 1)
            {
                SemanticSymbol parameter = scope.ParameterAt(i);
                string pointer = parameter.IsReference ? " *" : " ";
                parameters.Add(CType(parameter.Type!) + pointer + Name(parameter));
            }
            string arguments = parameters.Count == 0 ? "void" : string.Join(", ", parameters);
            return $"static {CType(function.Type!)} {Name(function)}({arguments})";
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
            return CBytes(Encoding.UTF8.GetBytes(text));
        }
        string CBytes(ReadOnlySpan<byte> bytes)
        {
            // Keep ordinary text readable without changing arbitrary byte values.
            // End each hex escape with a literal boundary: C otherwise consumes
            // following hexadecimal characters as part of the same escape.
            StringBuilder escaped = new("\"");
            foreach (byte value in bytes)
            {
                switch (value)
                {
                    case (byte)'"':
                        escaped.Append("\\\"");
                        break;
                    case (byte)'\\':
                        escaped.Append("\\\\");
                        break;
                    case (byte)'?':
                        // Avoid C11 trigraph interpretation before escape parsing.
                        escaped.Append("\\?");
                        break;
                    case 10:
                        escaped.Append("\\n");
                        break;
                    case 13:
                        escaped.Append("\\r");
                        break;
                    case 9:
                        escaped.Append("\\t");
                        break;
                    default:
                        if (value >= 32 && value <= 126)
                        {
                            escaped.Append((char)value);
                        }
                        else
                        {
                            escaped.Append("\\x");
                            escaped.Append(value.ToString("X2", CultureInfo.InvariantCulture));
                            escaped.Append("\"\"");
                        }
                        break;
                }
            }
            escaped.Append('"');
            return escaped.ToString();
        }
        string UncheckedArithmetic(SemanticType type, string operation, string left, string right)
        {
            // Cast to the selected Lyric type before C's usual conversions.
            // Narrow unsigned multiplication is widened to avoid signed int
            // promotion. Invalid safe-mode inputs otherwise inherit C behavior.
            string ctype = CType(type);
            string a = $"(({ctype}){left})";
            string b = $"(({ctype}){right})";
            if (operation == "*" && !SemanticTypes.IsSigned(type)
                && SemanticTypes.IntegerWidth(type) < 32)
            {
                a = $"((uint32_t){left})";
                b = $"((uint32_t){right})";
            }
            string result = $"({a} {operation} {b})";
            if (operation == "%" && SemanticTypes.IsSigned(type))
            {
                // This is valid Lyric even in safe mode, so unsafe must preserve
                // zero rather than execute C's undefined MIN % -1 operation.
                string minimum = Integer(SemanticTypes.Minimum(type));
                result = $"({a} == ({ctype}){minimum} && {b} == -1 ? 0 : {result})";
            }
            return result;
        }
        void ArithmeticHelpers()
        {
            foreach (SemanticType type in Analysis!.PredefinedTypes())
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
                Line($"static inline {ctype} ly_{kind}_bits(uint64_t bits)");
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
                    Line($"static inline {ctype} ly_{kind}_wrap_{operation}({ctype} a, {ctype} b)");
                    Line("{");
                    indentation += 1;
                    Line($"return ly_{kind}_bits((uint64_t)a {symbol} (uint64_t)b);");
                    indentation -= 1;
                    Line("}");
                }
                // The expression site validates the independently typed count
                // before conversion in safe mode. Unsafe invalid counts have no
                // promised result; valid counts preserve these same bit semantics.
                foreach (string operation in new[] { "shl", "shr" })
                {
                    Line($"static inline {ctype} ly_{kind}_{operation}({ctype} a, uint64_t count)");
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
                    Line($"static inline {ctype} ly_{kind}_{operation}({ctype} a, {ctype} b, const char *file, int line, int column)");
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
                    Line($"static inline {ctype} ly_{kind}_{operation}({ctype} a, {ctype} b, const char *file, int line, int column)");
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
        // Emit only the concrete descriptor operation. Evaluation order and ownership
        // transfer are supplied by the caller; IR already expresses both explicitly.
        string EmitArrayMutation(IrKind operation, string pointer, string argument,
            SemanticType arrayType, SyntaxNode node)
        {
            SemanticType elementType = SemanticTypes.ElementOf(arrayType);
            string element = CType(elementType);
            if (operation == IrKind.ArrayPush)
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
            }
            else if (operation == IrKind.ArrayReserve)
            {
                Line($"ly_array_reserve({pointer}, {argument}, sizeof({element}), {Location(node)});");
            }
            else
            {
                bool hasDefault = SemanticTypes.HasDefaultValue(elementType);
                if (operation == IrKind.ArrayResize && !hasDefault)
                {
                    // Handles and structs (including those inside tuples) cannot grow
                    // observable elements. Unsafe mode does not waive initialization.
                    Line($"if ({argument} > {pointer}->length)");
                    Line("{");
                    indentation += 1;
                    Line($"ly_array_failure({Location(node)}, \"LYR011: array element has no default value\");");
                    indentation -= 1;
                    Line("}");
                }
                // Shrink first, releasing each removed owning child exactly once.
                Line($"while ({pointer}->length > {argument})");
                Line("{");
                indentation += 1;
                Line($"{pointer}->length -= 1;");
                Drop($"(({element} *){pointer}->data)[{pointer}->length]", elementType, Location(node));
                indentation -= 1;
                Line("}");
                if (operation == IrKind.ArrayResize)
                {
                    // Unlike truncate, resize(0) gives up even an empty reservation.
                    Line($"if ({argument} == 0)");
                    Line("{");
                    indentation += 1;
                    Drop($"(*{pointer})", arrayType, Location(node));
                    indentation -= 1;
                    Line("}");
                    if (hasDefault)
                    {
                        Line($"ly_array_reserve({pointer}, {argument}, sizeof({element}), {Location(node)});");
                        Line($"while ({pointer}->length < {argument})");
                        Line("{");
                        indentation += 1;
                        // The eligibility query excludes handles and structs. For
                        // supported scalar/array/tuple defaults, C's typed initializer
                        // recursively produces zero values and empty descriptors.
                        // Write every newly live element, including dirty reused slots.
                        Line($"(({element} *){pointer}->data)[{pointer}->length] = ({element}){{0}};");
                        Line($"{pointer}->length += 1;");
                        indentation -= 1;
                        Line("}");
                    }
                }
            }
            string result = "ly_t" + temporary.ToString(CultureInfo.InvariantCulture);
            temporary += 1;
            Line($"ly_unit {result} = {{0}};");
            return result;
        }
    }
}
