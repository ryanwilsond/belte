
namespace Belte.Runtime;

public static class Console {
    public static long GetWidth() {
        return System.Console.WindowWidth;
    }

    public static long GetHeight() {
        return System.Console.WindowHeight;
    }

    public static void SetForegroundColor(long color) {
        System.Console.ForegroundColor = (System.ConsoleColor)color;
    }

    public static void SetBackgroundColor(long color) {
        System.Console.BackgroundColor = (System.ConsoleColor)color;
    }

    public static void SetCursorPosition(long? x, long? y) {
        System.Console.SetCursorPosition((int?)x ?? System.Console.CursorLeft, (int?)y ?? System.Console.CursorTop);
    }

    public static void SetCursorVisibility(bool visible) {
        System.Console.CursorVisible = visible;
    }
}
