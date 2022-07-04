# Developing Environment

To view future plans, docs, etc:

Github: [github.com/flamechain/belte](https://github.com/flamechain/belte)

Docs/Pages: [flamechain.github.io/belte](https://flamechain.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

## Tools

This project uses the .NET SDK (5.0) for building, wrapped with GNU Make.

## Building

The tools required for building are listed above.

All the following methods put the final executable into `./buckle.exe`.

For first time setup run `$ make setup`. Run `$ make` to build the project, copying the output executable into ./,
and run `$ make test` to build the test project and run the unit tests.
