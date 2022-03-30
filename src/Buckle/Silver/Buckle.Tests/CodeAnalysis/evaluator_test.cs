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
        [InlineData("false == false;", true)]
        [InlineData("true == false;", false)]
        [InlineData("false != false;", false)]
        [InlineData("true != false;", true)]
        [InlineData("true;", true)]
        [InlineData("false;", false)]
        [InlineData("!true;", false)]
        [InlineData("!false;", true)]
        [InlineData("{ auto a = 1; a = 10 * a; }", 10)]
        public void Evaluator_Computes_CorrectValues(string text, object expectedValue) {
            var tree = SyntaxTree.Parse(text);
            var compilation = new Compilation(tree);
            var variables = new Dictionary<VariableSymbol, object>();
            var result = compilation.Evaluate(variables);

            Assert.Empty(result.diagnostics.ToArray());
            Assert.Equal(expectedValue, result.value);
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
