using Xunit;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests.CodeAnalysis.Evaluating;

/// <summary>
/// Tests on the <see cref="Buckle.CodeAnalysis.Evaluating.Evaluator" /> and
/// <see cref="Buckle.CodeAnalysis.Evaluating.Executor" /> classes.
/// </summary>
public sealed class EvaluatorTests {
    [Theory]
    // Empty expressions
    [InlineData(";", null)]
    [InlineData(";;", null)]
    // Literal expressions
    [InlineData("return 1;", 1)]
    [InlineData("return 6.6;", 6.6)]
    [InlineData("return \"test\";", "test")]
    [InlineData("return true;", true)]
    [InlineData("return false;", false)]
    [InlineData("return (10);", 10)]
    [InlineData("return 0b1;", 1)]
    [InlineData("return -0B1;", -1)]
    [InlineData("return 0b01101;", 13)]
    [InlineData("return 0b11111111;", 255)]
    [InlineData("return 0x01;", 1)]
    [InlineData("return -0x01;", -1)]
    [InlineData("return 0XDEADBEEF;", 3735928559)]
    [InlineData("return 0xfF;", 255)]
    [InlineData("return 123_123;", 123123)]
    [InlineData("return 1_1;", 11)]
    [InlineData("return 1_1.42;", 11.42)]
    [InlineData("return 1_1._42;", 11.42)]
    [InlineData("return 1_1._4_2;", 11.42)]
    [InlineData("return 6.26e34;", 6.26E+34)]
    [InlineData("return 6.26e+34;", 6.26E+34)]
    [InlineData("return 6.26E34;", 6.26E+34)]
    [InlineData("return 6.26E+34;", 6.26E+34)]
    [InlineData("return 6E-10;", 6E-10)]
    // Unary expressions
    [InlineData("return +1;", 1)]
    [InlineData("return +6;", 6)]
    [InlineData("return + +6;", 6)]
    [InlineData("return -1;", -1)]
    [InlineData("return -6;", -6)]
    [InlineData("return - -6;", 6)]
    [InlineData("return - +6;", -6)]
    [InlineData("return + -6;", -6)]
    [InlineData("return !true;", false)]
    [InlineData("return !(!true);", true)]
    [InlineData("return !(!false);", false)]
    [InlineData("return ~1;", -2)]
    [InlineData("return ~4;", -5)]
    // Binary expressions
    [InlineData("return 14 + 12;", 26)]
    [InlineData("return 4 + -7;", -3)]
    [InlineData("return \"test\" + \"test2\";", "testtest2")]
    [InlineData("return 3.2 + 3.4;", 6.6)]
    [InlineData("return 12 - 3;", 9)]
    [InlineData("return 3 - 12;", -9)]
    [InlineData("return 3.2 - 3.4;", -0.19999999999999973)]
    [InlineData("return 4 * 2;", 8)]
    [InlineData("return -6 * -4;", 24)]
    [InlineData("return 10 * 1.5;", 15F)]
    [InlineData("return 9 / 3;", 3)]
    [InlineData("return 12 / 3;", 4)]
    [InlineData("return 9 / 2;", 4)]
    [InlineData("return 9.0 / 2;", 4.5)]
    [InlineData("return 9 / 2.0;", 4.5)]
    [InlineData("return 4 ** 2;", 16)]
    [InlineData("return 2 ** 4;", 16)]
    [InlineData("return 4.1 ** 2;", 16.81)]
    [InlineData("return 4.1 ** 2.1;", 19.35735875876448)]
    [InlineData("return 1 & 3;", 1)]
    [InlineData("return 1 & 0;", 0)]
    [InlineData("return false & false;", false)]
    [InlineData("return false & true;", false)]
    [InlineData("return true & false;", false)]
    [InlineData("return true & true;", true)]
    [InlineData("return 1 | 2;", 3)]
    [InlineData("return 1 | 0;", 1)]
    [InlineData("return false | false;", false)]
    [InlineData("return false | true;", true)]
    [InlineData("return true | false;", true)]
    [InlineData("return true | true;", true)]
    [InlineData("return 1 ^ 0;", 1)]
    [InlineData("return 0 ^ 1;", 1)]
    [InlineData("return 1 ^ 1;", 0)]
    [InlineData("return 1 ^ 3;", 2)]
    [InlineData("return false ^ false;", false)]
    [InlineData("return false ^ true;", true)]
    [InlineData("return true ^ false;", true)]
    [InlineData("return true ^ true;", false)]
    [InlineData("return 1 << 1;", 2)]
    [InlineData("return 3 << 2;", 12)]
    [InlineData("return 2 >> 1;", 1)]
    [InlineData("return 3 >> 1;", 1)]
    [InlineData("return 12 >> 2;", 3)]
    [InlineData("return -8 >> 2;", -2)]
    [InlineData("return 2 >>> 1;", 1)]
    [InlineData("return 3 >>> 1;", 1)]
    [InlineData("return 12 >>> 2;", 3)]
    [InlineData("return -8 >>> 2;", 4611686018427387902)]
    [InlineData("return false && false;", false)]
    [InlineData("return false && true;", false)]
    [InlineData("return true && false;", false)]
    [InlineData("return true && true;", true)]
    [InlineData("return false || false;", false)]
    [InlineData("return false || true;", true)]
    [InlineData("return true || false;", true)]
    [InlineData("return true || true;", true)]
    [InlineData("return false == false;", true)]
    [InlineData("return true == false;", false)]
    [InlineData("return 12 == 3;", false)]
    [InlineData("return 3 == 3;", true)]
    [InlineData("return \"test\" == \"abc\";", false)]
    [InlineData("return \"test\" == \"test\";", true)]
    [InlineData("return false != false;", false)]
    [InlineData("return true != false;", true)]
    [InlineData("return \"test\" != \"test\";", false)]
    [InlineData("return \"test\" != \"abc\";", true)]
    [InlineData("return 12 != 3;", true)]
    [InlineData("return 3 != 3;", false)]
    [InlineData("return 3 < 4;", true)]
    [InlineData("return 5 < 3;", false)]
    [InlineData("return 3 > 4;", false)]
    [InlineData("return 5 > 3;", true)]
    [InlineData("return 4 <= 4;", true)]
    [InlineData("return 4 <= 5;", true)]
    [InlineData("return 5 <= 4;", false)]
    [InlineData("return 4 >= 4;", true)]
    [InlineData("return 4 >= 5;", false)]
    [InlineData("return 5 >= 4;", true)]
    [InlineData("return null is null;", true)]
    [InlineData("return 3 is null;", false)]
    [InlineData("return null == null;", true)]
    [InlineData("return 3 == null;", false)]
    [InlineData("bool? a = true; bool? b = null; return a || b;", true)]
    [InlineData("bool? a = true; bool? b = null; return a && b;", false)]
    [InlineData("bool? a = null; bool? b = null; return a || b;", false)]
    [InlineData("bool? a = null; bool? b = null; return a && b;", false)]
    [InlineData("return (null + 3) is null;", true)]
    [InlineData("return (null > 3) is null;", true)]
    [InlineData("return 3 is any;", true)]
    [InlineData("return 3 is decimal;", false)]
    [InlineData("return 3 is int;", true)]
    [InlineData("return \"string\" is char;", false)]
    [InlineData("return \"string\" is string;", true)]
    [InlineData("return null is string;", false)]
    [InlineData("return null is Object;", false)]
    [InlineData("return null isnt null;", false)]
    [InlineData("return 3 isnt null;", true)]
    [InlineData("return 5 % 2;", 1)]
    [InlineData("return 9 % 5;", 4)]
    [InlineData("return 5 ?? 2;", 5)]
    [InlineData("return 5 ?! 2;", 2)]
    [InlineData("int? a = 3; return a?;", 3)]
    [InlineData("int? a = null; return a?;", 0)]
    [InlineData("string? a = \"t\"; return a?;", "t")]
    [InlineData("string? a = null; return a?;", "")]
    [InlineData("bool? a = true; return a?;", true)]
    [InlineData("bool? a = null; return a?;", false)]
    [InlineData("return ~((uint64)1 << 7);", 18446744073709551487)]
    [InlineData("int a = 5; return a / 60;", 0)]
    [InlineData("int a = 5; return a / 60.0;", 0.08333333333333333)]
    // Compound assignments
    [InlineData("var? a = 1; a += (2 + 3); return a;", 6)]
    [InlineData("var? a = 1; a -= (2 + 3); return a;", -4)]
    [InlineData("var? a = 1; a *= (2 + 3); return a;", 5)]
    [InlineData("var? a = 1; a /= (2 + 3); return a;", 0)]
    [InlineData("var? a = 2; a **= 2; return a;", 4)]
    [InlineData("var? a = true; a &= (false); return a;", false)]
    [InlineData("var? a = 1; a &= 3; return a;", 1)]
    [InlineData("var? a = 1; a &= 0; return a;", 0)]
    [InlineData("var? a = true; a |= (false); return a;", true)]
    [InlineData("var? a = 1; a |= 0; return a;", 1)]
    [InlineData("var? a = true; a ^= (true); return a;", false)]
    [InlineData("var? a = 1; a ^= 0; return a;", 1)]
    [InlineData("var? a = 1; a <<= 1; return a;", 2)]
    [InlineData("var? a = 2; a >>= 1; return a;", 1)]
    [InlineData("var? a = 8; a >>>= 1; return a;", 4)]
    [InlineData("var? a = -8; a >>>= 1; return a;", 9223372036854775804)]
    [InlineData("var? a = 12; a >>>= 5; return a;", 0)]
    [InlineData("var? a = 5; a %= 2; return a;", 1)]
    [InlineData("var? a = 5; a ??= 2; return a;", 5)]
    [InlineData("var? a = 5; a ?!= 2; return a;", 2)]
    [InlineData("int? a = null; a ??= 2; return a;", 2)]
    [InlineData("int? a = null; a ?!= 2; return a;", null)]
    [InlineData("var? a = 1; var? b = 2; var? c = 3; a += b += c; return a;", 6)]
    [InlineData("var? a = 1; var? b = 2; var? c = 3; a += b += c; return b;", 5)]
    [InlineData("var? a = 3; return a is null;", false)]
    [InlineData("var? a = 3; return a isnt null;", true)]
    [InlineData("var? a = 3; a += null; return a;", null)]
    [InlineData("int? a = 3; a += null; return a is null;", true)]
    [InlineData("int? a = 3; a += null; return a isnt null;", false)]
    // Ternary expressions
    [InlineData("return true ? 3 : 5;", 3)]
    [InlineData("return false ? \"asdf\" : \"asdf2\";", "asdf2")]
    [InlineData("int? a = 3; int? b = a > 2 ? 5 : 3; return b;", 5)]
    [InlineData("int? a = 3; int? b = a > 2 && false ? a + 5 : a + 3; return b;", 6)]
    // Assignment expressions
    [InlineData("int? a = 10; return a;", 10)]
    [InlineData("int? a = 10; return a * a;", 100)]
    [InlineData("int? a = 1; return 10 * a;", 10)]
    [InlineData("int? a; return a;", null)]
    [InlineData("int a = default; return a;", 0)]
    [InlineData("int? a = default; return a;", null)]
    [InlineData("string a = default; return a;", "")]
    [InlineData("bool a = default; return a;", false)]
    [InlineData("char a = default; return a;", '\0')]
    [InlineData("decimal a = default; return a;", 0)]
    [InlineData("uintptr a = default; return 0;", 0)]
    [InlineData("intptr a = default; return 0;", 0)]
    [InlineData("int* a = default; return 0;", 0)]
    [InlineData("void(int)* a = default; return 0;", 0)]
    [InlineData("int[][] a; a = new int[][] { { 1 } }; return a[0][0];", 1)]
    [InlineData("uint64 a = 3; var b = 1 + a; return LowLevel.GetType(b) == typeof(uint64);", true)]
    [InlineData("uint64 a = 3; var b = 1 + a; return LowLevel.GetType(a) == LowLevel.GetType(b);", true)]
    [InlineData("return typeof(int) == typeof(int64);", true)]
    [InlineData("return typeof(int32) == typeof(int64);", false)]
    [InlineData("return typeof(decimal) == typeof(float64);", true)]
    [InlineData("return typeof(float32) == typeof(float64);", false)]
    // Name expressions
    [InlineData("int? a = 3; int? b = 6; return a;", 3)]
    [InlineData("int? a = 3; int? b = 6; return b;", 6)]
    [InlineData("int? a = 3; int? b = 6; b += a; return a;", 3)]
    [InlineData("int? a = 3; int? b = 6; b += a; return b;", 9)]
    // Postfix expressions
    [InlineData("int? a = 3; a++; return a;", 4)]
    [InlineData("int? a = 3; a--; return a;", 2)]
    [InlineData("int? a = 3; a--; a++; return a;", 3)]
    [InlineData("int? a = 1; a--; a--; return a;", -1)]
    [InlineData("int? a = 4; int? b = a++; return b;", 4)]
    [InlineData("int? a = 4; int? b = a--; return b;", 4)]
    [InlineData("int? a = 4; return a!;", 4)]
    [InlineData("int? a = 4; return a! + 1;", 5)]
    [InlineData("decimal? a = 3.6; a++; return a;", 4.6)]
    [InlineData("decimal? a = 3.6; a--; return a;", 2.6)]
    // Prefix expressions
    [InlineData("int? a = 3; ++a; return a;", 4)]
    [InlineData("int? a = 3; --a; return a;", 2)]
    [InlineData("int? a = 3; --a; ++a; return a;", 3)]
    [InlineData("int? a = 1; --a; --a; return a;", -1)]
    [InlineData("int? a = 4; int? b = ++a; return b;", 5)]
    [InlineData("int? a = 4; int? b = --a; return b;", 3)]
    [InlineData("decimal? a = 3.6; ++a; return a;", 4.6)]
    [InlineData("decimal? a = 3.6; --a; return a;", 2.6)]
    // Parenthesized expressions
    [InlineData("int? a = (3 + 4) * 2; return a;", 14)]
    [InlineData("int? a = 3 + (4 * 2); return a;", 11)]
    [InlineData("int? a = 12 / (4 * 2); return a;", 1)]
    [InlineData("int? a = (12 / 4) * 2; return a;", 6)]
    // Call expressions
    [InlineData("int? F(int? a, int? b, int? c) { return a + b * c; } return F(b: 3, c: 2, a: 9);", 15)]
    [InlineData("int? F(int? a = 3) { return a; } return F(5);", 5)]
    [InlineData("int? F(int? a = 3) { return a; } return F();", 3)]
    [InlineData("int? F(int? a, int? b, int? c = 6) { return a + b * c; } return F(b: 3, c: 2, a: 9);", 15)]
    [InlineData("int? F(int? a, int? b, int? c = 6) { return a + b * c; } return F(b: 3, a: 9);", 27)]
    [InlineData("int? F(int? a, int? b) { if (a is null) return 1; if (b is null) return 2; return 3;} return F(1, 2);", 3)]
    [InlineData("int? F(int? a, int? b) { if (a is null) return 1; if (b is null) return 2; return 3;} return F(1,);", 2)]
    [InlineData("int? F(int? a, int? b) { if (a is null) return 1; if (b is null) return 2; return 3;} return F(,2);", 1)]
    [InlineData("class A { public int? a; public int? M() { if (a is null) a = 3; return a++; } } var myA = new A(); return myA.M();", 3)]
    [InlineData("class A { public int? a; public int? M() { if (a is null) a = 3; return a++; } } var myA = new A(); myA.M(); return myA.M();", 4)]
    [InlineData("int? F(int? a, int? b) { return a + b; } int?(int?, int?) a = F; return a(3, 4);", 7)]
    [InlineData("int? F(int? a) { return a + 3; } int?(int?) a = F; return a(3);", 6)]
    [InlineData("int? F() { return 3; } int?() a = F; return a();", 3)]
    [InlineData("void F(int? a, int? b) { } void(int?, int?) a = F; a(3, 4); return null;", null)]
    [InlineData("void F(int? a) { } void(int?) a = F; a(3); return null;", null)]
    [InlineData("void F() { } void() a = F; a(); return null;", null)]
    // Cast expressions
    [InlineData("return (decimal?)3;", 3)]
    [InlineData("return (int?)3.4;", 3)]
    [InlineData("return (int?)3.6;", 3)]
    [InlineData("return (int!)3;", 3)]
    [InlineData("return 10 * (int?)1.5;", 10)]
    [InlineData("string? a = (string?)(int?)3.6; return a;", "3")]
    [InlineData("return (string?)null;", null)]
    [InlineData("return (int?)null;", null)]
    [InlineData("int! a = 3; decimal! b = 3.5; return (int!)b == a;", true)]
    [InlineData("int! a = 3; string? b = \"test\" + (string?)a; return b;", "test3")]
    [InlineData("lowlevel { any a = new int?[] {1, 2, 3}; return ((int?[])a)[1]; }", 2)]
    [InlineData("lowlevel { any a = new bool?[] {true, false}; return ((bool?[])a)[0]; }", true)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[0]; }", 1)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[1]; }", 3.5)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[2]; }", true)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[3]; }", "test")]
    // Reference expressions
    [InlineData("int? x = 4; ref int? y = ref x; x++; return y;", 5)]
    [InlineData("int? x = 4; ref int? y = ref x; y++; return x;", 5)]
    [InlineData("int? x = 4; int? y = 3; ref int? z = ref x; z = ref y; z++; return x;", 4)]
    [InlineData("lowlevel { int?[] a = {1, 2, 3}; a[0] = 6; return a[0]; }", 6)]
    [InlineData("int? M() { ref int? F(ref int? a) { return ref a; } int? b = 3; F(ref b) = 6; return b; } return M();", 6)]
    // Cascade list expression
    [InlineData("class A { public int? f = 0; } var a = new A()..f=1.0..f=5; return a.f;", 5)]
    [InlineData("class A { public int? f = 0; public void M() { f++; } } var a = new A()..M()..M(); return a.f;", 2)]
    // Initializer list expressions and index expressions
    [InlineData("lowlevel { decimal?[] a = {3.1, 2.56, 5.23123}; return a[2]; }", 5.23123)]
    [InlineData("lowlevel { decimal?[] a = {3.1, 2.56, 5.23123}; return a[0]; }", 3.1)]
    [InlineData("lowlevel { string?[] a = {\"hello\", \"world\"}; return a[1]; }", "world")]
    [InlineData("lowlevel { bool?[] a = {true, true, false, false}; return a[3]; }", false)]
    [InlineData("lowlevel { bool?[] a = {true, true, false, false}; return a?[3]; }", false)]
    [InlineData("lowlevel { bool?[] a; return a?[3]; }", null)]
    [InlineData("lowlevel { bool?[] a = {true, false}; a = null; return a?[3]; }", null)]
    [InlineData("lowlevel { bool?[] a = null; return a?[3]; }", null)]
    [InlineData("lowlevel { int?[] a = {1, 2, null}; return a[0]; }", 1)]
    [InlineData("lowlevel { int?[] a = {1, 2, null}; return a[2]; }", null)]
    [InlineData("lowlevel { int?[][] a = { new int?[] { 1 } }; return a[0][0]; }", 1)]
    [InlineData("lowlevel { var a = new int?[] { 1, 2, 3 }; a = { 4, 5, 6 }; return a[0]; }", 4)]
    [InlineData(@"
        class A {
            public decimal? f = 1;
        }
        var c = new A[2];
        c[0] = new A();
        int? i = 0;
        var f = c[i].f;
        return f;
        ", 1)]
    // Member access expressions
    [InlineData("class A { public int? num; } A myVar = new A(); myVar.num = 3; return myVar.num + 1;", 4)]
    [InlineData("class A { public int? num; } class B { public A a; } B myVar = new B(); myVar.a = new A(); myVar.a.num = 3; return myVar.a.num + 1;", 4)]
    [InlineData("class A { public int? a; public int? b; } A myVar = new A(); myVar.a = 3; myVar.b = myVar.a + 3; return myVar.b;", 6)]
    [InlineData("class A { public int? a; public int? b; } A myVar = new A(); myVar.a = 3; myVar.b = myVar.a + 3; return myVar.a;", 3)]
    [InlineData("class A { public int? num; } A myVar; int? a = myVar?.num; return a;", null)]
    [InlineData("class A { public int? num; } A myVar = new A(); myVar.num = 7; int? a = myVar?.num; return a;", 7)]
    [InlineData("class A { public static int? a = 3; } return A.a;", 3)]
    [InlineData("class A { public static int? a = 3; static constructor() { a = 10; } } return A.a;", 10)]
    [InlineData("class A { public static int? a = 3; } A.a = 20; return A.a;", 20)]
    [InlineData("struct A { public int a; } var a = new A(); return a.a;", 0)]
    [InlineData("struct A { public int? a; } var a = new A(); return a.a;", null)]
    [InlineData("struct A { public int a; } A? a; a = new A(); return a.a;", 0)]
    [InlineData("struct A { public int a; } A? a; return a?.a;", null)]
    [InlineData("class A { public int a; } A? a; return a?.a;", null)]
    // This expression
    [InlineData("class A { public int? a; public void SetA(int? a) { this.a = 1; this.a = a; } public int? GetA() { return a; } } var myA = new A(); myA.SetA(3); return myA.GetA();", 3)]
    [InlineData("class A { public int? a; public void SetA(int? a) { this.a = 1; a = a; } public int? GetA() { return a; } } var myA = new A(); myA.SetA(3); return myA.GetA();", 1)]
    [InlineData("class A { public int? M() { return 1; } public int? N() { int? M() { return 2; } return M(); } } var myVar = new A(); return myVar.N();", 2)]
    [InlineData("class A { public int? M() { return 1; } public int? N() { int? M() { return 2; } return this.M(); } } var myVar = new A(); return myVar.N();", 1)]
    // Static member access
    [InlineData("class A { public constexpr int? a = 3; } return A.a;", 3)]
    [InlineData("class A { public constexpr int? a; } return A.a;", null)]
    [InlineData("class A { public static int? B() { return 0; } } return A.B();", 0)]
    [InlineData("class A { public static int? B(int a) { return a + 3; } } return A.B(4);", 7)]
    // Structs
    [InlineData("struct A { public int! a; } var a = new A(); a.a = 4; var b = a; b.a = 10; return a.a;", 4)]
    // If statements
    [InlineData("int? a = 0; if (a == 0) { a = 10; } return a;", 10)]
    [InlineData("int? a = 0; if (a == 4) { a = 10; } return a;", 0)]
    [InlineData("int? a = 0; if (a == 0) { a = 10; } else { a = 5; } return a;", 10)]
    [InlineData("int? a = 0; if (a == 4) { a = 10; } else { a = 5; } return a;", 5)]
    // Null-Binding statements
    [InlineData("int? a = 10; int! b = 0; if (a -> x!) { b = x; } return b;", 10)]
    [InlineData("int? a = null; int! b = 0; if (a -> x!) { b = x; } return b;", 0)]
    // Local function statements
    [InlineData("int? A() { int? B() { return 2; } return B() + 1; } return A();", 3)]
    [InlineData("int? A() { int? B() { int? A() { return 2; } return A() + 1; } return B() + 1; } return A();", 4)]
    [InlineData("int? A() { int? a = 1; int? B(int? b) { return a + b; } return B(4); } return A();", 5)]
    [InlineData("int? A() { int? a = 5; int? B(int? b) { return a + b; } return B(1); } return A();", 6)]
    [InlineData("int? A() { int? a = 5; void B() { a = 6; } B(); return a; } return A();", 6)]
    // Block statements and return statements
    [InlineData("{ int? a = 3; return a; }", 3)]
    [InlineData("int? a = 5; { a = 3; return a; }", 3)]
    [InlineData("int? a = 5; { a = 3; } return a;", 3)]
    [InlineData("int? a = 5; { int? b = 3 + a; return b; } return a;", 8)]
    // Constructors
    [InlineData("class A { public constructor() { } }", null)]
    [InlineData("class A { public int? a; public constructor(int? b) { a = b; } } var myVar = new A(6); return myVar.a;", 6)]
    [InlineData("class A { public int? a; public constructor(int? b) { a = b; } public constructor(int? b, int? c) { a = b + c; } } var myVar = new A(6); return myVar.a;", 6)]
    [InlineData("class A { public int? a; public constructor(int? b) { a = b; } public constructor(int? b, int? c) { a = b + c; } } var myVar = new A(6, 1); return myVar.a;", 7)]
    // For statements
    [InlineData("int? result = 1; for (int? i = 0; i <= 10; i++) { result += result; } return result;", 2048)]
    [InlineData("int? result = 0; for (int? i = 0; i < 5; i++) { result++; } return result;", 5)]
    [InlineData("int? result; for (int? i = 0; i <= 10; i++) { result = i; } return result;", 10)]
    [InlineData("int? result = 1; for (int? i = 10; i > 0; i--) { result += i; } return result;", 56)]
    // While statements
    [InlineData("int? i = 0; int? result = 1; while (i <= 10) { result += result; i++; } return result;", 2048)]
    [InlineData("int? i = 0; int? result = 0; while (i < 5) { result++; i++; } return result;", 5)]
    [InlineData("int? i = 0; int? result; while (i <= 10) { result = i; i++; } return result;", 10)]
    [InlineData("int? i = 10; int? result = 1; while (i > 0) { result += i; i--; } return result;", 56)]
    // Do-While statements
    [InlineData("int? result = 0; do { result++; } while (result < 10); return result;", 10)]
    [InlineData("int? result = 0; do { result++; } while (false); return result;", 1)]
    [InlineData("int? result = 0; do { result++; } while (result < 0); return result;", 1)]
    [InlineData("int? result = 10; do { result*=2; } while (result < 30); return result;", 40)]
    // Break statements
    [InlineData("int? result = 3; for (int? i = 0; i < 10; i++) { result++; if (result == 5) break; } return result;", 5)]
    [InlineData("int? result = 3; for (int? i = 0; i < 10; i++) { result++; if (result < 5) break; } return result;", 4)]
    [InlineData("int? result = 3; while (true) { result++; if (result == 5) break; } return result;", 5)]
    [InlineData("int? result = 3; while (true) { result++; if (result > 5) break; } return result;", 6)]
    // Continue statements
    [InlineData("var? cond = false; int? res = 3; while (true) { if (cond) continue; else break; res = 4; } return res;", 3)]
    [InlineData("var? cond = false; int? res = 3; while (true) { if (cond) continue; res = 4; if (res == 4) break; } return res;", 4)]
    [InlineData("var? cond = true; int? res = 3; while (true) { if (cond) ; else continue; res = 4; if (res == 4) break; } return res;", 4)]
    [InlineData("var? cond = true; int? res = 3; while (true) { if (cond) break; else continue; res = 4; } return res;", 3)]
    // Libraries
    [InlineData("class A { } var a = new A(); return a.ToString();", "A")]
    [InlineData("class A { public override string? ToString() { return \"a\"; } } var a = new A(); return a.ToString();", "a")]
    [InlineData("any[] a = {1, 2, 3}; return LowLevel.Length<any[]>(a);", 3)]
    // TypeOf expressions
    [InlineData("lowlevel { type a = typeof(int[]); }", null)]
    [InlineData("type? a = typeof(string);", null)]
    [InlineData("type? a = typeof(decimal!);", null)]
    [InlineData("class A { public int? num; } type? a = typeof(A);", null)]
    [InlineData("return typeof(int) == typeof(int);", true)]
    [InlineData("return typeof(int) == typeof(int!);", true)]
    [InlineData("return typeof(int?) == typeof(int?);", true)]
    [InlineData("return typeof(int?) == typeof(int);", false)]
    [InlineData("return typeof(int) == typeof(bool);", false)]
    [InlineData("return typeof(int*) == typeof(int64*);", true)]
    [InlineData("class C<type T> { public bool? M() { return typeof(T) == typeof(int?); } } var c = new C<int?>(); return c.M();", true)]
    [InlineData("class C<type T> where { T is notnull; } { public bool? M() { return typeof(T) == typeof(int?); } } var c = new C<int?>(); return c.M();", false)]
    [InlineData("class C<type T> { public bool? M() { return typeof(T) == typeof(int?); } } var c = new C<bool?>(); return c.M();", false)]
    [InlineData("bool? C<type T>() { return typeof(T) == typeof(int?); } return C<int?>();", true)]
    [InlineData("bool? C<type T>() { return typeof(T) == typeof(int?); } return C<bool?>();", false)]
    // Operators
    [InlineData(@"
        class A {
            public int? a;
            public constructor(int? a) { this.a = a; }
            public static int? operator+(A a) { return a.a; }
            public static int? operator+(A a, int? b) { return a.a + b; }
        }

        var a = new A(3);
        return a + 5;", 8)]
    [InlineData(@"
        class A {
            public int?[] a = { 1, 2, 3 };
            public static ref int? operator[](A a, int? b) {
                return ref a.a[b];
            }
        }

        var a = new A();
        a[1]++;
        return a[1] + a[0];", 4)]
    [InlineData(@"
        class A {
            public int? a;
            public static implicit operator A(int? b) {
                var c = new A();
                c.a = b;
                return c;
            }
        }

        A a = 3;
        return a.a;", 3)]
    // Overrides
    [InlineData(@"
        class A {
            public virtual string? M() { return ""A""; }
            public string? T() { return M(); }
        }

        class B extends A {
            public override string? M() { return ""B""; }
        }

        var b = new B();
        return b.T();", "B")]
    [InlineData("lowlevel class A { public int?[] b = { 1, 2, 3 }; } var a = new A(); ref var r = ref a.b; r[0]++; return a.b[0];", 2)]
    // Try statements
    [InlineData("try { int? x = 0; int? a = 56/x; return a; } catch { return 3; }", 3)]
    [InlineData("try { int? a = 56/1; return a; } catch { return 3; }", 56)]
    [InlineData("int? a = 3; try { int? x = 0; int? b = 56/x; a += b; return a; } catch { a += 3; return a; } finally { a++; }", 6)]
    [InlineData("int? a = 3; try { int? b = 56/1; a += b; return a; } catch { a += 3; return a; } finally { a++; }", 59)]
    // Switch statements
    [InlineData("var? a = 3; int? b = 1; switch (a) { case 3: b = 5; } return b;", 5)]
    [InlineData("var? a = 3; int? b = 1; switch (a) { case 4: b = 5; } return b;", 1)]
    [InlineData("var? a = 3; int? b = 1; switch (a) { case 3: goto case 5; case 5: goto default; default: b = 6; } return b;", 6)]
    [InlineData("var? a = 3; int? b = 1; switch (a) { case 1: case 2: case 3: case 4: b = 6; } return b;", 6)]
    // String interpolation
    [InlineData("var? a = 3; return f\"a is {a}\";", "a is 3")]
    [InlineData("int? a = null; return f\"a is {a}\";", "a is ")]
    [InlineData("return f\"a is {null}\";", "a is ")]
    [InlineData("List<int> a = null; return f\"a is {a}\";", "a is ")]
    [InlineData("class A { public override string ToString() { return \"text\"; } } A a = new A(); return f\"a is {a}\";", "a is text")]
    // Templates
    // TODO Is it worth testing non-type templates even though only the Evaluator supports them?
    // [InlineData("class A<int a, int b> { public static int Test() { return a + b; } } return A<2,3>.Test();", 5)]
    // [InlineData("int Test<int a, int b>() { return a + b; } return Test<2, 3>();", 5)]
    // [InlineData("string Test<string a>() { return a; } return Test<\"test\">();", "test")]
    // [InlineData("lowlevel int[] Test<int[] a>() { return a; } lowlevel { return Test<{1, 2, 3}>()[1]; }", 2)]
    // [InlineData("lowlevel { int[] Test<int[] a>() { return a; } return Test<{1, 2, 3}>()[1]; }", 2)]
    [InlineData("class A<type t> { public t a; } var a = new A<string>(); a.a = \"test\"; return a.a;", "test")]
    [InlineData("class A<type t> { public t a; } lowlevel { var a = new A<int?[]>(); a.a = new int?[] {1, 2, 3}; return a.a[1]; }", 2)]
    [InlineData("class A<type t> { }; var a = new A<A<int?>>();", null)]
    [InlineData("T Test<type T>(T a) { return a; } return Test<int?>(3);", 3)]
    [InlineData("T Test<type T>() { return default; } return Test<int?>();", null)]
    [InlineData("T Test<type T>() { return default; } return Test<int>();", 0)]
    // Misc for coverage
    [InlineData("using H = int?; H myVar = 3; return myVar;", 3)]
    [InlineData("enum A { q, w, e, r, t } return A.t;", 4)]
    [InlineData("enum flags A { q, w, e, r, t } return A.t;", 8)]
    [InlineData("enum A { q, w, e, r, t } A a = .t; return (int)a;", 4)]
    [InlineData("class P { int? a = 3; public int? M(int? a) { return a; } } var myP = new P(); return myP.M(4);", 4)]
    [InlineData("class P { public int? M(int? a, int? b) { return a + b; } public int? M(int? a) { return a; } } var myP = new P(); return myP.M(4, 5);", 9)]
    [InlineData("class P { public static T M<type T>() { T a = default; return a; } } return P.M<int?>();", null)]
    // TODO these crash
    // [InlineData("class P { public static T M<type T>(T b) { T a = b; L(); return a; void L() { a = null; } } } return P.M<int>(3);", null)]
    // [InlineData("class P { public static T M<type T>(T b) { T a = b; L<bool>(); return a; void L<type T2>() { a = null; } } } return P.M<int>();", null)]
    [InlineData("static class P { [DllImport(\"kernel32.dll\")]static extern int64* GetModuleHandle(string? lpModuleName); } return null;", null)]
    [InlineData("static class P { [DllImport(\"msvcrt.dll\", CallingConvention: CallingConvention.Cdecl)]static extern void* memcpy(void* dest, void* src, uint64 count); } return null;", null)]
    [InlineData(@"
    struct A { B b; }
    struct B { int a; }

    var c = new A();
    c.b.a = 4;
    return c.b.a;
    ", 4)]
    // Compound nullability operators
    [InlineData(@"
    class A {
        public int! Length() { return 4; }
    }

    A a = new A();
    int! b = a?.Length()?;
    return b;
    ", 4)]
    [InlineData(@"
    class A {
        public int! Length() { return 4; }
    }

    A a = null;
    int! b = a?.Length()?;
    return b;
    ", 0)]
    [InlineData(@"
    class A {
        public int? a;
    }

    A a = new A();
    a?.a = 3;
    return a?.a;
    ", 3)]
    [InlineData(@"
    class A {
        public int? a;
        public A b;
    }

    A a = new A();
    a?.b?.a = 3;
    return a?.b?.a;
    ", null)]
    [InlineData(@"
    class A {
        public int? a;
        public void M() { a = 5; }
    }

    A a = new A()?..M();
    return a.a;
    ", 5)]
    [InlineData(@"
    class A {
        public int? a;
        public void M() { a = 5; }
    }

    A a = null;
    var b = a?..M();
    return b?.a;
    ", null)]
    [InlineData(@"
    class A {
        public int? a;
        public A b;
    }

    var a = new A()?..b = (new A()..a = 4);
    return a.b.a;
    ", 4)]
    [InlineData(@"
    int?[][] a = null;
    a?[0]?[0] = 5;
    return a?[0]?[0];
    ", null)]
    [InlineData(@"
    int?[][] a = { { 1 } };
    a?[0]?[0] = 5;
    return a?[0]?[0];
    ", 5)]
    public void Evaluator_Computes_CorrectValues(string text, object? expectedValue) {
        AssertValue(text, expectedValue, evaluator: true, executor: true);
    }
}
