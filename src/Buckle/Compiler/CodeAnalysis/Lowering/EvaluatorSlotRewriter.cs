using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class EvaluatorSlotRewriter : BoundTreeRewriter {
    private readonly Dictionary<NamedTypeSymbol, EvaluatorSlotManager> _typeLayouts;

    private int _lateTempCount;

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
        slotManager.lateTempCount = rewriter._lateTempCount;
        return rewrittenBlock;
    }

    private void AssignParameterSlots(MethodSymbol method) {
        // TODO Check there isn't duplication happening here
        if (!method.isStatic)
            localSlotManager.AllocateSlot(method.thisParameter.type, LocalSlotConstraints.None);

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
        var receiver = (BoundExpression)Visit(node.receiver);
        var field = node.field;
        var layout = _typeLayouts[(NamedTypeSymbol)receiver.type.StrippedType()];
        var slot = layout.GetLocal(field).slot;
        return new BoundFieldSlotExpression(node.syntax, node, receiver, field, slot, node.type);
    }

    internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
        _lateTempCount++;
        return base.VisitObjectCreationExpression(node);
    }

    internal override BoundNode VisitArrayCreationExpression(BoundArrayCreationExpression node) {
        _lateTempCount++;
        return base.VisitArrayCreationExpression(node);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression node) {
        var method = node.method;

        if (method.containingType.Equals(GraphicsLibrary.Graphics) && GraphicsLibrary.MethodProducesTemp(method))
            _lateTempCount++;

        return base.VisitCallExpression(node);
    }
}
