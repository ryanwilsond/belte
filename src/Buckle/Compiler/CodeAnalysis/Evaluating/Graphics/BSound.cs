using Microsoft.Xna.Framework.Audio;

namespace Buckle.CodeAnalysis.Evaluating;

public sealed class BSound {
    public double? volume;
    public bool? loop;

    internal SoundEffect mSound;

    internal BSound(double? volume, bool? loop, SoundEffect mSound) {
        this.volume = volume;
        this.loop = loop;
        this.mSound = mSound;
    }
}
