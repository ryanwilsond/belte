using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Diagnostics;
using Buckle.Diagnostics;

namespace Buckle.Tests.CodeAnalysis;

public class EvaluatorTests {
    private readonly ITestOutputHelper writer;

    public EvaluatorTests(ITestOutputHelper writer) {
        this.writer = writer;
    }

    // Put all simple tests that want a specific result from the evaluator here
    [Theory]
    [InlineData(";", null)]

    [InlineData("1;", 1)]
    [InlineData("+1;", 1)]
    [InlineData("-1;", -1)]
    [InlineData("14 + 12;", 26)]
    [InlineData("12 - 3;", 9)]
    [InlineData("4 * 2;", 8)]
    [InlineData("4 ** 2;", 16)]
    [InlineData("9 / 3;", 3)]
    [InlineData("(10);", 10)]

    [InlineData("var a = 1; a += (2 + 3); return a;", 6)]
    [InlineData("var a = 1; a -= (2 + 3); return a;", -4)]
    [InlineData("var a = 1; a *= (2 + 3); return a;", 5)]
    [InlineData("var a = 1; a /= (2 + 3); return a;", 0)]
    [InlineData("var a = 2; a **= 2; return a;", 4)]
    [InlineData("var a = 1; a <<= 1; return a;", 2)]
    [InlineData("var a = 2; a >>= 1; return a;", 1)]
    [InlineData("var a = true; a &= (false); return a;", false)]
    [InlineData("var a = true; a |= (false); return a;", true)]
    [InlineData("var a = true; a ^= (true); return a;", false)]
    [InlineData("var a = 1; a |= 0; return a;", 1)]
    [InlineData("var a = 1; a &= 3; return a;", 1)]
    [InlineData("var a = 1; a &= 0; return a;", 0)]
    [InlineData("var a = 1; a ^= 0; return a;", 1)]
    [InlineData("var a = 1; var b = 2; var c = 3; a += b += c; return a;", 6)]
    [InlineData("var a = 1; var b = 2; var c = 3; a += b += c; return b;", 5)]

    [InlineData("1 | 2;", 3)]
    [InlineData("1 | 0;", 1)]
    [InlineData("1 & 3;", 1)]
    [InlineData("1 & 0;", 0)]
    [InlineData("1 ^ 0;", 1)]
    [InlineData("0 ^ 1;", 1)]
    [InlineData("1 ^ 1;", 0)]
    [InlineData("1 ^ 3;", 2)]
    [InlineData("~1;", -2)]
    [InlineData("~4;", -5)]
    [InlineData("1 << 1;", 2)]
    [InlineData("3 << 2;", 12)]
    [InlineData("2 >> 1;", 1)]
    [InlineData("3 >> 1;", 1)]
    [InlineData("12 >> 2;", 3)]
    [InlineData("-8 >> 2;", -2)]
    [InlineData("2 >>> 1;", 1)]
    [InlineData("3 >>> 1;", 1)]
    [InlineData("12 >>> 2;", 3)]
    [InlineData("-8 >>> 2;", 1073741822)]
    [InlineData("false | false;", false)]
    [InlineData("false | true;", true)]
    [InlineData("true | false;", true)]
    [InlineData("true | true;", true)]
    [InlineData("false & false;", false)]
    [InlineData("false & true;", false)]
    [InlineData("true & false;", false)]
    [InlineData("true & true;", true)]
    [InlineData("false ^ false;", false)]
    [InlineData("false ^ true;", true)]
    [InlineData("true ^ false;", true)]
    [InlineData("true ^ true;", false)]

    [InlineData("false == false;", true)]
    [InlineData("true == false;", false)]
    [InlineData("false != false;", false)]
    [InlineData("true != false;", true)]
    [InlineData("true && true;", true)]
    [InlineData("true && false;", false)]
    [InlineData("true;", true)]
    [InlineData("false;", false)]
    [InlineData("!true;", false)]
    [InlineData("!false;", true)]

    [InlineData("\"test\";", "test")]
    [InlineData("\"test\" + \"test2\";", "testtest2")]
    [InlineData("\"test\" == \"test\";", true)]
    [InlineData("\"test\" != \"test\";", false)]
    [InlineData("\"test\" == \"abc\";", false)]
    [InlineData("\"test\" != \"abc\";", true)]

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

    [InlineData("var a = 8; a >>>= 1; return a;", 4)]
    [InlineData("var a = -8; a >>>= 1; return a;", 2147483644)]
    [InlineData("var a = 12; a >>>= 5; return a;", 0)]

    // TODO @Logan remove need for casts
    [InlineData("3.2 + 3.4;", (float)6.6000004)]
    [InlineData("3.2 - 3.4;", -(float)0.20000005)]
    [InlineData("10 * 1.5;", (float)15)]
    [InlineData("10 * (int)1.5;", 10)]
    [InlineData("9 / 2;", 4)]
    [InlineData("9.0 / 2;", (float)4.5)]
    [InlineData("9 / 2.0;", (float)4.5)]
    [InlineData("4.1 ** 2;", (float)16.81)]
    [InlineData("4.1 ** 2.1;", (float)19.357355)]

    [InlineData("int a = 10; return a;", 10)]
    [InlineData("int a = 10; return a * a;", 100)]
    [InlineData("int a = 1; return 10 * a;", 10)]

    [InlineData("int a = 0; if (a == 0) { a = 10; } return a;", 10)]
    [InlineData("int a = 0; if (a == 4) { a = 10; } return a;", 0)]
    [InlineData("int a = 0; if (a == 0) { a = 10; } else { a = 5; } return a;", 10)]
    [InlineData("int a = 0; if (a == 4) { a = 10; } else { a = 5; } return a;", 5)]

    [InlineData("int i = 10; int result = 0; while (i > 0) { result++; i--; } return result;", 10)]
    [InlineData("int result = 1; for (int i=0; i<=10; i++) { result+=result; } return result;", 2048)]
    [InlineData("int result = 0; do { result++; } while (result < 10); return result;", 10)]

    [InlineData("[NotNull]int a = 10; return a;", 10)]
    [InlineData("[NotNull]int a = 10; return a * a;", 100)]
    [InlineData("[NotNull]int a = 1; return 10 * a;", 10)]
    [InlineData("[NotNull]int a = 0; if (a == 0) { a = 10; } return a;", 10)]
    [InlineData("[NotNull]int a = 0; if (a == 4) { a = 10; } return a;", 0)]
    [InlineData("[NotNull]int a = 0; if (a == 0) { a = 10; } else { a = 5; } return a;", 10)]
    [InlineData("[NotNull]int a = 0; if (a == 4) { a = 10; } else { a = 5; } return a;", 5)]

    [InlineData("decimal[] a = {3.1, 2.56, 5.23123}; return a[2];", (float)5.23123)]
    [InlineData("var a = {3.1, 2.56, 5.23123}; return a[0];", (float)3.1)]

    [InlineData("(null + 3) is null;", true)]
    [InlineData("(null > 3) is null;", true)]
    [InlineData("null || true;", true)]

    // TODO add these tests and implement required features (refs and is/isnt type)
    // It is commented out right now because theses features are bigger than was expected,
    // So these features are not going to be added in this PR
    // [InlineData("int x = 4; ref int y = ref x; x++; return y;", 5)]

    // [InlineData("3 is int;", true)]
    // [InlineData("null is int;", false)]
    // [InlineData("4 is decimal;", false)]
    // [InlineData("(decimal)4 is decimal;", true)]
    // [InlineData("4.0 is decimal;", true)]
    // [InlineData("4.0 isnt bool;", true)]
    // [InlineData("4 is any;", false)]
    // [InlineData("null isnt int;", true)]
    [InlineData("null is null;", true)]
    [InlineData("3 isnt null;", true)]
    [InlineData("null isnt null;", false)]
    [InlineData("var a = 3; return a is null;", false)]
    [InlineData("var a = 3; return a isnt null;", true)]
    [InlineData("int a = 3; a += null; return a is null;", true)]
    [InlineData("int a = 3; a += null; return a isnt null;", false)]

    [InlineData("type a = typeof(int[]);", null)]

    [InlineData("(decimal)3;", (float)3)]
    [InlineData("(int)3.4;", 3)]
    [InlineData("(int)3.6;", 3)]
    [InlineData("([NotNull]int)3;", 3)]

    [InlineData("int x = 2; int y = { return 2 * x; }; return y;", 4)]
    [InlineData("int funcA() { int funcB() { return 2; } return funcB() + 1; } return funcA(); ", 3)]
    [InlineData("int funcA() { int funcB() { int funcA() { return 2; } return funcA() + 1; } return funcB() + 1; } return funcA();", 3)]
    public void Evaluator_Computes_CorrectValues(string text, object expectedValue) {
        AssertValue(text, expectedValue);
    }

    // All other complex tests go here
    [Fact]
    public void Evaluator_IfStatement_Reports_NotReachableCode_Warning() {
        var text = @"
            void test() {
                const int x = 4 * 3;
                if (x > 12) {
                    [PrintLine(""x"")];
                } else {
                    PrintLine(""x"");
                }
            }
        ";

        var diagnostics = @"
            unreachable code
        ";

        AssertDiagnostics(text, diagnostics, true);
    }

    [Fact]
    public void Evaluator_CompoundExpression_Reports_Undefined() {
        var text = @"
            var x = 10;
            x [+=] false;
        ";

        var diagnostics = @"
            operator '+=' is not defined for types 'int' and 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_CompoundExpression_Assignment_NonDefinedVariable_Reports_Undefined() {
        var text = @"
            [x] += 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_CompoundDeclarationExpression_Reports_CannotAssign() {
        var text = @"
            {
                const int x = 10;
                x [+=] 1;
            }
        ";

        var diagnostics = @"
            assignment of constant variable 'x'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_ElseStatement_Reports_NotReachableCode_Warning() {
        var text = @"
            int test() {
                if (true)
                    return 1;
                else
                    [return] 0;
            }
        ";

        var diagnostics = @"
            unreachable code
        ";

        AssertDiagnostics(text, diagnostics, true);
    }

    [Fact]
    public void Evaluator_WhileStatement_Reports_NotReachableCode_Warning() {
        var text = @"
            void test() {
                while (false) {
                    [continue];
                }
            }
        ";

        var diagnostics = @"
            unreachable code
        ";

        AssertDiagnostics(text, diagnostics, true);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_NoInfiniteLoop() {
        var text = @"PrintLine(""Hi""[=]);";

        var diagnostics = @"
            unexpected token '='
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_Missing() {
        var text = @"
            PrintLine([)];
        ";

        var diagnostics = @"
            function 'PrintLine' expects 1 argument, got 0
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_Exceeding() {
        var text = @"
            PrintLine(""Hello""[, "" "", "" world!""]);
        ";

        var diagnostics = @"
            function 'PrintLine' expects 1 argument, got 3
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_FunctionParameters_NoInfiniteLoop() {
        // TODO doesn't throw when debugging, but does normally??
        // need to debug the test
        var text = @"
            void hi(string name[=]) {
                PrintLine(""Hi "" + name + ""!"");
            }
        ";

        var diagnostics = @"
            unexpected token '='
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_FunctionReturn_Missing() {
        var text = @"
            int [add](int a, int b) {
            }
        ";

        var diagnostics = @"
            not all code paths return a value
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Block_NoInfiniteLoop() {
        var text = @"
            {
            [)][]
        ";

        var diagnostics = @"
            unexpected token ')'
            expected '}' at end of input
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_IfStatement_Reports_CannotConvert() {
        var text = @"
            var x = 0;
            if ([10])
                x = 1;
        ";

        var diagnostics = @"
            cannot convert from type 'int' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_WhileStatement_Reports_CannotConvert() {
        var text = @"
            var x = 0;
            while ([10]) { x = 10; }
        ";

        var diagnostics = @"
            cannot convert from type 'int' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_DoWhileStatement_Reports_CannotConvert() {
        var text = @"
            var x = 0;
            do { x = 10; } while ([10]);
        ";

        var diagnostics = @"
            cannot convert from type 'int' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_ForStatement_Reports_CannotConvert() {
        var text = @"
            for (int i=0; [i]; i++) {}
        ";

        var diagnostics = @"
            cannot convert from type 'int' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_VariableDeclaration_Reports_Redeclaration() {
        var text = @"
            var x = 10;
            var y = 100;
            {
                var x = 10;
            }
            var [x] = 5;
        ";

        var diagnostics = @"
            redefinition of 'x'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_NameExpression_Reports_Undefined() {
        var text = @"
            [x] * 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_NameExpression_Reports_NoErrorForInsertedToken() {
        AssertDiagnostics("", "");
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_Undefined() {
        var text = @"
            [x] = 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotAssign() {
        var text = @"
            [PrintLine] = 10;
        ";

        var diagnostics = @"
            function 'PrintLine' used as a variable
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_Readonly() {
        var text = @"
            const int x = 10;
            x [=] 0;
        ";

        var diagnostics = @"
            assignment of constant variable 'x'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotConvert() {
        var text = @"
            var x = 10;
            x = [false];
        ";

        var diagnostics = @"
            cannot convert from type 'bool' to 'int'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_CallExpression_Reports_Undefined() {
        var text = @"
            [foo]();
        ";

        var diagnostics = @"
            undefined function 'foo'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_CallExpression_Reports_CannotCall() {
        var text = @"
            var foo = 4;
            [foo]();
        ";

        var diagnostics = @"
            called object 'foo' is not a function
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Variables_ShadowsFunction() {
        var text = @"
            int PrintLine = 4;
            [PrintLine](""test"");
        ";
        // TODO Maybe binder is skipping variables when going up the scopes to search for the function?

        var diagnostics = @"
            called object 'PrintLine' is not a function
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Function_ShouldNotReturnValue() {
        var text = @"
            void func() {
                [return] 5;
            }
        ";

        var diagnostics = @"
            return statement with a value, in function returning void
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Function_ShouldNotReturnVoid() {
        var text = @"
            int func() {
                [return];
            }
        ";

        var diagnostics = @"
            return statement with no value, in function returning non-void
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Script_Return() {
        var text = @"
            return;
        ";

        AssertValue(text, null);
    }

    [Fact]
    public void Evaluator_Expression_MustHaveValue() {
        var text = @"
            void func() {}
            var x = [func()];
        ";

        var diagnostics = @"
            expression must have a value
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Break_Invalid() {
        var text = @"
            [break];
        ";

        var diagnostics = @"
            break statement not within a loop
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Parameter_AlreadyDeclared() {
        var text = @"
            void func(int a, [int a]) {}
        ";

        var diagnostics = @"
            redefinition of parameter 'a'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Function_MustHaveName() {
        var text = @"
            void [(]int a) {}
        ";

        var diagnostics = @"
            expected identifier
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Function_WrongArgumentType() {
        var text = @"
            void func(int a) {}
            func([false]);
        ";

        var diagnostics = @"
            cannot convert from type 'bool' to 'int'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_InvalidType() {
        var text = @"
            void func([invalidType] a) {}
        ";

        var diagnostics = @"
            unknown type 'invalidType'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_UnaryOperator_Reports_Undefined() {
        var text = @"
            [+]true;
        ";

        var diagnostics = @"
            operator '+' is not defined for type 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_BinaryOperator_Reports_Undefined() {
        var text = @"
            10[+]true;
        ";

        var diagnostics = @"
            operator '+' is not defined for types 'int' and 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_DivideByZero_ThrowsException() {
        // TODO need a way to assert exceptions
        var text = @"
            56/0;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Function_CanDeclare() {
        var text = @"
            void myFunction(int num1, int num2) {
                Print(num1 + num2 / 3.14159);
            }
            myFunction(1, 2);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Function_CanCall() {
        var text = @"
            void myFunction(int num) {
                Print(num ** 2);
            }
            myFunction(2);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_CallExpression_ExpectedCloseParenthesis() {
        var text = @"
            Print(num ** 2 [(]
        ";

        var diagnostics = @"
            unexpected token '(', expected ')'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    private void AssertValue(string text, object expectedValue) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(null, syntaxTree);
        var variables = new Dictionary<VariableSymbol, object>();
        var result = compilation.Evaluate(variables);

        if (result.value is float && expectedValue is double d && (float)expectedValue == d )
            expectedValue = (float)expectedValue;

        Assert.Empty(result.diagnostics.FilterOut(DiagnosticType.Warning).ToArray());
        Assert.Equal(expectedValue, result.value);
    }

    private void AssertDiagnostics(string text, string diagnosticText, bool assertWarnings = false) {
        var annotatedText = AnnotatedText.Parse(text);
        var syntaxTree = SyntaxTree.Parse(annotatedText.text);

        var tempDiagnostics = new BelteDiagnosticQueue();

        // TODO currently all tests pass, but remind, does execution stop if parser has errors?
        if (syntaxTree.diagnostics.FilterOut(DiagnosticType.Warning).Any()) {
            tempDiagnostics.Move(syntaxTree.diagnostics);
        } else {
            var compilation = Compilation.CreateScript(null, syntaxTree);
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());
            tempDiagnostics = result.diagnostics;
        }

        var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);

        if (annotatedText.spans.Length != expectedDiagnostics.Length)
            throw new Exception("must mark as many spans as there are diagnostics");

        var diagnostics = assertWarnings
            ? tempDiagnostics
            : tempDiagnostics.FilterOut(DiagnosticType.Warning);

        if (expectedDiagnostics.Length != diagnostics.count) {
            writer.WriteLine($"Input: {annotatedText.text}");
            foreach (var diagnostic in diagnostics.AsList())
                writer.WriteLine($"Diagnostic ({diagnostic.info.severity}): {diagnostic.message}");
        }

        Assert.Equal(expectedDiagnostics.Length, diagnostics.count);

        for (int i=0; i<expectedDiagnostics.Length; i++) {
            var diagnostic = diagnostics.Pop();

            var expectedMessage = expectedDiagnostics[i];
            var actualMessage = diagnostic.message;
            Assert.Equal(expectedMessage, actualMessage);

            var expectedSpan = annotatedText.spans[i];
            var actualSpan = diagnostic.location.span;
            writer.WriteLine($"start: {expectedSpan.start}, {actualSpan.start}");
            Assert.Equal(expectedSpan.start, actualSpan.start);
            writer.WriteLine($"end: {expectedSpan.end}, {actualSpan.end}");
            Assert.Equal(expectedSpan.end, actualSpan.end);
            writer.WriteLine($"length: {expectedSpan.length}, {actualSpan.length}");
            Assert.Equal(expectedSpan.length, actualSpan.length);
        }
    }
}
