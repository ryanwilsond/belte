# Developing Environment

To view future plans, docs, etc:

Github: [github.com/flamechain/belte](https://github.com/flamechain/belte)

Docs/Pages: [flamechain.github.io/belte](https://flamechain.github.io/belte/)

Trello: [trello.com/b/iq8JUTTa/belte](https://trello.com/b/iq8JUTTa)

## Tools

This project mainly uses the .NET SDK (5.0) for building (Buckle Silver), wrapped with GNU Make. Buckle Bronze uses GNU gcc/g++ for building. Buckle Strap uses the Buckle compiler.

## Building

The tools required for building are listed above.

### Buckle Bronze

To setup the environment for the first time run `$ make -f Makebronze.mk setup`. After that running `$ make -f Makebronze.mk` should suffice.

### Buckle Silver

Since dotnet handles directories, there is no setup. Run `$ make` to build the project and copy into ./ and run `$ make test` to build the test project and run the unit tests.

### Buckle Strap

Run `$ make -f Makestrap.mk`.
