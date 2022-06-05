# Developing Environment

To view future plans, docs, etc:

Github: [github.com/flamechain/belte](https://github.com/flamechain/belte)

Docs/Pages: [flamechain.github.io/belte](https://flamechain.github.io/belte/)

Trello: [trello.com/belteindustries](https://trello.com/belteindustries)

## Tools

This project mainly uses the .NET SDK (5.0) for building (Buckle Silver), wrapped with GNU Make. Buckle Bronze uses GNU
gcc/g++ for building. Buckle Strap is a bootstrapping compiler, this uses the Buckle compiler to build.

## Building

The tools required for building are listed above.

All the following methods put the final executable into `./buckle.exe`.

### Buckle Bronze

To setup the environment for the first time run `$ make -f Makebronze.mk setup`. After that running
`$ make -f Makebronze.mk` should suffice.

### Buckle Silver

Similar with Buckle Bronze, for first time setup run `$ make setup`. Run `$ make` to build the project and copy into ./,
and run `$ make test` to build the test project and run the unit tests.

### Buckle Strap

Run `$ make -f Makestrap.mk`.
