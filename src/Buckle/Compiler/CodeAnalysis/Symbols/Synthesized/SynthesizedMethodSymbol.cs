using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedMethodSymbol : MethodSymbol {
    private ParameterSymbol _lazyThisParameter;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal sealed override bool isImplicitlyDeclared => true;

    internal abstract override bool isStatic { get; }

    internal override bool TryGetThisParameter(out ParameterSymbol thisParameter) {
        if (isStatic) {
            thisParameter = null;
            return true;
        }

        if (_lazyThisParameter is null)
            Interlocked.CompareExchange(ref _lazyThisParameter, new ThisParameterSymbol(this), null);

        thisParameter = _lazyThisParameter;
        return true;
    }

    internal sealed override UnmanagedCallersOnlyAttributeData GetUnmanagedCallersOnlyAttributeData(
        bool forceComplete) {
        return null;
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override bool isDeclaredConst => false;

    internal override bool hasMustUseReturnValueAttribute => false;

    internal sealed override bool hasUnscopedRefAttribute => false;
}
