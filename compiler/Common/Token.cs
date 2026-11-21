namespace Lyric;

// A source strongly owns each token. The span points into its immutable original
// bytes; spelling is never stored a second time. Suffix is semantic lexical
// metadata, not a converted magnitude. Decoded literal bytes are value-like data,
// transferred to this token at construction and never subsequently mutated.
internal sealed partial class Token
{
    internal SourceFile? Source;
    internal readonly string Kind;
    internal readonly int ByteOffset;
    internal readonly int ByteLength;
    internal readonly int Line;
    internal readonly int Column;
    internal readonly string? Suffix;
    internal readonly byte[]? Value;
    internal bool Destroyed;

    // Fully initializes the token, then attaches through the source's method.
    internal partial Token(SourceFile source, string kind, int byteOffset,
        int byteLength, int line, int column, string? suffix, byte[]? value);

    // Idempotently removes both ends of the source relationship.
    internal partial void Destroy();
}
