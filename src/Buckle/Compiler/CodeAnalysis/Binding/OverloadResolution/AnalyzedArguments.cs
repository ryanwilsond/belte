using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class AnalyzedArguments {
    internal static readonly ObjectPool<AnalyzedArguments> Pool = CreatePool();

    internal readonly ArrayBuilder<BoundExpressionOrTypeOrConstant> arguments;
    internal readonly ArrayBuilder<bool> hasErrors;
    internal readonly ArrayBuilder<SyntaxNode> syntaxes;
    internal readonly ArrayBuilder<TypeSymbol> types;
    internal readonly ArrayBuilder<(string Name, TextLocation Location)?> names;
    internal readonly ArrayBuilder<RefKind> refKinds;

    internal AnalyzedArguments() {
        arguments = new ArrayBuilder<BoundExpressionOrTypeOrConstant>(32);
        hasErrors = new ArrayBuilder<bool>(32);
        syntaxes = new ArrayBuilder<SyntaxNode>(32);
        types = new ArrayBuilder<TypeSymbol>(32);
        names = new ArrayBuilder<(string, TextLocation)?>(32);
        refKinds = new ArrayBuilder<RefKind>(32);
    }

    internal bool anyErrors {
        get {
            foreach (var hasError in hasErrors) {
                if (hasError)
                    return true;
            }

            return false;
        }
    }

    internal void Clear() {
        arguments.Clear();
        hasErrors.Clear();
        syntaxes.Clear();
        types.Clear();
        names.Clear();
        refKinds.Clear();
    }

    internal BoundExpressionOrTypeOrConstant Argument(int i) {
        return arguments[i];
    }

    internal void AddName(SyntaxToken name) {
        names.Add((name.text, name.location));
    }

    internal string Name(int i) {
        if (names.Count == 0)
            return null;

        var nameAndLocation = names[i];
        return nameAndLocation?.Name;
    }

    internal ImmutableArray<string> GetNames() {
        var count = names.Count;

        if (count == 0)
            return default;

        var builder = ArrayBuilder<string?>.GetInstance(names.Count);

        for (var i = 0; i < names.Count; i++)
            builder.Add(Name(i));

        return builder.ToImmutableAndFree();
    }

    internal RefKind RefKind(int i) {
        return refKinds.Count > 0 ? refKinds[i] : Symbols.RefKind.None;
    }

    internal static AnalyzedArguments GetInstance() {
        return Pool.Allocate();
    }

    internal static AnalyzedArguments GetInstance(AnalyzedArguments original) {
        var instance = GetInstance();
        instance.arguments.AddRange(original.arguments);
        instance.hasErrors.AddRange(original.hasErrors);
        instance.syntaxes.AddRange(original.syntaxes);
        instance.types.AddRange(original.types);
        instance.names.AddRange(original.names);
        instance.refKinds.AddRange(original.refKinds);
        return instance;
    }

    internal static AnalyzedArguments GetInstance(
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        ImmutableArray<bool> hasErrors,
        ImmutableArray<SyntaxNode> syntaxes,
        ImmutableArray<TypeSymbol> types,
        ImmutableArray<RefKind> argumentRefKindsOpt,
        ImmutableArray<(string, TextLocation)?> argumentNamesOpt) {
        var instance = GetInstance();
        instance.arguments.AddRange(arguments);
        instance.hasErrors.AddRange(hasErrors);
        instance.syntaxes.AddRange(syntaxes);
        instance.types.AddRange(types);

        if (!argumentRefKindsOpt.IsDefault)
            instance.refKinds.AddRange(argumentRefKindsOpt);

        if (!argumentNamesOpt.IsDefault)
            instance.names.AddRange(argumentNamesOpt);

        return instance;
    }

    internal void Free() {
        Clear();
        Pool.Free(this);
    }

    private static ObjectPool<AnalyzedArguments> CreatePool() {
        ObjectPool<AnalyzedArguments>? pool = null;
        pool = new ObjectPool<AnalyzedArguments>(() => new AnalyzedArguments(), 10);
        return pool;
    }
}
