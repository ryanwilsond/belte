using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shared;

namespace Buckle.CodeAnalysis.Evaluating;

internal partial class GraphicsHandler : Game {
    internal static string Title;
    internal static int Width;
    internal static int Height;

    private readonly GraphicsDeviceManager _graphics;
    private readonly UpdateHandler _updateHandler;
    private readonly ValueWrapper<bool> _abort;

    private SpriteBatch _spriteBatch;

    internal bool shouldRun;

    internal GraphicsHandler(UpdateHandler updateHandler, ValueWrapper<bool> abort) {
        _graphics = new GraphicsDeviceManager(this);
        _updateHandler = updateHandler;
        _abort = abort;
        IsMouseVisible = true;
    }

    internal delegate void UpdateHandler(double deltaTicks, ValueWrapper<bool> abort);

    internal Texture2D LoadSprite(string path) {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    internal void DrawSprite(Texture2D texture, float posX, float posY, float scaleX, float scaleY, int rotation) {
        var origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

        _spriteBatch.Draw(
            texture,
            new Vector2(posX, posY),
            null,
            Color.White,
            rotation,
            origin,
            new Vector2(scaleX, scaleY),
            SpriteEffects.None,
            0f
        );
    }

    protected override void Initialize() {
        Window.Title = Title;
        _graphics.IsFullScreen = false;
        _graphics.PreferredBackBufferWidth = Width;
        _graphics.PreferredBackBufferHeight = Height;
        _graphics.ApplyChanges();

        base.Initialize();
    }

    protected override void LoadContent() {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime) {
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin();
        _updateHandler(gameTime.ElapsedGameTime.Ticks / 10000000.0, _abort);
        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
