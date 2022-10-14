# What I Learned

The documents in this folder will fully explain how the compiler works, and some interesting things I learned along the
way. The reason for this is because while developing this compiler, I found it very hard to find in depth details on a
couple essential topics making it very hard to develop. This has the goal of helping people who also want to make a
similar compiler.

The documents in this folder also serve to elaborate on the summaries given in the Belte Industries OnBoarding
presentation.

## Why Not to Use C++

I stopped using C++ very early because of its limited OOP capabilities. Now these are critical because compilers like
GCC are built in C. However to start off the compiler I followed a very helpful
[series on how to build a simple compiler](https://www.youtube.com/watch?v=wgHIkdUQbp0&list=PLRAdsfhKI4OWNOSfS7EUu5GRAVmze1t2y)
that had a strong OOP design modeled after [Roslyn](https://github.com/dotnet/roslyn). I tried my best to make C++ work
but eventually after dealing with a few too many pointers and segmentation faults, switched to C#.

I do not recommend C++ because of its limited OOP, and how it is needlessly low-level for building a compiler.

## Why to Use C\#

C# has some of, if not the best OOP capabilities. This makes it very easy to organize the compiler in a way that makes
sense. For example, it allows a Node base class that all parsed expressions inherit from to allow a more generic
way to iterate over the entire syntax tree (which is handy). It also makes looking at
[Roslyn source code](https://sourceroslyn.io/) more relevant. I did not spend too much time browsing the Roslyn source,
however I did on a couple occasions because Roslyn is currently one of the best compilers around.

For these reasons a similar language like Java would probably work very well.

## Contents

There is not a section on the Evaluator because it is fairly simply, and will probably be discontinued soon. In that
case interpreting will just emit an IL executable in memory and run it within a wrapper. There is also not a section
on assembly emitting, because that hasn't been developed (yet).

- [Command Line](CommandLine.md)
- [Lexer](Lexer.md)
- [Parser](Parser.md)
- [Binder](Binder.md)
- [Lowerer](Lowerer.md)
- [IL Emitter](ILEmitter.md)
- [Repl](Repl.md)
