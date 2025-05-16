
namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class TemplateNameSyntax {
    public bool isUnboundTemplateName => templateArgumentList.arguments.Any(SyntaxKind.OmittedArgument);

    internal override string ErrorDisplayName() {
        return identifier.text;
    }
}
