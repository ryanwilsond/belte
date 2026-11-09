using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class PostLoweringOptimizationPass {
    internal static BoundBlockStatement Optimize(MethodSymbol method, BoundBlockStatement body) {
        /*

        Goal is to remove redundant assignments. Currently this targets field initializers that assign a field it's
        default value.

        Therefore we need to make 100% sure that the field is never read from before the assignment:
            - If the constructor has a `: this()` initializer, we abort

        This pass needs to happen post-lowering because the field assignments are used by CFG analysis.

        */
        if (!method.IsConstructor() || body.statements.Length <= 2)
            return body;

        if (method.HasThisConstructorInitializer())
            return body;

        ArrayBuilder<BoundStatement> builder = null;

        var i = 0;
        for (; i < body.statements.Length; i++) {
            var statement = body.statements[i];

            if (statement is not BoundExpressionStatement exprStmt)
                break;

            if (exprStmt.expression is not BoundAssignmentOperator assignment || assignment.isRef)
                break;

            FieldSymbol field;

            if (assignment.left is BoundFieldAccessExpression fieldAccess)
                field = fieldAccess.field;
            else if (assignment.left is BoundFieldSlotExpression fieldSlot)
                field = fieldSlot.field;
            else
                break;

            if (!field.type.HasDefaultValue()) {
                builder?.Add(statement);
                continue;
            }

            if (assignment.right.constantValue is not null &&
                assignment.right.constantValue.isDefaultValue) {
                if (builder is null) {
                    builder = ArrayBuilder<BoundStatement>.GetInstance();

                    for (var j = 0; j < i; j++)
                        builder.Add(body.statements[j]);
                }

                continue;
            }

            builder?.Add(statement);
        }

        if (builder is null)
            return body;

        for (; i < body.statements.Length; i++)
            builder.Add(body.statements[i]);

        return new BoundBlockStatement(
            body.syntax,
            builder.ToImmutableAndFree(),
            body.locals,
            body.localFunctions
        );
    }
}
