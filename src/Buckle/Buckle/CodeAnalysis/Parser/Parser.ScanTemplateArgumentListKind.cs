namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Parser {
    private enum ScanTemplateArgumentListKind {
        NotTemplateArgumentList,
        PossibleTemplateArgumentList,
        DefiniteTemplateArgumentList,
    }
}
