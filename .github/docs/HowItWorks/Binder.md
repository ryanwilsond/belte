---
layout: post
title: Binder
---

# Binder

The Binder is where the type checking and symbol resolving happens. In addition, it also rewrites some of the code to be
more explicit for the next stages of compilation. These code rewrites usually revolve around null checking.

One complicated feature to implement is nested functions. These are methods that are inside other methods. The
challenge is that in IL nested functions do not exist (mostly), they are artificial. To accomplish this task, whenever
the binder finds a nested function it will move it to a normally scoped method, and add a hidden parameter for each
local it references. The way it finds these is by turning on a toggle when binding that tells the binder to keep track
of every variable reference it finds, and then adds them to the parameter list and thus every call of the nested
function.

To handle the main method, it checks for any declaration with the name main OR if it is run in interpreting mode
(usually just used for the Repl) it creates a main method called $eval. This serves as the entry point to a file. If
no main or $eval method is present, the file is run top to bottom like a script (by enclosing the entire file contents
into a new main method).

Scoping works by having a stack of scopes, where the most recent scope takes priority (shadowing). It is in essence
simple, but can because complex when methods come into play. Method calls can happen before the method is
declared in the file, so before binding the compiler must first add all method declarations to the scope. Then add
placeholders for their contents, and come back to them when they find them on the final pass through the binder. This
means modifying past scopes which is hard to do because the structure is immutable.

All global statements are enclosed in the global scope and bound first.

There are many variations of a base type, like nullable integer vs non-nullable integer. To handle this efficiently,
the concept of type "clauses" is introduced. These are simple containers that contain all information about a type, and
this allows casting to work intuitively. This is also where primitive array support happens, as all types have
a dimensions field (0 for normal variables).

To make sure undefined behavior is avoided, a control flow graph is created to make sure every code path returns. This
graph is created by connecting statements (nodes) in a graph and tracing them back to the return. If there is a missing
link in the graph, then an error is raised. Similarly, dead code paths are removed by checking for disconnected nodes
on the same graph.

Implicitly typing variables is easier than it sounds. Every expression (even ones not tied to a type) has its type
clause. This means that if a variable is implicit it just checks the expression type and does an identity cast.
This is why implicit variables must be defined when declared.

There are many more small details to talk about in the binder, but the ones listed above are the most important ones
or the ones with the hardest solution. During and after the binding process, expansion, lowering, and optimization
occur.

### Mentioned Components

-> [Expander](Expander.md)

-> [Lowerer](Lowerer.md)

-> [Optimizer](Optimizer.md)
