namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class LanguageParser {
    private enum ScanTemplateArgumentListKind {
        NotTemplateArgumentList,
        PossibleTemplateArgumentList,
        DefiniteTemplateArgumentList,
    }
}
