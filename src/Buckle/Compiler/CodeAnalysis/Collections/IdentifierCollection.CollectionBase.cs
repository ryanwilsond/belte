using System;
using System.Collections.Generic;
using System.Linq;

namespace Buckle.CodeAnalysis;

internal partial class IdentifierCollection {
    private abstract class CollectionBase : ICollection<string> {
        private protected readonly IdentifierCollection _identifierCollection;
        private int _count = -1;

        private protected CollectionBase(IdentifierCollection identifierCollection) {
            _identifierCollection = identifierCollection;
        }

        public abstract bool Contains(string item);

        public void CopyTo(string[] array, int arrayIndex) {
            using var enumerator = GetEnumerator();

            while (arrayIndex < array.Length && enumerator.MoveNext()) {
                array[arrayIndex] = enumerator.Current;
                arrayIndex++;
            }
        }

        public int Count {
            get {
                if (_count == -1)
                    _count = _identifierCollection._map.Values.Sum(o => o is string ? 1 : ((ISet<string>)o).Count);

                return _count;
            }
        }

        public bool IsReadOnly => true;

        public IEnumerator<string> GetEnumerator() {
            foreach (var obj in _identifierCollection._map.Values) {
                if (obj is HashSet<string> strs) {
                    foreach (var s in strs)
                        yield return s;
                } else {
                    yield return (string)obj;
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(string item) {
            throw new NotSupportedException();
        }

        public void Clear() {
            throw new NotSupportedException();
        }

        public bool Remove(string item) {
            throw new NotSupportedException();
        }
    }
}
