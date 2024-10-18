
namespace Buckle;

/// <summary>
/// A type of compilation that will be performed, only one per compilation.
/// </summary>
public enum BuildMode : byte {
    /// Invokes the Repl.
    Repl,
    /// Runs the program after compilation, and automatically chooses either to interpret, evaluate, or execute.
    AutoRun,
    /// Runs the program after compilation by interpreting.
    Interpret,
    /// Runs the program after compilation by evaluating.
    Evaluate,
    /// Runs the program after compilation by executing.
    Execute,
    /// Compiles the program into a native executable.
    Independent,
    /// Transpiles the program into C# source.
    CSharpTranspile,
    /// Compiles with .NET integration which compiles into IL and assembles into a DLL.
    Dotnet,
}
