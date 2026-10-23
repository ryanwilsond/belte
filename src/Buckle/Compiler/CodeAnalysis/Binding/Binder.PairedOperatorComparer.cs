using System.Collections.Generic;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private class PairedOperatorComparer : IEqualityComparer<MethodSymbol> {
        public static readonly PairedOperatorComparer Instance = new PairedOperatorComparer();

        private PairedOperatorComparer() { }

        public bool Equals(MethodSymbol x, MethodSymbol y) {
            Debug.Assert(!x.isOverride);
            Debug.Assert(!x.isStatic);

            Debug.Assert(!y.isOverride);
            Debug.Assert(!y.isStatic);

            var typeComparer = SymbolEqualityComparer.AllIgnoreOptions;
            return typeComparer.Equals(x.containingType, y.containingType) &&
                   SourceMemberContainerTypeSymbol.DoOperatorsPair(x, y);
        }

        public int GetHashCode(MethodSymbol method) {
            Debug.Assert(!method.isOverride);
            Debug.Assert(!method.isStatic);

            var typeComparer = SymbolEqualityComparer.AllIgnoreOptions;
            int result = typeComparer.GetHashCode(method.containingType);

            if (method.parameterTypesWithAnnotations is [var typeWithAnnotations, ..])
                result = Hash.Combine(result, typeComparer.GetHashCode(typeWithAnnotations.type));

            return result;
        }
    }
}
