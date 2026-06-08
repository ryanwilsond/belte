# 2 Control Flow

- [2.1](#21-functions) Functions
  - [2.1.1](#211-nested-functions) Nested Functions
  - [2.1.2](#212-overloads) Overloads
  - [2.1.3](#213-default-arguments) Default Arguments
  - [2.1.4](#214-named-arguments) Named Arguments
  - [2.1.5](#215-template-arguments) Template Arguments
  - [2.1.6](#216-ref-arguments) Ref Arguments
    - [2.1.6.1](#2161-out-arguments) Out Arguments
  - [2.1.7](#217-argument-coercion) Argument Coercion
- [2.2](#22-entry-point) Entry Point
  - [2.2.1](#221-main) Main
  - [2.2.2](#222-program-and-update) Program And Update
  - [2.2.3](#223-disambiguating-entry-points) Disambiguating Entry Points
- [2.3](#23-conditionals) Conditionals
  - [2.3.1](#231-null-conditions) Null Conditions
  - [2.3.2](#232-null-binding-contracts) Null-Binding Contracts
- [2.4](#24-loops) Loops
  - [2.4.1](#241-while-loops) While Loops
  - [2.4.2](#242-do-while-loops) Do-While Loops
  - [2.4.3](#243-for-loops) For Loops
  - [2.4.4](#244-for-each-loops) For Each Loops
    - [2.4.4.1](#2441-string-collections) String Collections
    - [2.4.4.2](#2442-array-collections) Array Collections
    - [2.4.4.3](#2443-indexed-collections) Indexed Collections
    - [2.4.4.4](#2444-enumerated-collections) Enumerated Collections
  - [2.4.5](#245-break) Break
  - [2.4.6](#246-continue) Continue
- [2.5](#25-switch) Switch
- [2.6](#26-exceptions-and-handling) Exceptions and Handling
  - [2.6.1](#261-trycatchfinally) Try/Catch/Finally
- [2.7](#27-with-expressions-and-statements) With Expressions and Statements
- [2.8](#28-defer-statements) Defer Statements
- [2.9](#29-scoped-statements) Scoped Statements
- [2.10](#210-unreachable-statements) Unreachable Statements

## 2.1 Functions

Function syntax is the same as in many C-style languages with the format
`<return type> <name>(<parameters,...>) { <body> }` where the body is a list of statements.

```belte
void MyFunction() {
  var myNum = MyOtherFunction(true);
  Console.PrintLine(f"Number is {myNum}");
}

int MyOtherFunction(bool firstParam, string secondParam = "Default Value") {
  if (firstParam) {
    return String.Length(secondParam);
  } else {
    return 0;
  }
}
```

With [aggressive compiler warnings enabled](../Buckle.md#--warnlevelwarning-level-default-1), the compiler will warn
if a function return value is ignored such as in the following:

```belte
MyFunction();

int MyFunction() {
  return 3;
}
```

In this case, a discard assignment can be used to show that ignoring the return value was intentional:

```belte
_ = MyFunction();

int MyFunction() {
  return 3;
}
```

### 2.1.1 Nested Functions

Nested functions use the same syntax and can access symbols from the enclosing scope as such:

```belte
void TopLevelFunction(int param) {
  bool topLevelVariable = true;

  string NestedFunction() {
    return (string)param;
  }

  PrintLine(NestedFunction());
}
```

Nested functions marked `static` cannot access locals of the enclosing scope.

### 2.1.2 Overloads

As long as the signatures are different, it is valid to declare multiple functions with the same name (overloads). When
calling a function with that name, the "best" overload is chosen.

```belte
void MyFunction() { }

void MyFunction(int param) { }

MyFunction(3); // This calls the second overload because it expects an argument, while the first overload does not.
```

### 2.1.3 Default Arguments

Parameters can be given constant default values making them optional.

```belte
void MyFunction(int param = 3) { }

MyFunction(); // Because it is not specified, 'param' defaults to 3
```

Because they are optional, they must be placed after required parameters.

```belte
void MyFunction(int param1, bool param2, string param3 = "Default") { }
```

If there are multiple optional parameters, named arguments can be used to skip over specific ones.

### 2.1.4 Named Arguments

Normally, argument order must match parameter order. With named arguments, the order does not matter:

```belte
void MyFunction(int param1, bool param2) { }

MyFunction(param2: true, param1: 4);
```

This technique can be used to skip over optional parameters. Note that if using named arguments, they do not all need
to be named:

```belte
void MyFunction(int param1, int param2 = 5, int param3 = 7) { }

MyFunction(4, param3: 10);
```

### 2.1.5 Template Arguments

Similar to classes, declared functions (or methods) can be templated. The functionality of function templates are
identical to that of class templates, which can be [read about here](./ClassesAndObjects.md#45-templates). The syntax
for templates and template constraint clauses are as follows:
`void Func<template parameters...>() where { template constraint clauses... } { }`

### 2.1.6 Ref Arguments

Like locals and fields, parameters can use the `ref` keyword.

```belte
int a = 3;
Func(ref a);
Console.PrintLine(a); // 10

void Func(ref int param) {
  param = 10;
}
```

#### 2.1.6.1 Out Arguments

`out` parameters are a special kind of `ref` parameter where they do not read in the argument value.

```belte
int a = 0;
int b = Func(out a);

int Func(out int a) {
  a = 3;
  return 6;
}
```

To shortcut declaring a local immediately used as an argument for an out parameter, the declaration can be moved into
the argument:

```belte
int b = Func(out int a);

int Func(out int a) {
  a = 3;
  return 6;
}
```

Implicit typing also works in out argument declarations, but nullable annotations are disallowed:

```belte
int b = Func(out var a);

int Func(out int a) {
  a = 3;
  return 6;
}
```

Out parameters do not require assignment and will assign a default value in cases where they aren't assigned to within
the scope of the function. Because of this, types without a default value (non-nullable classes and arrays) cannot be
used as the type for an out parameter.

Out parameters can be given a default value. The following are equivalent:

```belte
void Func(out int a = 3) {
  // ...
}
```

```belte
void Func(out int a) {
  a = 3;
  // ...
}
```

If the result of the out argument is not needed, a discard expression can be used:

```belte
Func(out _);

void Func(out int a) {
  // ...
}
```

### 2.1.7 Argument Coercion

Normally, passing arguments uses normal casting rules. By using the `implicit` keyword between the parameter list and
body, explicit casts from arguments to parameters will be treated as though they were implicit:

```belte
F(3.3); // Explicit decimal -> int cast not needed

void F(int a) implicit { }
```

Without the `implicit` keyword, the call in the above example would have to be written:

```belte
F((int)3.3);

void F(int a) { }
```

## 2.2 Entry Point

If no specific entry point is declared, the program runs statements in a top-down approach (similar to Python). This is
only allowed if only one file in the compilation contains these top-level statements to avoid ambiguity.

### 2.2.1 Main

A function named `Main` is treated as the entry point if it is declared otherwise. To support command-line arguments,
the `Main` function can optionally take in arguments to retrieve them (similar to C).

**Valid** `Main` signatures:

```belte
void Main();
int32 Main();
void Main(string[]! args);
int32 Main(string[]! args);
```

Where `args` is an array of command-line arguments.

Note that to be recognized as a valid `Main`, the function identifier must be exactly `Main` (case sensitive), and
the parameter must have the exact type, but the parameter name can be anything.

**Invalid** `Main` signatures:

```belte
void main(); // Name does not match casing
string Main(); // Cannot return 'string'
void Main(int argc, string[]! argv); // Must have 0 or 1 parameters
int32 Main(string a); // Invalid parameter type, must be 'string[]!'
```

### 2.2.2 Program and Update

The `Main` function can also be found if it is a static method within a class.

```belte
public static class Program {
  public static void Main() { }
}
```

The name of the class does not matter.

Alternatively, `Main` can be made an instance method. If this is done, a single instance of the containing class will
be created so that `Main` can access instance data.

```belte
public class Program {
  int myField = 0;

  public void Main() {
    var local = myField + 3;
  }
}
```

This is most useful when compiling with `--type=graphics` where an `Update` method will be called every frame.

```belte
public class Program {
  int myField = 0;

  public void Main() {
    myField++;
  }

  public void Update(decimal deltaTime) {
    Console.PrintLine(myField);
  }
}
```

### 2.2.3 Disambiguating Entry Points

If multiple types contain a method that is recognized as the entry point, the compiler will fail to pick one. In such a
case, the [*--entry=\<name>*](../Buckle.md#--entryname) command-line argument can be used to specify a type name to
search for the entry point in.

The name passed can be just a type name, or a namespace qualified name. The passed name does not support nesting and
instead treats everything to the left of the last period as the namespace name. For example `--entry=A.B.C` would look
for the entry point within a type named `C` inside of a namespace named `A.B`.

## 2.3 Conditionals

To control the flow of the program indeterminately, `if` and `else` can be used. An `if` statement checks a condition,
and if it results as `true`, the code under it is run. Otherwise, it runs the code under the `else` statement if it
exists.

```belte
if (a > b)
  Console.PrintLine("a is greater than b");
else
  Console.PrintLine("a is not greater than b");
```

These statements contain a single statement under each of them, but this statement can be a block to allow larger
pieces of code to run under them.

```belte
if (a > b) {
  int difference = a - b;
  Console.PrintLine("a is " + (string)difference + " greater than b");
} else {
  Console.PrintLine("a is not greater than b");
}
```

If-else statements can also be chained:

```belte
if (a > b) {
  int difference = a - b;
  Console.PrintLine("a is " + (string)difference + " greater than b");
} else if (a == b) {
  Console.PrintLine("a is equal to b");
} else {
  int difference = b - a;
  Console.PrintLine("a is " + (string)difference + " less than b");
}
```

### 2.3.1 Null Conditions

The condition type of an `if` statement can be a nullable bool or non-nullable bool. If the condition type is nullable,
a runtime check is performed to see if the operand is null. If it is, a null condition exception is thrown at the site
of the condition.

To avoid this exception while still allowing nullable types in the condition expression, a
[null-erasure (`?`) operator](Data.md#3222-x) can be used which results in the operands default value if it is null.

For example:

```belte
int? a = null;

if ((a > 4)?) {
  // ...
} else {
  // ...
}
```

In this example, the result of `a > 4` is null because `a` is null. Then the null-erasure operator applies and sees null
as it's operand and results in the default value of the type, which in this case is `false` because the default value
for bools is `false`. The else block will then execute.

### 2.3.2 Null-Binding Contracts

A null-binding contract can be used to declare a temporary local within an `if` scope predicated on the fact that the
local is not null.

```belte
int? a = 3;

if (a -> x!) {
  int! b = x;
}
```

If the source expression is null, the block does not run. Otherwise, a non-nullable temporary is created for the block.

Similar to an ordinary if statement, an else block can be defined that runs only if the source expression is null.

```belte
int? a = 3;

if (a -> x!) {
  int! b = x;
} else {
  int? b = a;
}
```

## 2.4 Loops

### 2.4.1 While Loops

To conditionally loop a piece of code, a `while` loop can be used. A `while` loop takes a condition to check each loop
and a body of code to run, similar to an `if` statement. The condition is checked **before** the first loop, meaning
it is possible for the containing code to never be ran.

```belte
string msg = Input();

while (msg != "yes") {
  msg = Input();
}
```

### 2.4.2 Do-While Loops

To guarantee that the looped code runs at least once, a `do while` loop can be used instead:

```belte
string msg;

do {
  msg = Input();
} while (msg != "yes");
```

### 2.4.3 For Loops

A `for` loop contains a local declaration, condition, and iterator expression that executes once after each loop in the
form `for (<declaration> <condition>; <expression>) <body>`.

For example, a loop that displays the numbers 0 through 9 inclusive.

```belte
for (int i = 0; i < 10; i++) {
  Console.PrintLine(i);
}
```

### 2.4.4 For Each Loops

For loops can be used to iterate over a collection type. The collection expression must be an array, string, or be a
class type with special defined operators.

The for loop starts by naming a local to store the collection items, and an optional name for a local to keep track of
the current index. The index local is always of type `int!`, and the value local is inferred from the collection
expression.

#### 2.4.4.1 String Collections

Strings can be iterated over. The value type is `char!`:

```belte
for (val, idx in "test")
  Console.PrintLine(f"{idx}: '{val}'");
```

Output:

```txt
0: 't'
1: 'e'
2: 's'
3: 't'
```

Without the index local:

```belte
for (val in "test")
  Console.PrintLine(f"'{val}'");
```

#### 2.4.4.2 Array Collections

For arrays, the value type is the element type:

```belte
int sum = 0;

for (val in {1, 2, 3})
  sum += val;
```

#### 2.4.4.3 Indexed Collections

If a class defines a `length` operator and a `[]` operator where the second parameter type is `int` or `int!`, a for
loop can iterate over that an instance of that class. The
[`List<type T>` type from the standard library](StandardLibrary/List.md) is a good example of this.

The value type is the return type of the defined `[]` operator:

```belte
List<int> a = { 1, 2, 3 };
int sum = 0;

for (val in a)
  sum += val;
```

Here is a definition example:

```belte
var a = new MyClass();

for (val in a)
  Console.PrintLine(val);

class MyClass {
  private int[] arr;

  public constructor(int[] arr) {
    this.arr = arr;
  }

  public static ref int operator[](MyClass inst, int index) {
    return inst.arr[index];
  }

  public static int! operator length(MyClass inst) {
    return LowLevel.Length<int[]>(inst.arr);
  }
}
```

#### 2.4.4.4 Enumerated Collections

For complicated cases where you wish to iterate over a custom type with for loops but the items are not easily
indexable using the `[]` operator, an `iter` operator can be defined to return an `Enumerator<type T>`. Note that if a
class implements `length`, `[]`, and the `iter` operators, the for loop will prefer using `length` and `[]`.

The `Enumerator<type T>` returned by the `iter` operator must implement the `bool! MoveNext()` and `T Current()`
methods. The value type is the template argument of the `Enumerator` returned by the `iter` operator.

The [`Dictionary<type TKey, type TValue>` type from the standard library](StandardLibrary/Dictionary.md) is a good
example of a type that uses an enumerator. The `Dictionary<type TKey, type TValue` enumerator returns a
`KeyValuePair<TKey, TValue>`.

```belte
var a = {
  4: "string 1",
  5: "string 2",
  6: "string 3",
  7: "string 4"
};

for (pair in a)
  Console.PrintLine(f"[{pair.key}, {pair.value}]");
```

Here is a definition example:

```belte
var a = new A();
int sum = 0;

for (val in a)
  sum += val;

public class A {
  private int[] arr;

  public constructor(int[] arr) {
    this.arr = arr;
  }

  public static Enumerator<int>! operator iter(A a) {
    return new AEnumerator(a);
  }

  private class AEnumerator extends Enumerator<int> {
    private A a;
    private int! count = -1;

    public constructor(A a) {
      this.a = a;
    }

    public override bool! MoveNext() {
      count++;

      if (count < LowLevel.Length<int[]>(a.arr))
        return true;

      return false;
    }

    public override int Current() {
      return a.arr[count];
    }
  }
}
```

### 2.4.5 Break

In all the loops described, the `break` statement can further control the flow by exiting the entire loop early.

```belte
for (int i=0; i<10; i++) {
  if (i == 6)
    break;

  Console.PrintLine(i);
}
```

Output:

```txt
0
1
2
3
4
5
```

### 2.4.6 Continue

In all loops described, the `continue` statement can further control the flow by skipping to the next loop iteration
early.

```belte
for (int i=0; i<10; i++) {
  if (i == 6)
    continue;

  Console.PrintLine(i);
}
```

Output (note no `6`):

```txt
0
1
2
3
4
5
7
8
9
```

## 2.5 Switch

Switch statements can be used when comparing an expression to a set of known values called cases. Each case is separated
by a label. Switch statements can switch over primitive integral types and strings.

```belte
int a = /* ... */;

switch (a) {
  case 1:
    Console.PrintLine("a was 1");
  case 2:
    Console.PrintLine("a was 2");
  // ...
}
```

To share code across multiple cases, empty case labels can be stacked. Additionally, a default label can be used to
catch any values not covered by the cases:

```belte
switch (/* ... */) {
  case 1:
  case 2:
  case 3:
    // ...
  default:
    // ...
}
```

Cases do not fall through, but you can use gotos to move around the case labels:

```belte
switch (/* ... */) {
  case 1:
    // ...
    goto default;
  case 2:
    // ...
    goto case 3;
  case 3:
    // ...
  default:
    // ...
}
```

## 2.6 Exceptions and Handling

To break from the normal flow of the program, usually in the case of an error, an exception can be thrown:

```belte
throw new Exception();
```

This will crash the program. Throw expressions only accept objects that are or derive from `Exception`.

### 2.6.1 Try/Catch/Finally

A try block can be used to prevent the program the crashes if an exception is thrown. For example:

```belte
try {
  // ...
} catch {
  Console.PrintLine("exception thrown");
}
```

Where flow continues normally after the catch block is ran. A catch block is only ran if an exception is thrown inside
of the try body.

A finally body can be used to ensure a piece of code always runs:

```belte
try {
  // ...
} finally {
  Console.PrintLine("done");
}
```

Regardless of whether or not an exception was thrown in the try body, the finally body always after the try body runs.
This holds true even if the try body exits:

```belte
int Func() {
  try {
    return 3;
  } finally {
    Console.PrintLine("done");
  }
}
```

In this example, `Func` will return `3`, but the finally body will execute before exiting the function.

A finally block cannot return:

```belte
try {
  // ...
} finally {
  return; // Invalid
}
```

A try block must contain one catch body, one finally body, or both.

## 2.7 With Expressions and Statements

The `with` expression or statement can be used to wrap code inside of an assignment that is reversed when done.

For example:

```belte
this.a = 3;

return with (a = 6) SomeMethod();
```

In the above example, `SomeMethod` is ran with the field `a` set to 6, but before returning, `a` is set back to it's
starting value of 3.

In statement form:

```belte
this.a = 3;

with (a = 6) try {
  return SomeMethod();
}
```

In the case of an exception or return or other control-flow breaking circumstance within the body of the `with`, the
reversal will not take place as the with body is not exited normally. To ensure that the reversal always takes place,
a `try` keyword can be specified preceding the body as seen in the above example which wraps the body of the with in a
`try` block and the reversals inside a `finally` block. A warning is generated if a control-flow breaking construct is
used without specifying `try`.

The with expression and statement accept multiple assignments where they are assigned in the order they are listed and
reversed in the reverse order. For example, the following will result in the same order of reversals:

```belte
return with (a = 5, b = 10, c = 0) SomeMethod();
```

```belte
return with (a = 5) with (b = 10) with (c = 0) SomeMethod();
```

Using a single `with` where possible is preferred as the compiler can optimize it better.

Apart from assignments,
[user-defined reversal methods can be defined](ClassesAndObjects.md#4221-state-and-reverse-clauses) to use `with` in
more contexts.

## 2.8 Defer Statements

`defer` statements defer the execution of a statement to the end of the current block, regardless of how the block
exits.

For example:

```belte
defer Console.PrintLine("deferred");
Console.PrintLine("not deferred");
```

This will output:

```txt
deferred
not deferred
```

Defer statements are evaluated in reverse order of their placement inside the block:

```belte
defer Console.PrintLine(1);
defer Console.PrintLine(2);
defer Console.PrintLine(3);
```

```txt
3
2
1
```

Defer statements are placed inside of a [finally block](#261-trycatchfinally) so even if the program throws in the same
block, the defer statements will still run.

Defer statements can be used for resource cleanup:

```belte
var a = GetSomeResource();
defer a.Dispose();

// ...
```

Defer statements are useful when the block can return in multiple places. Instead of writing cleanup code multiple
times, a defer statement can be used:

```belte
var a = GetSomeResource();

if (err) {
  a.Dispose();
  return 1;
}

a.Dispose();
return 0;
```

Becomes:

```belte
var a = GetSomeResource();
defer a.Dispose();

if (err) {
  return 1;
}

return 0;
```

Note that even though defer statements are ran at the end of a block, they do not directly effect return statements:

```belte
int a = 3;
defer a = 6;
return a;
```

In the above example, `3` is returned. This is not a special case but rather a side effect of how returns interact with
finally blocks, where the return value is stored, the finally is evaluated, then the stored return value is returned.

Consider this code:

```belte
int F(out int a) {
  a = 3;
  defer a = 6;
  return a;
}

Console.PrintLine(F(out var a));
Console.PrintLine(a);
```

```txt
3
6
```

The function `F` returns 3 because that was the value of `a` at the return site, but the deferred assignment to `a` does
ultimately happen resulting in the out parameter `a` to be 6.

Because defer statements are attached to block scopes, the following:

```belte
{
  defer Console.PrintLine("first block");
}

{
  defer Console.PrintLine("second block");
}
```

Will output:

```txt
first block
second block
```

Because defer statements can defer most statements, blocks can be deferred:

```belte
defer {
  SomeFunc1();
  SomeFunc2();
  // ...
}
```

Similar to [finally blocks](#261-trycatchfinally), defer statements cannot return.

```belte
defer return; // Invalid
```

## 2.9 Scoped Statements

Similar to defer statements, scoped statements imply certain execution on block exit. For scoped statements, this is the
calling of a destructor on the local attached to the scoped block. For example:

```belte
scoped (var a = new A()) {
  Console.PrintLine("scoped body");
}

Console.PrintLine("outside scoped");

class A {
  destructor() { /* ... */ }
}
```

This is equivalent to:

```belte
var a = new A();

try {
  Console.PrintLine("scoped body");
} finally {
  a?.Dispose();
}

Console.PrintLine("outside scoped");

class A {
  destructor() { /* ... */ }
}
```

Instead of attaching a body to the scoped statement, it can be scoped to the enclosing block. This will result in all
statements after the scoped local declaration to be wrapped in the try:

```belte
Console.PrintLine("not captured by scoped");

scoped var a = new A();

Console.PrintLine("captured by scoped");
```

This is equivalent to:

```belte
Console.PrintLine("not captured by scoped");

var a = new A();

try {
  Console.PrintLine("captured by scoped");
} finally {
  a?.Dispose();
}
```

### 2.9.1 Destructors

The `destructor` keyword is used to create a destructor called by scoped statements:

```belte
class A {
  destructor() {
    // ...
  }
}
```

Using the `destructor` keyword ensures the member is public, has not parameters, and returns void.

Note that the destructor creates a method on the class with the signature `public void Dispose()`, which can be called
by name. This is why the above examples of scoped statements show a call to `Dispose()`.

## 2.10 Unreachable Statements

An `unreachable;` statement can be used as a shorthand for throwing an unreachable code exception.

For example, in the case of an non-exhaustive switch:

```belte
switch (/* ... */) {
  // ...
}

unreachable;
```

This can be used when the compiler cannot prove a method always returns and errs.

Note that because this turns into a [`throw`](#261-trycatchfinally), it will be caught by enclosing catch blocks.
