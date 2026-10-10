
namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private enum InstanceUserDefinedIncrementUsageMode : byte {
        None,
        ResultIsNotUsed,
        ResultIsUsed
    }
}
