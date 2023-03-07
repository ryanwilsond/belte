# README for Developers

To view future plans, docs, etc:

Github: [github.com/ryanwilsond/belte](https://github.com/ryanwilsond/belte)

Docs/Pages: [ryanwilsond.github.io/belte](https://ryanwilsond.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

OnBoarding:
[Belte Industries OnBoarding Presentation](https://docs.google.com/presentation/d/1OPQQ2u9eYoLJ0EJMaahhTUQPkZ3FQ6KigO9uWFbu9zQ/edit?usp=sharing)

## Tools for Building

This project uses the .NET SDK (7.0) for building, wrapped with GNU Make. You
can also run the project without GNU Make by launching a debug profile (
currently only for Visual Studio Code).

Visual Studio Code is strongly recommended, but not required.

## Building Buckle

Run `$ make` to build the project, and run `$ make test` to run the project's
tests.

The final executable is put into `./buckle.exe`.

If you are using Visual Studio Code, a debug task is provided and can be
launched by pressing F5.

## Building Sander

Run `$ make sander` to build the project.

The final executable is put into `./sander.exe`.
