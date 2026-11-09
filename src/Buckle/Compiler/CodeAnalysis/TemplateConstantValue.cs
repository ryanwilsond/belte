using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    internal TypeOrConstant Substitute(TemplateMap templateMap) {
        var newExpression = TemplateConstantSimplifier.Simplify(expression, templateMap);

        if (expression == newExpression)
            return new TypeOrConstant(this);

        if (newExpression.constantValue is { } constant)
            return new TypeOrConstant(constant);

        return new TypeOrConstant(new TemplateConstantValue(newExpression));
    }

    public override int GetHashCode() {
        return expression?.GetHashCode() ?? RuntimeHelpers.GetHashCode(this);
    }

    public override bool Equals(object obj) {
        return Equals(obj as ConstantValue);
    }

    public override bool Equals(ConstantValue other) {
        if (other is not TemplateConstantValue t)
            return false;

        return ConstraintsHelpers.TemplateConstraintComparer.ExpressionsEqual(expression, t.expression);
    }
}
