using System.Collections.Concurrent;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Synthesizes definitions for each instantiated non-type template type found by the <see cref="TemplateExpander" />.
/// </summary>
internal sealed class TemplateTypeRewriter : BoundTreeRewriterWithStackGuard {
    private readonly NamedTypeSymbol _originalType;
    private readonly SynthesizedTemplateType _instantiatedType;

    private TemplateTypeRewriter(NamedTypeSymbol originalType, SynthesizedTemplateType instantiatedType) {
        _originalType = originalType;
        _instantiatedType = instantiatedType;
    }

    internal static void Rewrite(
        NamedTypeSymbol originalType,
        SynthesizedTemplateType instantiatedType,
        ConcurrentDictionary<MethodSymbol, BoundBlockStatement> allMethods,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement>.Builder builder,
        ImmutableDictionary<(SynthesizedTemplateType, MethodSymbol), SynthesizedTemplateTypeMethod> methodMap) {
        var rewriter = new TemplateTypeRewriter(originalType, instantiatedType);

        foreach (var (method, body) in allMethods) {
            if (method.containingType.originalDefinition.Equals(originalType)) {
                if (!methodMap.TryGetValue((instantiatedType, method), out var newMethod))
                    newMethod = new SynthesizedTemplateTypeMethod(instantiatedType, method);

                var newBody = (BoundBlockStatement)rewriter.Visit(body);
                builder.Add(newMethod, newBody);
            }
        }
    }

    internal override BoundNode VisitTypeExpression(BoundTypeExpression node) {
        if (node.type is TemplateParameterSymbol templateParameter) {
            if (templateParameter.underlyingType.specialType != SpecialType.Type) {
                var typeOrConstant = _instantiatedType.unexpandedType.templateSubstitution
                    .SubstituteType(templateParameter);

                if (typeOrConstant.isConstant) {
                    return new BoundLiteralExpression(
                        node.syntax,
                        typeOrConstant.constant,
                        templateParameter.underlyingType.type
                    );
                }
            } else {
                if (_instantiatedType.replacementTemplateParameters.TryGetValue(templateParameter, out var value))
                    return new BoundTypeExpression(node.syntax, null, null, value);
            }
        }

        return base.VisitTypeExpression(node);
    }
}
