using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Rewrites method signatures and bodies if they contain instantiations of non-type template types
/// </summary>
internal sealed partial class TemplateExpander : BoundTreeRewriterWithStackGuard {
    private const int MaxTemplateRecursionDepth = 512;

    private readonly BelteDiagnosticQueue _diagnostics;

    private readonly ArrayBuilder<SynthesizedTemplateType> _typesBuilder;
    private readonly ImmutableDictionary<MethodSymbol, BoundBlockStatement>.Builder _methodsBuilder;

    private readonly Dictionary<ConstructedNamedTypeSymbol, SynthesizedTemplateType> _typesMap
        = new Dictionary<ConstructedNamedTypeSymbol, SynthesizedTemplateType>(
            new TemplateInstantiationComparer<NamedTypeSymbol>()
        );

    private readonly Dictionary<ConstructedMethodSymbol, SynthesizedTemplateMethod> _methodsMap
        = new Dictionary<ConstructedMethodSymbol, SynthesizedTemplateMethod>(
            new TemplateInstantiationComparer<ConstructedMethodSymbol>()
        );

    private readonly Dictionary<(SynthesizedTemplateType, MethodSymbol), SynthesizedTemplateTypeMethod> _typeMethodsMap = [];
    private readonly Dictionary<(SynthesizedTemplateType, FieldSymbol), SynthesizedTemplateTypeField> _typeFieldsMap = [];

    private ImmutableDictionary<MethodSymbol, MethodSymbol> _initialMethodRewriteMap;

    private readonly Dictionary<DataContainerSymbol, DataContainerSymbol> _localMap = [];

    private readonly Queue<TemplateInstantiation> _templateQueue = [];

    private TemplateInstantiation _currentInstantiation;
    private MethodSymbol _currentMethod;
    private MethodSymbol _replacementMethod;

    private TextLocation _currentLocation;

    private bool _inResolutionStage = false;

    internal TemplateExpander(
        ArrayBuilder<SynthesizedTemplateType> typesBuilder,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement>.Builder methodsBuilder,
        BelteDiagnosticQueue diagnostics) {
        _typesBuilder = typesBuilder;
        _methodsBuilder = methodsBuilder;
        _diagnostics = diagnostics;
    }

    internal bool InitialMethodSymbolRewrite(MethodSymbol method, out MethodSymbol newMethod) {
        // All methods should be rewritten before the main pass
        Debug.Assert(_initialMethodRewriteMap is null);
        Debug.Assert(!_inResolutionStage);

        var anyTemplates = TypeContainsUnexpandedTemplate(method.returnType, out var newReturnType);
        var builder = ArrayBuilder<ParameterSymbol>.GetInstance(method.parameterCount);

        foreach (var parameter in method.parameters) {
            var paramIsTemplate = TypeContainsUnexpandedTemplate(parameter.type, out var newParamType);
            var newParameter = paramIsTemplate
                ? new TypeSubstitutedParameterSymbol(parameter, new TypeWithAnnotations(newParamType))
                : parameter;

            anyTemplates |= paramIsTemplate;
            builder.Add(newParameter);
        }

        if (!anyTemplates) {
            newMethod = null;
            return false;
        }

        newMethod = new TypeSubstitutedMethodSymbol(
            method,
            new TypeWithAnnotations(newReturnType),
            builder.ToImmutableAndFree()
        );

        return true;
    }

    internal void SetInitialMethodRewriteMap(ImmutableDictionary<MethodSymbol, MethodSymbol> methodMap) {
        Debug.Assert(_initialMethodRewriteMap is null);
        Debug.Assert(!_inResolutionStage);
        _initialMethodRewriteMap = methodMap;
    }

    internal bool TryInitialMethodBodyRewrite(
        MethodSymbol method,
        BoundBlockStatement body,
        out MethodSymbol newMethod,
        out BoundBlockStatement newBody) {
        Debug.Assert(_initialMethodRewriteMap is not null);
        Debug.Assert(!_inResolutionStage);

        _currentMethod = method;

        if (_initialMethodRewriteMap.TryGetValue(method, out var value))
            _replacementMethod = value;

        newBody = (BoundBlockStatement)VisitBlockStatement(body);
        newMethod = _replacementMethod;

        return newBody != body;
    }

    internal void ResolveTemplates(ConcurrentDictionary<MethodSymbol, BoundBlockStatement> methodBodies) {
        // These are only used by the initial method pass
        _currentMethod = null;
        _replacementMethod = null;
        _inResolutionStage = true;

        while (_templateQueue.Count != 0) {
            var instantiation = _templateQueue.Dequeue();
            _currentInstantiation = instantiation;

            var template = instantiation.template;

            if (template is SynthesizedTemplateMethod templateMethod)
                ResolveTemplateMethod(templateMethod, methodBodies);
            else if (template is SynthesizedTemplateType templateType)
                ResolveTemplateType(templateType, methodBodies);
            else
                throw ExceptionUtilities.Unreachable();
        }
    }

    private void ResolveTemplateMethod(
        SynthesizedTemplateMethod templateMethod,
        ConcurrentDictionary<MethodSymbol, BoundBlockStatement> methodBodies) {
        Debug.Assert(_inResolutionStage);
        var originalDefinition = templateMethod.unexpandedSymbol.originalDefinition;

        var body = _methodsBuilder.TryGetValue(originalDefinition, out var originalBody)
            ? originalBody
            : methodBodies[originalDefinition];

        _replacementMethod = templateMethod;
        var newBody = (BoundBlockStatement)Visit(body);
        _methodsBuilder.Add(templateMethod, newBody);
    }

    private void ResolveTemplateType(
        SynthesizedTemplateType templateType,
        ConcurrentDictionary<MethodSymbol, BoundBlockStatement> methodBodies) {
        Debug.Assert(_inResolutionStage);
        var originalDefinition = templateType.unexpandedSymbol.originalDefinition;

        foreach (var (method, body) in methodBodies) {
            if (method.containingType.originalDefinition.Equals(originalDefinition)) {
                if (!_typeMethodsMap.TryGetValue((templateType, method), out var newMethod)) {
                    newMethod = new SynthesizedTemplateTypeMethod(this, templateType, method);
                    _typeMethodsMap.Add((templateType, method), newMethod);
                }

                _replacementMethod = newMethod;
                var newBody = (BoundBlockStatement)Visit(body);
                _methodsBuilder.Add(newMethod, newBody);
            }
        }
    }

    private bool TryEnqueue(ISynthesizedTemplate template, Symbol cause, TextLocation location) {
        var instantiation = new TemplateInstantiation(
            template,
            cause,
            _currentInstantiation ?? _templateQueue.LastOrDefault(),
            location
        );

        _templateQueue.Enqueue(instantiation);

        if (_templateQueue.Count > MaxTemplateRecursionDepth) {
            ReportTemplateRecursion(instantiation);
            return false;
        }

        return true;
    }

    private void ReportTemplateRecursion(TemplateInstantiation instantiation) {
        var cause = instantiation.cause;
        var template = instantiation.template;

        if (cause is not null) {
            _diagnostics.Push(Error.TemplateRecursionWithCause(
                cause.location,
                (Symbol)template,
                ((Symbol)template.unexpandedSymbol).originalDefinition,
                cause.kind.Localize(),
                cause
            ));
        } else {
            // TODO Add better reporting as it comes up (i.e. walking parent instantiations if cause is not given)
            throw ExceptionUtilities.Unreachable();
            // else
            //     _diagnostics.Push(Error.TemplateRecursion(template, template.unexpandedSymbol));
        }
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

    internal TypeWithAnnotations SubstituteType(
        TypeWithAnnotations type,
        ISynthesizedTemplate newOwner,
        Symbol cause,
        TextLocation location) {
        type = TemplateTypeReplacer<TemplateParameterSymbol, TemplateParameterSymbol, TemplateParameterSymbol>
            .Replace(type, newOwner.replacementTemplateParameters);
        type = type.SubstituteType((newOwner as ISymbolWithTemplates).templateSubstitution).type;
        type = new TypeWithAnnotations(VisitTypeCore(type.type, cause, location));
        return type;
    }

    private static bool IsGenericOnly(ISymbolWithTemplates symbol) {
        foreach (var templateParameter in symbol.templateParameters) {
            if (templateParameter.underlyingType.specialType != SpecialType.Type)
                return false;
        }

        return true;
    }

    private static bool IsGenericOrPlaceholderOnly(ISymbolWithTemplates symbol) {
        Debug.Assert(symbol.arity == symbol.templateParameters.Length && symbol.arity == symbol.templateArguments.Length);

        for (var i = 0; i < symbol.arity; i++) {
            if (symbol.templateParameters[i].underlyingType.specialType != SpecialType.Type) {
                if (symbol.templateArguments[i].isType)
                    Debug.Assert(symbol.templateArguments[i].type.type is TemplateParameterSymbol);
                else
                    return false;
            }
        }

        return true;
    }

    private bool NoteType(TypeSymbol type, Symbol cause, TextLocation location) {
        if (type is not ConstructedNamedTypeSymbol constructed || IsGenericOrPlaceholderOnly(constructed))
            return false;

        // These will be simplified and noted later
        if (ContainsExpressionConstants(constructed))
            return false;

        if (_typesMap.ContainsKey(constructed))
            return true;

        var containingSymbol = constructed.containingSymbol is TypeSymbol containingType
            ? VisitType(containingType)
            : constructed.containingSymbol;

        var synthesizedType = new SynthesizedTemplateType(this, containingSymbol, constructed);
        _typesMap.Add(constructed, synthesizedType);
        _typesBuilder.Add(synthesizedType);

        if (!TryEnqueue(synthesizedType, cause, location))
            return false;

        synthesizedType.NoteFields(_typeFieldsMap);

        return true;

        static bool ContainsExpressionConstants(ConstructedNamedTypeSymbol type) {
            foreach (var templateArgument in type.templateArguments) {
                if (templateArgument.isConstant && templateArgument.constant is TemplateConstantValue)
                    return true;
            }

            return false;
        }
    }

    private MethodSymbol ReplaceMethodOwner(NamedTypeSymbol newOwner, MethodSymbol method) {
        // This is for when rewriting method calls on a template type directly
        if (newOwner.originalDefinition is SynthesizedTemplateType templateOwner) {
            var originalDefinition = method.originalDefinition;

            if (_typeMethodsMap.TryGetValue((templateOwner, originalDefinition), out var result))
                return ConstructIfApplicable(result);

            var templateMethod = new SynthesizedTemplateTypeMethod(this, templateOwner, originalDefinition);
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

    private FieldSymbol ReplaceFieldOwner(NamedTypeSymbol newOwner, FieldSymbol field) {
        if (newOwner.originalDefinition is SynthesizedTemplateType templateOwner) {
            var originalDefinition = field.originalDefinition;

            if (_typeFieldsMap.TryGetValue((templateOwner, originalDefinition), out var result))
                return ConstructIfApplicable(result);

            // The map should be fully populated when the template type is noted
            throw ExceptionUtilities.Unreachable();
        } else {
            Debug.Assert(newOwner is ConstructedNamedTypeSymbol);
            return field.originalDefinition.AsMember(newOwner);
        }

        FieldSymbol ConstructIfApplicable(FieldSymbol synthesizedField) {
            if (newOwner is ConstructedNamedTypeSymbol)
                return synthesizedField.AsMember(newOwner);

            return synthesizedField;
        }
    }

    private bool TypeContainsUnexpandedTemplate(TypeSymbol type, out TypeSymbol replacedType) {
        replacedType = VisitType(type);

        if (TypeSymbol.Equals(type, replacedType, TypeCompareKind.ConsiderEverything))
            return false;

        return true;
    }

    private bool MethodContainsUnexpandedTemplate(
        MethodSymbol method,
        out MethodSymbol replacedMethod,
        Symbol cause,
        TextLocation location) {
        if (method is not ConstructedMethodSymbol constructed || IsGenericOrPlaceholderOnly(constructed)) {
            replacedMethod = null;
            return false;
        }

        if (_methodsMap.TryGetValue(constructed, out var templateMethod)) {
            replacedMethod = templateMethod;
            return true;
        }

        var synthesizedMethod = new SynthesizedTemplateMethod(method.containingSymbol, constructed);
        _methodsMap.Add(constructed, synthesizedMethod);

        if (!TryEnqueue(synthesizedMethod, cause, location)) {
            replacedMethod = null;
            return false;
        }

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

    internal override BoundNode Visit(BoundNode node) {
        if (node?.syntax?.location is not null)
            _currentLocation = node.syntax.location;

        return base.Visit(node);
    }

    internal override TypeSymbol VisitType(TypeSymbol type) {
        if (type is null)
            return null;

        if (_currentInstantiation is not null) {
            return SubstituteType(
                new TypeWithAnnotations(type),
                _currentInstantiation.template,
                null,
                _currentLocation
            ).type;
        } else {
            return VisitTypeCore(type, null, _currentLocation);
        }
    }

    private TypeSymbol VisitTypeCore(TypeSymbol type, Symbol cause, TextLocation location) {
        if (type is not null) {
            type.VisitType(VisitTypePredicate, (this, cause, location));

            return TemplateTypeReplacer<ConstructedNamedTypeSymbol, SynthesizedTemplateType, NamedTypeSymbol>.Replace(
                type,
                _typesMap,
                ConstructIfApplicable
            );
        }

        return type;

        static bool VisitTypePredicate(
            TypeSymbol type,
            (TemplateExpander expander, Symbol cause, TextLocation location) argument,
            bool canDigThroughNullable = true) {
            argument.expander.NoteType(type, argument.cause, argument.location);
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
        if (_initialMethodRewriteMap.TryGetValue(node.constructor, out var replacementMethod)) {
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
        if (_initialMethodRewriteMap.TryGetValue(node.method, out var replacementMethod)) {
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

        if (MethodContainsUnexpandedTemplate(node.method, out var templateMethod, null, node.syntax.location)) {
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

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        if (Binder.IsThisInstanceAccess(node) && _currentInstantiation is not null) {
            Debug.Assert(_inResolutionStage);

            if (_currentInstantiation.template is SynthesizedTemplateType t) {
                node = node.Update(
                    node.receiver,
                    _typeFieldsMap[(t, node.field)],
                    node.constantValue,
                    node.type
                );
            } else {
                throw ExceptionUtilities.Unreachable();
            }
        }

        if (TypeContainsUnexpandedTemplate(node.field.containingType, out var templateType)) {
            Debug.Assert(!_inResolutionStage || _currentInstantiation is not null);
            Debug.Assert(!(Binder.IsThisInstanceAccess(node) && _currentInstantiation is not null));

            node = node.Update(
                node.receiver,
                ReplaceFieldOwner((NamedTypeSymbol)templateType, node.field),
                node.constantValue,
                node.type
            );
        }

        return base.VisitFieldAccessExpression(node);
    }

    internal override BoundNode VisitFunctionLoad(BoundFunctionLoad node) {
        if (_initialMethodRewriteMap.TryGetValue(node.targetMethod, out var replacementMethod)) {
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

        if (MethodContainsUnexpandedTemplate(node.targetMethod, out var templateMethod, null, node.syntax.location)) {
            node = node.Update(
                node.receiver,
                templateMethod,
                node.type
            );
        }

        return base.VisitFunctionLoad(node);
    }

    internal override BoundNode VisitFunctionPointerLoad(BoundFunctionPointerLoad node) {
        if (_initialMethodRewriteMap.TryGetValue(node.targetMethod, out var replacementMethod)) {
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

        if (MethodContainsUnexpandedTemplate(node.targetMethod, out var templateMethod, null, node.syntax.location)) {
            node = node.Update(
                templateMethod,
                node.constrainedToType,
                node.type
            );
        }

        return base.VisitFunctionPointerLoad(node);
    }

    internal override BoundNode VisitTypeExpression(BoundTypeExpression node) {
        if (node.type is TemplateParameterSymbol templateParameter && _currentInstantiation is not null &&
            templateParameter.containingSymbol.Equals(
                ((Symbol)_currentInstantiation.template.unexpandedSymbol
            ).originalDefinition)) {
            Debug.Assert(_inResolutionStage);

            var template = _currentInstantiation.template;

            if (templateParameter.underlyingType.specialType != SpecialType.Type) {
                var typeOrConstant = template.unexpandedSymbol.templateSubstitution
                    .SubstituteType(templateParameter);

                if (typeOrConstant.isConstant) {
                    return new BoundLiteralExpression(
                        node.syntax,
                        typeOrConstant.constant,
                        templateParameter.underlyingType.type
                    );
                }
            } else {
                if (template.replacementTemplateParameters.TryGetValue(templateParameter, out var value))
                    return new BoundTypeExpression(node.syntax, null, null, value);
            }
        }

        return base.VisitTypeExpression(node);
    }
}
