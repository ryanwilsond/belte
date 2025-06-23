
namespace Buckle.CodeAnalysis.Evaluating;

public sealed class BSprite {
    public BVec2 position;
    public long? rotation;
    public BRect src;
    public BRect dst;
    public BTexture texture;

    public BSprite(BTexture texture, BVec2 position, BVec2 scale, long? rotation) {
        this.position = position;
        this.rotation = rotation;
        this.texture = texture;

        var width = scale?.x ?? texture.width;
        var height = scale?.y ?? texture.height;

        src = new BRect(0, 0, texture.width, texture.height);
        dst = new BRect((long)(position.x - width / 2), (long)(position.y - height / 2), (long)width, (long)height);
    }

    public BSprite(BTexture texture, BRect src, BRect dst, long? rotation) {
        this.src = src ?? new BRect(0, 0, texture.width, texture.height);
        this.dst = dst;
        this.rotation = rotation;
        this.texture = texture;

        position = new BVec2(dst.x + dst.w / 2, dst.y + dst.h / 2);
    }

    public void SetPosition(long? x, long? y) {
        dst.x = x.Value - dst.w / 2;
        dst.y = y.Value - dst.h / 2;
        position.x = x;
        position.y = y;
    }

    public void SetPosition(double? x, double? y) {
        dst.x = (long)(x - dst.w / 2);
        dst.y = (long)(y - dst.h / 2);
        position.x = x;
        position.y = y;
    }

    public BVec2 GetPosition() {
        position.x = dst.x + dst.w / 2;
        position.y = dst.y + dst.h / 2;
        return position;
    }
}
