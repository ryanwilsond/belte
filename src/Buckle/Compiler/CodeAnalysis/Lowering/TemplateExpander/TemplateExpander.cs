using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Rewrites method signatures and bodies if they contain instantiations of non-type template types
/// </summary>
internal sealed class TemplateExpander : BoundTreeRewriterWithStackGuard {
    private readonly ArrayBuilder<SynthesizedTemplateType> _typesBuilder;
    private readonly Dictionary<ConstructedNamedTypeSymbol, SynthesizedTemplateType> _typesMap = [];
    // Used for methods contained within an expanded template type
    private readonly Dictionary<(SynthesizedTemplateType, MethodSymbol), SynthesizedTemplateTypeMethod> _typeMethodsMap = [];
    // Used for methods with an expanded template return type or parameter type
    private ImmutableDictionary<MethodSymbol, MethodSymbol> _secondaryMethodsMap;
    // Used for methods needing expanding because they directly have non-type templates
    private readonly Dictionary<MethodSymbol, SynthesizedTemplateMethod> _methodsMap = [];
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
            var newReturnType = VisitType(returnType);
            var builder = ArrayBuilder<ParameterSymbol>.GetInstance(method.parameterCount);

            foreach (var parameter in method.parameters) {
                var newParameter = TypeContainsUnexpandedTemplate(parameter.type, out var newParamType)
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

    internal ImmutableDictionary<(SynthesizedTemplateType, MethodSymbol), SynthesizedTemplateTypeMethod> GetTypeMethodMap() {
        return _typeMethodsMap.ToImmutableDictionary();
    }

    internal ImmutableDictionary<MethodSymbol, SynthesizedTemplateMethod> GetMethodMap() {
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
        return type is ConstructedNamedTypeSymbol constructed && !IsGenericOnly(constructed);
    }

    internal static bool IsNonTypeTemplateType(TypeSymbol type) {
        return type is NamedTypeSymbol named && !IsGenericOnly(named);
    }

    internal static bool IsNonTypeTemplateMethod(MethodSymbol method) {
        return !IsGenericOnly(method);
    }

    internal static bool ShouldEmit(ISymbolWithTemplates type) {
        if (type.templateParameters.Any(t => t.underlyingType.specialType != SpecialType.Type))
            return false;

        return true;
    }

    private static bool IsGenericOnly(ISymbolWithTemplates symbol) {
        foreach (var templateParameter in symbol.templateParameters) {
            if (templateParameter.underlyingType.specialType != SpecialType.Type)
                return false;
        }

        return true;
    }

    private bool NoteType(TypeSymbol type) {
        if (type is not ConstructedNamedTypeSymbol constructed || IsGenericOnly(constructed))
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

    private MethodSymbol ReplaceMethodOwner(NamedTypeSymbol newOwner, MethodSymbol method) {
        // This is for when rewriting method calls on a template type directly
        if (newOwner.originalDefinition is SynthesizedTemplateType templateOwner) {
            var originalDefinition = method.originalDefinition;

            if (_typeMethodsMap.TryGetValue((templateOwner, originalDefinition), out var result))
                return ConstructIfApplicable(result);

            var templateMethod = new SynthesizedTemplateTypeMethod(templateOwner, originalDefinition);
            _typeMethodsMap.Add((templateOwner, originalDefinition), templateMethod);
            return ConstructIfApplicable(templateMethod);
        }
        // This is for when rewriting a method call not on a template type that contains template types (via return or param types)
        else {
            Debug.Assert(newOwner is ConstructedNamedTypeSymbol);
            return method.originalDefinition.AsMember(newOwner);
        }

        MethodSymbol ConstructIfApplicable(MethodSymbol synthesizedMethod) {
            if (newOwner is ConstructedNamedTypeSymbol)
                return synthesizedMethod.AsMember(newOwner);

            return synthesizedMethod;
        }
    }

    private bool TypeContainsUnexpandedTemplate(TypeSymbol type, out TypeSymbol replacedType) {
        replacedType = VisitType(type);

        if (TypeSymbol.Equals(type, replacedType, TypeCompareKind.ConsiderEverything))
            return false;

        return true;
    }

    private bool MethodContainsUnexpandedTemplate(MethodSymbol method, out MethodSymbol replacedMethod) {
        if (method is not ConstructedMethodSymbol constructed || IsGenericOnly(constructed)) {
            replacedMethod = null;
            return false;
        }

        if (_methodsMap.TryGetValue(method, out var templateMethod)) {
            replacedMethod = templateMethod;
            return true;
        }

        var synthesizedMethod = new SynthesizedTemplateMethod(method.containingSymbol, constructed);
        _methodsMap.Add(constructed, synthesizedMethod);

        replacedMethod = synthesizedMethod;
        return true;
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
        if (type is not null) {
            type.VisitType(VisitTypePredicate, this);

            return TemplateTypeReplacer<ConstructedNamedTypeSymbol, SynthesizedTemplateType, NamedTypeSymbol>.Replace(
                type,
                _typesMap,
                ConstructIfApplicable
            );
        }

        return type;

        static bool VisitTypePredicate(TypeSymbol type, TemplateExpander argument, bool canDigThroughNullable = true) {
            argument.NoteType(type);
            return false;
        }

        static NamedTypeSymbol ConstructIfApplicable(
            ConstructedNamedTypeSymbol source,
            SynthesizedTemplateType replacement) {
            if (replacement.arity == 0)
                return replacement;

            var builder = ArrayBuilder<TypeOrConstant>.GetInstance(replacement.arity);

            foreach (var templateArgument in source.templateArguments) {
                if (templateArgument.isType)
                    builder.Add(templateArgument);
            }

            Debug.Assert(builder.Count == replacement.arity);
            return replacement.Construct(builder.ToImmutableAndFree());
        }
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

        if (TypeContainsUnexpandedTemplate(node.type, out var templateType)) {
            node = node.Update(
                ReplaceMethodOwner((NamedTypeSymbol)templateType, node.constructor),
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

        if (TypeContainsUnexpandedTemplate(node.method.containingType, out var templateType)) {
            node = node.Update(
                node.receiver,
                ReplaceMethodOwner((NamedTypeSymbol)templateType, node.method),
                node.arguments,
                node.argumentRefKinds,
                node.defaultArguments,
                node.resultKind,
                node.type
            );
        }

        if (MethodContainsUnexpandedTemplate(node.method, out var templateMethod)) {
            node = node.Update(
                node.receiver,
                templateMethod,
                node.arguments,
                node.argumentRefKinds,
                node.defaultArguments,
                node.resultKind,
                templateMethod.returnType
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

        if (TypeContainsUnexpandedTemplate(node.targetMethod.containingType, out var templateType)) {
            node = node.Update(
                node.receiver,
                ReplaceMethodOwner((NamedTypeSymbol)templateType, node.targetMethod),
                node.type
            );
        }

        if (MethodContainsUnexpandedTemplate(node.targetMethod, out var templateMethod)) {
            node = node.Update(
                node.receiver,
                templateMethod,
                node.type
            );
        }

        return base.VisitFunctionLoad(node);
    }

    internal override BoundNode VisitFunctionPointerLoad(BoundFunctionPointerLoad node) {
        if (_secondaryMethodsMap.TryGetValue(node.targetMethod, out var replacementMethod)) {
            node = node.Update(
                replacementMethod,
                node.constrainedToType,
                node.type
            );
        }

        if (TypeContainsUnexpandedTemplate(node.targetMethod.containingType, out var templateType)) {
            node = node.Update(
                ReplaceMethodOwner((NamedTypeSymbol)templateType, node.targetMethod),
                node.constrainedToType,
                node.type
            );
        }

        if (MethodContainsUnexpandedTemplate(node.targetMethod, out var templateMethod)) {
            node = node.Update(
                templateMethod,
                node.constrainedToType,
                node.type
            );
        }

        return base.VisitFunctionPointerLoad(node);
    }
}
