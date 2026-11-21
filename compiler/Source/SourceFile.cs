using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Lyric;

internal sealed partial class SourceFile
{
    internal partial Diagnostic? Scan(TextWriter? output)
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Cannot scan a destroyed source.");
        }

        // These records contain only fixed keys, numbers, and generated U+XXXX
        // strings, never raw source text. Avoid HTML-style escaping of the plus
        // sign so the JSON remains readable. Filename diagnostics use stricter
        // escaping separately in the driver.
        JsonSerializerOptions? jsonOptions = null;
        if (output is not null)
        {
            jsonOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        int offset = 0;
        int line = 1;
        int column = 1;
        while (offset < Bytes.Length)
        {
            OperationStatus status = Rune.DecodeFromUtf8(
                Bytes.AsSpan(offset), out Rune rune, out int width);
            if (status != OperationStatus.Done)
            {
                // Never consume a replacement scalar or echo unsafe source bytes.
                return new Diagnostic(
                    "LYC001", "Malformed UTF-8 sequence.", offset, line, column);
            }

            int scalar = rune.Value;
            if (scalar == 13)
            {
                if (offset + 1 >= Bytes.Length || Bytes[offset + 1] != 10)
                {
                    return new Diagnostic(
                        "LYC003", "Lone U+000D; use LF or CRLF.", offset, line, column);
                }

                // Expose one newline event, retaining its two-byte source span.
                scalar = 10;
                width = 2;
            }

            string? property = SourceRules.ForbiddenProperty(scalar);
            if (property is not null)
            {
                return new Diagnostic(
                    "LYC002", $"Forbidden source character U+{scalar:X4} ({property}).",
                    offset, line, column);
            }

            if (output is not null)
            {
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    kind = "scalar",
                    byteOffset = offset,
                    byteLength = width,
                    line,
                    column,
                    scalar = $"U+{scalar:X4}"
                }, jsonOptions));
            }

            offset += width;
            if (scalar == 10)
            {
                line += 1;
                column = 1;
            }
            else
            {
                column += 1;
            }
        }

        if (output is not null)
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "end",
                byteOffset = offset,
                line,
                column
            }, jsonOptions));
        }

        return null;
    }
}
