# BELTE Design Principles

This document covers many concepts and inspirations behind BELTE, and also its major design principles.

- [Introduction](#introduction--logical-vs-physical)
- [Optimization](#optimization-tasks)
- [Consistency](#consistency)
- [Data Types](#datatypes)
- [Ilities](#ilities)

## Introduction & Logical vs Physical

BELTE is a statically and strongly typed language most similar to C# (BELTE is object oriented), compiled using the Buckle compiler.

This language and compiler is being developed to solve the biggest issue with all modern general purpose programming languages.
This is the distinction of **Logical vs Physical**. Low-level implementation details should not be a concern to the developer\*.

To better understand this distinction take the code example (in C++):

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

The only reason to use a structure over a class is more efficiency. In this example in particular the functionality of each is identical.
The developer should not need to engross themselves in these details while coding. The compiler should choose depending on which is better.

To get an idea of how the compiler could achieve this, lets look at another example:

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

The compiler would choose to treat `MyClassA` as a structure, because it can. It would not do this to `MyClassB` because it has a method, and it also has private members.
You could not make a structure equivalent because of these things, so it will always be treated as a class.
Since the compiler can be aware of the optimization to make `MyClassA` a structure behind the scenes, the developer **cannot** explicitly declare a structure.

There is no benefit to being able to choose between using a class versus a structure, because **logically** (the way they are used) structures are useless.
They are very basic classes that only exist because of the fact that they are closer to the machine. This is why some assembly languages and C have them, but not classes.
**Physically** (on the hardware level, for speed and space efficiency) there is a benefit to structures, but the developer should not care, hence the compiler doing these optimizations without the input of the developer.

One design challange is should bad statements be added because they are commonly used and people would not appreicate if they are gone. This is where the choice can be made to either keep on the path of doing it right, or aiming to make it accepted by the public. Some examples of this are the `goto` and `do while` statements, lamdas, and the `?:` operator. All of these examples are hard to read. Goto is a harder to read function, or a hack to exit a nested loop. Do while makes you go to the bottom of the statement to read the condition. Lamdas are harder to read inline functions, similar to macros. The ?: operator is a harder to read if-else statement, especially because it is not in every language.

\* With the exception of low-level tasks that require maximum efficiency or low-level features, e.g. using assembly language ever.

___

## Optimization Tasks

A big part of the language is being able to create routines in a class definition to tell the compiler how to optimize it if conditions are met.
Most of the STI use these to make the smart compiler optimizations possible and implemented. This is the unique part of the compiler where niche developers (e.g. STI implementation) go low-level for efficiency.
This also allows developers to make code and think about optimization later, because they can add these routines to object definitions without changing any of their original code\*.
This will encourage the creation of more readable and intuitive code first, and keeping it like that in the future even if it is a little inefficient,

\* Of course in a lot of situations the original approach is inefficient and this does not apply. Mostly just for custom object implementations.

___

## Consistency

A big issue with many languages is consistency in how different objects are treated. For example, int C# depending on what the object is, it will either be passed by value or by reference as a default.
This is an issue, because it forces the developer to know which objects are passed by value by default, versus by reference by default.
This is a small thing, but no matter the object (including C primitive objects like `int`) they will be passed by value. You can then specify that it is a reference.

Another point on C# is the use of a modifier (`?`) on a value type to make it nullable. This adds unnecessary complexity. All objects in BELTE are nullable.

___

## DataTypes

Some of the biggest changes with BELTE from other languages are the unique set of provided types. This goes over types that have notable design changes.

### NULL

Null can be used on any object, and it prevents using the object (calling methods, accessing members). Note that null is **not** the same as 0, false, empty string, etc.

### Pointers

C-style pointers are not available. References are safer and more intuitive.

### String

Mutable to allow string manipulation. Compiler switches to immutable under the hood for efficiency if developer never modifies string.

### Numbers

Instead of having different types of numbers depending on capacity, there will be different number objects purely based on fundamental differences.

| Type | Description |
|-|-|
| int | any whole number, no limit |
| decimal | any decimal number, no limit |

This list is short, because it is all that is needed.
You can add on to each of these base types a range on what numbers they can contain, allowing developers to have a limit on a number in situations where it is needed, for example an unsigned integer.

### Enumerables

Array versus vector versus linked list. They are used identically, but because of efficiency are used in different contexts.
Instead, the compiler will choose, and switch dynamically if needed.

| Type | Description |
|-|-|
| Collection/Collect | A mutable, ordered group of objects with a shared type, duplicates allowed |
| Set | A mutable, unordered group of objects with a shared type, with no duplicates |
| Map/Dictionary | A mutable, unordered\* group of key-value pairs, no duplicate keys |

\* A map is ordered based on keys under the hood unless unable (if the key is a `string` for example) to allow an algorithm to find them without iterating through every key (hashmap).

### Structures

The one use of structures specifically over classes is anonymous structures for parameters in functions (usually).
To solve this a C#-style `tuple` will be used instead because they are cleaner, more intuitive, and structures will never be added to BELTE.

___

## Ilities

List of priorities in order of most important to least important.

### Functionality

BELTE focusses on functionality as the first goal. This is one of the reasons that the syntax and design of the language were the first todo. Not biassed by industry standards, ease of implementation, or something similar. This language aims to fix issues with programming languages, added a unique spin on a c-style language. It aims to be intuitive like Python, robust like C#, high performance like C++, and able to be applicable to most situations.

### Usability

After the core functionality is there, the language also aims to be easy to use. Good for beginners, while also having the power of C++. This hopefully makes it very accessible and popular. Python got a lot of its popularity from its simplicity, and BELTE aims to do the same.

### Performance

While having the appeal of Python, it also aims to have high performance to make it more applicable to real software. C++ is the current leader is speed, but is hard to use. Performance is not the top priority however, to not limit the functionality of the language.

### Portability

Another goal is to allow compiling to an executable for small projects (another appeal for beginners), while also supporting integration with .NET. This will allow it to easily be used in projects that have been established for decades, without completely redesigning everything. This is increases accessibility, and its overall appeal. This integration allows people to focus more on the functionality and personal likeness, instead of analyzing if it will be better in the long run to redesign a system.

### Likability

The last priority is likeability. While it is still on the minds of the developers, functionality comes first. BELTE is not mainly focussed on having it appeal to the largest crowd, instead to give an example of how to make a better language. An simple example is the goto statement. It is in a lot of popular languages like C++, C#, and more. However it is believed to not be the best practice, so it is not available. This may be frustrating to some people who use goto, making it less popular.
