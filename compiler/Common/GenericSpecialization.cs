using System.Collections.Generic;

namespace Lyric;

// A concrete generic call target. Analysis and template strongly own it; it
// strongly owns ordered binding occurrences, derived syntax, and its callable.
// Names remain diagnostic text: identity is Template plus the ordered Types.
internal sealed partial class GenericSpecialization
{
    internal SemanticAnalysis? Analysis;
    internal SemanticSymbol? Template;
    internal SemanticSymbol? Function;
    internal bool Destroyed;
    private readonly List<GenericBinding> Bindings = new();
    private readonly List<SyntaxNode> Nodes = new();
    private readonly List<SemanticScope> Scopes = new();
    // Analysis also owns scopes; lexical child links remain nonowning.
    internal partial void AddScope(SemanticScope scope);
    internal partial void RemoveScope(SemanticScope scope);

    // Refuses foreign/retired types and duplicate complete keys before attaching.
    // Copies syntax anchors, not facts or symbols. A failed construction retires
    // every membership already established.
    internal partial GenericSpecialization(SemanticSymbol template,
        IReadOnlyList<SemanticType> types);
    internal partial bool Matches(SemanticSymbol template, IReadOnlyList<SemanticType> types);
    internal partial int BindingCount();
    internal partial SemanticType BindingAt(int index);
    internal partial SyntaxNode Root();
    internal partial void AddBinding(GenericBinding binding, SemanticType type);
    internal partial void RemoveBinding(GenericBinding binding);
    // Admits only a fresh derived node for this compilation, never parsed syntax.
    internal partial void AddSyntaxNode(SyntaxNode node, SourceFile source);
    internal partial void RemoveSyntaxNode(SyntaxNode node);
    internal partial void AddFunction(SemanticSymbol function);
    internal partial void RemoveFunction(SemanticSymbol function);
    internal partial void Destroy();
}

// One ordered canonical-type dependency, including repeated type arguments.
// Retiring its type retires the specialization, never the other argument types.
internal sealed partial class GenericBinding
{
    internal GenericSpecialization? Specialization;
    internal SemanticType? Type;
    internal bool Destroyed;
    internal partial GenericBinding(GenericSpecialization specialization, SemanticType type);
    internal partial void Destroy();
}

internal sealed partial class SemanticAnalysis
{
    private readonly List<GenericSpecialization> Specializations = new();
    internal partial GenericSpecialization? FindSpecialization(SemanticSymbol template,
        IReadOnlyList<SemanticType> types);
    internal partial void AddSpecialization(GenericSpecialization specialization);
    internal partial void RemoveSpecialization(GenericSpecialization specialization);
    internal partial IEnumerable<GenericSpecialization> ConcreteFunctions();
}

internal sealed partial class SemanticSymbol
{
    // Null on user-visible templates and ordinary functions. Concrete functions
    // remain scope-owned but are excluded from lexical name lookup.
    internal GenericSpecialization? Specialization;
    // Parsed type parameters identify templates; concrete copies omit this node.
    internal partial SyntaxNode? GenericParameters();
    private readonly List<GenericSpecialization> Specializations = new();
    internal partial void AddSpecialization(GenericSpecialization specialization);
    internal partial void RemoveSpecialization(GenericSpecialization specialization);
}

internal sealed partial class SemanticType
{
    private readonly List<GenericBinding> GenericBindings = new();
    internal partial void AddGenericBinding(GenericBinding binding);
    internal partial void RemoveGenericBinding(GenericBinding binding);
}

internal sealed partial class SyntaxNode
{
    // Derived nodes are co-owned by the specialization and source. Source keeps
    // byte provenance; specialization teardown removes all derived source nodes.
    // These nodes never become parsed roots or modify original source trees.
    internal GenericSpecialization? Specialization;
}

internal sealed partial class SemanticScope
{
    // Concrete-body scopes retire together even though lexical edges are weak.
    internal GenericSpecialization? Specialization;
}
