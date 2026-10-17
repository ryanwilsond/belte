using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class TypeConversions : ConversionsBase {
    internal TypeConversions(CorLibrary corLibrary) : base(corLibrary) { }

    internal override Conversion GetImplicitExtendedLiteralExpressionConversion(
        BoundUnconvertedExtendedLiteralExpression extended,
        TypeSymbol destination) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override Conversion GetMethodGroupConversion(BoundMethodGroup source, TypeSymbol destination) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override Conversion GetListExpressionConversion(
        BoundUnconvertedInitializerList node,
        TypeSymbol targetType) {
        throw ExceptionUtilities.Unreachable();
    }

    private protected override bool TryToConstructUserDefinedOperator(
        MethodSymbol op,
        BoundExpression argument,
        TypeSymbol source,
        TypeSymbol target,
        out MethodSymbol result) {
        result = null;
        return false;
    }

    private protected override Conversion GetImplicitArrayLengthConversion(
        BoundUnconvertedArrayLength length,
        TypeSymbol destination) {
        throw ExceptionUtilities.Unreachable();
    }
}
