using System;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A specific location for binding.
/// </summary>
[Flags]
internal enum BinderFlags : uint {
    None,
    LowLevelContext = 1 << 0,
    IgnoreAccessibility = 1 << 1,
    TemplateConstraintsClause = 1 << 2,
    SuppressConstraintChecks = 1 << 3,
    SuppressTemplateArgumentBinding = 1 << 4,
    ParameterDefaultValue = 1 << 5,
    FieldInitializer = 1 << 6,
    ConstructorInitializer = 1 << 7,
    ObjectInitializerMember = 1 << 8,
    ConstContext = 1 << 9,
    InWithTryBody = 1 << 10,
    InDeferBody = 1 << 11,

    InCatchBlock = 1 << 12,
    InFinallyBlock = 1 << 13,
    InTryBlockOfTryCatch = 1 << 14,
    InNestedFinallyBlock = 1 << 15,

    InContextualAttributeBinder = 1 << 16,
    AttributeArgument = 1 << 17,
    EarlyAttributeBinding = 1 << 18,

    AllClearedAtExecutableCodeBoundary = InCatchBlock | InFinallyBlock | InTryBlockOfTryCatch | InNestedFinallyBlock,
}
