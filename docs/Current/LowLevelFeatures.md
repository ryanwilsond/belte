# 6 Low-Level Features

~~These features are only enabled in low-level contexts.~~

Currently, all of these features are enabled everywhere for conciseness.
This may change.
~~These features are only enabled in low-level contexts.~~

Currently, all of these features are enabled everywhere for conciseness.
This may change.

- [6.1](#61-low-level-contexts) Low-Level Contexts
- [6.2](#62-structures) Structures
- [6.3](#63-arrays) Arrays
- [6.4](#64-numerics) Numerics
- [6.5](#65-pointers) Pointers
- [6.6](#66-function-pointers) Function Pointers
- [6.7](#67-extern-methods) Extern Methods

Additionally, the [Standard Library contains a class named LowLevel that provides
various helper methods](StandardLibrary/LowLevel.md).
- [6.4](#64-numerics) Numerics
- [6.5](#65-pointers) Pointers
- [6.6](#66-function-pointers) Function Pointers
- [6.7](#67-extern-methods) Extern Methods

Additionally, the [Standard Library contains a class named LowLevel that provides
various helper methods](StandardLibrary/LowLevel.md).

## 6.1 Low-Level Contexts

Low-level contexts are created by applying the `lowlevel` modifier to a type
declaration, method, or block.
Low-level contexts are created by applying the `lowlevel` modifier to a type
declaration, method, or block.

```belte
lowlevel class A { ... }
lowlevel struct A { ... }
lowlevel void M() { ... }
lowlevel { ... }
```

The low-level context extends from the declaration to all statements inside. In
other words, if a method is marked `lowlevel`, the parameter list of that method
can use low-level exclusive features.
The low-level context extends from the declaration to all statements inside. In
other words, if a method is marked `lowlevel`, the parameter list of that method
can use low-level exclusive features.

## 6.2 Structures

Structures are custom data types that pass by value and use the stack, unlike
classes which are heap-allocated.
Structures are custom data types that pass by value and use the stack, unlike
classes which are heap-allocated.

Structures only allow field declarations with no initializers. Fields within
structures cannot be constants or references.
Structures only allow field declarations with no initializers. Fields within
structures cannot be constants or references.

```belte
struct MyStruct {
  int a;
  string b;
}
```

Creating a new instance of a structure uses the same `new` keyword as classes,
but the constructor cannot be overridden and always takes no arguments:
Creating a new instance of a structure uses the same `new` keyword as classes,
but the constructor cannot be overridden and always takes no arguments:

```belte
var myInstance = new MyStruct();
```

Because of this, all fields must manually be written to after structure
creation:
Because of this, all fields must manually be written to after structure
creation:

```belte
myInstance.a = 3;
myInstance.b = "Hello";
```

## 6.3 Arrays

Whenever possible, a [List](StandardLibrary/List.md) should be used in place of
C-style arrays.
Whenever possible, a [List](StandardLibrary/List.md) should be used in place of
C-style arrays.

```belte
int![]! v = { 1, 2, 3 };
int![]! v = { 1, 2, 3 };
```

Arrays are heap allocated and have no members. To sort or get the length of the
array,
[`LowLevel.Length<T>(T!)` and `LowLevel.Sort<T>(T!)` can be used](StandardLibrary/LowLevel.md).

Arrays are runtime checked, meaning trying to access an index outside the bounds
of the array will throw an exception.
Arrays are heap allocated and have no members. To sort or get the length of the
array,
[`LowLevel.Length<T>(T!)` and `LowLevel.Sort<T>(T!)` can be used](StandardLibrary/LowLevel.md).

Arrays are runtime checked, meaning trying to access an index outside the bounds
of the array will throw an exception.

### 6.3.1 Initializer Lists

~~It is also important to note outside of low-level contexts, an initializer list will create a
[List](StandardLibrary/List.md), while inside of a low-level context, it will create an array.~~

Currently, initializer lists always create arrays.

```belte
int[] v = { 1, 2, 3 };
```

## 6.4 Numerics

To allow for better interop, several numeric types can be used to specify
specific sizes. These being `int8`, `uint8`, `int16`, `uint16`, `int32`,
`uint32`, `int64`, `uint64`, `float32`, `float64`. These types are always
non-nullable.

All arithmetic upcasts to `int` and `decimal`, so casting is required in cases
such as:

```belte
int32 myInt1 = 5;
int32 myInt2 = 27;
int32 myInt3 = (int32)(myInt1 | myInt2);
```

Unless knowing the specific size of the integer is required, use the normal
`int` and `decimal` types, which (eventually) will support specifying ranges.

The actual implementation size of `int` and `decimal` are not to be relied on as
they can change, though currently `int` is equivalent to `int64` and `decimal`
is equivalent to `float64`.

## 6.5 Pointers

To allow for better interop, C-style pointers and be used. Pointers are always
non-nullable and can only point to non-nullable types (unless the pointed at
type is heap allocated).

### 6.5.1 Creating and Dereferencing Pointers

To get the address of a local or field, the `&` operator can be used:

```belte
int! myInt = 3;
int* ptr = &myInt;
```

To dereference the pointer, the `*` operator can be used:

```belte
int! myInt = 3;
int* ptr = &myInt;
int! value = *ptr; // value = 3
```

Pointers support any level of indirection:

```belte
int! myInt = 3;
int* ptr1 = &myInt;
int** ptr2 = &ptr1;
...
```

Pointers can be freely cast to reinterpret them:

```belte
void* ptr = ...;

int* myIntPtr = (int*)ptr;
int myInt = *myIntPtr;
```

No runtime checks are performed so this operation is inherently unsafe.
Consider:

```belte
int! myInt = 3;
int* ptr = &myInt;
MyClass* ptr2 = (MyClass*)ptr;
(*ptr2).Method(); // Undefined behavior

class MyClass {
  public void Method() { ... }
}
```

Unlike all other non-nullable types, pointers can be created without an
initializer, in which case they will default to a null pointer:

```belte
int* ptr; // ptr = nullptr
```

The following are all equivalent:

```belte
int* ptr;
int* ptr = nullptr;
int* ptr = (int*)null;
```

### 6.5.2 Pointer arithmetic

To do arithmetic on a pointer, you must first cast it to an integer type and,
do the arithmetic, then cast it back.

For example:

```belte
void* myPtr = ...;
// Offset the pointer by 8 bytes
myPtr = (void*)((int64)myPtr + 8);
```

Indexing an operator will automatically offset the pointer and then dereference
it:

```belte
char* myPtr = ...;
char! myChar = myPtr[10];
```

The above example is equivalent to:

```belte
char* myPtr = ...;
char! myChar = *((char*)((int64)myPtr + 10 * LowLevel.SizeOf<char!>()));
```

## 6.6 Function Pointers

Function pointers allow calling a function using a pointer to the entry point
using the `stdcall` calling convention.

To get the pointer to managed method, use the `&` operator. A function pointer
can then be called like a normal method:

```belte
var myPtr = &MyMethod;
var myInt = myPtr(); // myInt = 4

int32 MyMethod() {
  return 4;
}
```

When not using `var`, the explicit function pointer type can be written as
`returnType(argTypes...)*`:

```belte
int32(bool, string)* myPtr = &MyMethod;

int32 MyMethod(bool arg1, string arg2) { ... }
```

Function pointers are treated the same as normal pointers in that they can be
freely cast. This is helpful when trying to call a function given a vtable. To
declare an unmanaged function pointer (such as with a COM interface vtable),
mark it as such with a `~`.
Consider this example of calling the first function of a vtable:

```belte
void** vtable = ...;

((void()*~)vtable[0])();
```

For clarity, the function pointer set to a temporary:

```belte
void** vtable = ...;

var MyFunction = (void()*~)vtable[0];
MyFunction();
```

## 6.7 Extern Methods

To call into a unmanaged DLL, an extern method with a `DllImport` attribute can
be declared and called like a typical method:

```belte
[DllImport("example.dll")]
static extern void SomeMethod();

SomeMethod();
```

The method is resolved at runtime, meaning if it cannot be found an exception
will be thrown.

Extern methods use the `UniCode` char set and the `stdcall` calling convention.
