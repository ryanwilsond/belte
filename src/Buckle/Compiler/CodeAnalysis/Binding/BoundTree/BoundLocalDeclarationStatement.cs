
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound local declaration statement, bound from a <see cref="Syntax.LocalDeclarationStatementSyntax" />.
/// </summary>
internal sealed class BoundLocalDeclarationStatement : BoundStatement {
    internal BoundLocalDeclarationStatement(BoundVariableDeclaration declaration) {
        this.declaration = declaration;
    }

    internal BoundVariableDeclaration declaration { get; }

    internal override BoundNodeKind kind => BoundNodeKind.LocalDeclarationStatement;
}
