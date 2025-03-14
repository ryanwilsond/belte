using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundTypeExpression {
    internal override Symbol expressionSymbol => type;

    internal override LookupResultKind resultKind {
        get {
            var errorType = type.originalDefinition as ErrorTypeSymbol;

            if (errorType is not null)
                return errorType.resultKind;
            else
                return LookupResultKind.Viable;
        }
    }
}
