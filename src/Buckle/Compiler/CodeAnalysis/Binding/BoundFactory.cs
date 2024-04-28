using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal static partial class BoundFactory {
    internal static BoundGlobalScope GlobalScope(BoundGlobalScope previous, BelteDiagnosticQueue diagnostics) {
        return new BoundGlobalScope(
            ImmutableArray<(MethodSymbol, BoundBlockStatement)>.Empty,
            previous,
            diagnostics,
            null,
            ImmutableArray<MethodSymbol>.Empty,
            ImmutableArray<VariableSymbol>.Empty,
            ImmutableArray<NamedTypeSymbol>.Empty,
            ImmutableArray<BoundStatement>.Empty,
            previous.libraryTypes
        );
    }

    internal static BoundProgram Program(BoundProgram previous, BelteDiagnosticQueue diagnostics) {
        return new BoundProgram(
            previous,
            diagnostics,
            null,
            ImmutableDictionary<MethodSymbol, BoundBlockStatement>.Empty,
            ImmutableArray<NamedTypeSymbol>.Empty
        );
    }

    internal static BoundNopStatement Nop() {
        return new BoundNopStatement();
    }

    internal static BoundLiteralExpression Literal(object value, BoundType type = null) {
        if (type != null)
            return new BoundLiteralExpression(value, type);

        return new BoundLiteralExpression(value);
    }

    internal static BoundBlockStatement Block(params BoundStatement[] statements) {
        return new BoundBlockStatement(ImmutableArray.Create(statements));
    }

    internal static BoundBlockStatement Block(params ImmutableArray<BoundStatement>[] blocks) {
        var builder = blocks[0].ToBuilder();

        for (var i = 1; i < blocks.Length; i++)
            builder.AddRange(blocks[i]);

        return new BoundBlockStatement(builder.ToImmutable());
    }

    internal static BoundBlockStatement Block() {
        return new BoundBlockStatement(ImmutableArray<BoundStatement>.Empty);
    }

    internal static BoundLabelStatement Label(BoundLabel label) {
        return new BoundLabelStatement(label);
    }

    internal static BoundGotoStatement Goto(BoundLabel label) {
        return new BoundGotoStatement(label);
    }

    internal static BoundConditionalGotoStatement GotoIf(BoundLabel @goto, BoundExpression @if) {
        return new BoundConditionalGotoStatement(@goto, @if, true);
    }

    internal static BoundConditionalGotoStatement GotoIfNot(BoundLabel @goto, BoundExpression @ifNot) {
        return new BoundConditionalGotoStatement(@goto, @ifNot, false);
    }

    internal static BoundExpressionStatement Statement(BoundExpression expression) {
        return new BoundExpressionStatement(expression);
    }

    internal static BoundWhileStatement While(
        BoundExpression condition, BoundStatement body, BoundLabel breakLabel, BoundLabel continueLabel) {
        return new BoundWhileStatement(condition, body, breakLabel, continueLabel);
    }

    internal static BoundCallExpression Call(MethodSymbol method, params BoundExpression[] arguments) {
        return new BoundCallExpression(new BoundEmptyExpression(), method, ImmutableArray.Create(arguments));
    }

    internal static BoundCastExpression Cast(BoundType type, BoundExpression expression) {
        return new BoundCastExpression(type, expression);
    }

    internal static BoundMemberAccessExpression MemberAccess(
        BoundExpression left,
        BoundExpression right,
        bool isStaticAccess = false) {
        return new BoundMemberAccessExpression(left, right, false, isStaticAccess);
    }

    internal static BoundIndexExpression Index(BoundExpression operand, BoundExpression index) {
        return new BoundIndexExpression(operand, index, false);
    }

    internal static BoundTernaryExpression NullConditional(
        BoundExpression @if, BoundExpression @then, BoundExpression @else) {
        var op = BoundTernaryOperator.Bind(
            SyntaxKind.QuestionToken, SyntaxKind.ColonToken, @if.type, @then.type, @else.type
        );

        return new BoundTernaryExpression(@if, op, @then, @else);
    }

    internal static BoundAssignmentExpression Assignment(BoundExpression left, BoundExpression right) {
        return new BoundAssignmentExpression(left, right);
    }

    internal static BoundCompoundAssignmentExpression Increment(BoundExpression operand) {
        var value = new BoundTypeWrapper(BoundType.Int, new BoundConstant(1));
        var op = BoundBinaryOperator.Bind(SyntaxKind.PlusToken, operand.type, value.type);

        return new BoundCompoundAssignmentExpression(operand, op, value);
    }

    internal static BoundCompoundAssignmentExpression Decrement(BoundExpression operand) {
        var value = new BoundTypeWrapper(BoundType.Int, new BoundConstant(1));
        var op = BoundBinaryOperator.Bind(SyntaxKind.MinusToken, operand.type, value.type);

        return new BoundCompoundAssignmentExpression(operand, op, value);
    }

    internal static BoundUnaryExpression Unary(BoundUnaryOperator op, BoundExpression operand) {
        return new BoundUnaryExpression(op, operand);
    }

    internal static BoundUnaryExpression Not(BoundExpression operand) {
        var op = BoundUnaryOperator.Bind(SyntaxKind.ExclamationToken, operand.type);

        return new BoundUnaryExpression(op, operand);
    }

    internal static BoundBinaryExpression Binary(BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        return new BoundBinaryExpression(left, op, right);
    }

    internal static BoundBinaryExpression Add(BoundExpression left, BoundExpression right) {
        var op = BoundBinaryOperator.Bind(SyntaxKind.PlusToken, left.type, right.type);

        return new BoundBinaryExpression(left, op, right);
    }

    internal static BoundBinaryExpression Subtract(BoundExpression left, BoundExpression right) {
        var op = BoundBinaryOperator.Bind(SyntaxKind.MinusToken, left.type, right.type);

        return new BoundBinaryExpression(left, op, right);
    }

    internal static BoundBinaryExpression And(BoundExpression left, BoundExpression right) {
        var op = BoundBinaryOperator.Bind(SyntaxKind.AmpersandAmpersandToken, left.type, right.type);

        return new BoundBinaryExpression(left, op, right);
    }
}
