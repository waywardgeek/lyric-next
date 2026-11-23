using System.Collections.Generic;
using System.Numerics;

namespace Lyric;

// Structured execution vocabulary, not basic blocks. Ownership operations are
// first-class: later passes must preserve their failure and finalization effects.
internal enum IrKind
{
    Region, If, While, For, Break, Continue, Return, Yield,
    Constant, Local, SelectField, SelectIndex, Load, Store, Call,
    Unary, Binary, Convert, Copy, Move, Clear,
    Ref, Unref, NewArray, FreeArray, NewObject, DestroyObject,
    ArrayLength, ArrayCapacity, ArrayReserve, ArrayResize, ArrayTruncate, ArrayPush,
}

// Value-like tags. A condition region executes on EVERY test; a for-step region
// executes on normal completion and continue, but not break/return. Yield ends
// a result-producing region. Short-circuit evaluation lowers to structured If.
internal enum IrRole { None, Body, Initializer, Condition, Then, Else, Step }
internal enum IrResult { None, Value, Address }

// Explicitly VALUE-LIKE immutable literal. No source or symbol relationships.
// Bytes are defensively copied and exposed one byte at a time, never as a mutable
// array. BigInteger and bool are values; the kind identifies the valid payload.
internal enum IrLiteralKind { Integer, Boolean, Bytes }
internal sealed partial class IrLiteral
{
    internal readonly IrLiteralKind Kind;
    internal readonly BigInteger Integer;
    internal readonly bool Boolean;
    private readonly byte[] Bytes;
    internal partial IrLiteral(BigInteger value);
    internal partial IrLiteral(bool value);
    internal partial IrLiteral(byte[] value);
    internal partial int ByteCount();
    internal partial byte ByteAt(int index);
}

internal sealed partial class SemanticAnalysis
{
    internal IrProgram? Ir;
    // Single strong child. Refused attachment changes neither end.
    internal partial void AddIr(IrProgram program);
    internal partial void RemoveIr(IrProgram program);
    internal partial void ClearIr();
}

// Analysis owns this execution representation. It is separate from checked
// syntax: optimization must not rewrite source or repeat name resolution.
internal sealed partial class IrProgram
{
    internal SemanticAnalysis? Analysis;
    internal bool Destroyed;
    private readonly List<IrFunction> Functions = new();
    internal partial IrProgram(SemanticAnalysis analysis);
    internal partial void Destroy();
    internal partial void AddFunction(IrFunction function);
    internal partial void RemoveFunction(IrFunction function);
    internal partial int FunctionCount();
    internal partial IrFunction FunctionAt(int index);
}

// Strong node arena, like SourceFile's syntax arena. Tree edges are weak:
// detaching a subtree does not delete it; destroying this function deletes ALL
// nodes, including incomplete/detached drafts. Declaration is a weak identity.
internal sealed partial class IrFunction
{
    internal IrProgram? Program;
    internal SemanticSymbol? Declaration;
    internal bool Destroyed;
    private readonly List<IrNode> Nodes = new();
    internal IrNode? Body;
    internal partial IrFunction(IrProgram program, SemanticSymbol declaration);
    internal partial void Destroy();
    internal partial void AddNode(IrNode node);
    internal partial void RemoveNode(IrNode node);
    internal partial int NodeCount();
    internal partial IrNode NodeAt(int index);
    internal partial void AddBody(IrNode node);
    internal partial void RemoveBody(IrNode node);
}

// Identity-bearing typed execution node, NEVER a value-like class. Region nodes
// hold ordered operations; control nodes hold role-labelled regions. Local nodes
// define temporary storage without inventing source symbols. Operands refer to
// actual definitions, not integer IDs. Address results select storage; Load/Copy
// explicitly read it. C emission must preserve this ordering and snapshots.
//
// This construction model admits incomplete drafts. It checks relationships, not
// opcode arity, dominance, complete region shapes, or typing. Those are a later
// lowering/validation contract. Deleting a definition detaches its uses rather
// than deleting consumers. Such a draft must not be emitted without validation.
internal sealed partial class IrNode
{
    internal IrFunction? Function;
    internal IrFunction? BodyOwner;
    internal IrNode? Parent;
    internal IrNode? Target;
    internal SyntaxNode? Origin;
    internal SemanticSymbol? Symbol;
    internal SemanticType? Type;
    internal readonly IrKind Kind;
    internal readonly IrRole Role;
    internal readonly IrResult Result;
    internal readonly IrLiteral? Literal;
    // Operator spelling is a value tag from existing lexer vocabulary, NOT a
    // declaration name. Selected calls/fields use Symbol instead of lookup.
    internal readonly string? Operator;
    internal bool Destroyed;
    private readonly List<IrNode> Children = new();
    private readonly List<IrOperand> Operands = new();
    private readonly List<IrOperand> Uses = new();
    private readonly List<IrNode> Transfers = new();
    internal partial IrNode(IrFunction function, IrKind kind,
        IrRole role = IrRole.None, IrResult result = IrResult.None,
        SemanticType? type = null, SyntaxNode? origin = null,
        SemanticSymbol? symbol = null, IrLiteral? literal = null,
        string? operation = null);
    internal partial void Destroy();
    // Tree edit: same function, no cycles, no existing parent/body membership.
    // Add appends; remove detaches both ends without deleting the child.
    internal partial void AddChild(IrNode child);
    internal partial void RemoveChild(IrNode child);
    internal partial int ChildCount();
    internal partial IrNode ChildAt(int index);
    internal partial void AddOperand(IrOperand operand);
    internal partial void RemoveOperand(IrOperand operand);
    internal partial int OperandCount();
    internal partial IrOperand OperandAt(int index);
    internal partial void AddUse(IrOperand operand);
    internal partial void RemoveUse(IrOperand operand);
    internal partial int UseCount();
    // Weak transfer destination, constrained to same-function loop nodes. Full
    // enclosing-loop validation waits until a draft's structured tree is complete.
    internal partial void AddTransfer(IrNode transfer);
    internal partial void RemoveTransfer(IrNode transfer);
    internal partial void SetType(SemanticType? type);
}

// Strongly owned by consumer, weak reference to definition. Both ends register.
// Repeated uses are distinct ordered operands. Destroying a definition leaves
// Definition null; destroying a consumer recursively destroys these use objects.
internal sealed partial class IrOperand
{
    internal IrNode? Consumer;
    internal IrNode? Definition;
    internal bool Destroyed;
    internal partial IrOperand(IrNode consumer, IrNode definition);
    internal partial void Destroy();
}

// Weak reverse dependencies. Nominal types are NOT plain scalar values: every
// persistent IR holder registers with the actual class. Semantic edits retire
// the analysis-owned IR before changing declarations or source structure.
internal sealed partial class SemanticSymbol
{
    // One body per concrete declaration; calls reach it through their Symbol.
    internal IrFunction? IrBody;
    private readonly List<IrNode> IrSymbols = new();
    private readonly List<IrNode> IrTypes = new();
    internal partial void AddIrFunction(IrFunction function);
    internal partial void RemoveIrFunction(IrFunction function);
    internal partial void AddIrSymbol(IrNode node);
    internal partial void RemoveIrSymbol(IrNode node);
    internal partial void ReserveIrType();
    internal partial void AddIrType(IrNode node, SemanticType type);
    internal partial void RemoveIrType(IrNode node);
}
internal sealed partial class SyntaxNode
{
    private readonly List<IrNode> IrOrigins = new();
    internal partial void AddIrOrigin(IrNode node);
    internal partial void RemoveIrOrigin(IrNode node);
}
