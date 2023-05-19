using System;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A wrapper of either a <see cref="SyntaxNode" /> or <see cref="SyntaxToken" />.
/// </summary>
public sealed class SyntaxNodeOrToken : IEquatable<SyntaxNodeOrToken> {
    private readonly SyntaxNode _nodeOrParent;
    private readonly GreenNode _token;
    private readonly int _tokenIndex;

    /// <summary>
    /// Creates a new <see cref="SyntaxNodeOrToken" /> wrapping a <see cref="SyntaxNode" />.
    /// </summary>
    internal SyntaxNodeOrToken(SyntaxNode node) {
        position = node.position;
        _nodeOrParent = node;
        _tokenIndex = -1;
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxNodeOrToken" /> wrapping a <see cref="SyntaxToken" />.
    /// </summary>
    internal SyntaxNodeOrToken(SyntaxNode parent, GreenNode token, int position, int index) {
        this.position = position;
        _tokenIndex = index;
        _nodeOrParent = parent;
        _token = token;
    }

    /// <summary>
    /// The kind of the underlying token or node.
    /// </summary>
    public SyntaxKind kind => _token?.kind ?? _nodeOrParent?.kind ?? SyntaxKind.None;

    /// <summary>
    /// The underlying <see cref="SyntaxNode" /> or parent of the token.
    /// </summary>
    public SyntaxNode parent => _token != null ? _nodeOrParent : _nodeOrParent?.parent;

    /// <summary>
    /// The position of the <see cref="SyntaxNode" />.
    /// </summary>
    public int position { get; }

    /// <summary>
    /// If this <see cref="SyntaxNodeOrToken" /> is wrapping a <see cref="SyntaxToken" />.
    /// </summary>
    public bool isToken => !isNode;

    /// <summary>
    /// If this <see cref="SyntaxNodeOrToken" /> is wrapping a <see cref="SyntaxNode" />.
    /// </summary>
    public bool isNode => _tokenIndex < 0;

    /// <summary>
    /// The full width of the token or underlying node.
    /// </summary>
    public int fullWidth => _token?.fullWidth ?? _nodeOrParent?.fullWidth ?? 0;

    /// <summary>
    /// The width of the token or underlying node.
    /// </summary>
    public int width => _token?.width ?? _nodeOrParent?.width ?? 0;

    /// <summary>
    /// The ending position of the token or the underlying node.
    /// </summary>
    public int endPosition => position + fullWidth;

    /// <summary>
    /// If the underlying token or node contains diagnostics.
    /// </summary>
    public bool containsDiagnostics => _token?.containsDiagnostics ?? _nodeOrParent?.containsDiagnostics ?? false;

    /// <summary>
    /// The span of the underlying node.
    /// </summary>
    public TextSpan span {
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
    public TextSpan fullSpan {
        get {
            if (_token != null)
                return new TextSpan(position, _token.fullWidth);

            if (_nodeOrParent != null)
                return _nodeOrParent.fullSpan;

            return null;
        }
    }

    /// <summary>
    /// The underlying <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode underlyingNode => _token ?? _nodeOrParent.green;

    /// <summary>
    /// Returns the underlying <see cref="SyntaxToken" /> if this <see cref="SyntaxNodeOrToken" /> is
    /// wrapping a <see cref="SyntaxToken" />.
    /// </summary>
    public SyntaxToken AsToken() {
        if (_token != null)
            return new SyntaxToken(_nodeOrParent, _token, position, _tokenIndex);

        return null;
    }

    /// <summary>
    /// Outputs the underlying <see cref="SyntaxToken" /> if this <see cref="SyntaxNodeOrToken" /> is
    /// wrapping a <see cref="SyntaxToken" />.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the wrapped contents were successfully outputted as a <see cref="SyntaxToken" />,
    /// otherwise <c>false</c>.
    /// </returns>
    public bool AsToken(out SyntaxToken token) {
        if (isToken) {
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
    public SyntaxNode AsNode() {
        if (_token != null)
            return null;

        return _nodeOrParent;
    }

    /// <summary>
    /// Outputs the underlying <see cref="SyntaxNode" /> if this <see cref="SyntaxNodeOrToken" /> is
    /// wrapping a <see cref="SyntaxNode" />.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the wrapped contents were successfully outputted as a <see cref="SyntaxNode" />,
    /// otherwise <c>false</c>.
    /// </returns>
    public bool AsNode(out SyntaxNode node) {
        if (isNode) {
            node = _nodeOrParent;
            return node is object;
        }

        node = null;
        return false;
    }

    /// <summary>
    /// All child nodes and tokens of this node or token.
    /// </summary>
    public ChildSyntaxList ChildNodesAndTokens() {
        if (AsNode(out var node))
            return node.ChildNodesAndTokens();

        return null;
    }

    /// <summary>
    /// Finds the index of the first child whose span contains the given position.
    /// </summary>
    internal static int GetFirstChildIndexSpanningPosition(ChildSyntaxList list, int position) {
        var lo = 0;
        var hi = list.Count - 1;

        while (lo <= hi) {
            var r = lo + ((hi - lo) >> 1);
            var m = list[r];

            if (position < m.position) {
                hi = r - 1;
            } else {
                if (position == m.position) {
                    for (; r > 0 && list[r - 1].fullWidth == 0; r--)
                        ;

                    return r;
                }

                if (position >= m.endPosition) {
                    lo = r + 1;
                    continue;
                }

                return r;
            }
        }

        throw ExceptionUtilities.Unreachable();
    }

    public static implicit operator SyntaxNodeOrToken(SyntaxToken token) {
        return new SyntaxNodeOrToken(token.parent, token.node, token.position, token.index);
    }

    public static implicit operator SyntaxNodeOrToken(SyntaxNode node) {
        return node is object
            ? new SyntaxNodeOrToken(node)
            : null;
    }

    public bool Equals(SyntaxNodeOrToken other) {
        return _nodeOrParent == other?._nodeOrParent &&
               _token == other?._token &&
               _tokenIndex == other?._tokenIndex;
    }

    public static bool operator ==(SyntaxNodeOrToken left, SyntaxNodeOrToken right) {
        return left.Equals(right);
    }

    public static bool operator !=(SyntaxNodeOrToken left, SyntaxNodeOrToken right) {
        return !left.Equals(right);
    }

    public override bool Equals(object? obj) {
        return obj is SyntaxNodeOrToken token && Equals(token);
    }

    public override int GetHashCode() {
        return HashCode.Combine(_nodeOrParent, HashCode.Combine(_token, _tokenIndex));
    }
}
