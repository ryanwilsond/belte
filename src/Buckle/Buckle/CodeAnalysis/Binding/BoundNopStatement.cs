
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound NOP statement. Used to replace <see cref="EmptyExpressions" /> and used as debugging symbols and placeholders.
/// Used to mark the start and end of exception handlers in the <see cref="Emitter" />.
/// </summary>
internal sealed class BoundNopStatement : BoundStatement {
    internal override BoundNodeType type => BoundNodeType.NopStatement;
}
