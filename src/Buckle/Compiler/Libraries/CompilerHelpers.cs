using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle;

/// <summary>
/// Exposed utilities used by the <see cref="Compiler"/>.
/// </summary>
public static class CompilerHelpers {
    internal static readonly CompilationOptions LibraryOptions
        = new CompilationOptions(BuildMode.None, OutputKind.Library);

    /// <summary>
    /// Creates a compilation containing all of the built-in libraries.
    /// </summary>
    public static Compilation LoadLibraries() {
        var assembly = Assembly.GetExecutingAssembly();
        var syntaxTrees = new List<SyntaxTree>();

        foreach (var libraryName in assembly.GetManifestResourceNames()) {
            if (libraryName.StartsWith("Compiler.Resources"))
                continue;

            // TODO Remove this, temp
            if (libraryName != "Compiler.Object.blt")
                continue;

            using var stream = assembly.GetManifestResourceStream(libraryName);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd().TrimEnd();

            var syntaxTree = SyntaxTree.Load(libraryName, text);
            syntaxTrees.Add(syntaxTree);
        }

        var corLibrary = Compilation.Create("CorLibrary", LibraryOptions, syntaxTrees.ToArray());
        corLibrary.GetDiagnostics();

        return corLibrary;
    }
}
