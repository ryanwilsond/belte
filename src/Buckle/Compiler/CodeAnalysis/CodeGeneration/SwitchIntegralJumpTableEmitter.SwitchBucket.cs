using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal partial struct SwitchIntegralJumpTableEmitter {
    private struct SwitchBucket {
        private readonly ImmutableArray<KeyValuePair<ConstantValue, object>> _allLabels;

        internal SwitchBucket(ImmutableArray<KeyValuePair<ConstantValue, object>> allLabels, int index) {
            startLabelIndex = index;
            endLabelIndex = index;
            _allLabels = allLabels;
            isDegenerate = true;
        }

        private SwitchBucket(
            ImmutableArray<KeyValuePair<ConstantValue, object>> allLabels,
            int startIndex,
            int endIndex) {
            startLabelIndex = startIndex;
            endLabelIndex = endIndex;
            _allLabels = allLabels;
            isDegenerate = false;
        }

        internal SwitchBucket(
            ImmutableArray<KeyValuePair<ConstantValue, object>> allLabels,
            int startIndex,
            int endIndex,
            bool isDegenerate) {
            startLabelIndex = startIndex;
            endLabelIndex = endIndex;
            _allLabels = allLabels;
            this.isDegenerate = isDegenerate;
        }

        internal bool isDegenerate { get; }

        internal uint labelsCount => (uint)(endLabelIndex - startLabelIndex + 1);

        internal KeyValuePair<ConstantValue, object> this[int i] => _allLabels[i + startLabelIndex];

        internal ulong bucketSize => GetBucketSize(startConstant, endConstant);

        internal int degenerateBucketSplit {
            get {
                if (isDegenerate)
                    return 0;

                var allLabels = _allLabels;
                var split = 0;
                var lastConst = startConstant;
                var lastLabel = allLabels[startLabelIndex].Value;

                for (var idx = startLabelIndex + 1; idx <= endLabelIndex; idx++) {
                    var switchLabel = allLabels[idx];

                    if (lastLabel != switchLabel.Value || !IsContiguous(lastConst, switchLabel.Key)) {
                        if (split != 0)
                            return -1;

                        split = idx;
                        lastLabel = switchLabel.Value;
                    }

                    lastConst = switchLabel.Key;
                }

                return split;
            }
        }

        internal int startLabelIndex { get; }

        internal int endLabelIndex { get; }

        internal ConstantValue startConstant => _allLabels[startLabelIndex].Key;

        internal ConstantValue endConstant => _allLabels[endLabelIndex].Key;

        private bool IsContiguous(ConstantValue lastConst, ConstantValue nextConst) {
            if (!lastConst.specialType.IsNumeric() || !nextConst.specialType.IsNumeric())
                return false;

            return GetBucketSize(lastConst, nextConst) == 2;
        }

        private static ulong GetBucketSize(ConstantValue startConstant, ConstantValue endConstant) {
            ulong bucketSize;

            if (startConstant.IsNegativeNumeric() || endConstant.IsNegativeNumeric()) {
                LiteralUtilities.TrySpecialCastCore(endConstant.value, endConstant.specialType, SpecialType.Int64, out var lEnd);
                LiteralUtilities.TrySpecialCastCore(startConstant.value, startConstant.specialType, SpecialType.Int64, out var lStart);
                bucketSize = unchecked((ulong)((long)lEnd - (long)lStart + 1));
            } else {
                LiteralUtilities.TrySpecialCastCore(endConstant.value, endConstant.specialType, SpecialType.UInt64, out var ulEnd);
                LiteralUtilities.TrySpecialCastCore(startConstant.value, startConstant.specialType, SpecialType.UInt64, out var ulStart);
                bucketSize = (ulong)ulEnd - (ulong)ulStart + 1;
            }

            return bucketSize;
        }

        private static bool BucketOverflowUInt64Limit(ConstantValue startConstant, ConstantValue endConstant) {
            if (startConstant.specialType == SpecialType.Int64) {
                return (long)startConstant.value == long.MinValue
                    && (long)endConstant.value == long.MaxValue;
            } else if (startConstant.specialType == SpecialType.UInt64) {
                return (ulong)startConstant.value == ulong.MinValue
                    && (ulong)endConstant.value == ulong.MaxValue;
            }

            return false;
        }

        private static bool BucketOverflow(ConstantValue startConstant, ConstantValue endConstant) {
            return BucketOverflowUInt64Limit(startConstant, endConstant)
                || GetBucketSize(startConstant, endConstant) > int.MaxValue;
        }

        private static bool IsValidSwitchBucketConstant(ConstantValue constant) {
            return constant is not null
                && ConstantValueHelpers.IsValidSwitchCaseLabelConstant(constant)
                && !ConstantValue.IsNull(constant)
                && constant.specialType != SpecialType.String;
        }

        private static bool IsValidSwitchBucketConstantPair(ConstantValue startConstant, ConstantValue endConstant) {
            return IsValidSwitchBucketConstant(startConstant)
                && IsValidSwitchBucketConstant(endConstant)
                && startConstant.specialType.IsUnsigned() == endConstant.specialType.IsUnsigned();
        }

        private static bool IsSparse(uint labelsCount, ulong bucketSize) {
            return bucketSize >= labelsCount * 2;
        }

        internal static bool MergeIsAdvantageous(SwitchBucket bucket1, SwitchBucket bucket2) {
            var startConstant = bucket1.startConstant;
            var endConstant = bucket2.endConstant;

            if (BucketOverflow(startConstant, endConstant))
                return false;

            var labelsCount = (uint)(bucket1.labelsCount + bucket2.labelsCount);
            var bucketSize = GetBucketSize(startConstant, endConstant);

            return !IsSparse(labelsCount, bucketSize);
        }

        internal bool TryMergeWith(SwitchBucket prevBucket) {
            if (MergeIsAdvantageous(prevBucket, this)) {
                this = new SwitchBucket(_allLabels, prevBucket.startLabelIndex, endLabelIndex);
                return true;
            }

            return false;
        }
    }
}
