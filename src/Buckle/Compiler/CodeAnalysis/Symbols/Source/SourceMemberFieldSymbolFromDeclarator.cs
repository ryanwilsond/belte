using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceMemberFieldSymbolFromDeclarator : SourceMemberFieldSymbol {
    private TypeAndRefKind _lazyTypeAndRefKind;

    internal SourceMemberFieldSymbolFromDeclarator(
        NamedTypeSymbol containingType,
        VariableDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        bool modifierErrors,
        BelteDiagnosticQueue diagnostics)
        : base(containingType, modifiers, declaration.identifier.text, new SyntaxReference(declaration)) {
        hasInitializer = declaration.initializer is not null;

        CheckAccessibility(diagnostics);

        if (!modifierErrors)
            ReportModifiersDiagnostics(diagnostics);
    }

    public sealed override RefKind refKind => GetTypeAndRefKind(ConsList<FieldSymbol>.Empty).refKind;

    internal sealed override bool hasInitializer { get; }

    private protected sealed override TypeSyntax _typeSyntax => _variableDeclaration.type;

    private protected sealed override SyntaxTokenList _modifiersTokenList
        => GetFieldDeclaration(_variableDeclaration).modifiers;

    private protected VariableDeclarationSyntax _variableDeclaration => (VariableDeclarationSyntax)syntaxNode;

    internal sealed override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return GetTypeAndRefKind(fieldsBeingBound).type;
    }

    internal override void AfterAddingTypeMembersChecks(BelteDiagnosticQueue diagnostics) {
        type.CheckAllConstraints(declaringCompilation, errorLocation, diagnostics);
        base.AfterAddingTypeMembersChecks(diagnostics);
    }

    internal bool FieldTypeInferred(ConsList<FieldSymbol> fieldsBeingBound) {
        // TODO Currently unnecessary method but helpful if we decide to allow implicitly typed fields
        GetFieldType(fieldsBeingBound);
        return false;
    }

    private protected override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        if (((SourceMemberContainerTypeSymbol)containingType).anyMemberHasAttributes)
            return OneOrMany.Create(GetFieldDeclaration(syntaxNode).attributeLists);

        return OneOrMany<SyntaxList<AttributeListSyntax>>.Empty;
    }

    private protected sealed override ConstantValue MakeConstantValue(
        HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
        BelteDiagnosticQueue diagnostics) {
        if (!isConstExpr)
            return null;

        if (_variableDeclaration.initializer is null)
            return new ConstantValue(null, type.specialType);

        return ConstantValueHelpers.EvaluateFieldConstant(
            this,
            _variableDeclaration.initializer,
            dependencies,
            diagnostics
        );
    }

    private static FieldDeclarationSyntax GetFieldDeclaration(BelteSyntaxNode declaration) {
        return (FieldDeclarationSyntax)declaration.parent;
    }

    private TypeAndRefKind GetTypeAndRefKind(ConsList<FieldSymbol> _1) {
        // TODO Use unused parameter to create recursive field errors
        if (_lazyTypeAndRefKind is not null)
            return _lazyTypeAndRefKind;

        var declaration = _variableDeclaration;
        var typeSyntax = declaration.type;
        var compilation = declaringCompilation;

        var diagnostics = BelteDiagnosticQueue.GetInstance();
        TypeWithAnnotations type;

        var binderFactory = compilation.GetBinderFactory(syntaxTree);
        var binder = binderFactory.GetBinder(typeSyntax);
        binder = binder.WithAdditionalFlagsAndContainingMember(BinderFlags.SuppressConstraintChecks, this);

        var typeOnly = typeSyntax.SkipRef(out var refKind);

        type = binder.BindTypeOrImplicitType(typeOnly, diagnostics, out var isImplicitlyTyped);

        if (isImplicitlyTyped) {
            var location = typeOnly is IdentifierNameSyntax
                ? typeOnly.location
                : ((FieldDeclarationSyntax)syntaxNode.parent).modifiers
                    .Last(m => m.kind is SyntaxKind.ConstexprKeyword or SyntaxKind.ConstKeyword).location;

            diagnostics.Push(Error.FieldsCannotBeImplicitlyTyped(location));
            type = new TypeWithAnnotations(binder.CreateErrorType());
        }

        if (Interlocked.CompareExchange(ref _lazyTypeAndRefKind, new TypeAndRefKind(refKind, type), null) is null) {
            TypeChecks(type.type, diagnostics);
            AddDeclarationDiagnostics(diagnostics);
            _state.NotePartComplete(CompletionParts.Type);
        }

        diagnostics.Free();
        return _lazyTypeAndRefKind;
    }
}
