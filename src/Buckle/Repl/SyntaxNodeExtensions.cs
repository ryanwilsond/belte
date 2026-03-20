using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Repl;

internal static class SyntaxNodeExtensions {
    internal static void PrettyPrint(DisplayText text, SyntaxNodeOrToken node, string indent = "", bool isLast = true) {
        var token = node.AsToken();

        if (token is not null) {
            foreach (var trivia in token.leadingTrivia) {
                text.Write(CreatePunctuation(indent));
                text.Write(CreatePunctuation("├─"));
                text.Write(CreateRedNode($"Lead: {trivia.kind} [{trivia.span.start}..{trivia.span.end})"));
                text.WriteLine();
            }
        }

        var hasTrailingTrivia = token is not null && token.trailingTrivia.Any();
        var tokenMarker = !hasTrailingTrivia && isLast ? "└─" : "├─";

        text.Write(CreatePunctuation($"{indent}{tokenMarker}"));

        if (node.isToken)
            text.Write(CreateGreenNode(node.AsToken().kind.ToString()));
        else
            text.Write(CreateBlueNode(node.AsNode().kind.ToString()));

        if (node.AsToken(out var t) && t.text is not null)
            text.Write(CreatePunctuation($" {t.text}"));

        if (node.isToken) {
            text.Write(CreateGreenNode($" [{node.span.start}..{node.span.end})"));
            text.WriteLine();
        } else {
            text.Write(CreateBlueNode($" [{node.span.start}..{node.span.end})"));
            text.WriteLine();
        }

        if (token is not null) {
            foreach (var trivia in token.trailingTrivia) {
                var isLastTrailingTrivia = trivia.index == token.trailingTrivia.Count - 1;
                var triviaMarker = isLast && isLastTrailingTrivia ? "└─" : "├─";

                text.Write(CreatePunctuation(indent));
                text.Write(CreatePunctuation(triviaMarker));
                text.Write(CreateRedNode($"Trail: {trivia.kind} [{trivia.span.start}..{trivia.span.end})"));
                text.WriteLine();
            }
        }

        indent += isLast ? "  " : "│ ";

        if (node.isToken)
            return;

        var children = node.AsNode().ChildNodesAndTokens();
        var lastChild = children.Last();

        foreach (var child in children)
            PrettyPrint(text, child, indent, child == lastChild);
    }
}
