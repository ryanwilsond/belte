
namespace Buckle.CodeAnalysis;

public class Message {
    private readonly MessageKind _kind;

    internal Message(MessageKind kind) {
        _kind = kind;
    }

    public MessageKind Kind() {
        return _kind;
    }
}
