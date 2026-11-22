using System.Collections.Generic;
using System.IO;

namespace Lyric;

// Closed syntax vocabulary shared by parsing and subsequent compiler passes.
internal enum SyntaxKind
{
    AbstractRelation,
    Adapter,
    Annotation,
    Array,
    ArrayType,
    Assignment,
    Binary,
    Binding,
    Block,
    Break,
    Byte,
    Call,
    Capability,
    Cast,
    Class,
    CleanupTemplate,
    Closure,
    ConditionBinding,
    Continue,
    Dictionary,
    DiscardResult,
    Else,
    Endpoint,
    Entry,
    Enum,
    Expression,
    ExpressionStatement,
    Extends,
    Field,
    FieldBinding,
    FieldValue,
    File,
    Float,
    For,
    Function,
    FunctionType,
    Generic,
    Global,
    Group,
    Guard,
    If,
    Impl,
    Import,
    Index,
    Integer,
    Interface,
    InterfaceField,
    InterpolatedString,
    Interpolation,
    Iterable,
    IterationIndex,
    Literal,
    LowerBound,
    ManyEndpoint,
    Match,
    MatchArm,
    Member,
    MethodBinding,
    Modifier,
    Name,
    OptionalType,
    Parameter,
    Parameters,
    PatternArguments,
    PatternLiteral,
    PatternName,
    PatternNegative,
    PatternTuple,
    Patterns,
    Postfix,
    Record,
    Relation,
    RelationBinding,
    RelationKind,
    RelationRoles,
    Requirement,
    ResultType,
    Return,
    Role,
    RoleBinding,
    Slice,
    String,
    StringText,
    Struct,
    Symbol,
    Tuple,
    TypeAlias,
    TypeApplication,
    TypeArguments,
    TypeGroup,
    TypeMember,
    TypeName,
    TypeParameter,
    TypeParameters,
    TypePrefix,
    Unary,
    UnionType,
    Unit,
    UpperBound,
    Variant,
    VariantField,
    VariantFields,
    VariantTest,
    Visibility,
    Where,
    While,
    Wildcard,
    Yield,
}


// A source strongly owns all of its syntax nodes, including unfinished parses.
// Parent/Children are weak structural relationships: detaching a tree edge does
// not delete either node. TextOffset/TextLength are a value-like byte region in
// the owning source, not a token reference or a second stored spelling.
internal sealed partial class SyntaxNode
{
    internal SourceFile? Source;
    internal SyntaxNode? Parent;
    internal readonly SyntaxKind Kind;
    // Offset -1 means this structural node has no spelling anchor. Otherwise
    // the byte region identifies its name, operator, or complete literal spelling.
    internal readonly int TextOffset;
    internal readonly int TextLength;
    private readonly List<SyntaxNode> Children = new();
    internal bool Destroyed;

    // Initializes state and registers both ends of source ownership.
    internal partial SyntaxNode(SourceFile source, SyntaxKind kind, int textOffset, int textLength);
    // Adds an ordered tree edge and its backlink. Refusal changes neither end.
    internal partial void AddChild(SyntaxNode child);
    internal partial void RemoveChild(SyntaxNode child);
    // Read-only structural traversal for semantic passes. Mutation stays in the
    // relationship methods; ChildAt rejects out-of-range indices normally.
    internal partial int ChildCount();
    internal partial SyntaxNode ChildAt(int index);
    // Detaches all structural edges and source ownership; does not delete peers.
    internal partial void Destroy();
    // Serializes the actual tree. Text is resolved through the owning source.
    internal partial void Write(System.Text.Json.Utf8JsonWriter writer);
}

internal sealed partial class SourceFile
{
    private readonly List<SyntaxNode> SyntaxNodes = new();

    // Construction registration: admits only a live, not-yet-owned node.
    // Source ownership is not transferable because anchors refer to its bytes.
    internal partial void AddSyntaxNode(SyntaxNode node);
    // Destruction-only detach: node must be destroyed and have no tree edges.
    // Refusal leaves membership/backlink unchanged; success removes both ends.
    internal partial void RemoveSyntaxNode(SyntaxNode node);
    // Removes the source-owned suffix, used for speculative rollback and cleanup.
    internal partial void ClearSyntax(int keepCount);
    internal partial string TokenText(int index);
    // Decodes an original source region. Callers obtain spans from validated tokens.
    internal partial string SourceText(int offset, int length);
    // Lexes first, then builds one file/expression inspection root. No evaluation.
    internal partial Diagnostic? Parse(bool expressionOnly);
    // Call only after Parse succeeds; serialization does not validate a partial tree.
    internal partial void WriteSyntax(TextWriter output);
    // After successful Parse, returns its root. Null when the arena is empty.
    // Do not inspect the root during construction: the first node may be unfinished.
    internal partial SyntaxNode? SyntaxRoot();
}

// Value-like control-flow exception carrying the first syntax diagnostic. It
// owns no model relationship; failed nodes remain in the source's owned arena.
internal sealed partial class SyntaxFailure : System.Exception
{
    internal readonly Diagnostic Diagnostic;
    internal partial SyntaxFailure(Diagnostic diagnostic);
}
