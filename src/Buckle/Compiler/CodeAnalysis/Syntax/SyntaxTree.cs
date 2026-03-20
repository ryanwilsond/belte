using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Buckle.CodeAnalysis.Syntax.InternalSyntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Tree of SyntaxNodes produced from the <see cref="LanguageParser" />.
/// </summary>
public partial class SyntaxTree {
    internal static readonly SyntaxTree Dummy = new DummySyntaxTree();

    internal SyntaxTree(SourceText text, SourceCodeKind kind) {
        this.kind = kind;
        this.text = text;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxTree" /> with the given node as the root.
    /// </summary>
    internal static SyntaxTree Create(SourceText text, BelteSyntaxNode root) {
        return new ParsedSyntaxTree(text, root, true, SourceCodeKind.Regular);
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxTree" /> with the given node as the root, but does not assign the
    /// given node's syntax tree.
    /// </summary>
    internal static SyntaxTree CreateWithoutClone(BelteSyntaxNode root) {
        return new ParsedSyntaxTree(null, root, false, SourceCodeKind.Regular);
    }

    /// <summary>
    /// <see cref="SourceText" /> the <see cref="SyntaxTree" /> was created from.
    /// </summary>
    public SourceText text { get; }

    /// <summary>
    /// The type of source.
    /// </summary>
    public SourceCodeKind kind { get; }

    /// <summary>
    /// EOF <see cref="SyntaxToken" />. Marks the end of the <see cref="SourceText" />, and does not map to an actual
    /// character in the <see cref="SourceText" />.
    /// </summary>
    internal virtual SyntaxToken endOfFile => null;

    /// <summary>
    /// The length of the <see cref="SourceText" />.
    /// </summary>
    private protected virtual int _length => text.length;

    /// <summary>
    /// Parses text (not necessarily related to a source file).
    /// </summary>
    /// <param name="text">Text to generate <see cref="SyntaxTree" /> from.</param>
    /// <returns>Parsed result as <see cref="SyntaxTree" />.</returns>
    public static SyntaxTree Parse(string text, SourceCodeKind kind = SourceCodeKind.Regular) {
        var sourceText = SourceText.From(text);
        return Parse(sourceText, kind);
    }

    public override string ToString() {
        return text.ToString();
    }

    /// <summary>
    /// Gets the root node of the syntax tree.
    /// </summary>
    public virtual BelteSyntaxNode GetRoot() => null;

    /// <summary>
    /// Gets the root node of the syntax tree as a <see cref="CompilationUnitSyntax" />.
    /// </summary>
    public CompilationUnitSyntax GetCompilationUnitRoot() {
        return (CompilationUnitSyntax)GetRoot();
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
    internal static SyntaxTree Parse(SourceText text, SourceCodeKind kind = SourceCodeKind.Regular) {
        var lexer = new Lexer(text, kind == SourceCodeKind.Regular);
        var parser = new LanguageParser(lexer);
        var compilationUnit = (CompilationUnitSyntax)parser.ParseCompilationUnit().CreateRed();
        var parsedTree = new ParsedSyntaxTree(text, compilationUnit, true, kind);
        return parsedTree;
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

    /// <summary>
    /// Gets all diagnostics on the tree, if the tree contains a root.
    /// </summary>
    internal BelteDiagnosticQueue GetDiagnostics() {
        var root = GetRoot();

        if (root is not null)
            return GetDiagnostics(root);
        else
            return new BelteDiagnosticQueue();
    }

    /// <summary>
    /// Gets all diagnostics on a node.
    /// </summary>
    internal BelteDiagnosticQueue GetDiagnostics(SyntaxNode node) {
        return GetDiagnostics(node.green, node.position);
    }

    /// <summary>
    /// Gets all diagnostics on a green node and gives them an absolute position.
    /// </summary>
    internal BelteDiagnosticQueue GetDiagnostics(GreenNode green, int position) {
        if (green.containsDiagnostics)
            return new BelteDiagnosticQueue(EnumerateDiagnostics(green, position));

        return new BelteDiagnosticQueue();
    }

    private protected T CloneNodeAsRoot<T>(T node) where T : BelteSyntaxNode {
        return SyntaxNode.CloneNodeAsRoot(node, this);
    }

    private IEnumerable<BelteDiagnostic> EnumerateDiagnostics(GreenNode node, int position) {
        var enumerator = new SyntaxTreeDiagnosticEnumerator(this, node, position);

        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }

    private SyntaxTree WithChanges(SourceText newText, ImmutableArray<TextChangeRange> changes) {
        ImmutableArray<TextChangeRange>? workingChanges = changes;
        var oldTree = this;

        if (workingChanges?.Length == 1 &&
            workingChanges?[0].span == new TextSpan(0, _length) &&
            workingChanges?[0].newLength == newText.length) {
            workingChanges = null;
            oldTree = null;
        }

        var lexer = new Lexer(newText, kind == SourceCodeKind.Regular);
        var parser = new LanguageParser(lexer, oldTree?.GetRoot(), workingChanges);

        var compilationUnit = (CompilationUnitSyntax)parser.ParseCompilationUnit().CreateRed();
        var parsedTree = new ParsedSyntaxTree(newText, compilationUnit, true, kind);
        return parsedTree;
    }
}
