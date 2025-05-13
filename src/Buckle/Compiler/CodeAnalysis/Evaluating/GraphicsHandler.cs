using System;
using System.Collections.Generic;
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
    private readonly ValueWrapper<bool> _abort;
    private readonly Dictionary<int, Action> _updateActions = [];

    private UpdateHandler _updateHandler;
    private SpriteBatch _spriteBatch;
    private int _actionCount;

    internal bool shouldRun;

    internal GraphicsHandler(ValueWrapper<bool> abort) {
        _graphics = new GraphicsDeviceManager(this);
        _abort = abort;
        IsMouseVisible = true;
    }

    internal delegate void UpdateHandler(double deltaTicks, ValueWrapper<bool> abort);

    internal void SetUpdateHandler(UpdateHandler updateHandler) {
        _updateHandler = updateHandler;
    }

    internal Texture2D LoadSprite(string path) {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    internal int AddAction(Action action) {
        var key = _actionCount++;
        _updateActions.Add(key, action);
        return key;
    }

    internal void RemoveAction(int key) {
        _updateActions.Remove(key);
    }

    internal void DrawSprite(Texture2D texture, float posX, float posY, float scaleX, float scaleY, int rotation) {
        // TODO Could optimize these casts probably if this becomes too slow
        var width = scaleX;
        var height = scaleY;
        var origin = new Vector2(width / 2f, height / 2f);
        var destinationRectangle = new Rectangle(
            (int)(posX - width / 2f),
            (int)(posY - height / 2f),
            (int)width,
            (int)height
        );

        _spriteBatch.Draw(
            texture,
            destinationRectangle,
            null,
            Color.White,
            rotation,
            origin,
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

        if (_updateHandler is not null)
            _updateHandler(gameTime.ElapsedGameTime.Ticks / 10000000.0, _abort);

        foreach (var action in _updateActions.Values)
            action.Invoke();

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
