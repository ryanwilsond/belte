using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class CommonAttributeDataComparer : IEqualityComparer<AttributeData> {
    public static CommonAttributeDataComparer Instance = new CommonAttributeDataComparer();
    private CommonAttributeDataComparer() { }

    public bool Equals(AttributeData attr1, AttributeData attr2) {
        return
            // attr1.attributeClass == attr2.attributeClass &&
            // attr1.AttributeConstructor == attr2.AttributeConstructor &&
            attr1.hasErrors == attr2.hasErrors
            // && attr1.IsConditionallyOmitted == attr2.IsConditionallyOmitted &&
            // attr1.CommonConstructorArguments.SequenceEqual(attr2.CommonConstructorArguments) &&
            // attr1.NamedArguments.SequenceEqual(attr2.NamedArguments)
            ;
    }

    public int GetHashCode(AttributeData attr) {
        // int hash = attr.AttributeClass?.GetHashCode() ?? 0;
        // hash = attr.AttributeConstructor != null ? Hash.Combine(attr.AttributeConstructor.GetHashCode(), hash) : hash;
        // hash = Hash.Combine(attr.HasErrors, hash);
        // hash = Hash.Combine(attr.IsConditionallyOmitted, hash);
        // hash = Hash.Combine(GetHashCodeForConstructorArguments(attr.CommonConstructorArguments), hash);
        // hash = Hash.Combine(GetHashCodeForNamedArguments(attr.NamedArguments), hash);

        // return hash;
        return attr.hasErrors.GetHashCode();
    }

    // private static int GetHashCodeForConstructorArguments(ImmutableArray<TypedConstant> constructorArguments) {
    //     int hash = 0;

    //     foreach (var arg in constructorArguments) {
    //         hash = Hash.Combine(arg.GetHashCode(), hash);
    //     }

    //     return hash;
    // }

    // private static int GetHashCodeForNamedArguments(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments) {
    //     int hash = 0;

    //     foreach (var arg in namedArguments) {
    //         if (arg.Key != null) {
    //             hash = Hash.Combine(arg.Key.GetHashCode(), hash);
    //         }

    //         hash = Hash.Combine(arg.Value.GetHashCode(), hash);
    //     }

    //     return hash;
    // }
}
