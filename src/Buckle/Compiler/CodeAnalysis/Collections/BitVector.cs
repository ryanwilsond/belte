using System;
using System.Collections.Generic;
using System.Diagnostics;
using Buckle.Utilities;
using Word = System.UInt64;

namespace Buckle.CodeAnalysis;

[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal struct BitVector : IEquatable<BitVector> {
    private const Word ZeroWord = 0;
    private const int Log2BitsPerWord = 6;

    internal const int BitsPerWord = 1 << Log2BitsPerWord;

    private static Word[] EmptyArray => Array.Empty<Word>();
    private static readonly BitVector NullValue = default;
    private static readonly BitVector EmptyValue = new(0, EmptyArray, 0);

    private Word _bits0;
    private Word[] _bits;
    private int _capacity;

    private BitVector(Word bits0, Word[] bits, int capacity) {
        _bits0 = bits0;
        _bits = bits;
        _capacity = capacity;
    }

    internal static BitVector Null => NullValue;

    internal static BitVector Empty => EmptyValue;

    internal readonly int capacity => _capacity;

    internal readonly bool isNull => _bits is null;

    public readonly bool Equals(BitVector other) {
        return _capacity == other._capacity &&
            _bits0 == other._bits0 &&
            _bits.AsSpan().SequenceEqual(other._bits.AsSpan());
    }

    public override readonly bool Equals(object obj) {
        return obj is BitVector other && Equals(other);
    }

    public static bool operator ==(BitVector left, BitVector right) {
        return left.Equals(right);
    }

    public static bool operator !=(BitVector left, BitVector right) {
        return !left.Equals(right);
    }

    public override int GetHashCode() {
        var bitsHash = _bits0.GetHashCode();

        if (_bits is not null) {
            for (var i = 0; i < _bits.Length; i++)
                bitsHash = Hash.Combine(_bits[i].GetHashCode(), bitsHash);
        }

        return Hash.Combine(_capacity, bitsHash);
    }

    internal void EnsureCapacity(int newCapacity) {
        if (newCapacity > _capacity) {
            var requiredWords = WordsForCapacity(newCapacity);

            if (requiredWords > _bits.Length)
                Array.Resize(ref _bits, requiredWords);

            _capacity = newCapacity;
        }
    }

    internal IEnumerable<Word> Words() {
        if (_capacity > 0) {
            yield return _bits0;
        }

        for (int i = 0, n = _bits?.Length ?? 0; i < n; i++) {
            yield return _bits![i];
        }
    }

    internal IEnumerable<int> TrueBits() {
        if (_bits0 != 0) {
            for (var bit = 0; bit < BitsPerWord; bit++) {
                var mask = ((Word)1) << bit;

                if ((_bits0 & mask) != 0) {
                    if (bit >= _capacity)
                        yield break;

                    yield return bit;
                }
            }
        }

        for (var i = 0; i < _bits.Length; i++) {
            var w = _bits[i];

            if (w != 0) {
                for (var b = 0; b < BitsPerWord; b++) {
                    var mask = ((Word)1) << b;

                    if ((w & mask) != 0) {
                        var bit = ((i + 1) << Log2BitsPerWord) | b;

                        if (bit >= _capacity)
                            yield break;

                        yield return bit;
                    }
                }
            }
        }
    }

    internal static BitVector FromWords(Word bits0, Word[] bits, int capacity) {
        return new BitVector(bits0, bits, capacity);
    }

    internal static BitVector Create(int capacity) {
        var requiredWords = WordsForCapacity(capacity);
        var bits = (requiredWords == 0) ? EmptyArray : new Word[requiredWords];
        return new BitVector(0, bits, capacity);
    }

    internal static BitVector AllSet(int capacity) {
        if (capacity == 0) {
            return Empty;
        }

        var requiredWords = WordsForCapacity(capacity);
        var bits = (requiredWords == 0) ? EmptyArray : new Word[requiredWords];
        var lastWord = requiredWords - 1;
        var bits0 = ~ZeroWord;

        for (var j = 0; j < lastWord; j++)
            bits[j] = ~ZeroWord;

        var numTrailingBits = capacity & (BitsPerWord - 1);

        if (numTrailingBits > 0) {
            var lastBits = ~((~ZeroWord) << numTrailingBits);

            if (lastWord < 0)
                bits0 = lastBits;
            else
                bits[lastWord] = lastBits;
        } else if (requiredWords > 0) {
            bits[lastWord] = ~ZeroWord;
        }

        return new BitVector(bits0, bits, capacity);
    }

    internal readonly BitVector Clone() {
        Word[] newBits;

        if (_bits is null || _bits.Length == 0)
            newBits = EmptyArray;
        else
            newBits = (Word[])_bits.Clone();

        return new BitVector(_bits0, newBits, _capacity);
    }

    internal void Invert() {
        _bits0 = ~_bits0;

        if (_bits is not null) {
            for (var i = 0; i < _bits.Length; i++)
                _bits[i] = ~_bits[i];
        }
    }

    internal bool IntersectWith(in BitVector other) {
        var anyChanged = false;
        var otherLength = other._bits.Length;
        var thisBits = _bits;
        var thisLength = thisBits.Length;

        if (otherLength > thisLength)
            otherLength = thisLength;

        {
            var oldV = _bits0;
            var newV = oldV & other._bits0;

            if (newV != oldV) {
                _bits0 = newV;
                anyChanged = true;
            }
        }

        for (var i = 0; i < otherLength; i++) {
            var oldV = thisBits[i];
            var newV = oldV & other._bits[i];

            if (newV != oldV) {
                thisBits[i] = newV;
                anyChanged = true;
            }
        }

        for (var i = otherLength; i < thisLength; i++) {
            if (thisBits[i] != 0) {
                thisBits[i] = 0;
                anyChanged = true;
            }
        }

        return anyChanged;
    }

    internal bool UnionWith(in BitVector other) {
        var anyChanged = false;

        if (other._capacity > _capacity)
            EnsureCapacity(other._capacity);

        var oldBits = _bits0;
        _bits0 |= other._bits0;

        if (oldBits != _bits0)
            anyChanged = true;

        for (var i = 0; i < other._bits.Length; i++) {
            oldBits = _bits[i];
            _bits[i] |= other._bits[i];

            if (_bits[i] != oldBits)
                anyChanged = true;
        }

        return anyChanged;
    }

    internal bool this[int index] {
        readonly get {
            if (index < 0)
                throw new IndexOutOfRangeException();

            if (index >= _capacity)
                return false;

            var i = (index >> Log2BitsPerWord) - 1;
            var word = (i < 0) ? _bits0 : _bits[i];

            return IsTrue(word, index);
        }

        set {
            if (index < 0)
                throw new IndexOutOfRangeException();

            if (index >= _capacity)
                EnsureCapacity(index + 1);

            var i = (index >> Log2BitsPerWord) - 1;
            var b = index & (BitsPerWord - 1);
            var mask = ((Word)1) << b;

            if (i < 0) {
                if (value)
                    _bits0 |= mask;
                else
                    _bits0 &= ~mask;
            } else {
                if (value)
                    _bits[i] |= mask;
                else
                    _bits[i] &= ~mask;
            }
        }
    }

    internal void Clear() {
        _bits0 = 0;

        if (_bits is not null)
            Array.Clear(_bits, 0, _bits.Length);
    }

    internal static bool IsTrue(Word word, int index) {
        var b = index & (BitsPerWord - 1);
        var mask = ((Word)1) << b;
        return (word & mask) != 0;
    }

    internal static int WordsRequired(int capacity) {
        if (capacity <= 0)
            return 0;

        return WordsForCapacity(capacity) + 1;
    }

    internal readonly string GetDebuggerDisplay() {
        var value = new char[_capacity];

        for (var i = 0; i < _capacity; i++)
            value[_capacity - i - 1] = this[i] ? '1' : '0';

        return new string(value);
    }

    private static int WordsForCapacity(int capacity) {
        if (capacity <= 0)
            return 0;

        var lastIndex = (capacity - 1) >> Log2BitsPerWord;
        return lastIndex;
    }
}
