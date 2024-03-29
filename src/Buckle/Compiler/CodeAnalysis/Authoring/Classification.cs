using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Authoring;

/// <summary>
/// Simplified classification of a <see cref="SyntaxToken" />.
/// </summary>
public enum Classification {
    Identifier,
    Keyword,
    Type,
    Number,
    String,
    Comment,
    Text,
    Line,
    Indent,
    Escape,
    RedNode,
    GreenNode,
    BlueNode,
}
