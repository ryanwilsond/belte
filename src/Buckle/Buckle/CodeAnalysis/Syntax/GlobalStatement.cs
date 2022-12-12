
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A global statement.
/// </summary>
internal sealed partial class GlobalStatement : Member {
    /// <summary>
    /// Creates a global statement.
    /// </summary>
    /// <param name="syntaxTree">Syntax tree this node resides in</param>
    /// <param name="statement">Statement</param>
    /// <returns></returns>
    internal GlobalStatement(SyntaxTree syntaxTree, Statement statement) : base(syntaxTree) {
        this.statement = statement;
    }

    /// <summary>
    /// Statement (should ignore that fact that it is global).
    /// </summary>
    internal Statement statement { get; }

    internal override SyntaxType type => SyntaxType.GlobalStatement;
}
