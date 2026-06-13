using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundExpression : BoundNode {
    internal virtual ConstantValue constantValue => null;

    internal virtual LookupResultKind resultKind => LookupResultKind.Viable;

    internal virtual Symbol expressionSymbol => null;

    internal virtual bool suppressVirtualCalls => false;

    internal RefKind GetRefKind() {
        return kind switch {
            BoundKind.DataContainerExpression => ((BoundDataContainerExpression)this).dataContainer.refKind,
            BoundKind.ParameterExpression => ((BoundParameterExpression)this).parameter.refKind,
            BoundKind.FieldAccessExpression => ((BoundFieldAccessExpression)this).field.refKind,
            BoundKind.CallExpression => ((BoundCallExpression)this).method.refKind,
            _ => RefKind.None,
        };
    }

    internal bool IsConst() {
        switch (kind) {
            case BoundKind.DataContainerExpression:
                return ((BoundDataContainerExpression)this).dataContainer.isConst;
            case BoundKind.ParameterExpression:
                return ((BoundParameterExpression)this).parameter.isConst;
            case BoundKind.FieldAccessExpression:
                var fieldAccess = (BoundFieldAccessExpression)this;
                return fieldAccess.field.isConst || (fieldAccess.receiver?.IsConst() == true);
            // TODO Pretty sure we don't care about calls because their "constness" is not referring to the return value
            // BoundKind.CallExpression => ((BoundCallExpression)this).method.isEffectivelyConst,
            default:
                return false;
        }
    }

    internal TypeSymbol Type() {
        if (type is null)
            return null;

        return type.UnderlyingTemplateTypeOrSelf();
    }

    internal TypeSymbol StrippedType() {
        if (type is null)
            return null;

        return type.StrippedType().UnderlyingTemplateTypeOrSelf().StrippedType();
    }

    internal bool IsLiteralNull() {
        return kind == BoundKind.LiteralExpression && ConstantValue.IsNull(constantValue);
    }

    internal bool IsLiteralDefault() {
        return kind == BoundKind.DefaultLiteral;
    }

    internal bool IsImplicitObjectCreation() {
        return kind == BoundKind.UnconvertedObjectCreationExpression;
    }

    internal bool IsLiteralDefaultOrImplicitObjectCreation() {
        return IsLiteralDefault() || IsImplicitObjectCreation();
    }

    internal bool NeedsToBeConverted() {
        switch (kind) {
            case BoundKind.DefaultLiteral:
            case BoundKind.TupleLiteral:
            case BoundKind.UnconvertedInitializerList:
            case BoundKind.UnconvertedImplicitEnumFieldExpression:
            case BoundKind.UnconvertedObjectCreationExpression:
            case BoundKind.UnconvertedConditionalOperator:
            case BoundKind.UnconvertedExtendedLiteralExpression:
                return true;
            default:
                return false;
        }
    }

    internal bool HasExpressionType() {
        return type is not null;
    }

    internal void GetExpressionSymbols(ArrayBuilder<Symbol> symbols) {
        switch (kind) {
            case BoundKind.MethodGroup:
                symbols.AddRange(((BoundMethodGroup)this).methods);
                break;
            case BoundKind.ErrorExpression:
                foreach (var s in ((BoundErrorExpression)this).symbols) {
                    if (s is not null)
                        symbols.Add(s);
                }

                break;
            default:
                if (expressionSymbol is not null)
                    symbols.Add(expressionSymbol);

                break;
        }
    }
}
