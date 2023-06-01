using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Parser {
    [Flags]
    private enum NameOptions {
        None = 0,
        InExpression = 1 << 0,
    }
}
