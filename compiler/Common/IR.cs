using System.Collections.Generic;
using System.Numerics;

namespace Lyric;

// Structured execution vocabulary, not basic blocks. Ownership operations are
// first-class: later passes must preserve their failure and finalization effects.
internal enum IrKind
{
    Region, If, While, For, Break, Continue, Return, Yield,
    Constant, Local, Borrow, SelectField, AddressOf, SelectIndex, Load, Store, Call, CallInitializer,
    Unary, Binary, Convert, Copy, Move, Clear,
    NullHandle, WrapNullable, UnwrapNullable,
    MembershipRetain, MembershipRelease,
    Ref, Unref, NewArray, FreeArray, NewObject, DestroyObject, ReclaimObject,
    ArrayLength, ArrayCapacity, ArrayReserve, ArrayResize, ArrayTruncate, ArrayPush,
    NewTuple, SelectTuple, DropTuple,
    NewStruct, SelectStruct, DropStruct,
}

// Value-like tags. A condition region executes on EVERY test; a for-step region
// executes on normal completion and continue, but not break/return. Yield ends
// a result-producing region. Short-circuit evaluation lowers to structured If.
internal enum IrRole { None, Body, Initializer, Condition, Then, Else, Step }
// Place is a logical field location, not a captured interior pointer. Address is
// fixed storage selected at a particular execution point. Neither is an owner.
internal enum IrResult { None, Value, Place, Address }

// Generated bodies retain their real class or field owner, never a fabricated function.
internal enum IrFunctionKind { Source, Destruction, FieldInitializer }

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
    // Reject incomplete execution trees without mutating them. This structural
    // check does not certify opcode typing or ownership/use ordering.
    internal partial void ValidateStructure();
    // Requires valid structure; checks implemented opcode categories, types and
    // selected identities, not execution availability or ownership balance.
    internal partial void ValidateOperations();
    // Requires structural and operation validity. Checks reachable definition order
    // and region exits, not storage initialization or ownership liveness.
    internal partial void ValidateExecution();
    // Mandatory ownership simplification, before publication. Keeps caller owners
    // across proven observing receiver calls; never adds runtime dispatch.
    internal partial void SimplifyOwnership();
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
    // Weak class identity, paired with SemanticSymbol.IrDestruction. Program owns us.
    internal SemanticSymbol? DestructionClass;
    internal readonly IrFunctionKind Kind;
    // IR calling contract inferred from nonescaping receiver uses. Calls retain
    // their holder across this body rather than transferring it into the body.
    internal bool ObservesReceiver;
    internal partial bool BorrowsReceiver();
    internal bool Destroyed;
    private readonly List<IrNode> Nodes = new();
    internal IrNode? Body;
    internal partial IrFunction(IrProgram program, SemanticSymbol declaration,
        IrFunctionKind kind = IrFunctionKind.Source);
    internal partial void Destroy();
    internal partial void AddNode(IrNode node);
    internal partial void RemoveNode(IrNode node);
    internal partial int NodeCount();
    internal partial IrNode NodeAt(int index);
    internal partial void AddBody(IrNode node);
    internal partial void RemoveBody(IrNode node);
}

// Borrow returns a non-owning address. A parameter Borrow receives the caller's
// address; an operand-bearing Borrow aliases that fixed address. Neither owns or
// releases its referent. Ref/Unref instead change counted-object ownership.
// Ref copies a live counted handle into a new owner and increments its count.
// Unref empties its operand holder before decrementing; empty is harmless and
// zero executes the class's generated destruction body. Move transfers a holder
// without changing the count. Constructor/final self slots are borrowed handles,
// not owning Local slots; field receiver snapshots likewise do not retain.
//
// MembershipRetain/Release change the independent reference owned by a refs
// membership. They have no result and never empty the child parameter holder.
// The function contributor identifies the relationship; the operand is its
// declared child parameter slot. Structural links themselves do not own counts.
//
// NullHandle creates the empty value of a nullable class type. WrapNullable and
// UnwrapNullable preserve the handle and its class identity; unwrap checks for
// null in safe mode before returning a nonnullable value. For counted classes,
// owning inputs are Move transfers and produce cleanup holders; borrowed Load
// inputs (including nested nullable conversions) produce borrowed observations.
// Neither operation increments the count. NullHandle is an empty cleanup holder for a counted type.
// These are not numeric conversions and never manufacture a live object.
//
// Identity-bearing typed execution node, NEVER a value-like class. Region nodes
// hold ordered operations; control nodes hold role-labelled regions. Local nodes
// define temporary storage without inventing source symbols. Operands refer to
// actual definitions, not integer IDs. SelectField takes a saved receiver handle
// and the actual field Symbol and produces a Place. It does not capture an interior
// pointer. Load/Copy/Store resolve that place when they execute, so a direct field
// assignment can survive class-storage growth during its RHS. AddressOf resolves a
// Place once into a fixed Address; subsequent borrowing never re-resolves it.
// AddressOf on an existing Address preserves that address. SelectIndex likewise
// produces a fixed address, not a re-resolvable path. Load/Copy explicitly read
// selected storage. C emission must preserve this ordering and snapshots.
//
// This construction model admits incomplete drafts. It checks relationships, not
// opcode arity, dominance, complete region shapes, or typing. Those are a later
// lowering/validation contract. Array Local, Copy, NewArray, Call and If results
// are owning holders (initially empty for Local); Copy recursively duplicates live
// contents, not spare capacity. Move empties its holder operand and produces a
// transfer value with one designated consumer: Store, Call, NewArray, ArrayPush, Return or Yield.
// NewArray consumes transferred element operands, or a Bytes literal payload
// with no operands. The latter allocates a fresh [u8] containing exactly those
// bytes (no trailing sentinel), including on each repeated region execution.
// The immutable payload is not runtime storage and is never itself released.
// FreeArray releases and empties a holder; releasing empty storage is harmless.
// Store receives an already-transferred array after old destination cleanup.
// SelectIndex takes an array holder/address and an evaluated integer index. It
// checks initialized-length bounds under the program's safe/unsafe policy, then
// captures a fixed element address. It neither copies nor owns the parent array.
// Load reads a scalar element; Copy reads an owned element independently. Writable
// nested selections use the original descriptor, never a temporary array copy.
// ArrayReserve/Resize/Truncate/Push take a selected descriptor address followed
// by the evaluated count or element. Push consumes a transferred owning element;
// the others consume no owner. Shrink releases removed owning elements. Resize(0)
// releases reservation too; Truncate(0) retains it. Growth preserves initialized
// values and uses typed defaults, with the existing allocation/failure contract.
// ArrayCapacity observes the original selected descriptor (or an owned computed
// temporary), never a copied reservation. Mutation results are unit values.
// CallInitializer invokes the actual field's zero-argument initializer body.
// An ordinary field returns a value with fresh ownership, as for a source call.
// A reference field returns an exact Address: neither the call nor its Return
// transfers ownership of the referent. Ordinary source returns remain values.
// NewObject allocates a class handle; lowering emits the actual constructor Call
// separately, after evaluating explicit arguments and before exposing the result.
// DestroyObject invokes Symbol.IrDestruction with the saved receiver handle.
// ReclaimObject is the final slot release inside that generated body, not another
// destruction invocation. The generated body calls the hook, releases owned
// array fields in declaration order, then reclaims. Last counted release uses
// the same generated body.
// NewTuple consumes one value per ordered component; owned components arrive as
// Move transfers. SelectTuple uses its integer Literal as a compile-time component
// index and returns a fixed address into a tuple value holder or selected Address.
// Resolve a Place with AddressOf first: selection never restores invalid borrows.
// Copy duplicates tuple contents recursively; Move empties ownership-bearing
// contents, and DropTuple releases and empties those contents without freeing the
// inline record itself. Plain scalar tuples require neither Move nor DropTuple.
// NewStruct consumes values in field declaration order after lowering has evaluated
// source initializers in written order. Its type is the actual nominal struct.
// SelectStruct carries the actual field Symbol and selects a fixed address from
// an evaluated struct holder/address. It never searches a field by spelling.
// DropStruct releases owned fields without freeing the inline record. Copy and
// Move have the same recursive ownership contracts as their tuple counterparts.
// These contracts do not make array handles scalar immutable snapshots.
// Deleting a definition detaches its uses rather
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
    // Atomic ordered tree replacement/insertion; refused edits leave both ends intact.
    internal partial void ReplaceChild(IrNode oldChild, IrNode newChild);
    internal partial void InsertAfter(IrNode previous, IrNode child);
    internal partial int ChildCount();
    internal partial IrNode ChildAt(int index);
    internal partial void AddOperand(IrOperand operand);
    internal partial void RemoveOperand(IrOperand operand);
    // Scalar managed-link storage, local address aliases, or writable participant
    // parameters declared by this relationship implementation. Other types keep
    // ordinary ownership even when used inside a relationship helper.
    internal partial bool IsManagedHandleStorage();
    // Scalar observations remain borrowed through nullable conversions. Derive
    // this from operand identity; no second mutable ownership flag is stored.
    internal partial bool IsBorrowedHandleValue();
    internal partial int OperandCount();
    internal partial IrOperand OperandAt(int index);
    internal partial void AddUse(IrOperand operand);
    internal partial void ReserveUse();
    internal partial void RemoveUse(IrOperand operand);
    internal partial int UseCount();
    internal partial IrOperand UseAt(int index);
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
    // Change the weak definition and its reverse use membership atomically.
    internal partial void SetDefinition(IrNode definition);
    internal partial void Destroy();
}

// Weak declaration dependencies. Canonical types separately track all typed IR
// holders, including scalar and array types. Semantic edits retire
// the analysis-owned IR before changing declarations or source structure.
internal sealed partial class SemanticSymbol
{
    // One body per concrete declaration; calls reach it through their Symbol.
    internal IrFunction? IrBody;
    // One generated teardown body per class. Weak backlink; the program owns it.
    internal IrFunction? IrDestruction;
    // Attach/detach both ends; reject incompatible or duplicate bodies unchanged.
    internal partial void AddIrDestruction(IrFunction function);
    internal partial void RemoveIrDestruction(IrFunction function);
    private readonly List<IrNode> IrSymbols = new();
    internal partial void AddIrFunction(IrFunction function);
    internal partial void RemoveIrFunction(IrFunction function);
    internal partial void AddIrSymbol(IrNode node);
    internal partial void RemoveIrSymbol(IrNode node);
}
internal sealed partial class SyntaxNode
{
    private readonly List<IrNode> IrOrigins = new();
    internal partial void AddIrOrigin(IrNode node);
    internal partial void RemoveIrOrigin(IrNode node);
}

internal sealed partial class Compilation
{
    // Lower current checked syntax without changing it. Failure destroys partial
    // IR and returns a located diagnostic. Internal malformed graphs fail before
    // successful inspection; this is not a source-language rejection.
    internal partial Diagnostic? LowerIr();
    // Serialize a successfully lowered model. IDs are temporary output labels,
    // never identity stored in the graph. No output on a lowering failure.
    internal partial void WriteIr(System.IO.TextWriter output);
}
