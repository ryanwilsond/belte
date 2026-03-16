using System;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundExpressionOrTypeOrConstant {
    private readonly BoundExpression _expression;
    private readonly TypeOrConstant _typeOrConstant;

    internal BoundExpressionOrTypeOrConstant(BoundExpression expression) {
        _expression = expression;
        isExpression = true;
    }

    internal BoundExpressionOrTypeOrConstant(TypeOrConstant typeOrConstant) {
        _typeOrConstant = typeOrConstant;
        isTypeOrConstant = true;
    }

    internal TypeSymbol type => isExpression ? _expression.type : typeOrConstant.type.type;

    internal bool isExpression { get; }

    internal bool isTypeOrConstant { get; }

    internal BoundExpression expression {
        get {
            if (isExpression)
                return _expression;

            throw new InvalidOperationException();
        }
    }

    internal TypeOrConstant typeOrConstant {
        get {
            if (isTypeOrConstant)
                return _typeOrConstant;

            throw new InvalidOperationException();
        }
    }
}
