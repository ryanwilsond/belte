namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    internal enum BetterResult : byte {
        Left,
        Right,
        Neither,
        Equal
    }
}
