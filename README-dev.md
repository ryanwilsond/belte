# README for Developers

To view future plans, docs, etc:

GitHub Repository: [github.com/ryanwilsond/belte](https://github.com/ryanwilsond/belte)

Docs/Pages: [ryanwilsond.github.io/belte](https://ryanwilsond.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

More onboarding resources and documentation exist on a per-request basis for
developers or for those who want to contribute.

## Tools Needed for Building

- [GNU Make](https://gnuwin32.sourceforge.net/packages/make.htm)
- [.NET SDK 8.0 and .NET Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

Visual Studio Code is strongly recommended, but not required.

## Build Commands for Belte

Before building Buckle in any way, run `$ make setup` if you haven't already.
This commend is a one-time setup that ensures the project is ready to be built.

If you have ever ran this command before, you shouldn't need to run it again.

### Publishing Buckle

Run `$ make release` to publish the project for Windows machines.

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
or the files the source generators use (currently only [Syntax.xml](src/Buckle/Compiler/CodeAnalysis/Syntax/Syntax.xml)).

### Testing

Run `$ make test` to test all projects. The results are displayed in the
terminal after the tests run.

### Cleaning

Run `$ make clean` to clean all projects.

This is only needed when debugging a build issue, otherwise, no need to call
this command.

### Formatting

Run `$ make format` to format all projects.

This command is only run when cutting a release, meaning
developers/contributors should never call this unless they are the ones
cutting a new release.
