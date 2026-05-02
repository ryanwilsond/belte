using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class GlobalExpressionVariable : SourceMemberFieldSymbol {
    private TypeWithAnnotations _lazyType;

    private readonly SyntaxReference _typeSyntaxOpt;

    internal GlobalExpressionVariable(
        SourceMemberContainerTypeSymbol containingType,
        DeclarationModifiers modifiers,
        TypeSyntax typeSyntax,
        string name,
        SyntaxReference syntax,
        TextLocation location)
        : base(containingType, modifiers, name, syntax) {
        _typeSyntaxOpt = typeSyntax is null ? null : new SyntaxReference(typeSyntax);
    }

    internal static GlobalExpressionVariable Create(
        SourceMemberContainerTypeSymbol containingType,
        DeclarationModifiers modifiers,
        TypeSyntax typeSyntax,
        string name,
        SyntaxNode syntax,
        TextLocation location,
        FieldSymbol containingFieldOpt,
        SyntaxNode nodeToBind) {

        var syntaxReference = new SyntaxReference(syntax);

        return (typeSyntax is null || typeSyntax.SkipRef(out _).isImplicitlyTyped)
            ? new InferrableGlobalExpressionVariable(
                containingType,
                modifiers,
                typeSyntax,
                name,
                syntaxReference,
                location,
                containingFieldOpt,
                nodeToBind)
            : new GlobalExpressionVariable(containingType, modifiers, typeSyntax, name, syntaxReference, location);
    }

    private protected override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() => OneOrMany<SyntaxList<AttributeListSyntax>>.Empty;

    private protected override TypeSyntax _typeSyntax => _typeSyntaxOpt?.node as TypeSyntax;

    private protected override SyntaxTokenList _modifiersTokenList => default;

    internal override bool hasInitializer => false;

    public sealed override RefKind refKind => RefKind.None;

    private protected override ConstantValue MakeConstantValue(
        HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
        BelteDiagnosticQueue diagnostics) {
        return null;
    }

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        if (_lazyType is not null)
            return _lazyType;

        var typeSyntax = _typeSyntax;

        var compilation = declaringCompilation;

        var diagnostics = BelteDiagnosticQueue.GetInstance();
        TypeWithAnnotations type;
        bool isVar;
        bool isNonNullable;
        bool isNullable;

        var binderFactory = compilation.GetBinderFactory(syntaxTree);
        var binder = binderFactory.GetBinder(typeSyntax ?? syntaxNode);

        if (typeSyntax is not null) {
            type = binder.BindTypeOrImplicitType(
                typeSyntax.SkipRef(out _),
                diagnostics,
                out isVar,
                out isNonNullable,
                out isNullable
            );
        } else {
            isVar = true;
            isNonNullable = false;
            isNullable = false;
            type = default;
        }

        if (isVar && !fieldsBeingBound.ContainsReference(this)) {
            InferFieldType(fieldsBeingBound, binder);
        } else {
            if (isVar) {
                throw ExceptionUtilities.Unreachable();
                // diagnostics.Add(ErrorCode.ERR_RecursivelyTypedVariable, this.ErrorLocation, this);
                // type = new TypeWithAnnotations(binder.CreateErrorType("var"));
            }

            SetType(diagnostics, type);
        }

        diagnostics.Free();
        return _lazyType;
    }

    private TypeWithAnnotations SetType(BelteDiagnosticQueue diagnostics, TypeWithAnnotations type) {
        if (Interlocked.CompareExchange(ref _lazyType, type, null) is null) {
            TypeChecks(type.type, diagnostics);
            AddDeclarationDiagnostics(diagnostics);
            _state.NotePartComplete(CompletionParts.Type);
        }

        return _lazyType;
    }

    internal TypeWithAnnotations SetTypeWithAnnotations(TypeWithAnnotations type, BelteDiagnosticQueue diagnostics) {
        return SetType(diagnostics, type);
    }

    private protected virtual void InferFieldType(ConsList<FieldSymbol> fieldsBeingBound, Binder binder) {
        throw ExceptionUtilities.Unreachable();
    }
}
