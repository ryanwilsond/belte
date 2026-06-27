using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class TypeConversions : ConversionsBase {
    private static readonly TypeConversions Instance = new();

    internal TypeConversions() { }

    internal static TypeConversions GetInstance() {
        return Instance;
    }

    internal override Conversion GetImplicitExtendedLiteralExpressionConversion(
        BoundUnconvertedExtendedLiteralExpression extended,
        TypeSymbol destination) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override Conversion GetMethodGroupConversion(BoundMethodGroup source, TypeSymbol destination) {
        throw ExceptionUtilities.Unreachable();
    }
}
