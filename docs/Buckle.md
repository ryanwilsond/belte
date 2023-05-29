# Using Buckle

Buckle is the Belte programming language compiler.

## Options Summary

### *-h*, *--help*

Displays a brief options summary. Does not display any info on use cases or examples of using the compiler.

### *--version*

Displays the compiler version information. The message will be in the form `Version: Buckle [version]`. The compiler
version will be in the form `MAJOR.MINOR.PATCH`, following [semantic versioning 2.0.0](https://semver.org/).

### *--dumpmachine*

Displays the compiler's target system. The only applies to compiling traditionally into an executable. If invoking the
Repl, transpiling, interpreting, or using .NET integration, the compiler is portable/cross-platform.

### *--no-out*

Runs the compiler normally, but prevents any file IO to occur. This option does not stop the compiler from printing to
the standard output however.

This option is mainly used for debugging the functionality of the compiler without having to worry about produced files.

### *--explain*[BU|RE|CL]*\<code>*

Displays extended information on a specific error. This option requires inputting an error code, not an error name. To
make it more convenient to use this option, whenever the compiler produces an error the error code is part of the error
message.

Errors are in the format `buckle: [error location]: error [error code]: [error message]`. To get information on an
error, call the compiler in this format: `buckle --explain [error code]`.

Note: If no error module prefix (BU, RE, etc.) is provided, the `--explain` option will by default get errors from the
BU module.

| Module Prefix | Description |
|-|-|
| BU | Buckle; diagnostics produced during the actual compilation process. |
| RE | Repl; diagnostics produced that are unique to the Repl. |
| CL | Command Line; diagnostics produced that are unique to the command-line interface. |

### *-r*, *--repl*

Invokes the Repl, a Read-Eval-Print Loop where the user can enter short code snippets and get the result in realtime.
The Repl is purely a command-line tool. If the *-r* or *--repl* option is passed, **all** other arguments are ignored.

For more information specifically on the Repl, see the [Repl help doc](.\Repl.md).

### *-s*

Stop compilation after compiling, resulting in assemble code. File output options are treated normally, and the
outputted compiled file will be an assembly file (ending in *.s* by default). The compiling phase is always ran by
default with no output modifier options.

### *-c*

Stop compilation after assembling, resulting in an byte code. File output options are treated normally, and the
outputted compiled file will be an object file (ending in *.o* by default). The assembling phase is always ran by
default with no output modifier options.

### *-i*

Performs all compilation steps before assembling and linking, and instead of assembling and linking to an executable,
the program is run immediately. There are three different methods in which the program can be run, and the compiler
automatically picks the most optimal method based on the size of the input.

If the input is short to medium in length, the code is compiled and then ran using the evaluator. The runtime
performance is better than interpreting, but still not as fast as traditional compilation because the code is not sent
to an independent executable before running. However, unlike interpreting, the compiler compiles the entire input before
evaluating. This causes the error checking is equivalent to if you were to compile to an executable. Because the
compiler is ran before evaluating, there is a pause before the program starts executing unlike interpreting.

If the input is long, the code is compiled down to IL code, and then into an executable. This executable is wrapped and
ran immediately. The runtime performance is the same as traditional compilation, though there will be a pause before
the program executes. This pause is the longest of the three methods.

### *--script*

Performs all compilation steps before assembling and linking, and instead of assembling and linking to an executable,
the program is run immediately. However, unlike the *-i* option, this mode is interpreting meaning the entry point has
to be the root of the file.

The interpreter compiles the code chunk by chunk (which is not always equivalent to a single line unlike many
interpreters) and evaluates those chunks before moving on to the next chunk. Because of this, runtime performance is
slow because the compiler is constantly being called after each chunk. There is no pause before the program starts
executing.

This script mode only supports one file input at a time, and the entry point is always the start of the file (any other
entry point that would be used in normal compilation, e.g. `Main`, will be ignored).

### *--evaluate*

Performs all compilation steps before assembling and linking, and instead of assembling and linking to an executable,
the program is run immediately. However, unlike the *-i* option, the method of running the program is always evaluation.

The evaluators runtime performance is better than interpreting, but still not as fast as traditional compilation because
the code is not sent to an independent executable before running. However, unlike interpreting, the compiler compiles
the entire input before evaluating. This causes the error checking is equivalent to if you were to compile to an
executable. Because the compiler is ran before evaluating, there is a pause before the program starts executing unlike
interpreting.

### *--execute*

Performs all compilation steps before assembling and linking, and instead of assembling and linking to an executable,
the program is run immediately. However, unlike the *-i* option, the method of running the program is always execution.

The code is compiled down to IL code, and then into an executable. This executable is wrapped and ran immediately. The
runtime performance is the same as traditional compilation, though there will be a pause before the program executes.
This pause is the longest of the three methods of running the program.

### *-t*, *--transpile*

The code is compiled without assembling or linking. Instead, the code is transpiled into C# source code. All language
features are supported with this option.

### *-o \<filename>*

Specifies the output path or filename. This option is only valid when using the compiler on a single input, or if the
all compilation phases are completed. You cannot specify this option in junction with *-p*, *-s*, and *-c* when multiple
files are inputted. You cannot also never specify this option in junction with *-i*, *--script*, *--evaluate*,
*--execute*, *-t*, or *--transpile*.

### *--severity=\<severity>*

The compiler stores all diagnostics of any severity. However, diagnostics are only logged or displayed if their severity
is greater than or equal to the given severity level. The default is *warning*.

All severities you can pass into this option:

| Severity | Description |
|-|-|
| *all* | Everything is shown. |
| *debug* | Verbose information is shown. Used for debugging purposes. |
| *info* | Any information hidden by default. |
| *warning* | Information that usually suggests a non-required change. |
| *error* | Any problem that does not immediately stop execution. |
| *fatal* | Any problem that immediately stops execution. |

### *-d*, *--dotnet*

Compile with .NET integration. All language features are enabled with this option. The output will be a .NET DLL that
can be used in a .NET project. For more information on using this option, read the
[Building with Dotnet](#building-with-dotnet) section.

Because this specifies an endpoint, the *-p*, *-s*, *-c*, *-i*, *--script*, *--evaluate*, *--execute*, *-t*, and
*--transpile* options are not valid in junction with this option.

### *--modulename=\<name>*

Specifies the module name used when .NET integration is enabled. Defaults to the name of the specified output file
without the file extension, or *a* is no output file was specified. This option is purely used for debugging purposes
and should not need to be used. This option is only valid in junction with the *-d* or *--dotnet* options.

### *--ref=\<file>*, *--reference=\<file>*

Adds a reference when .NET integration is enabled. This reference is a path to a DLL that will be added to the program
and can then be referenced from within the program. This option is only valid in junction with the *-d* or *--dotnet*
options.

## Running the Interpreter

There is no setup required. Just run Buckle with the `-i` (interpret mode) option, and add any files you want to run.

Example:

Program.ble

```belte
int Main() {
    PrintLine("Hello, world!");

    return 1;
}
```

Command Line

```bash
$ buckle -i Program.ble
Hello, world!
```

## Building with Dotnet

To start, make a `Directory.Build.props` file with the following contents to tell dotnet to look for belte files:

```xml
<Project>
  <PropertyGroup>
    <DefaultLanguageSourceExtension>.ble</DefaultLanguageSourceExtension>
  </PropertyGroup>
</Project>
```

You will also need to create a `Directory.Build.targets` file to tell dotnet how to invoke buckle. Here is a full
example:

```xml
<Project>

  <Target Name="CreateManifestResourceNames" />

  <Target Name="CoreCompile" DependsOnTargets="$(CoreCompileDependsOn)">
    <ItemGroup>
      <ReferencePath Remove="@(ReferencePath)"
        Condition="'%(FileName)' != 'System.Runtime' AND
        '%(FileName)' != 'System.Console' AND
        '%(FileName)' != 'System.Runtime.Extensions'" />
    </ItemGroup>

    <PropertyGroup>
      <BuckleCompilerArgs>@(Compile->'&quot;%(Identity)&quot;', ' ')</BuckleCompilerArgs>
      <BuckleCompilerArgs>$(BuckleCompilerArgs) -o &quot;@(IntermediateAssembly)&quot;</BuckleCompilerArgs>
      <BuckleCompilerArgs>$(BuckleCompilerArgs) @(ReferencePath->'--ref=&quot;%(Identity)&quot;', ' ')</BuckleCompilerArgs>
    </PropertyGroup>
    <Exec Command="dotnet run --project &quot;$(MSBuildThisFileDirectory)\..\src\Buckle\Belte\Belte.csproj&quot; -- -d $(BuckleCompilerArgs)"
      WorkingDirectory="$(MSBuildProjectDirectory)" />
  </Target>

</Project>
```

This first tells dotnet to only reference `System.Runtime`, `System.Console`, and `System.Runtime.Extensions`. This part
is optional, but referencing all default libraries may slow down compilation. Then it defines how to invoke buckle
pointing to its project file. Alternatively you can add buckle to environment variables path, and call it directly
instead of wrapping it inside of `dotnet run`.
