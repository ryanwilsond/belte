using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound field declaration, bound from a <see cref="FieldDeclaration" />.
/// There is no FieldDeclarationStatement, as it is a member, but this statement is added so the enclosing struct
/// or class body can be resolved after binding (during emitting or evaluating).
/// </summary>
internal sealed class BoundFieldDeclarationStatement : BoundStatement {
    internal BoundFieldDeclarationStatement(FieldSymbol field) {
        this.field = field;
    }

    internal FieldSymbol field { get; }

    internal override BoundNodeType type => BoundNodeType.FieldDeclarationStatement;
}
