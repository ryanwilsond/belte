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
    EnumUnderlyingType = 1 << 6,
    TemplateArguments = 1 << 7,
    TemplateParameters = 1 << 8,
    Members = 1 << 9,
    TypeMembers = 1 << 10,
    SynthesizedExplicitImplementations = 1 << 11,
    StartMemberChecks = 1 << 12,
    FinishMemberChecks = 1 << 13,
    MembersCompletedChecksStarted = 1 << 14,
    MembersCompleted = 1 << 15,

    All = (1 << 16) - 1,

    NamedTypeSymbolWithLocationAll = Attributes | StartBaseType | FinishBaseType | TemplateArguments | TemplateParameters |
        Members | TypeMembers | SynthesizedExplicitImplementations | StartMemberChecks | FinishMemberChecks | EnumUnderlyingType,

    NamedTypeSymbolAll = NamedTypeSymbolWithLocationAll | MembersCompletedChecksStarted | MembersCompleted,

    // For Usings
    StartValidatingImports = 1 << 4,
    FinishValidatingImports = 1 << 5,
    ImportsAll = StartValidatingImports | FinishValidatingImports,

    // For namespaces
    NameToMembersMap = 1 << 9,
    NamespaceSymbolAll = NameToMembersMap | MembersCompleted,

    // For fields
    FixedSize = 1 << 9,
    ConstantValue = 1 << 10,
    FieldSymbolAll = Attributes | Type | FixedSize | ConstantValue,

    // For methods
    StartMethodChecks = 1 << 9,
    FinishMethodChecks = 1 << 10,
    MethodSymbolAll = Attributes | ReturnTypeAttributes | Parameters | Type | TemplateParameters | StartMethodChecks | FinishMethodChecks,

    // For complex parameters
    StartDefaultSyntaxValue = 1 << 9,
    EndDefaultSyntaxValue = 1 << 10,
    EndDefaultSyntaxValueDiagnostics = 1 << 16,
    ComplexParameterSymbolAll = Attributes | StartDefaultSyntaxValue | EndDefaultSyntaxValue | EndDefaultSyntaxValueDiagnostics,

    // For template parameters
    TemplateParameterConstraints = 1 << 11,
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
