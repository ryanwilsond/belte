using System.Collections.Immutable;
using System.IO;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Tree of SyntaxNodes produced from the <see cref="InternalSyntax.Parser" />.
/// </summary>
public sealed class SyntaxTree {
    private SyntaxTree(SourceText text) {
        // Responsibility of settings the root is put on the caller
        this.text = text;
        diagnostics = new BelteDiagnosticQueue();
    }

    internal SyntaxTree(SourceText text, ParseHandler handler) : this(text) {
        handler(this, out var _root, out diagnostics);
        root = _root;
    }

    internal delegate void ParseHandler(
        SyntaxTree syntaxTree, out CompilationUnitSyntax root, out BelteDiagnosticQueue diagnostics
    );

    /// <summary>
    /// Root <see cref="SyntaxNode" />, does not represent something in a source file rather the entire source file.
    /// </summary>
    public CompilationUnitSyntax root { get; private set; }

    /// <summary>
    /// <see cref="SourceText" /> the <see cref="SyntaxTree" /> was created from.
    /// </summary>
    public SourceText text { get; }

    /// <summary>
    /// EOF <see cref="SyntaxToken" />.
    /// </summary>
    internal SyntaxToken endOfFile { get; }

    /// <summary>
    /// Diagnostics relating to <see cref="SyntaxTree" />.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics;

    private int length => root?.fullSpan?.length ?? text.length;

    /// <summary>
    /// Parses text (not necessarily related to a source file).
    /// </summary>
    /// <param name="text">Text to generate <see cref="SyntaxTree" /> from.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    public static SyntaxTree Parse(string text) {
        var sourceText = SourceText.From(text);

        return Parse(sourceText);
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxTree" /> with the given changes.
    /// </summary>
    public SyntaxTree WithChanges(params TextChange[] changes) {
        var newText = text.WithChanges(changes);
        return WithChangedText(newText);
    }

    /// <summary>
    /// Create a <see cref="SyntaxTree" /> from a source file.
    /// </summary>
    /// <param name="fileName">File name of source file.</param>
    /// <param name="text">Content of source file.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    internal static SyntaxTree Load(string fileName, string text) {
        var sourceText = SourceText.From(text, fileName);

        return Parse(sourceText);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxTree" /> from a source file, uses file name to open and read the file directly.
    /// </summary>
    /// <param name="fileName">File name of source file.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    internal static SyntaxTree Load(string fileName) {
        var text = File.ReadAllText(fileName);
        var sourceText = SourceText.From(text, fileName);

        return Parse(sourceText);
    }

    /// <summary>
    /// Parses <see cref="SourceText" />.
    /// </summary>
    /// <param name="text">Text to generate <see cref="SyntaxTree" /> from.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    internal static SyntaxTree Parse(SourceText text) {
        return new SyntaxTree(text, Parse);
    }

    /// <summary>
    /// Creates a new syntax based off this tree using a new source text.
    /// </summary>
    internal SyntaxTree WithChangedText(SourceText newText) {
        var changes = newText.GetChangeRanges(text);

        if (changes.Length == 0 && text == newText)
            return this;

        return WithChanges(newText, changes);
    }

    private SyntaxTree WithChanges(SourceText newText, ImmutableArray<TextChangeRange> changes) {
        ImmutableArray<TextChangeRange>? workingChanges = changes;
        var oldTree = this;

        if (workingChanges?.Length == 1 &&
            workingChanges?[0].span == new TextSpan(0, length) &&
            workingChanges?[0].newLength == newText.length) {
            workingChanges = null;
            oldTree = null;
        }

        var tree = new SyntaxTree(newText);
        var parser = new InternalSyntax.Parser(tree, oldTree?.root, workingChanges);
        tree.root = parser.ParseCompilationUnit().CreateRed();
        tree.diagnostics.Move(parser.diagnostics);

        return tree;
    }

    private static void Parse(
        SyntaxTree syntaxTree, out CompilationUnitSyntax root, out BelteDiagnosticQueue diagnostics) {
        var parser = new InternalSyntax.Parser(syntaxTree);
        root = parser.ParseCompilationUnit().CreateRed();
        diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(parser.diagnostics);
    }
}
