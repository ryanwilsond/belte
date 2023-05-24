using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Parser {
    [Flags]
    private enum TerminatorState {
        EndOfFile = 0,
        IsEndOfTemplateParameterList = 1 << 0,
        IsEndOfTemplateArgumentList = 1 << 2,
    }
}
