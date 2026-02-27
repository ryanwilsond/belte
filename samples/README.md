# Belte Code Samples

- [Samples List](#samples-list)
- [Running a Sample Using the Evaluator](#running-a-sample-using-the-evaluator)
- [Compiling a Sample](#compiling-a-sample)

## Samples List

Each sub-directory contains a single sample. In every sample, execution starts in the `Program.blt` file. (Note that
this is a convention, and not required.)

| Directory | Command Example | Description |
|-|-|-|
| [samples/HelloWorld](HelloWorld/Program.blt) | `buckle samples/HelloWorld` | Hello, world! program. |
| [samples/Echo](Echo/Program.blt) | `buckle samples/Echo -- arg1 arg2` | Echo program that prints command-line arguments. |
| [samples/GuessingGame](GuessingGame/Program.blt) | `buckle samples/GuessingGame` | Random number guessing game. |
| [samples/Pong](Pong/Program.blt) | `buckle samples/Pong --type=graphics` | 2D pong game. |
| [samples/Snake](Snake/Program.blt) | `buckle samples/Snake --type=graphics` | Snake game. |

## Running a Sample Using the Evaluator

To run a sample directly using the Buckle compiler, run `buckle <Path/to/sample>`.

E.g.

```bash
buckle samples/HelloWorld
```

Most of the samples are console projects, which is the default project type. For graphics projects
(like the Pong and Snake samples), you must also specify the project type to be `graphics` for all of the
graphics-related types to be properly loaded.

E.g.

```bash
buckle samples/Pong --type=graphics
```

## Compiling a Sample

> Note: using `dotnet build` or `dotnet run` commands builds the Buckle compiler before building the sample project,
> potentially slowing down build times

To compile a sample, locate into the desired sample directory and run `dotnet build`. This will place the finished
executable into `<sample directory>/bin/Debug/net10.0/<sample name>.exe`, which you can then run.

Alternatively, run `dotnet run` instead of the build command to automatically run the program after it is compiled.

E.g.

```bash
cd samples/HelloWorld
dotnet build
./bin/Debug/net10.0/HelloWorld.exe
```

or equivalently:

```bash
cd samples/HelloWorld
dotnet run
```
