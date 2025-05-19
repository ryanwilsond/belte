using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Emitting;

internal static class TypeSymbolExtensions {
    internal static bool IsVerifierReference(this TypeSymbol type) {
        return ILEmitter.CodeGenerator.IsReferenceType(type) && type.typeKind != TypeKind.TemplateParameter;
    }

    internal static bool IsVerifierValue(this TypeSymbol type) {
        return ILEmitter.CodeGenerator.IsValueType(type) && type.typeKind != TypeKind.TemplateParameter;
    }
}
