using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal partial class IdentifierCollection {
    private readonly Dictionary<string, object> _map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    internal IdentifierCollection() { }

    internal IdentifierCollection(IEnumerable<string> identifiers) {
        AddIdentifiers(identifiers);
    }

    internal void AddIdentifiers(IEnumerable<string> identifiers) {
        foreach (var identifier in identifiers)
            AddIdentifier(identifier);
    }

    internal void AddIdentifier(string identifier) {
        if (!_map.TryGetValue(identifier, out var value))
            AddInitialSpelling(identifier);
        else
            AddAdditionalSpelling(identifier, value);
    }

    private void AddAdditionalSpelling(string identifier, object value) {
        if (value is string strValue) {
            if (!string.Equals(identifier, strValue, StringComparison.Ordinal))
                _map[identifier] = new HashSet<string> { identifier, strValue };
        } else {
            var spellings = (HashSet<string>)value;
            spellings.Add(identifier);
        }
    }

    private void AddInitialSpelling(string identifier) {
        _map.Add(identifier, identifier);
    }

    internal bool ContainsIdentifier(string identifier, bool caseSensitive) {
        if (caseSensitive)
            return CaseSensitiveContains(identifier);
        else
            return CaseInsensitiveContains(identifier);
    }

    private bool CaseInsensitiveContains(string identifier) {
        return _map.ContainsKey(identifier);
    }

    private bool CaseSensitiveContains(string identifier) {
        if (_map.TryGetValue(identifier, out var spellings)) {
            if (spellings is string spelling)
                return string.Equals(identifier, spelling, StringComparison.Ordinal);

            var set = (HashSet<string>)spellings;
            return set.Contains(identifier);
        }

        return false;
    }

    internal ICollection<string> AsCaseSensitiveCollection() {
        return new CaseSensitiveCollection(this);
    }

    internal ICollection<string> AsCaseInsensitiveCollection() {
        return new CaseInsensitiveCollection(this);
    }
}
