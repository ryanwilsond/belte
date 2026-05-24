# 3 Data

- [3.1](#31-data-types) Data Types
  - [3.1.1](#311-casts) Casts
  - [3.1.2](#312-string-interpolation) String Interpolation
  - [3.1.3](#313-function-type) Function Type
  - [3.1.4](#314-default-literal) Default Literal
- [3.2](#32-operators) Operators
  - [3.2.1](#321-operator-precedence) Operator Precedence
  - [3.2.2](#322-uncommon-operators) Uncommon Operators
    - [3.2.2.1](#3221-x) `x!`
    - [3.2.2.2](#3222-x) `x?`
    - [3.2.2.3](#3223-ai) `a?[i]`
    - [3.2.2.4](#3224-xy) `x?.y`
    - [3.2.2.5](#3225-x--y) `x ?? y`
    - [3.2.2.6](#3226-x--y) `x ?! y`
    - [3.2.2.7](#3227-xy) `x..y`
    - [3.2.2.8](#3228-xy) `x?..y`
    - [3.2.2.9](#3229-x) `x!!`
    - [3.2.2.10](#32210-x--y) `x /\ y`
    - [3.2.2.11](#32211-x--y) `x \/ y`
    - [3.2.2.12](#32212-x--y-z) `x >< [y, z]`
  - [3.2.3](#323-isisntas-operators) Is/Isnt/As Operators
- [3.3](#33-variables-and-constants) Variables and Constants
  - [3.3.1](#331-implicit-typing) Implicit Typing
- [3.4](#34-attributes-and-modifiers) Attributes and Modifiers
- [3.5](#35-references) References
- [3.6](#36-arrays) Arrays
- [3.7](#37-compile-time-expressions) Compile-Time Expressions
  - [3.7.1](#371-examples) Examples
  - [3.7.2](#372-side-effects) Side Effects

## 3.1 Data Types

The following is a list of built-in types with links to further information:

| Name | Example | Value | More Info |
| - | - | - | - |
| Integer | `int` | Whole number | |
| Decimal | `decimal` | Number with a decimal point | |
| Boolean | `bool` | `true` or `false` | |
| String | `string` | Span of characters | |
| Character | `char` | Single unicode character | |
| Type | `type` | Represents another type (e.g. `typeof(int)`) | |
| Any | `any` | Anything | |
| Function | `void()` | Managed function | [Section 3.1.3](#313-function-type) |
| Object | `Object` | Anything, class base type | [Section 4.1](ClassesAndObjects.md#41-classes) |
| Array | `int[]` | Collection of items | [Section 3.6](#36-arrays) |
| WINBOOL | `winbool` | Boolean that marshals as 4 bytes for interop | [Section 6.7.1](LowLevelFeatures.md#671-winbool) |
| Signed-Byte | `int8` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Signed-Short | `int16` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Signed-Int | `int32` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Signed-Long | `int64` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Byte | `uint8` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Unsigned-Short | `uint16` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Unsigned-Int | `uint32` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Unsigned-Long | `uint64` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Single Precision Float | `float32` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Double Precision Float | `float64` | | [Section 6.4](LowLevelFeatures.md#64-numerics) |
| Pointer | `int*` | Memory location of a piece of data | [Section 6.5](LowLevelFeatures.md#65-pointers) |
| Function Pointer | `void()*` | Memory location of a function | [Section 6.6](LowLevelFeatures.md#66-function-pointers) |
| Integer Pointer | `intptr` | Used for .NET interop | |
| Unsigned Integer Pointer | `uintptr` | Used for .NET interop | |

Most primitive types are non-nullable by default, which can be [read about here](Overview.md#14-nullability-and-types).

Additionally, there are user-defined types:

| Name | Description | More Info |
| - | - | - |
| Class | Heap-allocated complex type | [Section 4.1](ClassesAndObjects.md#41-classes) |
| Enum | Definition of constants | [Section 4.6](ClassesAndObjects.md#46-enums) |
| Struct | Stack-allocated complex type | [Section 4.9](ClassesAndObjects.md#49-structs) |
| Union | Special struct | [Section 4.9.1](ClassesAndObjects.md#491-unions) |

Additional information:

- See also [arrays and initializer lists](LowLevelFeatures.md#63-arrays).
- See also [initializer dictionaries](StandardLibrary/Dictionary.md#5724-initializer-dictionaries).
- See also [sized numeric types](LowLevelFeatures.md#64-numerics).
- See also [pointer and function pointer types](LowLevelFeatures.md#65-pointers).

### 3.1.1 Casts

To convert from one data type to another, a cast can be used. If a cast is implicit, it can happen automatically. If a
cast is explicit, it requires a cast expression (e.g. `(int)"123"`).

The general rule is that lossless casts are implicit, lossy casts are explicit. For example, converting from an `int32`
to an `int64` is implicit because the entire range of values that `int32` can represent, `int64` can also represent:

```belte
int32 a = 10;
int64 b = a;
```

Going from `int64` to `int32` is lossy because `int64` can represent numbers higher and lower than `int32`, so an
explicit cast is needed:

```belte
int64 a = 10;
int32 b = (int32)a;
```

**Numeric explicit casts do not throw if the value is out of range and instead slice.**

Nullability also affects casting in the same way. Casting from a non-nullable value to a nullable one is implicit, while
the reverse is explicit. For example converting from `int32!` to `int32?`:

```belte
int32 a = 10;
int32? b = a;
```

Going from `int32?` to `int32!` is explicit and will throw if the value is null. When the underlying types are the same,
a nullable assert operator (`!`) can be used instead of a cast:

```belte
int32? a = 10;
int32 b = (int32)a;
int32 c = a!;
```

```belte
int32? a = null;
int32 b = (int32)a; // throws
int32 c = a!; // throws
```

Any type can be cast to an any implicit and from an any explicitly:

```belte
int32 a = 10;
any b = a;
int32 c = (int32)b;
```

Class-types can implicit "upcast" to their base types and explicitly "downcast" to derived types:

```belte
B myB = new B();
A myA = myB;
B myBAgain = (B)myA;

class A { }

class B extends A { }
```

Classes can also use the [`is`, `isnt`, and `as` operators](Data.md#323-isisntas-operators) to perform safer up/down
casts.

Additional information:

- See also [pointer casts](LowLevelFeatures.md#651-creating-and-dereferencing-pointers)
- See also [user-defined casts](ClassesAndObjects.md#4232-casts).
- See also [bit casts](LowLevelFeatures.md#641-bit-casts)

### 3.1.2 String Interpolation

Prefixing a string literal with `f` allows expressions to be embedded into the string, denoted by enclosing brace pairs.

The expressions within a string will automatically be casted to a string if they are a primitive. Otherwise
`Object.ToString()` is called on the expression.

For example:

```belte
var a = 3;
var b = f"A equals {a}"; // b = "A equals 3"
```

```belte
var a = new List<int>({ 1, 2, 3 });
var b = f"A equals {a}"; // b = "A equals { 1, 2, 3 }"
```

### 3.1.3 Function Type

Similar to [function pointers](LowLevelFeatures.md#66-function-pointers), a data container can have a function type and
then be assigned with unambiguous method groups.

For example:

```belte
int(int, int) myFunc = Add;
int sum = myFunc(3, 5);

int Add(int a, int b) {
  return a + b;
}
```

Parameter names are optional in function types and default to `p1`, `p2`, etc. based on parameter ordinal:

```belte
int(int a, int) myFunc; // Signature is: int(int a, int p2)
```

This is to allow [named arguments](ControlFlow.md#214-named-arguments) when calling the function.

Function types cannot include pointer types in the return value or parameter list. For cases where you need this
functionality, use function pointers instead.

### 3.1.4 Default Literal

The `default` literal can be used to indicate `null` for nullable types or the default value for primitives.

For example:

```belte
int a = default; // a = 0
int? a = default; // a = null
```

Types with no default value (non-nullable class types) cannot use the `default` literal.

## 3.2 Operators

All operators execute strictly left-to-right, meaning the left-most operand is always fully evaluated before the next
operand is.

For example, in the expression `F() + G()`, `F()` is always executed before `G()`.

The `?!`, `??`, `&&`, `||`, and `[]` operators short-circuit meaning depending on the result of the left operand, the
right operand might not be executed at all.

Lifted binary operators result in `null` if either operand is `null`, but do not short circuit. This includes the
equality operators `==` and `!=`. To check if an expression is null, use the `x is null` or `x isnt null` operators.

For example, in the expression `null + G()`, `G()` is executed even though the expression results in `null`.

### 3.2.1 Operator Precedence

Operators are used to interact with data. Each operator takes in one or more operands to perform on. Operators follow a
strict order of precedence:

| Operators | Category |
| - | - |
| a\[i\], a?\[i\], f(x), x.y, x?.y, x->y, x++, x--, x!, x!!, new, typeof, nameof, sizeof | Primary |
| x ** y | Power |
| +x, -x, !x, ~x, ++x, --x, (T)x, &x, *x | Unary |
| x..y, x?..y | Cascade |
| is, isnt, as | Type-Testing |
| x * y, x / y, x % y | Multiplicative |
| x + y, x - y | Additive |
| x << y, x >> y, x >>> y | Shift |
| x < y, x > y, x <= y, x >= y, x /\ y, x \/ y | Relational |
| x == y, x != y | Equality |
| x & y | Bitwise Logical AND |
| x ^ y | Bitwise Logical XOR |
| x \| y | Bitwise Logical OR |
| x && y | Conditional AND |
| x \|\| y | Conditional OR |
| x ?? y, x ?! y | Null-Coalescing |
| c ? t : f, x >< \[y, z] | Tertiary Conditional and Clamp |

Note that all binary operators are left-associative except for the power operator. For example `2 + 3 + 4` will parse as
`(2 + 3) + 4` while `2 ** 3 ** 4` will parse as `2 ** (3 ** 4)`.

### 3.2.2 Uncommon Operators

#### 3.2.2.1 `x!`

`x!` is a null assertion. It converts a nullable `x` into a non-nullable one. `x` must be nullable. The operator's
result is non-nullable. If `x` is null, a runtime null reference exception is thrown.

#### 3.2.2.2 `x?`

`x?` is a null erasure. If `x` is null, the exception results in the default value of the type of `x`, where the type
of `x` must be a primitive.

#### 3.2.2.3 `a?[i]`

`a?[i]` is a conditional indexer. If `a` is null, the index is not performed. `i` will not execute if `a` is null.

This operator is syntax sugar for `a is null ? null : a![i]`.

#### 3.2.2.4 `x?.y`

`x?.y` is a conditional member access. If `a` is null, the access is not performed.

This operator is syntax sugar for `x is null ? null : x!.y`.

#### 3.2.2.5 `x ?? y`

`x ?? y` is a null coalescing expression. If `x` is null, `y` is the result. Otherwise `x` is the result. `y` will not
execute if `x` is not null.

This operator is syntax sugar for `x is null ? y : null`.

#### 3.2.2.6 `x ?! y`

`x ?! y` is a null propagation expression. If `x` is null, `x` is the result. Otherwise `y` is the result. `y` will not
execute if `x` is null.

This operator is syntax sugar for `x is null ? null : y`.

#### 3.2.2.7 `x..y`

`x..y` is a cascade expression. Each cascade performs a field assignment or call on the receiver `x` but the result is
discarded.

For example:

```belte
var a = new Obj()..M()..f=3;
```

The above example is equivalent to:

```belte
var temp = new Obj();
temp.M();
temp.f = 3;
var a = temp;
```

Notice that even if `Obj.M()` returns a value, it is ignored.

#### 3.2.2.8 `x?..y`

`x?..y` is a conditional cascade expression. The field assignment or call expression `y` is only performed if `x` is not
null.

#### 3.2.2.9 `x!!`

`x!!` is a silent null assertion. It converts a nullable `x` into a non-nullable one. `x` must be nullable. The
operator's result is non-nullable. If `x` is null, a runtime null reference exception is thrown if building in
debug mode. If in release mode, no runtime check is performed at the assertion site explicitly, meaning exceptions will
raise elsewhere if the value is null.

In the case that the operand has a class type, using this operator when the operand is null risks polluting a
non-nullable context with null.

This operator is intended to be used when certain the operand is not null and thus the overhead of checking again is
unnecessary.

#### 3.2.2.10 `x /\ y`

`x /\ y` is equivalent to `Math.Min(x, y)`.

#### 3.2.2.11 `x \/ y`

`x \/ y` is equivalent to `Math.Max(x, y)`.

#### 3.2.2.12 `x >< [y, z]`

`x >< [y, z]` is equivalent to `Math.Clamp(x, y, z)`.

### 3.2.3 Is/Isnt/As Operators

Unlike normal binary operators, `is`, `isnt` and `as` have special rules for what the right operand can be.

The `as` operator requires the right operand to be a type. The `as` operator attempts to cast the left operand into the
right operand type and results in null if the cast cannot be performed. Unlike a normal cast, the `as` operator only
performs class up/down casting.

For example:

```belte
A a = new B();
B b = a as B;

class A { }

class B extends  A { }
```

The `isnt` operator requires the right operand to be a type or the `null` literal. It checks if the left operand is not
the right operand type or if the left operand is not `null`.

For example:

```belte
int? a = null;
bool b = a isnt null; // false
```

```belte
int? a = null;
bool b = a isnt int; // true
```

The `is` operator requires the right operand to be a type, the `null` literal, or a declaration. In the case of the
first two, it behaves opposite of the `isnt` operand. If the right operand is a declaration, it will store the
successful result into the declared local if the left operand matches the type.

For example:

```belte
any a = 3;

if (a is int t) {
  int b = t + 3;
}
```

## 3.3 Variables and Constants

Variables and constants both hold data. Variables can change, while constants cannot be modified after assigned.

Declaring a variable takes the format `<type> <name>;` or `<type> <name> = <initializer>;`. The former declares a
variable with no value, making it `null` until later defined. The latter gives the variable an initial value.

### 3.3.1 Implicit Typing

Instead of using type-names to declare, implicit keywords `var`, `const`, and `constexpr` can be used if the initializer
is distinct.

Examples:

```belte
var a = 3; // Same as 'int a = 3;'
const b = 3; // Same as 'const int b = 3;'
constexpr c = 3; // Same as `constexpr int c = 3;`
```

Implicit typing supports normal nullable annotations:

```belte
var? a = 3; // Same as `int? a = 3;`
```

```belte
var! a = new MyClass(); // Same as `MyClass! a = new MyClass();`
```

## 3.4 Attributes and Modifiers

Reference-type data is nullable by default. To disable this, an exclamation mark can be used:

```belte
MyType! a = new MyType();
```

Value-type data is non-nullable by default. To enable nullability, a question mark can be used:

```belte
int? a = null;
```

Both of these annotations are allowed in situations where they are redundant for clarity purposes:

```belte
int! a = 3;
MyType? b = new MyType();
```

## 3.5 References

All variables and constants are by-value by default, meaning they directly contain data. Alternatively, data can be
stored as a reference to data somewhere else. This is achieved using the `ref` keyword.

```belte
int a = 3;
ref int b = ref a;
```

In the above example, if `a` is changed, `b` reflects those changes, and vice versa. Note that the type of the
reference must match the type of what it is referencing including if it is constant or not.

Valid references:

```belte
int a = 3;
ref int b = ref a;

const string! a = "test";
ref const string! b = ref a;

bool a;
ref bool b = ref a;
```

A constant reference is a reference that cannot change what it is referencing:

```belte
int a = 3;
const ref int b = ref a;
```

Meaning a constant reference to a constant may look like:

```belte
const int a = 3;
const ref const int b = ref a;
```

Note that the first `const` keyword makes it a constant reference, and the second means it is referencing a constant.

## 3.6 Arrays

Data can be given dimensionality, called arrays. This is indicated with pairs of square brackets. This allows a variable
or constant to hold more than one piece of same-type data.

```belte
int[] a = { 1, 2, 3 };
```

The data can be accessed and modified using indexing (starting at 0).

```belte
int[] a = { 1, 2, 3 };
int b = a[1]; // 2

a[2] = 6;
```

An initializer list expression can be used to implicitly create an array in
contexts where it is not being used as an initializer such as in the examples
above.

The array creation can also be made explicit in these scenarios. The following
are equivalent:

```belte
F({1, 2, 3});
F(new int[] {1, 2, 3});

void F(int[] arr) { /* ... */}
```

## 3.7 Compile-Time Expressions

To evaluate an expression at compile-time, you can precede it with `$` or `$?`. The `$` operator tells the compiler to
evaluate the expression at compile time. The `$?` operator tells the compiler to try and evaluate the expression at
compile time, and if it cannot be evaluated ignore the failure and compile the expression as normal.

Not all expressions are able to be evaluated at compile time. If the type of the expression is an object, pointer, or
function pointer, the compiler does not attempt to evaluate the expression.

If the expression has a valid result type, the compiler does attempt to evaluate it, but still may not be able to do so.
If the result of the expression contains an object, pointer, or function pointer (such as a struct field), the
expression fails to fully evaluate. If the expression throws an uncaught exception, the expression fails to fully
evaluate. In both of these cases, consider potential [side effects](#372-side-effects).

For more complex compile-time execution/meta-programming consider using
[compiler handles](LowLevelFeatures.md#613-compiler-handle).

### 3.7.1 Examples

For example:

```belte
int myInt = $Add(4, 5);

int Add(int x, int y) {
  return x + y;
}
```

In the above example, the program produced by the compiler will evaluate the expression `Add(4, 5)` and put the result,
`9`, in its place:

```belte
int myInt = 9;

int Add(int x, int y) {
  return x + y;
}
```

Some expressions are not computable at compile-time. One possibility is that the expression tries to use data from
outside the scope of the expression:

```belte
var myClass = new MyClass();
var myInt = $myClass.GetF(); // Fails to compute

class MyClass {
  public int f = 10;

  public int GetF() {
    return f;
  }
}
```

In the above example, the expression cannot be computed at compile-time because it references local `myClass` which is
defined outside of the scope of the compile-time expression.

If you want to ignore any compile-time evaluation errors and continue you can use `$?`:

```belte
var myClass = new MyClass();
var myInt = $?myClass.GetF();

class MyClass {
  public int f = 10;

  public int GetF() {
    return f;
  }
}
```

The above example will not replace the `myInt` declaration with anything and will retain its original initializer of
`myClass.GetF()` because the compile-time evaluation failed.

### 3.7.2 Side Effects

Because whether or not an expression is evaluatable at compile time cannot always be predetermined, it is important to
note that there may be side effects to expressions marked to evaluate even if the expression fails, such as file IO.

For example:

```belte
$ExampleMethod();

void ExampleMethod() {
  File.AppendText("path/to/file.txt", "Some line of text");
  throw new Exception();
}
```

In the above example, the compilation will fail because `ExampleMethod` threw an uncaught exception, but the preceding
file write still happened.

Consider this more insidious example:

```belte
$?ExampleMethod();

void ExampleMethod() {
  File.AppendText("path/to/file.txt", "Some line of text");
  throw new Exception();
}
```

In this example, the compilation will succeed because the conditional `$?` operator was used. The file write will
happen, and then the exception thrown will cause the compile time expression to fail to evaluate, meaning the compiler
will emit the original expression.

The result is that if you compile and then run this program, the file write will happen twice.

The compiler cannot verify whether or not an expression has side effects, so the usage of the compile-time expression
operator is not restricted to prevent them from happening.
