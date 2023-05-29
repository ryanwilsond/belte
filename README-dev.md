# README for Developers

To view future plans, docs, etc:

Github: [github.com/ryanwilsond/belte](https://github.com/ryanwilsond/belte)

Docs/Pages: [ryanwilsond.github.io/belte](https://ryanwilsond.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

More onboarding resources and documentation exist on a per-request basis for
developers or for those who want to contribute.

## Tools for Building

This project uses the .NET SDK (8.0). To run the project, launch a debug or
release profile (currently only for Visual Studio Code). For publishing the
project, GNU Make is used.

Visual Studio Code is strongly recommended, but not required.

## Publishing Buckle

Run `$ make` to publish the project, and run `$ make test` to run the project's
tests.

The final executable is put into `.\bin\release\buckle.exe`.
