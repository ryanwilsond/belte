using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class NamedTypeSymbol {
    internal sealed class TupleExtraData {
        private ImmutableArray<TypeOrConstant> _lazyElementTypes;
        private ImmutableArray<FieldSymbol> _lazyDefaultElementFields;
        // TODO Perf: use SmallDictionary
        private Dictionary<Symbol, Symbol> _lazyUnderlyingDefinitionToMemberMap;

        internal TupleExtraData(NamedTypeSymbol underlyingType) {
            tupleUnderlyingType = underlyingType;
            locations = [];
        }

        internal TupleExtraData(
            NamedTypeSymbol underlyingType,
            ImmutableArray<string> elementNames,
            ImmutableArray<TextLocation> elementLocations,
            ImmutableArray<bool> errorPositions,
            ImmutableArray<TextLocation> locations)
            : this(underlyingType) {
            this.elementNames = elementNames;
            this.elementLocations = elementLocations;
            this.errorPositions = errorPositions;
            this.locations = locations.NullToEmpty();
        }

        internal ImmutableArray<string> elementNames { get; }

        internal ImmutableArray<TextLocation> elementLocations { get; }

        internal ImmutableArray<bool> errorPositions { get; }

        internal ImmutableArray<TextLocation> locations { get; }

        internal NamedTypeSymbol tupleUnderlyingType { get; }

        // TODO Perf: use SmallDictionary
        internal Dictionary<Symbol, Symbol> underlyingDefinitionToMemberMap {
            get {
                return _lazyUnderlyingDefinitionToMemberMap ??= ComputeDefinitionToMemberMap();

                Dictionary<Symbol, Symbol> ComputeDefinitionToMemberMap() {
                    var map = new Dictionary<Symbol, Symbol>(ReferenceEqualityComparer.Instance);
                    var members = tupleUnderlyingType.GetMembers();

                    for (var i = members.Length - 1; i >= 0; i--) {
                        var member = members[i];

                        switch (member.kind) {
                            case SymbolKind.Method:
                            case SymbolKind.NamedType:
                                map.Add(member.originalDefinition, member);
                                break;
                            case SymbolKind.Field:
                                var tupleUnderlyingField = ((FieldSymbol)member).tupleUnderlyingField;

                                if (tupleUnderlyingField is not null)
                                    map[tupleUnderlyingField.originalDefinition] = member;

                                break;
                            default:
                                throw ExceptionUtilities.UnexpectedValue(member.kind);
                        }
                    }

                    return map;
                }
            }
        }

        internal bool EqualsIgnoringTupleUnderlyingType(TupleExtraData other) {
            if (other is null && elementNames.IsDefault && elementLocations.IsDefault && errorPositions.IsDefault)
                return true;

            return other is not null
                && AreEqual(elementNames, other.elementNames)
                && AreEqual(elementLocations, other.elementLocations)
                && AreEqual(errorPositions, other.errorPositions);

            static bool AreEqual<T>(ImmutableArray<T> one, ImmutableArray<T> other) {
                if (one.IsDefault && other.IsDefault)
                    return true;

                if (one.IsDefault != other.IsDefault)
                    return false;

                return one.SequenceEqual(other);
            }
        }

        internal ImmutableArray<TypeOrConstant> TupleElementTypes(NamedTypeSymbol tuple) {
            if (_lazyElementTypes.IsDefault) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyElementTypes,
                    CollectTupleElementTypes(tuple)
                );
            }

            return _lazyElementTypes;

            static ImmutableArray<TypeOrConstant> CollectTupleElementTypes(NamedTypeSymbol tuple) {
                ImmutableArray<TypeOrConstant> elementTypes;

                if (tuple.arity == ValueTupleRestPosition) {
                    var extensionTupleElementTypes = tuple.templateArguments[ValueTupleRestPosition - 1]
                        .type.type.tupleElementTypes;

                    var typesBuilder = ArrayBuilder<TypeOrConstant>.GetInstance(
                        ValueTupleRestPosition - 1 + extensionTupleElementTypes.Length
                    );

                    typesBuilder.AddRange(tuple.templateArguments, ValueTupleRestPosition - 1);
                    typesBuilder.AddRange(extensionTupleElementTypes);
                    elementTypes = typesBuilder.ToImmutableAndFree();
                } else {
                    elementTypes = tuple.templateArguments;
                }

                return elementTypes;
            }
        }

        internal ImmutableArray<FieldSymbol> TupleElements(NamedTypeSymbol tuple) {
            if (_lazyDefaultElementFields.IsDefault) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyDefaultElementFields,
                    CollectTupleElementFields(tuple)
                );
            }

            return _lazyDefaultElementFields;

            ImmutableArray<FieldSymbol> CollectTupleElementFields(NamedTypeSymbol tuple) {
                var builder = ArrayBuilder<FieldSymbol>.GetInstance(
                    TupleElementTypes(tuple).Length,
                    fillWithValue: null
                );

                foreach (var member in tuple.GetMembers()) {
                    if (member.kind != SymbolKind.Field)
                        continue;

                    var candidate = (FieldSymbol)member;
                    var index = candidate.tupleElementIndex;

                    if (index >= 0) {
                        if (builder[index]?.isDefaultTupleElement != false)
                            builder[index] = candidate;
                    }
                }

                return builder.ToImmutableAndFree();
            }
        }

        internal TMember GetTupleMemberSymbolForUnderlyingMember<TMember>(TMember underlyingMemberOpt)
            where TMember : Symbol {
            if (underlyingMemberOpt is null)
                return null;

            var underlyingMemberDefinition = underlyingMemberOpt.originalDefinition;

            if (underlyingMemberDefinition is TupleElementFieldSymbol tupleField)
                underlyingMemberDefinition = tupleField.underlyingField;

            if (TypeSymbol.Equals(
                underlyingMemberDefinition.containingType,
                tupleUnderlyingType.originalDefinition,
                TypeCompareKind.ConsiderEverything)) {
                if (underlyingDefinitionToMemberMap.TryGetValue(underlyingMemberDefinition, out var result))
                    return (TMember)result;
            }

            return null;
        }
    }
}
