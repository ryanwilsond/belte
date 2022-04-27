using System;
using System.CodeDom.Compiler;

namespace Buckle.Generators;

internal class CurlyIndenter : IDisposable {
    private IndentedTextWriter indentedTextWriter_;

    public CurlyIndenter(IndentedTextWriter indentedTextWriter, string openingLine = "") {
        indentedTextWriter_ = indentedTextWriter;

        if (!string.IsNullOrWhiteSpace(openingLine))
            indentedTextWriter.Write($"{openingLine} ");

        indentedTextWriter.WriteLine("{");
        indentedTextWriter.Indent++;
    }

    public void Dispose() {
        indentedTextWriter_.Indent--;
        indentedTextWriter_.WriteLine("}");
    }
}
