---
layout: post
title: Design_Principles
---

# Belte Design Principles

This document covers many concepts and inspirations behind Belte, and also its major design principles.

- [Introduction](#introduction--logical-vs-physical)
- [Design by Contract](#design-by-contract)
- [Nullability & Attributes](#first-class-nullability--attributes)
- [Optimization](#optimization-tasks)
- [Consistency](#consistency)
- [Data Types](#datatypes)
- [Ilities](#ilities)

## Introduction & Logical vs Physical

Belte is a statically and strongly typed language most similar to C# (Belte is object-oriented), compiled using the
Buckle compiler.

This language and compiler are being developed to solve the biggest issue with all modern general-purpose programming
languages. This is the **Logical vs Physical** distinction. Low-level implementation details should not be a concern
to the developer\*.

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

The only reason to use a structure over a class is more efficiency. In this example in particular the functionality of
each is identical. The developer should not need to engross themselves in these details while coding. The compiler
should choose depending on which is better.

To get an idea of how the compiler could achieve this, look at another example:

```cpp
class MyClassA {
public:
    int val1;
    int val2;
};

class MyClassB {
private:
    int val3;

public:
    int val1;
    int val2;

    int MyMethod();
};
```

The compiler would choose to treat `MyClassA` as a structure because it can. It would not do this to `MyClassB`
because it has a method, and also has private members. You could not make a structure equivalent because of these
things, so it will always be treated as a class. Since the compiler can be aware of the optimization to make `MyClassA`
a structure behind the scenes, the developer **cannot** explicitly declare a structure.

There is no benefit to being able to choose between using a class versus a structure because **logically** (the way they
are used) structures are useless. They are very basic classes that only exist because they are closer to the machine.
This is why some assembly languages and C have them, but not classes. **Physically** (on the hardware level, for speed
and space efficiency) there is a benefit to structures, but the developer should not care, hence the compiler doing
these optimizations without the input of the developer.

One design challenge is: should bad statements be added because they are commonly used and people would not appreciate
them if they are gone? This is where the choice can be made to either keep on the path of doing it right or aim to make
it accepted by the public. One of the biggest examples of this is the `goto` statement. Goto acts as a harder-to-read
function, or a hack to exit a nested loop.

\* Except for low-level tasks that require maximum efficiency or low-level features, e.g. using assembly language ever.

___

## Design by Contract

Belte supports and encourages contract programming by making it native and very easy with built-in types. This also
allows built-in types to be more versatile and reduces the number of types. For example, instead of having an unsigned
int, you can add a bound to an int to make it greater than or equal to 0.

Declaring an unsigned int in C vs Belte:

```cpp
int myInt;
unsigned int myUint;
```

```belte
int myInt;
int<0> myUint;
```

Some restrictions you can add to all types are nullability and simple evaluation checks. For example, you can explicitly
declare that a type cannot be null (as it can be by default). You can also give a type an expression that must
evaluate to true for every value passing into it. This is achieved by using a `where` statement when inheriting from
`int`.

```belte
class MyCustomInt : int where { value > 2; value < 10; } { }

MyCustomInt myVar = 8;
myVar = 1; // Throws
```

### Integers

The two most important constraints on integers are minimum and maximum. This feature can be used in many scenarios. For
example, the C# `CompareTo()` method of IComparable returns an int. The return value can mean less than, equal to, or
greater than. Instead of returning a normal int, Belte would return an `int<-1,1>` to restrict it to 3 values, because
that is all that is needed (-1 being the minimum with 1 the maximum).

### Strings

Similar to integers, strings have a minimum and maximum bound for length, as well as an exact length and regex match. An
example is the `char` type is defined as `class char : string<1>` (constant length 1), and not a unique type.

As mentioned you can enforce a regular expression where the string always needs to comply, or else an error is thrown.

```belte
string<match="^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}$"> phone_number;

phone_number = "123-456-7890";
phone_number = "(123) 456-7890";
phone_number = "123 456 7890";
phone_number = "123.456.7890";
phone_number = "Hello, world!"; // Error
```

In this example, the string `phone_number` must always be a 10-digit phone number.

___

## First Class Nullability & Attributes

Unlike C# where it was added after the initial release thus making it feel like it was tacked on, Belte makes
nullability very important in the language. All objects inherit from the object base class, like C#, and part of this
class includes an attribute template that specifies nullability.

```belte
class object<attribute NotNull> where { !(NotNull && value == null); } {
    ...
}
```

An attribute is a template argument / generic parameter where instead of passing a value, you just use the attribute
name as a flag that converts to a boolean true if specified. In the object example, all objects are nullable by default
but you can specify the `NotNull` attribute to say that the value cannot be null.

```belte
int<NotNull> myVar = 3;
myVar = null; // Throws
```

A change from C# is that comparison operators (like <, >, <=, >=) return null if either side is null instead of false.
This means control of flow statements can have null as the condition, but it will throw a runtime error. This is because
whenever null is involved anywhere, it means that there is a lack of a value or you do not know the value, so if
prompted with `null < 5`, null could be anything so it is not always false. This distinction that null is not just the
lack of a value is important because it is a place where programming ignores the mathematical concept of null partially,
because of physical concerns.

___

## Optimization Tasks

A big part of the language is being able to create routines in a class definition to tell the compiler how to optimize
it if conditions are met. Most of the STI (Standard Type Implementations) use these to make the smart compiler
optimizations possible and implemented. This is the unique part of the compiler where niche developers go low-level for
efficiency. This also allows developers to make code and think about optimization later because they can add these
routines to object definitions without changing any of their original code\*. This will encourage the creation of more
readable and intuitive code first, and keeping it like that in the future even if it is a little inefficient,

\* Of course, in a lot of situations, the original approach is inefficient and this does not apply. Mostly just for
custom object implementations.

___

## Consistency

A big issue with many languages is consistency in how different objects are treated. For example, int C# depending on
what the object is, it will either be passed by value or by reference as a default. This is an issue because it forces
the developer to know which objects are passed by value by default, versus by reference by default. This is a small
thing, but no matter the object (including C primitive objects like `int`) they will be passed by value. You can then
specify that it is a reference.

Another point on C# is the use of a modifier (`?`) on a value type to make it nullable. This adds unnecessary
complexity. All objects in Belte are nullable.

___

## DataTypes

Some of the biggest changes with Belte from other languages are the unique set of provided types. This goes over types
that have notable design changes.

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
| int | any whole number, no limit |
| decimal | any decimal number, no limit |

This list is short because it is all that is needed. You can add to each of these base types a range on what numbers
they can contain, allowing developers to have a limit on a number in situations where it is needed, for example, an
unsigned integer.

### Enumerables

Array versus vector versus linked list. They are used identically, but because of efficiency are used in different
contexts. Instead, the compiler will choose, and switch dynamically if needed.

| Type | Description |
|-|-|
| Collection/Collect | A mutable, ordered group of objects with a shared type, duplicates allowed |
| Set | A mutable, unordered group of objects with a shared type, with no duplicates |
| Map/Dictionary | A mutable, unordered\* group of key-value pairs, no duplicate keys |

\* A map is ordered based on keys under the hood unless unable (if the key is a `string` for example) to allow an
algorithm to find them without iterating through every key (hashmap).

### Structures

The one use of structures specifically over classes is anonymous structures for parameters in methods (usually).
To solve this a C#-style `tuple` will be used instead because they are cleaner and more intuitive, and structures will
never be added to Belte.

___

## Ilities

List of priorities in order of most important to least important.

### Functionality

Belte focuses on functionality as the first goal. This is one of the reasons that the syntax and design of the language
were the first todos. Not biased by industry standards, ease of implementation, or something similar. This language aims
to fix issues with programming languages and added a unique spin on a C-style language. It aims to be intuitive like
Python, robust like C#, high-performance like C++, and able to apply to most situations.

### Consistency

A good way to make a language hard to use is to not keep strict guidelines that hold up the standard of consistency.
Not only as a style guide but as the language design itself. Everything in Belte aims to be as consistent as possible,
reducing the background knowledge the developer is required to have. A good example to highlight how a language can go
wrong with consistency is value versus reference types in C#. The developer must know whether the object they are using
is a value or reference type, while in Belte every type/class is a value type by default. This includes built-ins and
user-defined types. This helps code readability and ease of development.

### Usability

After the core functionality is there, the language also aims to be easy to use. Good for beginners, while also having
the power of C++. This hopefully makes it very accessible and popular. Python got a lot of its popularity from its
simplicity, and Belte aims to do the same.

### Performance

While having the appeal of Python, it also aims to have high performance to make it more applicable to real software.
C++ is the current leader in speed but is hard to use. Performance is not the top priority, however, to not limit the
functionality of the language.

### Portability

Another goal is to allow compiling to an executable for small projects (another appeal for beginners), while also
supporting integration with .NET. This will allow it to easily be used in projects that have been established for
decades, without completely redesigning everything. This increases accessibility and its overall appeal. This
integration allows people to focus more on functionality and personal likeness, instead of analyzing if it will be
better in the long run to redesign a system.

### Likability

The last priority is likeability. While it is still on the minds of the developers, functionality comes first. Belte
is not mainly focused on having it appeal to the largest crowd, but instead to give an example of how to make a better
language. A simple example is the goto statement. It is in a lot of popular languages like C++, C#, and more. However
it is believed to not be the best practice, so it is not available. This may be frustrating to some people who use
goto, making it less popular.
