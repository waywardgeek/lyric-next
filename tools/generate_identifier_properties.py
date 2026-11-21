"""Generate pinned Unicode identifier/NFC data; not part of the public graders."""
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / 'unicode/16.0.0'

def ranges(filename, wanted):
    result = []
    for raw in (DATA / filename).read_text().splitlines():
        parts = [p.strip() for p in raw.partition('#')[0].split(';')]
        if len(parts) < 2 or parts[1] != wanted:
            continue
        ends = parts[0].split('..')
        result.append((int(ends[0], 16), int(ends[-1], 16)))
    return result

excluded = set()
for lo, hi in ranges('DerivedNormalizationProps.txt', 'Full_Composition_Exclusion'):
    excluded.update(range(lo, hi + 1))
classes = []
decompositions = []
compositions = []
for raw in (DATA / 'UnicodeData.txt').read_text().splitlines():
    fields = raw.split(';')
    scalar = int(fields[0], 16)
    combining = int(fields[3])
    if combining:
        classes.append((scalar, combining))
    spelling = fields[5]
    # NFC uses canonical mappings, not tagged compatibility mappings.
    if spelling and not spelling.startswith('<'):
        parts = [int(x, 16) for x in spelling.split()]
        decompositions.append((scalar, parts))
        # Reverse eligible canonical pairs for composition; exclusions must
        # remain decomposed even when a two-scalar mapping exists.
        if len(parts) == 2 and scalar not in excluded:
            compositions.append((parts[0], parts[1], scalar))

text = '''// Generated from official Unicode 16.0.0 data by tools/generate_identifier_properties.py.
// See unicode/LICENSE.txt at the repository root. Do not hand-edit these tables.
namespace Lyric;

// Immutable Unicode value data; no live-object relationships.
internal static partial class IdentifierRules
{
'''
for name, prop in [('StartRanges', 'XID_Start'), ('ContinueRanges', 'XID_Continue')]:
    text += f'    private static readonly int[] {name} =\n    [\n'
    text += ''.join(f'        0x{a:X}, 0x{b:X},\n' for a,b in ranges('DerivedCoreProperties.txt', prop))
    text += '    ];\n\n'
text += '    private static readonly System.Collections.Generic.Dictionary<int, int> CombiningClasses = new()\n    {\n'
text += ''.join(f'        [0x{s:X}] = {c},\n' for s,c in classes)
text += '    };\n\n    private static readonly System.Collections.Generic.Dictionary<int, int[]> Decompositions = new()\n    {\n'
for scalar, parts in decompositions:
    mapping = ", ".join(f"0x{part:X}" for part in parts)
    text += f"        [0x{scalar:X}] = [{mapping}],\n"
text += '    };\n\n    private static readonly System.Collections.Generic.Dictionary<long, int> Compositions = new()\n    {\n'
text += ''.join(f'        [((long)0x{a:X} << 21) | 0x{b:X}] = 0x{c:X},\n' for a,b,c in compositions)
text += '    };\n}\n'
(ROOT / 'compiler/Common/IdentifierRules.Data.cs').write_text(text)
print(f'Generated {len(classes)} combining classes, {len(decompositions)} canonical decompositions, {len(compositions)} composition pairs.')
