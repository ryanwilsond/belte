
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A statement node, a line of code that is its own idea.
/// Statements either end with a closing curly brace or semicolon.
/// </summary>
internal abstract class Statement : Node {
    protected Statement(SyntaxTree syntaxTree): base(syntaxTree) { }
}
