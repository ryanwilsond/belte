using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal partial class SpecializedCollections {
    private partial class ReadOnly {
        internal class Set<TUnderlying, T> : Collection<TUnderlying, T>, ISet<T>, IReadOnlySet<T>
            where TUnderlying : ISet<T> {
            internal Set(TUnderlying underlying) : base(underlying) { }

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
                return underlying.IsProperSubsetOf(other);
            }

            public bool IsProperSupersetOf(IEnumerable<T> other) {
                return underlying.IsProperSupersetOf(other);
            }

            public bool IsSubsetOf(IEnumerable<T> other) {
                return underlying.IsSubsetOf(other);
            }

            public bool IsSupersetOf(IEnumerable<T> other) {
                return underlying.IsSupersetOf(other);
            }

            public bool Overlaps(IEnumerable<T> other) {
                return underlying.Overlaps(other);
            }

            public bool SetEquals(IEnumerable<T> other) {
                return underlying.SetEquals(other);
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
