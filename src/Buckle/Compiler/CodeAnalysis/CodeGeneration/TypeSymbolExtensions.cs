using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal static class TypeSymbolExtensions {
    internal static bool IsVerifierReference(this TypeSymbol type) {
        var stripped = type.StrippedType();
        return CodeGenerator.IsReferenceType(stripped) && stripped.typeKind != TypeKind.TemplateParameter;
    }

    internal static bool IsVerifierValue(this TypeSymbol type) {
        var stripped = type.StrippedType();
        return CodeGenerator.IsValueType(stripped) && stripped.typeKind != TypeKind.TemplateParameter;
    }
}
