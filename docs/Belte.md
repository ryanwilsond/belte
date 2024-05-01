# The Belte Language & Specification

> Note: This document is incomplete

- [1](#1-introduction) Introduction
- [2](#2-scope) Scope
- [3](#3-design-principles) Design Principles
- [4](#4-the-runtime-and-optimization-routines) The RunTime and Optimization Routines
- [5](#5-types) Types

## 1 Introduction

Belte (pronounced /belt/) is the acronym for Best Ever Language To Exist. As the humorous title suggests, the Belte
project was started to create a theoretical best language for modern programming.

The Belte project was started in 2022 and is licensed by Ryan Wilson. Belte is the response to the problem of logical vs
physical, i.e. logical programming being impacted by physical implementation. The simplest example of this is array
indexing starting at 0 in most contemporary languages.

Belte is an open-source statically typed programming language that supports both the object-oriented and procedural
programming paradigms. Part of the open-source Buckle compiler is to be cross-platform and support integration with
Microsoft's .NET Framework to bolster flexibility. Belte is syntactically a C-style language, most similar to C#.

## 2 Scope

This document will **briefly** outline the syntax and semantics of Belte, stopping to elaborate on the concepts and
structures that are unique or foundational to the programming language.

## 3 Design Principles

The Belte project identifies the following six broad categories in order of priority to guide design:

### 3.1 Functionality

Belte focuses on functionality as the first goal. Not biased by industry standards, ease of implementation, or something
similar. This language aims to fix issues with programming languages and added a unique spin on a C-style language. It
aims to be intuitive like Python, robust like C#, high-performance like C++, and be able to be applied in most
situations.

### 3.2 Consistency

A good way to make a language hard to use is to not keep strict guidelines that hold up the standard of consistency.
Not only as a style guide but as the language design itself. Everything in Belte aims to be as consistent as possible,
reducing the background knowledge the developer is required to have. A good example to highlight how a language can go
wrong with consistency is value versus reference types in C#. The developer must know whether the object they are using
is a value or reference type, while in Belte every type/class is a value type by default. This includes built-ins and
user-defined types. This helps code readability and ease of development.

### 3.3 Usability

After the core functionality is there, the language also aims to be easy to use. Good for beginners, while also having
the power of C++. This hopefully makes it very accessible and popular. Python got a lot of its popularity from its
simplicity, and Belte aims to do the same.

### 3.4 Performance

While having the appeal of Python, it also aims to have high performance to make it more applicable to production
software. C/C++ are the current leaders in speed, but they are harder to use. Performance is not the top priority,
but the language aims to be as performant as possible as to not limit the applicability of the language.

### 3.5 Portability

One goal was to allow immediate running (to appeal to beginners), compiling to an executable, transpiling to C#, and to
build with .NET integration/compatibility. Achieving this goal would make Belte accessible and usably at all levels,
such as allowing it to easily be used in projects that have been established for decades without redesigning to
accommodate Belte. This increases Belte's overall appeal.

### 3.6 Likability

The last priority is likeability. While it is still on the minds of the developers, functionality comes first. Belte was
not created to appeal to the largest crowd, but instead to create an idea of a better language.

### 4 The RunTime and Optimization Routines

The Belte RunTime is a background application that monitors Belte projects and executables to profile them and collect
data to inform automatic optimizations. Optimization Routines are snippets of code that use the data collected by the
RunTime to modify programs during compile-time and run-time to increase performance.

It is encouraged to **not** create Optimization Routines while initially implementing a feature or entire project.
Optimization Routines have two purposes. 1) To give the developer the ability to ignore performance while focusing on
the logical implementation of a feature, as Optimization Routines can be added later without modifying existing logic.
And 2) to boost the performance of complex types in a way that no other contemporary programming language is.

The `RunTime` class interfaces with the RunTime program to update or retrieve data from the database or to modify
components of the program. Take the following simplified List definition as an example:

```belte
using RunTime;

public class List<type T> {
  $Tracked (% ProbabilityOfMidInsert runtime, avg AverageElementSize alltime)
  $Dynamic
  private DynamicArray<T> _collection;

  public static void Insert(Int index, T value) {
    $Data (ProbabilityOfMidInsert) Add 1 when (index > 0 && index < _collection.Length)
    $Data (AverageElementSize) Add Size(value)

    _collection.Insert(index, value);
  }

  $OptimizationRoutine (_collection) {
    if (symbolData.ProbabilityOfMidInsert > 30% || symbolData.AverageElementSize > 4kb)
      RunTime.ChangeType(_collection, LinkedList<T>);
    else if (symbolData.ProbabilityOfMidInsert < 20% || symbolData.AverageElementSize < 1kb)
      RunTime.ChangeType(_collection, DynamicArray<T>);
  }
}
```

In the example, a theoretical `List<T>` type is being defined. This List implementation contains an internal collection
that starts as a dynamic array. It defines two data fields for the RunTime to collect: a probability
`ProbabilityOfMidInsert` and a size `AverageElementSize`. It is also marked as dynamic telling the RunTime and
compiler that the true type of the variable may change. However, it is a requirement that all types must provide the
same public interface (in the form of public properties and methods) so a pseudo-statically typed system can be
enforced.

A single Optimization Routine is declared on the field `_collection`, meaning the RunTime and Compiler will only check
the conditions for the routine when changes to the `_collection` field are made, to prevent slowed performance. The
compiler may check any Optimization Routine once to solidify starting types, and the RunTime may check any Optimization
Routines any number of times while the program is running.

In the `Insert` method, two database calls are being made.

- The first updates the `ProbabilityOfMidInsert` data field which is a percentage. Percentage fields track the
probability of an action being performed at least once during a specified time span. In this case, the
`ProbabilityOfMidInsert` data field was set to track per `runtime`, so the data field measures the likelihood of at
least one middle insertion being performed each run of the program. (For more precision, a mean average could be measured
instead.) `Add 1` serves to tell the database that the action was performed. The `when` clause states to only `Add 1` if
the condition is met.
- The second updates the `AverageElementSize` data field with the size of a value. The database then uses this size as a
data point to calculate the mean average over the specified time, in this case `alltime`, so it tracks across all runs
of the program. Each data point is weighed equally in this example. The `Size(value)` expression serves to get the size
of `value` in memory (in bytes).

With data tracking and collection defined, all that is left is the Optimization Routine itself. (Any number of
Optimization Routines may be defined for any symbol or combination of symbols.) In the example, the routine checks if
the tracked `ProbabilityOfMidInsert` is greater than thirty percent or if the `AverageElementSize` is greater than four
kilobytes. If so, the RunTime is instructed to change the type of `_collection` to a linked list to accommodate the use
case, if it is not already a linked list.

The second condition checks if the `ProbabilityOfMidInsert` is less then twenty percent or if the `AverageElementSize`
is less than one kilobyte. If so, the RunTime is instructed to change the type of `_collection` to a dynamic array, if
not one already.

Notice that there are cases where both checks fall through. This is intentional, and in this case that would signify a
case where the predicted boost in performance is not substantial enough to warrant changing the type of `_collection`
during runtime, because changing the type of a field can be an expensive operation.

## 5 Types

The biggest way the Belte language tackles the logical versus physical problem is through an improved standard type
library that takes advantage of Optimization Routines. By using Optimization Routines, the Standard Type Library and
Standard Library as a whole are able to afford rethinking fundamental data types focusing on logic and not physical
implementation.

### 5.1 Numeric Types

`Num`s are any number (no minimum or maximum, no precision requirements). `Int`s are a subset of `Num`s that
are restrained to whole numbers.

```belte
class Num<Num min = null, Num max = null>;
class Int<Int min = null, Int max = null> : Num<min, max> where { Num.IsWholeNumber(value); };
```

### 5.2 Strings

`String`s are as they are in any other similar language. The `Char` type is a subset of `String`s with a restricted
length.

```belte
class String<Int minLength = null, Int maxLength = null, Regex pattern = null>;
class Char : String<1, 1>;
```

### 5.3 Collections

`Map<TKey, TValue>`s are a mutable collection type that map keys to values. `List`s are a subset of `Map`s where the key
is always an integer and does not contain gaps in keys. `Set`s are a subset of `List`s that cannot contain duplicate
values and does not ensure order.

```belte
class Map<type TKey, type TValue, bool AllowGaps = true, bool AllowDuplicates = true>;
class List<type T> : Map<Int, T, false>;
class Set<type T> : Map<Int, T, true, false>;
```

Note that C-style arrays are only allowed in `lowlevel` contexts.

### 5.4 Nullability

All data is nullable by default. Nullability can be disallowed with the null-assert operator character.

```belte
Int // Nullable
Int! // Not nullable
```

<!--
## Logical vs. Physical

As mentioned prior, the main issue that Belte aimed to address was an assortment of issues found in every programming
language related to the physical implementation and concerns of the computer bleeding into the logical aspect of
programs due to language design.

To better understand this distinction, take the code example (in C++):

```cpp
struct MyStruct {
    int val1;
    int val2;
};

class MyClass {
public:
    int val1;
    int val2;
};
```

The functionality of each example is identical, the distinction being memory efficiency. The developer needs not to
concern themselves with implementation details such as memory efficiency. To keep Belte as efficient as possible, the
compiler must decide which construct to use behind the scenes to optimize.

Take the simplified example:

```cpp
class MyClassA {
public:
    int val1;
    int val2;
};

class MyClassB {
public:
    int val1;
    int val2;

    int MyMethod();
};
```

The compiler would choose to treat `MyClassA` as a structure behind the scenes as the nature of the data causes a
structure to be more efficient here. In contrast, `MyClassB` would remain a class because it contains a method. Through
more thorough methods, the compiler can accurately optimization the conversion of classes to structures, leading to
explicitly specifying structures not only unnecessary but also not allowed in the Belte language.

To clarify, the restriction of not being able to explicitly declare structure types is not due to the compiler being
able to optimize the program, but due to the lack of significant logical different between structures and classes.

The path of removing residuals from program implementation leads to many difficult considerations. One simple yet clear
example is the topic of indexing, or rather the fact that nearly all languages of all types start indexing at 0, rather
than the logical 1.

```c
list := [ 1, 2, 3, 4, 5 ]
list[0] // <--- Results in `1` because index 0 refers to the first element
```

Skipping over the valid history for this practice, it is no longer necessary with modern technology. So as a theoretical
language, Belte **should** start indexing at 1. However, when weighing usability, it becomes less clear what the correct
answer is, as developers are conditioned to think indexing starts at 0.

## Design by Contract

Belte supports and encourages contract programming by making it native and very easy with built-in types. This also
allows built-in types to be more versatile and remove outdated types. For example, instead of having a distinct unsigned
integer, Belte allows defining bounds to an integer such as restricting it to positive values.

Declaring an unsigned integer in C vs Belte with varying levels of implied syntax:

```cpp
// C++
unsigned int myUint;
```

```cpp
// Belte
Int<min=0,max=null> myUint;
Int<min=0> myUint;
Int<0> myUint;
```

Note all the declarations in the Belte example are semantically identical.

Some contracts are generic, as in can be applied to any type. For example, you can explicitly declare that a type cannot
be null (as it can be by default). Or giving a type a single or series of expressions that must evaluate to true for a
passed value to be valid.

```cs
class MyCustomInt : Int where { value > 2; value < 10; };

MyCustomInt myVar = 8;  // Valid

MyCustomInt myVar = 1;  // Results in a compile-time error as the compile can perform constant evaluation
                        // If the value could not be determined at compile-time, a runtime error would be thrown instead
```

### Integers

The two most important constraints on integers are minimum and maximum values. This feature can be used in many
scenarios. For example, the C# `CompareTo()` method of IComparable returns an integer. The return value can indicate the
comparison was less than, equal to, or greater than. Instead of returning a boundless integer, Belte would return an
`Int<-1,1>` to restrict it to the only three values it may return. This increased type constraint leads to increased
type safety, and thus less errors.

### Strings

Similar to integers, strings have a minimum and maximum bound for length. Strings also support exact length and regular
expression matching constraints.

In Belte, the `Char` type could be defined as:

```cs
class Char : String<length=1>;
// Or simply
class Char : String<1>;
```

As mentioned, another constraint is enforcing a regular expression pattern:

```cs
String<regex="^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}$"> phoneNumber;

phoneNumber = "123-456-7890";
phoneNumber = "(123) 456-7890";
phoneNumber = "123 456 7890";
phoneNumber = "123.456.7890";
phoneNumber = "Hello, world!"; // Error
```

## First Class Nullability

Unlike C# where nullability was added after the initial release thus making it tacked on, Belte makes nullability a
"first-class" feature, supported from the beginning leading to cleaner syntax. All classes inherit from a base
class, like C#, and part of this class includes an attribute template that specifies nullability.

```cpp
class Object<attribute NotNull> where { !(NotNull && value is null); } {
    ...
}
```

An attribute is a template argument where instead of passing a value, the name of the attribute is passed. In the
previous example, all instances of the Object class are nullable by default unless the `NotNull` attribute is specified.

```cpp
Int<NotNull> myVar = 3;
myVar = null; // Throws
```

To prevent excess typing, a shorthand for the `NotNull` attribute was added. The following declarations are semantically
identical:

```cpp
Int<NotNull> myVar;
Int! myVar;
```

A change from C# is that comparison operators (like <, >, <=, >=) return null if either side is null instead of false.
This means control of flow statements can have null as the condition, but it will throw a runtime error. This behavior
of with null and comparison operators is more consistent with the original, mathematical concept of null.

## Optimization Routines and Cast Validations

A unique part of Belte is the ability to interact with the compiler in multiple ways. In some cases, optimization
routines may be used to instruct the compiler and runtime how to optimize complex types at compile-time and runtime.
Cast validations are used to instruct the compiler what casts are allowed and how to perform them.

For example, many types in the STL (Standard Library) use optimization routines to ensure the types stay logical on the
frontend without compromising efficiency on the backend. Optimization routines also allow developers to code focusing
only on functionality, and then later to revisit and optimize the code using optimization routines so the functional
code does not change. This will encourage the creation of more readable and intuitive code first, and then optimizing
later if necessary without changing the existing code.

The syntax for optimization routines and cast validations have not been created yet, but a rough idea can be shown
here:

```cs
class List<type T> {
  private DynamicArray<T> _collection;

  ...

  $OptimizationRoutine(RunTime) {
    collectionData = RunTime.DataBase.GetEntry(_collection);

    if (collectionData.ProbabilityOfMidInsertions > 30% || collectionData.AverageElementSize > 40kb)
      RunTime.ChangeType(_collection, to LinkedList<T>);
    if (collectionData.ProbabilityOfMidInsertions < 30% || collectionData.AverageElementCount > 25)
      RunTime.ChangeType(_collection, to DynamicArray<T>);
  }
}
```

```cs
class Int<int min, int max> {
  ...

  $CastValidation {
    if (from.min >= to.min && from.max <= to.max)
      Cast.Implicit;
    else
      Cast.Explicit;
  }
}
```

## Object Consistency

A big issue with many languages is consistency in how different objects are treated. For example, in C# depending on
what the object is, it will either be passed by value or by reference. This is an issue because it forces the developer
to know which types are passed by value versus by reference. This is a small nag, but a logical inconsistency
nonetheless.

In Belte all types are passed by value by default. You can optionally specify to pass by reference instead. This helps
create a system where no distinction of primitives from complex objects exists.

## Data

### Null

Null can be used on any object, and it prevents using the object (calling methods, accessing members). Note that null
is **not** the same as 0, false, empty string, etc.

### Pointers

C-style pointers are not available. References are safer and more intuitive.

### String

Mutable to allow string manipulation. The compiler switches to immutable under the hood for efficiency if the developer
never modifies string.

### Numbers

Instead of having different types of numbers depending on capacity, there will be different number objects purely
based on fundamental differences.

| Type | Description |
|-|-|
| `Int` | any whole number, no limit |
| `Decimal` | any decimal number, no limit |

This list is short because it is all that is needed. You can add to each of these base types a range on what numbers
they can contain, allowing developers to have a limit on a number in situations where it is needed, for example, an
unsigned integer.

### Enumerables

Array versus vector versus linked list. They are used identically, but because of efficiency are used in different
contexts. Instead, the compiler will choose, and switch dynamically if needed.

| Type | Description |
|-|-|
| `List<T>` | A mutable, ordered group of objects with a shared type, duplicates allowed |
| `Set<T>` | A mutable, unordered group of objects with a shared type, with no duplicates |
| `Map<TKey, TValue>` | A mutable, unordered\* group of key-value pairs, no duplicate keys |

\* A map is ordered based on keys under the hood unless unable (if the key is a `string` for example) to allow an
algorithm to find them without iterating through every key (hashmap).

### Structures

The one use of structures specifically over classes is anonymous structures for parameters in methods (usually).
To solve this a C#-style `tuple` will be used instead because they are cleaner and more intuitive, and structures will
never be added to Belte outside of low-level contexts.

## Low-Level Contexts

To broaden the utility of Belte, many lower-level features are allowed but only under low-level contexts, similar to
C#'s unsafe contexts. A file can be marked as low-level via the preprocessor directive `#lowlevel`. Low-level problems
often require low-level solutions, and those situations are the only times low-level contexts should be used.

Low-level contexts add support for the following features:

- Structs
- Pointers
- Direct heap allocation
- Primitive types
- Fixed size buffers
- Function pointers
-->
