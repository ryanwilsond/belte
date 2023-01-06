using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Buckle.Tests.CodeAnalysis;

public sealed partial class EvaluatorTests {
    private readonly ITestOutputHelper writer;

    public EvaluatorTests(ITestOutputHelper writer) {
        this.writer = writer;
    }

    [Theory]
    // Empty expressions
    [InlineData(";", null)]
    [InlineData(";;", null)]
    // Literal expressions
    [InlineData("1;", 1)]
    [InlineData("6.6;", 6.6)]
    [InlineData("\"test\";", "test")]
    [InlineData("true;", true)]
    [InlineData("false;", false)]
    [InlineData("(10);", 10)]
    [InlineData("0b1;", 1)]
    [InlineData("-0B1;", -1)]
    [InlineData("0b01101;", 13)]
    [InlineData("0b11111111;", 255)]
    [InlineData("0x01;", 1)]
    [InlineData("-0x01;", -1)]
    [InlineData("0XDEADBEEF;", -559038737)]
    [InlineData("0xfF;", 255)]
    [InlineData("123_123;", 123123)]
    [InlineData("1_1;", 11)]
    [InlineData("1_1.42;", 11.42)]
    [InlineData("1_1._42;", 11.42)]
    [InlineData("1_1._4_2;", 11.42)]
    [InlineData("6.26e34;", 6.26E+34)]
    [InlineData("6.26e+34;", 6.26E+34)]
    [InlineData("6.26E34;", 6.26E+34)]
    [InlineData("6.26E+34;", 6.26E+34)]
    [InlineData("6E-10;", 6E-10)]
    // Unary expressions
    [InlineData("+1;", 1)]
    [InlineData("+6;", 6)]
    [InlineData("+ +6;", 6)]
    [InlineData("-1;", -1)]
    [InlineData("-6;", -6)]
    [InlineData("- -6;", 6)]
    [InlineData("- +6;", -6)]
    [InlineData("+ -6;", -6)]
    [InlineData("!true;", false)]
    [InlineData("!(!true);", true)]
    [InlineData("!(!false);", false)]
    [InlineData("~1;", -2)]
    [InlineData("~4;", -5)]
    // Binary expressions
    [InlineData("14 + 12;", 26)]
    [InlineData("4 + -7;", -3)]
    [InlineData("\"test\" + \"test2\";", "testtest2")]
    [InlineData("3.2 + 3.4;", 6.6)]
    [InlineData("12 - 3;", 9)]
    [InlineData("3 - 12;", -9)]
    [InlineData("3.2 - 3.4;", -0.19999999999999973)]
    [InlineData("4 * 2;", 8)]
    [InlineData("-6 * -4;", 24)]
    [InlineData("10 * 1.5;", 15)]
    [InlineData("10 * (int)1.5;", 10)]
    [InlineData("9 / 3;", 3)]
    [InlineData("12 / 3;", 4)]
    [InlineData("9 / 2;", 4)]
    [InlineData("9.0 / 2;", 4.5)]
    [InlineData("9 / 2.0;", 4.5)]
    [InlineData("4 ** 2;", 16)]
    [InlineData("2 ** 4;", 16)]
    [InlineData("4.1 ** 2;", 16.81)]
    [InlineData("4.1 ** 2.1;", 19.35735875876448)]
    [InlineData("1 & 3;", 1)]
    [InlineData("1 & 0;", 0)]
    [InlineData("false & false;", false)]
    [InlineData("false & true;", false)]
    [InlineData("true & false;", false)]
    [InlineData("true & true;", true)]
    [InlineData("1 | 2;", 3)]
    [InlineData("1 | 0;", 1)]
    [InlineData("false | false;", false)]
    [InlineData("false | true;", true)]
    [InlineData("true | false;", true)]
    [InlineData("true | true;", true)]
    [InlineData("1 ^ 0;", 1)]
    [InlineData("0 ^ 1;", 1)]
    [InlineData("1 ^ 1;", 0)]
    [InlineData("1 ^ 3;", 2)]
    [InlineData("false ^ false;", false)]
    [InlineData("false ^ true;", true)]
    [InlineData("true ^ false;", true)]
    [InlineData("true ^ true;", false)]
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
    [InlineData("false && false;", false)]
    [InlineData("false && true;", false)]
    [InlineData("true && false;", false)]
    [InlineData("true && true;", true)]
    [InlineData("false || false;", false)]
    [InlineData("false || true;", true)]
    [InlineData("true || false;", true)]
    [InlineData("true || true;", true)]
    [InlineData("null || true;", true)]
    [InlineData("false == false;", true)]
    [InlineData("true == false;", false)]
    [InlineData("12 == 3;", false)]
    [InlineData("3 == 3;", true)]
    [InlineData("\"test\" == \"abc\";", false)]
    [InlineData("\"test\" == \"test\";", true)]
    [InlineData("false != false;", false)]
    [InlineData("true != false;", true)]
    [InlineData("\"test\" != \"test\";", false)]
    [InlineData("\"test\" != \"abc\";", true)]
    [InlineData("12 != 3;", true)]
    [InlineData("3 != 3;", false)]
    [InlineData("3 < 4;", true)]
    [InlineData("5 < 3;", false)]
    [InlineData("3 > 4;", false)]
    [InlineData("5 > 3;", true)]
    [InlineData("4 <= 4;", true)]
    [InlineData("4 <= 5;", true)]
    [InlineData("5 <= 4;", false)]
    [InlineData("4 >= 4;", true)]
    [InlineData("4 >= 5;", false)]
    [InlineData("5 >= 4;", true)]
    [InlineData("null is null;", true)]
    [InlineData("3 is null;", false)]
    [InlineData("(null + 3) is null;", true)]
    [InlineData("(null > 3) is null;", true)]
    [InlineData("null isnt null;", false)]
    [InlineData("3 isnt null;", true)]
    [InlineData("5 % 2;", 1)]
    [InlineData("9 % 5;", 4)]
    [InlineData("5 ?? 2;", 5)]
    [InlineData("null ?? 2;", 2)]
    // Compound assignments
    [InlineData("var a = 1; a += (2 + 3); return a;", 6)]
    [InlineData("var a = 1; a -= (2 + 3); return a;", -4)]
    [InlineData("var a = 1; a *= (2 + 3); return a;", 5)]
    [InlineData("var a = 1; a /= (2 + 3); return a;", 0)]
    [InlineData("var a = 2; a **= 2; return a;", 4)]
    [InlineData("var a = true; a &= (false); return a;", false)]
    [InlineData("var a = 1; a &= 3; return a;", 1)]
    [InlineData("var a = 1; a &= 0; return a;", 0)]
    [InlineData("var a = true; a |= (false); return a;", true)]
    [InlineData("var a = 1; a |= 0; return a;", 1)]
    [InlineData("var a = true; a ^= (true); return a;", false)]
    [InlineData("var a = 1; a ^= 0; return a;", 1)]
    [InlineData("var a = 1; a <<= 1; return a;", 2)]
    [InlineData("var a = 2; a >>= 1; return a;", 1)]
    [InlineData("var a = 8; a >>>= 1; return a;", 4)]
    [InlineData("var a = -8; a >>>= 1; return a;", 2147483644)]
    [InlineData("var a = 12; a >>>= 5; return a;", 0)]
    [InlineData("var a = 5; a %= 2; return a;", 1)]
    [InlineData("var a = 5; a ??= 2; return a;", 5)]
    [InlineData("int a = null; a ??= 2; return a;", 2)]
    // * Will get fixed with the introduction of the Blender
    // [InlineData("var a = 1; var b = 2; var c = 3; a += b += c; return a;", 6)]
    // [InlineData("var a = 1; var b = 2; var c = 3; a += b += c; return b;", 5)]
    [InlineData("var a = 3; return a is null;", false)]
    [InlineData("var a = 3; return a isnt null;", true)]
    [InlineData("int a = 3; a += null; return a is null;", true)]
    [InlineData("int a = 3; a += null; return a isnt null;", false)]
    // Ternary expressions
    [InlineData("true ? 3 : 5;", 3)]
    [InlineData("false ? \"asdf\" : \"asdf2\";", "asdf2")]
    [InlineData("int a = 3; int b = a > 2 ? 5 : 3; return b;", 5)]
    [InlineData("int a = 3; int b = a > 2 && false ? a + 5 : a + 3; return b;", 6)]
    // Assignment expressions
    [InlineData("int a = 10; return a;", 10)]
    [InlineData("int a = 10; return a * a;", 100)]
    [InlineData("int a = 1; return 10 * a;", 10)]
    [InlineData("int a; return a;", null)]
    // If statements
    [InlineData("int a = 0; if (a == 0) { a = 10; } return a;", 10)]
    [InlineData("int a = 0; if (a == 4) { a = 10; } return a;", 0)]
    [InlineData("int a = 0; if (a == 0) { a = 10; } else { a = 5; } return a;", 10)]
    [InlineData("int a = 0; if (a == 4) { a = 10; } else { a = 5; } return a;", 5)]
    // For statements
    [InlineData("int result = 1; for (int i=0; i<=10; i++) { result+=result; } return result;", 2048)]
    [InlineData("int result = 0; for (int i=0; i<5; i++) { result++; } return result;", 5)]
    [InlineData("int result; for (int i=0; i<=10; i++) { result=i; } return result;", 10)]
    [InlineData("int result = 1; for (int i=10; i>0; i--) { result+=i; } return result;", 56)]
    // While statements
    [InlineData("int i = 0; int result = 1; while (i<=10) { result+=result; i++; } return result;", 2048)]
    [InlineData("int i = 0; int result = 0; while (i<5) { result++; i++; } return result;", 5)]
    [InlineData("int i = 0; int result; while (i<=10) { result=i; i++; } return result;", 10)]
    [InlineData("int i = 10; int result = 1; while (i>0) { result+=i; i--; } return result;", 56)]
    // Do-While statements
    [InlineData("int result = 0; do { result++; } while (result < 10); return result;", 10)]
    [InlineData("int result = 0; do { result++; } while (false); return result;", 1)]
    [InlineData("int result = 0; do { result++; } while (result < 0); return result;", 1)]
    [InlineData("int result = 10; do { result*=2; } while (result < 30); return result;", 40)]
    // Attributes
    [InlineData("[NotNull]int a = 10; return a;", 10)]
    [InlineData("[NotNull]int a = 10; return a * a;", 100)]
    [InlineData("[NotNull]int a = 1; return 10 * a;", 10)]
    [InlineData("[NotNull]int a = 0; if (a == 0) { a = 10; } return a;", 10)]
    [InlineData("[NotNull]int a = 0; if (a == 4) { a = 10; } return a;", 0)]
    [InlineData("[NotNull]int a = 0; if (a == 0) { a = 10; } else { a = 5; } return a;", 10)]
    [InlineData("[NotNull]int a = 0; if (a == 4) { a = 10; } else { a = 5; } return a;", 5)]
    // Initializer list expressions and index expressions
    [InlineData("decimal[] a = {3.1, 2.56, 5.23123}; return a[2];", 5.23123)]
    [InlineData("var a = {3.1, 2.56, 5.23123}; return a[0];", 3.1)]
    [InlineData("string[] a = {\"hello\", \"world\"}; return a[1];", "world")]
    [InlineData("bool[] a = {true, true, false, false}; return a[3];", false)]
    [InlineData("bool[] a = {true, true, false, false}; return a?[3];", false)]
    [InlineData("bool[] a; return a?[3];", null)]
    [InlineData("bool[] a = {true, false}; a = null; return a?[3];", null)]
    [InlineData("bool[] a = null; return a?[3];", null)]
    // Reference expressions
    [InlineData("int x = 4; ref int y = ref x; x++; return y;", 5)]
    [InlineData("int x = 4; ref int y = ref x; y++; return x;", 5)]
    [InlineData("int x = 4; int y = 3; ref int z = ref x; z = ref y; z++; return x;", 4)]
    [InlineData("var a = {1, 2, 3}; a[0] = 6; return a[0];", 6)]
    // Name expressions
    [InlineData("int a = 3; int b = 6; return a;", 3)]
    [InlineData("int a = 3; int b = 6; return b;", 6)]
    [InlineData("int a = 3; int b = 6; b += a; return a;", 3)]
    [InlineData("int a = 3; int b = 6; b += a; return b;", 9)]
    // Postfix expressions
    [InlineData("int a = 3; a++; return a;", 4)]
    [InlineData("int a = 3; a--; return a;", 2)]
    [InlineData("int a = 3; a--; a++; return a;", 3)]
    [InlineData("int a = 1; a--; a--; return a;", -1)]
    [InlineData("int a = 4; int b = a++; return b;", 4)]
    [InlineData("int a = 4; int b = a--; return b;", 4)]
    [InlineData("int a = 4; return a!;", 4)]
    [InlineData("[NotNull]int a = 4; return a! + 1;", 5)]
    // Prefix expressions
    [InlineData("int a = 3; ++a; return a;", 4)]
    [InlineData("int a = 3; --a; return a;", 2)]
    [InlineData("int a = 3; --a; ++a; return a;", 3)]
    [InlineData("int a = 1; --a; --a; return a;", -1)]
    [InlineData("int a = 4; int b = ++a; return b;", 5)]
    [InlineData("int a = 4; int b = --a; return b;", 3)]
    // Parenthesized expressions
    [InlineData("int a = (3 + 4) * 2; return a;", 14)]
    [InlineData("int a = 3 + (4 * 2); return a;", 11)]
    [InlineData("int a = 12 / (4 * 2); return a;", 1)]
    [InlineData("int a = (12 / 4) * 2; return a;", 6)]
    // Call expressions
    [InlineData("Value(3);", 3)]
    [InlineData("HasValue(3);", true)]
    [InlineData("HasValue(null);", false)]
    [InlineData("Value(\"test\");", "test")]
    // Cast expressions
    [InlineData("(decimal)3;", 3)]
    [InlineData("(int)3.4;", 3)]
    [InlineData("(int)3.6;", 3)]
    [InlineData("([NotNull]int)3;", 3)]
    [InlineData("string a = (string)(int)3.6; return a;", "3")]
    // Block statements and return expressions
    [InlineData("{ int a = 3; return a; }", 3)]
    [InlineData("int a = 5; { a = 3; return a; }", 3)]
    [InlineData("int a = 5; { a = 3; } return a;", 3)]
    [InlineData("int a = 5; { int b = 3 + a; return b; } return a;", 8)]
    // Local function statements
    [InlineData("int funcA() { int funcB() { return 2; } return funcB() + 1; } return funcA(); ", 3)]
    [InlineData("int funcA() { int funcB() { int funcA() { return 2; } return funcA() + 1; } return funcB() + 1; } return funcA();", 4)]
    [InlineData("int funcA() { int a = 1; int funcB(int b) { return a + b; } return funcB(4); } return funcA(); ", 5)]
    [InlineData("int funcA() { int a = 5; int funcB(int b) { return a + b; } return funcB(1); } return funcA(); ", 6)]
    // Member access expressions
    [InlineData("struct A { int num; } A myVar = A(); myVar.num = 3; return myVar.num + 1;", 4)]
    [InlineData("struct A { int num; } struct B { A a; } B myVar = B(); myVar.a = A(); myVar.a.num = 3; return myVar.a.num + 1;", 4)]
    [InlineData("struct A { int a; int b; } A myVar = A(); myVar.a = 3; myVar.b = myVar.a + 3; return myVar.b;", 6)]
    [InlineData("struct A { int a; int b; } A myVar = A(); myVar.a = 3; myVar.b = myVar.a + 3; return myVar.a;", 3)]
    [InlineData("struct A { int num; } A myVar; int a = myVar?.num; return a;", null)]
    [InlineData("struct A { int num; } A myVar = A(); myVar.num = 7; int a = myVar?.num; return a;", 7)]
    // TypeOf expressions
    [InlineData("type a = typeof(int[]);", null)]
    [InlineData("type a = typeof(string);", null)]
    [InlineData("type a = typeof([NotNull]decimal);", null)]
    [InlineData("struct A { int num; } type a = typeof(A);", null)]
    // Try statements
    [InlineData("try { int a = 56/0; return a; } catch { return 3; }", 3)]
    [InlineData("try { int a = 56/1; return a; } catch { return 3; }", 56)]
    [InlineData("int a = 3; try { int b = 56/0; a += b; } catch { a += 3; } finally { return a; }", 6)]
    [InlineData("int a = 3; try { int b = 56/1; a += b; } catch { a += 3; } finally { return a; }", 59)]
    // Break statements
    [InlineData("int result = 3; for (int i=0; i<10; i++) { result++; if (result == 5) break; } return result;", 5)]
    [InlineData("int result = 3; for (int i=0; i<10; i++) { result++; if (result < 5) break; } return result;", 4)]
    [InlineData("int result = 3; while (true) { result++; if (result == 5) break; } return result;", 5)]
    [InlineData("int result = 3; while (true) { result++; if (result > 5) break; } return result;", 6)]
    // Continue statements
    [InlineData("var condition = false; int result = 3; while (true) { if (condition) continue; else break; result = 4; } return result;", 3)]
    [InlineData("var condition = false; int result = 3; while (true) { if (condition) continue; result = 4; if (result == 4) break; } return result;", 4)]
    [InlineData("var condition = true; int result = 3; while (true) { if (condition) ; else continue; result = 4; if (result == 4) break; } return result;", 4)]
    [InlineData("var condition = true; int result = 3; while (true) { if (condition) break; else continue; result = 4; } return result;", 3)]
    public void Evaluator_Computes_CorrectValues(string text, object expectedValue) {
        AssertValue(text, expectedValue);
    }

    private void AssertValue(string text, object expectedValue) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(null, syntaxTree);
        var variables = new Dictionary<VariableSymbol, EvaluatorObject>();
        var _ = false;
        var result = compilation.Evaluate(variables, ref _);

        if (result.value is double && (Convert.ToDouble(expectedValue)).CompareTo(result.value) == 0)
            expectedValue = Convert.ToDouble(expectedValue);

        Assert.Empty(result.diagnostics.FilterOut(DiagnosticType.Warning).ToArray());
        Assert.Equal(expectedValue, result.value);
    }

    private void AssertExceptions(string text, params Exception[] exceptions) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(null, syntaxTree);
        var _ = false;
        var result = compilation.Evaluate(new Dictionary<VariableSymbol, EvaluatorObject>(), ref _);

        if (exceptions.Length != result.exceptions.Count) {
            writer.WriteLine($"Input: {text}");

            foreach (var exception in result.exceptions)
                writer.WriteLine($"Exception ({exception}): {exception.Message}");
        }

        Assert.Equal(exceptions.Length, result.exceptions.Count);

        for (int i=0; i<exceptions.Length; i++)
            Assert.Equal(exceptions[i].GetType(), result.exceptions[i].GetType());
    }

    private void AssertDiagnostics(string text, string diagnosticText, bool assertWarnings = false) {
        var annotatedText = AnnotatedText.Parse(text);
        var syntaxTree = SyntaxTree.Parse(annotatedText.text);

        var tempDiagnostics = new BelteDiagnosticQueue();

        if (syntaxTree.diagnostics.FilterOut(DiagnosticType.Warning).Any()) {
            tempDiagnostics.Move(syntaxTree.diagnostics);
        } else {
            var compilation = Compilation.CreateScript(null, syntaxTree);
            var _ = false;
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, EvaluatorObject>(), ref _);
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
