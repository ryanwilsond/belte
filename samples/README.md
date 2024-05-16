# Belte Code Samples

- [Samples List](#samples-list)
- [Running a Sample Using the Interpreter](#running-a-sample-using-the-interpreter)

## Samples List

Each sub-directory contains a single sample. In every sample, execution starts in the `Program.blt` file. (Note that
this is a convention, and not required.)

| Directory | Command Example | Description |
|-|-|-|
| [samples/HelloWorld](HelloWorld/Program.blt) | `buckle samples/HelloWorld` | Hello, world! program. |
| [samples/Echo](Echo/Program.blt) | `buckle samples/Echo -- arg1 arg2` | Echo program that prints command-line arguments. |
| [samples/GuessingGame](GuessingGame/Program.blt) | `buckle samples/GuessingGame` | Random number guessing game. |

## Running a Sample Using the Interpreter

To run a sample directly using the Buckle compiler, run `buckle <Path/to/sample>`.

E.g.

```bash
buckle samples/HelloWorld
```
