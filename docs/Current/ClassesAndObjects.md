# 4 Namespaces, Classes, and Objects

- [4.1](#41-classes) Classes
  - [4.1.1](#411-declaring-and-using-classes) Declaring And Using Classes
  - [4.1.2](#412-inheritance) Inheritance
  - [4.1.3](#413-base-access) Base Access
- [4.2](#42-members) Members
  - [4.2.1](#421-fields) Fields
  - [4.2.2](#422-methods) Methods
  - [4.2.3](#423-operators) Operators
    - [4.2.3.1](#4231-operator-overloading) Operator Overloading
    - [4.2.3.2](#4232-casts) Casts
- [4.3](#43-modifiers) Modifiers
  - [4.3.1](#431-accessibility-modifiers) Accessibility Modifiers
  - [4.3.2](#432-overriding-modifiers) Overriding Modifiers
  - [4.3.3](#433-static--constexpr) Static & ConstExpr
  - [4.3.4](#434-const) Const
  - [4.3.5](#435-sealed--abstract) Sealed & Abstract
- [4.4](#44-constructors) Constructors
- [4.5](#45-templates) Templates
  - [4.5.1](#451-constraint-clauses) Constraint Clauses
    - [4.5.1.1](#4511-expression-constraints) Expression Constraints
    - [4.5.1.2](#4512-special-constraints) Special Constraints
- [4.6](#46-enums) Enums
  - [4.6.1](#461-flags) Flags
  - [4.6.2](#462-implicit-enum-fields) Implicit Enum Fields
  - [4.6.3](#463-experimental-underlying-types) Experimental Underlying Types
- [4.7](#47-namespaces) Namespaces
- [4.8](#48-using-directives) Using Directives
  - [4.8.1](#481-aliasing) Aliasing
  - [4.8.2](#482-global-disambiguation) Global Disambiguation
  - [4.8.3](#483-global-using-directive) Global Using Directive
- [4.9](#49-structs) Structs
  - [4.9.1](#491-unions) Unions

## 4.1 Classes

Classes are structures that contain data and functionality in the form of fields and methods. Fields are similar to
variables, and methods are similar to functions both in syntax and functionality.

### 4.1.1 Declaring and Using Classes

Classes are declared using the `class` keyword:

```belte
class MyClass {
  // Members
}
```

An object is an instance of a class, and can be created using the `new` keyword:

```belte
new MyClass();
```

The containing instance can be accessed within a method using the `this` keyword:

```belte
class MyClass {
  int a;

  int GetA() {
    return this.a;
  }
}
```

### 4.1.2 Inheritance

The `extends` keyword is used to specify that a class inherits from another, meaning it adopts all of that classes
fields and methods. If a base type is not specified, classes will inherit directly from `Object`.

```belte
var myB = new B();
myB.M();

class A {
  public void M() { }
}

class B extends A { }
```

Members can interact with inheritance through [certain modifiers](#432-overriding-modifiers).

Classes can restrict or necessitate inheritance through the [sealed and abstract modifiers](#435-sealed--abstract).

### 4.1.3 Base Access

In the case of a base type containing a member of the same name as a derived class, the more derived member will take
precedence. To access the base member, the `base` keyword can be used similar to `this`.

```belte
class A {
  public virtual int M() {
    return 10;
  }
}

class B extends A {
  public override int M() {
    return 3;
  }

  public void F() {
    var num = M(); // num = 3
  }
}
```

```belte
class A {
  public virtual int M() {
    return 10;
  }
}

class B extends A {
  public override int M() {
    return 3;
  }

  public void F() {
    var num = base.M(); // num = 10
  }
}
```

## 4.2 Members

Members of an instance can be accessed externally using a member accession:

```belte
myInstance.member;
```

Members can also be accessed internally (i.e. inside of a method) using the `this` keyword:

```belte
this.member;
```

If no local symbol names conflict, the `this` keyword can be omitted:

```belte
member;
```

### 4.2.1 Fields

Fields are similar to variables or constants. They are declared as such:

```belte
class MyClass {
  int myField1;
  string myField2 = "Starting Value";
  // etc.
}
```

Fields have the same flexibility as traditional variables and constants, meaning they can be any type including another
class:

```belte
class A { }

class B {
  A myField = new A();
}
```

### 4.2.2 Methods

Methods are similar to functions. They are declared as such:

```belte
class MyClass {
  public void MyMethod(int param1) {
    // Body
  }
}
```

When accessing a method, it must be called:

```belte
var myInstance = new MyClass();
myInstance.MyMethod();
```

### 4.2.3 Operators

#### 4.2.3.1 Operator Overloading

Operators are similar to methods. They are declared as such:

```belte
class MyClass {
  public static MyClass operator+(MyClass left, MyClass right) {
    // Body
  }
}
```

Operator overloading is used to allow custom classes to use syntactical operators. The overloadable operators are:

| Operators | Notes |
|-|-|
| `+x`, `-x`, `!x`, `~x`, `++`, `--`, `x[]` | |
| `x + y`, `x - y`, `x * y`, `x / y`, `x % y`, `x & y`, `x \| y`, `x ^ y`, `x << y`, `x >> y`, `x >>> y` | |
| `x == y`, `x != y`, `x < y`, `x > y`, `x <= y`, `x >= y` | Must be overloaded in the following pairs: `==` and `!=`, `<` and `>`, `<=` and `>=` |

Note that operators must be marked [public](#431-accessibility-modifiers) and [static](#433-static--constexpr).

#### 4.2.3.2 Casts

Explicit and implicit casts from or to the class type can be declared as such:

Implicit cast from `A` to `int`:

```belte
class MyClass {
  public static implicit operator int(A a) {
    // Body
  }
}
```

Explicit cast from `int` to `A`:

```belte
class MyClass {
  public static explicit operator A(int a) {
    // Body
  }
}
```

These casts are automatically applied when casting like normal:

```belte
A a = (A)3;
```

```belte
int a = new A();
```

## 4.3 Modifiers

### 4.3.1 Accessibility Modifiers

Public indicates that the member can be
seen everywhere, including outside the class. Protected indicates that the member can only be seen within the class and
child classes. Private indicates that the member can only be seen within the class, not even in child classes.

```belte
class MyClass {
  private int a;
  protected int b;
  public int c;
}
```

A member can only have one accessibility modifier, but they do not require the modifier. By default, all struct members
are public and all class members are private.

All types of members can be marked with all three accessibility modifiers except operators, which must always be public.

### 4.3.2 Overriding Modifiers

By default, members cannot be overridden. To allow a member to be overridden, it can be marked `virtual`. Virtual
members still require a definition. To override a virtual member, the override can be marked `override`. To instead
hide a member without overriding, a member can be marked `new`. A member cannot be marked as both `override` and `new`
or `virtual`.

An overriding member can be marked `sealed` to prevent classes deriving from it to override the member again.

Similar to `virtual`, a member can be marked `abstract`. Abstract members must be overridden in all non-abstract child
implementations, and as such abstract members do not have a definition when declared.

Currently, these modifiers only apply to methods.

### 4.3.3 Static & ConstExpr

Class members are instance members by default, meaning they require an instance to access. With the `static` and
`constexpr` keywords methods and fields respectively can be accessed without an instance.

```belte
class MyClass {
  public constexpr int a = 3;

  public static void B() { }
}

var myA = MyClass.a;
MyClass.B();
```

Classes themselves can also be marked as `static`, meaning that all contained members must also be static or constant
expressions.

Static fields can be accessed without an instance and refer to a global singleton of the containing class. A private
static constructor can be defined for a class that will run the first time a static field is accessed.

### 4.3.4 Const

Methods marked as `const` cannot modify instance data or call instance methods not marked `const`. A `const` local of a
class type can only read fields and call `const` methods.

### 4.3.5 Sealed & Abstract

Classes can be marked as `sealed` to indicate that they cannot be derived.

```belte
sealed class A { }
```

Classes can be marked as `abstract` to indicate that they must be derived.

```belte
abstract class A { }
```

## 4.4 Constructors

When creating an object, values can be passed to modify the creation process. By default, no values are passed:

```belte
class MyClass { }

new MyClass();
```

But with a constructor data can be allowed to be passed when creating the object. Constructors are declared similar to
methods, but do not have a return type and they use the `constructor` keyword in place of an identifier.

```belte
class MyClass {
  int myField;

  public constructor(int a) {
    this.myField = a;
  }
}

new MyClass(4);
```

## 4.5 Templates

Classes can be declared as templates and take in template arguments that change how the class operates at runtime.
Template arguments can either be compile-time constants or types (similar to C++ templates and C# generics).

To demonstrate this concept, a simplified definition for a List type is shown:

```belte
class List<type t> {
  t[] array;
  int length;

  public constructor(t[] array) {
    this.array = array;
    length = Length(array);
  }

  public static ref t operator[](List<t> list, int index) {
    return ref list.array[index];
  }
}
```

This List template can be used to create List objects of different types:

```belte
var myList = new List<int>(
  { 1, 2, 3 }
);

var myList = new List<string>(
  { "Hello", "world!" }
);

var myList = new List<bool[]>(
  {
    { true, false },
    { false },
    { true, true, false }
  }
);
```

These types are not limited to primitives:

```belte
var myList = new List<List<int>>({
  new List<int>({ 1, 2, 3 }),
  new List<int>({ 3 }),
  new List<int>({ 3, 3, 2, 45 })
});
```

### 4.5.1 Constraint Clauses

Templates can be constrained at compile-time to ensure intended functionality. These constraints are defined within a
single `where` block in the class header.

#### 4.5.1.1 Expression Constraints

These expressions are enforced at compile-time, and as such they must be computable at compile time. To be computable
at compile time, the set of allowed expressions is limited:

| Expression | Additional Restrictions |
|-|-|
| Unary | |
| Binary | |
| Ternary | |
| Cast | Only compiler-computable casts; only casts between primitives |
| Index | Only constant indexes on constant initializer lists |
| Member access | Only when accessing members that are compile-time constants, meaning the accessed expression does not need to be a compile-time constant |
| Extend | Only on type template parameters |
| Initializer list | Only when every item is a compile-time constant |
| Literal | |
| Variable | Only template parameters |

Given the class definition:

```belte
class Int<int min, int max> where { min <= max; } {
  ...
}
```

Then we can see the following examples:

```belte
Int<0, 10>
Int<5, 5>
Int<10, 0> // Compile error
```

#### 4.5.1.2 Special Constraints

The following constraints only apply to type template parameters:

A `T extends Y` constraint ensures template parameter `T` is or derives from `Y`.

A `T is primitive` constraint ensure template parameter `T` is a primitive type.

A `T is notnull` constraint constrains the template parameter `T` to being a non-nullable type. Non-nullable annotations
are disallowed on type template parameters, so this constraint is required for the template class to know the template
parameter is a non-nullable type.

## 4.6 Enums

Enums are value types that contain a list of integral constants. Enum field values implicitly start at 0 and count up,
but explicit values can be specified. The underlying integral type defaults to `int` but can be specified:

```belte
enum MyEnum extends uint8 {
  Field1,
  Field2,
  ...
}
```

Where `Field1` equals 0 and `Field2` equals 1. Explicitly declaring field values can be done as such:

```belte
enum MyEnum {
  Field1 = 300,
  Field2 = 400,
  ...
}
```

Creating an instance of an enum type is done by initializing to a field of the enum:

```belte
MyEnum myLocal = MyEnum.Field1;

enum MyEnum {
  Field1,
  ...
}
```

Instances of enum types can interact with their underlying integral type implicitly:

```belte
int myLocal = MyEnum.Field1 + 10;
// myLocal = 20

enum MyEnum {
  Field1 = 10
}
```

### 4.6.1 Flags

The `flags` keyword can be used to signal to other developers that the enum is meant to be used with multiple fields
at the same time. For example:

```belte
var myLocal = MyEnum.Field1 | MyEnum.Field2;

enum flags MyEnum {
  None,
  Field1,
  Field2,
}
```

Beyond documentation, the `flags` keyword also changes the default value behavior of enum fields. Instead of
incrementally counting up from 0, enum fields will count up in powers of 2 (0, 1, 2, 4, 8, etc.) so that when the fields
are combined their bits do not conflict. You can still give fields explicit values like normal.

Additionally, flags enums string cast will display each field component of the value. For example:

```belte
var myLocal = MyEnum.Field1 | MyEnum.Field2;
var myString = (string)myLocal;
// myString = "Field1, Field2"

enum flags MyEnum {
  None,
  Field1,
  Field2,
}
```

### 4.6.2 Implicit Enum Fields

In target typed expressions, an implicit enum field expression can be used which
omits the enum type name. The following are equivalent:

```belte
var myLocal = MyEnum.Field1;
MyEnum myLocal = .Field1;
```

### 4.6.3 Experimental Underlying Types

When using the Evaluator, enums can additional represent the `string` and `char` primitives:

```belte
enum MyEnum extends string {
  Field1 = "some string",
}
```

This feature is experimental and may be removed.

## 4.7 Namespaces

Namespaces can optionally be defined in a source file to organize types. Namespace names allow periods.

```belte
namespace MyNamespace {
  class A {
    ...
  }

  ...
}
```

Instead of using enclosing curly braces, namespaces can be scoped to the entire source file. Only one namespace can be
used per file if they are file scoped:

```belte
namespace MyNamespace;

class A {
  ...
}
```

## 4.8 Using Directives

Using directives can be used to access namespace or class members without needing to type the qualifier when outside of
the container. Using namespace directives follow the format `using <namespace name>;`.

```belte
namespace MyNamespace {
  public class A { }
}
```

```belte
using MyNamespace;

var a = new A();
```

Using class directives follow the format `using static <class name>;`.

```belte
public class A {
  public class B { }
}
```

```belte
using static A;

var b = new B();
```

Using directives can be tied to the source file or to a namespace:

```belte
namespace A {
  using ...;
}
```

### 4.8.1 Aliasing

An alias can be defined to allow referencing a type or namespace with another name, typically for brevity or clarity:

```belte
using D = A.B.C.D;

namespace A {
  namespace B {
    namespace C {
      public class D { }
    }
  }
}

var a = new D();
```

### 4.8.3 Global Using Directive

A `global using` directive can be used to apply a using directive to an entire project instead of only in the source
file where the directive is placed.

### 4.8.2 Global Disambiguation

A `global::` qualifier can be used to disambiguate cases where it is not clear what member is being referred to due to
the usage of using directives:

```belte
using N;

class A { }

namespace N {
  public class A { }
}

var a = new A(); // ambiguous
var a = new global::A(); // clear
```

## 4.9 Structs

Structs are similar to classes. Unlike classes, structs are value types (passed by value). Structs are a collection of
ordered fields:

```belte
struct A {
  int a;
  bool b;
}
```

Structs always have a single parameter-less constructor that sets every member to it's default value. From there,
fields can be set.

```belte
var myStruct = new A();
myStruct.a = 3;
myStruct.b = true;

struct A {
  int a;
  bool b;
}
```

A [cascade expression](Data.md#3227-xy) can be used to simplify this process:

```belte
var myStruct = new A()
  ..a = 3
  ..b = true;

struct A {
  int a;
  bool b;
}
```

Because struct fields cannot have explicit initializers, structs can only contain fields of types with a default value.

### 4.9.1 Unions

A union struct is a struct where all of the fields overlap in memory. Because of this, assigning to any field in the
union may effect the other fields:

```belte
var myUnion = new A();
myUnion.a = 5;
Console.PrintLine(myUnion.b); // 5

union A {
  int32 a;
  int16 b;
}
```

An anonymous union can be used inside of a struct to align certain fields together:

```belte
var myStruct = new A()
  ..a = 3
  ..c = 10;

Console.PrintLine(myStruct.b); // 10

struct A {
  int32 a;

  union {
    int32 b;
    int16 c;
  }
}
```

In this example, the fields `b` and `c` are overlapping with each other but not with `a`.
