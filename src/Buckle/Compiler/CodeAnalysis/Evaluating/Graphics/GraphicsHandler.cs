using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Text;
using MonoGame.Extended.Text.Extensions;
using Shared;

namespace Buckle.CodeAnalysis.Evaluating;

internal partial class GraphicsHandler : Game {
    internal static string Title;
    internal static int Width;
    internal static int Height;

    private readonly GraphicsDeviceManager _graphics;
    private readonly FontManager _fontManager;
    private readonly ValueWrapper<bool> _abort;
    private readonly ConcurrentDictionary<int, Action> _updateActions = [];
    private readonly bool _usePointClamp;

    private UpdateHandler _updateHandler;
    private SpriteBatch _spriteBatch;
    private int _actionCount;

    internal bool shouldRun;

    internal GraphicsHandler(ValueWrapper<bool> abort, bool usePointClamp) {
        _graphics = new GraphicsDeviceManager(this);
        _fontManager = new FontManager();
        _abort = abort;
        _usePointClamp = usePointClamp;
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / 60d);
    }

    internal delegate void UpdateHandler(double deltaTicks, ValueWrapper<bool> abort);

    internal void SetUpdateHandler(UpdateHandler updateHandler) {
        _updateHandler = updateHandler;
    }

    internal Texture2D LoadTexture(string path, bool useColorKey, object r, object g, object b) {
        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(GraphicsDevice, stream);

        if (!useColorKey)
            return texture;

        var data = new Color[texture.Width * texture.Height];
        texture.GetData(data);

        var colorKey = GetColor(r, g, b);

        for (var i = 0; i < data.Length; i++) {
            if (data[i] == colorKey)
                data[i] = Color.Transparent;
        }

        var textureWithTransparency = new Texture2D(GraphicsDevice, texture.Width, texture.Height);
        textureWithTransparency.SetData(data);

        return textureWithTransparency;
    }

    internal DynamicSpriteFont LoadText(string path, float fontSize) {
        var font = _fontManager.LoadFont(path, fontSize);
        return new DynamicSpriteFont(GraphicsDevice, font);
    }

    internal int AddAction(Action action) {
        var key = _actionCount++;
        _updateActions.TryAdd(key, action);
        return key;
    }

    internal void RemoveAction(int key) {
        _updateActions.TryRemove(key, out _);
    }

    internal bool GetKey(string keyText) {
        if (Enum.TryParse(typeof(Keys), keyText, true, out var key))
            return KeyboardManager.IsKeyDown((Keys)key);

        return false;
    }

    internal void DrawSprite(
        Texture2D texture,
        int sx, int sy, int sw, int sh,
        int dx, int dy, int dw, int dh,
        object rotation) {
        var sourceRectangle = new Rectangle(sx, sy, sw, sh);
        var destinationRectangle = new Rectangle(dx, dy, dw, dh);
        var rotF = rotation is null ? 0 : Convert.ToSingle(rotation);

        _spriteBatch.Draw(
            texture,
            destinationRectangle,
            sourceRectangle,
            Color.White,
            rotF,
            Vector2.Zero,
            SpriteEffects.None,
            0f
        );
    }

    internal void Draw(Texture2D texture, EvaluatorObject src, EvaluatorObject dst, object rotation, object flip) {
        Rectangle? srcRect = null;

        if (src.members is not null) {
            var (sx, sy, sw, sh) = Evaluator.ExtractRectangleComponents(src);
            srcRect = new Rectangle(sx, sy, sw, sh);
        }

        var (dx, dy, dw, dh) = Evaluator.ExtractRectangleComponents(dst);
        var dstRect = new Rectangle(dx, dy, dw, dh);

        var rotF = rotation is null ? 0 : Convert.ToSingle(rotation);
        var flipB = flip is not null && Convert.ToBoolean(flip);

        _spriteBatch.Draw(
            texture,
            dstRect,
            srcRect,
            Color.White,
            rotF,
            Vector2.Zero,
            flipB ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f
        );
    }

    internal void DrawText(
        DynamicSpriteFont font,
        string text,
        object posX,
        object posY,
        object r,
        object g,
        object b) {
        var posXi = posX is null ? 0 : Convert.ToInt32(posX);
        var posYi = posY is null ? 0 : Convert.ToInt32(posY);

        var color = GetColor(r, g, b);
        var spacing = new Vector2(2, 2);
        var size = font.MeasureString(text, Vector2.Zero, Vector2.One, spacing);
        var location = new Vector2(posXi - size.X / 2, posYi - size.Y / 2);

        _spriteBatch.DrawString(font, text, location, spacing, color);
    }

    internal void DrawRect(int x, int y, int w, int h, object r, object g, object b) {
        var color = GetColor(r, g, b);
        var rectangle = new Rectangle(x, y, w, h);
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([color]);
        _spriteBatch.Draw(pixel, rectangle, color);
    }

    internal void Fill(object r, object g, object b) {
        var color = GetColor(r, g, b);
        GraphicsDevice.Clear(color);
    }

    private Color GetColor(object r, object g, object b) {
        var ri = r is null ? 255 : Convert.ToInt32(r);
        var gi = g is null ? 255 : Convert.ToInt32(g);
        var bi = b is null ? 255 : Convert.ToInt32(b);
        return new Color(ri, gi, bi);
    }

    protected override void Initialize() {
        _graphics.IsFullScreen = false;
        _graphics.PreferredBackBufferWidth = Width;
        _graphics.PreferredBackBufferHeight = Height;
        _graphics.ApplyChanges();

        base.Initialize();

        Window.Title = Title;
        SetWindowIcon(Window);
    }

    protected override void LoadContent() {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void UnloadContent() {
        _spriteBatch.Dispose();
        _fontManager.Dispose();
    }

    protected override void Update(GameTime gameTime) {
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        KeyboardManager.Update();

        _spriteBatch.Begin(samplerState: _usePointClamp ? SamplerState.PointClamp : null);

        if (_updateHandler is not null)
            _updateHandler(gameTime.ElapsedGameTime.Ticks / 10000000.0, _abort);

        foreach (var action in _updateActions.Values)
            action.Invoke();

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
