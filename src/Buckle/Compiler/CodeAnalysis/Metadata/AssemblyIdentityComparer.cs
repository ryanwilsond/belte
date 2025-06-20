using System;

namespace Buckle.CodeAnalysis;

internal partial class AssemblyIdentityComparer {
    internal AssemblyIdentityComparer() { }

    internal static AssemblyIdentityComparer Default { get; } = new AssemblyIdentityComparer();

    internal static StringComparer SimpleNameComparer => StringComparer.OrdinalIgnoreCase;

    internal static StringComparer CultureComparer => StringComparer.OrdinalIgnoreCase;

    internal bool ReferenceMatchesDefinition(string referenceDisplayName, AssemblyIdentity definition) {
        return Compare(
            reference: null,
            referenceDisplayName,
            definition,
            unificationApplied: out _,
            ignoreVersion: false
        ) != ComparisonResult.NotEquivalent;
    }

    internal bool ReferenceMatchesDefinition(AssemblyIdentity reference, AssemblyIdentity definition) {
        return Compare(
            reference,
            referenceDisplayName: null,
            definition,
            unificationApplied: out _,
            ignoreVersion: false
        ) != ComparisonResult.NotEquivalent;
    }

    internal ComparisonResult Compare(AssemblyIdentity reference, AssemblyIdentity definition) {
        return Compare(
            reference,
            referenceDisplayName: null,
            definition,
            unificationApplied: out _,
            ignoreVersion: true
        );
    }

    internal ComparisonResult Compare(
        AssemblyIdentity reference,
        string referenceDisplayName,
        AssemblyIdentity definition,
        out bool unificationApplied,
        bool ignoreVersion) {
        unificationApplied = false;
        AssemblyIdentityParts parts;

        if (reference is not null) {
            var eq = TriviallyEquivalent(reference, definition);

            if (eq.HasValue)
                return eq.GetValueOrDefault() ? ComparisonResult.Equivalent : ComparisonResult.NotEquivalent;

            parts = AssemblyIdentityParts.Name | AssemblyIdentityParts.Version |
                    AssemblyIdentityParts.Culture | AssemblyIdentityParts.PublicKeyToken;
        } else {
            if (!AssemblyIdentity.TryParseDisplayName(referenceDisplayName!, out reference, out parts) ||
                reference.contentType != definition.contentType) {
                return ComparisonResult.NotEquivalent;
            }
        }

        if (!ApplyUnificationPolicies(ref reference, ref definition, parts, out var isDefinitionFxAssembly))
            return ComparisonResult.NotEquivalent;

        if (ReferenceEquals(reference, definition))
            return ComparisonResult.Equivalent;

        var compareCulture = (parts & AssemblyIdentityParts.Culture) != 0;
        var compareinternalKeyToken = (parts & AssemblyIdentityParts.PublicKeyOrToken) != 0;

        if (!definition.isStrongName) {
            if (reference.isStrongName)
                return ComparisonResult.NotEquivalent;

            if (!AssemblyIdentity.IsFullName(parts)) {
                if (!SimpleNameComparer.Equals(reference.name, definition.name))
                    return ComparisonResult.NotEquivalent;

                if (compareCulture && !CultureComparer.Equals(reference.cultureName, definition.cultureName))
                    return ComparisonResult.NotEquivalent;

                return ComparisonResult.Equivalent;
            }

            isDefinitionFxAssembly = false;
        }

        if (!SimpleNameComparer.Equals(reference.name, definition.name))
            return ComparisonResult.NotEquivalent;

        if (compareCulture && !CultureComparer.Equals(reference.cultureName, definition.cultureName))
            return ComparisonResult.NotEquivalent;

        if (compareinternalKeyToken && !AssemblyIdentity.KeysEqual(reference, definition))
            return ComparisonResult.NotEquivalent;

        var hasSomeVersionParts = (parts & AssemblyIdentityParts.Version) != 0;
        var hasPartialVersion = (parts & AssemblyIdentityParts.Version) != AssemblyIdentityParts.Version;

        if (definition.isStrongName &&
            hasSomeVersionParts &&
            (hasPartialVersion || reference.version != definition.version)) {
            if (isDefinitionFxAssembly) {
                unificationApplied = true;
                return ComparisonResult.Equivalent;
            }

            if (ignoreVersion)
                return ComparisonResult.EquivalentIgnoringVersion;

            return ComparisonResult.NotEquivalent;
        }

        return ComparisonResult.Equivalent;
    }

    private static bool? TriviallyEquivalent(AssemblyIdentity x, AssemblyIdentity y) {
        if (x.contentType != y.contentType)
            return false;

        if (x.isRetargetable || y.isRetargetable)
            return null;

        return AssemblyIdentity.MemberwiseEqual(x, y);
    }

    internal virtual bool ApplyUnificationPolicies(
        ref AssemblyIdentity reference,
        ref AssemblyIdentity definition,
        AssemblyIdentityParts referenceParts,
        out bool isDefinitionFxAssembly) {
        isDefinitionFxAssembly = false;
        return true;
    }
}
