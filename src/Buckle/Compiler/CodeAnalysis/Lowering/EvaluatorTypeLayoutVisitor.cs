using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class EvaluatorTypeLayoutVisitor : SymbolVisitor {
    private readonly ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder _typeLayouts;
    private readonly HashSet<NamedTypeSymbol> _visited;

    private EvaluatorTypeLayoutVisitor(ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder typeLayouts) {
        _typeLayouts = typeLayouts;
        _visited = [];
    }

    internal static void CreateTypeLayouts(
        ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder typeLayouts,
        NamespaceSymbol globalNamespace) {
        var typeVisitor = new EvaluatorTypeLayoutVisitor(typeLayouts);
        typeVisitor.Visit(globalNamespace);
    }

    internal static void CreateTypeLayouts(
        ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder typeLayouts,
        NamedTypeSymbol type) {
        var typeVisitor = new EvaluatorTypeLayoutVisitor(typeLayouts);
        typeVisitor.Visit(type);
    }

    internal override void VisitNamespace(NamespaceSymbol symbol) {
        foreach (var member in symbol.GetMembersUnordered())
            member.Accept(this);
    }

    internal override void VisitNamedType(NamedTypeSymbol symbol) {
        if (!_visited.Add(symbol))
            return;

        var typeLayout = new EvaluatorSlotManager(symbol);
        var current = symbol;
        var types = new Stack<NamedTypeSymbol>();

        while (current is not null) {
            types.Push(current);
            current = current?.baseType;
        }

        while (types.Count > 0) {
            current = types.Pop();

            if (current.arity > 0) {
                foreach (var templateParameter in current.templateParameters) {
                    typeLayout.DeclareLocal(
                        templateParameter.underlyingType.type,
                        templateParameter,
                        templateParameter.name,
                        SynthesizedLocalKind.UserDefined,
                        CodeGeneration.LocalSlotConstraints.None,
                        false
                    );
                }
            }

            var slot = typeLayout.currentSlot;
            var isUnionStruct = current.isUnionStruct;
            var isAnonymousUnion = symbol is AnonymousUnionType;
            var seenGroupIds = new HashSet<int>();

            foreach (var member in current.GetMembers()) {
                switch (member) {
                    case FieldSymbol field:
                        if (!isAnonymousUnion && field.isAnonymousUnionMember) {
                            if (seenGroupIds.Add(field.unionGroupId)) {
                                var namedType = (SourceNamedTypeSymbol)symbol;
                                var union = namedType.anonymousUnionTypes[field];
                                var unionField = namedType.anonymousUnionFields[union];

                                typeLayout.DeclareLocalWithSlot(
                                    union,
                                    unionField,
                                    unionField.name,
                                    SynthesizedLocalKind.UserDefined,
                                    (field.refKind != RefKind.None)
                                        ? CodeGeneration.LocalSlotConstraints.ByRef
                                        : CodeGeneration.LocalSlotConstraints.None,
                                    slot++
                                );

                                union.Accept(this);
                            }
                        } else if (isUnionStruct) {
                            typeLayout.DeclareLocalWithSlot(
                                field.type,
                                field,
                                field.name,
                                SynthesizedLocalKind.UserDefined,
                                (field.refKind != RefKind.None)
                                    ? CodeGeneration.LocalSlotConstraints.ByRef
                                    : CodeGeneration.LocalSlotConstraints.None,
                                slot
                            );
                        } else {
                            typeLayout.DeclareLocalWithSlot(
                                field.type,
                                field,
                                field.name,
                                SynthesizedLocalKind.UserDefined,
                                (field.refKind != RefKind.None)
                                    ? CodeGeneration.LocalSlotConstraints.ByRef
                                    : CodeGeneration.LocalSlotConstraints.None,
                                slot++
                            );
                        }

                        break;
                    case NamedTypeSymbol namedTypeSymbol:
                        namedTypeSymbol.Accept(this);
                        break;
                }
            }
        }

        // We don't care if this fails
        _typeLayouts.TryAdd(symbol, typeLayout);
    }
}
