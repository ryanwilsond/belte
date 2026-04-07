using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Buckle.Utilities;
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

        Array.Sort(caseLabels, CompareIntegralSwitchLabels);
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
            var switchBuckets = GenerateSwitchBuckets(startLabelIndex, endLabelIndex);
            EmitSwitchBuckets(switchBuckets, 0, switchBuckets.Length - 1);
        } else {
            _builder.EmitBranch(OpCode.Br, _fallThroughLabel);
        }
    }

    private static int CompareIntegralSwitchLabels(
        KeyValuePair<ConstantValue, object> first,
        KeyValuePair<ConstantValue, object> second) {
        var firstConstant = first.Key;
        var secondConstant = second.Key;
        return ConstantValueHelpers.CompareSwitchCaseLabelConstants(firstConstant, secondConstant);
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
            var degenerateSplit = uncrumbled.degenerateBucketSplit;
            switch (degenerateSplit) {
                case -1:
                    crumbled.Add(uncrumbled);
                    break;
                case 0:
                    crumbled.Add(new SwitchBucket(_sortedCaseLabels, uncrumbled.startLabelIndex, uncrumbled.endLabelIndex, isDegenerate: true));
                    break;
                default:
                    crumbled.Add(new SwitchBucket(_sortedCaseLabels, uncrumbled.startLabelIndex, degenerateSplit - 1, isDegenerate: true));
                    crumbled.Add(new SwitchBucket(_sortedCaseLabels, degenerateSplit, uncrumbled.endLabelIndex, isDegenerate: true));
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
        var pivotConstant = switchBuckets[mid - 1].endConstant;

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
        if (switchBucket.labelsCount == 1) {
            var c = switchBucket[0];
            var constant = c.Key;
            var caseLabel = c.Value;
            EmitEqBranchForSwitch(constant, caseLabel);
        } else {
            if (switchBucket.isDegenerate) {
                EmitRangeCheckedBranch(switchBucket.startConstant, switchBucket.endConstant, switchBucket[0].Value);
            } else {
                EmitNormalizedSwitchKey(switchBucket.startConstant, switchBucket.endConstant, bucketFallThroughLabel);

                var labels = CreateBucketLabels(switchBucket);

                _builder.EmitSwitch(labels);
            }
        }

        _builder.EmitBranch(OpCode.Br, bucketFallThroughLabel);
    }

    private object[] CreateBucketLabels(SwitchBucket switchBucket) {
        var startConstant = switchBucket.startConstant;
        var hasNegativeCaseLabels = startConstant.IsNegativeNumeric();

        var nextCaseIndex = 0;
        ulong nextCaseLabelNormalizedValue = 0;

        var bucketSize = switchBucket.bucketSize;
        var labels = new object[bucketSize];

        for (ulong i = 0; i < bucketSize; ++i) {
            if (i == nextCaseLabelNormalizedValue) {
                labels[i] = switchBucket[nextCaseIndex].Value;
                nextCaseIndex++;

                if (nextCaseIndex >= switchBucket.labelsCount)
                    break;

                var caseLabelConstant = switchBucket[nextCaseIndex].Key;

                if (hasNegativeCaseLabels) {
                    LiteralUtilities.TrySpecialCastCore(caseLabelConstant.value, caseLabelConstant.specialType, SpecialType.Int64, out var lCaseLabel);
                    LiteralUtilities.TrySpecialCastCore(startConstant.value, startConstant.specialType, SpecialType.Int64, out var lStart);
                    var nextCaseLabelValue = (long)lCaseLabel;
                    nextCaseLabelNormalizedValue = (ulong)(nextCaseLabelValue - (long)lStart);
                } else {
                    LiteralUtilities.TrySpecialCastCore(caseLabelConstant.value, caseLabelConstant.specialType, SpecialType.UInt64, out var ulCaseLabel);
                    LiteralUtilities.TrySpecialCastCore(startConstant.value, startConstant.specialType, SpecialType.UInt64, out var ulStart);
                    var nextCaseLabelValue = (ulong)ulCaseLabel;
                    nextCaseLabelNormalizedValue = nextCaseLabelValue - (ulong)ulStart;
                }

                continue;
            }

            labels[i] = _fallThroughLabel;
        }

        return labels;
    }

    private void EmitCondBranchForSwitch(OpCode branchCode, ConstantValue constant, object targetLabel) {
        _generator.EmitLoad(_key);
        _generator.EmitConstantValue(constant, CorLibrary.GetSpecialType(constant.specialType));
        _builder.EmitBranch(branchCode, targetLabel, GetReverseBranchCode(branchCode));
    }

    private void EmitEqBranchForSwitch(ConstantValue constant, object targetLabel) {
        _generator.EmitLoad(_key);

        if (LiteralUtilities.GetDefaultValue(constant.specialType) == constant.value) {
            _builder.EmitBranch(OpCode.Brfalse, targetLabel);
        } else {
            _generator.EmitConstantValue(constant, CorLibrary.GetSpecialType(constant.specialType));
            _builder.EmitBranch(OpCode.Beq, targetLabel);
        }
    }

    private void EmitRangeCheckedBranch(ConstantValue startConstant, ConstantValue endConstant, object targetLabel) {
        _generator.EmitLoad(_key);

        if (LiteralUtilities.GetDefaultValue(startConstant.specialType) != startConstant.value) {
            _generator.EmitConstantValue(startConstant, CorLibrary.GetSpecialType(startConstant.specialType));
            _builder.Emit(OpCode.Sub);
        }

        if (_keyTypeCode is SpecialType.Int64 or SpecialType.UInt64) {
            LiteralUtilities.TrySpecialCastCore(endConstant.value, endConstant.specialType, SpecialType.Int64, out var lEnd);
            LiteralUtilities.TrySpecialCastCore(startConstant.value, startConstant.specialType, SpecialType.Int64, out var lStart);
            _generator.EmitLongConstant((long)lEnd - (long)lStart);
        } else {
            LiteralUtilities.TrySpecialCastCore(endConstant.value, endConstant.specialType, SpecialType.Int32, out var iEnd);
            LiteralUtilities.TrySpecialCastCore(startConstant.value, startConstant.specialType, SpecialType.Int32, out var iStart);
            _generator.EmitIntConstant((int)iEnd - (int)iStart);
        }

        _builder.EmitBranch(OpCode.Ble_Un, targetLabel, OpCode.Bgt_Un);
    }

    private static OpCode GetReverseBranchCode(OpCode branchCode) {
        return branchCode switch {
            OpCode.Beq => OpCode.Bne_Un,
            OpCode.Blt => OpCode.Bge,
            OpCode.Blt_Un => OpCode.Bge_Un,
            OpCode.Bgt => OpCode.Ble,
            OpCode.Bgt_Un => OpCode.Ble_Un,
            _ => throw ExceptionUtilities.UnexpectedValue(branchCode),
        };
    }

    private void EmitNormalizedSwitchKey(ConstantValue startConstant, ConstantValue endConstant, object bucketFallThroughLabel) {
        _generator.EmitLoad(_key);

        if (LiteralUtilities.GetDefaultValue(startConstant.specialType) != startConstant.value) {
            _generator.EmitConstantValue(startConstant, CorLibrary.GetSpecialType(startConstant.specialType));
            _builder.Emit(OpCode.Sub);
        }

        EmitRangeCheckIfNeeded(startConstant, endConstant, bucketFallThroughLabel);
        _generator.EmitNumericConversion(_keyTypeCode, SpecialType.UInt32);
    }

    private void EmitRangeCheckIfNeeded(ConstantValue startConstant, ConstantValue endConstant, object bucketFallThroughLabel) {
        if (_keyTypeCode is SpecialType.Int64 or SpecialType.UInt64) {
            LiteralUtilities.TrySpecialCastCore(endConstant.value, endConstant.specialType, SpecialType.Int64, out var lEnd);
            LiteralUtilities.TrySpecialCastCore(startConstant.value, startConstant.specialType, SpecialType.Int64, out var lStart);

            var inRangeLabel = new object();

            _builder.Emit(OpCode.Dup);
            _generator.EmitLongConstant((long)lEnd - (long)lStart);
            _builder.EmitBranch(OpCode.Ble_Un, inRangeLabel, OpCode.Bgt_Un);
            _builder.Emit(OpCode.Pop);
            _builder.EmitBranch(OpCode.Br, bucketFallThroughLabel);

            // _generator.AdjustStack(+1);
            // _generator.MarkLabel(inRangeLabel);
            // !!! This should be +1
            _builder.MarkLabel(inRangeLabel);
        }
    }
}
