
using Microsoft.Xna.Framework.Graphics;

namespace Buckle.CodeAnalysis.Evaluating;

public sealed class BTexture {
    public long width;
    public long height;

    internal Texture2D mTexture;

    internal BTexture(Texture2D mTexture) {
        this.mTexture = mTexture;
        width = mTexture.Width;
        height = mTexture.Height;
    }
}
