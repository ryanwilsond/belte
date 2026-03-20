using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum CompletionParts : ushort {
    None = 0,

    // For all symbols
    Attributes = 1 << 0,

    // For methods
    ReturnTypeAttributes = 1 << 1,
    Parameters = 1 << 2,

    // For symbols with type (methods, fields)
    Type = 1 << 3,

    // For named type symbols
    StartBaseType = 1 << 4,
    FinishBaseType = 1 << 5,
    TemplateArguments = 1 << 6,
    TemplateParameters = 1 << 7,
    Members = 1 << 8,
    TypeMembers = 1 << 9,
    SynthesizedExplicitImplementations = 1 << 10,
    StartMemberChecks = 1 << 11,
    FinishMemberChecks = 1 << 12,
    MembersCompletedChecksStarted = 1 << 13,
    MembersCompleted = 1 << 14,

    All = (1 << 15) - 1,

    NamedTypeSymbolWithLocationAll = Attributes | StartBaseType | FinishBaseType | TemplateArguments | TemplateParameters |
        Members | TypeMembers | SynthesizedExplicitImplementations | StartMemberChecks | FinishMemberChecks,

    NamedTypeSymbolAll = NamedTypeSymbolWithLocationAll | MembersCompletedChecksStarted | MembersCompleted,

    // For Usings
    StartValidatingImports = 1 << 4,
    FinishValidatingImports = 1 << 5,
    ImportsAll = StartValidatingImports | FinishValidatingImports,

    // For namespaces
    NameToMembersMap = 1 << 8,
    NamespaceSymbolAll = NameToMembersMap | MembersCompleted,

    // For fields
    ConstantValue = 1 << 8,
    FieldSymbolAll = Attributes | Type | ConstantValue,

    // For methods
    StartMethodChecks = 1 << 8,
    FinishMethodChecks = 1 << 9,
    MethodSymbolAll = Attributes | ReturnTypeAttributes | Parameters | Type | TemplateParameters | StartMethodChecks | FinishMethodChecks,

    // For complex parameters
    StartDefaultSyntaxValue = 1 << 8,
    EndDefaultSyntaxValue = 1 << 9,
    EndDefaultSyntaxValueDiagnostics = 1 << 15,
    ComplexParameterSymbolAll = Attributes | StartDefaultSyntaxValue | EndDefaultSyntaxValue | EndDefaultSyntaxValueDiagnostics,

    // For template parameters
    TemplateParameterConstraints = 1 << 10,
    TemplateParameterSymbolAll = Attributes | TemplateParameterConstraints | StartDefaultSyntaxValue | EndDefaultSyntaxValue,

    // For alias symbols
    AliasTarget = 1 << 4,

    // For assembly symbols
    StartAttributeChecks = 1 << 4,
    FinishAttributeChecks = 1 << 5,
    Module = 1 << 6,
    StartValidatingAddedModules = 1 << 7,
    FinishValidatingAddedModules = 1 << 8,
    AssemblySymbolAll = Attributes | StartAttributeChecks | FinishAttributeChecks |
        Module | StartValidatingAddedModules | FinishValidatingAddedModules,
}
