using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundExpression : BoundNode {
    internal virtual ConstantValue constantValue => null;

    internal virtual LookupResultKind resultKind => LookupResultKind.Viable;

    internal virtual Symbol expressionSymbol => null;

    internal RefKind GetRefKind() {
        return kind switch {
            BoundKind.DataContainerExpression => ((BoundDataContainerExpression)this).dataContainer.refKind,
            BoundKind.ParameterExpression => ((BoundParameterExpression)this).parameter.refKind,
            BoundKind.FieldAccessExpression => ((BoundFieldAccessExpression)this).field.refKind,
            BoundKind.CallExpression => ((BoundCallExpression)this).method.refKind,
            _ => RefKind.None,
        };
    }

    internal bool IsLiteralNull() {
        return kind == BoundKind.LiteralExpression && ConstantValue.IsNull(constantValue);
    }

    internal bool NeedsToBeConverted() {
        switch (kind) {
            case BoundKind.UnconvertedInitializerList:
                return true;
            default:
                return false;
        }
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
