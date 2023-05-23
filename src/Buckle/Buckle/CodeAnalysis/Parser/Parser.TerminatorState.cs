using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Parser {
    [Flags]
    private enum TerminatorState {
        IsEndOfTemplateParameterList = 0,
        IsEndOfTemplateArgumentList = 1 << 0,
    }
}
