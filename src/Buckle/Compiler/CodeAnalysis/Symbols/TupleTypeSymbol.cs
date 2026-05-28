using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class NamedTypeSymbol {
    internal const int ValueTupleRestPosition = 8;
    internal const int ValueTupleRestIndex = ValueTupleRestPosition - 1;
    internal const string ValueTupleTypeName = "ValueTuple";
    internal const string ValueTupleRestFieldName = "Rest";

    private TupleExtraData _lazyTupleData;

    private static readonly WellKnownType[] TupleTypes = {
        WellKnownType.ValueTuple_T1,
        WellKnownType.ValueTuple_T2,
        WellKnownType.ValueTuple_T3,
        WellKnownType.ValueTuple_T4,
        WellKnownType.ValueTuple_T5,
        WellKnownType.ValueTuple_T6,
        WellKnownType.ValueTuple_T7,
        WellKnownType.ValueTuple_TRest
    };

    private static readonly WellKnownMember[][] TupleMembers = [
        [
            WellKnownMember.ValueTuple_T1_Item1 ],

        [
            WellKnownMember.ValueTuple_T2_Item1,
            WellKnownMember.ValueTuple_T2_Item2 ],

        [
            WellKnownMember.ValueTuple_T3_Item1,
            WellKnownMember.ValueTuple_T3_Item2,
            WellKnownMember.ValueTuple_T3_Item3 ],

        [
            WellKnownMember.ValueTuple_T4_Item1,
            WellKnownMember.ValueTuple_T4_Item2,
            WellKnownMember.ValueTuple_T4_Item3,
            WellKnownMember.ValueTuple_T4_Item4 ],

        [
            WellKnownMember.ValueTuple_T5_Item1,
            WellKnownMember.ValueTuple_T5_Item2,
            WellKnownMember.ValueTuple_T5_Item3,
            WellKnownMember.ValueTuple_T5_Item4,
            WellKnownMember.ValueTuple_T5_Item5 ],

        [
            WellKnownMember.ValueTuple_T6_Item1,
            WellKnownMember.ValueTuple_T6_Item2,
            WellKnownMember.ValueTuple_T6_Item3,
            WellKnownMember.ValueTuple_T6_Item4,
            WellKnownMember.ValueTuple_T6_Item5,
            WellKnownMember.ValueTuple_T6_Item6 ],

        [
            WellKnownMember.ValueTuple_T7_Item1,
            WellKnownMember.ValueTuple_T7_Item2,
            WellKnownMember.ValueTuple_T7_Item3,
            WellKnownMember.ValueTuple_T7_Item4,
            WellKnownMember.ValueTuple_T7_Item5,
            WellKnownMember.ValueTuple_T7_Item6,
            WellKnownMember.ValueTuple_T7_Item7 ],

        [
            WellKnownMember.ValueTuple_TRest_Item1,
            WellKnownMember.ValueTuple_TRest_Item2,
            WellKnownMember.ValueTuple_TRest_Item3,
            WellKnownMember.ValueTuple_TRest_Item4,
            WellKnownMember.ValueTuple_TRest_Item5,
            WellKnownMember.ValueTuple_TRest_Item6,
            WellKnownMember.ValueTuple_TRest_Item7,
            WellKnownMember.ValueTuple_TRest_Rest ]
    ];

    internal NamedTypeSymbol tupleUnderlyingType
        => _lazyTupleData is not null ? tupleData.tupleUnderlyingType : (isTupleType ? this : null);

    internal sealed override bool isTupleType => IsTupleTypeOfCardinality(tupleCardinality: out _);

    internal TupleExtraData tupleData {
        get {
            if (!isTupleType)
                return null;

            if (_lazyTupleData is null)
                Interlocked.CompareExchange(ref _lazyTupleData, new TupleExtraData(this), null);

            return _lazyTupleData;
        }
    }

    internal sealed override ImmutableArray<string> tupleElementNames
        => _lazyTupleData is null ? default : _lazyTupleData.elementNames;

    private ImmutableArray<bool> _tupleErrorPositions
        => _lazyTupleData is null ? default : _lazyTupleData.errorPositions;

    private ImmutableArray<TextLocation> _tupleElementLocations
        => _lazyTupleData is null ? default : _lazyTupleData.elementLocations;

    internal sealed override ImmutableArray<TypeOrConstant> tupleElementTypes
        => isTupleType ? tupleData.TupleElementTypes(this) : default;

    internal sealed override ImmutableArray<FieldSymbol> tupleElements
        => isTupleType ? tupleData.TupleElements(this) : default;

    private protected abstract NamedTypeSymbol WithTupleDataCore(TupleExtraData newData);

    internal static NamedTypeSymbol CreateTuple(
        TextLocation location,
        ImmutableArray<TypeWithAnnotations> elementTypesWithAnnotations,
        ImmutableArray<TextLocation> elementLocations,
        ImmutableArray<string> elementNames,
        Compilation compilation,
        bool shouldCheckConstraints,
        ImmutableArray<bool> errorPositions,
        BelteSyntaxNode syntax = null,
        BelteDiagnosticQueue diagnostics = null) {
        var numElements = elementTypesWithAnnotations.Length;

        if (numElements <= 1)
            throw ExceptionUtilities.Unreachable();

        var underlyingType = GetTupleUnderlyingType(elementTypesWithAnnotations, syntax, compilation, diagnostics);

        var locations = location is null ? ImmutableArray<TextLocation>.Empty : ImmutableArray.Create(location);
        var constructedType = CreateTuple(underlyingType, elementNames, errorPositions, elementLocations, locations);

        if (shouldCheckConstraints && diagnostics is not null) {
            // TODO Constraints
            // constructedType.CheckConstraints(new ConstraintsHelper.CheckConstraintsArgs(compilation, compilation.Conversions, includeNullability, syntax.Location, diagnostics),
            //                                  syntax, elementLocations, nullabilityDiagnosticsOpt: includeNullability ? diagnostics : null);
        }

        return constructedType;

        static NamedTypeSymbol GetTupleUnderlyingType(
            ImmutableArray<TypeWithAnnotations> elementTypes,
            BelteSyntaxNode syntax,
            Compilation compilation,
            BelteDiagnosticQueue diagnostics) {
            var numElements = elementTypes.Length;
            var chainLength = NumberOfValueTuples(numElements, out var remainder);
            var firstTupleType = CorLibrary.GetWellKnownType(GetTupleType(remainder));

            NamedTypeSymbol chainedTupleType = null;

            if (chainLength > 1)
                chainedTupleType = CorLibrary.GetWellKnownType(GetTupleType(ValueTupleRestPosition));

            return ConstructTupleUnderlyingType(firstTupleType, chainedTupleType, elementTypes);
        }
    }

    internal static NamedTypeSymbol CreateTuple(
        NamedTypeSymbol tupleCompatibleType,
        ImmutableArray<string> elementNames = default,
        ImmutableArray<bool> errorPositions = default,
        ImmutableArray<TextLocation> elementLocations = default,
        ImmutableArray<TextLocation> locations = default) {
        return tupleCompatibleType.WithElementNames(elementNames, elementLocations, errorPositions, locations);
    }

    private static int NumberOfValueTuples(int numElements, out int remainder) {
        remainder = (numElements - 1) % (ValueTupleRestPosition - 1) + 1;
        return (numElements - 1) / (ValueTupleRestPosition - 1) + 1;
    }

    private static WellKnownType GetTupleType(int arity) {
        if (arity > ValueTupleRestPosition)
            throw ExceptionUtilities.Unreachable();

        return TupleTypes[arity - 1];
    }

    internal static WellKnownMember GetTupleTypeMember(int arity, int position) {
        return TupleMembers[arity - 1][position - 1];
    }

    private static NamedTypeSymbol ConstructTupleUnderlyingType(
        NamedTypeSymbol firstTupleType,
        NamedTypeSymbol chainedTupleType,
        ImmutableArray<TypeWithAnnotations> elementTypes) {
        var elementTypeOrConstants = elementTypes.Select(e => new TypeOrConstant(e)).ToImmutableArray();

        var numElements = elementTypes.Length;
        var chainLength = NumberOfValueTuples(numElements, out var remainder);

        var currentSymbol = firstTupleType.Construct(
            ImmutableArray.Create(elementTypeOrConstants, (chainLength - 1) * (ValueTupleRestPosition - 1), remainder)
        );

        var loop = chainLength - 1;

        while (loop > 0) {
            var chainedTypes = ImmutableArray.Create(
                    elementTypeOrConstants,
                    (loop - 1) * (ValueTupleRestPosition - 1),
                    ValueTupleRestPosition - 1)
                .Add(new TypeOrConstant(currentSymbol));

            currentSymbol = chainedTupleType.Construct(chainedTypes);
            loop--;
        }

        return currentSymbol;
    }

    internal NamedTypeSymbol WithElementNames(
        ImmutableArray<string> newElementNames,
        ImmutableArray<TextLocation> newElementLocations,
        ImmutableArray<bool> errorPositions,
        ImmutableArray<TextLocation> locations) {
        return WithTupleData(
            new TupleExtraData(tupleUnderlyingType, newElementNames, newElementLocations, errorPositions, locations)
        );
    }

    internal bool IsTupleTypeOfCardinality(out int tupleCardinality) {
        // TODO Good enough check for tuple types?
        if (!isUnboundTemplateType &&
            originalDefinition is PrimitiveTypeSymbol &&
            name == ValueTupleTypeName) {
            var arity = this.arity;

            if (arity >= 0 && arity < ValueTupleRestPosition) {
                tupleCardinality = arity;
                return true;
            } else if (arity == ValueTupleRestPosition && !isDefinition) {
                TypeSymbol typeToCheck = this;
                var levelsOfNesting = 0;

                do {
                    levelsOfNesting++;
                    typeToCheck = ((NamedTypeSymbol)typeToCheck).templateArguments[ValueTupleRestPosition - 1]
                        .type.type;
                } while (Equals(
                    typeToCheck.originalDefinition,
                    originalDefinition,
                    TypeCompareKind.ConsiderEverything) &&
                    !typeToCheck.isDefinition);

                arity = (typeToCheck as NamedTypeSymbol)?.arity ?? 0;

                if (arity > 0 && arity < ValueTupleRestPosition &&
                    ((NamedTypeSymbol)typeToCheck).IsTupleTypeOfCardinality(out tupleCardinality)) {
                    tupleCardinality += (ValueTupleRestPosition - 1) * levelsOfNesting;
                    return true;
                }
            }
        }

        tupleCardinality = 0;
        return false;
    }

    private NamedTypeSymbol WithTupleData(TupleExtraData newData) {
        if (newData.EqualsIgnoringTupleUnderlyingType(tupleData))
            return this;

        if (isDefinition) {
            if (newData.elementNames.IsDefault)
                return this;

            return ConstructCore(GetTemplateParametersAsTemplateArguments(), unbound: false).WithTupleData(newData);
        }

        return WithTupleDataCore(newData);
    }

    internal TMember? GetTupleMemberSymbolForUnderlyingMember<TMember>(TMember underlyingMemberOpt)
        where TMember : Symbol {
        return isTupleType ? tupleData.GetTupleMemberSymbolForUnderlyingMember(underlyingMemberOpt) : null;
    }
}
