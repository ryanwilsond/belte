
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Parser {
    private readonly struct ResetPoint {
        internal readonly int position;
        internal readonly GreenNode prevTokenTrailingTrivia;
        internal readonly TerminatorState terminatorState;
        internal readonly ParserContext context;
        internal readonly Stack<SyntaxKind> bracketStack;

        internal ResetPoint(
            int position,
            GreenNode prevTokenTrailingTrivia,
            TerminatorState terminatorState,
            ParserContext context,
            Stack<SyntaxKind> bracketStack) {
            this.position = position;
            this.prevTokenTrailingTrivia = prevTokenTrailingTrivia;
            this.terminatorState = terminatorState;
            this.context = context;
            this.bracketStack = bracketStack;
        }
    }
}
