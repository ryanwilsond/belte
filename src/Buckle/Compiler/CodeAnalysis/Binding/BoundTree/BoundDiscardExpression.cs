using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDiscardExpression {
    internal BoundExpression SetInferredTypeWithAnnotations(TypeWithAnnotations type) {
        return Update(isInferred: true, type.type);
    }

    internal BoundDiscardExpression FailInference(Binder binder, BelteDiagnosticQueue diagnostics) {
        diagnostics?.Push(Error.DiscardTypeInferenceFailed(syntax.location));
        return Update(isInferred, binder.CreateErrorType("var"));
    }
}
