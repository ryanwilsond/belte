using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol {
    internal class SynthesizedExplicitImplementations {
        internal static readonly SynthesizedExplicitImplementations Empty
            = new SynthesizedExplicitImplementations([], []);

        internal readonly ImmutableArray<SynthesizedExplicitImplementationForwardingMethod> forwardingMethods;
        internal readonly ImmutableArray<(MethodSymbol Body, MethodSymbol Implemented)> methodImpls;

        private SynthesizedExplicitImplementations(
            ImmutableArray<SynthesizedExplicitImplementationForwardingMethod> forwardingMethods,
            ImmutableArray<(MethodSymbol Body, MethodSymbol Implemented)> methodImpls) {
            this.forwardingMethods = forwardingMethods.NullToEmpty();
            this.methodImpls = methodImpls.NullToEmpty();
        }

        internal static SynthesizedExplicitImplementations Create(
            ImmutableArray<SynthesizedExplicitImplementationForwardingMethod> forwardingMethods,
            ImmutableArray<(MethodSymbol Body, MethodSymbol Implemented)> methodImpls) {
            if (forwardingMethods.IsDefaultOrEmpty && methodImpls.IsDefaultOrEmpty)
                return Empty;

            return new SynthesizedExplicitImplementations(forwardingMethods, methodImpls);
        }
    }
}
