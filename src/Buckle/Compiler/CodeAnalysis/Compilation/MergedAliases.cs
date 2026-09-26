using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed class MergedAliases {
    internal ArrayBuilder<string> aliasesOpt;
    internal ArrayBuilder<string> recursiveAliasesOpt;
    internal ArrayBuilder<MetadataReference> mergedReferencesOpt;

    internal void Merge(MetadataReference reference) {
        ArrayBuilder<string> aliases;
        if (reference.properties.hasRecursiveAliases) {
            if (recursiveAliasesOpt is null) {
                recursiveAliasesOpt = ArrayBuilder<string>.GetInstance();
                recursiveAliasesOpt.AddRange(reference.properties.aliases);
                return;
            }

            aliases = recursiveAliasesOpt;
        } else {
            if (aliasesOpt is null) {
                aliasesOpt = ArrayBuilder<string>.GetInstance();
                aliasesOpt.AddRange(reference.properties.aliases);
                return;
            }

            aliases = aliasesOpt;
        }

        Merge(
            aliases: aliases,
            newAliases: reference.properties.aliases
        );

        (mergedReferencesOpt ??= ArrayBuilder<MetadataReference>.GetInstance()).Add(reference);
    }

    internal static void Merge(ArrayBuilder<string> aliases, ImmutableArray<string> newAliases) {
        if (aliases.Count == 0 ^ newAliases.IsEmpty)
            AddNonIncluded(aliases, MetadataReferenceProperties.GlobalAlias);

        AddNonIncluded(aliases, newAliases);
    }

    internal static ImmutableArray<string> Merge(ImmutableArray<string> aliasesOpt, ImmutableArray<string> newAliases) {
        if (aliasesOpt.IsDefault)
            return newAliases;

        var result = ArrayBuilder<string>.GetInstance(aliasesOpt.Length);
        result.AddRange(aliasesOpt);
        Merge(result, newAliases);
        return result.ToImmutableAndFree();
    }

    private static void AddNonIncluded(ArrayBuilder<string> builder, string item) {
        if (!builder.Contains(item))
            builder.Add(item);
    }

    private static void AddNonIncluded(ArrayBuilder<string> builder, ImmutableArray<string> items) {
        var originalCount = builder.Count;

        foreach (var item in items) {
            if (builder.IndexOf(item, 0, originalCount) < 0)
                builder.Add(item);
        }
    }
}
