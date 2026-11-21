# Chapter 2 graders — Unicode and token inspection

## Retained Unicode foundation check

From the repository root, with Python 3.10+ and a built implementation:

```sh
python3 graders/ch02/unicode.py -- dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

Replace the command after `--` with the independently supplied implementation.
The grader requires adjacent pinned `unicode/16.0.0/` data. No network is used.
The main batched invocation has a 120-second timeout; malformed-input cases each
have 10 seconds. Timeout/crash/arbitrary nonzero exit never counts as rejection.

`--inspect-unicode FILE` reads JSON lines, each an array of integer Unicode scalar
values. These are Unicode data, not Lyric source: source-wide bans do not apply.
An empty file produces no records. Every valid input line produces one JSON object
with exactly `nfc`, `start`, and `continuation`. `nfc` is the normalized scalar
array; the other arrays contain booleans for each original scalar's Lyric XID start
and continuation membership (including underscore). Source-specific bans and
whole-identifier validation are separate, later lexer operations.

Valid input exits 0 with empty stderr. Object key order and whitespace are ignored;
record order, exact keys, values, and JSON types are checked. Invalid JSON, non-array
or null records, noninteger elements, surrogates, and out-of-range values exit 1,
produce empty stdout and exactly `error LYC005: Invalid Unicode inspection data.`
plus LF on stderr. Validate all requests before producing output. Unreadable files
use the existing LYC004 contract. This inspection calls the real compiler Unicode
component, not a fixture-specific implementation.

Retained groups:
- `unicode-nfc`: all five official NFC relations per NormalizationTest row plus empty input.
- `unicode-xid`: property membership for every Unicode scalar, independently read from upstream data.
- `unicode-request-rejection`: seven malformed requests after a valid first record; no partial stdout.

Success exits 0 and reports all 99,826 NFC vectors, 1,112,064 classified scalars,
and seven rejected malformed requests. This is not full lexer conformance. The
Chapter 1 retained suite remains required alongside the token checks below.

## Retained token inspection

```sh
python3 graders/ch02/tokens.py -- dotnet compiler/bin/Debug/net10.0/Lyric.dll
```

`--inspect-tokens FILE` first validates the entire file under Chapter 1's rules,
then lexes it. Exit 0 requires empty stderr and one JSON object per token,
including an end token. Exit 1 requires empty stdout and one located diagnostic
in Chapter 1's format. No partial tokens are printed on failure. Exit 2 remains
incorrect command usage. These modes do not claim that input is a valid program.

Every record has exactly these keys:
- `kind`: `identifier`, `keyword`, `discard`, `integer`, `float`, `tuple-index`,
  `punctuation`, `newline`, `string`, `byte`, `symbol`, `fstring-start`,
  `fstring-end`, `string-text`, `interpolation-start`, `interpolation-end`, or `end`.
- `text`: exact original spelling decoded from the source bytes, not normalized.
- `byteOffset`, `byteLength`, `line`, `column`: Chapter 1's positions. Length covers
  the complete spelling including quotes, suffixes, and escape syntax. A CRLF
  newline token has length two and text CRLF. The end token is zero length.
- `suffix`: the explicit numeric suffix or JSON null. Magnitudes are not converted.
- `value`: decoded bytes as a JSON integer array for string/byte/symbol/string-text
  tokens, including empty arrays; JSON null for all other tokens. No base64.

Key order, insignificant JSON whitespace, and equivalent JSON string escaping are
ignored. Record order, keys, text, values and JSON types must match. Boolean values
are not numeric positions or bytes. The token grader uses the Chapter 1 type-aware
JSON comparator, not Python's permissive boolean/number equality.

The hard keyword list is Section 3's list; contextual words remain identifiers.
After a member dot, digits form `tuple-index`, not a float or suffixed number.
Identifiers must already be NFC; the lexer rejects instead of rewriting them.
Numeric representability and tuple-index range remain semantic checks.

Comments and space/tab are skipped. A comment leaves its newline. The nearest
unmatched ordinary delimiter controls newline suppression: `(`/`[` suppress,
`{` restores. Interpolation openings also suppress. Ordinary unmatched delimiters
are left for the parser; an unfinished f-string is a lexical error. Longest
punctuation wins, with the identifier boundary required for `!as`. There is no
parser-directed splitting of `>>` in this mode.

An f-string emits `fstring-start` covering `f"`, zero or more nonempty source
text spans as `string-text`, expression openings/closings as interpolation
markers, and `fstring-end` covering the closing quote. Each maximal text span
includes its original escape syntax while `value` holds decoded bytes. Empty
text segments are omitted. Nested expressions use ordinary tokens; nested
f-strings repeat the same structure. Empty interpolations are a parser concern.
Triple f-strings remain excluded. Ordinary strings, raw triple strings, and
symbols each produce one token; raw triples preserve internal CRLF bytes.

Lexical diagnostics:
- LYC101: `Identifier is not NFC; use JSON_STRING.` at the identifier start.
  The suggestion is NFC, JSON quoted with equivalent escaping permitted.
- LYC102: `Malformed numeric literal.` at the literal start.
- LYC103: `Unexpected source character U+XXXX.` at the unknown scalar.
- LYC104: literal failure. `Incomplete escape.`, `Invalid Unicode escape.`,
  `Invalid byte escape.`, and `Unknown escape.` locate the backslash;
  `Newline in literal.` and `Invalid interpolated string text.` locate the
  offending scalar; `Unterminated interpolated string.` locates EOF;
  `Unterminated literal.`, `Byte literal must contain exactly one byte.`, and
  `Triple interpolated strings are not supported.` locate the opening delimiter.

All 58 current token cases are retained. They cover keywords/identifiers/NFC,
number/suffix boundaries, tuple selectors, punctuation, comments/newlines,
byte/string/raw/symbol decoding, interpolation nesting, and specific rejections.
Each invocation has a ten-second timeout. This is representative lexer coverage,
not exhaustive input enumeration. Run both Debug and Release commands, the
Unicode foundation check, and all 82 Chapter 1 cases for each configuration.
