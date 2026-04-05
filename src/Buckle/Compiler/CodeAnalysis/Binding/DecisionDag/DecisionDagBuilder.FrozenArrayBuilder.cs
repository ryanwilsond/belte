
namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class DecisionDagBuilder {
    private readonly struct FrozenArrayBuilder<T> {
        private readonly ArrayBuilder2<T> _arrayBuilder;

        internal FrozenArrayBuilder(ArrayBuilder2<T> arrayBuilder) {
            if (arrayBuilder.Capacity >= ArrayBuilder2<T>.PooledArrayLengthLimitExclusive
                && arrayBuilder.Count < ArrayBuilder2<T>.PooledArrayLengthLimitExclusive
                && arrayBuilder.Capacity >= arrayBuilder.Count * 2) {
                arrayBuilder.Capacity = arrayBuilder.Count;
            }

            _arrayBuilder = arrayBuilder;
        }

        internal void Free()
            => _arrayBuilder.Free();

        public int Count => _arrayBuilder.Count;

        public T this[int i] => _arrayBuilder[i];

        public T First() => _arrayBuilder.First();

        public ArrayBuilder2<T>.Enumerator GetEnumerator() => _arrayBuilder.GetEnumerator();

        public FrozenArrayBuilder<T> RemoveAt(int index) {
            var builder = ArrayBuilder2<T>.GetInstance(Count - 1);

            for (var i = 0; i < index; i++)
                builder.Add(this[i]);

            for (int i = index + 1, n = Count; i < n; i++)
                builder.Add(this[i]);

            return AsFrozen(builder);
        }
    }
}
