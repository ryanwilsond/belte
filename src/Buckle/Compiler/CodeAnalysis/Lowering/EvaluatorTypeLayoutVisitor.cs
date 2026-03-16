using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class EvaluatorTypeLayoutVisitor : SymbolVisitor {
    private readonly Dictionary<NamedTypeSymbol, EvaluatorSlotManager> _typeLayouts;

    private EvaluatorTypeLayoutVisitor() {
        _typeLayouts = [];
    }

    internal static Dictionary<NamedTypeSymbol, EvaluatorSlotManager> CreateTypeLayouts(
        NamespaceSymbol globalNamespace) {
        var typeVisitor = new EvaluatorTypeLayoutVisitor();
        typeVisitor.Visit(globalNamespace);
        return typeVisitor._typeLayouts;
    }

    internal static Dictionary<NamedTypeSymbol, EvaluatorSlotManager> CreateTypeLayouts(NamedTypeSymbol type) {
        var typeVisitor = new EvaluatorTypeLayoutVisitor();
        typeVisitor.Visit(type);
        return typeVisitor._typeLayouts;
    }

    internal override void VisitNamespace(NamespaceSymbol symbol) {
        foreach (var member in symbol.GetMembersUnordered())
            member.Accept(this);
    }

    internal override void VisitNamedType(NamedTypeSymbol symbol) {
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
                    if (templateParameter.underlyingType.specialType == SpecialType.Type)
                        continue;

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

            foreach (var member in current.GetMembers()) {
                switch (member) {
                    case FieldSymbol field:
                        typeLayout.DeclareLocal(
                            field.type,
                            field,
                            field.name,
                            SynthesizedLocalKind.UserDefined,
                            (field.refKind != RefKind.None)
                                ? CodeGeneration.LocalSlotConstraints.ByRef
                                : CodeGeneration.LocalSlotConstraints.None,
                            false
                        );
                        break;
                    case NamedTypeSymbol namedTypeSymbol:
                        namedTypeSymbol.Accept(this);
                        break;
                }
            }
        }

        _typeLayouts.Add(symbol, typeLayout);
    }
}
