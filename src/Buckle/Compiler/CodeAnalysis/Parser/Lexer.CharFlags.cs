
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Lexer {
    private enum CharFlags : byte {
        White,
        CR,
        LF,
        Letter,
        Digit,
        Punct,
        Dot,
        CompoundPunctStart,
        Slash,
        Complex,
        EndOfFile,
    }
}
