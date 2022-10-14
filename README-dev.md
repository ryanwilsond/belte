# Developing Environment

To view future plans, docs, etc:

Github: [github.com/flamechain/belte](https://github.com/flamechain/belte)

Docs/Pages: [flamechain.github.io/belte](https://flamechain.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

## Tools

This project uses the .NET SDK (6.0) for building, wrapped with GNU Make.

## Building Buckle

The final executable is put into `./buckle.exe`.

Run `$ make` to build the project, and run `$ make test` to run the unit tests.

If you are building for the first time, run `$ make setup` after you build to
copy some files to the root of the project.

## Building Sander

The final executable is put into `./sander.exe`.

For first time setup run `$ make debugsander` followed by `$ make sandersetup`.

Run `$ make sander` to build the project.
