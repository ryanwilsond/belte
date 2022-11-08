using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

// TODO finish preprocessor
// * Needs to use the lexer, parser, and constant evaluator to evaluate statements like #run, #if, #elif, and #define

namespace Buckle;

internal abstract class PreprocessLine {
    internal TextLine text;

    internal PreprocessLine() { }
}

internal sealed class PreprocessIf : PreprocessLine {
    // Includes elif, else, and end
}

internal sealed class PreprocessPragma : PreprocessLine { }

internal sealed class PreprocessDefine : PreprocessLine { }

internal sealed class PreprocessUndefine : PreprocessLine { }

internal sealed class PreprocessWarning : PreprocessLine { }

internal sealed class PreprocessError : PreprocessLine { }

internal sealed class PreprocessRun : PreprocessLine { }

internal sealed class PreprocessSand : PreprocessLine { }

internal sealed class PreprocessFile {
    internal ImmutableArray<PreprocessLine> lines;

    private PreprocessFile() { }

    internal static PreprocessFile Parse(ImmutableArray<TextLine> lines) {
        var preprocessFile = new PreprocessFile();

        var builder = ImmutableArray.CreateBuilder<PreprocessLine>();
        preprocessFile.lines = builder.ToImmutable();

        // ! Temp code - just so it compiles
        return preprocessFile;
    }
}

internal class Preprocessor {
    private Dictionary<string, string> symbols = new Dictionary<string, string>();

    /// <summary>
    /// Preprocesses a file
    /// </summary>
    /// <param name="filename">Name of input file</param>
    /// <param name="text">Contents of file</param>
    /// <returns>Preprocessed text</returns>
    internal string PreprocessText(string filename, string text) {
        var sourceText = SourceText.From(text, filename);
        var lines = sourceText.lines;

        var preprocessFile = PreprocessFile.Parse(lines);

        foreach (var line in preprocessFile.lines) {

        }

        // ! Temp code - just so it compiles
        return text;
    }
}
