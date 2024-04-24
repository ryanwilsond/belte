
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle;

/// <summary>
/// Exposed utilities used by the <see cref="Compiler"/>.
/// </summary>
public static class CompilerHelpers {
    /// <summary>
    /// Creates and returns the SyntaxTrees for loaded libraries.
    /// </summary>
    public static SyntaxTree[] LoadLibrarySyntaxTrees() {
        var assembly = Assembly.GetExecutingAssembly();
        var syntaxTrees = new List<SyntaxTree>();

        foreach (var libraryName in assembly.GetManifestResourceNames()) {
            if (libraryName.StartsWith("Compiler.Resources"))
                continue;

            using var stream = assembly.GetManifestResourceStream(libraryName);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd().TrimEnd();

            var syntaxTree = SyntaxTree.Load(libraryName, text);
            syntaxTrees.Add(syntaxTree);
        }

        return syntaxTrees.ToArray();
    }
}
