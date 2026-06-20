using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceNamedTypeSymbol : SourceMemberContainerTypeSymbol, IAttributeTargetSymbol {
    private ImmutableArray<ExpressionSyntax> _unboundConstraints;
    private ImmutableArray<BoundExpression> _lazyTemplateConstraints;
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;
    private Tuple<NamedTypeSymbol, ImmutableArray<NamedTypeSymbol>> _lazyDeclaredBases;
    private NamedTypeSymbol _lazyBaseType = ErrorTypeSymbol.UnknownResultType;
    private TemplateParameterInfo _lazyTemplateParameterInfo;
    private SynthesizedEnumValueFieldSymbol _lazyEnumValueField;
    private NamedTypeSymbol _lazyEnumUnderlyingType = ErrorTypeSymbol.UnknownResultType;
    private ImmutableArray<NamedTypeSymbol> _lazyInterfaces;

    internal SourceNamedTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        MergedTypeDeclaration declaration,
        BelteDiagnosticQueue diagnostics,
        TupleExtraData tupleData = null)
        : base(containingSymbol, declaration, diagnostics, tupleData) { }

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
                if (containingType is not null && typeKind != TypeKind.Enum)
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

    internal FieldSymbol enumValueField {
        get {
            if (typeKind != TypeKind.Enum)
                return null;

            if (_lazyEnumValueField is null)
                Interlocked.CompareExchange(ref _lazyEnumValueField, new SynthesizedEnumValueFieldSymbol(this), null);

            return _lazyEnumValueField;
        }
    }

    internal override NamedTypeSymbol enumUnderlyingType {
        get {
            if (ReferenceEquals(_lazyEnumUnderlyingType, ErrorTypeSymbol.UnknownResultType)) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                if ((object)Interlocked.CompareExchange(ref _lazyEnumUnderlyingType, GetEnumUnderlyingType(diagnostics), ErrorTypeSymbol.UnknownResultType) ==
                    ErrorTypeSymbol.UnknownResultType) {
                    AddDeclarationDiagnostics(diagnostics);
                    _state.NotePartComplete(CompletionParts.EnumUnderlyingType);
                }

                diagnostics.Free();
            }

            return _lazyEnumUnderlyingType;
        }
    }

    internal override bool isUnionStruct => typeKind == TypeKind.Struct &&
        _declaration.declarations[0].syntaxReference.node.kind == SyntaxKind.UnionDeclaration;

    internal bool isSimpleProgram => _declaration.declarations.Any(static d => d.isSimpleProgram);

    internal Tuple<NamedTypeSymbol, ImmutableArray<NamedTypeSymbol>> GetDeclaredBases(
        ConsList<TypeSymbol> basesBeingResolved) {
        if (_lazyDeclaredBases is null) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            if (Interlocked.CompareExchange(
                ref _lazyDeclaredBases,
                MakeDeclaredBases(basesBeingResolved, diagnostics), null) is null) {
                AddDeclarationDiagnostics(diagnostics);
            }

            diagnostics.Free();
        }

        return _lazyDeclaredBases;
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return GetDeclaredBases(basesBeingResolved).Item1;
    }

    internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved) {
        return GetDeclaredBases(basesBeingResolved).Item2;
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

    private protected override void CheckInterfaces(BelteDiagnosticQueue diagnostics) {
        var interfaces = interfacesAndTheirBaseInterfaces;

        if (interfaces.IsEmpty)
            return;

        var singleDeclaration = FirstDeclarationWithExplicitBases();

        if (singleDeclaration is not null) {
            var location = singleDeclaration.nameLocation;

            foreach (var pair in interfaces) {
                var set = pair.Value;

                foreach (var @interface in set)
                    @interface.CheckAllConstraints(location, diagnostics);

                if (set.Count > 1) {
                    var other = pair.Key;

                    foreach (var @interface in set) {
                        if ((object)other == @interface)
                            continue;

                        if (other.Equals(@interface, TypeCompareKind.ConsiderEverything)) {
                        } else if (other.Equals(@interface, TypeCompareKind.IgnoreTupleNames)) {
                            diagnostics.Push(Error.DuplicateInterfaceWithTupleNamesInBaseList(location, @interface, other, this));
                        } else {
                            diagnostics.Push(Error.DuplicateInterfaceWithDifferencesInBaseList(location, @interface, other, this));
                        }
                    }
                }
            }
        }
    }

    private SingleTypeDeclaration FirstDeclarationWithExplicitBases() {
        foreach (var singleDeclaration in _declaration.declarations) {
            var (baseSyntax, interfacesSyntax) = GetBaseListOpt(singleDeclaration);

            if (baseSyntax is not null || interfacesSyntax is not null)
                return singleDeclaration;
        }

        return null;
    }

    private static (BaseTypeSyntax, InterfaceListSyntax) GetBaseListOpt(SingleTypeDeclaration decl) {
        if (decl.hasBaseDeclarations) {
            switch (decl.syntaxReference.node.kind) {
                case SyntaxKind.ClassDeclaration: {
                        var node = (ClassDeclarationSyntax)decl.syntaxReference.node;
                        return (node.baseType, node.interfaceList);
                    }
                case SyntaxKind.FileScopedClassDeclaration: {
                        var node = (FileScopedClassDeclarationSyntax)decl.syntaxReference.node;
                        return (node.baseType, node.interfaceList);
                    }
                case SyntaxKind.EnumDeclaration:
                    return (((EnumDeclarationSyntax)decl.syntaxReference.node).baseType, null);
                case SyntaxKind.StructDeclaration:
                    return (null, ((StructDeclarationSyntax)decl.syntaxReference.node).interfaceList);
                default:
                    throw ExceptionUtilities.UnexpectedValue(decl.syntaxReference.node.kind);
            }
        }

        return (null, null);
    }

    private NamedTypeSymbol MakeAcyclicBaseType(BelteDiagnosticQueue diagnostics) {
        var typeKind = this.typeKind;
        var declaredBase = typeKind == TypeKind.Enum
            ? CorLibrary.GetSpecialType(SpecialType.Enum)
            : GetDeclaredBaseType(basesBeingResolved: null);

        if (declaredBase is null) {
            switch (typeKind) {
                case TypeKind.Class:
                    if (specialType == SpecialType.Object)
                        return null;

                    declaredBase = CorLibrary.GetSpecialType(SpecialType.Object);
                    break;
                case TypeKind.Struct:
                    declaredBase = CorLibrary.GetSpecialType(SpecialType.ValueType);
                    break;
                case TypeKind.Interface:
                    return null;
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

    internal sealed override ImmutableArray<NamedTypeSymbol> Interfaces(ConsList<TypeSymbol> basesBeingResolved) {
        if (_lazyInterfaces.IsDefault) {
            if (basesBeingResolved is not null && basesBeingResolved.ContainsReference(originalDefinition))
                return [];

            var diagnostics = BelteDiagnosticQueue.GetInstance();
            var acyclicInterfaces = MakeAcyclicInterfaces(basesBeingResolved, diagnostics);

            if (ImmutableInterlocked.InterlockedCompareExchange(ref _lazyInterfaces, acyclicInterfaces, default).IsDefault)
                AddDeclarationDiagnostics(diagnostics);

            diagnostics.Free();
        }

        return _lazyInterfaces;
    }

    private ImmutableArray<NamedTypeSymbol> MakeAcyclicInterfaces(
        ConsList<TypeSymbol> basesBeingResolved,
        BelteDiagnosticQueue diagnostics) {
        var typeKind = this.typeKind;

        if (typeKind == TypeKind.Enum)
            return [];

        var declaredInterfaces = GetDeclaredInterfaces(basesBeingResolved: basesBeingResolved);
        var isInterface = typeKind == TypeKind.Interface;
        var result = isInterface ? ArrayBuilder<NamedTypeSymbol>.GetInstance() : null;

        foreach (var t in declaredInterfaces) {
            if (isInterface) {
                if (BaseTypeAnalysis.TypeDependsOn(depends: t, on: this)) {
                    var error = Error.CycleInInterfaceInheritance(location, this, t);
                    diagnostics.Push(error);
                    result.Add(new ExtendedErrorTypeSymbol(t, LookupResultKind.NotReferencable, error));
                    continue;
                } else {
                    result.Add(t);
                }
            }
        }

        return isInterface ? result.ToImmutableAndFree() : declaredInterfaces;
    }

    private Tuple<NamedTypeSymbol, ImmutableArray<NamedTypeSymbol>> MakeDeclaredBases(
        ConsList<TypeSymbol> basesBeingResolved,
        BelteDiagnosticQueue diagnostics) {
        if (typeKind == TypeKind.Enum)
            return new(null, []);

        var decl = _declaration.declarations[0];
        var newBasesBeingResolved = basesBeingResolved.Prepend(originalDefinition);
        var baseInterfaces = ArrayBuilder<NamedTypeSymbol>.GetInstance();

        var interfaceLocations = SpecializedSymbolCollections
            .GetPooledSymbolDictionaryInstance<NamedTypeSymbol, TextLocation>();

        var (baseType, partInterfaces) = MakeOneDeclaredBases(newBasesBeingResolved, decl, diagnostics);
        var baseTypeLocation = decl.nameLocation;

        foreach (var t in partInterfaces) {
            if (!interfaceLocations.ContainsKey(t)) {
                baseInterfaces.Add(t);
                interfaceLocations.Add(t, decl.nameLocation);
            }
        }

        if (baseType is not null) {
            if (baseType.isStatic)
                diagnostics.Push(Error.CannotDeriveStatic(baseTypeLocation, baseType));

            if (!IsNoMoreVisibleThan(baseType))
                diagnostics.Push(Error.InconsistentAccessibilityClass(baseTypeLocation, baseType, this));
        }

        var baseInterfacesImmutable = baseInterfaces.ToImmutableAndFree();

        if (declaredAccessibility != Accessibility.Private && isInterface) {
            foreach (var i in baseInterfacesImmutable) {
                if (!i.IsAtLeastAsVisibleAs(this))
                    diagnostics.Push(Error.InconsistentAccessibilityInterface(interfaceLocations[i], this, i));
            }
        }

        interfaceLocations.Free();
        return new(baseType, baseInterfacesImmutable);
    }

    private Tuple<NamedTypeSymbol, ImmutableArray<NamedTypeSymbol>> MakeOneDeclaredBases(
        ConsList<TypeSymbol> newBasesBeingResolved,
        SingleTypeDeclaration decl,
        BelteDiagnosticQueue diagnostics) {
        var (baseSyntax, interfacesSyntax) = GetBaseListOpt(decl);

        if (baseSyntax is null && interfacesSyntax is null)
            return new(null, []);

        NamedTypeSymbol localBase = null;
        var localInterfaces = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        var baseBinder = declaringCompilation.GetBinder((BelteSyntaxNode)baseSyntax ?? interfacesSyntax);
        baseBinder = baseBinder.WithAdditionalFlagsAndContainingMember(BinderFlags.SuppressConstraintChecks, this);
        var baseTypeSyntax = baseSyntax?.type;
        var location = baseTypeSyntax?.location;

        TypeSymbol baseType = null;

        if (baseTypeSyntax is not null) {
            Debug.Assert(typeKind == TypeKind.Class);
            baseType = baseBinder.BindType(baseTypeSyntax, diagnostics, newBasesBeingResolved).type.StrippedType();
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
        }

        if (interfacesSyntax is not null) {
            foreach (var interfaceSyntax in interfacesSyntax.types) {
                baseType = baseBinder.BindType(interfaceSyntax, diagnostics, newBasesBeingResolved).type;

                switch (baseType.typeKind) {
                    case TypeKind.Interface:
                        foreach (var t in localInterfaces) {
                            if (t.Equals(baseType, TypeCompareKind.ConsiderEverything))
                                diagnostics.Push(Error.DuplicateInterfaceInInterfaceList(location, baseType));
                        }

                        if (isStatic)
                            diagnostics.Push(Error.StaticClassInterfaceImpl(location, this));

                        localInterfaces.Add((NamedTypeSymbol)baseType);
                        continue;
                    case TypeKind.Error:
                        localInterfaces.Add((NamedTypeSymbol)baseType);
                        continue;
                    default:
                        diagnostics.Push(Error.NonInterfaceInInterfaceList(location, baseType));
                        continue;
                }
            }
        }

        if (baseType?.typeKind == TypeKind.TemplateParameter)
            diagnostics.Push(Error.CannotDeriveTemplate(location, baseType));

        return new(localBase, localInterfaces.ToImmutableAndFree());

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
        var seenNames = new HashSet<string>();

        foreach (var templateSyntax in syntax.templateParameterList.parameters) {
            var identifier = templateSyntax.identifier;
            var name = identifier.valueText;

            var result = new SourceTemplateParameterSymbol(
                this,
                name,
                i,
                new SyntaxReference(templateSyntax)
            );

            if (!seenNames.Add(name))
                diagnostics.Push(Error.DuplicateTemplateParameter(identifier.location, name));

            builder.Add(result);

            i++;
        }

        return builder.ToImmutableAndFree();
    }

    private NamedTypeSymbol GetEnumUnderlyingType(BelteDiagnosticQueue diagnostics) {
        if (typeKind != TypeKind.Enum)
            return null;

        var compilation = declaringCompilation;
        var decl = _declaration.declarations[0];
        var (baseSyntax, interfacesSyntax) = GetBaseListOpt(decl);
        Debug.Assert(interfacesSyntax is null);

        if (baseSyntax is not null) {
            var typeSyntax = baseSyntax.type;

            var baseBinder = compilation.GetBinder(baseSyntax);
            var type = baseBinder.BindType(typeSyntax, diagnostics).type.StrippedType();

            if (!type.specialType.IsValidEnumUnderlyingType()) {
                diagnostics.Push(Error.InvalidEnumType(typeSyntax.location));
                type = CorLibrary.GetSpecialType(SpecialType.Int);
            }

            if (type.specialType is SpecialType.Char or SpecialType.String &&
                declaringCompilation.options.buildMode is BuildMode.CSharpTranspile or BuildMode.Execute or BuildMode.Dotnet) {
                diagnostics.Push(Error.Unsupported.NonIntegralEnum(typeSyntax.location));
            }

            return (NamedTypeSymbol)type;
        }

        return CorLibrary.GetSpecialType(SpecialType.Int);
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
            case SyntaxKind.FileScopedClassDeclaration:
            case SyntaxKind.StructDeclaration:
            case SyntaxKind.UnionDeclaration:
            case SyntaxKind.InterfaceDeclaration:
                var typeDeclaration = (TypeDeclarationSyntax)node;
                templateParameterList = typeDeclaration.templateParameterList;
                return typeDeclaration.constraintClauseList?.constraintClauses;
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }

    private protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) {
        return new SourceNamedTypeSymbol(containingType, _declaration, BelteDiagnosticQueue.Discarded, newData);
    }
}
