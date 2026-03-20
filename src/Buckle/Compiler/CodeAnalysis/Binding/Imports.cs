using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
internal sealed partial class Imports {
    internal static readonly Imports Empty = new Imports(
        ImmutableDictionary<string, AliasAndUsingDirective>.Empty,
        []
    );

    internal readonly ImmutableDictionary<string, AliasAndUsingDirective> usingAliases;
    internal readonly ImmutableArray<NamespaceOrTypeAndUsingDirective> usings;

    private Imports(
        ImmutableDictionary<string, AliasAndUsingDirective> usingAliases,
        ImmutableArray<NamespaceOrTypeAndUsingDirective> usings) {
        this.usingAliases = usingAliases;
        this.usings = usings;
    }

    internal bool isEmpty => usingAliases.IsEmpty && usings.IsEmpty;

    internal static Imports ExpandPreviousSubmissionImports(
        Imports previousSubmissionImports,
        Compilation newSubmission) {
        if (previousSubmissionImports == Empty)
            return Empty;

        var expandedAliases = ImmutableDictionary<string, AliasAndUsingDirective>.Empty;

        if (!previousSubmissionImports.usingAliases.IsEmpty) {
            var expandedAliasesBuilder = ImmutableDictionary.CreateBuilder<string, AliasAndUsingDirective>();

            foreach (var pair in previousSubmissionImports.usingAliases) {
                var name = pair.Key;
                var directive = pair.Value;
                expandedAliasesBuilder.Add(
                    name,
                    new AliasAndUsingDirective(directive.alias.ToNewSubmission(newSubmission),
                    directive.usingDirective
                ));
            }

            expandedAliases = expandedAliasesBuilder.ToImmutable();
        }

        var expandedUsings = ExpandPreviousSubmissionImports(previousSubmissionImports.usings, newSubmission);

        return Create(
            expandedAliases,
            expandedUsings
        );
    }

    internal static ImmutableArray<NamespaceOrTypeAndUsingDirective> ExpandPreviousSubmissionImports(
        ImmutableArray<NamespaceOrTypeAndUsingDirective> previousSubmissionUsings,
        Compilation newSubmission) {

        if (!previousSubmissionUsings.IsEmpty) {
            var expandedUsingsBuilder = ArrayBuilder<NamespaceOrTypeAndUsingDirective>
                .GetInstance(previousSubmissionUsings.Length);

            var expandedGlobalNamespace = newSubmission.globalNamespaceInternal;

            foreach (var previousUsing in previousSubmissionUsings) {
                var previousTarget = previousUsing.namespaceOrType;

                if (previousTarget.isType) {
                    expandedUsingsBuilder.Add(previousUsing);
                } else {
                    var expandedNamespace = ExpandPreviousSubmissionNamespace(
                        (NamespaceSymbol)previousTarget,
                        expandedGlobalNamespace
                    );

                    expandedUsingsBuilder.Add(
                        new NamespaceOrTypeAndUsingDirective(
                            expandedNamespace,
                            previousUsing.usingDirective,
                            dependencies: default
                        )
                    );
                }
            }

            return expandedUsingsBuilder.ToImmutableAndFree();
        }

        return previousSubmissionUsings;
    }

    internal static NamespaceSymbol ExpandPreviousSubmissionNamespace(
        NamespaceSymbol originalNamespace,
        NamespaceSymbol expandedGlobalNamespace) {
        var nameParts = ArrayBuilder<string>.GetInstance();
        var curr = originalNamespace;

        while (!curr.isGlobalNamespace) {
            nameParts.Add(curr.name);
            curr = curr.containingNamespace;
        }

        var expandedNamespace = expandedGlobalNamespace;

        for (var i = nameParts.Count - 1; i >= 0; i--)
            expandedNamespace = expandedNamespace.GetMembers(nameParts[i]).OfType<NamespaceSymbol>().Single();

        nameParts.Free();
        return expandedNamespace;
    }

    internal static Imports Create(
        ImmutableDictionary<string, AliasAndUsingDirective> usingAliases,
        ImmutableArray<NamespaceOrTypeAndUsingDirective> usings) {
        if (usingAliases.IsEmpty && usings.IsEmpty)
            return Empty;

        return new Imports(usingAliases, usings);
    }

    internal Imports Concat(Imports otherImports) {
        if (this == Empty)
            return otherImports;

        if (otherImports == Empty)
            return this;

        var usingAliases = this.usingAliases.SetItems(otherImports.usingAliases);
        var usings = this.usings.AddRange(otherImports.usings).Distinct(UsingTargetComparer.Instance);

        return Create(usingAliases, usings);
    }

    internal string GetDebuggerDisplay() {
        return string.Join("; ",
            usingAliases.OrderBy(x => x.Value.usingDirective.location.span.start)
                .Select(ua => $"{ua.Key} = {ua.Value.alias.target}").Concat(
            usings.Select(u => u.namespaceOrType.ToString()))
        );
    }
}
