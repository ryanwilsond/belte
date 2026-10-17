using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal struct MetadataReferenceProperties : IEquatable<MetadataReferenceProperties> {
    internal static string GlobalAlias => "global";

    private readonly MetadataImageKind _kind;
    private readonly ImmutableArray<string> _aliases;
    private readonly bool _embedInteropTypes;

    internal static MetadataReferenceProperties Module => new MetadataReferenceProperties(MetadataImageKind.Module);

    internal static MetadataReferenceProperties Assembly => new MetadataReferenceProperties(MetadataImageKind.Assembly);

    internal MetadataReferenceProperties(
        MetadataImageKind kind = MetadataImageKind.Assembly,
        ImmutableArray<string> aliases = default,
        bool embedInteropTypes = false) {
        if (!kind.IsValid())
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (kind == MetadataImageKind.Module) {
            if (embedInteropTypes)
                throw new ArgumentException("CannotEmbedInteropTypesFromModule", nameof(embedInteropTypes));

            if (!aliases.IsDefaultOrEmpty)
                throw new ArgumentException("CannotAliasModule", nameof(aliases));
        }

        if (!aliases.IsDefaultOrEmpty) {
            foreach (var alias in aliases) {
                if (!alias.IsValidClrTypeName())
                    throw new ArgumentException("InvalidAlias", nameof(aliases));
            }
        }

        _kind = kind;
        _aliases = aliases;
        _embedInteropTypes = embedInteropTypes;
        hasRecursiveAliases = false;
    }

    internal MetadataReferenceProperties(
        MetadataImageKind kind,
        ImmutableArray<string> aliases,
        bool embedInteropTypes,
        bool hasRecursiveAliases)
        : this(kind, aliases, embedInteropTypes) {
        this.hasRecursiveAliases = hasRecursiveAliases;
    }

    internal MetadataReferenceProperties WithAliases(IEnumerable<string> aliases) {
        return WithAliases(aliases.AsImmutableOrEmpty());
    }

    internal MetadataReferenceProperties WithAliases(ImmutableArray<string> aliases) {
        return new MetadataReferenceProperties(_kind, aliases, _embedInteropTypes, hasRecursiveAliases);
    }

    internal MetadataReferenceProperties WithEmbedInteropTypes(bool embedInteropTypes) {
        return new MetadataReferenceProperties(_kind, _aliases, embedInteropTypes, hasRecursiveAliases);
    }

    internal MetadataReferenceProperties WithRecursiveAliases(bool value) {
        return new MetadataReferenceProperties(_kind, _aliases, _embedInteropTypes, value);
    }

    internal MetadataImageKind kind => _kind;

    internal ImmutableArray<string> aliases => _aliases.NullToEmpty();

    internal bool embedInteropTypes => _embedInteropTypes;

    internal bool hasRecursiveAliases { get; private set; }

    public override bool Equals(object? obj) {
        return obj is MetadataReferenceProperties properties && Equals(properties);
    }

    public bool Equals(MetadataReferenceProperties other) {
        return aliases.SequenceEqual(other.aliases)
            && _embedInteropTypes == other._embedInteropTypes
            && _kind == other._kind
            && hasRecursiveAliases == other.hasRecursiveAliases;
    }

    public override int GetHashCode() {
        return Hash.Combine(
               Hash.CombineValues(aliases),
               Hash.Combine(_embedInteropTypes,
               Hash.Combine(hasRecursiveAliases,
                    ((int)_kind).GetHashCode())));
    }

    public static bool operator ==(MetadataReferenceProperties left, MetadataReferenceProperties right) {
        return left.Equals(right);
    }

    public static bool operator !=(MetadataReferenceProperties left, MetadataReferenceProperties right) {
        return !left.Equals(right);
    }
}
