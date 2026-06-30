
namespace Buckle.CodeAnalysis.Binding;

internal enum ForEachLoopKind : byte {
    Invalid = 0,
    Array,
    String,
    Enumerator,
    Length,
    Iter,
    IEnumerable
}
