using System.Collections.Generic;

namespace Lyric;

// Immutable specialization values, not runtime dispatch or model relationships.
internal enum RelationshipPolicy
{
    None,
    Owns,
    Refs,
}

// Value-like designation of the accounting boundary in a contributed function.
internal enum RelationshipOperation { None, Add, Remove }

internal enum ParticipantRole
{
    None,
    Parent,
    Child,
}

// One explicit compile-time multi-class binding. Analysis and interface template
// strongly own it. Its ordered endpoint occurrences preserve roles even when
// several participants bind the same class. Labels are lookup placement values,
// not runtime types. Concrete classes retain reverse dependency memberships.
internal sealed partial class MultiClassInstantiation
{
    internal SemanticAnalysis? Analysis;
    internal SemanticSymbol? Template;
    internal bool Destroyed;
    internal readonly RelationshipPolicy Policy;
    private readonly List<ParticipantBinding> Participants = new();
    // Source and instantiation jointly own copied nodes. Tree edges remain weak.
    private readonly List<SyntaxNode> Nodes = new();
    internal bool SyntaxSealed;
    // Scope owns placement; instantiation owns the introduced contribution's lifetime.
    private readonly List<SemanticSymbol> Contributions = new();
    private readonly List<SemanticScope> Scopes = new();
    internal partial void AddContribution(SemanticSymbol symbol);
    internal partial void RemoveContribution(SemanticSymbol symbol);
    internal partial void AddScope(SemanticScope scope);
    internal partial void RemoveScope(SemanticScope scope);

    // Construction-only admission; existing source syntax cannot be adopted.
    internal partial void AddSyntaxNode(SyntaxNode node, SourceFile source);
    internal partial void RemoveSyntaxNode(SyntaxNode node);
    // End structural assembly before checking any copied declaration.
    internal partial void SealSyntax();

    // Validates the complete key and endpoint namespaces before any attachment.
    // Refusal leaves existing model memberships untouched; partial construction
    // failure destroys only this candidate and its newly attached occurrences.
    internal partial MultiClassInstantiation(SemanticSymbol template,
        IReadOnlyList<SemanticSymbol> classes, IReadOnlyList<string?> labels,
        RelationshipPolicy policy = RelationshipPolicy.None);
    internal partial bool Matches(SemanticSymbol template,
        IReadOnlyList<SemanticSymbol> classes, IReadOnlyList<string?> labels,
        RelationshipPolicy policy = RelationshipPolicy.None);
    internal partial int ParticipantCount();
    internal partial ParticipantBinding ParticipantAt(int index);
    internal partial void AddParticipant(ParticipantBinding participant,
        SemanticSymbol concreteClass);
    internal partial void RemoveParticipant(ParticipantBinding participant);
    internal partial void Destroy();
}

// An identity-bearing occurrence, not merely a class/type pair. The instantiation
// owns it; ConcreteClass is a reciprocal dependency, not ownership of the class.
// Retiring either this occurrence or its class retires the instantiation.
internal sealed partial class ParticipantBinding
{
    internal MultiClassInstantiation? Instantiation;
    internal SemanticSymbol? ConcreteClass;
    // Non-owning semantic users. Each attachment updates its user's backlink.
    private readonly List<SemanticSymbol> ViewSymbols = new();
    private readonly List<SemanticFact> ViewFacts = new();
    private readonly List<SemanticSymbol> Members = new();
    // Scalar lookup views survive nullable wrapping, but never container wrapping.
    internal partial bool AcceptsViewType(SemanticType? type);
    internal partial void AddViewSymbol(SemanticSymbol symbol);
    internal partial void RemoveViewSymbol(SemanticSymbol symbol);
    internal partial void AddViewFact(SemanticFact fact);
    internal partial void RemoveViewFact(SemanticFact fact);
    internal partial void AddMember(SemanticSymbol symbol);
    internal partial void RemoveMember(SemanticSymbol symbol);
    // Returns an actual contributed symbol, never a reconstructed qualified name.
    internal partial SemanticSymbol? FindMember(string name);
    internal readonly int Position;
    internal readonly string? Label;
    internal bool Destroyed;
    internal partial ParticipantBinding(MultiClassInstantiation instantiation,
        SemanticSymbol concreteClass, string? label);
    internal partial void Destroy();
}

internal sealed partial class SemanticAnalysis
{
    private readonly List<MultiClassInstantiation> MultiClassInstantiations = new();
    internal partial MultiClassInstantiation? FindMultiClassInstantiation(
        SemanticSymbol template, IReadOnlyList<SemanticSymbol> classes,
        IReadOnlyList<string?> labels, RelationshipPolicy policy = RelationshipPolicy.None);
    internal partial void AddMultiClassInstantiation(MultiClassInstantiation instantiation);
    internal partial void RemoveMultiClassInstantiation(MultiClassInstantiation instantiation);
}

internal sealed partial class SemanticSymbol
{
    // Strong template ownership and concrete-class dependencies are distinct:
    // retiring a template/class retires dependent instances, never peer classes.
    private readonly List<MultiClassInstantiation> MultiClassInstantiations = new();
    private readonly List<ParticipantBinding> ParticipantBindings = new();
    internal partial int ParticipantArity();
    // Reads the declared endpoint occurrence, never infers role from concrete type.
    internal partial ParticipantRole ParticipantRoleAt(int position);
    internal partial ParticipantBinding? FindParticipantLabel(string label);
    internal partial void AddMultiClassInstantiation(MultiClassInstantiation instantiation);
    internal partial void RemoveMultiClassInstantiation(MultiClassInstantiation instantiation);
    internal partial void AddParticipantBinding(ParticipantBinding binding);
    internal partial void RemoveParticipantBinding(ParticipantBinding binding);
}

internal sealed partial class SyntaxNode
{
    internal MultiClassInstantiation? MultiClass;
}

internal sealed partial class SemanticSymbol
{
    internal MultiClassInstantiation? MultiClass;
}

internal sealed partial class SemanticScope
{
    // Lexical edges are weak; all derived checking scopes retire with the binding.
    internal MultiClassInstantiation? MultiClass;
    // Private participant annotation environment. Analysis and instance jointly
    // own it; its weak lexical parent is the template's declaring scope. Refusal
    // leaves all existing memberships unchanged; failed attachment is cleaned up.
    internal partial SemanticScope(MultiClassInstantiation instance);
}

internal sealed partial class SemanticSymbol
{
    // View of Type: a function's result, or a parameter/local/type binding's value.
    // This is distinct from the role that contributed the declaration itself.
    internal ParticipantBinding? ParticipantView;
    internal ParticipantBinding? ContributingParticipant;
}

internal sealed partial class SemanticFact
{
    // Compile-time lookup view only; canonical type and runtime ownership are unchanged.
    internal ParticipantBinding? ParticipantView;
}
