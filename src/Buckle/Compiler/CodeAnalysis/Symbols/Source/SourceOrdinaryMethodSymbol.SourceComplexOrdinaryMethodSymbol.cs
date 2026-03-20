using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceOrdinaryMethodSymbol {
    private sealed class SourceComplexOrdinaryMethodSymbol : SourceOrdinaryMethodSymbol {
        private readonly TemplateParameterInfo _templateParameterInfo;

        internal SourceComplexOrdinaryMethodSymbol(
            NamedTypeSymbol containingType,
            string name,
            MethodDeclarationSyntax syntax,
            MethodKind methodKind,
            BelteDiagnosticQueue diagnostics)
            : base(containingType, name, syntax, methodKind, diagnostics) {
            var templateParameters = MakeTemplateParameters(syntax, diagnostics);
            _templateParameterInfo = templateParameters.IsEmpty
                ? TemplateParameterInfo.Empty
                : new TemplateParameterInfo { lazyTemplateParameters = templateParameters };
        }

        public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters
            => _templateParameterInfo.lazyTemplateParameters;

        // TODO This should be something. _templateParameterInfo.lazyTemplateConstraints?
        // If so, rename TemplateParameterInfo => TemplateInfo to make more sense
        public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

        internal sealed override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
            if (_templateParameterInfo.lazyTypeParameterConstraintTypes.IsDefault) {
                GetTypeParameterConstraintKinds();

                var diagnostics = BelteDiagnosticQueue.GetInstance();
                var syntax = GetSyntax();
                var withTemplateParametersBinder = declaringCompilation
                    .GetBinderFactory(syntax.syntaxTree)
                    .GetBinder(syntax.returnType, syntax, this);

                var constraints = this.MakeTypeParameterConstraintTypes(
                    withTemplateParametersBinder,
                    templateParameters,
                    syntax.templateParameterList,
                    syntax.constraintClauseList.constraintClauses,
                    diagnostics
                );

                if (ImmutableInterlocked.InterlockedInitialize(
                    ref _templateParameterInfo.lazyTypeParameterConstraintTypes,
                    constraints)) {
                    AddDeclarationDiagnostics(diagnostics);
                }

                diagnostics.Free();
            }

            return _templateParameterInfo.lazyTypeParameterConstraintTypes;
        }

        internal sealed override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
            if (_templateParameterInfo.lazyTypeParameterConstraintKinds.IsDefault) {
                var syntax = GetSyntax();
                var withTemplateParametersBinder = declaringCompilation
                    .GetBinderFactory(syntax.syntaxTree)
                    .GetBinder(syntax.returnType, syntax, this);

                var constraints = this.MakeTypeParameterConstraintKinds(
                    withTemplateParametersBinder,
                    templateParameters,
                    syntax.templateParameterList,
                    syntax.constraintClauseList.constraintClauses
                );

                ImmutableInterlocked.InterlockedInitialize(
                    ref _templateParameterInfo.lazyTypeParameterConstraintKinds,
                    constraints
                );
            }

            return _templateParameterInfo.lazyTypeParameterConstraintKinds;
        }

        private ImmutableArray<TemplateParameterSymbol> MakeTemplateParameters(
            MethodDeclarationSyntax syntax,
            BelteDiagnosticQueue diagnostics) {
            if (syntax.templateParameterList is null)
                return [];

            OverriddenMethodTemplateParameterMapBase templateMap = null;

            if (isOverride)
                templateMap = new OverriddenMethodTemplateParameterMap(this);

            var templateParameters = syntax.templateParameterList.parameters;
            var result = ArrayBuilder<TemplateParameterSymbol>.GetInstance();

            for (var ordinal = 0; ordinal < templateParameters.Count; ordinal++) {
                var parameter = templateParameters[ordinal];
                var identifier = parameter.identifier;
                var location = identifier.location;
                var name = identifier.text;

                for (var i = 0; i < result.Count; i++) {
                    if (name == result[i].name) {
                        // TODO
                        // diagnostics.Add(ErrorCode.ERR_DuplicateTypeParameter, location, name);
                        break;
                    }
                }

                var enclosingTemplateParameter = containingType.FindEnclosingTemplateParameter(name);

                if (enclosingTemplateParameter is not null) {
                    // TODO
                    // Type parameter '{0}' has the same name as the type parameter from outer type '{1}'
                    // diagnostics.Add(ErrorCode.WRN_TypeParameterSameAsOuterTypeParameter, location, name, tpEnclosing.ContainingType);
                }

                var templateParameter = templateMap is null
                    ? new SourceMethodTemplateParameterSymbol(
                        this,
                        name,
                        ordinal,
                        new SyntaxReference(parameter)
                      )
                    : (TemplateParameterSymbol)new SourceOverridingMethodTemplateParameterSymbol(
                        templateMap,
                        name,
                        ordinal,
                        new SyntaxReference(parameter)
                      );

                result.Add(templateParameter);
            }

            return result.ToImmutableAndFree();
        }
    }
}
