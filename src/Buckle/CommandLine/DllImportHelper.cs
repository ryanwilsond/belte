using System;
using System.IO;

public static class DllImportHelper {
    public static void ExtractAndLoadDlls() {
        ExtractToBaseDirectory("Resources.Belte.Runtime.dll", "Belte.Runtime.dll");
        ExtractToBaseDirectory("Resources.Belte.Graphics.dll", "Belte.Graphics.dll");
        ExtractToBaseDirectory("Resources.freetype6.dll", "freetype6.dll");
        ExtractToBaseDirectory("Resources.openal.dll", "openal.dll");
        ExtractToBaseDirectory("Resources.SDL2.dll", "SDL2.dll");
    }

    private static void ExtractToBaseDirectory(string resourceName, string fileName) {
        var asm = typeof(Program).Assembly;

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new Exception($"Missing resource: {resourceName}");

        var outputPath = Path.Combine(AppContext.BaseDirectory, fileName);

        if (File.Exists(outputPath))
            return;

        using var file = File.Open(outputPath, FileMode.Create, FileAccess.Write);
        stream.CopyTo(file);
    }
}
