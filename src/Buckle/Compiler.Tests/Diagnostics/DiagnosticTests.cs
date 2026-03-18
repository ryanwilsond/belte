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
    public void Reports_Warning_BU0002_NullDereference() {
        var text = @"
            class A {
                public int num;
            }

            void MyFunc(A a) {
                [a.num] = 3;
            }
        ";

        var diagnostics = @"
            dereference of a possibly null value
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Error_BU0003_InvalidReference

    [Fact]
    public void Reports_Error_BU0004_InvalidType() {
        var text = @"
            int x = [99999999999999999999];
        ";

        var diagnostics = @"
            '99999999999999999999' is not a valid 'int'
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
            cannot convert from type 'int!' to 'string' implicitly; an explicit conversion exists (are you missing a cast?)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0008_InvalidUnaryOperatorUse() {
        var text = @"
            [-false];
        ";

        var diagnostics = @"
            unary operator '-' is not defined for type 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0009_UnexpectedArrayInit() {
        var text = @"
            int\[\]\[\] a = { [{ 1 }] };
        ";

        var diagnostics = @"
            initializer lists can only be used in a data container or field initializer; try using a new expression instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0010_NamedArgumentTwice() {
        var text = @"
            void F(int x) { }
            F(x: 1, [x]: 3);
        ";

        var diagnostics = @"
            named argument 'x' cannot be specified multiple times
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0011_InvalidBinaryOperatorUse() {
        var text = @"
            [false + 3];
        ";

        var diagnostics = @"
            binary operator '+' is not defined for operands of types 'bool' and 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0012_GlobalStatementInMultipleFiles
    // ! Error_BU0013_NoNamespacePrivate
    // ! Error_BU0014_UnexpectedAliasName

    [Fact]
    public void Reports_Error_BU0015_BadArgumentName() {
        var text = @"
            void Test(string a) { }
            Test([msg]: ""test"");
        ";

        var diagnostics = @"
            the best overload for 'Test' does not have a parameter named 'msg'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0016_MainAndGlobals() {
        var text = @"
            int a = 3;

            void [Main]() { }
        ";

        var diagnostics = @"
            declaring a main method and using global statements creates ambiguous entry point
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
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

    // ! Error_BU0018_ColonColonWithTypeAlias

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
            cannot convert from type 'A!' to 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0021_DuplicateNameInNamespace() {
        var text = @"
            class C { }
            class [C] { }
        ";

        var diagnostics = @"
            the namespace '<global>' already contains a definition for 'C'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0022_DuplicateAlias

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

    // ! Error_BU0024_DuplicateWithGlobalUsing

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

    // ! Error_BU0028_NoAliasHere
    // ! Error_BU0029_BadUsingType

    [Fact]
    public void Reports_Error_BU0030_ImplicitAssignedInitializerList() {
        var text = @"
            [var a = {1, 2, 3}];
        ";

        var diagnostics = @"
            cannot initialize an implicitly-typed data container with an initializer list
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0031_DuplicateUsing

    [Fact]
    public void Reports_Error_BU0032_CannotCallNonMethod() {
        var text = @"
            int x = 3;
            int y = [x]();
        ";

        var diagnostics = @"
            called object is not a method
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
            only assignment, call, throw, and increment expressions can be used as a statement
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    // ! Error_BU0034_BadUsingNamespace

    [Fact]
    public void Reports_Error_BU0035_InvalidBreakOrContinue() {
        var text = @"
            [break;]
        ";

        var diagnostics = @"
            break and continue statements can only be used within a loop
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0035_BadUsingStaticType

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
    public void Reports_Error_BU0039_ArrayInitToNonArrayType() {
        var text = @"
            int a = [{ 1, 2, 3 }];
        ";

        var diagnostics = @"
            can only use array initializer expressions to assign to array types; try using a new expression instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0040_NoInitOnImplicit() {
        var text = @"
            [var x];
        ";

        var diagnostics = @"
            implicitly-typed locals must have initializer
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
            [var x = null];
        ";

        var diagnostics = @"
            cannot assign <null> to an implicitly-typed data container
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0043_ArrayInitExpected

    [Fact]
    public void Reports_Error_BU0044_ArrayInitWrongLength() {
        var text = @"
            lowlevel {
                int\[\] x = new int\[4\] [{1, 2, 3}];
            }
        ";

        var diagnostics = @"
            an array initializer of length '4' is expected
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0045_IncompatibleEntryPointReturn

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
    public void Reports_Error_BU0047_ImplicitlyTypedLocalAssignedBadValue() {
        var text = @"
            [var x = null];
        ";

        var diagnostics = @"
            cannot assign <null> to an implicitly-typed data container
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0048_ExpectedOverloadableOperator() {
        var text = @"
            class A {
                static A operator[.]() { }
            }
        ";

        var diagnostics = @"
            expected overloadable unary, arithmetic, equality, or comparison operator
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0049_InitializeByReferenceWithByValue() {
        var text = @"
            int x = 3;
            [ref int y = x];
        ";

        var diagnostics = @"
            a by-reference data container must be initialized with a reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0050_InitializeByValueWithByReference() {
        var text = @"
            int x = 3;
            [int y = ref x];
        ";

        var diagnostics = @"
            cannot initialize a by-value data container with a reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0051_UnexpectedTokenExpectedAnother() {
        var text = @"
            namespace [{] { }
        ";

        var diagnostics = @"
            unexpected token '{', expected identifier
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0052_ExpectedTokenAtEOF() {
        var text = @"
            namespace A {[]
        ";

        var diagnostics = @"
            expected '}' at end of input
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Warning_BU0053_ImpliedReference
    // ! Error_BU0054_ReferenceToConstant

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
            class A {
                public static void F(int a) { }

                public static void F(string a) { }
            }

            A.[F](3, false);
        ";

        var diagnostics = @"
            no overload for method 'F' takes 2 arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0058_AmbiguousMethodOverload() {
        var text = @"
            class A {
                public static void myFunc(int a) { }

                public static void myFunc(string a) { }
            }

            A.[myFunc](null);
        ";

        var diagnostics = @"
            call is ambiguous between 'A.myFunc(int)' and 'A.myFunc(string)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0059_UnexpectedTokenExpectedOthers() {
        var text = @"
            class A {
                public constructor() : [if]() { }
            }
        ";

        var diagnostics = @"
            unexpected token 'if', expected 'this' or 'base'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0060_ExpectedTokensAtEOF() {
        var text = @"
            class A {
                public constructor() :[[[[[[]]]]]]
        ";

        var diagnostics = @"
            expected 'this' or 'base' at end of input
            expected '(' at end of input
            expected ')' at end of input
            expected '{' at end of input
            expected '}' at end of input
            expected '}' at end of input
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
    public void Reports_Error_BU0062_AssignableLValueExpected() {
        var text = @"
            [3] = 45;
        ";

        var diagnostics = @"
            left side of assignment operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0063_AnnotationsDisallowedInObjectCreation() {
        var text = @"
            class A { }
            var a = [new A!()];
        ";

        var diagnostics = @"
            cannot use a non-nullable annotation in object creation
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0064_ConstantToNonConstantReference
    // [Fact]
    // public void Reports_Error_BU0064_ConstantToNonConstantReference() {
    //     var text = @"
    //         int x = 3;
    //         ref const int y = ref [x];
    //     ";

    //     var diagnostics = @"
    //         cannot assign a reference to a data container to a by-reference data container expecting a reference to a constant
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0065_CannotAnnotateStruct() {
        var text = @"
            struct A { }
            [[A!] a];
        ";

        var diagnostics = @"
            cannot use a non-nullable annotation on a struct type
            non-nullable locals and class fields must have an initializer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0066_IncorrectUnaryOperatorArgs() {
        var text = @"
            class A {
                public static A operator[~](A a, A b, A c) { return a; }
            }
        ";

        var diagnostics = @"
            overloaded unary operator '~' takes 1 parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0067_ParameterAlreadySpecified() {
        var text = @"
            void M(int x) { }
            M(x: 2, [x]: 2);
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
            default parameter value for 'a' must be a compile-time constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0069_DefaultBeforeNoDefault() {
        var text = @"
            void MyFunc(int a = 3, int b[)] { }
        ";

        var diagnostics = @"
            all optional parameters must be specified after any required parameters
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0070_ConstantAndVariable() {
        var text = @"
            const var [x] = 3;
        ";

        var diagnostics = @"
            cannot mark a data container as both constant and variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0071_LocalUsingTypeName() {
        var text = @"
            class A { }

            A [A] = new A();
        ";

        var diagnostics = @"
            local 'A' shares a name with a type in this namespace
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0072_CannotImplyNull() {
        var text = @"
            void MyFunc(int a, int! b) { }

            MyFunc(,[]);
        ";

        var diagnostics = @"
            argument 2: cannot implicitly pass 'null' to a parameter of non-nullable type 'int!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0073_ExpectedOverloadableUnaryOperator() {
        var text = @"
            class A {
                public static A operator[*](A a) { return a; }
            }
        ";

        var diagnostics = @"
            expected overloadable unary operator
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

    [Fact]
    public void Reports_Error_BU0075_CannotUseRef() {
        var text = @"
            var a = typeof([ref] int);
        ";

        var diagnostics = @"
            cannot use a reference type in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0076_DivideByZero() {
        var text = @"
            int myInt = [5 / 0];
        ";

        var diagnostics = @"
            cannot divide by zero
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0077_ExpectedOverloadableBinaryOperator() {
        var text = @"
            class A {
                public static A operator[~](A a, A b) { return a; }
            }
        ";

        var diagnostics = @"
            expected overloadable arithmetic, equality, or comparison operator
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0078_RefReturnScopedParameter2

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
            var myInt = new [int]();
        ";

        var diagnostics = @"
            invalid object creation; cannot construct primitive
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0082_AnnotationsDisallowedInTemplateArgument() {
        var text = @"
            class A<type T> { }
            var a = new A<[int!]>();
        ";

        var diagnostics = @"
            cannot use a non-nullable annotation in template arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0083_OperatorRefParameter() {
        var text = @"
            class A {
                public static A [operator]+(ref A a, A b) { return a; }
            }
        ";

        var diagnostics = @"
            operators cannot have ref parameters
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0084_CannotUseStruct() {
        var text = @"
            struct MyStruct { }
            var a = new [MyStruct]();
        ";

        var diagnostics = @"
            cannot use structs outside of low-level contexts
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0085_CannotUseThis

    [Fact]
    public void Reports_Error_BU0086_MemberIsInaccessible() {
        var text = @"
            class A {
                private static void M() { }
            }
            [A.M]();
        ";

        var diagnostics = @"
            'A.M()' is inaccessible due to its protection level
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    [Fact]
    public void Reports_Error_BU0087_WrongConstructorArgumentCount() {
        var text = @"
            class MyClass {
                public constructor(int a) { }
            }

            MyClass myClass = new [MyClass](3, true);
        ";

        var diagnostics = @"
            type 'MyClass' does not contain a constructor that takes 2 arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0088_InvalidModifier() {
        var text = @"
            class MyClass {
                static int [a];
            }
        ";

        var diagnostics = @"
            modifier 'static' is not valid for this item
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0089_NoInstanceRequired() {
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
    public void Reports_Error_BU0090_InstanceRequired() {
        var text = @"
            class MyClass {
                public void MyMethod() { }
            }

            [MyClass.MyMethod]();
        ";

        var diagnostics = @"
            an object reference is required for non-static member 'MyClass.MyMethod()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0091_CannotInitializeInStructs() {
        var text = @"
            struct A {
                int num [=] 3;
            }
        ";

        var diagnostics = @"
            cannot initialize fields in structure definitions
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0092_MultipleMains() {
        var text = @"
            class P1 {
                public static void [Main]() { }
            }

            class P2 {
                public static void Main() { }
            }

        ";

        var diagnostics = @"
            cannot have multiple 'Main' entry points
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

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
    public void Reports_Error_BU0094_OperatorRefReturn() {
        var text = @"
            class A {
                public static ref A [operator]+(A a, A b) { return null; }
            }
        ";

        var diagnostics = @"
            non-indexing operators cannot return by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0095_RefReturnGlobal() {
        var text = @"
            int a = 3; ref int b = ref a; ref int F() { return ref [b]; }
        ";

        var diagnostics = @"
            cannot return a global by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0096_RefReturnOnlyParameter2
    // ! Error_BU0097_DottedTypeNamesNotFound

    [Fact]
    public void Reports_Error_BU0098_ConstructorInStaticClass() {
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
    public void Reports_Error_BU0099_StaticDataContainer() {
        var text = @"
            static class A { }
            [A a];
        ";

        var diagnostics = @"
            cannot declare a field or data container with a static type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0100_CannotCreateStatic() {
        var text = @"
            static class A { }
            [var] a = [new A()];
        ";

        var diagnostics = @"
            cannot initialize an implicitly-typed data container with the static type 'A!'
            cannot create an instance of the static class 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0101_ConflictingModifiers() {
        var text = @"
            abstract sealed class [A] { }
        ";

        var diagnostics = @"
            cannot mark symbol as both abstract and static
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0102_AssignmentInConstMethod() {
        var text = @"
            class A {
                int a = 3;
                const void B() {
                    [a]++;
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
                    [B]();
                }
            }
        ";

        var diagnostics = @"
            cannot call non-constant method 'A.B()' in a method marked as constant
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
            [a.B]();
        ";

        var diagnostics = @"
            cannot call non-constant method 'A.B()' on constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0105_CannotBeRefAndConstexpr() {
        var text = @"
            int x = 3;
            constexpr ref int [y] = [ref x];
        ";

        var diagnostics = @"
            reference type cannot be marked as a constant expression because references are not compile-time constants
            expected a compile-time constant value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0106_ConstantExpected() {
        var text = @"
            int Test() { return 3; }
            constexpr int y = [Test()];
        ";

        var diagnostics = @"
            expected a compile-time constant value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0107_CannotReturnStatic() {
        var text = @"
            static class A {}
            A [Test]() { return null; }
        ";

        var diagnostics = @"
            'A': static types cannot be used as return types
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0108_IncorrectBinaryOperatorArgs() {
        var text = @"
            class A {
                public static A operator[+](A a, A b, A c) { return a; }
            }
        ";

        var diagnostics = @"
            overloaded binary operator '+' takes 2 parameters
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
    public void Reports_Error_BU0110_OperatorInStaticClass() {
        var text = @"
            static class A {
                public static int operator[+](int a, int b) { return a; }
            }
        ";

        var diagnostics = @"
            static classes cannot contain operators
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0111_RefReturnParameter2
    // ! Error_BU0112_RefReturnScopedParameter
    // ! Error_BU0113_RefReturnOnlyParameter
    // ! Error_BU0114_ArrayOutsideOfLowLevelContext

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
            [int! a];
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
            'A.M()': virtual or abstract methods cannot be private
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0119_RefReturnParameter

    [Fact]
    public void Reports_Error_BU0119_RefReturnParameter() {
        var text = @"
            ref int M(int a) {
                return ref [a];
            }
        ";

        var diagnostics = @"
            cannot return a parameter by reference 'a' because it is not a ref parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0120_EscapeOther

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

    // ! Error_BU0122_UnknownTemplate

    [Fact]
    public void Reports_Error_BU0123_CannotExtendCheckNonType() {
        var text = @"
            class A<int [T]> where { T extends Object; } { }
        ";

        var diagnostics = @"
            template 'T' is not a type; cannot extension check a non-type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0124_ConstraintIsNotConstant() {
        var text = @"
            class A<string a> where { [a == Console.Input()]; } { }
        ";

        var diagnostics = @"
            template constraint is not a compile-time constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0125_RefReturnNonreturnableLocal2

    [Fact]
    public void Reports_Error_BU0126_ExtendConstraintFailed() {
        var text = @"
            class B { }
            class A<type T> where { T extends B; } {}
            var a = new [A<Object>]();
        ";

        var diagnostics = @"
            the type 'Object' must be or derive from 'B' in order to use it as parameter 'T' in the template type or method 'A<type! T>'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0127_ConstraintWasNull() {
        var text = @"
            class A<int a> where { a == 3; } { }
            var a = new [A<null>]();
        ";

        var diagnostics = @"
            template constraint fails: constraint results in null (a == 3)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0128_ConstraintFailed() {
        var text = @"
            class A<int a> where { a == 3; } { }
            var a = new [A<4>]();
        ";

        var diagnostics = @"
            template constraint fails (a == 3)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0129_RefReturnNonreturnableLocal
    // ! Error_BU0130_RefReturnLocal2
    // ! Error_BU0131_RefReturnLocal

    [Fact]
    public void Reports_Error_BU0132_ConflictingOverrideModifiers() {
        var text = @"
            class A {
                public virtual void M() {}
            }
            class B extends A {
                public virtual override void [M]() {}
            }
        ";

        var diagnostics = @"
            'B.M()': a member marked as override cannot be marked as new or virtual
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0133_MismatchedRefEscapeInTernary

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
            class [B] extends A { }
        ";

        var diagnostics = @"
            cannot derive from static type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0136_CallArgMixing
    // ! Error_BU0137_CannotUseBase

    [Fact]
    public void Reports_Error_BU0138_CannotCreateAbstract() {
        var text = @"
            abstract class A { }
            var a = [new A()];
        ";

        var diagnostics = @"
            cannot create an instance of the abstract class 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0139_NonAbstractMustHaveBody() {
        var text = @"
            class A {
                void [M]();
            }
        ";

        var diagnostics = @"
            'A.M()' must declare a body because it is not marked abstract
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0140_AbstractCannotHaveBody() {
        var text = @"
            abstract class A {
                public abstract void [M]() { }
            }
        ";

        var diagnostics = @"
            'A.M()' cannot declare a body because it is marked abstract
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0141_AbstractInNonAbstractType() {
        var text = @"
            class A {
                public abstract void [M]();
            }
        ";

        var diagnostics = @"
            'A.M()' is abstract but it is contained in non-abstract type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0142_TypeDoesNotImplementAbstract() {
        var text = @"
            abstract class A {
                public abstract void M();
            }
            class [B] extends A { }
        ";

        var diagnostics = @"
            'B' does not implement inherited abstract member 'A.M()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0143_OperatorNeedsMatch() {
        var text = @"
            class A {
                public static bool [operator]==(A x, A y) {
                    return true;
                }
            }
        ";

        var diagnostics = @"
            the operator A.op_Equality(A, A) requires a matching operator '!=' to also be defined
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0144_InvalidExpressionTerm() {
        var text = @"
            + [ref x];
        ";

        var diagnostics = @"
            invalid expression term 'ref'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0145_UnexpectedTemplateName

    [Fact]
    public void Reports_Error_BU0146_MultipleAccessibilities() {
        var text = @"
            public private class [A] {}
        ";

        var diagnostics = @"
            cannot apply multiple accessibility modifiers
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0147_CircularConstraint() {
        var text = @"
            class A<type T, [type T2]> where { T extends T2; T2 extends T; } { }
        ";

        var diagnostics = @"
            template parameters 'T' and 'T2' form a circular constraint
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0148_TemplateObjectBaseWithPrimitiveBase() {
        var text = @"
            class A<[type T], type T2> where { T2 is primitive; T extends T2; } { }
        ";

        var diagnostics = @"
            template parameter 'T2' cannot be used as a constraint for template parameter 'T'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0149_TemplateObjectBaseWithPrimitiveBase() {
        var text = @"
            class B { }
            class C { }
            class A<[type T]> where { T extends B; T extends C; } { }
        ";

        var diagnostics = @"
            template parameter 'T' cannot be constrained to both types 'C' and 'B'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0150_TemplateBaseBothObjectAndPrimitive() {
        var text = @"
            class A<[type T]> where { T is primitive; T extends Object; } { }
        ";

        var diagnostics = @"
            template parameter 'T' cannot be constrained as both an object type and a primitive type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0151_MemberNameSameAsType() {
        var text = @"
            class A {
                class [A] { }
            }
        ";

        var diagnostics = @"
            cannot declare a member with the same name as the enclosing type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0152_CircularBase() {
        var text = @"
            class [A] extends A { }
        ";

        var diagnostics = @"
            circular base dependency involving 'A' and 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0153_InconsistentAccessibilityClass() {
        var text = @"
            private class A { }
            public class [B] extends A { }
        ";

        var diagnostics = @"
            inconsistent accessibility: class 'A' is less accessible than class 'B'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0154_StaticDeriveFromNotObject() {
        var text = @"
            class A { }
            static class B extends [A] { }
        ";

        var diagnostics = @"
            static class 'B' cannot derive from type 'A'; static classes must derive from Object
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0155_CannotDeriveTemplate

    [Fact]
    public void Reports_Error_BU0156_InconsistentAccessibilityField() {
        var text = @"
            class A {
                private class B { }
                public [B f];
            }
        ";

        var diagnostics = @"
            inconsistent accessibility: type 'A.B' is less accessible than field 'A.f'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0157_InconsistentAccessibilityOperatorReturn() {
        var text = @"
            class A {
                private class B { }
                public static B [operator]+(A a, A b) { return null; }
            }
        ";

        var diagnostics = @"
            inconsistent accessibility: return type 'A.B' is less accessible than operator 'A.op_Addition(A, A)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0158_InconsistentAccessibilityReturn() {
        var text = @"
            class A {
                private class B { }
                public static B [F]() { return null; }
            }
        ";

        var diagnostics = @"
            inconsistent accessibility: return type 'A.B' is less accessible than method 'A.F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0159_InconsistentAccessibilityOperatorParameter() {
        var text = @"
            class A {
                private class B { }
                public static A [operator]+(B b, A a) { return null; }
            }
        ";

        var diagnostics = @"
            inconsistent accessibility: parameter type 'A.B' is less accessible than operator 'A.op_Addition(A.B, A)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0160_InconsistentAccessibilityParameter() {
        var text = @"
            class A {
                private class B { }
                public static void [F](B b) { }
            }
        ";

        var diagnostics = @"
            inconsistent accessibility: parameter type 'A.B' is less accessible than method 'A.F(A.B)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0161_NoSuitableEntryPoint() {
        var text = @"[]";

        var diagnostics = @"
            no suitable entry point found
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    [Fact]
    public void Reports_Error_BU0162_ArrayOfStaticType() {
        var text = @"
            static class A { }
            var a = new [A]\[\] {};
        ";

        var diagnostics = @"
            array elements cannot be of static type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0163_LocalUsedBeforeDeclarationAndHidesField() {
        var text = @"
            class A {
                int a;
                void F() {
                    int b = [a] + 3;
                    int a = 7;
                }
            }
        ";

        var diagnostics = @"
            cannot use local 'a' before it is declared; 'a' hides the field 'A.a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0164_LocalUsedBeforeDeclaration() {
        var text = @"
            class A {
                void F() {
                    int b = [a] + 3;
                    int a = 7;
                }
            }
        ";

        var diagnostics = @"
            cannot use local 'a' before it is declared
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0165_CannotUseThisInStaticMethod() {
        var text = @"
            class A {
                static A F() {
                    return [this];
                }
            }
        ";

        var diagnostics = @"
            cannot use 'this' in a static method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0166_CannotUseBaseInStaticMethod() {
        var text = @"
            class A {
                static Object F() {
                    return [base];
                }
            }
        ";

        var diagnostics = @"
            cannot use 'base' in a static method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0167_AmbiguousReference
    // ! Error_BU0168_AmbiguousMember
    // ! Error_BU0169_InvalidProtectedAccess

    [Fact]
    public void Reports_Error_BU0170_CannotInitializeVarWithStaticClass() {
        var text = @"
            static class A { }
            [var] a = [new A()];
        ";

        var diagnostics = @"
            cannot initialize an implicitly-typed data container with the static type 'A!'
            cannot create an instance of the static class 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0171_MustNotHaveRefReturn() {
        var text = @"
            class A {
                int f;
                int F() {
                    [return] ref f;
                }
            }
        ";

        var diagnostics = @"
            cannot return by-reference in a method without a reference return type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0172_MustHaveRefReturn() {
        var text = @"
            class A {
                int f;
                ref int F() {
                    [return] f;
                }
            }
        ";

        var diagnostics = @"
            must return by-reference in a method with a reference return type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0173_NoImplicitConversion

    [Fact]
    public void Reports_Error_BU0174_MethodGroupCannotBeUsedAsValue() {
        var text = @"
            int F() { }
            var a = [F];
        ";

        var diagnostics = @"
            method group '[ F() 1 ]' cannot be used as a value
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    [Fact]
    public void Reports_Error_BU0175_LocalShadowsParameter() {
        var text = @"
            void F(int a) {
                int [a] = 3;
            }
        ";

        var diagnostics = @"
            cannot declare a local with the name 'a' because that name is already used by a parameter in an enclosing scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0176_ParameterOrLocalShadowsTemplateParameter

    [Fact]
    public void Reports_Error_BU0177_LocalAlreadyDeclared() {
        var text = @"
            void F() {
                int a = 3;
                int [a] = 6;
            }
        ";

        var diagnostics = @"
            a local or local function with the name 'a' has already been declared in this scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0178_CannotConvertArgument() {
        var text = @"
            void F(int a) { }
            F([true]);
        ";

        var diagnostics = @"
            argument 1: cannot convert from type 'bool!' to 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0179_CannotConvertImplicitlyNullable() {
        var text = @"
            int a = 3;
            int! b = [a];
        ";

        var diagnostics = @"
            cannot convert from type 'int' to 'int!' implicitly; an explicit conversion exists (are you missing a cast?)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0180_NeverGivenType() {
        var text = @"
            bool b = [3 is Object];
        ";

        var diagnostics = @"
            the given expression is never of the provided type ('Object')
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0181_AmbiguousBinaryOperator() {
        var text = @"
            class A {
                public static A operator+(A a, B b) { return a; }
            }
            class B {
                public static B operator+(A a, B b) { return b; }
            }
            var a = new A();
            var b = new B();
            var c = [a + b];
        ";

        var diagnostics = @"
            binary operator '+' is ambiguous for operands with types 'A' and 'B'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0182_ProgramLocalReferencedOutsideOfTopLevelStatement() {
        var text = @"
            int a = 3;
            class A {
                int f = [a];
            }
        ";

        var diagnostics = @"
            cannot reference synthesized program local 'a' outside of top level statements
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    [Fact]
    public void Reports_Error_BU0183_ValueCannotBeNull() {
        var text = @"
            int! a = [null];
        ";

        var diagnostics = @"
            cannot convert null to 'int!' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0183_ValueCannotBeNull2() {
        var text = @"
            class A<type T> where { T is notnull; } { public T C() { return [null]; } }
        ";

        var diagnostics = @"
            cannot convert null to 'T' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0184_InvalidObjectCreation
    // ! Error_BU0185_AmbiguousUnaryOperator
    // ! Error_BU0186_RefConditionalNeedsTwoRefs

    [Fact]
    public void Reports_Error_BU0187_NullAssertAlwaysThrows() {
        var text = @"
            [null!];
        ";

        var diagnostics = @"
            cannot perform a 'not null' assertion on an expression with constant value 'null'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0188_NullAssertOnNonNullableType() {
        var text = @"
            [3!];
        ";

        var diagnostics = @"
            cannot perform a 'not null' assertion on an expression with type 'int!' as it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0189_CannotConvertToStatic() {
        var text = @"
            static class A { }
            Object a;
            [(A)a];
        ";

        var diagnostics = @"
            cannot cast to static type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0190_ArraySizeInDeclaration
    // ! Error_BU0191_ListNoTargetType

    [Fact]
    public void Reports_Error_BU0192_InstanceRequiredInFieldInitializer() {
        var text = @"
            class A {
                int a = [F]();
                int F() { return 3; }
            }
        ";

        var diagnostics = @"
            a field initializer cannot reference non-static member 'A.F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0193_ArgumentExtraRef
    // ! Error_BU0194_ArgumentWrongRef

    [Fact]
    public void Reports_Error_BU0195_NoCorrespondingArgument() {
        var text = @"
            void F(int a, int b) { }
            [F](a: 3);
        ";

        var diagnostics = @"
            there is no argument given that corresponds to the required parameter 'b' of 'F(int, int)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0196_BadNonTrailingNamedArgument() {
        var text = @"
            void F(int a, int b) { }
            F([b]: 3, 3);
        ";

        var diagnostics = @"
            named argument 'b' is used out-of-position but is followed by an unnamed argument
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0197_NamedArgumentUsedInPositional() {
        var text = @"
            void F(int a, int b) { }
            F(3, [a]: 5);
        ";

        var diagnostics = @"
            named argument 'a' specifies a parameter for which a positional argument has already been given
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0198_PossibleMistakenEmptyStatement() {
        var text = @"
            if (true) [;]
        ";

        var diagnostics = @"
            possible mistaken empty statement
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0199_BadEmbeddedStatement() {
        var text = @"
            if (true) [int a = 3;]
        ";

        var diagnostics = @"
            embedded statement cannot be a declaration
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0200_IncrementableLValueExpected() {
        var text = @"
            void F() {}
            [F()]++;
        ";

        var diagnostics = @"
            left side of increment or decrement operation must be a variable, parameter, field, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0201_RefLocalOrParameterExpected
    // ! Error_BU0202_RefLValueExpected

    [Fact]
    public void Reports_Error_BU0203_RefReturnLValueExpected() {
        var text = @"
            int F() { return 3; }
            ref int G() { return ref [F()]; }
        ";

        var diagnostics = @"
            an expression cannot be used in this context because it may not be passed or returned by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0204_InternalError

    [Fact]
    public void Reports_Error_BU0205_BadSKKnown() {
        var text = @"
            class A { }
            [A];
        ";

        var diagnostics = @"
            'A' is a type but is used like a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0206_NonInvocableMemberCalled() {
        var text = @"
            class A {
                constexpr int f;
            }
            [A.f]();
        ";

        var diagnostics = @"
            non-invocable member 'A.f' cannot be used like a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0207_BadSKUnknown
    // ! Error_BU0208_RefConstLocal
    // ! Error_BU0209_RefReturnThis
    // TODO Do we want this following case to be an error:
    // ! Error_BU0210_ConstantAssignmentThis
    // ! Error_BU0211_ReturnNotLValue
    // ! Error_BU0212_RefConstNotField
    // ! Error_BU0213_RefReturnConstNotField
    // ! Error_BU0214_ConstantAssignmentNotField
    // ! Error_BU0215_RefReturnConstNotField2
    // ! Error_BU0216_RefConstNotField2
    // ! Error_BU0217_ConstantAssignmentNotField2

    [Fact]
    public void Reports_Error_BU0218_RefReturnConstant() {
        var text = @"
            class A {
                const int x = 3;
                ref int F() {
                    return ref [x];
                }
            }
        ";

        var diagnostics = @"
            a constant field cannot be returned by writable reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0219_RefConstant() {
        var text = @"
            class A {
                const int x = 3;
                void F() {
                    G(ref [x]);
                }
                void G(ref int a) { }
            }
        ";

        var diagnostics = @"
            a constant field cannot be used as a ref value (except in a constructor)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0220_AssignmentConstantField() {
        var text = @"
            class A {
                const int x = 3;
                void F() {
                    [x] = 7;
                }
            }
        ";

        var diagnostics = @"
            a constant field cannot be assigned to (except in a constructor)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0221_RefReturnConstantStatic
    // ! Error_BU0222_RefConstantStatic
    // ! Error_BU0223_AssignmentConstantStatic
    // ! Error_BU0224_RefReturnConstant2
    // ! Error_BU0225_RefConstant2
    // ! Error_BU0226_AssignmentConstantField2
    // ! Error_BU0227_RefReturnConstantStatic2
    // ! Error_BU0228_RefConstantStatic2
    // ! Error_BU0229_AssignmentConstantStatic2
    // ! Error_BU0230_RefConstantLocalCause
    // ! Error_BU0231_AssignmentConstantLocalCause
    // ! Error_BU0232_PossibleBadNegativeCast
    // ! Error_BU0233_RefReturnMustHaveIdentityConversion
    // ! Error_BU0234_RefAssignmentMustHaveIdentityConversion
    // ! Error_BU0235_LocalSameNameAsTemplate

    [Fact]
    public void Reports_Error_BU0236_DuplicateParameterName() {
        var text = @"
            void F(int a, int [a]) {}
        ";

        var diagnostics = @"
            the parameter name 'a' is a duplicate
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0237_RecursiveConstructorCall() {
        var text = @"
            class A { constructor() : [this]() {} }
        ";

        var diagnostics = @"
            constructor 'A..ctor()' cannot call itself
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0238_NewTemplateWithArguments

    [Fact]
    public void Reports_Warning_BU0239_IncorrectBooleanAssignment() {
        var text = @"
            var a = true;
            if ([a = false]) { }
        ";

        var diagnostics = @"
            assignment in conditional expression is always constant; did you mean to use '==' instead of '=' ?
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Error_BU0240_LookupInTemplateVariable

    [Fact]
    public void Reports_Error_BU0241_AbstractBaseCall() {
        var text = @"
            abstract class A { public abstract void F(); }
            class B extends A {
                public override void F() { }
                void G() {
                    [base.F]();
                }
            }
        ";

        var diagnostics = @"
            cannot call abstract base member 'A.F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0242_StaticMemberInObjectInitializer
    // ! Warning_BU0243_RefConstNotVariable
    // ! Warning_BU0244_ArgExpectedRef
    // ! Error_BU0245_RefConditionalDifferentTypes
    // ! Error_BU0246_DuplicateTemplateParameter
    // ! Warning_BU0247_TemplateParameterSameAsOuterMethod
    // ! Warning_BU0248_TemplateParameterSameAsOuter

    [Fact]
    public void Reports_Error_BU0249_RefDefaultValue() {
        var text = @"
            void F([ref] int a = 3) { }
        ";

        var diagnostics = @"
            a ref parameter cannot have a default value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0250_NoCastForDefaultParameter() {
        var text = @"
            void F(bool [a] = ""Test"") { }
        ";

        var diagnostics = @"
            a value of type 'string!' cannot be used as a default parameter because there are no casts to type 'bool'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0251_NotNullRefDefaultParameter

    [Fact]
    public void Reports_Warning_BU0252_DefaultValueNoEffect() {
        var text = @"
            class A {
                public static A operator+(A a, int [b] = 3) { return a; }
            }
        ";

        var diagnostics = @"
            the default value specified for parameter 'b' will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Warning_BU0253_RefConstParameterDefaultValue
    // ! Error_BU0254_InvalidRefParameter
    // ! Error_BU0255_RefConstWrongOrder

    [Fact]
    public void Reports_Error_BU0256_ParameterIsStatic() {
        var text = @"
            static class A { }
            void F([A] a) { }
        ";

        var diagnostics = @"
            'A': static types cannot be used as parameters
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0257_CircularConstantValue() {
        var text = @"
            constexpr int a = [[a]];
        ";

        var diagnostics = @"
            the evaluation of the constant value for 'a' involves a circular definition
            expected a compile-time constant value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0258_DuplicateNameInClass() {
        var text = @"
            class A {
                int a;
                int [a];
            }
        ";

        var diagnostics = @"
            the type 'A' already contains a definition for 'a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0259_OverloadRefKind

    [Fact]
    public void Reports_Error_BU0260_ConstructorAlreadyExists() {
        var text = @"
            class A {
                constructor() {}
                [constructor]() {}
            }
        ";

        var diagnostics = @"
            type 'A' already defines a constructor with the same parameter types
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0261_MemberAlreadyExists() {
        var text = @"
            class A {
                void F() {}
                void [F]() {}
            }
        ";

        var diagnostics = @"
            type 'A' already defines a member called 'F' with the same parameter types
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0262_ProtectedInStatic() {
        var text = @"
            static class A {
                protected static void [F]() {}
            }
        ";

        var diagnostics = @"
            'A.F()': static classes cannot contain protected members
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0263_EqualsWithoutGetHashCode() {
        var text = @"
            class [A] {
                public override bool! Equals(Object o) { return true; }
            }
        ";

        var diagnostics = @"
            'A' overrides 'Object.Equals(Object)' but does not override 'Object.GetHashCode()'
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0264_EqualityOpWithoutEquals() {
        var text = @"
            class [[A]] {
                public static bool operator==(A a, A b) { return true; }
                public static bool operator!=(A a, A b) { return false; }
            }
        ";

        var diagnostics = @"
            'A' defines operator == or operator != but does not override 'Object.Equals(Object)'
            'A' defines operator == or operator != but does not override 'Object.GetHashCode()'
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0265_EqualityOpWithoutGetHashCode() {
        var text = @"
            class [[A]] {
                public static bool operator==(A a, A b) { return true; }
                public static bool operator!=(A a, A b) { return false; }
            }
        ";

        var diagnostics = @"
            'A' defines operator == or operator != but does not override 'Object.Equals(Object)'
            'A' defines operator == or operator != but does not override 'Object.GetHashCode()'
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0266_SealedNonOverride() {
        var text = @"
            class A {
                sealed void [F]();
            }
        ";

        var diagnostics = @"
            'A.F()' cannot be sealed because it is not an override
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0267_AbstractAndSealed() {
        var text = @"
            class A {
                public sealed abstract void [F]();
            }
        ";

        var diagnostics = @"
            'A.F()' cannot be both abstract and sealed
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0268_AbstractAndVirtual() {
        var text = @"
            class A {
                public virtual abstract void [F]();
            }
        ";

        var diagnostics = @"
            the abstract method 'A.F()' cannot be marked virtual
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0269_StaticAndConst() {
        var text = @"
            class A {
                public static const void [F]() { }
            }
        ";

        var diagnostics = @"
            the static member 'A.F()' cannot be marked 'const'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0270_VirtualInSealedType() {
        var text = @"
            sealed class A {
                public virtual void [F]() { }
            }
        ";

        var diagnostics = @"
            'A.F()' is a new virtual member in sealed type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0271_InstanceMemberInStatic() {
        var text = @"
            static class A {
                void [F]() { }
            }
        ";

        var diagnostics = @"
            'A.F()': cannot declare instance members in a static class
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0272_ProtectedInSealed() {
        var text = @"
            sealed class A {
                protected void [F]() { }
            }
        ";

        var diagnostics = @"
            'A.F': new protected member declared in sealed type; no different than private
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0273_NewRequired() {
        var text = @"
            class A {
                public void F() { }
            }
            class B extends A {
                public void [F]() { }
            }
        ";

        var diagnostics = @"
            'B.F()' hides inherited member 'A.F()'; use the new keyword if hiding was intended
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0274_NewNotRequired() {
        var text = @"
            class A {
                new void [F]() { }
            }
        ";

        var diagnostics = @"
            the member 'A.F()' does not hide an accessible member; the new keyword is not required
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Error_BU0275_HidingAbstractMember

    [Fact]
    public void Reports_Warning_BU0276_NewOrOverrideExpected() {
        var text = @"
            class A {
                public virtual void F() { }
            }
            class B extends A {
                public void [F]() { }
            }
        ";

        var diagnostics = @"
            'B.F()' hides inherited member 'A.F()'; to make the current member override that implementation, add the override keyword; otherwise add the new keyword
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Error_BU0277_HidingDifferentRefness
    // ! Error_BU0278_CantOverrideNonMethod

    [Fact]
    public void Reports_Error_BU0279_OverrideNotExpected() {
        var text = @"
            class A {
                public override void [F]() {}
            }
        ";

        var diagnostics = @"
            'A.F()': no suitable method found to override
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0280_AmbiguousOverride

    [Fact]
    public void Reports_Error_BU0281_CantOverrideNonVirtual() {
        var text = @"
            class A {
                public void F() {}
            }
            class B extends A {
                public override void [F]() { }
            }
        ";

        var diagnostics = @"
            'B.F()': cannot override inherited member 'A.F()' because it is not marked virtual, abstract, or override
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0282_CantOverrideSealed() {
        var text = @"
            class A {
                public virtual void F() {}
            }
            class B extends A {
                public sealed override void F() { }
            }
            class C extends B {
                public override void [F]() { }
            }
        ";

        var diagnostics = @"
            'C.F()': cannot override inherited member 'B.F()' because it is sealed
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0283_CantChangeAccessOnOverride() {
        var text = @"
            class A {
                public virtual void F() {}
            }
            class B extends A {
                protected override void [F]() { }
            }
        ";

        var diagnostics = @"
            'B.F()': cannot change access modifiers when overriding 'public' inherited member 'A.F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0284_CantChangeRefReturnOnOverride

    [Fact]
    public void Reports_Error_BU0285_CantChangeReturnTypeOnOverride() {
        var text = @"
            class A {
                public virtual void F() {}
            }
            class B extends A {
                public override int [F]() { return 3; }
            }
        ";

        var diagnostics = @"
            'B.F()': return type must be 'void' to match overridden member 'A.F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Warning_BU0286_OverridingDifferentRefness
    // ! Warning_BU0287_TopLevelNullabilityMismatchInParameterTypeOnOverride
    // ! Warning_BU0288_NullabilityMismatchInParameterTypeOnOverride
    // ! Warning_BU0289_TopLevelNullabilityMismatchInReturnTypeOnOverride
    // ! Warning_BU0290_NullabilityMismatchInReturnTypeOnOverride
    // ! Fatal_BU0291_LibraryErrors

    [Fact]
    public void Reports_Error_BU0292_OperatorCantReturnVoid() {
        var text = @"
            class A {
                public static void [operator]+(A a, A b) { }
            }
        ";

        var diagnostics = @"
            user-defined operators cannot return void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0293_BadUnaryOperatorSignature() {
        var text = @"
            class A {
                public static A [operator]+(int a) { return null; }
            }
        ";

        var diagnostics = @"
            the parameter of a unary operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0294_BadAbstractUnaryOperatorSignature

    [Fact]
    public void Reports_Error_BU0295_BadShiftOperatorSignature() {
        var text = @"
            class A {
                public static A [operator]<<(int a, int b) { return null; }
            }
        ";

        var diagnostics = @"
            the first operand of an overloaded shift operator must have the same type as the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0296_BadAbstractShiftOperatorSignature

    [Fact]
    public void Reports_Error_BU0297_BadBinaryOperatorSignature() {
        var text = @"
            class A {
                public static A [operator]+(int a, int b) { return null; }
            }
        ";

        var diagnostics = @"
            one of the parameters of a binary operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0298_BadAbstractBinaryOperatorSignature
    // ! Error_BU0299_BadAbstractEqualityOperatorSignature

    [Fact]
    public void Reports_Error_BU0300_BadIncrementOperatorSignature() {
        var text = @"
            class A {
                public static A [operator]++(int a) { return null; }
            }
        ";

        var diagnostics = @"
            the parameter type for ++ or -- operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0301_BadAbstractIncrementOperatorSignature

    [Fact]
    public void Reports_Error_BU0302_BadIncrementReturnType() {
        var text = @"
            class A {
                public static int [operator]++(A a) { return null; }
            }
        ";

        var diagnostics = @"
            the return type for ++ or -- operator must match the parameter type or be derived from the parameter type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0303_BadAbstractIncrementReturnType

    [Fact]
    public void Reports_Error_BU0304_BadIndexCount() {
        var text = @"
            var a = new int\[\] {1, 2, 3};
            [a\[2,3\]];
        ";

        var diagnostics = @"
            wrong number of indices inside []; expected 1
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0305_MultipleUpdates
    // ! Error_BU0306_SeparateMainAndUpdate

    [Fact]
    public void Reports_Error_BU0307_FieldsCannotBeImplicitlyTyped() {
        var text = @"
            class A {
                [var] a = 3;
            }
        ";

        var diagnostics = @"
            fields cannot be implicitly typed
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0308_NonIntArraySize() {
        var text = @"
            var a = new int[\[true\]];
        ";

        var diagnostics = @"
            array sizes must be of type 'int!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0309_BadArity() {
        var text = @"
            class A<type t> { }
            var a = new [A]();
        ";

        var diagnostics = @"
            the template type 'A<type! t>' requires 1 template arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0309_BadArity2() {
        var text = @"
            class A<int t> { }
            var a = new [A]();
        ";

        var diagnostics = @"
            the template type 'A<int t>' requires 1 template arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0310_ProtectedInStruct() {
        var text = @"
            struct A {
                protected [int f];
            }
        ";

        var diagnostics = @"
            'A': protected member declared in struct
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0311_EscapeCall
    // ! Error_BU0312_EscapeCall2
    // ! Error_BU0313_EscapeLocal
    // ! Error_BU0314_RefAssignReturnOnly
    // ! Error_BU0315_RefAssignNarrower
    // ! Error_BU0316_RefAssignValEscapeWider

    [Fact]
    public void Reports_Error_BU0317_MissingArraySize() {
        var text = @"
            var a = new int[\[\]];
        ";

        var diagnostics = @"
            array creation must have array size or array initializer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0318_GlobalUsingInNamespace
    // ! Error_BU0319_AliasNotFound
    // ! Error_BU0320_SingleTypeNameNotFound

    [Fact]
    public void Reports_Warning_BU0321_NamespaceNameShadowsBelte() {
        var text = @"
            namespace [Belte] { }
        ";

        var diagnostics = @"
            namespace 'Belte' potentially shadows parts of the Standard Library
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Error_BU0322_GlobalSingleTypeNameNotFound
    // ! Error_BU0323_DottedTypeNamesNotFoundInNamespace
    // ! Error_BU0324_ConflictingAliasAndMember
    // ! Error_BU0325_UnexpectedUnboundTemplateName
    // ! Error_BU0326_HasNoTemplate
    // ! Error_BU0327_TemplateNotAllowed

    [Fact]
    public void Reports_Error_BU0328_BadTemplateArgument() {
        var text = @"
            class A<type T> {}
            var a = new [A<void>]();
        ";

        var diagnostics = @"
            the type 'void' may not be used as a type argument
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0329_TemplateIsStatic() {
        var text = @"
            static class S { }
            class A<type T> {}
            var a = new [A<S>]();
        ";

        var diagnostics = @"
            'S': static types cannot be used as type arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0330_ObjectConstraintFailed() {
        var text = @"
            class A<type T> where { T extends Object; } {}
            var a = new [A<int>]();
        ";

        var diagnostics = @"
            the type 'int' must be an object type in order to use it as parameter 'T' in the template type or method 'A<type! T>'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0331_PrimitiveConstraintFailed() {
        var text = @"
            class A<type T> where { T is primitive; } {}
            var a = new [A<Object>]();
        ";

        var diagnostics = @"
            the type 'Object' must be a primitive type in order to use it as parameter 'T' in the template type or method 'A<type! T>'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0332_NotNullableConstraintFailed

    [Fact]
    public void Reports_Error_BU0333_DuplicateConstraint() {
        var text = @"
            class A<type T> where { T is notnull; [T is notnull;] } { }
        ";

        var diagnostics = @"
            duplicate constraint on template parameter 'T'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0334_CannotIsCheckNonType() {
        var text = @"
            class A<int [T]> where { T is primitive; } { }
        ";

        var diagnostics = @"
            template 'T' is not a type; cannot is check a non-type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0335_CannotPassGlobalByRef() {
        var text = @"
            ref int F(ref int a) { return ref a; } int b = 3; F([ref b]) = 6; return b;
        ";

        var diagnostics = @"
            cannot pass a global by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }
}
