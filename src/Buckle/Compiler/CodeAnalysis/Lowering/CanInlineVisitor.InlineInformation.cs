using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class CanInlineVisitor {
    internal struct InlineInformation {
        internal bool methodIsInlineable;
        internal bool accessesPrivateMembers;
        internal Symbol firstPrivateMember;
        internal BoundBlockStatement body;

        internal InlineInformation(
            bool methodIsInlineable,
            bool accessesPrivateMembers,
            Symbol firstPrivateMember,
            BoundBlockStatement body) {
            this.methodIsInlineable = methodIsInlineable;
            this.accessesPrivateMembers = accessesPrivateMembers;
            this.firstPrivateMember = firstPrivateMember;
            this.body = body;
        }
    }
}
