using System;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class CapturedToFrameSymbolReplacement : CapturedSymbolReplacement {
    internal readonly LambdaCapturedVariable hoistedField;

    internal CapturedToFrameSymbolReplacement(LambdaCapturedVariable hoistedField, bool isReusable)
        : base(isReusable) {
        this.hoistedField = hoistedField;
    }

    internal override BoundExpression Replacement<TArg>(
        SyntaxNode node,
        Func<NamedTypeSymbol, TArg, BoundExpression> makeFrame,
        TArg arg) {
        var frame = makeFrame(hoistedField.containingType, arg);
        var field = hoistedField.AsMember((NamedTypeSymbol)frame.type);
        return new BoundFieldAccessExpression(node, frame, field, constantValue: null, field.type);
    }
}
