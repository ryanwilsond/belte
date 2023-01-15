
namespace Buckle;

/// <summary>
/// A type of compilation that will be performed, only one per compilation.
/// </summary>
public enum BuildMode {
    Repl,
    Interpret,
    Independent,
    CSharpTranspile,
    Dotnet,
}
