using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound variable declaration statement, bound from a <see cref="Syntax.VariableDeclarationSyntax" />.
/// </summary>
internal sealed class BoundVariableDeclaration : BoundNode {
    internal BoundVariableDeclaration(VariableSymbol variable, BoundExpression initializer) {
        this.variable = variable;
        this.initializer = initializer;
    }

    internal VariableSymbol variable { get; }

    internal BoundExpression initializer { get; }

    internal override BoundNodeKind kind => BoundNodeKind.VariableDeclaration;
}
