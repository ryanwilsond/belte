using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Synthesizes definitions for each instantiated non-type template type found by the <see cref="TemplateExpander" />.
/// </summary>
internal sealed class TemplateTypeRewriter<T> : BoundTreeRewriterWithStackGuard
    where T : ISymbolWithTemplates {
    private readonly ISynthesizedTemplate<T> _instantiatedSymbol;
    private readonly Dictionary<(SynthesizedTemplateType, FieldSymbol), SynthesizedTemplateTypeField> _fieldMap;

    private TemplateTypeRewriter(
        ISynthesizedTemplate<T> instantiatedType,
        Dictionary<(SynthesizedTemplateType, FieldSymbol), SynthesizedTemplateTypeField> fieldMap = null) {
        _instantiatedSymbol = instantiatedType;
        _fieldMap = fieldMap;
    }

    internal static void Rewrite(
        SynthesizedTemplateMethod instantiatedMethod,
        BoundBlockStatement body,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement>.Builder builder) {
        var rewriter = new TemplateTypeRewriter<MethodSymbol>(instantiatedMethod);
        var newBody = (BoundBlockStatement)rewriter.Visit(body);
        builder.Add(instantiatedMethod, newBody);
    }

    internal static void Rewrite(
        TemplateExpander templateExpander,
        NamedTypeSymbol originalType,
        SynthesizedTemplateType instantiatedType,
        ConcurrentDictionary<MethodSymbol, BoundBlockStatement> allMethods,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement>.Builder builder,
        ImmutableDictionary<(SynthesizedTemplateType, MethodSymbol), SynthesizedTemplateTypeMethod> methodMap) {
        var rewriter = new TemplateTypeRewriter<NamedTypeSymbol>(instantiatedType, instantiatedType.fieldMap);

        foreach (var (method, body) in allMethods) {
            if (method.containingType.originalDefinition.Equals(originalType)) {
                if (!methodMap.TryGetValue((instantiatedType, method), out var newMethod))
                    newMethod = new SynthesizedTemplateTypeMethod(templateExpander, instantiatedType, method);

                var newBody = (BoundBlockStatement)rewriter.Visit(body);
                builder.Add(newMethod, newBody);
            }
        }
    }

    internal override BoundNode VisitTypeExpression(BoundTypeExpression node) {
        if (node.type is TemplateParameterSymbol templateParameter) {
            if (templateParameter.underlyingType.specialType != SpecialType.Type) {
                var typeOrConstant = _instantiatedSymbol.unexpandedSymbol.templateSubstitution
                    .SubstituteType(templateParameter);

                if (typeOrConstant.isConstant) {
                    return new BoundLiteralExpression(
                        node.syntax,
                        typeOrConstant.constant,
                        templateParameter.underlyingType.type
                    );
                }
            } else {
                if (_instantiatedSymbol.replacementTemplateParameters.TryGetValue(templateParameter, out var value))
                    return new BoundTypeExpression(node.syntax, null, null, value);
            }
        }

        return base.VisitTypeExpression(node);
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        if (Binder.IsThisInstanceAccess(node)) {
            Debug.Assert(_fieldMap is not null);

            if (_instantiatedSymbol is SynthesizedTemplateType t) {
                node = node.Update(
                    node.receiver,
                    _fieldMap[(t, node.field)],
                    node.constantValue,
                    node.type
                );
            } else {
                throw ExceptionUtilities.Unreachable();
            }
        }

        return base.VisitFieldAccessExpression(node);
    }
}
