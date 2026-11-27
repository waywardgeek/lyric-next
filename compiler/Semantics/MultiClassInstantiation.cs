using System;
using System.Collections.Generic;

namespace Lyric;

internal sealed partial class MultiClassInstantiation
{
    internal partial MultiClassInstantiation(SemanticSymbol template,
        IReadOnlyList<SemanticSymbol> classes, IReadOnlyList<string?> labels,
        RelationshipPolicy policy)
    {
        SemanticAnalysis? analysis = template.Scope?.Analysis;
        if (template.Destroyed || template.Kind != SymbolKind.Interface
            || analysis is null || analysis.Destroyed || template.Declaration is null
            || template.ParticipantArity() == 0
            || classes.Count != template.ParticipantArity() || labels.Count != classes.Count)
        {
            throw new InvalidOperationException("Invalid multi-class template or binding arity.");
        }
        int parents = 0;
        int children = 0;
        for (int index = 0; index < classes.Count; index += 1)
        {
            ParticipantRole role = template.ParticipantRoleAt(index);
            if (role == ParticipantRole.Parent)
            {
                parents += 1;
            }
            if (role == ParticipantRole.Child)
            {
                children += 1;
                if (policy == RelationshipPolicy.Owns && classes[index].IsCountedClass())
                {
                    throw new InvalidOperationException("An owning relationship requires ordinary children.");
                }
            }
        }
        bool relationship = parents != 0 || children != 0;
        if (policy is not (RelationshipPolicy.None or RelationshipPolicy.Owns or RelationshipPolicy.Refs)
            || (relationship && (parents != 1 || children != 1 || classes.Count != 2))
            || relationship != (policy != RelationshipPolicy.None))
        {
            throw new InvalidOperationException("Relationship roles and binding policy do not match.");
        }
        Policy = policy;
        for (int index = 0; index < classes.Count; index += 1)
        {
            SemanticSymbol concreteClass = classes[index];
            string? label = labels[index];
            if (concreteClass.Destroyed || concreteClass.Kind != SymbolKind.Class
                || concreteClass.Scope?.Analysis != analysis || concreteClass.MemberScope is null
                || label == "")
            {
                throw new InvalidOperationException("Invalid multi-class participant.");
            }
            if (label is not null)
            {
                if (concreteClass.MemberScope.LookupMember(label) is not null
                    || concreteClass.FindParticipantLabel(label) is not null)
                {
                    throw new InvalidOperationException("Participant scope name collision.");
                }
                for (int previous = 0; previous < index; previous += 1)
                {
                    if (classes[previous] == concreteClass && labels[previous] == label)
                    {
                        throw new InvalidOperationException("Repeated participant scope name.");
                    }
                }
            }
        }
        if (analysis.FindMultiClassInstantiation(template, classes, labels, policy) is not null)
        {
            throw new InvalidOperationException("Duplicate multi-class instantiation.");
        }
        analysis.AddMultiClassInstantiation(this);
        try
        {
            template.AddMultiClassInstantiation(this);
            for (int index = 0; index < classes.Count; index += 1)
            {
                _ = new ParticipantBinding(this, classes[index], labels[index]);
            }
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    internal partial bool Matches(SemanticSymbol template,
        IReadOnlyList<SemanticSymbol> classes, IReadOnlyList<string?> labels,
        RelationshipPolicy policy)
    {
        if (Destroyed || Template != template || Policy != policy || classes.Count != Participants.Count
            || labels.Count != Participants.Count)
        {
            return false;
        }
        for (int index = 0; index < Participants.Count; index += 1)
        {
            ParticipantBinding participant = Participants[index];
            if (participant.ConcreteClass != classes[index] || participant.Label != labels[index])
            {
                return false;
            }
        }
        return true;
    }

    internal partial int ParticipantCount() => Participants.Count;
    internal partial ParticipantBinding ParticipantAt(int index) => Participants[index];

    internal partial void AddParticipant(ParticipantBinding participant, SemanticSymbol concreteClass)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || Template is null
            || Participants.Count >= Template.ParticipantArity()
            || participant.Destroyed || participant.Instantiation is not null
            || participant.ConcreteClass is not null || participant.Position != Participants.Count
            || concreteClass.Destroyed || concreteClass.Kind != SymbolKind.Class
            || concreteClass.Scope?.Analysis != Analysis || concreteClass.MemberScope is null
            || participant.Label == "")
        {
            throw new InvalidOperationException("Invalid participant attachment.");
        }
        Participants.EnsureCapacity(Participants.Count + 1);
        concreteClass.AddParticipantBinding(participant);
        Participants.Add(participant);
        participant.Instantiation = this;
    }

    internal partial void RemoveParticipant(ParticipantBinding participant)
    {
        if (!participant.Destroyed || participant.Instantiation != this
            || !Participants.Contains(participant))
        {
            throw new InvalidOperationException("Different participant owner.");
        }
        participant.ConcreteClass?.RemoveParticipantBinding(participant);
        Participants.Remove(participant);
        participant.Instantiation = null;
    }

    internal partial void AddContribution(SemanticSymbol symbol)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || symbol.Destroyed
            || symbol.MultiClass is not null || symbol.Specialization is not null
            || symbol.Scope?.Analysis != Analysis || symbol.Scope.Class is null
            || symbol.Kind is not (SymbolKind.Function or SymbolKind.Field or SymbolKind.MemberNamespace)
            || (symbol.Declaration is not null && symbol.Declaration.MultiClass != this))
        {
            throw new InvalidOperationException("Cannot attach contributed member.");
        }
        Contributions.Add(symbol);
        symbol.MultiClass = this;
    }

    internal partial void RemoveContribution(SemanticSymbol symbol)
    {
        if (!symbol.Destroyed || symbol.MultiClass != this || !Contributions.Remove(symbol))
        {
            throw new InvalidOperationException("Different contribution owner.");
        }
        symbol.MultiClass = null;
    }

    internal partial void AddScope(SemanticScope scope)
    {
        if (Destroyed || Analysis is null || Analysis.Destroyed || scope.Destroyed
            || scope.Analysis != Analysis || scope.Specialization is not null || scope.MultiClass is not null)
        {
            throw new InvalidOperationException("Cannot attach multi-class checking scope.");
        }
        Scopes.Add(scope);
        scope.MultiClass = this;
    }

    internal partial void RemoveScope(SemanticScope scope)
    {
        if (!scope.Destroyed || scope.MultiClass != this || !Scopes.Remove(scope))
        {
            throw new InvalidOperationException("Different multi-class scope owner.");
        }
        scope.MultiClass = null;
    }

    internal partial void AddSyntaxNode(SyntaxNode node, SourceFile source)
    {
        if (Destroyed || SyntaxSealed || Analysis is null || Analysis.Destroyed
            || source.Destroyed || source.Parent != Analysis.Compilation
            || node.Destroyed || node.Source is not null || node.Parent is not null
            || node.Specialization is not null || node.MultiClass is not null
            || node.ChildCount() != 0 || node.Fact is not null
            || node.DeclaredSymbol is not null || node.ImplementedSymbol is not null)
        {
            throw new InvalidOperationException("Cannot attach multi-class derived syntax.");
        }
        Nodes.Add(node);
        node.MultiClass = this;
    }

    internal partial void RemoveSyntaxNode(SyntaxNode node)
    {
        if (!node.Destroyed || node.MultiClass != this || !Nodes.Remove(node))
        {
            throw new InvalidOperationException("Different multi-class syntax owner.");
        }
        node.MultiClass = null;
    }

    internal partial void SealSyntax()
    {
        if (Destroyed)
        {
            throw new InvalidOperationException("Cannot seal a retired instantiation.");
        }
        SyntaxSealed = true;
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        Analysis?.ClearIr();
        // Mark before cascading: endpoint retirement redirects back to this owner.
        Destroyed = true;
        while (Contributions.Count != 0)
        {
            Contributions[Contributions.Count - 1].Destroy();
        }
        while (Scopes.Count != 0)
        {
            Scopes[Scopes.Count - 1].Destroy();
        }
        // Nodes remove their facts and tree links before endpoint identities retire.
        while (Nodes.Count != 0)
        {
            Nodes[Nodes.Count - 1].Destroy();
        }
        while (Participants.Count != 0)
        {
            Participants[Participants.Count - 1].Destroy();
        }
        Template?.RemoveMultiClassInstantiation(this);
        Analysis?.RemoveMultiClassInstantiation(this);
    }
}

internal sealed partial class ParticipantBinding
{
    internal partial ParticipantBinding(MultiClassInstantiation instantiation,
        SemanticSymbol concreteClass, string? label)
    {
        Position = instantiation.ParticipantCount();
        Label = label;
        instantiation.AddParticipant(this, concreteClass);
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        if (Instantiation is MultiClassInstantiation owner && !owner.Destroyed)
        {
            owner.Destroy();
            return;
        }
        Instantiation?.Analysis?.ClearIr();
        while (ViewFacts.Count != 0)
        {
            RemoveViewFact(ViewFacts[ViewFacts.Count - 1]);
        }
        while (ViewSymbols.Count != 0)
        {
            RemoveViewSymbol(ViewSymbols[ViewSymbols.Count - 1]);
        }
        while (Members.Count != 0)
        {
            RemoveMember(Members[Members.Count - 1]);
        }
        Destroyed = true;
        Instantiation?.RemoveParticipant(this);
    }
}

internal sealed partial class SemanticAnalysis
{
    internal partial MultiClassInstantiation? FindMultiClassInstantiation(
        SemanticSymbol template, IReadOnlyList<SemanticSymbol> classes, IReadOnlyList<string?> labels,
        RelationshipPolicy policy)
    {
        foreach (MultiClassInstantiation instantiation in MultiClassInstantiations)
        {
            if (instantiation.Matches(template, classes, labels, policy))
            {
                return instantiation;
            }
        }
        return null;
    }

    internal partial void AddMultiClassInstantiation(MultiClassInstantiation instantiation)
    {
        if (Destroyed || Compilation is null || instantiation.Destroyed
            || instantiation.Analysis is not null || instantiation.Template is not null)
        {
            throw new InvalidOperationException("Invalid instantiation analysis owner.");
        }
        MultiClassInstantiations.Add(instantiation);
        instantiation.Analysis = this;
    }

    internal partial void RemoveMultiClassInstantiation(MultiClassInstantiation instantiation)
    {
        if (!instantiation.Destroyed || instantiation.Analysis != this
            || instantiation.Template is not null || !MultiClassInstantiations.Remove(instantiation))
        {
            throw new InvalidOperationException("Different instantiation analysis owner.");
        }
        instantiation.Analysis = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial int ParticipantArity()
    {
        if (Kind != SymbolKind.Interface || Declaration is null)
        {
            return 0;
        }
        for (int index = 0; index < Declaration.ChildCount(); index += 1)
        {
            SyntaxNode child = Declaration.ChildAt(index);
            if (child.Kind == SyntaxKind.TypeParameters)
            {
                return child.ChildCount();
            }
        }
        return 0;
    }

    internal partial ParticipantRole ParticipantRoleAt(int position)
    {
        if (Kind != SymbolKind.Interface || Declaration is null || position < 0)
        {
            return ParticipantRole.None;
        }
        for (int index = 0; index < Declaration.ChildCount(); index += 1)
        {
            SyntaxNode parameters = Declaration.ChildAt(index);
            if (parameters.Kind != SyntaxKind.TypeParameters || position >= parameters.ChildCount())
            {
                continue;
            }
            SyntaxNode parameter = parameters.ChildAt(position);
            for (int child = 0; child < parameter.ChildCount(); child += 1)
            {
                SyntaxNode modifier = parameter.ChildAt(child);
                if (modifier.Kind != SyntaxKind.Modifier)
                {
                    continue;
                }
                string text = modifier.Source!.SourceText(modifier.TextOffset, modifier.TextLength);
                if (text == "parent")
                {
                    return ParticipantRole.Parent;
                }
                if (text == "child")
                {
                    return ParticipantRole.Child;
                }
            }
        }
        return ParticipantRole.None;
    }

    internal partial ParticipantBinding? FindParticipantLabel(string label)
    {
        foreach (ParticipantBinding binding in ParticipantBindings)
        {
            if (binding.Label == label)
            {
                return binding;
            }
        }
        return null;
    }

    internal partial void AddMultiClassInstantiation(MultiClassInstantiation instantiation)
    {
        if (Destroyed || Kind != SymbolKind.Interface || Scope?.Analysis is null
            || Scope.Analysis.Destroyed || instantiation.Destroyed
            || instantiation.Analysis != Scope.Analysis || instantiation.Template is not null)
        {
            throw new InvalidOperationException("Invalid instantiation template owner.");
        }
        MultiClassInstantiations.Add(instantiation);
        instantiation.Template = this;
    }

    internal partial void RemoveMultiClassInstantiation(MultiClassInstantiation instantiation)
    {
        if (!instantiation.Destroyed || instantiation.Template != this
            || !MultiClassInstantiations.Remove(instantiation))
        {
            throw new InvalidOperationException("Different instantiation template owner.");
        }
        instantiation.Template = null;
    }

    internal partial void AddParticipantBinding(ParticipantBinding binding)
    {
        if (Destroyed || Kind != SymbolKind.Class || Scope?.Analysis is null
            || Scope.Analysis.Destroyed || MemberScope is null || binding.Destroyed
            || binding.ConcreteClass is not null || binding.Instantiation is not null
            || binding.Label == ""
            || (binding.Label is string label
                && (MemberScope.LookupMember(label) is not null || FindParticipantLabel(label) is not null)))
        {
            throw new InvalidOperationException("Invalid concrete participant dependency.");
        }
        ParticipantBindings.Add(binding);
        binding.ConcreteClass = this;
    }

    internal partial void RemoveParticipantBinding(ParticipantBinding binding)
    {
        if (!binding.Destroyed || binding.ConcreteClass != this || !ParticipantBindings.Remove(binding))
        {
            throw new InvalidOperationException("Different concrete participant dependency.");
        }
        binding.ConcreteClass = null;
    }
}

internal sealed partial class SemanticScope
{
    internal partial SemanticScope(MultiClassInstantiation instance)
    {
        if (instance.Destroyed
            || instance.Analysis is not SemanticAnalysis analysis
            || analysis.Destroyed
            || instance.Template?.Scope is not SemanticScope parent
            || parent.Destroyed
            || parent.Analysis != analysis)
        {
            throw new InvalidOperationException("Cannot construct participant annotation scope.");
        }
        analysis.AddScope(this);
        try
        {
            parent.AddChild(this);
            instance.AddScope(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }
}
