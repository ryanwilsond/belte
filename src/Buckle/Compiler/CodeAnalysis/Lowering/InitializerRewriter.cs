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

    internal static BoundBlockStatement RewriteOutParameters(MethodSymbol method) {
        ArrayBuilder<BoundStatement> builder = null;

        foreach (var parameter in method.parameters) {
            if (parameter.hasOutDefaultValue) {
                builder ??= ArrayBuilder<BoundStatement>.GetInstance();
                builder.Add(RewriteOutParameterInitializer(parameter));
            }
        }

        if (builder is null)
            return null;

        var syntax = (method is SourceMemberMethodSymbol sourceMethod)
            ? sourceMethod.syntaxNode
            : method.GetNonNullSyntaxNode();

        return new BoundBlockStatement(syntax, builder.ToImmutableAndFree(), [], []);
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

    private static BoundExpressionStatement RewriteOutParameterInitializer(ParameterSymbol parameter) {
        var syntax = parameter.GetNonNullSyntaxNode();
        var type = parameter.type;

        var boundStatement = new BoundExpressionStatement(
            syntax,
            new BoundAssignmentOperator(
                syntax,
                new BoundParameterExpression(
                    syntax,
                    parameter,
                    null,
                    type
                ),
                new BoundLiteralExpression(
                    syntax,
                    parameter.outDefaultValue,
                    type
                ),
                false,
                type
            )
        );

        return boundStatement;
    }
}
