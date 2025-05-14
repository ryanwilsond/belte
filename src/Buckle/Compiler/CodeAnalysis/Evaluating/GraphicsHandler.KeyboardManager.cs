using Microsoft.Xna.Framework.Input;

namespace Buckle.CodeAnalysis.Evaluating;

internal partial class GraphicsHandler {
    private static class KeyboardManager {
        private static KeyboardState KeyboardState;

        internal static void Update() {
            KeyboardState = Keyboard.GetState();
        }

        internal static bool IsKeyDown(Keys key) {
            return KeyboardState.IsKeyDown(key);
        }
    }
}
