# Using Buckle

Buckle is the Belte programming language compiler.

## Options

| Arg | Description |
|-|-|
| -h \| --help | Displays the help information. |
| -p | Have the compiler stop after preprocessing. |
| -s | Have the compiler stop after preprocessing and compiling (not assembling or linking). |
| -c | Have the compiler stop before linking. |
| -r | Invoke the builtin Repl, ignoring all other arguments. |
| -i | Interpret realtime instead of compiling to executable. Produces no output files. |
| -d | Build with .NET integration. Gives compatibility with .NET but generally slightly slower runtime speed. |
| -o *filename* | Specify where to put the resulting output file. Can only use this with multiple input files when not stopping before linking. Defaults to *a.exe*. |
| -W*options* | Specify one or multiple options to forward to the compilers sub-processes. |
| --entry=*symbol* | Specify the entry point of the program. By default searches for *main()*, *Program.Main()* or uses top of file. |
| --modulename=*name* | Specify the name of the .NET module produced, defaults to *a* or the name of the specified output file without the extension. |
| --ref=*file* | Specify a .NET reference to add to the project. Can specify multiple. |
| --dumpmachine | Display the compiler's target system. |
| --version | Display the compiler version information. |

## Using
