using System;
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

    // The Executor expects all native definitions to contain a corresponding implementation, so this is here to
    // satisfy that expectation even though all of the graphics calls actually use the above constructor
    public BTexture(IntPtr _, long _1, long _2) {
        throw new InvalidOperationException();
    }
}
