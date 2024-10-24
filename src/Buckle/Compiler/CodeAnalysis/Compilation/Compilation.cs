using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Handles evaluation of program, and keeps track of Symbols.
/// </summary>
public sealed class Compilation {
    private readonly SyntaxManager _syntax;
    private readonly ImmutableDictionary<SyntaxTree, int> _ordinalMap;
    private NamespaceSymbol _lazyGlobalNamespace;
    private WeakReference<BinderFactory>[] _binderFactories;

    private Compilation(
        CompilationOptions options,
        Compilation previous,
        SyntaxManager syntax) {
        this.options = options;
        this.previous = previous;
        _syntax = syntax;
        diagnostics = new BelteDiagnosticQueue();
    }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal CompilationOptions options { get; }

    internal Compilation previous { get; }

    internal ImmutableArray<SyntaxTree> syntaxTrees => _syntax.state.syntaxTrees;

    internal bool keepLookingForCorTypes { get; set; } = true;

    internal NamespaceSymbol globalNamespace {
        get {
            if (_lazyGlobalNamespace is null) {
                var result = new GlobalNamespaceSymbol(new NamespaceExtent(this));
                Interlocked.CompareExchange(ref _lazyGlobalNamespace, result, null);
            }

            return _lazyGlobalNamespace;
        }
    }

    public static Compilation Create(CompilationOptions options, params SyntaxTree[] syntaxTrees) {
        return Create(options, null, syntaxTrees);
    }

    public static Compilation Create(
        CompilationOptions options,
        Compilation previous,
        params SyntaxTree[] syntaxTrees) {
        return Create(options, previous, (IEnumerable<SyntaxTree>)syntaxTrees);
    }

    public static Compilation CreateScript(
        CompilationOptions options,
        Compilation previous,
        params SyntaxTree[] syntaxTrees) {
        options.isScript = true;
        return Create(options, previous, syntaxTrees);
    }

    internal int CompareSourceLocations(SyntaxReference syntax1, SyntaxReference syntax2) {
        var comparison = CompareSyntaxTreeOrdering(syntax1.syntaxTree, syntax2.syntaxTree);

        if (comparison != 0)
            return comparison;

        return syntax1.span.start - syntax2.span.start;
    }

    internal int CompareSourceLocations(
        SyntaxReference syntax1,
        TextLocation location1,
        SyntaxReference syntax2,
        TextLocation location2) {
        var comparison = CompareSyntaxTreeOrdering(syntax1.syntaxTree, syntax2.syntaxTree);

        if (comparison != 0)
            return comparison;

        return location1.span.start - location2.span.start;
    }

    internal int CompareSyntaxTreeOrdering(SyntaxTree tree1, SyntaxTree tree2) {
        if (tree1 == tree2)
            return 0;

        return GetSyntaxTreeOrdinal(tree1) - GetSyntaxTreeOrdinal(tree2);
    }

    internal Binder GetBinder(BelteSyntaxNode syntax) {
        return GetBinderFactory(syntax.syntaxTree).GetBinder(syntax);
    }

    internal BinderFactory GetBinderFactory(SyntaxTree syntaxTree) {
        var treeOrdinal = _ordinalMap[syntaxTree];
        var binderFactories = _binderFactories;

        if (binderFactories is null) {
            binderFactories = new WeakReference<BinderFactory>[syntaxTrees.Length];
            binderFactories = Interlocked.CompareExchange(ref _binderFactories, binderFactories, null)
                ?? binderFactories;
        }

        var previousWeakReference = binderFactories[treeOrdinal];

        if (previousWeakReference is not null && previousWeakReference.TryGetTarget(out var previousFactory))
            return previousFactory;

        return AddNewFactory(syntaxTree, ref binderFactories[treeOrdinal]);
    }

    internal int GetSyntaxTreeOrdinal(SyntaxTree syntaxTree) {
        return _syntax.state.ordinalMap[syntaxTree];
    }

    private Compilation AddSyntaxTrees(IEnumerable<SyntaxTree> trees) {
        ArgumentNullException.ThrowIfNull(trees);

        if (trees.IsEmpty())
            return this;

        var externalSyntaxTrees = PooledHashSet<SyntaxTree>.GetInstance();
        var syntax = _syntax;
        externalSyntaxTrees.AddAll(syntax.syntaxTrees);

        var i = 0;

        foreach (var tree in trees) {
            if (tree is null)
                throw new ArgumentNullException($"{nameof(trees)}[{i}]");

            if (externalSyntaxTrees.Contains(tree))
                throw new ArgumentException("Syntax tree already present", $"{nameof(trees)}[{i}]");

            externalSyntaxTrees.Add(tree);
            i++;
        }

        externalSyntaxTrees.Free();

        if (options.isScript && i > 1)
            throw new ArgumentException("Script can have at most 1 syntax tree", nameof(trees));

        syntax = syntax.AddSyntaxTrees(trees);
        return Update(syntax);
    }

    private Compilation Update(SyntaxManager syntax) {
        return new Compilation(options, previous, syntax);
    }

    private BinderFactory AddNewFactory(SyntaxTree syntaxTree, ref WeakReference<BinderFactory> slot) {
        var newFactory = new BinderFactory(this, syntaxTree);
        var newWeakReference = new WeakReference<BinderFactory>(newFactory);

        while (true) {
            var previousWeakReference = slot;

            if (previousWeakReference is not null && previousWeakReference.TryGetTarget(out var previousFactory))
                return previousFactory;

            if (Interlocked.CompareExchange(ref slot!, newWeakReference, previousWeakReference)
                == previousWeakReference) {
                return newFactory;
            }
        }
    }

    private static Compilation Create(
        CompilationOptions options,
        Compilation previous,
        IEnumerable<SyntaxTree> syntaxTrees) {
        var compilation = new Compilation(
            options,
            previous,
            new SyntaxManager([], null)
        );

        if (syntaxTrees is not null)
            compilation = compilation.AddSyntaxTrees(syntaxTrees);

        return compilation;
    }

    private static void CreateCfg(BoundProgram program) {
        var cfgPath = GetProjectPath("cfg.dot");
        var cfgStatement = program.entryPoint is null ? null : program.methodBodies[program.entryPoint];

        if (cfgStatement is not null) {
            var cfg = ControlFlowGraph.Create(cfgStatement);

            using var streamWriter = new StreamWriter(cfgPath);
            cfg.WriteTo(streamWriter);
        }
    }

    private static string GetProjectPath(string fileName) {
        var appPath = Environment.GetCommandLineArgs()[0];
        var appDirectory = Path.GetDirectoryName(appPath);
        return Path.Combine(appDirectory, fileName);
    }
}
