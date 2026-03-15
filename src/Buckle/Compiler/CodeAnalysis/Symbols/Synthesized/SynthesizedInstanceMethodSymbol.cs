using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedInstanceMethodSymbol : MethodSymbol {
    private ParameterSymbol _lazyThisParameter;

    internal override bool TryGetThisParameter(out ParameterSymbol thisParameter) {
        if (_lazyThisParameter is null)
            Interlocked.CompareExchange(ref _lazyThisParameter, new ThisParameterSymbol(this), null);

        thisParameter = _lazyThisParameter;
        return true;
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }
}
