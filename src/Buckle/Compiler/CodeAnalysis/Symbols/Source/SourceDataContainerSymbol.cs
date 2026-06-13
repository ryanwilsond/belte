using System;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceDataContainerSymbol : DataContainerSymbol, IAttributeTargetSymbol {
    private readonly TypeSyntax _typeSyntax;
    private readonly BelteDiagnosticQueue _declarationDiagnostics;

    private TypeWithAnnotations _type;
    private CustomAttributesBag<AttributeData> _lazyAttributeBag;

    [ThreadStatic] private static PooledHashSet<LocalTypeInferenceInProgressKey> LocalTypeInferenceInProgress;
    private ConcurrentSet<SyntaxNode> _forbiddenReferences;

    private SourceDataContainerSymbol(
        Symbol containingSymbol,
        Binder scopeBinder,
        bool allowRefKind,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        SyntaxTokenList modifiers,
        DataContainerDeclarationKind? kind = null) {
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

        if (kind is not null)
            declarationKind = kind.Value;

        this.isPinned = isPinned;
        GetAttributes();
    }

    public override string name => identifierToken.valueText;

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

    internal override TypeWithAnnotations typeWithAnnotations
        => GetTypeWithAnnotations(SyntaxTree.Dummy.GetRoot(), BelteDiagnosticQueue.Discarded);

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
                    out _,
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
        SyntaxNode nodeToBind = null) {
        if (nodeBinder is not null) {
            return new LocalSymbolWithEnclosingContext(
                containingSymbol,
                scopeBinder,
                nodeBinder,
                typeSyntax,
                identifierToken,
                modifiers,
                nodeToBind
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

    internal static SourceDataContainerSymbol MakeLocalSymbolWithEnclosingContext(
        Symbol containingSymbol,
        Binder scopeBinder,
        Binder nodeBinder,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        DataContainerDeclarationKind kind,
        SyntaxNode nodeToBind) {
        return new LocalSymbolWithEnclosingContext(
            containingSymbol,
            scopeBinder,
            nodeBinder,
            typeSyntax,
            identifierToken,
            null,
            nodeToBind,
            kind
        );
    }

    internal static SourceDataContainerSymbol MakeDeconstructionLocal(
        Symbol containingSymbol,
        Binder scopeBinder,
        Binder nodeBinder,
        TypeSyntax closestTypeSyntax,
        SyntaxToken identifierToken,
        DataContainerDeclarationKind kind,
        SyntaxNode deconstruction) {
        return closestTypeSyntax is null || closestTypeSyntax.SkipRef(out _).isImplicitlyTyped
            ? new DeconstructionLocalSymbol(
                containingSymbol,
                scopeBinder,
                nodeBinder,
                closestTypeSyntax,
                identifierToken,
                kind,
                deconstruction)
            : new SourceDataContainerSymbol(
                containingSymbol,
                scopeBinder,
                allowRefKind: false,
                closestTypeSyntax,
                identifierToken,
                SyntaxTokenList.Empty);
    }

    internal override TypeWithAnnotations GetTypeWithAnnotations(
        SyntaxNode reference,
        BelteDiagnosticQueue diagnostics) {
        if (_forbiddenReferences?.Contains(reference) == true) {
            diagnostics.Push(GetForbiddenDiagnostic(reference.location));
            return new TypeWithAnnotations(declaringCompilation.implicitlyTypedVariableUsedInForbiddenZoneType);
        }

        if (_type is null) {
            bool isImplicitlyTyped;
            bool isNonNullable;
            bool isNullable;
            TypeWithAnnotations declarationType;

            if (_typeSyntax is null) {
                isImplicitlyTyped = true;
                isNonNullable = false;
                isNullable = false;
                declarationType = default;
            } else {
                declarationType = scopeBinder.BindTypeOrImplicitType(
                    _typeSyntax.SkipRef(out _),
                    diagnostics,
                    out isImplicitlyTyped,
                    out isNonNullable,
                    out isNullable
                );
            }

            if (isImplicitlyTyped) {
                var free = false;
                var localTypeInferenceInProgress = LocalTypeInferenceInProgress;

                if (localTypeInferenceInProgress is null) {
                    free = true;
                    localTypeInferenceInProgress =
                        LocalTypeInferenceInProgress = PooledHashSet<LocalTypeInferenceInProgressKey>.GetInstance();
                }

                var key = new LocalTypeInferenceInProgressKey(this, reference);

                if (!localTypeInferenceInProgress.Add(key)) {
                    if (_forbiddenReferences is null)
                        Interlocked.CompareExchange(ref _forbiddenReferences, [], null);

                    _forbiddenReferences.Add(reference);
                    diagnostics.Push(GetForbiddenDiagnostic(reference.location));
                    return new TypeWithAnnotations(declaringCompilation.implicitlyTypedVariableUsedInForbiddenZoneType);
                }

                TypeWithAnnotations inferredType;

                try {
                    inferredType = InferTypeOfImplicit();
                } finally {
                    localTypeInferenceInProgress.Remove(key);

                    if (free) {
                        LocalTypeInferenceInProgress = null;
                        localTypeInferenceInProgress.Free();
                    }
                }

                if (_forbiddenReferences?.Contains(reference) == true) {
                    diagnostics.Push(GetForbiddenDiagnostic(reference.location));
                    return new TypeWithAnnotations(declaringCompilation.implicitlyTypedVariableUsedInForbiddenZoneType);
                }

                if (inferredType.hasType && !inferredType.IsVoidType()) {
                    if (isNonNullable && inferredType.IsNullableType())
                        inferredType = new TypeWithAnnotations(inferredType.nullableUnderlyingTypeOrSelf);

                    if (isNullable && !inferredType.IsNullableType())
                        inferredType = inferredType.SetIsAnnotated();

                    declarationType = inferredType;
                } else {
                    declarationType = new TypeWithAnnotations(
                        declaringCompilation.implicitlyTypedVariableInferenceFailedType
                    );
                }
            }

            SetTypeWithAnnotations(declarationType);
            return _type ?? declarationType;
        }

        return _type;
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
        return OneOrMany.Create((GetDeclarationSyntax().parent as LocalDeclarationStatementSyntax)?.attributeLists);
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
        if (_type is null &&
            (newType.type != (object)declaringCompilation.implicitlyTypedVariableInferenceFailedType ||
                 (LocalTypeInferenceInProgress?.Any(static (key, @this) => key.local == (object)@this, this) != true))) {
            Interlocked.CompareExchange(ref _type, newType, null);
        }
    }

    private protected virtual TypeWithAnnotations InferTypeOfImplicit() {
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

    private DataContainerDeclarationKind MakeModifiers(
        SyntaxTokenList modifiers,
        BelteDiagnosticQueue diagnostics,
        out bool isPinned) {
        var allowedModifiers = DeclarationModifiers.Const |
                               DeclarationModifiers.ConstExpr |
                               DeclarationModifiers.Final |
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
        var isFinal = (result & DeclarationModifiers.Final) != 0;
        var isConstExpr = (result & DeclarationModifiers.ConstExpr) != 0;
        var declarationKind = isConstExpr
            ? DataContainerDeclarationKind.ConstantExpression
            : (isConst
                ? DataContainerDeclarationKind.Constant
                : (isFinal
                    ? DataContainerDeclarationKind.Final
                    : DataContainerDeclarationKind.Variable));

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
