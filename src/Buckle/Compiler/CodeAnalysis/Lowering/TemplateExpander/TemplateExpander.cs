using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class TemplateExpander : BoundTreeRewriterWithStackGuard {
    private readonly ArrayBuilder<SynthesizedTemplateType> _typesBuilder;
    private readonly Dictionary<ConstructedNamedTypeSymbol, SynthesizedTemplateType> _typesMap = [];
    // Used for methods contained within an expanded template type
    private readonly Dictionary<(SynthesizedTemplateType, MethodSymbol), SynthesizedTemplateTypeMethod> _methodsMap = [];
    // Used for methods with an expanded template return type or parameter type
    private ImmutableDictionary<MethodSymbol, MethodSymbol> _secondaryMethodsMap;
    private readonly Dictionary<DataContainerSymbol, DataContainerSymbol> _localMap = [];

    private MethodSymbol _currentMethod;
    private MethodSymbol _replacementMethod;

    internal TemplateExpander(ArrayBuilder<SynthesizedTemplateType> typesBuilder) {
        _typesBuilder = typesBuilder;
    }

    internal bool RewriteMethodSymbol(MethodSymbol method, out MethodSymbol newMethod) {
        // All methods should be rewritten before the main pass
        Debug.Assert(_secondaryMethodsMap is null);

        var returnType = method.returnType;

        if (IsNonTypeTemplateTypeConstructed(returnType) ||
            method.parameterTypesWithAnnotations.Any(p => IsNonTypeTemplateTypeConstructed(p.type))) {
            var newReturnType = TypeIsUnexpandedTemplate(returnType, out var newType) ? newType : returnType;
            var builder = ArrayBuilder<ParameterSymbol>.GetInstance(method.parameterCount);

            foreach (var parameter in method.parameters) {
                var newParameter = TypeIsUnexpandedTemplate(parameter.type, out var newParamType)
                    ? new TypeSubstitutedParameterSymbol(parameter, new TypeWithAnnotations(newParamType))
                    : parameter;

                builder.Add(newParameter);
            }

            newMethod = new TypeSubstitutedMethodSymbol(
                method,
                new TypeWithAnnotations(newReturnType),
                builder.ToImmutableAndFree()
            );

            return true;
        }

        newMethod = null;
        return false;
    }

    internal void SetSecondaryMethodMap(ImmutableDictionary<MethodSymbol, MethodSymbol> methodMap) {
        Debug.Assert(_secondaryMethodsMap is null);
        _secondaryMethodsMap = methodMap;
    }

    internal ImmutableDictionary<(SynthesizedTemplateType, MethodSymbol), SynthesizedTemplateTypeMethod> GetMethodMap() {
        return _methodsMap.ToImmutableDictionary();
    }

    internal bool Expand(
        MethodSymbol method,
        BoundBlockStatement body,
        out MethodSymbol newMethod,
        out BoundBlockStatement newBody) {
        Debug.Assert(_secondaryMethodsMap is not null);

        _currentMethod = method;

        if (_secondaryMethodsMap.TryGetValue(method, out var value))
            _replacementMethod = value;

        newBody = (BoundBlockStatement)VisitBlockStatement(body);
        newMethod = _replacementMethod;

        return newBody != body;
    }

    internal static bool IsNonTypeTemplateTypeConstructed(TypeSymbol type) {
        return type is ConstructedNamedTypeSymbol constructed && !IsGenericOnlyType(constructed);
    }

    internal static bool IsNonTypeTemplateType(TypeSymbol type) {
        return type is NamedTypeSymbol named && !IsGenericOnlyType(named);
    }

    private static bool IsGenericOnlyType(NamedTypeSymbol type) {
        foreach (var templateParameter in type.templateParameters) {
            if (templateParameter.underlyingType.specialType != SpecialType.Type)
                return false;
        }

        return true;
    }

    private bool NoteType(TypeSymbol type) {
        if (type is not ConstructedNamedTypeSymbol constructed || IsGenericOnlyType(constructed))
            return false;

        if (_typesMap.ContainsKey(constructed))
            return true;

        var containingSymbol = constructed.containingSymbol is TypeSymbol containingType
            ? VisitType(containingType)
            : constructed.containingSymbol;

        var synthesizedType = new SynthesizedTemplateType(containingSymbol, constructed);
        _typesMap.Add(constructed, synthesizedType);
        _typesBuilder.Add(synthesizedType);

        return true;
    }

    private SynthesizedTemplateTypeMethod NoteMethod(SynthesizedTemplateType newOwner, MethodSymbol method) {
        if (_methodsMap.TryGetValue((newOwner, method), out var result))
            return result;

        var templateMethod = new SynthesizedTemplateTypeMethod(newOwner, method);
        _methodsMap.Add((newOwner, method), templateMethod);
        return templateMethod;
    }

    private bool TypeIsUnexpandedTemplate(TypeSymbol type, out SynthesizedTemplateType templateType) {
        if (NoteType(type)) {
            if (_typesMap.TryGetValue((ConstructedNamedTypeSymbol)type, out templateType))
                return true;
        }

        templateType = null;
        return false;
    }

    private ImmutableArray<DataContainerSymbol> RewriteLocals(ImmutableArray<DataContainerSymbol> locals) {
        var newLocals = ArrayBuilder<DataContainerSymbol>.GetInstance();

        foreach (var local in locals) {
            if (TryRewriteLocal(local, out var newLocal))
                newLocals.Add(newLocal);
        }

        return newLocals.ToImmutableAndFree();
    }

    private bool TryRewriteLocal(DataContainerSymbol local, out DataContainerSymbol newLocal) {
        if (_localMap.TryGetValue(local, out newLocal))
            return true;

        var newType = VisitType(local.type);

        if (TypeSymbol.Equals(newType, local.type, TypeCompareKind.ConsiderEverything) && _replacementMethod is null) {
            newLocal = local;
        } else {
            newLocal = new TypeSubstitutedLocalSymbol(
                local,
                new TypeWithAnnotations(newType),
                _replacementMethod ?? _currentMethod
            );

            _localMap.Add(local, newLocal);
        }

        return true;
    }

    internal override TypeSymbol VisitType(TypeSymbol type) {
        if (type is not null && TypeIsUnexpandedTemplate(type, out var newType))
            return newType;

        return type;
    }

    internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
        var newLocals = RewriteLocals(node.locals);
        var newLocalFunctions = node.localFunctions;
        var newStatements = VisitList(node.statements);
        return node.Update(newStatements, newLocals, newLocalFunctions);
    }

    internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        if (_localMap.TryGetValue(node.dataContainer, out var replacementLocal)) {
            return node.Update(
                replacementLocal,
                node.constantValue,
                replacementLocal.type
            );
        }

        return base.VisitDataContainerExpression(node);
    }

    internal override BoundNode VisitDataContainerDeclaration(BoundDataContainerDeclaration node) {
        if (_localMap.TryGetValue(node.dataContainer, out var replacementLocal)) {
            node = node.Update(
                replacementLocal,
                node.initializer
            );
        }

        return base.VisitDataContainerDeclaration(node);
    }

    internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
        if (_replacementMethod is not null) {
            var newParameter = _replacementMethod.parameters[node.parameter.ordinal];
            node = node.Update(newParameter, node.constantValue, newParameter.type);
        }

        return base.VisitParameterExpression(node);
    }

    internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
        if (_secondaryMethodsMap.TryGetValue(node.constructor, out var replacementMethod)) {
            node = node.Update(
                replacementMethod,
                node.arguments,
                node.argumentRefKinds,
                node.argsToParams,
                node.defaultArguments,
                node.wasTargetTyped,
                node.type
            );
        }

        if (TypeIsUnexpandedTemplate(node.type, out var templateType)) {
            node = node.Update(
                NoteMethod(templateType, node.constructor.originalDefinition),
                node.arguments,
                node.argumentRefKinds,
                node.argsToParams,
                node.defaultArguments,
                node.wasTargetTyped,
                templateType
            );
        }

        return base.VisitObjectCreationExpression(node);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        if (_secondaryMethodsMap.TryGetValue(node.method, out var replacementMethod)) {
            node = node.Update(
                node.receiver,
                replacementMethod,
                node.arguments,
                node.argumentRefKinds,
                node.defaultArguments,
                node.resultKind,
                replacementMethod.returnType
            );
        }

        if (TypeIsUnexpandedTemplate(node.method.containingType, out var templateType)) {
            node = node.Update(
                node.receiver,
                NoteMethod(templateType, node.method.originalDefinition),
                node.arguments,
                node.argumentRefKinds,
                node.defaultArguments,
                node.resultKind,
                node.type
            );
        }

        return base.VisitCallExpression(node);
    }

    internal override BoundNode VisitFunctionLoad(BoundFunctionLoad node) {
        if (_secondaryMethodsMap.TryGetValue(node.targetMethod, out var replacementMethod)) {
            node = node.Update(
                node.receiver,
                replacementMethod,
                node.type
            );
        }

        if (TypeIsUnexpandedTemplate(node.targetMethod.containingType, out var templateType)) {
            node = node.Update(
                node.receiver,
                NoteMethod(templateType, node.targetMethod.originalDefinition),
                node.type
            );
        }

        return base.VisitFunctionLoad(node);
    }
}
