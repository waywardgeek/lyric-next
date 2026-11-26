# Chapter data-structure diagrams

These diagrams introduce the compiler's Common vocabulary at the start of Chapters
1, 2, 3, 4, 7, 8 and 9. Chapters 5 and 6 add execution/checking behavior but no new
persistent Common relationships; Chapter 0 introduces the method, not code.
Chapter 0 teaches the notation with a fictional document-editor example rendered
through the same drawing function. Each compiler relationship appears exactly once, in its introduction chapter. Existing
types are included when a new relationship touches them. Chapter 9 uses two views.

## Regenerate

From the repository root, with existing .NET 10, Python 3 and Graphviz:

```sh
make diagrams DOTNET=/path/to/dotnet
python3 tools/common-diagrams/test_revisions.py /path/to/dotnet
```

No downloads or runtime execution. The source-only Roslyn extractor is adapted
from Ensemble's `scripts/common-diagram/extractor` (same stored-field extraction,
Lyric source-path label). SDK Roslyn assemblies are referenced directly and NuGet
sources are cleared. Builds, inventory, PNG previews and test evidence stay in
ignored `.build/`. Checked-in SVG/DOT and text relationship lists live in
`book/figures/`; Markdown readers need no generator.

`chapters.json` records introduction chapters, reviewed relationships and brief
class descriptions. The generator validates every stored Common-type field,
including reverse links, against the parsed inventory. Indirect nominal links
through Type.Class are checked as paths. Every relationship must be selected once,
in its introduction chapter; every nonstatic type needs an introduction and guide.
Static operation helpers, scalar enums and external framework types are not class
relationship boxes. Value-like records and failure carriers are included.

Strong ownership comes from inspected constructors/destruction, not a type-name
heuristic. Reviewed implementation hashes refuse generation after lifecycle edits;
review the change before intentionally refreshing a fingerprint. Ordinary links
and nominal invalidation dependencies are distinct. The latter may retire typed
facts/symbols without making the immutable SemanticType value their owner.

The generator replaces only marked diagram blocks in each chapter, immediately
after its title. The existing opening and TL;DR remain below. Every figure has
adjacent class explanations and a text
relationship companion naming exact fields and reverse paths. Chapter 0 explains
the style/multiplicity key once; chapter openings do not repeat it. Strong lines
are two points, versus 1.2-point non-owning lines, so open arrowheads stay visible. Labels distinguish
roles; they do not merely repeat destination class names. Styling differs by stroke
as well as color. Graphviz controls visual layout; relationships remain authoritative
in the checked map.

The recipe first tests the actual extractor with synthetic declarations: ordinary
fields and auto-properties are storage; computed properties and bodyless partial
property declarations are not. This regression does not execute compiler code.
After building the extractor, it can also be run independently:

```sh
python3 tools/common-diagrams/test_extractor.py /path/to/dotnet \
  tools/common-diagrams/.build/artifacts/bin/Extractor/debug/Extractor.dll
```

## Chapter 10 source-version migration

The historical `chapters.json` remains unchanged. `revisions.py` exports the pinned
accepted Chapter 9 Git object `6073e136c772019dc8dbe4ca11211340904cbf9e` to a temporary
directory, extracts its actual declarations, and checks its original coverage and
lifecycle fingerprints. It independently extracts the working Chapter 10 source
and validates `chapter10.json`: all stored relationships, canonical holder policy,
reviewed lifecycle files, and explicit replacements. No archived reconstruction
is used. The original `test.py` runs against that temporary historical source,
while inspecting the retained published historical figures and chapter blocks.

Every changed relationship names its predecessor(s) and reason. New and changed
relationships appear once in the revised views; explicitly marked CONTEXT may
repeat an unchanged historical relationship. Same-spelling changes count too:
`IrFunction.Declaration` now supports field initializers as well as source functions.
Canonical dependencies are not value-like fields or ownership of shared ingredients.
The independent required lifecycle-file set prevents dropping a stale fingerprint
from the manifest merely to pass validation. Fingerprints record reviewed model
code, not blanket proof of compiler algorithms or exception safety.

Four Chapter 10 views have visible NEW/CHANGED/CONTEXT labels and adjacent class
guides; their companion lists actual fields, backlinks and historical predecessors.
Earlier chapters link forward outside their unchanged generated blocks. The new
publisher touches only these revisions and forward notices, never historical figures.
All source checks, SVG rendering, exact headings, and ten-point-at-five-inch checks
complete in memory before publication. Ordinary write failure rolls back saved
bytes; this is not a power-loss-atomic multi-file transaction. Preview is the default:

```sh
python3 tools/common-diagrams/revisions.py /path/to/dotnet
# After inspecting .build/revisions previews:
python3 tools/common-diagrams/revisions.py /path/to/dotnet --publish
```

`make diagrams` builds/tests the extractor, runs both historical and migration
checks, then publishes. The regression includes a deliberately oversized final
figure: its refusal must leave all manuscript inputs unchanged. Visual inspection
is still required; computed font metrics cannot detect every confusing routing.

No Lyric print/EPUB publishing pipeline is added here. SVGs are vector figures;
PNG previews support local visual inspection. Final pagination and target-reader
acceptance belong to publication, not this source-only diagram check.

## Chapter 11 additions

`chapter11.json` covers the complete current model while drawing only its seven
new relationships, once each. `chapter11.py` validates the accepted Chapter 10
source at `0ea3d8e617bbb3c15680b7fabd2e7df415ced1dd` separately from fresh current
extraction. It does not refresh or republish Chapter 10's historical manifest.

From the repository root, using the installed SDK and extractor described above:

```sh
python3 tools/common-diagrams/test_chapter11.py
python3 tools/common-diagrams/chapter11.py "$DOTNET"
# After inspecting the preview figures:
python3 tools/common-diagrams/chapter11.py "$DOTNET" --publish
```

Preview output is under `.build/chapter11`. The publisher checks every stored
relationship and reverse link, reviewed lifecycle fingerprints, exact preservation
of historical relationships, unique new-edge coverage, and ten-point text at a
five-inch maximum figure width. It prepares all outputs before writing, preserves
prose outside the Chapter 11 model markers, and rolls back on ordinary publication
failure. The seven mutation controls test migration refusal; they do not replace
source review or rendered-image inspection. Publication writes only Chapter 11
outputs and does not certify a physical printout.
