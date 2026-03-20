using System.Linq;

namespace Buckle.CodeAnalysis.Syntax;

public partial class LocalDeclarationStatementSyntax {
    internal bool isConst => modifiers?.Where(t => t.kind == SyntaxKind.ConstKeyword)?.Any() ?? false;

    internal bool isConstExpr => modifiers?.Where(t => t.kind == SyntaxKind.ConstexprKeyword)?.Any() ?? false;
}
