using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class ExpressionVariableFinder<TFieldOrLocalSymbol> : SyntaxWalker
    where TFieldOrLocalSymbol : Symbol {
    private ArrayBuilder<TFieldOrLocalSymbol> _variablesBuilder;
    private SyntaxNode _nodeToBind;

    private protected abstract TFieldOrLocalSymbol MakeDeclarationExpressionVariable(
        DeclarationExpressionSyntax node,
        SyntaxToken identifier,
        BaseArgumentListSyntax argumentListSyntax,
        SyntaxTokenList modifiers,
        SyntaxNode nodeToBind
    );

    internal override void VisitEqualsValueClause(EqualsValueClauseSyntax node) {
        VisitNodeToBind(node.value);
    }

    internal override void VisitThrowExpression(ThrowExpressionSyntax node) {
        VisitNodeToBind(node.expression);
    }

    internal override void VisitReturnStatement(ReturnStatementSyntax node) {
        VisitNodeToBind(node.expression);
    }

    internal override void VisitExpressionStatement(ExpressionStatementSyntax node) {
        VisitNodeToBind(node.expression);
    }

    internal override void VisitIfStatement(IfStatementSyntax node) {
        VisitNodeToBind(node.expression);
    }

    internal override void VisitNullBindingStatement(NullBindingStatementSyntax node) {
        VisitNodeToBind(node.expression);
    }

    internal override void VisitBinaryExpression(BinaryExpressionSyntax node) {
        var operands = ArrayBuilder<ExpressionSyntax>.GetInstance();
        ExpressionSyntax current = node;

        do {
            var binOp = (BinaryExpressionSyntax)current;
            operands.Push(binOp.right);
            current = binOp.left;
        } while (current is BinaryExpressionSyntax);

        Visit(current);

        while (operands.Count > 0)
            Visit(operands.Pop());

        operands.Free();
    }

    internal override void VisitCallExpression(CallExpressionSyntax node) {
        if (ReceiverIsInvocation(node, out var nested)) {
            var invocations = ArrayBuilder<CallExpressionSyntax>.GetInstance();

            invocations.Push(node);

            node = nested;

            while (ReceiverIsInvocation(node, out nested)) {
                invocations.Push(node);
                node = nested;
            }

            Visit(node.expression);

            do {
                Visit(node.argumentList);
            } while (invocations.TryPop(out node));

            invocations.Free();
        } else {
            Visit(node.expression);
            Visit(node.argumentList);
        }

        static bool ReceiverIsInvocation(CallExpressionSyntax node, out CallExpressionSyntax nested) {
            if (node.expression is MemberAccessExpressionSyntax { expression: CallExpressionSyntax receiver }) {
                nested = receiver;
                return true;
            }

            nested = null;
            return false;
        }
    }

    internal override void VisitVariableDeclaration(VariableDeclarationSyntax node) {
        VisitNodeToBind(node.initializer);
    }

    internal override void VisitAssignmentExpression(AssignmentExpressionSyntax node) {
        Visit(node.left);
        Visit(node.right);
    }

    internal override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node) {
        if (node.constructorInitializer is not null)
            VisitNodeToBind(node.constructorInitializer);
    }

    private protected void FindExpressionVariables(ArrayBuilder<TFieldOrLocalSymbol> builder, BelteSyntaxNode node) {
        var save = _variablesBuilder;
        _variablesBuilder = builder;

        VisitNodeToBind(node);

        _variablesBuilder = save;
    }

    internal override void VisitDeclarationExpression(DeclarationExpressionSyntax node) {
        var argumentSyntax = node.parent as ArgumentSyntax;
        var argumentListSyntaxOpt = argumentSyntax?.parent as BaseArgumentListSyntax;

        VisitDeclarationExpressionDesignation(node, argumentListSyntaxOpt);
    }

    private void VisitDeclarationExpressionDesignation(
        DeclarationExpressionSyntax node,
        BaseArgumentListSyntax argumentListSyntax) {
        var variable = MakeDeclarationExpressionVariable(
            node,
            node.identifier,
            argumentListSyntax,
            null,
            _nodeToBind
        );

        if ((object)variable is not null)
            _variablesBuilder.Add(variable);
    }

    private protected void FindExpressionVariables(
        ArrayBuilder<TFieldOrLocalSymbol> builder,
        SeparatedSyntaxList<ExpressionSyntax> nodes) {
        var save = _variablesBuilder;
        _variablesBuilder = builder;

        foreach (var node in nodes)
            VisitNodeToBind(node);

        _variablesBuilder = save;
    }

    private void VisitNodeToBind(BelteSyntaxNode node) {
        var previousNodeToBind = _nodeToBind;
        _nodeToBind = node;
        Visit(node);
        _nodeToBind = previousNodeToBind;
    }
}
