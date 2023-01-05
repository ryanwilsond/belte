using System.IO;
using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound node, gets created from a <see cref="SyntaxNode" />.
/// </summary>
internal abstract class BoundNode {
    internal abstract BoundNodeKind kind { get; }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            this.WriteTo(writer);
            return writer.ToString();
        }
    }
}
