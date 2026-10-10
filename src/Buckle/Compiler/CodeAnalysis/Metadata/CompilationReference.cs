using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal abstract class CompilationReference : MetadataReference, IEquatable<CompilationReference> {
    internal CompilationReference(MetadataReferenceProperties properties) : base(properties) {
        Debug.Assert(properties.kind != MetadataImageKind.Module);
    }

    internal Compilation compilation => compilationCore;

    internal abstract Compilation compilationCore { get; }

    internal override string display => compilation.assemblyName;

    internal static MetadataReferenceProperties GetProperties(
        Compilation compilation,
        ImmutableArray<string> aliases,
        bool embedInteropTypes) {
        ArgumentNullException.ThrowIfNull(compilation);

        if (compilation.options.isScript)
            throw new NotSupportedException("CannotCreateReferenceToSubmission");

        return new MetadataReferenceProperties(MetadataImageKind.Assembly, aliases, embedInteropTypes);
    }

    internal new CompilationReference WithAliases(IEnumerable<string> aliases) {
        return WithAliases(ImmutableArray.CreateRange(aliases));
    }

    internal new CompilationReference WithAliases(ImmutableArray<string> aliases) {
        return WithProperties(properties.WithAliases(aliases));
    }

    internal new CompilationReference WithEmbedInteropTypes(bool value) {
        return WithProperties(properties.WithEmbedInteropTypes(value));
    }

    internal new CompilationReference WithProperties(MetadataReferenceProperties properties) {
        if (properties == this.properties)
            return this;

        if (properties.kind == MetadataImageKind.Module)
            throw new ArgumentException("CannotCreateReferenceToModule");

        return WithPropertiesImpl(properties);
    }

    internal sealed override MetadataReference WithPropertiesImplReturningMetadataReference(
        MetadataReferenceProperties properties) {
        if (properties.kind == MetadataImageKind.Module)
            throw new NotSupportedException("CannotCreateReferenceToModule");

        return WithPropertiesImpl(properties);
    }

    internal abstract CompilationReference WithPropertiesImpl(MetadataReferenceProperties properties);

    public bool Equals(CompilationReference other) {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Equals(compilation, other.compilation) &&
            Equals(properties, other.properties);
    }

    public override bool Equals(object? obj) {
        return Equals(obj as CompilationReference);
    }

    public override int GetHashCode() {
        return Hash.Combine(compilation.GetHashCode(), properties.GetHashCode());
    }
}
