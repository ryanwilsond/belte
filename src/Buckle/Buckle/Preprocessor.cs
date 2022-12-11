using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

// * Needs to use the lexer, parser, and constant evaluator to evaluate statements like #run, #if, #elif, and #define

namespace Buckle;

/// <summary>
/// A line of text that is a preprocessor statement.
/// </summary>
internal abstract class PreprocessLine {
    /// <summary>
    /// Line of text as seen in the source code.
    /// </summary>
    internal TextLine text;

    /// <summary>
    /// Creates a PreprocessLine
    /// </summary>
    internal PreprocessLine() { }
}

/// <summary>
/// A preprocessor simple if statement to exclude specific code from compilation.
/// </summary>
internal sealed class PreprocessIf : PreprocessLine {
    // Includes elif, else, and end
}

/// <summary>
/// A preprocessor flag to indicate specific behavior to the compilation.
/// </summary>
internal sealed class PreprocessPragma : PreprocessLine { }

/// <summary>
/// Defines a preprocessor constant that is copy pasted throughout.
/// </summary>
internal sealed class PreprocessDefine : PreprocessLine { }

/// <summary>
/// Removes an existing preprocessor constant.
/// </summary>
internal sealed class PreprocessUndefine : PreprocessLine { }

/// <summary>
/// Unconditionally raises a warning to the compilation.
/// </summary>
internal sealed class PreprocessWarning : PreprocessLine { }

/// <summary>
/// Unconditionally raises an error to the compilation.
/// </summary>
internal sealed class PreprocessError : PreprocessLine { }

/// <summary>
/// Uses the evaluator to run code during or before compilation.
/// Not evaluated during runtime.
/// </summary>
internal sealed class PreprocessRun : PreprocessLine { }

/// <summary>
/// TBD.
/// </summary>
internal sealed class PreprocessSand : PreprocessLine { }

/// <summary>
/// Represents a single source file to be preprocesses.
/// </summary>
internal sealed class PreprocessFile {
    /// <summary>
    /// All preprocessor lines in a file.
    /// </summary>
    internal ImmutableArray<PreprocessLine> lines;

    private PreprocessFile() { }

    /// <summary>
    /// Parses a source file and returns its preprocessor statements.
    /// </summary>
    /// <param name="lines">Original source lines</param>
    /// <returns>All preprocessor lines in the source file</returns>
    internal static PreprocessFile Parse(ImmutableArray<TextLine> lines) {
        var preprocessFile = new PreprocessFile();

        var builder = ImmutableArray.CreateBuilder<PreprocessLine>();
        preprocessFile.lines = builder.ToImmutable();

        // ! Temp code - just so it compiles
        return preprocessFile;
    }
}

/// <summary>
/// Evaluates preprocessor statements
/// </summary>
internal class Preprocessor {
    private Dictionary<string, string> symbols = new Dictionary<string, string>();

    /// <summary>
    /// Preprocesses a file.
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
