
namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal enum MessageKind : byte {
        Parsed,
        Bound,
        BeforeEmit,
        Finished,
    }
}
