using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
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
            SyntaxNode forbiddenZone)
            : base(
                containingSymbol,
                scopeBinder,
                allowRefKind: false,
                typeSyntax,
                identifierToken,
                modifiers) {
            _nodeBinder = nodeBinder;
            _nodeToBind = nodeToBind;
            this.forbiddenZone = forbiddenZone;
        }

        internal override SyntaxNode forbiddenZone { get; }

        internal override BelteDiagnostic forbiddenDiagnostic => null;

        private protected override TypeWithAnnotations InferTypeOfImplicit(BelteDiagnosticQueue diagnostics) {
            switch (_nodeToBind.kind) {
                case SyntaxKind.ArgumentList:
                    switch (_nodeToBind.parent) {
                        case ConstructorInitializerSyntax ctorInitializer:
                            _nodeBinder.BindConstructorInitializer(ctorInitializer, diagnostics);
                            break;
                        default:
                            throw ExceptionUtilities.UnexpectedValue(_nodeToBind.parent);
                    }
                    break;
                default:
                    _nodeBinder.BindExpression((ExpressionSyntax)_nodeToBind, diagnostics);
                    break;
            }

            if (_type is null)
                SetTypeWithAnnotations(new TypeWithAnnotations(_nodeBinder.CreateErrorType("var")));

            return _type;
        }
    }
}
