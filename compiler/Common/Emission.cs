using System;
using System.IO;

namespace Lyric;

internal sealed partial class Compilation
{
    // Requires successful executable checking of the current sources. Builds a
    // complete translation unit before publishing any output. Backend limitations
    // return a located diagnostic and leave output empty.
    internal partial Diagnostic? EmitC(TextWriter output, bool structureOfArrays = false);
}

internal sealed partial class SourceFile
{
    // Read-only access to already-decoded literal data by its exact source span.
    // The span is temporary and cannot outlive source/token mutation. No source
    // spelling is reparsed and no persistent token relationship is introduced.
    internal partial ReadOnlySpan<byte> LiteralBytes(int offset, int length);
}
