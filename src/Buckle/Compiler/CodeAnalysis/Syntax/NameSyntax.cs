
namespace Buckle.CodeAnalysis.Syntax;

public abstract partial class NameSyntax {
    internal abstract string ErrorDisplayName();

    internal abstract SimpleNameSyntax GetUnqualifiedName();

    internal string GetAliasQualifier() {
        var name = this;

        while (true) {
            switch (name.kind) {
                case SyntaxKind.QualifiedName:
                    name = ((QualifiedNameSyntax)name).left;
                    continue;
                case SyntaxKind.AliasQualifiedName:
                    return ((AliasQualifiedNameSyntax)name).alias.identifier.valueText;
            }

            return null;
        }
    }
}
