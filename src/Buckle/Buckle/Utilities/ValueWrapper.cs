
namespace Buckle.Utilities;

/// <summary>
/// Wraps a value-type object so it can be treated as a reference-type object.
/// </summary>
/// <typeparam name="T">Value type to wrap.</typeparam>
public class ValueWrapper<T> {
    /// <summary>
    /// The value-type object.
    /// </summary>
    internal T Value { get; set; }

    /// <summary>
    /// Creates an empty <see cref="ValueWrapper<T>" />.
    /// </summary>
    internal ValueWrapper() { }

    /// <summary>
    /// Creates a new <see cref="ValueWrapper<T> " /> with a value-type value.
    /// </summary>
    /// <param name="value"></param>
    internal ValueWrapper(T value) {
        Value = value;
    }

    public static implicit operator T(ValueWrapper<T> wrapper) {
        if (wrapper == null)
            return default(T);

        return wrapper.Value;
    }

    public static implicit operator ValueWrapper<T>(T value) {
        return new ValueWrapper<T>(value);
    }
}
