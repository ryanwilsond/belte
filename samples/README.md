# Belte Code Samples

- [Samples List](#samples-list)
- [Running a Sample Using the Executor or Evaluator](#running-a-sample-using-the-executor-or-evaluator)
- [Compiling a Sample](#compiling-a-sample)

## Samples List

| Directory | Command to Run | Description |
|-|-|-|
| [samples/HelloWorld](HelloWorld/Program.blt) | `buckle samples/HelloWorld` | Hello, world! program. |
| [samples/Echo](Echo/Program.blt) | `buckle samples/Echo -- arg1 arg2` | Echo program that prints command-line arguments. |
| [samples/GuessingGame](GuessingGame/Program.blt) | `buckle samples/GuessingGame` | Random number guessing game. |
| [samples/Xor](Xor/Program.blt) | `buckle samples/Xor` | Findings missing numbers from list using xor. |
| [samples/Donut](Donut/Program.blt) | `buckle samples/Donut` | ASCII spinning donut. |
| [samples/Pong](Pong/Program.blt) | `buckle samples/Pong --type=graphics` | 2D pong game. |
| [samples/Snake](Snake/Program.blt) | `buckle samples/Snake --type=graphics` | Snake game. |
| [samples/Win32](Win32/Program.blt) | `buckle samples/Win32` | Win32 api window. |
| [samples/Socket](Socket/Program.blt) | `buckle samples/Socket` | Simple socket connection. |

## Running a Sample Using the Executor or Evaluator

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

If something goes wrong, the Evaluator provides much better debug information, at the cost of much worse performance
(which is why the Executor is the default end point). To use the Evaluator, add the `--evaluate` flag.

## Compiling a Sample

Use the `-d` option to output a .NET dll that is ready to run. You can optionally specify an output path for the dll
with `-o <path/to/dll>`. Run the dll using `dotnet <path/to/dll>`.

E.g.

```bash
buckle samples/Donut -d -o donut.dll
dotnet donut.dll
```

### Building using `dotnet build`

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
