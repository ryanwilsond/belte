# Developing Environment

To view future plans, docs, etc:

Github: [github.com/flamechain/belte](https://github.com/flamechain/belte)

Docs/Pages: [flamechain.github.io/belte](https://flamechain.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

OnBoarding:
[Belte Industries OnBoarding Presentation](https://docs.google.com/presentation/d/1OPQQ2u9eYoLJ0EJMaahhTUQPkZ3FQ6KigO9uWFbu9zQ/edit?usp=sharing)

## Tools

This project uses the .NET SDK (5.0) for building, wrapped with GNU Make.

## Building Buckle

The final executable is put into `./buckle.exe`.

For first time setup run `$ make debugbuild` followed by `$ make setup`.

Run `$ make` to build the project, and run `$ make test` to run the unit tests.

## Building Sander

The final executable is put into `./sander.exe`.

For first time setup run `$ make debugsander` followed by `$ make sandersetup`.

Run `$ make sander` to build the project.
