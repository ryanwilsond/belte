
namespace Buckle.CodeAnalysis.Symbols;

internal static class TypeOrConstantExtensions {
    internal static bool HasTypeOrConstant(this TypeOrConstant typeOrConstant) {
        if (typeOrConstant is null)
            return false;

        if (typeOrConstant.isType)
            return typeOrConstant.type.HasType();

        return typeOrConstant.constant is not null;
    }
}
