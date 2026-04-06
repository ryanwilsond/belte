using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal partial struct SwitchIntegralJumpTableEmitter {
    private readonly CodeGenerator _generator;
    private readonly ILBuilder _builder;
    private readonly LocalOrParameter _key;
    private readonly SpecialType _keyTypeCode;
    private readonly object _fallThroughLabel;
    private readonly ImmutableArray<KeyValuePair<ConstantValue, object>> _sortedCaseLabels;

    private const int LinearSearchThreshold = 3;

    internal SwitchIntegralJumpTableEmitter(
        CodeGenerator generator,
        ILBuilder builder,
        KeyValuePair<ConstantValue, object>[] caseLabels,
        object fallThroughLabel,
        SpecialType keyTypeCode,
        LocalOrParameter key) {
        _generator = generator;
        _builder = builder;
        _key = key;
        _keyTypeCode = keyTypeCode;
        _fallThroughLabel = fallThroughLabel;

        // Array.Sort(caseLabels, CompareIntegralSwitchLabels);
        _sortedCaseLabels = ImmutableArray.Create(caseLabels);
    }

    internal void EmitJumpTable() {
        var sortedCaseLabels = _sortedCaseLabels;

        var endLabelIndex = sortedCaseLabels.Length - 1;
        int startLabelIndex;

        if (sortedCaseLabels[0].Key != ConstantValue.Null)
            startLabelIndex = 0;
        else
            startLabelIndex = 1;

        if (startLabelIndex <= endLabelIndex) {
            // var switchBuckets = GenerateSwitchBuckets(startLabelIndex, endLabelIndex);
            // EmitSwitchBuckets(switchBuckets, 0, switchBuckets.Length - 1);
        } else {
            _builder.EmitBranch(OpCode.Br, _fallThroughLabel);
        }
    }

    /*
    private static int CompareIntegralSwitchLabels(
        KeyValuePair<ConstantValue, object> first,
        KeyValuePair<ConstantValue, object> second) {
        var firstConstant = first.Key;
        var secondConstant = second.Key;

        return SwitchConstantValueHelper.CompareSwitchCaseLabelConstants(firstConstant, secondConstant);
    }

    private ImmutableArray<SwitchBucket> GenerateSwitchBuckets(int startLabelIndex, int endLabelIndex) {
        var switchBucketsStack = ArrayBuilder<SwitchBucket>.GetInstance();
        var curStartLabelIndex = startLabelIndex;

        while (curStartLabelIndex <= endLabelIndex) {
            var newBucket = CreateNextBucket(curStartLabelIndex, endLabelIndex);

            while (!switchBucketsStack.IsEmpty()) {
                var prevBucket = switchBucketsStack.Peek();

                if (newBucket.TryMergeWith(prevBucket))
                    switchBucketsStack.Pop();
                else
                    break;
            }

            switchBucketsStack.Push(newBucket);
            curStartLabelIndex++;
        }

        var crumbled = ArrayBuilder<SwitchBucket>.GetInstance();

        foreach (var uncrumbled in switchBucketsStack) {
            var degenerateSplit = uncrumbled.DegenerateBucketSplit;
            switch (degenerateSplit) {
                case -1:
                    crumbled.Add(uncrumbled);
                    break;
                case 0:
                    crumbled.Add(new SwitchBucket(_sortedCaseLabels, uncrumbled.StartLabelIndex, uncrumbled.EndLabelIndex, isDegenerate: true));
                    break;
                default:
                    crumbled.Add(new SwitchBucket(_sortedCaseLabels, uncrumbled.StartLabelIndex, degenerateSplit - 1, isDegenerate: true));
                    crumbled.Add(new SwitchBucket(_sortedCaseLabels, degenerateSplit, uncrumbled.EndLabelIndex, isDegenerate: true));
                    break;
            }
        }

        switchBucketsStack.Free();
        return crumbled.ToImmutableAndFree();
    }

    private SwitchBucket CreateNextBucket(int startLabelIndex, int endLabelIndex) {
        return new SwitchBucket(_sortedCaseLabels, startLabelIndex);
    }

    private void EmitSwitchBucketsLinearLeaf(ImmutableArray<SwitchBucket> switchBuckets, int low, int high) {
        for (var i = low; i < high; i++) {
            var nextBucketLabel = new object();
            EmitSwitchBucket(switchBuckets[i], nextBucketLabel);
            _builder.MarkLabel(nextBucketLabel);
        }

        EmitSwitchBucket(switchBuckets[high], _fallThroughLabel);
    }

    private void EmitSwitchBuckets(ImmutableArray<SwitchBucket> switchBuckets, int low, int high) {
        if (high - low < LinearSearchThreshold) {
            EmitSwitchBucketsLinearLeaf(switchBuckets, low, high);
            return;
        }

        var mid = (low + high + 1) / 2;
        var secondHalfLabel = new object();
        var pivotConstant = switchBuckets[mid - 1].EndConstant;

        EmitCondBranchForSwitch(
            _keyTypeCode.IsUnsigned() ? OpCode.Bgt_Un : OpCode.Bgt,
            pivotConstant,
            secondHalfLabel
        );

        EmitSwitchBuckets(switchBuckets, low, mid - 1);

        _builder.MarkLabel(secondHalfLabel);

        EmitSwitchBuckets(switchBuckets, mid, high);
    }

    private void EmitSwitchBucket(SwitchBucket switchBucket, object bucketFallThroughLabel) {
        if (switchBucket.LabelsCount == 1) {
            var c = switchBucket[0];
            var constant = c.Key;
            var caseLabel = c.Value;
            EmitEqBranchForSwitch(constant, caseLabel);
        } else {
            if (switchBucket.IsDegenerate) {
                EmitRangeCheckedBranch(switchBucket.StartConstant, switchBucket.EndConstant, switchBucket[0].Value);
            } else {
                EmitNormalizedSwitchKey(switchBucket.StartConstant, switchBucket.EndConstant, bucketFallThroughLabel);

                var labels = CreateBucketLabels(switchBucket);

                _builder.EmitSwitch(labels);
            }
        }

        _builder.EmitBranch(OpCode.Br, bucketFallThroughLabel);
    }

    private object[] CreateBucketLabels(SwitchBucket switchBucket) {
        var startConstant = switchBucket.StartConstant;
        bool hasNegativeCaseLabels = startConstant.IsNegativeNumeric;

        var nextCaseIndex = 0;
        ulong nextCaseLabelNormalizedValue = 0;

        var bucketSize = switchBucket.BucketSize;
        var labels = new object[bucketSize];

        for (ulong i = 0; i < bucketSize; ++i) {
            if (i == nextCaseLabelNormalizedValue) {
                labels[i] = switchBucket[nextCaseIndex].Value;
                nextCaseIndex++;

                if (nextCaseIndex >= switchBucket.LabelsCount)
                    break;

                var caseLabelConstant = switchBucket[nextCaseIndex].Key;

                if (hasNegativeCaseLabels) {
                    var nextCaseLabelValue = caseLabelConstant.Int64Value;
                    nextCaseLabelNormalizedValue = (ulong)(nextCaseLabelValue - startConstant.Int64Value);
                } else {
                    var nextCaseLabelValue = caseLabelConstant.UInt64Value;
                    nextCaseLabelNormalizedValue = nextCaseLabelValue - startConstant.UInt64Value;
                }

                continue;
            }

            labels[i] = _fallThroughLabel;
        }

        return labels;
    }

    private void EmitCondBranchForSwitch(OpCode branchCode, ConstantValue constant, object targetLabel) {
        _builder.EmitLoad(_key);
        _generator.EmitConstantValue(constant, CorLibrary.GetSpecialType(constant.specialType));
        _builder.EmitBranch(branchCode, targetLabel, GetReverseBranchCode(branchCode));
    }

    private void EmitEqBranchForSwitch(ConstantValue constant, object targetLabel) {
        _builder.EmitLoad(_key);

        if (constant.IsDefaultValue) {
            // ldloc key
            // brfalse targetLabel
            _generator.EmitBranch(ILOpCode.Brfalse, targetLabel);
        } else {
            _generator.EmitConstantValue(constant);
            _generator.EmitBranch(ILOpCode.Beq, targetLabel);
        }
    }

    private void EmitRangeCheckedBranch(ConstantValue startConstant, ConstantValue endConstant, object targetLabel) {
        _generator.EmitLoad(_key);

        // Normalize the key to 0 if needed

        // Emit:    ldc constant
        //          sub
        if (!startConstant.IsDefaultValue) {
            _generator.EmitConstantValue(startConstant);
            _generator.EmitOpCode(ILOpCode.Sub);
        }

        if (_keyTypeCode.Is64BitIntegral()) {
            _generator.EmitLongConstant(endConstant.Int64Value - startConstant.Int64Value);
        } else {
            int Int32Value(ConstantValue value) {
                // ConstantValue does not correctly convert byte and ushort values to int.
                // It sign extends them rather than padding them. We compensate for that here.
                // See also https://github.com/dotnet/roslyn/issues/18579
                switch (value.Discriminator) {
                    case ConstantValueTypeDiscriminator.Byte: return value.ByteValue;
                    case ConstantValueTypeDiscriminator.UInt16: return value.UInt16Value;
                    default: return value.Int32Value;
                }
            }

            _generator.EmitIntConstant(Int32Value(endConstant) - Int32Value(startConstant));
        }

        _generator.EmitBranch(ILOpCode.Ble_un, targetLabel, ILOpCode.Bgt_un);
    }

    private static ILOpCode GetReverseBranchCode(ILOpCode branchCode) {
        switch (branchCode) {
            case ILOpCode.Beq:
                return ILOpCode.Bne_un;

            case ILOpCode.Blt:
                return ILOpCode.Bge;

            case ILOpCode.Blt_un:
                return ILOpCode.Bge_un;

            case ILOpCode.Bgt:
                return ILOpCode.Ble;

            case ILOpCode.Bgt_un:
                return ILOpCode.Ble_un;

            default:
                throw ExceptionUtilities.UnexpectedValue(branchCode);
        }
    }

    private void EmitNormalizedSwitchKey(ConstantValue startConstant, ConstantValue endConstant, object bucketFallThroughLabel) {
        _generator.EmitLoad(_key);

        // Normalize the key to 0 if needed

        // Emit:    ldc constant
        //          sub
        if (!startConstant.IsDefaultValue) {
            _generator.EmitConstantValue(startConstant);
            _generator.EmitOpCode(ILOpCode.Sub);
        }

        // range-check normalized value if needed
        EmitRangeCheckIfNeeded(startConstant, endConstant, bucketFallThroughLabel);

        // truncate key to 32bit
        _generator.EmitNumericConversion(_keyTypeCode, Microsoft.Cci.PrimitiveTypeCode.UInt32, false);
    }

    private void EmitRangeCheckIfNeeded(ConstantValue startConstant, ConstantValue endConstant, object bucketFallThroughLabel) {
        // switch treats key as an unsigned int.
        // this ensures that normalization does not introduce [over|under]flows issues with 32bit or shorter keys.
        // 64bit values, however must be checked before 32bit truncation happens.
        if (_keyTypeCode.Is64BitIntegral()) {
            // Dup(normalized);
            // if ((ulong)(normalized) > (ulong)(endConstant - startConstant))
            // {
            //      // not going to use it in the switch
            //      Pop(normalized);
            //      goto bucketFallThroughLabel;
            // }

            var inRangeLabel = new object();

            _generator.EmitOpCode(ILOpCode.Dup);
            _generator.EmitLongConstant(endConstant.Int64Value - startConstant.Int64Value);
            _generator.EmitBranch(ILOpCode.Ble_un, inRangeLabel, ILOpCode.Bgt_un);
            _generator.EmitOpCode(ILOpCode.Pop);
            _generator.EmitBranch(ILOpCode.Br, bucketFallThroughLabel);
            // If we get to inRangeLabel, we should have key on stack, adjust for that.
            // builder cannot infer this since it has not seen all branches,
            // but it will verify that our Adjustment is valid when more branches are known.
            _generator.AdjustStack(+1);
            _generator.MarkLabel(inRangeLabel);
        }
    }
    private struct SwitchBucket {
        // sorted case labels
        private readonly ImmutableArray<KeyValuePair<ConstantValue, object>> _allLabels;

        // range of sorted case labels within this bucket
        private readonly int _startLabelIndex;
        private readonly int _endLabelIndex;

        private readonly bool _isKnownDegenerate;

        /// <summary>
        ///  Degenerate buckets here are buckets with contiguous range of constants
        ///  leading to the same label. Like:
        ///
        ///      case 0:
        ///      case 1:
        ///      case 2:
        ///      case 3:
        ///           DoOneThing();
        ///           break;
        ///
        ///      case 4:
        ///      case 5:
        ///      case 6:
        ///      case 7:
        ///           DoAnotherThing();
        ///           break;
        ///
        ///  NOTE: A trivial bucket with only one case constant is by definition degenerate.
        /// </summary>
        internal bool IsDegenerate {
            get {
                return _isKnownDegenerate;
            }
        }

        internal SwitchBucket(ImmutableArray<KeyValuePair<ConstantValue, object>> allLabels, int index) {
            _startLabelIndex = index;
            _endLabelIndex = index;
            _allLabels = allLabels;
            _isKnownDegenerate = true;
        }

        private SwitchBucket(ImmutableArray<KeyValuePair<ConstantValue, object>> allLabels, int startIndex, int endIndex) {
            Debug.Assert((uint)startIndex < (uint)endIndex);

            _startLabelIndex = startIndex;
            _endLabelIndex = endIndex;
            _allLabels = allLabels;
            _isKnownDegenerate = false;
        }

        internal SwitchBucket(ImmutableArray<KeyValuePair<ConstantValue, object>> allLabels, int startIndex, int endIndex, bool isDegenerate) {
            Debug.Assert((uint)startIndex <= (uint)endIndex);
            Debug.Assert((uint)startIndex != (uint)endIndex || isDegenerate);

            _startLabelIndex = startIndex;
            _endLabelIndex = endIndex;
            _allLabels = allLabels;
            _isKnownDegenerate = isDegenerate;
        }

        internal uint LabelsCount {
            get {
                return (uint)(_endLabelIndex - _startLabelIndex + 1);
            }
        }

        internal KeyValuePair<ConstantValue, object> this[int i] {
            get {
                Debug.Assert(i < LabelsCount, "index out of range");
                return _allLabels[i + _startLabelIndex];
            }
        }

        internal ulong BucketSize {
            get {
                return GetBucketSize(this.StartConstant, this.EndConstant);
            }
        }

        // if a bucket could be split into two degenerate ones
        // specifies a label index where the second bucket would start
        // -1 indicates that the bucket cannot be split into degenerate ones
        //  0 indicates that the bucket is already degenerate
        //
        // Code Review question: why are we supporting splitting only in two buckets. Why not in more?
        // Explanation:
        //  The input here is a "dense" bucket - the one that previous heuristics
        //  determined as not worth splitting.
        //
        //  A dense bucket has rough execution cost of 1 conditional branch (range check)
        //  and 1 computed branch (which cost roughly the same as conditional one or perhaps more).
        //  The only way to surely beat that cost via splitting is if the bucket can be
        //  split into 2 degenerate buckets. Then we have just 2 conditional branches.
        //
        //  3 degenerate buckets would require up to 3 conditional branches.
        //  On some hardware computed jumps may cost significantly more than
        //  conditional ones (because they are harder to predict or whatever),
        //  so it could still be profitable, but I did not want to guess that.
        //
        //  Basically if we have 3 degenerate buckets that can be merged into a dense bucket,
        //  we prefer a dense bucket, which we emit as "switch" opcode.
        //
        internal int DegenerateBucketSplit {
            get {
                if (IsDegenerate) {
                    return 0;
                }

                Debug.Assert(_startLabelIndex != _endLabelIndex, "1-sized buckets should be already known as degenerate.");

                var allLabels = this._allLabels;
                var split = 0;
                var lastConst = this.StartConstant;
                var lastLabel = allLabels[_startLabelIndex].Value;

                for (var idx = _startLabelIndex + 1; idx <= _endLabelIndex; idx++) {
                    var switchLabel = allLabels[idx];

                    if (lastLabel != switchLabel.Value ||
                        !IsContiguous(lastConst, switchLabel.Key)) {
                        if (split != 0) {
                            // found another discontinuity, so cannot be split
                            return -1;
                        }

                        split = idx;
                        lastLabel = switchLabel.Value;
                    }

                    lastConst = switchLabel.Key;
                }

                return split;
            }
        }

        private bool IsContiguous(ConstantValue lastConst, ConstantValue nextConst) {
            if (!lastConst.IsNumeric || !nextConst.IsNumeric) {
                return false;
            }

            return GetBucketSize(lastConst, nextConst) == 2;
        }

        private static ulong GetBucketSize(ConstantValue startConstant, ConstantValue endConstant) {
            Debug.Assert(!BucketOverflowUInt64Limit(startConstant, endConstant));
            Debug.Assert(endConstant.Discriminator == startConstant.Discriminator);

            ulong bucketSize;

            if (startConstant.IsNegativeNumeric || endConstant.IsNegativeNumeric) {
                Debug.Assert(endConstant.Int64Value >= startConstant.Int64Value);
                bucketSize = unchecked((ulong)(endConstant.Int64Value - startConstant.Int64Value + 1));
            } else {
                Debug.Assert(endConstant.UInt64Value >= startConstant.UInt64Value);
                bucketSize = endConstant.UInt64Value - startConstant.UInt64Value + 1;
            }

            return bucketSize;
        }

        // Check if bucket size exceeds UInt64.MaxValue
        private static bool BucketOverflowUInt64Limit(ConstantValue startConstant, ConstantValue endConstant) {
            Debug.Assert(IsValidSwitchBucketConstantPair(startConstant, endConstant));

            if (startConstant.Discriminator == ConstantValueTypeDiscriminator.Int64) {
                return startConstant.Int64Value == Int64.MinValue
                    && endConstant.Int64Value == Int64.MaxValue;
            } else if (startConstant.Discriminator == ConstantValueTypeDiscriminator.UInt64) {
                return startConstant.UInt64Value == UInt64.MinValue
                    && endConstant.UInt64Value == UInt64.MaxValue;
            }

            return false;
        }

        // Virtual switch instruction has a max limit of Int32.MaxValue labels
        // Check if bucket size exceeds Int32.MaxValue
        private static bool BucketOverflow(ConstantValue startConstant, ConstantValue endConstant) {
            return BucketOverflowUInt64Limit(startConstant, endConstant)
                || GetBucketSize(startConstant, endConstant) > Int32.MaxValue;
        }

        internal int StartLabelIndex {
            get {
                return _startLabelIndex;
            }
        }

        internal int EndLabelIndex {
            get {
                return _endLabelIndex;
            }
        }

        internal ConstantValue StartConstant {
            get {
                return _allLabels[_startLabelIndex].Key;
            }
        }

        internal ConstantValue EndConstant {
            get {
                return _allLabels[_endLabelIndex].Key;
            }
        }

        private static bool IsValidSwitchBucketConstant(ConstantValue constant) {
            return constant != null
                && SwitchConstantValueHelper.IsValidSwitchCaseLabelConstant(constant)
                && !constant.IsNull
                && !constant.IsString;
        }

        private static bool IsValidSwitchBucketConstantPair(ConstantValue startConstant, ConstantValue endConstant) {
            return IsValidSwitchBucketConstant(startConstant)
                && IsValidSwitchBucketConstant(endConstant)
                && startConstant.isUnsigned == endConstant.isUnsigned;
        }

        private static bool IsSparse(uint labelsCount, ulong bucketSize) {
            return bucketSize >= labelsCount * 2;
        }

        internal static bool MergeIsAdvantageous(SwitchBucket bucket1, SwitchBucket bucket2) {
            var startConstant = bucket1.StartConstant;
            var endConstant = bucket2.EndConstant;

            if (BucketOverflow(startConstant, endConstant))
                return false;

            var labelsCount = (uint)(bucket1.LabelsCount + bucket2.LabelsCount);
            var bucketSize = GetBucketSize(startConstant, endConstant);

            return !IsSparse(labelsCount, bucketSize);
        }

        internal bool TryMergeWith(SwitchBucket prevBucket) {
            if (MergeIsAdvantageous(prevBucket, this)) {
                this = new SwitchBucket(_allLabels, prevBucket._startLabelIndex, _endLabelIndex);
                return true;
            }

            return false;
        }
    }
    */
}
