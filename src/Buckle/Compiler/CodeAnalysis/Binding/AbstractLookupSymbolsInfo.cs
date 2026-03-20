using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class AbstractLookupSymbolsInfo<TSymbol> where TSymbol : class, ISymbol {
    internal struct ArityEnumerator : IEnumerator<int> {
        private const int ResetValue = -1;
        private const int ReachedEndValue = int.MaxValue;

        private int _current;
        private readonly int _low32bits;
        private int[]? _arities;

        internal ArityEnumerator(int bitVector, HashSet<int>? arities) {
            _current = ResetValue;
            _low32bits = bitVector;

            if (arities is null) {
                _arities = null;
            } else {
                _arities = [.. arities];
                Array.Sort(_arities);
            }
        }

        public int Current => _current;

        public void Dispose() => _arities = null;

        object? System.Collections.IEnumerator.Current => _current;

        public bool MoveNext() {
            if (_current == ReachedEndValue)
                return false;

            int arity;

            for (arity = ++_current; arity < 32; arity++) {
                if (((_low32bits >> arity) & 1) != 0) {
                    _current = arity;
                    return true;
                }
            }

            if (_arities is not null) {
                var index = _arities.BinarySearch(arity);

                if (index < 0)
                    index = ~index;

                if (index < _arities.Length) {
                    _current = _arities[index];
                    return true;
                }
            }

            _current = ReachedEndValue;
            return false;
        }

        public void Reset() => _current = ResetValue;
    }

    public interface IArityEnumerable {
        ArityEnumerator GetEnumerator();
        int Count { get; }
    }

    private struct UniqueSymbolOrArities : IArityEnumerable {
        private object? _uniqueSymbolOrArities;
        private int _arityBitVectorOrUniqueArity;

        internal UniqueSymbolOrArities(int arity, TSymbol uniqueSymbol) {
            _uniqueSymbolOrArities = uniqueSymbol;
            _arityBitVectorOrUniqueArity = arity;
        }

        internal void AddSymbol(TSymbol symbol, int arity) {
            if (symbol is not null && symbol == _uniqueSymbolOrArities)
                return;

            if (_hasUniqueSymbol) {
                _uniqueSymbolOrArities = null;

                var uniqueArity = _arityBitVectorOrUniqueArity;
                _arityBitVectorOrUniqueArity = 0;
                AddArity(uniqueArity);
            }

            AddArity(arity);
        }

        private bool _hasUniqueSymbol
            => _uniqueSymbolOrArities is not null && _uniqueSymbolOrArities is not HashSet<int>;

        private void AddArity(int arity) {
            if (arity < 32) {
                unchecked {
                    var bit = 1 << arity;
                    _arityBitVectorOrUniqueArity |= bit;
                }

                return;
            }

            if (_uniqueSymbolOrArities is not HashSet<int> hashSet) {
                hashSet = [];
                _uniqueSymbolOrArities = hashSet;
            }

            hashSet.Add(arity);
        }

        internal void GetUniqueSymbolOrArities(out IArityEnumerable? arities, out TSymbol? uniqueSymbol) {
            if (_hasUniqueSymbol) {
                arities = null;
                uniqueSymbol = (TSymbol)_uniqueSymbolOrArities;
            } else {
                arities = (_uniqueSymbolOrArities is null && _arityBitVectorOrUniqueArity == 0)
                    ? null
                    : (IArityEnumerable)this;

                uniqueSymbol = null;
            }
        }

        public readonly ArityEnumerator GetEnumerator() {
            return new ArityEnumerator(_arityBitVectorOrUniqueArity, (HashSet<int>?)_uniqueSymbolOrArities);
        }

        public readonly int Count {
            get {
                var count = BitArithmeticUtilities.CountBits(_arityBitVectorOrUniqueArity);
                var set = (HashSet<int>?)_uniqueSymbolOrArities;

                if (set is not null)
                    count += set.Count;

                return count;
            }
        }
    }

    private readonly IEqualityComparer<string> _comparer;
    private readonly Dictionary<string, UniqueSymbolOrArities> _nameMap;

    internal string filterName { get; set; }

    private protected AbstractLookupSymbolsInfo(IEqualityComparer<string> comparer) {
        _comparer = comparer;
        _nameMap = new Dictionary<string, UniqueSymbolOrArities>(comparer);
    }

    internal bool CanBeAdded(string name) => filterName is null || _comparer.Equals(name, filterName);

    internal void AddSymbol(TSymbol symbol, string name, int arity) {
        if (!_nameMap.TryGetValue(name, out var pair)) {
            pair = new UniqueSymbolOrArities(arity, symbol);
            _nameMap.Add(name, pair);
        } else {
            pair.AddSymbol(symbol, arity);
            _nameMap[name] = pair;
        }
    }

    internal ICollection<string> names => _nameMap.Keys;

    public int Count => _nameMap.Count;

    internal bool TryGetAritiesAndUniqueSymbol(string name, out IArityEnumerable? arities, out TSymbol? uniqueSymbol) {
        if (!_nameMap.TryGetValue(name, out var pair)) {
            arities = null;
            uniqueSymbol = null;
            return false;
        }

        pair.GetUniqueSymbolOrArities(out arities, out uniqueSymbol);
        return true;
    }

    public void Clear() {
        _nameMap.Clear();
        filterName = null;
    }
}
