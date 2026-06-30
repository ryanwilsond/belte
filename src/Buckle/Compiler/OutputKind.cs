
namespace Buckle;

/// <summary>
/// The type of project the Belte source is.
/// </summary>
public enum OutputKind : byte {
    /// Ordinary application with an entry point
    ConsoleApplication,
    /// Application that uses the native graphics library
    GraphicsApplication,
    /// Library with no entry point
    DynamicallyLinkedLibrary,
}
