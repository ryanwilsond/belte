using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Tests on the <see cref="Buckle.CodeAnalysis.Syntax.InternalSyntax.LanguageParser" /> class.
/// </summary>
public sealed class ParserTests {
    [Theory]
    [MemberData(nameof(GetBinaryOperatorPairsData))]
    internal void Parser_BinaryExpression_HonorsPrecedences(SyntaxKind op1, SyntaxKind op2) {
        var op1Precedence = SyntaxFacts.GetBinaryPrecedence(op1);
        var op2Precedence = SyntaxFacts.GetBinaryPrecedence(op2);
        var op1Text = SyntaxFacts.GetText(op1);
        var op2Text = SyntaxFacts.GetText(op2);

        Debug.Assert(op1Text is not null);
        Debug.Assert(op2Text is not null);

        var text = $"var v = a {op1Text} b {op2Text} c";
        var expression = ParseExpression(text);

        if (op1Precedence >= op2Precedence) {
            using var e = new AssertingEnumerator(expression);
            e.AssertNode(SyntaxKind.BinaryExpression);
            e.AssertNode(SyntaxKind.BinaryExpression);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "a");
            e.AssertToken(op1, op1Text);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "b");
            e.AssertToken(op2, op2Text);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "c");
        } else {
            using var e = new AssertingEnumerator(expression);
            e.AssertNode(SyntaxKind.BinaryExpression);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "a");
            e.AssertToken(op1, op1Text);
            e.AssertNode(SyntaxKind.BinaryExpression);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "b");
            e.AssertToken(op2, op2Text);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "c");
        }
    }

    [Theory]
    [MemberData(nameof(GetUnaryOperatorPairsData))]
    internal void Parser_UnaryExpression_HonorsPrecedences(SyntaxKind unaryKind, SyntaxKind binaryKind) {
        var unaryPrecedence = SyntaxFacts.GetUnaryPrecedence(unaryKind);
        var binaryPrecedence = SyntaxFacts.GetBinaryPrecedence(binaryKind);
        var unaryText = SyntaxFacts.GetText(unaryKind);
        var binaryText = SyntaxFacts.GetText(binaryKind);

        if (unaryText == "--" || unaryText == "++")
            return;

        Debug.Assert(unaryText is not null);
        Debug.Assert(binaryText is not null);

        var text = $"var v = {unaryText} a {binaryText} b";
        var expression = ParseExpression(text);

        if (unaryPrecedence >= binaryPrecedence) {
            using var e = new AssertingEnumerator(expression);
            e.AssertNode(SyntaxKind.BinaryExpression);
            e.AssertNode(SyntaxKind.UnaryExpression);
            e.AssertToken(unaryKind, unaryText);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "a");
            e.AssertToken(binaryKind, binaryText);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "b");
        } else {
            using var e = new AssertingEnumerator(expression);
            e.AssertNode(SyntaxKind.UnaryExpression);
            e.AssertToken(unaryKind, unaryText);
            e.AssertNode(SyntaxKind.BinaryExpression);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "a");
            e.AssertToken(binaryKind, binaryText);
            e.AssertNode(SyntaxKind.IdentifierName);
            e.AssertToken(SyntaxKind.IdentifierToken, "b");
        }
    }

    private static ExpressionSyntax ParseExpression(string text) {
        var syntaxTree = SyntaxTree.Parse(text);
        var member = Assert.Single(syntaxTree.GetCompilationUnitRoot().members);
        var globalStatement = Assert.IsType<GlobalStatementSyntax>(member);

        return Assert.IsType<LocalDeclarationStatementSyntax>(
            globalStatement.statement
        ).declaration.initializer.value;
    }

    private static bool AmbiguousOperator(SyntaxKind op1kind, SyntaxKind op2kind) {
        if (op1kind == SyntaxKind.LessThanToken && op2kind == SyntaxKind.GreaterThanToken)
            return true;

        return false;
    }

    private static IEnumerable<SyntaxKind> GetBinaryOperators() {
        return SyntaxFacts.GetBinaryOperatorTypes()
            .Where(k => k is not SyntaxKind.GreaterThanGreaterThanToken
                         and not SyntaxKind.GreaterThanGreaterThanGreaterThanToken);
    }

    public static IEnumerable<object[]> GetBinaryOperatorPairsData() {
        foreach (var op1 in GetBinaryOperators()) {
            foreach (var op2 in GetBinaryOperators()) {
                if (!AmbiguousOperator(op1, op2))
                    yield return new object[] { op1, op2 };
            }
        }
    }

    public static IEnumerable<object[]> GetUnaryOperatorPairsData() {
        foreach (var unary in SyntaxFacts.GetUnaryOperatorTypes()) {
            foreach (var binary in GetBinaryOperators()) {
                yield return new object[] { unary, binary };
            }
        }
    }
}
