using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.FlowAnalysis;

/// <summary>
/// Creates a graphical control flow graph from a <see cref="BasicBlock" />.
/// </summary>
internal sealed class ControlFlowGraph {
    private readonly Dictionary<Symbol, int> _slotMap;
    private readonly ArrayBuilder<Symbol> _symbolsBySlot;
    private readonly MultiDictionary<Symbol, Symbol> _closureCaptures;
    private readonly MethodSymbol _method;

    /// <summary>
    /// Creates a <see cref="ControlFlowGraph" />.
    /// </summary>
    /// <param name="start">Start of the graph.</param>
    /// <param name="end">End of the graph.</param>
    /// <param name="blocks">All blocks within the graph.</param>
    /// <param name="branch">All branches within the graph.</param>
    internal ControlFlowGraph(
        BasicBlock start,
        BasicBlock end,
        List<BasicBlock> blocks,
        List<ControlFlowBranch> branch,
        Dictionary<Symbol, int> slotMap,
        ArrayBuilder<Symbol> symbolsBySlot,
        MethodSymbol method) {
        this.start = start;
        this.end = end;
        this.blocks = blocks;
        branches = branch;
        _symbolsBySlot = symbolsBySlot;
        _slotMap = slotMap;
        _closureCaptures = [];
        _method = method;
    }

    /// <summary>
    /// Start graph block.
    /// </summary>
    internal BasicBlock start { get; }

    /// <summary>
    /// End graph block.
    /// </summary>
    internal BasicBlock end { get; }

    /// <summary>
    /// All BasicBlocks in the graph.
    /// </summary>
    internal List<BasicBlock> blocks { get; }

    /// <summary>
    /// All BasicBlockBranches in the graph.
    /// </summary>
    internal List<ControlFlowBranch> branches { get; }

    /// <summary>
    /// Creates a <see cref="ControlFlowGraph" /> from a <see cref="BoundBlockStatement" />.
    /// </summary>
    /// <param name="body"><see cref="BoundBlockStatement" /> to create from.</param>
    /// <returns><see cref="ControlFlowGraph" />.</returns>
    internal static ControlFlowGraph Create(MethodSymbol method, BoundBlockStatement body) {
        var (slotMap, symbolsBySlot) = SlotCounter.Count(method, body);
        var basicBlockBuilder = new ControlFlowGraphBuilder.BasicBlockBuilder(symbolsBySlot.Count);
        var blocks = basicBlockBuilder.Build(body);
        var graphBuilder = new ControlFlowGraphBuilder(method, slotMap, symbolsBySlot);
        var controlFlowGraph = graphBuilder.Build(blocks, basicBlockBuilder.regions);
        return controlFlowGraph;
    }

    /// <summary>
    /// Checks if all code paths in a body return.
    /// </summary>
    /// <returns>If all code paths return.</returns>
    internal bool AllPathsReturn() {
        foreach (var branch in end.incoming) {
            var lastStatement = branch.from.statements.LastOrDefault();

            if (lastStatement is null)
                return false;

            var lastStatementIsThrow = lastStatement.kind == BoundKind.UnreachableStatement ||
                (lastStatement is BoundExpressionStatement es && es.expression is BoundThrowExpression);

            if (lastStatement.kind != BoundKind.ReturnStatement && !lastStatementIsThrow)
                return false;
        }

        return true;
    }

    internal HashSet<Symbol> CheckDefiniteAssignment(BelteDiagnosticQueue diagnostics) {
        try {
            var result = DefiniteAssignment.CheckDefiniteAssignment(
                this,
                _symbolsBySlot,
                _slotMap,
                _method,
                _closureCaptures,
                diagnostics
            );

            _symbolsBySlot.Free();
            return result;
        } catch (BoundTreeVisitor.CancelledByStackGuardException ex) {
            ex.AddAnError(diagnostics);
            return _symbolsBySlot.ToArrayAndFree().ToHashSet();
        }
    }

    /// <summary>
    /// Writes <see cref="ControlFlowGraph" /> to out.
    /// </summary>
    /// <param name="writer">Out.</param>
    internal void WriteTo(TextWriter writer) {
        string Quote(string text) {
            return "\"" + text.TrimEnd()
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace(Environment.NewLine, "\\l") + "\"";
        }

        writer.WriteLine("digraph G {");

        var blockIds = new Dictionary<BasicBlock, string>();

        for (var i = 0; i < blocks.Count; i++) {
            var id = $"N{i}";
            blockIds.Add(blocks[i], id);
        }

        foreach (var block in blocks) {
            var id = blockIds[block];
            var label = Quote(block.ToString());
            writer.WriteLine($"    {id} [label = {label}, shape = box]");
        }

        foreach (var branch in branches) {
            var fromId = blockIds[branch.from];
            var toId = blockIds[branch.to];
            var label = Quote(branch.ToString());
            writer.WriteLine($"    {fromId} -> {toId} [label = {label}]");
        }

        writer.WriteLine("}");
    }
}
