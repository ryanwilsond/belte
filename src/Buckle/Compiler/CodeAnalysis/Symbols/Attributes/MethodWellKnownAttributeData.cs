using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class MethodWellKnownAttributeData : CommonMethodWellKnownAttributeData {
    internal bool hasDoesNotReturnAttribute { get; set; }

    internal bool hasSkipLocalsInitAttribute { get; set; }

    internal bool hasUnscopedRefAttribute { get; set; }

    internal UnmanagedCallersOnlyAttributeData? unmanagedCallersOnlyAttributeData { get; set; }

    internal ImmutableArray<string> notNullMembers { get; private set; } = [];

    internal ImmutableArray<string> notNullWhenTrueMembers { get; private set; } = [];

    internal ImmutableArray<string> notNullWhenFalseMembers { get; private set; } = [];

    internal void AddNotNullMember(string memberName) {
        notNullMembers = notNullMembers.Add(memberName);
    }

    internal void AddNotNullMember(ArrayBuilder<string> memberNames) {
        notNullMembers = notNullMembers.AddRange(memberNames);
    }

    internal void AddNotNullWhenMember(bool sense, string memberName) {
        if (sense)
            notNullWhenTrueMembers = notNullWhenTrueMembers.Add(memberName);
        else
            notNullWhenFalseMembers = notNullWhenFalseMembers.Add(memberName);
    }

    internal void AddNotNullWhenMember(bool sense, ArrayBuilder<string> memberNames) {
        if (sense)
            notNullWhenTrueMembers = notNullWhenTrueMembers.AddRange(memberNames);
        else
            notNullWhenFalseMembers = notNullWhenFalseMembers.AddRange(memberNames);
    }
}
