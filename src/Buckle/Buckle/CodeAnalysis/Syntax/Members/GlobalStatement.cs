
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A global statement.
/// </summary>
internal sealed partial class GlobalStatement : Member {
    /// <summary>
    /// Creates a <see cref="GlobalStatement" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> this <see cref="Node" /> resides in.</param>
    /// <param name="statement"><see cref="Statement" />.</param>
    /// <returns>.</returns>
    internal GlobalStatement(SyntaxTree syntaxTree, Statement statement) : base(syntaxTree) {
        this.statement = statement;
    }

    /// <summary>
    /// <see cref="Statement" /> (should ignore that fact that it is global).
    /// </summary>
    internal Statement statement { get; }

    internal override SyntaxType type => SyntaxType.GlobalStatement;
}
