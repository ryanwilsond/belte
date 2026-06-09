using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceDataContainerSymbol {
    private sealed class LocalSymbolWithEnclosingContext : SourceDataContainerSymbol {
        private readonly Binder _nodeBinder;
        private readonly SyntaxNode _nodeToBind;

        internal LocalSymbolWithEnclosingContext(
            Symbol containingSymbol,
            Binder scopeBinder,
            Binder nodeBinder,
            TypeSyntax typeSyntax,
            SyntaxToken identifierToken,
            SyntaxTokenList modifiers,
            SyntaxNode nodeToBind,
            DataContainerDeclarationKind? kind = null)
            : base(
                containingSymbol,
                scopeBinder,
                allowRefKind: false,
                typeSyntax,
                identifierToken,
                modifiers,
                kind) {
            _nodeBinder = nodeBinder;
            _nodeToBind = nodeToBind;
        }

        internal override BelteDiagnostic GetForbiddenDiagnostic(TextLocation location) {
            return null;
        }

        private protected override TypeWithAnnotations InferTypeOfImplicit() {
            switch (_nodeToBind.kind) {
                case SyntaxKind.ConstructorInitializer:
                    var initializer = (ConstructorInitializerSyntax)_nodeToBind;
                    _nodeBinder.BindConstructorInitializer(initializer, BelteDiagnosticQueue.Discarded);
                    break;
                case SyntaxKind.ArgumentList:
                    switch (_nodeToBind.parent) {
                        case ConstructorInitializerSyntax ctorInitializer:
                            _nodeBinder.BindConstructorInitializer(ctorInitializer, BelteDiagnosticQueue.Discarded);
                            break;
                        default:
                            throw ExceptionUtilities.UnexpectedValue(_nodeToBind.parent);
                    }
                    break;
                case SyntaxKind.GotoStatement:
                    _nodeBinder.BindStatement((GotoStatementSyntax)_nodeToBind, BelteDiagnosticQueue.Discarded);
                    break;
                default:
                    _nodeBinder.BindExpression((ExpressionSyntax)_nodeToBind, BelteDiagnosticQueue.Discarded);
                    break;
            }

            if (_type is null) {
                SetTypeWithAnnotations(
                    new TypeWithAnnotations(declaringCompilation.implicitlyTypedVariableInferenceFailedType)
                );
            }

            return _type;
        }
    }
}
