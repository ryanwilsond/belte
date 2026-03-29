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
    public void Evaluator_NullCoalescing_Report_NoDefinedForNullOperand() {
        var text = @"
            [null ?? 2];
        ";

        var diagnostics = @"
            binary operator '??' is not defined for operands of types '<null>' and 'int!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_ConditionalOr_Report_NoDefinedForNullOperand() {
        var text = @"
            [null || true];
        ";

        var diagnostics = @"
            binary operator '||' is not defined for operands of types '<null>' and 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_VariableDeclaration_Reports_UndefinedSymbol() {
        var text = @"
            ref int a = ref [b];
        ";

        var diagnostics = @"
            undefined symbol 'b'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_InitializerList_AllowsNull() {
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
    public void Evaluator_TernaryExpression_AllowsNull() {
        var text = @"
            null ? 3 : 5;
        ";

        AssertExceptions(text, _writer, new BelteNullReferenceException(null));
    }

    [Fact]
    public void Evaluator_CastExpression_NonNullableOnNull() {
        var text = @"
            [(int!)null];
        ";

        var diagnostics = @"
            cannot convert null to 'int!' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CastExpression_Versus_ParenthesizedExpression() {
        var text = @"
            int x = 3;
            int y = (x) + 1;
            return y;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
        AssertValue(text, 4);
    }

    [Fact]
    public void Evaluator_ReferenceExpression_Reports_CannotConvert() {
        var text = @"
            class A {
                public int num;
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
    public void Evaluator_AssignmentExpression_Reports_CannotAssignConstReference() {
        var text = @"
            int x = 3;
            const ref int y = ref x;
            [y] = ref x;
        ";

        var diagnostics = @"
            left side of ref assignment must be a ref variable, ref field, ref parameter, or ref indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotAssignConst() {
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
    public void Evaluator_Classes_Reports_NoImplicitTyping() {
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
    public void Evaluator_Classes_ReassignNull() {
        var text = @"
            class A {
                public int num;
            }

            var x = new A();
            x.num = 3;
            x = null;
            return x?.num;
        ";

        AssertValue(text, null);
    }

    [Fact]
    public void Evaluator_IfStatement_AllowsNull() {
        var text = @"
            if (null) {
                Console.PrintLine(3);
            }
        ";

        AssertExceptions(text, _writer, new BelteNullReferenceException(null));
    }

    [Fact]
    public void Evaluator_CompoundExpression_Reports_Undefined() {
        var text = @"
            var x = 10;
            [x += false];
        ";

        var diagnostics = @"
            binary operator '+=' is not defined for operands of types 'int' and 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CompoundExpression_Assignment_NonDefinedVariable_Reports_Undefined() {
        var text = @"
            [x] += 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CompoundDeclarationExpression_Reports_CannotAssign() {
        var text = @"
            {
                const int x = 10;
                [x] += 1;
            }
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_NoInfiniteLoop() {
        var text = @"Console.PrintLine(""Hi""[=]);";

        var diagnostics = @"
            unexpected token '='
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_Missing() {
        var text = @"
            void myFunc(int a) { }
            [myFunc]();
        ";

        var diagnostics = @"
            there is no argument given that corresponds to the required parameter 'a' of 'myFunc(int)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_Exceeding() {
        var text = @"
            void myFunc(int a) { }
            [myFunc](1, 2, 3);
        ";

        var diagnostics = @"
            no overload for method 'myFunc' takes 3 arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_FunctionParameters_NoInfiniteLoop() {
        var text = @"
            void hi(string name=[)] {
                Console.PrintLine(""Hi "" + name + ""!"");
            }
        ";

        var diagnostics = @"
            expected expression
        ";

        AssertDiagnostics(text, diagnostics, _writer);
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

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Block_NoInfiniteLoop() {
        var text = @"
            {
            [)][]
        ";

        var diagnostics = @"
            expected expression
            expected '}' at end of input
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_IfStatement_Reports_CannotConvert() {
        var text = @"
            var x = 0;
            if ([10])
                x = 1;
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_WhileStatement_Reports_CannotConvert() {
        var text = @"
            var x = 0;
            while ([10]) { x = 10; }
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_DoWhileStatement_Reports_CannotConvert() {
        var text = @"
            var x = 0;
            do { x = 10; } while ([10]);
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_ForStatement_Reports_CannotConvert() {
        var text = @"
            for (int i = 0; [i]; i++) {}
        ";

        var diagnostics = @"
            cannot convert from type 'int' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_VariableDeclaration_Reports_Redeclaration() {
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
    public void Evaluator_NameExpression_Reports_Undefined() {
        var text = @"
            [x] * 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_NameExpression_Reports_NoErrorForInsertedToken() {
        AssertDiagnostics("", "", _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_Undefined() {
        var text = @"
            [x] = 10;
        ";

        var diagnostics = @"
            undefined symbol 'x'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotAssign() {
        var text = @"
            [Console.PrintLine] = 10;
        ";

        var diagnostics = @"
            cannot assign to 'PrintLine' because it is a method group
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_Readonly() {
        var text = @"
            const int x = 10;
            [x] = 0;
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotConvert() {
        var text = @"
            var x = 10;
            x = [false];
        ";

        var diagnostics = @"
            cannot convert from type 'bool!' to 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CallExpression_Reports_Undefined() {
        var text = @"
            [foo]();
        ";

        var diagnostics = @"
            undefined symbol 'foo'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CallExpression_Reports_CannotCall() {
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
    public void Evaluator_Function_ShouldNotReturnValue() {
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
    public void Evaluator_Function_ShouldNotReturnVoid() {
        var text = @"
            int func() {
                [return];
            }
        ";

        var diagnostics = @"
            cannot return without a value in a method returning non-void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
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
            [var x = func()];
        ";

        var diagnostics = @"
            cannot assign void to an implicitly-typed local
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Break_Invalid() {
        var text = @"
            [break;]
        ";

        var diagnostics = @"
            break and continue statements can only be used within a loop
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Parameter_AlreadyDeclared() {
        var text = @"
            void func(int a, int [a]) {}
        ";

        var diagnostics = @"
            the parameter name 'a' is a duplicate
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Function_MustHaveName() {
        var text = @"
            void [(]int [a]) {}
        ";

        var diagnostics = @"
            unexpected token '('
            unexpected identifier, expected '('
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Function_WrongArgumentType() {
        var text = @"
            void func(int a) {}
            func([false]);
        ";

        var diagnostics = @"
            argument 1: cannot convert from type 'bool!' to 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_InvalidType() {
        var text = @"
            void func([invalidType] a) {}
        ";

        var diagnostics = @"
            undefined symbol 'invalidType'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_UnaryOperator_Reports_Undefined() {
        var text = @"
            [+true];
        ";

        var diagnostics = @"
            unary operator '+' is not defined for type 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_BinaryOperator_Reports_Undefined() {
        var text = @"
            [10+true];
        ";

        var diagnostics = @"
            binary operator '+' is not defined for operands of types 'int!' and 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Function_CanDeclare() {
        var text = @"
            void myFunction(int num1, int num2) {
                Console.Print(num1 + num2 / 3.14159);
            }
            myFunction(1, 2);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Function_CanCall() {
        var text = @"
            void myFunction(int num) {
                Console.Print(num ** 2);
            }
            myFunction(2);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CallExpression_ExpectedTokens() {
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
    public void Evaluator_MethodInvoke_DoNotPopLocalsInStatic() {
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
    public void Evaluator_ClassDefinition_SeeSubClasses() {
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
    public void Evaluator_IndexExpression_NotTreatedAsTypeClause() {
        var text = @"
            lowlevel {
                int a = 1;
                int\[\] b = {1, 2, 3};
                b\[a\] = 3;
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_MemberAccessExpression_NestedCalls() {
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
    public void Evaluator_CallExpression_ExceedingArgumentsOnZero() {
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
    public void Evaluator_PostfixExpression_AllowedOnRef() {
        var text = @"
            int x = 3;
            ref var y = ref x;
            y++;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CallExpression_CorrectErrorFormattingOnNonMethod() {
        var text = @"
            [3]();
        ";

        var diagnostics = @"
            called object is not a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_FieldDeclaration_CorrectErrorOnInvalidType() {
        var text = @"
            class A {
                [coasdf] G = 4;
            }
        ";

        var diagnostics = @"
            undefined symbol 'coasdf'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Cast_CannotConvertConstRefToRef() {
        var text = @"
            void Test(ref int a) { a++; }
            const int a = 3;
            Test(ref [a]);
        ";

        var diagnostics = @"
            ref value must be an assignable variable, field, parameter, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Assignment_HonorsConstantMemberAccess() {
        var text = @"
            class A {
                public int a = 3;
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
    public void Evaluator_MethodBody_StaticMethodCannotAccessMembers() {
        var text = @"
            class A {
                int a;
                static int Test() { return [a]; }
            }
        ";

        var diagnostics = @"
            an object reference is required for non-static member 'A.a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_MethodBody_StaticMethodCannotAccessMethods() {
        var text = @"
            class A {
                int Test() { return 3; }
                static int Test1() { return [Test](); }
            }
        ";

        var diagnostics = @"
            an object reference is required for non-static member 'A.Test()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Constexpr_AllowsImplicitTyping() {
        var text = @"
            constexpr y = 3;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_ReferenceExpression_NoInfiniteLoop() {
        var text = @"
            ref int y;
            y = ref y;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_TypeExpression_NotAllowedInContext() {
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
    public void Evaluator_CallExpression_NonInvocableType() {
        var text = @"
            static class A { }
            return [A]();
        ";

        var diagnostics = @"
            non-invocable member 'A' cannot be used like a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_ClassDeclaration_StaticCanSeeTemplates() {
        var text = @"
            class A<int a> {
                public static A<a> operator~(A<a> a) {
                    return a;
                }
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_OperatorOverloading_ReturnsCorrectType() {
        var text = @"
            class A<type t> {
                public int v = 3;

                public static A<t> operator+(int a, A<t> b) {
                    b.v = 7;
                    return b;
                }
            }

            var myA = new A<string>();
            var c = 6 + (3 + myA);
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Structs_InitializesProperly() {
        var text = @"
            lowlevel struct A<type T> {
                T a;
            }

            var a = new A<int>();
            a.a = 3;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Casts_CorrectlyParses() {
        var text = @"
            class A { }
            A a = (A)new A();
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Function_ParametersCanUseTemplates() {
        var text = @"
            void M<type T>(T x) { }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Template_TemplatesSeeConstraints() {
        var text = @"
            string M<type T>(T x) where { T extends Object; } {
                return x.ToString();
            }
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Bring these back when CFG issues are fixed
    // [Fact]
    // public void Evaluator_ElseStatement_Reports_NotReachableCode_Warning() {
    //     var text = @"
    //         int test() {
    //             if (true)
    //                 return 1;
    //             else
    //                 [return 0;]
    //         }
    //     ";

    //     var diagnostics = @"
    //         unreachable code
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer, true);
    // }

    // [Fact]
    // public void Evaluator_WhileStatement_Reports_NotReachableCode_Warning() {
    //     var text = @"
    //         void test() {
    //             while (false) {
    //                 [continue;]
    //             }
    //         }
    //     ";

    //     var diagnostics = @"
    //         unreachable code
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer, true);
    // }

    // [Fact]
    // public void Evaluator_IfStatement_Reports_NotReachableCode_Warning() {
    //     var text = @"
    //         void test() {
    //             const int x = 4 * 3;
    //             if (x > 12) {
    //                 [Console.PrintLine(""x"");]
    //             } else {
    //                 Console.PrintLine(""x"");
    //             }
    //         }
    //     ";

    //     var diagnostics = @"
    //         unreachable code
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer, true);
    // }
}
