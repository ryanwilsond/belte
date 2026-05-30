using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceMemberContainerTypeSymbol {
    private const int PointerSize = 8;
    private const int CacheLineSize = 64;

    private int _typeAlignment => explicitAlignment ?? PointerSize;

    private void CheckStructLayoutEfficiency(BelteDiagnosticQueue diagnostics) {
        if (!IsStructType() ||
            declaringCompilation.options.optimizationLevel != OptimizationLevel.Release ||
            isUnionStruct ||
            knownCircularStruct ||
            _typeAlignment == 1) {
            return;
        }

        var actualSize = GetStructSize(this, out var payloadSize, out _);
        // var actualPadding = actualSize - payloadSize;

        var optimalFieldLayout = GetOptimalFieldLayout(this);

        var optimalSize = GetStructSizeWithMembers(this, optimalFieldLayout, out _, out _);
        // var optimalPadding = optimalSize - payloadSize;

        var avoidablePadding = actualSize - optimalSize;
        var avoidablePercent = (double)avoidablePadding / actualSize;

        if (avoidablePadding == 0)
            return;

        if (actualSize <= 16) {
            diagnostics.Push(Info.StructInefficiency(location, actualSize, optimalSize));
            return;
        }

        // TODO These are just guesses, could probably tune what warrants a warning more
        var causesExtraCacheRead = ((actualSize - 1) / CacheLineSize) != ((optimalSize - 1) / CacheLineSize);
        var shouldWarn = avoidablePercent >= 0.25 && avoidablePadding >= 8;

        if (causesExtraCacheRead)
            diagnostics.Push(Warning.StructInefficiencyCache(location, actualSize, optimalSize));
        else if (shouldWarn)
            diagnostics.Push(Warning.StructInefficiencyPadding(location, actualSize, optimalSize));
        else
            diagnostics.Push(Info.StructInefficiency(location, actualSize, optimalSize));
    }

    private static ImmutableArray<Symbol> GetOptimalFieldLayout(NamedTypeSymbol type) {
        var layoutUnits = BuildLayoutUnits(type);

        var sortedUnits = layoutUnits.Sort((x, y) => {
            var comparison = y.alignment.CompareTo(x.alignment);

            if (comparison != 0)
                return comparison;

            comparison = y.size.CompareTo(x.size);

            if (comparison != 0)
                return comparison;

            return x.originalIndex.CompareTo(y.originalIndex);
        });

        var newMembersBuilder = ArrayBuilder<Symbol>.GetInstance();

        foreach (var layoutUnit in sortedUnits) {
            foreach (var member in layoutUnit.members)
                newMembersBuilder.Add(member);
        }

        return newMembersBuilder.ToImmutableAndFree();
    }

    private static ImmutableArray<LayoutUnit> BuildLayoutUnits(NamedTypeSymbol type) {
        var members = type.GetMembers();
        var layoutUnits = ArrayBuilder<LayoutUnit>.GetInstance();
        var seenGroupIds = new HashSet<int>();

        for (var i = 0; i < members.Length; i++) {
            var member = members[i];

            if (member is FieldSymbol f) {
                if (f.isStatic)
                    continue;

                if (f.isAnonymousUnionMember) {
                    if (seenGroupIds.Add(f.unionGroupId)) {
                        var unionSize = GetAnonymousUnionFieldSize(
                            f,
                            (SourceNamedTypeSymbol)type,
                            out _,
                            out var unionAlignment
                        );

                        var index = i;

                        var symbolsBuilder = ArrayBuilder<Symbol>.GetInstance();

                        for (; i < members.Length; i++) {
                            var possibleMember = members[i];

                            if (possibleMember is FieldSymbol uf) {
                                if (uf.isStatic)
                                    continue;

                                if (uf.isAnonymousUnionMember && uf.unionGroupId == f.unionGroupId) {
                                    symbolsBuilder.Add(uf);
                                    continue;
                                }

                                break;
                            }
                        }

                        layoutUnits.Add(new LayoutUnit() {
                            members = symbolsBuilder.ToImmutableAndFree(),
                            size = unionSize,
                            alignment = unionAlignment,
                            originalIndex = index
                        });

                        continue;
                    }

                    throw ExceptionUtilities.Unreachable();
                }

                if (f.isFixedSizeBuffer) {
                    var bufferSize = GetFixedSizeBufferSize((SourceFixedFieldSymbol)f, out var bufferAlignment);

                    layoutUnits.Add(new LayoutUnit() {
                        members = [f],
                        size = bufferSize,
                        alignment = bufferAlignment,
                        originalIndex = i
                    });

                    continue;
                }

                var fieldSize = GetTypeSize(f.type, out var fieldAlignment);

                layoutUnits.Add(new LayoutUnit() {
                    members = [f],
                    size = fieldSize,
                    alignment = fieldAlignment,
                    originalIndex = i
                });
            }
        }

        return layoutUnits.ToImmutableAndFree();
    }

    private static int GetTypeSize(TypeSymbol type, out int alignment) {
        alignment = PointerSize;

        if (type.IsVerifierReference() || type.IsTemplateParameter())
            return PointerSize;

        if (type.IsEnumType())
            return GetTypeSize(type.GetEnumUnderlyingType(), out alignment);

        if (type.IsPointerOrFunctionPointer() || type.specialType is SpecialType.IntPtr or SpecialType.UIntPtr)
            return PointerSize;

        if (type.IsNullableType()) {
            var tSize = GetTypeSize(type.StrippedType(), out _);
            alignment = Math.Min(tSize, PointerSize);
            return tSize + alignment;
        }

        var sizeInBytes = type.specialType.SizeInBytes();

        if (sizeInBytes > 0) {
            alignment = Math.Min(sizeInBytes, PointerSize);
            return sizeInBytes;
        }

        return GetStructSize((NamedTypeSymbol)type, out _, out alignment);
    }

    private static int GetStructSize(NamedTypeSymbol type, out int sizeWithoutPadding, out int alignment) {
        var members = type.GetMembers();
        return GetStructSizeWithMembers(type, members, out sizeWithoutPadding, out alignment);
    }

    private static int GetStructSizeWithMembers(
        NamedTypeSymbol type,
        ImmutableArray<Symbol> members,
        out int sizeWithoutPadding,
        out int alignment) {
        var size = 0;
        sizeWithoutPadding = 0;
        alignment = 0;

        if (type.isUnionStruct) {
            foreach (var member in members) {
                if (member is FieldSymbol f) {
                    if (f.isStatic)
                        continue;

                    size = Math.Max(size, GetTypeSize(f.type, out _));
                }
            }

            size = Math.Max(size, 1);
            sizeWithoutPadding = size;
            alignment = Math.Min(size, PointerSize);
            return size;
        }

        var seenGroupIds = new HashSet<int>();

        foreach (var member in members) {
            if (member is FieldSymbol f) {
                if (f.isStatic)
                    continue;

                if (f.isAnonymousUnionMember) {
                    if (seenGroupIds.Add(f.unionGroupId)) {
                        var unionSize = GetAnonymousUnionFieldSize(
                            f,
                            (SourceNamedTypeSymbol)type,
                            out var unionSizeWithoutPadding,
                            out var unionAlignment
                        );

                        sizeWithoutPadding += unionSizeWithoutPadding;

                        UpdateSizeAndAlignment(
                            ref size,
                            ref alignment,
                            unionSize,
                            GetEffectiveAlignment(unionAlignment)
                        );
                    }

                    continue;
                }

                if (f.isFixedSizeBuffer) {
                    var bufferSize = GetFixedSizeBufferSize((SourceFixedFieldSymbol)f, out var bufferAlignment);
                    sizeWithoutPadding += bufferSize;
                    UpdateSizeAndAlignment(ref size, ref alignment, bufferSize, GetEffectiveAlignment(bufferAlignment));
                    continue;
                }

                var fieldSize = GetTypeSize(f.type, out var fieldAlignment);
                sizeWithoutPadding += fieldSize;
                UpdateSizeAndAlignment(ref size, ref alignment, fieldSize, GetEffectiveAlignment(fieldAlignment));
            }
        }

        size = Align(size, alignment);
        size = Math.Max(size, 1);
        sizeWithoutPadding = Math.Max(sizeWithoutPadding, 1);

        return size;

        int GetEffectiveAlignment(int naturalAlignment) {
            if (type is SourceMemberContainerTypeSymbol memberContainer)
                return Math.Min(naturalAlignment, memberContainer._typeAlignment);

            return naturalAlignment;
        }

        static void UpdateSizeAndAlignment(ref int size, ref int alignment, int fieldSize, int fieldAlignment) {
            alignment = Math.Max(alignment, fieldAlignment);
            size = Align(size, fieldAlignment);
            size += fieldSize;
        }

        static int Align(int value, int alignment) {
            return (value + alignment - 1) & ~(alignment - 1);
        }
    }

    private static int GetAnonymousUnionFieldSize(
        FieldSymbol field,
        SourceNamedTypeSymbol parent,
        out int sizeWithoutPadding,
        out int alignment) {
        var union = parent.anonymousUnionTypes[field];
        return GetStructSize(union, out sizeWithoutPadding, out alignment);
    }

    private static int GetFixedSizeBufferSize(SourceFixedFieldSymbol field, out int alignment) {
        var elementType = ((PointerTypeSymbol)field.type).pointedAtType;
        var elementSize = elementType.FixedBufferElementSizeInBytes();
        alignment = Math.Min(elementSize, PointerSize);
        return field.fixedSize * elementSize;
    }
}
