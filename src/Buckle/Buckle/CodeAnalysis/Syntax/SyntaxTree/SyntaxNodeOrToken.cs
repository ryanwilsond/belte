using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

public sealed class SyntaxNodeOrToken {
    private readonly SyntaxNode _nodeOrParent;
    private readonly GreenNode _token;
    private readonly int _tokenIndex;

    internal SyntaxNodeOrToken(SyntaxNode node) {
        position = node.position;
        _nodeOrParent = node;
        _tokenIndex = -1;
    }

    internal SyntaxNodeOrToken(SyntaxNode parent, GreenNode token, int position, int index) {
        this.position = position;
        _tokenIndex = index;
        _nodeOrParent = parent;
        _token = token;
    }

    /// <summary>
    /// The underlying <see cref="SyntaxNode" /> or parent of the token.
    /// </summary>
    internal SyntaxNode parent => _token != null ? _nodeOrParent : _nodeOrParent?.parent;

    /// <summary>
    /// The underlying <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode underlyingNode => _token ?? _nodeOrParent.green;

    /// <summary>
    /// The position of the <see cref="SyntaxNode" />.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// If this <see cref="SyntaxNodeOrToken" /> is wrapping a <see cref="SyntaxToken" />.
    /// </summary>
    internal bool IsToken => !IsNode;

    /// <summary>
    /// If this <see cref="SyntaxNodeOrToken" /> is wrapping a <see cref="SyntaxNode" />.
    /// </summary>
    internal bool IsNode => _tokenIndex < 0;

    /// <summary>
    /// Returns the underlying <see cref="SyntaxToken" /> if this <see cref="SyntaxNodeOrToken" /> is
    /// wrapping a <see cref="SyntaxToken" />.
    /// </summary>
    internal SyntaxToken AsToken() {
        if (_token != null)
            return new SyntaxToken(_nodeOrParent, _token, position, _tokenIndex);

        return null;
    }

    internal bool AsToken(out SyntaxToken token) {
        if (IsToken) {
            token = AsToken();
            return token is object;
        }

        token = null;
        return false;
    }

    /// <summary>
    /// Returns the underlying <see cref="SyntaxNode" /> if this <see cref="SyntaxNodeOrToken" /> is
    /// wrapping a <see cref="SyntaxNode" />.
    /// </summary>
    internal SyntaxNode AsNode() {
        if (_token != null)
            return null;

        return _nodeOrParent;
    }

    internal bool AsNode(out SyntaxNode node) {
        if (IsNode) {
            node = _nodeOrParent;
            return node is object;
        }

        node = null;
        return false;
    }

    /// <summary>
    /// All child nodes and tokens of this node or token.
    /// </summary>
    internal ChildSyntaxList ChildNodesAndTokens() {
        if (AsNode(out var node))
            return node.ChildNodesAndTokens();

        return null;
    }

    /// <summary>
    /// The span of the underlying node.
    /// </summary>
    internal TextSpan span {
        get {
            if (_token != null)
                return AsToken().span;

            if (_nodeOrParent != null)
                return _nodeOrParent.span;

            return null;
        }
    }

    /// <summary>
    /// The full span of the underlying node.
    /// </summary>
    internal TextSpan fullSpan {
        get {
            if (_token != null)
                return new TextSpan(position, _token.fullWidth);

            if (_nodeOrParent != null)
                return _nodeOrParent.fullSpan;

            return null;
        }
    }
}
