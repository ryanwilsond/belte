# 9.1 Graphics

The Graphics class provides static helpers for loading and using graphics resources.

The Belte public interface for the Graphics class can be found
[on the Belte GitHub repository](https://github.com/ryanwilsond/belte/blob/main/src/Belte/Native/Graphics/Graphics.blt).

- [9.1.1](#911-methods) Methods

## 9.1.1 Methods

| Signature | Description |
|-|-|
| `void Initialize(string!, int!, int!, bool!)` | Creates a window with a title, dimensions, and flag determining if point clamp is enabled or not. |
| `void LockFramerate(int!)` | Locks `Update` framerate to an fps. |
| `Texture LoadTexture(string!)` | Loads a texture from an image path. |
| `Texture LoadTexture(string!, int!, int!, int!)` | Loads a texture from an image path with an RGB mask. |
| `Sprite LoadSprite(string!, Vec2, Vec2, int?)` | Loads a sprite from an image path with a screen position, scale, and rotation. |
| `int? Draw(Texture, Rect, Rect, int?, bool?, decimal?)` | Draws a texture to the screen with a given src rect, dst rect, rotation, whether or not to flip, and alpha. |
| `int? DrawSprite(Sprite)` | Draws a sprite to the screen. |
| `int? DrawSprite(Sprite, Vec2)` | Draws a sprite to the screen with an offset from its position. |
| `Text LoadText(string, string!, Vec2, decimal!, decimal?, int?, int?, int?)` | Loads a text object with a text string, font path, screen position, font size, angle, and RGB color. |
| `int? DrawText(Text)` | Draws a text object to the screen. |
| `int? DrawRect(Rect, int?, int?, int?)` | Draws a rectangle with an RGB color to the screen. |
| `int? DrawRect(Rect, int?, int?, int?, int?)` | Draws a rectangle with an RGBA color to the screen. |
| `void StopDraw(int?)` | Stops drawing a specific object using the ID returned from the draw call to stop (REPL only). |
| `bool! GetKey(string!)` | Checks if a given key is being pressed. |
| `void Fill(int!, int!, int!)` | Fills the screen with an RGB color. |
| `bool! GetMouseButton(string!)` | Checks if a mouse button is being pressed. |
| `Vec2! GetMousePosition()` | Gets the mouse position relative to the window. |
| `int! GetScroll()` | Gets the mouse scroll wheel delta. |
| `Sound LoadSound(string!)` | Loads a sound file. |
| `void PlaySound(Sound!)` | Plays a sound. |
| `void SetCursorVisibility(bool!)` | Sets the mouse cursors visibility. |
