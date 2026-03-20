using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

namespace Repl;

internal static class SyntaxTokenExtensions {
    internal static void PrettyPrint(DisplayText text, SyntaxToken token) {
        text.Write(CreatePunctuation("⟨"));
        // All tokens are tokens, so we don't need to display token every time
        text.Write(CreateIdentifier(token.kind.ToString().Replace("Token", "")));

        if (token.text is not null) {
            text.Write(CreatePunctuation(", "));
            text.Write(CreateString($"\"{token.text}\""));
        }

        text.Write(CreatePunctuation("⟩"));
    }
}
