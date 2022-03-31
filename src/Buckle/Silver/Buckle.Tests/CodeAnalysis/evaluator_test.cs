using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Xunit;

namespace Buckle.Tests.CodeAnalysis {
    public class EvaluatorTests {
        [Theory]
        [InlineData("1;", 1)]
        [InlineData("+1;", 1)]
        [InlineData("-1;", -1)]
        [InlineData("14 + 12;", 26)]
        [InlineData("12 - 3;", 9)]
        [InlineData("4 * 2;", 8)]
        [InlineData("9 / 3;", 3)]
        [InlineData("(10);", 10)]
        [InlineData("12 == 3;", false)]
        [InlineData("3 == 3;", true)]
        [InlineData("12 != 3;", true)]
        [InlineData("3 != 3;", false)]

        [InlineData("3 < 4;", true)]
        [InlineData("5 < 3;", false)]
        [InlineData("4 <= 4;", true)]
        [InlineData("4 <= 5;", true)]
        [InlineData("5 <= 4;", false)]
        [InlineData("3 > 4;", false)]
        [InlineData("5 > 3;", true)]
        [InlineData("4 >= 4;", true)]
        [InlineData("4 >= 5;", false)]
        [InlineData("5 >= 4;", true)]

        [InlineData("false == false;", true)]
        [InlineData("true == false;", false)]
        [InlineData("false != false;", false)]
        [InlineData("true != false;", true)]
        [InlineData("true;", true)]
        [InlineData("false;", false)]
        [InlineData("!true;", false)]
        [InlineData("!false;", true)]
        [InlineData("{ auto a = 1; a = 10 * a; }", 10)]

        [InlineData("{ auto a = 0; if (a == 0) { a = 10; } a; }", 10)]
        [InlineData("{ auto a = 0; if (a == 4) { a = 10; } a; }", 0)]
        [InlineData("{ auto a = 0; if (a == 0) { a = 10; } else { a = 5; } a; }", 10)]
        [InlineData("{ auto a = 0; if (a == 4) { a = 10; } else { a = 5; } a; }", 5)]

        [InlineData("{ auto i = 10; auto result = 0; while (i > 0) { result = result + i; i = i - 1; } result; }", 55)]
        public void Evaluator_Computes_CorrectValues(string text, object expectedValue) {
            AssertValue(text, expectedValue);
        }

        [Fact]
        public void Evaluator_VariableDelcaration_Reports_Reclaration() {
            var text = @"
                auto x = 10;
                auto y = 100;
                {
                    auto x = 10;
                }
                auto [x] = 5;
            ";

            var diagnostics = @"
                redefinition of 'x'
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Name_Reports_Undefined() {
            var text = @"[x] * 10;";

            var diagnostics = @"
                undefined symbol 'x'
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Assigns_Reports_Undefined() {
            var text = @"[x] = 10;";

            var diagnostics = @"
                undefined symbol 'x'
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Assigns_Reports_Readonly() {
            var text = @"
                let x = 10;
                x [=] 0;
            ";

            var diagnostics = @"
                assignment of read-only variable 'x'
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Assigns_Reports_CannotConvert() {
            var text = @"
                auto x = 10;
                x = [false];
            ";

            var diagnostics = @"
                cannot convert from System.Boolean to System.Int32
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Unary_Reports_Undefined() {
            var text = @"[+]true;";

            var diagnostics = @"
                operator '+' is not defined for type System.Boolean
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Binary_Reports_Undefined() {
            var text = @"10[+]true;";

            var diagnostics = @"
                operator '+' is not defined for types System.Int32 and System.Boolean
            ";

            AssertDiagnostics(text, diagnostics);
        }

        private void AssertValue(string text, object expectedValue) {
            var tree = SyntaxTree.Parse(text);
            var compilation = new Compilation(tree);
            var variables = new Dictionary<VariableSymbol, object>();
            var result = compilation.Evaluate(variables);

            Assert.Empty(result.diagnostics.ToArray());
            Assert.Equal(expectedValue, result.value);
        }

        private void AssertDiagnostics(string text, string diagnosticText) {
            var annotatedText = AnnotatedText.Parse(text);
            var syntaxTree = SyntaxTree.Parse(annotatedText.text);
            var compilation = new Compilation(syntaxTree);
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());

            var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);

            if (annotatedText.spans.Length != expectedDiagnostics.Length)
                throw new Exception("must mark as many spans as there are diagnostics");

            Assert.Equal(expectedDiagnostics.Length, result.diagnostics.count);

            for (int i=0; i<expectedDiagnostics.Length; i++) {
                var diagnostic = result.diagnostics.Pop();

                var expectedMessage = expectedDiagnostics[i];
                var actualMessage = diagnostic.msg;
                Assert.Equal(expectedMessage, actualMessage);

                var expectedSpan = annotatedText.spans[i];
                var actualSpan = diagnostic.span;
                Assert.Equal(expectedSpan.start, actualSpan.start);
                Assert.Equal(expectedSpan.end, actualSpan.end);
                Assert.Equal(expectedSpan.length, actualSpan.length);
            }
        }
    }
}
