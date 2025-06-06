using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal static class TypeSymbolExtensions {
    internal static bool IsVerifierReference(this TypeSymbol type) {
        return CodeGenerator.IsReferenceType(type) && type.typeKind != TypeKind.TemplateParameter;
    }

    internal static bool IsVerifierValue(this TypeSymbol type) {
        return CodeGenerator.IsValueType(type) && type.typeKind != TypeKind.TemplateParameter;
    }
}
