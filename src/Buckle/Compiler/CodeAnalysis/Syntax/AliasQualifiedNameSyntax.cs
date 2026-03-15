
namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class AliasQualifiedNameSyntax {
    internal override SimpleNameSyntax GetUnqualifiedName() {
        return name;
    }

    internal override string ErrorDisplayName() {
        return alias.ErrorDisplayName() + "::" + name.ErrorDisplayName();
    }
}
