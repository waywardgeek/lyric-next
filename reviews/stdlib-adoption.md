# Standard-library adoption decisions

## Scope

Bill authorized borrowing, omitting, or modifying the existing Lyric stdlib and updating the new specification. Cadence read all 772 lines of `/Users/bill/projects/lyric/stdlib/std.ly` and all 258 lines of `stdlib/string.ly`. The original sources were inspected, not modified. This is a specification decision record, not a port, compiled test, or claim about generated runtime behavior. The frozen predecessor is the version stored at `d2cf0c0:drafts/16-before-source-based-stdlib-choices.md`; the current requirements are integrated into Sections 4, 13, and 16 of [the specification](../lyric-language-spec.md).

Implementation algorithms remain separate from language/library requirements. Source-defined behavior is useful evidence; implementation defects and old compiler assumptions do not become compatibility requirements.

## Retain

### Byte strings and utility functions

`string.ly` explicitly starts from string = [u8]. Adopt string as a transparent alias using the new recursive array value semantics, not a distinct immutable-string object or shared-backing exception. Indexing/length are byte-oriented; runtime bytes need not be UTF-8. Assignment, ordinary parameters, slicing, and string/byte conversion helpers produce independent values. Explicit references use the same stable-borrow rules as all other arrays.

Retain the source's str_* utility API and its useful behavior:

- Prefix/suffix/search: string.ly 9–65. Empty needle matches at zero.
- Split/limited split: 73–120. Retain empty parts and limited remainder behavior, correcting the empty-separator case below.
- Trim: 124–157. Whitespace means space, tab, LF, CR—not every Unicode separator.
- ASCII case transforms: 161–189. Other bytes remain unchanged.
- Replace: 194–211. Nonoverlapping, left-to-right replacement; empty search returns the original value.
- Repeat/concatenate/join: 216–256. Nonpositive repeat counts yield empty; join places separators only between parts.

The canonical substring/text API uses explicit free functions such as str_contains. Since string is an alias, generic array contains(u8) must not secretly become substring containment depending on spelling. No separate nominal-string dispatch is introduced.

### Dictionary structure and lookup

Retain ordinary Dict and DictEntry identity classes with a maintained owning relation (std.ly 597–615), and entry-returning get (630–633). This is library source, not a compiler-intrinsic container lifetime category. keys returns independent values in unspecified order. Key/value persistence remains subject to the language's ordinary relationship/counting rules.

### Symbol API and builder

Retain sym(text), backtick construction, get_name, get_hash, and cached-hash interning (std.ly 536–571). Retain new_string_builder, byte/string write, len, and to_string (721–759), plus explicit writable push_bytes (749–755). Ordinary array value semantics establish builder snapshots; no separate shared-string policy is needed.

The existing error-code enumeration (std.ly 7–25) already closely matches the accepted concrete Status vocabulary. Preserve the current Result/Status design rather than restore the old error interface/tuple propagation.

## Modify

### Resolve hash collisions using equality

The inspected source passes a u64 hash into hash_lookup, whose match condition is only hash_key() == key (std.ly 408–430). sym returns that candidate without comparing its name (562–570); Dict.get does the same for arbitrary keys (630–633). Hashable declares only get_hash (575–577), despite a nearby comment mentioning equality.

New requirements: compare actual byte names for interning and actual keys for dictionary operations. Equal keys have equal hashes; equal hashes do not establish equal keys. A deliberately colliding custom key is a required conformance case. This is a source-grounded finding, not an executed collision reproduction.

Do not pretend the raw-u64 HashedList API already supplies equality-aware generic key lookup. Its u64 can be an actual application key. A generic Dict representation must preserve collision candidates and compare original keys; its specific bucket/probing algorithm is separate implementation design.

### Make dictionary updates and deletion explicit

The old set allocates a replacement entry and hash_insert swaps it into storage (std.ly 618–627, 384–394). The new contract instead updates an equal key's existing entry value in place. This preserves entry identity but does not suppress the replaced value's release/destruction effects.

The new remove destroys the owned entry, with generated teardown detaching both ends; missing keys return false without mutation. This replaces the old detach-plus-manual-unref path (643–649 and hash_remove). Any held entry or interior-value reference is checked using ordinary inferred effects and stable-borrow requirements, not a blanket special rule that every mutation invalidates every entry. Conservative class-wide destruction invalidation still applies when entry destruction is possible.

Construction/set remain no-Result convenience operations. Internal attachment preconditions must be established, never ignored; an unexpected refusal is a checked invariant failure. Resource failure in a no-Result operation produces checked failure rather than returning partial success. Recoverable-allocation APIs are not added in this change.

### Use explicit counted symbols

Adapt Sym from the original ordinary child of a permanent SymTable into an explicit value class. The program-lifetime intern table retains counted canonical symbols; callers retain counted handles. The table releases its references on teardown and cannot force-destroy symbols retained elsewhere. This uses the already-approved counted-value category and does not restore permanent, manual ref/unref, or holder backlinks.

The stored name and hash are private and immutable through the public API. Inputs and returned name arrays are values, so caller mutation cannot alter the canonical name. Empty names are valid and intern normally, distinct from null. There is no identifier-style NFC normalization of symbol data.

Global initialization/shutdown sequencing and concurrent interning still depend on the general runtime contracts. This is a retention policy, not an implemented synchronization scheme.

### Fix bounded splitting with an empty separator

str_split explicitly handles an empty separator by splitting into bytes (string.ly 98–107). str_split_n does not: str_index_of returns zero, separator length is zero, and start does not advance (73–94). For n <= 0 this does not make input progress; for positive limits it produces a different result from the intended byte-split behavior.

New contract: n <= 0 equals str_split; n == 1 returns [input]; n > 1 yields at most n parts with the remaining suffix last. Empty separator splits at byte boundaries under that limit. Empty input/empty separator yields [] except the explicit n == 1 case. No runtime test was run here.

### Replace permissive numeric parsing

The old parse_int skips nondigits (std.ly 663–679); str_to_float stops after a valid prefix (684–708). Their comments' failure sentinels do not establish strict parsing.

New parse_int/atoi returns Result<i64>, consumes optional sign plus decimal digits in full, and distinguishes InvalidArgument from OutOfRange. parse_float returns Result<f64> for a deliberately small full-input decimal grammar, with no whitespace, exponent or special-value spellings initially. str_to_float may alias that contract, not restore partial parsing. These APIs deliberately replace sentinel/tuple behavior; detailed conformance requirements are in Section 16.

## Do not carry forward

- permanent as a language modifier.
- Manual trusted ref/unref application protocols, old shared-array parameter semantics, or let-bound array mutation.
- Parent destructors that clear child parent links before invoking child destruction (for example std.ly 104–117 and 234–246). New final-before-own-detachment semantics remain authoritative.
- Hash-only equality, silently replacing relation membership, unchecked attachment, or persistent unchecked pointers inside generic containers.
- The old error interface as an alternate Result propagation protocol.
- A special immutable string object merely to explain snapshots.
- Claims that comments about memcpy, allocation growth, or O(1) behavior prove actual generated performance.

## Remaining boundaries

Mutable reference-bearing custom keys need a stable hash/equality contract; unsupported unsafe uses are rejected. Class/primitive extension syntax and generic external-method scope still need their source-level resolution rules. Global initialization, shutdown, concurrent interning, general allocation policy, broad operating-system API migration, and streaming contracts remain separate work. General count limits, array iteration mutation, sorting, and empty pop are not silently settled by these library choices.

The bounded verification receipt is retained at `d2cf0c0:stdlib-adoption-check.json` in Git history. It records source hashes and document checks, not language execution. See [the review index](README.md) for retrieval instructions.
