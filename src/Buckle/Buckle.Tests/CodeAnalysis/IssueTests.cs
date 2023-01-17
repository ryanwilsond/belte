using System;
using Xunit;

namespace Buckle.Tests.CodeAnalysis;

public sealed partial class EvaluatorTests {
    [Fact]
    public void Evaluator_CastExpression_Throws_NullReference() {
        var text = @"
            ([NotNull]int)null;
        ";

        AssertExceptions(text, new NullReferenceException());
    }

    [Fact]
    public void Evaluator_CastExpression_Versus_ParenthesizedExpression() {
        var text = @"
            int x = 3;
            int y = (x) + 1;
            return y;
        ";

        var diagnostics = @"";

        AssertDiagnostics(text, diagnostics);
        AssertValue(text, 4);
    }

    [Fact]
    public void Evaluator_ReferenceExpression_Reports_CannotConvert() {
        var text = @"
            struct A {
                int num;
            }

            void MyFunction(A a) {
                a.num = 5;
            }

            var a = A();
            MyFunction([ref a]);
        ";

        var diagnostics = @"
            argument 1: cannot convert from type 'ref A' to 'A'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_AssignmentExpression_Reports_CannotAssignConstReference() {
        var text = @"
            int x = 3;
            const y = ref x;
            y [=] ref x;
        ";

        var diagnostics = @"
            'y' cannot be assigned to with a reference as it is a constant reference
        ";

        AssertDiagnostics(text, diagnostics);
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

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Structs_Reports_NoImplicitTyping() {
        var text = @"
            struct A {
                [var] num;
            }
        ";

        var diagnostics = @"
            cannot use implicit-typing in this context
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Structs_ReassignNull() {
        var text = @"
            struct A {
                int num;
            }

            var x = A();
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
                PrintLine(3);
            }
        ";

        AssertExceptions(text, new NullReferenceException());
    }

    [Fact]
    public void Evaluator_IfStatement_Reports_NotReachableCode_Warning() {
        var text = @"
            void test() {
                const int x = 4 * 3;
                if (x > 12) {
                    [PrintLine(""x"");]
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
            compound operator '+=' is not defined for types 'int' and 'bool'
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
            'x' cannot be assigned to as it is a constant
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
                    [return 0;]
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
                    [continue;]
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
            void myFunc(int a) { }
            myFunc([)];
        ";

        var diagnostics = @"
            function 'myFunc' expects 1 argument, got 0
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_InvokeFunctionArguments_Exceeding() {
        var text = @"
            void myFunc(int a) { }
            myFunc(""Hello""[, "" "", "" world!""]);
        ";

        var diagnostics = @"
            function 'myFunc' expects 1 argument, got 3
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_FunctionParameters_NoInfiniteLoop() {
        var text = @"
            void hi(string name=[)] {
                PrintLine(""Hi "" + name + ""!"");
            }
        ";

        var diagnostics = @"
            expected expression
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
            expected expression
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
                var [x] = 10;
            }
            var [x] = 5;
        ";

        var diagnostics = @"
            variable 'x' is already declared in this scope
            variable 'x' is already declared in this scope
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
            function 'PrintLine' cannot be used as a variable
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
            'x' cannot be assigned to as it is a constant
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
            cannot return a value in a function returning void
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
            cannot return without a value in a function returning non-void
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
            break statements can only be used within a loop
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_Parameter_AlreadyDeclared() {
        var text = @"
            void func(int a, [int a]) {}
        ";

        var diagnostics = @"
            cannot reuse parameter name 'a'; parameter names must be unique
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
            argument 1: cannot convert from type 'bool' to 'int'
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
            unary operator '+' is not defined for type 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_BinaryOperator_Reports_Undefined() {
        var text = @"
            10[+]true;
        ";

        var diagnostics = @"
            binary operator '+' is not defined for types 'int' and 'bool'
        ";

        AssertDiagnostics(text, diagnostics);
    }

    [Fact]
    public void Evaluator_DivideByZero_ThrowsException() {
        var text = @"
            56/0;
        ";

        AssertExceptions(text, new DivideByZeroException());
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
    public void Evaluator_CallExpression_ExpectedMethodName() {
        var text = @"
            Print(num ** [2] ([]
        ";

        var diagnostics = @"
            expected method name
            expected ')' at end of input
        ";

        AssertDiagnostics(text, diagnostics);
    }
}
