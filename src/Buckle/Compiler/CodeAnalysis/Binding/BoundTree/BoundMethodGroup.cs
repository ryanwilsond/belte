using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BoundMethodGroup {
    internal MemberAccessExpressionSyntax memberAccessExpressionSyntax => syntax as MemberAccessExpressionSyntax;

    internal SyntaxNode nameSyntax {
        get {
            var memberAccess = memberAccessExpressionSyntax;

            if (memberAccess is not null)
                return memberAccess.name;
            else
                return syntax;
        }
    }
}
