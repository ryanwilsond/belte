
namespace Buckle.CodeAnalysis.Binding;

/// Note: All bound versions of the <see cref="Syntax.StatementSyntax" /> and <see cref="Syntax.ExpressionSyntax" />
/// share function with <see cref="Syntax.InternalSyntax.Parser" /> equivalents. Thus use their xml comments for
/// reference.

/// <summary>
/// A bound statement, bound from a <see cref="Syntax.StatementSyntax" />.
/// </summary>
internal abstract class BoundStatement : BoundNode { }
