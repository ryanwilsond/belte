
namespace Buckle.CodeAnalysis.Syntax;

public partial class AttributeSyntax {
    internal string GetErrorDisplayName() {
        return name.ErrorDisplayName();
    }
}
