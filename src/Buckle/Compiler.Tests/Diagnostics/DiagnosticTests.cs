using Xunit;
using Xunit.Abstractions;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests.Diagnostics;

/// <summary>
/// At least one test per diagnostic (any severity) if testable.
/// </summary>
public sealed class DiagnosticTests {
    private readonly ITestOutputHelper _writer;

    public DiagnosticTests(ITestOutputHelper writer) {
        _writer = writer;
    }

    [Fact]
    public void Reports_Warning_BU0001_AlwaysValue() {
        var text = @"
            var x = [null > 3];
        ";

        var diagnostics = @"
            expression will always result to 'null'
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0002_NullDeference() {
        var text = @"
            class A {
                public int num;
            }

            void MyFunc(A a) {
                a[.]num = 3;
            }
        ";

        var diagnostics = @"
            deference of a possibly null value
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0004_InvalidType() {
        var text = @"
            int x = [99999999999999999];
        ";

        var diagnostics = @"
            '99999999999999999' is not a valid 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0005_BadCharacter() {
        var text = @"
            [@];
        ";

        var diagnostics = @"
            unexpected character '@'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0006_UnexpectedToken() {
        var text = @"
            if [=](true) {}
        ";

        var diagnostics = @"
            unexpected token '='
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0007_CannotConvertImplicitly() {
        var text = @"
            string x = [3];
        ";

        var diagnostics = @"
            cannot convert from type 'int' to 'string' implicitly; an explicit conversion exists (are you missing a cast?)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0008_InvalidUnaryOperatorUse() {
        var text = @"
            [-]false;
        ";

        var diagnostics = @"
            unary operator '-' is not defined for type 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0009_NamedBeforeUnnamed() {
        var text = @"
            Console.Print([x]: 1, 3);
        ";

        var diagnostics = @"
            all named arguments must come after any unnamed arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0010_NamedArgumentTwice() {
        var text = @"
            Console.Print(x: 1, [x]: 3);
        ";

        var diagnostics = @"
            named argument 'x' cannot be specified multiple times
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0011_InvalidBinaryOperatorUse() {
        var text = @"
            false [+] 3;
        ";

        var diagnostics = @"
            binary operator '+' is not defined for types 'bool' and 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0013_ParameterAlreadyDeclared() {
        var text = @"
            void myFunc(int x, [int x]) { }
        ";

        var diagnostics = @"
            cannot reuse parameter name 'x'; parameter names must be unique
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0015_NoSuchParameter() {
        var text = @"
            void Test(string a) { }
            Test([msg]: ""test"");
        ";

        var diagnostics = @"
            method 'Test' does not have a parameter named 'msg'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0017_UndefinedSymbol() {
        var text = @"
            int x = [y];
        ";

        var diagnostics = @"
            undefined symbol 'y'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0018_MethodAlreadyDeclared() {
        var text = @"
            void myFunc() { }

            void [myFunc]() { }
        ";

        var diagnostics = @"
            redeclaration of method 'myFunc()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0019_NotAllPathsReturn() {
        var text = @"
            int [myFunc]() { }
        ";

        var diagnostics = @"
            not all code paths return a value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0020_CannotConvert() {
        var text = @"
            class A {
                int num;
            }

            bool x = [new A()];
        ";

        var diagnostics = @"
            cannot convert from type 'A' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0021_VariableAlreadyDeclared() {
        var text = @"
            var x = 5;
            var [x] = 7;
        ";

        var diagnostics = @"
            variable 'x' is already declared in this scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0022_ConstantAssignment() {
        var text = @"
            const int x = 5;
            x [=] 4;
        ";

        var diagnostics = @"
            'x' cannot be assigned to as it is a constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0023_AmbiguousElse() {
        var text = @"
            if (true)
                if (true)
                    Console.PrintLine();
            [else]
                Console.PrintLine();
        ";

        var diagnostics = @"
            ambiguous which if-statement this else-clause belongs to; use curly braces
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0024_NoValue() {
        var text = @"
            int x = [Console.PrintLine()];
        ";

        var diagnostics = @"
            expression must have a value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0025_CannotApplyIndexing() {
        var text = @"
            int x = 3;
            int y = [x\[0\]];
        ";

        var diagnostics = @"
            cannot apply indexing with [] to an expression of type 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0026_UnreachableCode() {
        var text = @"
            if (false) {
                [Console.PrintLine();]
                Console.PrintLine();
            }
        ";

        var diagnostics = @"
            unreachable code
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0027_UnterminatedString() {
        var text = @"
            string x = [""];[]
        ";

        var diagnostics = @"
            unterminated string literal
            expected ';' at end of input
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0028_UndefinedMethod() {
        var text = @"
            string x = [myFunc]();
        ";

        var diagnostics = @"
            undefined method 'myFunc'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0029_IncorrectArgumentCount() {
        var text = @"
            void myFunc() { }
            myFunc([3]);
        ";

        var diagnostics = @"
            method 'myFunc' expects 0 arguments, got 1
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0030_TypeAlreadyDeclared() {
        var text = @"
            class A { }

            class [A] { }
        ";

        var diagnostics = @"
            class 'A' has already been declared in this scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // TODO Cannot test invalid attributes until any attributes exist
    // [Fact]
    // public void Reports_Error_BU0031_DuplicateAttribute() {
    //     var text = @"
    //         \[NotNull\]\[[NotNull]\]int a = 3;
    //     ";

    //     var diagnostics = @"
    //         attribute 'NotNull' has already been applied
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0032_CannotCallNonMethod() {
        var text = @"
            int x = 3;
            int y = [x]();
        ";

        var diagnostics = @"
            called object 'x' is not a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0033_InvalidExpressionStatement() {
        var text = @"
            void myFunc() {
                [5 + 3;]
            }
        ";

        var diagnostics = @"
            only assignment and call expressions can be used as a statement
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0034_UnknownType() {
        var text = @"
            [MyType] x;
        ";

        var diagnostics = @"
            unknown type 'MyType'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0035_InvalidBreakOrContinue() {
        var text = @"
            [break];
        ";

        var diagnostics = @"
            break statements can only be used within a loop
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0037_UnexpectedReturnValue() {
        var text = @"
            void myFunc() {
                [return] 3;
            }
        ";

        var diagnostics = @"
            cannot return a value in a method returning void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0038_MissingReturnValue() {
        var text = @"
            int myFunc() {
                [return];
            }
        ";

        var diagnostics = @"
            cannot return without a value in a method returning non-void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0039_NotAVariable() {
        var text = @"
            void myFunc() { }

            int x = [myFunc] + 3;
        ";

        var diagnostics = @"
            method 'myFunc' cannot be used as a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0040_NoInitOnImplicit() {
        var text = @"
            var [x];
        ";

        var diagnostics = @"
            implicitly-typed variable must have initializer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0041_UnterminatedComment() {
        var text = @"
            [/*]
        ";

        var diagnostics = @"
            unterminated multi-line comment
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0042_NullAssignOnImplicit() {
        var text = @"
            var x = [null];
        ";

        var diagnostics = @"
            cannot initialize an implicitly-typed variable with 'null'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0043_EmptyInitializerListOnImplicit() {
        var text = @"
            lowlevel {
                var x = [{}];
            }
        ";

        var diagnostics = @"
            cannot initialize an implicitly-typed variable with an empty initializer list
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0044_ImpliedDimensions() {
        var text = @"
            lowlevel {
                var[\[\]] x = {1, 2, 3};
            }
        ";

        var diagnostics = @"
            collection dimensions on implicit types are inferred making them not necessary in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0045_CannotUseImplicit() {
        var text = @"
            [var] myFunc() { }
        ";

        var diagnostics = @"
            cannot use implicit-typing in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0046_NoCatchOrFinally() {
        var text = @"
            try { [}]
        ";

        var diagnostics = @"
            try statement must have a catch or finally
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0047_MemberMustBeStatic() {
        var text = @"
            static class A {
                void [Test]() { }
            }
        ";

        var diagnostics = @"
            cannot declare instance members in a static class
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0048_ExpectedOverloadableOperator() {
        var text = @"
            class A {
                static A operator[==]() { }
            }
        ";

        var diagnostics = @"
            expected overloadable unary or binary operator
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0049_ReferenceWrongInitialization() {
        var text = @"
            int x = 3;
            ref int y [=] x;
        ";

        var diagnostics = @"
            a by-reference variable must be initialized with a reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0050_WrongInitializationReference() {
        var text = @"
            int x = 3;
            int y [=] ref x;
        ";

        var diagnostics = @"
            cannot initialize a by-value variable with a reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0051_UnknownAttribute() {
        var text = @"
            \[[MyAttrib]\]class A { }
        ";

        var diagnostics = @"
            unknown attribute 'MyAttrib'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0052_NullAssignNotNull() {
        var text = @"
            int! x = [null];
        ";

        var diagnostics = @"
            cannot assign 'null' to a non-nullable variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0053_ImpliedReference() {
        var text = @"
            var x = 3;
            [ref] var y = ref x;
        ";

        var diagnostics = @"
            implicit types infer reference types making the 'ref' keyword not necessary in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0054_ReferenceToConstant() {
        var text = @"
            const int x = 3;
            ref int y [=] ref x;
        ";

        var diagnostics = @"
            cannot assign a reference to a constant to a by-reference variable expecting a reference to a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0055_VoidVariable() {
        var text = @"
            [void] a;
        ";

        var diagnostics = @"
            cannot use void as a type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0056_ExpectedToken() {
        var text = @"
            class [{]
                int num;
            }
        ";

        var diagnostics = @"
            expected identifier
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0057_NoMethodOverload() {
        var text = @"
            void myFunc(int a) { }

            void myFunc(string a) { }

            [myFunc](false);
        ";

        var diagnostics = @"
            no overload for method 'myFunc' matches parameter list
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0058_AmbiguousMethodOverload() {
        var text = @"
            void myFunc(int a) { }

            void myFunc(string a) { }

            [myFunc](null);
        ";

        var diagnostics = @"
            call is ambiguous between 'myFunc(int)' and 'myFunc(string)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0059_CannotIncrement() {
        var text = @"
            [1]++;
        ";

        var diagnostics = @"
            the operand of an increment or decrement operator must be a variable, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0060_InvalidTernaryOperatorUse() {
        var text = @"
            3 [?] 4 : 6;
        ";

        var diagnostics = @"
            ternary operator '?:' is not defined for types 'int', 'int', and 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0061_NoSuchMember() {
        var text = @"
            class MyClass {
                int a;
            }

            var myVar = new MyClass();
            myVar.[b];
        ";

        var diagnostics = @"
            'MyClass' contains no such member 'b'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0062_CannotAssign() {
        var text = @"
            [3] = 45;
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0063_CannotOverloadNested() {
        var text = @"
            void myFunc() {
                void myFunc2(int a) { }

                void [myFunc2](string a) { }
            }
        ";

        var diagnostics = @"
            cannot overload nested functions; nested function 'myFunc2' has already been defined
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0064_ConstantToNonConstantReference() {
        var text = @"
            int x = 3;
            ref const int y [=] ref x;
        ";

        var diagnostics = @"
            cannot assign a reference to a variable to a by-reference variable expecting a reference to a constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0065_InvalidPrefixUse() {
        var text = @"
            bool a = false;
            [++]a;
        ";

        var diagnostics = @"
            prefix operator '++' is not defined for type 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0066_InvalidPostfixUse() {
        var text = @"
            bool a = false;
            a[++];
        ";

        var diagnostics = @"
            postfix operator '++' is not defined for type 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0067_ParameterAlreadySpecified() {
        var text = @"
            Console.Print(x: 2, [x]: 2);
        ";

        var diagnostics = @"
            named argument 'x' cannot be specified multiple times
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0068_DefaultMustBeConstant() {
        var text = @"
            void MyFunc(int a = [Console.Input()]) { }
        ";

        var diagnostics = @"
            default values for parameters must be compile-time constants
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0069_DefaultBeforeNoDefault() {
        var text = @"
            void MyFunc([int a = 3], int b) { }
        ";

        var diagnostics = @"
            all optional parameters must be specified after any required parameters
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0070_ConstantAndVariable() {
        var text = @"
            const [var] x = 3;
        ";

        var diagnostics = @"
            cannot mark a type as both constant and variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0071_VariableUsingTypeName() {
        var text = @"
            class A { }

            A [A] = new A();
        ";

        var diagnostics = @"
            variable name 'A' is not valid as it is the name of a type in this namespace
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0072_CannotImplyNull() {
        var text = @"
            void MyFunc(int a, int! b) { }

            MyFunc(,[]);
        ";

        var diagnostics = @"
            cannot implicitly pass null in a non-nullable context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0073_CannotConvertNull() {
        var text = @"
            [(int!)null];
        ";

        var diagnostics = @"
            cannot convert 'null' to 'int!' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0074_ModifierAlreadyApplied() {
        var text = @"
            const [const] a = 3;
        ";

        var diagnostics = @"
            modifier 'const' has already been applied to this item
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // TODO See BU0091 todo
    // [Fact]
    // public void Reports_Error_BU0075_CannotUseRef() {
    //     var text = @"
    //         class MyClass {
    //             [ref] int myField;
    //         }
    //     ";

    //     var diagnostics = @"
    //         cannot use a reference type in this context
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0076_CannotUseRef() {
        var text = @"
            int myInt = [5 / 0];
        ";

        var diagnostics = @"
            cannot divide by zero
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0077_NameUsedInEnclosingScope() {
        var text = @"
            void MyFunc() {
                for (int [i] = 0; i < 10; i++) ;

                int i = 5;
            }
        ";

        var diagnostics = @"
            a local named 'i' cannot be declared in this scope because that name is used in an enclosing scope to define a local or parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0078_NullInitializerListOnImplicit() {
        var text = @"
            lowlevel {
                var myArray = [{ null, null }];
            }
        ";

        var diagnostics = @"
            cannot initialize an implicitly-typed variable with an initializer list only containing 'null'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0079_UnrecognizedEscapeSequence() {
        var text = @"
            var myString = ""test[\g]"";
        ";

        var diagnostics = @"
            unrecognized escape sequence '\g'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0080_PrimitivesDoNotHaveMembers() {
        var text = @"
            int myInt = 3;
            [myInt.b];
        ";

        var diagnostics = @"
            primitive types do not contain any members
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0081_CannotConstructPrimitive() {
        var text = @"
            var myInt = [new int()];
        ";

        var diagnostics = @"
            type 'int' is a primitive; primitives cannot be created with constructors
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0082_NoTemplateOverload() {
        var text = @"
            class MyClass<int T> { }

            class MyClass<bool T> { }

            var myClass = new [MyClass]<false, false>();
        ";

        var diagnostics = @"
            no overload for template 'MyClass' matches template argument list
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0083_AmbiguousTemplateOverload() {
        var text = @"
            class MyClass<int T> { }

            class MyClass<bool T> { }

            var myClass = new [MyClass]<null>();
        ";

        var diagnostics = @"
            template is ambiguous between 'MyClass<int>' and 'MyClass<bool>'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0084_CannotUseStruct() {
        var text = @"
            [struct] MyStruct { }
        ";

        var diagnostics = @"
            cannot use structs outside of low-level contexts
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0085_CannotUseThis() {
        var text = @"
            int myInt = 3;
            [this].myInt = 5;
        ";

        var diagnostics = @"
            cannot use 'this' outside of a class
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0086_MemberIsInaccessible() {
        var text = @"
            class A {
                private static void M() { }
            }
            A.[M]();
        ";

        var diagnostics = @"
            'A.M()' is inaccessible due to its protection level
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0087_NoConstructorOverload() {
        var text = @"
            class MyClass {
                public constructor(int a) { }

                public constructor(string a) { }
            }

            MyClass myClass = new [MyClass](true);
        ";

        var diagnostics = @"
            type 'MyClass' does not contain a constructor that matches the parameter list
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0088_InvalidModifier() {
        var text = @"
            class MyClass {
                [static] int a;
            }
        ";

        var diagnostics = @"
            modifier 'static' is not valid for this item
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0089_InvalidInstanceReference() {
        var text = @"
            class MyClass {
                public static void MyMethod() { }
            }

            var myClass = new MyClass();
            [myClass.MyMethod]();
        ";

        var diagnostics = @"
            member 'MyMethod' cannot be accessed with an instance reference; qualify it with the type name instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0090_InvalidStaticReference() {
        var text = @"
            class MyClass {
                public void MyMethod() { }
            }

            [MyClass.MyMethod]();
        ";

        var diagnostics = @"
            an object reference is required for non-static member 'MyMethod'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // TODO Add a flag to mark 'text' as low-level to allow struct syntax: the only way to produce this diagnostic
    // [Fact]
    // public void Reports_Error_Unsupported_BU0091_CannotInitializeInStructs() {
    //     var text = @"
    //         struct A {
    //             int num [=] 3;
    //         }
    //     ";

    //     var diagnostics = @"
    //         cannot initialize fields in structure definitions
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0093_InvalidAttributes() {
        var text = @"
            [\[asdf\]]int x = 3;
        ";

        var diagnostics = @"
            attributes are not valid in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0094_TemplateNotExpected() {
        var text = @"
            class A {}
            var a = new A[<3>]();
        ";

        var diagnostics = @"
            item 'A' does not expect any template arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0095_TemplateMustBeConstant() {
        var text = @"
            class A<int a> {}
            var b = 3;
            var a = new A<[b]>();
        ";

        var diagnostics = @"
            template argument must be a compile-time constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0096_CannotReferenceNonField() {
        var text = @"
            var a = ref [3];
        ";

        var diagnostics = @"
            cannot reference non-field or non-variable item
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0097_CannotUseType() {
        var text = @"
            class A { }
            [A];
        ";

        var diagnostics = @"
            'A' is a type, which is not valid in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0098_StaticConstructor() {
        var text = @"
            static class A {
                [constructor]() { }
            }
        ";

        var diagnostics = @"
            static classes cannot have constructors
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0099_StaticVariable() {
        var text = @"
            static class A { }
            [A] a;
        ";

        var diagnostics = @"
            cannot declare a variable with a static type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0100_CannotConstructStatic() {
        var text = @"
            static class A { }
            var a = [new A()];
        ";

        var diagnostics = @"
            cannot create an instance of the static class 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0101_ConflictingModifiers() {
        var text = @"
            class A {
                static [const] int B() {}
            }
        ";

        var diagnostics = @"
            cannot mark member as both static and constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0102_AssignmentInConstMethod() {
        var text = @"
            class A {
                int a = 3;
                const void B() {
                    a[++];
                }
            }
        ";

        var diagnostics = @"
            cannot assign to an instance member in a method marked as constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0103_NonConstantCallInConstant() {
        var text = @"
            class A {
                int a = 3;
                void B() {
                    a++;
                }
                const void C() {
                    [B()];
                }
            }
        ";

        var diagnostics = @"
            cannot call non-constant method 'B()' in a method marked as constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0104_NonConstantCallOnConstant() {
        var text = @"
            class A {
                int a = 3;
                public void B() {
                    a++;
                }
            }
            const a = new A();
            [a.B()];
        ";

        var diagnostics = @"
            cannot call non-constant method 'B()' on constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0105_CannotBeRefAndConstexpr() {
        var text = @"
            int x = 3;
            constexpr [ref] int y = ref x;
        ";

        var diagnostics = @"
            reference type cannot be marked as a constant expression because references are not compile-time constants
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0106_NotConstantExpression() {
        var text = @"
            int Test() { return 3; }
            constexpr int y = [Test()];
        ";

        var diagnostics = @"
            expression is not a compile-time constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0107_CannotReturnStatic() {
        var text = @"
            static class A {}
            [A] Test() { return A; }
        ";

        var diagnostics = @"
            static types cannot be used as return types
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0108_IncorrectOperatorParameterCount() {
        var text = @"
            class A {
                public static A operator[+](A a, A b, A c) { return a; }
            }
        ";

        var diagnostics = @"
            overloaded operator '+' takes 2 parameters
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0109_OperatorMustBePublicAndStatic() {
        var text = @"
            class A {
                A operator[+](A a, A b) { return a; }
            }
        ";

        var diagnostics = @"
            overloaded operators must be marked as public and static
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0110_StaticOperator() {
        var text = @"
            static class A {
                public static A operator[+](A a, A b) { return a; }
            }
        ";

        var diagnostics = @"
            static classes cannot contain operators
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0111_OperatorAtLeastOneClassParameter() {
        var text = @"
            class A {
                public static int operator[+](int a, int b) { return a; }
            }
        ";

        var diagnostics = @"
            at least one of the parameters of an operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0112_OperatorMustReturnClass() {
        var text = @"
            class A {
                public static int operator[++](A a) { return 3; }
            }
        ";

        var diagnostics = @"
            the return type for the '++' or '--' operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0113_IndexOperatorFirstParameter() {
        var text = @"
            class A {
                public static int operator[\[\]](int a, A b) { return 3; }
            }
        ";

        var diagnostics = @"
            the first parameter for the '[]' operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0114_ArrayOutsideOfLowLevelContext() {
        var text = @"
            [int\[\]] a;
        ";

        var diagnostics = @"
            cannot use arrays outside of low-level contexts
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0115_EmptyCharacterLiteral() {
        var text = @"
            char a = [''];
        ";

        var diagnostics = @"
            character literal cannot be empty
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0116_CharacterLiteralTooLong() {
        var text = @"
            char a = ['asdf'];
        ";

        var diagnostics = @"
            character literal cannot be more than one character
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0117_NoInitOnNonNullable() {
        var text = @"
            int! [a];
        ";

        var diagnostics = @"
            non-nullable locals and class fields must have an initializer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0118_CannotBePrivateAndVirtualOrAbstract() {
        var text = @"
            class A {
                virtual void [M]() {}
            }
        ";

        var diagnostics = @"
            virtual or abstract methods cannot be private
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0119_NoSuitableOverrideTarget() {
        var text = @"
            class A {
                public override void [M]() {}
            }
        ";

        var diagnostics = @"
            no suitable method found to override
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0120_OverrideCannotChangeAccessibility() {
        var text = @"
            class A {
                public virtual void M() {}
            }
            class B extends A {
                private override void [M]() {}
            }
        ";

        var diagnostics = @"
            cannot change access modifier of inherited member from 'public' to 'private'; cannot change access modifiers when overriding inherited members
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0121_CannotDerivePrimitive() {
        var text = @"
            class A extends [int] { }
        ";

        var diagnostics = @"
            cannot derive from primitive type 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0122_UnknownTemplate() {
        var text = @"
            class A where { [T] extends Object; } { }
        ";

        var diagnostics = @"
            type 'A' has no such template parameter 'T'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0123_CannotExtendCheckNonType() {
        var text = @"
            class A<int T> where { [T extends Object;] } { }
        ";

        var diagnostics = @"
            template 'T' is not a type; cannot extension check a non-type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0124_ConstraintIsNotConstant() {
        var text = @"
            class A<int T> where { [Hex(T) == ""3"" ? true : false;] } { }
        ";

        var diagnostics = @"
            template constraint is not a compile-time constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0125_StructTakesNoArguments() {
        var text = @"
            lowlevel struct A {}
            var a = new A([3]);
        ";

        var diagnostics = @"
            struct constructors take no arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0126_ExtendConstraintFailed() {
        var text = @"
            class A<type T> where { T extends Object; } { }
            var myA = new A[<int>]();
        ";

        var diagnostics = @"
            template constraint 1 fails ('T extends Object'); 'T' must be or inherit from 'Object'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0127_ConstraintWasNull() {
        var text = @"
            class A<int a, int b> where { a < b; } { }
            var myA = new A[<, >]();
        ";

        var diagnostics = @"
            template constraint 1 fails ('(a < b)'); constraint results in null
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0128_ConstraintFailed() {
        var text = @"
            class A<int a, int b> where { a < b; } { }
            var myA = new A[<3, 2>]();
        ";

        var diagnostics = @"
            template constraint 1 fails ('(a < b)')
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0129_CannotOverride() {
        var text = @"
            class A {
                public void M() { }
            }
            class B extends A {
                public override void [M]() { }
            }
        ";

        var diagnostics = @"
            cannot override inherited method 'M()' because it is not marked virtual or override
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0130_CannotUseGlobalInClass() {
        var text = @"
            A m = new A();
            class A {
                A a = [m];
            }
        ";

        var diagnostics = @"
            cannot use global 'm' in a class definition
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0131_MemberShadowsParent() {
        var text = @"
            class A {
                public void M() {}
            }
            class B extends A {
                public void [M]() {}
            }
        ";

        var diagnostics = @"
            'B.M()' hides inherited member 'A.M()'; use the 'new' keyword if hiding was intended
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0132_ConflictingOverrideModifiers() {
        var text = @"
            class A {
                public virtual void M() {}
            }
            class B extends A {
                public virtual [override] void M() {}
            }
        ";

        var diagnostics = @"
            a member marked as override cannot be marked as new or virtual
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0133_MemberShadowsNothing() {
        var text = @"
            class A {
                public new void [M]() {}
            }
        ";

        var diagnostics = @"
            the member 'A.M()' does not hide a member; the 'new' keyword is unnecessary
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0134_CannotDeriveSealed() {
        var text = @"
            sealed class A { }
            class B extends [A] { }
        ";

        var diagnostics = @"
            cannot derive from sealed type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0135_CannotDeriveStatic() {
        var text = @"
            static class A { }
            class B extends [A] { }
        ";

        var diagnostics = @"
            cannot derive from static type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0136_ExpectedType() {
        var text = @"
            class A {}
            var a = new A() as [3];
        ";

        var diagnostics = @"
            expected type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0137_CannotUseBase() {
        var text = @"
            int myInt = 3;
            [base].myInt = 5;
        ";

        var diagnostics = @"
            cannot use 'base' outside of a class
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }
}
