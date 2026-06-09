# Build Scripts

- [Introduction](#introduction)
- [Inputs & Outputs](#inputs--outputs)
- [Build Mode & Output Kind](#build-mode--output-kind)
- [References](#references)
- [Dependencies](#dependencies)
- [Concurrent Builds](#concurrent-builds)
- [Diagnostics](#diagnostics)
- [Logging](#logging)
- [Examples](#examples)

## Introduction

> The Builder API is a work in progress and not finalized. The API version can be obtained in source with `Buckle.Building.BuildInfo.APIVersion`.

A build script must define a static non-template method named `Build` with a single parameter of type
`Buckle.Building.Builder` (the assembly is imported automatically) that returns void.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  // ...
}
```

The builder provides an interface to define compilation information, for example:

```belte
using Buckle;
using Buckle.Building;

void Build(Builder builder) {
  builder.AddInput("src");
  builder.buildMode = BuildMode.Dotnet;
}
```

## Inputs & Outputs

An input file or directory can be added with `Builder.AddInput(path, options)`. By default, directories are searched
recursively, but this can be disabled with `InputOptions.Flat`. Any number of inputs can be added.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.AddInput("src");
  builder.AddInput("utils", .Flat);
}
```

To override the default output options, an output path can be specified with `Builder.SetOutput(path)`. Only one output
path can be specified.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetOutput("bin/project");
}
```

To override the default entry point search rules, a type name can be specified.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetEntryTypeName("MyNamespace.MyType");
}
```

Refer to the [*--entry* CLI option](Buckle.md#--entryname) for more information.

## Build Mode & Output Kind

The field `Builder.buildMode` can be assigned to to specify a build mode.

| BuildMode | Description |
| - | - |
| `None` | No output |
| `Repl` | Invokes the Repl |
| `AutoRun` | Runs the program after compilation, and automatically chooses either to interpret, evaluate, or execute |
| `Interpret` | Runs the program after compilation by interpreting |
| `Evaluate` | Runs the program after compilation by evaluating |
| `Execute` | Runs the program after compilation by executing |
| `Independent` | Compiles the program into a native executable |
| `CSharpTranspile` | Transpiles the program into C# source |
| `Dotnet` | Compiles with .NET integration which compiles into IL and assembles into a DLL |

The field `Builder.outputKind` can be assigned to to specify a project type.

| OutputKind | Description |
| - | - |
| `ConsoleApplication` (Default) | An application that interfaces purely with the console |
| `GraphicsApplication` | An application that creates a window |
| `DynamicallyLinkedLibrary` | Builds into a dynamically linked library |

For example:

```belte
using Buckle;
using Buckle.Building;

void Build(Builder builder) {
  builder.outputKind = .DynamicallyLinkedLibrary;
  builder.buildMode = .Dotnet;
}
```

```belte
using Buckle;
using Buckle.Building;

void Build(Builder builder) {
  builder.outputKind = .GraphicsApplication;
  builder.buildMode = .Execute;
}
```

To perform a debug build, the [*--debug* CLI option](Buckle.md#--debug) can be used when running `buckle build` or
`build run`, or the field `Builder.debugBuild` can be set to `true`.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.debugBuild = true;
}
```

## References

DLL references can be added with `Builder.AddRef(path, options)`. By default, directories search for `*.dll` files
recursively, but this can be disabled with `RefOptions.Flat`. Any number of references can be added. Additionally,
`RefOptions.Copy` can be used to have those references libraries copied to the output directory automatically.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.AddRef("lib", .Copy);
  builder.SetOutput("bin/project");
}
```

`Builder.IncludeNETSDK()` can be used to reference all installed core .NET SDK libraries automatically.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.IncludeNETSDK();
}
```

To disable building with the native Belte Standard Library, set the `Builder.includeStdLib` field to `false`.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.includeStdLib = false;
}
```

## Dependencies

To add dependency files that otherwise the compiler does not use (such as a unmanaged DLL loaded at runtime),
`Builder.AddDep(path, filter options)` can be used:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.AddDep("lib", "*.dll");
}
```

In the above example, all files ending in `.dll` found within the `lib` directory (searched recursively by default) will
be copied directly into the output directory. To not search recursively, `DepOptions.flatSearch` can be set to `true`:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.AddDep("lib", "*.dll", new DepOptions()..flatSearch = true);
}
```

To place all of the found files into a subdirectory of the output directory, `DepOptions.outSubDir` can be set:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.AddDep("lib", "*.dll", new DepOptions()..outSubDir = "libs");
}
```

To preserve the structure of the dependency directory, `DepOptions.preserveStructure` can be set to `true`:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.AddDep("lib", "*.dll", new DepOptions()..preserveStructure = true);
}
```

For example, if the DLL with path `lib/mydir/file.dll` is found with structure preserved, it will be placed in
`<out>/mydir/file.dll` instead of the default `<out>/file.dll`.

## Concurrent Builds

By default builds are concurrent. To specify the maximum number of CPU cores to use, use `Builder.SetMaxCores(count)`.
To disable concurrent builds, set count to 1.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetMaxCores(1);
}
```

## Diagnostics

### General Reporting

The diagnostic reporting severity can be set with `Builder.SetDiagnosticSeverity(severity)` where only diagnostics of
the given severity or more severe will be reported. The default is `DiagnosticSeverity.Warning`.

> [List of diagnostic severities](Buckle.md#--severityseverity-default-warning)

For example:

```belte
using Buckle.Building;
using Diagnostics;

void Build(Builder builder) {
  builder.SetDiagnosticSeverity(DiagnosticSeverity.Info);
}
```

The diagnostic reporting warning level can be set with `Builder.SetWarningLevel(level)`. The default is `1`.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetWarningLevel(2);
}
```

Specific warnings can be included and excluded by using `Builder.IncludeWarnings(codes)` and
`Builder.ExcludeWarnings(codes)`. Both of these can be called multiple times and they will be aggregated.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.IncludeWarnings({ "BU0002", "BU0041" });
  builder.ExcludeWarnings({ "BU0026" });
}
```

### Source-Specific Reporting

By default, diagnostic options apply to all added sources regardless of ordering. To set options on a per-source basis,
the diagnostic flag mode can be set to `DiagnosticFlagMode.Positional` via `Builder.SetDiagnosticFlagMode(mode)`. When
in this mode, options will be applied to the next added source. Any sources after that go back to using the default
diagnostic options unless set again.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetDiagnosticFlagMode(.Positional);

  builder.SetDiagnosticSeverity(.Warning);
  builder.AddInput("src");

  builder.SetDiagnosticSeverity(.Error);
  builder.AddInput("lib");
}
```

In the above example, files compiled under `src` will report warnings and above, while files compiled under `lib` will
only report errors and above.

The diagnostic flag mode can be set back to global where sources will use any globally set options.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetDiagnosticSeverity(.Error);
  builder.AddInput("src1");

  builder.SetDiagnosticFlagMode(.Positional);
  builder.SetDiagnosticSeverity(.Info);

  builder.AddInput("src2");

  builder.SetDiagnosticFlagMode(.Global);
  builder.AddInput("src3");
}
```

In the above example, `src1` and `src3` will both have a diagnostic reporting severity of errors or higher as they were
both added as inputs while in global diagnostic flag mode. `src2` will have a reporting severity of info or higher.

### Warnings as Errors

To treat all warnings as errors, `Builder.IncludeWarningsAsErrors()` can be used. After that, specific warnings can be
excluded from this promotion by using `Builder.ExcludeWarningsAsErrors(codes)`:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.IncludeWarningsAsErrors();
  builder.ExcludeWarningsAsErrors({ "BU0447" });
}
```

Alternatively, `Builder.IncludeWarningsAsErrors(codes)` can be used to instead default to treating warnings normally,
but promote a specific list of warnings to errors:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.IncludeWarningsAsErrors({ "BU0252", "BU0253", "BU0272" });
}
```

## Logging

To enable verbose logging, `Builder.SetVerboseMode(mode)` can be used.

| VerboseMode | Description |
| - | - |
| `Off` | No output on successful build |
| `Normal` | All verbose output including artifacts |
| `Reduced` | All verbose output excluding artifacts |
| `TimeOnly` | Only compilation timing information |

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetVerboseMode(.TimeOnly);
}
```

To override the default verbose artifact output path (the working directory), `Builder.SetVerboseArtifactPath(path)` can
be used.

For example:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.SetVerboseMode(.Normal);
  builder.SetVerboseArtifactPath("artifacts");
}
```

## Examples

Consider this setup:

```txt
├─artifacts/
├─bin/
├─lib/
│ └─Raylib/
│   ├─Raylib.blt
│   └─raylib.dll
├─src/
│ └─Program.blt
└─Build.blt
```

In this example, `Program.blt` is the main code. `Raylib.blt` is a library source file defining Belte bindings for
RayLib. The accompanying `raylib.dll` is the native library (not managed .NET).

This build script uses strict warning settings for the main code but minimal reporting for library code. It puts the
main outputs into `bin/` including copying `raylib.dll` which is found with `builder.AddDep`.

```belte
using Buckle;
using Buckle.Building;

void Build(Builder builder) {
  builder.SetDiagnosticFlagMode(.Positional);
  builder.SetDiagnosticSeverity(.Warning);
  builder.SetWarningLevel(2);
  builder.IncludeWarningsAsErrors();

  builder.AddInput("src");

  builder.SetDiagnosticSeverity(.Error);
  builder.AddInput("lib");

  builder.SetOutput("bin/Game");
  builder.buildMode = BuildMode.Dotnet;
  builder.outputKind = OutputKind.ConsoleApplication;

  builder.AddDep("lib", "*.dll");

  builder.SetVerboseMode(.Normal);
  builder.SetVerboseArtifactPath("artifacts");
}
```

Note that some of the above settings are only for clarity and reflect the default values for those fields (e.g.
`buildMode` and `outputKind`).

A more minimal setup could look like:

```belte
using Buckle.Building;

void Build(Builder builder) {
  builder.AddInput("src");
  builder.AddInput("lib");
  builder.SetOutput("bin/Game");
  builder.AddDep("lib", "*.dll");
}
```
