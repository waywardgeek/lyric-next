using System;

namespace Lyric;

internal sealed partial class SemanticScope
{
    internal partial SemanticSymbol? Lookup(string name)
    {
        for (SemanticScope? scope = this; scope is not null; scope = scope.Parent)
        {
            // Fields and methods require a receiver or nominal qualification.
            // Their scope remains in the parent graph for ownership/navigation,
            // but is not an unqualified lexical namespace.
            if (scope.MemberOwner is not null)
            {
                continue;
            }
            SemanticSymbol? symbol = scope.LookupMember(name);
            if (symbol is not null)
            {
                return symbol;
            }
        }
        return null;
    }

    internal partial System.Collections.Generic.IEnumerable<SemanticSymbol> Fields()
    {
        foreach (SemanticSymbol symbol in Symbols)
        {
            if (symbol.Kind == SymbolKind.Field)
            {
                yield return symbol;
            }
            else if (symbol.Kind == SymbolKind.MemberNamespace && symbol.MemberScope is not null)
            {
                // Labels organize lookup, not object storage. Namespace symbols
                // are admitted only directly within a class, so this has one level.
                foreach (SemanticSymbol field in symbol.MemberScope.Fields())
                {
                    yield return field;
                }
            }
        }
    }

    internal partial System.Collections.Generic.IEnumerable<SemanticSymbol> Methods()
    {
        foreach (SemanticSymbol symbol in Symbols)
        {
            if (symbol.Kind == SymbolKind.Function)
            {
                yield return symbol;
            }
            else if (symbol.Kind == SymbolKind.MemberNamespace && symbol.MemberScope is not null)
            {
                foreach (SemanticSymbol method in symbol.MemberScope.Methods())
                {
                    yield return method;
                }
            }
        }
    }

    internal partial SemanticSymbol? LookupMember(string name)
    {
        foreach (SemanticSymbol symbol in Symbols)
        {
            if (symbol.Name == name && symbol.Specialization is null
                && !symbol.IsDestructorFragment())
            {
                return symbol;
            }
        }
        return null;
    }

    internal partial void AddSymbol(SemanticSymbol symbol, SyntaxNode? declaration)
    {
        SemanticSymbol? existing = MemberOwner is null
            ? Lookup(symbol.Name)
            : LookupMember(symbol.Name);
        if (Destroyed || Analysis is null || Analysis.Destroyed || symbol.Destroyed
            || symbol.Scope is not null
            || (symbol.Specialization is null && existing is not null
                && declaration?.Kind != SyntaxKind.DestructorFragment)
            || (symbol.Specialization is not null
                && (symbol.Specialization.Template?.Scope != this
                    || symbol.Specialization.Analysis != Analysis)))
        {
            throw new InvalidOperationException("Cannot attach duplicate or invalid symbol.");
        }
        Symbols.Add(symbol);
        symbol.Scope = this;
    }

    internal partial void RemoveSymbol(SemanticSymbol symbol)
    {
        int index = Symbols.LastIndexOf(symbol);
        if (symbol.Scope != this || index < 0 || !symbol.Destroyed
            || symbol.Declaration is not null || symbol.Implementation is not null)
        {
            throw new InvalidOperationException("Only a destroyed symbol can leave its scope.");
        }
        Symbols.RemoveAt(index);
        symbol.Scope = null;
    }
}

internal sealed partial class SemanticSymbol
{
    internal partial SyntaxNode? BodySyntax()
    {
        return Implementation ?? Declaration;
    }

    internal partial bool IsCountedClass()
    {
        if (Kind != SymbolKind.Class || Declaration is null)
        {
            return false;
        }
        for (int i = 0; i < Declaration.ChildCount(); i += 1)
        {
            SyntaxNode child = Declaration.ChildAt(i);
            if (child.Kind == SyntaxKind.Modifier
                && child.Source!.SourceText(child.TextOffset, child.TextLength) == "value")
            {
                return true;
            }
        }
        return false;
    }

    internal partial bool BorrowsParticipantParameter(SemanticSymbol? parameter)
    {
        MultiClassInstantiation? instance = ContributingParticipant?.Instantiation;
        return !Destroyed && instance is not null
            && instance.Policy != RelationshipPolicy.None
            && parameter is not null && !parameter.Destroyed
            && parameter.Kind == SymbolKind.Parameter && !parameter.IsReference
            && parameter.Scope == FunctionScope
            && parameter.ParticipantView is ParticipantBinding participant
            && participant.Instantiation == instance
            && participant.AcceptsViewType(parameter.Type);
    }

    internal partial bool BorrowsParticipantLocal(SemanticSymbol? local)
    {
        MultiClassInstantiation? instance = ContributingParticipant?.Instantiation;
        if (Destroyed || instance is null || instance.Policy == RelationshipPolicy.None
            || local is null || local.Destroyed || local.Kind != SymbolKind.Local
            || local.IsReference || local.ParticipantView is not ParticipantBinding participant
            || participant.Instantiation != instance || !participant.AcceptsViewType(local.Type))
        {
            return false;
        }
        // Nested blocks retain a lexical path to the owning function. Stop at
        // the first function boundary; a shared concrete class grants nothing.
        for (SemanticScope? scope = local.Scope; scope is not null; scope = scope.Parent)
        {
            if (scope.Function is not null)
            {
                return scope.Function == this && scope == FunctionScope;
            }
        }
        return false;
    }

    internal partial RelationshipOperation RelationshipOperationKind()
    {
        if (Kind != SymbolKind.Function || Declaration is null
            || ContributingParticipant?.Instantiation?.Policy is null or RelationshipPolicy.None)
        {
            return RelationshipOperation.None;
        }
        for (int index = 0; index < Declaration.ChildCount(); index += 1)
        {
            SyntaxNode child = Declaration.ChildAt(index);
            if (child.Kind != SyntaxKind.Modifier)
            {
                continue;
            }
            string modifier = child.Source!.SourceText(child.TextOffset, child.TextLength);
            if (modifier == "add")
            {
                return RelationshipOperation.Add;
            }
            if (modifier == "remove")
            {
                return RelationshipOperation.Remove;
            }
        }
        return RelationshipOperation.None;
    }

    internal partial bool IsManagedHandle()
    {
        // Protection covers all contributed handle fields, but only an actual
        // participant slot borrows its value. An unrelated counted payload must
        // retain on assignment and release on replacement and owner destruction.
        return !Destroyed && Kind == SymbolKind.Field
            && ContributingParticipant?.Instantiation is MultiClassInstantiation instance
            && instance.Policy != RelationshipPolicy.None
            && ParticipantView is ParticipantBinding participant
            && participant.Instantiation == instance
            && participant.AcceptsViewType(Type);
    }

    internal partial bool IsManagedLink()
    {
        if (Kind != SymbolKind.Field || Type is null
            || ContributingParticipant?.Instantiation is not MultiClassInstantiation instance
            || instance.Policy == RelationshipPolicy.None)
        {
            return false;
        }
        // Inspect stored values, not the objects they refer to. A class handle
        // terminates traversal; array and aggregate wrappers cannot hide links.
        System.Collections.Generic.Stack<SemanticType> pending = new();
        System.Collections.Generic.HashSet<SemanticType> visited = new();
        pending.Push(Type);
        while (pending.Count != 0)
        {
            SemanticType current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }
            if (current.Kind is SemanticTypeKind.Nominal or SemanticTypeKind.Nullable
                && current.Class is not null)
            {
                return true;
            }
            if (current.Kind == SemanticTypeKind.Array)
            {
                pending.Push(current.Element!);
            }
            else if (current.Kind == SemanticTypeKind.Tuple)
            {
                for (int index = 0; index < current.ComponentCount(); index += 1)
                {
                    pending.Push(current.ComponentAt(index));
                }
            }
            else if (current.Declaration?.Kind == SymbolKind.Struct)
            {
                foreach (SemanticSymbol field in current.Declaration.MemberScope!.Fields())
                {
                    pending.Push(field.Type!);
                }
            }
        }
        return false;
    }

    internal partial bool IsDestructorFragment()
    {
        return Kind == SymbolKind.Function
            && Declaration?.Kind == SyntaxKind.DestructorFragment;
    }

    internal partial bool IsFinalHook()
    {
        if (Kind != SymbolKind.Function || Declaration is null)
        {
            return false;
        }
        for (int i = 0; i < Declaration.ChildCount(); i += 1)
        {
            SyntaxNode child = Declaration.ChildAt(i);
            if (child.Kind == SyntaxKind.Modifier
                && child.Source!.SourceText(child.TextOffset, child.TextLength) == "final")
            {
                return true;
            }
        }
        return false;
    }

    internal partial SemanticSymbol? FinalHook()
    {
        if (Kind != SymbolKind.Class || Declaration is null)
        {
            return null;
        }
        for (int i = 0; i < Declaration.ChildCount(); i += 1)
        {
            SemanticSymbol? member = Declaration.ChildAt(i).DeclaredSymbol;
            if (member is not null && member.IsFinalHook())
            {
                return member;
            }
        }
        return null;
    }

    internal partial SemanticSymbol(SemanticScope scope, SyntaxNode? declaration,
        string name, SymbolKind kind, SemanticType? type, bool mutable,
        BuiltinKind builtin, bool acceptsArray, bool isReference, GenericSpecialization? specialization,
        MultiClassInstantiation? multiClass)
    {
        // Labels occupy the ordinary class namespace but carry no runtime value.
        // Refuse invalid placement before registering any parent relationship.
        if (kind == SymbolKind.MemberNamespace
            && (scope.MemberOwner?.Kind != SymbolKind.Class || type is not null
                || isReference || mutable || acceptsArray || builtin != BuiltinKind.None
                || specialization is not null))
        {
            throw new ArgumentException("A member namespace requires a direct class scope and no value contract.");
        }
        // Reference fields belong only to value structs. Object fields still
        // cannot retain nonowning storage aliases beyond a synchronous use.
        bool referenceStorage = kind is SymbolKind.Local or SymbolKind.Parameter
            || (kind == SymbolKind.Field && scope.MemberOwner?.Kind == SymbolKind.Struct);
        if (isReference && (!referenceStorage
            || type is null || acceptsArray || !mutable))
        {
            throw new ArgumentException("A writable storage alias requires a concrete local, parameter, or struct field type.");
        }
        // Validate all existing parents before attaching to any of them. In
        // particular, a failed concrete callable must not cascade destruction
        // through an already-live specialization during constructor cleanup.
        SemanticAnalysis? analysis = scope.Analysis;
        if (scope.Destroyed || analysis is null || analysis.Destroyed
            || (type is not null && (type.Destroyed || type.Analysis != analysis
                || (type.Class is not null && kind == SymbolKind.Class)))
            || (declaration is not null && (declaration.Destroyed
                || declaration.Source is null || declaration.Source.Destroyed
                || declaration.Source.Parent != analysis.Compilation
                || declaration.DeclaredSymbol is not null || declaration.ImplementedSymbol is not null)))
        {
            throw new InvalidOperationException("Invalid symbol parents.");
        }
        if (specialization is not null
            && (specialization.Destroyed || specialization.Analysis != analysis
                || specialization.Template?.Scope != scope || specialization.Function is not null
                || kind != SymbolKind.Function || name != specialization.Template.Name
                || declaration != specialization.Root()))
        {
            throw new InvalidOperationException("Invalid specialized callable parents.");
        }
        if (multiClass is not null
            && (specialization is not null || multiClass.Destroyed || multiClass.Analysis != analysis
                || scope.Class is null || kind is not (SymbolKind.Function or SymbolKind.Field or SymbolKind.MemberNamespace)
                || (declaration is not null && declaration.MultiClass != multiClass)))
        {
            throw new InvalidOperationException("Invalid contributed member parents.");
        }
        // Fragments have no source-level callable name. Only an instance-owned
        // contribution may use this hidden-function admission; ordinary methods
        // retain the normal duplicate-name contract.
        bool fragment = declaration?.Kind == SyntaxKind.DestructorFragment;
        if (fragment && (kind != SymbolKind.Function || multiClass is null
            || multiClass.Policy == RelationshipPolicy.None))
        {
            throw new InvalidOperationException("A destructor fragment requires a relationship instance.");
        }
        SemanticSymbol? existing = scope.MemberOwner is null
            ? scope.Lookup(name) : scope.LookupMember(name);
        if (specialization is null && existing is not null && !fragment)
        {
            throw new InvalidOperationException("Duplicate symbol.");
        }
        IsReference = isReference;
        if (acceptsArray && (kind != SymbolKind.Parameter || type is not null))
        {
            throw new ArgumentException("An array-shaped parameter has no concrete type.");
        }
        AcceptsArray = acceptsArray;
        Name = name;
        Kind = kind;
        Builtin = builtin;
        Mutable = mutable;
        specialization?.AddFunction(this);
        try
        {
            scope.AddSymbol(this, declaration);
            SetType(type);
            declaration?.AddDeclaredSymbol(this);
            multiClass?.AddContribution(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }
        if (Specialization is GenericSpecialization owner && !owner.Destroyed)
        {
            owner.Destroy();
            return;
        }
        if (MultiClass is MultiClassInstantiation multiOwner && !multiOwner.Destroyed)
        {
            multiOwner.Destroy();
            return;
        }
        while (Specializations.Count != 0)
        {
            Specializations[Specializations.Count - 1].Destroy();
        }
        while (MultiClassInstantiations.Count != 0)
        {
            MultiClassInstantiations[MultiClassInstantiations.Count - 1].Destroy();
        }
        while (ParticipantBindings.Count != 0)
        {
            ParticipantBindings[ParticipantBindings.Count - 1].Destroy();
        }
        // IR borrows declarations and types; retire it before either disappears.
        Scope?.Analysis?.ClearIr();
        ContributingParticipant?.RemoveMember(this);
        MemberScope?.Destroy();
        FunctionScope?.Destroy();
        InitializerScope?.Destroy();
        NominalType?.Destroy();
        SetType(null);
        Implementation?.RemoveImplementedSymbol(this);
        Declaration?.RemoveDeclaredSymbol(this);
        // Removing a declaration invalidates its checked uses. Destroy facts,
        // not their syntax nodes, so no resolved reference survives this symbol.
        while (Uses.Count != 0)
        {
            Uses[Uses.Count - 1].Destroy();
        }
        Destroyed = true;
        Scope?.RemoveSymbol(this);
        Specialization?.RemoveFunction(this);
        MultiClass?.RemoveContribution(this);
    }
}

internal sealed partial class SyntaxNode
{
    internal partial void AddImplementedSymbol(SemanticSymbol symbol)
    {
        SemanticAnalysis? analysis = symbol.Scope?.Analysis;
        if (Destroyed || Kind != SyntaxKind.Function || Source is null || Source.Destroyed
            || DeclaredSymbol is not null || ImplementedSymbol is not null
            || symbol.Destroyed || symbol.Kind != SymbolKind.Function
            || symbol.Declaration is null || symbol.Declaration == this
            || symbol.Implementation is not null || analysis is null || analysis.Destroyed
            || analysis.Compilation is null || analysis.Compilation != Source.Parent)
        {
            throw new InvalidOperationException("Cannot attach a separate function implementation.");
        }
        analysis.ClearIr();
        ImplementedSymbol = symbol;
        symbol.Implementation = this;
    }

    internal partial void RemoveImplementedSymbol(SemanticSymbol symbol)
    {
        if (ImplementedSymbol != symbol || symbol.Implementation != this)
        {
            throw new InvalidOperationException("Implementation has a different symbol.");
        }
        symbol.Scope?.Analysis?.ClearIr();
        ImplementedSymbol = null;
        symbol.Implementation = null;
    }

    internal partial void AddDeclaredSymbol(SemanticSymbol symbol)
    {
        SemanticAnalysis? analysis = symbol.Scope?.Analysis;
        if (Destroyed || Source is null || Source.Destroyed || ImplementedSymbol is not null || DeclaredSymbol is not null
            || symbol.Destroyed || symbol.Declaration is not null || analysis is null
            || analysis.Destroyed || analysis.Compilation is null
            || analysis.Compilation != Source.Parent)
        {
            throw new InvalidOperationException("Cannot attach declaration from another analysis.");
        }
        DeclaredSymbol = symbol;
        symbol.Declaration = this;
    }

    internal partial void RemoveDeclaredSymbol(SemanticSymbol symbol)
    {
        if (DeclaredSymbol != symbol || symbol.Declaration != this)
        {
            throw new InvalidOperationException("Declaration has a different symbol.");
        }
        DeclaredSymbol = null;
        symbol.Declaration = null;
    }
}
