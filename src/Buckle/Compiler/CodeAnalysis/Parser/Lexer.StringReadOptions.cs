using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Lexer {
    [Flags]
    private enum StringReadOptions {
        Normal = 0,
        Character = 1 << 0,
        Interpolation = 1 << 1,
        ConsumeEndQuote = 1 << 2,
        Multiline = 1 << 3,
    }
}
