using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries.Graphics;

internal static partial class GraphicsLibrary {
    /// <summary>
    /// Gets all the pre-compiled symbols defined by the library.
    /// </summary>
    internal static Symbol[] GetSymbols() {
        return [Graphics, Physics, Vec2, Text, Sprite];
    }

    /// <summary>
    /// Method used to evaluate Graphics Library methods with no native implementation.
    /// </summary>
    internal static object EvaluateMethod(MethodSymbol method, object[] arguments) {
        if (method == Vec2.constructors[0]) {

        } else if (method == Physics.members[0]) {

        }

        return null;
    }
}
