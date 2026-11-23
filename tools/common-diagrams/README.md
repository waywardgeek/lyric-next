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
python3 tools/common-diagrams/test.py
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

These are chapter-era selected views of current declarations, not fabricated
historical source snapshots. For example, Chapter 4 does not show the structured
type introduced in Chapter 7, and Chapter 7 does not show Chapter 8's nominal class
link. Full type hierarchies and interning remain future chapter work.

No Lyric print/EPUB publishing pipeline is added here. SVGs are vector figures;
PNG previews support local visual inspection. Final pagination and target-reader
acceptance belong to publication, not this source-only diagram check.
