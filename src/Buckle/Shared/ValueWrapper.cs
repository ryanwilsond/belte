
namespace Shared;

/// <summary>
/// Wraps a value-type object so it can be treated as a reference-type object.
/// </summary>
/// <typeparam name="T">Value type to wrap.</typeparam>
public class ValueWrapper<T> {
    /// <summary>
    /// The value-type object.
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// Creates an empty <see cref="ValueWrapper<T>" />.
    /// </summary>
    public ValueWrapper() { }

    /// <summary>
    /// Creates a new <see cref="ValueWrapper<T> " /> with a value-type value.
    /// </summary>
    /// <param name="value"></param>
    public ValueWrapper(T value) {
        Value = value;
    }

    public static implicit operator T(ValueWrapper<T> wrapper) {
        if (wrapper is null)
            return default;

        return wrapper.Value;
    }

    public static implicit operator ValueWrapper<T>(T value) {
        return new ValueWrapper<T>(value);
    }
}
