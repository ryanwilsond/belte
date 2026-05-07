# Build Scripts

- [Introduction](#introduction)
- [Inputs & Outputs](#inputs--outputs)
- [Build Mode & Output Kind](#build-mode--output-kind)
- [References](#references)
- [Concurrent Builds](#concurrent-builds)
- [Logging](#logging)

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
