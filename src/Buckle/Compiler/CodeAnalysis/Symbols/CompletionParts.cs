using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum CompletionParts : uint {
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
    EnumUnderlyingType = 1 << 8,
    TemplateArguments = 1 << 9,
    TemplateParameters = 1 << 10,
    Members = 1 << 11,
    TypeMembers = 1 << 12,
    SynthesizedExplicitImplementations = 1 << 13,
    StartMemberChecks = 1 << 14,
    FinishMemberChecks = 1 << 15,
    MembersCompletedChecksStarted = 1 << 16,
    MembersCompleted = 1 << 17,

    All = (1 << 18) - 1,

    NamedTypeSymbolWithLocationAll = Attributes | StartBaseType | FinishBaseType | TemplateArguments | TemplateParameters |
        Members | TypeMembers | SynthesizedExplicitImplementations | StartMemberChecks | FinishMemberChecks | EnumUnderlyingType,

    NamedTypeSymbolAll = NamedTypeSymbolWithLocationAll | MembersCompletedChecksStarted | MembersCompleted,

    // For Usings
    StartValidatingImports = 1 << 4,
    FinishValidatingImports = 1 << 5,
    ImportsAll = StartValidatingImports | FinishValidatingImports,

    // For namespaces
    NameToMembersMap = 1 << 11,
    NamespaceSymbolAll = NameToMembersMap | MembersCompleted,

    // For fields
    FixedSize = 1 << 11,
    ConstantValue = 1 << 12,
    FieldSymbolAll = Attributes | Type | FixedSize | ConstantValue,

    // For methods
    StartMethodChecks = 1 << 13,
    FinishMethodChecks = 1 << 14,
    MethodSymbolAll = Attributes | ReturnTypeAttributes | Parameters | Type | TemplateParameters | StartMethodChecks | FinishMethodChecks,

    // For complex parameters
    StartDefaultSyntaxValue = 1 << 11,
    EndDefaultSyntaxValue = 1 << 12,
    EndDefaultSyntaxValueDiagnostics = 1 << 13,
    ComplexParameterSymbolAll = Attributes | StartDefaultSyntaxValue | EndDefaultSyntaxValue | EndDefaultSyntaxValueDiagnostics,

    // For template parameters
    TemplateParameterConstraints = 1 << 14,
    TemplateParameterSymbolAll = Attributes | TemplateParameterConstraints | StartDefaultSyntaxValue | EndDefaultSyntaxValue,

    // For alias symbols
    AliasTarget = 1 << 4,

    // For assembly symbols
    StartAttributeChecks = 1 << 4,
    FinishAttributeChecks = 1 << 5,
    Module = 1 << 6,
    StartValidatingAddedModules = 1 << 8,
    FinishValidatingAddedModules = 1 << 9,
    AssemblySymbolAll = Attributes | StartAttributeChecks | FinishAttributeChecks |
        Module | StartValidatingAddedModules | FinishValidatingAddedModules,
}
