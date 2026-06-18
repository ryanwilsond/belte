using Xunit;
using Xunit.Abstractions;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests.Diagnostics;

/// <summary>
/// At least one test per diagnostic (any severity) if testable. If not testable, an explanation as to why is given.
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
    public void Reports_Error_BU0002_NullableReceiver() {
        var text = @"
            class A {
                public int? num;
            }

            void MyFunc(A? a) {
                [a.num] = 3;
            }
        ";

        var diagnostics = @"
            cannot access fields through a nullable receiver; consider using a null assert or conditional access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0003_InvalidReference
    // Requires command-line arguments

    [Fact]
    public void Reports_Error_BU0004_InvalidType() {
        var text = @"
            int? x = [99999999999999999999];
        ";

        var diagnostics = @"
            '99999999999999999999' is not a valid 'int'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0005_BadCharacter() {
        var text = @"
            [`];
        ";

        var diagnostics = @"
            unexpected character '`'
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
            string? x = [3];
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'string?' implicitly; an explicit conversion exists (are you missing a cast?)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0008_InvalidUnaryOperatorUse() {
        var text = @"
            [-false];
        ";

        var diagnostics = @"
            unary operator '-' is not defined for type 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0009_UnexpectedArrayInit() {
        var text = @"
            var a = [{ null }];
        ";

        var diagnostics = @"
            cannot infer array type from initializer list; try using a new expression instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0010_NamedArgumentTwice() {
        var text = @"
            void F(int? x) { }
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
            binary operator '+' is not defined for operands of types 'bool!' and 'int!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0012_GlobalStatementInMultipleFiles
    // Requires multiple files

    [Fact]
    public void Reports_Error_BU0013_NoNamespacePrivate() {
        var text = @"
            namespace A {
                private class [P] { }
            }
        ";

        var diagnostics = @"
            members defined in a namespace cannot be explicitly declared as private or protected
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0014_UnexpectedAliasName() {
        var text = @"
            using B = A;

            namespace A {
                namespace C { }
            }

            namespace [B::C] { }

            int? a = 3;
        ";

        var diagnostics = @"
            unexpected use of an aliased name
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0015_BadArgumentName() {
        var text = @"
            void Test(string? a) { }
            Test([msg]: ""test"");
        ";

        var diagnostics = @"
            the best overload for 'Test' does not have a parameter named 'msg'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0015_BadArgumentName2() {
        var text = @"
            void(int)? F;
            F([a]: 3);
        ";

        var diagnostics = @"
            the best overload for 'F' does not have a parameter named 'a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0016_MainAndGlobals() {
        var text = @"
            [int? a = 3;]

            void [Main]() { }
        ";

        var diagnostics = @"
            declaring a main method and using global statements creates ambiguous entry point
            declaring a main method and using global statements creates ambiguous entry point
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    [Fact]
    public void Reports_Error_BU0017_UndefinedSymbol() {
        var text = @"
            int? x = [y];
        ";

        var diagnostics = @"
            undefined symbol 'y'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0018_ColonColonWithTypeAlias() {
        var text = @"
            using B = A;

            [B]::M();

            public static class A { public static void M() {}}
        ";

        var diagnostics = @"
            cannot use alias 'B' with '::' since the alias references a type; use '.' instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0019_NotAllPathsReturn() {
        var text = @"
            int? [myFunc]() { }
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
                int? num;
            }

            bool? x = [new A()];
        ";

        var diagnostics = @"
            cannot convert from type 'A!' to 'bool?'
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

    [Fact]
    public void Reports_Error_BU0022_DuplicateAlias() {
        var text = @"
            using B = A;
            using [B] = A;

            public static class A { }

            ;
        ";

        var diagnostics = @"
            the using alias 'B' appeared previously in this namespace
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
            ambiguous which statement this else-clause belongs to; use curly braces
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0024_DuplicateWithGlobalUsing
    // Requires multiple files

    [Fact]
    public void Reports_Error_BU0025_CannotApplyIndexing() {
        var text = @"
            int? x = 3;
            int? y = [x\[0\]];
        ";

        var diagnostics = @"
            cannot apply indexing with [] to an expression of type 'int?'
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
            string? x = [""];[]
        ";

        var diagnostics = @"
            unterminated string literal
            expected ';' at end of input
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0028_NoAliasHere() {
        var text = @"
            using static [B] = A;

            public static class A { }

            ;
        ";

        var diagnostics = @"
            a 'using static' directive cannot be used to declare an alias
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0029_BadUsingType() {
        var text = @"
            using static [A];
            namespace A { }
            ;
        ";

        var diagnostics = @"
            a 'using static' directive can only be applied to types; 'A' is a namespace not a type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0030_DuplicateConversion() {
        var text = @"
            class A {
                public static implicit operator int?(A a) { return 1; }
                public static implicit [operator] int?(A a) { return 1; }
            }
        ";

        var diagnostics = @"
            duplicate user-defined conversion in type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0031_DuplicateUsing() {
        var text = @"
            using A;
            using [A];
            namespace A { }
            ;
        ";

        var diagnostics = @"
            the using directive for 'A' appeared previously in this namespace
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0032_CannotCallNonMethod() {
        var text = @"
            int? x = 3;
            int? y = [x]();
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

    [Fact]
    public void Reports_Error_BU0034_BadUsingNamespace() {
        var text = @"
            using [A];
            class A { }
            ;
        ";

        var diagnostics = @"
            a 'using' directive can only be applied to namespaces; 'A' is a type not a namespace; consider a 'using static' directive instead
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

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

    [Fact]
    public void Reports_Error_BU0035_BadUsingStaticType() {
        var text = @"
            using static [A];
            struct A { }
            ;
        ";

        var diagnostics = @"
            'struct' type is not valid for 'using static'; only a class can be used
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
            int? myFunc() {
                [return];
            }
        ";

        var diagnostics = @"
            cannot return without a value in a method returning non-void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // !
    // Unreachable currently

    // [Fact]
    // public void Reports_Error_BU0039_ArrayInitToNonArrayType() {
    //     var text = @"
    //         int a = [{ 1, 2, 3 }];
    //     ";

    //     var diagnostics = @"
    //         can only use array initializer expressions to assign to array types; try using a new expression instead
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

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
    // Requires multi-dimension array types (e.g. `int[3,3]`)

    [Fact]
    public void Reports_Error_BU0044_ArrayInitWrongLength() {
        var text = @"
            lowlevel {
                int?\[\] x = new int?\[4\] [{1, 2, 3}];
            }
        ";

        var diagnostics = @"
            an array initializer of length '4' is expected
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0045_IncompatibleEntryPointReturn
    // Requires command-line arguments (specifically building to .NET dll)

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
            int? x = 3;
            [ref int? y = x];
        ";

        var diagnostics = @"
            a by-reference data container must be initialized with a reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0050_InitializeByValueWithByReference() {
        var text = @"
            int? x = 3;
            [int? y = ref x];
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
    // Unreachable currently

    // ! Error_BU0054_ReferenceToConstant
    // Unreachable currently

    [Fact]
    public void Reports_Error_BU0055_VoidUsedAsType() {
        var text = @"
            [void] a;
        ";

        var diagnostics = @"
            cannot use void as a type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0055_VoidUsedAsType2() {
        var text = @"
            void F([void] a) { }
        ";

        var diagnostics = @"
            cannot use void as a type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0055_VoidUsedAsType3() {
        var text = @"
            void([void])? a;
        ";

        var diagnostics = @"
            cannot use void as a type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0055_VoidUsedAsType4() {
        var text = @"
            var a = sizeof([void]);
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
                int? num;
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
                public static void F(int? a) { }

                public static void F(string? a) { }
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
                public static void myFunc(int? a) { }

                public static void myFunc(string? a) { }
            }

            A.[myFunc](null);
        ";

        var diagnostics = @"
            call is ambiguous between 'A.myFunc(int?)' and 'A.myFunc(string?)'
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
                int? a;
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
            cannot use a nullability annotation in object or array creation
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0063_AnnotationsDisallowedInObjectCreation2() {
        var text = @"
            var a = new [int\[1\]!] { 1 };
        ";

        var diagnostics = @"
            cannot use a nullability annotation in object or array creation
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0064_ConstantToNonConstantReference
    // Unreachable currently

    [Fact]
    public void Reports_Error_BU0065_NoSuchField() {
        var text = @"
            class A {
                public void f() {}

                public void M() initializes([f]) { }
            }
            ;
        ";

        var diagnostics = @"
            'A' contains no such field 'f'
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
            void M(int? x) { }
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
            void MyFunc(int? a = [Console.Input()]) { }
        ";

        var diagnostics = @"
            default parameter value for 'a' must be a compile-time constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0069_DefaultBeforeNoDefault() {
        var text = @"
            void MyFunc(int? a = 3, int? b[)] { }
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
            class A {
                public void M() {
                    int [A] = 3;
                }
            }
            ;
        ";

        var diagnostics = @"
            local 'A' shares a name with a type in this namespace
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0072_CannotImplyNull() {
        var text = @"
            void MyFunc(int? a, int! b) { }

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
            int? myInt = [5 / 0];
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
    // Scoped is not implemented yet

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
            int? myInt = 3;
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
    public void Reports_Error_BU0082_MissingFieldInit() {
        var text = @"
            class A {
                public int a;
                public constructor() {
                    Init();
                }
                private void [Init]() initializes(a) {

                }
            }
            ;
        ";

        var diagnostics = @"
            not all code paths initialize field 'A.a'
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

    // ! Error_BU0084_CannotUseStruct
    // Lowlevel restrictions not enforced currently

    // ! Error_BU0085_CannotUseThis
    // Taking address of this not forbidden currently

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
                const static [constructor]() { }
            }
        ";

        var diagnostics = @"
            modifier 'const' is not valid for this item
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
                int? num [= 3];
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
            [\[SomeAttribute\]]
            namespace A { }
            ;
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
                public static ref A? [operator]+(A a, A b) { return null; }
            }
        ";

        var diagnostics = @"
            non-indexing operators cannot return by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // !
    // ? Not enforced currently
    // [Fact]
    // public void Reports_Error_BU0095_RefReturnGlobal() {
    //     var text = @"
    //         int? a = 3; ref int? b = ref a; ref int? F() { return ref [b]; }
    //     ";

    //     var diagnostics = @"
    //         cannot return a global by reference
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // ! Error_BU0096_RefReturnOnlyParameter2
    // Unreachable currently

    [Fact]
    public void Reports_Error_BU0097_DottedTypeNamesNotFound() {
        var text = @"
            class A { }
            var a = new A.[B]();
        ";

        var diagnostics = @"
            the type name 'B' does not exist in the type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0098_StaticConstructorParameter() {
        var text = @"
            static class A {
                static [constructor](int? a) { }
            }
        ";

        var diagnostics = @"
            static constructors must be parameterless
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
                int? a = 3;
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
                int? a = 3;
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
                int? a = 3;
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
            int? x = 3;
            constexpr ref int? [y] = [ref x];
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
            int? Test() { return 3; }
            constexpr int? y = [Test()];
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
                public static int? operator[+](int? a, int? b) { return a; }
            }
        ";

        var diagnostics = @"
            static classes cannot contain operators
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0111_RefReturnParameter2() {
        var text = @"
            struct A { int? f; }
            ref int? M(A a) {
                return ref [a].f;
            }
        ";

        var diagnostics = @"
            cannot return by reference a member of parameter 'a' because is not a ref parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0112_RefReturnScopedParameter
    // Scoped not implemented yet

    // ! Error_BU0113_RefReturnOnlyParameter
    // Unreachable currently

    // ! Error_BU0114_ArrayOutsideOfLowLevelContext
    // Lowlevel restrictions not enforced currently

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
    public void Reports_Error_BU0117_UseOfUnassignedLocal() {
        var text = @"
            void M() {
                int a;
                int b = [a];
            }
        ";

        var diagnostics = @"
            use of unassigned local 'a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0117_UseOfUnassignedLocal2() {
        var text = @"
            void M() {
                int a;
                [F()];

                int F() { return a; }
            }
        ";

        var diagnostics = @"
            use of unassigned local 'a'
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

    [Fact]
    public void Reports_Error_BU0119_RefReturnParameter() {
        var text = @"
            ref int? M(int? a) {
                return ref [a];
            }
        ";

        var diagnostics = @"
            cannot return a parameter by reference 'a' because it is not a ref parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0120_EscapeOther
    // ? Unsure what expression would trigger this

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
            class A<type T> where { [T2] extends Object; } { }
        ";

        var diagnostics = @"
            type 'A' has no such template parameter 'T2'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0123_CannotExtendCheckNonType() {
    //     var text = @"
    //         class A<int? [T]> where { T extends Object; } { }
    //     ";

    //     var diagnostics = @"
    //         template 'T' is not a type; cannot extension check a non-type
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0124_ConstraintIsNotConstant() {
    //     var text = @"
    //         class A<string? a> where { [a == Console.Input()]; } { }
    //     ";

    //     var diagnostics = @"
    //         template constraint is not a compile-time constant
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0125_RefReturnNonreturnableLocal2() {
        var text = @"
            struct C { int? f; }

            ref int? A() {
                C a = new C();
                ref C b = ref a;
                return ref [b].f;
            }
        ";

        var diagnostics = @"
            a member of 'b' is returned by reference but was initialized to a value that cannot be returned by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0126_ExtendConstraintFailed() {
        var text = @"
            class B { }
            class A<type T> where { T extends B; } {}
            var a = new [A<Object>]();
        ";

        var diagnostics = @"
            the type 'Object!' must be or derive from 'B' in order to use it as parameter 'T' in the template type or method 'A<type! T>'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0127_ConstraintWasNull() {
    //     var text = @"
    //         class A<int? a> where { a == 3; } { }
    //         var a = new [A<null>]();
    //     ";

    //     var diagnostics = @"
    //         template constraint fails: constraint results in null (a == 3)
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0128_ConstraintFailed() {
    //     var text = @"
    //         class A<int? a> where { a == 3; } { }
    //         var a = new [A<4>]();
    //     ";

    //     var diagnostics = @"
    //         template constraint fails (a == 3)
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0129_RefReturnNonreturnableLocal() {
        var text = @"
            ref int? A() {
                int? a = 3;
                ref int? b = ref a;
                return ref [b];
            }
        ";

        var diagnostics = @"
            cannot return local 'b' by reference because it was initialized to a value that cannot be returned by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0130_RefReturnLocal2() {
        var text = @"
            struct A { public int? f; }

            ref int? M() {
                A a = new A();
                return ref [a].f;
            }
        ";

        var diagnostics = @"
            cannot return a member of local 'a' by reference because it is not a ref local
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0131_RefReturnLocal() {
        var text = @"
            ref int? A() {
                int? a = 3;
                return ref [a];
            }
        ";

        var diagnostics = @"
            cannot return local 'a' by reference because it is not a ref local
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

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
    // Ref expressions in ternary is not implemented yet

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
    // ? Unsure what expression would trigger this

    // ! Error_BU0137_CannotUseBase
    // Taking address of this not forbidden currently

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
            'A.M()' must declare a body because it is not marked abstract or extern
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
                public static bool? [operator]==(A x, A y) {
                    return true;
                }
            }
        ";

        var diagnostics = @"
            the operator A.op_Equality(A!, A!) requires a matching operator '!=' to also be defined
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
    // Unreachable currently (parser catches this instead of binder)

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
            class C {
            private class A { }
            public class [B] extends A { }
            }
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

    [Fact]
    public void Reports_Error_BU0155_CannotDeriveTemplate() {
        var text = @"
            class A<type T> extends [T] { }
        ";

        var diagnostics = @"
            cannot derive from template parameter 'type! T'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0155_CannotDeriveTemplate2() {
    //     var text = @"
    //         class A<[type T], int? T2> where { T extends T2; } { }
    //     ";

    //     var diagnostics = @"
    //         cannot derive from template parameter 'int? T2'
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0156_InconsistentAccessibilityField() {
        var text = @"
            class A {
                private class B { }
                public [B? f];
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
                public static B? [operator]+(A a, A b) { return null; }
            }
        ";

        var diagnostics = @"
            inconsistent accessibility: return type 'A.B' is less accessible than operator 'A.op_Addition(A!, A!)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0158_InconsistentAccessibilityReturn() {
        var text = @"
            class A {
                private class B { }
                public static B? [F]() { return null; }
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
                public static A? [operator]+(B b, A a) { return null; }
            }
        ";

        var diagnostics = @"
            inconsistent accessibility: parameter type 'A.B' is less accessible than operator 'A.op_Addition(A.B!, A!)'
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
            inconsistent accessibility: parameter type 'A.B' is less accessible than method 'A.F(A.B!)'
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
                int? a;
                void F() {
                    int? b = [a] + 3;
                    int? a = 7;
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
                    int? b = [a] + 3;
                    int? a = 7;
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

    [Fact]
    public void Reports_Error_BU0167_AmbiguousReference() {
        var text = @"
            using A;
            using C;

            namespace A {
                public class B { }
            }

            namespace C {
                public class B { }
            }

            var a = new [B]();
        ";

        var diagnostics = @"
            'B' is an ambiguous reference between 'A.B' and 'C.B'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0168_AmbiguousMember
    // ? Unsure how to trigger this

    // ! Error_BU0169_InvalidProtectedAccess
    // ? Unsure how to trigger this

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
                int? f;
                int? F() {
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
                int? f;
                ref int? F() {
                    [return] f;
                }
            }
        ";

        var diagnostics = @"
            must return by-reference in a method with a reference return type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0173_NoImplicitConversion() {
        var text = @"
            [void] a = [3];
        ";

        var diagnostics = @"
            cannot use void as a type
            cannot convert from type 'int!' to 'void' implicitly
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0174_MethodGroupCannotBeUsedAsValue() {
        var text = @"
            int? F() { }
            var a = [F];
        ";

        var diagnostics = @"
            method group 'F' cannot be used as a value
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    [Fact]
    public void Reports_Error_BU0175_LocalShadowsParameter() {
        var text = @"
            void F(int? a) {
                int? [a] = 3;
            }
        ";

        var diagnostics = @"
            cannot declare a local with the name 'a' because that name is already used by a parameter in an enclosing scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0176_ParameterOrLocalShadowsTemplateParameter() {
        var text = @"
            void F<type T>() {
                int [T] = 3;
            }
        ";

        var diagnostics = @"
            cannot declare a parameter, local, or local function with the name 'T' because that name is already used by a template parameter in an enclosing scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0177_LocalAlreadyDeclared() {
        var text = @"
            void F() {
                int? a = 3;
                int? [a] = 6;
            }
        ";

        var diagnostics = @"
            a local or local function with the name 'a' has already been declared in this scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0177_LocalAlreadyDeclared2() {
        var text = @"
            void F() {
                int? a = 3;
                int? [@a] = 6;
            }
        ";

        var diagnostics = @"
            a local or local function with the name 'a' has already been declared in this scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0177_LocalAlreadyDeclared3() {
        var text = @"
            void F() {
                int? a = 3;
                int? [@@a] = 6;
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
            void F(int? a) { }
            F([true]);
        ";

        var diagnostics = @"
            argument 1: cannot convert from type 'bool!' to 'int?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0179_CannotConvertImplicitlyNullable() {
        var text = @"
            int? a = 3;
            int! b = [a];
        ";

        var diagnostics = @"
            cannot convert from type 'int?' to 'int!' implicitly; an explicit conversion exists (are you missing a cast?)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0180_NeverGivenType() {
        var text = @"
            static class A { }
            bool b = [3 is A];
        ";

        var diagnostics = @"
            the given expression is never of the provided type ('A')
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
            binary operator '+' is ambiguous for operands with types 'A!' and 'B!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0182_ProgramLocalReferencedOutsideOfTopLevelStatement() {
        var text = @"
            int? a = 3;
            class A {
                int? f = [a];
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
            cannot convert null to 'type! T' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0184_InvalidObjectCreation() {
        var text = @"
            using A = Buffer<int>;
            var a = new [A]();
        ";

        var diagnostics = @"
            invalid object creation
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0185_AmbiguousUnaryOperator
    // TODO This should be reachable with a unary operator overload and an implicit int conversion for the same class

    // ! Error_BU0186_RefConditionalNeedsTwoRefs
    // Ternarys don't support ref expressions yet

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
            Object? a;
            [(A)a];
        ";

        var diagnostics = @"
            cannot cast to static type 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0190_ArraySizeInDeclaration
    // Parse catches this

    // ! Error_BU0191_ListNoTargetType
    // Pretty sure BU0009 covers all cases where this would be raised

    [Fact]
    public void Reports_Error_BU0192_InstanceRequiredInFieldInitializer() {
        var text = @"
            class A {
                int? a = [F]();
                int? F() { return 3; }
            }
        ";

        var diagnostics = @"
            a field initializer cannot reference non-static member 'A.F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0193_ArgumentExtraRef() {
        var text = @"
            void Outer() {
            void M(int a) { }
            int? a = 3;
            M([ref a]);
            }
        ";

        var diagnostics = @"
            argument 1 may not be passed with the 'ref' keyword
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0194_ArgumentWrongRef() {
        var text = @"
            void M(ref int? a) { }
            int? a = 3;
            M([a]);
        ";

        var diagnostics = @"
            argument 1 must be passed with the 'ref' keyword
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0195_NoCorrespondingArgument() {
        var text = @"
            void F(int? a, int? b) { }
            [F](a: 3);
        ";

        var diagnostics = @"
            there is no argument given that corresponds to the required parameter 'b' of 'F(int?, int?)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0195_NoCorrespondingArgument2() {
        var text = @"
            void(int a)? F;
            [F]();
        ";

        var diagnostics = @"
            there is no argument given that corresponds to the required parameter 'a' of 'void(int!)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0195_NoCorrespondingArgument3() {
        var text = @"
            void(int)? F;
            [F]();
        ";

        var diagnostics = @"
            there is no argument given that corresponds to the required parameter 'p1' of 'void(int!)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0196_BadNonTrailingNamedArgument() {
        var text = @"
            void F(int? a, int? b) { }
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
            void F(int? a, int? b) { }
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
    public void Reports_Error_BU0199_BadEmbeddedStatement2() {
        var text = @"
            int a = 10;
            with (a = 4) [int b = a;]
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

    [Fact]
    public void Reports_Error_BU0201_RefLocalOrParameterExpected() {
        var text = @"
            int? a = 3;
            [true ? 3 : 2] = ref a;
        ";

        var diagnostics = @"
            left side of ref assignment must be a ref variable, ref field, ref parameter, or ref indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0202_RefLValueExpected() {
        var text = @"
            ref int? a = ref [3];
        ";

        var diagnostics = @"
            ref value must be an assignable variable, field, parameter, or indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0203_RefReturnLValueExpected() {
        var text = @"
            int? F() { return 3; }
            ref int? G() { return ref [F()]; }
        ";

        var diagnostics = @"
            an expression cannot be used in this context because it may not be passed or returned by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0204_InternalError
    // Hopefully unreachable

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
                constexpr int? f;
            }
            [A.f]();
        ";

        var diagnostics = @"
            non-invocable member 'A.f' cannot be used like a method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0207_BadSKUnknown
    // ? Unsure how to trigger this

    // ! Error_BU0208_RefConstLocal
    // Struct methods not implemented yet

    // ! Error_BU0209_RefReturnThis
    // Struct methods not implemented yet

    [Fact]
    public void Reports_Error_BU0210_ConstantAssignmentThis() {
        var text = @"
            class P {
                void M(P! p) {
                    [this] = p;
                }
            }

            ;
        ";

        var diagnostics = @"
            cannot assign to 'this' because it is constant
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // Unsure how to trigger any of these
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
                const int? x = 3;
                ref int? F() {
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
                const int? x = 3;
                void F() {
                    ref int? f = ref [x];
                }
            }
            ;
        ";

        var diagnostics = @"
            a constant field cannot be used as a ref value (except in a constructor); consider using 'ref const'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0220_AssignmentConstantField() {
        var text = @"
            class A {
                const int? x = 3;
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

    // Unsure how to trigger any of these
    // ! Error_BU0221_RefReturnConstantStatic
    // ! Error_BU0222_RefConstantStatic
    // ! Error_BU0223_AssignmentConstantStatic
    // ! Error_BU0224_RefReturnConstant2

    [Fact]
    public void Reports_Error_BU0225_RefConstant2() {
        var text = @"
            class A {
                const B x = new ();
                void F() {
                    ref int f = ref [x.a];
                }
            }
            class B {
                public int a = default;
            }
            ;
        ";

        var diagnostics = @"
            members of constant field 'A.x' cannot be used as a ref value (except in a constructor); consider using 'ref const'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0226_AssignmentConstantField2
    // ! Error_BU0227_RefReturnConstantStatic2
    // ! Error_BU0228_RefConstantStatic2
    // ! Error_BU0229_AssignmentConstantStatic2
    // ! Error_BU0230_RefConstantLocalCause

    [Fact]
    public void Reports_Error_BU0231_AssignmentConstantLocalCause() {
        var text = @"
            [Console.PrintLine] = 10;
        ";

        var diagnostics = @"
            cannot assign to 'PrintLine' because it is a method group
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0232_PossibleBadNegativeCast
    // Unreachable currently

    [Fact]
    public void Reports_Error_BU0233_RefReturnMustHaveIdentityConversion() {
        var text = @"
            class A {
                public static ref A [operator]+(A a, A b) { return [null]; }
            }
        ";

        var diagnostics = @"
            non-indexing operators cannot return by reference
            the return expression must be of type 'A!' because this method returns by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0234_RefAssignmentMustHaveIdentityConversion() {
        var text = @"
            class A {
                const int? x = 3;
                void F() {
                    ref const int f = ref [x];
                }
            }
            ;
        ";

        var diagnostics = @"
            the expression must be of type 'int!' because it is being assigned by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0235_LocalSameNameAsTemplate() {
        var text = @"
            void M<type T>(int? [T]) { }
            ;
        ";

        var diagnostics = @"
            'T': a parameter, local, or local function cannot have the same name as a method template parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0236_DuplicateParameterName() {
        var text = @"
            void F(int? a, int? [a]) {}
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
    // ? Unsure how to trigger this

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

    [Fact]
    public void Reports_Error_BU0240_LookupInTemplateVariable() {
        var text = @"
            void M<type T>() {
                [T].M();
            }
            ;
        ";

        var diagnostics = @"
            cannot do non-virtual member lookup in 'type! T' because it is a template parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

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
    // Object initializers not implemented yet

    // ! Warning_BU0243_RefConstNotVariable
    // ! Warning_BU0244_ArgExpectedRef
    // ! Error_BU0245_RefConditionalDifferentTypes

    [Fact]
    public void Reports_Error_BU0246_DuplicateTemplateParameter() {
        var text = @"
            class A<type T, type [T]> { }
        ";

        var diagnostics = @"
            duplicate template parameter 'T'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Warning_BU0247_TemplateParameterSameAsOuterMethod
    // ! Warning_BU0248_TemplateParameterSameAsOuter

    [Fact]
    public void Reports_Error_BU0249_RefDefaultValue() {
        var text = @"
            void F([ref] int? a = 3) { }
        ";

        var diagnostics = @"
            a ref parameter cannot have a default value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0250_NoCastForDefaultParameter() {
        var text = @"
            void F(bool? [a] = ""Test"") { }
        ";

        var diagnostics = @"
            a value of type 'string!' cannot be used as a default parameter because there are no casts to type 'bool?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0251_NotNullRefDefaultParameter

    [Fact]
    public void Reports_Warning_BU0252_DefaultValueNoEffect() {
        var text = @"
            class A {
                public static A operator+(A a, int? [b] = 3) { return a; }
            }
        ";

        var diagnostics = @"
            the default value specified for parameter 'b' will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0253_RefFinalWrongOrder() {
        var text = @"
            void F([final] int a) { }
        ";

        var diagnostics = @"
            'final' modifier must be specified after 'ref'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0254_InvalidRefParameter

    // ? Currently not enforced
    // [Fact]
    // public void Reports_Error_BU0255_RefConstWrongOrder() {
    //     var text = @"
    //         void F([const] int a) { }
    //     ";

    //     var diagnostics = @"
    //         'const' modifier must be specified after 'ref'
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0257_CircularConstantValue() {
        var text = @"
            constexpr int? a = [[a]];
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
                int? a;
                int? [a];
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
                public override bool! Equals(Object? o) { return true; }
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
                public static bool? operator==(A a, A b) { return true; }
                public static bool? operator!=(A a, A b) { return false; }
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
                public static bool? operator==(A a, A b) { return true; }
                public static bool? operator!=(A a, A b) { return false; }
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

    // ! Warning_BU0277_HidingDifferentRefness
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

    [Fact]
    public void Reports_Error_BU0284_CantChangeRefReturnOnOverride() {
        var text = @"
            class A {
                public virtual ref int? M(ref int? a) { return ref a; }
            }

            class B extends A {
                public override int? [M](ref int? a) { return 3; }
            }
            ;
        ";

        var diagnostics = @"
            'B.M(ref int?)' must match by reference return of overridden member 'A.M(ref int?)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0285_CantChangeReturnTypeOnOverride() {
        var text = @"
            class A {
                public virtual void F() {}
            }
            class B extends A {
                public override int? [F]() { return 3; }
            }
        ";

        var diagnostics = @"
            'B.F()': return type must be 'void' to match overridden member 'A.F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // Not implemented yet
    // ! Warning_BU0286_OverridingDifferentRefness
    // ! Warning_BU0287_TopLevelNullabilityMismatchInParameterTypeOnOverride
    // ! Warning_BU0288_NullabilityMismatchInParameterTypeOnOverride
    // ! Warning_BU0289_TopLevelNullabilityMismatchInReturnTypeOnOverride
    // ! Warning_BU0290_NullabilityMismatchInReturnTypeOnOverride

    // ! Fatal_BU0291_LibraryErrors
    // Hopefully unreachable

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
                public static A? [operator]+(int a) { return null; }
            }
        ";

        var diagnostics = @"
            the parameter of a unary operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0294_BadAbstractUnaryOperatorSignature
    // Abstract operators not implements yet

    [Fact]
    public void Reports_Error_BU0295_BadShiftOperatorSignature() {
        var text = @"
            class A {
                public static A? [operator]<<(int? a, int? b) { return null; }
            }
        ";

        var diagnostics = @"
            the first operand of an overloaded shift operator must have the same type as the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0296_BadAbstractShiftOperatorSignature
    // Abstract operators not implements yet

    [Fact]
    public void Reports_Error_BU0297_BadBinaryOperatorSignature() {
        var text = @"
            class A {
                public static A? [operator]+(int? a, int? b) { return null; }
            }
        ";

        var diagnostics = @"
            one of the parameters of a binary operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0298_BadAbstractBinaryOperatorSignature
    // Abstract operators not implements yet

    // ! Error_BU0299_BadAbstractEqualityOperatorSignature
    // Abstract operators not implements yet

    [Fact]
    public void Reports_Error_BU0300_BadIncrementOperatorSignature() {
        var text = @"
            class A {
                public static A? [operator]++(int? a) { return null; }
            }
        ";

        var diagnostics = @"
            the parameter type for ++ or -- operator must be the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0301_BadAbstractIncrementOperatorSignature
    // Abstract operators not implements yet

    [Fact]
    public void Reports_Error_BU0302_BadIncrementReturnType() {
        var text = @"
            class A {
                public static int? [operator]++(A a) { return null; }
            }
        ";

        var diagnostics = @"
            the return type for ++ or -- operator must match the parameter type or be derived from the parameter type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0303_BadAbstractIncrementReturnType
    // Abstract operators not implements yet

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
    // Requires command-line arguments (specifically `--type=graphics`)

    // ! Error_BU0306_SeparateMainAndUpdate
    // Requires command-line arguments (specifically `--type=graphics`)

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

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0309_BadArity2() {
    //     var text = @"
    //         class A<int? t> { }
    //         var a = new [A]();
    //     ";

    //     var diagnostics = @"
    //         the template type 'A<int? t>' requires 1 template arguments
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0310_ProtectedInStruct() {
        var text = @"
            struct A {
                protected [int? f];
            }
        ";

        var diagnostics = @"
            'A': protected member declared in struct
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // !
    // TODO We don't properly handle nested spans that aren't identical
    // [Fact]
    // public void Reports_Error_BU0311_EscapeCall() {
    //     var text = @"
    //         ref int M() {
    //             int x = 10;
    //             return ref [GetRef(ref [x])];
    //         }

    //         ref int GetRef(ref int p) { return ref p; }
    //     ";

    //     var diagnostics = @"
    //         use of result of 'GetRef(ref int)' in this context may expose locals referenced by parameter 'p' outside of their declaration scope
    //         cannot return local 'x' by reference because it is not a ref local
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // [Fact]
    // public void Reports_Error_BU0312_EscapeCall2() {
    //     var text = @"
    //         struct A { int f; }

    //         ref int M() {
    //             A a = new A();
    //             ref A b = ref a;
    //             return ref [GetRef(ref [b])].f;
    //         }

    //         ref A GetRef(ref A p) { return ref p; }
    //     ";

    //     var diagnostics = @"
    //         use of member of result of 'GetRef(ref A!)' in this context may expose locals referenced by parameter 'p' outside of their declaration scope
    //         cannot return local 'b' by reference because it was initialized to a value that cannot be returned by reference
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // ! Error_BU0313_EscapeLocal
    // ? Unsure how to trigger this

    [Fact]
    public void Reports_Error_BU0314_RefAssignReturnOnly() {
        var text = @"
            struct A {
                protected [int? f];
            }
        ";

        var diagnostics = @"
            'A': protected member declared in struct
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0315_RefAssignNarrower() {
        var text = @"
            void M(int? a, ref int? b) {
                [b = ref a];
            }
        ";

        var diagnostics = @"
            cannot ref-assign 'a' to 'b' because 'a' has a narrower escape scope than 'b'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0316_RefAssignValEscapeWider
    // ? Unsure how to trigger this

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

    [Fact]
    public void Reports_Error_BU0318_GlobalUsingInNamespace() {
        var text = @"
            namespace A {
                [global] using B = A;
            }
            ;
        ";

        var diagnostics = @"
            cannot use a global using directive in a namespace declaration
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0319_AliasNotFound() {
        var text = @"
            var a = [A]::C();
        ";

        var diagnostics = @"
            alias 'A' not found
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0320_SingleTypeNameNotFound() {
        var text = @"
            [asdf] a = 3;
        ";

        var diagnostics = @"
            the type or namespace name 'asdf' could not be found
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

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

    [Fact]
    public void Reports_Error_BU0322_GlobalSingleTypeNameNotFound() {
        var text = @"
            var a = new global::[A]();
        ";

        var diagnostics = @"
            the type or namespace name 'A' could not be found in the global namespace
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0323_DottedTypeNamesNotFoundInNamespace() {
        var text = @"
            namespace A { }
            var a = new A.[B]();
        ";

        var diagnostics = @"
            the type or namespace name 'B' does not exist in the namespace 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0324_ConflictingAliasAndMember
    // ? Unsure how to trigger this

    [Fact]
    public void Reports_Error_BU0325_UnexpectedUnboundTemplateName() {
        var text = @"
            class A<type T> { }
            var a = new [A<>]();
        ";

        var diagnostics = @"
            unexpected use of an unbound template name
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0326_HasNoTemplate() {
        var text = @"
            class A { }
            var a = new [A<int>]();
            ;
        ";

        var diagnostics = @"
            the non-template type 'A' cannot be used with template arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0327_TemplateNotAllowed
    // ? Unsure how to trigger this

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
            var a = new [A<int?>]();
        ";

        var diagnostics = @"
            the type 'int?' must be an object type in order to use it as parameter 'T' in the template type or method 'A<type! T>'
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
            the type 'Object!' must be a primitive type in order to use it as parameter 'T' in the template type or method 'A<type! T>'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0332_NotNullableConstraintFailed
    // Not implemented currently

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

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0334_CannotIsCheckNonType() {
    //     var text = @"
    //         class A<int? [T]> where { T is primitive; } { }
    //     ";

    //     var diagnostics = @"
    //         template 'T' is not a type; cannot is check a non-type
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // !
    // ? Error not currently enforced
    // [Fact]
    // public void Reports_Error_BU0335_CannotPassGlobalByRef() {
    //     var text = @"
    //         ref int? F(ref int? a) { return ref a; } int? b = 3; F([ref b]) = 6; return b;
    //     ";

    //     var diagnostics = @"
    //         cannot pass a global by reference
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0336_ThrowMisplaced() {
        var text = @"
            3 + [throw] new Exception();
        ";

        var diagnostics = @"
            a throw expression is not valid in this context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0337_CannotReturnFromFinally() {
        var text = @"
            try {
                return 4;
            } finally {
                [return 6;]
            }
        ";

        var diagnostics = @"
            control cannot leave the body of a finally clause
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0338_StaticConstructorWithAccessModifier() {
        var text = @"
            static class A {
                public static [constructor]() { }
            }
        ";

        var diagnostics = @"
            access modifiers are not allowed on static constructors
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0339_StaticConstructorWithInitializer() {
        var text = @"
            static class A {
                static constructor() : [base]() { }
            }
        ";

        var diagnostics = @"
            static constructor cannot have an explicit 'this' or 'base' constructor call
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0340_InvalidInitializerDictionary() {
        var text = @"
            var a = [{3: ""test"", true: ""Test""}];
        ";

        var diagnostics = @"
            cannot infer dictionary type from initializer; try using an object creation expression instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0341_InvalidAddrOp() {
        var text = @"
            void* ptr = &[null];
        ";

        var diagnostics = @"
            cannot take the address of the given expression
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0342_PtrExpected() {
        var text = @"
            int? a = 3;
            int? b = [*a];
        ";

        var diagnostics = @"
            cannot dereference a non-pointer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0343_VoidPtr() {
        var text = @"
            int? a = 3;
            void* ptr = &a;
            int? b = [*ptr];
        ";

        var diagnostics = @"
            must cast a void pointer before dereferencing
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0344_CannotConvertConstantValue() {
        var text = @"
            var? a = (bool?)[""asdf""];
        ";

        var diagnostics = @"
            constant value 'asdf' cannot be converted to 'bool?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0345_AbstractAndExtern() {
        var text = @"
            abstract class A {
                public abstract extern void [M]();
            }
        ";

        var diagnostics = @"
            'A.M()' cannot be both abstract and extern
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0346_ExternCannotHaveBody() {
        var text = @"
            class A {
                public extern void [M]() { }
            }
        ";

        var diagnostics = @"
            'A.M()' cannot declare a body because it is marked extern
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // !
    // Unreachable currently, attributes not implemented yet
    // [Fact]
    // public void Reports_Error_BU0347_DllImportOnInvalidMethod() {
    //     var text = @"
    //         class A {
    //             \[[DllImport](""d3d12.dll"")\]
    //             public static void M() { }
    //         }
    //     ";

    //     var diagnostics = @"
    //         the DllImport attribute must be specified on a method marked 'static' and 'extern'
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // [Fact]
    // public void Reports_Error_BU0348_DllImportOnTemplateMethod() {
    //     var text = @"
    //         class A {
    //             \[[DllImport](""d3d12.dll"")\]
    //             public static void M<type T>() { }
    //         }
    //     ";

    //     var diagnostics = @"
    //         the DllImport attribute cannot be applied to a method that is template or contained in a template method or type
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // ! Error_BU0349_InvalidAttributeArgument
    // Unreachable currently, attributes not implemented yet

    [Fact]
    public void Reports_Error_BU0350_FixedBufferTooManyDimensions() {
        var text = @"
            struct A {
                int32 a[\[1,1\]];
            }
        ";

        var diagnostics = @"
            a fixed buffer can only have one dimension
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0351_FixedOverflow() {
        var text = @"
            struct A {
                int32 a\[[[9999999999999]]\];
            }
        ";

        var diagnostics = @"
            cannot convert from type 'int!' to 'int32!' implicitly; an explicit conversion exists (are you missing a cast?)
            fixed size buffer of length '1316134911' and type 'int32!' is too big
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0352_InvalidFixedArraySize() {
        var text = @"
            struct A {
                int32 a\[[-1]\];
            }
        ";

        var diagnostics = @"
            fixed size buffers must have a length greater than zero
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0353_FixedNotInStruct() {
        var text = @"
            class A {
                [int32 a\[30\]];
            }
        ";

        var diagnostics = @"
            fixed size buffer fields may only be members of structs
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0354_FixedFieldMustNotBeRef() {
        var text = @"
            struct A {
                [ref int32 a\[30\]];
            }
        ";

        var diagnostics = @"
            fixed fields cannot be ref fields
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0355_IllegalFixedType() {
        var text = @"
            struct A {
                [char?] a\[30\];
            }
        ";

        var diagnostics = @"
            fixed size buffer type must a non-nullable bool or a non-nullable sized numeric primitive
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0356_NullptrNoTargetType() {
        var text = @"
            var a = [nullptr];
        ";

        var diagnostics = @"
            there is no target type for the null pointer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0357_InvalidCompileTimeExpression() {
        var text = @"
            class A {
                public int Method() { return 3; }
            }

            var a = new A();
            var b = [$a.Method()];
        ";

        var diagnostics = @"
            expression is not computable at compile time
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0358_FixedNeedsLValue
    // ? Unsure how to trigger this

    [Fact]
    public void Reports_Error_BU0359_InvalidCascadeExpression() {
        var text = @"
            class A { }

            var a = new A()..[3];
        ";

        var diagnostics = @"
            cascade expression must be an assignment or call expression
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0360_NestedCascadeExpression() {
        var text = @"
            class A { }

            var a = new A()..[a.M]();
        ";

        var diagnostics = @"
            cascade expression must access a direct member of the target receiver
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0361_InvalidCompileTimeType() {
        var text = @"
            class A { }
            A GetA() { return new A(); }
            var a = [$GetA()];
        ";

        var diagnostics = @"
            compile time expression must result in a primitive or struct type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0362_BadStackAllocExpression() {
        var text = @"
            int32 a[\[1,2\]];
        ";

        var diagnostics = @"
            a stackalloc expression or local requires a type with a single array size specifier
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0363_NegativeStackAllocSize() {
        var text = @"
            var a = stackalloc int32\[[(int32!)-1]\];
        ";

        var diagnostics = @"
            cannot use a negative size with a stackalloc expression or local
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0364_NoStackAllocTarget() {
        var text = @"
            [stackalloc int32\[10\]];
        ";

        var diagnostics = @"
            stackalloc expression can only be used as a data container initializer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0365_StackAllocInCatchFinally() {
        var text = @"
            try {
            } catch {
                var a = [stackalloc int32\[10\]];
            }
        ";

        var diagnostics = @"
            a stackalloc expression or local may not be used in a catch or finally block
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0366_StackAllocLocalWithInitializer() {
        var text = @"
            [int32 a\[10\] = 3];
        ";

        var diagnostics = @"
            a stackalloc local cannot not have an explicit initializer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0367_ImplicitlyTypedStackAllocLocal() {
        var text = @"
            [var a\[10\] = 3];
        ";

        var diagnostics = @"
            a stackalloc local cannot be implicitly typed
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0368_InvalidEnumType() {
        var text = @"
            enum A extends [Object] { }
        ";

        var diagnostics = @"
            an enum type can only derive from integral primitives or string
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0369_EnumOverflow() {
        var text = @"
            enum A extends uint8 {
                SomeField = 254,
                G,
                [H]
            }
        ";

        var diagnostics = @"
            'A.H': the enum value is too large to fit in its type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0370_UnbalancedILStack() {
        var text = @"
            [il] {
                dup;
            }
        ";

        var diagnostics = @"
            inline IL block does not have a balanced stack
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0371_UnknownILOpCode() {
        var text = @"
            il {
                [asdf;]
            }
        ";

        var diagnostics = @"
            unknown IL instruction 'asdf'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0372_InvalidILOperandKind() {
        var text = @"
            il noverify {
                [Ldc.I4;]
            }
        ";

        var diagnostics = @"
            IL instruction 'ldc.i4' expects an operand of type 'int32'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0373_InvalidILOperand() {
        var text = @"
            il noverify {
                [add 3;]
            }
        ";

        var diagnostics = @"
            IL instruction 'add' does not expect an operand
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // !
    // TODO Fix diagnostic locations for directives
    // [Fact]
    // public void Reports_Error_BU0374_InvalidDirectivePlacement() {
    //     var text = @"
    //         int a = 3; [#if]
    //     ";

    //     var diagnostics = @"
    //         preprocessor directives must appear as the first non-whitespace character on a line
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // [Fact]
    // public void Reports_Error_BU0375_EndifDirectiveExpected() {
    //     var text = @"
    //         #if ASDF[]
    //     ";

    //     var diagnostics = @"
    //         #endif directive expected
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // [Fact]
    // public void Reports_Error_BU0376_UnexpectedDirective() {
    //     var text = @"
    //         [#asdf]
    //     ";

    //     var diagnostics = @"
    //         unexpected preprocessor directive
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // [Fact]
    // public void Reports_Error_BU0377_DirectiveFollowsToken() {
    //     var text = @"
    //         int a = 3;
    //         [#define] ASDF
    //     ";

    //     var diagnostics = @"
    //         cannot define/undefine preprocessor symbols after first token in file
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // [Fact]
    // public void Reports_Error_BU0378_InvalidDirectiveExpression() {
    //     var text = @"
    //         #if [3]
    //         #endif
    //     ";

    //     var diagnostics = @"
    //         invalid preprocessor expression
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0379_InvalidImplicitEnum() {
        var text = @"
            enum A extends string {
                [F]
            }
        ";

        var diagnostics = @"
            enum members with a string underlying type must have explicit values
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0380_EnumFieldNoTargetType() {
        var text = @"
            var a = [.A];
        ";

        var diagnostics = @"
            there is no target type for the implicit enum field
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0381_WrongEnumTargetType() {
        var text = @"
            int32 a = [.A];
        ";

        var diagnostics = @"
            cannot infer enum type from implicit enum field
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0382_InvalidGotoCase() {
        var text = @"
            [goto default;]
        ";

        var diagnostics = @"
            a goto is only valid inside a switch statement
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0383_SwitchTypeValueExpected() {
        var text = @"
            class A { }
            switch ([new A()]) { }
        ";

        var diagnostics = @"
            a switch expression or case label must be a primitive
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0384_SwitchExpressionValueExpected() {
        var text = @"
            switch ([null]) { }
        ";

        var diagnostics = @"
            the switch expression must be a value; found 'null'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0385_LabelNotFound() {
        var text = @"
            switch (3) {
                case 1:
                    [goto case 5;]
            }
        ";

        var diagnostics = @"
            no such label 'case 5:' within the scope of the goto statement
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0386_DuplicateCaseLabel() {
        var text = @"
            switch (3) {
                case 1:
                [case 1:]
            }
        ";

        var diagnostics = @"
            the switch statement contains multiple cases with the label value '1'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0387_SwitchCaseSubsumed
    // Unreachable currently

    [Fact]
    public void Reports_Error_BU0388_MultipleFileScopedNamespaces() {
        var text = @"
            namespace A;
            namespace [B];
        ";

        var diagnostics = @"
            file can only contain one file-scoped namespace declaration
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0389_FileScopedAndNormalNamespace() {
        var text = @"
            namespace A;
            namespace [B] { }
        ";

        var diagnostics = @"
            file can not contain both file-scoped and normal namespace declarations
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0390_FileScopedNamespaceNotFirstMember() {
        var text = @"
            namespace A { }
            namespace [B];
        ";

        var diagnostics = @"
            file-scoped namespace must precede all other members in a file
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0391_EntryConstructor() {
        var text = @"
            public class [Program] {
                public constructor(int a) { }
                public void Main() { }
            }
        ";

        var diagnostics = @"
            entry point type cannot define instance constructors
        ";

        AssertDiagnostics(text, diagnostics, _writer, script: false);
    }

    [Fact]
    public void Reports_Error_BU0392_StructLayoutCycle() {
        var text = @"
            struct A { public B [b]; }
            struct B { public A [a]; }
            ;
        ";

        var diagnostics = @"
            struct member 'A.b' of type 'B' causes a cycle in the struct layout
            struct member 'B.a' of type 'A' causes a cycle in the struct layout
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0393_InvalidForEachExpression() {
        var text = @"
            for (val in [3]) { }
        ";

        var diagnostics = @"
            for iterator expression must be an array, string, Enumerator or define the iter operator or define the length and [] operators
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0394_LengthMustReturnInt() {
        var text = @"
            public class A {
                public static bool? [operator] length(A a) { return true; }
            }
        ";

        var diagnostics = @"
            length operator must return 'int!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0395_IterMustReturnEnumerator() {
        var text = @"
            public class A {
                public static bool? [operator] iter(A a) { return true; }
            }
        ";

        var diagnostics = @"
            iter operator must return 'Enumerator!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0396_UnexpectedParameterList() {
        var text = @"
            il noverify {
                box int : [(bool)];
            }
        ";

        var diagnostics = @"
            unexpected parameter list (operand is not a method)
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0397_InvalidParameterList() {
        var text = @"
            il noverify {
                call [Math.Sin] : (bool);
            }
        ";

        var diagnostics = @"
            no overload for method 'Sin' matches the parameter list
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0398_NamespaceUnexpected() {
        var text = @"
            namespace A;

            int? [a] = 3;
        ";

        var diagnostics = @"
            namespaces cannot directly contain fields, methods, or statements
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0399_NullErasureOnNonNullableType() {
        var text = @"
            int! a = 3;
            int! b = [a?];
        ";

        var diagnostics = @"
            cannot apply a null erasure operator to an expression with type 'int!' as it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0400_NullErasureOnTypeWithNoDefault() {
        var text = @"
            class A { }
            A? a = new A();
            A! b = [a?];
        ";

        var diagnostics = @"
            cannot apply a null erasure operator to an expression with type 'A?' because it has no non-null default value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0401_NullErasureOnNull() {
        var text = @"
            [null?];
        ";

        var diagnostics = @"
            cannot apply a null erasure operator to a null literal
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0401_NullErasureOnNull2() {
        var text = @"
            [nullptr?];
        ";

        var diagnostics = @"
            cannot apply a null erasure operator to a null literal
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0402_NullBindingRequiresNullable() {
        var text = @"
            int! a = 3;

            if ([a] -> x!) {
            }
        ";

        var diagnostics = @"
            the source expression type of a null-binding contract must be nullable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0403_NullBindingOnNull() {
        var text = @"
            if ([null] -> x!) {
            }
        ";

        var diagnostics = @"
            cannot create a null-binding contract on a null literal
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0403_NullBindingOnNull2() {
        var text = @"
            if ([nullptr] -> x!) {
            }
        ";

        var diagnostics = @"
            cannot create a null-binding contract on a null literal
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0404_NoHandleTarget
    // Requires preprocessor statements (`#handle`)

    // ! Error_BU0405_AmbiguousHandleTarget
    // Requires preprocessor statements (`#handle`)

    [Fact]
    public void Reports_Error_BU0406_FunctionCannotContainPointer() {
        var text = @"
            [void(int*)]? a;
        ";

        var diagnostics = @"
            function types cannot contain pointers or function pointers; consider using a function pointer instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0407_MethodFunctionMismatch() {
        var text = @"
            void F(int a) { }
            void()? a = [F];
        ";

        var diagnostics = @"
            no overload for 'F' matches function 'void()?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0408_BadReturnType() {
        var text = @"
            int? F() { return 3; }
            void() a = [F];
        ";

        var diagnostics = @"
            'int? F()' has the wrong return type; expected 'void'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0409_FunctionRefMismatch() {
        var text = @"
            ref int? F(ref int? a) { return ref a; }
            int?(ref int?) a = [F];
        ";

        var diagnostics = @"
            ref mismatch between 'F(ref int?)' and function 'int?(ref int?)'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0410_UnknownCallingConvention() {
        var text = @"
            void()*~\[[asdf]\] a;
        ";

        var diagnostics = @"
            unrecognized calling convention 'asdf'; valid calling conventions are 'stdcall', 'winapi', 'fastcall', 'cdecl', and 'thiscall'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0411_CannotAnnotatePointer() {
        var text = @"
            [int32*?] a;
        ";

        var diagnostics = @"
            cannot use a nullable annotation on a pointer or function pointer type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! TODO
    // [Fact]
    // public void Reports_Error_BU0412_CannotAnnotateTypeTemplate() {
    //     var text = @"
    //         class A<[type?] T> { }
    //     ";

    //     var diagnostics = @"
    //         type template parameters cannot be nullable
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0413_CannotAnnotateTemplate() {
        var text = @"
            class A<type T> {
                public [T!] a;
            }
        ";

        var diagnostics = @"
            cannot use a non-nullable annotation on a template parameter type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0414_DefaultLiteralNoTargetType() {
        var text = @"
            var a = [default];
        ";

        var diagnostics = @"
            there is no target type for the default literal
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0415_TypeWithNoDefault() {
        var text = @"
            class A { }
            A! a = [default];
        ";

        var diagnostics = @"
            cannot use a default literal for type 'A!' because it has no default value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0416_UnusedUsingDirective() {
        var text = @"
            [using Test;]

            namespace Test { }

            ;
        ";

        var diagnostics = @"
            using directive is unnecessary
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Currently unreachable?
    // [Fact]
    // public void Reports_Error_BU0417_TypeInferenceFailedForOut() {
    //     var text = @"
    //         F(out var a);

    //         void F(out int a) { a = 3; }
    //     ";

    //     var diagnostics = @"
    //         cannot infer the type of implicitly-typed out data container 'a'
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer, true);
    // }

    [Fact]
    public void Reports_Error_BU0418_OutVarAnnotated() {
        var text = @"
            F(out [var?] a);

            void F(out int a) { a = 3; }
        ";

        var diagnostics = @"
            cannot annotate the type of an implicitly typed out data container
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0419_OutNoDefaultValue() {
        var text = @"
            void F(out [int\[\]!] a) { }
        ";

        var diagnostics = @"
            cannot use the out modifier for type 'int![]!' because it has no default value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0420_BadPatternExpression() {
        var text = @"
            void F() {}

            var b = [F()] is int t;
        ";

        var diagnostics = @"
            invalid operand for pattern match; value required, but found 'F()'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0421_CannotAnnotateTypePattern() {
        var text = @"
            int a = 3;

            var b = a is [int?] c;
        ";

        var diagnostics = @"
            cannot use nullable type 'int?' in a pattern; use the underlying type 'int!' or a null-binding contract instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0422_PatternCannotHandleTypes() {
        var text = @"
            int a = 3;

            var b = a is [bool] c;
        ";

        var diagnostics = @"
            an expression of type 'int!' cannot be handled by a pattern of type 'bool!'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0423_StructWithNoDefault() {
        var text = @"
            class A { }

            struct B {
                A! a;

                constructor() {
                    a = new ();
                }
            }

            B b = [default];
        ";

        var diagnostics = @"
            cannot use a default literal for struct type 'B!' because it has fields with no default value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0424_WithExpressionNotAssignment() {
        var text = @"
            int a = 3;

            with ([a]) {
                int b = 5;
            }
        ";

        var diagnostics = @"
            the context expression of a with statement or with expression must be an assignment
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0425_ExitingControlFlowInWith() {
        var text = @"
            int a = 3;

            with (a = 0) {
                [return 3;]
            }
        ";

        var diagnostics = @"
            exiting the with body early will result in the reversals not taking place; consider using a 'with (...) try'
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! We don't error check attributes right now
    // [Fact]
    // public void Reports_Error_BU0426_UnmanagedRequiresStatic() {
    //     var text = @"
    //         public class A {
    //             \[Unmanaged\]
    //             public void M() { }
    //         }
    //     ";

    //     var diagnostics = @"
    //         'Unmanaged' can only be applied to ordinary static non-abstract, non-virtual methods or static local functions
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    // [Fact]
    // public void Reports_Error_BU0427_UnmanagedCannotBeTemplate() {
    //     var text = @"
    //         public class A<type T> {
    //             \[Unmanaged\]
    //             public void M() { }
    //         }
    //     ";

    //     var diagnostics = @"
    //         methods attributed with 'Unmanaged' cannot have template parameters and cannot be declared in a template type
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0428_DestructorInStaticClass() {
        var text = @"
            public static class A {
                [destructor]() { }
            }
        ";

        var diagnostics = @"
            static classes cannot contain destructors
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0429_CannotReturnFromDefer() {
        var text = @"
            defer {
                [return;]
            }
        ";

        var diagnostics = @"
            control cannot leave the body of a defer statement
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0430_ScopedWithoutDispose() {
        var text = @"
            {
                [scoped int a = 3;]
            }
        ";

        var diagnostics = @"
            'int': type used in a scoped statement must define a destructor
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0431_ObjectCreationNoTargetType() {
        var text = @"
            var a = [new ()];
        ";

        var diagnostics = @"
            there is no target type for the implicit object creation
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0432_ObjectCreationIllegalTargetType() {
        var text = @"
            int a = [new ()];
        ";

        var diagnostics = @"
            'int' is not a valid type for object creation
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0433_FileScopedClassNotFirstMember() {
        var text = @"
            class A { }
            class [B];
        ";

        var diagnostics = @"
            file-scoped class must precede all other members in a namespace or type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0434_FileScopedClassWithinNonFileScoped() {
        var text = @"
            class A {
                class [B];
            }
        ";

        var diagnostics = @"
            file-scoped class cannot be contained within a non-file-scoped namespace or non-file-scoped class
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0435_NoBuildMethod
    // Requires CLI args

    [Fact]
    public void Reports_Error_BU0436_ShebangNotOnFirstLine() {
        var text = @"
            ;
            [#]!asdf
        ";

        var diagnostics = @"
            '#!' must be the first characters on the first line of the file
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0437_AmbiguousTernary() {
        var text = @"
            var a = [true ? (int32)1 : (uint32)1];
        ";

        var diagnostics = @"
            the type of conditional expression cannot be determined because 'int32!' and 'uint32!' implicitly convert to one another
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0438_InvalidTernary() {
        var text = @"
            var a = [true ? 1 : null];
        ";

        var diagnostics = @"
            the type of conditional expression cannot be determined because there is no implicit conversion between 'int!' and '<null>'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0439_CannotTakeFunctionPointerOfNonStatic() {
        var text = @"
            class A {
                public static void M() {
                    int a = 3;
                    int F() { return a; }
                    int()* g = &[F];
                }
            }
            ;
        ";

        var diagnostics = @"
            cannot create a function pointer to 'F()' because it is not a static method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0440_NonPublicParameterlessStructConstructor() {
        var text = @"
            struct A {
                private [constructor]() { }
            }
            ;
        ";

        var diagnostics = @"
            parameterless struct constructors must be 'public'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0441_UnionMustHaveField() {
        var text = @"
            union [A] { }
            ;
        ";

        var diagnostics = @"
            unions must contain at least 1 non-static field
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0441_UnionMustHaveField2() {
        var text = @"
            struct A {
                [union] { }
            }
            ;
        ";

        var diagnostics = @"
            unions must contain at least 1 non-static field
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0442_ClampMustBeNumeric() {
        var text = @"
            var b = [true] >< \[false, true\];
        ";

        var diagnostics = @"
            cannot clamp on type 'bool!'; can only clamp on numeric primitives
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0443_CannotBitCastFromNullable() {
        var text = @"
            int? a = 3;
            decimal b = (decimal&)[a];
        ";

        var diagnostics = @"
            cannot bit cast operand of nullable type 'int?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0444_CannotBitCastToNullable() {
        var text = @"
            int a = 3;
            decimal? b = ([decimal?]&)a;
        ";

        var diagnostics = @"
           cannot bit cast to nullable type 'decimal?'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0445_UnknownBitCastSize() {
        var text = @"
            A! a = new ();
            decimal b = [(decimal&)a];

            class A { }
        ";

        var diagnostics = @"
           cannot bit cast with 'A!' as it's size is not known at compile time; consider using 'LowLevel.BitCast<type TFrom, type TTo>(TFrom)' instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0446_DifferentSizesInBitCast() {
        var text = @"
            int32 a = 3;
            int64 b = [(int64&)a];
        ";

        var diagnostics = @"
           cannot bit cast from 'int32!' to 'int64!' because they don't have the same size
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0447_IgnoringReturnValue() {
        var text = @"
            int F() { return 0; }
            [F()];
        ";

        var diagnostics = @"
           ignoring return value of method 'F()'; consider using a discard assignment if this is intended
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0448_VoidAssignment() {
        var text = @"
            void F() {}
            [_] = F();
        ";

        var diagnostics = @"
           a value of type 'void' may not be assigned
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0449_DiscardTypeInferenceFailed() {
        var text = @"
            [_] = null;
        ";

        var diagnostics = @"
           cannot infer the type of implicitly-typed discard
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0450_FinalizerInStaticClass() {
        var text = @"
            public static class A {
                [finalizer]() { }
            }
        ";

        var diagnostics = @"
            static classes cannot contain finalizers
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0451_ArgumentWrongRefConst() {
        var text = @"
            void M(ref int? a) { }

            void F() {
                const int? a = 3;
                M([ref a]);
            }
        ";

        var diagnostics = @"
            argument 1: cannot pass a reference to a constant to a parameter expecting a reference to a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0452_TupleTooFewElements() {
        var text = @"
            static (int[)] M() {}
        ";

        var diagnostics = @"
            tuple must contain at least 2 elements
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0453_TupleReservedElementNameAnyPosition() {
        var text = @"
            (int [ToString], bool b)? a;
        ";

        var diagnostics = @"
            tuple element name 'ToString' is disallowed
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0454_TupleReservedElementName() {
        var text = @"
            (int a, bool [Item1])? a;
        ";

        var diagnostics = @"
            tuple element name 'Item1' is only allowed at position 1
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0455_TupleDuplicateElementName() {
        var text = @"
            (int a, bool [a])? a;
        ";

        var diagnostics = @"
            'a': tuple element names must be unique
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0456_CannotCreateTuple() {
        var text = @"
            var a = [new (int, bool)(3, true)];
        ";

        var diagnostics = @"
            'new' cannot be used with tuple types; use a tuple literal instead
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0457_InvalidMemberDeclarationToken() {
        var text = @"
            class A {
                [+]
            }
        ";

        var diagnostics = @"
            invalid token '+' in a member declaration
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0458_UsingAfterMembers() {
        var text = @"
            namespace B { }

            namespace A {
                class C { }
                [using B;]
            }
        ";

        var diagnostics = @"
            using directive must precede all other namespace members
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0459_ExtendedLiteralNoTargetType() {
        var text = @"
            var a = [3s];
        ";

        var diagnostics = @"
            there is no target type for the literal with suffix 's'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0460_NoExtendedLiteralConversion() {
        var text = @"
            int a = [3s];
        ";

        var diagnostics = @"
            type 'int!' has no definition for literals of type 'int!' with the suffix 's'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0461_BadLiteralOperatorReturnType() {
        var text = @"
            class A {
                public static int [literal] s(int num) { return num; }
            }
        ";

        var diagnostics = @"
            the return type for literal operators must match the containing type or be derived from the containing type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0462_LiteralOperatorMustHaveSingleParameter() {
        var text = @"
            class A {
                public static A [literal] s() { return new(); }
            }
        ";

        var diagnostics = @"
            literal operators must have exactly 1 parameter
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0463_BadLiteralOperatorParameterType() {
        var text = @"
            class A {
                public static A [literal] s(A a) { return a; }
            }
        ";

        var diagnostics = @"
            literal operator parameter must be a type represented by a literal
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0464_TransientForEachAssignment() {
        var text = @"
            struct Elem {
                int e;
                constructor(int e) { this.e = e; }
            }

            Elem\[\] arr = { new (10), new (20) };

            for (elem in arr)
                [elem.e] = 10;
        ";

        var diagnostics = @"
            assignment to a for-each iterator local does not modify the element in the source collection
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0465_VoidInTuple() {
        var text = @"
            void F() { }

            var a = ([F()], 3);
        ";

        var diagnostics = @"
            a tuple may not contain a value of type 'void'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0466_StructInefficiencyCache() {
        var text = @"
            struct [A] {
                int8  a0;
                int64  b0;

                int8  a1;
                int64  b1;

                int8  a2;
                int64  b2;

                int8  a3;
                int64  b3;

                int8  a4;
                int64  b4;

                int8  a5;
                int64  b5;

                int8  a6;
            }
        ";

        var diagnostics = @"
            'A': struct crosses an unnecessary cache line; struct layout could be reduced from 104 bytes to 56 bytes by reordering fields
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0467_StructInefficiencyPadding() {
        var text = @"
            struct [A] {
                int8 a;
                int64 b;
                int8 c;
            }
        ";

        var diagnostics = @"
            'A': struct layout could be reduced from 24 bytes to 16 bytes by reordering fields
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0467_StructInefficiencyPadding2() {
        var text = @"
            struct packed(4) [A] {
                int8 a;
                int64 b;
                int8 c;
                int64 d;
                int8 e;
            }
        ";

        var diagnostics = @"
            'A': struct layout could be reduced from 28 bytes to 20 bytes by reordering fields
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0467_StructInefficiencyPadding3() {
        var text = @"
            struct packed(8) [A] {
                int8 a;
                int64 b;
                int8 c;
                int64 d;
                int8 e;
            }
        ";

        var diagnostics = @"
            'A': struct layout could be reduced from 40 bytes to 24 bytes by reordering fields
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Info_BU0470_StructInefficiency
    // ? We don't have a way to test info severity diagnostics yet

    [Fact]
    public void Reports_Error_BU0469_InvalidPackedAlignment() {
        var text = @"
            struct packed([3]) A { }
        ";

        var diagnostics = @"
            struct pack alignment must be 1, 2, 4, 8, 16, 32, 64, or 128
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0470_BadEmbeddedStatementDefer() {
        var text = @"
            int a = 10;
            if (true) [defer a = 4;]
        ";

        var diagnostics = @"
            embedded statement cannot be a defer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0470_BadEmbeddedStatementDefer2() {
        var text = @"
            int a = 10;
            defer [defer a = 4;]
        ";

        var diagnostics = @"
            embedded statement cannot be a defer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0471_LongTuple() {
        var text = @"
            var a = [(3, 3, 3, 3, 3, 3, 3, 3, 3, 3)];
        ";

        var diagnostics = @"
            long tuple (10 elements); consider using a named struct
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    // ! Error_BU0472_PredefinedTypeNotFound
    // ? Requires '--nostdlib' command-line argument

    // ! Error_BU0473_InvalidDeclarationExpression
    // ? Unreachable currently

    // ? Unreachable currently
    // [Fact]
    // public void Reports_Error_BU0474_DeconstructVariableCannotBeRef() {
    //     var text = @"
    //         [ref] var (a, b) = (3, 3);
    //     ";

    //     var diagnostics = @"
    //         a deconstruction variable cannot be declared as a ref local
    //     ";

    //     AssertDiagnostics(text, diagnostics, _writer);
    // }

    [Fact]
    public void Reports_Error_BU0475_TypeInferenceFailedForDeconstruction() {
        var text = @"
            int x = 4;
            (var [a], int b) = [x];
        ";

        var diagnostics = @"
            cannot infer the type of implicitly-typed deconstruction variable 'a'
            type 'int!' has no implicit conversion to a tuple of cardinality 2
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0476_DeconstructWrongCardinality() {
        var text = @"
            [(var a, var b) = (3, 3, 3)];
        ";

        var diagnostics = @"
            cannot deconstruct a tuple of '3' elements into '2' variables
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Error_BU0477_DeconstructTooFewElements
    // ? Unreachable currently

    [Fact]
    public void Reports_Error_BU0478_InvalidReverseParameter() {
        var text = @"
            class A {
                void M() { }
                [reverse] (a) { }
            }
            ;
        ";

        var diagnostics = @"
            reverse clause cannot take a parameter because the target method returns void
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0479_ReverseRefMismatch() {
        var text = @"
            class A {
                int M() { return 1; }
                [reverse] (ref int a) { }
            }
            ;
        ";

        var diagnostics = @"
            ref mismatch between 'A.M()' and parameter 'ref int! a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0480_RefReverseMustHaveIdentityConversion() {
        var text = @"
            class A {
                ref int M(ref int a) { return ref a; }
                reverse ([ref decimal] a) { }
            }
            ;
        ";

        var diagnostics = @"
            the reverse clause parameter must be of type 'int!' because it is being assigned by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0481_ReversibleCannotBeRef() {
        var text = @"
            class A {
                ref int [M](ref int a) { return ref a; }
                state(int) { return 3; }
                reverse (int a) { }
            }
            ;
        ";

        var diagnostics = @"
            a method with a state clause cannot return by reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0482_StateClauseWithoutReverseClause() {
        var text = @"
            class A {
                void [M]() { }
                state(int) { return 3; }
            }
            ;
        ";

        var diagnostics = @"
            a method with a state clause must have a reverse clause
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0483_UseOfUnassignedField() {
        var text = @"
            class A {
                A! a;

                constructor() {
                    var b = [a];
                    a = new ();
                }
            }
            ;
        ";

        var diagnostics = @"
            use of unassigned field 'A.a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0484_FieldNoDefiniteAssignment() {
        var text = @"
            class A {
                int! [a];
            }
            ;
        ";

        var diagnostics = @"
            cannot declare a class field without an initializer or definite constructor assignment with type 'int!' because it is non-nullable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0485_LowLevelDefaultOutsideLowLevelContext() {
        var text = @"
            int a = [lowlevel default];
        ";

        var diagnostics = @"
            cannot use a lowlevel default literal outside of a lowlevel context
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0486_NoInitOnNonNullable() {
        var text = @"
            [int a];
        ";

        var diagnostics = @"
            non-nullable globals and const or final locals must have an initializer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0487_NoNewTypeVar() {
        var text = @"
            class A<type T> {
                T t = [new ()];
            }
            ;
        ";

        var diagnostics = @"
            cannot create an instance of the type 'type! T' because it does not have the constructor constraint
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0488_FieldNoDefiniteAssignmentStruct() {
        var text = @"
            class C { }
            struct A {
                C! [a];
            }
            ;
        ";

        var diagnostics = @"
            cannot declare a struct field without definite constructor assignment with type 'C!' because it has no default value
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0489_LowLevelFieldInNonLowLevelType() {
        var text = @"
            class A {
                lowlevel [int a];
            }
            ;
        ";

        var diagnostics = @"
            cannot declare a lowlevel field inside of a non-lowlevel type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0490_NullableReceiverArray() {
        var text = @"
            var? a = new int\[10\];
            [a\[0\]] = 4;
        ";

        var diagnostics = @"
            cannot access arrays through a nullable receiver; consider using a null assert or conditional access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0491_NullableReceiverCall() {
        var text = @"
            class A {
                public void M() { }
            }

            void MyFunc(A? a) {
                [a.M]();
            }
        ";

        var diagnostics = @"
            cannot call methods through a nullable receiver; consider using a null assert or conditional access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0492_NullableReceiverIndex() {
        var text = @"
            var? a = ""test"";
            var b = [a\[0\]];
        ";

        var diagnostics = @"
            cannot index a nullable receiver; consider using a null assert or conditional access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0493_ReverseDoesNotMatchState() {
        var text = @"
            class A {
                public static int M(int p) {
                    return p;
                } state(bool) {
                    return p > 4;
                } [reverse] {
                }
            }
            ;
        ";

        var diagnostics = @"
            reverse clause must have a parameter that matches the type of the state clause
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0494_UndefinedToken() {
        var text = @"
            reverse [a];
        ";

        var diagnostics = @"
            undefined token 'a'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0495_TokenAlreadyDeclared() {
        var text = @"
            class A {
                public static void M() { }
                    reverse { }
            }

            reversible T: A.M();
            reversible [T]: A.M();
        ";

        var diagnostics = @"
            a token with the name 'T' has already been declared in this scope
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0496_ReversibleExpressionNotReversible() {
        var text = @"
            reversible T: [3];
        ";

        var diagnostics = @"
            the target expression of a reversible expression must be a call to a reversible method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0497_ReverseDeferExpressionNotReversible() {
        var text = @"
            reverse defer [3];
        ";

        var diagnostics = @"
            the target expression of a reverse defer statement must be a call to a reversible method
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0498_InvalidCommit() {
        var text = @"
            [commit;]
        ";

        var diagnostics = @"
            commit statements can only be used within a with statement
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0499_MissingDeconstruct() {
        var text = @"
            int x = 4;
            (int a, int b) = [x];
        ";

        var diagnostics = @"
            type 'int!' has no implicit conversion to a tuple of cardinality 2
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0500_DeconstructRequiresExpression() {
        var text = @"
            (int a, int b) = [null];
        ";

        var diagnostics = @"
            deconstruct assignment requires an expression with a type on the right-hand-side
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0501_AmbiguousDeconstruct() {
        var text = @"
            class A {
                public static implicit operator (int, int)(A _) {
                    return (0, 0);
                }
                public static implicit operator (bool, bool)(A _) {
                    return (false, false);
                }
            }
            var x = new A();
            (var [a], var [b]) = [x];
        ";

        var diagnostics = @"
            cannot infer the type of implicitly-typed deconstruction variable 'a'
            cannot infer the type of implicitly-typed deconstruction variable 'b'
            deconstruction of type 'A!' is ambiguous; consider explicitly typing deconstruction variables
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0502_ExpectedVerbatimLiteral() {
        var text = @"
            var [@] = 3;
        ";

        var diagnostics = @"
            expected identifier after verbatim specifier: @
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0502_ExpectedVerbatimLiteral2() {
        var text = @"
            var [@@] = 3;
        ";

        var diagnostics = @"
            expected identifier after verbatim specifier: @@
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0503_UnnecessaryLowLevelDefaultLiteral() {
        var text = @"
            lowlevel {
                int a = [lowlevel default];
            }
        ";

        var diagnostics = @"
            lowlevel default literal is unnecessary because the type 'int!' has a default value; consider using a regular default literal
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Warning_BU0504_UnnecessaryLowLevelDefaultExpression() {
        var text = @"
            lowlevel {
                var a = [lowlevel default(int)];
            }
        ";

        var diagnostics = @"
            lowlevel default expression is unnecessary because the type 'int!' has a default value; consider using a regular default expression
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0505_InvalidBufferCreation() {
        var text = @"
            var a = [new Buffer<int>()];
        ";

        var diagnostics = @"
            buffer creation must have exactly 1 or 2 arguments
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0506_InvalidRefKindInBufferCreation() {
        var text = @"
            void Func() {
                int a = 3;
                var b = new Buffer<int>([ref a]);
            }
        ";

        var diagnostics = @"
            arguments in buffer creation cannot be by-reference
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0507_OmittedArgumentInBufferCreation() {
        var text = @"
            var a = new Buffer<int>([], 4);
        ";

        var diagnostics = @"
            arguments in buffer creation cannot be omitted
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0508_InvalidBufferCreationArgument() {
        var text = @"
            var a = new Buffer<int>(10, [10]);
        ";

        var diagnostics = @"
            second argument in buffer creation must be an initializer list
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0509_LocalFunctionUsingEntryPointName() {
        var text = @"
            void [Main](int args) {

            }
        ";

        var diagnostics = @"
            local function uses the entry point name but is not treated as the entry point because it does not have the correct signature
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0510_ArgumentWrongConst() {
        var text = @"
            class A {
                public int f = 0;
            }

            void Func(A a) { }

            const a = new A();
            Func([a]);
        ";

        var diagnostics = @"
            argument 1: cannot pass a constant to a parameter expecting a variable
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0511_CantChangeConstOnOverride() {
        var text = @"
            class A {
                public virtual const void M() { }
            }

            class B extends A {
                public override void [M]() { }
            }

            ;
        ";

        var diagnostics = @"
            'B.M()': member must be marked 'const' when overriding inherited member 'A.M()' because it is marked 'const'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0512_DifferentConstOnOverride() {
        var text = @"
            class A {
                public virtual void M() { }
            }

            class B extends A {
                public override const void [M]() { }
            }

            ;
        ";

        var diagnostics = @"
            'B.M()': member is marked 'const' but overridden member 'A.M()' is not
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0513_CantChangeConstOnOverrideParameter() {
        var text = @"
            class A {
                public virtual void M(const int a) { }
            }

            class B extends A {
                public override void M(int [a]) { }
            }

            ;
        ";

        var diagnostics = @"
            'B.M(int!)': parameter 'a' must be marked 'const' when overriding inherited member 'A.M(const int!)' because the corresponding parameter is marked 'const'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Warning_BU0514_DifferentConstOnOverrideParameter() {
        var text = @"
            class A {
                public virtual void M(int a) { }
            }

            class B extends A {
                public override void M(const int [a]) { }
            }

            ;
        ";

        var diagnostics = @"
            'B.M(const int!)': parameter 'a' is marked 'const' but the corresponding parameter on overridden member 'A.M(int!)' is not
        ";

        AssertDiagnostics(text, diagnostics, _writer, true);
    }

    [Fact]
    public void Reports_Error_BU0515_CannotCreateEntryType() {
        var text = @"
            class Program;

            void Main() {
                var p = [new Program()];
            }
        ";

        var diagnostics = @"
            invalid object creation; cannot construct the type containing the entry point
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0516_NonNullableReceiver() {
        var text = @"
            class A {
                public int a = 0;
            }

            var a = new A();
            [a?.a] = 10;
        ";

        var diagnostics = @"
            cannot use a conditional field access because the receiver is not nullable; consider using a regular field access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0517_NonNullableReceiverArray() {
        var text = @"
            var a = new int\[10\];
            [a?\[0\]] = 10;
        ";

        var diagnostics = @"
            cannot use a conditional array access because the receiver is not nullable; consider using a regular array access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0518_NonNullableReceiverCall() {
        var text = @"
            class A {
                public void M() { }
            }

            var a = new A();
            [a?.M]();
        ";

        var diagnostics = @"
            cannot use a conditional call because the receiver is not nullable; consider using a regular call
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0519_NonNullableReceiverIndex() {
        var text = @"
            var a = ""test"";
            var b = [a?\[0\]];
        ";

        var diagnostics = @"
            cannot use a conditional indexer because the receiver is not nullable; consider using a regular indexer
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0520_CannotNullCheckNonNull() {
        var text = @"
            int a = 3;
            bool b = [a is null];
        ";

        var diagnostics = @"
            cannot perform an 'is null' check on an operand of type 'int!' because it is a non-nullable type
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0521_NullableReceiverProperty() {
        var text = @"
            Buffer<int>? a = {1, 2, 3};
            var b = [a.Length];
        ";

        var diagnostics = @"
            cannot access properties through a nullable receiver; consider using a null assert or conditional access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    [Fact]
    public void Reports_Error_BU0522_NonNullableReceiverProperty() {
        var text = @"
            Buffer<int> a = {1, 2, 3};
            var b = [a?.Length];
        ";

        var diagnostics = @"
            cannot use a conditional property access because the receiver is not nullable; consider using a regular property access
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }

    // ! Reports_Error_BU0523_SameFullNameAggAgg
    // ? Requires references (i.e. command-line args)

    // ! Reports_Warning_BU0524_DuplicateReference
    // ? Requires references (i.e. command-line args)

    // ! Reports_Warning_BU0525_DuplicateAssembly
    // ? Requires references (i.e. command-line args)

    [Fact]
    public void Reports_Error_BU0526_CannotCreateInterface() {
        var text = @"
            interface A { }
            var a = [new A()];
        ";

        var diagnostics = @"
            cannot create an instance of the interface 'A'
        ";

        AssertDiagnostics(text, diagnostics, _writer);
    }
}
