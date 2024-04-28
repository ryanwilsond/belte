# The Belte Language & Specification

**Note:** This document will be revised to go further in depth on the topics covered and to include a complete language
specification for Belte

- [Introduction](#introduction)
- [Design Principles](#design-principles)
- [Logical vs. Physical](#logical-vs-physical)
- [Design by Contract](#design-by-contract)
- [First Class Nullability](#first-class-nullability)
- [Optimization Routines & Cast Validations](#optimization-routines-and-cast-validations)
- [Object Consistency](#object-consistency)
- [Data](#data)

## Introduction

Belte (pronounced /belt/) is the acronym for Best Ever Language To Exist. As the humorous title suggests, the Belte
project was started to create a theoretical best language for modern programming.

The Belte project was started in 2022 and is licensed by Ryan Wilson. Belte is the response to the problem of logical vs
physical, i.e. logical programming being impacted by physical implementation. The simplest example of this is array
indexing starting at 0 in most contemporary languages.

## Design Principles

The Belte project identifies the following six broad categories in order of priority to guide design:

### 1 Functionality

Belte focuses on functionality as the first goal. Not biased by industry standards, ease of implementation, or something
similar. This language aims to fix issues with programming languages and added a unique spin on a C-style language. It
aims to be intuitive like Python, robust like C#, high-performance like C++, and be able to be applied in most
situations.

### 2 Consistency

A good way to make a language hard to use is to not keep strict guidelines that hold up the standard of consistency.
Not only as a style guide but as the language design itself. Everything in Belte aims to be as consistent as possible,
reducing the background knowledge the developer is required to have. A good example to highlight how a language can go
wrong with consistency is value versus reference types in C#. The developer must know whether the object they are using
is a value or reference type, while in Belte every type/class is a value type by default. This includes built-ins and
user-defined types. This helps code readability and ease of development.

### 3 Usability

After the core functionality is there, the language also aims to be easy to use. Good for beginners, while also having
the power of C++. This hopefully makes it very accessible and popular. Python got a lot of its popularity from its
simplicity, and Belte aims to do the same.

### 4 Performance

While having the appeal of Python, it also aims to have high performance to make it more applicable to production
software. C/C++ are the current leaders in speed, but they are harder to use. Performance is not the top priority,
but the language aims to be as performant as possible as to not limit the applicability of the language.

### 5 Portability

One goal was to allow immediate running (to appeal to beginners), compiling to an executable, transpiling to C#, and to
build with .NET integration/compatibility. Achieving this goal would make Belte accessible and usably at all levels,
such as allowing it to easily be used in projects that have been established for decades without redesigning to
accommodate Belte. This increases Belte's overall appeal.

### 6 Likability

The last priority is likeability. While it is still on the minds of the developers, functionality comes first. Belte was
not created to appeal to the largest crowd, but instead to create an idea of a better language.

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
