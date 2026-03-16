using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal abstract partial class MethodToClassRewriter : BoundTreeRewriterWithStackGuard {
    private protected readonly Dictionary<DataContainerSymbol, DataContainerSymbol> _localMap = [];
    private protected readonly TypeCompilationState _compilationState;
    private protected readonly BelteDiagnosticQueue _diagnostics;

    private protected Dictionary<Symbol, CapturedSymbolReplacement> _proxies = [];

    private protected MethodToClassRewriter(TypeCompilationState compilationState, BelteDiagnosticQueue diagnostics) {
        _compilationState = compilationState;
        _diagnostics = diagnostics;
    }

    private protected abstract TemplateMap _templateMap { get; }

    private protected abstract MethodSymbol _currentMethod { get; }

    private protected abstract NamedTypeSymbol _containingType { get; }

    private protected abstract BoundExpression FramePointer(SyntaxNode syntax, NamedTypeSymbol frameClass);

    internal override BoundNode DefaultVisit(BoundNode node) {
        return base.DefaultVisit(node);
    }

    private protected abstract bool NeedsProxy(Symbol localOrParameter);

    private protected void RewriteLocals(
        ImmutableArray<DataContainerSymbol> locals,
        ArrayBuilder<DataContainerSymbol> newLocals) {
        foreach (var local in locals) {
            if (TryRewriteLocal(local, out var newLocal))
                newLocals.Add(newLocal);
        }
    }

    private protected bool TryRewriteLocal(DataContainerSymbol local, out DataContainerSymbol newLocal) {
        if (NeedsProxy(local)) {
            newLocal = null;
            return false;
        }

        if (_localMap.TryGetValue(local, out newLocal))
            return true;

        var newType = VisitType(local.type);

        if (TypeSymbol.Equals(newType, local.type, TypeCompareKind.ConsiderEverything)) {
            newLocal = local;
        } else {
            newLocal = new TypeSubstitutedLocalSymbol(local, new TypeWithAnnotations(newType), _currentMethod);
            _localMap.Add(local, newLocal);
        }

        return true;
    }

    private ImmutableArray<DataContainerSymbol> RewriteLocals(ImmutableArray<DataContainerSymbol> locals) {
        if (locals.IsEmpty)
            return locals;

        var newLocals = ArrayBuilder<DataContainerSymbol>.GetInstance();
        RewriteLocals(locals, newLocals);
        return newLocals.ToImmutableAndFree();
    }

    internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
        var newLocals = RewriteLocals(node.locals);
        var newLocalFunctions = node.localFunctions;
        var newStatements = VisitList(node.statements);
        return node.Update(newStatements, newLocals, newLocalFunctions);
    }

    internal override BoundNode VisitForStatement(BoundForStatement node) {
        var newOuterLocals = RewriteLocals(node.locals);
        var initializer = (BoundStatement)Visit(node.initializer);
        var newInnerLocals = RewriteLocals(node.innerLocals);
        var condition = (BoundExpression)Visit(node.condition);
        var increment = (BoundStatement)Visit(node.step);
        var body = (BoundStatement)Visit(node.body);

        return node.Update(
            newOuterLocals,
            initializer,
            newInnerLocals,
            condition,
            increment,
            body,
            node.breakLabel,
            node.continueLabel
        );
    }

    internal override BoundNode VisitDoWhileStatement(BoundDoWhileStatement node) {
        var newLocals = RewriteLocals(node.locals);
        var condition = (BoundExpression)Visit(node.condition);
        var body = (BoundStatement)Visit(node.body);
        return node.Update(newLocals, condition, body, node.breakLabel, node.continueLabel);
    }

    internal override BoundNode VisitWhileStatement(BoundWhileStatement node) {
        var newLocals = RewriteLocals(node.locals);
        var condition = (BoundExpression)Visit(node.condition);
        var body = (BoundStatement)Visit(node.body);
        return node.Update(newLocals, condition, body, node.breakLabel, node.continueLabel);
    }

    // internal override BoundNode VisitUsingStatement(BoundUsingStatement node) {
    //     var newLocals = RewriteLocals(node.Locals);
    //     var declarations = (BoundMultipleLocalDeclarations?)this.Visit(node.DeclarationsOpt);
    //     var expression = (BoundExpression?)this.Visit(node.ExpressionOpt);
    //     var body = (BoundStatement)this.Visit(node.Body);
    //     return node.Update(newLocals, declarations, expression, body, node.AwaitOpt, node.PatternDisposeInfoOpt);
    // }

    internal sealed override TypeSymbol VisitType(TypeSymbol type) {
        return _templateMap.SubstituteType(type)?.type?.type;
    }


    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        var rewrittenMethodSymbol = VisitMethodSymbol(node.method);
        var rewrittenReceiver = (BoundExpression)Visit(node.receiver);
        var rewrittenArguments = VisitList(node.arguments);
        var rewrittenType = VisitType(node.type);

        if (BaseReferenceInReceiverWasRewritten(node.receiver, rewrittenReceiver) && node.method.IsMetadataVirtual())
            rewrittenMethodSymbol = GetMethodWrapperForBaseNonVirtualCall(rewrittenMethodSymbol, node.syntax);

        return node.Update(
            rewrittenReceiver,
            rewrittenMethodSymbol,
            rewrittenArguments,
            node.argumentRefKinds,
            node.defaultArguments,
            node.resultKind,
            rewrittenType
        );
    }

    internal override BoundNode VisitBinaryOperator(BoundBinaryOperator node) {
        return node.Update(
            (BoundExpression)Visit(node.left),
            (BoundExpression)Visit(node.right),
            node.operatorKind,
            VisitMethodSymbol(node.method),
            node.constantValue,
            VisitType(node.type)
        );
    }

    internal override BoundNode VisitUnaryOperator(BoundUnaryOperator node) {
        return node.Update(
            (BoundExpression)Visit(node.operand),
            node.operatorKind,
            VisitMethodSymbol(node.method),
            node.constantValue,
            VisitType(node.type)
        );
    }

    internal override BoundNode VisitCastExpression(BoundCastExpression node) {
        var conversion = node.conversion;

        // if (conversion.method is not null) {
        //     conversion = conversion.SetConversionMethod(VisitMethodSymbol(conversion.Method));
        // }

        return node.Update(
            (BoundExpression)Visit(node.operand),
            conversion,
            node.constantValue,
            VisitType(node.type)
        );
    }

    private MethodSymbol GetMethodWrapperForBaseNonVirtualCall(MethodSymbol methodBeingCalled, SyntaxNode syntax) {
        var newMethod = GetOrCreateBaseFunctionWrapper(methodBeingCalled, syntax);

        if (!newMethod.isTemplateMethod)
            return newMethod;

        var typeArgs = methodBeingCalled.templateArguments;

        var visitedTypeArgs = ArrayBuilder<TypeOrConstant>.GetInstance(typeArgs.Length);

        foreach (var typeArg in typeArgs)
            visitedTypeArgs.Add(new TypeOrConstant(VisitType(typeArg.type.type)));

        return newMethod.Construct(visitedTypeArgs.ToImmutableAndFree());
    }

    private MethodSymbol GetOrCreateBaseFunctionWrapper(MethodSymbol methodBeingWrapped, SyntaxNode syntax) {
        methodBeingWrapped = methodBeingWrapped.constructedFrom;

        var wrapper = _compilationState.GetMethodWrapper(methodBeingWrapped);

        if (wrapper is not null)
            return wrapper;

        var containingType = _containingType;

        var methodName = GeneratedNames.MakeBaseMethodWrapperName(_compilationState.nextWrapperMethodIndex);
        wrapper = new BaseMethodWrapperSymbol(containingType, methodBeingWrapped, syntax, methodName);

        // _compilationState.AddSynthesizedDefinition(containingType, wrapper.GetCciAdapter());

        wrapper.GenerateMethodBody(_compilationState, _diagnostics);
        return wrapper;
    }

    private bool TryReplaceWithProxy(Symbol parameterOrLocal, SyntaxNode syntax, out BoundNode replacement) {
        if (_proxies.TryGetValue(parameterOrLocal, out var proxy)) {
            replacement = proxy.Replacement(
                syntax,
                static (frameType, arg) => arg.self.FramePointer(arg.syntax, frameType),
                (syntax, self: this)
            );

            return true;
        }

        replacement = null;
        return false;
    }

    internal sealed override BoundNode VisitParameterExpression(BoundParameterExpression node) {
        if (TryReplaceWithProxy(node.parameter, node.syntax, out var replacement))
            return replacement;

        return VisitUnhoistedParameter(node);
    }

    private protected virtual BoundNode VisitUnhoistedParameter(BoundParameterExpression node) {
        return base.VisitParameterExpression(node);
    }

    internal sealed override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        if (TryReplaceWithProxy(node.dataContainer, node.syntax, out var replacement))
            return replacement;

        return VisitUnhoistedLocal(node);
    }

    private bool TryGetHoistedField(Symbol variable, out FieldSymbol field) {
        if (_proxies.TryGetValue(variable, out var proxy)) {
            field = proxy switch {
                // CapturedToStateMachineFieldReplacement stateMachineProxy => (FieldSymbol)stateMachineProxy.HoistedField,
                CapturedToFrameSymbolReplacement closureProxy => closureProxy.hoistedField,
                _ => throw ExceptionUtilities.UnexpectedValue(proxy)
            };

            return true;
        }

        field = null;
        return false;
    }

    private BoundNode VisitUnhoistedLocal(BoundDataContainerExpression node) {
        if (_localMap.TryGetValue(node.dataContainer, out var replacementLocal)) {
            return new BoundDataContainerExpression(
                node.syntax,
                replacementLocal,
                node.constantValue,
                replacementLocal.type,
                node.hasErrors
            );
        }

        return base.VisitDataContainerExpression(node);
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        var local = node.declaration.dataContainer;

        if (!NeedsProxy(local))
            return base.VisitLocalDeclarationStatement(node);

        var assignment = BoundFactory.Assignment(
            node.syntax,
            new BoundDataContainerExpression(node.syntax, local, null, local.type),
            node.declaration.initializer,
            local.isRef,
            local.type
        );

        var statement = new BoundExpressionStatement(node.syntax, assignment);
        return Visit(statement);
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
        var originalLeft = node.left;

        if (originalLeft.kind != BoundKind.DataContainerExpression)
            return base.VisitAssignmentOperator(node);

        var leftLocal = (BoundDataContainerExpression)originalLeft;
        var originalRight = node.right;

        if (leftLocal.dataContainer.refKind != RefKind.None &&
            node.isRef &&
            NeedsProxy(leftLocal.dataContainer)) {
            throw ExceptionUtilities.Unreachable();
        }

        if (NeedsProxy(leftLocal.dataContainer) && !_proxies.ContainsKey(leftLocal.dataContainer))
            throw ExceptionUtilities.Unreachable();

        var rewrittenLeft = (BoundExpression)Visit(leftLocal);
        var rewrittenRight = (BoundExpression)Visit(originalRight);
        var rewrittenType = VisitType(node.type);

        return node.Update(rewrittenLeft, rewrittenRight, node.isRef, rewrittenType);
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        var receiverOpt = (BoundExpression)Visit(node.receiver);
        var type = VisitType(node.type);
        var fieldSymbol = node.field.originalDefinition
            .AsMember((NamedTypeSymbol)VisitType(node.field.containingType));
        return node.Update(receiverOpt, fieldSymbol, node.constantValue, type);
    }

    internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
        var rewritten = (BoundObjectCreationExpression?)base.VisitObjectCreationExpression(node);

        if (!TypeSymbol.Equals(rewritten.type, node.type, TypeCompareKind.ConsiderEverything) &&
            node.constructor is not null) {
            var ctor = VisitMethodSymbol(node.constructor);
            rewritten = rewritten.Update(
                ctor,
                rewritten.arguments,
                rewritten.argumentRefKinds,
                rewritten.argsToParams,
                rewritten.defaultArguments,
                rewritten.wasTargetTyped,
                rewritten.type
            );
        }

        return rewritten;
    }

    // public override BoundNode VisitLoweredConditionalAccess(BoundLoweredConditionalAccess node) {
    //     var receiver = (BoundExpression)this.Visit(node.Receiver);
    //     var whenNotNull = (BoundExpression)this.Visit(node.WhenNotNull);
    //     var whenNullOpt = (BoundExpression?)this.Visit(node.WhenNullOpt);
    //     TypeSymbol type = this.VisitType(node.Type);
    //     return node.Update(receiver, VisitMethodSymbol(node.HasValueMethodOpt), whenNotNull, whenNullOpt, node.Id, node.ForceCopyOfNullableValueType, type);
    // }

    private protected MethodSymbol VisitMethodSymbol(MethodSymbol method) {
        if (method is null)
            return null;

        // if (method.containingType.IsAnonymousType) {
        //     //  Method of an anonymous type
        //     var newType = (NamedTypeSymbol)TypeMap.SubstituteType(method.ContainingType).AsTypeSymbolOnly();
        //     if (ReferenceEquals(newType, method.ContainingType)) {
        //         //  Anonymous type symbol was not rewritten
        //         return method;
        //     }

        //     //  get a new method by name
        //     foreach (var member in newType.GetMembers(method.Name)) {
        //         if (member.Kind == SymbolKind.Method) {
        //             return (MethodSymbol)member;
        //         }
        //     }

        //     throw ExceptionUtilities.Unreachable();
        // } else {
        //  Method of a regular type
        return method.originalDefinition
            .AsMember((NamedTypeSymbol)_templateMap.SubstituteType(method.containingType).type.type)
            .ConstructIfTemplate(
                _templateMap.SubstituteTypes(method.templateArguments.Select(t => t.type).ToImmutableArray()
            ));
        // }
    }

    private FieldSymbol VisitFieldSymbol(FieldSymbol field) {
        return field.originalDefinition
            .AsMember((NamedTypeSymbol)_templateMap.SubstituteType(field.containingType).type.type);
    }

    private static bool BaseReferenceInReceiverWasRewritten(
        BoundExpression originalReceiver,
        BoundExpression rewrittenReceiver) {
        return originalReceiver is { kind: BoundKind.BaseExpression } &&
               rewrittenReceiver is { kind: not BoundKind.BaseExpression };
    }
}
