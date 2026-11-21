using System;

namespace Lyric;

internal sealed partial class Token
{
    internal partial Token(SourceFile source, string kind, int byteOffset,
        int byteLength, int line, int column, string? suffix, byte[]? value)
    {
        Kind = kind;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        Line = line;
        Column = column;
        Suffix = suffix;
        Value = value;
        source.AddToken(this);
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }

        Source?.RemoveToken(this);
        Destroyed = true;
    }
}

internal sealed partial class SourceFile
{
    internal partial void AddToken(Token token)
    {
        if (Destroyed || token.Destroyed || token.Source is not null)
        {
            throw new InvalidOperationException("Cannot attach token to this source.");
        }

        Tokens.Add(token);
        token.Source = this;
    }

    internal partial void RemoveToken(Token token)
    {
        // Reverse destruction removes the last token. Search from that end so
        // teardown is linear overall, while arbitrary removal preserves order.
        int index = Tokens.LastIndexOf(token);
        if (token.Source != this || index < 0)
        {
            throw new InvalidOperationException("Token does not belong to this source.");
        }

        Tokens.RemoveAt(index);
        token.Source = null;
    }
}
