
namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class QualifiedNameSyntax {
    internal override string ErrorDisplayName() {
        return string.Concat(left.ErrorDisplayName(), ".", right.ErrorDisplayName());
    }

    internal override SimpleNameSyntax GetUnqualifiedName() {
        return right;
    }
}
