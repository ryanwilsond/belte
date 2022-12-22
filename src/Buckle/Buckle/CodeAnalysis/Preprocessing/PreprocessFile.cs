using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Preprocessing;

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
    /// <param name="lines">Original source lines.</param>
    /// <returns>All preprocessor lines in the source file.</returns>
    internal static PreprocessFile Parse(ImmutableArray<TextLine> lines) {
        var preprocessFile = new PreprocessFile();

        var builder = ImmutableArray.CreateBuilder<PreprocessLine>();
        preprocessFile.lines = builder.ToImmutable();

        // ! Temp code - just so it compiles
        return preprocessFile;
    }
}
