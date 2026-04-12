
namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal class Message {
        private readonly MessageKind _kind;

        internal Message(MessageKind kind) {
            _kind = kind;
        }

        internal MessageKind Kind() {
            return _kind;
        }
    }
}
