using System.IO;
using Buckle.IO;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound node, gets created from a <see cref="Node" />.
/// </summary>
internal abstract class BoundNode {
    internal abstract BoundNodeType type { get; }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);
            return writer.ToString();
        }
    }
}
