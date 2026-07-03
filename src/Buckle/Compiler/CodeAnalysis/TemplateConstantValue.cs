using System;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal sealed class TemplateConstantValue : ConstantValue {
    internal TemplateConstantValue(BoundExpression expression) {
        this.expression = expression;
        specialType = expression.type.specialType;
        Debug.Assert(specialType != SpecialType.None);
    }

    internal BoundExpression expression { get; }

    internal override SpecialType specialType { get; }

    internal override object value => throw new InvalidOperationException();

    internal override BelteDiagnostic[] diagnostics => null;

    internal override bool IsSameAs(ConstantValue other) {
        if (other is not TemplateConstantValue t)
            return false;

        // TODO This returns false if the trees were bound separately but turn out equivalent
        // Does this matter?
        return expression == t.expression;
    }

    internal TypeOrConstant Substitute(TemplateMap templateMap) {
        var newExpression = TemplateConstantSimplifier.Simplify(expression, templateMap);

        if (expression == newExpression)
            return new TypeOrConstant(this);

        if (newExpression.constantValue is { } constant)
            return new TypeOrConstant(constant);

        return new TypeOrConstant(new TemplateConstantValue(newExpression));
    }
}
