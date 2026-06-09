using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class LanguageParser {
    [Flags]
    private enum TerminatorState : byte {
        EndOfFile = 0,
        IsEndOfTemplateParameterList = 1 << 0,
        IsEndOfTemplateArgumentList = 1 << 2,
        IsAttributeListTerminator = 1 << 3,
        IsPossibleMemberStartOrStop = 1 << 4,
        IsNamespaceMemberStartOrStop = 1 << 5,
    }
}
