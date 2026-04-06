
namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private interface FloatingTC<T> : INumericTC<T> {
        T NaN { get; }
    }
}
