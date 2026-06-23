# 6 Low-Level Features

~~These features are only enabled in low-level contexts.~~

Currently, most of these features are enabled everywhere for conciseness.
[Lowlevel fields](#615-lowlevel-fields) and
[lowlevel default literals](#616-lowlevel-default-literal) still require being inside of
a lowlevel context.

- [6.1](#61-low-level-contexts) Low-Level Contexts
- [6.2](#62-structs) Structs
  - [6.2.1](#621-packing) Packing
- [6.3](#63-arrays-and-buffers) Arrays and Buffers
  - [6.3.1](#631-alternate-entry-point-signature) Alternate Entry Point Signature
- [6.4](#64-numerics) Numerics
  - [6.4.1](#641-bit-casts) Bit Casts
- [6.5](#65-pointers) Pointers
  - [6.5.1](#651-creating-and-dereferencing-pointers) Creating and Dereferencing Pointers
  - [6.5.2](#652-pointer-arithmetic) Pointer Arithmetic
- [6.6](#66-function-pointers) Function Pointers
  - [6.6.1](#661-calling-conventions) Calling Conventions
- [6.7](#67-extern-methods) Extern Methods
  - [6.7.1](#671-winbool) WinBool
  - [6.7.2](#672-unmanaged-methods) Unmanaged Methods
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
- [6.14](#614-c-strings) C-Strings
- [6.15](#615-lowlevel-fields) LowLevel Fields
- [6.16](#616-lowlevel-default-literal) LowLevel Default Literal
- [6.17](#617-double-verbatim-identifiers) Double Verbatim Identifiers

Additionally, the
[Standard Library contains a class named LowLevel that provides various helper methods](StandardLibrary/LowLevel.md).

## 6.1 Low-Level Contexts

Low-level contexts are created by applying the `lowlevel` modifier to a type
declaration, method, or block.

```belte
lowlevel class A { /* ... */ }
lowlevel struct A { /* ... */ }
lowlevel void M() { /* ... */ }
lowlevel { /* ... */ }
```

The low-level context extends from the declaration to all statements inside. In
other words, if a method is marked `lowlevel`, the parameter list of that method
can use low-level exclusive features.

## 6.2 Structs

> [Main struct docs](ClassesAndObjects.md#49-structs)

Structs may be restricted to [lowlevel contexts](#61-low-level-contexts) in the
future.

### 6.2.1 Packing

A struct's packing size is it's maximum alignment. Without an explicit packing
size, a struct's packing size is typically the machine's word size (8 bytes on
64-bit machines). The `packed` keyword can be used to set the struct's packing
size to 1 byte (i.e. no padding between fields).

```belte
// Total size: 24 bytes
struct A {
  int8 a;   // Offset 0
  int64 b;  // Offset 8
  int8 c;   // Offset 16
}
```

```belte
// Total size: 10 bytes
struct packed A {
  int8 a;   // Offset 0
  int64 b;  // Offset 1
  int8 c;   // Offset 9
}
```

An explicit packing size of 1, 2, 4, 8, 16, 32, 64, or 128 can be specified:

```belte
// Total size: 12 bytes
struct packed(2) A {
  int8 a;   // Offset 0
  int64 b;  // Offset 2
  int8 c;   // Offset 10
}
```

If the natural alignment of the struct is below the packing size, it will stay
at the natural alignment. Consider the following example:

```belte
// Total size: 12 bytes
struct packed(64) A {
  int8 a;   // Offset 0
  int32 b;  // Offset 4
  int8 c;   // Offset 8
}
```

In the above example, the struct's natural alignment is 4 bytes, and because
that is less that the specified packing size, the struct's actual alignment
will stay at 4 bytes.

## 6.3 Arrays and Buffers

> [Main array docs](Data.md#36-arrays)

The ordinary array syntax `int[]` is shorthand for `Array<int>` which tracks initialization state for each element to
prevent reading before writing to an element. To use a raw CLR array instead, a `Buffer<T>` can be used:

```belte
Buffer<int> a = new Buffer<int>(10);
int b = a[0]; // Okay
```

The buffer creation can take a size, a size and an initializer, or just an initializer form which the size is inferred:

```belte
new Buffer<int>(10);
new Buffer<int>(10, {1, 2, 3, 4, 5, 6, 7, 8, 9, 10});
new Buffer<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10});
```

In the case that no initializer is given, each element will be zero-initialized even if the type has no default value.
Buffers should only be used in performance critical code, interop, or if the initialization state of each element is
tracked separately to prevent corrupting the type system.

To get the length of the buffer, a property access can be used:

```belte
Buffer<int> a = new Buffer<int>(10);
int len = a.Length; // len = 10
```

Additionally, the [`LowLevel` helper class](StandardLibrary/LowLevel.md) provides a few methods that operate on buffers.

### 6.3.1 Alternate Entry Point Signature

The [`Main` entry point](ControlFlow.md#221-main) can optionally take in a `Buffer<string>` instead of `string[]`:

```belte
void Main(Buffer<string> args) { }
int32 Main(Buffer<string> args) { }
```

## 6.4 Numerics

To allow for better interop, several numeric types can be used to specify
specific sizes. These being `int8`, `uint8`, `int16`, `uint16`, `int32`,
`uint32`, `int64`, `uint64`, `float32`, `float64`.

Most arithmetic upcasts to `int32` or `int64`, so casting is required in cases
such as:

```belte
int16 myInt1 = 5;
int16 myInt2 = 27;
int16 myInt3 = (int16)(myInt1 | myInt2);
```

Unless knowing the specific size of the integer is required, use the normal
`int` and `decimal` types, which (eventually) will support specifying ranges.

The actual implementation size of `int` and `decimal` are not to be relied on as
they can change, though currently `int` is equivalent to `int64` and `decimal`
is equivalent to `float64`.

### 6.4.1 Bit Casts

A bit cast copies the operand bit-for-bit into a new type. For example:

```belte
int32 myInt = 3;
float32 myFloat = (float32&)myInt;
```

Where `myFloat` is now `0b11` under the hood representing the float value
`4E-45`. This operation copies the bits instead of doing C-style pointer punning
so the operand does not have to have a location:

```belte
float32 myFloat = (float32&)30;
```

The same can be done using `LowLevel.BitCast<type TFrom, type TTo>(TFrom)`:

```belte
int32 myInt = 3;
float32 myFloat = LowLevel.BitCast<int32, float32>(myInt);
```

## 6.5 Pointers

To allow for better interop, C-style pointers and be used. Pointers are always
non-nullable and can only point to non-nullable types (unless the pointed at
type is heap allocated).

Note that when possible, [references](Data.md#35-references) should be used
instead.

### 6.5.1 Creating and Dereferencing Pointers

To get the address of a local or field, the `&` operator can be used:

```belte
int! myInt = 3;
int* ptr = &myInt;
```

The address operator cannot be used on locals or fields that are marked as
[`const`, `final`, or `constexpr`](Data.md#331-modifiers) to ensure they aren't
reassigned.

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
// ...
```

Pointers can be freely cast to reinterpret them:

```belte
void* ptr = /* ... */;

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
  public void Method() { /* ... */ }
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
void* myPtr = /* ... */;
// Offset the pointer by 8 bytes
myPtr = (void*)((int64)myPtr + 8);
```

Indexing an operator will automatically offset the pointer and then dereference
it:

```belte
char* myPtr = /* ... */;
char! myChar = myPtr[10];
```

The above example is equivalent to:

```belte
char* myPtr = /* ... */;
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

static int32 MyMethod() {
  return 4;
}
```

Non-static local functions cannot have their address taken with a function pointer.

When not using `var`, the explicit function pointer type can be written as
`returnType(argTypes...)*`:

```belte
int32(bool, string)* myPtr = &MyMethod;

static int32 MyMethod(bool arg1, string arg2) { /* ... */ }
```

Function pointers are treated the same as normal pointers in that they can be
freely cast. This is helpful when trying to call a function given a vtable. To
declare an unmanaged function pointer (such as with a COM interface vtable),
mark it as such with a `~`.
Consider this example of calling the first function of a vtable:

```belte
void** vtable = /* ... */;

((void()*~)vtable[0])();
```

For clarity, the function pointer set to a temporary:

```belte
void** vtable = /* ... */;

var MyFunction = (void()*~)vtable[0];
MyFunction();
```

Parameter names are optional in function pointers types and default to `p1`,
`p2`, etc. based on parameter ordinal:

```belte
int(int a, int)* myFunc; // Signature is: int(int a, int p2)*
```

This is to allow [named arguments](ControlFlow.md#214-named-arguments) when calling the function.

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

### 6.7.1 WinBool

For interop with Windows dlls such as Win32, note that `WINBOOL` is often used,
which is 4 bytes instead of 1. For such cases, use the primitive `winbool`:

```belte
[DllImport("user32.dll")]
static extern winbool! UpdateWindow(int64* hWnd);
```

### 6.7.2 Unmanaged Methods

To pass methods as callbacks to unmanaged libraries, they must be marked with the `[Unmanaged]` attribute. Unmanaged
methods cannot be called in a managed context. Unmanaged methods must be non-templated, static, and non-virtual.

```belte
[DllImport("lib.dll")]
public static extern void SomeFunc(void(int) callback);

[Unmanaged]
public static void MyMethod(int param) { /* ... */ }

SomeFunc(MyMethod);
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
// ...

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
| - | - |
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
// ...
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
  call Console.PrintLine : (string?);
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
program. This library is not referenced by default so either [`-l1`](../Buckle.md#-l0--l1--lall) or a
[`--ref=<path>`](../Buckle.md#--refflatcopypath---referenceflatcopypath) option must be used. Some parts of the compiler
rely on other libraries that also would require referencing to use, such as `Diagnostics.dll` and `CommandLine.dll`.

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
[feature availability](Overview.md#12-endpoint-specific-features).

### 6.13.1 Messages

The following is a current list of all messages types, any extra data they might include, and when they are triggered.

| MessageKind | Description |
| - | - |
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

## 6.14 C-Strings

LPCSTRs and LPCWSTRs can be creating using `LowLevel.CreateLPCSTR(string)` and `LowLevel.CreateLPCWSTR(string)`
respectively. Alternatively, c-string literals can be used. C-strings are allocated on the heap so they should be freed.
Using a c-string literal will automatically free the string at the end of the block where the literal was created. To
manage the freeing of the strings more explicitly, the aforementioned helper methods should be used instead.

The following are equivalent:

```belte
uint8* a = c"test";
```

```belte
uint8* temp = LowLevel.CreateLPCSTR("test");
defer LowLevel.FreeLPCSTR(temp);
uint8* a = temp;
```

Similarly for wide strings:

```belte
char* a = w"test";
```

```belte
char* temp = LowLevel.CreateLPCWSTR("test");
defer LowLevel.FreeLPCWSTR(temp);
char* a = temp;
```

C-strings can be [interpolated](Data.md#3122-string-interpolation):

```belte
int myNum = 10;
char* a = wf"num is {myNum}";
uint8* b = cf"num is {myNum}";
```

C-strings can also be [multiline](Data.md#3121-multiline-strings):

```belte
int myNum = 10;
char* a = wf"""
  num is
    {myNum}
  """;
```

## 6.15 LowLevel Fields

A field marked `lowlevel` has no [definite assignment](ClassesAndObjects.md#4211-definite-assignment) restrictions
meaning types without a default value can exist as fields without an initializer or constructor assignment:

```belte
lowlevel class A {
  lowlevel string a;
}
```

The `lowlevel` field modifier can only be used in lowlevel contexts.

This should only be used in cases where [`initialize` annotations](ClassesAndObjects.md#4211-definite-assignment) are
not sufficient.

## 6.16 LowLevel Default Literal

A `lowlevel default` literal assigns a default value to types that normally don't accept a default value:

```belte
class A { }

lowlevel {
  A! a = lowlevel default;
}
```

Like normal default literals, they can also be explicitly typed:

```belte
class A { }

lowlevel {
  var! a = lowlevel default(A);
}
```

Lowlevel default literals can only be used in lowlevel contexts.

This should only be used in cases where read access to a data container is tightly controlled to avoid reading while
not initialized to a valid value.

## 6.17 Double Verbatim Identifiers

The double verbatim specifier `@@` reads all trailing characters as a part of the identifier terminating at whitespace
or a subsequent `@`. This could be used to directly reference compiler-generated symbols. Here be dragons.
