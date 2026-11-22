using System.Collections.Generic;
using System.Numerics;

namespace Lyric;

// A compilation owns one analysis of its current source trees. Source/tree
// mutation invalidates the entire analysis, including cross-file resolutions.
internal sealed partial class Compilation
{
    private SemanticAnalysis? Analysis;

    // Construction/destruction registration updates both ends; refusal changes neither.
    internal partial void AddAnalysis(SemanticAnalysis analysis);
    internal partial void RemoveAnalysis(SemanticAnalysis analysis);
    // Idempotently retires all semantic state before changing its source inputs.
    internal partial void ClearAnalysis();
}

// Identity-bearing owner of semantic state, not a value-like snapshot. Its
// compilation backlink is established by construction and cleared by destruction.
internal sealed partial class SemanticAnalysis
{
    internal Compilation? Compilation;
    internal bool Destroyed;
    private readonly List<SemanticScope> Scopes = new();

    // Analysis owns scope lifetimes. Registration updates the scope backlink.
    internal partial void AddScope(SemanticScope scope);
    internal partial void RemoveScope(SemanticScope scope);

    internal partial SemanticAnalysis(Compilation compilation);
    internal partial void Destroy();
}

// Identity-bearing lexical scope. Analysis owns its lifetime; Parent/Children
// are weak structure for lookup. No scope may move to another analysis.
internal sealed partial class SemanticScope
{
    internal SemanticAnalysis? Analysis;
    internal SemanticScope? Parent;
    internal bool Destroyed;
    private readonly List<SemanticScope> Children = new();

    // A null parent creates a global scope. Otherwise parent must belong to the
    // supplied analysis. Failure leaves no escaped membership.
    internal partial SemanticScope(SemanticAnalysis analysis, SemanticScope? parent);
    internal partial void AddChild(SemanticScope child);
    internal partial void RemoveChild(SemanticScope child);
    // Detaches weak children without destroying them; analysis owns their lives.
    internal partial void Destroy();
}

// Value-like identity of the supported external standard operations. The checker
// assigns this at declaration creation; backend dispatch never repeats name lookup.
internal enum BuiltinKind
{
    None,
    Print,
    Println,
    Length,
}

// Value-like checked class operation. The fact's Symbol relationship identifies
// the owning class; emission must not rediscover this operation from source text.
internal enum ClassOperation
{
    None,
    Destroy,
}

// Value-like checked intrinsic array method; not a runtime method registry.
internal enum ArrayOperation
{
    None,
    Reserve,
    Resize,
    Truncate,
    Push,
}

// Closed declaration roles for the initial semantic checker.
internal enum SymbolKind
{
    Function,
    Parameter,
    Local,
    Type,
    Class,
    Field,
}

internal sealed partial class SemanticScope
{
    private readonly List<SemanticSymbol> Symbols = new();

    // Lexical lookup skips qualified class members; siblings are never searched.
    internal partial SemanticSymbol? Lookup(string name);
    // Searches only this scope, for explicitly qualified member selection.
    internal partial SemanticSymbol? LookupMember(string name);
    // Strong membership. Class members share a qualified namespace; other
    // declarations refuse an already-visible lexical name before mutation.
    internal partial void AddSymbol(SemanticSymbol symbol);
    internal partial void RemoveSymbol(SemanticSymbol symbol);
}

// Identity-bearing declaration. Scope strongly owns it; Declaration is a weak
// syntax relationship. Builtins have no declaration node, not fabricated source.
internal sealed partial class SemanticSymbol
{
    internal SemanticScope? Scope;
    internal SyntaxNode? Declaration;
    internal readonly string Name;
    internal readonly SymbolKind Kind;
    internal readonly BuiltinKind Builtin;
    // For functions this is the result type; for bindings it is the value type.
    // Null means inference is incomplete, or an explicitly array-shaped parameter.
    // AcceptsArray is a bounded builtin contract, not a runtime dynamic type.
    internal readonly bool AcceptsArray;
    internal SemanticType? Type;
    // A fixed nonowning storage alias. Type remains the referent's value type;
    // Mutable grants writes through the alias, never retargeting or ownership.
    internal readonly bool IsReference;
    internal readonly bool Mutable;
    internal bool Destroyed;

    // Registers scope ownership and optional syntax backlink, or cleans up both.
    internal partial SemanticSymbol(SemanticScope scope, SyntaxNode? declaration,
        string name, SymbolKind kind, SemanticType? type, bool mutable,
        BuiltinKind builtin = BuiltinKind.None, bool acceptsArray = false, bool isReference = false);
    internal partial void Destroy();
}

internal sealed partial class SyntaxNode
{
    internal SemanticSymbol? DeclaredSymbol;

    // Weak semantic relationship; its analysis must belong to this compilation.
    // Unlike syntax mutation, adding semantic information does not invalidate it.
    internal partial void AddDeclaredSymbol(SemanticSymbol symbol);
    internal partial void RemoveDeclaredSymbol(SemanticSymbol symbol);
}

internal sealed partial class SemanticAnalysis
{
    private readonly List<SemanticFact> Facts = new();
    internal partial void AddFact(SemanticFact fact);
    internal partial void RemoveFact(SemanticFact fact);
}

// Identity-bearing checked expression, strongly owned by its analysis. Node and
// Symbol are weak relationships, both maintained with reverse links. Scalar types
// are values; nominal types also register a reverse dependency on their class.
internal sealed partial class SemanticFact
{
    internal SemanticAnalysis? Analysis;
    internal SyntaxNode? Node;
    internal SemanticSymbol? Symbol;
    internal SemanticType Type;
    // Value-like exact integer result, when known from a literal expression.
    // Null means unknown, not zero. It has no model lifetime or backlinks.
    internal readonly BigInteger? IntegerConstant;
    // Block/conditional reachability is checked once. A noncontinuing arm
    // contributes no value at a branch join, even when its placeholder type is unit.
    internal readonly bool CanContinue;
    internal readonly ArrayOperation ArrayOperation;
    internal readonly ClassOperation ClassOperation;
    internal bool Destroyed;

    internal partial SemanticFact(SemanticAnalysis analysis, SyntaxNode node,
        SemanticType type, SemanticSymbol? symbol, BigInteger? integerConstant = null,
        bool canContinue = true, ArrayOperation arrayOperation = ArrayOperation.None,
        ClassOperation classOperation = ClassOperation.None);
    internal partial void Destroy();
}

internal sealed partial class SyntaxNode
{
    internal SemanticFact? Fact;
    internal partial void AddFact(SemanticFact fact);
    internal partial void RemoveFact(SemanticFact fact);
}

internal sealed partial class SemanticSymbol
{
    private readonly List<SemanticFact> Uses = new();
    internal partial void AddUse(SemanticFact fact);
    internal partial void RemoveUse(SemanticFact fact);
}

internal sealed partial class Compilation
{
    // Checks parsed sources; executable mode additionally requires main.
    // Failure retires incomplete analysis.
    internal partial Diagnostic? Check(bool executable);
}

internal sealed partial class SourceFile
{
    // Value-like source location, calculated from the authoritative UTF-8 bytes.
    internal partial Diagnostic SemanticDiagnostic(SyntaxNode node, string code, string message);
}

internal sealed partial class SemanticSymbol
{
    internal SemanticScope? FunctionScope;
    // Strong function/scope membership, with complete reverse registration.
    internal partial void AddFunctionScope(SemanticScope scope);
    internal partial void RemoveFunctionScope(SemanticScope scope);
}

internal sealed partial class SemanticScope
{
    internal SemanticSymbol? Function;
    // Constructs a function- or class-owned scope with complete owner/lexical links.
    internal partial SemanticScope(SemanticSymbol function);
    // Parameter order is declaration order in Symbols, not a copied signature.
    internal partial int ParameterCount();
    internal partial SemanticSymbol ParameterAt(int index);
}

// Value-like failure used only to unwind the current check, never model state.
internal sealed partial class SemanticFailure : System.Exception
{
    internal readonly Diagnostic Diagnostic;
    internal partial SemanticFailure(Diagnostic diagnostic);
}

// A class declaration is a nominal identity, not a numeric scalar code. Its
// member scope is strongly owned here and by the analysis; each has a backlink.
internal sealed partial class SemanticSymbol
{
    internal SemanticScope? MemberScope;
    private readonly List<SemanticSymbol> TypedSymbols = new();
    private readonly List<SemanticFact> TypedFacts = new();

    internal partial void AddMemberScope(SemanticScope scope);
    internal partial void RemoveMemberScope(SemanticScope scope);
    // Sets a binding/result type and maintains any nominal-class dependency.
    // Refusal leaves the old type and both reverse lists unchanged.
    internal partial void SetType(SemanticType? type);
    internal partial void AddTypedSymbol(SemanticSymbol symbol, SemanticType type);
    internal partial void RemoveTypedSymbol(SemanticSymbol symbol);
    internal partial void AddTypedFact(SemanticFact fact, SemanticType type);
    internal partial void RemoveTypedFact(SemanticFact fact);
}

internal sealed partial class SemanticScope
{
    internal SemanticSymbol? Class;
}
