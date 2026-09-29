using System.Collections.Generic;
using System.Diagnostics;

namespace Buckle.CodeAnalysis.CodeGeneration;

using HashBucket = List<KeyValuePair<ConstantValue, object>>;

internal readonly struct SwitchStringJumpTableEmitter {
    private readonly Compilation _compilation;
    private readonly CodeGenerator _generator;
    private readonly ILBuilder _builder;
    private readonly LocalOrParameter _key;
    private readonly KeyValuePair<ConstantValue, object>[] _caseLabels;
    private readonly object _fallThroughLabel;

    private readonly EmitStringCompareAndBranch _emitStringCondBranchDelegate;
    private readonly GetStringHashCode _computeStringHashcodeDelegate;
    private readonly LocalOrParameter? _keyHash;

    internal SwitchStringJumpTableEmitter(
        Compilation compilation,
        CodeGenerator generator,
        ILBuilder builder,
        LocalOrParameter key,
        KeyValuePair<ConstantValue, object>[] caseLabels,
        object fallThroughLabel,
        LocalOrParameter? keyHash,
        EmitStringCompareAndBranch emitStringCondBranchDelegate,
        GetStringHashCode computeStringHashcodeDelegate) {
        Debug.Assert(caseLabels.Length > 0);

        _compilation = compilation;
        _generator = generator;
        _builder = builder;
        _key = key;
        _caseLabels = caseLabels;
        _fallThroughLabel = fallThroughLabel;
        _keyHash = keyHash;
        _emitStringCondBranchDelegate = emitStringCondBranchDelegate;
        _computeStringHashcodeDelegate = computeStringHashcodeDelegate;
    }

    internal delegate void EmitStringCompareAndBranch(
        LocalOrParameter key,
        ConstantValue stringConstant,
        object targetLabel);

    internal delegate uint GetStringHashCode(string? key);

    internal void EmitJumpTable() {
        Debug.Assert(_keyHash is null || ShouldGenerateHashTableSwitch(_caseLabels.Length));

        if (_keyHash is not null)
            EmitHashTableSwitch();
        else
            EmitNonHashTableSwitch(_caseLabels);
    }

    private void EmitHashTableSwitch() {
        Debug.Assert(_keyHash is not null);

        var stringHashMap = ComputeStringHashMap(
            _caseLabels,
            _computeStringHashcodeDelegate
        );

        var hashBucketLabelsMap = EmitHashBucketJumpTable(stringHashMap);

        foreach (var kvPair in stringHashMap) {
            _builder.MarkLabel(hashBucketLabelsMap[kvPair.Key]);

            var hashBucket = kvPair.Value;
            EmitNonHashTableSwitch(hashBucket.ToArray());
        }
    }

    private Dictionary<uint, object> EmitHashBucketJumpTable(Dictionary<uint, HashBucket> stringHashMap) {
        var count = stringHashMap.Count;
        var hashBucketLabelsMap = new Dictionary<uint, object>(count);
        var jumpTableLabels = new KeyValuePair<ConstantValue, object>[count];
        var i = 0;

        foreach (var hashValue in stringHashMap.Keys) {
            var hashConstant = new ConstantValue(hashValue, SpecialType.UInt32);
            var hashBucketLabel = new object();

            jumpTableLabels[i] = new KeyValuePair<ConstantValue, object>(hashConstant, hashBucketLabel);
            hashBucketLabelsMap[hashValue] = hashBucketLabel;

            i++;
        }

        var hashBucketJumpTableEmitter = new SwitchIntegralJumpTableEmitter(
            _compilation,
            _generator,
            _builder,
            caseLabels: jumpTableLabels,
            fallThroughLabel: _fallThroughLabel,
            keyTypeCode: SpecialType.UInt32,
            key: _keyHash.Value
        );

        hashBucketJumpTableEmitter.EmitJumpTable();

        return hashBucketLabelsMap;
    }

    private void EmitNonHashTableSwitch(KeyValuePair<ConstantValue, object>[] labels) {
        foreach (var kvPair in labels)
            EmitCondBranchForStringSwitch(kvPair.Key, kvPair.Value);

        _builder.EmitBranch(OpCode.Br, _fallThroughLabel);
    }

    private void EmitCondBranchForStringSwitch(ConstantValue stringConstant, object targetLabel) {
        _emitStringCondBranchDelegate(_key, stringConstant, targetLabel);
    }

    private static Dictionary<uint, HashBucket> ComputeStringHashMap(
        KeyValuePair<ConstantValue, object>[] caseLabels,
        GetStringHashCode computeStringHashcodeDelegate) {
        var stringHashMap = new Dictionary<uint, HashBucket>(caseLabels.Length);

        foreach (var kvPair in caseLabels) {
            var stringConstant = kvPair.Key;
            Debug.Assert(ConstantValue.IsNull(stringConstant) || ConstantValue.IsString(stringConstant));

            var hash = computeStringHashcodeDelegate((string)stringConstant.value);

            if (!stringHashMap.TryGetValue(hash, out var bucket)) {
                bucket = new HashBucket();
                stringHashMap.Add(hash, bucket);
            }

            Debug.Assert(!bucket.Contains(kvPair));
            bucket.Add(kvPair);
        }

        return stringHashMap;
    }

    internal static bool ShouldGenerateHashTableSwitch(int labelsCount) {
        return labelsCount >= 7;
    }
}
