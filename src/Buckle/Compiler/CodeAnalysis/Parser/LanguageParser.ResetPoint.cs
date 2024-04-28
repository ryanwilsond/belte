using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class LanguageParser {
    private new readonly struct ResetPoint {
        internal readonly SyntaxParser.ResetPoint baseResetPoint;
        internal readonly TerminatorState terminatorState;
        internal readonly ParserContext context;
        internal readonly Stack<SyntaxKind> bracketStack;

        internal ResetPoint(
            SyntaxParser.ResetPoint baseResetPoint,
            TerminatorState terminatorState,
            ParserContext context,
            Stack<SyntaxKind> bracketStack) {
            this.baseResetPoint = baseResetPoint;
            this.terminatorState = terminatorState;
            this.context = context;
            this.bracketStack = bracketStack;
        }
    }
}
