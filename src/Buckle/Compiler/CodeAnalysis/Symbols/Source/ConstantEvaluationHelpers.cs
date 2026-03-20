using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstantEvaluationHelpers {
    internal static void OrderAllDependencies(
        this SourceFieldSymbolWithSyntaxReference field,
        ArrayBuilder<FieldInfo> order) {

        var graph = PooledDictionary<SourceFieldSymbolWithSyntaxReference, Node<SourceFieldSymbolWithSyntaxReference>>
            .GetInstance();

        CreateGraph(graph, field);
        OrderGraph(graph, order);

        graph.Free();
    }

    private static void CreateGraph(
        Dictionary<SourceFieldSymbolWithSyntaxReference, Node<SourceFieldSymbolWithSyntaxReference>> graph,
        SourceFieldSymbolWithSyntaxReference field) {
        var pending = ArrayBuilder<SourceFieldSymbolWithSyntaxReference>.GetInstance();
        pending.Push(field);

        while (pending.Count > 0) {
            field = pending.Pop();

            if (graph.TryGetValue(field, out var node)) {
                if (node.dependencies is not null)
                    continue;
            } else {
                node = new Node<SourceFieldSymbolWithSyntaxReference> {
                    dependedOnBy = []
                };
            }

            var dependencies = field.GetConstantValueDependencies();
            node.dependencies = dependencies;
            graph[field] = node;

            foreach (var dependency in dependencies) {
                pending.Push(dependency);

                if (!graph.TryGetValue(dependency, out node)) {
                    node = new Node<SourceFieldSymbolWithSyntaxReference> {
                        dependedOnBy = []
                    };
                }

                node.dependedOnBy = node.dependedOnBy.Add(field);
                graph[dependency] = node;
            }
        }

        pending.Free();
    }

    private static void OrderGraph(
        Dictionary<SourceFieldSymbolWithSyntaxReference, Node<SourceFieldSymbolWithSyntaxReference>> graph,
        ArrayBuilder<FieldInfo> order) {
        PooledHashSet<SourceFieldSymbolWithSyntaxReference> lastUpdated = null;
        ArrayBuilder<SourceFieldSymbolWithSyntaxReference> fieldsInvolvedInCycles = null;

        while (graph.Count > 0) {
            var search = ((IEnumerable<SourceFieldSymbolWithSyntaxReference>)lastUpdated) ?? graph.Keys;
            var set = ArrayBuilder<SourceFieldSymbolWithSyntaxReference>.GetInstance();

            foreach (var field in search) {
                if (graph.TryGetValue(field, out var node)) {
                    if (node.dependencies.Count == 0)
                        set.Add(field);
                }
            }

            lastUpdated?.Free();

            if (set.Count > 0) {
                var updated = PooledHashSet<SourceFieldSymbolWithSyntaxReference>.GetInstance();

                foreach (var field in set) {
                    var node = graph[field];

                    foreach (var dependedOnBy in node.dependedOnBy) {
                        var n = graph[dependedOnBy];
                        n.dependencies = n.dependencies.Remove(field);
                        graph[dependedOnBy] = n;
                        updated.Add(dependedOnBy);
                    }

                    graph.Remove(field);
                }

                foreach (var item in set)
                    order.Add(new FieldInfo(item, startsCycle: false));

                lastUpdated = updated;
            } else {
                var field = GetStartOfFirstCycle(graph, ref fieldsInvolvedInCycles);
                var node = graph[field];

                foreach (var dependency in node.dependencies) {
                    var n = graph[dependency];
                    n.dependedOnBy = n.dependedOnBy.Remove(field);
                    graph[dependency] = n;
                }

                node = graph[field];
                var updated = PooledHashSet<SourceFieldSymbolWithSyntaxReference>.GetInstance();

                foreach (var dependedOnBy in node.dependedOnBy) {
                    var n = graph[dependedOnBy];
                    n.dependencies = n.dependencies.Remove(field);
                    graph[dependedOnBy] = n;
                    updated.Add(dependedOnBy);
                }

                graph.Remove(field);
                order.Add(new FieldInfo(field, startsCycle: true));

                lastUpdated = updated;
            }

            set.Free();
        }

        lastUpdated?.Free();
        fieldsInvolvedInCycles?.Free();
    }

    private static SourceFieldSymbolWithSyntaxReference GetStartOfFirstCycle(
        Dictionary<SourceFieldSymbolWithSyntaxReference, Node<SourceFieldSymbolWithSyntaxReference>> graph,
        ref ArrayBuilder<SourceFieldSymbolWithSyntaxReference> fieldsInvolvedInCycles) {
        if (fieldsInvolvedInCycles is null) {
            fieldsInvolvedInCycles = ArrayBuilder<SourceFieldSymbolWithSyntaxReference>.GetInstance(graph.Count);
            fieldsInvolvedInCycles.AddRange(graph.Keys.GroupBy(static f => f.declaringCompilation).
                SelectMany(static g => g.OrderByDescending(
                    (f1, f2) => g.Key.CompareSourceLocations(
                        f1.syntaxReference, f1.errorLocation, f2.syntaxReference, f2.errorLocation
                    )
                )));
        }

        while (true) {
            var field = fieldsInvolvedInCycles.Pop();

            if (graph.ContainsKey(field) && IsPartOfCycle(graph, field))
                return field;
        }
    }

    private static bool IsPartOfCycle(
        Dictionary<SourceFieldSymbolWithSyntaxReference, Node<SourceFieldSymbolWithSyntaxReference>> graph,
        SourceFieldSymbolWithSyntaxReference field) {
        var set = PooledHashSet<SourceFieldSymbolWithSyntaxReference>.GetInstance();
        var stack = ArrayBuilder<SourceFieldSymbolWithSyntaxReference>.GetInstance();

        var stopAt = field;
        var result = false;
        stack.Push(field);

        while (stack.Count > 0) {
            field = stack.Pop();
            var node = graph[field];

            if (node.dependencies.Contains(stopAt)) {
                result = true;
                break;
            }

            foreach (var dependency in node.dependencies) {
                if (set.Add(dependency))
                    stack.Push(dependency);
            }
        }

        stack.Free();
        set.Free();
        return result;
    }
}
