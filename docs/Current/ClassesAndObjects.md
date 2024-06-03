# 4 Classes and Objects

- [4.1](#41-classes) Classes
- [4.2](#42-members) Members
- [4.3](#43-modifiers) Modifiers
- [4.4](#44-constructors) Constructors
- [4.5](#45-templates) Templates

## 4.1 Classes

Classes are structures that contain data and functionality in the form of fields and methods. Fields are similar to
variables, and methods are similar to functions both in syntax and functionality.

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

Operators are similar to methods. They are declared as such:

```belte
class MyClass {
  public static MyClass operator+(MyClass left, MyClass right) {
    // Body
  }
}
```

Operator overloading is used to allow custom classes to use syntactical operators. The overloadable operators are:

`**, *, /, %, +, -, <<, >>, >>>, &, ^, |, ++, --, !, ~, []`

Note that operators must be marked [public](#431-accessibility-modifiers) and [static](#432-static--constexpr).

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

### 4.3.4 Overriding Modifiers

By default, members cannot be overridden. To allow a member to be overridden, it can be marked `virtual`. Virtual
members still require a definition. To override a virtual member, the override can be marked `override`. To instead
hide a member without overriding, a member can be marked `new`. A member cannot be marked as both `override` and `new`
or `virtual`.

Similar to `virtual`, a member can be marked `abstract`. Abstract members must be overridden in all non-abstract child
implementations, and as such abstract members do not have a definition when declared.

Currently, these modifiers only apply to methods.

### 4.3.2 Static & ConstExpr

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

### 4.3.3 Sealed  & Abstract

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

  public static t operator[](List list, int index) {
    return list.array[index];
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
