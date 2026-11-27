using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Lyric;

internal sealed partial class Compilation
{
    internal partial void WriteIr(TextWriter output)
    {
        IrProgram program = Analysis?.Ir
            ?? throw new InvalidOperationException("Lower before inspecting IR.");
        // Draft edits are legal after lowering. Validate this graph state again
        // before writing anything; previous validation is not a lasting certificate.
        program.ValidateStructure();
        program.ValidateOperations();
        program.ValidateExecution();
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            // Document-local IDs preserve declaration identity even when class
            // methods share a display name. They are not persistent model state.
            Dictionary<SemanticSymbol, int> symbols = new();
            int SymbolId(SemanticSymbol symbol)
            {
                if (!symbols.TryGetValue(symbol, out int id))
                {
                    id = symbols.Count;
                    symbols.Add(symbol, id);
                }
                return id;
            }
            // These IDs describe existing canonical objects. Never intern by
            // display name here: inspection must expose, not repair, identity.
            Dictionary<SemanticType, int> typeIds = new();
            List<SemanticType> types = new();
            int TypeId(SemanticType type)
            {
                if (!typeIds.TryGetValue(type, out int id))
                {
                    id = types.Count;
                    typeIds.Add(type, id);
                    types.Add(type);
                }
                return id;
            }
            void WriteSymbol(string property, SemanticSymbol? symbol)
            {
                if (symbol is null)
                {
                    writer.WriteNull(property);
                }
                else
                {
                    writer.WriteNumber(property, SymbolId(symbol));
                }
            }
            writer.WriteStartObject();
            writer.WriteString("format", "lyric-ir-1");
            writer.WriteBoolean("unsafe", Analysis!.Unsafe);
            writer.WriteStartArray("functions");
            for (int f = 0; f < program.FunctionCount(); f += 1)
            {
                IrFunction function = program.FunctionAt(f);
                Dictionary<IrNode, int> ids = new();
                for (int n = 0; n < function.NodeCount(); n += 1)
                {
                    ids.Add(function.NodeAt(n), n);
                }
                writer.WriteStartObject();
                SemanticSymbol identity = function.Declaration ?? function.DestructionClass!;
                writer.WriteString("kind", function.Kind.ToString());
                writer.WriteBoolean("borrowedReceiver", function.BorrowsReceiver());
                writer.WriteString("name", identity.Name);
                WriteSymbol("symbolId", identity);
                WriteSymbol("ownerClass", function.DestructionClass ?? identity.Scope?.Class);
                writer.WriteNumber("body", ids[function.Body!]);
                writer.WriteStartArray("nodes");
                for (int n = 0; n < function.NodeCount(); n += 1)
                {
                    IrNode node = function.NodeAt(n);
                    writer.WriteStartObject();
                    writer.WriteNumber("id", n);
                    writer.WriteString("kind", node.Kind.ToString());
                    writer.WriteString("role", node.Role.ToString());
                    writer.WriteString("result", node.Result.ToString());
                    writer.WriteString("type", node.Type is SemanticType type ? SemanticTypes.Name(type) : null);
                    if (node.Type is null)
                    {
                        writer.WriteNull("typeId");
                    }
                    else
                    {
                        writer.WriteNumber("typeId", TypeId(node.Type));
                    }
                    writer.WriteString("symbol", node.Symbol?.Name);
                    WriteSymbol("symbolId", node.Symbol);
                    WriteSymbol("ownerClass", node.Symbol?.Scope?.Class);
                    WriteSymbol("classId", node.Type?.Class);
                    writer.WriteBoolean("counted", node.Type?.Class?.IsCountedClass() == true);
                    writer.WriteString("operator", node.Operator);
                    writer.WriteNumber("offset", node.Origin!.TextOffset);
                    if (node.Target is null)
                    {
                        writer.WriteNull("target");
                    }
                    else
                    {
                        writer.WriteNumber("target", ids[node.Target]);
                    }
                    string? literal = node.Literal?.Kind switch
                    {
                        IrLiteralKind.Integer => node.Literal.Integer.ToString(CultureInfo.InvariantCulture),
                        IrLiteralKind.Boolean => node.Literal.Boolean ? "true" : "false",
                        _ => null,
                    };
                    writer.WriteString("literal", literal);
                    // Numeric bytes preserve embedded zero and invalid UTF-8. This
                    // payload is data, not text and not a second source decoder.
                    writer.WriteStartArray("bytes");
                    if (node.Literal?.Kind == IrLiteralKind.Bytes)
                    {
                        for (int b = 0; b < node.Literal.ByteCount(); b += 1)
                        {
                            writer.WriteNumberValue(node.Literal.ByteAt(b));
                        }
                    }
                    writer.WriteEndArray();
                    writer.WriteStartArray("children");
                    for (int c = 0; c < node.ChildCount(); c += 1)
                    {
                        writer.WriteNumberValue(ids[node.ChildAt(c)]);
                    }
                    writer.WriteEndArray();
                    writer.WriteStartArray("operands");
                    for (int a = 0; a < node.OperandCount(); a += 1)
                    {
                        writer.WriteNumberValue(ids[node.OperandAt(a).Definition!]);
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("types");
            // Discover components iteratively while writing. Appended elements
            // are visited too, without recursion or a second persistent table.
            for (int t = 0; t < types.Count; t += 1)
            {
                SemanticType type = types[t];
                writer.WriteStartObject();
                writer.WriteNumber("id", t);
                writer.WriteString("kind", type.Kind.ToString());
                writer.WriteString("name", SemanticTypes.Name(type));
                writer.WriteString("scalar", type.Kind == SemanticTypeKind.Scalar
                    ? type.ScalarTag.ToString() : null);
                if (type.Element is null)
                {
                    writer.WriteNull("elementTypeId");
                }
                else
                {
                    writer.WriteNumber("elementTypeId", TypeId(type.Element));
                }
                WriteSymbol("classId", type.Declaration);
                writer.WriteStartArray("componentTypeIds");
                for (int c = 0; c < type.ComponentCount(); c += 1)
                {
                    writer.WriteNumberValue(TypeId(type.ComponentAt(c)));
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        output.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
