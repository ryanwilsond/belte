using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceDataContainerSymbol {
    private sealed class DeconstructionLocalSymbol : SourceDataContainerSymbol {
        private readonly SyntaxNode _deconstruction;
        private readonly Binder _nodeBinder;

        internal DeconstructionLocalSymbol(
            Symbol containingSymbol,
            Binder scopeBinder,
            Binder nodeBinder,
            TypeSyntax typeSyntax,
            SyntaxToken identifierToken,
            DataContainerDeclarationKind declarationKind,
            SyntaxNode deconstruction)
            : base(
                containingSymbol,
                scopeBinder,
                allowRefKind: false,
                typeSyntax,
                identifierToken,
                SyntaxTokenList.Empty) {
            _deconstruction = deconstruction;
            _nodeBinder = nodeBinder;
            this.declarationKind = declarationKind;
        }

        internal override DataContainerDeclarationKind declarationKind { get; }

        internal override SyntaxNode forbiddenZone {
            get {
                switch (_deconstruction.kind) {
                    case SyntaxKind.AssignmentExpression:
                        return _deconstruction;
                    // TODO This should be set to the declaration if we start using one
                    // case SyntaxKind.ForEachStatement:
                    //     return (ForEachStatementSyntax)_deconstruction;
                    default:
                        return null;
                }
            }
        }

        private protected override TypeWithAnnotations InferTypeOfImplicit(BelteDiagnosticQueue diagnostics) {
            switch (_deconstruction.kind) {
                case SyntaxKind.AssignmentExpression:
                    throw ExceptionUtilities.Unreachable();
                // var assignment = (AssignmentExpressionSyntax)_deconstruction;
                // DeclarationExpressionSyntax declaration = null;
                // ExpressionSyntax expression = null;
                // _nodeBinder.BindDeconstruction(assignment, assignment.Left, assignment.Right, diagnostics, ref declaration, ref expression);
                // break;
                case SyntaxKind.ForEachStatement:
                    _nodeBinder.BindForEachDeconstruction(diagnostics, _nodeBinder);
                    break;
                default:
                    return new TypeWithAnnotations(_nodeBinder.CreateErrorType());
            }

            return _type;
        }
    }
}
