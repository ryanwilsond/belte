using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class TemplateExpander {
    private sealed class TemplateInstantiation {
        internal TemplateInstantiation(
            ISynthesizedTemplate template,
            Symbol cause,
            TemplateInstantiation parent,
            TextLocation location) {
            this.template = template;
            this.cause = cause;
            this.parent = parent;
            this.location = location;
        }

        internal ISynthesizedTemplate template { get; }

        internal Symbol cause { get; }

        internal TemplateInstantiation parent { get; }

        internal TextLocation location { get; }
    }
}
