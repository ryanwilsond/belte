using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Lowering {
    internal sealed class Lowerer : BoundTreeRewriter {
        private Lowerer() {}

        public static BoundStatement Lower(BoundStatement statement) {
            var lowerer = new Lowerer();
            return lowerer.RewriteStatement(statement);
        }
    }
}
