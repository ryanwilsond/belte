using System;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundExpressionOrTypeOrConstant {
    private readonly BoundExpression _expression;
    private readonly TypeOrConstant _typeOrConstant;
    private readonly Compilation _compilation;

    internal BoundExpressionOrTypeOrConstant(BoundExpression expression) {
        syntax = expression.syntax;
        _expression = expression;
        isExpression = true;
    }

    internal BoundExpressionOrTypeOrConstant(
        Compilation compilation,
        SyntaxNode syntax,
        TypeOrConstant typeOrConstant) {
        this.syntax = syntax;
        _typeOrConstant = typeOrConstant;
        _compilation = compilation;
        isTypeOrConstant = true;
    }

    internal SyntaxNode syntax { get; }

    internal TypeSymbol type {
        get {
            if (isExpression)
                return _expression.Type();

            Debug.Assert(typeOrConstant is not null);

            if (typeOrConstant.isType)
                return typeOrConstant.type.type;

            if (typeOrConstant.constant is null)
                return null;

            Debug.Assert(_compilation is not null);

            return _compilation.GetSpecialType(typeOrConstant.constant.specialType);
        }
    }

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
