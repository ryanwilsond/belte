
namespace Buckle.CodeAnalysis.Evaluating;

public sealed class BVec2 {
    public double? x;
    public double? y;

    public BVec2(double? x, double? y) {
        this.x = x;
        this.y = y;
    }

    public BVec2 Copy() {
        return new BVec2(x, y);
    }
}
