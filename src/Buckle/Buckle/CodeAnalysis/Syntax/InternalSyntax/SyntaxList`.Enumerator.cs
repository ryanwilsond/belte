
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class SyntaxList<T> where T : GreenNode {
    internal struct Enumerator {
        private readonly SyntaxList<T> _list;
        private int _index;

        internal Enumerator(SyntaxList<T> list) {
            _list = list;
            _index = -1;
        }

        public bool MoveNext() {
            var newIndex = _index + 1;

            if (newIndex < _list.count) {
                _index = newIndex;
                return true;
            }

            return false;
        }

        public T Current => _list[_index];
    }
}
