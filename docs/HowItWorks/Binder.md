# Binder

The binder is where the type checking and symbol resolving happens. In addition, it also rewrites some of the code to be
more explicit for the next stages of compilation. These code rewrites usually revolve around null checking.

One complicated feature to implement is nested functions. These are functions than are inside other functions. The
challenge is that in IL nested functions do not exist (mostly), they are artificial. To accomplish this task, whenever
the binder finds a nested function it will move it to a normally scoped function, and add a hidden parameter for each
local it references. The way it finds these is turning on a toggle when binding it thats tells the binder to keep track
of every variable reference it finds, and then adds them to the parameter list and thus every call of the nested
function.

Another complex feature is inline block evaluations. These are a unique feature (I think) in Belte that is very similar
to a lambda, but it is an expression instead of a statement and can be inserted into any place a variable could be. It
is simply a block statement that can have return statements, but instead of returning out of the enclosing function it
returns out of the block, into wherever it is placed. This makes it so one-time complex computations can be put in place
of an inline function, macro, or lambda without requiring a declaration. This reduces clutter in the scope. To obtain
this functionality the binder converts all inline block evaluations into called nested functions, which in turn rewrite
into normal functions. All abstraction.

To handle the main function, it checks for any declaration with the name main OR if it is run in interpreting mode
(usually just used for the REPL) it creates a main function called $eval. This serves as the entry point to a file. If
no main or $eval function is present, the file is run top to bottom like a script (by enclosing the entire file contents
into a new main function).

Scoping works by having a stack of scopes, where the most recent scope takes priority (shadowing). It is in essence
simple, but can because complex when functions come into play. Function calls can happen before the function is
declared in the file, so before binding the compiler must first add all function declarations to the scope. Then add
placeholders for their contents, and come back to them when they find them on the final pass through the binder. This
means modifying past scopes which is hard to do because the structure is immutable.

All global statements are enclosed in the global scope and bound first.

There are many variations off of a base type, like nullable integer vs non-nullable integer. To handle this efficiently,
the concept of type clauses are introduced. These are simple containers that contain all information about a type, and
this allows casting to work in an intuitive way. This is also where primitive array support happens, as all types have
a dimensions field (0 for normal variables).

To make sure undefined behavior is avoided, a control flow graph is created to make sure every code path returns. This
graph is created by connecting statements (nodes) in a graph and tracing them back to the return. If there is a missing
link in the graph, then an error is raised. Similarly, dead code paths are removed by checking for disconnected nodes
on the same graph.

To handle null, every binary or similar expression involving at least one null operand is rewritten into an inline block
evaluation. This block first does null checks. If they find that a operand is null, it returns null, sidestepping the
actual operation as operations such as addition do not support null in IL. Then if neither side is null it runs the
operation and returns a non-null result. This is the main reason inline block evaluations and nested functions are
crucial.

Implicitly typing variables is easier than it sounds. Every expression (even ones not tied to a type) have their own
type clause. This means that if a variable is implicit it just checks the expression type and does an identity cast.
This is why implicit variables must be defined when declared.

There are many more small details to talk about in the binder, but the ones listed above are the most important ones
or the ones with the hardest solution. During and after the binding process, lowering happens which simplifies the code
by removing language features and replacing them with lower level code.

### Mentioned Components

-> [Lowerer](Lowerer.md)
