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

    InCatchBlock = 1 << 10,
    InFinallyBlock = 1 << 11,
    InTryBlockOfTryCatch = 1 << 12,
    InNestedFinallyBlock = 1 << 13,

    AllClearedAtExecutableCodeBoundary = InCatchBlock | InFinallyBlock | InTryBlockOfTryCatch | InNestedFinallyBlock,
}
