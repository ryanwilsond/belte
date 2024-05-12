# Belte Code Samples

- [Samples List](#samples-list)
- [Running a Sample Using the Interpreter](#running-a-sample-using-the-interpreter)
<!--
- [Running a Sample Using .NET](#running-a-sample-using-net)
-->

## Samples List

Each sub-directory contains a single sample. In every sample, execution starts in the `Program.blt` file. (Note that
this is a convention, and not required.)

| Directory | Command Example | Description |
|-|-|-|
| [samples/HelloWorld](HelloWorld/Program.blt) | `buckle samples/HelloWorld` | Hello, world! program. |
| [samples/Echo](Echo/Program.blt) | `buckle samples/Echo -- arg1 arg2` | Echo program that prints command-line arguments. |
| [samples/GuessingGame](GuessingGame/Program.blt) | `buckle samples/GuessingGame` | Random number guessing game. |
| [samples/Donut](Donut/Program.blt) | `buckle samples/Donut` | Spinning ASCII torus. |
| [samples/Cube](Cube/Program.blt) | `buckle samples/Cube` | Spinning ASCII cube. |
<!-- | [samples/Pong](Pong/Program.blt) | `buckle samples/Pong --type=graphics` | Retro pong clone. | -->

## Running a Sample Using the Interpreter

To run a sample directly using the Buckle compiler, run `buckle <Path/to/sample>`.

E.g.

```bash
buckle samples/HelloWorld
```

<!--
Some samples require an additional flag, `--type=graphics`. Currently, only the Pong sample requires this:

```bash
buckle samples/Pong --type=graphics
```
-->

<!--
## Running a Sample Using .NET

To run a sample using .NET, run `dotnet run --project <Path/to/sample>`.

E.g.

```bash
dotnet run --project samples/HelloWorld/HelloWorld.msproj
```
-->
