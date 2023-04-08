
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A statement <see cref="SyntaxNode" />, a line of code that is its own idea.
/// Statements either end with a closing curly brace or semicolon.
/// </summary>
internal abstract class StatementSyntax : SyntaxNode {
    protected StatementSyntax(SyntaxTree syntaxTree) : base(syntaxTree) { }
}
