using System;
using System.CodeDom.Compiler;

namespace Buckle.Generators;

/// <summary>
/// Keeps track of a new curly brace enclosed scope.
/// </summary>
internal class CurlyIndenter : IDisposable {
    private IndentedTextWriter indentedTextWriter_;

    /// <summary>
    /// Creates a new scope using curly braces.
    /// </summary>
    /// <param name="indentedTextWriter">Out to use</param>
    /// <param name="openingLine">What to put on the opening curly brace line</param>
    internal CurlyIndenter(IndentedTextWriter indentedTextWriter, string openingLine = "") {
        indentedTextWriter_ = indentedTextWriter;

        if (!string.IsNullOrWhiteSpace(openingLine))
            indentedTextWriter.Write($"{openingLine} ");

        indentedTextWriter.WriteLine("{");
        indentedTextWriter.Indent++;
    }

    /// <summary>
    /// Exits scope.
    /// </summary>
    public void Dispose() {
        indentedTextWriter_.Indent--;
        indentedTextWriter_.WriteLine("}");
    }
}
