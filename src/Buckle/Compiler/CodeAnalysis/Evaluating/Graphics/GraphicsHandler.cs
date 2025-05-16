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

    private UpdateHandler _updateHandler;
    private SpriteBatch _spriteBatch;
    private int _actionCount;

    internal bool shouldRun;

    internal GraphicsHandler(ValueWrapper<bool> abort) {
        _graphics = new GraphicsDeviceManager(this);
        _fontManager = new FontManager();
        _abort = abort;
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / 60d);
    }

    internal delegate void UpdateHandler(double deltaTicks, ValueWrapper<bool> abort);

    internal void SetUpdateHandler(UpdateHandler updateHandler) {
        _updateHandler = updateHandler;
    }

    internal Texture2D LoadSprite(string path) {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
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
        object posX,
        object posY,
        object scaleX,
        object scaleY,
        object rotation) {
        var posXi = posX is null ? 0 : Convert.ToInt32(posX);
        var posYi = posY is null ? 0 : Convert.ToInt32(posY);
        var scaleXi = scaleX is null ? texture.Width : Convert.ToInt32(scaleX);
        var scaleXy = scaleY is null ? texture.Height : Convert.ToInt32(scaleY);
        var rotF = rotation is null ? 0 : Convert.ToSingle(rotation);

        var destinationRectangle = new Rectangle(
            posXi - scaleXi / 2,
            posYi - scaleXy / 2,
            scaleXi,
            scaleXy
        );

        _spriteBatch.Draw(
            texture,
            destinationRectangle,
            null,
            Color.White,
            rotF,
            Vector2.Zero,
            SpriteEffects.None,
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
        var ri = r is null ? 255 : Convert.ToInt32(r);
        var gi = g is null ? 255 : Convert.ToInt32(g);
        var bi = b is null ? 255 : Convert.ToInt32(b);
        var posXi = posX is null ? 0 : Convert.ToInt32(posX);
        var posYi = posY is null ? 0 : Convert.ToInt32(posY);

        var color = new Color(ri, gi, bi);
        var spacing = new Vector2(2, 2);
        var size = font.MeasureString(text, Vector2.Zero, Vector2.One, spacing);
        var location = new Vector2(posXi - size.X / 2, posYi - size.Y / 2);

        _spriteBatch.DrawString(font, text, location, spacing, color);
    }

    internal void DrawRect(int x, int y, int w, int h, int r, int g, int b) {
        var color = new Color(r, g, b);
        var rectangle = new Rectangle(x, y, w, h);
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([color]);
        _spriteBatch.Draw(pixel, rectangle, color);
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
        GraphicsDevice.Clear(Color.Black);

        KeyboardManager.Update();

        _spriteBatch.Begin();

        if (_updateHandler is not null)
            _updateHandler(gameTime.ElapsedGameTime.Ticks / 10000000.0, _abort);

        foreach (var action in _updateActions.Values)
            action.Invoke();

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
