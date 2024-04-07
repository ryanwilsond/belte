---
layout: post
title: Command Line
---

# Command Line

The compiler first parses the command line arguments manually. I chose to not use a library for this because I wanted
full control of the command line arguments to make it robust and work exactly how I envisioned (which is mostly like
GCC). Using an arg-parsing library MonoOptions is fine though.

After resolving all the input files, outputting help dialogs, etc, it chooses whether to call the Repl or to continue
compilation normally. Assuming the latter, the actual compiler process gets called. The way this project is organized is
first the command line and Repl are grouped because they are the only parts that directly deal with the console. The
command line then calls the compiler giving a diagnostic (errors/warnings) callback so that the compiler can print out
diagnostics without knowing about the console. Hence, the diagnostic formatting/pretty printing happens in the command
line part of the project.

The way multiple files are handled is through a compiler state. This contains information like the build mode, options,
an entry for each file to hold file-specific information, and more. Each file entry contains an input and output name,
its current stage in the compilation process, and the actual file contents. The compiler stage is how the compiler keeps
track of all the files because sometimes files in different stages will be inputted.

The compiler chooses to either interpret the file, compile with .NET compatibility, or compile independently. In all
these cases the normal Lexer-Parser-Binder trio happens, but how the output is handled is where they differ.
Interpreting runs the source at that moment, .NET compatibility uses MonoCecil to emit IL, and independent emits
assembly.

The compiler hands the files to a syntax tree that handles loading the files and creating a source text from them. This
allows tracking code down to the line and character, which is helpful in diagnostics as the developer will know exactly
where the error occurred. The syntax tree then handles calling the Lexer and Parser. After that, the compiler calls the
Binder (which in turn calls the Lowerer), and then finally, either the Emitter or Evaluator is called.

### Mentioned Components

-> [Lexer](Lexer.md)

-> [Parser](Parser.md)

-> [Binder](Binder.md)

-> [Lowerer](Lowerer.md)

-> [IL Emitter](ILEmitter.md)

-> [Repl](Repl.md)
