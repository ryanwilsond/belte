
namespace Buckle.CodeAnalysis.Lowering;

internal enum ClosureKind : byte {
    Static,
    Singleton,
    ThisOnly,
    General,
}
