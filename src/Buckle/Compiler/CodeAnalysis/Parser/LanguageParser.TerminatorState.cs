using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class LanguageParser {
    [Flags]
    private enum TerminatorState : byte {
        EndOfFile = 0,
        IsEndOfTemplateParameterList = 1 << 0,
        IsEndOfTemplateArgumentList = 1 << 2,
    }
}
