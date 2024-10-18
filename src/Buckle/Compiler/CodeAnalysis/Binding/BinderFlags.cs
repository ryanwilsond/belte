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

    InCatchBlock = 1 << 6,
    InFinallyBlock = 1 << 7,
    InTryBlockofTryCatch = 1 << 8,
    InNestedFinallyBlock = 1 << 9,

    AllClearedAtExecutableCodeBoundary = InCatchBlock | InFinallyBlock | InTryBlockofTryCatch | InNestedFinallyBlock,
}
