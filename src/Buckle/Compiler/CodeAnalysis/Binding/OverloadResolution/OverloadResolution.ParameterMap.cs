using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    internal readonly struct ParameterMap {
        private readonly int[] _parameters;
        private readonly int _length;

        internal ParameterMap(int[] parameters, int length) {
            _parameters = parameters;
            _length = length;
        }

        internal bool isTrivial => _parameters is null;

        internal int length => _length;

        internal int this[int argument] => _parameters is null ? argument : _parameters[argument];

        internal ImmutableArray<int> ToImmutableArray() {
            return _parameters.AsImmutableOrNull();
        }
    }
}
