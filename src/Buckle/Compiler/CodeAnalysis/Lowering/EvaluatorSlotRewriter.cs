using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class EvaluatorSlotRewriter : BoundTreeRewriter {
    private readonly Dictionary<NamedTypeSymbol, EvaluatorSlotManager> _typeLayouts;

    internal readonly EvaluatorSlotManager localSlotManager;

    private EvaluatorSlotRewriter(
        MethodSymbol method,
        Dictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts) {
        _typeLayouts = typeLayouts;
        localSlotManager = new EvaluatorSlotManager(method);
    }

    internal static BoundBlockStatement Rewrite(
        MethodSymbol method,
        BoundStatement statement,
        Dictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts,
        out EvaluatorSlotManager slotManager) {
        var rewriter = new EvaluatorSlotRewriter(method, typeLayouts);
        rewriter.AssignParameterSlots(method);
        var rewrittenBlock = (BoundBlockStatement)rewriter.Visit(statement);

        slotManager = rewriter.localSlotManager;
        return rewrittenBlock;
    }

    private void AssignParameterSlots(MethodSymbol method) {
        // TODO Check there isn't duplication happening here
        if (!method.isStatic)
            localSlotManager.AllocateSlot(method.thisParameter.type, LocalSlotConstraints.None);

        for (var i = 0; i < method.parameterCount; i++) {
            var parameter = method.parameters[i];
            var constraints = (method.parameterRefKinds[i] != RefKind.None)
                ? LocalSlotConstraints.ByRef
                : LocalSlotConstraints.None;

            localSlotManager.AllocateSlot(parameter.type, constraints);
        }
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        var local = node.declaration.dataContainer;

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

        return base.VisitLocalDeclarationStatement(node);
    }

    internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
        var local = node.dataContainer;

        if (!local.isGlobal) {
            var slot = localSlotManager.GetLocal(local).slot;
            return new BoundStackSlotExpression(node.syntax, node, local, slot, node.type);
        }

        return node;
    }

    internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
        var parameter = node.parameter;
        var slot = localSlotManager.GetLocal(parameter).slot;
        return new BoundStackSlotExpression(node.syntax, node, parameter, slot, node.type);
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        var receiver = node.receiver;
        var field = node.field;
        var layout = _typeLayouts[receiver.type as NamedTypeSymbol];
        var slot = layout.GetLocal(field).slot;
        return new BoundFieldSlotExpression(node.syntax, node, receiver, field, slot, node.type);
    }
}
