using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceDataContainerSymbol : DataContainerSymbol, IAttributeTargetSymbol {
    private readonly TypeSyntax _typeSyntax;
    private readonly BelteDiagnosticQueue _declarationDiagnostics;

    private TypeWithAnnotations _type;
    private CustomAttributesBag<AttributeData> _lazyAttributeBag;

    private SourceDataContainerSymbol(
        Symbol containingSymbol,
        Binder scopeBinder,
        bool allowRefKind,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        SyntaxTokenList modifiers) {
        this.containingSymbol = containingSymbol;
        this.scopeBinder = scopeBinder;
        this.identifierToken = identifierToken;
        _typeSyntax = typeSyntax;
        _declarationDiagnostics = new BelteDiagnosticQueue();

        if (allowRefKind) {
            typeSyntax.SkipRef(out var refKind);
            this.refKind = refKind;
        }

        scope = refKind == RefKind.None ? ScopedKind.Value : ScopedKind.Ref;

        declarationKind = MakeModifiers(modifiers, _declarationDiagnostics, out var isPinned);
        this.isPinned = isPinned;
        GetAttributes();
    }

    public override string name => identifierToken.text;

    public override RefKind refKind { get; }

    internal Binder scopeBinder { get; }

    internal override Symbol containingSymbol { get; }

    internal override SyntaxNode scopeDesignator => scopeBinder.scopeDesignator;

    internal override DataContainerDeclarationKind declarationKind { get; }

    internal override ScopedKind scope { get; }

    internal override SyntaxToken identifierToken { get; }

    internal override bool hasSourceLocation => true;

    internal override SyntaxReference syntaxReference => new SyntaxReference(GetDeclarationSyntax());

    internal override TextLocation location => identifierToken.location;

    internal override bool isPinned { get; }

    internal override bool isCompilerGenerated => false;

    IAttributeTargetSymbol IAttributeTargetSymbol.attributesOwner => this;

    AttributeLocation IAttributeTargetSymbol.defaultAttributeLocation => AttributeLocation.Parameter;

    AttributeLocation IAttributeTargetSymbol.allowedAttributeLocations => AttributeLocation.Parameter;

    internal override TypeWithAnnotations typeWithAnnotations {
        get {
            if (_type is null) {
                var localType = GetTypeSymbol();
                SetTypeWithAnnotations(localType);
            }

            return _type;
        }
    }

    internal override SynthesizedLocalKind synthesizedKind => SynthesizedLocalKind.UserDefined;

    internal bool isImplicitlyTyped {
        get {
            if (_typeSyntax is null)
                return true;

            var typeSyntax = _typeSyntax.SkipRef(out _);

            if (typeSyntax.isImplicitlyTyped) {
                scopeBinder.BindTypeOrImplicitType(
                    typeSyntax,
                    BelteDiagnosticQueue.Discarded,
                    out var result,
                    out _
                );

                return result;
            }

            return false;
        }
    }

    internal static SourceDataContainerSymbol MakeLocal(
        Symbol containingSymbol,
        Binder scopeBinder,
        bool allowRefKind,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        EqualsValueClauseSyntax initializer,
        SyntaxTokenList modifiers,
        Binder initializerBinder = null,
        Binder nodeBinder = null,
        SyntaxNode nodeToBind = null,
        SyntaxNode forbiddenZone = null) {
        if (nodeBinder is not null) {
            return new LocalSymbolWithEnclosingContext(
                containingSymbol,
                scopeBinder,
                nodeBinder,
                typeSyntax,
                identifierToken,
                modifiers,
                nodeToBind,
                forbiddenZone
            );
        }

        return MakeDataContainer(
            containingSymbol,
            scopeBinder,
            allowRefKind,
            typeSyntax,
            identifierToken,
            initializer,
            modifiers,
            initializerBinder ?? scopeBinder
        );
    }

    internal sealed override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyAttributeBag;

        if (bag is not null && bag.isSealed)
            return bag;

        LoadAndValidateAttributes(
            GetAttributeDeclarations(),
            ref _lazyAttributeBag
        );

        return _lazyAttributeBag;
    }

    private OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(((LocalDeclarationStatementSyntax)GetDeclarationSyntax().parent).attributeLists);
    }

    internal sealed override SyntaxNode GetDeclarationSyntax() {
        return identifierToken.parent;
    }

    internal override ConstantValue GetConstantValue(
        SyntaxNode node,
        DataContainerSymbol inProgress,
        BelteDiagnosticQueue diagnostics) {
        return null;
    }

    internal override BelteDiagnosticQueue GetConstantValueDiagnostics(BoundExpression boundInitValue) {
        return BelteDiagnosticQueue.Discarded;
    }

    internal void GetDeclarationDiagnostics(BelteDiagnosticQueue addTo) {
        addTo.PushRange(_declarationDiagnostics);
    }

    internal void SetTypeWithAnnotations(TypeWithAnnotations newType) {
        if (_type is null)
            Interlocked.CompareExchange(ref _type, newType, null);
    }

    private protected virtual TypeWithAnnotations InferTypeOfImplicit(BelteDiagnosticQueue diagnostics) {
        return _type;
    }

    private static SourceDataContainerSymbol MakeDataContainer(
        Symbol containingSymbol,
        Binder scopeBinder,
        bool allowRefKind,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        EqualsValueClauseSyntax initializer,
        SyntaxTokenList modifiers,
        Binder initializerBinder) {
        return initializer is null
            ? new SourceDataContainerSymbol(
                containingSymbol,
                scopeBinder,
                allowRefKind,
                typeSyntax,
                identifierToken,
                modifiers
              )
            : new SourceDataContainerWithInitializerSymbol(
                containingSymbol,
                scopeBinder,
                typeSyntax,
                identifierToken,
                initializer,
                initializerBinder,
                modifiers
              );
    }

    private TypeWithAnnotations GetTypeSymbol() {
        var diagnostics = BelteDiagnosticQueue.Discarded;

        bool isImplicitlyTyped;
        bool isNonNullable;
        TypeWithAnnotations declarationType;

        if (_typeSyntax is null) {
            isImplicitlyTyped = true;
            isNonNullable = false;
            declarationType = default;
        } else {
            declarationType = scopeBinder.BindTypeOrImplicitType(
                _typeSyntax.SkipRef(out _),
                diagnostics,
                out isImplicitlyTyped,
                out isNonNullable
            );
        }

        if (isImplicitlyTyped) {
            var inferredType = InferTypeOfImplicit(diagnostics);

            if (inferredType.hasType && !inferredType.IsVoidType()) {
                if (isNonNullable && inferredType.IsNullableType())
                    inferredType = new TypeWithAnnotations(inferredType.nullableUnderlyingTypeOrSelf);

                declarationType = inferredType;
            } else {
                declarationType = new TypeWithAnnotations(scopeBinder.CreateErrorType("var"));
            }
        }

        return declarationType;
    }

    private DataContainerDeclarationKind MakeModifiers(
        SyntaxTokenList modifiers,
        BelteDiagnosticQueue diagnostics,
        out bool isPinned) {
        var allowedModifiers = DeclarationModifiers.Const |
                               DeclarationModifiers.ConstExpr |
                               DeclarationModifiers.Pinned;

        var result = ModifierHelpers.CreateAndCheckNonTypeMemberModifiers(
            modifiers,
            DeclarationModifiers.None,
            allowedModifiers,
            location,
            diagnostics,
            out var hasErrors
        );

        isPinned = (result & DeclarationModifiers.Pinned) != 0;
        var isConst = (result & DeclarationModifiers.Const) != 0;
        var isConstExpr = (result & DeclarationModifiers.ConstExpr) != 0;
        var declarationKind = isConstExpr
            ? DataContainerDeclarationKind.ConstantExpression
            : (isConst
                ? DataContainerDeclarationKind.Constant
                : DataContainerDeclarationKind.Variable);

        if (hasErrors)
            return declarationKind;

        if (refKind != RefKind.None && isConstExpr)
            diagnostics.Push(Error.CannotBeRefAndConstexpr(location));
        else if (isConst && isConstExpr)
            diagnostics.Push(Error.ConflictingModifiers(location, "const", "constexpr"));

        return declarationKind;
    }

    internal sealed override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if ((object)obj == this)
            return true;

        return obj is SourceDataContainerSymbol symbol
            && symbol.identifierToken.Equals(identifierToken)
            && symbol.containingSymbol.Equals(containingSymbol, compareKind);
    }

    public sealed override int GetHashCode() {
        return Hash.Combine(identifierToken.GetHashCode(), containingSymbol.GetHashCode());
    }
}
