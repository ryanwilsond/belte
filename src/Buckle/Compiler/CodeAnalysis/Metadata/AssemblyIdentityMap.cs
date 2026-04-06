using System;
using System.Collections.Generic;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal sealed class AssemblyIdentityMap<TValue> {
    private readonly Dictionary<string, OneOrMany<KeyValuePair<AssemblyIdentity, TValue>>> _map;

    internal AssemblyIdentityMap() {
        _map = new Dictionary<string, OneOrMany<KeyValuePair<AssemblyIdentity, TValue>>>(
            AssemblyIdentityComparer.SimpleNameComparer
        );
    }

    internal bool Contains(AssemblyIdentity identity, bool allowHigherVersion = true) {
        return TryGetValue(identity, out _, allowHigherVersion);
    }

    internal bool TryGetValue(AssemblyIdentity identity, out TValue value, bool allowHigherVersion = true) {
        if (_map.TryGetValue(identity.name, out var sameName)) {
            var minHigherVersionCandidate = -1;

            for (var i = 0; i < sameName.Count; i++) {
                var currentIdentity = sameName[i].Key;

                if (AssemblyIdentity.EqualIgnoringNameAndVersion(currentIdentity, identity)) {
                    if (currentIdentity.version == identity.version) {
                        value = sameName[i].Value;
                        return true;
                    }

                    if (!allowHigherVersion || currentIdentity.version < identity.version)
                        continue;

                    if (minHigherVersionCandidate == -1 ||
                        currentIdentity.version < sameName[minHigherVersionCandidate].Key.version) {
                        minHigherVersionCandidate = i;
                    }
                }
            }

            if (minHigherVersionCandidate >= 0) {
                value = sameName[minHigherVersionCandidate].Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    internal bool TryGetValue(
        AssemblyIdentity identity,
        out TValue value,
        Func<Version, Version, TValue, bool> comparer) {
        if (_map.TryGetValue(identity.name, out var sameName)) {
            for (var i = 0; i < sameName.Count; i++) {
                var currentIdentity = sameName[i].Key;

                if (comparer(identity.version, currentIdentity.version, sameName[i].Value) &&
                    AssemblyIdentity.EqualIgnoringNameAndVersion(currentIdentity, identity)) {
                    value = sameName[i].Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    internal void Add(AssemblyIdentity identity, TValue value) {
        var pair = KeyValuePairUtilities.Create(identity, value);

        _map[identity.name] = _map.TryGetValue(identity.name, out var sameName)
            ? sameName.Add(pair)
            : OneOrMany.Create(pair);
    }
}
