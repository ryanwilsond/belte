# 6 Low-Level Features

~~These features are only enabled in low-level contexts.~~

Currently, all of these features are enabled everywhere for conciseness.
This may change.

- [6.1](#61-low-level-contexts) Low-Level Contexts
- [6.2](#62-structures) Structures
- [6.3](#63-arrays) Arrays
  - [6.3.1](#631-initializer-lists) Initializer Lists
- [6.4](#64-numerics) Numerics
- [6.5](#65-pointers) Pointers
  - [6.5.1](#651-creating-and-dereferencing-pointers) Creating and Dereferencing Pointers
  - [6.5.2](#652-pointer-arithmetic) Pointer Arithmetic
- [6.6](#66-function-pointers) Function Pointers
  - [6.6.1](#661-calling-conventions) Calling Conventions
- [6.7](#67-extern-methods) Extern Methods
- [6.8](#68-fixed-size-buffers) Fixed Size Buffers
- [6.9](#69-sizeof-operator) Sizeof Operator
- [6.10](#610-stackalloc-operator) Stackalloc Operator
  - [6.10.1](#6101-stackalloc-locals) Stackalloc Locals
- [6.11](#611-inline-il) Inline IL
  - [6.11.1](#6111-verification) Verification
  - [6.11.2](#6112-unsupported-instructions) Unsupported Instructions
- [6.12](#612-pinned-locals) Pinned Locals
- [6.13](#613-compiler-handle) Compiler Handle
  - [6.13.1](#6131-messages) Messages
  - [6.13.2](#6132-ordering) Ordering

Additionally, the
[Standard Library contains a class named LowLevel that provides various helper methods](StandardLibrary/LowLevel.md).

## 6.1 Low-Level Contexts

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

## 6.2 Structures

Structures are custom data types that pass by value and use the stack, unlike
classes which are heap-allocated.

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

```belte
var myInstance = new MyStruct();
```

Because of this, all fields must manually be written to after structure
creation:

```belte
myInstance.a = 3;
myInstance.b = "Hello";
```

## 6.3 Arrays

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
char! myChar = *((char*)((int64)myPtr + 10 * sizeof(char!)));
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

### 6.6.1 Calling Conventions

The default unmanaged calling convention is WinAPI/STDCall (they are the same). Specifying a calling convention can be
done by following the function pointer type with `stdcall`, `winapi`, `fastcall`, `thiscall`, or `cdecl` (not case
sensitive):

```belte
void()*~[cdecl] a;
```

## 6.7 Extern Methods

> To call code in .NET (managed) DLLs, consider using a [.NET DLL reference](Interop.md)

To call into a unmanaged DLL, an extern method with a `DllImport` attribute can
be declared and called like a typical method:

```belte
[DllImport("example.dll")]
static extern void SomeMethod();

SomeMethod();
```

The method is resolved at runtime, meaning if it cannot be found an exception
will be thrown.

Extern methods use the `UniCode` char set and the `stdcall` calling convention
by default. The calling convention can be specified using the
`CallingConvention` type:

```belte
[DllImport("example.dll", CallingConvention: CallingConvention.Cdecl)]
static extern void SomeMethod();
```

## 6.8 Fixed Size Buffers

Arbitrary blobs of memory can be reserved with fixed size buffers. Fixed size
buffers are struct fields specifying a numeric type and a quantity:

```belte
struct MyStruct {
  int32 field[32];
}
```

In the above example, `field` reserves a contiguous piece of memory 128 bytes
long (`sizeof(int32) * 32 = 128`).

The field is then treated as a pointer to the start of the blob, which can then
be indexed:

```belte
var myStruct = new MyStruct();
myStruct.field[0] = 5;
myStruct.field[1] = 10;
...

struct MyStruct {
  int32 field[32];
}
```

The type pointed at by the buffer can be `bool!`, `uint8`, `int8`, `uint16`,
`int16`, `uint32`, `int32`, `uint64`, `int64`, `float32`, `float64`, or `char!`.
Note that `int` and `decimal` are not valid types in this context because their
size is not publicly defined.

## 6.9 Sizeof Operator

The `sizeof(T)` operator is the shorthand form of `$?LowLevel.SizeOf<T>()`. It
operates on a type. If the type has a known size at compile time, it replaces
the operator with that value as an `int32!`. Otherwise, it computes the size at
runtime. Size is calculated in terms of number of bytes.

The following statements are equivalent:

```belte
var myInt = sizeof(bool!);
var myInt = $?LowLevel.SizeOf<bool>();
var myInt = (int32)1;
```

The following table shows all types with a known size at compile time. All other
types compute their size at runtime.

| Type | Size |
|-|-|
| `bool!` | 1 |
| `int8` | 1 |
| `uint8` | 1 |
| `char!` | 2 |
| `int16` | 2 |
| `uint16` | 2 |
| `int32` | 4 |
| `uint32` | 4 |
| `float32` | 4 |
| `int64` | 8 |
| `uint64` | 8 |
| `float64` | 8 |

Note that taking the size of a reference type will return the size of the
reference itself, not the object. Similarly, taking the size of a pointer
returns the pointer size, not the size of the pointed at type.

## 6.10 Stackalloc Operator

Similar to fixed sized buffers for fields, the `stackalloc T[s]` operator can be
used to create a segment of memory for indexing where the size of the memory
is `sizeof(T) * s`. The memory is allocated on the stack. The operator results
in a pointer to the start of the memory.

```belte
int32* ptr = stackalloc int32[10];
ptr[0] = 5;
ptr[1] = 10;
...
```

### 6.10.1 Stackalloc Locals

A C-style shorthand is available for stackalloc expressions. The following are
equivalent:

```belte
int32 ptr[10];
int32* ptr = stackalloc int32[10];
```

## 6.11 Inline IL

For performance critical code paths or when you are trying to emit specific
instructions with no language equivalent, an inline IL block can be used:

```belte
int32 a = 0;

il {
  ldc.i4.0;
  stloc.0;
}
```

Symbols can be referenced like normal:

```belte
int32 a = 0;

il {
  call Func
  stloc.0;
}

int32 Func() {
  return 10;
}
```

Instructions with a method operand optionally allow a parameter list to
disambiguate the symbol.

```belte
il {
  call Console.PrintLine : ();
}
```

```belte
il {
  ldstr "Hello, world!";
  call Console.PrintLine : (string);
}
```

Instructions involving a constructor call also allow a parameter list, but the
operand is the type to construct.

```belte
il {
  newobj MyClass : (int, bool);
}
```

### 6.11.1 Verification

The instructions in the IL block are minimally verified. All instructions must
provide the proper number and kind of arguments.

Additionally, the stack must be balanced within the block. To bypass this check,
the `noverify` modifier can be used:

```belte
il noverify {
  add;
}
```

### 6.11.2 Unsupported Instructions

The inline IL allows most CIL instructions. The following instructions are not
currently supported:

- All branch instructions
- `endfault`
- `endfilter`
- `endfinally`
- `jmp`
- `leave`
- `leave.s`
- `no.`
- `ret`
- `rethrow`
- `switch`
- `throw`

The `jmp`, `switch`, and branch instructions are unsupported because there is
currently no way to get instruction addresses or define labels.

The `endfault`, `endfilter`, `endfinally`, `leave`, `leave.s`, `no.`, `rethrow`,
and `throw` instructions are not supported because there is currently no way to
specify exception handling blocks within the inline IL.

The `ret` instruction is unsupported to ensure the IL remains localized to it's
block and has a zero delta stack balance.

## 6.12 Pinned Locals

Locals can be pinned meaning the object they refer to will not be moved around on the heap by the garbage collector:

```belte
pinned var myLocal = new List<int>();
```

## 6.13 Compiler Handle

The `#handle <target>` preprocessor directive can be used to add a handler to the compilation. The operand of `#handle`
has to be a unambiguous class name. Any code within that class will be compiled early so that it can "watch" the rest
of the compilation via messages and a hook into the compilation.

The handle class has to contain an unambiguous static method that takes in two arguments but can return anything. The
first parameter type must be `Buckle.CodeAnalysis.Message` and the second must be `Buckle.CodeAnalysis.CompilerContext`.
The message will give phase information about the compilation. Each handler method will be called once per unique
message. The second argument gives an interface to interact with the compilation allowing things such as modifying or
adding symbols or collecting general information.

The required parameter types of the handle come from a shipped `Compiler.dll` that lives alongside the actual compiler
program. This library is not referenced by default so a
[`--ref=<path>` argument](../Buckle.md#--reffile---referencefile) must be used. Some parts of the compiler rely on
other libraries that also would require referencing to use, such as `Diagnostics.dll` and `CommandLine.dll`.

Basic example:

```belte
#handle HandleClass

using Buckle.CodeAnalysis;

public static class HandleClass {
    private static void Handler(Message msg, CompilerContext context) {
        switch (msg.Kind()) {
            case .Parsed:
                Console.PrintLine("Parsed");
            case .Bound:
                Console.PrintLine("Bound");
            case .BeforeEmit:
                Console.PrintLine("BeforeEmit");
            case .Finished:
                Console.PrintLine("Finished");
        }
    }
}
```

The handler is run during compilation using the Executor regardless of the target endpoint, so keep in mind
[feature availability](Overview.md#11-endpoint-specific-features).

### 6.13.1 Messages

The following is a current list of all messages types, any extra data they might include, and when they are triggered.

| MessageKind | Description |
|-|-|
| `Parsed` | Triggered whenever a parsed syntax tree is added to the compilation. |
| `Bound` | Triggered after method bodies have finished compiling into the abstract syntax tree. |
| `BeforeEmit` | Can never happen more than once. Triggers immediately before the compiler targets an endpoint. |
| `Finished` | Can never happen more than once. Triggers immediately after the compiler finishes emitting. Is followed by one last diagnostics resolution. |
| `Diagnostics` | Triggered whenever diagnostics are requested from the compilation object for resolution. Does not trigger for diagnostics outside of the compilation (e.g. command line parsing diagnostics). Passes the diagnostics to tentatively resolve (which can be accessed by casting the message to `DiagnosticMessage` can calling `Diagnostics()`). This trigger happens even if the diagnostic queue is empty. |

### 6.13.2 Ordering

In the case you define multiple handlers and care about which one runs first, you can specify a priority number in the
handle directive, e.g. `#handle(3) HandleClass`.

If a priority is not specified, the default is 0 meaning that handle will run last. Higher priority number means run
earlier.

If multiple handlers have the same priority (such as the default 0), they will run in an undetermined order among
themselves, but will still order correctly relative to higher/lower priority handlers.

The priority number must fit within an `int32` literal.
