
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class LanguageParser {
    private enum ScanTypeFlags : byte {
        NotType,
        MustBeType,
        TemplateTypeOrMethod,
        TemplateTypeOrExpression,
        NonTemplateTypeOrExpression,
        AliasQualifiedName,
        NonNullableType,
        PointerOrMultiplication,
    }
}
