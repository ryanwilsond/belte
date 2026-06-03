using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class TupleBinaryOperatorInfo {
    internal class Single : TupleBinaryOperatorInfo {
        internal readonly BinaryOperatorKind kind;
        internal readonly MethodSymbol methodSymbol;
        internal readonly TypeSymbol constrainedToType;

        internal readonly BoundValuePlaceholder conversionForBoolPlaceholder;
        internal readonly BoundExpression conversionForBool;

        internal readonly UnaryOperatorSignature boolOperator;

        internal Single(
            TypeSymbol leftConvertedTypeOpt,
            TypeSymbol rightConvertedTypeOpt,
            BinaryOperatorKind kind,
            MethodSymbol methodSymbolOpt,
            TypeSymbol constrainedToTypeOpt,
            BoundValuePlaceholder conversionForBoolPlaceholder,
            BoundExpression conversionForBool,
            UnaryOperatorSignature boolOperator)
            : base(leftConvertedTypeOpt, rightConvertedTypeOpt) {
            this.kind = kind;
            methodSymbol = methodSymbolOpt;
            constrainedToType = constrainedToTypeOpt;
            this.conversionForBoolPlaceholder = conversionForBoolPlaceholder;
            this.conversionForBool = conversionForBool;
            this.boolOperator = boolOperator;
        }

        internal override TupleBinaryOperatorInfoKind infoKind => TupleBinaryOperatorInfoKind.Single;

        public override string ToString() {
            return $"binaryOperatorKind: {kind}";
        }
    }
}
