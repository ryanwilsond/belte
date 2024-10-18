using System.Collections.Generic;
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

internal sealed class SourceNamedTypeSymbol : SourceMemberContainerTypeSymbol {
    private NamedTypeSymbol _lazyDeclaredBase;
    private NamedTypeSymbol _lazyBaseType = ErrorTypeSymbol.UnknownResultType;

    private readonly TemplateParameterInfo _templateParameterInfo;

    internal SourceNamedTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        TypeDeclarationSyntax declaration,
        BelteDiagnosticQueue diagnostics)
        : base(containingSymbol, declaration, diagnostics) {
        _templateParameterInfo = arity == 0 ? TemplateParameterInfo.Empty : new TemplateParameterInfo();
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

    internal override NamedTypeSymbol baseType {
        get {
            if (ReferenceEquals(_lazyBaseType, ErrorTypeSymbol.UnknownResultType)) {
                if (containingType is not null)
                    _ = containingType.baseType;

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

    private static bool TypeDependsOn(NamedTypeSymbol depends, NamedTypeSymbol on) {
        var hashSet = PooledHashSet<Symbol>.GetInstance();
        TypeDependsClosure(depends, depends.declaringCompilation, hashSet);
        var result = hashSet.Contains(on);
        hashSet.Free();
        return result;
    }

    private static void TypeDependsClosure(
        NamedTypeSymbol type,
        Compilation currentCompilation,
        HashSet<Symbol> partialClosure) {
        if (type is null)
            return;

        type = type.originalDefinition;

        if (partialClosure.Add(type)) {
            TypeDependsClosure(type.GetDeclaredBaseType(null), currentCompilation, partialClosure);

            if (currentCompilation is not null && type.IsFromCompilation(currentCompilation))
                TypeDependsClosure(type.containingType, currentCompilation, partialClosure);
        }
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

        if (TypeDependsOn(declaredBase, this)) {
            return new ExtendedErrorTypeSymbol(
                declaredBase,
                LookupResultKind.NotReferencable,
                diagnostics.Push(Error.CircularBase(syntaxReference.location, declaredBase, this))
            );
        }

        _hasNoBaseCycles = true;
        return declaredBase;
    }

    private NamedTypeSymbol MakeDeclaredBase(
        ConsList<TypeSymbol> basesBeingResolved,
        BelteDiagnosticQueue diagnostics) {
        var newBasesBeingResolved = basesBeingResolved.Prepend(originalDefinition);
        var baseType = MakeOneDeclaredBase(newBasesBeingResolved, diagnostics);
        var baseTypeLocation = _declaration.identifier.location;

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
        BelteDiagnosticQueue diagnostics) {
        if (_declaration is not ClassDeclarationSyntax cd)
            return null;

        var baseSyntax = cd.baseType;

        if (baseSyntax is null)
            return null;

        NamedTypeSymbol localBase = null;
        var baseBinder = declaringCompilation.GetBinder(baseSyntax);
        baseBinder = baseBinder.WithAdditionalFlagsAndContainingMember(BinderFlags.SuppressConstraintChecks, this);
        var typeSyntax = baseSyntax.type;
        var location = typeSyntax.location;

        TypeSymbol baseType;

        if (typeKind == TypeKind.Class) {
            baseType = baseBinder.BindType(typeSyntax, diagnostics, newBasesBeingResolved).type;
            var baseSpecialType = baseType.specialType;

            if (IsRestrictedBaseType(baseSpecialType))
                diagnostics.Push(Error.CannotDerivePrimitive(location, baseType));

            if (baseType.isSealed && !isStatic)
                diagnostics.Push(Error.CannotDeriveSealed(location, baseType));

            if ((baseType.typeKind == TypeKind.Class) &&
                (localBase is null)) {
                localBase = (NamedTypeSymbol)baseType;

                if (isStatic && localBase.specialType != SpecialType.Object) {
                    var info = diagnostics.Push(Error.StaticDeriveFromNotObject(location, localBase));
                    localBase = new ExtendedErrorTypeSymbol(localBase, LookupResultKind.NotReferencable, info);
                }
            }
        } else {
            baseType = baseBinder.BindType(typeSyntax, diagnostics, newBasesBeingResolved).type;
        }

        if (baseType.typeKind == TypeKind.TemplateParameter)
            diagnostics.Push(Error.CannotDeriveTemplate(location, baseType));

        return localBase;

        static bool IsRestrictedBaseType(SpecialType specialType) {
            return specialType switch {
                SpecialType.Array or SpecialType.Any or SpecialType.String or
                SpecialType.Bool or SpecialType.Char or SpecialType.Int or
                SpecialType.Decimal or SpecialType.Type or SpecialType.Void => true,
                _ => false,
            };
        }
    }

    private ImmutableArray<TemplateParameterSymbol> MakeTemplateParameters(BelteDiagnosticQueue diagnostics) {
        if (_declaration.templateParameterList is null)
            return [];

        var builder = ArrayBuilder<TemplateParameterSymbol>.GetInstance();
        var i = 0;

        foreach (var templateSyntax in _declaration.templateParameterList.parameters) {
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

            var constraintClauses = GetConstraintClauses(_declaration, out var templateParameterList);

            if (!skipPartialDeclarationsWithoutConstraintClauses || constraintClauses.Count != 0) {
                var binderFactory = declaringCompilation.GetBinderFactory(_declaration.syntaxTree);
                Binder binder;
                ImmutableArray<TypeParameterConstraintClause> constraints;

                if (constraintClauses.Count == 0) {
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

            var constraintClauses = GetConstraintClauses(_declaration, out var templateParameterList);

            if (!skipPartialDeclarationsWithoutConstraintClauses || constraintClauses.Count != 0) {
                var binderFactory = declaringCompilation.GetBinderFactory(_declaration.syntaxTree);
                Binder binder;
                ImmutableArray<TypeParameterConstraintClause> constraints;

                if (constraintClauses.Count == 0) {
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
        return GetConstraintClauses(_declaration, out _).Count != 0;
    }

    private static SyntaxList<TemplateConstraintClauseSyntax> GetConstraintClauses(
        TypeDeclarationSyntax node,
        out TemplateParameterListSyntax templateParameterList) {
        switch (node.kind) {
            case SyntaxKind.ClassDeclaration:
            case SyntaxKind.StructDeclaration:
                templateParameterList = node.templateParameterList;
                return node.constraintClauseList.constraintClauses;
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }
}
