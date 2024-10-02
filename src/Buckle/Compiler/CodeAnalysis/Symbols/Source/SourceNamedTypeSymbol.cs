using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceNamedTypeSymbol : SourceMemberContainerTypeSymbol {
    private readonly TemplateParameterInfo _templateParameterInfo;

    internal SourceNamedTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        TypeDeclarationSyntax declaration,
        BelteDiagnosticQueue diagnostics)
        : base(containingSymbol, declaration, diagnostics) {
        _templateParameterInfo = arity == 0 ? TemplateParameterInfo.Empty : new TemplateParameterInfo();
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
