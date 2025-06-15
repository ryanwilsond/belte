using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Xna.Framework;

namespace Buckle.CodeAnalysis.Evaluating;

internal partial class GraphicsHandler {
    [SupportedOSPlatform("windows")]
    private static void SetWindowIcon(GameWindow window) {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Compiler.BelteCapital.png");

        using var bmp = new System.Drawing.Bitmap(stream);

        var data = bmp.LockBits(
            new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb
        );

        var surface = SDL_CreateRGBSurfaceFrom(
            data.Scan0,
            bmp.Width,
            bmp.Height,
            32,
            data.Stride,
            0x00FF0000,
            0x0000FF00,
            0x000000FF,
            0xFF000000
        );

        SDL_SetWindowIcon(window.Handle, surface);
        bmp.UnlockBits(data);
    }

#if _WINDOWS
    [DllImport("SDL2.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowIcon(IntPtr window, IntPtr surface);

    [DllImport("SDL2.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private static extern IntPtr SDL_CreateRGBSurfaceFrom(
        IntPtr pixels, int width, int height, int depth, int pitch,
        uint Rmask, uint Gmask, uint Bmask, uint Amask);
#endif
}
