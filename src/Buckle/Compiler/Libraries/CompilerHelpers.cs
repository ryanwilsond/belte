
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
    /// <summary>
    /// Creates and returns the SyntaxTrees for loaded libraries.
    /// </summary>
    public static Compilation LoadLibraries(CompilationOptions options) {
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

        var newOptions = new CompilationOptions(
            options.buildMode,
            options.arguments,
            options.isScript,
            options.enableOutput,
            true
        );

        return Compilation.Create(newOptions, syntaxTrees.ToArray());
    }
}
