using System;
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
            var! a = { 1, 2, 3 };
            a = [{null, 2, 3 }];
        ";

        var diagnostics = @"
            cannot convert from type 'int[]' to 'int[]!' implicitly; an explicit conversion exists (are you missing a cast?)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_TernaryExpression_AllowsNull() {
        var text = @"
            null ? 3 : 5;
        ";

        AssertExceptions(text, _writer, new NullReferenceException());
    }

    [Fact]
    public void Evaluator_CastExpression_NonNullableOnNull() {
        var text = @"
            [(int!)null];
        ";

        var diagnostics = @"
            cannot convert 'null' to 'int!' because it is a non-nullable type
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
                int num;
            }

            void MyFunction(A a) {
                a.num = 5;
            }

            var a = new A();
            MyFunction([ref a]);
        ";

        var diagnostics = @"
            argument 1: cannot convert from type 'ref A' to 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotAssignConstReference() {
        var text = @"
            int x = 3;
            const y = ref x;
            y [=] ref x;
        ";

        var diagnostics = @"
            'y' cannot be assigned to as it is a constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotAssignConst() {
        var text = @"
            const x = 3;
            x [=] 56;
        ";

        var diagnostics = @"
            'x' cannot be assigned to as it is a constant
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
            cannot use implicit-typing in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Classes_ReassignNull() {
        var text = @"
            class A {
                int num;
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

        AssertExceptions(text, _writer, new NullReferenceException());
    }

    [Fact]
    public void Evaluator_IfStatement_Reports_NotReachableCode_Warning() {
        var text = @"
            void test() {
                const int x = 4 * 3;
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
    public void Evaluator_CompoundExpression_Reports_Undefined() {
        var text = @"
            var x = 10;
            x [+=] false;
        ";

        var diagnostics = @"
            compound operator '+=' is not defined for types 'int' and 'bool'
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
                x [+=] 1;
            }
        ";

        var diagnostics = @"
            'x' cannot be assigned to as it is a constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_ElseStatement_Reports_NotReachableCode_Warning() {
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
    public void Evaluator_WhileStatement_Reports_NotReachableCode_Warning() {
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
            myFunc([)];
        ";

        var diagnostics = @"
            method 'myFunc' expects 1 argument, got 0
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_Exceeding() {
        var text = @"
            void myFunc(int a) { }
            myFunc(""Hello""[, "" "", "" world!""]);
        ";

        var diagnostics = @"
            method 'myFunc' expects 1 argument, got 3
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
            cannot convert from type 'int' to 'bool'
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
            cannot convert from type 'int' to 'bool'
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
            cannot convert from type 'int' to 'bool'
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
            variable 'x' is already declared in this scope
            variable 'x' is already declared in this scope
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
            [PrintLine] = 10;
        ";

        var diagnostics = @"
            method 'PrintLine' cannot be used as a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_Readonly() {
        var text = @"
            const int x = 10;
            x [=] 0;
        ";

        var diagnostics = @"
            'x' cannot be assigned to as it is a constant
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
            cannot convert from type 'bool' to 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_CallExpression_Reports_Undefined() {
        var text = @"
            [foo]();
        ";

        var diagnostics = @"
            undefined method 'foo'
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
            called object 'foo' is not a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Variables_ShadowsFunction() {
        var text = @"
            int PrintLine = 4;
            [PrintLine](""test"");
        ";

        var diagnostics = @"
            called object 'PrintLine' is not a method
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
            var x = [func()];
        ";

        var diagnostics = @"
            expression must have a value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Break_Invalid() {
        var text = @"
            [break];
        ";

        var diagnostics = @"
            break statements can only be used within a loop
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Parameter_AlreadyDeclared() {
        var text = @"
            void func(int a, [int a]) {}
        ";

        var diagnostics = @"
            cannot reuse parameter name 'a'; parameter names must be unique
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Function_MustHaveName() {
        var text = @"
            void [(]int a) {}
        ";

        var diagnostics = @"
            expected identifier
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
            argument 1: cannot convert from type 'bool' to 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_InvalidType() {
        var text = @"
            void func([invalidType] a) {}
        ";

        var diagnostics = @"
            unknown type 'invalidType'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_UnaryOperator_Reports_Undefined() {
        var text = @"
            [+]true;
        ";

        var diagnostics = @"
            unary operator '+' is not defined for type 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_BinaryOperator_Reports_Undefined() {
        var text = @"
            10[+]true;
        ";

        var diagnostics = @"
            binary operator '+' is not defined for types 'int' and 'bool'
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
                static void Util() {
                    Console.PrintLine(""123"");
                }
                void Test() {
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
            int a = 1;
            int\[\] b = {1, 2, 3};
            b\[a\] = 3;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_MemberAccessExpression_NestedCalls() {
        var text = @"
            class A {
                void Test() { }
            }
            class B {
                A First() { return new A(); }
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
            Test([,]);
        ";

        var diagnostics = @"
            method 'Test' expects 0 arguments, got 2
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_PostfixExpression_AllowedOnRef() {
        var text = @"
            int x = 3;
            var y = ref x;
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
            unknown type 'coasdf'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Cast_CannotConvertConstRefToRef() {
        var text = @"
            void Test(ref int a) { a++; }
            const int a = 3;
            Test([ref a]);
        ";

        var diagnostics = @"
            argument 1: cannot convert from type 'ref const int' to 'ref int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_Assignment_HonorsConstantMemberAccess() {
        var text = @"
            class A {
                int a = 3;
            }
            const a = new A();
            a.a[++];
        ";

        var diagnostics = @"
            'a' cannot be assigned to as it is a constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_FunctionDeclaration_ParsesConst() {
        var text = @"
            [const] int Test() {}
        ";

        var diagnostics = @"
            modifier 'const' is not valid for this item
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
            an object reference is required for non-static member 'a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Evaluator_MethodBody_StaticMethodCannotAccessMethods() {
        var text = @"
            class A {
                int Test() { return 3; }
                static int Test1() { return [Test()]; }
            }
        ";

        var diagnostics = @"
            an object reference is required for non-static member 'Test'
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
            'A' is a type, which is not valid in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }
}
