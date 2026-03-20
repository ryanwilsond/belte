using MonoGame.Extended.Text;

namespace Buckle.CodeAnalysis.Evaluating;

public sealed class BText {
    public string text;
    public string fontPath;
    public BVec2 position;
    public double fontSize;
    public double? angle;
    public long? r;
    public long? g;
    public long? b;

    internal DynamicSpriteFont mFont;

    internal BText(
        string text,
        string fontPath,
        BVec2 position,
        double fontSize,
        double? angle,
        long? r,
        long? g,
        long? b,
        DynamicSpriteFont mFont) {
        this.text = text;
        this.fontPath = fontPath;
        this.position = position;
        this.fontSize = fontSize;
        this.angle = angle;
        this.r = r;
        this.g = g;
        this.b = b;
        this.mFont = mFont;
    }
}
