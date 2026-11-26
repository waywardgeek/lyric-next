using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Lyric;

internal sealed partial class SourceFile
{
    internal partial Diagnostic? Lex()
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Cannot lex a destroyed source.");
        }

        // A new lexical pass replaces the previous parse, including unfinished nodes.
        ClearSyntax(0);

        while (Tokens.Count != 0)
        {
            Tokens[Tokens.Count - 1].Destroy();
        }

        // Lex is safe as a standalone operation too: never decode unvalidated
        // bytes, even if the orchestration already ran source inspection.
        Diagnostic? invalidSource = Scan(null);
        if (invalidSource is not null)
        {
            return invalidSource;
        }

        int offset = 0;
        int line = 1;
        int column = 1;
        List<int> delimiters = new();
        bool afterDot = false;
        // -1 marks an interpolation expression opening. -2 marks f-string text.
        // Ordinary delimiters share this stack so nested braces restore newlines.
        // A nested f-string temporarily puts its own text marker above expression
        // delimiters; finishing it reveals the original expression unchanged.

        // These local functions share only this invocation's cursor. No copied
        // source buffer, hidden module state, or independently owned scanner exists.
        int Scalar()
        {
            if (offset == Bytes.Length)
            {
                return -1;
            }

            Rune.DecodeFromUtf8(Bytes.AsSpan(offset), out Rune rune, out _);
            return rune.Value;
        }

        void Advance()
        {
            Rune.DecodeFromUtf8(Bytes.AsSpan(offset), out Rune rune, out int width);
            offset += width;
            if (rune.Value == 13)
            {
                offset += 1; // Source validation established the following LF.
            }

            if (rune.Value is 10 or 13)
            {
                line += 1;
                column = 1;
            }
            else
            {
                column += 1;
            }
        }

        bool Starts(string text)
        {
            // All callers use ASCII delimiters/operators.
            if (offset + text.Length > Bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < text.Length; i += 1)
            {
                if (Bytes[offset + i] != text[i])
                {
                    return false;
                }
            }

            return true;
        }

        static bool Digit(int scalar)
        {
            return scalar >= '0' && scalar <= '9';
        }

        static int Hex(int scalar)
        {
            if (scalar >= '0' && scalar <= '9')
            {
                return scalar - '0';
            }

            if (scalar >= 'a' && scalar <= 'f')
            {
                return scalar - 'a' + 10;
            }

            return scalar >= 'A' && scalar <= 'F' ? scalar - 'A' + 10 : -1;
        }

        Diagnostic LiteralError(string message, int at, int atLine, int atColumn)
        {
            return new Diagnostic("LYC104", message, at, atLine, atColumn);
        }

        // Escaped bytes are data, not source: never run them through delimiter or
        // source-ban recognition again. Unicode escapes append UTF-8 bytes.
        Diagnostic? Escape(List<byte> value, bool allowUnicode)
        {
            int at = offset;
            int atLine = line;
            int atColumn = column;
            Advance();
            int escaped = Scalar();
            if (escaped < 0)
            {
                return LiteralError("Incomplete escape.", at, atLine, atColumn);
            }

            if (escaped == 'u' && allowUnicode)
            {
                Advance();
                if (Scalar() != '{')
                {
                    return LiteralError("Invalid Unicode escape.", at, atLine, atColumn);
                }

                Advance();
                int scalarValue = 0;
                int digits = 0;
                while (Hex(Scalar()) >= 0 && digits < 6)
                {
                    scalarValue = scalarValue * 16 + Hex(Scalar());
                    digits += 1;
                    Advance();
                }

                if (digits == 0 || Scalar() != '}' || !Rune.IsValid(scalarValue))
                {
                    return LiteralError("Invalid Unicode escape.", at, atLine, atColumn);
                }

                Advance();
                value.AddRange(Encoding.UTF8.GetBytes(new Rune(scalarValue).ToString()));
                return null;
            }

            if (escaped == 'x')
            {
                Advance();
                int high = Hex(Scalar());
                if (high < 0)
                {
                    return LiteralError("Invalid byte escape.", at, atLine, atColumn);
                }

                Advance();
                int low = Hex(Scalar());
                if (low < 0)
                {
                    return LiteralError("Invalid byte escape.", at, atLine, atColumn);
                }

                Advance();
                value.Add((byte)(high * 16 + low));
                return null;
            }

            int decoded = escaped switch
            {
                'n' => 10,
                'r' => 13,
                't' => 9,
                '\\' => 92,
                '\'' => 39,
                '"' => 34,
                '0' => 0,
                _ => -1
            };
            if (decoded < 0)
            {
                return LiteralError("Unknown escape.", at, atLine, atColumn);
            }

            Advance();
            value.Add((byte)decoded);
            return null;
        }

        void AppendScalar(List<byte> value)
        {
            int start = offset;
            Advance();
            for (int i = start; i < offset; i += 1)
            {
                value.Add(Bytes[i]);
            }
        }

        while (offset < Bytes.Length)
        {
            int scalar = Scalar();
            if (delimiters.Count != 0 && delimiters[^1] == -2)
            {
                int textStart = offset;
                int textLine = line;
                int textColumn = column;
                List<byte> textValue = new();
                while (offset < Bytes.Length)
                {
                    scalar = Scalar();
                    if (scalar == '"' || (scalar == '{' && !Starts("{{")))
                    {
                        break;
                    }

                    if (scalar is 10 or 13 || (scalar == '}' && !Starts("}}")))
                    {
                        return LiteralError("Invalid interpolated string text.", offset, line, column);
                    }

                    if (scalar == '\\')
                    {
                        Diagnostic? error = Escape(textValue, true);
                        if (error is not null)
                        {
                            return error;
                        }
                    }
                    else if (Starts("{{") || Starts("}}"))
                    {
                        textValue.Add((byte)scalar);
                        Advance();
                        Advance();
                    }
                    else
                    {
                        AppendScalar(textValue);
                    }
                }

                if (offset > textStart)
                {
                    _ = new Token(this, "string-text", textStart, offset - textStart,
                        textLine, textColumn, null, textValue.ToArray());
                }

                if (offset == Bytes.Length)
                {
                    return LiteralError("Unterminated interpolated string.", offset, line, column);
                }

                string marker = Scalar() == '"' ? "fstring-end" : "interpolation-start";
                int markerOffset = offset;
                int markerLine = line;
                int markerColumn = column;
                if (Scalar() == '"')
                {
                    delimiters.RemoveAt(delimiters.Count - 1);
                }
                else
                {
                    delimiters.Add(-1);
                }

                Advance();
                _ = new Token(this, marker, markerOffset, 1, markerLine, markerColumn, null, null);
                afterDot = false;
                continue;
            }
            if (scalar is ' ' or '\t')
            {
                Advance();
                continue;
            }

            if (Starts("//"))
            {
                while (offset < Bytes.Length && Scalar() is not (10 or 13))
                {
                    Advance();
                }

                continue; // Leave the line ending for the ordinary newline rule.
            }

            int begin = offset;
            int beginLine = line;
            int beginColumn = column;
            string kind;
            string? suffix = null;
            byte[]? literalValue = null;
            if (scalar is 10 or 13)
            {
                Advance();
                if (delimiters.Count != 0 && delimiters[^1] is '(' or '[' or -1)
                {
                    continue;
                }

                kind = "newline";
            }
            else if (Starts("f\""))
            {
                if (Starts("f\"\"\""))
                {
                    return LiteralError("Triple interpolated strings are not supported.", begin, beginLine, beginColumn);
                }

                Advance();
                Advance();
                delimiters.Add(-2);
                kind = "fstring-start";
            }
            else if (scalar is '"' or '\'' or '`')
            {
                bool triple = Starts("\"\"\"");
                bool raw = triple || scalar == '`';
                int closing = scalar;
                string terminator = triple ? "\"\"\"" : ((char)closing).ToString();
                int openingLength = terminator.Length;
                for (int i = 0; i < openingLength; i += 1)
                {
                    Advance();
                }

                List<byte> value = new();
                while (offset < Bytes.Length && !Starts(terminator))
                {
                    if (!triple && Scalar() is 10 or 13)
                    {
                        return LiteralError("Newline in literal.", offset, line, column);
                    }

                    if (!raw && Scalar() == '\\')
                    {
                        Diagnostic? error = Escape(value, closing != '\'');
                        if (error is not null)
                        {
                            return error;
                        }
                    }
                    else
                    {
                        AppendScalar(value);
                    }
                }

                if (offset == Bytes.Length)
                {
                    return LiteralError("Unterminated literal.", begin, beginLine, beginColumn);
                }

                for (int i = 0; i < openingLength; i += 1)
                {
                    Advance();
                }

                if (closing == '\'' && value.Count != 1)
                {
                    return LiteralError("Byte literal must contain exactly one byte.", begin, beginLine, beginColumn);
                }

                if (triple)
                {
                    // Trim bytes only at the boundaries. The ASCII rule cannot
                    // split a multibyte UTF-8 scalar or alter internal indentation.
                    int first = 0;
                    int end = value.Count;
                    while (first < end && (value[first] is >= 9 and <= 13 or 32))
                    {
                        first += 1;
                    }

                    while (end > first && (value[end - 1] is >= 9 and <= 13 or 32))
                    {
                        end -= 1;
                    }

                    value = value.GetRange(first, end - first);
                }

                literalValue = value.ToArray();
                kind = closing == '\'' ? "byte" : closing == '`' ? "symbol" : "string";
            }
            else if (scalar == '}' && delimiters.Count != 0 && delimiters[^1] == -1)
            {
                Advance();
                delimiters.RemoveAt(delimiters.Count - 1);
                kind = "interpolation-end";
            }
            else if (IdentifierRules.IsStart(scalar))
            {
                Advance();
                while (IdentifierRules.IsContinue(Scalar()))
                {
                    Advance();
                }

                string spelling = Encoding.UTF8.GetString(Bytes, begin, offset - begin);
                string normalized = IdentifierRules.Normalize(spelling);
                if (spelling != normalized)
                {
                    // JSON escaping renders the suggestion without emitting raw
                    // direction-changing or other invisible display controls.
                    string suggestion = JsonSerializer.Serialize(normalized);
                    return new Diagnostic("LYC101", $"Identifier is not NFC; use {suggestion}.",
                        begin, beginLine, beginColumn);
                }

                kind = spelling switch
                {
                    "_" => "discard",
                    "func" or "class" or "struct" or "enum" or "interface" or
                    "relation" or "destructor" or "import" or "impl" or "as" or
                    "is" or "type" or "typeof" or "where" or "owns" or "refs" or "ref" or
                    "self" or "from" or "true" or "false" or "null" or "pub" or
                    "let" or "var" or "if" or "else" or "for" or "in" or "while" or
                    "match" or "matchtype" or "return" or "break" or "continue" or "yield" or "extends"
                        => "keyword",
                    _ => "identifier"
                };
            }
            else if (Digit(scalar))
            {
                bool tupleIndex = afterDot;
                bool floating = false;
                bool malformed = false;
                void Digits()
                {
                    while (Digit(Scalar()))
                    {
                        Advance();
                        if (!tupleIndex && Scalar() == '_')
                        {
                            Advance();
                            if (!Digit(Scalar()))
                            {
                                malformed = true;
                                return;
                            }
                        }
                    }
                }

                Digits();
                if (!tupleIndex && Starts(".") && offset + 1 < Bytes.Length
                    && Digit(Bytes[offset + 1]))
                {
                    floating = true;
                    Advance();
                    Digits();
                }

                int suffixStart = offset;
                while (IdentifierRules.IsContinue(Scalar()))
                {
                    Advance();
                }

                if (offset != suffixStart)
                {
                    suffix = Encoding.UTF8.GetString(Bytes, suffixStart, offset - suffixStart);
                    bool integerSuffix = suffix is "u8" or "u16" or "u32" or "u64"
                        or "i8" or "i16" or "i32" or "i64";
                    bool floatSuffix = suffix is "f32" or "f64";
                    malformed |= tupleIndex || (!integerSuffix && !floatSuffix)
                        || (floating && integerSuffix);
                    floating |= floatSuffix;
                }

                if (malformed)
                {
                    return new Diagnostic("LYC102", "Malformed numeric literal.",
                        begin, beginLine, beginColumn);
                }

                kind = tupleIndex ? "tuple-index" : floating ? "float" : "integer";
            }
            else
            {
                // Longest first. !as has the same whole-word boundary as keywords.
                string? punctuation = null;
                string[] candidates =
                {
                    "!+=", "!-=", "!*=", "<->", "!as", "->", "=>", "==", "!=",
                    "<=", ">=", "<<", ">>", "&&", "||", "+=", "-=", "*=", "/=",
                    "!+", "!-", "!*", "(", ")", "[", "]", "{", "}", ".", ",",
                    ":", ";", "?", "!", "+", "-", "*", "/", "%", "=", "<", ">",
                    "&", "|", "^"
                };
                foreach (string candidate in candidates)
                {
                    if (!Starts(candidate))
                    {
                        continue;
                    }

                    if (candidate == "!as" && offset + 3 < Bytes.Length)
                    {
                        Rune.DecodeFromUtf8(Bytes.AsSpan(offset + 3), out Rune next, out _);
                        if (IdentifierRules.IsContinue(next.Value))
                        {
                            continue;
                        }
                    }

                    punctuation = candidate;
                    break;
                }

                if (punctuation is null)
                {
                    return new Diagnostic("LYC103", $"Unexpected source character U+{scalar:X4}.",
                        begin, beginLine, beginColumn);
                }

                foreach (char unused in punctuation)
                {
                    Advance();
                }

                if (punctuation is "(" or "[" or "{")
                {
                    delimiters.Add(scalar);
                }
                else if (punctuation is ")" or "]" or "}")
                {
                    int opening = scalar == ')' ? '(' : scalar == ']' ? '[' : '{';
                    if (delimiters.Count != 0 && delimiters[^1] == opening)
                    {
                        delimiters.RemoveAt(delimiters.Count - 1);
                    }
                }

                kind = "punctuation";
            }

            _ = new Token(this, kind, begin, offset - begin, beginLine, beginColumn, suffix, literalValue);
            afterDot = kind == "punctuation" && Bytes[begin] == '.';
        }

        if (delimiters.Contains(-2))
        {
            return LiteralError("Unterminated interpolated string.", offset, line, column);
        }

        _ = new Token(this, "end", offset, 0, line, column, null, null);
        return null;
    }

    internal partial void WriteTokens(TextWriter output)
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Cannot inspect a destroyed source.");
        }

        foreach (Token token in Tokens)
        {
            string text = Encoding.UTF8.GetString(Bytes, token.ByteOffset, token.ByteLength);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                kind = token.Kind,
                text,
                byteOffset = token.ByteOffset,
                byteLength = token.ByteLength,
                line = token.Line,
                column = token.Column,
                suffix = token.Suffix,
                // System.Text.Json encodes byte[] as base64; expose integers so
                // inspection is language-independent and each byte is visible.
                value = token.Value is null ? null : Array.ConvertAll(token.Value, item => (int)item)
            }));
        }
    }
}
