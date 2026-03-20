using System;

namespace Buckle.CodeAnalysis.Binding;

[Flags]
internal enum LookupOptions : int {
    Default = 0,
    NamespacesOrTypesOnly = 1 << 0,
    MustBeInvocableIfMember = 1 << 2,
    MustBeInstance = 1 << 3,
    MustNotBeInstance = 1 << 4,
    MustNotBeNamespace = 1 << 5,
    AllMethodsOnArityZero = 1 << 6,
    UseBaseReferenceAccessibility = 1 << 7,
    AllNamedTypesOnArityZero = 1 << 8,
    MustNotBeMethodTemplateParameter = 1 << 9,
    MustBeAbstractOrVirtual = 1 << 10,
    MustNotBeParameter = 1 << 11,
    NamespaceAliasesOnly = 1 << 12,
}
