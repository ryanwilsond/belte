using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SubstitutedNamedTypeSymbol : WrappedNamedTypeSymbol {
    private readonly TemplateMap _inputMap;

    private int _hashCode;
    private TemplateMap _lazyMap;
    private ImmutableArray<TemplateParameterSymbol> _lazyTemplateParameters;
    private NamedTypeSymbol _lazyBaseType = ErrorTypeSymbol.UnknownResultType;
    private ConcurrentCache<string, ImmutableArray<Symbol>> _lazyMembersByNameCache;
    private ImmutableArray<Symbol> _lazyMembers;
    private int _lazyHasStructDefault;

    private protected SubstitutedNamedTypeSymbol(
        Symbol newContainer,
        TemplateMap templateMap,
        NamedTypeSymbol originalDefinition,
        NamedTypeSymbol constructedFrom = null,
        bool isUnboundTemplateType = false,
        TupleExtraData tupleData = null)
        : base(originalDefinition, tupleData) {
        containingSymbol = newContainer;
        _inputMap = templateMap;
        this.isUnboundTemplateType = isUnboundTemplateType;

        if (constructedFrom is not null) {
            _lazyTemplateParameters = constructedFrom.templateParameters;
            _lazyMap = templateMap;
        }
    }

    public sealed override SymbolKind kind => originalDefinition.kind;

    public sealed override TemplateMap templateSubstitution {
        get {
            EnsureMapAndTemplateParameters();
            return _lazyMap;
        }
    }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters {
        get {
            EnsureMapAndTemplateParameters();
            return _lazyTemplateParameters;
        }
    }

    internal sealed override bool isUnboundTemplateType { get; }

    internal sealed override Symbol containingSymbol { get; }

    internal sealed override NamedTypeSymbol originalDefinition => underlyingNamedType;

    internal override NamedTypeSymbol containingType => containingSymbol as NamedTypeSymbol;

    internal sealed override NamedTypeSymbol baseType {
        get {
            if (isUnboundTemplateType)
                return null;

            if (ReferenceEquals(_lazyBaseType, ErrorTypeSymbol.UnknownResultType)) {
                var @base = templateSubstitution.SubstituteNamedType(originalDefinition.baseType);
                Interlocked.CompareExchange(ref _lazyBaseType, @base, ErrorTypeSymbol.UnknownResultType);
            }

            return _lazyBaseType;
        }
    }

    internal sealed override IEnumerable<string> memberNames {
        get {
            if (isUnboundTemplateType)
                return new List<string>(GetTypeMembers().Select(s => s.name).Distinct());

            if (isTupleType)
                return GetMembers().Select(s => s.name).Distinct();

            return originalDefinition.memberNames;
        }
    }

    internal override bool hasStructDefault {
        get {
            if (_lazyHasStructDefault == (int)ThreeState.Unknown) {
                if (typeKind != TypeKind.Struct) {
                    Interlocked.CompareExchange(
                        ref _lazyHasStructDefault,
                        (int)ThreeState.False,
                        (int)ThreeState.Unknown
                    );
                } else {
                    var value = (int)CheckHasStructDefault().ToThreeState();
                    Interlocked.CompareExchange(ref _lazyHasStructDefault, value, (int)ThreeState.Unknown);
                }
            }

            return _lazyHasStructDefault == (int)ThreeState.True;
        }
    }

    internal sealed override NamedTypeSymbol enumUnderlyingType => originalDefinition.enumUnderlyingType;

    internal sealed override bool isUnionStruct => originalDefinition.isUnionStruct;

    internal sealed override ImmutableArray<AttributeData> GetAttributes() {
        return originalDefinition.GetAttributes();
    }

    internal sealed override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return isUnboundTemplateType
            ? null
            : templateSubstitution.SubstituteNamedType(originalDefinition.GetDeclaredBaseType(basesBeingResolved));
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return originalDefinition.GetTypeMembers().SelectAsArray((t, self) => t.AsMember(self), this);
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return originalDefinition.GetTypeMembers(name).SelectAsArray((t, self) => t.AsMember(self), this);
    }

    internal sealed override ImmutableArray<Symbol> GetMembers() {
        if (!_lazyMembers.IsDefault)
            return _lazyMembers;

        var builder = ArrayBuilder<Symbol>.GetInstance();

        if (isUnboundTemplateType) {
            foreach (var member in originalDefinition.GetMembers()) {
                if (member.kind == SymbolKind.NamedType)
                    builder.Add(((NamedTypeSymbol)member).AsMember(this));
            }
        } else {
            foreach (var member in originalDefinition.GetMembers())
                builder.Add(member.SymbolAsMember(this));
        }

        builder = AddOrWrapTupleMembersIfNecessary(builder);

        var result = builder.ToImmutableAndFree();
        ImmutableInterlocked.InterlockedInitialize(ref _lazyMembers, result);
        return _lazyMembers;
    }

    internal sealed override ImmutableArray<Symbol> GetMembers(string name) {
        if (isUnboundTemplateType)
            return StaticCast<Symbol>.From(GetTypeMembers(name));

        var cache = _lazyMembersByNameCache;

        if (cache is not null && cache.TryGetValue(name, out var result))
            return result;

        return GetMembersWorker(name);
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(
        ConsList<TypeSymbol> basesBeingResolved) {
        return isUnboundTemplateType
            ? []
            : templateSubstitution.SubstituteNamedTypes(originalDefinition.GetDeclaredInterfaces(basesBeingResolved));
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> Interfaces(ConsList<TypeSymbol> basesBeingResolved) {
        return isUnboundTemplateType
            ? []
            : templateSubstitution.SubstituteNamedTypes(originalDefinition.Interfaces(basesBeingResolved));
    }

    public override int GetHashCode() {
        if (_hashCode == 0)
            _hashCode = ComputeHashCode();

        return _hashCode;
    }

    private ImmutableArray<Symbol> GetMembersWorker(string name) {
        if (isTupleType) {
            var result = GetMembers().WhereAsArray((m, name) => m.name == name, name);
            CacheResult(result);
            return result;
        }

        var originalMembers = originalDefinition.GetMembers(name);

        if (originalMembers.IsDefaultOrEmpty)
            return originalMembers;

        var builder = ArrayBuilder<Symbol>.GetInstance(originalMembers.Length);

        foreach (var member in originalMembers)
            builder.Add(member.SymbolAsMember(this));

        var substitutedMembers = builder.ToImmutableAndFree();
        CacheResult(substitutedMembers);
        return substitutedMembers;

        void CacheResult(ImmutableArray<Symbol> result) {
            var cache = _lazyMembersByNameCache ??= new ConcurrentCache<string, ImmutableArray<Symbol>>(8);
            cache.TryAdd(name, result);
        }
    }

    private void EnsureMapAndTemplateParameters() {
        if (!_lazyTemplateParameters.IsDefault)
            return;

        var newMap = _inputMap.WithAlphaRename(originalDefinition, this, out var typeParameters);
        var previousMap = Interlocked.CompareExchange(ref _lazyMap, newMap, null);

        if (previousMap is not null)
            typeParameters = previousMap.SubstituteTemplateParameters(originalDefinition.templateParameters);

        ImmutableInterlocked.InterlockedCompareExchange(
            ref _lazyTemplateParameters,
            typeParameters,
            default
        );
    }

    private ArrayBuilder<Symbol> AddOrWrapTupleMembersIfNecessary(ArrayBuilder<Symbol> builder) {
        if (isTupleType) {
            var existingMembers = builder.ToImmutableAndFree();
            var replacedFields = new HashSet<Symbol>(ReferenceEqualityComparer.Instance);
            builder = MakeSynthesizedTupleMembers(existingMembers, replacedFields);

            foreach (var existingMember in existingMembers) {
                if (!replacedFields.Contains(existingMember))
                    builder.Add(existingMember);
            }
        }

        return builder;
    }
}
