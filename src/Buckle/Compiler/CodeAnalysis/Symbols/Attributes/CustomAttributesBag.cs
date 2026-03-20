using System.Collections.Immutable;
using System.Threading;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a bag of custom attributes and the associated decoded well-known attribute data.
/// </summary>
internal sealed partial class CustomAttributesBag<T> where T : AttributeData {
    internal static readonly CustomAttributesBag<T> Empty
        = new CustomAttributesBag<T>(CustomAttributeBagCompletionPart.All, []);

    private ImmutableArray<T> _customAttributes;
    private WellKnownAttributeData _decodedWellKnownAttributeData;
    // private EarlyWellKnownAttributeData _earlyDecodedWellKnownAttributeData;
    private int _state;

    private CustomAttributesBag(CustomAttributeBagCompletionPart part, ImmutableArray<T> customAttributes) {
        _customAttributes = customAttributes;
        NotePartComplete(part);
    }

    internal CustomAttributesBag() : this(CustomAttributeBagCompletionPart.None, default) { }

    internal static CustomAttributesBag<T> WithEmptyData() {
        return new CustomAttributesBag<T>(
            CustomAttributeBagCompletionPart.EarlyDecodedWellKnownAttributeData |
            CustomAttributeBagCompletionPart.DecodedWellKnownAttributeData,
            default
        );
    }

    internal bool isEmpty {
        get {
            return
                isSealed &&
                _customAttributes.IsEmpty &&
                _decodedWellKnownAttributeData == null
                // && _earlyDecodedWellKnownAttributeData == null
                ;
        }
    }

    // internal bool SetEarlyDecodedWellKnownAttributeData(EarlyWellKnownAttributeData data) {
    //     WellKnownAttributeData.Seal(data);
    //     // Early decode must complete before full decode
    //     Debug.Assert(!IsPartComplete(CustomAttributeBagCompletionPart.DecodedWellKnownAttributeData) || IsPartComplete(CustomAttributeBagCompletionPart.EarlyDecodedWellKnownAttributeData));
    //     var setOnOurThread = Interlocked.CompareExchange(ref _earlyDecodedWellKnownAttributeData, data, null) == null;
    //     NotePartComplete(CustomAttributeBagCompletionPart.EarlyDecodedWellKnownAttributeData);
    //     return setOnOurThread;
    // }

    internal ImmutableArray<T> attributes => _customAttributes;

    internal WellKnownAttributeData decodedWellKnownAttributeData => _decodedWellKnownAttributeData;

    // internal EarlyWellKnownAttributeData EarlyDecodedWellKnownAttributeData {
    //     get {
    //         Debug.Assert(IsPartComplete(CustomAttributeBagCompletionPart.EarlyDecodedWellKnownAttributeData));
    //         return _earlyDecodedWellKnownAttributeData;
    //     }
    // }

    private CustomAttributeBagCompletionPart state {
        get {
            return (CustomAttributeBagCompletionPart)_state;
        }
        set {
            _state = (int)value;
        }
    }

    internal bool isSealed => IsPartComplete(CustomAttributeBagCompletionPart.All);

    internal bool isEarlyDecodedWellKnownAttributeDataComputed
        => IsPartComplete(CustomAttributeBagCompletionPart.EarlyDecodedWellKnownAttributeData);

    internal bool isDecodedWellKnownAttributeDataComputed
        => IsPartComplete(CustomAttributeBagCompletionPart.DecodedWellKnownAttributeData);

    private void NotePartComplete(CustomAttributeBagCompletionPart part) {
        ThreadSafeFlagOperations.Set(ref _state, (int)(state | part));
    }

    internal bool IsPartComplete(CustomAttributeBagCompletionPart part) {
        return (state & part) == part;
    }

    internal bool SetDecodedWellKnownAttributeData(WellKnownAttributeData data) {
        var setOnOurThread = Interlocked.CompareExchange(ref _decodedWellKnownAttributeData, data, null) is null;
        NotePartComplete(CustomAttributeBagCompletionPart.DecodedWellKnownAttributeData);
        return setOnOurThread;
    }

    internal bool SetAttributes(ImmutableArray<T> newCustomAttributes) {
        var setOnOurThread = ImmutableInterlocked.InterlockedCompareExchange(
            ref _customAttributes,
            newCustomAttributes,
            default) == default;

        NotePartComplete(CustomAttributeBagCompletionPart.Attributes);
        return setOnOurThread;
    }
}
