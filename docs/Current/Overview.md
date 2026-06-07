# 1 Overview

The Belte language is syntactically very similar to C#; Belte is a "C-Style" language that focuses on the
object-oriented paradigm.

Currently, the Belte compiler, Buckle, supports interpretation and building to a .NET executable.

> [Using the compiler CLI](../Buckle.md)

- [1.1](#11-conventions) Conventions
- [1.2](#12-endpoint-specific-features) Endpoint Specific Features
- [1.3](#13-keywords) Keywords
  - [1.3.1](#131-non-contextual-keywords) Non-Contextual Keywords
  - [1.3.2](#132-contextual-keywords) Contextual Keywords
- [1.4](#14-nullability-and-types) Nullability and Types
  - [1.4.1](#141-normal-types) Normal Types
  - [1.4.2](#142-pointers-and-function-pointers) Pointers and Function Pointers
  - [1.4.3](#143-initializers) Initializers
  - [1.4.4](#144-fields) Fields
  - [1.4.5](#145-implicit-typing) Implicit Typing
  - [1.4.6](#146-null-flow-analysis) Null-Flow Analysis
- [1.5](#15-differences-from-c) Differences from C#

## 1.1 Conventions

`// ...` and `/* ... */` in code samples refer to an arbitrary statement or expression or continuation of a pattern.

For example, if a code sample showed:

```belte
int a = /* ... */;
```

The usage of `/* ... */` is meant to imply the actual expression used does not matter for the sake of demonstrating the
feature being discussed.

Similarly, if a code sample showed:

```belte
void Func() {
  // ...
}
```

The usage of `// ...` is meant to imply the body of the function `Func` does not matter for the sake of the feature
being discussed.

Note that these shortcuts do not necessarily ensure correctness and are used for brevity. For example:

```belte
int Func() {
  // ...
}
```

In this example, `Func` in this state is invalid because it fails to return even though the function signature has a
return type of `int`. In this case the `// ...` implies an arbitrary valid return.

## 1.2 Endpoint Specific Features

Some features are not supported across all endpoints for various reasons.

The following list describes all of the features where full parity is not currently implemented or was not always
implemented.

- Evaluator: the internal interpreter endpoint. Used for the [REPL](../Repl.md), `--evaluate` builds, and [compile-time expressions](Data.md#37-compile-time-expressions).
- Executor: the endpoint for emitting to an in-memory delegate to execute immediately. This is the default endpoint and is used for `--execute` builds and [compile-time handles](LowLevelFeatures.md#613-compiler-handle).
- IL Emitter: the endpoint for emitting to an executable which relies on .NET. Used for `--dotnet` builds and [build scripts](../Build.md).

| Feature | Evaluator | Executor | IL Emitter | Explanation |
| - | - | - | - | - |
| `--type=graphics` projects | ✓ | ✓ | ✕ | Standalone graphics DLL under development |
| Non-type templates | ✓ | ✕ | ✕ | Not supported by the .NET runtime |
| Non-integral enums | ✓ | ✕ | ✕ | Not supported by the .NET runtime |
| Pointers | ✕ | ✓ | ✓ | Partially supported the Evaluator but not stable due to internal memory structure |
| Function pointers | ✕ | ✓ | ✓ | Disallowed in the Evaluator due to internal memory structure |
| Externs/DllImport | ✕ | ✓ | ✓ | Incompatible with the Evaluator |
| Inline IL | ✕ | ✓ | ✓ | Incompatible with the Evaluator |
| .NET DLL references | ✕ | ✓ | ✓ | Incompatible with the Evaluator |

## 1.3 Keywords

The following lists all keywords used in the language. No type names (e.g. `int`) are reserved.

Some keywords have multiple meanings depending on context. Those keywords will be disambiguated in the lists below.

### 1.3.1 Non-Contextual Keywords

These keywords are reserved names and cannot be used as identifiers.

- [abstract](ClassesAndObjects.md#435-sealed-and-abstract)
- [as](Data.md#32-operators)
- [base](ClassesAndObjects.md#413-base-access)
- [break](ControlFlow.md#245-break)
- [case](ControlFlow.md#25-switch)
- [catch](ControlFlow.md#261-trycatchfinally)
- [class](ClassesAndObjects.md#41-classes)
- [const](Data.md#331-modifiers) (data containers)
- [const](ClassesAndObjects.md#434-const) (methods)
- [constexpr](Data.md#331-modifiers) (locals and parameters)
- [constexpr](ClassesAndObjects.md#433-static-and-constexpr) (fields)
- [constructor](ClassesAndObjects.md#44-constructors-and-finalizers)
- [continue](ControlFlow.md#246-continue)
- [default](Data.md#314-default-literal) (literal)
- [default](ControlFlow.md#25-switch) (switch label)
- [defer](ControlFlow.md#28-defer-statements)
- [destructor](ControlFlow.md#291-destructors)
- [do](ControlFlow.md#242-do-while-loops)
- [else](ControlFlow.md#23-conditionals)
- [enum](ClassesAndObjects.md#46-enums)
- [extends](ClassesAndObjects.md#412-inheritance) (inheritance)
- [extends](ClassesAndObjects.md#4512-special-constraints) (template constraints)
- [extern](LowLevelFeatures.md#67-extern-methods)
- [false](Data.md#31-data-types)
- [final](Data.md#331-modifiers)
- [finalizer](ClassesAndObjects.md#44-constructors-and-finalizers)
- [finally](ControlFlow.md#261-trycatchfinally)
- [for](ControlFlow.md#243-for-loops) (for loop)
- [for](ControlFlow.md#244-for-each-loops) (for each loop)
- [global](ClassesAndObjects.md#483-global-using-directive) (using directive)
- [global](ClassesAndObjects.md#482-global-disambiguation) (disambiguation)
- [goto](ControlFlow.md#25-switch)
- [if](ControlFlow.md#23-conditionals) (conditional)
- [if](Preprocessor.md#72-control) (preprocessor)
- [il](LowLevelFeatures.md#611-inline-il)
- [in](ControlFlow.md#244-for-each-loops)
- [is](Data.md#32-operators)
- [isnt](Data.md#32-operators)
- [lowlevel](LowLevelFeatures.md#61-low-level-contexts)
- [nameof](Data.md#32-operators)
- [namespace](ClassesAndObjects.md#47-namespaces)
- [new](ClassesAndObjects.md#411-declaring-and-using-classes) (instantiation)
- [new](ClassesAndObjects.md#432-overriding-modifiers) (modifier)
- [null](Data.md#31-data-types)
- [nullptr](LowLevelFeatures.md#651-creating-and-dereferencing-pointers)
- [out](ControlFlow.md#216-ref-arguments)
- [override](ClassesAndObjects.md#432-overriding-modifiers)
- [pinned](LowLevelFeatures.md#612-pinned-locals)
- [private](ClassesAndObjects.md#431-accessibility-modifiers)
- [protected](ClassesAndObjects.md#431-accessibility-modifiers)
- [public](ClassesAndObjects.md#431-accessibility-modifiers)
- [ref](Data.md#35-references)
- [return](ControlFlow.md#21-functions)
- [scoped](ControlFlow.md#29-scoped-statements)
- [sealed](ClassesAndObjects.md#435-sealed-and-abstract) (classes)
- [sealed](ClassesAndObjects.md#432-overriding-modifiers) (members)
- [sizeof](LowLevelFeatures.md#69-sizeof-operator)
- [stackalloc](LowLevelFeatures.md#610-stackalloc-operator)
- [static](ClassesAndObjects.md#433-static-and-constexpr) (modifier)
- [static](ClassesAndObjects.md#48-using-directives) (using directive)
- [struct](ClassesAndObjects.md#49-structs)
- [switch](ControlFlow.md#25-switch)
- [this](ClassesAndObjects.md#411-declaring-and-using-classes)
- [throw](ControlFlow.md#26-exceptions-and-handling)
- [true](Data.md#31-data-types)
- [try](ControlFlow.md#261-trycatchfinally)
- [typeof](Data.md#32-operators)
- [unreachable](ControlFlow.md#210-unreachable-statements)
- [using](ClassesAndObjects.md#48-using-directives)
- [virtual](ClassesAndObjects.md#432-overriding-modifiers)
- [where](ClassesAndObjects.md#451-constraint-clauses)
- [while](ControlFlow.md#241-while-loops)
- [with](ControlFlow.md#27-with-expressions-and-statements)

### 1.3.2 Contextual Keywords

These keywords only act as keywords inside specific contexts. As such they can be used as identifiers in most places.

- [define](Preprocessor.md#71-defineundef)
- [elif](Preprocessor.md#72-control)
- [endif](Preprocessor.md#72-control)
- [explicit](ClassesAndObjects.md#4232-casts)
- [flags](ClassesAndObjects.md#461-flags)
- [handle](LowLevelFeatures.md#613-compiler-handle)
- [has](ClassesAndObjects.md#4512-special-constraints)
- [implicit](ClassesAndObjects.md#4232-casts) (user-defined conversions)
- [implicit](ControlFlow.md#217-argument-coercion) (argument coercion)
- [literal](ClassesAndObjects.md#4233-user-defined-literals)
- [notnull](ClassesAndObjects.md#4512-special-constraints)
- [noverify](LowLevelFeatures.md#6111-verification)
- [operator](ClassesAndObjects.md#423-operators) (normal operators)
- [operator](ControlFlow.md#244-for-each-loops) (for each operators)
- [packed](LowLevelFeatures.md#621-packing)
- [primitive](ClassesAndObjects.md#4512-special-constraints)
- [reverse](ControlFlow.md#271-reverse-methods)
- [undef](Preprocessor.md#71-defineundef)

## 1.4 Nullability and Types

Nullability is treated in a non-standard way in Belte. As such, a close read of the following section is recommended,
which is a consolidation of important nullability semantics found in the rest of the documentation.

To summarize:

- Reference types (classes) are nullable by default
- Value types (primitives, pointers, structs) are non-nullable by default
- `!` removes nullability
- `?` adds nullability
- Pointer types are never nullable

A type is "nullable" when it permits `null`.

### 1.4.1 Normal Types

Classes are reference types meaning they are heap allocated, garbage collected, and not copied when passed.

For example:

```belte
var a = new MyClass();
var b = a;
b.i = 5;

class MyClass {
  public int i = 0;
}
```

In this example, `a.i` is `5` because both `a` and `b` refer to the same object in memory.

Reference types are nullable by default. To make it non-nullable, a `!` annotation can be used:

```belte
var a = new MyClass();
a = null; // OK

var! a = new MyClass();
a = null; // Invalid
```

Notice how nullable annotations apply normally even when implicitly typing. The following are identical:

```belte
MyClass! a = new MyClass();
var! a = new MyClass();
```

All non-reference types are non-nullable by default (this includes primitives, structs, and pointers). The only
exception is the primitive `any` type which is nullable by default.

To make a non-reference type nullable, a `?` annotation can be used:

```belte
int a = 3;
a = null; // Invalid

int? a = 3;
a = null; // OK
```

Most value-types (non-reference-types) are able to made nullable. Pointers and function pointers are not.

In much of the documentation and standard library, redundant annotations are used for clarity. Note that the following
are identical because `int` defaults to being non-nullable:

```belte
int a = 3;
int! a = 3;
```

Likewise, the following are identical because class types default to being nullable:

```belte
MyClass a = new MyClass();
MyClass? a = new MyClass();
```

Redundant annotations are encouraged in source docs for clarity.

### 1.4.2 Pointers and Function Pointers

Nullability in pointers is not covered with the type system, but rather with the `nullptr` keyword. A `nullptr` under
the hood is just a 0-valued pointer. The following are equivalent:

```belte
int* ptr = nullptr;
int* ptr = (int*)0;
int* ptr;
```

Even though pointers and function pointers themselves do not use the nullable type system, their elements can:

```belte
int?* ptr; // OK
int?*? ptr; // Invalid

void(int?)* ptr; // OK
void(int?)*? ptr; // Invalid
```

### 1.4.3 Initializers

Local initializers are required for non-nullable types. For nullable types, initializers are optional and a local
without one will be set to null. The following are identical:

```belte
int? a = null;
int? a;
```

The exception to this is pointers and function pointers. They are set to `nullptr` by default if no initializer is
present. The following are identical:

```belte
int* ptr = nullptr;
int* ptr;
```

When implicitly typing, the inferred type is usually the direct type of the initializer. The following are identical:

```belte
int a = 3;
var a = 3;
```

The exception to this is object creation expressions, which will automatically "lift", meaning that an implicit local
with an initializer that is an object creation expression will default to being nullable. The following are identical:

```belte
MyClass a = new MyClass();
MyClass? a = new MyClass();
var a = new MyClass();
var? a = new MyClass();
```

As the object creation itself is not nullable, they can be used for non-nullable reference types. The following are
identical:

```belte
MyClass! a = new MyClass();
var! a = new MyClass();
```

### 1.4.4 Fields

Unlike locals, non-nullable fields do not require an initializer and instead are set to a default value. Struct fields
cannot have initializers at all. The default value of numeric types is `0`, for booleans `false`, and an empty string
for strings.

An mentioned earlier, pointers and function pointers default to `nullptr`.

A non-nullable struct's default value is a created struct where each field is set to it's default value. For example:

```belte
var a = new MyClass();
var b = a.str.num;
// b equals 0

class MyClass {
  MyStruct str;
}

struct MyStruct {
  int num;
}
```

### 1.4.5 Implicit Typing

`var`, `const`, and `constexpr` can be used when defining a local indicating that their type should be inferred. The
inferred type is usually the exact type of the initializer. The following are identical:

```belte
int a = 3;
var a = 3;
```

Nullability persists when inferring a type. The following are identical:

```belte
int? a = Func();
var a = Func();

int? Func() { /* ... */ }
```

`var` means a normal local declaration. `const` and `constexpr` infer the type of a local with the respective modifiers.
The following are identical:

```belte
const int a = 3;
const a = 3;

constexpr int a = 3;
constexpr a = 3;
```

`const` means the local cannot be assigned to or otherwise modified. `constexpr` means the value of the local is a
compile-time constant that will be substituted at compile time. For classes, a `const` modifier means fields can only be
read but not written to, and only methods marked `const` can be called. Class-types cannot use the `constexpr` modifier
because they are not compile-time constants.

### 1.4.6 Null-Flow Analysis

Belte does not currently perform automatic null-flow analysis. Instead, explicit null checks and control flow should be
used.

When a value is needed to be non-null at a certain time, the [`!` operator](Data.md#3221-x) can be used that asserts
that the value is not null, otherwise a runtime error occurs. For example:

```belte
int? a = Func();
int! b = a!; // If a is null, runtime exception
```

To conditionally execute code if a value is not null, a
[null-binding contract](ControlFlow.md#232-null-binding-contracts) can be used:

```belte
int? a = Func();

if (a -> b!) {
  // ...
}
```

In this example, `b` is declared as a non-nullable local set to the value of `a`, so in this case the type of `b` is
`int!`. `b` only lives inside of the block.

To use a type's default value in the case of null, the [`?` operator](Data.md#3222-x) can be used. For example:

```belte
bool? a = Func();

if (a?) {
  // ...
} else {
  // ...
}
```

In this example, the else block is executed if `a` is false or null. Note that conditions inside if, for, and while
constructs can be nullable. If the condition is null at runtime, an exception is thrown. For example:

```belte
bool? a = Func();

if (a) {
  // ...
}
```

In this example, if `a` is null, a runtime exception is thrown at the if condition.

## 1.5 Differences from C\#

Belte is similar enough to C# so that the differences are more notable than the similarities. The following is a list of
most of the differences to make it more clear where the language is unique with links to relevant doc sections:

- [Enforced non-nullable reference types](#14-nullability-and-types)
- [Extremely flexible meta-programming](LowLevelFeatures.md#613-compiler-handle)
- [Compile-time expressions](Data.md#37-compile-time-expressions)
- [Optional build scripts instead of project files](../Build.md)
- No interfaces
- No properties
- No extension methods (yet)
- No array covariance
- [Class fields have no default value](ClassesAndObjects.md#421-fields)
- [`defer` statements](ControlFlow.md#28-defer-statements)
- [`with` expressions and statements](ControlFlow.md#27-with-expressions-and-statements)
- [Duck-typed `scoped` statements instead of `using` statements](ControlFlow.md#29-scoped-statements)
- [`destructor` keyword](ControlFlow.md#291-destructors)
- [User-defined literals](ClassesAndObjects.md#4233-user-defined-literals)
- [File-scoped classes](ClassesAndObjects.md#411-declaring-and-using-classes)
- Primitive types (e.g. `int`) don't have members
- [`constructor` and `finalizer` keywords](ClassesAndObjects.md#44-constructors-and-finalizers)
- Types are not reserved names (including primitives)
- [`unreachable` statements](ControlFlow.md#210-unreachable-statements)
- [`const` and `final` locals and fields with reference types instead of `readonly`](Data.md#331-modifiers)
- [`const` methods](ClassesAndObjects.md#434-const)
- [`constexpr` locals and fields](ClassesAndObjects.md#433-static-and-constexpr)
- [First-class `flags` enums](ClassesAndObjects.md#461-flags)
- [`extends` keyword for base lists](ClassesAndObjects.md#412-inheritance)
- [Different generic/template constraints include expression constraints](ClassesAndObjects.md#451-constraint-clauses)
- [Duck-typed `for` "each" loops with index support](ControlFlow.md#244-for-each-loops)
- [Inline-IL blocks](LowLevelFeatures.md#611-inline-il)
- [`isnt` instead of `is not`](Data.md#32-operators)
- [`lowlevel` contexts](LowLevelFeatures.md#61-low-level-contexts)
- [C++-style `nullptr` literal](LowLevelFeatures.md#651-creating-and-dereferencing-pointers)
- [`out` parameters don't require assignment](ControlFlow.md#216-ref-arguments)
- [GC `pinned` locals](LowLevelFeatures.md#612-pinned-locals)
- No `internal`/`private protected`/`protected internal` accessibilities (yet)
- [Switch cases don't require `break` statements](ControlFlow.md#25-switch)
- No catch filter blocks (yet)
- [Conditionals accept expressions of type `bool?` instead of `bool`](ControlFlow.md#231-null-conditions)
- [Null-binding contracts](ControlFlow.md#232-null-binding-contracts)
- Pointers and other low-level features don't require `unsafe` contexts
- [More concise function and function pointer type syntax](Data.md#313-function-type)
- [More concise unmanaged function pointer type syntax](LowLevelFeatures.md#66-function-pointers)
- [More concise calling convention syntax](LowLevelFeatures.md#661-calling-conventions)
- [Fixed fields don't require a `fixed` keyword](LowLevelFeatures.md#68-fixed-size-buffers)
- [C-style stackalloc syntax](LowLevelFeatures.md#6101-stackalloc-locals)
- [Explicitly-named sized numerics (e.g. `uint16`)](LowLevelFeatures.md#64-numerics)
- [`winbool` type instead of marshalling `bool` as 4-bytes in `extern`s](LowLevelFeatures.md#671-winbool)
- `bool` marshals as 1 byte in `extern`s
- [String interpolation uses `f""` instead of `$""`](Data.md#312-string-interpolation)
- [More expressive implicit typing allowing with `var`, `const`, and `constexpr` and nullable annotations](Data.md#332-implicit-typing)
- [Argument coercion with `implicit` keyword](ControlFlow.md#217-argument-coercion)
- [More operators (`x!`, `x!!`, `x?`, `x /\ y`, `x \/ y`, `x >< [y, z]`, `x ?! y`, `x..y`, `x?..y`)](Data.md#322-uncommon-operators)
- Structs cannot have field initializers
- [Enums can have methods](ClassesAndObjects.md#465-methods)
- [Implicit enum fields](ClassesAndObjects.md#462-implicit-enum-fields)
- [More concise enum bit testing](ClassesAndObjects.md#464-bit-testing)
- [C-style `union`s and anonymous unions](ClassesAndObjects.md#491-unions)
- [First-class bit casting](LowLevelFeatures.md#641-bit-casts)
- [`out` parameters can have a default value](ControlFlow.md#2161-out-arguments)
- [C-string literals](LowLevelFeatures.md#614-c-strings)
- [`using` aliases can be placed anywhere instead of only before all members](ClassesAndObjects.md#481-aliasing)
- Struct layout efficiency analysis
- [`packed` keyword instead of StructLayout attribute](LowLevelFeatures.md#621-packing)
- [Experimental: Non-numeric enum underlying types](ClassesAndObjects.md#463-experimental-underlying-types)
- [Experimental: Non-type generics/templates](ClassesAndObjects.md#45-templates)
- Experimental: Integrated graphics support with `Update()` point
