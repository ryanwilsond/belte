
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Expression, not a full line of code and most expressions can be interchanged with most other expressions.
/// </summary>
internal abstract class Expression : Node {
    protected Expression(SyntaxTree syntaxTree) : base(syntaxTree) { }
}
