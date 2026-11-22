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
    internal partial Diagnostic? Check(bool executable)
    {
        ClearAnalysis();
        SemanticAnalysis analysis = new(this);
        SemanticScope global = new(analysis, null);
        // These lists are temporary traversal work, not duplicated persistent state.
        List<SemanticSymbol> functions = new();
        List<SemanticSymbol> classes = new();
        // Temporary context for constructor initialization, never model ownership.
        SemanticSymbol? checkingFunction = null;
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
        try
        {
            foreach (SemanticType type in SemanticTypes.Predefined())
            {
                string name = type == SemanticType.ByteArray ? "string" : SemanticTypes.Name(type);
                _ = new SemanticSymbol(global, null, name, SymbolKind.Type, type, false);
            }
            foreach (string name in new[] { "print", "println" })
            {
                SemanticSymbol builtin = new(global, null, name, SymbolKind.Function, SemanticType.Unit, false,
                    name == "print" ? BuiltinKind.Print : BuiltinKind.Println);
                SemanticScope parameters = new(builtin);
                _ = new SemanticSymbol(parameters, null, "text", SymbolKind.Parameter, SemanticType.ByteArray, false);
            }
            SemanticSymbol length = new(global, null, "len", SymbolKind.Function,
                SemanticType.U64, false, BuiltinKind.Length);
            SemanticScope lengthParameters = new(length);
            _ = new SemanticSymbol(lengthParameters, null, "array", SymbolKind.Parameter,
                null, false, acceptsArray: true);
            // Collect every top-level name before signatures and bodies: forward
            // calls and shadowing checks must not depend on declaration order.
            foreach (SourceFile source in Sources)
            {
                SyntaxNode root = source.SyntaxRoot() ?? throw new InvalidOperationException("Parse before checking.");
                for (int i = 0; i < root.ChildCount(); i += 1)
                {
                    SyntaxNode declaration = root.ChildAt(i);
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
                        Unsupported(nameNode);
                    }
                    string name = Text(nameNode);
                    RefuseDuplicate(global, nameNode, name);
                    SemanticSymbol function = new(global, declaration, name, SymbolKind.Function, SemanticType.Unit, false);
                    functions.Add(function);
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
                        if (fieldType.Class is not null || fieldType == SemanticType.Unit)
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
                        SymbolKind.Function, SemanticType.Unit, false);
                    functions.Add(method);
                }
            }
            foreach (SemanticSymbol function in functions)
            {
                SemanticScope scope = new(function);
                SyntaxNode declaration = function.Declaration!;
                for (int i = 1; i < declaration.ChildCount(); i += 1)
                {
                    SyntaxNode child = declaration.ChildAt(i);
                    if (child.Kind == SyntaxKind.Parameters)
                    {
                        for (int j = 0; j < child.ChildCount(); j += 1)
                        {
                            SyntaxNode parameter = child.ChildAt(j);
                            string name = Text(parameter);
                            RefuseDuplicate(scope, parameter, name);
                            if (parameter.ChildCount() == 0 && name == "self"
                                && j == 0 && function.Scope!.Class is SemanticSymbol owner)
                            {
                                SemanticType selfType = new(ScalarKind.Unit, 0, owner);
                                _ = new SemanticSymbol(scope, parameter, name,
                                    SymbolKind.Parameter, selfType, false);
                                continue;
                            }
                            if (parameter.ChildCount() != 1)
                            {
                                Unsupported(parameter);
                            }
                            SyntaxNode parameterType = parameter.ChildAt(0);
                            bool isReference = parameterType.Kind == SyntaxKind.TypePrefix && Text(parameterType) == "ref";
                            SemanticType type = ReadType(isReference ? parameterType.ChildAt(0) : parameterType, scope);
                            _ = new SemanticSymbol(scope, parameter, name, SymbolKind.Parameter,
                                type, isReference, isReference: isReference);
                        }
                    }
                    else if (child.Kind == SyntaxKind.ResultType)
                    {
                        function.SetType(ReadType(child.ChildAt(0), scope));
                    }
                    else if (child.Kind != SyntaxKind.Block && child.Kind != SyntaxKind.Visibility)
                    {
                        Unsupported(child);
                    }
                }
            }
            // Receiver identity comes from the actual member-scope parent.
            // Constructor bodies return unit; construction expressions will
            // supply the nominal object separately from that body result.
            foreach (SemanticSymbol function in functions)
            {
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
                if (function.Name == owner.Name && function.Type != SemanticType.Unit)
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
            if (executable && (main!.FunctionScope!.ParameterCount() != 0 || main.Type != SemanticType.Unit))
            {
                Fail(main!.Declaration!, "LYC305", "main must have no parameters and return unit.");
            }
            Dictionary<SemanticSymbol, SyntaxNode> bodies = new();
            foreach (SemanticSymbol function in functions)
            {
                SyntaxNode? body = null;
                SyntaxNode declaration = function.Declaration!;
                for (int i = 0; i < declaration.ChildCount(); i += 1)
                {
                    if (declaration.ChildAt(i).Kind == SyntaxKind.Block)
                    {
                        body = declaration.ChildAt(i);
                    }
                }
                if (body is null)
                {
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
                bool continues = CheckBlock(body!, scope, function);
                if (continues)
                {
                    RequireInitializedReceiver(declaration);
                }
                bodies.Add(function, body!);
                if (continues && function.Type != SemanticType.Unit)
                {
                    Fail(declaration, "LYC309", $"Function '{function.Name}' may finish without returning {SemanticTypes.Name(function.Type!.Value)}.");
                }
            }
            // Effects and ordered overlap obligations are monotone, finite sets
            // of formal-parameter paths. All bodies have facts before iteration,
            // so forward calls and recursive groups use the same analysis.
            Dictionary<SemanticSymbol, HashSet<(SemanticSymbol Root, int Depth)>> effects = new();
            Dictionary<SemanticSymbol, HashSet<((SemanticSymbol Root, int Depth) Changed,
                (SemanticSymbol Root, int Depth) Used)>> obligations = new();
            foreach (SemanticSymbol function in functions)
            {
                effects.Add(function, new());
                obligations.Add(function, new());
            }
            Dictionary<SemanticSymbol, HashSet<SemanticSymbol>> classEffects = new();
            foreach (SemanticSymbol function in functions)
            {
                classEffects.Add(function, new());
            }
            bool changed;
            do
            {
                changed = false;
                foreach (SemanticSymbol function in functions)
                {
                    changed |= CheckBorrows(function, bodies[function], effects, obligations, classEffects, false);
                }
            }
            while (changed);
            foreach (SemanticSymbol function in functions)
            {
                CheckBorrows(function, bodies[function], effects, obligations, classEffects, true);
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
            Dictionary<SemanticSymbol, HashSet<(SemanticSymbol Root, int Depth)>> effects,
            Dictionary<SemanticSymbol, HashSet<((SemanticSymbol Root, int Depth) Changed,
                (SemanticSymbol Root, int Depth) Used)>> obligations,
            Dictionary<SemanticSymbol, HashSet<SemanticSymbol>> classEffects,
            bool diagnose)
        {
            bool summaryChanged = false;
            List<(SemanticSymbol Root, int Depth)> paths = new();
            Dictionary<SemanticSymbol, int> aliases = new();
            List<(SyntaxNode Node, int Operation, int Borrow, List<int> Next)> graph = new();
            Dictionary<int, ((SemanticSymbol Root, int Depth) Changed,
                (SemanticSymbol Root, int Depth) Used)> callRequirements = new();
            // These are temporary graph identities, not persistent model parents.
            // Class effects are separate from array-path mutations: allocation
            // does not invalidate indexed object handles; destruction does.
            List<SemanticSymbol> objectClasses = new();
            Dictionary<SemanticSymbol, int> objectBindings = new();
            Dictionary<SyntaxNode, int> objectValues = new();
            Dictionary<int, SemanticSymbol> destroyedClasses = new();
            Dictionary<int, int> selectedObjects = new();
            List<int> frontier = new();
            Stack<(int Header, int Exit)> loopEdges = new();
            int entry = Event(body, 0, -1);
            for (int i = 0; i < function.FunctionScope!.ParameterCount(); i += 1)
            {
                SemanticSymbol parameter = function.FunctionScope.ParameterAt(i);
                if (parameter.Type is SemanticType parameterType
                    && parameterType.ArrayDepth == 0 && parameterType.Class is not null)
                {
                    objectBindings.Add(parameter, NewObject(parameter.Declaration!, parameterType.Class));
                }
                if (parameter.IsReference)
                {
                    aliases.Add(parameter, Acquire(parameter.Declaration!, (parameter, 0)));
                }
            }
            BuildBlock(body);
            UseConstructedReceiver(body);

            // Keep the cause, not just an invalid bit: distinct formal roots may
            // overlap only at a caller. Acquisition kills this handle's history,
            // but does not revive any other alias to the previous allocation.
            HashSet<(int Borrow, (SemanticSymbol Root, int Depth) Cause)>?[] incoming =
                new HashSet<(int Borrow, (SemanticSymbol Root, int Depth) Cause)>?[graph.Count];
            incoming[entry] = new();
            HashSet<int>?[] invalidObjects = new HashSet<int>?[graph.Count];
            invalidObjects[entry] = new();
            Queue<int> pending = new();
            pending.Enqueue(entry);
            while (pending.Count != 0)
            {
                int index = pending.Dequeue();
                var step = graph[index];
                var state = new HashSet<(int Borrow, (SemanticSymbol Root, int Depth) Cause)>(incoming[index]!);
                HashSet<int> objectState = new(invalidObjects[index]!);
                if (step.Operation == 5)
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
                    SemanticSymbol owner = destroyedClasses[index];
                    summaryChanged |= classEffects[function].Add(owner);
                    for (int i = 0; i < objectClasses.Count; i += 1)
                    {
                        if (objectClasses[i] == owner)
                        {
                            objectState.Add(i);
                        }
                    }
                }
                else if (step.Operation == 1)
                {
                    state.RemoveWhere(item => item.Borrow == step.Borrow);
                }
                else if (step.Operation == 2)
                {
                    foreach (var item in state)
                    {
                        if (item.Borrow == step.Borrow)
                        {
                            RequireSafe(step.Node, item.Cause, paths[step.Borrow]);
                        }
                    }
                }
                else if (step.Operation == 3)
                {
                    var mutation = paths[step.Borrow];
                    if (IsFormal(mutation.Root))
                    {
                        summaryChanged |= effects[function].Add(mutation);
                    }
                    for (int i = 0; i < paths.Count; i += 1)
                    {
                        var borrowed = paths[i];
                        if ((borrowed.Root == mutation.Root && borrowed.Depth > mutation.Depth)
                            || (borrowed.Root != mutation.Root
                                && IsFormal(borrowed.Root) && IsFormal(mutation.Root)))
                        {
                            state.Add((i, mutation));
                        }
                    }
                }
                else if (step.Operation == 4)
                {
                    var requirement = callRequirements[index];
                    RequireSafe(step.Node, requirement.Changed, requirement.Used);
                }
                foreach (int next in step.Next)
                {
                    if (incoming[next] is null)
                    {
                        incoming[next] = new(state);
                        invalidObjects[next] = new(objectState);
                        pending.Enqueue(next);
                    }
                    else
                    {
                        int before = incoming[next]!.Count + invalidObjects[next]!.Count;
                        incoming[next]!.UnionWith(state);
                        invalidObjects[next]!.UnionWith(objectState);
                        if (incoming[next]!.Count + invalidObjects[next]!.Count != before)
                        {
                            pending.Enqueue(next);
                        }
                    }
                }
            }
            return summaryChanged;

            bool IsFormal(SemanticSymbol symbol)
            {
                return symbol.Kind == SymbolKind.Parameter && symbol.IsReference
                    && symbol.Scope == function.FunctionScope;
            }
            void RequireSafe(SyntaxNode node, (SemanticSymbol Root, int Depth) mutation,
                (SemanticSymbol Root, int Depth) used)
            {
                if (mutation.Root == used.Root && used.Depth > mutation.Depth)
                {
                    if (diagnose)
                    {
                        Fail(node, "LYC326", "Borrowed storage may have been invalidated.");
                    }
                }
                else if (mutation.Root != used.Root && IsFormal(mutation.Root) && IsFormal(used.Root))
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
                    Event(node, 6, objectBindings[receiver]);
                }
            }
            int NewObject(SyntaxNode node, SemanticSymbol owner)
            {
                int handle = objectClasses.Count;
                objectClasses.Add(owner);
                Event(node, 5, handle);
                return handle;
            }
            void UseObject(SyntaxNode node)
            {
                if (objectValues.TryGetValue(node, out int handle))
                {
                    Event(node, 6, handle);
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
            int Acquire(SyntaxNode node, (SemanticSymbol Root, int Depth) path)
            {
                int handle = paths.Count;
                paths.Add(path);
                Event(node, 1, handle);
                return handle;
            }
            void Use(SyntaxNode node, int handle)
            {
                Event(node, 2, handle);
                if (selectedObjects.TryGetValue(handle, out int selected))
                {
                    Event(node, 6, selected);
                }
            }
            // Select each containing descriptor before evaluating the next
            // index. Later index/argument effects must not invalidate it.
            int Select(SyntaxNode node)
            {
                if (node.Kind == SyntaxKind.Name)
                {
                    SemanticSymbol symbol = node.Fact!.Symbol!;
                    if (aliases.TryGetValue(symbol, out int alias))
                    {
                        Use(node, alias);
                        return Acquire(node, paths[alias]);
                    }
                    return Acquire(node, (symbol, 0));
                }
                if (node.Kind == SyntaxKind.Member)
                {
                    // Direct field assignment currently keeps no escaping borrow.
                    // A field is the root of any owned array backing, distinct
                    // from the local variable holding the object's index.
                    SyntaxNode receiver = node.ChildAt(0);
                    BuildExpression(receiver);
                    UseObject(receiver);
                    int selected = Acquire(node, (node.Fact!.Symbol!, 0));
                    selectedObjects.Add(selected, objectValues[receiver]);
                    return selected;
                }
                int parent = Select(node.ChildAt(0));
                BuildExpression(node.ChildAt(1));
                Use(node, parent);
                var path = paths[parent];
                return Acquire(node, (path.Root, path.Depth + 1));
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
                    // Completion runs after children. Each saved class-valued
                    // expression has its own identity across later effects.
                    if (node.Fact is SemanticFact fact
                        && fact.Type.ArrayDepth == 0 && fact.Type.Class is not null)
                    {
                        work.Push(() =>
                        {
                            objectValues[node] = NewObject(node, fact.Type.Class);
                        });
                    }
                    if (node.Kind == SyntaxKind.Name)
                    {
                        if (node.Fact?.Symbol is SemanticSymbol symbol)
                        {
                            if (aliases.TryGetValue(symbol, out int alias))
                            {
                                Use(node, alias);
                            }
                            if (objectBindings.TryGetValue(symbol, out int objectHandle))
                            {
                                Event(node, 6, objectHandle);
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
                        if (node.Fact!.ArrayOperation != ArrayOperation.None)
                        {
                            int receiver = Select(node.ChildAt(0).ChildAt(0));
                            BuildExpression(node.ChildAt(1));
                            Use(node, receiver);
                            Event(node, 3, receiver);
                            return;
                        }
                        if (node.Fact.ClassOperation == ClassOperation.Destroy)
                        {
                            SyntaxNode receiver = node.ChildAt(0).ChildAt(0);
                            BuildExpression(receiver);
                            UseObject(receiver);
                            DestroyClass(node, node.Fact.Symbol!);
                            return;
                        }
                        List<(SyntaxNode Node, int Handle)> arguments = new();
                        SemanticSymbol target = node.Fact.Symbol!;
                        int parameterOffset = target.Scope?.Class is null ? 0 : 1;
                        if (node.ChildAt(0).Kind == SyntaxKind.Member)
                        {
                            BuildExpression(node.ChildAt(0).ChildAt(0));
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
                        foreach (var argument in arguments)
                        {
                            Use(argument.Node, argument.Handle);
                        }
                        if (node.ChildAt(0).Kind == SyntaxKind.Member)
                        {
                            UseObject(node.ChildAt(0).ChildAt(0));
                        }
                        for (int i = 1; i < node.ChildCount(); i += 1)
                        {
                            UseObject(node.ChildAt(i));
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
                            Dictionary<SemanticSymbol, (SemanticSymbol Root, int Depth)> actuals = new();
                            int referenceIndex = 0;
                            for (int i = 0; i < target.FunctionScope!.ParameterCount(); i += 1)
                            {
                                SemanticSymbol parameter = target.FunctionScope.ParameterAt(i);
                                if (parameter.IsReference)
                                {
                                    actuals.Add(parameter, paths[arguments[referenceIndex].Handle]);
                                    referenceIndex += 1;
                                }
                            }
                            (SemanticSymbol Root, int Depth) Substitute((SemanticSymbol Root, int Depth) formal)
                            {
                                var actual = actuals[formal.Root];
                                return (actual.Root, actual.Depth + formal.Depth);
                            }
                            // Requirements describe internal order. Do not turn
                            // all argument uses into post-mutation uses: that
                            // would reject a valid write-before-grow helper.
                            foreach (var requirement in obligations[target])
                            {
                                int check = Event(node, 4, -1);
                                callRequirements.Add(check,
                                    (Substitute(requirement.Changed), Substitute(requirement.Used)));
                            }
                            foreach (var effect in effects[target])
                            {
                                int handle = paths.Count;
                                paths.Add(Substitute(effect));
                                Event(node, 3, handle);
                            }
                        }
                        return;
                    }
                    if (node.Kind == SyntaxKind.Binary && Text(node) is "&&" or "||")
                    {
                        BuildExpression(node.ChildAt(0));
                        List<int> skipped = new(frontier);
                        BuildExpression(node.ChildAt(1));
                        frontier.AddRange(skipped);
                        return;
                    }
                    work.Push(() =>
                    {
                        for (int i = 0; i < node.ChildCount(); i += 1)
                        {
                            UseObject(node.ChildAt(i));
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
            void BuildIf(SyntaxNode node)
            {
                BuildExpression(node.ChildAt(0));
                List<int> start = new(frontier);
                BuildBlock(node.ChildAt(1));
                List<int> thenExit = new(frontier);
                frontier = start;
                if (node.ChildCount() > 2)
                {
                    SyntaxNode alternative = node.ChildAt(2);
                    if (alternative.Kind == SyntaxKind.If)
                    {
                        BuildIf(alternative);
                    }
                    else
                    {
                        BuildBlock(alternative);
                    }
                }
                frontier.AddRange(thenExit);
            }
            void BuildBlock(SyntaxNode block)
            {
                for (int i = 0; i < block.ChildCount(); i += 1)
                {
                    SyntaxNode statement = block.ChildAt(i);
                    switch (statement.Kind)
                    {
                        case SyntaxKind.Binding:
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
                            }
                            // Definite assignment independently prevents reads of
                            // an uninitialized local. Its lifetime identity exists
                            // even when the first value arrives in a later store.
                            if (symbol.Type is SemanticType bindingType
                                && bindingType.ArrayDepth == 0 && bindingType.Class is not null)
                            {
                                objectBindings.Add(symbol, NewObject(statement, bindingType.Class));
                            }
                            break;
                        case SyntaxKind.Assignment:
                            SyntaxNode target = statement.ChildAt(0);
                            int destination = Select(target);
                            BuildExpression(statement.ChildAt(1));
                            Use(target, destination);
                            UseObject(statement.ChildAt(1));
                            if (target.Kind == SyntaxKind.Name
                                && objectBindings.TryGetValue(target.Fact!.Symbol!, out int assignedObject))
                            {
                                Event(target, 5, assignedObject);
                            }
                            if (target.Fact!.Type.ArrayDepth > 0)
                            {
                                Event(target, 3, destination);
                            }
                            break;
                        case SyntaxKind.ExpressionStatement:
                            BuildExpression(statement.ChildAt(0));
                            break;
                        case SyntaxKind.While:
                            int header = Event(statement, 0, -1);
                            List<int> before = frontier;
                            frontier = new();
                            int exit = Event(statement, 0, -1);
                            frontier = before;
                            loopEdges.Push((header, exit));
                            BuildExpression(statement.ChildAt(0));
                            foreach (int condition in frontier)
                            {
                                graph[condition].Next.Add(exit);
                            }
                            BuildBlock(statement.ChildAt(1));
                            foreach (int back in frontier)
                            {
                                graph[back].Next.Add(header);
                            }
                            loopEdges.Pop();
                            frontier = new() { exit };
                            break;
                        case SyntaxKind.Break:
                        case SyntaxKind.Continue:
                            var loop = loopEdges.Peek();
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
                            }
                            UseConstructedReceiver(statement);
                            frontier = new();
                            break;
                    }
                }
            }
        }

        // Each block is checked in a distinct lexical scope owned by analysis;
        // its assignment sets are temporary work.
        // The return value describes reachability, not whether a return appeared
        // somewhere in the syntax: only continuing paths reach a later read.
        bool CheckBlock(SyntaxNode block, SemanticScope parent, SemanticSymbol function,
            bool valueBlock = false, SemanticType? expectedType = null)
        {
            SemanticScope scope = new(analysis, parent);
            bool continues = true;
            SemanticType resultType = SemanticType.Unit;
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
                                RequireType(expression, resultType, expectedType.Value);
                            }
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
                            ? SemanticType.Unit
                            : CheckExpression(statement.ChildAt(0), scope, function.Type);
                        RequireType(statement.ChildCount() == 0 ? function.Declaration! : statement.ChildAt(0),
                            actual, function.Type!.Value);
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
            definitelyAssigned.RemoveWhere(symbol => symbol.Scope == scope);
            possiblyAssigned.RemoveWhere(symbol => symbol.Scope == scope);
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
            RequireType(condition, CheckExpression(condition, scope), SemanticType.Bool);
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
            _ = new SemanticFact(analysis, node, SemanticType.Unit, null);
        }
        bool CheckIf(SyntaxNode node, SemanticScope scope, SemanticSymbol function,
            bool valueRequired = false, SemanticType? expectedType = null)
        {
            if (valueRequired && node.ChildCount() != 3)
            {
                Fail(node, "LYC319", "Value-producing if requires an else branch.");
            }
            SyntaxNode condition = node.ChildAt(0);
            RequireType(condition, CheckExpression(condition, scope), SemanticType.Bool);
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
            SemanticType resultType = SemanticType.Unit;
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
                        RequireType(node.ChildAt(1), thenType, expectedType.Value);
                    }
                    if (elseContinues)
                    {
                        RequireType(node.ChildAt(2), elseType, expectedType.Value);
                    }
                    resultType = expectedType.Value;
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
                    resultType = common!.Value;
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
        void RefuseDuplicate(SemanticScope scope, SyntaxNode node, string name)
        {
            SemanticSymbol? existing = scope.Class is null
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
            if (node.Kind != SyntaxKind.Unary || Text(node) != "ref")
            {
                Fail(node, "LYC325", "Reference argument requires explicit ref storage.");
            }
            SyntaxNode storage = node.ChildAt(0);
            if (storage.Kind is not (SyntaxKind.Name or SyntaxKind.Index))
            {
                Unsupported(storage);
            }
            SemanticSymbol symbol = WritableRoot(storage, scope);
            SemanticType type = CheckExpression(storage, scope);
            _ = new SemanticFact(analysis, node, type, symbol);
            return type;
        }
        void CheckBinding(SyntaxNode node, SemanticScope scope)
        {
            SyntaxNode pattern = node.ChildAt(0);
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
            bool referenceAnnotation = false;
            if (next < node.ChildCount() && node.ChildAt(next).Kind == SyntaxKind.Annotation)
            {
                SyntaxNode annotation = node.ChildAt(next).ChildAt(0);
                referenceAnnotation = annotation.Kind == SyntaxKind.TypePrefix && Text(annotation) == "ref";
                declaredType = ReadType(referenceAnnotation ? annotation.ChildAt(0) : annotation, scope);
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
                if (isReference && declaredType is not null && actual != declaredType.Value)
                {
                    Fail(initializer, "LYC325", "Reference storage type must match exactly.");
                }
                if (declaredType is not null)
                {
                    RequireType(initializer, actual, declaredType.Value);
                }
                else
                {
                    declaredType = actual;
                }
            }
            SemanticSymbol symbol = new(scope, node, name, SymbolKind.Local,
                declaredType!.Value, isReference || Text(node) == "var", isReference: isReference);
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
            if (!IsConstructor())
            {
                return;
            }
            SyntaxNode declaration = checkingFunction!.Scope!.Class!.Declaration!;
            for (int i = 0; i < declaration.ChildCount(); i += 1)
            {
                SemanticSymbol? field = declaration.ChildAt(i).DeclaredSymbol;
                if (field?.Kind == SymbolKind.Field && !definitelyAssigned.Contains(field))
                {
                    Fail(location, "LYC328", $"Field '{field.Name}' is not definitely initialized.");
                }
            }
        }
        SemanticSymbol CheckField(SyntaxNode node, SemanticScope scope, bool read)
        {
            SyntaxNode receiver = node.ChildAt(0);
            SemanticType receiverType;
            SemanticSymbol? receiverSymbol = receiver.Kind == SyntaxKind.Name
                ? scope.Lookup(Text(receiver)) : null;
            if (IsConstructingSelf(receiverSymbol))
            {
                // Selecting a field does not expose the unfinished whole object.
                receiverType = receiverSymbol!.Type!.Value;
                _ = new SemanticFact(analysis, receiver, receiverType, receiverSymbol);
            }
            else
            {
                receiverType = CheckExpression(receiver, scope);
            }
            SemanticSymbol? field = receiverType.ArrayDepth == 0
                ? receiverType.Class?.MemberScope?.LookupMember(Text(node)) : null;
            if (field?.Kind != SymbolKind.Field)
            {
                Fail(node, "LYC329", $"No field '{Text(node)}' exists on {SemanticTypes.Name(receiverType)}.");
            }
            if (read && IsConstructingSelf(receiverSymbol) && !definitelyAssigned.Contains(field!))
            {
                Fail(node, "LYC328", $"Field '{field!.Name}' is not definitely initialized.");
            }
            _ = new SemanticFact(analysis, node, field!.Type!.Value, field);
            return field;
        }
        SemanticSymbol WritableRoot(SyntaxNode node, SemanticScope scope)
        {
            SyntaxNode root = node;
            while (root.Kind == SyntaxKind.Index)
            {
                root = root.ChildAt(0);
            }
            if (root.Kind != SyntaxKind.Name)
            {
                Unsupported(node);
            }
            SemanticSymbol? symbol = scope.Lookup(Text(root));
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
            return symbol;
        }
        void CheckAssignment(SyntaxNode node, SemanticScope scope)
        {
            SyntaxNode target = node.ChildAt(0);
            string assignment = Text(node);
            bool compound = assignment != "=";
            if (target.Kind == SyntaxKind.Member)
            {
                if (compound)
                {
                    Unsupported(node);
                }
                SemanticSymbol field = CheckField(target, scope, false);
                SemanticType supplied = CheckExpression(node.ChildAt(1), scope, field.Type);
                RequireType(node.ChildAt(1), supplied, field.Type!.Value);
                if (IsConstructingSelf(target.ChildAt(0).Fact!.Symbol))
                {
                    MarkAssigned(field);
                }
                return;
            }
            if (target.Kind == SyntaxKind.Index)
            {
                if (compound)
                {
                    Unsupported(node);
                }
                SemanticSymbol owner = WritableRoot(target, scope);
                protectedArrays.Push(owner);
                SemanticType selected = CheckExpression(target, scope);
                SemanticType supplied = CheckExpression(node.ChildAt(1), scope, selected);
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
            if (compound && !definitelyAssigned.Contains(symbol))
            {
                Fail(target, "LYC307", $"Local '{symbol.Name}' is not definitely assigned.");
            }
            SyntaxNode value = node.ChildAt(1);
            SemanticType actual = CheckExpression(value, scope, symbol.Type);
            if (compound)
            {
                SemanticType targetType = symbol.Type!.Value;
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
                RequireType(value, result!.Value, targetType);
                if (assignment == "/=" && value.Fact!.IntegerConstant == BigInteger.Zero)
                {
                    Fail(node, "LYC316", "Integer division or remainder by zero.");
                }
            }
            else
            {
                RequireType(value, actual, symbol.Type!.Value);
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
            _ = new SemanticFact(analysis, target, symbol.Type.Value, symbol);
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
        SemanticType ReadType(SyntaxNode node, SemanticScope scope)
        {
            if (node.Kind == SyntaxKind.ArrayType && node.ChildCount() == 1)
            {
                SemanticType element = ReadType(node.ChildAt(0), scope);
                if (element.ArrayDepth == 0 && element != SemanticType.Bool
                    && !SemanticTypes.IsInteger(element))
                {
                    Unsupported(node);
                }
                return SemanticTypes.ArrayOf(element);
            }
            if (node.Kind == SyntaxKind.TypeName)
            {
                SemanticSymbol? symbol = scope.Lookup(Text(node));
                if (symbol?.Kind == SymbolKind.Type)
                {
                    return symbol.Type!.Value;
                }
                if (symbol?.Kind == SymbolKind.Class)
                {
                    return new SemanticType(ScalarKind.Unit, 0, symbol);
                }
            }
            Unsupported(node);
            return SemanticType.Unit;
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
            SemanticType type = SemanticType.I32;
            if (position < spelling.Length)
            {
                string suffix = spelling[position..];
                foreach (SemanticType candidate in SemanticTypes.Predefined())
                {
                    if (SemanticTypes.IsInteger(candidate) && SemanticTypes.Name(candidate) == suffix)
                    {
                        type = candidate;
                        break;
                    }
                }
            }
            else if (expectedType is not null && SemanticTypes.IsInteger(expectedType.Value))
            {
                type = expectedType.Value;
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
        Dictionary<SyntaxNode, SemanticType?> DiscoverHints(SyntaxNode root, SemanticScope scope,
            Dictionary<string, SemanticType?>? locals = null)
        {
            // Precedence climbing can build arbitrarily deep left-associated trees.
            // Discover hints bottom-up once, using temporary algorithm storage rather
            // than recursion or repeated subtree scans. No facts or effects occur here.
            Dictionary<SyntaxNode, SemanticType?> hints = new();
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
                    || (node.Kind == SyntaxKind.Unary && Text(node) is "-" or "!-" or "!" or "ref");
                if (!item.Ready && node.Kind is SyntaxKind.Array or SyntaxKind.Index or SyntaxKind.Member)
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
                if (node.Kind == SyntaxKind.Member)
                {
                    SemanticType? receiver = hints[node.ChildAt(0)];
                    if (receiver is SemanticType owner && owner.ArrayDepth == 0)
                    {
                        SemanticSymbol? field = owner.Class?.MemberScope?.LookupMember(Text(node));
                        if (field?.Kind == SymbolKind.Field)
                        {
                            hint = field.Type;
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
                    hint = SemanticType.Bool;
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
                        hint = SemanticTypes.ArithmeticType(left.Value, right.Value);
                    }
                }
                else if (unary && node.Kind != SyntaxKind.Cast)
                {
                    hint = hints[node.ChildAt(0)];
                }
                else if (node.Kind == SyntaxKind.Cast)
                {
                    // A cast fixes its result type, not its operand's type.
                    hint = ReadType(node.ChildAt(1), scope);
                }
                else if (node.Kind == SyntaxKind.Integer)
                {
                    string spelling = Text(node);
                    foreach (SemanticType candidate in SemanticTypes.Predefined())
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
                    hint = SemanticType.ByteArray;
                }
                else if (node.Kind == SyntaxKind.Literal && Text(node) is "true" or "false")
                {
                    hint = SemanticType.Bool;
                }
                else if (node.Kind == SyntaxKind.Unit)
                {
                    hint = SemanticType.Unit;
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
                        }
                    }
                }
                else if (node.Kind == SyntaxKind.Call && node.ChildAt(0).Kind == SyntaxKind.Member)
                {
                    SyntaxNode member = node.ChildAt(0);
                    SemanticType? receiver = hints[member.ChildAt(0)];
                    if (receiver is SemanticType owner && owner.ArrayDepth == 0)
                    {
                        SemanticSymbol? method = owner.Class?.MemberScope?.LookupMember(Text(member));
                        if (method?.Kind == SymbolKind.Function)
                        {
                            hint = method.Type;
                        }
                    }
                }
                else if (node.Kind == SyntaxKind.Call && node.ChildAt(0).Kind == SyntaxKind.Name)
                {
                    SemanticSymbol? function = scope.Lookup(Text(node.ChildAt(0)));
                    if (function?.Kind == SymbolKind.Function)
                    {
                        hint = function.Type;
                    }
                    else if (function?.Kind == SymbolKind.Class)
                    {
                        hint = new SemanticType(ScalarKind.Unit, 0, function);
                    }
                }
                hints.Add(node, hint);
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
                : (Type: (SemanticType?)SemanticType.Unit, Continues: true);
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
            SemanticType leftType = left.Type ?? SemanticType.I32;
            SemanticType rightType = right.Type ?? SemanticType.I32;
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
                    if (pattern.Kind != SyntaxKind.PatternName || pattern.ChildCount() != 1)
                    {
                        continue;
                    }
                    int next = 1;
                    SemanticType? type = null;
                    if (next < statement.ChildCount() && statement.ChildAt(next).Kind == SyntaxKind.Annotation)
                    {
                        // Preview the value obtained by reading this binding;
                        // reference permission/acquisition remains the real check's job.
                        SyntaxNode annotation = statement.ChildAt(next).ChildAt(0);
                        bool reference = annotation.Kind == SyntaxKind.TypePrefix && Text(annotation) == "ref";
                        type = ReadType(reference ? annotation.ChildAt(0) : annotation, scope);
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
                            type = SemanticType.I32;
                        }
                    }
                    locals[Text(pattern.ChildAt(0))] = type;
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
            return (SemanticType.Unit, true);
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
                if (node.Kind == SyntaxKind.Array && item.Stage <= node.ChildCount())
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
                        work.Push((node.ChildAt(0), SemanticType.Bool, 0));
                    }
                    else
                    {
                        skipped.Add(node, (new(definitelyAssigned), new(possiblyAssigned)));
                        work.Push((node, item.Expected, 2));
                        work.Push((node.ChildAt(1), SemanticType.Bool, 0));
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
                    work.Push((right, logical ? SemanticType.Bool : hints[right] ?? hints[left], 0));
                    work.Push((left, logical ? SemanticType.Bool : hints[left] ?? hints[right], 0));
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
                    work.Push((node.ChildAt(0), SemanticType.Bool, 0));
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
        void CheckArguments(SyntaxNode call, SemanticSymbol target, SemanticScope scope, int offset)
        {
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
                SemanticType expected = parameter.Type!.Value;
                if (parameter.IsReference)
                {
                    SemanticType referenced = CheckReference(argument, scope);
                    if (referenced != expected)
                    {
                        Fail(argument, "LYC325", "Reference storage type must match exactly.");
                    }
                    continue;
                }
                SemanticType actual = CheckExpression(argument, scope, expected);
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
                        return SemanticType.Unit;
                    }
                    SemanticType elementType = SemanticTypes.ElementOf(type);
                    if (elementType.ArrayDepth == 0 && elementType != SemanticType.Bool
                        && !SemanticTypes.IsInteger(elementType))
                    {
                        Unsupported(node);
                    }
                    for (int i = 0; i < node.ChildCount(); i += 1)
                    {
                        SyntaxNode element = node.ChildAt(i);
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
                case SyntaxKind.If:
                    SemanticScope functionScope = scope;
                    while (functionScope.Function is null && functionScope.Parent is not null)
                    {
                        functionScope = functionScope.Parent;
                    }
                    if (functionScope.Function is null)
                    {
                        Unsupported(node);
                    }
                    CheckIf(node, scope, functionScope.Function!, true, expectedType);
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
                            Fail(node, "LYC318", $"Integer conversion is outside {SemanticTypes.Name(type)} range.");
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
                            Fail(node, "LYC311", $"Integer constant operation is outside {SemanticTypes.Name(type)} range.");
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
                    type = selected!.Value;
                    BigInteger? leftConstant = leftNode.Fact!.IntegerConstant;
                    BigInteger? rightConstant = rightNode.Fact!.IntegerConstant;
                    if (Text(node) is "/" or "%" && rightConstant == BigInteger.Zero)
                    {
                        Fail(node, "LYC316", "Integer division or remainder by zero.");
                    }
                    if (leftConstant is not null && rightConstant is not null)
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
                            Fail(node, "LYC311", $"Integer constant operation is outside {SemanticTypes.Name(type)} range.");
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
                        Fail(node, "LYC317", "Integer shift count is outside operand width.");
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
                        && node.ChildAt(0).Fact!.Type == SemanticType.Bool
                        && node.ChildAt(1).Fact!.Type == SemanticType.Bool)
                    {
                        type = SemanticType.Bool;
                        break;
                    }
                    if (!SemanticTypes.IsInteger(node.ChildAt(0).Fact!.Type)
                        || !SemanticTypes.IsInteger(node.ChildAt(1).Fact!.Type))
                    {
                        Fail(node, "LYC313", "Integer comparison requires integer operands.");
                    }
                    type = SemanticType.Bool;
                    break;
                case SyntaxKind.Binary when Text(node) is "&&" or "||":
                    RequireType(node.ChildAt(0), node.ChildAt(0).Fact!.Type, SemanticType.Bool);
                    RequireType(node.ChildAt(1), node.ChildAt(1).Fact!.Type, SemanticType.Bool);
                    type = SemanticType.Bool;
                    break;
                case SyntaxKind.Unary when Text(node) == "!":
                    RequireType(node.ChildAt(0), node.ChildAt(0).Fact!.Type, SemanticType.Bool);
                    type = SemanticType.Bool;
                    break;
                case SyntaxKind.String:
                    type = SemanticType.ByteArray;
                    break;
                case SyntaxKind.Literal when Text(node) is "true" or "false":
                    type = SemanticType.Bool;
                    break;
                case SyntaxKind.Unit:
                    type = SemanticType.Unit;
                    break;
                case SyntaxKind.Group:
                    type = node.ChildAt(0).Fact!.Type;
                    constant = node.ChildAt(0).Fact!.IntegerConstant;
                    break;
                case SyntaxKind.Member:
                    return CheckField(node, scope, true).Type!.Value;
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
                    if (!definitelyAssigned.Contains(resolved))
                    {
                        Fail(node, "LYC307", $"Local '{resolved.Name}' is not definitely assigned.");
                    }
                    if (IsConstructingSelf(resolved))
                    {
                        // Copying/passing self exposes every field, unlike a direct
                        // field selection. Do not leak an unfinished receiver.
                        RequireInitializedReceiver(node);
                    }
                    type = resolved.Type!.Value;
                    break;
                case SyntaxKind.Call:
                    SyntaxNode callee = node.ChildAt(0);
                    if (callee.Kind == SyntaxKind.Member)
                    {
                        SyntaxNode receiverNode = callee.ChildAt(0);
                        SemanticType? receiverHint = DiscoverHints(receiverNode, scope)[receiverNode];
                        if (receiverHint is SemanticType receiverClass
                            && receiverClass.ArrayDepth == 0 && receiverClass.Class is not null)
                        {
                            SemanticType actualReceiver = CheckExpression(receiverNode, scope);
                            if (Text(callee) == "destroy")
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
                                _ = new SemanticFact(analysis, node, SemanticType.Unit, destroyedClass,
                                    classOperation: ClassOperation.Destroy);
                                return SemanticType.Unit;
                            }
                            SemanticSymbol? method = actualReceiver.Class!.MemberScope!.LookupMember(Text(callee));
                            if (method?.Kind != SymbolKind.Function || method.Name == actualReceiver.Class.Name)
                            {
                                Fail(callee, "LYC329", $"No instance method '{Text(callee)}' exists on {SemanticTypes.Name(actualReceiver)}.");
                            }
                            CheckArguments(node, method!, scope, 1);
                            _ = new SemanticFact(analysis, node, method!.Type!.Value, method);
                            return method.Type.Value;
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
                        SemanticSymbol owner = WritableRoot(receiver, scope);
                        bool nested = receiver.Kind == SyntaxKind.Index;
                        if (nested)
                        {
                            protectedArrays.Push(owner);
                        }
                        SemanticType arrayType = CheckExpression(receiver, scope);
                        if (arrayType.ArrayDepth == 0)
                        {
                            Fail(receiver, "LYC323", "Expected an array argument.");
                        }
                        SemanticType argumentType = operation == ArrayOperation.Push
                            ? SemanticTypes.ElementOf(arrayType) : SemanticType.U64;
                        SemanticType supplied = CheckExpression(node.ChildAt(1), scope, argumentType);
                        RequireType(node.ChildAt(1), supplied, argumentType);
                        if (nested)
                        {
                            protectedArrays.Pop();
                        }
                        _ = new SemanticFact(analysis, node, SemanticType.Unit, null,
                            arrayOperation: operation);
                        return SemanticType.Unit;
                    }
                    if (callee.Kind != SyntaxKind.Name)
                    {
                        Unsupported(callee);
                    }
                    SemanticSymbol? target = scope.Lookup(Text(callee));
                    if (target is null)
                    {
                        Fail(callee, "LYC301", $"Unknown name '{Text(callee)}'.");
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
                    CheckArguments(node, target, scope, constructedClass is null ? 0 : 1);
                    // A constructor body returns unit; the call produces the new
                    // class identity. The fact still resolves its actual method.
                    resolved = target;
                    type = constructedClass is null ? target.Type!.Value
                        : new SemanticType(ScalarKind.Unit, 0, constructedClass);
                    break;
                default:
                    Unsupported(node);
                    return SemanticType.Unit;
            }
            _ = new SemanticFact(analysis, node, type, resolved, constant);
            return type;
        }
    }
}
