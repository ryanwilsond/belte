using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal static class InitializerRewriter {
    internal static BoundBlockStatement RewriteConstructor(
        ImmutableArray<BoundInitializer> boundInitializers,
        MethodSymbol method) {
        var builder = ArrayBuilder<BoundStatement>.GetInstance();

        foreach (var initializer in boundInitializers)
            builder.Add(RewriteInitializer(initializer));

        var syntax = (method is SourceMemberMethodSymbol sourceMethod)
            ? sourceMethod.syntaxNode
            : method.GetNonNullSyntaxNode();

        return new BoundBlockStatement(syntax, builder.ToImmutableAndFree(), [], []);

        BoundStatement RewriteInitializer(BoundInitializer initializer) {
            return initializer.kind switch {
                BoundKind.FieldEqualsValue => RewriteFieldInitializer(initializer as BoundFieldEqualsValue),
                _ => throw ExceptionUtilities.UnexpectedValue(initializer.kind),
            };
        }
    }

    private static BoundExpressionStatement RewriteFieldInitializer(BoundFieldEqualsValue fieldInitializer) {
        var field = fieldInitializer.field;
        var syntax = fieldInitializer.syntax;
        var boundReceiver = field.isStatic ? null : new BoundThisExpression(syntax, field.containingType);

        var boundStatement = new BoundExpressionStatement(
            syntax,
            new BoundAssignmentOperator(
                syntax,
                new BoundFieldAccessExpression(
                    syntax,
                    boundReceiver,
                    field,
                    null,
                    field.type
                ),
                fieldInitializer.value,
                field.refKind != RefKind.None,
                field.type
            )
        );

        return boundStatement;
    }
}
