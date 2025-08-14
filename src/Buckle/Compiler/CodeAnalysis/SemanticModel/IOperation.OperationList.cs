using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

public partial interface IOperation {
    public readonly partial struct OperationList : IReadOnlyCollection<IOperation> {
        private readonly Operation _operation;

        internal OperationList(Operation operation) {
            _operation = operation;
        }

        public int Count => _operation.ChildOperationsCount;

        public Enumerator GetEnumerator() => new Enumerator(_operation);

        public ImmutableArray<IOperation> ToImmutableArray() {
            return _operation switch {
                { ChildOperationsCount: 0 } => [],
                NoneOperation { Children: var children } => (ImmutableArray<IOperation>)children,
                InvalidOperation { Children: var children } => (ImmutableArray<IOperation>)children,
                _ => [.. this],
            };
        }

        IEnumerator<IOperation> IEnumerable<IOperation>.GetEnumerator() {
            if (Count == 0)
                return SpecializedCollections.EmptyEnumerator<IOperation>();

            return new EnumeratorImpl(new Enumerator(_operation));
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<IOperation>)this).GetEnumerator();

        public bool Any() => Count > 0;

        public IOperation First() {
            var enumerator = GetEnumerator();

            if (enumerator.MoveNext())
                return enumerator.Current;

            throw new InvalidOperationException();
        }

        public Reversed Reverse() => new Reversed(_operation);

        public IOperation Last() {
            var enumerator = Reverse().GetEnumerator();

            if (enumerator.MoveNext())
                return enumerator.Current;

            throw new InvalidOperationException();
        }
    }
}
