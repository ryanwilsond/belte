using Buckle.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests;

/// <summary>
/// Tests that were added in result to a bug.
/// </summary>
public sealed class IssueTests {
    private readonly ITestOutputHelper _writer;

    public IssueTests(ITestOutputHelper writer) {
        _writer = writer;
    }

    [Fact]
    public void NullCoalescing_NotDefinedForNullOperand() {
        var text = @"
            [null ?? 2];
        ";

        var diagnostics = @"
            binary operator '??' is not defined for operands of types '<null>' and 'int!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ConditionalOr_NotDefinedForNullOperand() {
        var text = @"
            [null || true];
        ";

        var diagnostics = @"
            binary operator '||' is not defined for operands of types '<null>' and 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void VariableDeclaration_UndefinedSymbol() {
        var text = @"
            ref int? a = ref [b];
        ";

        var diagnostics = @"
            undefined symbol 'b'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void InitializerList_AllowsNull() {
        var text = @"
            lowlevel {
                var! a = { 1, 2, 3 };
                a = { [null], 2, 3 };
            }
        ";

        var diagnostics = @"
            cannot convert null to 'int!' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void TernaryExpression_AllowsNull() {
        var text = @"
            null ? 3 : 5;
        ";

        AssertExceptions(text, _writer, new BelteNullReferenceException(null));
    }

    [Fact]
    public void CastExpression_NonNullableOnNull() {
        var text = @"
            [(int!)null];
        ";

        var diagnostics = @"
            cannot convert null to 'int!' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CastExpression_Versus_ParenthesizedExpression() {
        var text = @"
            int? x = 3;
            int? y = (x) + 1;
            return y;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
        AssertValue(text, 4);
    }

    [Fact]
    public void ReferenceExpression_CannotConvert() {
        var text = @"
            class A {
                public int? num;
            }

            void MyFunction(A a) {
                a.num = 5;
            }

            void MyFunction2() {
                var a = new A();
                MyFunction([ref a]);
            }
        ";

        var diagnostics = @"
            argument 1 may not be passed with the 'ref' keyword
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void AssignmentExpression_CannotAssignConstReference() {
        var text = @"
            int? x = 3;
            const ref int? y = ref x;
            [y] = ref x;
        ";

        var diagnostics = @"
            left side of ref assignment must be a ref variable, ref field, ref parameter, or ref indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void AssignmentExpression_CannotAssignConst() {
        var text = @"
            const x = 3;
            [x] = 56;
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Classes_NoImplicitTyping() {
        var text = @"
            class A {
                [var] num;
            }
        ";

        var diagnostics = @"
            fields cannot be implicitly typed
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Classes_ReassignNull() {
        var text = @"
            class A {
                public int? num;
            }

            var? x = new A();
            x?.num = 3;
            x = null;
            return x?.num;
        ";

        AssertValue(text, null);
    }

    [Fact]
    public void IfStatement_AllowsNull() {
        var text = @"
            if (null) {
                Console.PrintLine(3);
            }
        ";

        AssertExceptions(text, _writer, new BelteNullConditionException(null));
    }

    [Fact]
    public void CompoundExpression_Undefined() {
        var text = @"
            var? x = 10;
            [x += false];
        ";

        var diagnostics = @"
            binary operator '+=' is not defined for operands of types 'int?' and 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CompoundExpression_Assignment_NonDefinedVariable_Undefined() {
        var text = @"
            [x] += 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CompoundDeclarationExpression_CannotAssign() {
        var text = @"
            {
                const int? x = 10;
                [x] += 1;
            }
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void InvokeFunctionArguments_NoInfiniteLoop() {
        var text = @"Console.PrintLine(""Hi""[=]);";

        var diagnostics = @"
            unexpected token '='
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void InvokeFunctionArguments_Missing() {
        var text = @"
            void myFunc(int? a) { }
            [myFunc]();
        ";

        var diagnostics = @"
            there is no argument given that corresponds to the required parameter 'a' of 'myFunc(int?)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void InvokeFunctionArguments_Exceeding() {
        var text = @"
            void myFunc(int? a) { }
            [myFunc](1, 2, 3);
        ";

        var diagnostics = @"
            no overload for method 'myFunc' takes 3 arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void FunctionParameters_NoInfiniteLoop() {
        var text = @"
            void hi(string? name=[)] {
                Console.PrintLine(""Hi "" + name + ""!"");
            }
        ";

        var diagnostics = @"
            expected expression
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void FunctionReturn_Missing() {
        var text = @"
            int? [add](int? a, int? b) {
            }
        ";

        var diagnostics = @"
            not all code paths return a value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Block_NoInfiniteLoop() {
        var text = @"
            {[]
            )[]
        ";

        var diagnostics = @"
            expected token '}'
            expected '}' at end of input
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Block_MinimalDiagnostics() {
        var text = @"
            {[]
            )
            }
        ";

        var diagnostics = @"
            expected token '}'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void IfStatement_CannotConvert() {
        var text = @"
            var x = 0;
            if ([10])
                x = 1;
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'bool?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void WhileStatement_CannotConvert() {
        var text = @"
            var x = 0;
            while ([10]) { x = 10; }
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'bool?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void DoWhileStatement_CannotConvert() {
        var text = @"
            var x = 0;
            do { x = 10; } while ([10]);
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'bool?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ForStatement_CannotConvert() {
        var text = @"
            for (int? i = 0; [i]; i++) {}
        ";

        var diagnostics = @"
            cannot convert from type 'int?' to 'bool?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void VariableDeclaration_Redeclaration() {
        var text = @"
            var x = 10;
            var y = 100;
            {
                var [x] = 10;
            }
            var [x] = 5;
        ";

        var diagnostics = @"
            cannot declare a local with the name 'x' because that name is already used by a parameter in an enclosing scope
            a local or local function with the name 'x' has already been declared in this scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void NameExpression_Undefined() {
        var text = @"
            [x] * 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void NameExpression_NoErrorForInsertedToken() {
        AssertDiagnostics("", "", _writer);
    }

    [Fact]
    public void AssignmentExpression_Undefined() {
        var text = @"
            [x] = 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void AssignmentExpression_Readonly() {
        var text = @"
            const int? x = 10;
            [x] = 0;
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void AssignmentExpression_CannotConvert() {
        var text = @"
            var? x = 10;
            x = [false];
        ";

        var diagnostics = @"
            cannot convert from type 'bool!' to 'int?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CallExpression_Undefined() {
        var text = @"
            [foo]();
        ";

        var diagnostics = @"
            undefined symbol 'foo'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CallExpression_CannotCall() {
        var text = @"
            var foo = 4;
            [foo]();
        ";

        var diagnostics = @"
            called object is not a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Function_ShouldNotReturnValue() {
        var text = @"
            void func() {
                [return] 5;
            }
        ";

        var diagnostics = @"
            cannot return a value in a method returning void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Function_ShouldNotReturnVoid() {
        var text = @"
            int? func() {
                [return];
            }
        ";

        var diagnostics = @"
            cannot return without a value in a method returning non-void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Script_Return() {
        var text = @"
            return;
        ";

        AssertValue(text, null);
    }

    [Fact]
    public void Expression_MustHaveValue() {
        var text = @"
            void func() {}
            [var x = func()];
        ";

        var diagnostics = @"
            cannot assign void to an implicitly-typed local
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Break_Invalid() {
        var text = @"
            [break;]
        ";

        var diagnostics = @"
            break and continue statements can only be used within a loop
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Parameter_AlreadyDeclared() {
        var text = @"
            void func(int? a, int? [a]) {}
        ";

        var diagnostics = @"
            the parameter name 'a' is a duplicate
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Function_WrongArgumentType() {
        var text = @"
            void func(int? a) {}
            func([false]);
        ";

        var diagnostics = @"
            argument 1: cannot convert from type 'bool!' to 'int?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void InvalidType() {
        var text = @"
            void func([invalidType] a) {}
        ";

        var diagnostics = @"
            the type or namespace name 'invalidType' could not be found
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void UnaryOperator_Undefined() {
        var text = @"
            [+true];
        ";

        var diagnostics = @"
            unary operator '+' is not defined for type 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void BinaryOperator_Undefined() {
        var text = @"
            [10+true];
        ";

        var diagnostics = @"
            binary operator '+' is not defined for operands of types 'int!' and 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Function_CanDeclare() {
        var text = @"
            void myFunction(int? num1, int? num2) {
                Console.Print(num1 + num2 / 3.14159);
            }
            myFunction(1, 2);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Function_CanCall() {
        var text = @"
            void myFunction(int? num) {
                Console.Print(num ** 2);
            }
            myFunction(2);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CallExpression_ExpectedTokens() {
        var text = @"
            Console.Print(num ** 2 ([][][]
        ";

        var diagnostics = @"
            expected ')' at end of input
            expected ')' at end of input
            expected ';' at end of input
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void MethodInvoke_DoNotPopLocalsInStatic() {
        var text = @"
            class A {
                public static void Util() {
                    Console.PrintLine(""123"");
                }
                public void Test() {
                    Util();
                    A.Util();
                }
            }
            var a = new A();
            a.Test();
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ClassDefinition_SeeSubClasses() {
        var text = @"
            class A {
                class B { }
                B b = new B();
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void IndexExpression_NotTreatedAsTypeClause() {
        var text = @"
            lowlevel {
                int a = 1;
                int?\[\] b = {1, 2, 3};
                b\[a\] = 3;
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void MemberAccessExpression_NestedCalls() {
        var text = @"
            class A {
                public void Test() { }
            }
            class B {
                public A First() { return new A(); }
            }
            var myB = new B();
            myB.First().Test();
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CallExpression_ExceedingArgumentsOnZero() {
        var text = @"
            void Test() {}
            [Test](,);
        ";

        var diagnostics = @"
           no overload for method 'Test' takes 2 arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void PostfixExpression_AllowedOnRef() {
        var text = @"
            int? x = 3;
            ref var y = ref x;
            y++;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CallExpression_CorrectErrorFormattingOnNonMethod() {
        var text = @"
            [3]();
        ";

        var diagnostics = @"
            called object is not a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void FieldDeclaration_CorrectErrorOnInvalidType() {
        var text = @"
            class A {
                [coasdf] G = 4;
            }
        ";

        var diagnostics = @"
            the type or namespace name 'coasdf' could not be found
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Cast_CannotConvertConstRefToRef() {
        var text = @"
            void Test(ref int? a) { a++; }
            const int? a = 3;
            Test([ref a]);
        ";

        var diagnostics = @"
            argument 1: cannot pass a reference to a constant to a parameter expecting a reference to a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Assignment_HonorsConstantMemberAccess() {
        var text = @"
            class A {
                public int? a = 3;
            }
            const a = new A();
            [a.a]++;
        ";

        var diagnostics = @"
            left side of increment or decrement operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void MethodBody_StaticMethodCannotAccessMembers() {
        var text = @"
            class A {
                int? a;
                static int? Test() { return [a]; }
            }
        ";

        var diagnostics = @"
            an object reference is required for non-static member 'A.a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void MethodBody_StaticMethodCannotAccessMethods() {
        var text = @"
            class A {
                int? Test() { return 3; }
                static int? Test1() { return [Test](); }
            }
        ";

        var diagnostics = @"
            an object reference is required for non-static member 'A.Test()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Constexpr_AllowsImplicitTyping() {
        var text = @"
            constexpr y = 3;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ReferenceExpression_NoInfiniteLoop() {
        var text = @"
            ref int? y;
            y = ref y;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void TypeExpression_NotAllowedInContext() {
        var text = @"
            static class A { }
            return [A];
        ";

        var diagnostics = @"
            'A' is a type but is used like a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void CallExpression_NonInvocableType() {
        var text = @"
            static class A { }
            return [A]();
        ";

        var diagnostics = @"
            non-invocable member 'A' cannot be used like a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! TODO
    // [Fact]
    // public void ClassDeclaration_StaticCanSeeTemplates() {
    //     var text = @"
    //         class A<int? a> {
    //             public static A<a> operator~(A<a> a) {
    //                 return a;
    //             }
    //         }
    //     ";

    //     var diagnostics = @"";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void OperatorOverloading_ReturnsCorrectType() {
        var text = @"
            class A<type t> {
                public int? v = 3;

                public static A<t> operator+(int? a, A<t> b) {
                    b.v = 7;
                    return b;
                }
            }

            var myA = new A<string?>();
            var c = 6 + (3 + myA);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Structs_InitializesProperly() {
        var text = @"
            lowlevel struct A<type T> where { T has default; } {
                T a;
            }

            var a = new A<int?>();
            a.a = 3;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Casts_CorrectlyParses() {
        var text = @"
            class A { }
            A a = (A)new A();
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Function_ParametersCanUseTemplates() {
        var text = @"
            void M<type T>(T x) { }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Template_TemplatesSeeConstraints() {
        var text = @"
            string? M<type T>(T x) where { T extends Object; } {
                return x?.ToString();
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ElseStatement_NotReachableCode_Warning() {
        var text = @"
            int test() {
                if (true)
                    return 1;
                else
                    [return 0;]
            }
        ";

        var diagnostics = @"
            unreachable code
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void WhileStatement_NotReachableCode_Warning() {
        var text = @"
            void test() {
                while (false) {
                    [continue;]
                }
            }
        ";

        var diagnostics = @"
            unreachable code
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void IfStatement_NotReachableCode_Warning() {
        var text = @"
            void test() {
                constexpr int x = 4 * 3;
                if (x > 12) {
                    [Console.PrintLine(""x"");]
                } else {
                    Console.PrintLine(""x"");
                }
            }
        ";

        var diagnostics = @"
            unreachable code
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Enum_ArgumentAllowsImplicitField() {
        var text = @"
            M(.B);
            void M(A a) { }
            enum A { B }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void BinaryExpression_DoesNotParseAsTemplate() {
        var text = @"
            var ch = '0';
            var b = ((ch < 'A' || ch > 'Z') && (ch < 'a' || ch > 'z'));
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void BinaryExpression_DoesNotParseAsTemplate2() {
        var text = @"
            struct A { int f; }
            var a = new A();
            var b = new A();
            var c = b.f < 0 || a.f > b.f;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void BinaryExpression_DoesNotParseAsTemplate3() {
        var text = @"
            struct A { int f; }
            var a = new A();
            var c = (a.f < 21 && a.f > -7);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ConditionalOperator_GetsTargetTyped() {
        var text = @"
            int? M() {
                var cond = true;
                return cond ? 3 : null;
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ImplicitTyping_CannotConvert() {
        var text = @"
            int? M() {
                int a = 3;
                ref const? b = ref [a];
            }
        ";

        var diagnostics = @"
            the expression must be of type 'int?' because it is being assigned by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Assignment_NoInfiniteLoop() {
        var text = @"
            var a = [a];
        ";

        var diagnostics = @"
            cannot use local 'a' before it is declared
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void IndexExpression_NoCrashOnNoIndex() {
        var text = @"
            int\[\] a = { 1 };
            var b = [a\[\]];
        ";

        var diagnostics = @"
            wrong number of indices inside []; expected 1
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void StackallocLocal_NoCrashOnNoIndex() {
        var text = @"
            int a[\[\]];
        ";

        var diagnostics = @"
            a stackalloc expression or local requires a type with a single array size specifier
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void TypeDeclaration_MissingClosingBraceMinimalDiagnostics() {
        var text = @"
            class A {
                public int M() {
                    return 3;[]

                public int B() {
                    return 10;
                }
            }
        ";

        var diagnostics = @"
            expected token '}'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void UsingDirective_AllowsPlacementBetweenMembers() {
        var text = @"
            using static A;

            class A {
                public int a = default;
            }

            using B = A;

            var a = new B();
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ForEach_AllowsModification() {
        var text = @"
            class Elem {
                public int e;

                public constructor(int e) {
                    this.e = e;
                }
            }

            Elem\[\] a = { new (10), new (20), new (30) };

            for (num in a)
                num.e = 5;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void MisplacedKeyword_MinimalDiagnostics() {
        var text = @"
            var [out] = 3;
        ";

        var diagnostics = @"
            unexpected token 'out', expected identifier
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // TODO It would be more ideal if this actually mentioned the issue of using `a` before being declared
    [Fact]
    public void DeconstructAssignment_NoInfiniteLoop() {
        var text = @"
            (var [a], var [b]) = a;
        ";

        var diagnostics = @"
            cannot infer the type of implicitly-typed deconstruction variable 'a'
            cannot infer the type of implicitly-typed deconstruction variable 'b'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Parameter_AcceptsKnownImmutableArgument() {
        var text = @"
            class A { }

            void Func(A a) { }

            const a = new A();
            Func(a);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void NestedStruct_BakesWithoutCrashing() {
        var text = @"
            class C {
                struct S {
                    int32 f\[10\];
                }
            }
            ;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void LocalFunction_CanCallInConstMethod() {
        var text = @"
            class C {
                public const void M() {
                    F();
                    void F() { }
                }
            }
            ;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void NullCoalescing_DoesNotAllowNonNull() {
        var text = @"
            int a = [3 ?? 5];
        ";

        var diagnostics = @"
            binary operator '??' is not defined for operands of types 'int!' and 'int!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void NullAssert_PreventsStructAssignment() {
        var text = @"
            class A {
                public S? b;

                public struct S {
                    public int? a;
                }
            }

            var a = ((A?)new A())?..b = (new A.S()..a = 4);
            [a?.b!.a] = 10;
            return a?.b!.a;
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Struct_PassesCopy() {
        var text = @"
            void Func(int? num) {
                num = 10;
            }

            int? a = 5;
            Func(a);
            return a!;
        ";

        AssertValue(text, 5);
    }

    [Fact]
    public void Template_WorksWithRecursion() {
        var text = @"
            abstract class Comparable<type T> {
                abstract public int compareTo(T other);
            }

            class Int extends Comparable<Int> {
                public int value;

                public constructor(int value) {
                    this.value = value;
                }

                override public int compareTo(Int other) {
                    return this.value - other.value;
                }
            }

            class Tree<type T> where { T extends Comparable<T>; } { }

            var i1 = new Int(10);
            var i2 = new Int(15);
            return i1.compareTo(i2);
        ";

        AssertValue(text, -5);
    }

    [Fact]
    public void Template_ErrsWithRecursion() {
        var text = @"
            abstract class Comparable<type T> {
                abstract public int compareTo(T other);
            }

            class Tree<[Comparable<T> T]> { }

            ;
        ";

        var diagnostics = @"
            unsupported: cannot declare a non-type template when building for .NET, transpiling to C#, or executing
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Override_ReturnIsNullabilitySensitive() {
        var text = @"
            class A {
                public virtual bool M() { return false; }
            }
            class B extends  A {
                public override bool? [M]() { return false; }
            }
            ;
        ";

        var diagnostics = @"
            'B.M()': return type must be 'bool!' to match overridden member 'A.M()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Override_ParametersAreNullabilitySensitive() {
        var text = @"
            class A {
                public virtual void M(bool b) { }
            }
            class B extends  A {
                public override void [M](bool? b) { }
            }
            ;
        ";

        var diagnostics = @"
            'B.M(bool?)': no suitable method found to override
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Method_AllowsConstRefConstParameter() {
        var text = @"
            class A {
                public void M(const ref const bool b) { }
            }
            ;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Interface_SeesTemplateSubstitution() {
        var text = @"
            interface A<type T> {
                void B(T t);
            }
            class C implements A<int> {
                public void B(int t) { }
            }
            ;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Conversion_AllowsDownCast() {
        var text = @"
            abstract class Decl { }

            class TDecl extends Decl { }

            Decl decl = new TDecl();
            TDecl tdecl = (TDecl)decl;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void ScopedStatement_AllPathsReturn() {
        var text = @"
            class A { destructor() { } }

            int M() {
                scoped var a = new A();
                scoped var b = new A();
                return 0;
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }
}
