# 6 Low-Level Features

These features are only enabled in low-level contexts.

- [6.1](#61-low-level-contexts) Low-Level Contexts
- [6.2](#62-structures) Structures
- [6.3](#63-arrays) Arrays

## 6.1 Low-Level Contexts

Low-level contexts are created by applying the `lowlevel` modifier to a type declaration, method, or block.

```belte
lowlevel class A { ... }
lowlevel struct A { ... }
lowlevel void M() { ... }
lowlevel { ... }
```

The low-level context extends from the declaration to all statements inside. In other words, if a method is marked
`lowlevel`, the parameter list of that method can use low-level exclusive features.

## 6.2 Structures

Structures are less feature rich but more efficient classes. Eventually, the compiler will automatically rewrite classes
into structures if possible to optimize, but this is currently unsupported.

Structures only allow field declarations with no initializers. Fields within structures cannot be constants or
references.

```belte
struct MyStruct {
  int a;
  string b;
}
```

Creating a new instance of a structure uses the same `new` keyword as classes, but the constructor cannot be overridden
and always takes no arguments:

```belte
var myInstance = new MyStruct();
```

Because of this, all fields must manually be written to after structure creation:

```belte
myInstance.a = 3;
myInstance.b = "Hello";
```

## 6.3 Arrays

Whenever possible, a [List](StandardLibrary/List.md) should be used in place of C-style arrays.

C-style arrays have inherent syntax ambiguity making them unsuitable for most code.

```belte
int[]! v;
```

In the previous example, the type is marked as non-nullable. But does this apply to the variable as a whole, or is
each element also non-nullable? It is always more clear to use a [List](StandardLibrary/List.md). An array should only
be used in performance critical situations.

### 6.3.1 Initializer Lists

It is also important to note outside of low-level contexts, an initializer list will create a
[List](StandardLibrary/List.md), while inside of a low-level context, it will create an array.

```belte
// Outside of low-level context
{
  List<int> v = {1, 2, 3};
}
// Inside of low-level context
lowlevel {
  int[] v = {1, 2, 3};
}
```
