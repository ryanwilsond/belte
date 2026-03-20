using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal static class TypeSymbolExtensions {
    internal static bool IsVerifierReference(this TypeSymbol type) {
        return CodeGenerator.IsReferenceType(type) && type.StrippedType().typeKind != TypeKind.TemplateParameter;
    }

    internal static bool IsVerifierValue(this TypeSymbol type) {
        return CodeGenerator.IsValueType(type) && type.StrippedType().typeKind != TypeKind.TemplateParameter;
    }
}
