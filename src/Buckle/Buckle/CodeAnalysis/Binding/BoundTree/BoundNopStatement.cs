
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound NOP statement. Used to replace <see cref="Syntax.EmptyExpressionSyntax" /> and used as debugging symbols and
/// placeholders. Used to mark the start and end of exception handlers in the <see cref="Emitting.ILEmitter" />.
/// </summary>
internal sealed class BoundNopStatement : BoundStatement {
    internal override BoundNodeKind kind => BoundNodeKind.NopStatement;
}
