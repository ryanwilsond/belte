using System;
using System.CodeDom.Compiler;

namespace Buckle.Generators.Utilities;

/// <summary>
/// Keeps track of a new curly brace enclosed scope.
/// </summary>
public sealed class CurlyIndenter : IDisposable {
    private IndentedTextWriter _indentedTextWriter;
    private bool _includeSemicolon;

    /// <summary>
    /// Creates a new scope using curly braces.
    /// </summary>
    /// <param name="indentedTextWriter">Out to use.</param>
    /// <param name="openingLine">What to put on the opening curly brace line.</param>
    public CurlyIndenter(
        IndentedTextWriter indentedTextWriter,
        string openingLine = "",
        bool includeSemicolon = false,
        bool sameLine = false) {
        _indentedTextWriter = indentedTextWriter;
        _includeSemicolon = includeSemicolon;

        if (!string.IsNullOrWhiteSpace(openingLine))
            indentedTextWriter.Write($"{openingLine} ");

        if (sameLine)
            indentedTextWriter.Write("{ ");
        else
            indentedTextWriter.WriteLine("{");

        indentedTextWriter.Indent++;
    }

    /// <summary>
    /// Exits scope.
    /// </summary>
    public void Dispose() {
        _indentedTextWriter.Indent--;
        _indentedTextWriter.WriteLine(_includeSemicolon ? "};" : "}");
    }
}
