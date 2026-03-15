using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal partial class SpecializedCollections {
    private partial class Empty {
        internal class Set<T> : Collection<T>, ISet<T>, IReadOnlySet<T> {
            public static new readonly Set<T> Instance = new();

            private protected Set() { }

            public new bool Add(T item) {
                throw new NotSupportedException();
            }

            public void ExceptWith(IEnumerable<T> other) {
                throw new NotSupportedException();
            }

            public void IntersectWith(IEnumerable<T> other) {
                throw new NotSupportedException();
            }

            public bool IsProperSubsetOf(IEnumerable<T> other) {
                return !other.IsEmpty();
            }

            public bool IsProperSupersetOf(IEnumerable<T> other) {
                return false;
            }

            public bool IsSubsetOf(IEnumerable<T> other) {
                return true;
            }

            public bool IsSupersetOf(IEnumerable<T> other) {
                return other.IsEmpty();
            }

            public bool Overlaps(IEnumerable<T> other) {
                return false;
            }

            public bool SetEquals(IEnumerable<T> other) {
                return other.IsEmpty();
            }

            public void SymmetricExceptWith(IEnumerable<T> other) {
                throw new NotSupportedException();
            }

            public void UnionWith(IEnumerable<T> other) {
                throw new NotSupportedException();
            }
        }
    }
}
