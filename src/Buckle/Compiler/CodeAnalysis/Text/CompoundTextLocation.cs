using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Text;

internal sealed class CompoundTextLocation : TextLocation {
    private readonly TextLocation _primary;
    private readonly SyntaxNode _secondary;

    internal CompoundTextLocation(TextLocation primaryLocation, SyntaxNode secondaryLocation) {
        _primary = primaryLocation;
        _secondary = secondaryLocation;
    }

    public override SourceText text => _primary.text;

    public override TextSpan span => _primary.span;

    public override SyntaxTree tree => _primary.tree;

    internal TextLocation secondaryLocation => _secondary.location;

    internal SyntaxNode sourceNode => _secondary;
}
