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

Run `$ make releasemf` or `$ make release` to publish the project for Windows
machines either multi-file or single-file respectively.

The final executable is put into `.\bin\release\buckle.exe`.

### Publishing a Portable Release of Buckle

Run `$ make portable` to publish the project portably.

The final executable is put into `.\bin\portable\buckle.exe`.

### Building Buckle in Debug Mode

Run `$ make` or `$ make debug` to build the project in debug mode.

All debug files are put into `.\bin\debug\` and the final executable is put into
`.\bin\debug\buckle.exe`.

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
