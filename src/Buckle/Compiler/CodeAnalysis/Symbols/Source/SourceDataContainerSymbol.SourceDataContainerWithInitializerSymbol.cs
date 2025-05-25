using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceDataContainerSymbol {
    private sealed class SourceDataContainerWithInitializerSymbol : SourceDataContainerSymbol {
        private readonly EqualsValueClauseSyntax _initializer;
        private readonly Binder _initializerBinder;

        private ConstantValue _lazyConstantValue;

        internal SourceDataContainerWithInitializerSymbol(
            Symbol containingSymbol,
            Binder scopeBinder,
            TypeSyntax typeSyntax,
            SyntaxToken identifierToken,
            EqualsValueClauseSyntax initializer,
            Binder initializerBinder,
            DataContainerDeclarationKind declarationKind,
            SyntaxTokenList modifiers)
            : base(containingSymbol, scopeBinder, true, typeSyntax, identifierToken, declarationKind, modifiers) {
            _initializer = initializer;
            _initializerBinder = initializerBinder;
        }

        internal override SyntaxNode forbiddenZone => _initializer;

        internal override ConstantValue GetConstantValue(
            SyntaxNode node,
            DataContainerSymbol inProgress,
            BelteDiagnosticQueue diagnostics) {
            if (isConstExpr && inProgress == this) {
                diagnostics?.Push(Error.CircularConstantValue(node.location, this));
                return null;
            }

            MakeConstantValue(inProgress, null);
            return _lazyConstantValue;
        }

        internal override BelteDiagnosticQueue GetConstantValueDiagnostics(BoundExpression boundInitValue) {
            MakeConstantValue(null, boundInitValue);
            return _lazyConstantValue is null
                ? BelteDiagnosticQueue.Discarded
                : new BelteDiagnosticQueue(_lazyConstantValue.diagnostics);
        }

        private protected override TypeWithAnnotations InferTypeOfImplicit(BelteDiagnosticQueue diagnostics) {
            var initializer = _initializerBinder.BindInferredDataContainerInitializer(diagnostics, refKind, _initializer, _initializer);
            return new TypeWithAnnotations(initializer.type);
        }

        private void MakeConstantValue(DataContainerSymbol inProgress, BoundExpression boundInitValue) {
            if (isConstExpr && _lazyConstantValue is null) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();
                var type = this.type;

                if (boundInitValue is null) {
                    var inProgressBinder = new LocalInProgressBinder(this, _initializerBinder);
                    boundInitValue = inProgressBinder.BindVariableOrAutoPropInitializerValue(
                        _initializer,
                        refKind,
                        type,
                        diagnostics
                    );
                }

                var value = ConstantValueHelpers.GetAndValidateConstantValue(
                    boundInitValue,
                    this,
                    type,
                    _initializer.value,
                    diagnostics
                );

                Interlocked.CompareExchange(
                    ref _lazyConstantValue,
                    new ConstantValue(value.value, type.specialType, diagnostics.ToArrayAndFree()),
                    null
                );
            }
        }
    }
}
