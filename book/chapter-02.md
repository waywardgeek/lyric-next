# Chapter 2 — Recognize Tokens Without Parser Feedback

Parser generators are not the only tools whose tradeoffs change when an AI can write the implementation. The same question applies to Lex and Flex: do we want generated code, or do we want straightforward code that implements a precise specification?

For lexers, this argument is older. I remember people advocating hand-written lexers back in the 1980s. Efficiency was one reason. A scanner is a hot loop, and writing it yourself lets you control exactly what happens for each input byte. Another reason was simply that a lexer for a modest language could often be written in a day or two. If the implementation is that small, how much machinery do we need to avoid writing it?

That does not mean every hand-written lexer beats every generated scanner. Flex can produce efficient scanners, and bad manual code is still bad code. The point is that a lexer is often small enough to understand and tune directly. We do not have to make it mysterious.

AI makes the choice easier for me. I want readable scanning code with explicit state, not an extra framework between the language rules and the loop that reads the source. The model can write the routine parts. We can spend our attention on the rules that are easy to get subtly wrong.

And there are plenty of those. Is `123u16name` a number followed by an identifier, or one malformed numeric spelling? Does a newline matter inside a block nested within parentheses? Does a brace produced by a string escape start interpolation? Which location do we report after a multibyte character? A generated scanner does not relieve us of deciding these things. Neither does an LLM.

So we write this lexer directly, but we do not let its implementation invent the language. The specification defines the rules. Public tests check exact tokens, original byte spans, decoded values, and failures. Unicode properties come from pinned data, not whatever the host library happens to recognize this year.

Writing the scanning loop is the easy part. Knowing exactly what it must recognize—and proving what our checks actually cover—is the work worth keeping.

## TL;DR

Turn Chapter 1's validated source into tokens. Keep original bytes authoritative, attach each token to its source, and retain exact spans rather than copying spelling. Recognize Unicode identifiers, typed numbers, operators, comments, and literals without asking a parser what the characters mean. A small lexical mode stack handles interpolation and significant newlines. Pin identifier properties and normalization to Unicode 16.0.0. Reject non-NFC identifiers with a suggestion; never silently rewrite them.

Build this increment on your accepted Chapter 1 implementation. The [language specification](../lyric-language-spec.md) and [PEG](../lyric-grammar.peg) define the language; the [public Chapter 2 contract](../graders/ch02/README.md) defines inspection records and diagnostics. All Chapter 1 behavior remains available. There is still no AST, expression parser, type checker, or generated program.

## Tokens are decisions about spelling

Consider `123u16`. The lexer recognizes one integer token, including its explicit type suffix. It does not need a parser to tell it that `u16` belongs to the number. Conversely, `123u16name` is malformed: splitting it into a valid number and a name would hide an invalid suffix boundary.

Recognition is not range checking. Keep numeric spelling and suffix without converting the magnitude into a machine integer. A later semantic check decides whether the value fits the requested type. Keep a leading minus separate, so the lexer does not prematurely reject the positive magnitude used to spell a signed minimum. `123f32` and `1.5f64` are float tokens; decimal-point forms cannot carry integer suffixes. Base prefixes and exponent notation are not part of this increment's language contract.

The same separation applies elsewhere. A token stream may contain an unmatched ordinary parenthesis or an empty interpolation. Token inspection must not claim those form valid expressions. It reports lexical decisions, not successful compilation.

## Extend the model, not a second source representation

Chapter 1's compilation owns sources. Now each source strongly owns an ordered collection of tokens, and each token retains its source backlink. A token records its kind, byte offset and length, starting line and scalar column, optional numeric suffix, and optional decoded literal bytes. Its spelling is obtained from its source span. Do not maintain a second authoritative copy of source text.

Decoded bytes are a different fact from spelling. The source spelling `"\xFF"` contains quotes and escape characters; its value contains one byte, 255. That value is not necessarily UTF-8. Represent it as value-like byte data owned by the token, transferred at construction and not subsequently mutated. It needs no holder backlinks. A missing value and an empty literal value are different: use null for the former and an empty array for the latter.

Declare persistent state, relationships, constructors, and callable operations in Common. Implement matching partial members in Model, Lexer, and Unicode modules. Functional modules use Common's vocabulary, not sibling implementation helpers. The driver constructs and connects the system. Compile the partial sections into the same assembly, with no service bag or wrapper connecting declarations to bodies.

A token constructor initializes its data, then calls the source's add method. That method validates that both objects are alive and the token is unattached before changing either end. Removal validates membership, removes the token, and clears its backlink before returning. Refused changes leave both ends unchanged. Token destruction calls removal and is idempotent. Source destruction first detaches the source from its compilation, then destroys its tokens; tokens remove themselves rather than being pre-detached by their parent.

Preserve token order for inspection. Append is sufficient for construction. Destroying from the end, and searching from that end when removing a token, keeps the normal cascade linear overall. Do not search the entire growing collection on every attachment when the backlink already establishes that the token is unattached. That unnecessary check would turn token construction into quadratic work.

## Build reproducible identifier rules

Source validity and identifier validity are different layers. Chapter 1 accepts combining marks as source characters. Chapter 2 recognizes an identifier using Unicode XID_Start followed by XID_Continue, with underscore admitted explicitly in both positions. A standalone underscore is a discard token. Match hard keywords only after consuming the complete identifier; `if` is a keyword, while `iffy` is not. Use the specification's exact hard-keyword list. Contextual words remain identifiers.

Do not ask the host runtime which Unicode version it happens to support. Use the supplied [Unicode 16.0.0 inputs](../unicode/README.md). Generate sorted property ranges from DerivedCoreProperties, canonical combining classes and canonical decompositions from UnicodeData, and composition exclusions from DerivedNormalizationProps. Compatibility decompositions, marked with tags such as `<compat>`, do not belong in NFC. Build non-Hangul composition pairs from eligible two-scalar canonical decompositions, excluding Full_Composition_Exclusion entries. A pair key can be packed as `first × 2²¹ + second` in a sufficiently wide integer. Keep generated immutable tables in Common; keep algorithms in Unicode. Students create their own generator, not copy the reference generator.

Binary search over sorted ranges gives bounded membership lookup without a Unicode-sized bitmap. Missing combining-class entries mean class zero. Normalization then has three stages:

1. **Canonical decomposition.** Recursively expand each canonical mapping. Decompose Hangul syllables arithmetically: subtract U+AC00, select the leading consonant using division by 588, the vowel using division by 28 modulo 21, and an optional trailing consonant using modulo 28. Their bases are U+1100, U+1161, and U+11A7 respectively. There are 11,172 syllables; a zero trailing index means no trailing consonant.
2. **Stable canonical ordering.** Order nonzero-class combining marks by increasing class, without crossing a class-zero starter or changing the order of equal-class marks. A direct stable insertion pass is adequate here. Do not reorder the original source buffer.
3. **Canonical composition.** Track the current starter's output position and the last unconsumed combining class. Try composing the starter with the next scalar only when the intervening class is zero or smaller than the next class. On success replace the starter and consume the scalar; do not make that consumed mark a new blocker. Otherwise append it, update the last class, and make a class-zero scalar the new starter. Alongside table pairs, compose Hangul leading-plus-vowel and an untrailed syllable-plus-trailing consonant arithmetically.

For a blocked-composition trace, use `U+0041 U+0305 U+030A`: A, combining overline, combining ring above. Both marks have class 230. The overline cannot compose with A and remains unconsumed, so its equal class blocks the later A-plus-ring pair. The result stays as those three scalars. Without the overline, `U+0041 U+030A` composes to `U+00C5`. This contrasts an actual intervening blocker with a mark already consumed into the starter.

Compare the resulting NFC string with the original identifier. If they differ, emit LYC101 at its starting position with a JSON-quoted NFC suggestion. Do not normalize the stored spelling, comments, symbols, or string values. This keeps source locations exact and makes an author's noncanonical identifier visible rather than silently changing its identity.

The real Unicode component also supports `--inspect-unicode FILE`, described in the grader contract. Its JSON scalar arrays are data, not Lyric source; source-wide bans therefore do not apply. Validate every request before output. The official normalization vectors and all-scalar XID checks test this component independently of tokenization. This mode must continue calling the same operations used by the lexer.

## Walk the original bytes once the source is valid

Lexing starts by clearing any old token collection through token destruction, then running Chapter 1 validation over the entire source. A forbidden character inside a comment must still fail. Only after validation succeeds may the lexer skip comments or decode escapes.

Keep invocation state local: byte offset, one-based line and scalar column, a delimiter/mode stack, and whether the preceding relevant token was a member dot. Use the original buffer. Advance by a decoded scalar's byte width; treat CRLF as one newline consuming two bytes. Save the starting position before recognizing each token. Token length is the difference between final and starting byte offsets, including delimiters and suffixes.

The main loop must give context-sensitive lexical forms their proper precedence:

- If in interpolated text mode, handle that mode before ordinary token rules.
- Skip space and tab. Skip `//` through the last non-newline character, leaving the newline for its own rule. There are no block comments.
- Recognize line endings, `f"` openings before ordinary identifiers, literal openings, and interpolation closings in their applicable contexts.
- Consume complete identifiers or numeric spellings.
- Otherwise choose the longest applicable punctuation spelling, or diagnose an unexpected source character.

Only a dot followed immediately by a digit extends an ordinary numeric spelling into its fractional part. Decimal digit separators must occur between digits, not before suffixes or at an end. Consume the complete following XID-continuation sequence when checking a suffix; accept only the specified suffix set. Do not let invalid suffixes become a separate identifier.

After a member dot, ASCII digits form a tuple-index token instead. Thus `nested.0.1` has two selectors, not a floating literal. Selectors have neither digit separators nor type suffixes; their range is a later check. Ordinary horizontal whitespace and comments do not by themselves turn the member selector into a float. Significant newline tokens end that preceding-dot state.

Match punctuation longest first, including wrapping operators and compounds. The textual operator `!as` requires an identifier boundary just as a keyword does. Token inspection emits `>>` as one punctuation token: do not introduce parser-directed splitting to solve a later generic grammar problem here.

## Newlines require lexical context, not a parser

Maintain unmatched ordinary delimiters on a stack. The nearest opening parenthesis or bracket suppresses newline tokens. A nearer opening brace restores significant newlines. Interpolation openings suppress newlines in the same way as parentheses. Closing an ordinary delimiter pops its matching top opening; mismatches remain for the parser to diagnose rather than making this inspection mode a partial syntax validator.

This is why an unconditional “skip newlines inside any parentheses” flag is wrong. A block inside a parenthesized expression can need its own line separators. The nearest delimiter, not the existence of some outer parenthesis, controls the decision. Comments leave line endings available for exactly this rule.

## Decode literals without rescanning their values

Ordinary quoted strings produce bytes. Copy unescaped scalars' original UTF-8 bytes; decode escapes into bytes. Support the specified simple escapes, exactly two hexadecimal digits after `\x`, and one through six hexadecimal digits inside `\u{…}` for text literals. A Unicode escape must denote a scalar, not a surrogate or value above U+10FFFF; append its UTF-8 encoding. A byte literal permits byte escapes, not Unicode escapes, and must decode to exactly one byte. An unescaped multibyte character cannot satisfy that one-byte requirement merely because it is one scalar.

A visible Unicode escape may encode a character banned literally in source. That is intentional: the source spelling is visible and already validated. Never feed decoded data back through source bans or delimiter recognition. In particular, an escape decoding to `{` is text, not an interpolation opening.

Ordinary strings, byte literals, and backtick symbols cannot contain literal line endings. Backticks are raw: backslashes are data, not escape introductions. Triple-quoted strings are also raw, can span lines, and terminate at the first closing triple quote. Trim only boundary ASCII whitespace bytes 9 through 13 and 32 from their decoded value. Preserve interior indentation and original internal CRLF bytes. Whole-source validation ran before trimming, so trimming cannot conceal forbidden source characters. Keep full source spans including delimiters and trimmed boundary text.

Follow the public diagnostic contract for incomplete escapes, invalid scalar/byte escapes, unknown escapes, line endings, and missing closing delimiters. Their locations are intentional: an escape error points to its backslash, while an unterminated ordinary literal points to its opening delimiter.

## Interpolation is a stack of lexical modes

An interpolated string emits separate tokens rather than one opaque string:

- `fstring-start` covers `f"`.
- Each maximal nonempty text segment becomes `string-text`, carrying both its original span and decoded bytes.
- A single opening brace emits `interpolation-start` and enters expression mode.
- Its closing brace emits `interpolation-end` and returns to text mode.
- The closing quote emits `fstring-end`.

In text mode, doubled braces become literal brace bytes. Decode ordinary text escapes without rescanning the decoded result. A lone closing brace or literal newline is an error. Empty text segments are omitted. Triple interpolated strings are excluded.

Keep a text marker beneath its interpolation-expression marker on the lexical stack. Ordinary braces above an interpolation marker belong to the expression; only a closing brace exposed directly to the interpolation marker ends that interpolation. A nested f-string pushes its own modes and later reveals the enclosing expression unchanged. This handles nested strings, blocks, and interpolations without parser feedback. End of file while any f-string text marker remains is an unterminated-interpolation error at EOF.

**The construction lesson:** parser independence does not mean stateless scanning. A small explicit lexical stack is sufficient; expression meaning and validity still belong to later phases.

## Inspect the real tokens, not a parallel lexer

Add `--inspect-tokens FILE`. Lex completely before writing any records. On failure report the exact located diagnostic with exit 1 and empty stdout. Tokens constructed before failure remain owned and are cleaned up through the normal source lifecycle; no output is mistaken for partial success. On success print the model's tokens, including a zero-length end token at the final position, with exit 0 and empty stderr.

Each JSON record exposes kind, original text, span and position, suffix, and decoded value. Render decoded bytes as integer arrays, not a serializer's implicit base64 encoding. Preserve null versus empty arrays. JSON escaping and key order are not lexical semantics. The grader accepts equivalent encodings but requires the right keys, record order, values, and types: a boolean must not pass as numeric position one or zero.

The source inspection mode retains its earlier boundary. Unicode inspection retains its data boundary. Token inspection does not claim parsing or successful compilation. Keep all three modes attached to real components as the compiler grows.

## Exercises

### 1. Extend your accepted implementation

Work in your assigned solution workspace using your accepted Chapter 1 code, this teaching, the specification/PEG, public graders, and pinned Unicode fixtures. Do not inspect the Chapter 2 reference compiler, generated tables, reference generator, or private development history. You own implementation changes. Report suspected grader defects; do not edit graders or expectations to obtain a pass.

Implement the Unicode operations, token model, lexer, and two new inspection modes described above. Preserve the architecture and lifecycle rules. You may write diagnostic tests for confidence; only the designated graders and fixtures carry forward across executions.

### 2. Run the cumulative retained checks

Prerequisites remain C# 14/.NET 10 and Python 3.10+. No additional runtime package is needed. Use the supplied pinned Unicode files; tests require no network. Keep the read-only `graders/` and `unicode/` directories adjacent. Set `GRADERS` to the absolute graders path and `DOTNET` to your SDK executable. Commands below run from your solution workspace, assuming `compiler/Lyric.csproj` and the standard output path. Substitute your actual executable command after each `--` if necessary.

```sh
export DOTNET="${DOTNET:-dotnet}"
"$DOTNET" build compiler/Lyric.csproj --nologo --tl:off
python3 "$GRADERS/ch01/grade.py" -- "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 "$GRADERS/ch02/unicode.py" -- "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
python3 "$GRADERS/ch02/tokens.py" -- "$DOTNET" compiler/bin/Debug/net10.0/Lyric.dll
```

Repeat for Release:

```sh
"$DOTNET" build compiler/Lyric.csproj -c Release --nologo --tl:off
python3 "$GRADERS/ch01/grade.py" -- "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
python3 "$GRADERS/ch02/unicode.py" -- "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
python3 "$GRADERS/ch02/tokens.py" -- "$DOTNET" compiler/bin/Release/net10.0/Lyric.dll
```

Both builds and all six grader invocations are required. Each configuration must pass all 82 Chapter 1 cases, all 58 token cases, 99,826 NFC vectors, XID membership for all 1,112,064 scalars, and seven malformed Unicode-request cases. All are retained; there are no chapter-only checks. Stable group identifiers and comparison rules are in the grader READMEs and readable fixtures. Unicode's batched check permits 120 seconds; individual token and malformed-request cases permit ten seconds. Do not drop the Unicode check because it is larger than the focused token cases.

Success means exit zero and every expected check passing. A negative case passes only for the specified exit, diagnostic, location, and empty stdout—not a crash, timeout, or unrelated error. The token cases are representative, not exhaustive enumeration. During reference development, graders also rejected successfully built disposable variants that bypassed NFC, accepted unknown suffixes, or suppressed newlines inside braces; no student is required to copy those mutations.

### 3. Explain the relationships and boundaries

Trace construction, refused attachment, arbitrary token removal, re-lexing, failed lexing, and compilation destruction. Check both relationship ends and demonstrate why repeated destruction is harmless. Explain where the original bytes live, why literal values are not duplicate source authority, and why no parser is needed to tokenize `123u16` or nested f-strings. Trace an NFC blocked-composition case and a newline inside a brace nested within parentheses. CLI passes do not substitute for this structural self-check.

Report actual checks, assistance, and remaining limits. Completion also requires your human collaborator's use; passing graders do not authorize advancing a chapter trial or installing a compiler.

## Take it for a Spin

From the repository root, run:

```sh
make spin02
```

This builds Release automatically, then runs the [small walkthrough script](../tools/spin02.py).
As in Chapter 1, you need Make, .NET 10, and Python 3.10+. If the SDK is not on
PATH, use `make spin02 DOTNET=/absolute/path/to/dotnet`. No tools are installed.
The demonstration uses disposable local files with no credentials, paid calls,
network services, or persistent processes.

For the full retained test suite in both Debug and Release, run `make test`.

Observe one integer token with suffix `u16`, a string value `[255]`, and separate interpolation text/expression markers around the identifier. The decomposed identifier produces LYC101 at byte zero, line one, column one, with the composed spelling suggested. The successful sample is a lexical demonstration, not a complete compilable program. Temporary files are removed automatically. Try this with your human collaborator before acceptance; the author's executed demonstration cannot replace that use.
