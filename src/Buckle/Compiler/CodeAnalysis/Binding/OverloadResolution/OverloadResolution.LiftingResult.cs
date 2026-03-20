
namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    private enum LiftingResult : byte {
        NotLifted,
        LiftOperandsAndResult,
        LiftOperandsButNotResult
    }
}
