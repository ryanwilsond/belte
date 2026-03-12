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

    // ! Error_BU0009

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
    // ! Error_BU0030
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

    [Fact]
    public void Reports_Error_BU0064_ConstantToNonConstantReference() {
        var text = @"
            int x = 3;
            ref const int y = ref [x];
        ";

        var diagnostics = @"
            cannot assign a reference to a data container to a by-reference data container expecting a reference to a constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

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

    // ! Error_BU0082_NoTemplateOverload
    // ! Error_BU0083_AmbiguousTemplateOverload

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

    // ! Error_BU0094_TemplateNotExpected
    // ! Error_BU0095_TemplateMustBeConstant
    // ! Error_BU0096_RefReturnOnlyParameter2

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

    // ! Error_BU0100_CannotCreateStatic

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
                public static A operator[+](A a, A b) { return a; }
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
    // ! Error_BU0123_CannotExtendCheckNonType
    // ! Error_BU0124_ConstraintIsNotConstant
    // ! Error_BU0125_RefReturnNonreturnableLocal2
    // ! Error_BU0126_ExtendConstraintFailed
    // ! Error_BU0127_ConstraintWasNull
    // ! Error_BU0128_ConstraintFailed
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
}
