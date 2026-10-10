# README for Developers

GitHub Repository: [github.com/ryanwilsond/belte](https://github.com/ryanwilsond/belte)

Docs/Pages: [ryanwilsond.github.io/belte](https://ryanwilsond.github.io/belte/)

## Tools Needed for Building

- [GNU Make](https://gnuwin32.sourceforge.net/packages/make.htm)
- [.NET SDK 10.0 and .NET Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

Note the [global.json](global.json) specifies a specific .NET version.

Visual Studio Code is strongly recommended, but not required.

## Build Commands for Belte

Before building Buckle in any way, run `$ make setup` if you haven't already.
This commend is a one-time setup that ensures the project is ready to be built.

If you have ever ran this command before, you shouldn't need to run it again.

### Publishing Buckle

If Buckle **has** been built before and has been added to path, run
`$ make releasemf` to publish the project for Windows. The final executable is
put into `./bin/release/buckle.exe` along with it's dependencies. If moving
the executable, make sure to copy all of the files inside the release folder.

If Buckle **has not** been built before, you first need to run
`$ make releasemfnolibs`. Then you need to compile the Belte Standard Library
by running `$ cd src/Belte/Belte.Core && ../../../bin/release/buckle.exe build`.
Move back to the repository root (`$ cd ../../..`) and then copy the resulting
standard library assembly to the release folder with
`$ cp src/Belte/Belte.Core/bin/Bete.Core.dll bin/release/Belte.Core.dll`. Now
the release folder is ready to be added to path. After doing so, Buckle can be
rebuilt just by using `$ make releasemf`.

### Publishing a Portable Release of Buckle

Run `$ make portable` to publish the project portably.

The final executable is put into `./bin/portable/buckle.exe`.

### Building Buckle in Debug Mode

Run `$ make` or `$ make debug` to build the project in debug mode.

All debug files are put into `./bin/debug/` and the final executable is put into
`./bin/debug/buckle.exe`.

### Generating

Run `$ make generate` to generate source files.

This is only required when changes are made to the source generators themselves,
or the files the source generators use
([Syntax.xml](src/Buckle/Compiler/CodeAnalysis/Syntax/Syntax.xml) and
[BoundNodes.xml](src/Buckle/Compiler/CodeAnalysis/Binding/BoundTree/BoundNodes.xml)).

### Testing

Run `$ make test` to test all projects. The results are displayed in the
terminal after the tests run.

### Cleaning

Run `$ make clean` to clean all projects.

This is only needed when debugging a build issue, otherwise, no need to call
this command.

### Formatting

Run `$ make format` to format all projects.
