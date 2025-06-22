using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

internal static class FlowAnalysisPass {
    internal static BoundBlockStatement AppendImplicitReturn(BoundBlockStatement body) {
        var syntax = body.syntax;

        BoundStatement ret = new BoundReturnStatement(syntax, RefKind.None, null);

        return body.Update(
            body.statements.Add(ret),
            body.locals,
            body.localFunctions
        );
    }
}
