using System.Collections.Generic;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

#pragma warning disable CS0659

internal sealed partial class OverloadResolution {
    private sealed class BinaryOperatorMethodEqualityComparer : IEqualityComparer<MethodSymbol> {
        internal static readonly BinaryOperatorMethodEqualityComparer Instance = new();

        private BinaryOperatorMethodEqualityComparer() { }

        public override bool Equals(object? obj) {
            return base.Equals(obj);
        }

        public bool Equals(MethodSymbol x, MethodSymbol y) {
            Debug.Assert(x is not null && y is not null);
            Debug.Assert(x.isStatic && y.isStatic);
            Debug.Assert(x is SubstitutedMethodSymbol && y is SubstitutedMethodSymbol);
            Debug.Assert((object)x != x.constructedFrom && (object)y != y.constructedFrom);
            // Purposely NOT checking the original definition because they could be different

            if (!TypeSymbol.Equals(x.containingType, y.containingType, TypeCompareKind.ConsiderEverything))
                return false;

            if (x.name != y.name)
                return false;

            if (x.parameterCount != y.parameterCount)
                return false;

            for (var i = 0; i < x.parameterCount; i++) {
                if (!TypeSymbol.Equals(
                        x.GetParameterType(i),
                        y.GetParameterType(i),
                        TypeCompareKind.ConsiderEverything)) {
                    return false;
                }
            }

            if (!TypeSymbol.Equals(x.returnType, y.returnType, TypeCompareKind.ConsiderEverything))
                return false;

            if (x.arity != y.arity)
                return false;

            for (var i = 0; i < x.arity; i++) {
                if (!x.templateArguments[i].Equals(y.templateArguments[i], TypeCompareKind.ConsiderEverything))
                    return false;
            }

            return true;
        }

        public int GetHashCode(MethodSymbol obj) {
            throw ExceptionUtilities.Unreachable();
        }
    }
}
