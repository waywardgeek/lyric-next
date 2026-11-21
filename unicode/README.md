# Pinned Unicode data

Lyric's initial source-character contract uses Unicode **16.0.0**. These files are unmodified upstream data, not a reference compiler or private test fixtures.

- `16.0.0/DerivedCoreProperties.txt`: https://www.unicode.org/Public/16.0.0/ucd/DerivedCoreProperties.txt
- `16.0.0/PropList.txt`: https://www.unicode.org/Public/16.0.0/ucd/PropList.txt
- `16.0.0/UnicodeData.txt`: https://www.unicode.org/Public/16.0.0/ucd/UnicodeData.txt
- `16.0.0/DerivedNormalizationProps.txt`: https://www.unicode.org/Public/16.0.0/ucd/DerivedNormalizationProps.txt
- `16.0.0/NormalizationTest.txt`: https://www.unicode.org/Public/16.0.0/ucd/NormalizationTest.txt
- `LICENSE.txt`: https://www.unicode.org/license.txt

`SHA256SUMS` records the downloaded bytes. The compiler's generated source-ban table uses Default_Ignorable_Code_Point, Bidi_Control, and General_Category Cc. HT, LF, and CR have explicit scanner handling. Identifier XID and NFC checks are not implemented in Chapter 1.

The reference table can be regenerated from the repository root with `python3 tools/generate_source_properties.py`. That tool belongs to the reference implementation and is not supplied to reconstruction students; students construct their own table generator from Chapter 1 and the upstream data. Graders read these upstream data independently; they do not import any generator or compiler table.

Chapter 2 adds pinned XID classification and NFC using canonical decompositions,
combining classes, composition exclusions, and algorithmic Hangul rules. Its
reference-only generator is `tools/generate_identifier_properties.py`. Public
graders use the official property data and normalization vectors independently.
