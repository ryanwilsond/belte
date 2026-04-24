# Using Buckle

Buckle is the Belte programming language compiler.

- [Options Summary](#options-summary)
- [Running Programs](#running-programs)
- [Building to a .NET DLL](#building-to-a-net-dll)
- [Debugging a Program](#debugging-a-program)

## Options Summary

### *-h*, *--help*

Displays a brief options summary.

### *-i* (Default)

Instead of producing an executable, the program is run immediately after being compiled. This is the default behavior
when no build options are specified. There are two different methods in which the program can be run, and the compiler
gets to pick which method to use.

Currently, the compiler will always choose *--execute*.

### *-r*, *--repl*

Invokes the Repl, a Read-Eval-Print Loop where the user can enter short code snippets and get the result immediately.
The Repl is purely a command-line tool. If the *-r* or *--repl* option is passed, **all** other arguments are ignored.

For more information specifically on the Repl, see the [Repl help doc](Repl.md).

### *--type=*[console|graphics|...] (Default *console*)

Specifies the project type.

|||
|-|-|
| `console` (Default) | An application that interfaces purely with the console. |
| `graphics` | An application that creates a window. |
| `dll` | Builds into a dynamically linked library. |

### *--entry=\<name>*

Specifies a type name to search for the entry point (and update point) symbols in. The type can be namespace qualified
but cannot be nested.

### *--nostdlib*

Disables compiling with the higher-level Standard Library (collections, IO, etc.). Certain parts of the Standard Library
are still compiled with where removing them would break core language functionality (such as primitive type
definitions).

### *--evaluate*

Instead of producing an executable, the program is run immediately after being compiled. Unlike the *-i* option, the
method of running the program is always evaluation.

The program is run in a virtual environment. The Repl uses this mode.

Note that [some features have restricted](Current/Overview.md#11-endpoint-specific-features) when using this option.

### *--execute*

Instead of producing an executable, the program is run immediately after being compiled. Unlike the *-i* option, the
method of running the program is always execution.

The program is emitted to a dynamic assembly and ran, offering better performance than *--evaluate* at the cost of
slightly longer compile time.

Note that [some features have restricted](Current/Overview.md#11-endpoint-specific-features) when using this option.

### *-o \<filename>*

Specifies the output file. You cannot specify this option in junction with *-i*, *--evaluate*, or *--execute*.

### *-m:\<count>*

Specifies the maximum number of CPU cores to use. Without this option the compilation will be concurrent and use
most cores if possible. Specifying a count of 1 will disable concurrent building.

### *--severity=\<severity>* (Default *warning*)

The compiler stores all diagnostics of any severity. However, diagnostics are only logged or displayed if their severity
is greater than or equal to the given severity level. The default is *warning*.

| Severity | Description |
|-|-|
| *all* | Everything is shown. |
| *debug* | Verbose information is shown. Used for debugging purposes. |
| *info* | Any information hidden by default. |
| *warning* | Information that usually suggests a non-required change. |
| *error* | Any problem that prevents full compilation. |
| *fatal* | Any problem that immediately stops the compiler as it cannot continue. |

### *--warnlevel=\<warning-level>* (Default *1*)

Warnings are grouped into levels based on how "ignorable" they are. If *0* is passed as the
warning level, warnings are suppressed even if the [severity level](#--severityseverity-default-warning) indicates they
should be logged/displayed. Warnings are logged/displayed if their warning level is less than or equal to the passed
warning level. The default level is *1*.

> [List of which warnings are included on each level](WarningLevels.md)

### *--wignore=<*[BU|RE|CL]*\<code>,...>*

Suppresses specified warnings. Warnings should be comma delimited. Warnings should be specified using their codes.

### *--winclude=<*[BU|RE|CL]*\<code>,...>*

Specifically avoids suppressing specific warnings, even if the [severity level](#--severityseverity-default-warning) or
[warning level](#--warnlevelwarning-level-default-1) would suggest to do so. Warnings should be comma delimited.
Warnings should be specified using their codes.

### *--version*

Displays the compiler version information. The message will be in the form `Version: Buckle [version]`. The compiler
version will be in the form `MAJOR.MINOR.PATCH`.

### *--noout*

Runs the compiler normally, but prevents any file IO to occur. This option does not stop the compiler from printing to
the standard output however.

If compiling in a mode where the program would immediately run, the program is not run.

### *--clearsubmissions*

Deletes Repl submissions. If used with *-r*, the submissions are deleted before the Repl evaluates them.

### *-d*, *--dotnet*

Compile with .NET integration. The output will be a .NET DLL that can be used in a .NET project or ran depending on the
[project type](#--typeconsolegraphics-default-console).

Note that [some features have restricted](Current/Overview.md#11-endpoint-specific-features) when using this option.

### *--modulename=\<name>*

Specifies the module name used when .NET integration is enabled. Defaults to the name of the specified output file
without the file extension, or *a* is no output file was specified. This option is purely used for debugging purposes
and should not need to be used. This option is only valid in junction with the *-d*/*--dotnet* option.

### *--ref=\<file>*, *--reference=\<file>*

Adds a reference when .NET integration is enabled. This reference is a path to a DLL that will be added to the program
and can then be referenced from within the program. This option is only valid in junction with the *-d*/*--dotnet*
option.

### *--debug*

Emits a .NET PDB file containing debugging symbols. Only emits the file if the *-d*/*--dotnet* option was specified.

### *-l0*, *-l1*

Automatically includes certain library references. Each level includes all of the libraries from previous levels.

| l# | Libraries |
|-|-|
| `l0` | `System.Runtime.dll`, `System.IO.dll`, `System.Console.dll`, `System.Collections.dll` |
| `l1` | `Diagnostics.dll`, `Compiler.dll`, `Shared.dll`, `System.Collections.Immutable.dll` |

### *--time*

Displays how much time each stage of compilation took.

### *--verbose*

Displays additional information about the compilation process, such as file targets, compilation time, compiler version,
etc. The user of the compiler does **not** need to know this information to properly use the compiler. This option is
typically used for debugging.

The *--noout* option overrides *--verbose*, meaning that no information will be logged if both options are used. The
*--verbose* option will automatically set the diagnostic reporting [severity level](#--severityseverity-default-warning)
to *all*, the [warning level](#--warnlevelwarning-level-default-1) to max, and will also display
[timing information](#--time).

### *--verbose-path=\<path>*

Specifies the path the *--verbose* mode will dump files. Defaults to the working directory.

### *--info*

Displays *--verbose* information without producing file artifacts.

### *--sae*

If specified, the user will be prompted for any input after argument parsing but before any compilation, and then
prompted again after compilation is finished. This allows attaching processes to the compiler before any work is done.

## Running Programs

There is no setup required. No command-line arguments are needed apart from the files to run.

Example:

*Program.blt*

```belte
Console.PrintLine("Hello, world!");
```

*Command Line*

```bash
buckle Program.blt
```

*Result (via stdout)*

```
Hello, world!
```

## Building to a .NET DLL

Both the `-d` and `--type=dll` options output a .NET dll. The former outputs a dll alongside a runtime config file so
that the dll is ready to run by using `dotnet <path/to/dll>`.

The `--type=dll` option outputs a dll that can be referenced by other applications, but is not a runnable application
itself. There is no entry point.

## Debugging a Program

When [building to a .NET DLL](#building-to-a-net-dll) with the `--debug` flag, a PDB file is produced next to the output
assembly.

[Here is a sample that you can debug in VSCode](https://github.com/ryanwilsond/belte/tree/staging/samples/Debug/README.md).
