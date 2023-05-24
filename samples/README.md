# Belte Samples

Reading docs can be daunting, so to help new users this directory has multiple
samples.

Each sub-directory contains a single sample. In every sample, execution starts
in `Program.blt`. (Note that this is a convention, and not required.)

To run a sample, read the [Running](#running) section.

> The following only applies if you are running the project using .NET

To replicate these samples somewhere else, you will need to copy both
[Directory.Build.props](Directory.Build.props) and
[Directory.Build.targets](Directory.Build.targets) into your project. You will
also need to copy the *msproj* file from the sample you are using or
[create](#creating-an-msproj-file) your own](#creating-an-msproj-file).

## Samples List

| Directory | Description |
|-|-|
| [samples/HelloWorld](HelloWorld/Program.blt) | Hello, world! program. |
| [samples/GuessingGame](GuessingGame/Program.blt) | Random number guessing game. |

## Running

To run a sample using .NET, run `dotnet run --project <Path/to/sample>` (e.g.
`dotnet run --project samples/HelloWorld/HelloWorld.msproj`).

To run a sample directly using the Buckle compiler, run
`buckle -i <Path/to/sample>`. When using the Buckle compiler to run the samples,
specifying each file is unnecessary, instead just specify the directory
(e.g. `buckle -i samples/HelloWorld`).

## Creating an *.msproj File

Name your file *\<Your project name\>.msproj* (e.g. *HelloWol.msproj*,
*Belte.msproj*).

Then paste in the following line:

```xml
<Project Sdk="Microsoft.NET.Sdk"></Project>
```

And you're done!

Because the `Directory.Build.props` contains all the required properties, no
properties are required in the `msproj` files.
