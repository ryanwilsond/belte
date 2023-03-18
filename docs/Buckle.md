# Using Buckle

Buckle is the Belte programming language compiler.

## Options

| Arg | Description |
|-|-|
| -h \| --help | Displays the help information. |
| --version | Display the compiler version information. |
| --dumpmachine | Display the compiler's target system. |
| --no-out | Disable any output the compiler would have produced; mainly used for checking syntax or debugging. |
| --explain[BU\|RE\|CL]\<code\> | Extended information for a specific error. |
| -r | Invoke the builtin Repl, ignoring all other arguments. |
| -p | Have the compiler stop after preprocessing. |
| -s | Have the compiler stop after preprocessing and compiling (not assembling or linking). |
| -c | Have the compiler stop before linking. |
| -i | Interpret realtime instead of compiling to executable. Produces no output files. |
| -t | Transpile into C# instead of emitting to an executable. |
| -o *filename* | Specify where to put the resulting output file. Can only use this with multiple input files when not stopping before linking. Defaults to *a.exe*. |
| --severity=*severity* | Specify a reporting severity, default is *warning*. [Here](#severities) for more information on severities. |
| -d | Build with .NET integration. Gives compatibility with .NET but generally slightly slower runtime speed. |
| --modulename=*name* | Specify the name of the .NET module produced, defaults to *a* or the name of the specified output file without the extension. |
| --ref=*file* | Specify a .NET reference to add to the project. Can specify multiple. |

### Severities

| Severity | Description |
|-|-|
| *all* | Everything is shown. |
| *debug* | Verbose information is shown. Used for debugging purposes. |
| *info* | Any information hidden by default. |
| *warning* | Information that usually suggests a non-required change. |
| *error* | Any problem that does not immediately stop execution. |
| *fatal* | Any problem that immediately stops execution. |

## Running the Interpreter

There is no setup required. Just run Buckle with the `-i` (interpret mode) option, and add any files you want to run.

Example:

Program.ble
```
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
