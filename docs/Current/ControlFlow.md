# 2 Control Flow

- [2.1](#21-functions) Functions
- [2.2](#22-entry-point) Entry Point
- [2.3](#23-conditionals) Conditionals
- [2.4](#24-loops) Loops

## 2.1 Functions

Belte supports top-level and nested functions with support for overloads and default parameters.

Function syntax is the same as in many C-style languages with the format
`<return type> <name>(<parameters,...>) { <body> }`

```belte
void MyFunction() {
  // Body statements
}

int MyOtherFunction(bool firstParam, string secondParam = "Default Value") {
  // Body statements
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
identical to that of class templates, which can be read about [here](./ClassesAndObjects.md#45-templates). The syntax
for templates and template constraint clauses are as follows:
`void Func<template parameters...>() where { template constraint clauses... } { }`

## 2.2 Entry Point

If no specific entry point is declared, the program runs statements in a top-down approach (similar to Python). This is
only allowed if only one file in the compilation contains these top-level statements to avoid ambiguity.

### 2.2.1 Main

A function named `Main` is treated as the entry point if it is declared otherwise. To support command-line arguments,
the `Main` function can optionally take in arguments to retrieve them (similar to C).

Valid `Main` signatures:

```belte
void Main();
int Main();
void Main(int! argc, string[]! argv);
int Main(int! argc, string[]! argv);
```

Where `argc` is the number of command-line arguments and `argv` is an array of command-line arguments.

Note that to be recognized as a valid `Main`, the function identifier must be exactly `Main` (NOT case sensitive), and
the parameters must have the exact types, but the parameter names can be anything:

More valid `Main` signatures:

```belte
void main();
int MAIN();
void main(int! a, string[]! b);
int MaiN(int! argcount, string[]! args);
```

**Invalid** `Main` signatures:

```bete
string Main(); // Cannot return 'string'
void Main(int! argc); // Must have 0 or 2 parameters
int Main(string a, bool b); // Invalid parameters types, must be 'int!' and 'string[]!'
```

## 2.3 Conditionals

To control the flow of the program indeterminately, `if` and `else` can be used. An `if` statement checks a condition,
and if it results as `true`, the code under it is run. Otherwise, it runs the code under the `else` statement if it
exists.

```belte
if (a > b)
  PrintLine("a is greater than b");
else
  PrintLine("a is not greater than b");
```

These statements contain a single statement under each of them, but this statement can be a block to allow larger
pieces of code to run under them.

```belte
if (a > b) {
  int difference = a - b;
  PrintLine("a is " + (string)difference + " greater than b");
} else {
  PrintLine("a is not greater than b");
}
```

If-else statements can also be chained:

```belte
if (a > b) {
  int difference = a - b;
  PrintLine("a is " + (string)difference + " greater than b");
} else if (a == b) {
  PrintLine("a is equal to b");
} else {
  int difference = b - a;
  PrintLine("a is " + (string)difference + " less than b");
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

A more complex form of a `while` loop is a `for` loop which is the same allows for a count to be help each loop or to
loop a specific number of times:

```belte
for (int i=0; i<10; i++) {
  PrintLine(i);
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
6
7
8
9
```

For loops take the format of `for (<iterator>; <condition>; <expression>) { <body> }` as seen above.

### 2.4.4 Break

In all the loops described, the `break` statement can further control the flow by exiting the entire loop early.

```belte
for (int i=0; i<10; i++) {
  if (i == 6)
    break;

  PrintLine(i);
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

### 2.4.5 Continue

In all loops described, the `continue` statement can further control the flow by skipping to the next loop iteration
early.

```belte
for (int i=0; i<10; i++) {
  if (i == 6)
    continue;

  PrintLine(i);
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
