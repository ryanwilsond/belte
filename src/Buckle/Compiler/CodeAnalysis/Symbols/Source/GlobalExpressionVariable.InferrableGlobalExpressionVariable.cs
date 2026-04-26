using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class GlobalExpressionVariable {
    private class InferrableGlobalExpressionVariable : GlobalExpressionVariable {
        private readonly FieldSymbol _containingFieldOpt;
        private readonly SyntaxReference _nodeToBind;

        internal InferrableGlobalExpressionVariable(
            SourceMemberContainerTypeSymbol containingType,
            DeclarationModifiers modifiers,
            TypeSyntax typeSyntax,
            string name,
            SyntaxReference syntax,
            TextLocation location,
            FieldSymbol containingFieldOpt,
            SyntaxNode nodeToBind)
            : base(containingType, modifiers, typeSyntax, name, syntax, location) {
            _containingFieldOpt = containingFieldOpt;
            _nodeToBind = new SyntaxReference(nodeToBind);
        }

        private protected override void InferFieldType(ConsList<FieldSymbol> fieldsBeingBound, Binder binder) {
            var nodeToBind = _nodeToBind.node;

            // TODO Double check VariableDeclaration is the right node
            if (_containingFieldOpt is not null && nodeToBind.kind != SyntaxKind.VariableDeclaration) {
                binder = binder.WithContainingMember(_containingFieldOpt)
                    .WithAdditionalFlags(BinderFlags.FieldInitializer);
            }

            fieldsBeingBound = new ConsList<FieldSymbol>(this, fieldsBeingBound);

            binder = new ImplicitlyTypedFieldBinder(binder, fieldsBeingBound);

            switch (nodeToBind.kind) {
                // TODO See above
                // case SyntaxKind.VariableDeclaration:
                //     binder.BindDeclaratorArguments((VariableDeclaratorSyntax)nodeToBind, BindingDiagnosticBag.Discarded);
                //     break;
                default:
                    binder.BindExpression((ExpressionSyntax)nodeToBind, BelteDiagnosticQueue.Discarded);
                    break;
            }
        }
    }
}
