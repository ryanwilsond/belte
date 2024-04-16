# Belte Code Samples

- [Samples List](#samples-list)
- [Running a Sample Using the Interpreter](#running-a-sample-using-the-interpreter)
<!--
- [Running a Sample Using .NET](#running-a-sample-using-net)
-->

## Samples List

Each sub-directory contains a single sample. In every sample, execution starts in the `Program.blt` file. (Note that
this is a convention, and not required.)

| Directory | Description |
|-|-|
| [samples/HelloWorld](HelloWorld/Program.blt) | Hello, world! program. |
| [samples/Echo](Echo/Program.blt) | Echo program that prints command-line arguments. |
| [samples/GuessingGame](GuessingGame/Program.blt) | Random number guessing game. |
| [samples/Pong](Pong/Program.blt) | Retro pong clone. |

## Running a Sample Using the Interpreter

To run a sample directly using the Buckle compiler, run `buckle <Path/to/sample>`.

E.g.

```bash
buckle samples/HelloWorld
```

<!--
## Running a Sample Using .NET

To run a sample using .NET, run `dotnet run --project <Path/to/sample>`.

E.g.

```bash
dotnet run --project samples/HelloWorld/HelloWorld.msproj
```
-->
