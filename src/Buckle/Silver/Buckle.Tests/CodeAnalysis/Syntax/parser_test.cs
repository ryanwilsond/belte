using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Syntax {
    public class ParserTests {
        [Theory]
        [MemberData(nameof(GetBinaryOperatorPairsData))]
        internal void Parser_BinaryExpression_HonorsPrecedences(SyntaxType op1, SyntaxType op2) {
            var op1Precedence = SyntaxFacts.GetBinaryPrecedence(op1);
            var op2Precedence = SyntaxFacts.GetBinaryPrecedence(op2);
            var op1Text = SyntaxFacts.GetText(op1);
            var op2Text = SyntaxFacts.GetText(op2);
            var text = $"a {op1Text} b {op2Text} c";
            Expression expression = ParseExpression(text);

            if (op1Precedence >= op2Precedence) {
                using (var e = new AssertingEnumerator(expression)) {
                    e.AssertNode(SyntaxType.BINARY_EXPRESSION);
                    e.AssertNode(SyntaxType.BINARY_EXPRESSION);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "a");
                    e.AssertToken(op1, op1Text);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "b");
                    e.AssertToken(op2, op2Text);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "c");
                }
            } else {
                using (var e = new AssertingEnumerator(expression)) {
                    e.AssertNode(SyntaxType.BINARY_EXPRESSION);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "a");
                    e.AssertToken(op1, op1Text);
                    e.AssertNode(SyntaxType.BINARY_EXPRESSION);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "b");
                    e.AssertToken(op2, op2Text);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "c");
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetUnaryOperatorPairsData))]
        internal void Parser_UnaryExpression_HonorsPrecedences(SyntaxType unaryType, SyntaxType binaryType) {
            var unaryPrecedence = SyntaxFacts.GetUnaryPrecedence(unaryType);
            var binaryPrecedence = SyntaxFacts.GetBinaryPrecedence(binaryType);
            var unaryText = SyntaxFacts.GetText(unaryType);
            var binaryText = SyntaxFacts.GetText(binaryType);
            var text = $"{unaryText} a {binaryText} b";
            Expression expression = ParseExpression(text);

            if (unaryPrecedence >= binaryPrecedence) {
                using (var e = new AssertingEnumerator(expression)) {
                    e.AssertNode(SyntaxType.BINARY_EXPRESSION);
                    e.AssertNode(SyntaxType.UNARY_EXPRESSION);
                    e.AssertToken(unaryType, unaryText);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "a");
                    e.AssertToken(binaryType, binaryText);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "b");
                }
            } else {
                using (var e = new AssertingEnumerator(expression)) {
                    e.AssertNode(SyntaxType.UNARY_EXPRESSION);
                    e.AssertToken(unaryType, unaryText);
                    e.AssertNode(SyntaxType.BINARY_EXPRESSION);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "a");
                    e.AssertToken(binaryType, binaryText);
                    e.AssertNode(SyntaxType.NAME_EXPRESSION);
                    e.AssertToken(SyntaxType.IDENTIFIER_TOKEN, "b");
                }
            }
        }

        private static Expression ParseExpression(string text) {
            var syntaxTree = SyntaxTree.Parse(text);
            var member = Assert.Single(syntaxTree.root.members);
            var globalStatement = Assert.IsType<GlobalStatement>(member);
            return Assert.IsType<ExpressionStatement>(globalStatement.statement).expression;
        }

        public static IEnumerable<object[]> GetBinaryOperatorPairsData() {
            foreach (var op1 in SyntaxFacts.GetBinaryOperatorTypes()) {
                foreach (var op2 in SyntaxFacts.GetBinaryOperatorTypes()) {
                    yield return new object[] { op1, op2 };
                }
            }
        }

        public static IEnumerable<object[]> GetUnaryOperatorPairsData() {
            foreach (var unary in SyntaxFacts.GetUnaryOperatorTypes()) {
                foreach (var binary in SyntaxFacts.GetBinaryOperatorTypes()) {
                    yield return new object[] { unary, binary };
                }
            }
        }
    }
}
