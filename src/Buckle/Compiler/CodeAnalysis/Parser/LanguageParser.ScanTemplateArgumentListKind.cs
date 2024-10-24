namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class LanguageParser {
    private enum ScanTemplateArgumentListKind : byte {
        NotTemplateArgumentList,
        PossibleTemplateArgumentList,
        DefiniteTemplateArgumentList,
    }
}
