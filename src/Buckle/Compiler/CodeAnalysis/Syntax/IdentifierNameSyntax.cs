
namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class IdentifierNameSyntax {
    internal override string ErrorDisplayName() {
        return identifier.text;
    }
}
