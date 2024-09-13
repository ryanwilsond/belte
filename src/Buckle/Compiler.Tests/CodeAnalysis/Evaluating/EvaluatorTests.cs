using Xunit;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests.CodeAnalysis.Evaluating;

/// <summary>
/// Tests on the <see cref="Buckle.CodeAnalysis.Evaluating.Evaluator" /> class.
/// </summary>
public sealed class EvaluatorTests {
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
    [InlineData("3 is any;", true)]
    [InlineData("3 is decimal;", false)]
    [InlineData("3 is int;", true)]
    [InlineData("\"string\" is char;", false)]
    [InlineData("\"string\" is string;", true)]
    [InlineData("null is string;", false)]
    [InlineData("null is Object;", false)]
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
    [InlineData("var a = 1; var b = 2; var c = 3; a += b += c; return a;", 6)]
    [InlineData("var a = 1; var b = 2; var c = 3; a += b += c; return b;", 5)]
    [InlineData("var a = 3; return a is null;", false)]
    [InlineData("var a = 3; return a isnt null;", true)]
    [InlineData("var a = 3; a += null; return a;", null)]
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
    [InlineData("int result = 1; for (int i = 0; i <= 10; i++) { result += result; } return result;", 2048)]
    [InlineData("int result = 0; for (int i = 0; i < 5; i++) { result++; } return result;", 5)]
    [InlineData("int result; for (int i = 0; i <= 10; i++) { result = i; } return result;", 10)]
    [InlineData("int result = 1; for (int i = 10; i > 0; i--) { result += i; } return result;", 56)]
    // While statements
    [InlineData("int i = 0; int result = 1; while (i <= 10) { result += result; i++; } return result;", 2048)]
    [InlineData("int i = 0; int result = 0; while (i < 5) { result++; i++; } return result;", 5)]
    [InlineData("int i = 0; int result; while (i <= 10) { result = i; i++; } return result;", 10)]
    [InlineData("int i = 10; int result = 1; while (i > 0) { result += i; i--; } return result;", 56)]
    // Do-While statements
    [InlineData("int result = 0; do { result++; } while (result < 10); return result;", 10)]
    [InlineData("int result = 0; do { result++; } while (false); return result;", 1)]
    [InlineData("int result = 0; do { result++; } while (result < 0); return result;", 1)]
    [InlineData("int result = 10; do { result*=2; } while (result < 30); return result;", 40)]
    // Attributes
    // TODO Cannot test invalid attributes until any attributes exist
    // Initializer list expressions and index expressions
    [InlineData("lowlevel { decimal[] a = {3.1, 2.56, 5.23123}; return a[2]; }", 5.23123)]
    [InlineData("lowlevel { var a = {3.1, 2.56, 5.23123}; return a[0]; }", 3.1)]
    [InlineData("lowlevel { string[] a = {\"hello\", \"world\"}; return a[1]; }", "world")]
    [InlineData("lowlevel { bool[] a = {true, true, false, false}; return a[3]; }", false)]
    [InlineData("lowlevel { bool[] a = {true, true, false, false}; return a?[3]; }", false)]
    [InlineData("lowlevel { bool[] a; return a?[3]; }", null)]
    [InlineData("lowlevel { bool[] a = {true, false}; a = null; return a?[3]; }", null)]
    [InlineData("lowlevel { bool[] a = null; return a?[3]; }", null)]
    [InlineData("lowlevel { int[] a = {1, 2, null}; return a[0]; }", 1)]
    [InlineData("lowlevel { int[] a = {1, 2, null}; return a[2]; }", null)]
    // Reference expressions
    [InlineData("int x = 4; ref int y = ref x; x++; return y;", 5)]
    [InlineData("int x = 4; ref int y = ref x; y++; return x;", 5)]
    [InlineData("int x = 4; int y = 3; ref int z = ref x; z = ref y; z++; return x;", 4)]
    [InlineData("lowlevel { var a = {1, 2, 3}; a[0] = 6; return a[0]; }", 6)]
    // TODO Add this test back after adding containingAssembly checks to CannotUseGlobalInClass
    // [InlineData("int a = 3; class A { public ref int b = ref a; } var m = new A(); a = 6; return m.b;", 6)]
    [InlineData("lowlevel class A { public int[] b = { 1, 2, 3 }; } var a = new A(); var r = ref a.b; r[0]++; return a.b[0];", 2)]
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
    [InlineData("int! a = 4; return a! + 1;", 5)]
    [InlineData("decimal a = 3.6; a++; return a;", 4.6)]
    [InlineData("decimal a = 3.6; a--; return a;", 2.6)]
    // Prefix expressions
    [InlineData("int a = 3; ++a; return a;", 4)]
    [InlineData("int a = 3; --a; return a;", 2)]
    [InlineData("int a = 3; --a; ++a; return a;", 3)]
    [InlineData("int a = 1; --a; --a; return a;", -1)]
    [InlineData("int a = 4; int b = ++a; return b;", 5)]
    [InlineData("int a = 4; int b = --a; return b;", 3)]
    [InlineData("decimal a = 3.6; ++a; return a;", 4.6)]
    [InlineData("decimal a = 3.6; --a; return a;", 2.6)]
    // Parenthesized expressions
    [InlineData("int a = (3 + 4) * 2; return a;", 14)]
    [InlineData("int a = 3 + (4 * 2); return a;", 11)]
    [InlineData("int a = 12 / (4 * 2); return a;", 1)]
    [InlineData("int a = (12 / 4) * 2; return a;", 6)]
    // Call expressions
    [InlineData("int F(int a, int b, int c) { return a + b * c; } return F(b: 3, c: 2, a: 9);", 15)]
    [InlineData("int F(int a = 3) { return a; } return F(5);", 5)]
    [InlineData("int F(int a = 3) { return a; } return F();", 3)]
    [InlineData("int F(int a, int b, int c = 6) { return a + b * c; } return F(b: 3, c: 2, a: 9);", 15)]
    [InlineData("int F(int a, int b, int c = 6) { return a + b * c; } return F(b: 3, a: 9);", 27)]
    [InlineData("int F(int a, int b) { if (a is null) return 1; if (b is null) return 2; return 3;} return F(1, 2);", 3)]
    [InlineData("int F(int a, int b) { if (a is null) return 1; if (b is null) return 2; return 3;} return F(1,);", 2)]
    [InlineData("int F(int a, int b) { if (a is null) return 1; if (b is null) return 2; return 3;} return F(,2);", 1)]
    [InlineData("class A { public int a; public int M() { if (a is null) a = 3; return a++; } } var myA = new A(); return myA.M();", 3)]
    [InlineData("class A { public int a; public int M() { if (a is null) a = 3; return a++; } } var myA = new A(); myA.M(); return myA.M();", 4)]
    [InlineData("int A(int a) { return 1; } int A(int a, int b = 3) { return 2; } return A(9);", 1)]
    // Builtin Methods
    [InlineData("Console.Print(message: \"test\");", null)]
    [InlineData("Hex(13);", "D")]
    [InlineData("Hex(13, false);", "D")]
    [InlineData("Hex(1324, true);", "0x52C")]
    [InlineData("Ascii(\"c\");", 99)]
    [InlineData("Ascii(\"┼\");", 9532)]
    // Cast expressions
    [InlineData("(decimal)3;", 3)]
    [InlineData("(int)3.4;", 3)]
    [InlineData("(int)3.6;", 3)]
    [InlineData("(int!)3;", 3)]
    [InlineData("string a = (string)(int)3.6; return a;", "3")]
    [InlineData("(string)null;", null)]
    [InlineData("(int)null;", null)]
    [InlineData("lowlevel { any a = {1, 2, 3}; return ((int[])a)[1]; }", 2)]
    [InlineData("lowlevel { any a = {true, false}; return ((bool[])a)[0]; }", true)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[0]; }", 1)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[1]; }", 3.5)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[2]; }", true)]
    [InlineData("lowlevel { any[] a = {1, 3.5, true, \"test\"}; return a[3]; }", "test")]
    // Block statements and return expressions
    [InlineData("{ int a = 3; return a; }", 3)]
    [InlineData("int a = 5; { a = 3; return a; }", 3)]
    [InlineData("int a = 5; { a = 3; } return a;", 3)]
    [InlineData("int a = 5; { int b = 3 + a; return b; } return a;", 8)]
    // Local function statements
    [InlineData("int A() { int B() { return 2; } return B() + 1; } return A();", 3)]
    [InlineData("int A() { int B() { int A() { return 2; } return A() + 1; } return B() + 1; } return A();", 4)]
    [InlineData("int A() { int a = 1; int B(int b) { return a + b; } return B(4); } return A(); ", 5)]
    [InlineData("int A() { int a = 5; int B(int b) { return a + b; } return B(1); } return A(); ", 6)]
    [InlineData("int A() { int a = 5; void B() { a = 6; } B(); return a; } return A();", 6)]
    // Member access expressions
    [InlineData("class A { public int num; } A myVar = new A(); myVar.num = 3; return myVar.num + 1;", 4)]
    [InlineData("class A { public int num; } class B { public A a; } B myVar = new B(); myVar.a = new A(); myVar.a.num = 3; return myVar.a.num + 1;", 4)]
    [InlineData("class A { public int a; public int b; } A myVar = new A(); myVar.a = 3; myVar.b = myVar.a + 3; return myVar.b;", 6)]
    [InlineData("class A { public int a; public int b; } A myVar = new A(); myVar.a = 3; myVar.b = myVar.a + 3; return myVar.a;", 3)]
    [InlineData("class A { public int num; } A myVar; int a = myVar?.num; return a;", null)]
    [InlineData("class A { public int num; } A myVar = new A(); myVar.num = 7; int a = myVar?.num; return a;", 7)]
    // TypeOf expressions
    [InlineData("lowlevel { type a = typeof(int[]); }", null)]
    [InlineData("type a = typeof(string);", null)]
    [InlineData("type a = typeof(decimal!);", null)]
    [InlineData("class A { public int num; } type a = typeof(A);", null)]
    // Try statements
    [InlineData("try { int x = 0; int a = 56/x; return a; } catch { return 3; }", 3)]
    [InlineData("try { int a = 56/1; return a; } catch { return 3; }", 56)]
    [InlineData("int a = 3; try { int x = 0; int b = 56/x; a += b; } catch { a += 3; } finally { return a; }", 6)]
    [InlineData("int a = 3; try { int b = 56/1; a += b; } catch { a += 3; } finally { return a; }", 59)]
    // Break statements
    [InlineData("int result = 3; for (int i = 0; i < 10; i++) { result++; if (result == 5) break; } return result;", 5)]
    [InlineData("int result = 3; for (int i = 0; i < 10; i++) { result++; if (result < 5) break; } return result;", 4)]
    [InlineData("int result = 3; while (true) { result++; if (result == 5) break; } return result;", 5)]
    [InlineData("int result = 3; while (true) { result++; if (result > 5) break; } return result;", 6)]
    // Continue statements
    [InlineData("var cond = false; int res = 3; while (true) { if (cond) continue; else break; res = 4; } return res;", 3)]
    [InlineData("var cond = false; int res = 3; while (true) { if (cond) continue; res = 4; if (res == 4) break; } return res;", 4)]
    [InlineData("var cond = true; int res = 3; while (true) { if (cond) ; else continue; res = 4; if (res == 4) break; } return res;", 4)]
    [InlineData("var cond = true; int res = 3; while (true) { if (cond) break; else continue; res = 4; } return res;", 3)]
    // Constructors
    [InlineData("class A { public constructor() { } }", null)]
    [InlineData("class A { public int a; public constructor(int b) { a = b; } } var myVar = new A(6); return myVar.a;", 6)]
    [InlineData("class A { public int a; public constructor(int b) { a = b; } public constructor(int b, int c) { a = b + c; } } var myVar = new A(6); return myVar.a;", 6)]
    [InlineData("class A { public int a; public constructor(int b) { a = b; } public constructor(int b, int c) { a = b + c; } } var myVar = new A(6, 1); return myVar.a;", 7)]
    // This expression
    [InlineData("class A { public int a; public void SetA(int a) { this.a = 1; this.a = a; } public int GetA() { return a; } } var myA = new A(); myA.SetA(3); return myA.GetA();", 3)]
    [InlineData("class A { public int a; public void SetA(int a) { this.a = 1; a = a; } public int GetA() { return a; } } var myA = new A(); myA.SetA(3); return myA.GetA();", 1)]
    [InlineData("class A { public int M() { return 1; } public int N() { int M() { return 2; } return M(); } } var myVar = new A(); return myVar.N();", 2)]
    [InlineData("class A { public int M() { return 1; } public int N() { int M() { return 2; } return this.M(); } } var myVar = new A(); return myVar.N();", 1)]
    // Static member access
    [InlineData("class A { public constexpr int a = 3; } return A.a;", 3)]
    [InlineData("class A { public constexpr int a; } return A.a;", null)]
    [InlineData("class A { public static int B() { return 0; } } return A.B();", 0)]
    [InlineData("class A { public static int B(int a) { return a + 3; } } return A.B(4);", 7)]
    // Templates
    [InlineData("class A<int a, int b> { public static int Test() { return a + b; } } return A<2,3>.Test();", 5)]
    [InlineData("class A<type t> { public t a; } var a = new A<string>(); a.a = \"test\"; return a.a;", "test")]
    [InlineData("class A<type t> { public t a; } lowlevel { var a = new A<int[]>(); a.a = {1, 2, 3}; return a.a[1]; }", 2)]
    [InlineData("class A<type t> { }; var a = new A<A<int>>();", null)]
    [InlineData("int Test<int a, int b>() { return a + b; } return Test<2, 3>();", 5)]
    [InlineData("string Test<string a>() { return a; } return Test<\"test\">();", "test")]
    [InlineData("lowlevel int[] Test<int[] a>() { return a; } lowlevel { return Test<{1, 2, 3}>()[1]; }", 2)]
    // TODO
    // [InlineData("lowlevel { int[] Test<int[] a>() { return a; } return Test<{1, 2, 3}>()[1]; }", 2)]
    // Operators
    [InlineData(@"
        class A {
            public int a;

            public constructor(int a) {
                this.a = a;
            }

            public static int operator+(A a) {
                return a.a;
            }

            public static int operator+(A a, int b) {
                return a.a + b;
            }
        }

        var a = new A(3);
        return a + 5;", 8)]
    [InlineData(@"
        class A {
            public virtual string M() {
                return ""A"";
            }

            public string T() {
                return M();
            }
        }

        class B extends A {
            public override string M() {
                return ""B"";
            }
        }

        var b = new B();
        return b.T();", "B")]
    public void Evaluator_Computes_CorrectValues(string text, object expectedValue) {
        AssertValue(text, expectedValue);
    }
}
