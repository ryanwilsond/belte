using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private readonly struct MethodInfo {
        internal Symbol symbol { get; }

        internal MethodSymbol method { get; }

        internal MethodSymbol setMethod { get; }

        internal bool returnsRefToRefStruct
            => method is { refKind: not RefKind.None, returnType: { } returnType } &&
                returnType.IsRefLikeOrAllowsRefLikeType();

        private MethodInfo(Symbol symbol, MethodSymbol method, MethodSymbol setMethod) {
            this.symbol = symbol;
            this.method = method;
            this.setMethod = setMethod;
        }

        internal static MethodInfo Create(MethodSymbol method) {
            return new MethodInfo(method, method, null);
        }

        public override string? ToString() => method?.ToString();
    }
}
