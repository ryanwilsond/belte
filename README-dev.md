# README for Developers

To view future plans, docs, etc:

Github: [github.com/ryanwilsond/belte](https://github.com/ryanwilsond/belte)

Docs/Pages: [ryanwilsond.github.io/belte](https://ryanwilsond.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

More onboarding resources and documentation exist on a per-request basis for
developers or for those who want to contribute.

## Tools Needed for Building

This project uses the .NET SDK (8.0). To run the project, launch a debug or
release profile (currently only for Visual Studio Code). For publishing the
project, GNU Make is used.

Visual Studio Code is strongly recommended, but not required.

## Build Commands for Belte

### Publishing Buckle

Run `$ make` to publish the project in release mode.

The final executable is put into `.\bin\release\buckle.exe`.

### Publishing a Portable Release of Buckle

Run `$ make portable` to publish the project in portable release mode.

The final executable is put into `.\bin\portable\buckle.exe`.

### Building Buckle in Debug Mode

Run `$ make debug` to build the project in debug mode.

All debug files are put into `.\bin\debug\` and the final executable is put into
`.\bin\debug\buckle.exe`.

### Generating

Run `$ make generate` to generate source files.

Note that the `$ make`, `$ make portable`, and `$ make debug` commands
automatically generate source files, so manually calling the generators is
rarely needed.

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
