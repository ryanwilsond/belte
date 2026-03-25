
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class LanguageParser {
    private enum ParseTypeMode : byte {
        Normal,
        Parameter,
        AfterIs,
        AfterRef,
        AsExpression,
        NewExpression,
    }
}
