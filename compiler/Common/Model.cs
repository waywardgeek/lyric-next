using System.Collections.Generic;
using System.IO;

namespace Lyric;

// Compilation is the strong owner of loaded sources. Its collection and every
// SourceFile.Parent agree after each successful relationship operation.
internal sealed partial class Compilation
{
    private readonly List<SourceFile> Sources = new();
    private bool Destroyed;

    internal partial Compilation();

    // Attaches a detached source at both ends; refusal leaves both unchanged.
    internal partial void AddSource(SourceFile source);

    // Detaches both ends without destroying the source. Refuses wrong membership.
    internal partial void RemoveSource(SourceFile source);

    // Idempotent logical destruction: destroy sources, which detach themselves.
    internal partial void Destroy();
}

// One source owns its original byte buffer. Loading transfers that buffer here;
// no caller subsequently mutates it. Source text is never normalized or replaced.
internal sealed partial class SourceFile
{
    internal Compilation? Parent;
    internal readonly string Path;
    private readonly byte[] Bytes;
    internal bool Destroyed;
    private readonly List<Token> Tokens = new();

    // Sets complete source state, then establishes both ends of strong ownership.
    internal partial SourceFile(Compilation parent, string path, byte[] bytes);

    // Token relationships are maintained at both ends; refusal changes neither.
    internal partial void AddToken(Token token);
    internal partial void RemoveToken(Token token);

    // Clears previous tokens and lexes validated bytes. On failure, the caller
    // emits no tokens. All token objects remain owned until retry or destruction.
    internal partial Diagnostic? Lex();
    internal partial void WriteTokens(TextWriter output);

    // Detaches from the owner before dropping the source's logical lifetime.
    internal partial void Destroy();

    // Scans the real source. Null output validates silently; non-null output emits
    // inspection events. The driver validates first to avoid partial success output.
    // Returns a value-like diagnostic on the first error, otherwise null.
    internal partial Diagnostic? Scan(TextWriter? output);
}

// Value-like diagnostic: no source-object reference, ownership, or holder backlinks.
// Its position identifies the first byte of the offending scalar/encoding sequence.
internal sealed partial class Diagnostic
{
    internal readonly string Code;
    internal readonly string Message;
    internal readonly int ByteOffset;
    internal readonly int Line;
    internal readonly int Column;

    internal partial Diagnostic(string code, string message, int byteOffset, int line, int column);
}

// Pure scalar classification against the pinned generated ranges in Common.
internal static partial class SourceRules
{
    internal static partial string? ForbiddenProperty(int scalar);
}
