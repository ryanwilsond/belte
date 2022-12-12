using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound variable declaration statement, bound from a parser VariableDeclarationStatement.
/// </summary>
internal sealed class BoundVariableDeclarationStatement : BoundStatement {
    internal BoundVariableDeclarationStatement(VariableSymbol variable, BoundExpression initializer) {
        this.variable = variable;
        this.initializer = initializer;
    }

    internal VariableSymbol variable { get; }

    internal BoundExpression initializer { get; }

    internal override BoundNodeType type => BoundNodeType.VariableDeclarationStatement;
}
