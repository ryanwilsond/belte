using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceNamedTypeSymbol : SourceMemberContainerTypeSymbol, IAttributeTargetSymbol {
    private ImmutableArray<ExpressionSyntax> _unboundConstraints;
    private ImmutableArray<BoundExpression> _lazyTemplateConstraints;
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;
    private NamedTypeSymbol _lazyDeclaredBase;
    private NamedTypeSymbol _lazyBaseType = ErrorTypeSymbol.UnknownResultType;
    private TemplateParameterInfo _lazyTemplateParameterInfo;

    internal SourceNamedTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        MergedTypeDeclaration declaration,
        BelteDiagnosticQueue diagnostics)
        : base(containingSymbol, declaration, diagnostics) { }

    private TemplateParameterInfo _templateParameterInfo {
        get {
            if (_lazyTemplateParameterInfo is null) {
                var templateParameterInfo = (arity == 0) ? TemplateParameterInfo.Empty : new TemplateParameterInfo();
                Interlocked.CompareExchange(ref _lazyTemplateParameterInfo, templateParameterInfo, null);
            }

            return _lazyTemplateParameterInfo;
        }
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters {
        get {
            if (_templateParameterInfo.lazyTemplateParameters.IsDefault) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                if (ImmutableInterlocked.InterlockedInitialize(
                    ref _templateParameterInfo.lazyTemplateParameters,
                    MakeTemplateParameters(diagnostics))) {
                    AddDeclarationDiagnostics(diagnostics);
                }

                diagnostics.Free();
            }

            return _templateParameterInfo.lazyTemplateParameters;
        }
    }

    public override ImmutableArray<BoundExpression> templateConstraints {
        get {
            if (_lazyTemplateConstraints.IsDefault) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyTemplateConstraints,
                    MakeTemplateConstraints(diagnostics)
                );

                AddDeclarationDiagnostics(diagnostics);
                diagnostics.Free();
            }

            return _lazyTemplateConstraints;
        }
    }

    public override ImmutableArray<TypeOrConstant> templateArguments
        => GetTemplateParametersAsTemplateArguments();

    IAttributeTargetSymbol IAttributeTargetSymbol.attributesOwner => this;

    AttributeLocation IAttributeTargetSymbol.defaultAttributeLocation => AttributeLocation.Type;

    AttributeLocation IAttributeTargetSymbol.allowedAttributeLocations {
        get {
            switch (typeKind) {
                case TypeKind.Struct:
                case TypeKind.Class:
                    return AttributeLocation.Type;
                default:
                    return AttributeLocation.None;
            }
        }
    }

    internal override NamedTypeSymbol baseType {
        get {
            if (ReferenceEquals(_lazyBaseType, ErrorTypeSymbol.UnknownResultType)) {
                if (containingType is not null)
                    _ = containingType.baseType;

                if (specialType == SpecialType.Object) {
                    Interlocked.CompareExchange(ref _lazyBaseType, null, ErrorTypeSymbol.UnknownResultType);
                    return _lazyBaseType;
                }

                var diagnostics = BelteDiagnosticQueue.GetInstance();
                var acyclicBase = MakeAcyclicBaseType(diagnostics);

                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _lazyBaseType, acyclicBase, ErrorTypeSymbol.UnknownResultType),
                    ErrorTypeSymbol.UnknownResultType)) {
                    AddDeclarationDiagnostics(diagnostics);
                }

                diagnostics.Free();
            }

            return _lazyBaseType;
        }
    }

    internal bool isSimpleProgram => _declaration.declarations.Any(static d => d.isSimpleProgram);

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        if (_lazyDeclaredBase is null) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            if (Interlocked.CompareExchange(
                ref _lazyDeclaredBase,
                MakeDeclaredBase(basesBeingResolved, diagnostics), null) is null) {
                AddDeclarationDiagnostics(diagnostics);
            }

            diagnostics.Free();
        }

        return _lazyDeclaredBase;
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        if (LoadAndValidateAttributes(OneOrMany.Create(GetAttributeDeclarations()), ref _lazyAttributesBag))
            _state.NotePartComplete(CompletionParts.Attributes);

        return _lazyAttributesBag;
    }

    internal sealed override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    internal ImmutableArray<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return _declaration.GetAttributeDeclarations();
    }

    internal ImmutableArray<TypeWithAnnotations> GetTypeParameterConstraintTypes(
        int ordinal,
        BelteDiagnosticQueue diagnostics) {
        var constraintTypes = GetTypeParameterConstraintTypes(diagnostics);
        return (constraintTypes.Length > 0) ? constraintTypes[ordinal] : [];
    }

    internal TypeParameterConstraintKinds GetTypeParameterConstraintKinds(int ordinal) {
        var constraintKinds = GetTypeParameterConstraintKinds();
        return (constraintKinds.Length > 0) ? constraintKinds[ordinal] : TypeParameterConstraintKinds.None;
    }

    private protected override void CheckBase(BelteDiagnosticQueue diagnostics) {
        var localBase = baseType;

        if (localBase is null)
            return;

        var singleDeclaration = FirstDeclarationWithExplicitBases();

        if (singleDeclaration is not null) {
            var location = singleDeclaration.nameLocation;
            localBase.CheckAllConstraints(location, diagnostics);
        }
    }

    private SingleTypeDeclaration FirstDeclarationWithExplicitBases() {
        foreach (var singleDeclaration in _declaration.declarations) {
            var bases = GetBaseListOpt(singleDeclaration);

            if (bases is not null)
                return singleDeclaration;
        }

        return null;
    }

    private static BaseTypeSyntax GetBaseListOpt(SingleTypeDeclaration decl) {
        if (decl.hasBaseDeclarations) {
            var typeDeclaration = (ClassDeclarationSyntax)decl.syntaxReference.node;
            return typeDeclaration.baseType;
        }

        return null;
    }

    private NamedTypeSymbol MakeAcyclicBaseType(BelteDiagnosticQueue diagnostics) {
        var typeKind = this.typeKind;
        var declaredBase = GetDeclaredBaseType(basesBeingResolved: null);

        if (declaredBase is null) {
            switch (typeKind) {
                case TypeKind.Class:
                    if (specialType == SpecialType.Object)
                        return null;

                    declaredBase = CorLibrary.GetSpecialType(SpecialType.Object);
                    break;
                case TypeKind.Struct:
                    declaredBase = null;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(typeKind);
            }
        }

        if (typeKind == TypeKind.Class && BaseTypeAnalysis.TypeDependsOn(declaredBase, this)) {
            var error = Error.CircularBase(location, declaredBase, this);
            diagnostics.Push(error);

            return new ExtendedErrorTypeSymbol(declaredBase, LookupResultKind.NotReferencable, error);
        }

        _hasNoBaseCycles = true;
        return declaredBase;
    }

    private ImmutableArray<BoundExpression> MakeTemplateConstraints(BelteDiagnosticQueue diagnostics) {
        _ = GetTypeParameterConstraintTypes(diagnostics);

        if (_unboundConstraints.IsDefault || _unboundConstraints.Length == 0)
            return [];

        var binderFactory = declaringCompilation.GetBinderFactory(syntaxReference.syntaxTree);
        var binder = binderFactory.GetBinder(_unboundConstraints[0]);
        binder = binder.WithAdditionalFlagsAndContainingMember(
            BinderFlags.TemplateConstraintsClause | BinderFlags.SuppressConstraintChecks,
            this
        );

        return binder.BindExpressionConstraints(_unboundConstraints, templateParameters, diagnostics);
    }

    private NamedTypeSymbol MakeDeclaredBase(
        ConsList<TypeSymbol> basesBeingResolved,
        BelteDiagnosticQueue diagnostics) {
        var decl = _declaration.declarations[0];
        var newBasesBeingResolved = basesBeingResolved.Prepend(originalDefinition);
        var baseType = MakeOneDeclaredBase(newBasesBeingResolved, decl, diagnostics);
        var baseTypeLocation = decl.nameLocation;

        if (baseType is not null) {
            if (baseType.isStatic)
                diagnostics.Push(Error.CannotDeriveStatic(baseTypeLocation, baseType));

            if (!IsNoMoreVisibleThan(baseType))
                diagnostics.Push(Error.InconsistentAccessibilityClass(baseTypeLocation, baseType, this));
        }

        return baseType;
    }

    private NamedTypeSymbol MakeOneDeclaredBase(
        ConsList<TypeSymbol> newBasesBeingResolved,
        SingleTypeDeclaration decl,
        BelteDiagnosticQueue diagnostics) {
        var baseSyntax = GetBaseListOpt(decl);

        if (baseSyntax is null)
            return null;

        NamedTypeSymbol localBase = null;
        var baseBinder = declaringCompilation.GetBinder(baseSyntax);
        baseBinder = baseBinder.WithAdditionalFlagsAndContainingMember(BinderFlags.SuppressConstraintChecks, this);
        var typeSyntax = baseSyntax.type;
        var location = typeSyntax.location;

        TypeSymbol baseType;

        if (typeKind == TypeKind.Class) {
            baseType = baseBinder.BindType(typeSyntax, diagnostics, newBasesBeingResolved).type.StrippedType();
            var baseSpecialType = baseType.specialType;

            if (IsRestrictedBaseType(baseSpecialType))
                diagnostics.Push(Error.CannotDerivePrimitive(location, baseType));

            if (baseType.isSealed && !isStatic)
                diagnostics.Push(Error.CannotDeriveSealed(location, baseType));

            if ((baseType.typeKind == TypeKind.Class) &&
                (localBase is null)) {
                localBase = (NamedTypeSymbol)baseType;

                if (isStatic && localBase.specialType != SpecialType.Object) {
                    var error = Error.StaticDeriveFromNotObject(location, this, localBase);
                    diagnostics.Push(error);
                    localBase = new ExtendedErrorTypeSymbol(localBase, LookupResultKind.NotReferencable, error);
                }
            }
        } else {
            baseType = baseBinder.BindType(typeSyntax, diagnostics, newBasesBeingResolved).type;
        }

        if (baseType.typeKind == TypeKind.TemplateParameter)
            diagnostics.Push(Error.CannotDeriveTemplate(location, baseType));

        return localBase;

        static bool IsRestrictedBaseType(SpecialType specialType) {
            if (specialType.IsNumeric())
                return true;

            switch (specialType) {
                case SpecialType.Array:
                case SpecialType.Any:
                case SpecialType.String:
                case SpecialType.Bool:
                case SpecialType.Type:
                case SpecialType.Void:
                    return true;
                default:
                    return false;
            }
        }
    }

    private ImmutableArray<TemplateParameterSymbol> MakeTemplateParameters(BelteDiagnosticQueue diagnostics) {
        if (_declaration.arity == 0)
            return [];

        var builder = ArrayBuilder<TemplateParameterSymbol>.GetInstance();
        var i = 0;

        var syntax = (TypeDeclarationSyntax)_declaration.declarations[0].syntaxReference.node;

        foreach (var templateSyntax in syntax.templateParameterList.parameters) {
            var result = new SourceTemplateParameterSymbol(
                this,
                templateSyntax.identifier.text,
                i,
                new SyntaxReference(templateSyntax)
            );

            builder.Add(result);

            i++;
        }

        return builder.ToImmutableAndFree();
    }

    private ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes(
        BelteDiagnosticQueue diagnostics) {
        if (_templateParameterInfo.lazyTypeParameterConstraintTypes.IsDefault) {
            GetTypeParameterConstraintKinds();

            if (ImmutableInterlocked.InterlockedInitialize(
                ref _templateParameterInfo.lazyTypeParameterConstraintTypes,
                MakeTypeParameterConstraintTypes(diagnostics))) {
                AddDeclarationDiagnostics(diagnostics);
            }
        }

        return _templateParameterInfo.lazyTypeParameterConstraintTypes;
    }

    private ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        if (_templateParameterInfo.lazyTypeParameterConstraintKinds.IsDefault) {
            ImmutableInterlocked.InterlockedInitialize(
                ref _templateParameterInfo.lazyTypeParameterConstraintKinds,
                MakeTypeParameterConstraintKinds());
        }

        return _templateParameterInfo.lazyTypeParameterConstraintKinds;
    }

    private ImmutableArray<ImmutableArray<TypeWithAnnotations>> MakeTypeParameterConstraintTypes(
        BelteDiagnosticQueue diagnostics) {
        var templateParameters = this.templateParameters;
        var results = ImmutableArray<TypeParameterConstraintClause>.Empty;
        var arity = templateParameters.Length;

        if (arity > 0) {
            var skipPartialDeclarationsWithoutConstraintClauses = SkipPartialDeclarationsWithoutConstraintClauses();
            ArrayBuilder<ImmutableArray<TypeParameterConstraintClause>> otherPartialClauses = null;

            var constraintClauses = GetConstraintClauses(syntaxReference.node, out var templateParameterList);

            if (!skipPartialDeclarationsWithoutConstraintClauses ||
                (constraintClauses is not null && constraintClauses.Count != 0)) {
                var binderFactory = declaringCompilation.GetBinderFactory(syntaxReference.syntaxTree);
                Binder binder;
                ImmutableArray<TypeParameterConstraintClause> constraints;

                if (constraintClauses is null || constraintClauses.Count == 0) {
                    binder = binderFactory.GetBinder(templateParameterList.parameters[0]);
                    constraints = binder.GetDefaultTypeParameterConstraintClauses(templateParameterList);
                } else {
                    binder = binderFactory.GetBinder(constraintClauses[0]);
                    binder = binder.WithAdditionalFlagsAndContainingMember(
                        BinderFlags.TemplateConstraintsClause | BinderFlags.SuppressConstraintChecks,
                        this
                    );

                    constraints = binder.BindTypeParameterConstraintClauses(
                        this,
                        templateParameters,
                        templateParameterList,
                        constraintClauses,
                        diagnostics
                    );
                }

                if (results.Length == 0) {
                    results = constraints;
                } else {
                    (otherPartialClauses ??= ArrayBuilder<ImmutableArray<TypeParameterConstraintClause>>.GetInstance())
                        .Add(constraints);
                }
            }

            var constraintsBuilder = ArrayBuilder<ExpressionSyntax>.GetInstance();

            foreach (var constraint in results) {
                if ((constraint.constraints & TypeParameterConstraintKinds.Expression) != 0)
                    constraintsBuilder.Add(constraint.expression);
            }

            _unboundConstraints = constraintsBuilder.ToImmutableAndFree();

            if (results.All(clause => clause.constraintTypes.IsEmpty))
                results = [];

            otherPartialClauses?.Free();
        }

        return results.SelectAsArray(clause => clause.constraintTypes);
    }

    private ImmutableArray<TypeParameterConstraintKinds> MakeTypeParameterConstraintKinds() {
        var templateParameters = this.templateParameters;
        var results = ImmutableArray<TypeParameterConstraintClause>.Empty;
        var arity = templateParameters.Length;

        if (arity > 0) {
            var skipPartialDeclarationsWithoutConstraintClauses = SkipPartialDeclarationsWithoutConstraintClauses();
            ArrayBuilder<ImmutableArray<TypeParameterConstraintClause>> otherPartialClauses = null;

            var constraintClauses = GetConstraintClauses(syntaxReference.node, out var templateParameterList);

            if (!skipPartialDeclarationsWithoutConstraintClauses ||
                (constraintClauses is not null && constraintClauses.Count != 0)) {
                var binderFactory = declaringCompilation.GetBinderFactory(syntaxReference.syntaxTree);
                Binder binder;
                ImmutableArray<TypeParameterConstraintClause> constraints;

                if (constraintClauses is null || constraintClauses.Count == 0) {
                    binder = binderFactory.GetBinder(templateParameterList.parameters[0]);
                    constraints = binder.GetDefaultTypeParameterConstraintClauses(templateParameterList);
                } else {
                    binder = binderFactory.GetBinder(constraintClauses[0]);
                    binder = binder.WithAdditionalFlagsAndContainingMember(
                        BinderFlags.TemplateConstraintsClause |
                        BinderFlags.SuppressConstraintChecks |
                        BinderFlags.SuppressTemplateArgumentBinding,
                        this
                    );

                    constraints = binder.BindTypeParameterConstraintClauses(
                        this,
                        templateParameters,
                        templateParameterList,
                        constraintClauses,
                        new BelteDiagnosticQueue()
                    );
                }

                if (results.Length == 0) {
                    results = constraints;
                } else {
                    (otherPartialClauses ??= ArrayBuilder<ImmutableArray<TypeParameterConstraintClause>>.GetInstance())
                        .Add(constraints);
                }
            }

            results = ConstraintsHelpers.AdjustConstraintKindsBasedOnConstraintTypes(templateParameters, results);

            if (results.All(clause => clause.constraints == TypeParameterConstraintKinds.None))
                results = [];

            otherPartialClauses?.Free();
        }

        return results.SelectAsArray(clause => clause.constraints);
    }

    private bool SkipPartialDeclarationsWithoutConstraintClauses() {
        foreach (var decl in _declaration.declarations) {
            var constraints = GetConstraintClauses(decl.syntaxReference.node, out _);

            if (constraints is not null && constraints.Count != 0)
                return true;
        }

        return false;
    }

    private static SyntaxList<TemplateConstraintClauseSyntax> GetConstraintClauses(
        SyntaxNode node,
        out TemplateParameterListSyntax templateParameterList) {
        switch (node.kind) {
            case SyntaxKind.ClassDeclaration:
            case SyntaxKind.StructDeclaration:
                var typeDeclaration = (TypeDeclarationSyntax)node;
                templateParameterList = typeDeclaration.templateParameterList;
                return typeDeclaration.constraintClauseList?.constraintClauses;
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }
}
