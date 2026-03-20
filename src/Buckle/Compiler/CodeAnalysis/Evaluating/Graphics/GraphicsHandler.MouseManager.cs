using Microsoft.Xna.Framework.Input;

namespace Buckle.CodeAnalysis.Evaluating;

internal partial class GraphicsHandler {
    private static class MouseManager {
        private static MouseState MouseState;

        internal static void Update() {
            MouseState = Mouse.GetState();
        }

        internal static bool IsMouseButtonPressed(string button) {
            return button switch {
                "left" => MouseState.LeftButton == ButtonState.Pressed,
                "right" => MouseState.RightButton == ButtonState.Pressed,
                _ => false,
            };
        }

        internal static int GetScroll() {
            return MouseState.ScrollWheelValue;
        }

        internal static (int, int) GetPosition() {
            return (MouseState.Position.X, MouseState.Position.Y);
        }
    }
}
