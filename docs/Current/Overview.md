# 1 Overview

The Belte language is syntactically very similar to C#; Belte is a "C-Style" language that focuses on the
object-oriented paradigm.

Currently, the Belte compiler, Buckle, supports interpretation and building to a .NET executable.

> [Using the compiler CLI](../Buckle.md)

- [1.1](#11-endpoint-specific-features) Endpoint Specific Features
- [1.2](#12-keywords) Keywords
  - [1.2.1](#121-non-contextual-keywords) Non-Contextual Keywords
  - [1.2.2](#122-contextual-keywords) Contextual Keywords
- [1.3](#13-nullability-and-types) Nullability and Types
  - [1.3.1](#131-normal-types) Normal Types
  - [1.3.2](#132-pointers-and-function-pointers) Pointers and Function Pointers
  - [1.3.3](#133-initializers) Initializers
  - [1.3.4](#134-fields) Fields
  - [1.3.5](#135-implicit-typing) Implicit Typing
  - [1.3.6](#136-null-flow-analysis) Null-Flow Analysis

## 1.1 Endpoint Specific Features

Some features are not supported across all endpoints for various reasons.

The following list describes all of the features where full parity is not currently implemented or was not always
implemented.

- Evaluator: the internal interpreter endpoint. Used for the [REPL](../Repl.md), `--evaluate` builds, and [compile-time expressions](Data.md#37-compile-time-expressions).
- Executor: the default endpoint which relies the compiler infrastructure.
- IL Emitter: the endpoint for emitting to an executable which relies on .NET.

| Feature | Evaluator | Executor | IL Emitter | Explanation |
|-|-|-|-|-|
| `--type=graphics` projects | ✓ | ✓ | ✕ | Standalone graphics DLL under development |
| Non-type templates | ✓ | ✕ | ✕ | Not supported by the .NET runtime |
| Non-integral enums | ✓ | ✕ | ✕ | Not supported by the .NET runtime |
| Pointers | ✕ | ✓ | ✓ | Partially supported the Evaluator but not stable due to internal memory structure |
| Function pointers | ✕ | ✓ | ✓ | Disallowed in the Evaluator due to internal memory structure |
| Externs/DllImport | ✕ | ✓ | ✓ | Incompatible with the Evaluator |
| Inline IL | ✕ | ✓ | ✓ | Incompatible with the Evaluator |
| .NET DLL references | ✕ | ✓ | ✓ | Incompatible with the Evaluator |

## 1.2 Keywords

The following lists all keywords used in the language. No type names (e.g. `int`) are reserved.

Some keywords have multiple meanings depending on context. Those keywords will be disambiguated in the lists below.

### 1.2.1 Non-Contextual Keywords

These keywords are reserved names and cannot be used as identifiers.

- [abstract](ClassesAndObjects.md#432-static--constexpr)
- [as](Data.md#32-operators)
- [base](ClassesAndObjects.md#413-base-access)
- [break](ControlFlow.md#245-break)
- [case](ControlFlow.md#25-switch)
- [class](ClassesAndObjects.md#41-classes)
- [constexpr](ClassesAndObjects.md#433-static--constexpr)
- [const](Data.md#33-variables-and-constants) (locals)
- [const](ClassesAndObjects.md#434-const) (methods)
- [constructor](ClassesAndObjects.md#44-constructors)
- [continue](ControlFlow.md#246-continue)
- [default](Data.md#314-default-literal) (literal)
- [default](ControlFlow.md#25-switch) (switch label)
- [define](Preprocessor.md#71-defineundef)
- [do](ControlFlow.md#242-do-while-loops)
- [elif](Preprocessor.md#72-control)
- [else](ControlFlow.md#23-conditionals)
- [endif](Preprocessor.md#72-control)
- [enum](ClassesAndObjects.md#46-enums)
- [extends](ClassesAndObjects.md#412-inheritance) (inheritance)
- [extends](ClassesAndObjects.md#4512-special-constraints) (template constraints)
- [extern](LowLevelFeatures.md#67-extern-methods)
- [false](Data.md#31-data-types)
- [for](ControlFlow.md#243-for-loops) (for loop)
- [for](ControlFlow.md#244-for-each-loops) (for each loop)
- [global](ClassesAndObjects.md#483-global-using-directive) (global using)
- [global](ClassesAndObjects.md#482-global-disambiguation) (global disambiguation)
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
- [operator](ClassesAndObjects.md#423-operators) (normal operators)
- [operator](ControlFlow.md#244-for-each-loops) (for each operators)
- [override](ClassesAndObjects.md#432-overriding-modifiers)
- [pinned](LowLevelFeatures.md#612-pinned-locals)
- [private](ClassesAndObjects.md#431-accessibility-modifiers)
- [protected](ClassesAndObjects.md#431-accessibility-modifiers)
- [public](ClassesAndObjects.md#431-accessibility-modifiers)
- [ref](Data.md#35-references)
- [return](ControlFlow.md#21-functions)
- [sealed](ClassesAndObjects.md#435-sealed--abstract) (classes)
- [sealed](ClassesAndObjects.md#432-overriding-modifiers) (members)
- [sizeof](LowLevelFeatures.md#69-sizeof-operator)
- [stackalloc](LowLevelFeatures.md#610-stackalloc-operator)
- [static](ClassesAndObjects.md#433-static--constexpr) (modifier)
- [static](ClassesAndObjects.md#48-using-directives) (using directive)
- [struct](LowLevelFeatures.md#62-structures)
- [switch](ControlFlow.md#25-switch)
- [this](ClassesAndObjects.md#411-declaring-and-using-classes)
- [throw](ControlFlow.md#26-exceptions)
- [true](Data.md#31-data-types)
- [typeof](Data.md#32-operators)
- [undef](Preprocessor.md#71-defineundef)
- [using](ClassesAndObjects.md#48-using-directives)
- [virtual](ClassesAndObjects.md#432-overriding-modifiers)
- [where](ClassesAndObjects.md#451-constraint-clauses)
- [while](ControlFlow.md#241-while-loops)

The following keywords are reserved names but are not yet used:

- catch
- finally
- try

### 1.2.2 Contextual Keywords

These keywords only act as keywords inside specific contexts. As such they can be used as identifiers in most places.

- [explicit](ClassesAndObjects.md#4232-casts)
- [flags](ClassesAndObjects.md#461-flags)
- [handle](LowLevelFeatures.md#613-compiler-handle)
- [implicit](ClassesAndObjects.md#4232-casts)
- [notnull](ClassesAndObjects.md#4512-special-constraints)
- [noverify](LowLevelFeatures.md#6111-verification)
- [primitive](ClassesAndObjects.md#4512-special-constraints)

## 1.3 Nullability and Types

Nullability is treated in a non-standard way in Belte. As such, a close read of the following section is recommended,
which is a consolidation of important nullability semantics found in the rest of the documentation.

To summarize:

- Reference types (classes) are nullable by default
- Value types (primitives, pointers, structs) are non-nullable by default
- `!` removes nullability
- `?` adds nullability
- Pointer types are never nullable

A type is "nullable" when it permits `null`.

### 1.3.1 Normal Types

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

### 1.3.2 Pointers and Function Pointers

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

### 1.3.3 Initializers

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

## 1.3.4 Fields

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

## 1.3.5 Implicit Typing

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

int? Func() { ... }
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

## 1.3.6 Null-Flow Analysis

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
  ...
}
```

In this example, `b` is declared as a non-nullable local set to the value of `a`, so in this case the type of `b` is
`int!`. `b` only lives inside of the block.

To use a type's default value in the case of null, the [`?` operator](Data.md#3222-x) can be used. For example:

```belte
bool? a = Func();

if (a?) {
  ...
} else {
  ...
}
```

In this example, the else block is executed if `a` is false or null. Note that conditions inside if, for, and while
constructs can be nullable. If the condition is null at runtime, an exception is thrown. For example:

```belte
bool? a = Func();

if (a) {
  ...
}
```

In this example, if `a` is null, a runtime exception is thrown at the if condition.
