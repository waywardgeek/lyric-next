using System.Collections.Generic;

namespace Lyric;

// Pinned Unicode operations. Normalization returns a new value; it never changes
// source bytes. The lexer will compare spellings and report a suggestion, not
// silently normalize identifiers. These operations also serve inspection graders.
internal static partial class IdentifierRules
{
    internal static partial bool IsStart(int scalar);
    internal static partial bool IsContinue(int scalar);
    internal static partial string Normalize(string text);

    private static partial bool Contains(int[] ranges, int scalar);
    private static partial int CombiningClass(int scalar);
    private static partial void Decompose(int scalar, List<int> result);
    private static partial int Compose(int first, int second);
}
