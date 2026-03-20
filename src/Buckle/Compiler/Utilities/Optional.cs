
namespace Buckle.Utilities;

public readonly struct Optional<T>(T value) {
    private readonly bool _hasValue = true;
    private readonly T _value = value;

    public bool hasValue => _hasValue;

    public T value => _value;

    public static implicit operator Optional<T>(T value) {
        return new Optional<T>(value);
    }

    public override string ToString() {
        return _hasValue
            ? _value?.ToString() ?? "null"
            : "unspecified";
    }
}
