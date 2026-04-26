
namespace Buckle.CodeAnalysis;

public enum MessageKind : byte {
    Parsed,
    Bound,
    BeforeEmit,
    Finished,
    Diagnostics,
}
