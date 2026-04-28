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
    InWithBody = 1 << 10,

    InCatchBlock = 1 << 11,
    InFinallyBlock = 1 << 12,
    InTryBlockOfTryCatch = 1 << 13,
    InNestedFinallyBlock = 1 << 14,

    InContextualAttributeBinder = 1 << 15,
    AttributeArgument = 1 << 16,
    EarlyAttributeBinding = 1 << 17,

    AllClearedAtExecutableCodeBoundary = InCatchBlock | InFinallyBlock | InTryBlockOfTryCatch | InNestedFinallyBlock,
}
