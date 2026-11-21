namespace Lyric;

internal static partial class SourceRules
{
    internal static partial string? ForbiddenProperty(int scalar)
    {
        // The small sorted table favors an obvious bounded scan. No host Unicode
        // property API participates, so an OS update cannot change acceptance.
        for (int index = 0; index < ForbiddenRanges.Length; index += 2)
        {
            if (scalar < ForbiddenRanges[index])
            {
                return null;
            }

            if (scalar <= ForbiddenRanges[index + 1])
            {
                // In Unicode 16.0.0 every Bidi_Control is also default-ignorable.
                // The generator checks that inclusion rather than assuming it.
                if (scalar <= 0x9F)
                {
                    return "General_Category=Cc";
                }

                return "Default_Ignorable_Code_Point";
            }
        }

        return null;
    }
}
