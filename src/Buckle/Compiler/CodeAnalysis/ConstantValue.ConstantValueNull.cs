
namespace Buckle.CodeAnalysis;

internal partial class ConstantValue {
    private sealed class ConstantValueNull : ConstantValue {
        private ConstantValueNull() { }

        internal static readonly ConstantValueNull Uninitialized = new ConstantValueNull();
    }
}
