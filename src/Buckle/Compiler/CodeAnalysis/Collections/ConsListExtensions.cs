
namespace Buckle.CodeAnalysis;

internal static class ConsListExtensions {
    internal static ConsList<T> Prepend<T>(this ConsList<T>? list, T head) {
        return new ConsList<T>(head, list ?? ConsList<T>.Empty);
    }
}
