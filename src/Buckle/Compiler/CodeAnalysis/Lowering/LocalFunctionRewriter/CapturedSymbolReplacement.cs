using System;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

internal abstract class CapturedSymbolReplacement {
    internal readonly bool isReusable;

    internal CapturedSymbolReplacement(bool isReusable) {
        this.isReusable = isReusable;
    }

    internal abstract BoundExpression Replacement<TArg>(
        SyntaxNode node,
        Func<NamedTypeSymbol, TArg, BoundExpression> makeFrame,
        TArg arg);
}
