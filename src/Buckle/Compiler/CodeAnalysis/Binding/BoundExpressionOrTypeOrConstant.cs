using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundExpressionOrTypeOrConstant {
    private readonly BoundExpression _expression;
    private readonly TypeOrConstant _typeOrConstant;

    internal BoundExpressionOrTypeOrConstant(BoundExpression expression) {
        syntax = expression.syntax;
        _expression = expression;
        isExpression = true;
    }

    internal BoundExpressionOrTypeOrConstant(SyntaxNode syntax, TypeOrConstant typeOrConstant) {
        this.syntax = syntax;
        _typeOrConstant = typeOrConstant;
        isTypeOrConstant = true;
    }

    internal SyntaxNode syntax { get; }

    internal TypeSymbol type => isExpression
        ? _expression.Type()
        : typeOrConstant.isType
            ? typeOrConstant.type.type
            : CorLibrary.GetSpecialType(typeOrConstant.constant.specialType);

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
