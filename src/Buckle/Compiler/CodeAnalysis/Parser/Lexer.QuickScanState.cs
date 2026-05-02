
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Lexer {
    private enum QuickScanState : byte {
        Initial,
        FollowingWhite,
        FollowingCR,
        Ident,
        Number,
        Punctuation,
        Dot,
        CompoundPunctStart,
        DoneAfterNext,
        Done,
        Bad = Done + 1
    }
}
