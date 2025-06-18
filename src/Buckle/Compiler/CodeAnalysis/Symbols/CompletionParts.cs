using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum CompletionParts : ushort {
    None = 0,

    // For all symbols
    Attributes = 1 << 0,

    // For methods
    Parameters = 1 << 1,

    // For symbols with type (methods, fields)
    Type = 1 << 2,

    // For named type symbols
    StartBaseType = 1 << 3,
    FinishBaseType = 1 << 4,
    TemplateArguments = 1 << 5,
    TemplateParameters = 1 << 6,
    Members = 1 << 7,
    TypeMembers = 1 << 8,
    SynthesizedExplicitImplementations = 1 << 9,
    StartMemberChecks = 1 << 10,
    FinishMemberChecks = 1 << 11,
    MembersCompletedChecksStarted = 1 << 12,
    MembersCompleted = 1 << 13,

    All = (1 << 14) - 1,

    NamedTypeSymbolWithLocationAll = StartBaseType | FinishBaseType | TemplateArguments | TemplateParameters |
        Members | TypeMembers | SynthesizedExplicitImplementations | StartMemberChecks | FinishMemberChecks,

    NamedTypeSymbolAll = NamedTypeSymbolWithLocationAll | MembersCompletedChecksStarted | MembersCompleted,

    // For Usings
    StartValidatingImports = 1 << 3,
    FinishValidatingImports = 1 << 4,
    ImportsAll = StartValidatingImports | FinishValidatingImports,

    // For namespaces
    NameToMembersMap = 1 << 7,
    NamespaceSymbolAll = NameToMembersMap | MembersCompleted,

    // For fields
    ConstantValue = 1 << 7,
    FieldSymbolAll = Type | ConstantValue,

    // For methods
    StartMethodChecks = 1 << 7,
    FinishMethodChecks = 1 << 8,
    MethodSymbolAll = Parameters | Type | TemplateParameters | StartMethodChecks | FinishMethodChecks,

    // For complex parameters
    StartDefaultSyntaxValue = 1 << 7,
    EndDefaultSyntaxValue = 1 << 8,
    EndDefaultSyntaxValueDiagnostics = 1 << 14,
    ComplexParameterSymbolAll = StartDefaultSyntaxValue | EndDefaultSyntaxValue | EndDefaultSyntaxValueDiagnostics,

    // For template parameters
    TemplateParameterConstraints = 1 << 9,
    TemplateParameterSymbolAll = TemplateParameterConstraints | StartDefaultSyntaxValue | EndDefaultSyntaxValue,

    // For alias symbols
    AliasTarget = 1 << 3,

    // For assembly symbols
    StartAttributeChecks = 1 << 3,
    FinishAttributeChecks = 1 << 4,
    Module = 1 << 5,
    StartValidatingAddedModules = 1 << 6,
    FinishValidatingAddedModules = 1 << 7,
    AssemblySymbolAll = Attributes | StartAttributeChecks | FinishAttributeChecks |
        Module | StartValidatingAddedModules | FinishValidatingAddedModules,
}
