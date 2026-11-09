using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial struct MethodTypeInferrer {
    internal abstract partial class Extensions {
        private sealed class DefaultExtensions : Extensions {
            internal override TypeWithAnnotations GetTypeWithAnnotations(BoundExpression expr) {
                return new TypeWithAnnotations(expr.Type());
            }

            internal override TypeWithAnnotations GetMethodGroupResultType(BoundMethodGroup group, MethodSymbol method) {
                return method.returnTypeWithAnnotations;
            }
        }
    }
}
