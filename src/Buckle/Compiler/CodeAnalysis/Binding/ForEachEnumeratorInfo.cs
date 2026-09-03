using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ForEachEnumeratorInfo {
    internal readonly MethodSymbol getEnumeratorMethod;
    internal readonly MethodSymbol moveNextMethod;
    internal readonly MethodSymbol getCurrentMethod;
    internal readonly MethodSymbol disposeMethod;

    internal ForEachEnumeratorInfo(
        MethodSymbol getEnumeratorMethod,
        MethodSymbol moveNextMethod,
        MethodSymbol getCurrentMethod,
        MethodSymbol disposeMethod) {
        this.getEnumeratorMethod = getEnumeratorMethod;
        this.moveNextMethod = moveNextMethod;
        this.getCurrentMethod = getCurrentMethod;
        this.disposeMethod = disposeMethod;
    }
}
