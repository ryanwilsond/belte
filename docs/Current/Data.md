# 3 Data

- [3.1](#31-data-types) Data Types
- [3.2](#32-operators) Operators
- [3.3](#33-variables-and-constants) Variables and Constants
- [3.4](#34-attributes-and-modifiers) Attributes and Modifiers
- [3.5](#35-references) References
- [3.6](#36-arrays) Arrays

## 3.1 Data Types

Apart from classes, there are five distinct data types:

| Name | Identifier | Values |
|-|-|-|
| Integer | `int` | Integers from -2,147,483,647 to 2,147,483,647 |
| Decimal | `decimal` | Numbers approximately from ±5.0 × 10<sup>−324</sup> to ±1.7 × 10<sup>308</sup> |
| Boolean | `bool` | `true` or `false` |
| String | `string` | Spans of characters, no length limit |
| Any | `any` | Any integer, decimal, boolean, or string value |

### 3.1.1 Casts

To convert from one data type to another, a cast can be used. If a cast is implicit, it can happen automatically with
no special syntax. If a cast is explicit, it must use a special syntax (e.g. `(int)"123"`).

| From | To | Cast Type | Notes |
|-|-|-|
| Integer | Decimal | Implicit | |
| Integer | String | Explicit | |
| Integer | Bool | None | |
| Decimal | Integer | Explicit | Truncates |
| Decimal | Boolean | None | |
| Decimal | String | Explicit | |
| Boolean | Integer | None | |
| Boolean | Decimal | None | |
| Boolean | String | Explicit | |
| String | Integer | Explicit | Can throw |
| String | Decimal | Explicit | Can throw |
| String | Boolean | Explicit | Can throw |

In addition, nullability affects casting:

| From | To | Cast Type | Notes |
|-|-|-|
| type | type! | Explicit | Can throw |
| type! | type | Implicit | |

## 3.2 Operators

Operators are used to interact with data. Each operator takes in one or more operands to perform on. Operators follow a
strict order of precedence:

| Operators | Category |
|-|-|
| a\[i\], a?\[i\], f(x), (T)y, x.y, x?.y, x++, x--, x!, new, typeof | Primary |
| +x, -x, !x, ~x, ++x, --x | Unary |
| x ** y | Power |
| x * y, x / y, x % y | Multiplicative |
| x + y, x - y | Additive |
| x << y, x >> y, x >>> y | Shift |
| x < y, x > y, x <= y, x >= y, is, isnt | Relational and Type-Testing |
| x == y, x != y | Equality |
| x & y | Bitwise Logical AND |
| x ^ y | Bitwise Logical XOR |
| x \| y | Bitwise Logical OR |
| x && y | Conditional AND |
| x \|\| y | Conditional OR |
| x ?? y | Null-Coalescing |
| c ? t : f | Tertiary Conditional |

## 3.3 Variables and Constants

Variables and constants both hold data. Variables can change, while constants cannot be modified after assigned.

Declaring a variable takes the format `<type> <name>;` or `<type> <name> = <initializer>;`. The former declares a
variable with no value, making it `null` until later defined. The latter gives the variable an initial value.

### 3.3.1 Implicit Typing

Instead of using type-names to declare, implicit keywords `var` and `const` can be used if the initializer is distinct.

Examples:

```belte
var a = 3; // Same as 'int a = 3;'
const b = 3; // Same as 'const int b = 3;'
```

## 3.4 Attributes and Modifiers

All data is nullable by default. To disable this, an exclamation mark can be used:

```belte
int! a = 3;
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

Note that this functionality will eventually be moved to be exclusive to low-level contexts, and be replaced with more
powerful collection types.
