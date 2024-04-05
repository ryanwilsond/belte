using System;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Parser {
    [Flags]
    private enum ParserContext : byte {
        None = 0,
        InExpression = 1 << 0,
        InTemplateArgumentList = 1 << 1,
        InClassDefinition = 1 << 2,
        InStatement = 1 << 3,
        InStructDefinition = 1 << 4,
    }
}
