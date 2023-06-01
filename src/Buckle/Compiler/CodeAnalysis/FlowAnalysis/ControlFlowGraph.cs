using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.FlowAnalysis;

/// <summary>
/// Creates a graphical control flow graph from a <see cref="BasicBlock" />.
/// </summary>
internal sealed class ControlFlowGraph {
    /// <summary>
    /// Creates a <see cref="ControlFlowGraph" />.
    /// </summary>
    /// <param name="start">Start of the graph.</param>
    /// <param name="end">End of the graph.</param>
    /// <param name="blocks">All blocks within the graph.</param>
    /// <param name="branch">All branches within the graph.</param>
    internal ControlFlowGraph(
        BasicBlock start, BasicBlock end, List<BasicBlock> blocks, List<ControlFlowBranch> branch) {
        this.start = start;
        this.end = end;
        this.blocks = blocks;
        branches = branch;
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
    internal static ControlFlowGraph Create(BoundBlockStatement body) {
        var basicBlockBuilder = new ControlFlowGraphBuilder.BasicBlockBuilder();
        var blocks = basicBlockBuilder.Build(body);
        var graphBuilder = new ControlFlowGraphBuilder();

        return graphBuilder.Build(blocks);
    }

    /// <summary>
    /// Checks (using a <see cref="ControlFlowGraph" />) if all code paths in a body return.
    /// </summary>
    /// <param name="body">Body to check.</param>
    /// <returns>If all code paths return.</returns>
    internal static bool AllPathsReturn(BoundBlockStatement body) {
        var graph = Create(body);

        foreach (var branch in graph.end.incoming) {
            var lastStatement = branch.from.statements.LastOrDefault();

            if (lastStatement is null || lastStatement.kind != BoundNodeKind.ReturnStatement)
                return false;
        }

        return true;
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
