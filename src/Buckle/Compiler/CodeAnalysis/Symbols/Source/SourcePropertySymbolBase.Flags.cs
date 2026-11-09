using System;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourcePropertySymbolBase {
    [Flags]
    private enum Flags : ushort {
        IsExpressionBodied = 1 << 0,
        HasAutoPropertyGet = 1 << 1,
        HasAutoPropertySet = 1 << 2,
        GetterUsesFieldKeyword = 1 << 3,
        SetterUsesFieldKeyword = 1 << 4,
        IsExplicitInterfaceImplementation = 1 << 5,
        HasInitializer = 1 << 6,
        AccessorsHaveImplementation = 1 << 7,
        HasExplicitAccessModifier = 1 << 8,
        RequiresBackingField = 1 << 9,
    }
}
