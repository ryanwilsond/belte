using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum CompletionParts : int {
    None = 0,

    // For methods
    Parameters = 1 << 0,

    // For symbols with type (methods, fields)
    Type = 1 << 1,

    // For named type symbols
    StartBaseType = 1 << 2,
    FinishBaseType = 1 << 3,
    TemplateArguments = 1 << 4,
    TemplateParameters = 1 << 5,
    Members = 1 << 6,
    TypeMembers = 1 << 7,
    StartMemberChecks = 1 << 8,
    FinishMemberChecks = 1 << 9,
    MembersCompletedChecksStarted = 1 << 10,
    MembersCompleted = 1 << 11,

    All = (1 << 12) - 1,

    NamedTypeSymbolWithLocationAll = StartBaseType | FinishBaseType | TemplateArguments | TemplateParameters |
        Members | TypeMembers | StartMemberChecks | FinishMemberChecks,

    NamedTypeSymbolAll = NamedTypeSymbolWithLocationAll | MembersCompletedChecksStarted | MembersCompleted,

    // For fields
    ConstantValue = 1 << 6,
    FieldSymbolAll = Type | ConstantValue,

    // For methods
    StartMethodChecks = 1 << 6,
    FinishMethodChecks = 1 << 7,
    MethodSymbolAll = Parameters | Type | TemplateParameters | StartMethodChecks | FinishMethodChecks,

    // For complex parameters
    StartDefaultSyntaxValue = 1 << 6,
    EndDefaultSyntaxValue = 1 << 7,
    ComplexParameterSymbolAll = StartDefaultSyntaxValue | EndDefaultSyntaxValue,

    // For template parameters
    TemplateParameterConstraints = 1 << 8,
    TemplateParameterSymbolAll = TemplateParameterConstraints | StartDefaultSyntaxValue | EndDefaultSyntaxValue,
}
