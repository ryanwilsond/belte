using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial struct MethodTypeInferrer {
    internal abstract partial class Extensions {
        internal static readonly Extensions Default = new DefaultExtensions();

        internal abstract TypeWithAnnotations GetTypeWithAnnotations(BoundExpression expr);

        internal abstract TypeWithAnnotations GetMethodGroupResultType(BoundMethodGroup group, MethodSymbol method);
    }
}
