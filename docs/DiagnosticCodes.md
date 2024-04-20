# Diagnostic Codes

- [Compiler Diagnostics](#compiler-diagnostics)
- [Command Line Diagnostics](#command-line-diagnostics)
- [Repl Diagnostics](#repl-diagnostics)

A more in-depth explanation on any diagnostic can be seen using the Buckle program,
[here](./Buckle.md#explainbureclcode).

## Compiler Diagnostics

| Code | Severity | Message |
|-|-|-|
| BU0001 | Warning | expression will always result to '{0}' |
| BU0002 | Warning | deference of a possibly null value |
| BU0003 | Error | {0}: no such file or invalid file type |
| BU0004 | Error | '{0}' is not a valid '{1}' |
| BU0005 | Error | unexpected character '{0}' |
| BU0006 | Error | unexpected {0} |
| BU0007 | Error | cannot convert from type '{0}' to '{0}' implicitly; an explicit conversion exists (are you missing a cast?) |
| BU0008 | Error | unary operator '{0}' is not defined for type '{1}' |
| BU0009 | Error | all named arguments must come after any unnamed arguments |
| BU0010 | Error | named argument '{0}' cannot be specified multiple times |
| BU0011 | Error | binary operator '{0}' is not defined for types '{1}' and '{2}' |
| BU0012 | Error | multiple files with global statements creates ambiguous entry point |
| BU0013 | Error | cannot reuse parameter name '{0}'; parameter names must be unique |
| BU0014 | Error | invalid main signature: must return void or int and take in no arguments or take in 'int! argc, string[]! argv' |
| BU0015 | Error | method '{0}' does not have a parameter named '{1}' |
| BU0016 | Error | declaring a main method and using global statements creates ambiguous entry point |
| BU0017 | Error | undefined symbol '{0}' |
| BU0018 | Error | type '{0}' already declares a member named '{1}' with the same parameter types |
| BU0019 | Error | not all code paths return a value |
| BU0021 | Error | cannot convert from type '{0}' to '{1}' |
| BU0021 | Error | variable '{0}' is already declared in this scope |
| BU0022 | Error | '{0}' cannot be assigned to as it is a constant |
| BU0023 | Error | ambiguous which if-statement this else-clause belongs to; use curly braces |
| BU0024 | Error | expression must have a value |
| BU0025 | Error | cannot apply indexing with [] to an expression of type '{0}' |
| BU0026 | Warning | unreachable code |
| BU0027 | Error | unterminated string literal |
| BU0028 | Error | undefined method '{0}' |
| BU0029 | Error | method '{0}' expects {1} arguments, got {2} |
| BU0031 | Error | class '{0}' has already been declared in this scope |
| BU0031 | Error | attribute '{0}' has already been applied |
| BU0032 | Error | called object '{0}' is not a method |
| BU0033 | Error | only assignment and call expressions can be used as a statement |
| BU0034 | Error | unknown type '{0}' |
| BU0035 | Error | {0} statements can only be used within a loop |
| BU0036 | Error | return statements can only be used within a method |
| BU0037 | Error | cannot return a value in a method returning void |
| BU0038 | Error | cannot return without a value in a method returning non-void |
| BU0039 | Error | method '{0}' cannot be used as a variable |
| BU0041 | Error | implicitly-typed variable must have initializer |
| BU0041 | Error | unterminated multi-line comment |
| BU0042 | Error | cannot initialize an implicitly-typed variable with 'null' |
| BU0043 | Error | cannot initialize an implicitly-typed variable with an empty initializer list |
| BU0044 | Error | collection dimensions on implicit types are inferred making them not necessary in this context |
| BU0045 | Error | cannot use implicit-typing in this context |
| BU0046 | Error | try statement must have a catch or finally |
| BU0047 | Error | cannot declare instance members in a static class |
| BU0048 | Error | expected overloadable unary or binary operator |
| BU0049 | Error | a by-reference variable must be initialized with a reference |
| BU0051 | Error | cannot initialize a by-value variable with a reference |
| BU0051 | Error | unknown attribute '{0}' |
| BU0052 | Error | cannot assign 'null' to a non-nullable variable |
| BU0053 | Error | implicit types infer reference types making the 'ref' keyword not necessary in this context |
| BU0054 | Error | cannot assign a reference to a constant to a by-reference variable expecting a reference to a variable |
| BU0055 | Error | cannot use void as a type |
| BU0056 | Error | expected {0} |
| BU0057 | Error | no overload for method '{0}' matches parameter list |
| BU0058 | Error | call is ambiguous between |
| BU0059 | Error | the operand of an increment or decrement operator must be a variable, field, or indexer |
| BU0061 | Error | ternary operator '{0}' is not defined for types '{1}', '{2}', and '{3}' |
| BU0061 | Error | '{0}' contains no such member '{1}' |
| BU0062 | Error | left side of assignment operation must be a variable, field, or indexer |
| BU0063 | Error | cannot overload nested functions; nested function '{0}' has already been defined |
| BU0064 | Error | cannot assign a reference to a variable to a by-reference variable expecting a reference to a constant |
| BU0065 | Error | prefix operator '{0}' is not defined for type '{1}' |
| BU0066 | Error | postfix operator '{0}' is not defined for type '{1}' |
| BU0067 | Error | named argument '{name}' specifies a parameter for which a positional argument has already been given |
| BU0068 | Error | default values for parameters must be compile-time constants |
| BU0069 | Error | all optional parameters must be specified after any required parameters |
| BU0071 | Error | cannot mark a type as both constant and variable |
| BU0071 | Error | variable name '{0}' is not valid as it is the name of a type in this namespace |
| BU0072 | Error | cannot implicitly pass null in a non-nullable context |
| BU0073 | Error | cannot convert 'null' to '{0}' because it is a non-nullable type |
| BU0074 | Error | modifier '{0}' has already been applied to this item |
| BU0075 | Error | cannot use a reference type in this context |
| BU0076 | Error | cannot divide by zero |
| BU0077 | Error | a local named '{0}' cannot be declared in this scope because that name is used in an enclosing scope to define a local or parameter |
| BU0078 | Error | cannot initialize an implicitly-typed variable with an initializer list only containing 'null' |
| BU0079 | Error | unrecognized escape sequence '\\{0}' |
| BU0081 | Error | primitive types do not contain any members |
| BU0081 | Error | type '{0}' is a primitive; primitives cannot be created with constructors |
| BU0082 | Error | no overload for template '{0}' matches template argument list |
| BU0083 | Error | template is ambiguous between |
| BU0084 | Error | cannot use structs outside of a low-level context |
| BU0085 | Error | cannot use 'this' outside of a class |
| BU0086 | Error | constructor name must match the name of the enclosing class; in this case constructors must be named '{0}' |
| BU0087 | Error | type '{0}' does not contain a constructor that matches the parameter list |
| BU0088 | Error | modifier '{0}' is not valid for this item |
| BU0089 | Error | member '{0}' cannot be accessed with an instance reference; qualify it with the type name instead |
| BU0090 | Error | an object reference is required for non-static member '{0}' |
| BU0091 | Error | cannot initialize fields in structure definitions |
| BU0092 | Error | cannot have multiple 'Main' entry points |
| BU0093 | Error | attributes are not valid in this context |
| BU0094 | Error | item '{0}' does not expect any template arguments |
| BU0095 | Error | template argument must be a compile-time constant |
| BU0096 | Error | cannot reference non-field or non-variable item |
| BU0097 | Error | '{0}' is a type, which is not valid in this context |
| BU0098 | Error | static classes cannot have constructors |
| BU0099 | Error | cannot declare a variable with a static type |
| BU0100 | Error | cannot create an instance of the static class '{0}' |
| BU0101 | Error | cannot mark member as both {0} and {1} |
| BU0102 | Error | cannot assign to an instance member in a method marked as constant |
| BU0103 | Error | cannot call non-constant method '{0}' in a method marked as constant |
| BU0104 | Error | cannot call non-constant method '{0}' on constant |
| BU0105 | Error | reference type cannot be marked as a constant expression because references are not compile-time constants |
| BU0106 | Error | expression is not a compile-time constant |
| BU0107 | Error | static types cannot be used as return types |
| BU0108 | Error | overloaded operator '{0}' takes {1} parameters |
| BU0109 | Error | overloaded operators must be marked as static |
| BU0110 | Error | static classes cannot contain operators |

## Command Line Diagnostics

| Code | Severity | Message |
|-|-|-|
| CL0001 | Error | missing filename after '-o' |
| CL0002 | Error | cannot specify '--explain' more than once |
| CL0003 | Error | missing diagnostic code after '--explain' (usage: '--explain[BU\|RE\|CL]<code>') |
| CL0004 | Error | missing name after '{0}' (usage: '--modulename=<name>') |
| CL0005 | Error | missing name after '{0}' (usage: '--ref=<name>') |
| CL0006 | Error | failed to open file '{0}'; most likely due to the file being used by another process |
| CL0007 | Error | missing severity after '{0}' (usage: '--severity=<severity>') |
| CL0008 | Error | unrecognized severity '{0}' |
| CL0009 | Error | unrecognized command line option '{0}'; see 'buckle --help' |
| CL0010 | Info | all arguments are ignored when invoking the Repl |
| CL0011 | Fatal | cannot specify '-p', '-s', '-c', or '-t' with .NET integration |
| CL0012 | Fatal | cannot specify output file with '-p', '-s', '-c', or '-t' with multiple files |
| CL0013 | Fatal | cannot specify output path or use '-p', '-s', '-c', or '-t' with interpreter |
| CL0014 | Fatal | cannot specify module name without .NET integration |
| CL0015 | Fatal | cannot specify references without .NET integration |
| CL0016 | Fatal | no input files |
| CL0017 | Error | {0}: no such file or directory |
| CL0018 | Info | unknown file type of input file '{0}'; ignoring |
| CL0019 | Error | '{0}' is not a valid error code; must be in the format: [BU\|CL\|RE]<code> |
| CL0020 | Info | {0}: file already compiled; ignoring |
| CL0021 | Error | '{0}' is not a used error code |
| CL0022 | Fatal | cannot pass multiple files when running as a script |
| CL0023 | Fatal | cannot interpret file |
| CL0024 | Error | missing warning level after '{0}' (usage: '--warnlevel=<warning level>') |
| CL0025 | Error | invalid warning level '{0}'; warning level must be a number between 0 and 2 |
| CL0026 | Error | missing warning code after '{0}' (usage: '--wignore=<[BU\|RE\|CL]<code>,...>') |
| CL0027 | Error | missing warning code after '{arg}' (usage: '--winclude=<[BU\|RE\|CL]<code>,...>') |
| CL0028 | Error | '{0}' is not the code of a warning |

## Repl Diagnostics

| Code | Severity | Message |
|-|-|-|
| RE0001 | Error | unknown repl command '{0}' |
| RE0002 | Error | invalid number of arguments |
| RE0003 | Error | undefined symbol '{0}' |
| RE0004 | Error | {0}: no such file |
| RE0005 | Error | invalid argument '{0}'; expected argument of type '{0}' |
| RE0006 | Error | no such method with the signature '{0}' exists |
| RE0007 | Error | '{0}' is ambiguous between |
| RE0008 | Error | failed to generate IL: cannot reference locals or globals from previous submissions with the '#showIL' toggle on |
