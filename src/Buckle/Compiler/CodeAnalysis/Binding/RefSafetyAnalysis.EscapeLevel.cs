
namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private enum EscapeLevel : uint {
        CallingMethod = CallingMethodScope,
        ReturnOnly = ReturnOnlyScope,
    }
}
