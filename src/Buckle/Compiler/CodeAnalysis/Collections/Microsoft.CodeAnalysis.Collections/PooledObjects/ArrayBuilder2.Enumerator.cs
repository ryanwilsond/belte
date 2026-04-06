
namespace Buckle.CodeAnalysis;

internal partial class ArrayBuilder2<T> {
    /// <summary>
    /// struct enumerator used in foreach.
    /// </summary>
    internal struct Enumerator {
        private readonly ArrayBuilder2<T> _builder;
        private int _index;

        public Enumerator(ArrayBuilder2<T> builder) {
            _builder = builder;
            _index = -1;
        }

        public readonly T Current {
            get {
                return _builder[_index];
            }
        }

        public bool MoveNext() {
            _index++;
            return _index < _builder.Count;
        }
    }
}
