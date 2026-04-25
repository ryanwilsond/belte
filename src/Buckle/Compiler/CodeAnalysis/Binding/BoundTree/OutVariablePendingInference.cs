using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class OutVariablePendingInference {
    internal BoundExpression SetInferredTypeWithAnnotations(
        TypeWithAnnotations type,
        BelteDiagnosticQueue diagnostics) {
        return SetInferredTypeWithAnnotations(type, null, diagnostics);
    }

    internal BoundExpression SetInferredTypeWithAnnotations(
        TypeWithAnnotations type,
        Binder binderOpt,
        BelteDiagnosticQueue diagnostics) {
        var inferenceFailed = !type.hasType;

        if (inferenceFailed) {
            type = new TypeWithAnnotations(binderOpt.CreateErrorType("var"));
        } else {
            if (isNullable && !type.IsNullableType())
                type = type.SetIsAnnotated();
            else if (isNonNullable && type.IsNullableType())
                type = new TypeWithAnnotations(type.nullableUnderlyingTypeOrSelf);
        }

        switch (symbol.kind) {
            case SymbolKind.Local:
                var localSymbol = (SourceDataContainerSymbol)symbol;

                if (diagnostics is not null) {
                    if (inferenceFailed)
                        ReportInferenceFailure(diagnostics);
                }

                localSymbol.SetTypeWithAnnotations(type);

                return new BoundDataContainerExpression(
                    syntax,
                    localSymbol,
                    constantValue: null,
                    type: type.type,
                    hasErrors: hasErrors || inferenceFailed
                );
            case SymbolKind.Field:
                var fieldSymbol = (GlobalExpressionVariable)symbol;
                var inferenceDiagnostics = BelteDiagnosticQueue.GetInstance();

                if (inferenceFailed)
                    ReportInferenceFailure(inferenceDiagnostics);

                type = fieldSymbol.SetTypeWithAnnotations(type, inferenceDiagnostics);
                inferenceDiagnostics.Free();

                return new BoundFieldAccessExpression(
                    syntax,
                    receiver,
                    fieldSymbol,
                    null,
                    type: type.type,
                    hasErrors: hasErrors || inferenceFailed
                );
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }
    }

    internal BoundExpression FailInference(Binder binder, BelteDiagnosticQueue diagnostics) {
        return SetInferredTypeWithAnnotations(new TypeWithAnnotations(null), binder, diagnostics);
    }

    private void ReportInferenceFailure(BelteDiagnosticQueue diagnostics) {
        var identifier = ((DeclarationExpressionSyntax)syntax).identifier;

        diagnostics.Push(Error.TypeInferenceFailedForOut(identifier.location, identifier.text));
    }
}
