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
  - [1.4.7](#147-arrays) Arrays
  - [1.4.8](#148-nullability-in-depth) Nullability In-Depth
  - [1.4.9](#149-object-and-valuetype) Object and ValueType
- [1.5](#15-differences-from-c) Differences from C#
  - [1.5.1](#151-type-system) Type System
  - [1.5.2](#152-language-features) Language Features
  - [1.5.3](#153-metaprogramming) Metaprogramming
  - [1.5.4](#154-low-level-programming--interop) Low-Level Programming & Interop
- [1.6](#16-identifiers) Identifiers

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
- [class](ClassesAndObjects.md#41-classes) (type declaration)
- [class](ClassesAndObjects.md#4512-special-constraints) (template constraint)
- [commit](ControlFlow.md#271-commit-statements)
- [const](Data.md#331-modifiers) (data container modifier)
- [const](ControlFlow.md#212-const-parameters) (parameter modifier)
- [const](ClassesAndObjects.md#434-const) (method modifier)
- [constexpr](Data.md#331-modifiers) (local and parameter modifier)
- [constexpr](ClassesAndObjects.md#433-static-and-constexpr) (field modifier)
- [constructor](ClassesAndObjects.md#44-constructors-and-finalizers)
- [continue](ControlFlow.md#246-continue)
- [default](Data.md#315-default-literal) (literal)
- [default](ControlFlow.md#25-switch) (switch label)
- [defer](ControlFlow.md#28-defer-statements) (defer)
- [defer](ControlFlow.md#211-reverse-statements) (reverse defer)
- [destructor](ControlFlow.md#291-destructors)
- [do](ControlFlow.md#242-do-while-loops)
- [else](ControlFlow.md#23-conditionals)
- [enum](ClassesAndObjects.md#46-enums)
- [extends](ClassesAndObjects.md#412-inheritance) (inheritance)
- [extends](ClassesAndObjects.md#4512-special-constraints) (template constraint)
- [extern](LowLevelFeatures.md#67-extern-methods) (modifier)
- [extern](LowLevelFeatures.md#673-extern-blocks) (member block)
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
- [implements](ClassesAndObjects.md#412-inheritance) (inheritance)
- [implements](ClassesAndObjects.md#4512-special-constraints) (template constraint)
- [in](ControlFlow.md#244-for-each-loops)
- [interface](ClassesAndObjects.md#410-interfaces)
- [is](Data.md#32-operators)
- [isnt](Data.md#32-operators)
- [lowlevel](LowLevelFeatures.md#61-low-level-contexts) (scope modifier)
- [lowlevel](LowLevelFeatures.md#615-lowlevel-fields) (field modifier)
- [lowlevel](LowLevelFeatures.md#616-lowlevel-default-literal) (lowlevel default literal)
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
- [reverse](ClassesAndObjects.md#4222-state-and-reverse-clauses) (method clause)
- [reverse](ControlFlow.md#211-reverse-statements) (statement)
- [reversible](ControlFlow.md#211-reverse-statements)
- [scoped](ControlFlow.md#29-scoped-statements)
- [sealed](ClassesAndObjects.md#435-sealed-and-abstract) (class modifier)
- [sealed](ClassesAndObjects.md#432-overriding-modifiers) (member modifier)
- [sizeof](LowLevelFeatures.md#69-sizeof-operator)
- [stackalloc](LowLevelFeatures.md#610-stackalloc-operator)
- [static](ClassesAndObjects.md#433-static-and-constexpr) (modifier)
- [static](ClassesAndObjects.md#48-using-directives) (using directive)
- [struct](ClassesAndObjects.md#49-structs) (type declaration)
- [struct](ClassesAndObjects.md#4512-special-constraints) (template constraint)
- [switch](ControlFlow.md#25-switch)
- [this](ClassesAndObjects.md#411-declaring-and-using-classes)
- [throw](ControlFlow.md#26-exceptions-and-handling)
- [true](Data.md#31-data-types)
- [try](ControlFlow.md#261-trycatchfinally)
- [typeof](Data.md#32-operators)
- [union](ClassesAndObjects.md#491-unions)
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
- [get](ClassesAndObjects.md#424-properties)
- [handle](LowLevelFeatures.md#613-compiler-handle)
- [has](ClassesAndObjects.md#4512-special-constraints)
- [implicit](ClassesAndObjects.md#4232-casts) (user-defined conversions)
- [implicit](ControlFlow.md#217-argument-coercion) (argument coercion)
- [initializes](ClassesAndObjects.md#4211-definite-assignment)
- [literal](ClassesAndObjects.md#4233-user-defined-literals)
- [notnull](ClassesAndObjects.md#4512-special-constraints)
- [noverify](LowLevelFeatures.md#6111-verification)
- [operator](ClassesAndObjects.md#423-operators) (normal operators)
- [operator](ControlFlow.md#244-for-each-loops) (for each operators)
- [packed](LowLevelFeatures.md#621-packing)
- [properties](ClassesAndObjects.md#424-properties)
- [set](ClassesAndObjects.md#424-properties)
- [state](ClassesAndObjects.md#4222-state-and-reverse-clauses)
- [undef](Preprocessor.md#71-defineundef)

## 1.4 Nullability and Types

Nullability is treated in a non-standard way in Belte. As such, a close read of the following section is recommended,
which is a consolidation of important nullability semantics found in the rest of the documentation.

To summarize:

- Types are non-nullable by default
- `!` removes nullability
- `?` adds nullability
- Pointer types are never nullable

A type is "nullable" when it permits the sentinel value `null`.

A slightly more in-depth explanation of nullability can be found at this
[end of the section](#148-nullability-in-depth).

### 1.4.1 Normal Types

Every type `T` has two variants:

- `T!`: the non-nullable variant.
- `T?`: the nullable variant.

When a nullability annotation (`!` or `?`) is omitted, the non-nullable variant is assumed:

```belte
int x = 3; // Equivalent to `int! x = 3;`
int? y = null;
```

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

Reference types are non-nullable by default. To make it nullable, a `?` annotation can be used:

```belte
var a = new MyClass();
a = null; // Invalid

var? a = new MyClass();
a = null; // Okay
```

Notice how nullable annotations apply normally even when implicitly typing. The following are identical:

```belte
MyClass! a = new MyClass();
var! a = new MyClass();
```

Value types are also non-nullable by default (this includes primitives, structs, and pointers).

To make a non-reference type nullable, a `?` annotation can be used just like reference types:

```belte
int a = 3;
a = null; // Invalid

int? a = 3;
a = null; // OK
```

Most value-types (non-reference-types) are able to made nullable. Pointers and function pointers are not.

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

When implicitly typing, the inferred type is the direct type of the initializer. The following are identical:

```belte
int a = 3;
var a = 3;
```

### 1.4.4 Fields

Unlike locals, non-nullable struct fields do not allow initializers and instead are set to a default value. The default
value of numeric types is `0`, for booleans `false`, and an empty string for strings.

An mentioned earlier, pointers and function pointers default to `nullptr`.

A non-nullable struct's default value is a created struct where each field is set to it's default value. For example:

```belte
var a = new MyClass();
var b = a.str.num;
// b equals 0

class MyClass {
  MyStruct str = default;
}

struct MyStruct {
  int num;
}
```

Class fields always require an initializer or definite assignment, which can be
[read about here](ClassesAndObjects.md#4211-definite-assignment).

Since structs set their fields to their default value, the default value of a struct is a struct where every field is
set to it's default value. If a struct contains a field that has no default value, the struct also has no default value:

```belte
class A { }

struct MyStruct {
  A a;

  constructor(A a) {
    this.a = a;
  }
}

MyStruct myStruct = default; // Invalid because type `A` has no default value
```

### 1.4.5 Implicit Typing

`var`, `const`, `final`, and `constexpr` can be used when defining a local indicating that their type should be
inferred. The inferred type is usually the exact type of the initializer. The following are identical:

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

`var` means a normal local declaration. `const`, `final`, and `constexpr` infer the type of a local with the respective
modifiers. The following are identical:

```belte
const int a = 3;
const a = 3;

constexpr int a = 3;
constexpr a = 3;

final int a = 3;
final a = 3;
```

`const` means the local cannot be assigned to or otherwise modified. `constexpr` means the value of the local is a
compile-time constant that will be substituted at compile time. For classes, a `const` modifier means fields can only be
read but not written to, and only methods marked `const` can be called. Class-types cannot use the `constexpr` modifier
because they are not compile-time constants. `final` means the local cannot be assigned to but can be modified. This
means any class method can be called or array elements can be modified.

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

### 1.4.7 Arrays

Arrays are a collection of elements. Elements cannot be read before they are written:

```belte
var arr = new int[10];
arr[0]; // Exception
```

```belte
var arr = new int[10];
arr[0] = 45;
arr[0]; // Okay
```

A [`Buffer<T>`](LowLevelFeatures.md#63-arrays-and-buffers) can be used to avoid these checks, but should be avoided if
possible because it potentially allows reading invalid values for a type:

```belte
class A { }

var buffer = new Buffer<A>(10);
A a = buffer[0]; // null
```

In the above example, the non-nullable local `a` is given a null value from the buffer. Buffers should only be used when
performance is critical or working with unconstrained templates.

### 1.4.8 Nullability In-Depth

A type `T` represents a set of possible values that share some common property. Each type `T` has two variants, a
null-permitting `T?` and a null-forbidding `T!`. `null` is a sentinel value outside of the normal set `{ T1, T2, ... }`,
meaning `T?` contains the elements `{ T1, T2, ... } ∪ { NULL }`. `{ NULL }` is a set of a single element `null` which is
a sentinel value that is equal to itself but not equal to any other value. This is notably different than in relational
theory where null represents an unknown value. When declaring data containers (variables, constants, fields, etc.),
omitting a nullability annotation `!` or `?` will default to the null-forbidding variant. For example:

```belte
int a = 3; // Short-hand for `int! a = 3;`
```

For example, given a set of integer values within a certain range `{ INT }`, the null-forbidding variant `int!` is
defined as `int = int! = { INT }`, while the null-permitting variant `int?` is defined as `int? = { INT } ∪ { NULL }`.

In most of the language, a variant of a given type will be used instead of the base type. The exception to this is an
unconstrained type [template parameter](ClassesAndObjects.md#45-templates) which disallows nullable annotations as it
represents the possibility of both nullable and non-nullable types. Hence given
`class A<type T>`, `T t = null` and `T t = default` are both not allowed as `T` does not represent either `T!` or `T?`
specifically.

Unconstrained type template parameters have no defined behavior beyond passing it around and
calling methods of `Object` on it because all that is known about it is that it ultimately derives from
[`Object`](#149-object-and-valuetype). Many [non-bounding constraints](ClassesAndObjects.md#4512-special-constraints)
(that is, a constraint that does not specify that `T` derives from a class or implements an interface) exist to allow
interacting with a type template parameter, including excluding any null-permitting `T?` with `where { T is notnull; }`.

### 1.4.9 Object and ValueType

Belte uses the same object model as .NET. I.e. all non-pointer types ultimately derive from `Object`. Any type deriving
from `Object` is a reference type unless it derives `Object` through the type `ValueType` which directly derives
`Object`. Ordinary struct types and primitives directly derive `ValueType` which in turn derives `Object` meaning they
are value types. All enum types derive `Enum` which in turn derives `ValueType`.

Interfaces do not explicitly derive from `Object` as they are not concrete types, but interface values are still treated
as Objects. This means methods of `Object` can be called on an interface receivers.

Pointers are special types that map directly to machine addresses so they do not derive from `Object` or `ValueType`.
Since .NET generic type parameters (type template parameters) guarantee that they are ultimately derived from `Object`,
pointer types cannot be used as type template arguments.

## 1.5 Differences from C\#

Belte is similar enough to C# so that the differences are more notable than the similarities. The following lists link
to relevant doc sections.

To summarize the main differences:

- First-class nullability and stronger initialization guarantees
- Compile-time metaprogramming
- Reversible execution
- First-class low-level programming without unsafe contexts
- No properties

### 1.5.1 Type System

- [First-class uniform nullability across reference and value types](#14-nullability-and-types)
- [Class fields have no default value](ClassesAndObjects.md#421-fields)
- [Class field definite assignment guarantees](ClassesAndObjects.md#4211-definite-assignment)
- [Arrays prevent reading before writing to elements](#147-arrays)
- [Null-binding contracts](ControlFlow.md#232-null-binding-contracts)
- [Different generic/template constraints include expression constraints](ClassesAndObjects.md#451-constraint-clauses)
- [Conditionals accept expressions of type `bool?` instead of `bool`](ControlFlow.md#231-null-conditions)
- [More expressive implicit typing allowing with `var`, `const`, and `constexpr` and nullable annotations](Data.md#332-implicit-typing)
- [Enums can have methods](ClassesAndObjects.md#465-methods)
- [Built-in MustUseReturnValue attribute](ClassesAndObjects.md#411-attributes)

### 1.5.2 Language Features

- [Reversible methods](ClassesAndObjects.md#4222-state-and-reverse-clauses)
- [Reversible statements](ControlFlow.md#211-reverse-statements)
- [`defer` statements](ControlFlow.md#28-defer-statements)
- [`with` expressions and statements](ControlFlow.md#27-with-expressions-and-statements)
- [Duck-typed `scoped` statements instead of `using` statements](ControlFlow.md#29-scoped-statements)
- [User-defined literals](ClassesAndObjects.md#4233-user-defined-literals)
- [Duck-typed `for` "each" loops with index support](ControlFlow.md#244-for-each-loops)
- [`const` methods](ClassesAndObjects.md#434-const)
- [`constexpr` locals and fields](ClassesAndObjects.md#433-static-and-constexpr)
- [File-scoped classes](ClassesAndObjects.md#411-declaring-and-using-classes)
- [`unreachable` statements](ControlFlow.md#210-unreachable-statements)
- [First-class `flags` enums](ClassesAndObjects.md#461-flags)
- [`out` parameters don't require assignment](ControlFlow.md#216-ref-arguments)
- [`out` parameters can have a default value](ControlFlow.md#2161-out-arguments)
- [More operators (`x!`, `x!!`, `x?`, `x /\ y`, `x \/ y`, `x..y`, etc.)](Data.md#322-uncommon-operators)
- Numeric literals automatically shrink/expand to fit the context (i.e. `f` suffix for float literals is unnecessary)

### 1.5.3 Metaprogramming

- [Compile-time expressions](Data.md#37-compile-time-expressions)
- [Optional build scripts instead of project files](../Build.md)
- [Experimental: flexible meta-programming](LowLevelFeatures.md#613-compiler-handle)

### 1.5.4 Low-Level Programming & Interop

- Pointers and other low-level features don't require `unsafe` contexts
- [`lowlevel` contexts](LowLevelFeatures.md#61-low-level-contexts)
- [Inline-IL blocks](LowLevelFeatures.md#611-inline-il)
- [C++-style `nullptr` literal](LowLevelFeatures.md#651-creating-and-dereferencing-pointers)
- [GC `pinned` locals](LowLevelFeatures.md#612-pinned-locals)
- [C-style stackalloc syntax](LowLevelFeatures.md#6101-stackalloc-locals)
- [C-style `union`s and anonymous unions](ClassesAndObjects.md#491-unions)
- [`winbool` type instead of marshalling `bool` as 4-bytes in `extern`s](LowLevelFeatures.md#671-winbool)
- [Argument coercion with `implicit` keyword](ControlFlow.md#217-argument-coercion)
- [First-class bit casting](LowLevelFeatures.md#641-bit-casts)
- [C-string literals](LowLevelFeatures.md#614-c-strings)
- [Extern block declarations to share modifiers/attributes across members](LowLevelFeatures.md#673-extern-blocks)
- Struct layout efficiency analysis

## 1.6 Identifiers

Identifiers are used to name symbols. For example, in the statement `var a = 3;`, the name of the symbol is `a`, which
is the identifier.

Identifiers are continuous strings of letters, digits, and the underscore (`_`) character, where the first character has
to be a non-digit. Legal identifier could be `myLocal`, `My_Local`, `MyTemp3`, or `_`, whereas `3myLocal` would be
illegal because it starts with a digit. Identifiers cannot be the same as any
[non-contextual keywords](#131-non-contextual-keywords). For example, `var class = 3;` would be illegal because `class`
is a non-contextual keyword.

The verbatim specifier `@` can be used to treat what would be a keyword as an identifier. For example, `var @class = 3;`
would be legal where `class` is the identifier (note that the `@` is not included, so `myLocal` and `@myLocal` are the
same identifier).
