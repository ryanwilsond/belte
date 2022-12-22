using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

// * Needs to use the lexer, parser, and constant evaluator to evaluate statements like #run, #if, #elif, and #define

namespace Buckle.CodeAnalysis.Preprocessing;

/// <summary>
/// Evaluates preprocessor statements
/// </summary>
internal sealed class Preprocessor {
    private Dictionary<string, string> symbols = new Dictionary<string, string>();

    internal Preprocessor() {
        diagnostics = new BelteDiagnosticQueue();
    }

    /// <summary>
    /// Preprocesses a file.
    /// </summary>
    /// <param name="filename">Name of input file.</param>
    /// <param name="text">Contents of file.</param>
    /// <returns>Preprocessed text.</returns>
    internal string PreprocessText(string filename, string text) {
        var sourceText = SourceText.From(text, filename);
        var lines = sourceText.lines;

        var preprocessFile = PreprocessFile.Parse(lines);

        foreach (var line in preprocessFile.lines) {

        }

        // ! Temp code - just so it compiles
        return text;
    }

    internal BelteDiagnosticQueue diagnostics;
}
