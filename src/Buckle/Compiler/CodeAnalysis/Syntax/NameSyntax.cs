
namespace Buckle.CodeAnalysis.Syntax;

public abstract partial class NameSyntax {
    internal abstract string ErrorDisplayName();

    internal abstract SimpleNameSyntax GetUnqualifiedName();
}
