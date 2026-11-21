using System.Collections.Generic;
using System.Text;

namespace Lyric;

internal static partial class IdentifierRules
{
    internal static partial bool IsStart(int scalar)
    {
        return scalar == '_' || Contains(StartRanges, scalar);
    }

    internal static partial bool IsContinue(int scalar)
    {
        return scalar == '_' || Contains(ContinueRanges, scalar);
    }

    private static partial bool Contains(int[] ranges, int scalar)
    {
        // Property records are ordered by code point. Binary search bounds the
        // per-character cost without allocating a full Unicode-sized bitmap.
        int low = 0;
        int high = ranges.Length / 2 - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            if (scalar < ranges[middle * 2])
            {
                high = middle - 1;
            }
            else if (scalar > ranges[middle * 2 + 1])
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    private static partial int CombiningClass(int scalar)
    {
        return CombiningClasses.TryGetValue(scalar, out int value) ? value : 0;
    }

    private static partial void Decompose(int scalar, List<int> result)
    {
        // Unicode's algorithmic Hangul decomposition avoids storing every syllable.
        int syllable = scalar - 0xAC00;
        if (syllable >= 0 && syllable < 11172)
        {
            result.Add(0x1100 + syllable / 588);
            result.Add(0x1161 + (syllable % 588) / 28);
            int trailing = syllable % 28;
            if (trailing != 0)
            {
                result.Add(0x11A7 + trailing);
            }

            return;
        }

        if (Decompositions.TryGetValue(scalar, out int[]? parts))
        {
            foreach (int part in parts)
            {
                Decompose(part, result);
            }

            return;
        }

        result.Add(scalar);
    }

    private static partial int Compose(int first, int second)
    {
        int leading = first - 0x1100;
        int vowel = second - 0x1161;
        if (leading >= 0 && leading < 19 && vowel >= 0 && vowel < 21)
        {
            return 0xAC00 + (leading * 21 + vowel) * 28;
        }

        int syllable = first - 0xAC00;
        int trailing = second - 0x11A7;
        if (syllable >= 0 && syllable < 11172 && syllable % 28 == 0
            && trailing > 0 && trailing < 28)
        {
            return first + trailing;
        }

        long key = (long)first * 0x200000 + second;
        return Compositions.TryGetValue(key, out int composite) ? composite : -1;
    }

    internal static partial string Normalize(string text)
    {
        List<int> decomposed = new();
        foreach (Rune rune in text.EnumerateRunes())
        {
            Decompose(rune.Value, decomposed);
        }

        // Stable canonical ordering never moves a mark across a class-zero starter
        // or an equal-class mark. Source spelling itself remains untouched.
        for (int i = 1; i < decomposed.Count; i += 1)
        {
            int scalar = decomposed[i];
            int currentClass = CombiningClass(scalar);
            int position = i;
            while (currentClass != 0 && position > 0
                && CombiningClass(decomposed[position - 1]) > currentClass)
            {
                decomposed[position] = decomposed[position - 1];
                position -= 1;
            }

            decomposed[position] = scalar;
        }

        List<int> composed = new();
        int starterPosition = -1;
        int lastClass = 0;
        foreach (int scalar in decomposed)
        {
            int currentClass = CombiningClass(scalar);
            int composite = -1;
            if (starterPosition >= 0 && (lastClass == 0 || lastClass < currentClass))
            {
                composite = Compose(composed[starterPosition], scalar);
            }

            if (composite >= 0)
            {
                composed[starterPosition] = composite;
                // A consumed mark is no longer a blocker for a later composition.
                continue;
            }

            if (currentClass == 0)
            {
                starterPosition = composed.Count;
            }

            composed.Add(scalar);
            lastClass = currentClass;
        }

        StringBuilder result = new();
        foreach (int scalar in composed)
        {
            result.Append(new Rune(scalar).ToString());
        }

        return result.ToString();
    }
}
