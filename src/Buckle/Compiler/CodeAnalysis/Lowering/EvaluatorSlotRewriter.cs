using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class EvaluatorSlotRewriter : BoundTreeRewriter {
    private readonly Dictionary<NamedTypeSymbol, EvaluatorSlotManager> _typeLayouts;
    private readonly BoundProgram _previous;

    private int _lateTempCount;

    internal readonly EvaluatorSlotManager localSlotManager;

    private EvaluatorSlotRewriter(
        MethodSymbol method,
        Dictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts,
        BoundProgram previous) {
        _typeLayouts = typeLayouts;
        _previous = previous;
        localSlotManager = new EvaluatorSlotManager(method);
    }

    internal static BoundBlockStatement Rewrite(
        MethodSymbol method,
        BoundStatement statement,
        Dictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts,
        BoundProgram previous,
        out EvaluatorSlotManager slotManager) {
        var rewriter = new EvaluatorSlotRewriter(method, typeLayouts, previous);
        rewriter.AssignParameterSlots(method);
        var rewrittenBlock = (BoundBlockStatement)rewriter.Visit(statement);

        slotManager = rewriter.localSlotManager;
        slotManager.lateTempCount = rewriter._lateTempCount;
        return rewrittenBlock;
    }

    private void AssignParameterSlots(MethodSymbol method) {
        if (!method.isStatic)
            localSlotManager.AllocateSlot(method.thisParameter.type, LocalSlotConstraints.None);
        else
            // The type here doesn't actually matter beyond the fact that it is something
            localSlotManager.AllocateSlot(method.returnType, LocalSlotConstraints.None);

        if (method.arity > 0) {
            foreach (var templateParameter in method.templateParameters) {
                localSlotManager.DeclareLocal(
                    templateParameter.underlyingType.type,
                    templateParameter,
                    templateParameter.name,
                    SynthesizedLocalKind.UserDefined,
                    LocalSlotConstraints.None,
                    false
                );
            }
        }

        for (var i = 0; i < method.parameterCount; i++) {
            var parameter = method.parameters[i];
            var constraints = (!method.parameterRefKinds.IsDefault && method.parameterRefKinds[i] != RefKind.None)
                ? LocalSlotConstraints.ByRef
                : LocalSlotConstraints.None;

            localSlotManager.DeclareLocal(
                parameter.type,
                parameter,
                parameter.name,
                SynthesizedLocalKind.UserDefined,
                constraints,
                false
            );
        }
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        var local = node.declaration.dataContainer;
        var syntax = node.syntax;

        if (!local.isGlobal) {
            localSlotManager.DeclareLocal(
                local.type,
                local,
                local.name,
                local.synthesizedKind,
                local.isRef ? LocalSlotConstraints.ByRef : LocalSlotConstraints.None,
                false
            );
        }

        if (local.isRef && node.declaration.initializer.IsLiteralNull())
            _lateTempCount++;

        return Visit(new BoundExpressionStatement(syntax,
            Assignment(node.syntax,
                new BoundDataContainerExpression(syntax, local, null, local.type),
                node.declaration.initializer,
                local.isRef,
                local.type
            )
        ));
    }

    internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        var local = node.dataContainer;

        if (!local.isGlobal) {
            var slot = localSlotManager.GetLocal(local).slot;
            return new BoundStackSlotExpression(node.syntax, node, local, slot, node.Type());
        }

        return node;
    }

    internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
        var parameter = node.parameter;
        var slot = localSlotManager.GetLocal(parameter).slot;
        return new BoundStackSlotExpression(node.syntax, node, parameter, slot, node.Type());
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        var field = node.field;
        var receiver = (BoundExpression)Visit(node.receiver);
        var receiverType = (receiver?.StrippedType() ?? field.containingType).originalDefinition;

        if (!_typeLayouts.TryGetValue((NamedTypeSymbol)receiverType, out var layout)) {
            if (!_previous.TryGetTypeLayoutIncludingParents((NamedTypeSymbol)receiverType, out layout))
                throw ExceptionUtilities.Unreachable();
        }

        var slot = layout.GetLocal(field).slot;
        return new BoundFieldSlotExpression(node.syntax, node, receiver, field, slot, node.Type());
    }

    internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
        _lateTempCount++;
        return base.VisitObjectCreationExpression(node);
    }

    internal override BoundNode VisitArrayCreationExpression(BoundArrayCreationExpression node) {
        _lateTempCount++;
        return base.VisitArrayCreationExpression(node);
    }

    internal override BoundNode VisitSwitchDispatch(BoundSwitchDispatch node) {
        _lateTempCount++;
        return base.VisitSwitchDispatch(node);
    }

    internal override BoundNode VisitCompileTimeExpression(BoundCompileTimeExpression node) {
        var structStack = new Stack<NamedTypeSymbol>();

        if (node.Type().IsStructType())
            structStack.Push((NamedTypeSymbol)node.Type());

        while (structStack.Count > 0) {
            _lateTempCount++;
            var structType = structStack.Pop();

            foreach (var member in structType.GetMembers()) {
                if (member is FieldSymbol f && f.type.IsStructType())
                    structStack.Push((NamedTypeSymbol)f.type);
            }
        }

        return base.VisitCompileTimeExpression(node);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        var method = node.method;

        if (method.containingType?.Equals(GraphicsLibrary.Graphics) == true && GraphicsLibrary.MethodProducesTemp(method))
            _lateTempCount++;

        if (node.receiver is not null && node.receiver.type.StrippedType().IsStructType())
            _lateTempCount++;

        return base.VisitCallExpression(node);
    }
}
