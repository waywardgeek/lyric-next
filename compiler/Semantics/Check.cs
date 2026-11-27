using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace Lyric;

internal sealed partial class SourceFile
{
    internal partial Diagnostic SemanticDiagnostic(SyntaxNode node, string code, string message)
    {
        // Structural nodes have no anchor. Prefer the first anchored descendant;
        // an empty file reports its beginning. UTF-8 has already been validated.
        int offset = node.TextOffset;
        if (offset < 0)
        {
            Stack<SyntaxNode> pending = new();
            pending.Push(node);
            while (pending.Count != 0)
            {
                SyntaxNode current = pending.Pop();
                if (current.TextOffset >= 0)
                {
                    offset = current.TextOffset;
                    break;
                }
                for (int i = current.ChildCount() - 1; i >= 0; i -= 1)
                {
                    pending.Push(current.ChildAt(i));
                }
            }
        }
        offset = Math.Max(0, offset);
        int line = 1;
        int column = 1;
        int position = 0;
        while (position < offset)
        {
            Rune.DecodeFromUtf8(Bytes.AsSpan(position), out Rune scalar, out int size);
            position += size;
            if (scalar.Value == '\r')
            {
                // CRLF is one event, and original byte offsets remain unchanged.
                position += 1;
                line += 1;
                column = 1;
            }
            else if (scalar.Value == '\n')
            {
                line += 1;
                column = 1;
            }
            else
            {
                column += 1;
            }
        }
        return new Diagnostic(code, message, offset, line, column);
    }
}

internal sealed partial class Compilation
{
    internal partial Diagnostic? Check(bool executable, bool unsafeMode)
    {
        ClearAnalysis();
        SemanticAnalysis analysis = new(this, unsafeMode);
        SemanticScope global = new(analysis, null);
        // These lists are temporary traversal work, not duplicated persistent state.
        List<SemanticSymbol> functions = new();
        // Discovery depth bounds growing monomorphization, not the number of
        // independent roots. This is temporary checking work, not type identity
        // or a persistent ownership relationship. Existing keys are reused first.
        const int maximumSpecializationDepth = 1024;
        Dictionary<GenericSpecialization, int> specializationDepths = new();
        // Signature dependencies can arise in typeof operands. These sets
        // distinguish a completed contract from an active dependency cycle.
        HashSet<SemanticSymbol> completedSignatures = new();
        HashSet<SemanticSymbol> activeSignatures = new();
        List<SemanticSymbol> classes = new();
        List<SemanticSymbol> structs = new();
        List<SyntaxNode> implementations = new();
        List<SemanticSymbol> multiClassTemplates = new();
        List<SyntaxNode> multiClassImplementations = new();
        // Temporary context for constructor initialization, never model ownership.
        SemanticSymbol? checkingFunction = null;
        // A type query checks operations but never reads runtime storage.
        bool unevaluated = false;
        // Temporary flow facts, not persistent declaration state. Keeping the
        // sets distinct matters once branch joins are introduced.
        HashSet<SemanticSymbol> definitelyAssigned = new();
        HashSet<SemanticSymbol> possiblyAssigned = new();
        // Temporary storage selections must survive evaluation of their indices
        // and RHS/arguments. Root-level precision is conservative for this slice.
        Stack<SemanticSymbol> protectedArrays = new();
        // Loop records are temporary flow analysis, not model relationships.
        // Their scopes identify storage that exists before this iteration.
        Stack<(SemanticScope Scope, HashSet<SemanticSymbol> Back,
            HashSet<SemanticSymbol> Exits, List<HashSet<SemanticSymbol>> ExitDefinite,
            List<(SemanticSymbol Symbol, SyntaxNode Target)> Writes)> loops = new();
        bool HasBlock(SyntaxNode declaration)
        {
            for (int i = 0; i < declaration.ChildCount(); i += 1)
            {
                if (declaration.ChildAt(i).Kind == SyntaxKind.Block)
                {
                    return true;
                }
            }
            return false;
        }
        SyntaxNode ParametersOf(SyntaxNode declaration)
        {
            for (int i = 0; i < declaration.ChildCount(); i += 1)
            {
                if (declaration.ChildAt(i).Kind == SyntaxKind.Parameters)
                {
                    return declaration.ChildAt(i);
                }
            }
            throw new InvalidOperationException("Parsed function has no parameters.");
        }
        (SemanticType Type, bool Reference) ParameterType(
            SyntaxNode parameter, SemanticSymbol function, int position)
        {
            if (parameter.ChildCount() == 0 && Text(parameter) == "self"
                && position == 0 && function.Scope!.MemberOwner is SemanticSymbol owner)
            {
                SemanticSymbol receiver = owner.Kind == SymbolKind.MemberNamespace
                    ? function.Scope.Class! : owner;
                SemanticType type = receiver.Kind == SymbolKind.Struct
                    ? analysis.StructType(receiver) : analysis.ClassType(receiver);
                return (type, false);
            }
            if (parameter.ChildCount() != 1)
            {
                Unsupported(parameter);
            }
            SyntaxNode syntax = parameter.ChildAt(0);
            bool reference = syntax.Kind == SyntaxKind.TypePrefix && Text(syntax) == "ref";
            SemanticType resolved = ReadType(reference ? syntax.ChildAt(0) : syntax,
                function.Specialization is null && function.MultiClass is null
                    ? function.Scope! : function.FunctionScope!, true);
            return (resolved, reference);
        }
        // A lookup view is orthogonal to canonical compatibility. Explicit type
        // syntax selects the view; it cannot make an incompatible value legal.
        ParticipantBinding? TypeView(SyntaxNode node, SemanticScope scope)
        {
            while (node.Kind == SyntaxKind.OptionalType
                || (node.Kind == SyntaxKind.TypePrefix && Text(node) == "ref")
                || (node.Kind == SyntaxKind.TypeGroup && node.ChildCount() == 1
                    && (node.TextOffset < 0 || Text(node) != ",")))
            {
                node = node.ChildAt(0);
            }
            if (node.Kind == SyntaxKind.TypeName)
            {
                return scope.Lookup(Text(node))?.ParticipantView;
            }
            if (node.Kind == SyntaxKind.TypeOf)
            {
                return node.ChildAt(0).Fact?.ParticipantView;
            }
            return null;
        }
        // Chapter 13 carries participant views only on scalar handles. Do not
        // silently turn those borrowed values into owning aggregate elements.
        // An explicit concrete local drops the view and remains an ordinary owner.
        void RequireAggregateElementView(SyntaxNode origin, ParticipantBinding? view,
            bool annotation = false)
        {
            if (view?.Instantiation is not MultiClassInstantiation instance
                || instance.Policy == RelationshipPolicy.None)
            {
                return;
            }
            if (annotation || checkingFunction?.ContributingParticipant?.Instantiation == instance)
            {
                Fail(origin, "LYC304", "Participant aggregate elements are not supported in this chapter; use scalar participant values or explicit concrete owning values.");
            }
        }
        void CheckSignature(SemanticSymbol function)
        {
            if (function.Declaration is null || completedSignatures.Contains(function))
            {
                return;
            }
            if (!activeSignatures.Add(function))
            {
                Fail(function.Declaration, "LYC345", "Cyclic function signature dependency through typeof.");
            }
            SemanticSymbol? savedFunction = checkingFunction;
            checkingFunction = function;
            try
            {
                CheckSignatureCore(function);
                completedSignatures.Add(function);
            }
            finally
            {
                checkingFunction = savedFunction;
                activeSignatures.Remove(function);
            }
        }
        void CheckSignatureCore(SemanticSymbol function)
        {
            SemanticScope scope = function.FunctionScope ?? new SemanticScope(function);
            SyntaxNode declaration = function.Declaration!;
            SyntaxNode? implementation = function.Implementation;
            SyntaxNode? implementationParameters = implementation is null
                ? null : ParametersOf(implementation);
            // Separate bodies have their own parameter spellings. Keep the
            // declaration environment alive for its typeof facts, but never put
            // those names in the implementation body's lexical scope.
            SemanticScope declarationScope = implementation is null
                ? scope : new SemanticScope(analysis, function.Scope!);
            // Keep declaration names distinct even when the implementation uses
            // different lexical names. The signature types still come from Common.
            HashSet<string> declaredNames = new();
            for (int i = 1; i < declaration.ChildCount(); i += 1)
            {
                SyntaxNode child = declaration.ChildAt(i);
                if (child.Kind == SyntaxKind.Parameters)
                {
                    if (implementationParameters is not null
                        && implementationParameters.ChildCount() != child.ChildCount())
                    {
                        Fail(implementationParameters, "LYC342", "Implementation parameter count does not match its declaration.");
                    }
                    for (int j = 0; j < child.ChildCount(); j += 1)
                    {
                        SyntaxNode contract = child.ChildAt(j);
                        string contractName = Text(contract);
                        if (!declaredNames.Add(contractName)
                            || function.Scope!.Lookup(contractName) is not null)
                        {
                            Fail(contract, "LYC302", "Duplicate or shadowing declared parameter.");
                        }
                        // Resolve types outside parameter-local names. A renamed
                        // implementation parameter cannot change a declared type.
                        var signature = ParameterType(contract, function, j);
                        SyntaxNode parameter = implementationParameters?.ChildAt(j) ?? contract;
                        string name = Text(parameter);
                        if (implementationParameters is not null)
                        {
                            var supplied = ParameterType(parameter, function, j);
                            if (signature.Type != supplied.Type
                                || signature.Reference != supplied.Reference
                                || (contractName == "self") != (name == "self"))
                            {
                                Fail(parameter, "LYC342", "Implementation parameter type, reference mode, or receiver status does not match its declaration.");
                            }
                        }
                        if (implementationParameters is not null)
                        {
                            RefuseDuplicate(declarationScope, contract, contractName);
                            _ = new SemanticSymbol(declarationScope, contract, contractName,
                                SymbolKind.Parameter, signature.Type, signature.Reference,
                                isReference: signature.Reference);
                        }
                        RefuseDuplicate(scope, parameter, name);
                        SemanticSymbol parameterSymbol = new(scope, parameter, name, SymbolKind.Parameter,
                            signature.Type, signature.Reference, isReference: signature.Reference);
                        ParticipantBinding? parameterView = parameter.ChildCount() == 0
                            ? function.ContributingParticipant
                            : TypeView(parameter.ChildAt(0), scope);
                        parameterView?.AddViewSymbol(parameterSymbol);
                    }
                }
                else if (child.Kind == SyntaxKind.ResultType)
                {
                    function.SetType(ReadType(child.ChildAt(0), declarationScope, true));
                    TypeView(child.ChildAt(0), declarationScope)?.AddViewSymbol(function);
                }
                else if (child.Kind == SyntaxKind.Modifier && Text(child) == "final"
                    && function.Scope!.Class is not null)
                {
                    // The hook is identified by its existing declaration.
                }
                else if (child.Kind == SyntaxKind.Modifier && Text(child) is "add" or "remove"
                    && function.ContributingParticipant is ParticipantBinding endpoint
                    && endpoint.Instantiation!.Policy != RelationshipPolicy.None)
                {
                    // Signature validation below identifies the child endpoint.
                    // Lowering inserts refs-counted accounting at the checked
                    // preparation/structural boundaries, not at function entry.
                }
                else if (child.Kind == SyntaxKind.RelationKind && function.IsDestructorFragment())
                {
                    // The binding selected this policy before publishing the body.
                }
                else if (child.Kind != SyntaxKind.Block && child.Kind != SyntaxKind.Visibility)
                {
                    Unsupported(child);
                }
            }
            // Membership accounting needs one unambiguous parent/child pair.
            // Views distinguish endpoint occurrences even when their classes agree.
            for (int index = 0; index < declaration.ChildCount(); index += 1)
            {
                SyntaxNode modifier = declaration.ChildAt(index);
                if (modifier.Kind != SyntaxKind.Modifier
                    || Text(modifier) is not ("add" or "remove"))
                {
                    continue;
                }
                ParticipantBinding endpoint = function.ContributingParticipant!;
                MultiClassInstantiation instance = endpoint.Instantiation!;
                ParticipantBinding? childEndpoint = null;
                for (int position = 0; position < instance.ParticipantCount(); position += 1)
                {
                    if (instance.Template!.ParticipantRoleAt(position) == ParticipantRole.Child)
                    {
                        childEndpoint = instance.ParticipantAt(position);
                    }
                }
                if (instance.Template!.ParticipantRoleAt(endpoint.Position) != ParticipantRole.Parent
                    || scope.ParameterCount() != 2
                    || function.Type != analysis.ScalarType(ScalarKind.Unit))
                {
                    Fail(declaration, "LYC304",
                        "A relationship operation must take parent self and one child, and return unit.");
                }
                SemanticSymbol receiver = scope.ParameterAt(0);
                SemanticSymbol childParameter = scope.ParameterAt(1);
                if (receiver.Name != "self" || receiver.IsReference
                    || receiver.ParticipantView != endpoint
                    || receiver.Type != analysis.ClassType(endpoint.ConcreteClass!)
                    || childParameter.IsReference
                    || childParameter.ParticipantView != childEndpoint
                    || childParameter.Type != analysis.ClassType(childEndpoint!.ConcreteClass!))
                {
                    Fail(declaration, "LYC304",
                        "A relationship operation requires its parent receiver and a nonnullable child-role value.");
                }
            }
        }
        try
        {
            foreach (SemanticType type in analysis.PredefinedTypes())
            {
                string name = type == SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.U8)) ? "string" : SemanticTypes.Name(type);
                _ = new SemanticSymbol(global, null, name, SymbolKind.Type, type, false);
            }
            foreach (string name in new[] { "print", "println" })
            {
                SemanticSymbol builtin = new(global, null, name, SymbolKind.Function, analysis.ScalarType(ScalarKind.Unit), false,
                    name == "print" ? BuiltinKind.Print : BuiltinKind.Println);
                SemanticScope parameters = new(builtin);
                _ = new SemanticSymbol(parameters, null, "text", SymbolKind.Parameter, SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.U8)), false);
            }
            SemanticSymbol assertion = new(global, null, "assert", SymbolKind.Function,
                analysis.ScalarType(ScalarKind.Unit), false, BuiltinKind.Assert);
            SemanticScope assertionParameters = new(assertion);
            _ = new SemanticSymbol(assertionParameters, null, "condition", SymbolKind.Parameter,
                analysis.ScalarType(ScalarKind.Bool), false);
            foreach (var query in new[] { ("len", BuiltinKind.Length), ("capacity", BuiltinKind.Capacity) })
            {
                SemanticSymbol builtin = new(global, null, query.Item1, SymbolKind.Function,
                    analysis.ScalarType(ScalarKind.U64), false, query.Item2);
                SemanticScope parameters = new(builtin);
                _ = new SemanticSymbol(parameters, null, "array", SymbolKind.Parameter,
                    null, false, acceptsArray: true);
            }
            // Collect every top-level name before signatures and bodies: forward
            // calls and shadowing checks must not depend on declaration order.
            foreach (SourceFile source in Sources)
            {
                SyntaxNode root = source.SyntaxRoot() ?? throw new InvalidOperationException("Parse before checking.");
                for (int i = 0; i < root.ChildCount(); i += 1)
                {
                    SyntaxNode declaration = root.ChildAt(i);
                    if (declaration.Kind == SyntaxKind.Interface)
                    {
                        string interfaceName = Text(declaration);
                        RefuseDuplicate(global, declaration, interfaceName);
                        SemanticSymbol template = new(global, declaration, interfaceName,
                            SymbolKind.Interface, null, false);
                        multiClassTemplates.Add(template);
                        continue;
                    }
                    if (declaration.Kind == SyntaxKind.Impl)
                    {
                        multiClassImplementations.Add(declaration);
                        continue;
                    }
                    if (declaration.Kind == SyntaxKind.Struct)
                    {
                        string structName = Text(declaration);
                        RefuseDuplicate(global, declaration, structName);
                        SemanticSymbol structure = new(global, declaration, structName,
                            SymbolKind.Struct, null, false);
                        _ = new SemanticScope(structure);
                        structs.Add(structure);
                        continue;
                    }
                    if (declaration.Kind == SyntaxKind.Class)
                    {
                        string className = Text(declaration);
                        RefuseDuplicate(global, declaration, className);
                        SemanticSymbol classSymbol = new(global, declaration, className,
                            SymbolKind.Class, null, false);
                        _ = new SemanticScope(classSymbol);
                        classes.Add(classSymbol);
                        continue;
                    }
                    if (declaration.Kind != SyntaxKind.Function)
                    {
                        Unsupported(declaration);
                    }
                    SyntaxNode nameNode = declaration.ChildAt(0);
                    if (nameNode.Kind != SyntaxKind.Name)
                    {
                        implementations.Add(declaration);
                        continue;
                    }
                    string name = Text(nameNode);
                    RefuseDuplicate(global, nameNode, name);
                    SemanticSymbol function = new(global, declaration, name, SymbolKind.Function, analysis.ScalarType(ScalarKind.Unit), false);
                    functions.Add(function);
                }
            }
            // Declaration validity is independent of whether a template is used.
            // Bodies remain unchecked until their participant types are bound.
            foreach (SemanticSymbol template in multiClassTemplates)
            {
                ValidateMultiClassTemplate(template);
            }
            // Resolve all struct fields before inspecting inline layout. A forward
            // nominal identity is complete as a key, but not yet as a layout.
            foreach (SemanticSymbol structure in structs)
            {
                SyntaxNode declaration = structure.Declaration!;
                SemanticScope members = structure.MemberScope!;
                for (int i = 0; i < declaration.ChildCount(); i += 1)
                {
                    SyntaxNode field = declaration.ChildAt(i);
                    if (field.Kind == SyntaxKind.Visibility)
                    {
                        continue;
                    }
                    if (field.Kind == SyntaxKind.Function
                        && field.ChildAt(0).Kind == SyntaxKind.Name)
                    {
                        SyntaxNode methodName = field.ChildAt(0);
                        string methodText = Text(methodName);
                        RefuseDuplicate(members, methodName, methodText);
                        SemanticSymbol method = new(members, field, methodText,
                            SymbolKind.Function, analysis.ScalarType(ScalarKind.Unit), false);
                        functions.Add(method);
                        continue;
                    }
                    // Defaults have their own declaration-scoped execution context.
                    if (field.Kind != SyntaxKind.Field || field.ChildCount() is < 1 or > 2)
                    {
                        Unsupported(field);
                    }
                    string name = Text(field);
                    RefuseDuplicate(members, field, name);
                    // A reference field stores an address of the canonical
                    // referent type; reference mode belongs to the field symbol.
                    SyntaxNode annotation = field.ChildAt(0);
                    bool reference = annotation.Kind == SyntaxKind.TypePrefix
                        && Text(annotation) == "ref";
                    SemanticType type = ReadType(reference ? annotation.ChildAt(0) : annotation,
                        members, allowStructs: true);
                    SemanticSymbol symbol = new(members, field, name,
                        SymbolKind.Field, type, true, isReference: reference);
                    if (field.ChildCount() == 2)
                    {
                        _ = new SemanticScope(symbol);
                    }
                }
            }
            foreach (SemanticSymbol structure in structs)
            {
                // All fields now exist, including forward nominal dependencies.
                // An earlier type read could not see borrows in a later declaration.
                RequireValidArrayStorage(structure.Declaration!, analysis.StructType(structure));
                if (!SemanticTypes.HasFiniteLayout(analysis.StructType(structure)))
                {
                    Fail(structure.Declaration!, "LYC341",
                        $"Struct '{structure.Name}' has an infinite inline layout.");
                }
            }
            // All nominal identities exist before member types are resolved.
            // Members belong to a qualified namespace, not the lexical namespace
            // of method bodies. Their declarations retain normal syntax backlinks.
            foreach (SemanticSymbol classSymbol in classes)
            {
                SyntaxNode declaration = classSymbol.Declaration!;
                SemanticScope members = classSymbol.MemberScope!;
                for (int i = 0; i < declaration.ChildCount(); i += 1)
                {
                    SyntaxNode member = declaration.ChildAt(i);
                    if (member.Kind == SyntaxKind.Modifier && Text(member) == "value")
                    {
                        continue;
                    }
                    if (member.Kind == SyntaxKind.Field)
                    {
                        if (member.ChildCount() != 1)
                        {
                            Unsupported(member);
                        }
                        string fieldName = Text(member);
                        RefuseDuplicate(members, member, fieldName);
                        SemanticType fieldType = ReadType(member.ChildAt(0), members);
                        // Preserve the current representation admissions here.
                        // Persistent handle safety is checked after all relationship
                        // contributions exist, using their actual participant bindings.
                        if ((fieldType.Class is not null && fieldType.ArrayDepth > 0)
                            || fieldType == analysis.ScalarType(ScalarKind.Unit))
                        {
                            Unsupported(member);
                        }
                        _ = new SemanticSymbol(members, member, fieldName,
                            SymbolKind.Field, fieldType, true);
                        continue;
                    }
                    if (member.Kind != SyntaxKind.Function
                        || member.ChildAt(0).Kind != SyntaxKind.Name)
                    {
                        Unsupported(member);
                    }
                    SyntaxNode methodName = member.ChildAt(0);
                    string name = Text(methodName);
                    RefuseDuplicate(members, methodName, name);
                    SemanticSymbol method = new(members, member, name,
                        SymbolKind.Function, analysis.ScalarType(ScalarKind.Unit), false);
                    functions.Add(method);
                }
            }
            // Bindings are admitted after ordinary members, so contributions
            // cannot silently replace an existing field, method, or namespace.
            foreach (SyntaxNode implementation in multiClassImplementations)
            {
                BindMultiClass(implementation);
            }
            // Local lifetime analysis cannot repair a stale handle hidden in an
            // object. Inspect physical storage after contributions are attached,
            // including labeled namespaces, before checking any method bodies.
            foreach (SemanticSymbol owner in classes)
            {
                foreach (SemanticSymbol field in owner.MemberScope!.Fields())
                {
                    Stack<SemanticType> pending = new();
                    HashSet<SemanticType> visited = new();
                    pending.Push(field.Type!);
                    while (pending.Count != 0)
                    {
                        SemanticType stored = pending.Pop();
                        if (!visited.Add(stored))
                        {
                            continue;
                        }
                        if (stored.Kind is SemanticTypeKind.Nominal or SemanticTypeKind.Nullable
                            && stored.Class is SemanticSymbol target)
                        {
                            // A handle ends the inline walk. Counted targets keep
                            // themselves alive; their own fields are checked separately.
                            if (target.IsCountedClass())
                            {
                                continue;
                            }
                            bool maintained = false;
                            MultiClassInstantiation? relationship =
                                field.ContributingParticipant?.Instantiation;
                            if (relationship is not null
                                && relationship.Policy != RelationshipPolicy.None)
                            {
                                for (int role = 0; role < relationship.ParticipantCount(); role += 1)
                                {
                                    if (relationship.ParticipantAt(role).ConcreteClass == target)
                                    {
                                        maintained = true;
                                    }
                                }
                            }
                            if (!maintained)
                            {
                                Fail(field.Declaration!, "LYC304",
                                    "Persistent ordinary-object references require a maintained relationship to that participant.");
                            }
                            continue;
                        }
                        if (stored.Kind == SemanticTypeKind.Array)
                        {
                            pending.Push(stored.Element!);
                        }
                        else if (stored.Kind == SemanticTypeKind.Tuple)
                        {
                            for (int index = 0; index < stored.ComponentCount(); index += 1)
                            {
                                pending.Push(stored.ComponentAt(index));
                            }
                        }
                        else if (stored.Declaration?.Kind == SymbolKind.Struct)
                        {
                            foreach (SemanticSymbol component in stored.Declaration.MemberScope!.Fields())
                            {
                                pending.Push(component.Type!);
                            }
                        }
                    }
                }
            }
            // Resolve bodies only after all member declarations exist. They do not
            // introduce symbols or overloads, regardless of their source order.
            foreach (SyntaxNode implementation in implementations)
            {
                SyntaxNode name = implementation.ChildAt(0);
                if (name.Kind != SyntaxKind.Member || name.ChildCount() != 1
                    || name.ChildAt(0).Kind != SyntaxKind.Name)
                {
                    Unsupported(name);
                }
                SemanticSymbol? owner = global.Lookup(Text(name.ChildAt(0)));
                SemanticSymbol? method = owner?.MemberScope?.LookupMember(Text(name));
                if (owner?.Kind != SymbolKind.Struct || method?.Kind != SymbolKind.Function)
                {
                    Fail(name, "LYC342", "A separate body must implement a declared struct method.");
                }
                if (method!.Implementation is not null || HasBlock(method.Declaration!)
                    || !HasBlock(implementation))
                {
                    Fail(implementation, "LYC342", "A declared method requires exactly one body.");
                }
                implementation.AddImplementedSymbol(method);
            }
            // Templates retain one source-visible declaration. Only concrete
            // bodies enter the ordinary signature/checking queue.
            foreach (SemanticSymbol template in functions)
            {
                if (template.GenericParameters() is not SyntaxNode parameters)
                {
                    continue;
                }
                if (template.Scope!.MemberOwner is not null || template.Implementation is not null)
                {
                    Unsupported(template.Declaration!);
                }
                HashSet<string> names = new();
                for (int index = 0; index < parameters.ChildCount(); index += 1)
                {
                    SyntaxNode parameter = parameters.ChildAt(index);
                    if (parameter.ChildCount() != 0)
                    {
                        Unsupported(parameter);
                    }
                    string name = Text(parameter);
                    if (!names.Add(name) || template.Scope.Lookup(name) is not null)
                    {
                        Fail(parameter, "LYC302", "Duplicate or shadowing generic parameter.");
                    }
                }
                for (int index = 0; index < ParametersOf(template.Declaration!).ChildCount(); index += 1)
                {
                    SyntaxNode parameter = ParametersOf(template.Declaration!).ChildAt(index);
                    if (!names.Add(Text(parameter)) || template.Scope.Lookup(Text(parameter)) is not null)
                    {
                        Fail(parameter, "LYC302", "Duplicate or shadowing declared parameter.");
                    }
                }
            }
            functions.RemoveAll(function => function.GenericParameters() is not null);
            // A signature's typeof operand can discover concrete functions.
            // Index traversal admits that growth; completed signatures are reused.
            for (int index = 0; index < functions.Count; index += 1)
            {
                CheckSignature(functions[index]);
            }
            // Implementation result queries can discover functions too. Complete
            // each signature before validating its contract, including new entries.
            for (int index = 0; index < functions.Count; index += 1)
            {
                SemanticSymbol function = functions[index];
                CheckSignature(function);
                // Reject the contract even when there is no return expression to check.
                if (SemanticTypes.HasContainedBorrows(function.Type!))
                {
                    Fail(function.Declaration!, "LYC343", "Function results must not contain borrowed references.");
                }
                if (function.Implementation is not SyntaxNode implementation)
                {
                    continue;
                }
                SemanticType result = analysis.ScalarType(ScalarKind.Unit);
                for (int i = 1; i < implementation.ChildCount(); i += 1)
                {
                    SyntaxNode child = implementation.ChildAt(i);
                    if (child.Kind == SyntaxKind.ResultType)
                    {
                        result = ReadType(child.ChildAt(0), function.FunctionScope!, true);
                    }
                    else if (child.Kind is not (SyntaxKind.Parameters or SyntaxKind.Block or SyntaxKind.Visibility))
                    {
                        Unsupported(child);
                    }
                }
                if (result != function.Type)
                {
                    Fail(implementation, "LYC342", "Implementation result type does not match its declaration.");
                }
            }
            // Receiver identity comes from the actual member-scope parent.
            // Constructor bodies return unit; construction expressions will
            // supply the nominal object separately from that body result.
            foreach (SemanticSymbol function in functions)
            {
                if (function.Scope!.MemberOwner is SemanticSymbol structure
                    && structure.Kind == SymbolKind.Struct)
                {
                    SemanticScope signature = function.FunctionScope!;
                    for (int p = 0; p < signature.ParameterCount(); p += 1)
                    {
                        SemanticSymbol parameter = signature.ParameterAt(p);
                        if (parameter.Name != "self")
                        {
                            continue;
                        }
                        if (p != 0 || parameter.Type != analysis.StructType(structure))
                        {
                            Fail(parameter.Declaration!, "LYC304",
                                "A struct receiver must be the first parameter and have the enclosing struct type.");
                        }
                    }
                    continue;
                }
                SemanticSymbol? owner = function.Scope!.Class;
                if (owner is null)
                {
                    continue;
                }
                SemanticScope parameters = function.FunctionScope!;
                if (parameters.ParameterCount() == 0
                    || parameters.ParameterAt(0).Name != "self")
                {
                    Unsupported(function.Declaration!);
                }
                if (function.IsFinalHook())
                {
                    if (function.Name == owner.Name)
                    {
                        Fail(function.Declaration!, "LYC333", "A constructor cannot be a final hook.");
                    }
                    if (parameters.ParameterCount() != 1 || function.Type != analysis.ScalarType(ScalarKind.Unit))
                    {
                        Fail(function.Declaration!, "LYC333", "A final hook must take only self and return unit.");
                    }
                    if (owner.FinalHook() != function)
                    {
                        Fail(function.Declaration!, "LYC333", "A class may have only one final hook.");
                    }
                }
                if (function.Name == owner.Name && function.Type != analysis.ScalarType(ScalarKind.Unit))
                {
                    Fail(function.Declaration!, "LYC327",
                        "An infallible constructor body must return unit.");
                }
            }
            SemanticSymbol? main = global.Lookup("main");
            SyntaxNode first = Sources[0].SyntaxRoot()!;
            if (executable && (main is null || main.Kind != SymbolKind.Function))
            {
                Fail(first, "LYC305", "Executable requires a main function.");
            }
            if (executable && (main!.GenericParameters() is not null
                || main.FunctionScope!.ParameterCount() != 0 || main.Type != analysis.ScalarType(ScalarKind.Unit)))
            {
                Fail(main!.Declaration!, "LYC305", "main must have no parameters and return unit.");
            }
            Dictionary<SemanticSymbol, SyntaxNode> bodies = new();
            // Check every declared default, even when all current constructions
            // override it. Its scope excludes the partial receiver and sibling fields.
            foreach (SemanticSymbol structure in structs)
            {
                foreach (SemanticSymbol field in structure.MemberScope!.Fields())
                {
                    if (field.InitializerScope is not SemanticScope scope)
                    {
                        continue;
                    }
                    SyntaxNode expression = field.Declaration!.ChildAt(1);
                    definitelyAssigned.Clear();
                    possiblyAssigned.Clear();
                    checkingFunction = field;
                    SemanticType actual;
                    if (field.IsReference)
                    {
                        // A reference default names storage, not a converted value.
                        // Declaration scope still excludes caller locals and self.
                        actual = CheckReference(expression, scope);
                        if (actual != field.Type)
                        {
                            Fail(expression, "LYC325", "Reference field defaults require the exact referent type.");
                        }
                    }
                    else
                    {
                        actual = CheckExpression(expression, scope, field.Type);
                        RequireType(expression, actual, field.Type!);
                    }
                    bodies.Add(field, expression);
                }
            }
            for (int nextFunction = 0; nextFunction < functions.Count; nextFunction += 1)
            {
                SemanticSymbol function = functions[nextFunction];
                SyntaxNode? body = null;
                SyntaxNode declaration = function.BodySyntax()!;
                for (int i = 0; i < declaration.ChildCount(); i += 1)
                {
                    if (declaration.ChildAt(i).Kind == SyntaxKind.Block)
                    {
                        body = declaration.ChildAt(i);
                    }
                }
                if (body is null)
                {
                    if (function.Scope!.MemberOwner?.Kind == SymbolKind.Struct)
                    {
                        Fail(declaration, "LYC342", "A declared method requires exactly one body.");
                    }
                    Unsupported(declaration);
                }
                definitelyAssigned.Clear();
                possiblyAssigned.Clear();
                SemanticScope scope = function.FunctionScope!;
                for (int i = 0; i < scope.ParameterCount(); i += 1)
                {
                    MarkAssigned(scope.ParameterAt(i));
                }
                checkingFunction = function;
                if (IsConstructor())
                {
                    foreach (SemanticSymbol field in function.Scope!.Class!.MemberScope!.Fields())
                    {
                        // Allocation installs null even in reused slots. Only
                        // nullable managed links acquire source initialization;
                        // a nonnullable cleanup sentinel is not a language value.
                        if (field.IsManagedLink() && field.Type!.Kind == SemanticTypeKind.Nullable)
                        {
                            MarkAssigned(field);
                        }
                    }
                }
                bool continues = CheckBlock(body!, scope, function);
                if (continues)
                {
                    RequireInitializedReceiver(declaration);
                }
                bodies.Add(function, body!);
                if (continues && function.Type != analysis.ScalarType(ScalarKind.Unit))
                {
                    Fail(declaration, "LYC309", $"Function '{function.Name}' may finish without returning {SemanticTypes.Name(function.Type!)}.");
                }
            }
            // Effects and ordered overlap obligations are monotone, finite sets
            // of formal-parameter paths. All bodies have facts before iteration,
            // so forward calls and recursive groups use the same analysis.
            Dictionary<SemanticSymbol, HashSet<(SemanticSymbol Root, int Depth, int Component)>> effects = new();
            Dictionary<SemanticSymbol, HashSet<((SemanticSymbol Root, int Depth, int Component) Changed,
                (SemanticSymbol Root, int Depth, int Component) Used)>> obligations = new();
            foreach (SemanticSymbol function in bodies.Keys)
            {
                effects.Add(function, new());
                obligations.Add(function, new());
            }
            Dictionary<SemanticSymbol, HashSet<SemanticSymbol>> classEffects = new();
            foreach (SemanticSymbol function in bodies.Keys)
            {
                classEffects.Add(function, new());
            }
            // Exit summaries describe externally writable contained-reference slots.
            // Targets alone are insufficient: copying an invalid or expired alias
            // into a caller's holder must preserve that history.
            Dictionary<SemanticSymbol, Dictionary<(SemanticSymbol Parameter, int Component),
                (HashSet<(SemanticSymbol Root, int Depth, int Component)> Targets,
                 HashSet<(SemanticSymbol Root, int Depth, int Component)> Causes,
                 bool Expired)>> replacements = new();
            foreach (SemanticSymbol function in bodies.Keys)
            {
                replacements.Add(function, new());
            }
            // Bottom means no returning path has been established, not an
            // identity replacement. Otherwise an early forward/recursive call
            // would permanently export an obsolete unchanged target.
            HashSet<SemanticSymbol> returningFunctions = new();
            // Initializer result graphs use ordered result positions, never the
            // handles of a particular flow-graph build. Edges retain overlapping
            // reachable storage; facts grow monotonically with the fixed point.
            Dictionary<SemanticSymbol, List<
                (HashSet<(SemanticSymbol Root, int Depth, int Component)> Targets,
                 HashSet<(SemanticSymbol Root, int Depth, int Component)> Causes,
                 bool Expired, List<HashSet<int>> Referents, bool Repeated)>> initializerResults = new();
            bool changed;
            do
            {
                changed = false;
                foreach (SemanticSymbol function in bodies.Keys)
                {
                    changed |= CheckBorrows(function, bodies[function], effects, obligations, classEffects, replacements, returningFunctions, initializerResults, false);
                }
            }
            while (changed);
            // Preparation may validate and reserve storage, but may not publish
            // relationship changes. These temporary summaries follow resolved
            // symbol identities, including forward/recursive helper calls.
            HashSet<SemanticSymbol> managedWriters = new();
            bool ManagedDestination(SyntaxNode node)
            {
                if (node.Fact?.Symbol?.IsManagedLink() == true)
                {
                    return true;
                }
                if (node.Kind is SyntaxKind.Group or SyntaxKind.Member or SyntaxKind.Index)
                {
                    return node.ChildCount() != 0 && ManagedDestination(node.ChildAt(0));
                }
                return false;
            }
            bool DestructionWritesLinks(SemanticSymbol owner)
            {
                Stack<SemanticSymbol> pending = new();
                HashSet<SemanticSymbol> visited = new();
                pending.Push(owner);
                while (pending.Count != 0)
                {
                    SemanticSymbol current = pending.Pop();
                    if (!visited.Add(current))
                    {
                        continue;
                    }
                    foreach (SemanticSymbol method in current.MemberScope!.Methods())
                    {
                        if ((method.IsFinalHook() || method.IsDestructorFragment())
                            && managedWriters.Contains(method))
                        {
                            return true;
                        }
                    }
                    // The existing class-effect closure already includes releases
                    // and generated teardown; do not reconstruct object identities.
                    foreach (SemanticSymbol method in current.MemberScope.Methods())
                    {
                        if ((method.IsFinalHook() || method.IsDestructorFragment())
                            && classEffects.TryGetValue(method, out var affected))
                        {
                            foreach (SemanticSymbol other in affected)
                            {
                                pending.Push(other);
                            }
                        }
                    }
                }
                return false;
            }
            bool WritesManagedLinks(SyntaxNode root)
            {
                Stack<SyntaxNode> pending = new();
                pending.Push(root);
                while (pending.Count != 0)
                {
                    SyntaxNode node = pending.Pop();
                    if (node.Kind == SyntaxKind.TypeOf)
                    {
                        continue;
                    }
                    if (node.Kind == SyntaxKind.MatchType)
                    {
                        pending.Push(node.ChildAt(node.Fact!.SelectedArmIndex).ChildAt(1));
                        continue;
                    }
                    if ((node.Kind == SyntaxKind.Assignment
                            || (node.Kind == SyntaxKind.Unary && Text(node) == "ref"))
                        && ManagedDestination(node.ChildAt(0)))
                    {
                        return true;
                    }
                    if (node.Kind == SyntaxKind.Call && node.Fact is SemanticFact call)
                    {
                        if (call.ArrayOperation != ArrayOperation.None
                            && ManagedDestination(node.ChildAt(0).ChildAt(0)))
                        {
                            return true;
                        }
                        if (call.ClassOperation == ClassOperation.Destroy
                            && DestructionWritesLinks(call.Symbol!))
                        {
                            return true;
                        }
                        if (call.Symbol is SemanticSymbol target && managedWriters.Contains(target))
                        {
                            return true;
                        }
                    }
                    for (int index = node.ChildCount() - 1; index >= 0; index -= 1)
                    {
                        pending.Push(node.ChildAt(index));
                    }
                }
                return false;
            }
            do
            {
                changed = false;
                foreach (SemanticSymbol function in bodies.Keys)
                {
                    if (managedWriters.Contains(function))
                    {
                        continue;
                    }
                    bool writes = WritesManagedLinks(bodies[function]);
                    foreach (SemanticSymbol owner in classEffects[function])
                    {
                        writes |= DestructionWritesLinks(owner);
                    }
                    if (writes)
                    {
                        changed |= managedWriters.Add(function);
                    }
                }
            }
            while (changed);
            foreach (SyntaxNode body in bodies.Values)
            {
                if (body.ChildCount() != 0 && body.ChildAt(0).Kind == SyntaxKind.Prepare
                    && WritesManagedLinks(body.ChildAt(0)))
                {
                    Fail(body.ChildAt(0), "LYC308", "Preparation may not mutate managed relationship links, directly or through helpers.");
                }
            }
            // Check the full fixed point before ordinary stale-use diagnostics:
            // the final contract forbids same-class destruction even without a
            // later source read, and includes effects of other generated hooks.
            foreach (SemanticSymbol function in bodies.Keys)
            {
                if (function.IsFinalHook() && classEffects[function].Contains(function.Scope!.Class!))
                {
                    Fail(function.Declaration!, "LYC334", "A final hook may not transitively destroy its own class.");
                }
            }
            foreach (SemanticSymbol function in bodies.Keys)
            {
                CheckBorrows(function, bodies[function], effects, obligations, classEffects, replacements, returningFunctions, initializerResults, true, DestructionWritesLinks);
            }
            return null;
        }
        catch (SemanticFailure failure)
        {
            ClearAnalysis();
            return failure.Diagnostic;
        }

        // Graphs, storage paths, and interprocedural summaries are temporary
        // analysis data. An ordered obligation means that a mutation can precede
        // a use of an already selected borrow, not merely that both occur.
        bool CheckBorrows(SemanticSymbol function, SyntaxNode body,
            Dictionary<SemanticSymbol, HashSet<(SemanticSymbol Root, int Depth, int Component)>> effects,
            Dictionary<SemanticSymbol, HashSet<((SemanticSymbol Root, int Depth, int Component) Changed,
                (SemanticSymbol Root, int Depth, int Component) Used)>> obligations,
            Dictionary<SemanticSymbol, HashSet<SemanticSymbol>> classEffects,
            Dictionary<SemanticSymbol, Dictionary<(SemanticSymbol Parameter, int Component),
                (HashSet<(SemanticSymbol Root, int Depth, int Component)> Targets,
                 HashSet<(SemanticSymbol Root, int Depth, int Component)> Causes,
                 bool Expired)>> replacements,
            HashSet<SemanticSymbol> returningFunctions,
            Dictionary<SemanticSymbol, List<
                (HashSet<(SemanticSymbol Root, int Depth, int Component)> Targets,
                 HashSet<(SemanticSymbol Root, int Depth, int Component)> Causes,
                 bool Expired, List<HashSet<int>> Referents, bool Repeated)>> initializerResults,
            bool diagnose, Func<SemanticSymbol, bool>? destructionWritesLinks = null)
        {
            bool summaryChanged = false;
            // Component -1 denotes ordinary storage. Nonnegative components name
            // ordered reference occurrences inside a formal aggregate, not field
            // names: repeated nested types can designate different actual targets.
            List<(SemanticSymbol Root, int Depth, int Component)> paths = new();
            Dictionary<SemanticSymbol, int> aliases = new();
            // Slots are ordered reference occurrences, not distinct field symbols:
            // two inline copies of the same struct still contain separate aliases.
            Dictionary<SemanticSymbol, List<int>> containedBindings = new();
            // A folded recursive location can represent multiple concrete holders.
            // Even a singleton destination set therefore requires a weak update.
            HashSet<int> summarizedStorage = new();
            // Only externally reachable storage is exported. A value parameter's
            // root is private, but storage reached through its references is not.
            Dictionary<(SemanticSymbol Parameter, int Component), int> externalFormalSlots = new();
            // Each expression records a snapshot, not a live link to a local's
            // slots. Later argument evaluation may replace that local's value.
            Dictionary<SyntaxNode, List<int>> containedValues = new();
            Dictionary<SyntaxNode, int> referenceValues = new();
            // Saved result values and their external storage destinations are
            // separate. A reference result itself occupies position zero; the
            // remaining positions follow the finite type graph's slot order.
            List<int> initializerValues = new();
            List<int> initializerDestinations = new();
            // Explicit storage-selection events publish identities of live slots,
            // not snapshots of their contents. Copies of a pointer preserve these
            // identities so later dereference can observe holder replacement.
            Dictionary<int, List<int>> attachedStorage = new();
            // Dereference reads choose live source slots during flow analysis,
            // not while the syntax graph is being constructed.
            Dictionary<int, (int Address, int Occurrence)> dereferencedStorage = new();
            // Allocate a complete result graph before installing its edges. This
            // lets cyclic/overlapping summaries refer only to this invocation's
            // slots, never to handles from the initializer's analysis.
            Dictionary<int, (SemanticSymbol Field, List<int> Slots)> importedInitializers = new();
            Dictionary<int, (int Address, int Offset, int Count)> projectedStorage = new();
            Dictionary<int, (int Address, int Occurrence, int Count, int Source)> storedReferents = new();
            // Call-entry traversal keeps saved values separate from writable slots.
            // Referents maps each formal slot to its ordered child slot numbers.
            Dictionary<int, (List<int> Roots, List<List<int>> Referents,
                List<int> Inputs, int Address, List<int> Values, List<int> Destinations)> mappedActuals = new();
            // A copy event snapshots targets and their invalidation history. It
            // must not retain a live link to a source handle that can be acquired
            // again on a later loop iteration. DepthDelta follows array selection.
            Dictionary<int, (int Source, int DepthDelta)> copiedTargets = new();
            // Event vocabulary (the payload is named Borrow for historical
            // storage-path events): 0 join/no-op; 1 acquire storage; 2 use storage;
            // 3 mutate storage; 4 ordered call obligation; 5 acquire object value;
            // 6 use object value; 7 class destruction effect; 8 occupy holder;
            // 9 release and empty holder; 10 empty holder without release (move);
            // 11 possible partial release, preserving possible occupancy;
            // 12 end local storage (independent of ownership cleanup);
            // 13 copy target state, optionally selecting a deeper array address;
            // 14 successful function exit, after all applicable cleanup;
            // 15 replace a caller slot with a callee's saved exit alternatives;
            // 16 attach the selected referent's ordered live slot identities;
            // 17 snapshot one occurrence from the currently selected referent;
            // 18 narrow live referent slots to an inline component;
            // 19 store a saved value occurrence through selected live storage;
            // 20 map a finite formal storage graph to caller values and destinations;
            // 21 import an initializer's post-cleanup result graph and alias history.
            List<(SyntaxNode Node, int Operation, int Borrow, List<int> Next)> graph = new();
            Dictionary<int, (int Changed, int Used)> callRequirements = new();
            // All sources are snapshotted before any caller slot is overwritten.
            // Multiple formals can designate one actual slot; their outputs merge.
            Dictionary<int, List<(int Address, int Occurrence, List<int> Sources,
                List<int> Causes, bool Expired)>> replacedTargets = new();
            // These are temporary graph identities, not persistent model parents.
            // Class effects are separate from array-path mutations: allocation
            // does not invalidate indexed object handles; destruction does.
            List<SemanticSymbol> objectClasses = new();
            // The generated caller retains this receiver's storage until every
            // teardown fragment returns. Other values of its class are not protected.
            HashSet<int> teardownReceivers = new();
            Dictionary<SemanticSymbol, List<int>> objectBindings = new();
            Dictionary<SyntaxNode, List<int>> objectValues = new();
            Dictionary<int, SemanticSymbol> destroyedClasses = new();
            Dictionary<int, List<int>> selectedObjects = new();
            // Inline component selection keeps array depth unchanged, but does
            // not replace the entire cleanup holder when written through an alias.
            HashSet<int> partialHolders = new();
            // Negative depths identify class-wide effects, not array nesting.
            // Growth changes field addresses; destruction also ends payload life.
            const int classGrowth = -1;
            const int classDestruction = -2;
            HashSet<int> indexedFieldDestinations = new();
            List<int> frontier = new();
            Stack<(int Header, int Exit, int CleanupDepth)> loopEdges = new();
            // Temporary ownership frames follow lexical/control-transfer exits.
            List<(List<int> Holders, List<SemanticSymbol> Storage)> cleanupFrames =
                new() { (new(), new()) };
            Dictionary<int, SemanticSymbol> endedStorage = new();
            List<SemanticType> holderTypes = new();
            Dictionary<SemanticSymbol, int> localHolders = new();
            // Aggregate expression owners follow emitted temporary transfers.
            // A move disarms only that temporary, not its receiving owner.
            Dictionary<SyntaxNode, int> aggregateTemporaries = new();
            int entry = Event(body, 0, -1);
            SemanticScope executionScope = function.InitializerScope ?? function.FunctionScope!;
            SemanticSymbol? teardownReceiver = function.IsDestructorFragment()
                ? executionScope.ParameterAt(0) : null;
            for (int i = 0; i < executionScope.ParameterCount(); i += 1)
            {
                SemanticSymbol parameter = executionScope.ParameterAt(i);
                if (parameter.Type is SemanticType parameterType)
                {
                    List<int> parameterObjects = NewObjects(parameter.Declaration!, parameterType);
                    objectBindings.Add(parameter, parameterObjects);
                    if (parameter == teardownReceiver)
                    {
                        teardownReceivers.UnionWith(parameterObjects);
                    }
                    var formal = FormalStorage(parameterType, parameter.IsReference);
                    List<int> allTargets = new();
                    for (int component = 0; component < formal.Slots.Count; component += 1)
                    {
                        int target = Acquire(parameter.Declaration!, (parameter, 0, component));
                        allTargets.Add(target);
                        int container = formal.Slots[component].Container;
                        if (container != 0 || parameter.IsReference)
                        {
                            externalFormalSlots.Add((parameter, component), target);
                        }
                        if (formal.Nodes[container].Repeated)
                        {
                            summarizedStorage.Add(target);
                        }
                    }
                    // Allocate the complete finite graph before publishing links:
                    // a recursive edge can refer to slots allocated earlier or later.
                    for (int component = 0; component < formal.Slots.Count; component += 1)
                    {
                        int referent = formal.Slots[component].Referent;
                        List<int> referentTargets = new();
                        foreach (int slot in formal.Nodes[referent].Slots)
                        {
                            referentTargets.Add(allTargets[slot]);
                        }
                        if (referentTargets.Count != 0)
                        {
                            int attach = Event(parameter.Declaration!, 16, allTargets[component]);
                            attachedStorage.Add(attach, referentTargets);
                        }
                    }
                    List<int> parameterTargets = new();
                    foreach (int slot in formal.Nodes[0].Slots)
                    {
                        parameterTargets.Add(allTargets[slot]);
                    }
                    if (parameterTargets.Count != 0)
                    {
                        containedBindings.Add(parameter, parameterTargets);
                    }
                }
                bool borrowedSelf = i == 0 && function.Scope!.Class is SemanticSymbol receiverClass
                    && (function.Name == receiverClass.Name || function.IsFinalHook()
                        || function.IsDestructorFragment());
                if (!parameter.IsReference)
                {
                    // The parameter slot is callee-local even when its value has
                    // no cleanup. Reference parameters instead designate caller storage.
                    cleanupFrames[0].Storage.Add(parameter);
                }
                if (!parameter.IsReference && !borrowedSelf && parameter.Type is SemanticType ownedParameter)
                {
                    RegisterHolder(parameter.Declaration!, ownedParameter, true);
                }
                if (parameter.IsReference)
                {
                    int address = Acquire(parameter.Declaration!, (parameter, 0, -1));
                    aliases.Add(parameter, address);
                    if (containedBindings.TryGetValue(parameter, out var parameterSlots))
                    {
                        // The reference parameter designates caller storage. Its
                        // selected address and contained occurrences must describe
                        // the same live slots, not independent value snapshots.
                        attachedStorage.Add(Event(parameter.Declaration!, 16, address), parameterSlots);
                    }
                }
            }
            if (function.InitializerScope is not null)
            {
                // Save aliases before cleanup, then check those same handles after
                // cleanup. A reference result is an address, never an owned value
                // to transfer; a value result may itself contain borrowed aliases.
                List<int> resultTargets;
                if (function.IsReference)
                {
                    SyntaxNode reference = body;
                    while (reference.Kind == SyntaxKind.Group)
                    {
                        reference = reference.ChildAt(0);
                    }
                    int address = Select(reference.ChildAt(0));
                    resultTargets = new() { address };
                }
                else
                {
                    BuildExpression(body);
                    MoveAggregate(body);
                    resultTargets = containedValues.TryGetValue(body, out List<int>? valueTargets)
                        ? valueTargets
                        : new();
                }
                Cleanup(body, 0);
                foreach (int target in resultTargets)
                {
                    Use(body, target);
                }
                UseObject(body);
                var resultGraph = FormalStorage(function.Type!, function.IsReference);
                List<int> roots = resultTargets;
                int resultAddress = -1;
                if (function.IsReference)
                {
                    resultAddress = resultTargets[0];
                    initializerValues.Add(resultAddress);
                    initializerDestinations.Add(-1);
                    roots = new();
                    for (int occurrence = 0; occurrence < resultGraph.Nodes[0].Slots.Count; occurrence += 1)
                    {
                        roots.Add(ReadReferentOccurrence(body, resultAddress, occurrence));
                    }
                }
                if (roots.Count != resultGraph.Nodes[0].Slots.Count)
                {
                    throw new InvalidOperationException("Initializer result has incomplete reference occurrences.");
                }
                List<List<int>> referents = new();
                List<int> values = new();
                List<int> destinations = new();
                foreach (var slot in resultGraph.Slots)
                {
                    referents.Add(resultGraph.Nodes[slot.Referent].Slots);
                    values.Add(FixedTarget((function, 0, -1)));
                    destinations.Add(FixedTarget((function, 0, -1)));
                }
                if (values.Count != 0)
                {
                    mappedActuals.Add(Event(body, 20, -1),
                        (resultGraph.Nodes[0].Slots, referents, roots, resultAddress, values, destinations));
                }
                initializerValues.AddRange(values);
                initializerDestinations.AddRange(destinations);
                if (!initializerResults.TryGetValue(function, out var resultSummary))
                {
                    resultSummary = new();
                    initializerResults.Add(function, resultSummary);
                    if (function.IsReference)
                    {
                        resultSummary.Add((new(), new(), false,
                            EmptyEdges(resultGraph.Nodes[0].Slots.Count), false));
                    }
                    foreach (var slot in resultGraph.Slots)
                    {
                        resultSummary.Add((new(), new(), false,
                            EmptyEdges(resultGraph.Nodes[slot.Referent].Slots.Count),
                            resultGraph.Nodes[slot.Container].Repeated));
                    }
                }
            }
            else
            {
                BuildBlock(body);
                Cleanup(body, 0);
                UseConstructedReceiver(body);
            }
            // This event is reachable only on normal fallthrough. Explicit returns
            // have their own exit event; break/continue are never summary exits.
            Event(body, 14, -1);

            // Keep the cause, not just an invalid bit: distinct formal roots may
            // overlap only at a caller. Acquisition kills this handle's history,
            // but does not revive any other alias to the previous allocation.
            HashSet<(int Borrow, (SemanticSymbol Root, int Depth, int Component) Cause)>?[] incoming =
                new HashSet<(int Borrow, (SemanticSymbol Root, int Depth, int Component) Cause)>?[graph.Count];
            incoming[entry] = new();
            HashSet<int>?[] invalidObjects = new HashSet<int>?[graph.Count];
            invalidObjects[entry] = new();
            // Possible occupancy is distinct from definite source initialization.
            // Empty holders have no final effects; joins preserve any live path.
            HashSet<int>?[] occupiedHolders = new HashSet<int>?[graph.Count];
            occupiedHolders[entry] = new();
            // Storage expiry includes scalars and descriptor slots, not just owned
            // payloads. Keep it separate from exported mutation causes.
            HashSet<int>?[] expiredStorage = new HashSet<int>?[graph.Count];
            expiredStorage[entry] = new();
            // Target identities are flow state, not lexical alias substitutions.
            // Each event owns its incoming sets; outgoing changes never mutate a
            // predecessor. Union at joins preserves every possible branch target.
            Dictionary<int, HashSet<(SemanticSymbol Root, int Depth, int Component)>>?[] targets =
                new Dictionary<int, HashSet<(SemanticSymbol Root, int Depth, int Component)>>?[graph.Count];
            targets[entry] = new();
            Dictionary<int, List<HashSet<int>>>?[] referentSlots =
                new Dictionary<int, List<HashSet<int>>>?[graph.Count];
            referentSlots[entry] = new();
            Queue<int> pending = new();
            pending.Enqueue(entry);
            while (pending.Count != 0)
            {
                int index = pending.Dequeue();
                var step = graph[index];
                var state = new HashSet<(int Borrow, (SemanticSymbol Root, int Depth, int Component) Cause)>(incoming[index]!);
                HashSet<int> objectState = new(invalidObjects[index]!);
                HashSet<int> holderState = new(occupiedHolders[index]!);
                HashSet<int> expiredState = new(expiredStorage[index]!);
                var targetState = CopyTargets(targets[index]!);
                var storageState = CopyStorage(referentSlots[index]!);
                void ReplaceTargets(
                    int destination,
                    List<int> sources,
                    List<int> outputCauses,
                    bool outputExpired)
                {
                    var replacement = new HashSet<(SemanticSymbol Root, int Depth, int Component)>();
                    var causes = new HashSet<(SemanticSymbol Root, int Depth, int Component)>();
                    bool expired = outputExpired;
                    List<HashSet<int>> storage = new();
                    foreach (int source in sources)
                    {
                        if (targetState.TryGetValue(source, out var sourceTargets))
                        {
                            replacement.UnionWith(sourceTargets);
                        }
                        else
                        {
                            replacement.Add(paths[source]);
                        }
                        foreach (var invalidation in state)
                        {
                            if (invalidation.Borrow == source)
                            {
                                causes.Add(invalidation.Cause);
                            }
                        }
                        expired |= expiredState.Contains(source);
                        if (storageState.TryGetValue(source, out var sourceStorage))
                        {
                            MergeStorage(storage, sourceStorage);
                        }
                    }
                    foreach (int cause in outputCauses)
                    {
                        if (targetState.TryGetValue(cause, out var causeTargets))
                        {
                            causes.UnionWith(causeTargets);
                        }
                        else
                        {
                            causes.Add(paths[cause]);
                        }
                    }
                    state.RemoveWhere(item => item.Borrow == destination);
                    expiredState.Remove(destination);
                    targetState[destination] = replacement;
                    storageState[destination] = storage;
                    foreach (var cause in causes)
                    {
                        state.Add((destination, cause));
                    }
                    if (expired)
                    {
                        expiredState.Add(destination);
                    }
                }
                void ApplyClassEffect(SemanticSymbol owner)
                {
                    // Use the scheduled release event, not a declaration-type
                    // guess: preparation locals can survive into the body, while
                    // nested scopes and overwritten holders release immediately.
                    if (diagnose && destructionWritesLinks is not null
                        && destructionWritesLinks(owner))
                    {
                        for (SyntaxNode? source = step.Node; source is not null; source = source.Parent)
                        {
                            if (source.Kind == SyntaxKind.Prepare)
                            {
                                Fail(step.Node, "LYC308", "Preparation cleanup may not mutate managed relationship links.");
                            }
                        }
                    }
                    // Ordinary field ownership can cascade into counted children.
                    // This is a conservative effect closure, not a traversal of
                    // runtime objects: cycles in the class graph terminate here.
                    Stack<SemanticSymbol> pending = new();
                    HashSet<SemanticSymbol> visited = new();
                    pending.Push(owner);
                    while (pending.Count != 0)
                    {
                        SemanticSymbol current = pending.Pop();
                        if (!visited.Add(current))
                        {
                            continue;
                        }
                        summaryChanged |= classEffects[function].Add(current);
                        ApplyMutation((current, classDestruction, -1));
                        // Generated destruction invokes these actual checked
                        // functions. Include their effects in the same fixed point
                        // as ordinary calls; hidden syntax grants no safety waiver.
                        foreach (SemanticSymbol hook in current.MemberScope!.Methods())
                        {
                            if (!hook.IsFinalHook() && !hook.IsDestructorFragment())
                            {
                                continue;
                            }
                            foreach (var effect in effects[hook])
                            {
                                if (effect.Depth == classGrowth || effect.Root.Kind == SymbolKind.Field)
                                {
                                    ApplyMutation(effect);
                                }
                            }
                            foreach (SemanticSymbol affected in classEffects[hook])
                            {
                                pending.Push(affected);
                            }
                        }
                        foreach (SemanticSymbol field in current.MemberScope!.Fields())
                        {
                            // Membership teardown, not each structural slot, owns
                            // the counted release. Its operation supplies the effect.
                            if (field.IsManagedHandle())
                            {
                                continue;
                            }
                            foreach (SemanticSymbol contained in ContainedClasses(field.Type!))
                            {
                                if (contained.IsCountedClass())
                                {
                                    pending.Push(contained);
                                }
                            }
                        }
                        for (int i = 0; i < objectClasses.Count; i += 1)
                        {
                            if (objectClasses[i] == current && !current.IsCountedClass()
                                && !teardownReceivers.Contains(i))
                            {
                                objectState.Add(i);
                            }
                        }
                    }
                }
                void ApplyMutation((SemanticSymbol Root, int Depth, int Component) mutation, bool export = true)
                {
                    bool classWide = mutation.Depth < 0;
                    if (export && (IsFormal(mutation) || mutation.Root.Kind == SymbolKind.Field
                        || mutation.Depth == classGrowth))
                    {
                        summaryChanged |= effects[function].Add(mutation);
                    }
                    for (int i = 0; i < paths.Count; i += 1)
                    {
                        if (!targetState.TryGetValue(i, out var possibleTargets))
                        {
                            continue;
                        }
                        foreach (var borrowed in possibleTargets)
                        {
                            bool affected;
                            if (classWide)
                            {
                                affected = IsFormal(borrowed)
                                    || (borrowed.Root.Kind == SymbolKind.Field
                                        && borrowed.Root.Scope!.Class == mutation.Root
                                        && (mutation.Depth == classDestruction || borrowed.Depth == 0));
                                // A plain field store saves an index, not a pointer.
                                if (indexedFieldDestinations.Contains(i))
                                {
                                    affected = false;
                                }
                            }
                            else
                            {
                                affected = (SameTarget(borrowed, mutation) && borrowed.Depth > mutation.Depth)
                                    || (!SameTarget(borrowed, mutation)
                                        && ((IsFormal(borrowed)
                                                && (IsFormal(mutation) || mutation.Root.Kind == SymbolKind.Field))
                                            || (IsFormal(mutation) && borrowed.Root.Kind == SymbolKind.Field)));
                            }
                            if (affected)
                            {
                                state.Add((i, mutation));
                            }
                        }
                    }
                }
                if (step.Operation == 8)
                {
                    holderState.Add(step.Borrow);
                }
                else if (step.Operation == 10)
                {
                    holderState.Remove(step.Borrow);
                }
                else if (step.Operation is 9 or 11)
                {
                    // A partial shrink can release leaves while retaining others.
                    bool occupied = step.Operation == 9
                        ? holderState.Remove(step.Borrow) : holderState.Contains(step.Borrow);
                    if (occupied)
                    {
                        foreach (SemanticSymbol owner in ContainedClasses(holderTypes[step.Borrow]))
                        {
                            if (!owner.IsCountedClass())
                            {
                                continue;
                            }
                            ApplyClassEffect(owner);
                            SemanticSymbol? hook = owner.FinalHook();
                            if (hook is not null)
                            {
                                foreach (SemanticSymbol affected in classEffects[hook])
                                {
                                    ApplyClassEffect(affected);
                                }
                            }
                        }
                    }
                }
                else if (step.Operation == 5)
                {
                    objectState.Remove(step.Borrow);
                }
                else if (step.Operation == 6)
                {
                    if (diagnose && objectState.Contains(step.Borrow))
                    {
                        Fail(step.Node, "LYC330", "Object reference may have been invalidated.");
                    }
                }
                else if (step.Operation == 7)
                {
                    ApplyClassEffect(destroyedClasses[index]);
                }
                else if (step.Operation == 1)
                {
                    state.RemoveWhere(item => item.Borrow == step.Borrow);
                    expiredState.Remove(step.Borrow);
                    targetState[step.Borrow] = new() { paths[step.Borrow] };
                    storageState.Remove(step.Borrow);
                }
                else if (step.Operation == 13)
                {
                    var copy = copiedTargets[index];
                    // Snapshot before replacement also makes self-copy well-defined.
                    var replacement = new HashSet<(SemanticSymbol Root, int Depth, int Component)>();
                    if (targetState.TryGetValue(copy.Source, out var sourceTargets))
                    {
                        foreach (var source in sourceTargets)
                        {
                            replacement.Add((source.Root, source.Depth + copy.DepthDelta, source.Component));
                        }
                    }
                    var causes = new List<(SemanticSymbol Root, int Depth, int Component)>();
                    foreach (var item in state)
                    {
                        if (item.Borrow == copy.Source)
                        {
                            causes.Add(item.Cause);
                        }
                    }
                    // Snapshot identities before replacement, including self-copy.
                    List<HashSet<int>> storage = new();
                    if (copy.DepthDelta == 0
                        && storageState.TryGetValue(copy.Source, out var sourceStorage))
                    {
                        foreach (var occurrence in sourceStorage)
                        {
                            storage.Add(new(occurrence));
                        }
                    }
                    storageState[step.Borrow] = storage;
                    bool expired = expiredState.Contains(copy.Source);
                    state.RemoveWhere(item => item.Borrow == step.Borrow);
                    expiredState.Remove(step.Borrow);
                    targetState[step.Borrow] = replacement;
                    foreach (var cause in causes)
                    {
                        state.Add((step.Borrow, cause));
                    }
                    if (expired)
                    {
                        expiredState.Add(step.Borrow);
                    }
                }
                else if (step.Operation == 15)
                {
                    // Resolve every destination before writing. Different formal
                    // parameters can designate the same live caller slot.
                    Dictionary<int, (List<int> Sources, List<int> Causes, bool Expired)> outputs = new();
                    foreach (var replacement in replacedTargets[index])
                    {
                        if (!storageState.TryGetValue(replacement.Address, out var slots)
                            || replacement.Occurrence >= slots.Count)
                        {
                            throw new InvalidOperationException(
                                "Call replacement has no matching live referent slot.");
                        }
                        var destinations = slots[replacement.Occurrence];
                        foreach (int destination in destinations)
                        {
                            if (!outputs.TryGetValue(destination, out var output))
                            {
                                output = (new(), new(), false);
                            }
                            output.Sources.AddRange(replacement.Sources);
                            output.Causes.AddRange(replacement.Causes);
                            output.Expired |= replacement.Expired;
                            if (destinations.Count != 1 || summarizedStorage.Contains(destination))
                            {
                                // This caller slot may not have been selected.
                                output.Sources.Add(destination);
                            }
                            outputs[destination] = output;
                        }
                    }
                    // Sources are independent snapshots captured before this
                    // event; only a slot's own untouched alternative remains live.
                    foreach (var output in outputs)
                    {
                        ReplaceTargets(output.Key, output.Value.Sources,
                            output.Value.Causes, output.Value.Expired);
                    }
                }
                else if (step.Operation == 21)
                {
                    var import = importedInitializers[index];
                    if (!initializerResults.TryGetValue(import.Field, out var summary)
                        || summary.Count != import.Slots.Count)
                    {
                        throw new InvalidOperationException("Returning initializer has no matching result graph.");
                    }
                    for (int position = 0; position < import.Slots.Count; position += 1)
                    {
                        int handle = import.Slots[position];
                        var result = summary[position];
                        targetState[handle] = new(result.Targets);
                        state.RemoveWhere(invalidation => invalidation.Borrow == handle);
                        foreach (var cause in result.Causes)
                        {
                            state.Add((handle, cause));
                        }
                        expiredState.Remove(handle);
                        if (result.Expired)
                        {
                            expiredState.Add(handle);
                        }
                        List<HashSet<int>> storage = new();
                        foreach (var edge in result.Referents)
                        {
                            HashSet<int> destinations = new();
                            foreach (int destination in edge)
                            {
                                if (destination < 0 || destination >= import.Slots.Count)
                                {
                                    throw new InvalidOperationException("Initializer result edge is outside its graph.");
                                }
                                destinations.Add(import.Slots[destination]);
                            }
                            storage.Add(destinations);
                        }
                        storageState[handle] = storage;
                        if (result.Repeated)
                        {
                            summarizedStorage.Add(handle);
                        }
                    }
                }
                else if (step.Operation == 20)
                {
                    var mapping = mappedActuals[index];
                    List<HashSet<int>> sources = new();
                    List<HashSet<int>> destinations = new();
                    for (int slot = 0; slot < mapping.Values.Count; slot += 1)
                    {
                        sources.Add(new());
                        destinations.Add(new());
                    }
                    Queue<(int Formal, int Actual)> reachable = new();
                    for (int occurrence = 0; occurrence < mapping.Roots.Count; occurrence += 1)
                    {
                        int slot = mapping.Roots[occurrence];
                        reachable.Enqueue((slot, mapping.Inputs[occurrence]));
                        if (mapping.Address >= 0)
                        {
                            if (!storageState.TryGetValue(mapping.Address, out var rootSlots)
                                || rootSlots.Count != mapping.Roots.Count)
                            {
                                throw new InvalidOperationException("Call root has no matching live storage slots.");
                            }
                            destinations[slot].UnionWith(rootSlots[occurrence]);
                        }
                    }
                    // A recursive formal graph and a cyclic actual graph are both
                    // finite. Visit their product, not arbitrarily long paths.
                    HashSet<(int Formal, int Actual)> visited = new();
                    while (reachable.Count != 0)
                    {
                        var pair = reachable.Dequeue();
                        if (!visited.Add(pair))
                        {
                            continue;
                        }
                        sources[pair.Formal].Add(pair.Actual);
                        var children = mapping.Referents[pair.Formal];
                        if (children.Count == 0)
                        {
                            continue;
                        }
                        if (!storageState.TryGetValue(pair.Actual, out var actualSlots)
                            || actualSlots.Count != children.Count)
                        {
                            throw new InvalidOperationException("Nested call actual has no matching referent slots.");
                        }
                        for (int occurrence = 0; occurrence < children.Count; occurrence += 1)
                        {
                            int child = children[occurrence];
                            foreach (int actual in actualSlots[occurrence])
                            {
                                destinations[child].Add(actual);
                                reachable.Enqueue((child, actual));
                            }
                        }
                    }
                    // These destinations are new bookkeeping handles, not caller
                    // slots. Publish only after traversal; no live storage is written.
                    for (int slot = 0; slot < mapping.Values.Count; slot += 1)
                    {
                        if (sources[slot].Count == 0)
                        {
                            throw new InvalidOperationException("A formal call slot has no actual source.");
                        }
                        ReplaceTargets(mapping.Values[slot], new(sources[slot]), new(), false);
                        storageState[mapping.Destinations[slot]] = new() { new(destinations[slot]) };
                    }
                }
                else if (step.Operation == 17)
                {
                    var read = dereferencedStorage[index];
                    if (!storageState.TryGetValue(read.Address, out var slots)
                        || read.Occurrence >= slots.Count)
                    {
                        throw new InvalidOperationException(
                            "Contained dereference has no live referent slot.");
                    }
                    List<int> sources = new(slots[read.Occurrence]);
                    ReplaceTargets(step.Borrow, sources, new(), false);
                }
                else if (step.Operation == 16)
                {
                    List<HashSet<int>> storage = new();
                    foreach (int slot in attachedStorage[index])
                    {
                        storage.Add(new() { slot });
                    }
                    storageState[step.Borrow] = storage;
                }
                else if (step.Operation == 18)
                {
                    var projection = projectedStorage[index];
                    List<HashSet<int>> storage = new();
                    if (projection.Count != 0)
                    {
                        if (!storageState.TryGetValue(projection.Address, out var slots)
                            || projection.Offset + projection.Count > slots.Count)
                        {
                            throw new InvalidOperationException(
                                "Inline storage projection exceeds live referent slots.");
                        }
                        for (int occurrence = 0; occurrence < projection.Count; occurrence += 1)
                        {
                            storage.Add(new(slots[projection.Offset + occurrence]));
                        }
                    }
                    storageState[step.Borrow] = storage;
                }
                else if (step.Operation == 19)
                {
                    var store = storedReferents[index];
                    if (!storageState.TryGetValue(store.Address, out var slots)
                        || slots.Count != store.Count)
                    {
                        throw new InvalidOperationException(
                            "Contained assignment has no matching live referent slots.");
                    }
                    // Copy the destination set before writing any of its members.
                    // A may-select address cannot prove that every candidate was
                    // overwritten: retain each candidate's untouched alternative.
                    List<int> destinations = new(slots[store.Occurrence]);
                    foreach (int destination in destinations)
                    {
                        List<int> sources = new() { store.Source };
                        if (destinations.Count != 1 || summarizedStorage.Contains(destination))
                        {
                            sources.Add(destination);
                        }
                        ReplaceTargets(destination, sources, new(), false);
                    }
                }
                else if (step.Operation == 12)
                {
                    SemanticSymbol local = endedStorage[index];
                    for (int borrow = 0; borrow < paths.Count; borrow += 1)
                    {
                        if (targetState.TryGetValue(borrow, out var possibleTargets))
                        {
                            foreach (var target in possibleTargets)
                            {
                                if (target.Root == local && target.Component == -1)
                                {
                                    expiredState.Add(borrow);
                                }
                            }
                        }
                    }
                }
                else if (step.Operation == 2)
                {
                    if (diagnose && expiredState.Contains(step.Borrow))
                    {
                        Fail(step.Node, "LYC326", "Borrowed local storage has ended its lifetime.");
                    }
                    foreach (var item in state)
                    {
                        if (item.Borrow == step.Borrow)
                        {
                            foreach (var target in targetState[step.Borrow])
                            {
                                RequireSafe(step.Node, item.Cause, target);
                            }
                        }
                    }
                }
                else if (step.Operation == 3)
                {
                    // A constructor's direct self store changes newly allocated
                    // storage, not an existing caller object's payload. It still
                    // invalidates local borrows acquired earlier in this body.
                    bool freshSelfStore = step.Node.Kind == SyntaxKind.Member
                        && function.Scope?.Class is SemanticSymbol constructingClass
                        && function.Name == constructingClass.Name
                        && step.Node.ChildAt(0).Fact?.Symbol == function.FunctionScope!.ParameterAt(0);
                    if (targetState.TryGetValue(step.Borrow, out var mutatedTargets))
                    {
                        foreach (var target in mutatedTargets)
                        {
                            ApplyMutation(target, !freshSelfStore);
                        }
                    }
                    else
                    {
                        // Summary effects and constructor growth are fixed effect
                        // descriptors, not acquired addresses. They still execute
                        // even though they have no flow-sensitive target holder.
                        ApplyMutation(paths[step.Borrow], !freshSelfStore);
                    }
                }
                else if (step.Operation == 4)
                {
                    var requirement = callRequirements[index];
                    var changedTargets = targetState.TryGetValue(requirement.Changed, out var changedActuals)
                        ? changedActuals
                        : new HashSet<(SemanticSymbol Root, int Depth, int Component)> { paths[requirement.Changed] };
                    var usedTargets = targetState.TryGetValue(requirement.Used, out var usedActuals)
                        ? usedActuals
                        : new HashSet<(SemanticSymbol Root, int Depth, int Component)> { paths[requirement.Used] };
                    foreach (var changedTarget in changedTargets)
                    {
                        foreach (var usedTarget in usedTargets)
                        {
                            RequireSafe(step.Node, changedTarget, usedTarget);
                        }
                    }
                }
                if (step.Operation == 14)
                {
                    summaryChanged |= returningFunctions.Add(function);
                    if (function.InitializerScope is not null)
                    {
                        var resultSummary = initializerResults[function];
                        for (int position = 0; position < initializerValues.Count; position += 1)
                        {
                            int handle = initializerValues[position];
                            var result = resultSummary[position];
                            if (targetState.TryGetValue(handle, out var resultTargets))
                            {
                                foreach (var target in resultTargets)
                                {
                                    summaryChanged |= result.Targets.Add(target);
                                }
                            }
                            foreach (var invalidation in state)
                            {
                                if (invalidation.Borrow == handle)
                                {
                                    summaryChanged |= result.Causes.Add(invalidation.Cause);
                                }
                            }
                            if (expiredState.Contains(handle) && !result.Expired)
                            {
                                result.Expired = true;
                                summaryChanged = true;
                            }
                            int destination = initializerDestinations[position];
                            if (destination >= 0
                                && storageState.TryGetValue(destination, out var destinationSlots)
                                && destinationSlots[0].Count > 1
                                && !result.Repeated)
                            {
                                result.Repeated = true;
                                summaryChanged = true;
                            }
                            if (result.Referents.Count != 0)
                            {
                                if (!storageState.TryGetValue(handle, out var children)
                                    || children.Count != result.Referents.Count)
                                {
                                    throw new InvalidOperationException("Initializer result lost nested storage.");
                                }
                                for (int occurrence = 0; occurrence < children.Count; occurrence += 1)
                                {
                                    foreach (int child in children[occurrence])
                                    {
                                        bool mapped = false;
                                        for (int candidate = 0; candidate < initializerDestinations.Count; candidate += 1)
                                        {
                                            int candidateHandle = initializerDestinations[candidate];
                                            if (candidateHandle >= 0
                                                && storageState.TryGetValue(candidateHandle, out var candidateSlots)
                                                && candidateSlots[0].Contains(child))
                                            {
                                                mapped = true;
                                                summaryChanged |= result.Referents[occurrence].Add(candidate);
                                            }
                                        }
                                        if (!mapped)
                                        {
                                            throw new InvalidOperationException("Initializer result edge has no summary position.");
                                        }
                                    }
                                }
                            }
                            resultSummary[position] = result;
                        }
                    }
                    // Root copies stay private; nested referents are caller-visible
                    // for both value and reference parameters. Capture after cleanup.
                    foreach (var binding in externalFormalSlots)
                    {
                        var key = binding.Key;
                        int slot = binding.Value;
                        if (!replacements[function].TryGetValue(key, out var result))
                        {
                            result = (new(), new(), false);
                            replacements[function].Add(key, result);
                            summaryChanged = true;
                        }
                        if (targetState.TryGetValue(slot, out var exitTargets))
                        {
                            foreach (var target in exitTargets)
                            {
                                summaryChanged |= result.Targets.Add(target);
                            }
                        }
                        foreach (var invalidation in state)
                        {
                            if (invalidation.Borrow == slot)
                            {
                                summaryChanged |= result.Causes.Add(invalidation.Cause);
                            }
                        }
                        if (expiredState.Contains(slot) && !result.Expired)
                        {
                            result.Expired = true;
                            summaryChanged = true;
                        }
                        replacements[function][key] = result;
                    }
                }
                foreach (int next in step.Next)
                {
                    if (incoming[next] is null)
                    {
                        incoming[next] = new(state);
                        invalidObjects[next] = new(objectState);
                        occupiedHolders[next] = new(holderState);
                        expiredStorage[next] = new(expiredState);
                        targets[next] = CopyTargets(targetState);
                        referentSlots[next] = CopyStorage(storageState);
                        pending.Enqueue(next);
                    }
                    else
                    {
                        int before = incoming[next]!.Count + invalidObjects[next]!.Count
                            + occupiedHolders[next]!.Count + expiredStorage[next]!.Count;
                        incoming[next]!.UnionWith(state);
                        invalidObjects[next]!.UnionWith(objectState);
                        occupiedHolders[next]!.UnionWith(holderState);
                        expiredStorage[next]!.UnionWith(expiredState);
                        bool targetsChanged = false;
                        foreach (var pair in targetState)
                        {
                            if (!targets[next]!.TryGetValue(pair.Key, out var existingTargets))
                            {
                                targets[next]!.Add(pair.Key, new(pair.Value));
                                targetsChanged = true;
                            }
                            else
                            {
                                int targetCount = existingTargets.Count;
                                existingTargets.UnionWith(pair.Value);
                                targetsChanged |= existingTargets.Count != targetCount;
                            }
                        }
                        foreach (var pair in storageState)
                        {
                            if (!referentSlots[next]!.TryGetValue(pair.Key, out var slots))
                            {
                                slots = new();
                                referentSlots[next]!.Add(pair.Key, slots);
                                targetsChanged = true;
                            }
                            targetsChanged |= MergeStorage(slots, pair.Value);
                        }
                        if (targetsChanged || incoming[next]!.Count + invalidObjects[next]!.Count
                            + occupiedHolders[next]!.Count + expiredStorage[next]!.Count != before)
                        {
                            pending.Enqueue(next);
                        }
                    }
                }
            }
            return summaryChanged;

            Dictionary<int, HashSet<(SemanticSymbol Root, int Depth, int Component)>> CopyTargets(
                Dictionary<int, HashSet<(SemanticSymbol Root, int Depth, int Component)>> source)
            {
                var copy = new Dictionary<int, HashSet<(SemanticSymbol Root, int Depth, int Component)>>();
                foreach (var pair in source)
                {
                    copy.Add(pair.Key, new(pair.Value));
                }
                return copy;
            }
            Dictionary<int, List<HashSet<int>>> CopyStorage(
                Dictionary<int, List<HashSet<int>>> source)
            {
                Dictionary<int, List<HashSet<int>>> copy = new();
                foreach (var pair in source)
                {
                    List<HashSet<int>> occurrences = new();
                    foreach (var occurrence in pair.Value)
                    {
                        occurrences.Add(new(occurrence));
                    }
                    copy.Add(pair.Key, occurrences);
                }
                return copy;
            }
            bool MergeStorage(List<HashSet<int>> destination, List<HashSet<int>> source)
            {
                if (source.Count == 0)
                {
                    return false;
                }
                if (destination.Count == 0)
                {
                    foreach (var occurrence in source)
                    {
                        destination.Add(new(occurrence));
                    }
                    return true;
                }
                if (destination.Count != source.Count)
                {
                    throw new InvalidOperationException("Referent storage occurrence count changed at a flow join.");
                }
                bool changed = false;
                for (int i = 0; i < source.Count; i += 1)
                {
                    int before = destination[i].Count;
                    destination[i].UnionWith(source[i]);
                    changed |= destination[i].Count != before;
                }
                return changed;
            }
            bool IsFormal((SemanticSymbol Root, int Depth, int Component) path)
            {
                return path.Root.Kind == SymbolKind.Parameter
                    && (path.Root.IsReference || path.Component >= 0)
                    && path.Root.Scope == function.FunctionScope;
            }
            bool SameTarget((SemanticSymbol Root, int Depth, int Component) first,
                (SemanticSymbol Root, int Depth, int Component) second)
            {
                return first.Root == second.Root && first.Component == second.Component;
            }
            void RequireSafe(SyntaxNode node, (SemanticSymbol Root, int Depth, int Component) mutation,
                (SemanticSymbol Root, int Depth, int Component) used)
            {
                if (mutation.Depth < 0)
                {
                    if (IsFormal(used))
                    {
                        summaryChanged |= obligations[function].Add((mutation, used));
                    }
                    else if (diagnose && used.Root.Kind == SymbolKind.Field
                        && used.Root.Scope!.Class == mutation.Root
                        && (mutation.Depth == classDestruction || used.Depth == 0))
                    {
                        Fail(node, "LYC326", "Borrowed storage may have been invalidated.");
                    }
                }
                else if (SameTarget(mutation, used) && used.Depth > mutation.Depth)
                {
                    if (diagnose)
                    {
                        Fail(node, "LYC326", "Borrowed storage may have been invalidated.");
                    }
                }
                else if (!SameTarget(mutation, used)
                    && ((IsFormal(mutation)
                            && (IsFormal(used) || used.Root.Kind == SymbolKind.Field))
                        || (IsFormal(used) && mutation.Root.Kind == SymbolKind.Field)))
                {
                    summaryChanged |= obligations[function].Add((mutation, used));
                }
            }

            void UseConstructedReceiver(SyntaxNode node)
            {
                // A constructor's unit body still returns an object implicitly.
                // It must not hand its caller a receiver invalidated by the body.
                if (function.Scope?.Class is SemanticSymbol owner && function.Name == owner.Name)
                {
                    SemanticSymbol receiver = function.FunctionScope!.ParameterAt(0);
                    ObjectEvents(node, 6, objectBindings[receiver]);
                }
            }
            int NewObject(SyntaxNode node, SemanticSymbol owner)
            {
                int handle = objectClasses.Count;
                objectClasses.Add(owner);
                // Protect a fresh direct observation of the exact receiver, not
                // every object reached through it or a saved child of equal type.
                if (teardownReceiver is not null && node.Kind == SyntaxKind.Name
                    && node.Fact?.Symbol == teardownReceiver)
                {
                    teardownReceivers.Add(handle);
                }
                Event(node, 5, handle);
                return handle;
            }
            List<int> NewObjects(SyntaxNode node, SemanticType type)
            {
                List<int> handles = new();
                foreach (SemanticSymbol owner in ContainedClasses(type))
                {
                    handles.Add(NewObject(node, owner));
                }
                return handles;
            }
            void ObjectEvents(SyntaxNode node, int operation, List<int> handles)
            {
                foreach (int handle in handles)
                {
                    Event(node, operation, handle);
                }
            }
            void UseObject(SyntaxNode node)
            {
                if (objectValues.TryGetValue(node, out List<int>? handles))
                {
                    ObjectEvents(node, 6, handles);
                }
            }
            IEnumerable<SemanticSymbol> ContainedClasses(SemanticType type)
            {
                // These are possible lifetime effects, not runtime destruction
                // steps. Repeated components of one class contribute one effect.
                // Follow canonical structure rather than names or flattened keys.
                Stack<SemanticType> pendingTypes = new();
                HashSet<SemanticType> visitedTypes = new();
                HashSet<SemanticSymbol> visitedClasses = new();
                pendingTypes.Push(type);
                while (pendingTypes.Count != 0)
                {
                    SemanticType current = pendingTypes.Pop();
                    if (!visitedTypes.Add(current))
                    {
                        continue;
                    }
                    if (current.Kind == SemanticTypeKind.Array)
                    {
                        pendingTypes.Push(SemanticTypes.ElementOf(current));
                    }
                    else if (current.Kind == SemanticTypeKind.Tuple)
                    {
                        for (int i = current.ComponentCount() - 1; i >= 0; i -= 1)
                        {
                            pendingTypes.Push(current.ComponentAt(i));
                        }
                    }
                    else if (current.Declaration?.Kind == SymbolKind.Struct)
                    {
                        // Inline values preserve the lifetime obligations of all
                        // contained handles. The visited set also bounds recursion
                        // through array descriptors back to this nominal type.
                        foreach (SemanticSymbol field in current.Declaration.MemberScope!.Fields())
                        {
                            if (!field.IsReference)
                            {
                                pendingTypes.Push(field.Type!);
                            }
                        }
                    }
                    else if (current.Class is SemanticSymbol owner && visitedClasses.Add(owner))
                    {
                        yield return owner;
                    }
                }
            }
            bool HasCountedContents(SemanticType type)
            {
                foreach (SemanticSymbol owner in ContainedClasses(type))
                {
                    if (owner.IsCountedClass())
                    {
                        return true;
                    }
                }
                return false;
            }
            void Release(SyntaxNode node, SemanticType type)
            {
                // A release may reach zero. Record every contained counted
                // class's finalization without invalidating retained siblings.
                foreach (SemanticSymbol owner in ContainedClasses(type))
                {
                    if (!owner.IsCountedClass())
                    {
                        continue;
                    }
                    DestroyClass(node, owner);
                    SemanticSymbol? hook = owner.FinalHook();
                    if (hook is not null)
                    {
                        foreach (SemanticSymbol affected in classEffects[hook])
                        {
                            DestroyClass(node, affected);
                        }
                    }
                }
            }
            int RegisterHolder(SyntaxNode node, SemanticType type, bool initialized)
            {
                if (!HasCountedContents(type))
                {
                    return -1;
                }
                int holder = holderTypes.Count;
                holderTypes.Add(type);
                cleanupFrames[^1].Holders.Add(holder);
                // A loop executes the declaration again with fresh empty storage.
                Event(node, initialized ? 8 : 10, holder);
                return holder;
            }
            bool KnownEmptyArray(SyntaxNode node)
            {
                // Only literal construction proves that no counted leaf exists.
                // Unknown copies/calls remain conservative; later mutations can
                // populate a known-empty owner through its ordinary root path.
                Stack<SyntaxNode> pending = new();
                pending.Push(node);
                while (pending.Count != 0)
                {
                    SyntaxNode next = pending.Pop();
                    if (next.Kind == SyntaxKind.Group)
                    {
                        pending.Push(next.ChildAt(0));
                    }
                    else if (next.Kind == SyntaxKind.Array)
                    {
                        for (int i = 0; i < next.ChildCount(); i += 1)
                        {
                            pending.Push(next.ChildAt(i));
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            void MoveAggregate(SyntaxNode node)
            {
                while (node.Kind == SyntaxKind.Group)
                {
                    node = node.ChildAt(0);
                }
                if (aggregateTemporaries.TryGetValue(node, out int holder))
                {
                    Event(node, 10, holder);
                }
            }
            void Cleanup(SyntaxNode node, int firstFrame)
            {
                for (int frame = cleanupFrames.Count - 1; frame >= firstFrame; frame -= 1)
                {
                    List<int> holders = cleanupFrames[frame].Holders;
                    for (int holder = holders.Count - 1; holder >= 0; holder -= 1)
                    {
                        Event(node, 9, holders[holder]);
                    }
                    // Finalizer effects run while this frame's local storage
                    // still exists; only then do its addresses cease to be valid.
                    foreach (SemanticSymbol local in cleanupFrames[frame].Storage)
                    {
                        int end = Event(node, 12, -1);
                        endedStorage.Add(end, local);
                    }
                }
            }
            void DestroyClass(SyntaxNode node, SemanticSymbol owner)
            {
                int effect = Event(node, 7, -1);
                destroyedClasses.Add(effect, owner);
            }
            int Event(SyntaxNode node, int operation, int borrow)
            {
                int index = graph.Count;
                graph.Add((node, operation, borrow, new()));
                foreach (int previous in frontier)
                {
                    graph[previous].Next.Add(index);
                }
                frontier = new() { index };
                return index;
            }
            IEnumerable<SemanticSymbol> ReferenceOccurrences(SemanticType type)
            {
                // Layout validation has already excluded inline cycles. Do not
                // deduplicate canonical types: repeated inline positions are
                // distinct slots. Arrays and class handles end this traversal;
                // escape classification separately rejects borrowed array elements.
                Stack<(SemanticType Type, SemanticSymbol? Reference)> pending = new();
                pending.Push((type, null));
                while (pending.Count != 0)
                {
                    var current = pending.Pop();
                    if (current.Reference is SemanticSymbol reference)
                    {
                        yield return reference;
                        continue;
                    }
                    if (current.Type.Kind == SemanticTypeKind.Tuple)
                    {
                        for (int index = current.Type.ComponentCount() - 1; index >= 0; index -= 1)
                        {
                            pending.Push((current.Type.ComponentAt(index), null));
                        }
                    }
                    else if (current.Type.Kind == SemanticTypeKind.Nominal
                        && current.Type.Declaration?.Kind == SymbolKind.Struct)
                    {
                        SemanticScope scope = current.Type.Declaration.MemberScope
                            ?? throw new InvalidOperationException("Struct member scope is incomplete.");
                        List<SemanticSymbol> fields = new(scope.Fields());
                        for (int index = fields.Count - 1; index >= 0; index -= 1)
                        {
                            SemanticSymbol field = fields[index];
                            SemanticType fieldType = field.Type
                                ?? throw new InvalidOperationException("Struct field type is incomplete.");
                            pending.Push((fieldType, field.IsReference ? field : null));
                        }
                    }
                }
            }
            List<HashSet<int>> EmptyEdges(int count)
            {
                List<HashSet<int>> edges = new();
                for (int occurrence = 0; occurrence < count; occurrence += 1)
                {
                    edges.Add(new());
                }
                return edges;
            }
            (List<(SemanticType Type, int Parent, List<int> Slots, bool Repeated)> Nodes,
                List<(SemanticSymbol Field, int Container, int Referent)> Slots)
                FormalStorage(SemanticType type, bool rootIsReferent)
            {
                // This is a finite symbolic storage graph, not a layout expansion.
                // Inline reference occurrences remain ordered and distinct. A type
                // reached again on the SAME ancestry path folds to that ancestor;
                // equal types in sibling branches never merge merely by identity.
                List<(SemanticType Type, int Parent, List<int> Slots, bool Repeated)> nodes = new();
                List<(SemanticSymbol Field, int Container, int Referent)> slots = new();
                AddNode(type, -1);
                for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
                {
                    foreach (int slotIndex in nodes[nodeIndex].Slots)
                    {
                        var slot = slots[slotIndex];
                        SemanticType referentType = slot.Field.Type
                            ?? throw new InvalidOperationException("Reference field type is incomplete.");
                        int referent = nodeIndex;
                        // A value parameter's root is a private copy, never the
                        // external storage reached through one of its references.
                        // Keep it distinct even when the referent type matches.
                        while (referent >= 0
                            && (nodes[referent].Type != referentType
                                || (referent == 0 && !rootIsReferent)))
                        {
                            referent = nodes[referent].Parent;
                        }
                        if (referent < 0)
                        {
                            referent = AddNode(referentType, nodeIndex);
                        }
                        else
                        {
                            var repeated = nodes[referent];
                            repeated.Repeated = true;
                            nodes[referent] = repeated;
                        }
                        slots[slotIndex] = (slot.Field, slot.Container, referent);
                    }
                }
                // Descendants of a folded node can also stand for several actual
                // locations, including branches that are not themselves cyclic.
                Queue<int> pending = new();
                for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
                {
                    if (nodes[nodeIndex].Repeated)
                    {
                        pending.Enqueue(nodeIndex);
                    }
                }
                while (pending.Count != 0)
                {
                    int nodeIndex = pending.Dequeue();
                    foreach (int slotIndex in nodes[nodeIndex].Slots)
                    {
                        int referent = slots[slotIndex].Referent;
                        var reached = nodes[referent];
                        if (!reached.Repeated)
                        {
                            reached.Repeated = true;
                            nodes[referent] = reached;
                            pending.Enqueue(referent);
                        }
                    }
                }
                return (nodes, slots);

                int AddNode(SemanticType nodeType, int parent)
                {
                    int index = nodes.Count;
                    List<int> occurrences = new();
                    nodes.Add((nodeType, parent, occurrences, false));
                    foreach (SemanticSymbol field in ReferenceOccurrences(nodeType))
                    {
                        occurrences.Add(slots.Count);
                        slots.Add((field, index, -1));
                    }
                    return index;
                }
            }
            int ReferenceCount(SemanticType type)
            {
                int count = 0;
                foreach (SemanticSymbol field in ReferenceOccurrences(type))
                {
                    count += 1;
                }
                return count;
            }
            (int Offset, int Count, bool IsReference) ContainedProjection(SyntaxNode member)
            {
                SemanticType receiverType = member.ChildAt(0).Fact!.Type;
                int offset = 0;
                if (receiverType.Kind == SemanticTypeKind.Tuple)
                {
                    int index = int.Parse(Text(member), System.Globalization.CultureInfo.InvariantCulture);
                    for (int preceding = 0; preceding < index; preceding += 1)
                    {
                        offset += ReferenceCount(receiverType.ComponentAt(preceding));
                    }
                    return (offset, ReferenceCount(receiverType.ComponentAt(index)), false);
                }
                SemanticScope scope = receiverType.Declaration!.MemberScope!;
                foreach (SemanticSymbol field in scope.Fields())
                {
                    int count = field.IsReference ? 1 : ReferenceCount(field.Type!);
                    if (field == member.Fact!.Symbol)
                    {
                        return (offset, count, field.IsReference);
                    }
                    offset += count;
                }
                throw new InvalidOperationException("Selected struct field is absent from its declared scope.");
            }
            int FixedTarget((SemanticSymbol Root, int Depth, int Component) path)
            {
                // A summary descriptor is not an acquired address. Its fixed
                // identity remains meaningful without a worklist acquisition.
                int handle = paths.Count;
                paths.Add(path);
                return handle;
            }
            int Acquire(SyntaxNode node, (SemanticSymbol Root, int Depth, int Component) path)
            {
                int handle = paths.Count;
                paths.Add(path);
                Event(node, 1, handle);
                return handle;
            }
            int AcquireDerived(SyntaxNode node, int parent, int depth)
            {
                int handle = paths.Count;
                paths.Add((paths[parent].Root, depth, paths[parent].Component));
                int copied = Event(node, 13, handle);
                copiedTargets.Add(copied, (parent, depth - paths[parent].Depth));
                if (partialHolders.Contains(parent))
                {
                    partialHolders.Add(handle);
                }
                if (selectedObjects.TryGetValue(parent, out List<int>? objects))
                {
                    selectedObjects.Add(handle, objects);
                }
                return handle;
            }
            List<int> SnapshotContained(SyntaxNode node, List<int> sources)
            {
                List<int> snapshot = new();
                foreach (int source in sources)
                {
                    snapshot.Add(AcquireDerived(node, source, paths[source].Depth));
                }
                return snapshot;
            }
            int ReadReferentOccurrence(SyntaxNode node, int address, int occurrence)
            {
                // This path is bookkeeping only: event 17 supplies the actual
                // targets and history without acquiring a fresh valid borrow.
                int handle = FixedTarget(paths[address]);
                int read = Event(node, 17, handle);
                dereferencedStorage.Add(read, (address, occurrence));
                return handle;
            }
            List<int> SnapshotReferent(SyntaxNode node, int address)
            {
                List<int> snapshot = new();
                int count = ReferenceCount(node.Fact!.Type);
                for (int occurrence = 0; occurrence < count; occurrence += 1)
                {
                    snapshot.Add(ReadReferentOccurrence(node, address, occurrence));
                }
                return snapshot;
            }
            void StoreContained(SyntaxNode node, List<int> destinations, List<int> sources)
            {
                if (destinations.Count != sources.Count)
                {
                    throw new InvalidOperationException("Contained reference occurrence count changed during assignment.");
                }
                for (int i = 0; i < destinations.Count; i += 1)
                {
                    // Replace this slot on the current graph path. The worklist
                    // joins alternatives; lexical traversal must not choose one.
                    int copied = Event(node, 13, destinations[i]);
                    copiedTargets.Add(copied, (sources[i], 0));
                }
            }
            void Use(SyntaxNode node, int handle)
            {
                Event(node, 2, handle);
                SemanticSymbol root = paths[handle].Root;
                // Selecting the local descriptor for whole-value replacement
                // does not read its old object elements. Deeper selections do.
                if (paths[handle].Depth > 0
                    && root.Type is SemanticType rootType && rootType.ArrayDepth > 0
                    && objectBindings.TryGetValue(root, out List<int>? arrayObjects))
                {
                    ObjectEvents(node, 6, arrayObjects);
                }
                if (selectedObjects.TryGetValue(handle, out List<int>? selected))
                {
                    ObjectEvents(node, 6, selected);
                }
            }
            // Select each containing descriptor before evaluating the next
            // index. Later index/argument effects must not invalidate it.
            int Select(SyntaxNode node)
            {
                if (node.Kind == SyntaxKind.Group)
                {
                    return Select(node.ChildAt(0));
                }
                if (node.Kind == SyntaxKind.Name)
                {
                    SemanticSymbol symbol = node.Fact!.Symbol!;
                    if (aliases.TryGetValue(symbol, out int alias))
                    {
                        Use(node, alias);
                        return AcquireDerived(node, alias, paths[alias].Depth);
                    }
                    int storage = Acquire(node, (symbol, 0, -1));
                    if (containedBindings.TryGetValue(symbol, out List<int>? storageTargets))
                    {
                        attachedStorage.Add(Event(node, 16, storage), storageTargets);
                    }
                    return storage;
                }
                if (node.Kind == SyntaxKind.Member)
                {
                    if (node.ChildAt(0).Fact!.Type.Kind == SemanticTypeKind.Tuple
                        || node.ChildAt(0).Fact!.Type.Declaration?.Kind == SymbolKind.Struct)
                    {
                        // Inline selection crosses no backing allocation. Keep
                        // the storage root/depth and its last-use obligations.
                        int tupleParent = Select(node.ChildAt(0));
                        Use(node, tupleParent);
                        var projection = ContainedProjection(node);
                        if (projection.IsReference)
                        {
                            // The field holds an address elsewhere. Selecting it
                            // does not select the pointer slot inside this holder.
                            int target = ReadReferentOccurrence(node, tupleParent, projection.Offset);
                            Use(node, target);
                            return target;
                        }
                        int selectedComponent = AcquireDerived(node, tupleParent, paths[tupleParent].Depth);
                        projectedStorage.Add(Event(node, 18, selectedComponent),
                            (tupleParent, projection.Offset, projection.Count));
                        partialHolders.Add(selectedComponent);
                        if (objectBindings.TryGetValue(paths[tupleParent].Root, out List<int>? tupleObjects))
                        {
                            // Storage selection does not evaluate a copied tuple.
                            // Carry its saved identities so a later effect cannot
                            // revive a handle by selecting the component again.
                            selectedObjects[selectedComponent] = tupleObjects;
                            ObjectEvents(node, 6, tupleObjects);
                        }
                        return selectedComponent;
                    }
                    // Direct field assignment currently keeps no escaping borrow.
                    // A field is the root of any owned array backing, distinct
                    // from the local variable holding the object's index.
                    SyntaxNode receiver = node.ChildAt(0);
                    BuildExpression(receiver);
                    UseObject(receiver);
                    RegisterComputedReceiver(receiver);
                    int selected = Acquire(node, (node.Fact!.Symbol!, 0, -1));
                    selectedObjects.Add(selected, objectValues[receiver]);
                    return selected;
                }
                int parent = Select(node.ChildAt(0));
                BuildExpression(node.ChildAt(1));
                Use(node, parent);
                var path = paths[parent];
                return AcquireDerived(node, parent, path.Depth + 1);
            }
            // Descriptor observation borrows selected storage instead of making
            // the independent array copies used by ordinary value expressions.
            // A computed array's owned temporary has no external storage root.
            int ObserveArray(SyntaxNode node)
            {
                if (node.Kind == SyntaxKind.Group)
                {
                    return ObserveArray(node.ChildAt(0));
                }
                if (node.Kind is SyntaxKind.Name or SyntaxKind.Member)
                {
                    return Select(node);
                }
                if (node.Kind == SyntaxKind.Index)
                {
                    int parent = ObserveArray(node.ChildAt(0));
                    BuildExpression(node.ChildAt(1));
                    if (parent >= 0)
                    {
                        Use(node, parent);
                        var path = paths[parent];
                        return AcquireDerived(node, parent, path.Depth + 1);
                    }
                    return -1;
                }
                BuildExpression(node);
                return -1;
            }
            void RegisterComputedReceiver(SyntaxNode receiver)
            {
                while (receiver.Kind == SyntaxKind.Group
                    || receiver.Fact?.Symbol?.Kind == SymbolKind.MemberNamespace)
                {
                    receiver = receiver.ChildAt(0);
                }
                // Both field reads and field destinations borrow named storage.
                // A computed receiver instead owns a temporary through the scope.
                if (receiver.Kind != SyntaxKind.Name
                    && receiver.Fact!.Type.Class is SemanticSymbol owner
                    && owner.IsCountedClass())
                {
                    RegisterHolder(receiver, receiver.Fact.Type, true);
                }
            }
            void BuildExpression(SyntaxNode expression)
            {
                // Ordinary expression chains can be long. Use work items for
                // their traversal; only structurally bounded control-flow and
                // storage selections recurse into the graph builder.
                Stack<Action> work = new();
                work.Push(() => Visit(expression));
                while (work.Count != 0)
                {
                    work.Pop()();
                }
                void Visit(SyntaxNode node)
                {
                    // Type-query facts describe types, not runtime values or
                    // temporaries. Never import operand effects into this graph.
                    if (node.Kind == SyntaxKind.TypeOf)
                    {
                        return;
                    }
                    if (node.Kind == SyntaxKind.MatchType)
                    {
                        SemanticFact selection = node.Fact!;
                        List<int> resultObjects = NewObjects(node, selection.Type);
                        List<int> resultTargets = new();
                        objectValues[node] = resultObjects;
                        containedValues[node] = resultTargets;
                        SyntaxNode selected = node.ChildAt(selection.SelectedArmIndex).ChildAt(1);
                        BuildBlock(selected, selection.Type != analysis.ScalarType(ScalarKind.Unit),
                            resultObjects, resultTargets);
                        if (HasCountedContents(selection.Type)
                            && (selection.Type.Kind is SemanticTypeKind.Array or SemanticTypeKind.Tuple
                                || selection.Type.Declaration?.Kind == SymbolKind.Struct))
                        {
                            aggregateTemporaries[node] = RegisterHolder(node, selection.Type, true);
                        }
                        return;
                    }
                    if (node.Fact?.Symbol?.Kind == SymbolKind.MemberNamespace)
                    {
                        // Labels preserve the receiver's saved identity and lifetime;
                        // they neither acquire a second owner nor revive a stale handle.
                        SyntaxNode receiver = node.ChildAt(0);
                        work.Push(() =>
                        {
                            objectValues[node] = objectValues[receiver];
                        });
                        work.Push(() => Visit(receiver));
                        return;
                    }
                    // Completion runs after children. Each saved class-valued
                    // expression has its own identity across later effects.
                    if (node.Fact is SemanticFact fact && node.Kind != SyntaxKind.If)
                    {
                        work.Push(() =>
                        {
                            objectValues[node] = NewObjects(node, fact.Type);
                        });
                    }
                    if (node.Fact is SemanticFact aggregateFact
                        && (aggregateFact.Type.Kind is SemanticTypeKind.Array or SemanticTypeKind.Tuple
                            || aggregateFact.Type.Declaration?.Kind == SymbolKind.Struct)
                        && HasCountedContents(aggregateFact.Type) && node.Kind != SyntaxKind.Group)
                    {
                        work.Push(() =>
                        {
                            aggregateTemporaries[node] = RegisterHolder(
                                node, aggregateFact.Type, !KnownEmptyArray(node));
                        });
                    }
                    if (node.Kind == SyntaxKind.Name)
                    {
                        if (node.Fact?.Symbol is SemanticSymbol symbol)
                        {
                            if (aliases.TryGetValue(symbol, out int alias))
                            {
                                Use(node, alias);
                                // A reference observes the current contents of its
                                // captured storage, including branch-selected storage.
                                // Reading a value snapshots those aliases now, not
                                // when the reference binding was declared.
                                containedValues[node] = SnapshotReferent(node, alias);
                            }
                            else if (containedBindings.TryGetValue(symbol, out List<int>? bindingTargets))
                            {
                                containedValues[node] = SnapshotContained(node, bindingTargets);
                            }
                            if (objectBindings.TryGetValue(symbol, out List<int>? objectHandles))
                            {
                                ObjectEvents(node, 6, objectHandles);
                            }
                        }
                        return;
                    }
                    if (node.Kind == SyntaxKind.If)
                    {
                        BuildIf(node);
                        return;
                    }
                    if (node.Kind == SyntaxKind.Call)
                    {
                        if (node.Fact!.Symbol?.Builtin == BuiltinKind.Capacity)
                        {
                            int observed = ObserveArray(node.ChildAt(1));
                            if (observed >= 0)
                            {
                                Use(node, observed);
                            }
                            return;
                        }
                        if (node.Fact.ArrayOperation != ArrayOperation.None)
                        {
                            int receiver = Select(node.ChildAt(0).ChildAt(0));
                            BuildExpression(node.ChildAt(1));
                            Use(node, receiver);
                            Event(node, 3, receiver);
                            if (node.Fact.ArrayOperation == ArrayOperation.Push)
                            {
                                UseObject(node.ChildAt(1));
                                MoveAggregate(node.ChildAt(1));
                                if (!KnownEmptyArray(node.ChildAt(1))
                                    && localHolders.TryGetValue(paths[receiver].Root, out int filledHolder))
                                {
                                    Event(node, 8, filledHolder);
                                }
                            }
                            else if (node.Fact.ArrayOperation is ArrayOperation.Resize or ArrayOperation.Truncate)
                            {
                                // Shrink can release any removed counted leaf,
                                // including leaves inside nested arrays.
                                if (!partialHolders.Contains(receiver)
                                    && localHolders.TryGetValue(paths[receiver].Root, out int arrayHolder))
                                {
                                    Event(node, 11, arrayHolder);
                                    if (paths[receiver].Depth == 0
                                        && node.ChildAt(1).Fact!.IntegerConstant == BigInteger.Zero)
                                    {
                                        Event(node, 10, arrayHolder);
                                    }
                                }
                                else
                                {
                                    Release(node, node.ChildAt(0).ChildAt(0).Fact!.Type);
                                }
                            }
                            return;
                        }
                        if (node.Fact.ClassOperation == ClassOperation.Destroy)
                        {
                            SyntaxNode receiver = node.ChildAt(0).ChildAt(0);
                            SyntaxNode identity = receiver;
                            while (identity.Kind == SyntaxKind.Group
                                || identity.Fact?.Symbol?.Kind == SymbolKind.MemberNamespace)
                            {
                                identity = identity.ChildAt(0);
                            }
                            if (teardownReceiver is not null && identity.Fact?.Symbol == teardownReceiver)
                            {
                                Fail(node, "LYC334", "A destructor fragment cannot destroy its active receiver.");
                            }
                            BuildExpression(receiver);
                            UseObject(receiver);
                            SemanticSymbol destroyedOwner = node.Fact.Symbol!;
                            SemanticSymbol? hook = destroyedOwner.FinalHook();
                            if (hook is not null)
                            {
                                foreach (SemanticSymbol affected in classEffects[hook])
                                {
                                    DestroyClass(node, affected);
                                }
                            }
                            DestroyClass(node, destroyedOwner);
                            return;
                        }
                        List<(SyntaxNode Node, int Handle)> arguments = new();
                        SemanticSymbol target = node.Fact.Symbol!;
                        bool structReceiver = target.Scope?.MemberOwner?.Kind == SymbolKind.Struct
                            && target.FunctionScope!.ParameterCount() > 0
                            && target.FunctionScope.ParameterAt(0).Name == "self";
                        int parameterOffset = target.Scope?.Class is not null || structReceiver ? 1 : 0;
                        bool hasReceiver = node.ChildAt(0).Kind == SyntaxKind.Member
                            && parameterOffset == 1;
                        if (hasReceiver)
                        {
                            SyntaxNode receiver = node.ChildAt(0).ChildAt(0);
                            if (structReceiver && target.FunctionScope!.ParameterAt(0).IsReference)
                            {
                                SyntaxNode reference = receiver;
                                while (reference.Kind == SyntaxKind.Group)
                                {
                                    reference = reference.ChildAt(0);
                                }
                                int selected = Select(reference.ChildAt(0));
                                arguments.Add((receiver, selected));
                            }
                            else
                            {
                                BuildExpression(receiver);
                            }
                        }
                        for (int i = 1; i < node.ChildCount(); i += 1)
                        {
                            SyntaxNode argument = node.ChildAt(i);
                            if (target.FunctionScope!.ParameterAt(i - 1 + parameterOffset).IsReference)
                            {
                                int selected = Select(argument.ChildAt(0));
                                arguments.Add((argument, selected));
                            }
                            else
                            {
                                BuildExpression(argument);
                            }
                        }
                        Dictionary<(SemanticSymbol Parameter, int Component), int> actuals = new();
                        Dictionary<(SemanticSymbol Parameter, int Component), int> writableActuals = new();
                        int referenceIndex = 0;
                        for (int i = 0; i < target.FunctionScope!.ParameterCount(); i += 1)
                        {
                            SemanticSymbol parameter = target.FunctionScope.ParameterAt(i);
                            List<int>? argumentTargets = null;
                            if (parameter.IsReference)
                            {
                                int selected = arguments[referenceIndex].Handle;
                                actuals.Add((parameter, -1), selected);
                                // Capture current contents after all arguments ran.
                                // Output destinations retain the address separately.
                                argumentTargets = new();
                                int count = ReferenceCount(parameter.Type!);
                                for (int component = 0; component < count; component += 1)
                                {
                                    argumentTargets.Add(ReadReferentOccurrence(node, selected, component));
                                }
                                referenceIndex += 1;
                            }
                            else
                            {
                                SyntaxNode? valueArgument = null;
                                if (i < parameterOffset)
                                {
                                    if (hasReceiver)
                                    {
                                        valueArgument = node.ChildAt(0).ChildAt(0);
                                    }
                                }
                                else
                                {
                                    valueArgument = node.ChildAt(i - parameterOffset + 1);
                                }
                                if (valueArgument is not null)
                                {
                                    containedValues.TryGetValue(valueArgument, out argumentTargets);
                                }
                            }
                            // Builtin placeholder parameters need not carry a concrete
                            // type. They have no user-function formal storage summary.
                            if (parameter.Type is not SemanticType formalType)
                            {
                                continue;
                            }
                            var formal = FormalStorage(formalType, parameter.IsReference);
                            if (formal.Slots.Count != 0)
                            {
                                if (argumentTargets is null || argumentTargets.Count != formal.Nodes[0].Slots.Count)
                                {
                                    throw new InvalidOperationException("Call actual has no matching root occurrences.");
                                }
                                List<List<int>> referents = new();
                                List<int> values = new();
                                List<int> destinations = new();
                                for (int component = 0; component < formal.Slots.Count; component += 1)
                                {
                                    int referent = formal.Slots[component].Referent;
                                    referents.Add(formal.Nodes[referent].Slots);
                                    // Event 20 supplies real targets; these paths are
                                    // bookkeeping only, never a fallback callee identity.
                                    int value = FixedTarget(paths[argumentTargets[0]]);
                                    int destination = FixedTarget(paths[argumentTargets[0]]);
                                    values.Add(value);
                                    destinations.Add(destination);
                                    actuals.Add((parameter, component), value);
                                    if (parameter.IsReference || formal.Slots[component].Container != 0)
                                    {
                                        writableActuals.Add((parameter, component), destination);
                                    }
                                }
                                int rootAddress = parameter.IsReference ? actuals[(parameter, -1)] : -1;
                                int map = Event(node, 20, -1);
                                mappedActuals.Add(map, (formal.Nodes[0].Slots, referents,
                                    argumentTargets, rootAddress, values, destinations));
                            }
                        }
                        foreach (var argument in arguments)
                        {
                            Use(argument.Node, argument.Handle);
                        }
                        foreach (var actual in actuals)
                        {
                            if (actual.Key.Component >= 0)
                            {
                                // A later argument may invalidate an earlier saved
                                // value. Check entry validity before callee effects;
                                // internal ordering is checked by its obligations.
                                Use(node, actual.Value);
                            }
                        }
                        if (hasReceiver)
                        {
                            UseObject(node.ChildAt(0).ChildAt(0));
                        }
                        for (int i = 1; i < node.ChildCount(); i += 1)
                        {
                            UseObject(node.ChildAt(i));
                        }
                        if (target.Scope?.Class is SemanticSymbol constructedClass
                            && target.Name == constructedClass.Name)
                        {
                            // Arguments are evaluated before constructor allocation.
                            int growth = paths.Count;
                            paths.Add((constructedClass, classGrowth, -1));
                            Event(node, 3, growth);
                            // Selected reference arguments must survive the
                            // allocation before the constructor body can use them.
                            foreach (var argument in arguments)
                            {
                                Use(argument.Node, argument.Handle);
                            }
                            foreach (var actual in actuals)
                            {
                                if (actual.Key.Component >= 0)
                                {
                                    // Allocation can invalidate field-backed
                                    // aliases inside value arguments as well.
                                    Use(node, actual.Value);
                                }
                            }
                        }
                        if (classEffects.TryGetValue(target, out var destroyed))
                        {
                            foreach (SemanticSymbol owner in destroyed)
                            {
                                DestroyClass(node, owner);
                            }
                        }
                        if (effects.ContainsKey(target))
                        {
                            int Substitute((SemanticSymbol Root, int Depth, int Component) formal)
                            {
                                if (!actuals.TryGetValue((formal.Root, formal.Component), out int actual))
                                {
                                    if (formal.Root.Kind == SymbolKind.Parameter && formal.Component >= 0)
                                    {
                                        throw new InvalidOperationException("A contained formal target has no caller mapping.");
                                    }
                                    // Class-wide effects retain nominal identity.
                                    return FixedTarget(formal);
                                }
                                // Snapshot at this call, after every argument has
                                // run. Branch-dependent actuals remain alternatives
                                // in the worklist rather than becoming static paths.
                                return AcquireDerived(node, actual, paths[actual].Depth + formal.Depth);
                            }
                            // Requirements describe internal order. Do not turn
                            // all argument uses into post-mutation uses: that
                            // would reject a valid write-before-grow helper.
                            foreach (var requirement in obligations[target])
                            {
                                int changedTarget = Substitute(requirement.Changed);
                                int usedTarget = Substitute(requirement.Used);
                                int check = Event(node, 4, -1);
                                callRequirements.Add(check, (changedTarget, usedTarget));
                            }
                            foreach (var effect in effects[target])
                            {
                                int handle = Substitute(effect);
                                var actualEffect = paths[handle];
                                Event(node, 3, handle);
                                // A mutating helper can populate an empty actual.
                                // Its path summary deliberately does not promise
                                // exact length or count after the call.
                                if (actualEffect.Depth >= 0
                                    && localHolders.TryGetValue(actualEffect.Root, out int changedHolder))
                                {
                                    Event(node, 8, changedHolder);
                                }
                            }
                            List<(int Address, int Occurrence, List<int> Sources,
                                List<int> Causes, bool Expired)> outputs = new();
                            foreach (var replacement in replacements[target])
                            {
                                if (!writableActuals.TryGetValue(replacement.Key, out int address))
                                {
                                    throw new InvalidOperationException("A caller replacement requires its actual address.");
                                }
                                List<int> sources = new();
                                List<int> causes = new();
                                foreach (var source in replacement.Value.Targets)
                                {
                                    sources.Add(Substitute(source));
                                }
                                foreach (var cause in replacement.Value.Causes)
                                {
                                    causes.Add(Substitute(cause));
                                }
                                outputs.Add((address, 0, sources,
                                    causes, replacement.Value.Expired));
                            }
                            // Capture every source before the single batched write:
                            // a swap must not read an already-replaced caller slot.
                            if (outputs.Count != 0)
                            {
                                int replacementEvent = Event(node, 15, -1);
                                replacedTargets.Add(replacementEvent, outputs);
                            }
                        }
                        // Ordinary callees consume value arguments. Inline
                        // builtins such as len instead leave their evaluated
                        // array temporary owned by the caller's cleanup scope.
                        if (target.Builtin == BuiltinKind.None)
                        {
                            if (structReceiver && !target.FunctionScope!.ParameterAt(0).IsReference)
                            {
                                MoveAggregate(node.ChildAt(0).ChildAt(0));
                            }
                            for (int i = 1; i < node.ChildCount(); i += 1)
                            {
                                MoveAggregate(node.ChildAt(i));
                            }
                        }
                        if (effects.ContainsKey(target) && !returningFunctions.Contains(target))
                        {
                            // Rebuild this graph on the next fixed-point pass.
                            // Following an unknown return now would incorrectly
                            // preserve old caller slots as a possible call result.
                            frontier = new();
                        }
                        return;
                    }
                    if (node.Kind == SyntaxKind.Record)
                    {
                        // Explicit values precede default invocations. Each default
                        // uses its field summary, never a caller evaluation of its AST.
                        work.Push(() =>
                        {
                            Dictionary<SemanticSymbol, SyntaxNode> supplied = new();
                            for (int i = 1; i < node.ChildCount(); i += 1)
                            {
                                SyntaxNode fieldValue = node.ChildAt(i);
                                supplied.Add(fieldValue.Fact!.Symbol!, fieldValue.ChildAt(0));
                            }
                            List<int> defaultObjects = new();
                            Dictionary<SemanticSymbol, List<int>> omittedTargets = new();
                            foreach (SemanticSymbol field in node.Fact!.Type.Declaration!.MemberScope!.Fields())
                            {
                                if (supplied.ContainsKey(field))
                                {
                                    continue;
                                }
                                foreach (SemanticSymbol owner in classEffects[field])
                                {
                                    DestroyClass(node, owner);
                                }
                                foreach (var requirement in obligations[field])
                                {
                                    int check = Event(node, 4, -1);
                                    callRequirements.Add(check,
                                        (FixedTarget(requirement.Changed), FixedTarget(requirement.Used)));
                                }
                                foreach (var effect in effects[field])
                                {
                                    int handle = paths.Count;
                                    paths.Add(effect);
                                    Event(node, 3, handle);
                                }
                                defaultObjects.AddRange(NewObjects(node, field.Type!));
                                if (!returningFunctions.Contains(field))
                                {
                                    // A default is a generated call, not inline AST
                                    // evaluation. Unknown return is bottom just as
                                    // for an ordinary call: later fixed-point passes
                                    // reconnect this continuation once an exit exists.
                                    frontier = new();
                                }
                                var resultGraph = FormalStorage(field.Type!, field.IsReference);
                                int rootOffset = field.IsReference ? 1 : 0;
                                List<int> importedSlots = new();
                                for (int position = 0; position < resultGraph.Slots.Count + rootOffset; position += 1)
                                {
                                    // Event 21 replaces these bookkeeping identities
                                    // with saved targets; there is no acquisition.
                                    importedSlots.Add(FixedTarget((field, 0, -1)));
                                }
                                importedInitializers.Add(Event(node, 21, -1), (field, importedSlots));
                                List<int> resultRoots = new();
                                if (field.IsReference)
                                {
                                    resultRoots.Add(importedSlots[0]);
                                }
                                else
                                {
                                    foreach (int root in resultGraph.Nodes[0].Slots)
                                    {
                                        resultRoots.Add(importedSlots[root]);
                                    }
                                }
                                omittedTargets.Add(field, resultRoots);
                            }
                            ObjectEvents(node, 6, defaultObjects);
                            List<int> recordTargets = new();
                            foreach (SemanticSymbol field in node.Fact!.Type.Declaration!.MemberScope!.Fields())
                            {
                                if (!supplied.TryGetValue(field, out SyntaxNode? value))
                                {
                                    foreach (int target in omittedTargets[field])
                                    {
                                        // Later defaults can invalidate an earlier
                                        // result. Preserve its saved history through
                                        // the final construction boundary.
                                        Use(node, target);
                                    }
                                    recordTargets.AddRange(SnapshotContained(node, omittedTargets[field]));
                                    continue;
                                }
                                if (field.IsReference)
                                {
                                    int selected = referenceValues[value];
                                    Use(value, selected);
                                    recordTargets.Add(AcquireDerived(node, selected, paths[selected].Depth));
                                }
                                else if (containedValues.TryGetValue(value, out List<int>? fieldTargets))
                                {
                                    recordTargets.AddRange(SnapshotContained(node, fieldTargets));
                                }
                            }
                            if (recordTargets.Count != 0)
                            {
                                containedValues[node] = recordTargets;
                            }
                            for (int i = 1; i < node.ChildCount(); i += 1)
                            {
                                UseObject(node.ChildAt(i).ChildAt(0));
                            }
                            for (int i = 1; i < node.ChildCount(); i += 1)
                            {
                                MoveAggregate(node.ChildAt(i).ChildAt(0));
                            }
                        });
                        for (int i = node.ChildCount() - 1; i >= 1; i -= 1)
                        {
                            SyntaxNode fieldValue = node.ChildAt(i);
                            SyntaxNode initializer = fieldValue.ChildAt(0);
                            if (fieldValue.Fact!.Symbol!.IsReference)
                            {
                                work.Push(() =>
                                {
                                    SyntaxNode reference = initializer;
                                    while (reference.Kind == SyntaxKind.Group)
                                    {
                                        reference = reference.ChildAt(0);
                                    }
                                    // Reference construction captures storage, not
                                    // a value copy of the referent expression.
                                    referenceValues[initializer] = Select(reference.ChildAt(0));
                                });
                            }
                            else
                            {
                                work.Push(() => Visit(initializer));
                            }
                        }
                        return;
                    }
                    if (node.Kind == SyntaxKind.Binary && Text(node) is "&&" or "||")
                    {
                        BuildExpression(node.ChildAt(0));
                        List<int> skipped = new(frontier);
                        // The emitted right operand has its own conditional C
                        // scope. Finalize its temporaries before merging paths;
                        // the skipped path never acquired these holders.
                        int rightCleanup = cleanupFrames.Count;
                        cleanupFrames.Add((new(), new()));
                        BuildExpression(node.ChildAt(1));
                        Cleanup(node, rightCleanup);
                        cleanupFrames.RemoveAt(cleanupFrames.Count - 1);
                        frontier.AddRange(skipped);
                        return;
                    }
                    work.Push(() =>
                    {
                        for (int i = 0; i < node.ChildCount(); i += 1)
                        {
                            UseObject(node.ChildAt(i));
                        }
                        if (node.Kind == SyntaxKind.Group
                            && containedValues.TryGetValue(node.ChildAt(0), out List<int>? groupedTargets))
                        {
                            containedValues[node] = SnapshotContained(node, groupedTargets);
                        }
                        if (node.Kind == SyntaxKind.Tuple)
                        {
                            List<int> tupleTargets = new();
                            for (int i = 0; i < node.ChildCount(); i += 1)
                            {
                                if (containedValues.TryGetValue(node.ChildAt(i), out List<int>? componentTargets))
                                {
                                    tupleTargets.AddRange(SnapshotContained(node, componentTargets));
                                }
                            }
                            if (tupleTargets.Count != 0)
                            {
                                containedValues[node] = tupleTargets;
                            }
                        }
                        if (node.Kind is SyntaxKind.Array or SyntaxKind.Tuple)
                        {
                            for (int i = 0; i < node.ChildCount(); i += 1)
                            {
                                MoveAggregate(node.ChildAt(i));
                            }
                        }
                        if (node.Kind == SyntaxKind.Member)
                        {
                            SyntaxNode receiver = node.ChildAt(0);
                            if (containedValues.TryGetValue(receiver, out List<int>? receiverTargets))
                            {
                                var projection = ContainedProjection(node);
                                if (projection.Offset + projection.Count > receiverTargets.Count)
                                {
                                    throw new InvalidOperationException("Contained projection exceeds the receiver's target snapshot.");
                                }
                                if (projection.IsReference)
                                {
                                    // Reading a reference field uses its stored
                                    // alias, not the address of the field slot.
                                    int address = receiverTargets[projection.Offset];
                                    Use(node, address);
                                    containedValues[node] = SnapshotReferent(node, address);
                                }
                                else if (projection.Count != 0)
                                {
                                    // Each repeated inline occurrence keeps its
                                    // own range, even when canonical types match.
                                    List<int> selectedTargets = receiverTargets.GetRange(
                                        projection.Offset, projection.Count);
                                    containedValues[node] = SnapshotContained(node, selectedTargets);
                                }
                            }
                            RegisterComputedReceiver(receiver);
                        }
                    });
                    for (int i = node.ChildCount() - 1; i >= 0; i -= 1)
                    {
                        SyntaxNode child = node.ChildAt(i);
                        if (child.Fact is not null)
                        {
                            work.Push(() => Visit(child));
                        }
                    }
                }
            }
            void BuildIf(SyntaxNode node, List<int>? resultObjects = null, List<int>? resultTargets = null)
            {
                // Preserve every contained class identity across branch cleanup.
                // Completion must not revive a result invalidated by finalization.
                resultObjects ??= NewObjects(node, node.Fact!.Type);
                objectValues[node] = resultObjects;
                // All continuing arms write the same result slots. Their target
                // states merge at the join, rather than choosing an arm lexically.
                resultTargets ??= new();
                containedValues[node] = resultTargets;
                BuildExpression(node.ChildAt(0));
                List<int> start = new(frontier);
                BuildBlock(node.ChildAt(1), node.Fact!.Type != analysis.ScalarType(ScalarKind.Unit), resultObjects, resultTargets);
                List<int> thenExit = new(frontier);
                frontier = start;
                if (node.ChildCount() > 2)
                {
                    SyntaxNode alternative = node.ChildAt(2);
                    if (alternative.Kind == SyntaxKind.If)
                    {
                        // Else-if condition temporaries belong to the emitted
                        // else scope, not to the surrounding source block.
                        int alternativeCleanup = cleanupFrames.Count;
                        cleanupFrames.Add((new(), new()));
                        BuildIf(alternative, resultObjects, resultTargets);
                        Cleanup(alternative, alternativeCleanup);
                        ObjectEvents(alternative, 6, resultObjects);
                        foreach (int target in resultTargets)
                        {
                            Use(alternative, target);
                        }
                        cleanupFrames.RemoveAt(cleanupFrames.Count - 1);
                    }
                    else
                    {
                        BuildBlock(alternative, node.Fact!.Type != analysis.ScalarType(ScalarKind.Unit), resultObjects, resultTargets);
                    }
                }
                frontier.AddRange(thenExit);
            }
            void BuildBlock(SyntaxNode block, bool transfersTail = false, List<int>? resultObjects = null,
                List<int>? resultTargets = null, bool sharesCleanup = false)
            {
                // Preparation is a phase of the surrounding body, not a local
                // lifetime boundary. Its holders join that body's cleanup frame.
                if (!sharesCleanup)
                {
                    cleanupFrames.Add((new(), new()));
                }
                for (int i = 0; i < block.ChildCount(); i += 1)
                {
                    SyntaxNode statement = block.ChildAt(i);
                    switch (statement.Kind)
                    {
                        case SyntaxKind.Prepare:
                            BuildBlock(statement.ChildAt(0), sharesCleanup: true);
                            break;
                        case SyntaxKind.Binding:
                            if (statement.ChildAt(0).Kind is SyntaxKind.PatternTuple or SyntaxKind.Wildcard)
                            {
                                SyntaxNode aggregate = statement.ChildAt(statement.ChildCount() - 1);
                                BuildExpression(aggregate);
                                UseObject(aggregate);
                                MoveAggregate(aggregate);
                                containedValues.TryGetValue(aggregate, out List<int>? aggregateTargets);
                                int aggregateTargetCount = ReferenceCount(aggregate.Fact!.Type);
                                if ((aggregateTargets?.Count ?? 0) != aggregateTargetCount)
                                {
                                    throw new InvalidOperationException("Destructuring initializer is missing contained reference targets.");
                                }
                                aggregateTargets ??= new();
                                int targetOffset = 0;
                                Stack<(SyntaxNode Pattern, SemanticType Type)> pendingBindings = new();
                                List<(SyntaxNode Pattern, SemanticType Type)> discarded = new();
                                pendingBindings.Push((statement.ChildAt(0), aggregate.Fact!.Type));
                                while (pendingBindings.Count > 0)
                                {
                                    (SyntaxNode part, SemanticType partType) = pendingBindings.Pop();
                                    if (part.Kind == SyntaxKind.PatternTuple)
                                    {
                                        for (int j = part.ChildCount() - 1; j >= 0; j -= 1)
                                        {
                                            pendingBindings.Push((part.ChildAt(j), partType.ComponentAt(j)));
                                        }
                                    }
                                    else if (part.Kind == SyntaxKind.Wildcard)
                                    {
                                        discarded.Add((part, partType));
                                        targetOffset += ReferenceCount(partType);
                                    }
                                    else
                                    {
                                        SemanticSymbol local = part.DeclaredSymbol!;
                                        cleanupFrames[^1].Storage.Add(local);
                                        int holder = RegisterHolder(part, partType, true);
                                        if (holder >= 0)
                                        {
                                            localHolders.Add(local, holder);
                                        }
                                        objectBindings.Add(local, NewObjects(part, partType));
                                        int targetCount = ReferenceCount(partType);
                                        if (targetCount != 0)
                                        {
                                            List<int> localTargets = new();
                                            for (int occurrence = 0; occurrence < targetCount; occurrence += 1)
                                            {
                                                localTargets.Add(FixedTarget((local, 0, occurrence)));
                                            }
                                            containedBindings.Add(local, localTargets);
                                            // Establish all named aliases before discarded owners
                                            // are released; their effects must reach these copies.
                                            StoreContained(part, localTargets,
                                                aggregateTargets.GetRange(targetOffset, targetCount));
                                        }
                                        targetOffset += targetCount;
                                    }
                                }
                                if (targetOffset != aggregateTargets.Count)
                                {
                                    throw new InvalidOperationException("Destructuring did not consume every contained reference occurrence.");
                                }
                                // Copies retain named counted contents. Only discarded
                                // contents can finalize when the initializer is released.
                                foreach ((SyntaxNode part, SemanticType partType) in discarded)
                                {
                                    Release(part, partType);
                                }
                                break;
                            }
                            SemanticSymbol symbol = statement.DeclaredSymbol!;
                            SyntaxNode initializer = statement.ChildAt(statement.ChildCount() - 1);
                            if (symbol.IsReference)
                            {
                                int selected = Select(initializer.ChildAt(0));
                                aliases.Add(symbol, selected);
                            }
                            else if (initializer.Fact is not null)
                            {
                                BuildExpression(initializer);
                                UseObject(initializer);
                                MoveAggregate(initializer);
                            }
                            if (!symbol.IsReference && symbol.Type is SemanticType ownedLocal)
                            {
                                cleanupFrames[^1].Storage.Add(symbol);
                                int localHolder = RegisterHolder(statement, ownedLocal,
                                    initializer.Fact is not null && !KnownEmptyArray(initializer));
                                if (localHolder >= 0)
                                {
                                    localHolders.Add(symbol, localHolder);
                                }
                            }
                            // Definite assignment independently prevents reads of
                            // an uninitialized local. Its lifetime identity exists
                            // even when the first value arrives in a later store.
                            if (symbol.Type is SemanticType bindingType)
                            {
                                objectBindings.Add(symbol, NewObjects(statement, bindingType));
                                if (!symbol.IsReference)
                                {
                                    List<int> localTargets = new();
                                    foreach (SemanticSymbol field in ReferenceOccurrences(bindingType))
                                    {
                                        // A slot exists before definite assignment,
                                        // but has no acquired target until a store.
                                        localTargets.Add(FixedTarget((symbol, 0, localTargets.Count)));
                                    }
                                    if (localTargets.Count != 0)
                                    {
                                        containedBindings.Add(symbol, localTargets);
                                        if (containedValues.TryGetValue(initializer, out List<int>? initialTargets))
                                        {
                                            StoreContained(statement, localTargets, initialTargets);
                                        }
                                    }
                                }
                            }
                            break;
                        case SyntaxKind.Assignment:
                            SyntaxNode target = statement.ChildAt(0);
                            int destination = Select(target);
                            if (target.Kind == SyntaxKind.Member
                                && target.ChildAt(0).Fact!.Type.Class is not null)
                            {
                                indexedFieldDestinations.Add(destination);
                            }
                            BuildExpression(statement.ChildAt(1));
                            Use(target, destination);
                            UseObject(statement.ChildAt(1));
                            MoveAggregate(statement.ChildAt(1));
                            if (containedValues.TryGetValue(statement.ChildAt(1), out List<int>? assignedTargets))
                            {
                                for (int occurrence = 0; occurrence < assignedTargets.Count; occurrence += 1)
                                {
                                    int store = Event(statement, 19, destination);
                                    storedReferents.Add(store,
                                        (destination, occurrence, assignedTargets.Count, assignedTargets[occurrence]));
                                }
                            }
                            // Depth counts backing allocations, not inline fields.
                            // A component alias must never disarm sibling cleanup.
                            if (paths[destination].Depth == 0
                                && !partialHolders.Contains(destination)
                                && localHolders.TryGetValue(paths[destination].Root, out int replacedHolder))
                            {
                                Event(target, 9, replacedHolder);
                                Event(target, KnownEmptyArray(statement.ChildAt(1)) ? 10 : 8, replacedHolder);
                            }
                            else
                            {
                                Release(target, target.Fact!.Type);
                            }
                            if ((paths[destination].Depth > 0 || partialHolders.Contains(destination))
                                && !KnownEmptyArray(statement.ChildAt(1))
                                && localHolders.TryGetValue(paths[destination].Root, out int containingHolder))
                            {
                                Event(target, 8, containingHolder);
                            }
                            if (target.Kind == SyntaxKind.Name
                                && objectBindings.TryGetValue(target.Fact!.Symbol!, out List<int>? assignedObjects))
                            {
                                ObjectEvents(target, 5, assignedObjects);
                            }
                            if (target.Fact!.Type.ArrayDepth > 0)
                            {
                                Event(target, 3, destination);
                            }
                            break;
                        case SyntaxKind.ExpressionStatement:
                            BuildExpression(statement.ChildAt(0));
                            // A value tail transfers its result out of this block.
                            // A trailing discard marker prevents this condition.
                            if (!transfersTail || i != block.ChildCount() - 1)
                            {
                                if (!KnownEmptyArray(statement.ChildAt(0)))
                                {
                                    Release(statement, statement.ChildAt(0).Fact!.Type);
                                }
                            }
                            if (transfersTail && i == block.ChildCount() - 1 && resultObjects is not null)
                            {
                                UseObject(statement.ChildAt(0));
                                ObjectEvents(statement.ChildAt(0), 5, resultObjects);
                                if (resultTargets is not null)
                                {
                                    SyntaxNode tail = statement.ChildAt(0);
                                    containedValues.TryGetValue(tail, out List<int>? tailTargets);
                                    int expectedTargets = ReferenceCount(tail.Fact!.Type);
                                    if ((tailTargets?.Count ?? 0) != expectedTargets)
                                    {
                                        throw new InvalidOperationException("Conditional tail is missing contained reference targets.");
                                    }
                                    tailTargets ??= new();
                                    // Copy before cleanup so scope expiry and finalizer
                                    // effects also invalidate the preserved result.
                                    if (resultTargets.Count == 0)
                                    {
                                        resultTargets.AddRange(SnapshotContained(statement, tailTargets));
                                    }
                                    else
                                    {
                                        StoreContained(statement, resultTargets, tailTargets);
                                    }
                                }
                            }
                            MoveAggregate(statement.ChildAt(0));
                            break;
                        case SyntaxKind.While:
                            int header = Event(statement, 0, -1);
                            List<int> before = frontier;
                            frontier = new();
                            int exit = Event(statement, 0, -1);
                            frontier = before;
                            int conditionCleanup = cleanupFrames.Count;
                            loopEdges.Push((header, exit, conditionCleanup));
                            cleanupFrames.Add((new(), new()));
                            BuildExpression(statement.ChildAt(0));
                            List<int> conditionFrontier = new(frontier);
                            // The false path leaves the condition scope without
                            // entering the body. Keep its cleanup effects off the
                            // true path, whose temporaries survive through the body.
                            Cleanup(statement, conditionCleanup);
                            foreach (int condition in frontier)
                            {
                                graph[condition].Next.Add(exit);
                            }
                            frontier = conditionFrontier;
                            BuildBlock(statement.ChildAt(1));
                            Cleanup(statement, conditionCleanup);
                            foreach (int back in frontier)
                            {
                                graph[back].Next.Add(header);
                            }
                            cleanupFrames.RemoveAt(cleanupFrames.Count - 1);
                            loopEdges.Pop();
                            frontier = new() { exit };
                            break;
                        case SyntaxKind.Break:
                        case SyntaxKind.Continue:
                            var loop = loopEdges.Peek();
                            Cleanup(statement, loop.CleanupDepth);
                            int transfer = statement.Kind == SyntaxKind.Break ? loop.Exit : loop.Header;
                            foreach (int previous in frontier)
                            {
                                graph[previous].Next.Add(transfer);
                            }
                            frontier = new();
                            break;
                        case SyntaxKind.Return:
                            if (statement.ChildCount() != 0)
                            {
                                BuildExpression(statement.ChildAt(0));
                                MoveAggregate(statement.ChildAt(0));
                            }
                            Cleanup(statement, 0);
                            // The preserved result leaves only after cleanup.
                            // A final hook may invalidate an ordinary result even
                            // though evaluating it before cleanup was safe.
                            if (statement.ChildCount() != 0)
                            {
                                UseObject(statement.ChildAt(0));
                            }
                            UseConstructedReceiver(statement);
                            Event(statement, 14, -1);
                            frontier = new();
                            break;
                    }
                }
                if (!sharesCleanup)
                {
                    Cleanup(block, cleanupFrames.Count - 1);
                }
                if (resultObjects is not null)
                {
                    // As with return, the preserved result must remain valid
                    // after all branch-local finalization effects have run.
                    ObjectEvents(block, 6, resultObjects);
                }
                if (resultTargets is not null)
                {
                    foreach (int target in resultTargets)
                    {
                        Use(block, target);
                    }
                }
                if (!sharesCleanup)
                {
                    cleanupFrames.RemoveAt(cleanupFrames.Count - 1);
                }
            }
        }

        // Each block is checked in a distinct lexical scope owned by analysis;
        // its assignment sets are temporary work.
        // The return value describes reachability, not whether a return appeared
        // somewhere in the syntax: only continuing paths reach a later read.
        bool CheckBlock(SyntaxNode block, SemanticScope parent, SemanticSymbol function,
            bool valueBlock = false, SemanticType? expectedType = null, bool sharesScope = false)
        {
            // Only the initial preparation phase shares the outer body scope.
            SemanticScope scope = sharesScope ? parent : new(analysis, parent);
            bool continues = true;
            SemanticType resultType = analysis.ScalarType(ScalarKind.Unit);
            for (int i = 0; i < block.ChildCount(); i += 1)
            {
                SyntaxNode statement = block.ChildAt(i);
                if (statement.Kind == SyntaxKind.DiscardResult)
                {
                    continue;
                }
                if (!continues)
                {
                    // Do not silently accept unchecked dead syntax. Unreachable
                    // statements are outside this increment, not forbidden Lyric.
                    Unsupported(statement);
                }
                switch (statement.Kind)
                {
                    case SyntaxKind.Prepare:
                        SyntaxNode declaration = function.Declaration!;
                        bool operation = false;
                        bool functionBody = false;
                        for (int childIndex = 0; childIndex < declaration.ChildCount(); childIndex += 1)
                        {
                            SyntaxNode child = declaration.ChildAt(childIndex);
                            operation |= child.Kind == SyntaxKind.Modifier && Text(child) is "add" or "remove";
                            functionBody |= child == block && child.Kind == SyntaxKind.Block;
                        }
                        if (!operation || !functionBody || i != 0)
                        {
                            Fail(statement, "LYC303", "Prepare is only the initial phase of a relationship operation.");
                        }
                        continues = CheckBlock(statement.ChildAt(0), scope, function, sharesScope: true);
                        _ = new SemanticFact(analysis, statement, resultType, null, canContinue: continues);
                        break;
                    case SyntaxKind.Binding:
                        CheckBinding(statement, scope);
                        break;
                    case SyntaxKind.Assignment:
                        CheckAssignment(statement, scope);
                        break;
                    case SyntaxKind.ExpressionStatement:
                        SyntaxNode expression = statement.ChildAt(0);
                        if (valueBlock && i == block.ChildCount() - 1)
                        {
                            resultType = CheckExpression(expression, scope, expectedType);
                            if (expectedType is not null)
                            {
                                RequireType(expression, resultType, expectedType!);
                            }
                        }
                        else if (expression.Kind == SyntaxKind.MatchType)
                        {
                            continues = CheckMatchType(expression, scope, function);
                        }
                        else if (expression.Kind == SyntaxKind.If)
                        {
                            continues = CheckIf(expression, scope, function);
                        }
                        else
                        {
                            CheckExpression(expression, scope);
                        }
                        break;
                    case SyntaxKind.While:
                        CheckWhile(statement, scope, function);
                        break;
                    case SyntaxKind.Break:
                    case SyntaxKind.Continue:
                        string transfer = statement.Kind == SyntaxKind.Break ? "break" : "continue";
                        if (loops.Count == 0)
                        {
                            Fail(function.Declaration!, "LYC315", $"{transfer} requires an enclosing loop.");
                        }
                        var loop = loops.Peek();
                        HashSet<SemanticSymbol> destination = statement.Kind == SyntaxKind.Break
                            ? loop.Exits : loop.Back;
                        destination.UnionWith(possiblyAssigned);
                        if (statement.Kind == SyntaxKind.Break)
                        {
                            loop.ExitDefinite.Add(new(definitelyAssigned));
                        }
                        continues = false;
                        break;
                    case SyntaxKind.Return:
                        SemanticType actual = statement.ChildCount() == 0
                            ? analysis.ScalarType(ScalarKind.Unit)
                            : CheckExpression(statement.ChildAt(0), scope, function.Type);
                        // A value copy does not extend any contained target's lifetime.
                        // Reject wrappers too; the canonical query follows their fields.
                        if (SemanticTypes.HasContainedBorrows(actual))
                        {
                            Fail(statement, "LYC343", "Borrow-containing values must not be returned.");
                        }
                        RequireType(statement.ChildCount() == 0 ? function.Declaration! : statement.ChildAt(0),
                            actual, function.Type!);
                        RequireInitializedReceiver(function.Declaration!);
                        continues = false;
                        break;
                    default:
                        Unsupported(statement);
                        break;
                }
            }
            // Only temporary flow entries expire here. Persistent declarations
            // and their semantic uses remain owned by the analysis graph.
            if (!sharesScope)
            {
                definitelyAssigned.RemoveWhere(symbol => symbol.Scope == scope);
                possiblyAssigned.RemoveWhere(symbol => symbol.Scope == scope);
            }
            _ = new SemanticFact(analysis, block, resultType, null, canContinue: continues);
            return continues;
        }
        bool ExistsBeforeLoop(SemanticSymbol symbol, SemanticScope loopScope)
        {
            for (SemanticScope? ancestor = loopScope; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (symbol.Scope == ancestor)
                {
                    return true;
                }
            }
            return false;
        }
        void CheckWhile(SyntaxNode node, SemanticScope scope, SemanticSymbol function)
        {
            HashSet<SemanticSymbol> back = new();
            HashSet<SemanticSymbol> exits = new();
            List<HashSet<SemanticSymbol>> exitDefinite = new();
            List<(SemanticSymbol Symbol, SyntaxNode Target)> writes = new();
            // The condition executes again on a back-edge, so its immutable
            // writes belong to this loop too. Its first evaluation is mandatory;
            // snapshot outgoing initialization only after checking it.
            loops.Push((scope, back, exits, exitDefinite, writes));
            SyntaxNode condition = node.ChildAt(0);
            RequireType(condition, CheckExpression(condition, scope), analysis.ScalarType(ScalarKind.Bool));
            HashSet<SemanticSymbol> incomingDefinite = new(definitelyAssigned);
            HashSet<SemanticSymbol> incomingPossible = new(possiblyAssigned);
            bool bodyContinues = CheckBlock(node.ChildAt(1), scope, function);
            if (bodyContinues)
            {
                back.UnionWith(possiblyAssigned);
            }
            loops.Pop();
            // Any potentially assigned outer let reaching a back-edge could
            // encounter its assignment again. Body-local declarations instead
            // denote fresh storage each iteration and are not recorded here.
            foreach (var write in writes)
            {
                if (back.Contains(write.Symbol))
                {
                    Fail(write.Target, "LYC314", $"Loop may assign immutable binding '{write.Symbol.Name}' again.");
                }
            }
            // Do not use constant-condition optimization as an initialization
            // proof in this increment. Zero iterations preserve the entry path.
            definitelyAssigned = incomingDefinite;
            // A break in the condition can bypass assignments in its continuing
            // arm. Every actual break exit must agree with the false-condition
            // path before a binding is definitely initialized after the loop.
            foreach (HashSet<SemanticSymbol> exit in exitDefinite)
            {
                definitelyAssigned.IntersectWith(exit);
            }
            possiblyAssigned = incomingPossible;
            foreach (SemanticSymbol symbol in back)
            {
                if (ExistsBeforeLoop(symbol, scope))
                {
                    possiblyAssigned.Add(symbol);
                }
            }
            foreach (SemanticSymbol symbol in exits)
            {
                if (ExistsBeforeLoop(symbol, scope))
                {
                    possiblyAssigned.Add(symbol);
                }
            }
            _ = new SemanticFact(analysis, node, analysis.ScalarType(ScalarKind.Unit), null);
        }
        bool CheckMatchType(SyntaxNode node, SemanticScope scope, SemanticSymbol function,
            bool valueRequired = false, SemanticType? expectedType = null)
        {
            HashSet<string> variables = new();
            SyntaxNode parameters = node.ChildAt(0);
            for (int i = 0; i < parameters.ChildCount(); i += 1)
            {
                SyntaxNode parameter = parameters.ChildAt(i);
                string name = Text(parameter);
                RefuseDuplicate(scope, parameter, name);
                if (!variables.Add(name))
                {
                    Fail(parameter, "LYC302", $"Duplicate type-pattern variable '{name}'.");
                }
            }
            // Validate every pattern before choosing: a later misspelling must
            // not disappear merely because this specialization matches earlier.
            Dictionary<SyntaxNode, SemanticType> concrete = new();
            for (int i = 2; i < node.ChildCount(); i += 1)
            {
                Stack<SyntaxNode> pending = new();
                pending.Push(node.ChildAt(i).ChildAt(0));
                while (pending.Count != 0)
                {
                    SyntaxNode pattern = pending.Pop();
                    if (pattern.Kind == SyntaxKind.Wildcard)
                    {
                        continue;
                    }
                    if (pattern.Kind is SyntaxKind.TypeGroup or SyntaxKind.ArrayType)
                    {
                        for (int j = 0; j < pattern.ChildCount(); j += 1)
                        {
                            pending.Push(pattern.ChildAt(j));
                        }
                        continue;
                    }
                    if (pattern.Kind == SyntaxKind.TypeName)
                    {
                        string name = Text(pattern);
                        if (variables.Contains(name))
                        {
                            continue;
                        }
                        if (scope.Lookup(name) is null)
                        {
                            Fail(pattern, "LYC301", $"Unknown type '{name}'.");
                        }
                    }
                    concrete.Add(pattern, ReadType(pattern, scope, true));
                }
            }
            SemanticType subject = CheckUnevaluated(node.ChildAt(1), scope);
            for (int i = 2; i < node.ChildCount(); i += 1)
            {
                Dictionary<string, SemanticType> bindings = new();
                Stack<(SyntaxNode Pattern, SemanticType Type)> pending = new();
                pending.Push((node.ChildAt(i).ChildAt(0), subject));
                bool matches = true;
                while (matches && pending.Count != 0)
                {
                    var item = pending.Pop();
                    SyntaxNode pattern = item.Pattern;
                    SemanticType type = item.Type;
                    if (pattern.Kind == SyntaxKind.Wildcard)
                    {
                        continue;
                    }
                    if (pattern.Kind == SyntaxKind.TypeName && variables.Contains(Text(pattern)))
                    {
                        string name = Text(pattern);
                        if (bindings.TryGetValue(name, out SemanticType? previous))
                        {
                            matches = previous == type;
                        }
                        else
                        {
                            bindings.Add(name, type);
                        }
                    }
                    else if (pattern.Kind == SyntaxKind.ArrayType)
                    {
                        matches = type.Kind == SemanticTypeKind.Array;
                        if (matches)
                        {
                            pending.Push((pattern.ChildAt(0), type.Element!));
                        }
                    }
                    else if (pattern.Kind == SyntaxKind.TypeGroup)
                    {
                        if (pattern.ChildCount() == 1 && (pattern.TextOffset < 0 || Text(pattern) != ","))
                        {
                            pending.Push((pattern.ChildAt(0), type));
                            continue;
                        }
                        matches = type.Kind == SemanticTypeKind.Tuple
                            && pattern.ChildCount() == type.ComponentCount();
                        if (matches)
                        {
                            for (int j = pattern.ChildCount() - 1; j >= 0; j -= 1)
                            {
                                pending.Push((pattern.ChildAt(j), type.ComponentAt(j)));
                            }
                        }
                    }
                    else
                    {
                        matches = concrete[pattern] == type;
                    }
                }
                if (!matches)
                {
                    continue;
                }
                // Only the chosen arm acquires a scope and checked declarations.
                // These ordinary type symbols retain canonical reverse links.
                SemanticScope armScope = new(analysis, scope);
                foreach (var binding in bindings)
                {
                    _ = new SemanticSymbol(armScope, null, binding.Key,
                        SymbolKind.Type, binding.Value, false);
                }
                SyntaxNode block = node.ChildAt(i).ChildAt(1);
                bool continues = CheckBlock(block, armScope, function, valueRequired, expectedType);
                if (valueRequired && !continues)
                {
                    Unsupported(node);
                }
                SemanticType result = valueRequired
                    ? block.Fact!.Type : analysis.ScalarType(ScalarKind.Unit);
                _ = new SemanticFact(analysis, node, result, null,
                    canContinue: continues, selectedArmIndex: i);
                return continues;
            }
            Fail(node, "LYC346", "No matchtype arm matches the subject type.");
            return false;
        }
        bool CheckIf(SyntaxNode node, SemanticScope scope, SemanticSymbol function,
            bool valueRequired = false, SemanticType? expectedType = null)
        {
            if (valueRequired && node.ChildCount() != 3)
            {
                Fail(node, "LYC319", "Value-producing if requires an else branch.");
            }
            SyntaxNode condition = node.ChildAt(0);
            RequireType(condition, CheckExpression(condition, scope), analysis.ScalarType(ScalarKind.Bool));
            HashSet<SemanticSymbol> incomingDefinite = new(definitelyAssigned);
            HashSet<SemanticSymbol> incomingPossible = new(possiblyAssigned);

            bool thenContinues = CheckBlock(node.ChildAt(1), scope, function, valueRequired, expectedType);
            HashSet<SemanticSymbol> thenDefinite = definitelyAssigned;
            HashSet<SemanticSymbol> thenPossible = possiblyAssigned;

            // Check the alternative from the SAME incoming state, not the
            // mutated then-state. Missing else is an unchanged continuing path.
            definitelyAssigned = incomingDefinite;
            possiblyAssigned = incomingPossible;
            bool elseContinues = true;
            if (node.ChildCount() == 3)
            {
                SyntaxNode alternative = node.ChildAt(2);
                if (alternative.Kind == SyntaxKind.If)
                {
                    elseContinues = CheckIf(alternative, scope, function, valueRequired, expectedType);
                }
                else
                {
                    elseContinues = CheckBlock(alternative, scope, function, valueRequired, expectedType);
                }
            }
            if (thenContinues && elseContinues)
            {
                definitelyAssigned.IntersectWith(thenDefinite);
                possiblyAssigned.UnionWith(thenPossible);
            }
            else if (thenContinues)
            {
                definitelyAssigned = thenDefinite;
                possiblyAssigned = thenPossible;
            }
            else if (!elseContinues)
            {
                // No outgoing execution path; no following statement may use
                // either branch's temporary assignment state.
                definitelyAssigned.Clear();
                possiblyAssigned.Clear();
            }
            SemanticType resultType = analysis.ScalarType(ScalarKind.Unit);
            if (valueRequired)
            {
                if (!thenContinues && !elseContinues)
                {
                    // Propagating noncontinuation through arbitrary operand and
                    // argument positions is a later increment. Do not invent a
                    // unit value for a conditional that never produces a value.
                    Unsupported(node);
                }
                SemanticType thenType = node.ChildAt(1).Fact!.Type;
                SemanticType elseType = node.ChildAt(2).Fact!.Type;
                if (expectedType is not null)
                {
                    if (thenContinues)
                    {
                        RequireType(node.ChildAt(1), thenType, expectedType!);
                    }
                    if (elseContinues)
                    {
                        RequireType(node.ChildAt(2), elseType, expectedType!);
                    }
                    resultType = expectedType!;
                }
                else if (!thenContinues)
                {
                    resultType = elseType;
                }
                else if (!elseContinues || thenType == elseType)
                {
                    resultType = thenType;
                }
                else
                {
                    SemanticType? common = SemanticTypes.IsInteger(thenType) && SemanticTypes.IsInteger(elseType)
                        ? SemanticTypes.ArithmeticType(thenType, elseType) : null;
                    if (common is null)
                    {
                        Fail(node, "LYC320", $"Incompatible conditional branch types: {SemanticTypes.Name(thenType)} and {SemanticTypes.Name(elseType)}.");
                    }
                    resultType = common!;
                }
            }
            bool continues = thenContinues || elseContinues;
            _ = new SemanticFact(analysis, node, resultType, null, canContinue: continues);
            return continues;
        }

        string Text(SyntaxNode node)
        {
            return node.Source!.SourceText(node.TextOffset, node.TextLength);
        }
        void Fail(SyntaxNode node, string code, string message)
        {
            throw new SemanticFailure(node.Source!.SemanticDiagnostic(node, code, message));
        }
        void Unsupported(SyntaxNode node)
        {
            Fail(node, "LYC306", $"Unsupported semantic construct: {node.Kind}.");
        }
        void BindMultiClass(SyntaxNode implementation)
        {
            // Empty explicit bindings may select a relationship policy. Adapter
            // bodies remain unsupported; never silently discard an impl member.
            if (implementation.ChildCount() is not (2 or 3)
                || implementation.ChildAt(0).Kind != SyntaxKind.Name
                || implementation.ChildAt(1).Kind != SyntaxKind.TypeArguments
                || (implementation.ChildCount() == 3
                    && implementation.ChildAt(2).Kind != SyntaxKind.RelationKind))
            {
                Unsupported(implementation);
            }
            SyntaxNode name = implementation.ChildAt(0);
            SemanticSymbol? template = global.Lookup(Text(name));
            if (template?.Kind != SymbolKind.Interface)
            {
                Fail(name, "LYC301", $"Unknown multi-class interface '{Text(name)}'.");
            }
            RelationshipPolicy policy = RelationshipPolicy.None;
            if (implementation.ChildCount() == 3)
            {
                policy = Text(implementation.ChildAt(2)) == "owns"
                    ? RelationshipPolicy.Owns : RelationshipPolicy.Refs;
            }
            bool relationship = template!.ParticipantRoleAt(0) != ParticipantRole.None;
            if (relationship != (policy != RelationshipPolicy.None))
            {
                Fail(implementation, "LYC304", "Relationship roles require an owns or refs binding; ordinary interfaces have no policy.");
            }
            SyntaxNode arguments = implementation.ChildAt(1);
            if (arguments.ChildCount() != template!.ParticipantArity())
            {
                Fail(arguments, "LYC303", "Participant argument count does not match the interface.");
            }
            List<SemanticSymbol> endpoints = new();
            List<string?> labels = new();
            for (int index = 0; index < arguments.ChildCount(); index += 1)
            {
                SyntaxNode argument = arguments.ChildAt(index);
                string? label = null;
                SyntaxNode typeSyntax = argument;
                if (argument.Kind == SyntaxKind.RoleBinding)
                {
                    label = Text(argument);
                    typeSyntax = argument.ChildAt(0);
                }
                SemanticType type = ReadType(typeSyntax, global);
                // Class navigation follows nullable wrappers. Participant admission
                // instead requires the concrete nominal identity itself.
                if (type.Kind != SemanticTypeKind.Nominal || type.Class is null)
                {
                    Fail(typeSyntax, "LYC304", "A participant requires a concrete class.");
                }
                if (policy == RelationshipPolicy.Owns
                    && template.ParticipantRoleAt(index) == ParticipantRole.Child
                    && type.Class!.IsCountedClass())
                {
                    Fail(typeSyntax, "LYC304", "An owning relationship requires an ordinary child class.");
                }
                endpoints.Add(type.Class!);
                labels.Add(label);
            }
            if (analysis.FindMultiClassInstantiation(template, endpoints, labels, policy) is not null)
            {
                Fail(implementation, "LYC302", "Duplicate multi-class instantiation.");
            }
            SyntaxNode original = template.Declaration!;
            SyntaxNode parameters = original.ChildAt(0);
            // Temporary admission inventory. All prospective names are checked
            // before constructing an owner, namespace, or contributed symbol.
            Dictionary<SemanticSymbol, HashSet<string>> reserved = new();
            for (int index = 0; index < endpoints.Count; index += 1)
            {
                SemanticSymbol endpoint = endpoints[index];
                if (!reserved.TryGetValue(endpoint, out HashSet<string>? names))
                {
                    names = new();
                    reserved.Add(endpoint, names);
                }
                string? label = labels[index];
                if (label is not null
                    && (!names.Add(label) || endpoint.MemberScope!.LookupMember(label) is not null
                        || endpoint.FindParticipantLabel(label) is not null))
                {
                    Fail(arguments.ChildAt(index), "LYC302", $"Member scope '{label}' already exists.");
                }
                for (int blockIndex = 0; blockIndex < original.ChildCount(); blockIndex += 1)
                {
                    SyntaxNode block = original.ChildAt(blockIndex);
                    if (block.Kind != SyntaxKind.Class || Text(block) != Text(parameters.ChildAt(index)))
                    {
                        continue;
                    }
                    for (int memberIndex = 0; memberIndex < block.ChildCount(); memberIndex += 1)
                    {
                        SyntaxNode member = block.ChildAt(memberIndex);
                        if (member.Kind == SyntaxKind.DestructorFragment)
                        {
                            // Hidden teardown bodies have identity, not source names.
                            continue;
                        }
                        if (member.Kind is not (SyntaxKind.Function or SyntaxKind.Field))
                        {
                            Unsupported(member);
                        }
                        SyntaxNode memberName = member.Kind == SyntaxKind.Field
                            ? member : member.ChildAt(0);
                        string text = Text(memberName);
                        if (label is null
                            && (!names.Add(text) || endpoint.MemberScope!.LookupMember(text) is not null))
                        {
                            Fail(memberName, "LYC302", $"Contributed member '{text}' already exists.");
                        }
                    }
                }
            }
            MultiClassInstantiation instance = new(template, endpoints, labels, policy);
            try
            {
                // Field annotations share the template's private role environment,
                // not a concrete class's public member namespace. Its scope and
                // aliases retire with this instantiation, including on failure.
                SemanticScope annotations = new(instance);
                for (int role = 0; role < endpoints.Count; role += 1)
                {
                    SemanticSymbol binding = new(annotations, null, Text(parameters.ChildAt(role)),
                        SymbolKind.Type, analysis.ClassType(endpoints[role]), false);
                    instance.ParticipantAt(role).AddViewSymbol(binding);
                }
                SyntaxNode root = new(original.Source!, original.Kind,
                    original.TextOffset, original.TextLength, multiClass: instance);
                Stack<(SyntaxNode Original, SyntaxNode Copy, int Next)> pending = new();
                pending.Push((original, root, 0));
                while (pending.Count != 0)
                {
                    var frame = pending.Pop();
                    if (frame.Next == frame.Original.ChildCount())
                    {
                        continue;
                    }
                    pending.Push((frame.Original, frame.Copy, frame.Next + 1));
                    SyntaxNode source = frame.Original.ChildAt(frame.Next);
                    SyntaxNode copy = new(source.Source!, source.Kind,
                        source.TextOffset, source.TextLength, multiClass: instance);
                    frame.Copy.AddChild(copy);
                    pending.Push((source, copy, 0));
                }
                for (int index = 0; index < endpoints.Count; index += 1)
                {
                    SemanticScope placement = endpoints[index].MemberScope!;
                    if (labels[index] is string label)
                    {
                        SemanticSymbol memberNamespace = new(placement, null, label,
                            SymbolKind.MemberNamespace, null, false, multiClass: instance);
                        placement = new SemanticScope(memberNamespace);
                    }
                    for (int blockIndex = 0; blockIndex < root.ChildCount(); blockIndex += 1)
                    {
                        SyntaxNode block = root.ChildAt(blockIndex);
                        if (block.Kind != SyntaxKind.Class || Text(block) != Text(parameters.ChildAt(index)))
                        {
                            continue;
                        }
                        for (int memberIndex = 0; memberIndex < block.ChildCount(); memberIndex += 1)
                        {
                            SyntaxNode member = block.ChildAt(memberIndex);
                            if (member.Kind == SyntaxKind.DestructorFragment)
                            {
                                string selectedPolicy = policy == RelationshipPolicy.Owns ? "owns" : "refs";
                                if (Text(member.ChildAt(1)) != selectedPolicy)
                                {
                                    continue;
                                }
                            }
                            if (member.Kind == SyntaxKind.Field)
                            {
                                // Match ordinary field representation admission.
                                // The post-registration persistent-reference check
                                // also visits these contributed physical fields.
                                if (member.ChildCount() != 1)
                                {
                                    Unsupported(member);
                                }
                                SemanticType fieldType = ReadType(member.ChildAt(0), annotations);
                                if ((fieldType.Class is not null && fieldType.ArrayDepth > 0)
                                    || fieldType == analysis.ScalarType(ScalarKind.Unit))
                                {
                                    Unsupported(member);
                                }
                                SemanticSymbol field = new(placement, member, Text(member),
                                    SymbolKind.Field, fieldType, true, multiClass: instance);
                                instance.ParticipantAt(index).AddMember(field);
                                TypeView(member.ChildAt(0), annotations)?.AddViewSymbol(field);
                                continue;
                            }
                            SemanticSymbol method = new(placement, member, Text(member.ChildAt(0)),
                                SymbolKind.Function, analysis.ScalarType(ScalarKind.Unit), false,
                                multiClass: instance);
                            instance.ParticipantAt(index).AddMember(method);
                            SemanticScope scope = new(method);
                            for (int role = 0; role < endpoints.Count; role += 1)
                            {
                                SemanticSymbol binding = new(scope, null, Text(parameters.ChildAt(role)),
                                    SymbolKind.Type, analysis.ClassType(endpoints[role]), false);
                                instance.ParticipantAt(role).AddViewSymbol(binding);
                            }
                            functions.Add(method);
                        }
                    }
                }
                instance.SealSyntax();
            }
            catch
            {
                // No partially published contributions survive a failed build.
                instance.Destroy();
                throw;
            }
        }
        void ValidateMultiClassTemplate(SemanticSymbol template)
        {
            SyntaxNode declaration = template.Declaration!;
            Dictionary<string, HashSet<string>> participants = new();
            int parents = 0;
            int children = 0;
            for (int index = 0; index < declaration.ChildCount(); index += 1)
            {
                SyntaxNode child = declaration.ChildAt(index);
                if (child.Kind != SyntaxKind.TypeParameters)
                {
                    continue;
                }
                for (int position = 0; position < child.ChildCount(); position += 1)
                {
                    SyntaxNode parameter = child.ChildAt(position);
                    if (parameter.ChildCount() > 1
                        || (parameter.ChildCount() == 1
                            && parameter.ChildAt(0).Kind != SyntaxKind.Modifier))
                    {
                        Unsupported(parameter);
                    }
                    ParticipantRole role = template.ParticipantRoleAt(position);
                    if (role == ParticipantRole.Parent)
                    {
                        parents += 1;
                    }
                    if (role == ParticipantRole.Child)
                    {
                        children += 1;
                    }
                    string name = Text(parameter);
                    if (participants.ContainsKey(name) || global.Lookup(name) is not null)
                    {
                        Fail(parameter, "LYC302", "Duplicate or shadowing participant name.");
                    }
                    participants.Add(name, new HashSet<string>());
                }
            }
            if (participants.Count == 0)
            {
                Unsupported(declaration);
            }
            if ((parents != 0 || children != 0)
                && (parents != 1 || children != 1 || participants.Count != 2))
            {
                Fail(declaration, "LYC303", "A relationship requires exactly one parent and one child participant.");
            }
            for (int index = 0; index < declaration.ChildCount(); index += 1)
            {
                SyntaxNode participant = declaration.ChildAt(index);
                if (participant.Kind is SyntaxKind.TypeParameters or SyntaxKind.Visibility)
                {
                    continue;
                }
                if (participant.Kind != SyntaxKind.Class)
                {
                    Unsupported(participant);
                }
                string name = Text(participant);
                if (!participants.TryGetValue(name, out HashSet<string>? members))
                {
                    Fail(participant, "LYC301", $"Unknown interface participant '{name}'.");
                }
                for (int memberIndex = 0; memberIndex < participant.ChildCount(); memberIndex += 1)
                {
                    SyntaxNode member = participant.ChildAt(memberIndex);
                    SyntaxNode nameNode = member;
                    if (member.Kind == SyntaxKind.DestructorFragment)
                    {
                        if (parents != 1 || children != 1)
                        {
                            Fail(member, "LYC303", "Destructor fragments require parent and child roles.");
                        }
                        // Policy-qualified fragments share no ordinary member name.
                        // Reserve a private validation key only to reject duplicates
                        // across repeated blocks for the same participant.
                        string fragmentKey = "partial " + Text(member.ChildAt(1));
                        if (!members!.Add(fragmentKey))
                        {
                            Fail(member, "LYC302", "Duplicate destructor fragment policy.");
                        }
                        continue;
                    }
                    if (member.Kind == SyntaxKind.Function)
                    {
                        ValidateRelationshipOperation(member, parents == 1 && children == 1);
                        nameNode = member.ChildAt(0);
                        if (nameNode.Kind != SyntaxKind.Name)
                        {
                            Unsupported(nameNode);
                        }
                    }
                    else if (member.Kind != SyntaxKind.Field)
                    {
                        Unsupported(member);
                    }
                    string memberName = Text(nameNode);
                    if (!members!.Add(memberName))
                    {
                        Fail(nameNode, "LYC302", $"Duplicate contributed member '{memberName}'.");
                    }
                }
            }
        }
        // Validate the source boundary even for an unused template. This is
        // not proof of the trusted link algorithm: resolved managed writes and
        // membership accounting are separate obligations of specialization.
        void ValidateRelationshipOperation(SyntaxNode declaration, bool relationship)
        {
            SyntaxNode? operation = null;
            SyntaxNode? body = null;
            for (int index = 0; index < declaration.ChildCount(); index += 1)
            {
                SyntaxNode child = declaration.ChildAt(index);
                if (child.Kind == SyntaxKind.Modifier && Text(child) is "add" or "remove")
                {
                    if (operation is not null)
                    {
                        Fail(child, "LYC303", "A method has only one relationship operation.");
                    }
                    operation = child;
                }
                if (child.Kind == SyntaxKind.Block)
                {
                    body = child;
                }
            }
            if (operation is null)
            {
                return;
            }
            if (!relationship)
            {
                Fail(operation, "LYC303", "Relationship operations require parent and child roles.");
            }
            if (body is null || body.ChildCount() == 0 || body.ChildAt(0).Kind != SyntaxKind.Prepare)
            {
                Fail(operation, "LYC303", "A relationship operation must begin with prepare.");
            }
            SyntaxNode preparation = body!.ChildAt(0);
            Stack<(SyntaxNode Node, bool Structural)> pending = new();
            for (int index = body.ChildCount() - 1; index >= 0; index -= 1)
            {
                pending.Push((body.ChildAt(index), index != 0));
            }
            while (pending.Count != 0)
            {
                (SyntaxNode node, bool structural) = pending.Pop();
                if (node.Kind == SyntaxKind.Prepare && node != preparation)
                {
                    Fail(node, "LYC303", "Only the initial prepare block is permitted.");
                }
                if (structural && node.Kind == SyntaxKind.Return)
                {
                    Fail(node, "LYC303", "A structural relationship body cannot return early.");
                }
                for (int index = node.ChildCount() - 1; index >= 0; index -= 1)
                {
                    pending.Push((node.ChildAt(index), structural));
                }
            }
        }
        void RefuseDuplicate(SemanticScope scope, SyntaxNode node, string name)
        {
            SemanticSymbol? existing = scope.MemberOwner is null
                ? scope.Lookup(name)
                : scope.LookupMember(name);
            if (existing is not null)
            {
                Fail(node, "LYC302", $"Name '{name}' is already visible.");
            }
        }
        void MarkAssigned(SemanticSymbol symbol)
        {
            definitelyAssigned.Add(symbol);
            possiblyAssigned.Add(symbol);
        }
        void RequireType(SyntaxNode node, SemanticType actual, SemanticType expected)
        {
            if (!SemanticTypes.CanWiden(actual, expected))
            {
                Fail(node, "LYC304", $"Expected {SemanticTypes.Name(expected)}, got {SemanticTypes.Name(actual)}.");
            }
        }
        // Local references select existing writable storage. A separate control-
        // flow pass rejects use after backing invalidation; acquisition itself
        // also performs the usual initialized-read and index checks.
        SemanticType CheckReference(SyntaxNode node, SemanticScope scope)
        {
            if (node.Kind == SyntaxKind.Group)
            {
                SemanticType grouped = CheckReference(node.ChildAt(0), scope);
                _ = new SemanticFact(analysis, node, grouped, node.ChildAt(0).Fact!.Symbol);
                return grouped;
            }
            if (node.Kind != SyntaxKind.Unary || Text(node) != "ref")
            {
                Fail(node, "LYC325", "Reference argument requires explicit ref storage.");
            }
            SyntaxNode storage = node.ChildAt(0);
            if (storage.Kind == SyntaxKind.Member)
            {
                if (int.TryParse(Text(storage), out _))
                {
                    SemanticType component = CheckExpression(storage, scope);
                    SemanticSymbol owner = WritableRoot(storage, scope);
                    _ = new SemanticFact(analysis, node, component, owner);
                    return component;
                }
                SemanticSymbol field = CheckField(storage, scope, true);
                _ = WritableRoot(storage, scope);
                SemanticType fieldType = field.Type!;
                _ = new SemanticFact(analysis, node, fieldType, field);
                return fieldType;
            }
            if (storage.Kind is not (SyntaxKind.Name or SyntaxKind.Index))
            {
                Unsupported(storage);
            }
            SemanticType type = CheckExpression(storage, scope);
            SemanticSymbol symbol = WritableRoot(storage, scope);
            _ = new SemanticFact(analysis, node, type, symbol);
            return type;
        }
        // A pattern introduces all of its names after one initializer evaluation.
        // Validate names and shape before attaching any leaf declarations.
        void CheckDestructuring(SyntaxNode node, SemanticScope scope)
        {
            int next = 1;
            SemanticType? expected = null;
            if (next < node.ChildCount() && node.ChildAt(next).Kind == SyntaxKind.Annotation)
            {
                expected = ReadType(node.ChildAt(next).ChildAt(0), scope, true);
                next += 1;
            }
            if (next != node.ChildCount() - 1)
            {
                Fail(node, "LYC304", "Destructuring requires one initializer.");
            }
            SyntaxNode initializer = node.ChildAt(next);
            SemanticType actual = CheckExpression(initializer, scope, expected);
            if (expected is not null)
            {
                RequireType(initializer, actual, expected);
            }
            Stack<(SyntaxNode Pattern, SemanticType Type)> pending = new();
            List<(SyntaxNode Pattern, SemanticType Type, string Name)> leaves = new();
            HashSet<string> names = new(StringComparer.Ordinal);
            pending.Push((node.ChildAt(0), actual));
            while (pending.Count > 0)
            {
                (SyntaxNode pattern, SemanticType type) = pending.Pop();
                if (pattern.Kind == SyntaxKind.Wildcard)
                {
                    continue;
                }
                if (pattern.Kind == SyntaxKind.PatternTuple)
                {
                    if (type.Kind != SemanticTypeKind.Tuple || pattern.ChildCount() != type.ComponentCount())
                    {
                        Fail(pattern, "LYC304", "Tuple pattern must match the initializer shape exactly.");
                    }
                    for (int i = pattern.ChildCount() - 1; i >= 0; i -= 1)
                    {
                        pending.Push((pattern.ChildAt(i), type.ComponentAt(i)));
                    }
                    continue;
                }
                if (pattern.Kind != SyntaxKind.PatternName || pattern.ChildCount() != 1
                    || pattern.ChildAt(0).Kind != SyntaxKind.Name)
                {
                    Unsupported(pattern);
                }
                string name = Text(pattern.ChildAt(0));
                RefuseDuplicate(scope, pattern, name);
                if (!names.Add(name))
                {
                    Fail(pattern, "LYC302", $"Name '{name}' occurs more than once in this pattern.");
                }
                leaves.Add((pattern, type, name));
            }
            foreach ((SyntaxNode pattern, SemanticType type, string name) in leaves)
            {
                SemanticSymbol symbol = new(scope, pattern, name, SymbolKind.Local,
                    type, Text(node) == "var");
                MarkAssigned(symbol);
            }
        }
        void CheckBinding(SyntaxNode node, SemanticScope scope)
        {
            SyntaxNode pattern = node.ChildAt(0);
            if (pattern.Kind is SyntaxKind.PatternTuple or SyntaxKind.Wildcard)
            {
                CheckDestructuring(node, scope);
                return;
            }
            if (pattern.Kind != SyntaxKind.PatternName || pattern.ChildCount() != 1
                || pattern.ChildAt(0).Kind != SyntaxKind.Name)
            {
                Unsupported(pattern);
            }
            SyntaxNode nameNode = pattern.ChildAt(0);
            string name = Text(nameNode);
            RefuseDuplicate(scope, nameNode, name);
            int next = 1;
            SemanticType? declaredType = null;
            ParticipantBinding? declaredView = null;
            bool hasAnnotation = false;
            bool referenceAnnotation = false;
            if (next < node.ChildCount() && node.ChildAt(next).Kind == SyntaxKind.Annotation)
            {
                SyntaxNode annotation = node.ChildAt(next).ChildAt(0);
                referenceAnnotation = annotation.Kind == SyntaxKind.TypePrefix && Text(annotation) == "ref";
                declaredType = ReadType(referenceAnnotation ? annotation.ChildAt(0) : annotation, scope, true);
                declaredView = TypeView(annotation, scope);
                hasAnnotation = true;
                next += 1;
            }
            SyntaxNode? initializer = next < node.ChildCount() ? node.ChildAt(next) : null;
            bool referenceInitializer = initializer is not null
                && initializer.Kind == SyntaxKind.Unary && Text(initializer) == "ref";
            bool isReference = referenceAnnotation || referenceInitializer;
            if (isReference && (Text(node) != "let" || initializer is null
                || (declaredType is not null && !referenceAnnotation)))
            {
                Fail(nameNode, "LYC325", "A reference local requires let and a reference initializer.");
            }
            if (initializer is not null)
            {
                if (next + 1 != node.ChildCount())
                {
                    Unsupported(node.ChildAt(next + 1));
                }
                // Evaluate before registering the name: locals are visible only
                // after their declaration, never forward or self-initialized.
                SemanticType actual = isReference
                    ? CheckReference(initializer, scope)
                    : CheckExpression(initializer, scope, declaredType);
                if (isReference && declaredType is not null && actual != declaredType!)
                {
                    Fail(initializer, "LYC325", "Reference storage type must match exactly.");
                }
                if (declaredType is not null)
                {
                    RequireType(initializer, actual, declaredType!);
                }
                else
                {
                    declaredType = actual;
                }
            }
            SemanticSymbol symbol = new(scope, node, name, SymbolKind.Local,
                declaredType!, isReference || Text(node) == "var", isReference: isReference);
            ParticipantBinding? localView = hasAnnotation ? declaredView : initializer?.Fact?.ParticipantView;
            localView?.AddViewSymbol(symbol);
            if (initializer is not null)
            {
                MarkAssigned(symbol);
            }
        }
        // Constructor bodies initialize the actual self object. Other objects of
        // the same class do not share its definite-assignment state.
        bool IsConstructor()
        {
            SemanticSymbol? owner = checkingFunction?.Scope?.Class;
            return owner is not null && checkingFunction!.Name == owner.Name;
        }
        bool IsConstructingSelf(SemanticSymbol? symbol)
        {
            return IsConstructor()
                && symbol == checkingFunction!.FunctionScope!.ParameterAt(0);
        }
        void RequireInitializedReceiver(SyntaxNode location)
        {
            if (unevaluated || !IsConstructor())
            {
                return;
            }
            foreach (SemanticSymbol field in checkingFunction!.Scope!.Class!.MemberScope!.Fields())
            {
                if (!definitelyAssigned.Contains(field))
                {
                    Fail(location, "LYC328", $"Field '{field.Name}' is not definitely initialized.");
                }
            }
        }
        // Only grouping and namespace selection may expose partial self for a
        // direct field access. Validate the whole path before attaching facts;
        // calls and ordinary field reads must use normal initialization checks.
        SemanticSymbol? ConstructorReceiver(SyntaxNode node, SemanticScope scope, bool attach)
        {
            if (!IsConstructor())
            {
                return null;
            }
            List<SyntaxNode> path = new();
            SyntaxNode root = node;
            while (root.Kind is SyntaxKind.Group or SyntaxKind.Member)
            {
                path.Add(root);
                root = root.ChildAt(0);
            }
            SemanticSymbol? self = root.Kind == SyntaxKind.Name ? scope.Lookup(Text(root)) : null;
            if (!IsConstructingSelf(self))
            {
                return null;
            }
            SemanticScope placement = self!.Type!.Class!.MemberScope!;
            List<SemanticSymbol> symbols = new();
            SemanticSymbol selected = self;
            for (int index = path.Count - 1; index >= 0; index -= 1)
            {
                SyntaxNode part = path[index];
                if (part.Kind == SyntaxKind.Member)
                {
                    SemanticSymbol? label = placement.LookupMember(Text(part));
                    if (label?.Kind != SymbolKind.MemberNamespace)
                    {
                        return null;
                    }
                    selected = label;
                    placement = label.MemberScope!;
                }
                symbols.Add(selected);
            }
            if (attach)
            {
                _ = new SemanticFact(analysis, root, self.Type!, self);
                for (int index = path.Count - 1; index >= 0; index -= 1)
                {
                    _ = new SemanticFact(analysis, path[index], self.Type!, symbols[path.Count - 1 - index]);
                }
            }
            return self;
        }
        SemanticSymbol CheckField(SyntaxNode node, SemanticScope scope, bool read)
        {
            SyntaxNode receiver = node.ChildAt(0);
            SemanticType receiverType;
            SemanticSymbol? receiverSymbol = ConstructorReceiver(receiver, scope, attach: true);
            if (receiverSymbol is not null)
            {
                // Selecting a field does not expose the unfinished whole object.
                receiverType = receiverSymbol.Type!;
            }
            else
            {
                receiverType = CheckExpression(receiver, scope);
            }
            if (receiverType.Kind == SemanticTypeKind.Nullable)
            {
                Fail(receiver, "LYC304", "Nullable field access requires checked unwrap.");
            }
            SemanticScope? selectedScope = receiver.Fact?.Symbol?.Kind == SymbolKind.MemberNamespace
                ? receiver.Fact.Symbol.MemberScope : null;
            SemanticSymbol? field = receiverType.ArrayDepth == 0
                ? selectedScope is not null ? selectedScope.LookupMember(Text(node))
                    : receiver.Fact?.ParticipantView?.FindMember(Text(node))
                        ?? receiverType.Declaration?.MemberScope?.LookupMember(Text(node))
                : null;
            if (read && field?.Kind == SymbolKind.MemberNamespace)
            {
                // Direct field selection constructs its namespace facts through
                // ConstructorReceiver. A standalone namespace still exposes the
                // whole handle and cannot bypass partial-self restrictions.
                if (receiverSymbol is not null)
                {
                    RequireInitializedReceiver(node);
                }
                // A placement label selects members, not a new runtime object.
                _ = new SemanticFact(analysis, node, receiverType, field);
                return field;
            }
            if (field?.Kind != SymbolKind.Field)
            {
                Fail(node, "LYC329", $"No field '{Text(node)}' exists on {SemanticTypes.Name(receiverType)}.");
            }
            if (!unevaluated && read && IsConstructingSelf(receiverSymbol) && !definitelyAssigned.Contains(field!))
            {
                Fail(node, "LYC328", $"Field '{field!.Name}' is not definitely initialized.");
            }
            SemanticFact fieldFact = new(analysis, node, field!.Type!, field);
            field.ParticipantView?.AddViewFact(fieldFact);
            return field;
        }
        SemanticSymbol WritableRoot(SyntaxNode node, SemanticScope scope)
        {
            SyntaxNode root = node;
            while (root.Kind is SyntaxKind.Index or SyntaxKind.Group
                || (root.Kind == SyntaxKind.Member
                    && (root.ChildAt(0).Fact?.Type.Kind == SemanticTypeKind.Tuple
                        || root.ChildAt(0).Fact?.Type.Declaration?.Kind == SymbolKind.Struct)))
            {
                // A reference field selects its referent, not its pointer slot.
                // Copying the alias into an immutable holder does not revoke the
                // writable-storage permission established by CheckReference.
                if (root.Kind == SyntaxKind.Member && root.Fact?.Symbol?.IsReference == true)
                {
                    break;
                }
                // Ordinary inline fields retain the mutability of their root.
                // Class fields still have their own declared mutable symbol.
                root = root.ChildAt(0);
            }
            if (root.Kind is not (SyntaxKind.Name or SyntaxKind.Member))
            {
                Unsupported(node);
            }
            // Selection has already been checked: reuse its authoritative field
            // identity rather than checking the receiver again or looking up a name.
            SemanticSymbol? symbol = root.Kind == SyntaxKind.Member
                ? root.Fact!.Symbol : scope.Lookup(Text(root));
            if (symbol is null)
            {
                Fail(root, "LYC301", $"Unknown name '{Text(root)}'.");
            }
            if (protectedArrays.Contains(symbol!))
            {
                Fail(root, "LYC324", "Cannot modify an array while its storage is selected.");
            }
            if (!symbol!.Mutable)
            {
                Fail(root, "LYC308", $"Cannot modify immutable binding '{symbol.Name}'.");
            }
            if (symbol.IsManagedLink()
                && checkingFunction?.ContributingParticipant?.Instantiation
                    != symbol.ContributingParticipant!.Instantiation)
            {
                Fail(root, "LYC308", "Managed links are writable only inside their relationship implementation.");
            }
            return symbol;
        }
        void CheckAssignment(SyntaxNode node, SemanticScope scope)
        {
            SyntaxNode target = node.ChildAt(0);
            string assignment = Text(node);
            bool compound = assignment != "=";
            // Numeric selectors name inline tuple components, not class fields.
            // Use the ordinary checked-storage path once, preserving class
            // constructors' separate partial-self initialization rules.
            bool tupleComponent = target.Kind == SyntaxKind.Member
                && int.TryParse(Text(target), out _);
            if (target.Kind == SyntaxKind.Member && !tupleComponent)
            {
                if (compound)
                {
                    Unsupported(node);
                }
                SemanticSymbol field = CheckField(target, scope, false);
                _ = WritableRoot(target, scope);
                SemanticType supplied = CheckExpression(node.ChildAt(1), scope, field.Type);
                RequireType(node.ChildAt(1), supplied, field.Type!);
                if (ConstructorReceiver(target.ChildAt(0), scope, attach: false) is not null)
                {
                    MarkAssigned(field);
                }
                return;
            }
            if (target.Kind == SyntaxKind.Index || tupleComponent)
            {
                if (compound)
                {
                    Unsupported(node);
                }
                SemanticType selected = CheckExpression(target, scope);
                SemanticSymbol owner = WritableRoot(target, scope);
                protectedArrays.Push(owner);
                SemanticType supplied = CheckExpression(node.ChildAt(1), scope, selected);
                RequireAggregateElementView(node.ChildAt(1), node.ChildAt(1).Fact!.ParticipantView);
                RequireType(node.ChildAt(1), supplied, selected);
                protectedArrays.Pop();
                return;
            }
            if (assignment is not ("=" or "+=" or "-=" or "*=" or "/=" or "!+=" or "!-=" or "!*=")
                || target.Kind != SyntaxKind.Name)
            {
                Unsupported(node);
            }
            SemanticSymbol? symbol = scope.Lookup(Text(target));
            if (symbol is null)
            {
                Fail(target, "LYC301", $"Unknown name '{Text(target)}'.");
            }
            if (symbol!.Kind is not (SymbolKind.Local or SymbolKind.Parameter))
            {
                Unsupported(target);
            }
            if (protectedArrays.Contains(symbol))
            {
                Fail(target, "LYC324", "Cannot modify an array while its storage is selected.");
            }
            if (!symbol.Mutable && possiblyAssigned.Contains(symbol))
            {
                Fail(target, "LYC308", $"Cannot assign immutable binding '{symbol.Name}' again.");
            }
            if (!unevaluated && compound && !definitelyAssigned.Contains(symbol))
            {
                Fail(target, "LYC307", $"Local '{symbol.Name}' is not definitely assigned.");
            }
            SyntaxNode value = node.ChildAt(1);
            SemanticType actual = CheckExpression(value, scope, symbol.Type);
            if (compound)
            {
                SemanticType targetType = symbol.Type!;
                if (!SemanticTypes.IsInteger(targetType) || !SemanticTypes.IsInteger(actual))
                {
                    Fail(node, "LYC312", "Integer arithmetic requires integer operands.");
                }
                SemanticType? result = SemanticTypes.ArithmeticType(targetType, actual);
                if (result is null)
                {
                    Fail(node, "LYC312", $"No integer arithmetic type contains both {SemanticTypes.Name(targetType)} and {SemanticTypes.Name(actual)}.");
                }
                // Assignment does not introduce an implicit narrowing conversion.
                RequireType(value, result!, targetType);
                if (!unsafeMode && assignment == "/=" && value.Fact!.IntegerConstant == BigInteger.Zero)
                {
                    Fail(node, "LYC316", "Integer division or remainder by zero.");
                }
            }
            else
            {
                RequireType(value, actual, symbol.Type!);
            }
            // Resolving the destination did not perform the store. RHS branches
            // may have initialized it meanwhile; only continuing paths reach this
            // write. Do not mark it assigned early, which would permit RHS reads.
            if (!symbol.Mutable && possiblyAssigned.Contains(symbol))
            {
                Fail(target, "LYC308", $"Cannot assign immutable binding '{symbol.Name}' again.");
            }
            // Retain the resolved storage location for emission. Plain assignment
            // need not read it; compound assignment proved that read valid above.
            _ = new SemanticFact(analysis, target, symbol.Type!, symbol);
            if (!symbol.Mutable)
            {
                foreach (var loop in loops)
                {
                    if (ExistsBeforeLoop(symbol, loop.Scope))
                    {
                        loop.Writes.Add((symbol, target));
                    }
                }
            }
            MarkAssigned(symbol);
        }
        void RequireTupleSlice(SyntaxNode node, SemanticType type)
        {
            // Tuple composition does not enable the still-unsupported floating
            // point backend. Check nested components as well as immediate ones.
            Stack<SemanticType> pending = new();
            pending.Push(type);
            while (pending.Count != 0)
            {
                SemanticType current = pending.Pop();
                if (current.Kind == SemanticTypeKind.Tuple)
                {
                    for (int i = 0; i < current.ComponentCount(); i += 1)
                    {
                        pending.Push(current.ComponentAt(i));
                    }
                }
                else if (current.Kind == SemanticTypeKind.Array)
                {
                    pending.Push(SemanticTypes.ElementOf(current));
                }
                else if (SemanticTypes.IsFloat(current))
                {
                    Unsupported(node);
                }
            }
        }
        // Validate array placement independently of inline layout. Arrays break
        // layout cycles, but do not stop the propagation of contained borrows.
        // Canonical type identity bounds this walk even for recursive nominal types.
        void RequireValidArrayStorage(SyntaxNode location, SemanticType root)
        {
            Stack<SemanticType> pending = new();
            HashSet<SemanticType> visited = new();
            pending.Push(root);
            while (pending.Count != 0)
            {
                SemanticType type = pending.Pop();
                if (!visited.Add(type))
                {
                    continue;
                }
                if (type.Kind == SemanticTypeKind.Array)
                {
                    if (SemanticTypes.HasContainedBorrows(type.Element!))
                    {
                        Fail(location, "LYC343", "Array elements must not contain borrowed references.");
                    }
                    pending.Push(type.Element!);
                }
                else if (type.Kind == SemanticTypeKind.Tuple)
                {
                    for (int i = 0; i < type.ComponentCount(); i += 1)
                    {
                        pending.Push(type.ComponentAt(i));
                    }
                }
                else if (type.Declaration?.Kind == SymbolKind.Struct)
                {
                    foreach (SemanticSymbol field in type.Declaration.MemberScope!.Fields())
                    {
                        pending.Push(field.Type!);
                    }
                }
            }
        }
        // Type queries share real checking while isolating runtime flow state.
        SemanticType CheckUnevaluated(SyntaxNode operand, SemanticScope scope)
        {
            // Reuse real expression checking, not hints. Isolate flow state:
            // assignments and control flow inside an unevaluated expression
            // must not initialize caller storage or exit a surrounding loop.
            var savedDefinite = definitelyAssigned;
            var savedPossible = possiblyAssigned;
            var savedLoops = loops;
            var savedProtected = protectedArrays;
            bool savedUnevaluated = unevaluated;
            definitelyAssigned = new(savedDefinite);
            possiblyAssigned = new(savedPossible);
            loops = new();
            protectedArrays = new();
            unevaluated = true;
            try
            {
                return CheckExpression(operand, scope);
            }
            finally
            {
                definitelyAssigned = savedDefinite;
                possiblyAssigned = savedPossible;
                loops = savedLoops;
                protectedArrays = savedProtected;
                unevaluated = savedUnevaluated;
            }
        }
        // Value signatures, local annotations and struct fields admit structs.
        // Class fields remain separately gated. Recursive calls preserve the
        // caller's admission; this does not grant an array-growth default.
        SemanticType ReadType(SyntaxNode node, SemanticScope scope, bool allowStructs = false)
        {
            if (node.Kind == SyntaxKind.OptionalType)
            {
                SemanticType payload = ReadType(node.ChildAt(0), scope, allowStructs);
                if (payload.Kind != SemanticTypeKind.Nominal || payload.Class is null)
                {
                    Fail(node, "LYC304", "Nullable types currently require a nonnullable class handle.");
                }
                return analysis.NullableType(payload);
            }
            if (node.Kind == SyntaxKind.TypeOf)
            {
                if (node.Fact is not null)
                {
                    return node.Fact.Type;
                }
                SemanticType result = CheckUnevaluated(node.ChildAt(0), scope);
                _ = new SemanticFact(analysis, node, result, null);
                return result;
            }
            if (node.Kind == SyntaxKind.TypeGroup)
            {
                if (node.ChildCount() == 1 && (node.TextOffset < 0 || Text(node) != ","))
                {
                    return ReadType(node.ChildAt(0), scope, allowStructs);
                }
                List<SemanticType> components = new();
                for (int i = 0; i < node.ChildCount(); i += 1)
                {
                    SyntaxNode component = node.ChildAt(i);
                    components.Add(ReadType(component, scope, allowStructs));
                    RequireAggregateElementView(component, TypeView(component, scope), annotation: true);
                }
                SemanticType tuple = analysis.TupleType(components);
                RequireTupleSlice(node, tuple);
                return tuple;
            }
            if (node.Kind == SyntaxKind.ArrayType && node.ChildCount() == 1)
            {
                SemanticType element = ReadType(node.ChildAt(0), scope, allowStructs);
                RequireAggregateElementView(node.ChildAt(0),
                    TypeView(node.ChildAt(0), scope), annotation: true);
                if (element.ArrayDepth == 0 && element != analysis.ScalarType(ScalarKind.Bool)
                    && !SemanticTypes.IsInteger(element) && element.Class is null
                    && !(allowStructs && (element.Kind == SemanticTypeKind.Tuple
                        || element.Declaration?.Kind == SymbolKind.Struct)))
                {
                    Unsupported(node);
                }
                SemanticType array = SemanticTypes.ArrayOf(element);
                RequireValidArrayStorage(node, array);
                return array;
            }
            if (node.Kind == SyntaxKind.TypeName)
            {
                SemanticSymbol? symbol = scope.Lookup(Text(node));
                if (symbol is null)
                {
                    Fail(node, "LYC301", $"Unknown type '{Text(node)}'.");
                }
                if (symbol?.Kind == SymbolKind.Type)
                {
                    return symbol.Type!;
                }
                if (allowStructs && symbol?.Kind == SymbolKind.Struct)
                {
                    return analysis.StructType(symbol);
                }
                if (symbol?.Kind == SymbolKind.Class)
                {
                    return analysis.ClassType(symbol);
                }
            }
            Unsupported(node);
            return analysis.ScalarType(ScalarKind.Unit);
        }
        SemanticType CheckInteger(SyntaxNode location, SyntaxNode literal,
            bool negative, SemanticType? expectedType, out BigInteger value)
        {
            string spelling = Text(literal);
            BigInteger magnitude = BigInteger.Zero;
            int position = 0;
            // The lexer already validated separators and suffix boundaries.
            // Accumulate without a host-width limit before checking Lyric range.
            while (position < spelling.Length)
            {
                char digit = spelling[position];
                if (digit == '_')
                {
                    position += 1;
                    continue;
                }
                if (digit < '0' || digit > '9')
                {
                    break;
                }
                magnitude = magnitude * 10 + (digit - '0');
                position += 1;
            }
            SemanticType type = analysis.ScalarType(ScalarKind.I32);
            if (position < spelling.Length)
            {
                string suffix = spelling[position..];
                foreach (SemanticType candidate in analysis.PredefinedTypes())
                {
                    if (SemanticTypes.IsInteger(candidate) && SemanticTypes.Name(candidate) == suffix)
                    {
                        type = candidate;
                        break;
                    }
                }
            }
            else if (expectedType is not null && SemanticTypes.IsInteger(expectedType!))
            {
                type = expectedType!;
            }
            value = negative ? -magnitude : magnitude;
            if (!SemanticTypes.Represents(type, value))
            {
                Fail(location, "LYC310", $"Integer literal is outside {SemanticTypes.Name(type)} range.");
            }
            return type;
        }
        BigInteger WrapInteger(SemanticType type, BigInteger value)
        {
            // Normalize the mathematical remainder, then interpret the sign bit.
            // BigInteger remainder alone keeps the dividend's sign.
            BigInteger modulus = BigInteger.One << SemanticTypes.IntegerWidth(type);
            BigInteger bits = value % modulus;
            if (bits < 0)
            {
                bits += modulus;
            }
            if (SemanticTypes.IsSigned(type) && bits > SemanticTypes.Maximum(type))
            {
                bits -= modulus;
            }
            return bits;
        }
        SemanticType? PreviewType(SyntaxNode node, SemanticScope scope,
            Dictionary<string, SemanticType?>? locals = null)
        {
            if (node.Kind == SyntaxKind.TypeOf)
            {
                if (node.Fact is not null)
                {
                    return node.Fact.Type;
                }
                SyntaxNode operand = node.ChildAt(0);
                return DiscoverHints(operand, scope, locals)[operand];
            }
            if (node.Kind == SyntaxKind.ArrayType)
            {
                SemanticType? element = PreviewType(node.ChildAt(0), scope, locals);
                return element is null ? null : SemanticTypes.ArrayOf(element);
            }
            if (node.Kind == SyntaxKind.TypeGroup)
            {
                if (node.ChildCount() == 1 && (node.TextOffset < 0 || Text(node) != ","))
                {
                    return PreviewType(node.ChildAt(0), scope, locals);
                }
                List<SemanticType> components = new();
                for (int index = 0; index < node.ChildCount(); index += 1)
                {
                    SemanticType? component = PreviewType(node.ChildAt(index), scope, locals);
                    if (component is null)
                    {
                        return null;
                    }
                    components.Add(component);
                }
                return analysis.TupleType(components);
            }
            return ReadType(node, scope, true);
        }
        Dictionary<SyntaxNode, SemanticType?> DiscoverHints(SyntaxNode root, SemanticScope scope,
            Dictionary<string, SemanticType?>? locals = null)
        {
            // Precedence climbing can build arbitrarily deep left-associated trees.
            // Discover hints bottom-up once, using temporary algorithm storage rather
            // than recursion or repeated subtree scans. No facts or effects occur here.
            Dictionary<SyntaxNode, SemanticType?> hints = new();
            Dictionary<SyntaxNode, ParticipantBinding?> views = new();
            Dictionary<SyntaxNode, SemanticScope?> namespaces = new();
            Stack<(SyntaxNode Node, bool Ready)> discovery = new();
            discovery.Push((root, false));
            while (discovery.Count != 0)
            {
                var item = discovery.Pop();
                SyntaxNode node = item.Node;
                bool binary = node.Kind == SyntaxKind.Binary
                    && Text(node) is "+" or "-" or "*" or "/" or "%" or "!+" or "!-" or "!*" or "&" or "|" or "^" or "<<" or ">>" or "==" or "!=" or "<" or "<=" or ">" or ">=" or "&&" or "||";
                // A reference initializer previews the referent's read type.
                // This traversal does not acquire storage or grant write access.
                bool unary = node.Kind is SyntaxKind.Group or SyntaxKind.Cast
                    || (node.Kind == SyntaxKind.Postfix && Text(node) == "!")
                    || (node.Kind == SyntaxKind.Unary && Text(node) is "-" or "!-" or "!" or "ref");
                if (!item.Ready && node.Kind is SyntaxKind.Array or SyntaxKind.Tuple or SyntaxKind.Index or SyntaxKind.Member)
                {
                    discovery.Push((node, true));
                    for (int i = node.ChildCount() - 1; i >= 0; i -= 1)
                    {
                        discovery.Push((node.ChildAt(i), false));
                    }
                    continue;
                }
                if (!item.Ready && (binary || unary))
                {
                    discovery.Push((node, true));
                    if (binary)
                    {
                        discovery.Push((node.ChildAt(1), false));
                    }
                    discovery.Push((node.ChildAt(0), false));
                    continue;
                }
                if (!item.Ready && node.Kind == SyntaxKind.Call
                    && node.ChildAt(0).Kind == SyntaxKind.Member)
                {
                    discovery.Push((node, true));
                    discovery.Push((node.ChildAt(0).ChildAt(0), false));
                    continue;
                }
                SemanticType? hint = null;
                ParticipantBinding? view = null;
                SemanticScope? placement = null;
                if (node.Kind == SyntaxKind.Group)
                {
                    view = views[node.ChildAt(0)];
                    placement = namespaces[node.ChildAt(0)];
                }
                if (node.Kind == SyntaxKind.Record)
                {
                    SemanticSymbol? declaration = scope.Lookup(Text(node.ChildAt(0)));
                    if (declaration?.Kind == SymbolKind.Struct)
                    {
                        hint = analysis.StructType(declaration);
                    }
                }
                else if (node.Kind == SyntaxKind.Member)
                {
                    SemanticType? receiver = hints[node.ChildAt(0)];
                    if (receiver is SemanticType owner && owner.ArrayDepth == 0)
                    {
                        if (owner.Kind == SemanticTypeKind.Tuple
                            && int.TryParse(Text(node), out int index)
                            && index >= 0 && index < owner.ComponentCount())
                        {
                            hint = owner.ComponentAt(index);
                        }
                        SemanticScope? memberScope = namespaces[node.ChildAt(0)];
                        SemanticSymbol? field = memberScope is not null
                            ? memberScope.LookupMember(Text(node))
                            : views[node.ChildAt(0)]?.FindMember(Text(node))
                                ?? owner.Declaration?.MemberScope?.LookupMember(Text(node));
                        if (field?.Kind == SymbolKind.Field)
                        {
                            hint = field.Type;
                            view = field.ParticipantView;
                        }
                        else if (field?.Kind == SymbolKind.MemberNamespace)
                        {
                            // Preview preserves the physical receiver type only.
                            hint = owner;
                            placement = field.MemberScope;
                        }
                    }
                }
                else if (node.Kind == SyntaxKind.Index)
                {
                    SemanticType? container = hints[node.ChildAt(0)];
                    if (container is SemanticType array && array.ArrayDepth > 0)
                    {
                        hint = SemanticTypes.ElementOf(array);
                    }
                }
                else if (node.Kind == SyntaxKind.Tuple)
                {
                    List<SemanticType> components = new();
                    for (int i = 0; i < node.ChildCount(); i += 1)
                    {
                        if (hints[node.ChildAt(i)] is SemanticType component)
                        {
                            components.Add(component);
                        }
                    }
                    if (components.Count == node.ChildCount())
                    {
                        hint = analysis.TupleType(components);
                    }
                }
                else if (node.Kind == SyntaxKind.Array && node.ChildCount() > 0)
                {
                    SemanticType? element = hints[node.ChildAt(0)];
                    if (element is SemanticType known)
                    {
                        hint = SemanticTypes.ArrayOf(known);
                    }
                }
                else if (binary && Text(node) is "<<" or ">>")
                {
                    hint = hints[node.ChildAt(0)];
                }
                else if ((binary && Text(node) is not ("+" or "-" or "*" or "/" or "%" or "!+" or "!-" or "!*" or "&" or "|" or "^"))
                    || (node.Kind == SyntaxKind.Unary && Text(node) == "!"))
                {
                    hint = analysis.ScalarType(ScalarKind.Bool);
                }
                else if (binary)
                {
                    SemanticType? left = hints[node.ChildAt(0)];
                    SemanticType? right = hints[node.ChildAt(1)];
                    if (left is null)
                    {
                        hint = right;
                    }
                    else if (right is null)
                    {
                        hint = left;
                    }
                    else
                    {
                        hint = SemanticTypes.ArithmeticType(left, right);
                    }
                }
                else if (node.Kind == SyntaxKind.Postfix && Text(node) == "!")
                {
                    SemanticType? operand = hints[node.ChildAt(0)];
                    hint = operand?.Kind == SemanticTypeKind.Nullable ? operand.Element : null;
                    view = views[node.ChildAt(0)];
                }
                else if (unary && node.Kind != SyntaxKind.Cast)
                {
                    hint = hints[node.ChildAt(0)];
                }
                else if (node.Kind == SyntaxKind.Cast)
                {
                    // A cast fixes its result type, not its operand's type.
                    hint = PreviewType(node.ChildAt(1), scope, locals);
                }
                else if (node.Kind == SyntaxKind.Integer)
                {
                    string spelling = Text(node);
                    foreach (SemanticType candidate in analysis.PredefinedTypes())
                    {
                        if (SemanticTypes.IsInteger(candidate)
                            && spelling.EndsWith(SemanticTypes.Name(candidate), StringComparison.Ordinal))
                        {
                            hint = candidate;
                            break;
                        }
                    }
                }
                else if (node.Kind == SyntaxKind.If)
                {
                    hint = ConditionalHint(node, scope, locals).Type;
                }
                else if (node.Kind == SyntaxKind.String)
                {
                    hint = SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.U8));
                }
                else if (node.Kind == SyntaxKind.Literal && Text(node) is "true" or "false")
                {
                    hint = analysis.ScalarType(ScalarKind.Bool);
                }
                else if (node.Kind == SyntaxKind.Unit)
                {
                    hint = analysis.ScalarType(ScalarKind.Unit);
                }
                else if (node.Kind == SyntaxKind.Name)
                {
                    // Branch-local hints are provisional, not declarations. A
                    // present unknown local must not fall back to an outer name.
                    if (locals is null || !locals.TryGetValue(Text(node), out hint))
                    {
                        SemanticSymbol? symbol = scope.Lookup(Text(node));
                        if (symbol?.Kind is SymbolKind.Local or SymbolKind.Parameter)
                        {
                            hint = symbol.Type;
                            view = symbol.ParticipantView;
                        }
                    }
                }
                else if (node.Kind == SyntaxKind.Call && node.ChildAt(0).Kind == SyntaxKind.Member)
                {
                    SyntaxNode member = node.ChildAt(0);
                    SemanticType? receiver = hints[member.ChildAt(0)];
                    if (receiver is SemanticType owner && owner.ArrayDepth == 0)
                    {
                        SyntaxNode receiverNode = member.ChildAt(0);
                        SemanticScope? memberNamespace = namespaces[receiverNode];
                        SemanticSymbol? method = memberNamespace is not null
                            ? memberNamespace.LookupMember(Text(member))
                            : views[receiverNode]?.FindMember(Text(member))
                                ?? owner.Declaration?.MemberScope?.LookupMember(Text(member));
                        if (method?.Kind == SymbolKind.Function)
                        {
                            hint = method.Type;
                            view = method.ParticipantView;
                        }
                    }
                }
                else if (node.Kind == SyntaxKind.Call && node.ChildAt(0).Kind == SyntaxKind.Name)
                {
                    SemanticSymbol? function = scope.Lookup(Text(node.ChildAt(0)));
                    if (function?.Kind == SymbolKind.Function)
                    {
                        hint = function.Type;
                        view = function.ParticipantView;
                    }
                    else if (function?.Kind == SymbolKind.Class)
                    {
                        hint = analysis.ClassType(function);
                    }
                }
                hints.Add(node, hint);
                views.Add(node, view);
                namespaces.Add(node, placement);
            }

            return hints;
        }
        // Preview only: null type means contextual/unknown, not unit. A false
        // continuation means this arm contributes no value to the join. These
        // hints create no semantic objects and perform no assignment effects;
        // actual checking remains authoritative for diagnostics and flow state.
        (SemanticType? Type, bool Continues) ConditionalHint(SyntaxNode node, SemanticScope scope,
            Dictionary<string, SemanticType?>? locals)
        {
            var left = BranchHint(node.ChildAt(1), scope, locals);
            var right = node.ChildCount() == 3
                ? BranchHint(node.ChildAt(2), scope, locals)
                : (Type: (SemanticType?)analysis.ScalarType(ScalarKind.Unit), Continues: true);
            if (!left.Continues)
            {
                return right;
            }
            if (!right.Continues)
            {
                return left;
            }
            if (left.Type == right.Type)
            {
                return left;
            }
            // Without a destination context, an unsuffixed branch literal has
            // its normal i32 default. Two untyped branches remain contextual.
            SemanticType leftType = left.Type ?? analysis.ScalarType(ScalarKind.I32);
            SemanticType rightType = right.Type ?? analysis.ScalarType(ScalarKind.I32);
            SemanticType? common = SemanticTypes.IsInteger(leftType) && SemanticTypes.IsInteger(rightType)
                ? SemanticTypes.ArithmeticType(leftType, rightType) : null;
            return (common, true);
        }
        (SemanticType? Type, bool Continues) BranchHint(SyntaxNode branch, SemanticScope scope,
            Dictionary<string, SemanticType?>? inherited)
        {
            if (branch.Kind == SyntaxKind.If)
            {
                return ConditionalHint(branch, scope, inherited);
            }
            // No semantic objects or assignment facts are created here. These
            // temporary names let a tail refer to preceding local declarations
            // without checking the branch twice or exposing sibling locals.
            Dictionary<string, SemanticType?> locals = inherited is null
                ? new() : new(inherited);
            for (int i = 0; i < branch.ChildCount(); i += 1)
            {
                SyntaxNode statement = branch.ChildAt(i);
                if (statement.Kind == SyntaxKind.Binding)
                {
                    SyntaxNode pattern = statement.ChildAt(0);
                    int next = 1;
                    SemanticType? type = null;
                    if (next < statement.ChildCount() && statement.ChildAt(next).Kind == SyntaxKind.Annotation)
                    {
                        // Preview the value obtained by reading this binding;
                        // reference permission/acquisition remains the real check's job.
                        SyntaxNode annotation = statement.ChildAt(next).ChildAt(0);
                        bool reference = annotation.Kind == SyntaxKind.TypePrefix && Text(annotation) == "ref";
                        type = PreviewType(reference ? annotation.ChildAt(0) : annotation, scope, locals);
                        next += 1;
                    }
                    if (type is null && next < statement.ChildCount())
                    {
                        SyntaxNode initializer = statement.ChildAt(next);
                        type = DiscoverHints(initializer, scope, locals)[initializer];
                        bool referenceInitializer = initializer.Kind == SyntaxKind.Unary
                            && Text(initializer) == "ref";
                        // Unknown storage is not an unsuffixed integer expression.
                        // Never manufacture an i32 referent during provisional lookup.
                        if (type is null && !referenceInitializer
                            && initializer.Kind is SyntaxKind.Integer or SyntaxKind.Unary
                                or SyntaxKind.Binary or SyntaxKind.Group or SyntaxKind.If)
                        {
                            type = analysis.ScalarType(ScalarKind.I32);
                        }
                    }
                    // Preview names without creating semantic declarations. Unknown
                    // component hints remain unknown; shape errors belong to checking.
                    Stack<(SyntaxNode Pattern, SemanticType? Type)> pendingHints = new();
                    pendingHints.Push((pattern, type));
                    while (pendingHints.Count > 0)
                    {
                        (SyntaxNode part, SemanticType? partType) = pendingHints.Pop();
                        if (part.Kind == SyntaxKind.PatternTuple)
                        {
                            bool matching = partType?.Kind == SemanticTypeKind.Tuple
                                && partType.ComponentCount() == part.ChildCount();
                            for (int j = part.ChildCount() - 1; j >= 0; j -= 1)
                            {
                                pendingHints.Push((part.ChildAt(j), matching ? partType!.ComponentAt(j) : null));
                            }
                        }
                        else if (part.Kind == SyntaxKind.PatternName && part.ChildCount() == 1)
                        {
                            locals[Text(part.ChildAt(0))] = partType;
                        }
                    }
                }
                if (statement.Kind is SyntaxKind.Return or SyntaxKind.Break or SyntaxKind.Continue)
                {
                    return (null, false);
                }
                if (statement.Kind == SyntaxKind.ExpressionStatement)
                {
                    SyntaxNode expression = statement.ChildAt(0);
                    if (expression.Kind == SyntaxKind.If)
                    {
                        var conditional = ConditionalHint(expression, scope, locals);
                        // Discarding a value does not undo a control transfer.
                        // A statement-if with no continuing arm ends this branch,
                        // even when a following semicolon adds a discard marker.
                        if (!conditional.Continues || i == branch.ChildCount() - 1)
                        {
                            return conditional;
                        }
                    }
                    else if (i == branch.ChildCount() - 1)
                    {
                        return (DiscoverHints(expression, scope, locals)[expression], true);
                    }
                }
            }
            return (analysis.ScalarType(ScalarKind.Unit), true);
        }
        void BindRecord(SyntaxNode node, SemanticScope scope)
        {
            SemanticType type = ReadType(node.ChildAt(0), scope, true);
            if (type.Declaration?.Kind != SymbolKind.Struct)
            {
                Fail(node, "LYC304", "Named construction requires a struct type.");
            }
            // Inspect the resolved closure, not an expanded recursive layout.
            // Floating values remain outside this slice. Reference fields use
            // the same referent closure; the visited set bounds recursive edges.
            Stack<SemanticType> pending = new();
            HashSet<SemanticType> visited = new();
            pending.Push(type);
            while (pending.Count != 0)
            {
                SemanticType current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }
                if (SemanticTypes.IsFloat(current))
                {
                    Unsupported(node);
                }
                if (current.Kind == SemanticTypeKind.Array)
                {
                    pending.Push(current.Element!);
                }
                else if (current.Kind == SemanticTypeKind.Tuple)
                {
                    for (int i = 0; i < current.ComponentCount(); i += 1)
                    {
                        pending.Push(current.ComponentAt(i));
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
            HashSet<SemanticSymbol> supplied = new();
            for (int i = 1; i < node.ChildCount(); i += 1)
            {
                SyntaxNode initializer = node.ChildAt(i);
                SemanticSymbol? field = type.Declaration!.MemberScope!.LookupMember(Text(initializer));
                if (field?.Kind != SymbolKind.Field)
                {
                    Fail(initializer, "LYC329", $"No field '{Text(initializer)}' exists on {SemanticTypes.Name(type)}.");
                }
                if (!supplied.Add(field!))
                {
                    Fail(initializer, "LYC302", $"Field '{field!.Name}' is supplied more than once.");
                }
                _ = new SemanticFact(analysis, initializer, field!.Type!, field);
            }
            foreach (SemanticSymbol field in type.Declaration!.MemberScope!.Fields())
            {
                if (!supplied.Contains(field) && field.InitializerScope is null)
                {
                    Fail(node, "LYC304", $"Required field '{field.Name}' is missing.");
                }
            }
            _ = new SemanticFact(analysis, node.ChildAt(0), type, null);
        }
        SemanticType CheckExpression(SyntaxNode root, SemanticScope scope, SemanticType? expectedType = null)
        {
            Dictionary<SyntaxNode, SemanticType?> hints = DiscoverHints(root, scope);

            // Push right before left so checking still visits left first. Parent
            // completion consumes checked child facts and validates intermediate
            // overflow. The direct signed-literal case remains one operation.
            Stack<(SyntaxNode Node, SemanticType? Expected, int Stage)> work = new();
            Dictionary<SyntaxNode, (HashSet<SemanticSymbol> Definite, HashSet<SemanticSymbol> Possible)> skipped = new();
            work.Push((root, expectedType, 0));
            while (work.Count != 0)
            {
                var item = work.Pop();
                SyntaxNode node = item.Node;
                if (node.Kind == SyntaxKind.Record)
                {
                    if (item.Stage == 0)
                    {
                        BindRecord(node, scope);
                    }
                    int fieldPosition = item.Stage + 1;
                    if (fieldPosition < node.ChildCount())
                    {
                        SyntaxNode initializer = node.ChildAt(fieldPosition);
                        work.Push((node, item.Expected, item.Stage + 1));
                        if (initializer.Fact!.Symbol!.IsReference)
                        {
                            // The field stores an address, not a converted value.
                            // Check it at its written position, just like a ref argument.
                            SemanticType actual = CheckReference(initializer.ChildAt(0), scope);
                            if (actual != initializer.Fact.Type)
                            {
                                Fail(initializer.ChildAt(0), "LYC325", "Reference storage type must match exactly.");
                            }
                        }
                        else
                        {
                            work.Push((initializer.ChildAt(0), initializer.Fact.Type, 0));
                        }
                    }
                    else
                    {
                        CheckExpressionNode(node, scope, item.Expected);
                    }
                }
                else if (node.Kind == SyntaxKind.Tuple && item.Stage <= node.ChildCount())
                {
                    // Each position receives its own context, with no early
                    // evaluation or inference from a neighboring component.
                    if (item.Stage < node.ChildCount())
                    {
                        SemanticType? component = item.Expected is SemanticType tupleContext
                            && tupleContext.Kind == SemanticTypeKind.Tuple
                            && tupleContext.ComponentCount() == node.ChildCount()
                            ? tupleContext.ComponentAt(item.Stage) : null;
                        work.Push((node, item.Expected, item.Stage + 1));
                        work.Push((node.ChildAt(item.Stage), component, 0));
                    }
                    else
                    {
                        CheckExpressionNode(node, scope, item.Expected);
                    }
                }
                else if (node.Kind == SyntaxKind.Array && item.Stage <= node.ChildCount())
                {
                    // Check one element at a time: the first checked value can
                    // supply context without evaluating any later element early.
                    if (item.Stage < node.ChildCount())
                    {
                        SemanticType? element = item.Expected is SemanticType context && context.ArrayDepth > 0
                            ? SemanticTypes.ElementOf(context)
                            : item.Stage > 0 ? node.ChildAt(0).Fact!.Type : null;
                        work.Push((node, item.Expected, item.Stage + 1));
                        work.Push((node.ChildAt(item.Stage), element, 0));
                    }
                    else
                    {
                        CheckExpressionNode(node, scope, item.Expected);
                    }
                }
                else if (node.Kind == SyntaxKind.Index && item.Stage == 0)
                {
                    work.Push((node, item.Expected, 2));
                    work.Push((node.ChildAt(1), null, 0));
                    work.Push((node.ChildAt(0), null, 0));
                }
                else if (node.Kind == SyntaxKind.Binary && Text(node) is "&&" or "||" && item.Stage != 2)
                {
                    if (item.Stage == 0)
                    {
                        work.Push((node, item.Expected, 1));
                        work.Push((node.ChildAt(0), analysis.ScalarType(ScalarKind.Bool), 0));
                    }
                    else
                    {
                        skipped.Add(node, (new(definitelyAssigned), new(possiblyAssigned)));
                        work.Push((node, item.Expected, 2));
                        work.Push((node.ChildAt(1), analysis.ScalarType(ScalarKind.Bool), 0));
                    }
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Binary && Text(node) is "+" or "-" or "*" or "/" or "%" or "!+" or "!-" or "!*" or "&" or "|" or "^")
                {
                    SyntaxNode left = node.ChildAt(0);
                    SyntaxNode right = node.ChildAt(1);
                    work.Push((node, item.Expected, 2));
                    // Preserve an operand's intrinsic type. Neighbor/destination
                    // context types only expressions that have no type yet;
                    // widening the receiving binding cannot widen this operation.
                    work.Push((right, hints[right] ?? hints[left] ?? item.Expected, 0));
                    work.Push((left, hints[left] ?? hints[right] ?? item.Expected, 0));
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Binary && Text(node) is "<<" or ">>")
                {
                    // A count does not supply a neighboring type hint or widen
                    // the left operand. It has its own independent integer type.
                    work.Push((node, item.Expected, 2));
                    work.Push((node.ChildAt(1), null, 0));
                    work.Push((node.ChildAt(0), hints[node.ChildAt(0)] ?? item.Expected, 0));
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Binary
                    && Text(node) is "==" or "!=" or "<" or "<=" or ">" or ">=" or "&&" or "||")
                {
                    SyntaxNode left = node.ChildAt(0);
                    SyntaxNode right = node.ChildAt(1);
                    bool logical = Text(node) is "&&" or "||";
                    // A comparison's bool destination is not numeric context.
                    // Only its neighboring operand can type an integer literal.
                    work.Push((node, item.Expected, 2));
                    work.Push((right, logical ? analysis.ScalarType(ScalarKind.Bool) : hints[right] ?? hints[left], 0));
                    work.Push((left, logical ? analysis.ScalarType(ScalarKind.Bool) : hints[left] ?? hints[right], 0));
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Unary
                    && (Text(node) == "!-" || (Text(node) == "-"
                        && node.ChildAt(0).Kind != SyntaxKind.Integer)))
                {
                    work.Push((node, item.Expected, 2));
                    work.Push((node.ChildAt(0), hints[node.ChildAt(0)] ?? item.Expected, 0));
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Unary && Text(node) == "!")
                {
                    work.Push((node, item.Expected, 2));
                    work.Push((node.ChildAt(0), analysis.ScalarType(ScalarKind.Bool), 0));
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Postfix && Text(node) == "!")
                {
                    work.Push((node, item.Expected, 2));
                    work.Push((node.ChildAt(0), null, 0));
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Cast)
                {
                    work.Push((node, item.Expected, 2));
                    work.Push((node.ChildAt(0), null, 0));
                }
                else if (item.Stage == 0 && node.Kind == SyntaxKind.Group)
                {
                    work.Push((node, item.Expected, 2));
                    work.Push((node.ChildAt(0), item.Expected, 0));
                }
                else
                {
                    CheckExpressionNode(node, scope, item.Expected);
                    if (skipped.Remove(node, out var skipState))
                    {
                        // The right operand may not execute: only assignments
                        // common to both paths are definite; either may occur.
                        definitelyAssigned.IntersectWith(skipState.Definite);
                        possiblyAssigned.UnionWith(skipState.Possible);
                    }
                }
            }
            return root.Fact!.Type;
        }
        // Match syntax structure against an already checked argument type. This
        // binds variables; it does not solve conversions or infer from a result.
        void MatchTypePattern(SyntaxNode pattern, SemanticType actual,
            Dictionary<string, SemanticType?> bindings, SyntaxNode argument)
        {
            Stack<(SyntaxNode Pattern, SemanticType Actual)> pending = new();
            pending.Push((pattern, actual));
            while (pending.Count != 0)
            {
                var item = pending.Pop();
                SyntaxNode syntax = item.Pattern;
                SemanticType type = item.Actual;
                if (syntax.Kind == SyntaxKind.TypeName
                    && bindings.TryGetValue(Text(syntax), out SemanticType? previous))
                {
                    if (previous is not null && previous != type)
                    {
                        Fail(argument, "LYC304", $"Conflicting inferred types for '{Text(syntax)}'.");
                    }
                    bindings[Text(syntax)] = type;
                }
                else if (syntax.Kind == SyntaxKind.ArrayType && syntax.ChildCount() == 1)
                {
                    if (type.Kind != SemanticTypeKind.Array)
                    {
                        Fail(argument, "LYC304", "Expected an array type for generic inference.");
                    }
                    pending.Push((syntax.ChildAt(0), type.Element!));
                }
                else if (syntax.Kind == SyntaxKind.TypeGroup)
                {
                    if (syntax.ChildCount() == 1 && (syntax.TextOffset < 0 || Text(syntax) != ","))
                    {
                        pending.Push((syntax.ChildAt(0), type));
                        continue;
                    }
                    if (type.Kind != SemanticTypeKind.Tuple
                        || type.ComponentCount() != syntax.ChildCount())
                    {
                        Fail(argument, "LYC304", "Tuple shape does not match generic parameter.");
                    }
                    for (int index = syntax.ChildCount() - 1; index >= 0; index -= 1)
                    {
                        pending.Push((syntax.ChildAt(index), type.ComponentAt(index)));
                    }
                }
                // Concrete leaves are checked against the completed signature
                // later. They do not contribute or negotiate variable bindings.
            }
        }
        SemanticSymbol Specialize(SyntaxNode call, SemanticSymbol template,
            SemanticScope callerScope, out List<SemanticType>? checkedArguments)
        {
            SyntaxNode callee = call.ChildAt(0);
            SyntaxNode? parameters = template.GenericParameters();
            checkedArguments = null;
            if (parameters is null)
            {
                Unsupported(callee);
            }
            List<SemanticType> types = new();
            if (callee.Kind == SyntaxKind.Generic)
            {
                SyntaxNode arguments = callee.ChildAt(1);
                if (arguments.ChildCount() != parameters!.ChildCount())
                {
                    Fail(callee, "LYC303", "Generic type argument count does not match the declaration.");
                }
                for (int index = 0; index < arguments.ChildCount(); index += 1)
                {
                    types.Add(ReadType(arguments.ChildAt(index), callerScope, true));
                }
            }
            else
            {
                Dictionary<string, SemanticType?> bindings = new();
                for (int index = 0; index < parameters!.ChildCount(); index += 1)
                {
                    bindings.Add(Text(parameters.ChildAt(index)), null);
                }
                SyntaxNode valueParameters = ParametersOf(template.Declaration!);
                if (valueParameters.ChildCount() != call.ChildCount() - 1)
                {
                    Fail(callee, "LYC303", "Function argument count does not match the declaration.");
                }
                checkedArguments = new();
                for (int index = 0; index < valueParameters.ChildCount(); index += 1)
                {
                    SyntaxNode parameter = valueParameters.ChildAt(index);
                    if (parameter.ChildCount() != 1)
                    {
                        Unsupported(parameter);
                    }
                    SyntaxNode pattern = parameter.ChildAt(0);
                    bool reference = pattern.Kind == SyntaxKind.TypePrefix && Text(pattern) == "ref";
                    if (reference)
                    {
                        pattern = pattern.ChildAt(0);
                    }
                    SyntaxNode argument = call.ChildAt(index + 1);
                    // Preserve evaluation/checking order and acquire reference
                    // storage once. Completed-signature validation reuses this
                    // result instead of attaching duplicate expression facts.
                    SemanticType actual = reference
                        ? CheckReference(argument, callerScope)
                        : CheckExpression(argument, callerScope);
                    checkedArguments.Add(actual);
                    MatchTypePattern(pattern, actual, bindings, argument);
                }
                for (int index = 0; index < parameters.ChildCount(); index += 1)
                {
                    string name = Text(parameters.ChildAt(index));
                    if (bindings[name] is not SemanticType type)
                    {
                        Fail(callee, "LYC303", $"Cannot infer '{name}'; supply explicit type arguments.");
                    }
                    types.Add(bindings[name]!);
                }
            }
            GenericSpecialization? existing = analysis.FindSpecialization(template, types);
            if (existing is not null)
            {
                return existing.Function!;
            }
            int depth = 0;
            if (checkingFunction?.Specialization is GenericSpecialization requester)
            {
                depth = specializationDepths[requester] + 1;
            }
            if (depth > maximumSpecializationDepth)
            {
                Fail(callee, "LYC344",
                    $"Generic specialization discovery depth exceeds {maximumSpecializationDepth}.");
            }
            GenericSpecialization specialization = new(template, types);
            specializationDepths.Add(specialization, depth);
            SemanticSymbol function = new(template.Scope!, specialization.Root(), template.Name,
                SymbolKind.Function, analysis.ScalarType(ScalarKind.Unit), false,
                specialization: specialization);
            SemanticScope scope = new(function);
            for (int index = 0; index < parameters.ChildCount(); index += 1)
            {
                SyntaxNode parameter = parameters.ChildAt(index);
                if (parameter.ChildCount() != 0)
                {
                    Unsupported(parameter);
                }
                string name = Text(parameter);
                RefuseDuplicate(scope, parameter, name);
                _ = new SemanticSymbol(scope, null, name, SymbolKind.Type,
                    specialization.BindingAt(index), false);
            }
            CheckSignature(function);
            if (SemanticTypes.HasContainedBorrows(function.Type!))
            {
                Fail(function.Declaration!, "LYC343", "Function results must not contain borrowed references.");
            }
            // Signature is now complete. A recursive request finds this same
            // callable before its body is checked, rather than copying again.
            functions.Add(function);
            return function;
        }
        void CheckArguments(SyntaxNode call, SemanticSymbol target, SemanticScope scope, int offset,
            List<SemanticType>? checkedArguments = null)
        {
            CheckSignature(target);
            int count = target.FunctionScope!.ParameterCount() - offset;
            SyntaxNode callee = call.ChildAt(0);
            if (count != call.ChildCount() - 1)
            {
                Fail(callee, "LYC303", $"Function '{target.Name}' expects {count} argument(s), got {call.ChildCount() - 1}.");
            }
            for (int i = 0; i < count; i += 1)
            {
                SyntaxNode argument = call.ChildAt(i + 1);
                SemanticSymbol parameter = target.FunctionScope.ParameterAt(i + offset);
                if (parameter.AcceptsArray)
                {
                    SemanticType arrayType = CheckExpression(argument, scope);
                    if (arrayType.ArrayDepth == 0)
                    {
                        Fail(argument, "LYC323", "Expected an array argument.");
                    }
                    continue;
                }
                SemanticType expected = parameter.Type!;
                if (parameter.IsReference)
                {
                    SemanticType referenced = checkedArguments is null
                        ? CheckReference(argument, scope) : checkedArguments[i];
                    if (referenced != expected)
                    {
                        Fail(argument, "LYC325", "Reference storage type must match exactly.");
                    }
                    continue;
                }
                SemanticType actual = checkedArguments is null
                    ? CheckExpression(argument, scope, expected) : checkedArguments[i];
                if (!SemanticTypes.CanWiden(actual, expected))
                {
                    Fail(argument, "LYC304", $"Expected {SemanticTypes.Name(expected)}, got {SemanticTypes.Name(actual)}.");
                }
            }
        }
        SemanticType CheckExpressionNode(SyntaxNode node, SemanticScope scope, SemanticType? expectedType)
        {
            SemanticType type;
            SemanticSymbol? resolved = null;
            BigInteger? constant = null;
            switch (node.Kind)
            {
                case SyntaxKind.Record:
                    type = node.ChildAt(0).Fact!.Type;
                    for (int i = 1; i < node.ChildCount(); i += 1)
                    {
                        SyntaxNode initializer = node.ChildAt(i);
                        RequireType(initializer.ChildAt(0), initializer.ChildAt(0).Fact!.Type,
                            initializer.Fact!.Type);
                    }
                    break;
                case SyntaxKind.Tuple:
                    List<SemanticType> tupleComponents = new();
                    bool contextualTuple = expectedType is not null
                        && expectedType.Kind == SemanticTypeKind.Tuple
                        && expectedType.ComponentCount() == node.ChildCount();
                    for (int i = 0; i < node.ChildCount(); i += 1)
                    {
                        RequireAggregateElementView(node.ChildAt(i), node.ChildAt(i).Fact!.ParticipantView);
                        SemanticType actual = node.ChildAt(i).Fact!.Type;
                        SemanticType component = contextualTuple ? expectedType!.ComponentAt(i) : actual;
                        RequireType(node.ChildAt(i), actual, component);
                        tupleComponents.Add(component);
                    }
                    type = analysis.TupleType(tupleComponents);
                    RequireTupleSlice(node, type);
                    break;
                case SyntaxKind.Array:
                    if (expectedType is SemanticType context && context.ArrayDepth > 0)
                    {
                        type = context;
                    }
                    else if (node.ChildCount() > 0)
                    {
                        type = SemanticTypes.ArrayOf(node.ChildAt(0).Fact!.Type);
                    }
                    else
                    {
                        Fail(node, "LYC320", "Empty array literal requires an array type context.");
                        return analysis.ScalarType(ScalarKind.Unit);
                    }
                    SemanticType elementType = SemanticTypes.ElementOf(type);
                    // Array ownership cannot turn contained aliases into owned targets.
                    // This restriction also applies to empty literals and nested wrappers.
                    if (SemanticTypes.HasContainedBorrows(elementType))
                    {
                        Fail(node, "LYC343", "Array elements must not contain borrowed references.");
                    }
                    if (elementType.ArrayDepth == 0 && elementType != analysis.ScalarType(ScalarKind.Bool)
                        && !SemanticTypes.IsInteger(elementType) && elementType.Class is null
                        && elementType.Declaration?.Kind != SymbolKind.Struct
                        && elementType.Kind != SemanticTypeKind.Tuple)
                    {
                        Unsupported(node);
                    }
                    for (int i = 0; i < node.ChildCount(); i += 1)
                    {
                        SyntaxNode element = node.ChildAt(i);
                        RequireAggregateElementView(element, element.Fact!.ParticipantView);
                        if (!SemanticTypes.CanWiden(element.Fact!.Type, elementType))
                        {
                            Fail(element, "LYC304", $"Expected {SemanticTypes.Name(elementType)}, got {SemanticTypes.Name(element.Fact.Type)}.");
                        }
                    }
                    break;
                case SyntaxKind.Index:
                    SemanticType containerType = node.ChildAt(0).Fact!.Type;
                    if (containerType.ArrayDepth == 0)
                    {
                        Fail(node, "LYC321", "Indexing requires an array value.");
                    }
                    if (!SemanticTypes.IsInteger(node.ChildAt(1).Fact!.Type))
                    {
                        Fail(node.ChildAt(1), "LYC322", "Array index must be an integer.");
                    }
                    type = SemanticTypes.ElementOf(containerType);
                    break;
                case SyntaxKind.MatchType:
                case SyntaxKind.If:
                    SemanticScope functionScope = scope;
                    while (functionScope.Function is null && functionScope.InitializerField is null
                        && functionScope.Parent is not null)
                    {
                        functionScope = functionScope.Parent;
                    }
                    SemanticSymbol? executionOwner = functionScope.Function ?? functionScope.InitializerField;
                    if (executionOwner is null)
                    {
                        Unsupported(node);
                    }
                    if (node.Kind == SyntaxKind.MatchType)
                    {
                        CheckMatchType(node, scope, executionOwner!, true, expectedType);
                    }
                    else
                    {
                        CheckIf(node, scope, executionOwner!, true, expectedType);
                    }
                    return node.Fact!.Type;
                case SyntaxKind.Cast:
                    type = ReadType(node.ChildAt(1), scope);
                    SemanticFact converted = node.ChildAt(0).Fact!;
                    if (!SemanticTypes.IsInteger(type) || !SemanticTypes.IsInteger(converted.Type))
                    {
                        Unsupported(node);
                    }
                    if (converted.IntegerConstant is BigInteger original)
                    {
                        constant = Text(node) == "!as" ? WrapInteger(type, original) : original;
                        if (!SemanticTypes.Represents(type, constant.Value))
                        {
                            if (!unsafeMode)
                            {
                                Fail(node, "LYC318", $"Integer conversion is outside {SemanticTypes.Name(type)} range.");
                            }
                            constant = null;
                        }
                    }
                    break;
                case SyntaxKind.Integer:
                    type = CheckInteger(node, node, false, expectedType, out BigInteger positive);
                    constant = positive;
                    break;
                case SyntaxKind.Unary when Text(node) == "-" && node.ChildAt(0).Kind == SyntaxKind.Integer:
                    // The signed literal is checked as a whole. Its magnitude
                    // need not itself fit the signed type (for example -128i8).
                    type = CheckInteger(node, node.ChildAt(0), true, expectedType, out BigInteger negative);
                    constant = negative;
                    break;
                case SyntaxKind.Unary when Text(node) is "-" or "!-":
                    type = node.ChildAt(0).Fact!.Type;
                    if (!SemanticTypes.IsInteger(type))
                    {
                        Fail(node, "LYC312", "Integer arithmetic requires integer operands.");
                    }
                    if (node.ChildAt(0).Fact!.IntegerConstant is BigInteger operand)
                    {
                        constant = -operand;
                        if (Text(node) == "!-")
                        {
                            constant = WrapInteger(type, constant.Value);
                        }
                        if (!SemanticTypes.Represents(type, constant.Value))
                        {
                            if (!unsafeMode)
                            {
                                Fail(node, "LYC311", $"Integer constant operation is outside {SemanticTypes.Name(type)} range.");
                            }
                            constant = null;
                        }
                    }
                    break;
                case SyntaxKind.Binary when Text(node) is "+" or "-" or "*" or "/" or "%" or "!+" or "!-" or "!*" or "&" or "|" or "^":
                    SyntaxNode leftNode = node.ChildAt(0);
                    SyntaxNode rightNode = node.ChildAt(1);
                    SemanticType left = leftNode.Fact!.Type;
                    SemanticType right = rightNode.Fact!.Type;
                    if (!SemanticTypes.IsInteger(left) || !SemanticTypes.IsInteger(right))
                    {
                        Fail(node, "LYC312", "Integer arithmetic requires integer operands.");
                    }
                    SemanticType? selected = SemanticTypes.ArithmeticType(left, right);
                    if (selected is null)
                    {
                        Fail(node, "LYC312", $"No integer arithmetic type contains both {SemanticTypes.Name(left)} and {SemanticTypes.Name(right)}.");
                    }
                    type = selected!;
                    BigInteger? leftConstant = leftNode.Fact!.IntegerConstant;
                    BigInteger? rightConstant = rightNode.Fact!.IntegerConstant;
                    if (!unsafeMode && Text(node) is "/" or "%" && rightConstant == BigInteger.Zero)
                    {
                        Fail(node, "LYC316", "Integer division or remainder by zero.");
                    }
                    if (leftConstant is not null && rightConstant is not null
                        && !(Text(node) is "/" or "%" && rightConstant == BigInteger.Zero))
                    {
                        constant = Text(node) switch
                        {
                            "+" or "!+" => leftConstant.Value + rightConstant.Value,
                            "-" or "!-" => leftConstant.Value - rightConstant.Value,
                            "*" or "!*" => leftConstant.Value * rightConstant.Value,
                            "&" => leftConstant.Value & rightConstant.Value,
                            "|" => leftConstant.Value | rightConstant.Value,
                            "^" => leftConstant.Value ^ rightConstant.Value,
                            "/" => leftConstant.Value / rightConstant.Value,
                            _ => leftConstant.Value % rightConstant.Value,
                        };
                        if (Text(node) is "!+" or "!-" or "!*")
                        {
                            constant = WrapInteger(type, constant.Value);
                        }
                        if (!SemanticTypes.Represents(type, constant.Value))
                        {
                            if (!unsafeMode)
                            {
                                Fail(node, "LYC311", $"Integer constant operation is outside {SemanticTypes.Name(type)} range.");
                            }
                            constant = null;
                        }
                    }
                    break;
                case SyntaxKind.Binary when Text(node) is "<<" or ">>":
                    type = node.ChildAt(0).Fact!.Type;
                    if (!SemanticTypes.IsInteger(type) || !SemanticTypes.IsInteger(node.ChildAt(1).Fact!.Type))
                    {
                        Fail(node, "LYC312", "Integer arithmetic requires integer operands.");
                    }
                    BigInteger? shiftCount = node.ChildAt(1).Fact!.IntegerConstant;
                    int width = SemanticTypes.IntegerWidth(type);
                    if (shiftCount is not null && (shiftCount < 0 || shiftCount >= width))
                    {
                        if (!unsafeMode)
                        {
                            Fail(node, "LYC317", "Integer shift count is outside operand width.");
                        }
                        shiftCount = null;
                    }
                    if (shiftCount is not null && node.ChildAt(0).Fact!.IntegerConstant is BigInteger shifted)
                    {
                        // BigInteger right shift has arithmetic sign extension.
                        // Width reduction is still necessary for left shifts.
                        constant = Text(node) == "<<" ? shifted << (int)shiftCount.Value : shifted >> (int)shiftCount.Value;
                        constant = WrapInteger(type, constant.Value);
                    }
                    break;
                case SyntaxKind.Binary when Text(node) is "==" or "!=" or "<" or "<=" or ">" or ">=":
                    if (Text(node) is "==" or "!="
                        && node.ChildAt(0).Fact!.Type == analysis.ScalarType(ScalarKind.Bool)
                        && node.ChildAt(1).Fact!.Type == analysis.ScalarType(ScalarKind.Bool))
                    {
                        type = analysis.ScalarType(ScalarKind.Bool);
                        break;
                    }
                    if (Text(node) is "==" or "!="
                        && SemanticTypes.CanCompareHandleIdentity(
                            node.ChildAt(0).Fact!.Type, node.ChildAt(1).Fact!.Type))
                    {
                        type = analysis.ScalarType(ScalarKind.Bool);
                        break;
                    }
                    if (!SemanticTypes.IsInteger(node.ChildAt(0).Fact!.Type)
                        || !SemanticTypes.IsInteger(node.ChildAt(1).Fact!.Type))
                    {
                        Fail(node, "LYC313", "Integer comparison requires integer operands.");
                    }
                    type = analysis.ScalarType(ScalarKind.Bool);
                    break;
                case SyntaxKind.Binary when Text(node) is "&&" or "||":
                    RequireType(node.ChildAt(0), node.ChildAt(0).Fact!.Type, analysis.ScalarType(ScalarKind.Bool));
                    RequireType(node.ChildAt(1), node.ChildAt(1).Fact!.Type, analysis.ScalarType(ScalarKind.Bool));
                    type = analysis.ScalarType(ScalarKind.Bool);
                    break;
                case SyntaxKind.Unary when Text(node) == "!":
                    RequireType(node.ChildAt(0), node.ChildAt(0).Fact!.Type, analysis.ScalarType(ScalarKind.Bool));
                    type = analysis.ScalarType(ScalarKind.Bool);
                    break;
                case SyntaxKind.Postfix when Text(node) == "!":
                    SemanticType nullableOperand = node.ChildAt(0).Fact!.Type;
                    if (nullableOperand.Kind != SemanticTypeKind.Nullable)
                    {
                        Fail(node, "LYC304", "Checked unwrap requires a nullable class handle.");
                    }
                    type = nullableOperand.Element!;
                    break;
                case SyntaxKind.Literal when Text(node) == "null":
                    if (expectedType?.Kind != SemanticTypeKind.Nullable)
                    {
                        Fail(node, "LYC304", "Null requires a nullable class-handle context.");
                    }
                    type = expectedType!;
                    break;
                case SyntaxKind.String:
                    type = SemanticTypes.ArrayOf(analysis.ScalarType(ScalarKind.U8));
                    break;
                case SyntaxKind.Literal when Text(node) is "true" or "false":
                    type = analysis.ScalarType(ScalarKind.Bool);
                    break;
                case SyntaxKind.Unit:
                    type = analysis.ScalarType(ScalarKind.Unit);
                    break;
                case SyntaxKind.Group:
                    type = node.ChildAt(0).Fact!.Type;
                    constant = node.ChildAt(0).Fact!.IntegerConstant;
                    if (node.ChildAt(0).Fact!.Symbol?.Kind == SymbolKind.MemberNamespace)
                    {
                        resolved = node.ChildAt(0).Fact!.Symbol;
                    }
                    break;
                case SyntaxKind.Member:
                    SemanticType? memberHint = DiscoverHints(node.ChildAt(0), scope)[node.ChildAt(0)];
                    if (memberHint?.Kind == SemanticTypeKind.Tuple
                        || int.TryParse(Text(node), out _))
                    {
                        SemanticType receiver = CheckExpression(node.ChildAt(0), scope);
                        if (receiver.Kind != SemanticTypeKind.Tuple
                            || !int.TryParse(Text(node), out int selector)
                            || selector < 0 || selector >= receiver.ComponentCount())
                        {
                            Fail(node, "LYC304", "Tuple selection requires an in-range component index.");
                        }
                        int componentIndex = int.Parse(Text(node), System.Globalization.CultureInfo.InvariantCulture);
                        type = receiver.ComponentAt(componentIndex);
                        break;
                    }
                    _ = CheckField(node, scope, true);
                    return node.Fact!.Type;
                case SyntaxKind.Name:
                    resolved = scope.Lookup(Text(node));
                    if (resolved is null)
                    {
                        Fail(node, "LYC301", $"Unknown name '{Text(node)}'.");
                    }
                    if (resolved!.Kind is not (SymbolKind.Parameter or SymbolKind.Local))
                    {
                        Unsupported(node);
                    }
                    if (!unevaluated && !definitelyAssigned.Contains(resolved))
                    {
                        Fail(node, "LYC307", $"Local '{resolved.Name}' is not definitely assigned.");
                    }
                    if (IsConstructingSelf(resolved))
                    {
                        // Copying/passing self exposes every field, unlike a direct
                        // field selection. Do not leak an unfinished receiver.
                        RequireInitializedReceiver(node);
                    }
                    type = resolved.Type!;
                    break;
                case SyntaxKind.Call:
                    SyntaxNode callee = node.ChildAt(0);
                    if (callee.Kind == SyntaxKind.Member)
                    {
                        SyntaxNode receiverNode = callee.ChildAt(0);
                        // A struct namespace is not a runtime receiver. Resolve its
                        // static function directly and retain the actual declaration.
                        SemanticSymbol? namespaceOwner = receiverNode.Kind == SyntaxKind.Name
                            ? scope.Lookup(Text(receiverNode)) : null;
                        if (namespaceOwner?.Kind == SymbolKind.Struct)
                        {
                            SemanticSymbol? method = namespaceOwner.MemberScope!.LookupMember(Text(callee));
                            if (method?.Kind != SymbolKind.Function)
                            {
                                Fail(callee, "LYC329", $"No static method '{Text(callee)}' exists on {namespaceOwner.Name}.");
                            }
                            if (method!.FunctionScope!.ParameterCount() > 0
                                && method.FunctionScope.ParameterAt(0).Name == "self")
                            {
                                Fail(callee, "LYC304", "An instance method requires a value or explicit reference receiver.");
                            }
                            CheckArguments(node, method, scope, 0);
                            _ = new SemanticFact(analysis, node, method.Type!, method);
                            return method.Type!;
                        }
                        SemanticType? receiverHint = DiscoverHints(receiverNode, scope)[receiverNode];
                        SyntaxNode ungroupedReceiver = receiverNode;
                        while (ungroupedReceiver.Kind == SyntaxKind.Group)
                        {
                            ungroupedReceiver = ungroupedReceiver.ChildAt(0);
                        }
                        bool referenceReceiver = ungroupedReceiver.Kind == SyntaxKind.Unary
                            && Text(ungroupedReceiver) == "ref";
                        if (referenceReceiver)
                        {
                            receiverHint = DiscoverHints(ungroupedReceiver.ChildAt(0), scope)[ungroupedReceiver.ChildAt(0)];
                        }
                        if (receiverHint?.Declaration?.Kind == SymbolKind.Struct
                            && receiverHint.ArrayDepth == 0)
                        {
                            SemanticSymbol receiverOwner = receiverHint.Declaration;
                            SemanticSymbol? method = receiverOwner.MemberScope!.LookupMember(Text(callee));
                            if (method?.Kind != SymbolKind.Function
                                || method.FunctionScope!.ParameterCount() == 0
                                || method.FunctionScope.ParameterAt(0).Name != "self")
                            {
                                Fail(callee, "LYC329", $"No instance method '{Text(callee)}' exists on {receiverOwner.Name}.");
                            }
                            SemanticSymbol self = method!.FunctionScope!.ParameterAt(0);
                            if (self.IsReference != referenceReceiver)
                            {
                                Fail(receiverNode, "LYC325", "Receiver mode must match: use explicit ref only for a reference receiver.");
                            }
                            SemanticType actual = self.IsReference
                                ? CheckReference(receiverNode, scope) : CheckExpression(receiverNode, scope);
                            RequireType(receiverNode, actual, self.Type!);
                            CheckArguments(node, method, scope, 1);
                            _ = new SemanticFact(analysis, node, method.Type!, method);
                            return method.Type!;
                        }
                        if (receiverHint is SemanticType receiverClass
                            && receiverClass.ArrayDepth == 0 && receiverClass.Class is not null)
                        {
                            SemanticType actualReceiver = CheckExpression(receiverNode, scope);
                            if (actualReceiver.Kind == SemanticTypeKind.Nullable)
                            {
                                Fail(receiverNode, "LYC304", "Nullable method access requires checked unwrap.");
                            }
                            // A label selects members; it is not a second object
                            // value or an alternate route to built-in destruction.
                            if (Text(callee) == "destroy"
                                && receiverNode.Fact?.Symbol?.Kind != SymbolKind.MemberNamespace)
                            {
                                SemanticSymbol destroyedClass = actualReceiver.Class!;
                                SyntaxNode declaration = destroyedClass.Declaration!;
                                for (int i = 0; i < declaration.ChildCount(); i += 1)
                                {
                                    SyntaxNode part = declaration.ChildAt(i);
                                    if (part.Kind == SyntaxKind.Modifier && Text(part) == "value")
                                    {
                                        Fail(callee, "LYC331", "Counted value objects cannot be explicitly destroyed.");
                                    }
                                }
                                if (node.ChildCount() != 1)
                                {
                                    Fail(callee, "LYC303", "Destruction expects no arguments.");
                                }
                                _ = new SemanticFact(analysis, node, analysis.ScalarType(ScalarKind.Unit), destroyedClass,
                                    classOperation: ClassOperation.Destroy);
                                return analysis.ScalarType(ScalarKind.Unit);
                            }
                            SemanticScope methodScope = receiverNode.Fact?.Symbol?.Kind == SymbolKind.MemberNamespace
                                ? receiverNode.Fact.Symbol.MemberScope!
                                : actualReceiver.Class!.MemberScope!;
                            SemanticSymbol? method = receiverNode.Fact?.Symbol?.Kind == SymbolKind.MemberNamespace
                                ? methodScope.LookupMember(Text(callee))
                                : receiverNode.Fact?.ParticipantView?.FindMember(Text(callee))
                                    ?? methodScope.LookupMember(Text(callee));
                            if (method?.Kind != SymbolKind.Function || method.Name == actualReceiver.Class!.Name)
                            {
                                Fail(callee, "LYC329", $"No instance method '{Text(callee)}' exists on {SemanticTypes.Name(actualReceiver)}.");
                            }
                            if (method!.IsFinalHook())
                            {
                                Fail(callee, "LYC332", "A final hook may only be invoked by generated destruction.");
                            }
                            CheckArguments(node, method!, scope, 1);
                            SemanticFact callFact = new(analysis, node, method!.Type!, method);
                            method.ParticipantView?.AddViewFact(callFact);
                            return method.Type!;
                        }
                        ArrayOperation operation = Text(callee) switch
                        {
                            "reserve" => ArrayOperation.Reserve,
                            "resize" => ArrayOperation.Resize,
                            "truncate" => ArrayOperation.Truncate,
                            "push" => ArrayOperation.Push,
                            _ => ArrayOperation.None,
                        };
                        if (operation == ArrayOperation.None)
                        {
                            Unsupported(callee);
                        }
                        if (node.ChildCount() != 2)
                        {
                            Fail(callee, "LYC303", $"Array method '{Text(callee)}' expects 1 argument(s), got {node.ChildCount() - 1}.");
                        }
                        SyntaxNode receiver = callee.ChildAt(0);
                        SemanticType arrayType = CheckExpression(receiver, scope);
                        SemanticSymbol owner = WritableRoot(receiver, scope);
                        bool nested = receiver.Kind == SyntaxKind.Index;
                        if (nested)
                        {
                            protectedArrays.Push(owner);
                        }
                        if (arrayType.ArrayDepth == 0)
                        {
                            Fail(receiver, "LYC323", "Expected an array argument.");
                        }
                        SemanticType argumentType = operation == ArrayOperation.Push
                            ? SemanticTypes.ElementOf(arrayType) : analysis.ScalarType(ScalarKind.U64);
                        SemanticType supplied = CheckExpression(node.ChildAt(1), scope, argumentType);
                        if (operation == ArrayOperation.Push)
                        {
                            RequireAggregateElementView(node.ChildAt(1), node.ChildAt(1).Fact!.ParticipantView);
                        }
                        RequireType(node.ChildAt(1), supplied, argumentType);
                        if (nested)
                        {
                            protectedArrays.Pop();
                        }
                        _ = new SemanticFact(analysis, node, analysis.ScalarType(ScalarKind.Unit), null,
                            arrayOperation: operation);
                        return analysis.ScalarType(ScalarKind.Unit);
                    }
                    SyntaxNode callableName = callee.Kind == SyntaxKind.Generic ? callee.ChildAt(0) : callee;
                    if (callableName.Kind != SyntaxKind.Name)
                    {
                        Unsupported(callee);
                    }
                    SemanticSymbol? target = scope.Lookup(Text(callableName));
                    if (target is null)
                    {
                        Fail(callableName, "LYC301", $"Unknown name '{Text(callableName)}'.");
                    }
                    SemanticSymbol? constructedClass = target!.Kind == SymbolKind.Class ? target : null;
                    if (constructedClass is not null)
                    {
                        target = constructedClass.MemberScope!.LookupMember(constructedClass.Name);
                        if (target?.Kind != SymbolKind.Function)
                        {
                            Fail(callee, "LYC329", $"Class '{constructedClass.Name}' has no constructor.");
                        }
                    }
                    if (target!.Kind != SymbolKind.Function)
                    {
                        Unsupported(callee);
                    }
                    List<SemanticType>? checkedArguments = null;
                    if (callee.Kind == SyntaxKind.Generic || target.GenericParameters() is not null)
                    {
                        target = Specialize(node, target, scope, out checkedArguments);
                    }
                    CheckArguments(node, target, scope, constructedClass is null ? 0 : 1,
                        checkedArguments);
                    // A constructor body returns unit; the call produces the new
                    // class identity. The fact still resolves its actual method.
                    resolved = target;
                    type = constructedClass is null ? target.Type!
                        : analysis.ClassType(constructedClass);
                    break;
                default:
                    Unsupported(node);
                    return analysis.ScalarType(ScalarKind.Unit);
            }
            SemanticFact completedFact = new(analysis, node, type, resolved, constant);
            ParticipantBinding? expressionView = node.Kind == SyntaxKind.Group
                || (node.Kind == SyntaxKind.Postfix && Text(node) == "!")
                ? node.ChildAt(0).Fact?.ParticipantView : resolved?.ParticipantView;
            if (expressionView is not null && expressionView.AcceptsViewType(type))
            {
                expressionView.AddViewFact(completedFact);
            }
            return type;
        }
    }
}
