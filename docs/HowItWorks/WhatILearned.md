---
layout: post
title: What I Learned
---

# What I Learned

The documents in this folder will abstractly explain how the compiler works, and some interesting things I learned along
the way. The reason for this is that while developing this compiler, I found it very hard to find in-depth details on a
couple of essential topics making it very hard to develop. This has the goal of helping people who also want to make a
similar compiler.

The documents in this folder also serve to elaborate on the summaries given in the Belte Industries OnBoarding
presentation.

## Why Not Use C++

I stopped using C++ very early because of its limited OOP capabilities. Object-oriented programming works well with
building a compiler as the grammatical structure has a hierarchy in nature, implemented using inheritance. To start
building the compiler I followed a very helpful [series on how to build a simple
compiler](https://www.youtube.com/watch?v=wgHIkdUQbp0&list=PLRAdsfhKI4OWNOSfS7EUu5GRAVmze1t2y) that had a strong OOP
design, modeled after [Roslyn](https://github.com/dotnet/roslyn). I tried my best to make C++ work, but eventually after
dealing with a few too many pointers and segmentation faults, switched to C#.

I do not recommend C++ because of its limited OOP, and how it is needlessly low-level for building a compiler.

## Why Use C\#

C# has some of the best OOP capabilities. This makes it very easy to organize the compiler in a way that makes
sense. For example, it allows a Node base class that all parsed expressions inherit from to allow a more generic
way to iterate over the entire syntax tree (which is handy). It also makes looking at
[Roslyn source code](https://sourceroslyn.io/) more relevant. I did not spend too much time browsing the Roslyn source,
however, I did on occasion because Roslyn is currently one of the best compilers around.

For these reasons, a similar language like Java would probably work similarly as well.

## Contents

There is not yet a section on native assembly emitting, because that has not been developed (yet).

- [Command Line](CommandLine.md)
- [Lexer](Lexer.md)
- [Parser](Parser.md)
- [Binder](Binder.md)
- [Expander](Expander.md)
- [Lowerer](Lowerer.md)
- [Optimizer](Optimizer.md)
- [IL Emitter](ILEmitter.md)
- [C# Emitter](CSharpEmitter.md)
- [Evaluator](Evaluator.md)
- [Repl](Repl.md)
